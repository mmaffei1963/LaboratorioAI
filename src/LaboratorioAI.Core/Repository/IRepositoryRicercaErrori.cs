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

    /// <summary>Lotti (per anno di ricezione) con IdRicevitore non valorizzato.</summary>
    Task<IReadOnlyList<RigaLottoErrore>> LottiSenzaRicevitoreAsync(int annoDa, int annoA, CancellationToken ct = default);

    /// <summary>Lotti (per anno di ricezione) con IdVettore non valorizzato.</summary>
    Task<IReadOnlyList<RigaLottoErrore>> LottiSenzaVettoreAsync(int annoDa, int annoA, CancellationToken ct = default);

    /// <summary>
    /// Coerenza di categoria (interno vs cliente/terza parte, decisa dai nomi) fra prelevatore
    /// del campione e vettore del lotto, più presenza indebita del verbale per prelievi esterni.
    /// </summary>
    Task<IReadOnlyList<RigaCoerenzaPrelevatore>> CoerenzaPrelevatoreVettoreAsync(int annoDa, int annoA, CancellationToken ct = default);

    /// <summary>
    /// Campioni con AnalisiChiusa spuntato ma non pronti alla stampa: prove con VerificaValore
    /// non spuntato, DataInizioProva/DataFineProva mancanti, oppure rigametodo collegata senza
    /// Arrotonda/MaxDecimali. Ricerca per intervallo di date (Lotto.DataRic) oppure per singolo
    /// lotto/campione (in tal caso le date sono ignorate).
    /// </summary>
    Task<IReadOnlyList<RigaCampioneStampa>> CampioniProntiStampaAsync(
        DateOnly? da, DateOnly? a, string? codLotto, string? codCampione, CancellationToken ct = default);
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
    public string? NomeParametro { get; init; }
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
    public string? NomeParametro { get; init; }
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
    public string? NomeParametro { get; init; }
    public int NMetodi { get; init; }
}

/// <summary>Lotto con un riferimento anagrafico mancante (ricevitore o vettore).</summary>
public record RigaLottoErrore
{
    public int IdLotto { get; init; }
    public string? CodLotto { get; init; }
    public DateTime? DataRic { get; init; }
    public int? IdCommessa { get; init; }
    public string? RagioneSociale { get; init; }
}

/// <summary>Prova di un campione «chiuso» che non è pronta alla stampa (una o più condizioni non rispettate).</summary>
public record RigaCampioneStampa
{
    public int IdLotto { get; init; }
    public string? CodLotto { get; init; }
    public DateTime? DataRic { get; init; }
    public int IdCampione { get; init; }
    public string? CodCampione { get; init; }
    public string? RagioneSociale { get; init; }
    public int IdProva { get; init; }
    public int IdRigaMetodo { get; init; }
    public string? DenominazioneRiga { get; init; }
    public string? NomeParametro { get; init; }
    public bool VerificaValore { get; init; }
    public DateTime? DataInizioProva { get; init; }
    public DateTime? DataFineProva { get; init; }
    public int? Arrotonda { get; init; }
    public int? MaxDecimali { get; init; }
}

/// <summary>Campione a prelevatore interno con incoerenza su vettore e/o verbale.</summary>
public record RigaCoerenzaPrelevatore
{
    public int IdLotto { get; init; }
    public string? CodLotto { get; init; }
    public DateTime? DataRic { get; init; }
    public int IdCampione { get; init; }
    public string? CodCampione { get; init; }
    public int? IdPrelevatore { get; init; }
    public string? Prelevatore { get; init; }
    public int? IdVettore { get; init; }
    public string? Vettore { get; init; }
    public string? BuonoPrelievo { get; init; }
    /// <summary>Il vettore è una persona diversa dal prelevatore interno.</summary>
    public bool AnomaliaVettore { get; init; }
    /// <summary>È presente un verbale di prelievo che per un prelievo interno non dovrebbe esserci.</summary>
    public bool AnomaliaVerbale { get; init; }
}
