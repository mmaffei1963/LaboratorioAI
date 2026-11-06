# ============================================================
#  Tracciato — Variabili d'ambiente dei segreti (server)
#
#  Da eseguire UNA VOLTA sul server, in PowerShell come
#  AMMINISTRATORE, poi "iisreset".
#  I segreti NON stanno nel codice né nel repository.
#
#  >>> Sostituire i valori segnaposto prima di eseguire. <<<
# ============================================================

# --- Posta (Brevo SMTP): inviti, ordini ai fornitori --------
[Environment]::SetEnvironmentVariable("Posta__Host", "INSERIRE-HOST-SMTP", "Machine")
[Environment]::SetEnvironmentVariable("Posta__Porta", "587", "Machine")
[Environment]::SetEnvironmentVariable("Posta__Utente", "INSERIRE-UTENTE-SMTP", "Machine")
[Environment]::SetEnvironmentVariable("Posta__Password", "INSERIRE-PASSWORD-SMTP", "Machine")
[Environment]::SetEnvironmentVariable("Posta__MittenteEmail", "INSERIRE-MITTENTE-VERIFICATO", "Machine")

# --- Primo accesso: utente gestore MARE.A -------------------
#  Serve SOLO al primo avvio con il database vuoto: crea l'utente gestore.
#  Dopo il primo accesso queste tre variabili si possono togliere.
[Environment]::SetEnvironmentVariable("Tracciabilita__GestoreIniziale__Email", "INSERIRE-EMAIL-GESTORE", "Machine")
[Environment]::SetEnvironmentVariable("Tracciabilita__GestoreIniziale__Nome", "Gestore MARE.A", "Machine")
[Environment]::SetEnvironmentVariable("Tracciabilita__GestoreIniziale__Password", "INSERIRE-PASSWORD-GESTORE", "Machine")

Write-Host "Variabili impostate a livello di macchina."
Write-Host "Applicare con un riavvio di IIS:  iisreset"
