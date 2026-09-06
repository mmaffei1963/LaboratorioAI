namespace LaboratorioAI.Core.Modelli;

/// <summary>
/// Esito dell'estrazione AI da un certificato (Rapporto di Prova) di un laboratorio
/// ESTERNO. È una PROPOSTA da rivedere: il codice estratto va validato contro ArcLab.
/// </summary>
public record EstrazioneCertificatoEsterno
{
    /// <summary>Ragione sociale del laboratorio emittente, riconosciuta dall'intestazione.</summary>
    public string? Laboratorio { get; init; }

    /// <summary>Nostro lotto (parte prima della "/" nel codice NNNN/NN).</summary>
    public string? Lotto { get; init; }

    /// <summary>Nostro campione (parte dopo la "/").</summary>
    public string? Campione { get; init; }

    /// <summary>Numero di Rapporto di Prova del laboratorio esterno (sua identità documentale).</summary>
    public string? RdPEsterno { get; init; }

    /// <summary>Data di emissione del certificato esterno (yyyy-MM-dd), usata per il nome file.</summary>
    public string? DataEmissione { get; init; }

    /// <summary>True se il certificato dichiara una non conformità o un superamento di limite.</summary>
    public bool NonConforme { get; init; }

    /// <summary>Criticità rilevate: risultati fuori specifica, valori anomali, non conformità.</summary>
    public IReadOnlyList<string> Criticita { get; init; } = new List<string>();

    /// <summary>Valutazione discorsiva dell'AI: sintesi e motivo del giudizio per l'avviso al cliente.</summary>
    public string? Valutazione { get; init; }

    /// <summary>Note/avvertenze dell'estrazione (es. codice nostro non trovato).</summary>
    public string? Note { get; init; }

    /// <summary>Comodo: "lotto/campione" ricomposto, se presenti.</summary>
    public string? CodiceNostro =>
        string.IsNullOrWhiteSpace(Lotto) ? null
        : string.IsNullOrWhiteSpace(Campione) ? Lotto : $"{Lotto}/{Campione}";
}

/// <summary>
/// Campi per comporre il nome file di un certificato esterno: la parte fissa proviene
/// da ArcLab (via lotto/campione), la parte esterna dall'estrazione del PDF.
/// </summary>
public record DatiNomeCertificatoEsterno
{
    public DateOnly Data { get; init; }
    public string Lotto { get; init; } = "";
    public string Campione { get; init; } = "";
    public string? IdInsediamento { get; init; }
    public string? Cliente { get; init; }
    public string? Prodotto { get; init; }
    public string? Matrice { get; init; }
    public string LabAcronimo { get; init; } = "";
    public string RdPEsterno { get; init; } = "";
    /// <summary>True → suffisso "-AVV" (lettera di avviso al cliente); False → "-STD".</summary>
    public bool ConAvviso { get; init; }
}

/// <summary>Dati del campione letti da ArcLab (sola lettura) tramite lotto/campione.</summary>
public record CampioneArcLab
{
    public int IdCampione { get; init; }
    public int Anno { get; init; }
    public string Lotto { get; init; } = "";
    public string Campione { get; init; } = "";
    public string? RagioneSociale { get; init; }
    public int IdInsediamento { get; init; }
    public string? NomeInsediamento { get; init; }
    public string? DenominazioneProdotto { get; init; }
    public string? Matrice { get; init; }
    public DateTime DataRicezione { get; init; }
}

/// <summary>
/// Anteprima di un certificato esterno pronta per la conferma dell'operatore:
/// dati estratti dal PDF, campione ArcLab agganciato, nomi proposti e criticità.
/// </summary>
public record AnteprimaCertificatoEsterno
{
    public EstrazioneCertificatoEsterno Estrazione { get; init; } = new();
    public CampioneArcLab? Campione { get; init; }
    public bool TrovatoInArcLab => Campione is not null;

    /// <summary>Acronimo laboratorio ricavato dall'estrazione.</summary>
    public string LabAcronimo { get; init; } = "";

    /// <summary>Nome proposto senza avviso (…-STD).</summary>
    public string? NomeStandard { get; init; }
    /// <summary>Nome proposto con avviso (…-AVV).</summary>
    public string? NomeConAvviso { get; init; }

    /// <summary>Criticità rilevate (dal PDF).</summary>
    public IReadOnlyList<string> Criticita { get; init; } = new List<string>();
    /// <summary>Avvertenze di processo (codice non trovato, campione non in archivio, ecc.).</summary>
    public IReadOnlyList<string> Avvertenze { get; init; } = new List<string>();

    /// <summary>True se il modulo suggerisce l'avviso (per default proposto quando ci sono criticità).</summary>
    public bool AvvisoSuggerito { get; init; }

    /// <summary>True se l'estrazione è stata riusata dalla cache (nessuna nuova chiamata AI).</summary>
    public bool DaCache { get; init; }

    /// <summary>Data usata nel nome file (yyyy-MM-dd): emissione del certificato o, in mancanza, ricezione.</summary>
    public string? DataNome { get; init; }
}
