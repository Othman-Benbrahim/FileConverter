@echo off
setlocal EnableExtensions DisableDelayedExpansion

if "%~1"=="" goto usage

set "PFX_PASSWORD=%~1"
set "SCRIPT_DIR=%~dp0"
set "PFX_PATH=%SCRIPT_DIR%FileConverter-Development.pfx"
set "CER_PATH=%SCRIPT_DIR%FileConverter-Development.cer"

echo This creates a local DEVELOPMENT certificate and adds its public key to
echo LocalMachine\TrustedPeople and LocalMachine\Root. Run this command from
echo an administrator cmd. Do not distribute this certificate or its PFX.
choice /c YN /n /m "Continue [Y/N]? "
if errorlevel 2 exit /b 2

powershell.exe -NoLogo -NoProfile -NonInteractive -Command ^
 "$principal=New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent()); if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { exit 5 }"
if errorlevel 1 goto administrator_required

powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ^
 "$ErrorActionPreference='Stop';" ^
 "$password=ConvertTo-SecureString $env:PFX_PASSWORD -AsPlainText -Force;" ^
 "$cert=New-SelfSignedCertificate -Subject 'CN=File Converter Community Build' -FriendlyName 'File Converter development signing' -Type Custom -KeyUsage DigitalSignature -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddYears(3) -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3');" ^
 "Export-PfxCertificate -Cert $cert -FilePath $env:PFX_PATH -Password $password -Force | Out-Null;" ^
 "Export-Certificate -Cert $cert -FilePath $env:CER_PATH -Force | Out-Null;" ^
 "Import-Certificate -FilePath $env:CER_PATH -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null;" ^
 "Import-Certificate -FilePath $env:CER_PATH -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null" || exit /b 3

echo Development certificate created:
echo   %PFX_PATH%
echo   %CER_PATH%
exit /b 0

:administrator_required
echo ERROR: open cmd as administrator, then run this script again.
exit /b 5

:usage
echo Usage: create-development-certificate.cmd ^<pfx-password^>
exit /b 1
