# ============================================================
#  Laboratorio AI — Variabili d'ambiente dei segreti (server)
#
#  Da eseguire UNA VOLTA sul server, in PowerShell come
#  AMMINISTRATORE. Imposta le variabili a livello di macchina,
#  lette dalle app in-process sotto IIS.
#
#  I segreti NON stanno nel codice né nel repository: vivono
#  solo qui, nelle variabili d'ambiente del server.
#
#  >>> Sostituire i valori segnaposto prima di eseguire. <<<
# ============================================================

# --- Web app: analisi AI (Anthropic) -------------------------
[Environment]::SetEnvironmentVariable("Anthropic__ChiaveApi", "sk-ant-INSERIRE-QUI", "Machine")

# --- Web app: autenticazione Entra ID ------------------------
#  TenantId e ClientId si leggono dalla registrazione dell'app
#  sul portale Entra ID / Azure.
[Environment]::SetEnvironmentVariable("EntraId__TenantId", "INSERIRE-TENANT-ID", "Machine")
[Environment]::SetEnvironmentVariable("EntraId__ClientId", "INSERIRE-CLIENT-ID", "Machine")

# --- Portale: credenziale accesso operatore ------------------
#  Password scelta per l'accesso operatore (codice MAREA).
#  Se lasciata vuota, l'accesso operatore resta disabilitato.
[Environment]::SetEnvironmentVariable("Portale__CredenzialeOperatore", "INSERIRE-PASSWORD-OPERATORE", "Machine")

Write-Host "Variabili impostate a livello di macchina."
Write-Host "Applicare con un riavvio di IIS:  iisreset"
