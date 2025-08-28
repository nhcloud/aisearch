@echo off
echo Starting AI Search MVC Frontend...

echo.
echo Checking if backend API is running...
powershell -Command "try { Invoke-WebRequest -Uri 'http://localhost:5000/api' -Method GET -TimeoutSec 5 | Out-Null; Write-Host 'Backend API is accessible' -ForegroundColor Green } catch { Write-Host 'Warning: Backend API is not accessible at http://localhost:5000' -ForegroundColor Yellow }"

echo.
echo Starting MVC application...
dotnet run --urls "https://localhost:7001;http://localhost:5001"
