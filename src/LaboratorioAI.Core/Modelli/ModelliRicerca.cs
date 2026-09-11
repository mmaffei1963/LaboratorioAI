namespace LaboratorioAI.Core.Modelli;

/// <summary>Stato di lavorazione di un campione.</summary>
public enum StatoCampione
{
    /// <summary>Analisi in corso, certificato non ancora emesso.</summary>
    InCorso = 1,

    /// <summary>Certificato MARE.A emesso.</summary>
    Certificato = 2,

    /// <summary>
    /// Campione inviato in originale a laboratorio esterno senza ricertificazione:
    /// privo di prove e giacente da oltre dieci giorni, OPPURE con un certificato
    /// esterno caricato nel portale ma senza nostro certificato.
    /// </summary>
    SpeditoOriginale = 3
}

/// <summary>Criteri di ricerca dei campioni nell'archivio.</summary>
public record CriteriRicercaCampioni
{
    public DateOnly? PrelievoDa { get; init; }
    public DateOnly? PrelievoA { get; init; }
    public DateOnly? RicezioneDa { get; init; }
    public DateOnly? RicezioneA { get; init; }

    public int? IdCliente { get; init; }
    public int? IdInsediamento { get; init; }
    public int? IdMatrice { get; init; }
    public int? IdControllo { get; init; }

    public string? CodiceLim { get; init; }
    public string? VerbalePrelievo { get; init; }
    public string? DenominazioneCampione { get; init; }
    public string? Prelevatore { get; init; }
    public string? Ricevitore { get; init; }
    public string? LuogoPrelievo { get; init; }
    public string? PuntoPrelievo { get; init; }
    public int? IdLaboratorioEsterno { get; init; }

    /// <summary>Email (di cliente/insediamento/contatto): raggruppa i condomìni di un amministratore.</summary>
    public string? Email { get; init; }

    public StatoCampione? Stato { get; init; }

    public int Limite { get; init; } = 500;

    /// <summary>Vero se non è stato impostato alcun criterio selettivo.</summary>
    public bool Vuoto =>
        PrelievoDa is null && PrelievoA is null && RicezioneDa is null && RicezioneA is null &&
        IdCliente is null && IdInsediamento is null && IdMatrice is null && IdControllo is null &&
        string.IsNullOrWhiteSpace(CodiceLim) && string.IsNullOrWhiteSpace(VerbalePrelievo) &&
        string.IsNullOrWhiteSpace(DenominazioneCampione) && string.IsNullOrWhiteSpace(Prelevatore) &&
        string.IsNullOrWhiteSpace(Ricevitore) &&
        string.IsNullOrWhiteSpace(LuogoPrelievo) && string.IsNullOrWhiteSpace(PuntoPrelievo) &&
        string.IsNullOrWhiteSpace(Email) &&
        IdLaboratorioEsterno is null && Stato is null;
}

/// <summary>Riga di risultato della ricerca campioni.</summary>
public record RisultatoRicercaCampione
{
    public int IdCampione { get; init; }
    public string NumeroCampione { get; init; } = "";
    public string? RagioneSociale { get; init; }
    public string? CodContabilita { get; init; }
    public string? Email { get; init; }
    public string? NomeInsediamento { get; init; }
    public string? DenominazioneCampione { get; init; }
    public string? Matrice { get; init; }
    public int IdControllo { get; init; }
    public string? Controllo { get; init; }
    public string? LuogoPrelievo { get; init; }
    public string? PuntoPrelievo { get; init; }
    public string? VerbalePrelievo { get; init; }
    public string? Prelevatore { get; init; }
    public decimal? PrezzoControllo { get; init; }
    public DateTime? DataRicezione { get; init; }
    public DateTime? DataPrelievo { get; init; }
    public int? IdCertificato { get; init; }
    public int GiorniInLaboratorio { get; init; }
    public int NumeroProve { get; init; }
    /// <summary>Esiste un certificato esterno caricato nel portale per questo campione (non ricertificato).</summary>
    public bool EsternoPresente { get; init; }

    public StatoCampione Stato =>
        IdCertificato is not null                                        ? StatoCampione.Certificato
        : (NumeroProve == 0 && GiorniInLaboratorio > 10) || EsternoPresente ? StatoCampione.SpeditoOriginale
        :                                                                  StatoCampione.InCorso;
}

/// <summary>Totali complessivi di una ricerca (sull'intero risultato, non solo le righe mostrate).</summary>
public record TotaliRicerca
{
    public int Totale { get; init; }
    public decimal? TotalePrezzo { get; init; }
}

/// <summary>Esito di una ricerca, con indicazione di troncamento.</summary>
public record EsitoRicerca<T>
{
    public required IReadOnlyList<T> Elementi { get; init; }
    public int Totale => Elementi.Count;
    public bool Troncato { get; init; }
}

/// <summary>Voce di un elenco anagrafico usato per i filtri a tendina.</summary>
public record VoceAnagrafica(int Id, string Nome);

/// <summary>Laboratorio esterno con la frase da riportare in offerta per le prove in subappalto.</summary>
public record VoceLaboratorioEsterno(int Id, string Nome, string? Frase);

/// <summary>Insediamento, con il riferimento al cliente per i filtri a cascata.</summary>
public record VoceInsediamento(int Id, string Nome, int IdCliente);
