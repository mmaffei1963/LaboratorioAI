namespace LaboratorioAI.Core.Entita;

/// <summary>Associazione fra utente e ruolo.</summary>
public class UtenteRuolo
{
    public int IdUtente { get; set; }
    public Utente Utente { get; set; } = null!;

    public int IdRuolo { get; set; }
    public Ruolo Ruolo { get; set; } = null!;

    public DateTime DataAssegnazione { get; set; }

    /// <summary>Utente che ha effettuato l'assegnazione. Nullo se assegnata dal sistema.</summary>
    public int? IdUtenteAssegnante { get; set; }
}
