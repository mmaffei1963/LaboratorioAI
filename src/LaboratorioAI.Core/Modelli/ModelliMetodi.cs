namespace LaboratorioAI.Core.Modelli;

/// <summary>Logica di confronto fra i metodi selezionati e quelli di un controllo.</summary>
public enum LogicaRicercaControlli
{
    /// <summary>Il controllo contiene tutti i metodi indicati, eventualmente più altri.</summary>
    Tutti = 1,

    /// <summary>Il controllo contiene almeno uno dei metodi indicati.</summary>
    AlmenoUno = 2,

    /// <summary>Il controllo contiene esattamente i metodi indicati, né più né meno.</summary>
    Esattamente = 3
}

/// <summary>Metodo analitico, come compare nella ricerca.</summary>
public record MetodoRicercabile
{
    public int IdMetodo { get; init; }
    public string? RiferimentoNormativo { get; init; }
    public string? NomeParametro { get; init; }
    public string? DenominazioneRiga { get; init; }
    public bool Accreditato { get; init; }
    public string? LaboratorioEsterno { get; init; }
    public bool AccreditatoEsterno { get; init; }

    /// <summary>Testo su cui opera la ricerca incrementale nell'interfaccia.</summary>
    public string TestoRicerca =>
        string.Join(' ', new[] { RiferimentoNormativo, NomeParametro, DenominazioneRiga }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Controllo che soddisfa i criteri di ricerca sui metodi.</summary>
public record ControlloTrovato
{
    public int IdControllo { get; init; }
    public string Controllo { get; init; } = "";
    public bool Attivo { get; init; }
    public int NumeroMetodi { get; init; }
}

/// <summary>Metodo appartenente a un controllo, con lo stato di accreditamento.</summary>
public record MetodoDelControllo
{
    public int IdControllo { get; init; }
    public string Controllo { get; init; } = "";
    public int IdMetodo { get; init; }
    public string? DenominazioneRiga { get; init; }
    public string? RiferimentoNormativo { get; init; }
    public string? NomeParametro { get; init; }

    /// <summary>Metodo accreditato presso MARE.A.</summary>
    public bool Accreditato { get; init; }

    /// <summary>Laboratorio a cui la prova è affidata in subappalto.</summary>
    public string? LaboratorioEsterno { get; init; }

    /// <summary>Il laboratorio in subappalto è accreditato per questo metodo.</summary>
    public bool AccreditatoEsterno { get; init; }

    /// <summary>Esiste una procedura attiva per questo metodo.</summary>
    public bool Attivo { get; init; }
}

/// <summary>
/// Quadro dei prezzi di un controllo sulle commesse attualmente in vigore.
///
/// Considera esclusivamente le commesse prive di data di scadenza incarico.
/// </summary>
public record PrezziControllo
{
    public decimal? PrezzoMinimo { get; init; }
    public string? ClientePrezzoMinimo { get; init; }
    public int ClientiAlMinimo { get; init; }

    public decimal? PrezzoMassimo { get; init; }
    public string? ClientePrezzoMassimo { get; init; }
    public int ClientiAlMassimo { get; init; }

    public int ClientiTotali { get; init; }

    public bool Disponibili => PrezzoMinimo is not null;

    /// <summary>Vero se tutti i clienti applicano lo stesso prezzo.</summary>
    public bool PrezzoUnico => PrezzoMinimo == PrezzoMassimo;
}

/// <summary>Controllo esploso nei suoi metodi, con il quadro dei prezzi.</summary>
public record ControlloEsploso
{
    public required int IdControllo { get; init; }
    public required string Controllo { get; init; }
    public required IReadOnlyList<MetodoDelControllo> Metodi { get; init; }

    /// <summary>Valorizzato solo per gli utenti abilitati alla visione dei prezzi.</summary>
    public PrezziControllo? Prezzi { get; init; }
}
