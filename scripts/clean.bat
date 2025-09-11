@echo off
echo ===============================================
echo Cleaning AI Search Solution (.NET 9)
echo ===============================================

echo.
echo Cleaning solution...
dotnet clean AISearch.sln

echo.
echo Removing bin and obj directories...
for /d /r . %%d in (bin obj) do @if exist "%%d" rd /s /q "%%d" 2>nul

echo.
echo ? Clean completed successfully!
echo.
echo Next step: Run build.bat to rebuild the solution
echo.
pause