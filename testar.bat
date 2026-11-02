@echo off
cd /d "%~dp0"
dotnet test tests\Quina.Tests\Quina.Tests.csproj --logger "trx;LogFileName=quina-tests.trx" --results-directory TestResults
pause
