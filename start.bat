@echo off
echo ===============================================
echo Starting AI Search Solution (.NET 9)
echo ===============================================

echo.
echo Starting services in the background...

echo.
echo [1/2] Starting AI Search API Backend...
cd dotnet\AISearch.Api
start "AI Search API" cmd /c "echo Starting API Backend... & dotnet run & pause"
echo API Backend starting in new window...
cd ..\..

echo.
echo Waiting for API to initialize...
timeout /t 5 /nobreak >nul

echo.
echo [2/2] Starting AI Search Web Frontend...
cd dotnet\AISearch.Web
start "AI Search Web" cmd /c "echo Starting Web Frontend... & dotnet run & pause"
echo Web Frontend starting in new window...
cd ..\..

echo.
echo ===============================================
echo ? Both services are starting...
echo ===============================================
echo.
echo Service URLs:
echo   ?? Web Frontend:    https://localhost:7002
echo   ?? API Backend:     https://localhost:7001
echo   ?? Swagger UI:      https://localhost:7001/swagger
echo.
echo Alternative HTTP URLs (if HTTPS issues):
echo   ?? Web Frontend:    http://localhost:5002
echo   ?? API Backend:     http://localhost:5000
echo.
echo Please wait for both services to fully start...
echo.
echo Service Status Check:
echo Checking API status...
powershell -Command "try { Start-Sleep -Seconds 10; $response = Invoke-WebRequest -Uri 'https://localhost:7001/swagger' -Method GET -TimeoutSec 10 -SkipCertificateCheck 2>$null; Write-Host '? API Backend is running and accessible' -ForegroundColor Green } catch { Write-Host '??  API Backend may still be starting - check the API window' -ForegroundColor Yellow }"

echo.
echo Next steps:
echo   1. Wait for both service windows to show "Now listening on..."
echo   2. Open https://localhost:7002 in your browser
echo   3. Sign in with your Azure AD credentials
echo   4. Start using the AI Search interface
echo.
echo To stop services: Close both command windows or press Ctrl+C in each
echo.
pause
