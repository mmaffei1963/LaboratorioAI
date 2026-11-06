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

    /// <summary>Trova i campioni che applicano questo metodo (via prove → rigametodo).</summary>
    public int? IdMetodo { get; init; }

    /// <summary>Ricerca per testo sul nome/parametro/riferimento del metodo (es. "glutine"): trova tutti i metodi che lo contengono.</summary>
    public string? MetodoNome { get; init; }

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
        IdLaboratorioEsterno is null && IdMetodo is null && string.IsNullOrWhiteSpace(MetodoNome) && Stato is null;
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
    /// <summary>Somma del prezzo del metodo (CostoMetodoEuro) sulle prove trovate, se è impostato il filtro Metodo.</summary>
    public decimal? TotalePrezzoMetodo { get; init; }
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

/// <summary>Cliente ArcLab con campioni recenti (invio massivo delle approvazioni, 10/10/2026).</summary>
public record ClienteConCampioni(int IdCliente, string? RagioneSociale, string? PartitaIva, string? Email, int NumeroCampioni, DateTime? UltimoCampione);

/// <summary>Laboratorio esterno con la frase da riportare in offerta per le prove in subappalto.</summary>
public record VoceLaboratorioEsterno(int Id, string Nome, string? Frase);

/// <summary>Insediamento, con il riferimento al cliente per i filtri a cascata.</summary>
public record VoceInsediamento(int Id, string Nome, int IdCliente);

/// <summary>Punto di prelievo già usato in un insediamento, con quante volte (prelievo vocale, 13/10/2026).</summary>
public record PuntoPrelievoUsato(int IdInsediamento, string Punto, int Volte);

/// <summary>
/// Dettaglio di un cliente per la precompilazione in Preventivi: Partita IVA e indirizzo di
/// fatturazione (sede legale), entrambi letti direttamente dalla tabella Clienti — l'offerta è
/// intestata al cliente, non a un insediamento/sede specifico.
/// </summary>
public record DettaglioCliente(
    int IdCliente, string? PartitaIva, string? Indirizzo, string? Cap, string? Citta, string? Provincia, string? Email);

/// <summary>
/// Insediamento del cliente con indirizzo completo ed email di contatto (Insediamento.Email), per il
/// richiamo ai recapiti riportato in fondo alle offerte/preventivi.
/// </summary>
public record InsediamentoContatto(string Nome, string? Indirizzo, string? Email);
