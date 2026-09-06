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

    /// <summary>True se il certificato dichiara una non conformità o un superamento di limite.</summary>
    public bool NonConforme { get; init; }

    /// <summary>Criticità rilevate: risultati fuori specifica, valori anomali, non conformità.</summary>
    public IReadOnlyList<string> Criticita { get; init; } = new List<string>();

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
    public string? Accettazione { get; init; }
    public string? Cliente { get; init; }
    public string? Prodotto { get; init; }
    public string? Matrice { get; init; }
    public string LabAcronimo { get; init; } = "";
    public string RdPEsterno { get; init; } = "";
    /// <summary>True → suffisso "-AVV" (lettera di avviso al cliente); False → "-STD".</summary>
    public bool ConAvviso { get; init; }
}
