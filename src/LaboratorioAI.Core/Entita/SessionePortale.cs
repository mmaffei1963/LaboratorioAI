namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Sessione attiva di un cliente sul portale.
///
/// Persistita su database anziché in memoria: sopravvive ai riavvii
/// dell'Application Pool e permette la revoca puntuale di una sessione.
/// Non contiene in alcuna forma la credenziale usata per l'accesso.
/// </summary>
public class SessionePortale
{
    public int IdSessione { get; set; }

    /// <summary>Token opaco generato con generatore crittograficamente sicuro.</summary>
    public string Token { get; set; } = null!;

    /// <summary>Riferimento logico al cliente su ArcLab. Nullo per l'accesso operatore.</summary>
    public int? IdCliente { get; set; }

    public string? CodiceContabilita { get; set; }
    public string? RagioneSociale { get; set; }

    /// <summary>Sessione di operatore interno, con visibilità su tutti i clienti.</summary>
    public bool Operatore { get; set; }

    /// <summary>
    /// 19/11/2026: e-mail aziendale (Microsoft 365) dell'operatore che ha aperto la sessione, per sapere sempre chi è
    /// entrato. Nulla per i clienti e per il vecchio accesso con codice operatore.
    /// </summary>
    public string? EmailOperatore { get; set; }

    public DateTime DataCreazione { get; set; }
    public DateTime Scadenza { get; set; }
    public DateTime? DataRevoca { get; set; }

    public string? IndirizzoIp { get; set; }
    public string? UserAgent { get; set; }
}
