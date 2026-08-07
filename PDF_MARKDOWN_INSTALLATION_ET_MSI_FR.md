# Correctif 5–6 : fusion/découpage PDF et Markdown

Ce paquet s'applique par-dessus la version **2.2.1** intégrant PDF vers DOCX/TXT et l'OCR local. Il contient uniquement les fichiers à remplacer ou à ajouter.

## Installation

1. Fermer File Converter et Visual Studio.
2. Extraire le ZIP directement à la racine du projet complet, là où se trouve `FileConverter.sln`.
3. Accepter la fusion des dossiers et le remplacement des fichiers.

## Fonctions ajoutées

- `PDF tools/Merge PDFs` : sélectionner au moins deux PDF, puis utiliser le menu File Converter. Un fichier `Merged.pdf` est créé à côté du premier PDF. L'ordre fourni par l'Explorateur est conservé.
- `PDF tools/Split PDF` : produit un PDF par page avec des noms comme `document-page-1.pdf`.
- `To Markdown` : accepte PDF, TXT, DOC, DOCX et ODT. Les PDF scannés utilisent automatiquement l'OCR local français/anglais.
- Les conversions restent entièrement locales.

La conversion Markdown privilégie le texte : elle ne reproduit pas exactement les colonnes, images, tableaux complexes ou la mise en page graphique du document source. La conversion des formats Word nécessite Microsoft Word, comme les autres conversions Word du projet.

Ghostscript réécrit les PDF fusionnés et découpés. Une signature numérique existante ne restera donc pas valide ; conserver les originaux pour les documents signés ou dotés de fonctions PDF avancées.

## Fichiers à remplacer

- `Application/FileConverter/Application.xaml.cs`
- `Application/FileConverter/ConversionJobs/ConversionJobFactory.cs`
- `Application/FileConverter/ConversionJobs/ConversionJob_Ghostscript.cs`
- `Application/FileConverter/ConversionJobs/ConversionJob_Word.cs`
- `Application/FileConverter/ConversionPreset/ConversionPreset.cs`
- `Application/FileConverter/FileConverter.csproj`
- `Application/FileConverter/Helpers.cs`
- `Application/FileConverter/OutputType.cs`
- `Application/FileConverter/PathHelpers.cs`
- `Application/FileConverter/Properties/AssemblyInfo.cs`
- `Application/FileConverter/Properties/Resources.Designer.cs`
- `Application/FileConverter/Properties/Resources.en.resx`
- `Application/FileConverter/Properties/Resources.fr-FR.resx`
- `Application/FileConverter/Properties/Resources.resx`
- `Application/FileConverter/Settings.default.xml`
- `Application/FileConverter/ViewModels/OutputTypeViewModel.cs`
- `Application/FileConverter/ViewModels/SettingsViewModel.cs`
- `Application/FileConverter/Views/SettingsWindow.xaml`
- `Application/FileConverterExtension/ConversionPresetReference.cs`
- `Application/FileConverterExtension/FileConverterExtension.cs`
- `Installer/Installer.wixproj`
- `Installer/Product.wxs`
- `README.md`

## Fichiers à ajouter

- `Application/FileConverter/ConversionJobs/ConversionJob_GhostscriptBase.cs`
- `Application/FileConverter/ConversionJobs/ConversionJob_Markdown.cs`
- `Application/FileConverter/ConversionJobs/ConversionJob_PdfTools.cs`
- `Application/FileConverter/ConversionJobs/MarkdownTextConverter.cs`
- `PDF_MARKDOWN_INSTALLATION_ET_MSI_FR.md`

La version de l'application et du MSI devient **2.3.0**.

## Générer le MSI uniquement avec CMD

Dans un nouveau `cmd` :

```bat
call "%ProgramFiles%\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 -host_arch=x64
cd /d "C:\Users\othma\Desktop\FileConverterModif\FileConverter-integration"
msbuild FileConverter.sln /restore /t:Rebuild /m /p:Configuration=Release /p:Platform=x64
```

Le MSI sera généré dans :

```text
Installer\bin\x64\Release\FileConverter-setup.msi
```

Sans `Installer\Installer.sign`, le MSI Release est généré sans signature.

## Tests avant publication

1. fusionner deux PDF et vérifier leur ordre ;
2. découper un PDF de plusieurs pages ;
3. convertir un PDF texte en Markdown ;
4. convertir un PDF scanné français en Markdown ;
5. convertir un TXT et un DOCX en Markdown ;
6. tester l'annulation d'une fusion ou d'un découpage volumineux ;
7. installer le MSI et vérifier l'apparition des trois nouveaux presets dans le menu de l'Explorateur.
