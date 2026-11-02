@echo off
cd /d "%~dp0"
dotnet run --project src\Quina.Desktop\Quina.Desktop.csproj
if errorlevel 1 pause
