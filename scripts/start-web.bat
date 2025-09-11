@echo off
echo Starting AI Search Web Frontend (.NET 9)...

cd ..\dotnet\AISearch.Web

echo.
echo Checking for required configuration...
if not exist "appsettings.json" (
    echo WARNING: appsettings.json not found!
    echo Please configure Azure AD and API settings before running the web app.
    echo See SETUP.md for configuration instructions.
    pause
    exit /b 1
)

echo.
echo Starting Razor Pages frontend...
echo This will run on:
echo   - HTTPS: https://localhost:7002
echo   - HTTP:  http://localhost:5002
echo.
echo Make sure the API backend is running at https://localhost:7001

dotnet run

echo.
echo Web Frontend stopped.
cd ..\..\scripts
pause