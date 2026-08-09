// <copyright file="DocumentConversionRouter.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionEngines
{
    using System;

    public enum DocumentConversionPipeline
    {
        Unavailable,
        PandocDirect,
        LibreOfficeDirect,
        LibreOfficePreprocess,
        PandocThenLibreOffice,
    }

    public sealed class DocumentConversionRoute
    {
        public DocumentConversionPipeline Pipeline { get; set; }

        public DocumentFormatDefinition SourceFormat { get; set; }

        public DocumentFormatDefinition TargetFormat { get; set; }

        public DocumentToolPaths Tools { get; set; }

        public int EstimatedFidelity { get; set; }

        public string UnavailableReason { get; set; }

        public bool IsAvailable => this.Pipeline != DocumentConversionPipeline.Unavailable;
    }

    /// <summary>
    /// Selects the highest-fidelity available document conversion pipeline.
    /// </summary>
    public static class DocumentConversionRouter
    {
        public static DocumentConversionRoute Select(string sourceExtension, OutputType outputType)
        {
            DocumentFormatDefinition sourceFormat;
            DocumentFormatDefinition targetFormat;
            DocumentToolPaths tools = DocumentToolDetector.Detect();

            if (!DocumentFormatCatalog.TryGetByExtension(sourceExtension, out sourceFormat) ||
                !DocumentFormatCatalog.TryGetByOutputType(outputType, out targetFormat))
            {
                return Unavailable(sourceFormat, targetFormat, tools, "This document format pair is not registered.");
            }

            // PDF text extraction and OCR remain owned by the existing Ghostscript engine.
            if (string.Equals(sourceFormat.Id, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Unavailable(sourceFormat, targetFormat, tools, "PDF input must be processed by the PDF/OCR engine first.");
            }

            bool targetIsPdf = outputType == OutputType.Pdf;
            bool officeSource = sourceFormat.PreferLibreOfficeReader;
            bool writerSource = sourceFormat.Id == "doc" || sourceFormat.Id == "docx" || sourceFormat.Id == "odt" || sourceFormat.Id == "rtf";

            if (officeSource && tools.HasLibreOffice && targetIsPdf)
            {
                return Available(DocumentConversionPipeline.LibreOfficeDirect, sourceFormat, targetFormat, tools, 92);
            }

            if (writerSource && tools.HasLibreOffice && targetFormat.PreferLibreOfficeWriter)
            {
                return Available(DocumentConversionPipeline.LibreOfficeDirect, sourceFormat, targetFormat, tools, 92);
            }

            if (targetIsPdf)
            {
                if (tools.HasPandoc && tools.HasTypst && !string.IsNullOrEmpty(sourceFormat.PandocReader))
                {
                    return Available(DocumentConversionPipeline.PandocDirect, sourceFormat, targetFormat, tools, 88);
                }

                if (tools.HasPandoc && tools.HasLibreOffice && !string.IsNullOrEmpty(sourceFormat.PandocReader))
                {
                    return Available(DocumentConversionPipeline.PandocThenLibreOffice, sourceFormat, targetFormat, tools, 82);
                }

                return Unavailable(sourceFormat, targetFormat, tools, "PDF creation from markup requires Pandoc plus Typst or LibreOffice.");
            }

            if (sourceFormat.Id == "rtf" && tools.HasLibreOffice && tools.HasPandoc && !string.IsNullOrEmpty(targetFormat.PandocWriter))
            {
                return Available(DocumentConversionPipeline.LibreOfficePreprocess, sourceFormat, targetFormat, tools, 86);
            }

            if (tools.HasPandoc && !string.IsNullOrEmpty(sourceFormat.PandocReader) && !string.IsNullOrEmpty(targetFormat.PandocWriter))
            {
                int fidelity = sourceFormat.Id == "rtf" ? 72 : 85;
                return Available(DocumentConversionPipeline.PandocDirect, sourceFormat, targetFormat, tools, fidelity);
            }

            if (writerSource && tools.HasLibreOffice && tools.HasPandoc && !string.IsNullOrEmpty(targetFormat.PandocWriter))
            {
                return Available(DocumentConversionPipeline.LibreOfficePreprocess, sourceFormat, targetFormat, tools, 78);
            }

            if (officeSource && !writerSource)
            {
                return Unavailable(sourceFormat, targetFormat, tools, "Presentation and spreadsheet inputs currently support PDF output only.");
            }

            string dependency = officeSource
                ? "Install LibreOffice and Pandoc, or Microsoft Office for supported legacy conversions."
                : "Install Pandoc. LibreOffice is also recommended for Office and PDF output.";
            return Unavailable(sourceFormat, targetFormat, tools, dependency);
        }

        private static DocumentConversionRoute Available(DocumentConversionPipeline pipeline, DocumentFormatDefinition source, DocumentFormatDefinition target, DocumentToolPaths tools, int fidelity)
        {
            return new DocumentConversionRoute
            {
                Pipeline = pipeline,
                SourceFormat = source,
                TargetFormat = target,
                Tools = tools,
                EstimatedFidelity = fidelity,
            };
        }

        private static DocumentConversionRoute Unavailable(DocumentFormatDefinition source, DocumentFormatDefinition target, DocumentToolPaths tools, string reason)
        {
            return new DocumentConversionRoute
            {
                Pipeline = DocumentConversionPipeline.Unavailable,
                SourceFormat = source,
                TargetFormat = target,
                Tools = tools,
                UnavailableReason = reason,
            };
        }
    }
}
