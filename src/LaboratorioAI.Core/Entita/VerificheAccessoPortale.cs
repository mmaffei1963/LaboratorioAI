namespace LaboratorioAI.Core.Entita;

// Verifica del primo accesso da un browser nuovo (18/11/2026): chi entra con la partita IVA da un browser non ancora
// riconosciuto riceve un codice all'email del cliente in ArcLab. Si conservano solo impronte.

/// <summary>Browser riconosciuto per un cliente dopo la verifica via email (valido 180 giorni, revocabile).</summary>
public class DispositivoFidatoPortale
{
    public int IdDispositivoFidatoPortale { get; set; }
    public int IdClienteArcLab { get; set; }
    public string ImprontaToken { get; set; } = "";
    public DateTime DataCreazioneUtc { get; set; }
    public DateTime ScadenzaUtc { get; set; }
    public DateTime? UltimoUsoUtc { get; set; }
    public bool Revocato { get; set; }
    public DateTime? DataRevocaUtc { get; set; }
    public string? IndirizzoIp { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>Verifica in corso: codice inviato per email dopo credenziali corrette da un browser nuovo.</summary>
public class VerificaAccessoPortale
{
    public int IdVerificaAccessoPortale { get; set; }
    public int IdClienteArcLab { get; set; }
    public string CodiceContabilita { get; set; } = "";
    public string RagioneSociale { get; set; } = "";
    /// <summary>Impronta del valore casuale che lega la verifica al browser che l'ha chiesta.</summary>
    public string ImprontaToken { get; set; } = "";
    public string Sale { get; set; } = "";
    public string ImprontaCodice { get; set; } = "";
    public DateTime DataCreazioneUtc { get; set; }
    public DateTime ScadenzaUtc { get; set; }
    public int Tentativi { get; set; }
    public bool Usata { get; set; }
    public string? IndirizzoIp { get; set; }
}
