namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Controllo di plausibilità AI sui valori nutrizionali di una ricetta.
/// Archiviato: la «Firma» rappresenta i valori verificati, così una ricetta
/// invariata non richiama il modello una seconda volta.
/// </summary>
public class VerificaNutrizionale
{
    public int IdVerifica { get; set; }

    public int IdRicetta { get; set; }
    public Ricetta? Ricetta { get; set; }

    public DateTime DataVerifica { get; set; }

    /// <summary>Impronta dei valori verificati (denominazione + dichiarazione per 100 g).</summary>
    public string Firma { get; set; } = null!;

    public string Denominazione { get; set; } = null!;

    /// <summary>Verdetto sintetico (prima riga della risposta).</summary>
    public string? Esito { get; set; }

    /// <summary>Testo completo della valutazione di plausibilità.</summary>
    public string Testo { get; set; } = null!;

    public string ModelloAi { get; set; } = null!;
    public int TokenInput { get; set; }
    public int TokenOutput { get; set; }
    public decimal? CostoStimato { get; set; }
    public string VersioneApplicazione { get; set; } = null!;

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }
}
