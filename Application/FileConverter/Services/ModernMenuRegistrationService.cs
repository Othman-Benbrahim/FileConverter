// <copyright file="ModernMenuRegistrationService.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.Services
{
    using System;
    using System.Diagnostics;
    using System.IO;

    internal static class ModernMenuRegistrationService
    {
        private const string PackageName = "FileConverter.ModernShell";

        public static bool Register(string packagePath, string externalLocation)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath) || string.IsNullOrWhiteSpace(externalLocation) || !Directory.Exists(externalLocation))
            {
                return false;
            }

            string command = "$ErrorActionPreference='Stop'; Add-AppxPackage -Path " + ToPowerShellLiteral(Path.GetFullPath(packagePath)) +
                             " -ExternalLocation " + ToPowerShellLiteral(Path.GetFullPath(externalLocation));
            return RunPowerShell(command);
        }

        public static bool Unregister()
        {
            string command = "$ErrorActionPreference='Stop'; Get-AppxPackage -Name " + ToPowerShellLiteral(PackageName) +
                             " | Remove-AppxPackage";
            return RunPowerShell(command);
        }

        private static string ToPowerShellLiteral(string value)
        {
            return "'" + value.Replace("'", "''") + "'";
        }

        private static bool RunPowerShell(string command)
        {
            string powerShellPath = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            if (!File.Exists(powerShellPath))
            {
                return false;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(powerShellPath)
            {
                Arguments = "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
    }
}
