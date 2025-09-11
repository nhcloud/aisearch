@echo off
echo ===============================================
echo Publishing AI Search Solution (.NET 9)
echo ===============================================

set OUTPUT_DIR=publish
set CONFIG=Release

echo.
echo Creating output directory: %OUTPUT_DIR%
if not exist %OUTPUT_DIR% mkdir %OUTPUT_DIR%

echo.
echo [1/2] Publishing AISearch.Api...
dotnet publish dotnet\AISearch.Api\AISearch.Api.csproj -c %CONFIG% -o %OUTPUT_DIR%\api --self-contained false
if %errorlevel% neq 0 (
    echo ERROR: API publish failed!
    pause
    exit /b %errorlevel%
)

echo.
echo [2/2] Publishing AISearch.Web...
dotnet publish dotnet\AISearch.Web\AISearch.Web.csproj -c %CONFIG% -o %OUTPUT_DIR%\web --self-contained false
if %errorlevel% neq 0 (
    echo ERROR: Web publish failed!
    pause
    exit /b %errorlevel%
)

echo.
echo ===============================================
echo ? Publish completed successfully!
echo ===============================================
echo.
echo Published applications:
echo   ?? API Backend:  %OUTPUT_DIR%\api\
echo   ?? Web Frontend: %OUTPUT_DIR%\web\
echo.
echo Deployment instructions:
echo   1. Copy published folders to your production server
echo   2. Configure production appsettings.json files
echo   3. Set up IIS or use dotnet hosting
echo   4. Update Azure service endpoints
echo.
echo For self-contained deployment (includes .NET runtime):
echo   dotnet publish -c Release --self-contained true -r win-x64
echo.
pause