namespace LaboratorioAI.Core.Modelli;

/// <summary>Dati per generare il Rapporto di Taratura dei termometri.</summary>
public record DatiRapportoTaratura
{
    // ── Testata ─────────────────────────────────────────────────────
    public string NumeroRapporto { get; init; } = "";
    public DateOnly DataTaratura { get; init; }
    public DateOnly? DataScadenza { get; init; }
    public string? RiferimentoIstruzione { get; init; }
    public string? RedattoDa { get; init; }
    public string? Committente { get; init; }
    /// <summary>Insediamento del cliente (03/11/2026): stampato in testata solo se indicato.</summary>
    public string? Insediamento { get; init; }

    // ── Strumento in taratura (UUT) ─────────────────────────────────
    public string StrumentoIdentificazione { get; init; } = "";
    public string? StrumentoCodice { get; init; }
    public double RisoluzioneStrumento { get; init; }

    // ── Campione di riferimento (CDR) ───────────────────────────────
    public string CdrIdentificazione { get; init; } = "";
    public string? CdrTipo { get; init; }
    public string? CdrCentroLat { get; init; }
    public string? CdrNumeroCertificato { get; init; }
    public DateOnly? CdrDataTaratura { get; init; }
    public DateOnly? CdrDataScadenza { get; init; }
    public double CdrRisoluzione { get; init; }
    public double CdrFattoreCopertura { get; init; } = 2.0;
    public IReadOnlyList<PuntoCertificatoCdr> Certificato { get; init; } = new List<PuntoCertificatoCdr>();

    // ── Risultati ───────────────────────────────────────────────────
    public RisultatoTaratura Risultato { get; init; } = new();
}
