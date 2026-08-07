// <copyright file="ConversionJob_Ghostscript.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO;
    using System.Text;

    public class ConversionJob_Ghostscript : ConversionJob_GhostscriptBase
    {
        private const int MinimumExtractableCharacters = 20;
        private const string OcrLanguages = "fra+eng";

        public ConversionJob_Ghostscript()
            : base()
        {
        }

        public ConversionJob_Ghostscript(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (this.State == ConversionState.Failed)
            {
                return;
            }

            if (this.ConversionPreset == null)
            {
                throw new Exception("The conversion preset must be valid.");
            }

            if (!string.Equals(Path.GetExtension(this.InputFilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Ghostscript document conversion only supports PDF input files.");
            }

            if (this.ConversionPreset.OutputType != OutputType.Docx &&
                this.ConversionPreset.OutputType != OutputType.Md &&
                this.ConversionPreset.OutputType != OutputType.Txt)
            {
                throw new NotSupportedException($"Unsupported Ghostscript output format '{this.ConversionPreset.OutputType}'.");
            }
        }

        protected override void Convert()
        {
            if (string.IsNullOrEmpty(this.GhostscriptPath))
            {
                this.ConversionFailed(Properties.Resources.ErrorFailedToLaunchGhostscript);
                return;
            }

            string identifier = Guid.NewGuid().ToString("N");
            string textProbePath = Path.Combine(Path.GetTempPath(), $"FileConverter-{identifier}-probe.txt");
            string ocrTextPath = Path.Combine(Path.GetTempPath(), $"FileConverter-{identifier}-ocr.txt");
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
                    this.UserState = this.ConversionPreset.OutputType == OutputType.Md ? Properties.Resources.ConversionStateCreateMarkdown : Properties.Resources.ConversionStateConversion;
                    this.Progress = 0.5f;

                    if (this.ConversionPreset.OutputType == OutputType.Txt)
                    {
                        File.Copy(textProbePath, this.OutputFilePath);
                    }
                    else if (this.ConversionPreset.OutputType == OutputType.Md)
                    {
                        MarkdownTextConverter.WriteMarkdown(textProbePath, this.InputFilePath, this.OutputFilePath);
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
                    else if (this.ConversionPreset.OutputType == OutputType.Md)
                    {
                        if (!this.RunGhostscript("ocr", this.InputFilePath, ocrTextPath, $"-r300 -sOCRLanguage={OcrLanguages}", true))
                        {
                            return;
                        }

                        if (!this.HasMeaningfulText(ocrTextPath))
                        {
                            this.ConversionFailed(Properties.Resources.ErrorNoTextAfterOCR);
                            return;
                        }

                        this.UserState = Properties.Resources.ConversionStateCreateMarkdown;
                        this.Progress = 0.8f;
                        MarkdownTextConverter.WriteMarkdown(ocrTextPath, this.InputFilePath, this.OutputFilePath);
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
                this.TryDeleteTemporaryFile(ocrTextPath);
                this.TryDeleteTemporaryFile(searchablePdfPath);
            }
        }

        private bool ValidateOcrLanguageData()
        {
            string englishDataPath = Path.Combine(this.TessdataPath, "eng.traineddata");
            string frenchDataPath = Path.Combine(this.TessdataPath, "fra.traineddata");
            if (File.Exists(englishDataPath) && File.Exists(frenchDataPath))
            {
                return true;
            }

            this.ConversionFailed(string.Format(Properties.Resources.ErrorOCRLanguageDataMissing, this.TessdataPath));
            Diagnostics.Debug.Log($"OCR language data is missing from '{this.TessdataPath}'.");
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
    }
}
