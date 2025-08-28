@echo off
echo Building AI Search Solution...

echo.
echo Building Backend...
cd src\backend\AISearch.Api
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo Backend build failed!
    pause
    exit /b %errorlevel%
)

echo.
echo Installing Frontend Dependencies...
cd ..\..\..\src\frontend
call npm install
if %errorlevel% neq 0 (
    echo Frontend dependency installation failed!
    pause
    exit /b %errorlevel%
)

echo.
echo Building Frontend...
call npm run build
if %errorlevel% neq 0 (
    echo Frontend build failed!
    pause
    exit /b %errorlevel%
)

echo.
echo Build completed successfully!
echo.
echo Next steps:
echo 1. Configure Azure services (see SETUP.md)
echo 2. Update appsettings.json with your Azure endpoints
echo 3. Run start.bat to launch the application
echo.
pause
