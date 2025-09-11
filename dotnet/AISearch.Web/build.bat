@echo off
echo Building AISearch.Web (.NET 9 Razor Pages Frontend)...

echo.
echo Restoring NuGet packages...
dotnet restore

echo.
echo Building project...
dotnet build --configuration Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ? Build completed successfully!
    echo.
    echo To run the Razor Pages application:
    echo   dotnet run
    echo.
    echo Web app will be available at:
    echo   https://localhost:7002
    echo.
    echo For production deployment:
    echo   dotnet publish -c Release -o ./publish
    echo.
    echo Note: Make sure AISearch.Api is running for full functionality
) else (
    echo.
    echo ? Build failed with errors.
    exit /b %ERRORLEVEL%
)
