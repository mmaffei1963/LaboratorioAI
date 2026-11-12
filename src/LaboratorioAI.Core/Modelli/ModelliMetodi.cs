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
    /// <summary>05/11/2026 (Zatterin, prove su superfici indistinguibili): dati che distinguono metodi con stessa norma e parametro.</summary>
    public string? Acronimo { get; init; }
    public string? RifMetodo { get; init; }
    public string? DettagliMetodo { get; init; }
    public string? UnitaMisura { get; init; }
    public bool Accreditato { get; init; }
    public string? LaboratorioEsterno { get; init; }
    public bool AccreditatoEsterno { get; init; }

    /// <summary>Testo su cui opera la ricerca incrementale nell'interfaccia (anche ID, sigla, dettagli e unità di misura).</summary>
    public string TestoRicerca =>
        string.Join(' ', new[] { IdMetodo.ToString(), RiferimentoNormativo, NomeParametro, DenominazioneRiga, Acronimo, RifMetodo, DettagliMetodo, UnitaMisura }
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

    /// <summary>12/11/2026: prezzo medio pesato per cliente (media dei prezzi dei clienti, ognuno conta uno).</summary>
    public decimal? PrezzoMedio { get; init; }

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

/// <summary>
/// 12/11/2026: campioni di una matrice raggruppati per commessa (controllo, cliente, prezzo), per la vista
/// «Controlli per matrice». Solo commesse di laboratorio attive con campioni nel periodo.
/// </summary>
public record CampioniCommessaMatrice(int IdControllo, string? Controllo, int IdCommessa, string CodCliente, decimal? Prezzo, int Campioni);

/// <summary>12/11/2026: controllo usato sulle matrici scelte, con campioni, clienti e prezzi delle commesse attive.</summary>
public record ControlloPerMatrice(int IdControllo, string Controllo, int Campioni, int Clienti, int Commesse,
    decimal? PrezzoMinimo, decimal? PrezzoMedio, decimal? PrezzoMassimo);

/// <summary>Calcoli dei prezzi dei controlli (12/11/2026).</summary>
public static class StatistichePrezzi
{
    /// <summary>
    /// Medio pesato per cliente: per ogni cliente la media dei prezzi delle sue commesse, poi la media tra i clienti
    /// (ogni cliente conta uno). Prezzi nulli o a zero esclusi. Null se non c'è nessun prezzo.
    /// </summary>
    public static decimal? MedioPerCliente(IEnumerable<(string Cliente, decimal? Prezzo)> prezzi)
    {
        var perCliente = prezzi.Where(p => p.Prezzo is > 0)
            .GroupBy(p => (p.Cliente ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Average(p => p.Prezzo!.Value)).ToList();
        return perCliente.Count == 0 ? null : Math.Round(perCliente.Average(), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Controlli delle matrici scelte, dal più usato (campioni) al meno usato.</summary>
    public static IReadOnlyList<ControlloPerMatrice> PerMatrice(IEnumerable<CampioniCommessaMatrice> righe) =>
        righe.GroupBy(r => r.IdControllo)
            .Select(g =>
            {
                var prezzi = g.Where(r => r.Prezzo is > 0).Select(r => r.Prezzo!.Value).ToList();
                return new ControlloPerMatrice(g.Key, g.Select(r => r.Controllo).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim() ?? $"Controllo {g.Key}",
                    g.Sum(r => r.Campioni),
                    g.Select(r => (r.CodCliente ?? "").Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    g.Select(r => r.IdCommessa).Distinct().Count(),
                    prezzi.Count == 0 ? null : prezzi.Min(),
                    MedioPerCliente(g.Select(r => (r.CodCliente, r.Prezzo))),
                    prezzi.Count == 0 ? null : prezzi.Max());
            })
            .OrderByDescending(c => c.Campioni).ThenBy(c => c.Controllo, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
