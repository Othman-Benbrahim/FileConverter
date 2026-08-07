@echo off
setlocal EnableExtensions DisableDelayedExpansion

if "%~1"=="" goto usage
if "%~2"=="" goto usage

set "PFX_PATH=%~f1"
set "PFX_PASSWORD=%~2"
set "ROOT_DIR=%~dp0"
set "VSDEVCMD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"

if not exist "%VSDEVCMD%" (
    echo ERROR: Visual Studio 2022 Community was not found.
    exit /b 2
)

call "%VSDEVCMD%" -arch=x64 -host_arch=x64 || exit /b 3
cd /d "%ROOT_DIR%" || exit /b 4

msbuild "FileConverter.sln" /restore /m /t:FileConverterModernMenu;FileConverter /p:Configuration=Release /p:Platform=x64 || exit /b 5
call "Packaging\ModernMenu\build-modern-menu-package.cmd" "%PFX_PATH%" "%PFX_PASSWORD%" || exit /b 7
msbuild "FileConverter.sln" /restore /m /t:Installer /p:Configuration=Release /p:Platform=x64 /p:IncludeModernMenu=1 || exit /b 8

echo.
echo Release created:
echo   %ROOT_DIR%Installer\bin\x64\Release\FileConverter-setup.msi
exit /b 0

:usage
echo Usage: build-release.cmd ^<certificate.pfx^> ^<password^>
exit /b 1
