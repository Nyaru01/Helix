@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo Helix Blast v0.3.0 - Portable Windows build
echo Bundled NCBI BLAST+ - no WSL required
echo ============================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: The .NET SDK was not found.
    echo Install the .NET 8 SDK for Windows, then run this file again.
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

where powershell.exe >nul 2>nul
if errorlevel 1 (
    echo ERROR: Windows PowerShell was not found.
    pause
    exit /b 1
)

echo Preparing official NCBI BLAST+ Windows binaries...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0prepare-blast.ps1"
if errorlevel 1 goto :error

echo.
echo Restoring NuGet packages...
dotnet restore LocalBlast.Wpf.csproj
if errorlevel 1 goto :error

echo.
echo Publishing a self-contained Windows x64 executable...
dotnet publish LocalBlast.Wpf.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishReadyToRun=false

if errorlevel 1 goto :error

set "PUBLISH_DIR=bin\Release\net8.0-windows\win-x64\publish"
if not exist "%PUBLISH_DIR%\HelixBlast.exe" goto :error

copy /Y "%PUBLISH_DIR%\HelixBlast.exe" "HelixBlast.exe" >nul

echo.
echo ============================================================
echo SUCCESS
echo %CD%\HelixBlast.exe
echo.
echo This EXE includes:
echo - Helix Blast WPF interface
echo - self-contained .NET runtime
echo - NCBI BLAST+ 2.17.0 Windows x64: blastn, blastp, tblastn
echo.
echo WSL, Ubuntu, Python and a separate BLAST installation are NOT required.
echo ============================================================
if not defined LOCALBLAST_NO_PAUSE pause
exit /b 0

:error
echo.
echo BUILD FAILED. Read the error shown above.
if not defined LOCALBLAST_NO_PAUSE pause
exit /b 1
