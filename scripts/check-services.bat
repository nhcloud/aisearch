@echo off
echo ===============================================
echo AI Search Solution - Service Status Check
echo ===============================================

echo.
echo Checking service status...

echo.
echo [1/2] Checking API Backend (https://localhost:7001)...
powershell -Command "try { $response = Invoke-WebRequest -Uri 'https://localhost:7001/swagger' -Method GET -TimeoutSec 5 -SkipCertificateCheck 2>$null; Write-Host '? API Backend is running' -ForegroundColor Green } catch { Write-Host '? API Backend is not running' -ForegroundColor Red }"

echo.
echo [2/2] Checking Web Frontend (https://localhost:7002)...
powershell -Command "try { $response = Invoke-WebRequest -Uri 'https://localhost:7002' -Method GET -TimeoutSec 5 -SkipCertificateCheck 2>$null; Write-Host '? Web Frontend is running' -ForegroundColor Green } catch { Write-Host '? Web Frontend is not running' -ForegroundColor Red }"

echo.
echo Alternative HTTP endpoints check:
echo.
echo Checking API Backend (http://localhost:5000)...
powershell -Command "try { $response = Invoke-WebRequest -Uri 'http://localhost:5000' -Method GET -TimeoutSec 5; Write-Host '? API Backend HTTP is running' -ForegroundColor Green } catch { Write-Host '? API Backend HTTP is not running' -ForegroundColor Red }"

echo.
echo Checking Web Frontend (http://localhost:5002)...
powershell -Command "try { $response = Invoke-WebRequest -Uri 'http://localhost:5002' -Method GET -TimeoutSec 5; Write-Host '? Web Frontend HTTP is running' -ForegroundColor Green } catch { Write-Host '? Web Frontend HTTP is not running' -ForegroundColor Red }"

echo.
echo ===============================================
echo Service URLs:
echo   ?? Web Frontend:    https://localhost:7002
echo   ?? API Backend:     https://localhost:7001
echo   ?? Swagger UI:      https://localhost:7001/swagger
echo ===============================================
echo.
echo To start services: run 'start.bat' from the root directory
echo.
pause