@echo off
REM Build the portable single-file publish\TimeZoneCalc.exe and a zip of it. Run from cmd.exe.
cd /d "%~dp0.."
dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj
if errorlevel 1 exit /b 1
if exist publish rmdir /s /q publish
dotnet publish src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -c Release -p:Platform=x64 -o publish
if errorlevel 1 exit /b 1
REM tar instead of Compress-Archive: some system process may keep a file open, which Compress-Archive refuses
tar -a -c -f publish\TimeZoneCalc-win-x64.zip -C publish TimeZoneCalc.exe
if errorlevel 1 exit /b 1
echo Done: publish\TimeZoneCalc.exe and publish\TimeZoneCalc-win-x64.zip
