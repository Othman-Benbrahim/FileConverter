// <copyright file="OutputTypeViewModel.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ViewModels
{
    public class OutputTypeViewModel 
    {
        public OutputTypeViewModel(OutputType type)
        {
            this.Type = type;
            this.Category = Helpers.GetExtensionCategory(Helpers.GetOutputExtension(type));
        }

        public OutputType Type
        {
            get;
            private set;
        }

        public string Category
        {
            get;
            set;
        }

        public string DisplayName
        {
            get
            {
                switch (this.Type)
                {
                    case OutputType.PdfMerge:
                        return Properties.Resources.OutputTypePdfMerge;

                    case OutputType.PdfSplit:
                        return Properties.Resources.OutputTypePdfSplit;

                    case OutputType.Md:
                        return Properties.Resources.OutputTypeMarkdown;

                    case OutputType.Latex:
                        return "LaTeX";

                    case OutputType.Html:
                        return "HTML";

                    case OutputType.Epub:
                        return "EPUB";

                    case OutputType.Rst:
                        return "reStructuredText";

                    case OutputType.Rtf:
                        return "RTF";

                    case OutputType.Odt:
                        return "ODT";

                    default:
                        return this.Type.ToString();
                }
            }
        }

        public override bool Equals(object other)
        {
            OutputTypeViewModel outputTypeViewModel = other as OutputTypeViewModel;

            return outputTypeViewModel?.Type == this.Type;
        }

        public override int GetHashCode()
        {
            return this.Type.GetHashCode();
        }
    }
}
