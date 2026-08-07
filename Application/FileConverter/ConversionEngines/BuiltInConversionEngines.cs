// <copyright file="BuiltInConversionEngines.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionEngines
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using FileConverter.ConversionJobs;

    internal static class BuiltInConversionEngines
    {
        public static IEnumerable<IConversionEngine> Create()
        {
            yield return new BatchEngine("PDF merge", 1000, OutputType.PdfMerge, (preset, paths) => new ConversionJob_PdfMerge(preset, paths.ToArray()));
            yield return new SingleInputEngine("CD audio extraction", 900, (preset, extension) => extension == "cda", (preset, path) => new ConversionJob_ExtractCDA(preset, path));
            yield return new SingleInputEngine("Ghostscript document", 850, (preset, extension) => extension == "pdf" && IsOneOf(preset.OutputType, OutputType.Docx, OutputType.Md, OutputType.Txt), (preset, path) => new ConversionJob_Ghostscript(preset, path));
            yield return new SingleInputEngine("PDF split", 840, (preset, extension) => extension == "pdf" && preset.OutputType == OutputType.PdfSplit, (preset, path) => new ConversionJob_PdfSplit(preset, path));
            yield return new SingleInputEngine("Markdown", 830, (preset, extension) => extension == "txt" && preset.OutputType == OutputType.Md, (preset, path) => new ConversionJob_Markdown(preset, path));
            yield return new SingleInputEngine("Microsoft Word", 800, (preset, extension) => IsOneOf(extension, "docx", "odt", "doc"), (preset, path) => new ConversionJob_Word(preset, path));
            yield return new SingleInputEngine("Microsoft Excel", 790, (preset, extension) => IsOneOf(extension, "xlsx", "ods", "xls"), (preset, path) => new ConversionJob_Excel(preset, path));
            yield return new SingleInputEngine("Microsoft PowerPoint", 780, (preset, extension) => IsOneOf(extension, "pptx", "odp", "ppt"), (preset, path) => new ConversionJob_PowerPoint(preset, path));
            yield return new SingleInputEngine("ICO", 700, (preset, extension) => preset.OutputType == OutputType.Ico, (preset, path) => new ConversionJob_Ico(preset, path));
            yield return new SingleInputEngine("GIF", 690, (preset, extension) => preset.OutputType == OutputType.Gif, (preset, path) => new ConversionJob_Gif(preset, path));
            yield return new SingleInputEngine("ImageMagick", 650, (preset, extension) => IsOneOf(preset.OutputType, OutputType.Pdf, OutputType.Avif, OutputType.Jpg, OutputType.Png, OutputType.Webp), (preset, path) => new ConversionJob_ImageMagick(preset, path));
            yield return new SingleInputEngine("FFmpeg", 0, (preset, extension) => true, (preset, path) => new ConversionJob_FFMPEG(preset, path));
        }

        private static string GetExtension(string inputFilePath)
        {
            string extension = Path.GetExtension(inputFilePath);
            return string.IsNullOrEmpty(extension) ? string.Empty : extension.TrimStart('.').ToLowerInvariant();
        }

        private static bool IsOneOf<T>(T value, params T[] values)
        {
            return values.Contains(value);
        }

        private sealed class SingleInputEngine : IConversionEngine
        {
            private readonly Func<ConversionPreset, string, bool> predicate;
            private readonly Func<ConversionPreset, string, ConversionJob> factory;

            public SingleInputEngine(string name, int priority, Func<ConversionPreset, string, bool> predicate, Func<ConversionPreset, string, ConversionJob> factory)
            {
                this.Name = name;
                this.Priority = priority;
                this.predicate = predicate;
                this.factory = factory;
            }

            public string Name { get; }

            public int Priority { get; }

            public bool HandlesMultipleInputs => false;

            public bool CanHandle(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths)
            {
                return inputFilePaths.Count == 1 && this.predicate(conversionPreset, GetExtension(inputFilePaths[0]));
            }

            public IEnumerable<ConversionJob> CreateJobs(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths)
            {
                return inputFilePaths.Select(path => this.factory(conversionPreset, path));
            }
        }

        private sealed class BatchEngine : IConversionEngine
        {
            private readonly OutputType outputType;
            private readonly Func<ConversionPreset, IReadOnlyList<string>, ConversionJob> factory;

            public BatchEngine(string name, int priority, OutputType outputType, Func<ConversionPreset, IReadOnlyList<string>, ConversionJob> factory)
            {
                this.Name = name;
                this.Priority = priority;
                this.outputType = outputType;
                this.factory = factory;
            }

            public string Name { get; }

            public int Priority { get; }

            public bool HandlesMultipleInputs => true;

            public bool CanHandle(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths)
            {
                return conversionPreset.OutputType == this.outputType;
            }

            public IEnumerable<ConversionJob> CreateJobs(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths)
            {
                if (inputFilePaths.Count < 2 || inputFilePaths.Any(path => GetExtension(path) != "pdf"))
                {
                    throw new ArgumentException("PDF merging requires at least two PDF input files.", nameof(inputFilePaths));
                }

                yield return this.factory(conversionPreset, inputFilePaths);
            }
        }
    }
}
