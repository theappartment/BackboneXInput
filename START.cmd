@echo off
setlocal
cd /d "%~dp0windows-x64"
if not exist BackboneXInput.exe (
  echo Pacchetto Windows non trovato. Leggi README per compilare e pubblicare.
  pause
  exit /b 1
)
:menu
cls
echo BackboneXInput
echo.
echo 1 - Elenco controller
echo 2 - Seleziona controller fisico
echo 3 - Input Monitor (Ctrl+C per fermare)
echo 4 - Mapping Wizard
echo 5 - Diagnostica
echo 6 - Avvia Xbox virtuale (Ctrl+C per fermare)
echo 0 - Esci
echo.
choice /c 1234560 /n /m "Scegli: "
if errorlevel 7 exit /b 0
if errorlevel 6 (call :execute run & goto menu)
if errorlevel 5 (call :execute diagnose & goto menu)
if errorlevel 4 (call :execute wizard & goto menu)
if errorlevel 3 (call :execute monitor & goto menu)
if errorlevel 2 (call :execute select & goto menu)
if errorlevel 1 (call :execute devices & goto menu)
goto menu
:execute
BackboneXInput.exe %1
echo.
pause
exit /b
