namespace LaboratorioAI.Core.Entita;

public enum StatoGiudizioDaRiportare
{
    /// <summary>Validato in LaboratorioAI, in attesa che Palomar lo importi in ArcLab.</summary>
    DaRiportare = 1,
    /// <summary>In ArcLab c'è il giudizio validato e AnalisiChiusa = 1.</summary>
    Riportato = 2,
    /// <summary>Annullato prima dell'importazione (nessuna cancellazione fisica).</summary>
    Annullato = 3,
    /// <summary>In ArcLab c'è un giudizio diverso da quello validato: da controllare.</summary>
    Conflitto = 4
}

/// <summary>
/// Validazione di un campione fatta in LaboratorioAI (18/10/2026): giudizio da riportare in ArcLab (GiudCamp) con la
/// chiusura dell'analisi (Campione.AnalisiChiusa). LaboratorioAI scrive solo qui (schema «scambio»); Palomar legge le righe
/// «Da riportare» e le importa in ArcLab. Una sola riga non annullata per campione (indice univoco filtrato).
/// Definizione: claude/definizione-validazione-giudizio.md.
/// </summary>
public class GiudizioDaRiportare
{
    public int IdGiudizioDaRiportare { get; set; }
    public int IdCampione { get; set; }
    public string? NumeroCampione { get; set; }
    public int IdGiudizio { get; set; }
    public string? CodGiudizio { get; set; }
    public string? GiudizioTesto { get; set; }
    /// <summary>Giudizio 212 messo in automatico (cliente senza richiesta di giudizio e nessuna scelta).</summary>
    public bool Automatico { get; set; }
    /// <summary>Colore dei limiti normativi al momento della validazione (viola, rosso, …).</summary>
    public string? ColoreLimiti { get; set; }
    public bool ConfermaColore { get; set; }
    public string ValidatoDa { get; set; } = "";
    public int? IdUtenteValidazione { get; set; }
    public DateTime DataValidazione { get; set; }
    public StatoGiudizioDaRiportare Stato { get; set; }
    public DateTime? DataRiportato { get; set; }
    public string? AnnullatoDa { get; set; }
    public DateTime? DataAnnullamento { get; set; }
    public string? Nota { get; set; }
}
