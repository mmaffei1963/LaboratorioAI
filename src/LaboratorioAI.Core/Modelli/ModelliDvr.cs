namespace LaboratorioAI.Core.Modelli;

/// <summary>Regione di riferimento, che determina il modello documentale da usare.</summary>
public enum RegioneDvr
{
    EmiliaRomagna = 1,
    Marche = 2
}

/// <summary>Modalità di produzione dell'acqua calda sanitaria.</summary>
public enum ProduzioneAcquaCalda
{
    NonDefinita = 0,
    Istantanea = 1,
    AccumuloCentralizzato = 2,
    AccumuloLocale = 3,
    Misto = 4
}

/// <summary>
/// Dati raccolti per la valutazione del rischio legionellosi.
///
/// I campi rispecchiano il questionario compilato in fase di sopralluogo.
/// Il popolamento del modello documentale avviene per corrispondenza fra
/// queste proprietà e i controlli contenuto presenti nel modello.
/// </summary>
public record SchedaDvr
{
    // ── Identificazione ────────────────────────────────────────────
    public int? IdCliente { get; init; }
    public int? IdInsediamento { get; init; }

    public required RegioneDvr Regione { get; init; }
    public required string RagioneSociale { get; init; }
    public string? NomeInsediamento { get; init; }

    public string? Indirizzo { get; init; }
    public string? Comune { get; init; }
    public string? Provincia { get; init; }
    public string? Cap { get; init; }
    public string? PartitaIva { get; init; }

    // ── Riferimenti ────────────────────────────────────────────────
    public string? LegaleRappresentante { get; init; }
    public string? ResponsabileStruttura { get; init; }
    public string? DataSopralluogo { get; init; }

    // ── Caratteristiche della struttura ────────────────────────────
    public string? TipologiaStruttura { get; init; }
    public int? NumeroCamere { get; init; }
    public int? NumeroPianiFuoriTerra { get; init; }
    public int? AnnoCostruzione { get; init; }
    public int? AnnoUltimaRistrutturazione { get; init; }

    /// <summary>Mesi di apertura, per le strutture a esercizio stagionale.</summary>
    public string? PeriodoApertura { get; init; }
    public bool Stagionale { get; init; }

    // ── Impianto idrico ────────────────────────────────────────────
    public ProduzioneAcquaCalda ProduzioneAcqua { get; init; }
    public bool SerbatoiAccumuloAcquaFredda { get; init; }
    public bool RicircoloAcquaCalda { get; init; }
    public decimal? TemperaturaMandata { get; init; }
    public decimal? TemperaturaRitorno { get; init; }
    public string? TrattamentiAcqua { get; init; }

    // ── Elementi che aumentano il rischio ──────────────────────────
    public bool TorriEvaporative { get; init; }
    public bool FontaneDecorative { get; init; }
    public bool PiscineOIdromassaggio { get; init; }
    public bool UmidificatoriAria { get; init; }
    public bool ImpiantiAntincendioAdUmido { get; init; }
    public string? AltriElementiRischio { get; init; }

    // ── Punti di prelievo ──────────────────────────────────────────
    public IReadOnlyList<PuntoPrelievoDvr> PuntiPrelievo { get; init; } = [];

    // ── Note ───────────────────────────────────────────────────────
    public string? NoteSopralluogo { get; init; }
    public string? MisureGiaAdottate { get; init; }

    /// <summary>
    /// Sezioni del modello da mantenere.
    /// Le sezioni condizionate non pertinenti vengono rimosse dal documento.
    /// </summary>
    public IReadOnlyDictionary<string, bool> SezioniAttive => new Dictionary<string, bool>
    {
        ["torri_evaporative"]   = TorriEvaporative,
        ["fontane"]             = FontaneDecorative,
        ["piscine"]             = PiscineOIdromassaggio,
        ["umidificatori"]       = UmidificatoriAria,
        ["antincendio"]         = ImpiantiAntincendioAdUmido,
        ["stagionale"]          = Stagionale,
        ["accumulo_freddo"]     = SerbatoiAccumuloAcquaFredda,
        ["ricircolo"]           = RicircoloAcquaCalda
    };
}

/// <summary>Punto di prelievo previsto dal piano di campionamento.</summary>
public record PuntoPrelievoDvr
{
    public required string Descrizione { get; init; }
    public string? Ubicazione { get; init; }
    public string? Tipologia { get; init; }
}

/// <summary>Esito della produzione di un documento.</summary>
public record RisultatoGenerazioneDvr
{
    public required bool Riuscito { get; init; }
    public string? NomeFile { get; init; }
    public string? PercorsoCompleto { get; init; }
    public string? Messaggio { get; init; }

    /// <summary>Controlli del modello rimasti privi di valore. Utile in diagnosi.</summary>
    public IReadOnlyList<string> CampiNonPopolati { get; init; } = [];

    public static RisultatoGenerazioneDvr Errore(string messaggio) =>
        new() { Riuscito = false, Messaggio = messaggio };
}

/// <summary>Stato di avanzamento di una generazione massiva.</summary>
public record StatoLottoDvr
{
    public required Guid IdLotto { get; init; }
    public required int Totale { get; init; }
    public int Completati { get; init; }
    public int Riusciti { get; init; }
    public int Falliti { get; init; }
    public bool Concluso { get; init; }
    public string? ElaborazioneCorrente { get; init; }
    public IReadOnlyList<RigaLottoDvr> Righe { get; init; } = [];

    public int PercentualeAvanzamento =>
        Totale == 0 ? 100 : (int)Math.Round(Completati * 100.0 / Totale);
}

/// <summary>Singolo esito all'interno di una generazione massiva.</summary>
public record RigaLottoDvr
{
    public required string RagioneSociale { get; init; }
    public string? NomeInsediamento { get; init; }
    public bool Riuscito { get; init; }
    public string? NomeFile { get; init; }
    public string? Messaggio { get; init; }
}
