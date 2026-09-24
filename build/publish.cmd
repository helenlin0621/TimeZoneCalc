@echo off
REM Build the portable folder publish\TimeZoneCalc and zip it. Run from cmd.exe.
cd /d "%~dp0.."
dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj
if errorlevel 1 exit /b 1
if exist publish\TimeZoneCalc rmdir /s /q publish\TimeZoneCalc
if exist publish\TimeZoneCalc-win-x64.zip del publish\TimeZoneCalc-win-x64.zip
dotnet publish src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -c Release -p:Platform=x64 -o publish\TimeZoneCalc
if errorlevel 1 exit /b 1
REM tar instead of Compress-Archive: some system process keeps one DLL open, which Compress-Archive refuses
tar -a -c -f publish\TimeZoneCalc-win-x64.zip -C publish TimeZoneCalc
if errorlevel 1 exit /b 1
echo Done: publish\TimeZoneCalc-win-x64.zip
