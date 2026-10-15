@echo off
title VeriPass Development Environment Launcher
echo ======================================================
echo    Starting VeriPass Sovereign Trust Platform
echo ======================================================

echo [1/3] Checking PostgreSQL Service...
sc query postgresql-x64-18 | find "RUNNING" >nul
if %ERRORLEVEL% equ 0 (
    echo [OK] PostgreSQL 18 is running.
) else (
    echo [INFO] Starting PostgreSQL 18 service...
    net start postgresql-x64-18
)

echo.
echo [2/3] Starting Backend API (.NET 8)...
start "VeriPass Backend API (Port 5000)" cmd /k "cd /d %~dp0backend\src\VeriPass.API && dotnet watch run"

echo.
echo [3/3] Starting Frontend Web App (Angular 19)...
start "VeriPass Frontend Web (Port 4200)" cmd /k "cd /d %~dp0frontend\veripass-web && npm start"

echo.
echo ======================================================
echo Backend URL:  http://localhost:5000/swagger
echo Frontend URL: http://localhost:4200
echo ======================================================
echo Both servers are launching in separate windows!
timeout /t 5
