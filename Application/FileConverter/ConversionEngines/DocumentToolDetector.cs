// <copyright file="DocumentToolDetector.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionEngines
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    public sealed class DocumentToolPaths
    {
        public string PandocPath { get; set; }

        public string LibreOfficePath { get; set; }

        public string TypstPath { get; set; }

        public bool HasPandoc => !string.IsNullOrEmpty(this.PandocPath);

        public bool HasLibreOffice => !string.IsNullOrEmpty(this.LibreOfficePath);

        public bool HasTypst => !string.IsNullOrEmpty(this.TypstPath);
    }

    /// <summary>
    /// Locates bundled tools first, then user and machine installations, then PATH.
    /// </summary>
    public static class DocumentToolDetector
    {
        public static DocumentToolPaths Detect()
        {
            string applicationDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return new DocumentToolPaths
            {
                PandocPath = FindExecutable("pandoc.exe", new[]
                {
                    Path.Combine(applicationDirectory, "pandoc.exe"),
                    Path.Combine(applicationDirectory, "Tools", "Pandoc", "pandoc.exe"),
                    Path.Combine(programFiles, "Pandoc", "pandoc.exe"),
                    Path.Combine(localApplicationData, "Pandoc", "pandoc.exe"),
                }),
                LibreOfficePath = FindExecutable("soffice.exe", new[]
                {
                    Path.Combine(applicationDirectory, "soffice.exe"),
                    Path.Combine(applicationDirectory, "Tools", "LibreOffice", "program", "soffice.exe"),
                    Path.Combine(programFiles, "LibreOffice", "program", "soffice.exe"),
                    Path.Combine(programFilesX86, "LibreOffice", "program", "soffice.exe"),
                }),
                TypstPath = FindExecutable("typst.exe", new[]
                {
                    Path.Combine(applicationDirectory, "typst.exe"),
                    Path.Combine(applicationDirectory, "Tools", "Typst", "typst.exe"),
                    Path.Combine(localApplicationData, "Programs", "Typst", "typst.exe"),
                }),
            };
        }

        private static string FindExecutable(string executableName, IEnumerable<string> preferredPaths)
        {
            string preferredPath = preferredPaths.FirstOrDefault(File.Exists);
            if (!string.IsNullOrEmpty(preferredPath))
            {
                return preferredPath;
            }

            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string pathEntry in pathValue.Split(Path.PathSeparator))
            {
                string directory = pathEntry.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                try
                {
                    string candidate = Path.Combine(directory, executableName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // Ignore malformed PATH entries.
                }
            }

            return null;
        }
    }
}
