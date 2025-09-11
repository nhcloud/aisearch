@echo off
echo Building AISearch.Api (.NET 9)...

cd ..\dotnet\AISearch.Api

echo.
echo Restoring packages...
dotnet restore

echo.
echo Building API backend...
dotnet build --configuration Release

if %errorlevel% equ 0 (
    echo.
    echo ? AISearch.Api build completed successfully!
    echo.
    echo To run the API:
    echo   dotnet run
    echo.
    echo API will be available at:
    echo   https://localhost:7001
    echo   Swagger UI: https://localhost:7001/swagger
) else (
    echo.
    echo ? AISearch.Api build failed!
)

cd ..\..\scripts
pause