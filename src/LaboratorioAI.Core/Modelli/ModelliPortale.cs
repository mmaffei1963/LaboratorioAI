namespace LaboratorioAI.Core.Modelli;

/// <summary>Cliente riconosciuto in fase di accesso al portale.</summary>
public record ClientePortale
{
    public int IdCliente { get; init; }
    public string CodiceContabilita { get; init; } = "";
    public string RagioneSociale { get; init; } = "";
}

/// <summary>
/// Esito di un tentativo di accesso.
///
/// I motivi del rifiuto non vengono comunicati al chiamante in forma
/// distinguibile: all'utente si risponde sempre allo stesso modo, per
/// non rivelare quali codici cliente esistano.
/// </summary>
public enum EsitoAccesso
{
    Riuscito = 1,
    CredenzialiNonValide = 2,
    TroppiTentativi = 3
}

/// <summary>Sessione attiva sul portale.</summary>
public record SessioneAttiva
{
    public required string Token { get; init; }
    public int? IdCliente { get; init; }
    public string? RagioneSociale { get; init; }
    public bool Operatore { get; init; }
    public DateTime Scadenza { get; init; }
}

/// <summary>Criteri di ricerca disponibili al cliente sul portale.</summary>
public record CriteriPortale
{
    public DateOnly? DataDa { get; init; }
    public DateOnly? DataA { get; init; }

    /// <summary>Valorizzato solo dalle sessioni operatore, per scegliere il cliente.</summary>
    public int? IdCliente { get; init; }

    public int? IdInsediamento { get; init; }
    public int? IdMatrice { get; init; }
    public int? IdControllo { get; init; }

    public string? LuogoPrelievo { get; init; }
    public string? PuntoPrelievo { get; init; }
    public string? VerbalePrelievo { get; init; }
    public string? TestoLibero { get; init; }

    public StatoCampione? Stato { get; init; }

    public int Limite { get; init; } = 200;
}

/// <summary>Campione come viene mostrato al cliente.</summary>
public record CampionePortale
{
    public int IdCampione { get; init; }
    public string NumeroCampione { get; init; } = "";
    public string? RagioneSociale { get; init; }
    public string? NomeInsediamento { get; init; }
    public string? DenominazioneCampione { get; init; }
    public string? Matrice { get; init; }
    public string? Controllo { get; init; }
    public string? LuogoPrelievo { get; init; }
    public string? PuntoPrelievo { get; init; }
    public string? VerbalePrelievo { get; init; }
    public DateTime? DataRicezione { get; init; }
    public DateTime? DataPrelievo { get; init; }
    public int? IdCertificato { get; init; }
    public int GiorniInLaboratorio { get; init; }
    public int NumeroProve { get; init; }

    public StatoCampione Stato =>
        IdCertificato is not null                      ? StatoCampione.Certificato
        : NumeroProve == 0 && GiorniInLaboratorio > 10 ? StatoCampione.SpeditoOriginale
        :                                                StatoCampione.InCorso;
}

/// <summary>Conteggi di sintesi mostrati in cima ai risultati.</summary>
public record SintesiPortale
{
    public int Totale { get; init; }
    public int Certificati { get; init; }
    public int InCorso { get; init; }
}
