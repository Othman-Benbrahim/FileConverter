# File Converter — build modifié 2.5.3

File Converter permet de convertir et compresser des fichiers depuis le menu contextuel de l’Explorateur Windows. Ce dépôt est une évolution du projet libre [Tichau/FileConverter](https://github.com/Tichau/FileConverter), toujours distribué sous GPL v3.

![File Converter Usage](Resources/FileConverterUsage.gif)

## Modifications ajoutées dans ce build

### Documents PDF et texte

- Conversion PDF vers DOCX avec reconstruction locale du texte et des paragraphes.
- Conversion PDF vers TXT.
- Détection des PDF scannés et messages d’erreur plus explicites.
- OCR entièrement local avec Ghostscript/Tesseract, modèles français et anglais inclus dans `tessdata`.
- Fusion de plusieurs PDF avec le préréglage `PDF tools/Merge PDFs`.
- Découpage d’un PDF en un fichier par page avec `PDF tools/Split PDF`.
- Conversion PDF ou TXT vers Markdown avec le préréglage `To Markdown`.
- Ghostscript mis à jour en 10.07.1 et fichiers de licence associés au MSI.

### Routeur documentaire ouvert

- Catalogue central de 22 formats : DOC, DOCX, ODT, RTF, TXT, Markdown, HTML, EPUB, LaTeX, reStructuredText, Org, AsciiDoc, DocBook, OPML, PPT, PPTX, ODP, XLS, XLSX, ODS, CSV et PDF.
- Nouveaux formats de sortie configurables : ODT, RTF, HTML autonome, EPUB 3, LaTeX (`.tex`) et reStructuredText (`.rst`).
- Extension des sorties DOCX, TXT, Markdown et PDF aux formats documentaires compatibles.
- Routage automatique vers Pandoc, LibreOffice, Typst ou les moteurs historiques selon la paire source/cible et les outils présents.
- Priorité conservée au moteur Ghostscript/OCR pour les PDF en entrée ; le nouveau routeur ne contourne donc pas la détection des scans.
- Conversion LibreOffice exécutée dans un profil temporaire isolé afin de ne pas interférer avec une session LibreOffice déjà ouverte.
- Messages explicites lorsqu’une dépendance manque, contrôle du fichier produit et avertissement avant une conversion estimée à faible fidélité.
- Les présentations et feuilles de calcul sont volontairement limitées à la sortie PDF. Les formats complexes peuvent perdre des éléments de mise en page lors d’une conversion structurelle avec Pandoc.

### Menu Windows

- Conservation de l’extension SharpShell historique sous Windows 10. Sous Windows 11, son inscription est retirée automatiquement afin d’éviter un menu `File Converter` en double.
- Ajout d’une DLL native x64 `IExplorerCommand` pour le menu contextuel principal de Windows 11.
- Affichage dynamique des seuls préréglages compatibles avec tous les fichiers sélectionnés.
- Regroupement réel des préréglages dans des sous-menus (`Documents`, `PDF tools`) au lieu d’une longue liste aplatie.
- Compilation UTF-8 de la DLL native afin d’éliminer les libellés corrompus de type `â€º`.
- Gestion de la fusion PDF uniquement lorsqu’au moins deux PDF sont sélectionnés.
- Accès à `Configure presets...` dans le sous-menu moderne.
- Transmission de grandes sélections par fichier temporaire UTF-8, supprimé par l’application après lecture.
- Recherche de l’exécutable par `HKCU`, puis `HKLM\Software\FileConverter\Path`, avec repli sur le dossier de la DLL.
- Déploiement du menu moderne au moyen d’un package MSIX sparse signé, inscrit automatiquement par le MSI.

### Architecture modulaire des moteurs

- Remplacement des tests en cascade du `ConversionJobFactory` par un registre ordonné de moteurs.
- Interface publique `IConversionEngine` avec nom, priorité, détection de compatibilité et création des tâches.
- Prise en charge séparée des moteurs unitaires et des moteurs par lot.
- Sélection d’un moteur par fichier, ce qui conserve les sélections mixtes prises en charge auparavant.
- API d’enregistrement et de retrait permettant d’ajouter un moteur sans modifier la fabrique.
- Moteurs intégrés : fusion/découpage PDF, Ghostscript/OCR, routeur documentaire Pandoc/LibreOffice/Typst, Markdown, Word, Excel, PowerPoint, CDA, ICO, GIF, ImageMagick et FFmpeg.

Exemple d’ajout au démarrage :

```csharp
ConversionJobFactory.EngineRegistry.Register(new MonMoteurDeConversion());
```

`MonMoteurDeConversion` doit implémenter `FileConverter.ConversionEngines.IConversionEngine`. Une priorité plus élevée est évaluée en premier ; le moteur FFmpeg reste le dernier recours.

### Correctifs de compilation et d’installation

- Correction de la référence `InputCategoryNames` dans les paramètres document.
- Correction du PostBuild lorsque le chemin du projet contient des espaces.
- Acceptation correcte des codes de succès `robocopy` 0 à 7.
- Suppression du problème de guillemet provoqué par le `\` final de `$(TargetDir)`.
- Copie de FFmpeg, Ghostscript, `tessdata`, des langues et des paramètres par défaut dans la sortie Release.
- MSI mis à jour pour installer les nouveaux exécutables, DLL, données OCR, documentation et package du menu Windows 11.

## Prérequis de compilation

Dans Visual Studio Installer 2022 Community, installer :

- **Développement Desktop en .NET** ;
- **Développement Desktop en C++** avec MSVC v143 ;
- le **.NET Framework 4.8 Developer Pack** ;
- le **Windows 10/11 SDK**, notamment MakeAppx et SignTool ;
- WiX 5 est restauré par NuGet lors de la compilation.

Le projet complet est indispensable : le ZIP correctif livré séparément doit être copié par-dessus une copie complète de FileConverter en conservant exactement les sous-dossiers.

## Dépendances des conversions documentaires

Les exécutables ne sont pas embarqués dans le dépôt ni dans le MSI. Le routeur les recherche à côté de `FileConverter.exe`, dans un sous-dossier `Tools`, dans leurs emplacements Windows habituels, puis dans `PATH`.

Installez au minimum Pandoc pour les conversions structurelles :

```bat
winget install --source winget --exact --id JohnMacFarlane.Pandoc
```

LibreOffice est fortement recommandé et nécessaire pour les anciens formats DOC/RTF, les documents Office vers PDF et le repli PDF sans Typst :

```bat
winget install --source winget --exact --id TheDocumentFoundation.LibreOffice
```

Typst est facultatif ; lorsqu’il est présent, Pandoc l’utilise pour produire directement les PDF depuis les formats de balisage :

```bat
winget install --source winget --exact --id Typst.Typst
```

Après installation, fermez puis relancez File Converter. Aucune recompilation n’est nécessaire.

## Générer un MSI depuis `cmd`

Le menu principal de Windows 11 impose un package MSIX signé. Pour un test sur votre propre poste, créez une seule fois un certificat de développement. Depuis `cmd` :

```bat
cd /d "C:\Users\othma\Desktop\FileConverterModif\FileConverter-integration"
Packaging\ModernMenu\create-development-certificate.cmd VotreMotDePassePfx
```

Ouvrez `cmd` **en tant qu’administrateur**. Le script demande confirmation, crée un PFX local et place son certificat public dans `Cert:\LocalMachine\TrustedPeople` et `Cert:\LocalMachine\Root`. Cette approbation de racine est réservée aux essais sur une machine maîtrisée. Le PFX contient la clé privée : ne publiez jamais le PFX, son mot de passe ou ce certificat de développement.

Fichiers créés pour les essais locaux :

```text
Packaging\ModernMenu\FileConverter-Development.pfx
Packaging\ModernMenu\FileConverter-Development.cer
```

Générez ensuite toute la release :

```bat
cd /d "C:\Users\othma\Desktop\FileConverterModif\FileConverter-integration"
build-release.cmd "Packaging\ModernMenu\FileConverter-Development.pfx" "VotreMotDePassePfx"
```

Le script appelle automatiquement `VsDevCmd.bat`, compile l’application, l’extension historique et la DLL C++ x64, construit et signe le package sparse, génère le MSI avec WiX, puis signe également le MSI avec le même PFX. Il n’est plus nécessaire d’appeler `signtool` à la main.

Résultat :

```text
Installer\bin\x64\Release\FileConverter-setup.msi
```

Pour une release distribuée à d’autres machines, utilisez un certificat de production dont la chaîne est reconnue par Windows. Un certificat auto-signé approuvé seulement sur votre poste ne convient pas à la distribution. Le sujet du certificat doit correspondre au champ `Publisher` ; cette version utilise `CN=File Converter Community Build` dans :

- `Packaging\ModernMenu\AppxManifest.xml` ;
- `Packaging\ModernMenu\build-modern-menu-package.cmd` (`EXPECTED_SUBJECT`).

Si votre certificat de production possède un autre sujet, modifiez ces deux valeurs de façon strictement identique avant la compilation. Pour horodater les signatures d’une release publique, passez l’URL HTTPS fournie par votre autorité de certification en troisième argument :

```bat
build-release.cmd "Packaging\ModernMenu\Production.pfx" "VotreMotDePassePfx" "https://adresse-horodatage-fournie-par-votre-autorite"
```

Le PFX utilisé signe le MSIX et le MSI. Ne placez jamais le PFX ni son mot de passe dans GitHub ou dans les fichiers de release.

## Installation et vérification

Installez le MSI, puis redémarrez l’Explorateur si le menu n’apparaît pas immédiatement :

```bat
taskkill /f /im explorer.exe
start explorer.exe
```

Vérifiez l’inscription du package moderne :

```bat
powershell.exe -NoProfile -Command "Get-AppxPackage FileConverter.ModernShell"
```

Sous Windows 11, `File Converter` doit apparaître une seule fois dans le premier menu contextuel. Sous Windows 10, l’extension SharpShell historique reste utilisée.

Erreurs courantes :

- `0x800B0109` : le certificat auto-signé n’est pas approuvé par la machine ; ouvrir `cmd` en administrateur et relancer le script de certificat, qui l’importe dans `LocalMachine\TrustedPeople` et `LocalMachine\Root`.
- `0x80073CF9` : la même version du package est déjà inscrite ; désinstaller d’abord l’ancien MSI ou exécuter `powershell.exe -NoProfile -Command "Get-AppxPackage FileConverter.ModernShell | Remove-AppxPackage"`.
- menu absent après installation : redémarrer l’Explorateur ou fermer puis rouvrir la session.
- erreur MSI 1722/1603 pendant `PostInstallInit` : la version 2.5.2 ou ultérieure lit correctement le chemin machine et rend cette initialisation récupérable.
- message `Can't retrieve the file converter executable path` : vérifier `HKLM\Software\FileConverter\Path` et réinstaller la version 2.5.2 ou ultérieure.

## Fichiers structurants de cette évolution

- `Application\FileConverter\ConversionEngines\` : contrat, registre et moteurs intégrés.
- `Application\FileConverter\ConversionEngines\DocumentFormatCatalog.cs` : catalogue indépendant des 22 formats.
- `Application\FileConverter\ConversionEngines\DocumentConversionRouter.cs` : choix de la chaîne de conversion disponible la plus fidèle.
- `Application\FileConverter\ConversionEngines\DocumentToolDetector.cs` : détection locale de Pandoc, LibreOffice et Typst.
- `Application\FileConverter\ConversionJobs\ConversionJob_Document.cs` : exécution, annulation, contrôle et nettoyage des conversions documentaires.
- `Application\FileConverterModernMenu\` : serveur COM natif `IExplorerCommand` x64.
- `Packaging\ModernMenu\` : manifeste, ressources, certificat de développement et création du MSIX sparse.
- `Installer\Product.wxs` : déploiement, inscription et désinscription des deux menus.
- `build-release.cmd` : génération et signature complètes du MSIX et du MSI depuis `cmd`.

## Middlewares

- **FFmpeg 8.0.1** pour l’audio et la vidéo.
- **ImageMagick 14.10** pour les images et certains PDF.
- **Ghostscript 10.07.1** pour les PDF et l’OCR local.
- **Pandoc** pour les conversions documentaires structurelles (installation séparée).
- **LibreOffice** pour les documents bureautiques et certaines sorties PDF (installation séparée).
- **Typst** pour la génération directe de PDF par Pandoc (installation séparée facultative).
- Modèles OCR français et anglais issus de `tessdata_fast`.
- **SharpShell 2.7.2** pour le menu historique.
- **Markdown.XAML** pour l’affichage Markdown dans l’application.
- **Ripper** et **yeti.mmedia** pour l’extraction CD Audio.
- **WpfAnimatedGif** pour les GIF animés.

## Licence

File Converter et ces modifications sont placés sous GNU GPL version 3. Consultez [LICENSE.md](LICENSE.md). Les middlewares conservent leurs licences respectives, copiées ou référencées dans le projet.
