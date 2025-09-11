@echo off
echo ===============================================
echo Building AI Search Solution (.NET 9)
echo ===============================================

echo.
echo [1/2] Restoring NuGet packages for entire solution...
dotnet restore AISearch.sln
if %errorlevel% neq 0 (
    echo ERROR: Package restoration failed!
    pause
    exit /b %errorlevel%
)

echo.
echo [2/2] Building entire solution (Release configuration)...
dotnet build AISearch.sln --configuration Release --no-restore
if %errorlevel% neq 0 (
    echo ERROR: Solution build failed!
    pause
    exit /b %errorlevel%
)

echo.
echo ===============================================
echo ? Build completed successfully!
echo ===============================================
echo.
echo Built Projects:
echo   ? AISearch.Core    - Core library (.NET 9)
echo   ? AISearch.Api     - Web API backend (.NET 9)
echo   ? AISearch.Web     - Razor Pages frontend (.NET 9)
echo.
echo Next steps:
echo   1. Configure Azure services (see SETUP.md)
echo   2. Update appsettings.json files with your Azure endpoints:
echo      - dotnet\AISearch.Api\appsettings.json
echo      - dotnet\AISearch.Web\appsettings.json
echo   3. Run 'start.bat' to launch both API and Web applications
echo.
echo Available commands:
echo   - Full build:       build.bat
echo   - Start services:   start.bat
echo   - Run API only:     cd dotnet\AISearch.Api ^&^& dotnet run
echo   - Run Web only:     cd dotnet\AISearch.Web ^&^& dotnet run
echo   - Run tests:        dotnet test
echo   - Clean solution:   dotnet clean
echo   - Publish for prod: dotnet publish -c Release
echo.
echo Individual project builds available in scripts\ directory
echo.
pause
