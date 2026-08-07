// <copyright file="MarkdownTextConverter.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System.IO;
    using System.Text;

    internal static class MarkdownTextConverter
    {
        public static void WriteMarkdown(string sourceTextPath, string originalInputPath, string outputPath)
        {
            string sourceText = File.ReadAllText(sourceTextPath, Encoding.UTF8);
            WriteMarkdownText(sourceText, originalInputPath, outputPath);
        }

        public static void WriteMarkdownText(string sourceText, string originalInputPath, string outputPath)
        {
            string title = Path.GetFileNameWithoutExtension(originalInputPath);
            string normalizedText = NormalizeText(sourceText);

            StringBuilder markdown = new StringBuilder();
            markdown.Append("# ");
            markdown.AppendLine(EscapeHeading(title));
            markdown.AppendLine();
            markdown.Append(normalizedText);
            if (!normalizedText.EndsWith("\n"))
            {
                markdown.AppendLine();
            }

            File.WriteAllText(outputPath, markdown.ToString(), new UTF8Encoding(false));
        }

        public static bool HasMeaningfulText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            int meaningfulCharacters = 0;
            foreach (char character in text)
            {
                if (char.IsLetterOrDigit(character))
                {
                    meaningfulCharacters++;
                    if (meaningfulCharacters >= 2)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeText(string sourceText)
        {
            string text = (sourceText ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Replace('\v', '\n')
                .Replace("\a", " | ")
                .Replace("\f", "\n\n---\n\n");

            string[] lines = text.Split('\n');
            StringBuilder result = new StringBuilder();
            int consecutiveEmptyLines = 0;
            foreach (string sourceLine in lines)
            {
                string line = sourceLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    consecutiveEmptyLines++;
                    if (consecutiveEmptyLines > 1)
                    {
                        continue;
                    }

                    result.AppendLine();
                    continue;
                }

                consecutiveEmptyLines = 0;
                result.AppendLine(EscapeBlockMarker(line));
            }

            return result.ToString().Trim();
        }

        private static string EscapeHeading(string text)
        {
            return (text ?? string.Empty).Replace("\\", "\\\\").Replace("#", "\\#");
        }

        private static string EscapeBlockMarker(string line)
        {
            string trimmedStart = line.TrimStart();
            int indentationLength = line.Length - trimmedStart.Length;
            if (trimmedStart.StartsWith("#") ||
                trimmedStart.StartsWith(">") ||
                trimmedStart.StartsWith("- ") ||
                trimmedStart.StartsWith("+ ") ||
                trimmedStart.StartsWith("* "))
            {
                return line.Insert(indentationLength, "\\");
            }

            return line;
        }
    }
}
