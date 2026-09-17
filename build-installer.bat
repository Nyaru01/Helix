@echo off
setlocal
cd /d "%~dp0"

set "LOCALBLAST_NO_PAUSE=1"
call "%~dp0build-exe.bat"
if errorlevel 1 exit /b 1

where iscc >nul 2>nul
if not errorlevel 1 (
  set "ISCC=iscc"
) else if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
  set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
) else (
  echo.
  echo Inno Setup 6 is required to create the installer.
  echo Install it from https://jrsoftware.org/isdl.php then run this script again.
  pause
  exit /b 1
)

"%ISCC%" "%~dp0installer\HelixBlast.iss"
if errorlevel 1 (
  echo Installer build failed.
  pause
  exit /b 1
)

echo.
echo Installer ready in %CD%\dist
pause
