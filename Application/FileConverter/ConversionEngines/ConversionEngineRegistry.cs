// <copyright file="ConversionEngineRegistry.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionEngines
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using FileConverter.ConversionJobs;

    /// <summary>
    /// Ordered registry of conversion engines. New engines can be registered without changing the job factory.
    /// </summary>
    public sealed class ConversionEngineRegistry
    {
        private readonly object syncRoot = new object();
        private readonly List<IConversionEngine> engines = new List<IConversionEngine>();

        public ConversionEngineRegistry(IEnumerable<IConversionEngine> engines)
        {
            if (engines == null)
            {
                throw new ArgumentNullException(nameof(engines));
            }

            foreach (IConversionEngine engine in engines)
            {
                this.Register(engine);
            }
        }

        public static ConversionEngineRegistry CreateDefault()
        {
            return new ConversionEngineRegistry(BuiltInConversionEngines.Create());
        }

        public IReadOnlyList<IConversionEngine> Engines
        {
            get
            {
                lock (this.syncRoot)
                {
                    return this.engines.ToArray();
                }
            }
        }

        public void Register(IConversionEngine engine)
        {
            if (engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            lock (this.syncRoot)
            {
                this.engines.RemoveAll(item => string.Equals(item.Name, engine.Name, StringComparison.OrdinalIgnoreCase));
                this.engines.Add(engine);
                this.engines.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            }
        }

        public bool Unregister(string engineName)
        {
            if (string.IsNullOrWhiteSpace(engineName))
            {
                return false;
            }

            lock (this.syncRoot)
            {
                return this.engines.RemoveAll(item => string.Equals(item.Name, engineName, StringComparison.OrdinalIgnoreCase)) > 0;
            }
        }

        public IEnumerable<ConversionJob> CreateJobs(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths)
        {
            if (conversionPreset == null)
            {
                throw new ArgumentNullException(nameof(conversionPreset));
            }

            if (inputFilePaths == null || inputFilePaths.Count == 0 || inputFilePaths.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("At least one valid input file is required.", nameof(inputFilePaths));
            }

            IConversionEngine[] snapshot;
            lock (this.syncRoot)
            {
                snapshot = this.engines.ToArray();
            }

            IConversionEngine batchEngine = snapshot.FirstOrDefault(engine => engine.HandlesMultipleInputs && engine.CanHandle(conversionPreset, inputFilePaths));
            if (batchEngine != null)
            {
                return ValidateJobs(batchEngine, batchEngine.CreateJobs(conversionPreset, inputFilePaths));
            }

            List<ConversionJob> jobs = new List<ConversionJob>();
            foreach (string inputFilePath in inputFilePaths)
            {
                IReadOnlyList<string> singleInput = new[] { inputFilePath };
                IConversionEngine selectedEngine = snapshot.FirstOrDefault(engine => !engine.HandlesMultipleInputs && engine.CanHandle(conversionPreset, singleInput));
                if (selectedEngine == null)
                {
                    throw new NotSupportedException($"No conversion engine supports preset '{conversionPreset.FullName}' for input '{inputFilePath}'.");
                }

                jobs.AddRange(ValidateJobs(selectedEngine, selectedEngine.CreateJobs(conversionPreset, singleInput)));
            }

            return jobs;
        }

        private static IEnumerable<ConversionJob> ValidateJobs(IConversionEngine engine, IEnumerable<ConversionJob> createdJobs)
        {
            ConversionJob[] jobs = createdJobs.ToArray();
            if (jobs.Length == 0 || jobs.Any(job => job == null))
            {
                throw new InvalidOperationException($"Conversion engine '{engine.Name}' did not create a valid conversion job.");
            }

            return jobs;
        }
    }
}
