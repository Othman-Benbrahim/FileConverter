// <copyright file="ConversionJob_Ghostscript.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    public class ConversionJob_Ghostscript : ConversionJob
    {
        private const int MinimumExtractableCharacters = 20;
        private const string OcrLanguages = "fra+eng";

        private readonly object processLock = new object();
        private Process ghostscriptProcess;
        private string applicationDirectory;
        private string ghostscriptPath;
        private string tessdataPath;

        public ConversionJob_Ghostscript() : base()
        {
        }

        public ConversionJob_Ghostscript(ConversionPreset conversionPreset, string inputFilePath) : base(conversionPreset, inputFilePath)
        {
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

            if (this.ConversionPreset.OutputType != OutputType.Docx && this.ConversionPreset.OutputType != OutputType.Txt)
            {
                throw new NotSupportedException($"Unsupported Ghostscript output format '{this.ConversionPreset.OutputType}'.");
            }

            this.applicationDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            this.ghostscriptPath = Path.Combine(this.applicationDirectory, "gswin64c.exe");
            this.tessdataPath = Path.Combine(this.applicationDirectory, "tessdata");

            if (!File.Exists(this.ghostscriptPath))
            {
                this.ConversionFailed(Properties.Resources.ErrorCantFindGhostscript);
                Diagnostics.Debug.Log($"Can't find Ghostscript executable ({this.ghostscriptPath}). Try to reinstall the application.");
            }
        }

        protected override void Convert()
        {
            if (string.IsNullOrEmpty(this.ghostscriptPath))
            {
                this.ConversionFailed(Properties.Resources.ErrorFailedToLaunchGhostscript);
                return;
            }

            string identifier = Guid.NewGuid().ToString("N");
            string textProbePath = Path.Combine(Path.GetTempPath(), $"FileConverter-{identifier}-probe.txt");
            string searchablePdfPath = Path.Combine(Path.GetTempPath(), $"FileConverter-{identifier}-ocr.pdf");
            bool ocrWasUsed = false;

            try
            {
                this.UserState = Properties.Resources.ConversionStateDetectTextLayer;
                this.Progress = 0.05f;

                if (!this.RunGhostscript("txtwrite", this.InputFilePath, textProbePath, "-dTextFormat=3", false))
                {
                    return;
                }

                bool hasExtractableText = this.HasMeaningfulText(textProbePath);
                Diagnostics.Debug.Log(hasExtractableText ? "PDF text layer detected." : "No usable PDF text layer detected. Local OCR will be used.");

                if (hasExtractableText)
                {
                    this.UserState = Properties.Resources.ConversionStateConversion;
                    this.Progress = 0.5f;

                    if (this.ConversionPreset.OutputType == OutputType.Txt)
                    {
                        File.Copy(textProbePath, this.OutputFilePath);
                    }
                    else if (!this.RunGhostscript("docxwrite", this.InputFilePath, this.OutputFilePath, string.Empty, false))
                    {
                        return;
                    }
                }
                else
                {
                    ocrWasUsed = true;
                    if (!this.ValidateOcrLanguageData())
                    {
                        return;
                    }

                    this.UserState = Properties.Resources.ConversionStateOCR;
                    this.Progress = 0.2f;

                    if (this.ConversionPreset.OutputType == OutputType.Txt)
                    {
                        if (!this.RunGhostscript("ocr", this.InputFilePath, this.OutputFilePath, $"-r300 -sOCRLanguage={OcrLanguages}", true))
                        {
                            return;
                        }
                    }
                    else
                    {
                        if (!this.RunGhostscript("pdfocr24", this.InputFilePath, searchablePdfPath, $"-r300 -sOCRLanguage={OcrLanguages}", true))
                        {
                            return;
                        }

                        this.UserState = Properties.Resources.ConversionStateConversion;
                        this.Progress = 0.75f;
                        if (!this.RunGhostscript("docxwrite", searchablePdfPath, this.OutputFilePath, string.Empty, false))
                        {
                            return;
                        }
                    }
                }

                if (!File.Exists(this.OutputFilePath) || new FileInfo(this.OutputFilePath).Length == 0)
                {
                    this.ConversionFailed(Properties.Resources.ErrorGhostscriptEmptyOutput);
                    return;
                }

                if (this.ConversionPreset.OutputType == OutputType.Txt && !this.HasMeaningfulText(this.OutputFilePath))
                {
                    this.ConversionFailed(ocrWasUsed ? Properties.Resources.ErrorNoTextAfterOCR : Properties.Resources.ErrorNoExtractableText);
                }
            }
            finally
            {
                this.TryDeleteTemporaryFile(textProbePath);
                this.TryDeleteTemporaryFile(searchablePdfPath);
            }
        }

        private bool RunGhostscript(string device, string inputPath, string outputPath, string additionalArguments, bool isOcr)
        {
            this.TryDeleteTemporaryFile(outputPath);

            string arguments = $"-dSAFER -dBATCH -dNOPAUSE -dNOPROMPT -q -sDEVICE={device} {additionalArguments} -sOutputFile=\"{outputPath}\" -f \"{inputPath}\"";
            ProcessStartInfo processStartInfo = new ProcessStartInfo(this.ghostscriptPath)
            {
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = this.applicationDirectory
            };

            if (Directory.Exists(this.tessdataPath))
            {
                processStartInfo.EnvironmentVariables["TESSDATA_PREFIX"] = this.tessdataPath;
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

                    // Flush asynchronous output events after the process has exited.
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

        private bool ValidateOcrLanguageData()
        {
            string englishDataPath = Path.Combine(this.tessdataPath, "eng.traineddata");
            string frenchDataPath = Path.Combine(this.tessdataPath, "fra.traineddata");
            if (File.Exists(englishDataPath) && File.Exists(frenchDataPath))
            {
                return true;
            }

            this.ConversionFailed(string.Format(Properties.Resources.ErrorOCRLanguageDataMissing, this.tessdataPath));
            Diagnostics.Debug.Log($"OCR language data is missing from '{this.tessdataPath}'.");
            return false;
        }

        private bool HasMeaningfulText(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                return false;
            }

            string text = File.ReadAllText(path, Encoding.UTF8);
            int extractableCharacters = 0;
            for (int index = 0; index < text.Length; index++)
            {
                if (char.IsLetterOrDigit(text[index]))
                {
                    extractableCharacters++;
                    if (extractableCharacters >= MinimumExtractableCharacters)
                    {
                        return true;
                    }
                }
            }

            return false;
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

        private void TryDeleteTemporaryFile(string path)
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
