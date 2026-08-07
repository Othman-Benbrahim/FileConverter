# File Converter — build modifié 2.4.0

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

### Menu Windows

- Conservation de l’extension SharpShell historique, visible sous **Afficher plus d’options** dans Windows 11. Elle sert aussi de solution de repli sous Windows 10 ou si le package moderne ne peut pas être inscrit.
- Ajout d’une DLL native x64 `IExplorerCommand` pour le menu contextuel principal de Windows 11.
- Affichage dynamique des seuls préréglages compatibles avec tous les fichiers sélectionnés.
- Gestion de la fusion PDF uniquement lorsqu’au moins deux PDF sont sélectionnés.
- Accès à `Configure presets...` dans le sous-menu moderne.
- Transmission de grandes sélections par fichier temporaire UTF-8, supprimé par l’application après lecture.
- Recherche de l’exécutable par `HKCU\Software\FileConverter\Path`, avec repli sur le dossier de la DLL.
- Déploiement du menu moderne au moyen d’un package MSIX sparse signé, inscrit automatiquement par le MSI.

### Architecture modulaire des moteurs

- Remplacement des tests en cascade du `ConversionJobFactory` par un registre ordonné de moteurs.
- Interface publique `IConversionEngine` avec nom, priorité, détection de compatibilité et création des tâches.
- Prise en charge séparée des moteurs unitaires et des moteurs par lot.
- Sélection d’un moteur par fichier, ce qui conserve les sélections mixtes prises en charge auparavant.
- API d’enregistrement et de retrait permettant d’ajouter un moteur sans modifier la fabrique.
- Moteurs intégrés : fusion/découpage PDF, Ghostscript/OCR, Markdown, Word, Excel, PowerPoint, CDA, ICO, GIF, ImageMagick et FFmpeg.

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

## Générer un MSI depuis `cmd`

Le menu principal de Windows 11 impose un package MSIX signé. Pour un test sur votre propre poste, créez une seule fois un certificat de développement. Depuis `cmd` :

```bat
cd /d "C:\Users\othma\Desktop\FileConverterModif\FileConverter-integration"
Packaging\ModernMenu\create-development-certificate.cmd VotreMotDePassePfx
```

Le script demande confirmation, crée un PFX local et place uniquement son certificat public dans `Cert:\CurrentUser\TrustedPeople`. Le PFX contient la clé privée : ne le publiez jamais et ne l’ajoutez pas au ZIP ou au dépôt.

Générez ensuite toute la release :

```bat
cd /d "C:\Users\othma\Desktop\FileConverterModif\FileConverter-integration"
build-release.cmd "Packaging\ModernMenu\FileConverter-Development.pfx" "VotreMotDePassePfx"
```

Le script appelle automatiquement `VsDevCmd.bat`, compile l’application, l’extension historique et la DLL C++ x64, construit et signe le package sparse, puis génère le MSI avec WiX.

Résultat :

```text
Installer\bin\x64\Release\FileConverter-setup.msi
```

Pour une release distribuée à d’autres machines, utilisez un certificat de production dont la chaîne est reconnue par Windows. Un certificat auto-signé approuvé seulement sur votre poste ne convient pas à la distribution. Le sujet du certificat doit correspondre au champ `Publisher` ; cette version utilise `CN=File Converter Community Build` dans :

- `Packaging\ModernMenu\AppxManifest.xml` ;
- `Application\FileConverter\app.manifest` ;
- `Packaging\ModernMenu\build-modern-menu-package.cmd` (`EXPECTED_SUBJECT`).

Si votre certificat de production possède un autre sujet, modifiez ces trois valeurs de façon strictement identique avant la compilation. Le MSIX est signé par `build-release.cmd`. La signature Authenticode du MSI reste pilotée par l’éventuel fichier privé `Installer\Installer.sign` du projet d’origine.

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

Sous Windows 11, `File Converter` doit apparaître dans le premier menu contextuel. L’extension historique reste disponible dans **Afficher plus d’options**.

Erreurs courantes :

- `0x800B0109` : le certificat auto-signé n’est pas dans `CurrentUser\TrustedPeople` ; relancer le script de certificat et accepter l’import.
- `0x80073CF9` : la même version du package est déjà inscrite ; désinstaller d’abord l’ancien MSI ou exécuter `powershell.exe -NoProfile -Command "Get-AppxPackage FileConverter.ModernShell | Remove-AppxPackage"`.
- menu absent après installation : redémarrer l’Explorateur ou fermer puis rouvrir la session.
- échec du menu moderne mais MSI installé : utiliser **Afficher plus d’options** ; l’extension SharpShell est conservée volontairement comme repli.

## Fichiers structurants de cette évolution

- `Application\FileConverter\ConversionEngines\` : contrat, registre et moteurs intégrés.
- `Application\FileConverterModernMenu\` : serveur COM natif `IExplorerCommand` x64.
- `Packaging\ModernMenu\` : manifeste, ressources, certificat de développement et création du MSIX sparse.
- `Installer\Product.wxs` : déploiement, inscription et désinscription des deux menus.
- `build-release.cmd` : génération complète de la release depuis `cmd`.

## Middlewares

- **FFmpeg 8.0.1** pour l’audio et la vidéo.
- **ImageMagick 14.10** pour les images et certains PDF.
- **Ghostscript 10.07.1** pour les PDF et l’OCR local.
- Modèles OCR français et anglais issus de `tessdata_fast`.
- **SharpShell 2.7.2** pour le menu historique.
- **Markdown.XAML** pour l’affichage Markdown dans l’application.
- **Ripper** et **yeti.mmedia** pour l’extraction CD Audio.
- **WpfAnimatedGif** pour les GIF animés.

## Licence

File Converter et ces modifications sont placés sous GNU GPL version 3. Consultez [LICENSE.md](LICENSE.md). Les middlewares conservent leurs licences respectives, copiées ou référencées dans le projet.
