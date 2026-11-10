@echo off
setlocal
cd /d "%~dp0UrlScreenshotter"
dotnet build -c Release
if errorlevel 1 (
  echo.
  echo Build failed. Install the .NET 10 SDK and try again.
  pause
  exit /b 1
)
echo.
echo Build succeeded.
pause
