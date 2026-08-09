// <copyright file="ConversionJob_Document.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Windows;

    using FileConverter.ConversionEngines;

    /// <summary>
    /// Executes document conversion routes selected by <see cref="DocumentConversionRouter"/>.
    /// </summary>
    public sealed class ConversionJob_Document : ConversionJob
    {
        private static readonly object warningSyncRoot = new object();
        private static readonly HashSet<string> acceptedFidelityWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly object processSyncRoot = new object();
        private DocumentConversionRoute route;
        private Process currentProcess;

        public ConversionJob_Document()
            : base()
        {
        }

        public ConversionJob_Document(ConversionPreset conversionPreset, string inputFilePath)
            : base(conversionPreset, inputFilePath)
        {
        }

        public override void Cancel()
        {
            lock (this.processSyncRoot)
            {
                try
                {
                    if (this.currentProcess != null && !this.currentProcess.HasExited)
                    {
                        this.currentProcess.Kill();
                    }
                }
                catch (InvalidOperationException)
                {
                    // The process ended while cancellation was being requested.
                }
            }

            base.Cancel();
        }

        protected override void Initialize()
        {
            base.Initialize();

            string sourceExtension = Path.GetExtension(this.InputFilePath);
            this.route = DocumentConversionRouter.Select(sourceExtension, this.ConversionPreset.OutputType);
            if (!this.route.IsAvailable)
            {
                string unavailableReason = LocalizeUnavailableReason(this.route.UnavailableReason);
                this.ConversionFailed(Localize(
                    $"No document conversion engine is available. {unavailableReason} Install with: winget install JohnMacFarlane.Pandoc and winget install TheDocumentFoundation.LibreOffice.",
                    $"Aucun moteur de conversion documentaire n'est disponible. {unavailableReason} Installez les dépendances avec : winget install JohnMacFarlane.Pandoc puis winget install TheDocumentFoundation.LibreOffice."));
                return;
            }

            Diagnostics.Debug.Log($"Document route: {this.route.SourceFormat.Id} -> {this.route.TargetFormat.Id}; pipeline={this.route.Pipeline}; estimated fidelity={this.route.EstimatedFidelity}%.");
            if (this.route.EstimatedFidelity < 80 && !this.ConfirmLowFidelityConversion())
            {
                this.ConversionFailed(Localize("Document conversion was cancelled because of the expected formatting loss.", "Conversion annulée en raison du risque de perte de mise en forme."));
            }
        }

        protected override void Convert()
        {
            this.UserState = Properties.Resources.ConversionStateConversion;
            this.Progress = 0.1f;

            string temporaryDirectory = Path.Combine(Path.GetTempPath(), "FileConverter-Document-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                bool success;
                switch (this.route.Pipeline)
                {
                    case DocumentConversionPipeline.PandocDirect:
                        success = this.ConvertWithPandoc(this.InputFilePath, this.OutputFilePath, this.route.SourceFormat, this.route.TargetFormat);
                        break;

                    case DocumentConversionPipeline.LibreOfficeDirect:
                        success = this.ConvertWithLibreOffice(this.InputFilePath, this.OutputFilePath, this.route.TargetFormat, temporaryDirectory);
                        break;

                    case DocumentConversionPipeline.LibreOfficePreprocess:
                        success = this.ConvertWithLibreOfficeThenPandoc(temporaryDirectory);
                        break;

                    case DocumentConversionPipeline.PandocThenLibreOffice:
                        success = this.ConvertWithPandocThenLibreOffice(temporaryDirectory);
                        break;

                    default:
                        this.ConversionFailed(Localize("Unsupported document conversion route.", "Route de conversion documentaire non prise en charge."));
                        return;
                }

                if (!success)
                {
                    return;
                }

                if (!File.Exists(this.OutputFilePath) || new FileInfo(this.OutputFilePath).Length == 0)
                {
                    this.ConversionFailed(Localize("The document converter did not create a valid output file.", "Le convertisseur documentaire n'a pas créé de fichier de sortie valide."));
                    return;
                }

                this.Progress = 0.98f;
            }
            finally
            {
                TryDeleteDirectory(temporaryDirectory);
            }
        }

        private bool ConvertWithLibreOfficeThenPandoc(string temporaryDirectory)
        {
            DocumentFormatDefinition docxFormat;
            if (!DocumentFormatCatalog.TryGetByExtension("docx", out docxFormat))
            {
                this.ConversionFailed("The DOCX intermediate format is not registered.");
                return false;
            }

            string intermediatePath = Path.Combine(temporaryDirectory, Path.GetFileNameWithoutExtension(this.InputFilePath) + ".docx");
            this.Progress = 0.2f;
            if (!this.ConvertWithLibreOffice(this.InputFilePath, intermediatePath, docxFormat, temporaryDirectory))
            {
                return false;
            }

            this.Progress = 0.55f;
            return this.ConvertWithPandoc(intermediatePath, this.OutputFilePath, docxFormat, this.route.TargetFormat);
        }

        private bool ConvertWithPandocThenLibreOffice(string temporaryDirectory)
        {
            DocumentFormatDefinition htmlFormat;
            if (!DocumentFormatCatalog.TryGetByExtension("html", out htmlFormat))
            {
                this.ConversionFailed("The HTML intermediate format is not registered.");
                return false;
            }

            string intermediatePath = Path.Combine(temporaryDirectory, Path.GetFileNameWithoutExtension(this.InputFilePath) + ".html");
            this.Progress = 0.2f;
            if (!this.ConvertWithPandoc(this.InputFilePath, intermediatePath, this.route.SourceFormat, htmlFormat))
            {
                return false;
            }

            this.Progress = 0.55f;
            return this.ConvertWithLibreOffice(intermediatePath, this.OutputFilePath, this.route.TargetFormat, temporaryDirectory);
        }

        private bool ConvertWithPandoc(string inputPath, string outputPath, DocumentFormatDefinition sourceFormat, DocumentFormatDefinition targetFormat)
        {
            if (string.IsNullOrEmpty(this.route.Tools.PandocPath))
            {
                this.ConversionFailed(Localize("Pandoc was not found.", "Pandoc est introuvable."));
                return false;
            }

            StringBuilder arguments = new StringBuilder();
            arguments.Append("--from=").Append(QuoteArgument(sourceFormat.PandocReader)).Append(' ');
            if (targetFormat.OutputType == OutputType.Pdf)
            {
                if (string.IsNullOrEmpty(this.route.Tools.TypstPath))
                {
                    this.ConversionFailed(Localize("Typst was not found for PDF generation.", "Typst est introuvable pour générer le PDF."));
                    return false;
                }

                arguments.Append("--pdf-engine=").Append(QuoteArgument(this.route.Tools.TypstPath)).Append(' ');
            }
            else
            {
                arguments.Append("--to=").Append(QuoteArgument(targetFormat.PandocWriter)).Append(' ');
            }

            foreach (string extraArgument in targetFormat.ExtraPandocWriteArguments)
            {
                arguments.Append(extraArgument).Append(' ');
            }

            arguments.Append("--output=").Append(QuoteArgument(outputPath)).Append(' ');
            arguments.Append(QuoteArgument(inputPath));

            string workingDirectory = Path.GetDirectoryName(inputPath);
            return this.RunProcess(this.route.Tools.PandocPath, arguments.ToString(), workingDirectory, "Pandoc");
        }

        private bool ConvertWithLibreOffice(string inputPath, string outputPath, DocumentFormatDefinition targetFormat, string temporaryRoot)
        {
            if (string.IsNullOrEmpty(this.route.Tools.LibreOfficePath))
            {
                this.ConversionFailed(Localize("LibreOffice was not found.", "LibreOffice est introuvable."));
                return false;
            }

            string stageDirectory = Path.Combine(temporaryRoot, "lo-" + Guid.NewGuid().ToString("N"));
            string profileDirectory = Path.Combine(temporaryRoot, "lo-profile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stageDirectory);
            Directory.CreateDirectory(profileDirectory);

            string profileUri = new Uri(profileDirectory + Path.DirectorySeparatorChar).AbsoluteUri;
            string arguments = "-env:UserInstallation=" + QuoteArgument(profileUri) +
                               " --headless --nologo --nodefault --nolockcheck --nofirststartwizard --convert-to " +
                               QuoteArgument(targetFormat.PrimaryExtension) + " --outdir " + QuoteArgument(stageDirectory) + " " + QuoteArgument(inputPath);
            if (!this.RunProcess(this.route.Tools.LibreOfficePath, arguments, stageDirectory, "LibreOffice"))
            {
                return false;
            }

            string expectedOutput = Path.Combine(stageDirectory, Path.GetFileNameWithoutExtension(inputPath) + "." + targetFormat.PrimaryExtension);
            if (!File.Exists(expectedOutput))
            {
                expectedOutput = Directory.GetFiles(stageDirectory, "*." + targetFormat.PrimaryExtension).FirstOrDefault();
            }

            if (string.IsNullOrEmpty(expectedOutput) || !File.Exists(expectedOutput))
            {
                this.ConversionFailed(Localize("LibreOffice did not produce the expected output file.", "LibreOffice n'a pas produit le fichier de sortie attendu."));
                return false;
            }

            File.Copy(expectedOutput, outputPath, false);
            return true;
        }

        private bool RunProcess(string executablePath, string arguments, string workingDirectory, string toolName)
        {
            StringBuilder standardOutput = new StringBuilder();
            StringBuilder standardError = new StringBuilder();
            ProcessStartInfo startInfo = new ProcessStartInfo(executablePath, arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (sender, eventArgs) =>
                {
                    if (eventArgs.Data != null)
                    {
                        standardOutput.AppendLine(eventArgs.Data);
                    }
                };
                process.ErrorDataReceived += (sender, eventArgs) =>
                {
                    if (eventArgs.Data != null)
                    {
                        standardError.AppendLine(eventArgs.Data);
                    }
                };

                try
                {
                    lock (this.processSyncRoot)
                    {
                        this.currentProcess = process;
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                    }

                    process.WaitForExit();
                    process.WaitForExit();
                }
                catch (Exception exception)
                {
                    this.ConversionFailed(string.Format(CultureInfo.InvariantCulture, Localize("Unable to launch {0}: {1}", "Impossible de lancer {0} : {1}"), toolName, exception.Message));
                    return false;
                }
                finally
                {
                    lock (this.processSyncRoot)
                    {
                        this.currentProcess = null;
                    }
                }

                Diagnostics.Debug.Log($"{toolName} output: {standardOutput}");
                if (process.ExitCode != 0)
                {
                    string details = standardError.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(details))
                    {
                        details = standardOutput.ToString().Trim();
                    }

                    this.ConversionFailed(string.Format(CultureInfo.InvariantCulture, Localize("{0} failed (exit code {1}). {2}", "Échec de {0} (code de sortie {1}). {2}"), toolName, process.ExitCode, details));
                    return false;
                }

                return true;
            }
        }

        private bool ConfirmLowFidelityConversion()
        {
            string warningKey = this.route.SourceFormat.Id + "->" + this.route.TargetFormat.Id;
            lock (warningSyncRoot)
            {
                if (acceptedFidelityWarnings.Contains(warningKey))
                {
                    return true;
                }
            }

            string message = Localize(
                $"Estimated formatting fidelity is {this.route.EstimatedFidelity}%. Some layout, fonts, fields or tables may be lost. Continue?",
                $"La fidélité de mise en forme estimée est de {this.route.EstimatedFidelity} %. Une partie de la disposition, des polices, des champs ou des tableaux peut être perdue. Continuer ?");

            MessageBoxResult result = MessageBoxResult.Yes;
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    result = MessageBox.Show(message, "File Converter", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                });
            }

            if (result == MessageBoxResult.Yes)
            {
                lock (warningSyncRoot)
                {
                    acceptedFidelityWarnings.Add(warningKey);
                }

                return true;
            }

            return false;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string Localize(string english, string french)
        {
            return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "fr", StringComparison.OrdinalIgnoreCase) ? french : english;
        }

        private static string LocalizeUnavailableReason(string reason)
        {
            if (!string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "fr", StringComparison.OrdinalIgnoreCase))
            {
                return reason;
            }

            switch (reason)
            {
                case "This document format pair is not registered.":
                    return "Cette paire de formats n'est pas enregistrée.";

                case "PDF input must be processed by the PDF/OCR engine first.":
                    return "Les PDF en entrée doivent d'abord être traités par le moteur PDF/OCR.";

                case "PDF creation from markup requires Pandoc plus Typst or LibreOffice.":
                    return "La création d'un PDF depuis un format de balisage nécessite Pandoc avec Typst ou LibreOffice.";

                case "Presentation and spreadsheet inputs currently support PDF output only.":
                    return "Les présentations et feuilles de calcul ne prennent actuellement en charge que la sortie PDF.";

                case "Install LibreOffice and Pandoc, or Microsoft Office for supported legacy conversions.":
                    return "Installez LibreOffice et Pandoc, ou Microsoft Office pour les conversions historiques compatibles.";

                case "Install Pandoc. LibreOffice is also recommended for Office and PDF output.":
                    return "Installez Pandoc. LibreOffice est également recommandé pour les documents bureautiques et la sortie PDF.";

                default:
                    return reason;
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (IOException exception)
            {
                Diagnostics.Debug.Log($"Unable to delete temporary document directory '{path}': {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Diagnostics.Debug.Log($"Unable to delete temporary document directory '{path}': {exception.Message}");
            }
        }
    }
}
