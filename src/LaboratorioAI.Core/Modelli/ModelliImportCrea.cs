namespace LaboratorioAI.Core.Modelli;

/// <summary>
/// Una riga del foglio «Etichetta_100g» del database CREA: valori già pronti per
/// 100 g, con il Sale già calcolato dal Sodio nel file di origine.
/// </summary>
public record RigaCrea
{
    public string Codice { get; init; } = "";
    public string Alimento { get; init; } = "";
    public string? Categoria { get; init; }

    public decimal? Acqua { get; init; }
    public decimal? EnergiaKj { get; init; }
    public decimal? EnergiaKcal { get; init; }
    public decimal? Grassi { get; init; }
    public decimal? GrassiSaturi { get; init; }
    public decimal? Carboidrati { get; init; }
    public decimal? Zuccheri { get; init; }
    public decimal? Fibre { get; init; }
    public decimal? Proteine { get; init; }
    public decimal? Sale { get; init; }

    public string? FonteUrl { get; init; }
}

/// <summary>Esito dell'importazione CREA.</summary>
public record EsitoImportCrea
{
    public int Lette { get; init; }
    public int Nuove { get; init; }
    public int Aggiornate { get; init; }
}
