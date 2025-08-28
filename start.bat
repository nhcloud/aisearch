@echo off
echo Starting AI Search Solution...

echo.
echo Starting .NET Backend...
start "Backend" cmd /k "cd /d src\backend\AISearch.Api && dotnet run"

echo.
echo Waiting for backend to start...
timeout /t 10 /nobreak

echo.
echo Starting React Frontend...
start "Frontend" cmd /k "cd /d src\frontend && npm start"

echo.
echo Both services are starting...
echo Backend will be available at: http://localhost:5000
echo Frontend will be available at: http://localhost:3000
echo Swagger UI will be available at: http://localhost:5000
echo.
pause
