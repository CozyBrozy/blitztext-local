@echo off
setlocal
set "APP=%LOCALAPPDATA%\Programs\BlitztextLocal\Blitztext.Windows.exe"
if not exist "%APP%" (
  echo Bitte zuerst setup.ps1 ausfuehren. Siehe ANLEITUNG.md.
  exit /b 1
)
start "" "%APP%"
