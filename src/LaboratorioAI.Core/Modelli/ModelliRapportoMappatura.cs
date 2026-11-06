namespace LaboratorioAI.Core.Modelli;

/// <summary>Dati per generare il Rapporto di Mappatura di un'apparecchiatura termostatica (Modulo 04-03).</summary>
public record DatiRapportoMappatura
{
    public string NumeroRapporto { get; init; } = "";
    public DateOnly DataMappatura { get; init; }
    public DateOnly? DataScadenza { get; init; }
    public string? RiferimentoIstruzione { get; init; }
    public string RedattoDa { get; init; } = "";
    public string? Committente { get; init; }
    /// <summary>Insediamento del cliente (03/11/2026): stampato in testata solo se indicato.</summary>
    public string? Insediamento { get; init; }

    public string ApparecchiaturaIdentificazione { get; init; } = "";
    public string? ApparecchiaturaCodice { get; init; }

    public double ValoreNominale { get; init; }
    public double Tolleranza { get; init; }

    public string CdrIdentificazione { get; init; } = "";
    public string? CdrNumeroCertificato { get; init; }
    public DateOnly? CdrDataTaratura { get; init; }
    public DateOnly? CdrDataScadenza { get; init; }

    public required RisultatoMappatura Risultato { get; init; }
}
