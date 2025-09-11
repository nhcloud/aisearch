@echo off
echo Starting AISearch.Web (.NET 9 Razor Pages Frontend)...

echo.
echo Checking for required configuration...
if not exist "appsettings.json" (
    echo WARNING: appsettings.json not found!
    echo Please configure Azure AD and API settings before running.
    echo See the main README.MD and SETUP.md for configuration instructions.
    pause
    exit /b 1
)

echo.
echo Starting Razor Pages frontend...
echo This will run on:
echo   - HTTPS: https://localhost:7002
echo   - HTTP:  http://localhost:5002
echo.
echo IMPORTANT: Make sure the API backend is running at https://localhost:7001
echo           Use the main start.bat script to start both services automatically.

dotnet run

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ? AISearch.Web started successfully!
) else (
    echo.
    echo ? AISearch.Web failed to start.
    echo Check the error messages above for details.
    pause
    exit /b %ERRORLEVEL%
)
