namespace LaboratorioAI.Core.Modelli;

/// <summary>Criteri di ricerca delle commesse.</summary>
public record CriteriRicercaCommesse
{
    /// <summary>Tipo commessa. Se non indicato si assume il laboratorio.</summary>
    public int? IdTipoCommessa { get; init; }

    public int? IdCliente { get; init; }
    public int? IdInsediamento { get; init; }
    public string? ResponsabileCommessa { get; init; }

    // Criteri validi per le sole commesse di laboratorio
    public int? IdControllo { get; init; }
    public string? NomeControllo { get; init; }

    public string? DescrizioneIncarico { get; init; }
    public string? NumeroContratto { get; init; }

    /// <summary>
    /// Per le commesse diverse dal laboratorio: se vero mostra anche
    /// quelle concluse, altrimenti solo le attive.
    /// </summary>
    public bool IncludiConcluse { get; init; }

    public int Limite { get; init; } = 300;
}

/// <summary>Commessa attiva.</summary>
public record Commessa
{
    public int IdCommessa { get; init; }
    public int IdTipoCommessa { get; init; }
    public string? TipoCommessa { get; init; }

    public string? RagioneSociale { get; init; }
    public string? NomeInsediamento { get; init; }

    public string? DescrizioneIncarico { get; init; }
    public int? IdControllo { get; init; }
    public string? Controllo { get; init; }

    /// <summary>Valorizzato solo per gli utenti abilitati alla visione degli importi.</summary>
    public decimal? PrezzoControllo { get; init; }

    public DateTime? DataCommessa { get; init; }
    public string? NumeroContratto { get; init; }
    public int? Durata { get; init; }
    public string? ResponsabileCommessa { get; init; }

    public bool DiLaboratorio => IdTipoCommessa == 3;
}

/// <summary>Campione collegato a una commessa di laboratorio.</summary>
public record CampioneDiCommessa
{
    public int IdCampione { get; init; }
    public string NumeroCampione { get; init; } = "";
    public DateTime? DataRicezione { get; init; }
    public DateTime? DataPrelievo { get; init; }
    public string? DenominazioneCampione { get; init; }
    public string? Matrice { get; init; }
    public string? LuogoPrelievo { get; init; }
    public string? VerbalePrelievo { get; init; }
    public int? IdCertificato { get; init; }
    public int GiorniInLaboratorio { get; init; }
    public int NumeroProve { get; init; }

    public StatoCampione Stato =>
        IdCertificato is not null                      ? StatoCampione.Certificato
        : NumeroProve == 0 && GiorniInLaboratorio > 10 ? StatoCampione.SpeditoOriginale
        :                                                StatoCampione.InCorso;
}

/// <summary>Prestazione erogata nell'ambito di una commessa di consulenza.</summary>
public record PrestazioneDiCommessa
{
    public string? Descrizione { get; init; }
    public DateTime? DataInizio { get; init; }
    public DateTime? DataFine { get; init; }

    /// <summary>Scadenza calcolata come data di inizio più la durata della commessa.</summary>
    public DateTime? ScadenzaCalcolata { get; init; }

    public string? Responsabile { get; init; }
    public bool Fatturata { get; init; }
    public DateTime? DataFattura { get; init; }

    /// <summary>Vero se la scadenza calcolata è già trascorsa e la prestazione non è chiusa.</summary>
    public bool InRitardo =>
        DataFine is null && ScadenzaCalcolata is not null && ScadenzaCalcolata < DateTime.Today;
}
