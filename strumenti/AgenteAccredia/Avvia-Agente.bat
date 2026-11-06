@echo off
REM Esegue l'agente ACCREDIA una volta e chiude. Per una prova manuale, doppio clic.
REM Per l'esecuzione automatica quotidiana, vedi le istruzioni ricevute per creare
REM un'attivita pianificata di Windows che lancia il .exe pubblicato.
cd /d "%~dp0"
dotnet run -c Release
pause
