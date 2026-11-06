@echo off
rem Agente fonti normative: prova manuale (vedi AgenteFonti.csproj).
rem Esempi:  Avvia-Agente.bat                                  (server di produzione, tutte le fonti)
rem          Avvia-Agente.bat --server http://localhost:5080   (LaboratorioAI in sviluppo)
cd /d "%~dp0"
dotnet run -- %*
pause
