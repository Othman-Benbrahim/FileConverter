// <copyright file="ConversionJob_Markdown.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.IO;
    using System.Text;

    public class ConversionJob_Markdown : ConversionJob
    {
        public ConversionJob_Markdown()
            : base()
        {
        }

        public ConversionJob_Markdown(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (this.ConversionPreset.OutputType != OutputType.Md || !string.Equals(Path.GetExtension(this.InputFilePath), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("The Markdown converter only supports TXT to Markdown conversion.");
            }
        }

        protected override void Convert()
        {
            this.UserState = Properties.Resources.ConversionStateCreateMarkdown;
            this.Progress = 0.25f;

            string text = File.ReadAllText(this.InputFilePath, Encoding.UTF8);
            if (!MarkdownTextConverter.HasMeaningfulText(text))
            {
                this.ConversionFailed(Properties.Resources.ErrorMarkdownSourceEmpty);
                return;
            }

            MarkdownTextConverter.WriteMarkdownText(text, this.InputFilePath, this.OutputFilePath);
        }
    }
}
