@echo off
setlocal EnableExtensions DisableDelayedExpansion

if "%~1"=="" goto usage
if "%~2"=="" goto usage

set "PFX_PATH=%~f1"
set "PFX_PASSWORD=%~2"
set "TIMESTAMP_URL=%~3"
set "ROOT_DIR=%~dp0"
set "VSDEVCMD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"
set "MSI_PATH=%ROOT_DIR%Installer\bin\x64\Release\FileConverter-setup.msi"

if not exist "%VSDEVCMD%" (
    echo ERROR: Visual Studio 2022 Community was not found.
    exit /b 2
)

call "%VSDEVCMD%" -arch=x64 -host_arch=x64 || exit /b 3
cd /d "%ROOT_DIR%" || exit /b 4

msbuild "FileConverter.sln" /restore /m /t:FileConverterModernMenu;FileConverter /p:Configuration=Release /p:Platform=x64 || exit /b 5
call "Packaging\ModernMenu\build-modern-menu-package.cmd" "%PFX_PATH%" "%PFX_PASSWORD%" "%TIMESTAMP_URL%" || exit /b 7
msbuild "FileConverter.sln" /restore /m /t:Installer /p:Configuration=Release /p:Platform=x64 /p:IncludeModernMenu=1 || exit /b 8
if not exist "%MSI_PATH%" (
    echo ERROR: MSI was not created: %MSI_PATH%
    exit /b 9
)

set "SDK_BIN=%WindowsSdkDir%bin\%WindowsSDKVersion%x64"
if not exist "%SDK_BIN%\signtool.exe" (
    echo ERROR: signtool.exe was not found in %SDK_BIN%.
    exit /b 10
)

if defined TIMESTAMP_URL (
    "%SDK_BIN%\signtool.exe" sign /fd SHA256 /td SHA256 /tr "%TIMESTAMP_URL%" /f "%PFX_PATH%" /p "%PFX_PASSWORD%" "%MSI_PATH%" || exit /b 11
) else (
    "%SDK_BIN%\signtool.exe" sign /fd SHA256 /f "%PFX_PATH%" /p "%PFX_PASSWORD%" "%MSI_PATH%" || exit /b 11
)

"%SDK_BIN%\signtool.exe" verify /pa /v "%MSI_PATH%"
if errorlevel 1 echo WARNING: the MSI is signed, but its certificate chain is not trusted on this machine.

echo.
echo Release created:
echo   %MSI_PATH%
exit /b 0

:usage
echo Usage: build-release.cmd ^<certificate.pfx^> ^<password^> [timestamp-url]
exit /b 1
