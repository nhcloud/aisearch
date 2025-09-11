@echo off
echo Building AISearch.Web (.NET 9)...

cd ..\dotnet\AISearch.Web

echo.
echo Restoring packages...
dotnet restore

echo.
echo Building Razor Pages frontend...
dotnet build --configuration Release

if %errorlevel% equ 0 (
    echo.
    echo ? AISearch.Web build completed successfully!
    echo.
    echo To run the Web application:
    echo   dotnet run
    echo.
    echo Web app will be available at:
    echo   https://localhost:7002
) else (
    echo.
    echo ? AISearch.Web build failed!
)

cd ..\..\scripts
pause