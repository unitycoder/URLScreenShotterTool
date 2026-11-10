@echo off
setlocal
cd /d "%~dp0UrlScreenshotter"
dotnet publish -c Release -r win-x64 --self-contained true -o "%~dp0publish\win-x64"
if errorlevel 1 (
  echo.
  echo Publish failed. Install the .NET 10 SDK and try again.
  pause
  exit /b 1
)
echo.
echo Published to publish\win-x64
start "" "%~dp0publish\win-x64"
pause
