// <copyright file="ConversionJob_GhostscriptBase.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    public abstract class ConversionJob_GhostscriptBase : ConversionJob
    {
        private readonly object processLock = new object();
        private Process ghostscriptProcess;

        protected ConversionJob_GhostscriptBase()
            : base()
        {
        }

        protected ConversionJob_GhostscriptBase(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        protected string ApplicationDirectory
        {
            get;
            private set;
        }

        protected string GhostscriptPath
        {
            get;
            private set;
        }

        protected string TessdataPath
        {
            get;
            private set;
        }

        public override void Cancel()
        {
            base.Cancel();
            this.TryKillGhostscriptProcess();
        }

        protected override void Initialize()
        {
            base.Initialize();

            this.ApplicationDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            this.GhostscriptPath = Path.Combine(this.ApplicationDirectory, "gswin64c.exe");
            this.TessdataPath = Path.Combine(this.ApplicationDirectory, "tessdata");

            if (!File.Exists(this.GhostscriptPath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindGhostscript);
                Diagnostics.Debug.Log($"Can't find Ghostscript executable ({this.GhostscriptPath}). Try to reinstall the application.");
            }
        }

        protected bool RunGhostscript(string device, string inputPath, string outputPath, string additionalArguments, bool isOcr)
        {
            return this.RunGhostscript(device, new[] { inputPath }, outputPath, additionalArguments, isOcr);
        }

        protected bool RunGhostscript(string device, IEnumerable<string> inputPaths, string outputPath, string additionalArguments, bool isOcr)
        {
            this.TryDeleteTemporaryFile(outputPath);

            StringBuilder arguments = new StringBuilder();
            arguments.Append("-dSAFER -dBATCH -dNOPAUSE -dNOPROMPT -q ");
            arguments.Append($"-sDEVICE={device} ");
            if (!string.IsNullOrWhiteSpace(additionalArguments))
            {
                arguments.Append(additionalArguments);
                arguments.Append(' ');
            }

            arguments.Append($"-sOutputFile=\"{outputPath}\" -f");
            foreach (string inputPath in inputPaths)
            {
                arguments.Append($" \"{inputPath}\"");
            }

            ProcessStartInfo processStartInfo = new ProcessStartInfo(this.GhostscriptPath)
            {
                Arguments = arguments.ToString(),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = this.ApplicationDirectory
            };

            if (Directory.Exists(this.TessdataPath))
            {
                processStartInfo.EnvironmentVariables["TESSDATA_PREFIX"] = this.TessdataPath;
            }

            StringBuilder processOutput = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = processStartInfo;
                process.OutputDataReceived += (sender, eventArgs) => this.RecordProcessOutput(processOutput, eventArgs.Data);
                process.ErrorDataReceived += (sender, eventArgs) => this.RecordProcessOutput(processOutput, eventArgs.Data);

                lock (this.processLock)
                {
                    this.ghostscriptProcess = process;
                }

                try
                {
                    if (!process.Start())
                    {
                        this.ConversionFailed(Properties.Resources.ErrorFailedToLaunchGhostscript);
                        return false;
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    while (!process.WaitForExit(100))
                    {
                        if (this.CancelIsRequested)
                        {
                            this.TryKillGhostscriptProcess();
                            return false;
                        }
                    }

                    process.WaitForExit();
                    if (this.CancelIsRequested)
                    {
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        string details = this.GetErrorDetails(processOutput);
                        string errorTemplate = isOcr ? Properties.Resources.ErrorOCRFailed : Properties.Resources.ErrorGhostscriptConversionFailed;
                        this.ConversionFailed(string.Format(errorTemplate, process.ExitCode, details));
                        return false;
                    }

                    return true;
                }
                catch (Exception exception)
                {
                    Diagnostics.Debug.Log(exception.ToString());
                    this.ConversionFailed(Properties.Resources.ErrorFailedToLaunchGhostscript);
                    return false;
                }
                finally
                {
                    lock (this.processLock)
                    {
                        if (ReferenceEquals(this.ghostscriptProcess, process))
                        {
                            this.ghostscriptProcess = null;
                        }
                    }
                }
            }
        }

        protected void TryDeleteTemporaryFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Debug.Log($"Can't delete temporary file '{path}': {exception.Message}");
            }
        }

        private void RecordProcessOutput(StringBuilder processOutput, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            lock (processOutput)
            {
                processOutput.AppendLine(line);
            }

            Diagnostics.Debug.Log($"Ghostscript: {line}");
        }

        private string GetErrorDetails(StringBuilder processOutput)
        {
            string details;
            lock (processOutput)
            {
                details = processOutput.ToString().Trim();
            }

            const int MaximumLength = 500;
            if (details.Length > MaximumLength)
            {
                details = details.Substring(0, MaximumLength) + "…";
            }

            return details;
        }

        private void TryKillGhostscriptProcess()
        {
            lock (this.processLock)
            {
                try
                {
                    if (this.ghostscriptProcess != null && !this.ghostscriptProcess.HasExited)
                    {
                        this.ghostscriptProcess.Kill();
                    }
                }
                catch (InvalidOperationException)
                {
                    // The process has already exited.
                }
                catch (System.ComponentModel.Win32Exception exception)
                {
                    Diagnostics.Debug.Log($"Can't stop Ghostscript process: {exception.Message}");
                }
            }
        }
    }
}
