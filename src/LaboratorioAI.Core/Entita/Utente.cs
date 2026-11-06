namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Utente applicativo interno.
///
/// L'identità è gestita da Entra ID: qui si conservano solo il collegamento
/// all'account aziendale e i dati applicativi. Il sistema non gestisce password.
/// </summary>
public class Utente
{
    public int IdUtente { get; set; }

    /// <summary>
    /// Identificativo univoco dell'account su Entra ID (claim <c>oid</c>). Nullo per una persona registrata in anticipo
    /// dall'Amministratore (11/10/2026): al primo accesso con quella e-mail il collegamento si completa e la persona
    /// trova già i suoi ruoli.
    /// </summary>
    public Guid? ObjectIdEntra { get; set; }

    public string Email { get; set; } = null!;
    public string NomeCompleto { get; set; } = null!;

    /// <summary>
    /// Un utente disattivato non può accedere ma resta referenziato
    /// dalle analisi e dalle voci di audit già registrate.
    /// </summary>
    public bool Attivo { get; set; } = true;

    public DateTime DataCreazione { get; set; }
    public DateTime? DataUltimoAccesso { get; set; }

    /// <summary>Annotazioni dell'Amministratore (es. chi usa una casella condivisa come stafflab@ o info@).</summary>
    public string? Note { get; set; }

    public ICollection<UtenteRuolo> Ruoli { get; set; } = new List<UtenteRuolo>();
    public ICollection<AnalisiAi> Analisi { get; set; } = new List<AnalisiAi>();
}
