/*───────────────────────────────────────────────────────────────────────────
  Setup completo dell'import ArcLab in un colpo solo.
  Richiama gli script dei singoli moduli (controlli + accettazione).

  ⚠ RICHIEDE la "Modalità SQLCMD" attiva in SSMS:
     menu  Query → Modalità SQLCMD  (l'icona !=SQLCMD nella barra deve essere attiva)
  Senza quella modalità, i comandi ":r" danno errore di sintassi.

  Eseguire su LabAnalisiDati dopo ogni refresh della copia.
  In produzione basta eseguirlo una volta.
───────────────────────────────────────────────────────────────────────────*/
:r C:\Sviluppo\LaboratorioAI\scripts\sql\ImportArcLab_Controlli.sql
:r C:\Sviluppo\LaboratorioAI\scripts\sql\ImportArcLab_Accettazioni.sql
:r C:\Sviluppo\LaboratorioAI\scripts\sql\ImportArcLab_CertificatiEsterni.sql
GO
PRINT 'Setup import ArcLab completato: proc dbo.Importa_Tutto pronta.';
GO
