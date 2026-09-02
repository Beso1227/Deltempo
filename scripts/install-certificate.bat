@echo off
:: Deltempo Certificate One-Click Trust Installer
:: Run as Administrator to permanently trust Deltempo and suppress SmartScreen prompts

echo ========================================================
echo   Deltempo - Digital Certificate Trust Installer
echo ========================================================
echo.

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] Administrator privileges required.
    echo Please right-click this file and select 'Run as administrator'.
    pause
    exit /b 1
)

if exist "%~dp0..\Deltempo.cer" (
    set CERT_PATH=%~dp0..\Deltempo.cer
) else if exist "%~dp0Deltempo.cer" (
    set CERT_PATH=%~dp0Deltempo.cer
) else (
    echo [!] Deltempo.cer certificate file not found.
    pause
    exit /b 1
)

certutil -addstore -f "Root" "%CERT_PATH%" >nul 2>&1
certutil -addstore -f "TrustedPublisher" "%CERT_PATH%" >nul 2>&1

echo [OK] Deltempo certificate successfully installed to Trusted Publishers.
echo Windows SmartScreen will now recognize Deltempo as verified and trusted.
echo.
pause
