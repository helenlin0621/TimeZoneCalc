@echo off
REM Build both portable versions. Run from cmd.exe.
REM   publish\folder\TimeZoneCalc\TimeZoneCalc.exe  folder version (many files, no extraction)
REM   publish\single\TimeZoneCalc.exe               single-file version (extracts to %%TEMP%%\.net on first run)
REM   publish\TimeZoneCalc-folder-win-x64.zip / publish\TimeZoneCalc-single-win-x64.zip
cd /d "%~dp0.."
dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj
if errorlevel 1 exit /b 1
if exist publish rmdir /s /q publish
dotnet publish src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -c Release -p:Platform=x64 -p:PublishSingleFile=false -o publish\folder\TimeZoneCalc
if errorlevel 1 exit /b 1
dotnet publish src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -c Release -p:Platform=x64 -o publish\single
if errorlevel 1 exit /b 1
REM tar instead of Compress-Archive: some system process may keep a file open, which Compress-Archive refuses
tar -a -c -f publish\TimeZoneCalc-folder-win-x64.zip -C publish\folder TimeZoneCalc
if errorlevel 1 exit /b 1
tar -a -c -f publish\TimeZoneCalc-single-win-x64.zip -C publish\single TimeZoneCalc.exe
if errorlevel 1 exit /b 1
echo Done:
echo   folder version: publish\folder\TimeZoneCalc\  and  publish\TimeZoneCalc-folder-win-x64.zip
echo   single exe:     publish\single\TimeZoneCalc.exe  and  publish\TimeZoneCalc-single-win-x64.zip
