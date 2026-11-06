namespace LaboratorioAI.Core.Modelli;

/// <summary>Dati per generare il Rapporto di Taratura di un incubatore/cella (Modulo 04-02).</summary>
public record DatiRapportoIncubatore
{
    public string NumeroRapporto { get; init; } = "";
    public DateOnly DataTaratura { get; init; }
    public DateOnly? DataScadenza { get; init; }
    public string? RiferimentoIstruzione { get; init; }
    public string RedattoDa { get; init; } = "";
    public string? Committente { get; init; }
    /// <summary>Insediamento del cliente (03/11/2026): stampato in testata solo se indicato.</summary>
    public string? Insediamento { get; init; }

    // ── Strumenti ───────────────────────────────────────────────────
    public string MisuratoreIdentificazione { get; init; } = "";
    public string? MisuratoreCodice { get; init; }
    public string ApparecchiaturaIdentificazione { get; init; } = "";
    public string? ApparecchiaturaCodice { get; init; }

    // ── Condizioni ──────────────────────────────────────────────────
    public double ValoreNominale { get; init; }
    public double Tolleranza { get; init; }
    public double RisoluzioneStrumento { get; init; }

    // ── CDR ─────────────────────────────────────────────────────────
    public string CdrIdentificazione { get; init; } = "";
    public string? CdrCentroLat { get; init; }
    public string? CdrNumeroCertificato { get; init; }
    public DateOnly? CdrDataTaratura { get; init; }
    public DateOnly? CdrDataScadenza { get; init; }
    public double CdrRisoluzione { get; init; }

    public required RisultatoTaraturaIncubatore Risultato { get; init; }
}
