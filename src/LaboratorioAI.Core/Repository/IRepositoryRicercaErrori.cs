namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Ricerca errori: verifiche diagnostiche (SOLA LETTURA) su ArcLab.
/// Nessuna scrittura: individua solo anomalie da correggere a mano nel gestionale.
/// </summary>
public interface IRepositoryRicercaErrori
{
    /// <summary>
    /// Rapporti di prova (certificati) in cui, sullo stesso campione, un IdParametro
    /// compare più volte con la stessa denominazione riga. Esclude i microbiologici
    /// (Parametro.IdTipoParametro = 2) e la coppia legittima del valore energetico
    /// (IdParametro 236 con esattamente due IdRigaMetodo diversi).
    /// </summary>
    Task<IReadOnlyList<RigaDuplicatoRapporto>> DuplicatiRapportiAsync(int annoDa, int annoA, CancellationToken ct = default);

    /// <summary>Metodi attivi con Cifre_significative e/o Decimali_Max non compilati (NULL).</summary>
    Task<IReadOnlyList<RigaMetodoArrotondamento>> MetodiSenzaArrotondamentoAsync(CancellationToken ct = default);

    /// <summary>
    /// Metodi attivi con margine insufficiente: (CostoMetodoEuro − CostoAnoi)/CostoMetodoEuro
    /// sotto la soglia indicata (frazione, es. 0,15 = 15%). Include i casi con CostoAnoi
    /// superiore a CostoMetodoEuro (margine negativo).
    /// </summary>
    Task<IReadOnlyList<RigaMetodoMargine>> MetodiMargineAsync(decimal sogliaFrazione, CancellationToken ct = default);

    /// <summary>
    /// Metodi attivi potenzialmente doppi: stesso IdParametro, stessa DettagliMetodo e
    /// stessa RifMetodo (al netto di spazi iniziali/finali e maiuscole/minuscole).
    /// </summary>
    Task<IReadOnlyList<RigaMetodoDoppio>> MetodiDoppiAsync(CancellationToken ct = default);
}

/// <summary>Una prova che concorre a un doppione di parametro sullo stesso campione.</summary>
public record RigaDuplicatoRapporto
{
    public int IdCertificato { get; init; }
    public string? NCertificato { get; init; }
    public DateTime? DataCertificato { get; init; }
    public int IdCampione { get; init; }
    public string? CodCampione { get; init; }
    public int? IdParametro { get; init; }
    public string? NomeParametro { get; init; }
    public int? IdTipoParametro { get; init; }
    public int IdRigaMetodo { get; init; }
    public string? DenominazioneRiga { get; init; }
    public int IdProva { get; init; }
    public double? ValoreCalcolato { get; init; }
    public bool MaggioreDi { get; init; }
    public bool MinoreDi { get; init; }
    public int Occorrenze { get; init; }
}

/// <summary>Metodo attivo con arrotondamento incompleto.</summary>
public record RigaMetodoArrotondamento
{
    public int IdMetodo { get; init; }
    public int? IdParametro { get; init; }
    public int? IdTipoMetodo { get; init; }
    public string? RifMetodo { get; init; }
    public string? DettagliMetodo { get; init; }
    public string? RifBiblio { get; init; }
    public int? IdUnita { get; init; }
    public int? CifreSignificative { get; init; }
    public int? DecimaliMax { get; init; }
}

/// <summary>Metodo attivo con margine sotto soglia (o negativo).</summary>
public record RigaMetodoMargine
{
    public int IdMetodo { get; init; }
    public int? IdParametro { get; init; }
    public int? IdTipoMetodo { get; init; }
    public string? RifMetodo { get; init; }
    public string? DettagliMetodo { get; init; }
    public string? RifBiblio { get; init; }
    public int? IdUnita { get; init; }
    public decimal? CostoAnoi { get; init; }
    public decimal? CostoMetodoEuro { get; init; }
    public decimal? MarginePct { get; init; }
}

/// <summary>Metodo attivo che coincide con altri per parametro + denominazione + riferimento.</summary>
public record RigaMetodoDoppio
{
    public int IdMetodo { get; init; }
    public int? IdParametro { get; init; }
    public int? IdTipoMetodo { get; init; }
    public string? RifMetodo { get; init; }
    public string? DettagliMetodo { get; init; }
    public string? RifBiblio { get; init; }
    public int? IdUnita { get; init; }
    public int NMetodi { get; init; }
}
