namespace LaboratorioAI.Core.Entita;

// Password del portale clienti (17/11/2026, definizione in claude/definizione-password-portale.md).
// Tutto nel database LaboratorioAI: ArcLab resta in sola lettura (codice cliente e partita IVA).

/// <summary>
/// Password personale scelta dal cliente al posto della partita IVA. Si conserva solo l'impronta (PasswordHasher di
/// ASP.NET Core Identity). Al massimo una attiva per cliente; il ripristino dell'operatore la annulla (nessuna cancellazione fisica).
/// </summary>
public class PasswordCliente
{
    public int IdPasswordCliente { get; set; }
    public int IdClienteArcLab { get; set; }
    public string ImprontaPassword { get; set; } = "";
    /// <summary>«cambio» (dall'area riservata) o «recupero» (con il codice via email).</summary>
    public string Origine { get; set; } = "";
    public DateTime DataImpostazioneUtc { get; set; }
    public bool Annullata { get; set; }
    public DateTime? DataAnnullamentoUtc { get; set; }
    /// <summary>Motivo dell'annullamento: «sostituita» da una nuova password o «ripristino operatore».</summary>
    public string? MotivoAnnullamento { get; set; }
}

/// <summary>Codice temporaneo inviato per email per il recupero della password (si conserva solo l'impronta).</summary>
public class CodiceRecuperoPassword
{
    public int IdCodiceRecuperoPassword { get; set; }
    public int IdClienteArcLab { get; set; }
    public string Sale { get; set; } = "";
    public string ImprontaCodice { get; set; } = "";
    public DateTime DataCreazioneUtc { get; set; }
    public DateTime ScadenzaUtc { get; set; }
    public int Tentativi { get; set; }
    public bool Usato { get; set; }
    public string? IndirizzoIp { get; set; }
}

/// <summary>Registro di cambi, recuperi e ripristini della password (mai password o codici in chiaro).</summary>
public class EventoPasswordCliente
{
    public long IdEventoPasswordCliente { get; set; }
    public int? IdClienteArcLab { get; set; }
    public string? CodiceContabilita { get; set; }
    public string Evento { get; set; } = "";
    public DateTime DataUtc { get; set; }
    public string? IndirizzoIp { get; set; }
    public string? UserAgent { get; set; }
    public string? Dettaglio { get; set; }
}
