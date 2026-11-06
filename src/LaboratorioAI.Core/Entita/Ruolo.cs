namespace LaboratorioAI.Core.Entita;

/// <summary>Insieme nominato di permessi assegnabile agli utenti.</summary>
public class Ruolo
{
    public int IdRuolo { get; set; }

    /// <summary>Codice stabile, usato nel codice applicativo. Vedi <see cref="Costanti.Ruoli"/>.</summary>
    public string Codice { get; set; } = null!;

    public string Nome { get; set; } = null!;
    public string? Descrizione { get; set; }

    /// <summary>I ruoli di sistema non possono essere eliminati dall'interfaccia.</summary>
    public bool DiSistema { get; set; }

    /// <summary>Falso per un ruolo non più usato (es. «Operatore» dall'11/10/2026): non si assegna più, resta nello storico.</summary>
    public bool Attivo { get; set; } = true;

    /// <summary>Ordine di presentazione nella pagina Utenti e ruoli.</summary>
    public int Ordine { get; set; }

    public ICollection<UtenteRuolo> Utenti { get; set; } = new List<UtenteRuolo>();
    public ICollection<RuoloPermesso> Permessi { get; set; } = new List<RuoloPermesso>();
}
