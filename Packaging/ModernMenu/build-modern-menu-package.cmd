@echo off
setlocal EnableExtensions DisableDelayedExpansion

if "%~1"=="" goto usage
if "%~2"=="" goto usage

set "PFX_PATH=%~f1"
set "PFX_PASSWORD=%~2"
set "TIMESTAMP_URL=%~3"
set "SCRIPT_DIR=%~dp0"
set "PACKAGE_ROOT=%SCRIPT_DIR%obj\package"
set "PACKAGE_OUTPUT=%SCRIPT_DIR%bin\FileConverter.ModernMenu.msix"
set "EXPECTED_SUBJECT=CN=File Converter Community Build"

if not exist "%PFX_PATH%" goto missing_certificate
if not defined WindowsSdkDir goto missing_sdk_environment
if not defined WindowsSDKVersion goto missing_sdk_version

set "SDK_BIN=%WindowsSdkDir%bin\%WindowsSDKVersion%x64"
if not exist "%SDK_BIN%\makeappx.exe" goto missing_makeappx
if not exist "%SDK_BIN%\signtool.exe" goto missing_signtool

powershell.exe -NoLogo -NoProfile -NonInteractive -Command "$pfx=[System.Security.Cryptography.X509Certificates.X509Certificate2]::new($env:PFX_PATH,$env:PFX_PASSWORD); if ($pfx.Subject -ine $env:EXPECTED_SUBJECT) { Write-Error ('Certificate subject is ' + $pfx.Subject + '; expected ' + $env:EXPECTED_SUBJECT); exit 4 }"
if errorlevel 1 goto invalid_certificate

if exist "%PACKAGE_ROOT%" rmdir /s /q "%PACKAGE_ROOT%"
mkdir "%PACKAGE_ROOT%\Assets"
if errorlevel 1 goto staging_error
if not exist "%SCRIPT_DIR%bin" mkdir "%SCRIPT_DIR%bin"
if errorlevel 1 goto staging_error

copy /y "%SCRIPT_DIR%AppxManifest.xml" "%PACKAGE_ROOT%\AppxManifest.xml" >nul
if errorlevel 1 goto staging_error
copy /y "%SCRIPT_DIR%Assets\StoreLogo.png" "%PACKAGE_ROOT%\Assets\StoreLogo.png" >nul
if errorlevel 1 goto staging_error
copy /y "%SCRIPT_DIR%Assets\Square44x44Logo.png" "%PACKAGE_ROOT%\Assets\Square44x44Logo.png" >nul
if errorlevel 1 goto staging_error
copy /y "%SCRIPT_DIR%Assets\Square150x150Logo.png" "%PACKAGE_ROOT%\Assets\Square150x150Logo.png" >nul
if errorlevel 1 goto staging_error

"%SDK_BIN%\makeappx.exe" pack /d "%PACKAGE_ROOT%" /p "%PACKAGE_OUTPUT%" /o /nv
if errorlevel 1 exit /b 6
if defined TIMESTAMP_URL (
    "%SDK_BIN%\signtool.exe" sign /fd SHA256 /td SHA256 /tr "%TIMESTAMP_URL%" /f "%PFX_PATH%" /p "%PFX_PASSWORD%" "%PACKAGE_OUTPUT%"
) else (
    "%SDK_BIN%\signtool.exe" sign /fd SHA256 /f "%PFX_PATH%" /p "%PFX_PASSWORD%" "%PACKAGE_OUTPUT%"
)
if errorlevel 1 exit /b 7
"%SDK_BIN%\signtool.exe" verify /pa /v "%PACKAGE_OUTPUT%"
if errorlevel 1 echo WARNING: SignTool does not trust the self-signed development certificate. The package remains signed and can be registered when its CER is installed in TrustedPeople.

echo Modern menu package created: %PACKAGE_OUTPUT%
exit /b 0

:missing_certificate
echo ERROR: certificate not found: %PFX_PATH%
exit /b 2

:missing_sdk_environment
echo ERROR: WindowsSdkDir is not defined. Run this script through build-release.cmd.
exit /b 3

:missing_sdk_version
echo ERROR: WindowsSDKVersion is not defined. Install the Windows 10/11 SDK.
exit /b 3

:missing_makeappx
echo ERROR: makeappx.exe was not found in %SDK_BIN%.
exit /b 3

:missing_signtool
echo ERROR: signtool.exe was not found in %SDK_BIN%.
exit /b 3

:invalid_certificate
echo ERROR: the signing certificate does not match the package publisher or its password is invalid.
exit /b 4

:staging_error
echo ERROR: failed to prepare the modern menu package files.
exit /b 5

:usage
echo Usage: build-modern-menu-package.cmd ^<certificate.pfx^> ^<password^> [timestamp-url]
exit /b 1
