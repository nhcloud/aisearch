@echo off
echo Building AI Search MVC Frontend...

echo.
echo Restoring NuGet packages...
dotnet restore

echo.
echo Building project...
dotnet build --configuration Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build completed successfully!
    echo.
    echo To run the application:
    echo   dotnet run
    echo.
    echo To publish for deployment:
    echo   dotnet publish -c Release -o ./publish
) else (
    echo.
    echo Build failed with errors.
    exit /b %ERRORLEVEL%
)
