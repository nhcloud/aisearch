@echo off
echo Building AISearch.Core (.NET 9)...

cd ..\dotnet\AISearch.Core

echo.
echo Restoring packages...
dotnet restore

echo.
echo Building Core library...
dotnet build --configuration Release

if %errorlevel% equ 0 (
    echo.
    echo ? AISearch.Core build completed successfully!
) else (
    echo.
    echo ? AISearch.Core build failed!
)

cd ..\..\scripts
pause