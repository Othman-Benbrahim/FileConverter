// <copyright file="DocumentFormatDefinition.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionEngines
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Describes a document format independently from the tool used to convert it.
    /// </summary>
    public sealed class DocumentFormatDefinition
    {
        public DocumentFormatDefinition(
            string id,
            string displayName,
            IEnumerable<string> extensions,
            string pandocReader,
            string pandocWriter,
            bool preferLibreOfficeReader,
            bool preferLibreOfficeWriter,
            OutputType? outputType = null,
            string[] extraPandocWriteArguments = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A document format id is required.", nameof(id));
            }

            this.Id = id;
            this.DisplayName = displayName ?? id;
            this.Extensions = extensions.Select(NormalizeExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            this.PandocReader = pandocReader;
            this.PandocWriter = pandocWriter;
            this.PreferLibreOfficeReader = preferLibreOfficeReader;
            this.PreferLibreOfficeWriter = preferLibreOfficeWriter;
            this.OutputType = outputType;
            this.ExtraPandocWriteArguments = extraPandocWriteArguments ?? new string[0];
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IReadOnlyList<string> Extensions { get; }

        public string PrimaryExtension => this.Extensions[0];

        public string PandocReader { get; }

        public string PandocWriter { get; }

        public bool PreferLibreOfficeReader { get; }

        public bool PreferLibreOfficeWriter { get; }

        public OutputType? OutputType { get; }

        public IReadOnlyList<string> ExtraPandocWriteArguments { get; }

        private static string NormalizeExtension(string extension)
        {
            return (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        }
    }
}
