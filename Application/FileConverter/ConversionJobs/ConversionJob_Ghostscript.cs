// <copyright file="ConversionJob_Ghostscript.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    public class ConversionJob_Ghostscript : ConversionJob
    {
        private readonly object processLock = new object();
        private ProcessStartInfo ghostscriptProcessStartInfo;
        private Process ghostscriptProcess;

        public ConversionJob_Ghostscript() : base()
        {
        }

        public ConversionJob_Ghostscript(ConversionPreset conversionPreset, string inputFilePath) : base(conversionPreset, inputFilePath)
        {
        }

        protected virtual string GhostscriptPath
        {
            get
            {
                string applicationDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                return Path.Combine(applicationDirectory, "gswin64c.exe");
            }
        }

        public override void Cancel()
        {
            base.Cancel();
            this.TryKillGhostscriptProcess();
        }

        protected override void Initialize()
        {
            base.Initialize();

            if (this.ConversionPreset == null)
            {
                throw new Exception("The conversion preset must be valid.");
            }

            if (!string.Equals(Path.GetExtension(this.InputFilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Ghostscript document conversion only supports PDF input files.");
            }

            string device;
            string deviceArguments = string.Empty;
            switch (this.ConversionPreset.OutputType)
            {
                case OutputType.Docx:
                    device = "docxwrite";
                    break;

                case OutputType.Txt:
                    device = "txtwrite";
                    deviceArguments = "-dTextFormat=3 ";
                    break;

                default:
                    throw new NotSupportedException($"Unsupported Ghostscript output format '{this.ConversionPreset.OutputType}'.");
            }

            string ghostscriptPath = this.GhostscriptPath;
            if (!File.Exists(ghostscriptPath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindGhostscript);
                Diagnostics.Debug.Log($"Can't find Ghostscript executable ({ghostscriptPath}). Try to reinstall the application.");
                return;
            }

            this.ghostscriptProcessStartInfo = new ProcessStartInfo(ghostscriptPath)
            {
                Arguments = $"-dSAFER -dBATCH -dNOPAUSE -dNOPROMPT -q -sDEVICE={device} {deviceArguments}-sOutputFile=\"{this.OutputFilePath}\" -f \"{this.InputFilePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        protected override void Convert()
        {
            if (this.ghostscriptProcessStartInfo == null)
            {
                this.ConversionFailed(Properties.Resources.ErrorFailedToLaunchGhostscript);
                return;
            }

            this.UserState = Properties.Resources.ConversionStateConversion;
            this.Progress = 0f;

            StringBuilder processOutput = new StringBuilder();
            using (Process process = new Process())
            {
                process.StartInfo = this.ghostscriptProcessStartInfo;
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
                        return;
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    while (!process.WaitForExit(100))
                    {
                        if (this.CancelIsRequested)
                        {
                            this.TryKillGhostscriptProcess();
                            return;
                        }
                    }

                    // Flush asynchronous output events after the process has exited.
                    process.WaitForExit();

                    if (this.CancelIsRequested)
                    {
                        return;
                    }

                    if (process.ExitCode != 0)
                    {
                        string details = this.GetErrorDetails(processOutput);
                        this.ConversionFailed(string.Format(Properties.Resources.ErrorGhostscriptConversionFailed, process.ExitCode, details));
                        return;
                    }
                }
                catch (Exception exception)
                {
                    Diagnostics.Debug.Log(exception.ToString());
                    this.ConversionFailed(Properties.Resources.ErrorFailedToLaunchGhostscript);
                    return;
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

            if (!File.Exists(this.OutputFilePath) || new FileInfo(this.OutputFilePath).Length == 0)
            {
                this.ConversionFailed(Properties.Resources.ErrorGhostscriptEmptyOutput);
                return;
            }

            if (this.ConversionPreset.OutputType == OutputType.Txt && string.IsNullOrWhiteSpace(File.ReadAllText(this.OutputFilePath, Encoding.UTF8)))
            {
                this.ConversionFailed(Properties.Resources.ErrorNoExtractableText);
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

            const int maximumLength = 500;
            if (details.Length > maximumLength)
            {
                details = details.Substring(0, maximumLength) + "…";
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
