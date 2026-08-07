// <copyright file="ConversionJob_PdfTools.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO;
    using System.Linq;

    using ImageMagick;

    public class ConversionJob_PdfMerge : ConversionJob_GhostscriptBase
    {
        private readonly string[] inputFilePaths;

        public ConversionJob_PdfMerge(ConversionPreset conversionPreset, string[] inputFilePaths)
            : base(conversionPreset, GetFirstInputPath(inputFilePaths))
        {
            this.inputFilePaths = inputFilePaths.ToArray();
        }

        protected override InputPostConversionAction InputPostConversionAction => InputPostConversionAction.None;

        protected override void Initialize()
        {
            base.Initialize();
            if (this.State == ConversionState.Failed)
            {
                return;
            }

            if (this.ConversionPreset.OutputType != OutputType.PdfMerge)
            {
                throw new NotSupportedException("Invalid output type for PDF merging.");
            }

            if (this.inputFilePaths.Length < 2)
            {
                this.ConversionFailed(Properties.Resources.ErrorPdfMergeNeedsMultipleFiles);
                return;
            }

            if (this.inputFilePaths.Any(path => !File.Exists(path) || !string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)))
            {
                this.ConversionFailed(Properties.Resources.ErrorPdfInputsOnly);
            }
        }

        protected override void Convert()
        {
            this.UserState = Properties.Resources.ConversionStateMergePdf;
            this.Progress = 0.1f;

            if (!this.RunGhostscript("pdfwrite", this.inputFilePaths, this.OutputFilePath, "-dCompatibilityLevel=1.7", false))
            {
                return;
            }

            if (!File.Exists(this.OutputFilePath) || new FileInfo(this.OutputFilePath).Length == 0)
            {
                this.ConversionFailed(Properties.Resources.ErrorGhostscriptEmptyOutput);
            }
        }

        private static string GetFirstInputPath(string[] inputFilePaths)
        {
            if (inputFilePaths == null || inputFilePaths.Length == 0)
            {
                throw new ArgumentException(Properties.Resources.ErrorPdfMergeNeedsMultipleFiles, nameof(inputFilePaths));
            }

            return inputFilePaths[0];
        }
    }

    public class ConversionJob_PdfSplit : ConversionJob_GhostscriptBase
    {
        public ConversionJob_PdfSplit()
            : base()
        {
        }

        public ConversionJob_PdfSplit(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        protected override int GetOutputFilesCount()
        {
            try
            {
                string applicationDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                MagickNET.SetGhostscriptDirectory(applicationDirectory);

                using (MagickImageCollection images = new MagickImageCollection())
                {
                    MagickReadSettings settings = new MagickReadSettings
                    {
                        Density = new Density(1, 1)
                    };
                    images.Read(this.InputFilePath, settings);
                    if (images.Count > 0)
                    {
                        return images.Count;
                    }
                }
            }
            catch (Exception exception)
            {
                Diagnostics.Debug.Log(exception.ToString());
            }

            this.ConversionFailed(Properties.Resources.ErrorPdfPageCountFailed);
            return 1;
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (this.State == ConversionState.Failed)
            {
                return;
            }

            if (this.ConversionPreset.OutputType != OutputType.PdfSplit ||
                !string.Equals(Path.GetExtension(this.InputFilePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Invalid input or output type for PDF splitting.");
            }
        }

        protected override void Convert()
        {
            this.UserState = Properties.Resources.ConversionStateSplitPdf;
            for (int index = 0; index < this.OutputFilePaths.Length; index++)
            {
                if (this.CancelIsRequested)
                {
                    return;
                }

                this.CurrentOutputFilePathIndex = index;
                this.Progress = (float)index / this.OutputFilePaths.Length;
                int pageNumber = index + 1;
                string pageArguments = $"-dCompatibilityLevel=1.7 -dFirstPage={pageNumber} -dLastPage={pageNumber}";
                if (!this.RunGhostscript("pdfwrite", this.InputFilePath, this.OutputFilePath, pageArguments, false))
                {
                    return;
                }

                if (!File.Exists(this.OutputFilePath) || new FileInfo(this.OutputFilePath).Length == 0)
                {
                    this.ConversionFailed(Properties.Resources.ErrorGhostscriptEmptyOutput);
                    return;
                }
            }
        }
    }
}
