# Correctif 3–4 : détection des scans et OCR local

Ce paquet s'applique par-dessus la précédente version ajoutant **PDF vers DOCX/TXT**. Il contient uniquement les fichiers à remplacer ou à ajouter.

## Installation des fichiers

1. Fermer Visual Studio et File Converter.
2. Extraire le ZIP.
3. Copier tout le contenu du dossier extrait à la racine du projet `FileConverterModif`.
4. Accepter la fusion des dossiers et le remplacement des fichiers.

L'arborescence du ZIP correspond directement à celle du projet : ne pas déplacer les fichiers individuellement dans d'autres dossiers.

## Fonctionnement ajouté

- Une extraction TXT provisoire mesure la présence d'une couche texte exploitable.
- À partir de 20 caractères alphanumériques, la conversion normale est conservée.
- En dessous de ce seuil, le PDF est considéré comme numérisé et l'OCR local est lancé automatiquement.
- PDF vers TXT utilise le périphérique Ghostscript `ocr` en UTF-8.
- PDF vers DOCX crée d'abord un PDF OCR local avec `pdfocr24`, puis le convertit avec `docxwrite`.
- Langues OCR embarquées : français et anglais (`fra+eng`), résolution 300 dpi.
- Aucun document ni contenu OCR n'est envoyé sur Internet.

## Fichiers à remplacer

- `Application/FileConverter/ConversionJobs/ConversionJob_Ghostscript.cs`
- `Application/FileConverter/FileConverter.csproj`
- `Application/FileConverter/Properties/AssemblyInfo.cs`
- `Application/FileConverter/Properties/Resources.Designer.cs`
- `Application/FileConverter/Properties/Resources.en.resx`
- `Application/FileConverter/Properties/Resources.fr-FR.resx`
- `Application/FileConverter/Properties/Resources.resx`
- `Installer/Installer.wixproj`
- `Installer/Product.wxs`
- `Middleware/gs/gsdll64.dll`
- `Middleware/gs/gswin64c.exe`
- `README.md`

## Fichiers à ajouter

- `Middleware/gs/COPYING`
- `Middleware/gs/tessdata/eng.traineddata`
- `Middleware/gs/tessdata/fra.traineddata`
- `Middleware/gs/tessdata/LICENSE`
- `OCR_LOCAL_INSTALLATION_ET_MSI_FR.md`

Le numéro de version de l'application et du MSI passe à **2.2.1**.

## Générer le MSI uniquement avec cmd

Prérequis : Visual Studio 2022, la charge de travail Développement Desktop .NET, Roslyn et le Targeting Pack .NET Framework 4.8.

Ouvrir un nouveau `cmd`, puis exécuter cette commande unique :

```bat
call "%ProgramFiles%\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 -host_arch=x64 && cd /d "%USERPROFILE%\Desktop\FileConverterModif" && msbuild FileConverter.sln /restore /m /p:Configuration=Release /p:Platform=x64
```

Adapter uniquement le chemin après `cd /d` si le projet est ailleurs.

Le MSI est créé dans :

```text
Installer\bin\x64\Release\FileConverter-setup.msi
```

Le script de post-compilation accepte désormais les chemins contenant des espaces. En l'absence du fichier privé `Installer/Installer.sign`, le MSI Release est généré sans signature.

## Vérifications avant publication

Tester au minimum :

1. un PDF texte vers TXT ;
2. un PDF texte vers DOCX ;
3. un PDF scanné français vers TXT ;
4. un PDF scanné français vers DOCX ;
5. l'annulation pendant un OCR multipage ;
6. la présence de `tessdata\eng.traineddata` et `tessdata\fra.traineddata` dans `C:\Program Files\File Converter` après installation.

Ghostscript a été remplacé par la version officielle **10.07.1**, car la version 10.02.1 précédemment embarquée est antérieure à des correctifs de sécurité concernant l'OCR. Les données linguistiques proviennent du projet officiel `tesseract-ocr/tessdata_fast`.
