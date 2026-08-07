// <copyright file="ConversionJobFactory.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FileConverter.ConversionEngines;

    public static class ConversionJobFactory
    {
        private static ConversionEngineRegistry engineRegistry = ConversionEngineRegistry.CreateDefault();

        public static ConversionEngineRegistry EngineRegistry
        {
            get => engineRegistry;
            set => engineRegistry = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static IEnumerable<ConversionJob> CreateJobs(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths)
        {
            return EngineRegistry.CreateJobs(conversionPreset, inputFilePaths);
        }

        public static ConversionJob Create(ConversionPreset conversionPreset, string[] inputFilePaths)
        {
            ConversionJob[] jobs = CreateJobs(conversionPreset, inputFilePaths).ToArray();
            if (jobs.Length != 1)
            {
                throw new InvalidOperationException("This overload requires an engine that creates exactly one batch job.");
            }

            return jobs[0];
        }

        public static ConversionJob Create(ConversionPreset conversionPreset, string inputFilePath)
        {
            ConversionJob[] jobs = CreateJobs(conversionPreset, new[] { inputFilePath }).ToArray();
            if (jobs.Length != 1)
            {
                throw new InvalidOperationException("This overload requires an engine that creates exactly one job.");
            }

            return jobs[0];
        }
    }
}
