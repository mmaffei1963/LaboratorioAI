@echo off
REM Crea sul Desktop i collegamenti di Tracciato (una volta sola).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Crea-Collegamenti-Desktop-Tracciato.ps1"
pause
