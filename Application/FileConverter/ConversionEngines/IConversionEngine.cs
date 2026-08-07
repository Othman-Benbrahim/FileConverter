// <copyright file="IConversionEngine.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionEngines
{
    using System.Collections.Generic;

    using FileConverter.ConversionJobs;

    /// <summary>
    /// Selects and creates conversion jobs for a preset and one or several inputs.
    /// </summary>
    public interface IConversionEngine
    {
        string Name { get; }

        int Priority { get; }

        bool HandlesMultipleInputs { get; }

        bool CanHandle(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths);

        IEnumerable<ConversionJob> CreateJobs(ConversionPreset conversionPreset, IReadOnlyList<string> inputFilePaths);
    }
}
