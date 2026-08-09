// <copyright file="DocumentFormatCatalog.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionEngines
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Central catalog for document formats supported by Pandoc, LibreOffice or the existing PDF engine.
    /// </summary>
    public static class DocumentFormatCatalog
    {
        private static readonly DocumentFormatDefinition[] formats =
        {
            new DocumentFormatDefinition("doc", "Microsoft Word 97-2003", new[] { "doc" }, null, null, true, true),
            new DocumentFormatDefinition("docx", "Microsoft Word", new[] { "docx" }, "docx", "docx", false, true, OutputType.Docx),
            new DocumentFormatDefinition("odt", "OpenDocument Text", new[] { "odt" }, "odt", "odt", true, true, OutputType.Odt),
            // Pandoc writes RTF but does not provide an RTF reader. LibreOffice normalizes it to DOCX first.
            new DocumentFormatDefinition("rtf", "Rich Text Format", new[] { "rtf" }, null, "rtf", true, true, OutputType.Rtf),
            // Plain text is intentionally parsed as Markdown: this preserves paragraphs without inventing formatting.
            new DocumentFormatDefinition("txt", "Plain text", new[] { "txt" }, "markdown", "plain", false, true, OutputType.Txt),
            new DocumentFormatDefinition("markdown", "Markdown", new[] { "md", "markdown" }, "markdown", "gfm", false, false, OutputType.Md),
            new DocumentFormatDefinition("html", "HTML", new[] { "html", "htm" }, "html", "html5", false, false, OutputType.Html, new[] { "--standalone", "--embed-resources" }),
            new DocumentFormatDefinition("epub", "EPUB", new[] { "epub" }, "epub", "epub3", false, false, OutputType.Epub, new[] { "--toc" }),
            new DocumentFormatDefinition("latex", "LaTeX", new[] { "tex", "latex" }, "latex", "latex", false, false, OutputType.Latex, new[] { "--standalone" }),
            new DocumentFormatDefinition("rst", "reStructuredText", new[] { "rst" }, "rst", "rst", false, false, OutputType.Rst),
            new DocumentFormatDefinition("org", "Org mode", new[] { "org" }, "org", "org", false, false),
            new DocumentFormatDefinition("asciidoc", "AsciiDoc", new[] { "adoc", "asciidoc" }, "asciidoc", "asciidoc", false, false),
            new DocumentFormatDefinition("docbook", "DocBook", new[] { "dbk", "docbook", "xml" }, "docbook", "docbook5", false, false),
            new DocumentFormatDefinition("opml", "OPML", new[] { "opml" }, "opml", "opml", false, false),
            new DocumentFormatDefinition("ppt", "Microsoft PowerPoint 97-2003", new[] { "ppt" }, null, null, true, true),
            new DocumentFormatDefinition("pptx", "Microsoft PowerPoint", new[] { "pptx" }, null, "pptx", true, true),
            new DocumentFormatDefinition("odp", "OpenDocument Presentation", new[] { "odp" }, null, null, true, true),
            new DocumentFormatDefinition("xls", "Microsoft Excel 97-2003", new[] { "xls" }, null, null, true, true),
            new DocumentFormatDefinition("xlsx", "Microsoft Excel", new[] { "xlsx" }, null, null, true, true),
            new DocumentFormatDefinition("ods", "OpenDocument Spreadsheet", new[] { "ods" }, null, null, true, true),
            new DocumentFormatDefinition("csv", "Comma-separated values", new[] { "csv" }, "csv", null, false, true),
            new DocumentFormatDefinition("pdf", "Portable Document Format", new[] { "pdf" }, null, null, true, true, OutputType.Pdf),
        };

        private static readonly Dictionary<string, DocumentFormatDefinition> formatsByExtension = formats
            .SelectMany(format => format.Extensions.Select(extension => new KeyValuePair<string, DocumentFormatDefinition>(extension, format)))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<OutputType, DocumentFormatDefinition> formatsByOutputType = formats
            .Where(format => format.OutputType.HasValue)
            .ToDictionary(format => format.OutputType.Value, format => format);

        public static IReadOnlyList<DocumentFormatDefinition> Formats => formats;

        public static bool TryGetByExtension(string extension, out DocumentFormatDefinition format)
        {
            return formatsByExtension.TryGetValue(NormalizeExtension(extension), out format);
        }

        public static bool TryGetByOutputType(OutputType outputType, out DocumentFormatDefinition format)
        {
            return formatsByOutputType.TryGetValue(outputType, out format);
        }

        public static bool IsDocumentOutput(OutputType outputType)
        {
            return formatsByOutputType.ContainsKey(outputType);
        }

        private static string NormalizeExtension(string extension)
        {
            return (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        }
    }
}
