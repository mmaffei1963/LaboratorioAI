using System.Globalization;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Direzione;

/// <summary>
/// Riga della ricerca documenti AZW (11/11/2026): una voce di fattura o nota di credito di vendita (ricavo) oppure una
/// registrazione di costo della contabilità analitica. <see cref="Importo"/>: ricavi con il segno (note di credito
/// negative), costi in positivo (uno storno di costo è negativo).
/// </summary>
public sealed record RigaRicercaDocumento(
    bool Costo, string TipoDocumento, DateTime Data, string Numero, string? CodiceControparte, string? Controparte,
    string Descrizione, decimal? Quantita, decimal? Prezzo, string? Sconto, decimal Importo,
    string Centro, string Area, string? Voce, string? Commessa, string? Zona);

/// <summary>Totali di un'area per la ricerca documenti.</summary>
public sealed record TotaleAreaRicerca(string Area, decimal Ricavi, decimal Costi);

/// <summary>Riquadri riassuntivi della ricerca documenti, calcolati su tutte le righe filtrate.</summary>
public sealed record RiepilogoRicercaDocumenti(
    decimal Ricavi, int Fatture, int NoteCredito, decimal Costi, int DocumentiCosto, int Clienti, int Fornitori, int Righe,
    IReadOnlyList<TotaleAreaRicerca> PerArea)
{
    public decimal Differenza => Ricavi - Costi;
}

/// <summary>Regole della ricerca documenti AZW (filtri, zona alla data, riepiloghi). Nessun accesso ai dati.</summary>
public static class RicercaDocumentiAzw
{
    /// <summary>Valore dei filtri commessa e zona per le righe che non ne hanno.</summary>
    public const string Nessuno = "-";

    private static readonly CompareInfo Confronto = CultureInfo.GetCultureInfo("it-IT").CompareInfo;
    private const CompareOptions Opzioni = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// Vero se la riga contiene almeno una delle parole (senza badare a maiuscole e accenti). Nessuna parola = tutte.
    /// Per i costi si cerca anche nel nome del fornitore, perché le registrazioni non hanno il testo della fattura.
    /// </summary>
    public static bool ContieneTesto(RigaRicercaDocumento r, IReadOnlyList<string> parole)
    {
        if (parole.Count == 0) return true;
        foreach (var p in parole)
        {
            if (Confronto.IndexOf(r.Descrizione ?? "", p, Opzioni) >= 0) return true;
            if (r.Costo && r.Controparte is { } c && Confronto.IndexOf(c, p, Opzioni) >= 0) return true;
        }
        return false;
    }

    /// <summary>Filtro su un codice (commessa o zona): null = tutte, <see cref="Nessuno"/> = solo le righe senza codice.</summary>
    public static bool Corrisponde(string? valore, string? filtro)
    {
        if (string.IsNullOrWhiteSpace(filtro)) return true;
        if (filtro.Trim() == Nessuno) return string.IsNullOrWhiteSpace(valore);
        return string.Equals(valore?.Trim(), filtro.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Zona valida alla data: la riga dello storico che copre il giorno; per le date precedenti alla prima rilevazione si
    /// usa la prima zona conosciuta (AZW non ne conserva di più vecchie). Storico vuoto = null.
    /// </summary>
    public static string? ZonaAl(IReadOnlyList<StoricoZonaCliente> storico, DateTime data)
    {
        if (storico.Count == 0) return null;
        var d = data.Date;
        var ordinato = storico.OrderBy(s => s.DataDal).ToList();
        foreach (var s in ordinato)
            if (s.DataDal.Date <= d && (s.DataAl is null || d < s.DataAl.Value.Date)) return s.Zona;
        return d < ordinato[0].DataDal.Date ? ordinato[0].Zona : ordinato[^1].Zona;
    }

    /// <summary>
    /// Allinea lo storico alle zone attuali di AZW: per ogni cliente senza riga aperta o con una zona diversa si chiude la
    /// riga aperta (DataAl = oggi) e se ne apre una nuova. Restituisce le righe nuove; quelle esistenti chiuse o corrette
    /// sono modificate sul posto e restituite in <paramref name="modificate"/>. I clienti non più presenti in AZW restano come sono.
    /// </summary>
    public static List<StoricoZonaCliente> AllineaZone(IEnumerable<StoricoZonaCliente> aperte, IEnumerable<(string Codice, string? Zona)> attuali,
        DateTime oggi, out List<StoricoZonaCliente> modificate)
    {
        var giorno = oggi.Date;
        var perCliente = aperte.Where(a => a.DataAl is null)
            .GroupBy(a => a.CodiceCliente.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataDal).First(), StringComparer.OrdinalIgnoreCase);
        var nuove = new List<StoricoZonaCliente>();
        modificate = new List<StoricoZonaCliente>();
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (codice, zona) in attuali)
        {
            var cod = (codice ?? "").Trim();
            if (cod.Length == 0 || !visti.Add(cod)) continue;
            var z = string.IsNullOrWhiteSpace(zona) ? null : zona.Trim().ToUpperInvariant();
            if (perCliente.TryGetValue(cod, out var aperta))
            {
                if (string.Equals(aperta.Zona, z, StringComparison.OrdinalIgnoreCase)) continue;
                // Cambio rilevato lo stesso giorno dell'apertura: si corregge la riga invece di crearne una di zero giorni.
                if (aperta.DataDal.Date >= giorno) { aperta.Zona = z; modificate.Add(aperta); continue; }
                aperta.DataAl = giorno;
                modificate.Add(aperta);
            }
            nuove.Add(new StoricoZonaCliente { CodiceCliente = cod, Zona = z, DataDal = giorno, DataCreazione = DateTime.UtcNow });
        }
        return nuove;
    }

    /// <summary>
    /// Commessa di riserva per le voci di vendita senza commessa: quella della contabilità analitica dello stesso documento
    /// e centro se è una sola, altrimenti quella del documento se è una sola, altrimenti nessuna.
    /// </summary>
    public static string? CommessaDiRiserva(IReadOnlyCollection<(string Centro, string Commessa)> righeDocumento, string? centro)
    {
        var stessoCentro = righeDocumento.Where(r => string.Equals(r.Centro, centro, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Commessa).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (stessoCentro.Count == 1) return stessoCentro[0];
        var tutte = righeDocumento.Select(r => r.Commessa).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return tutte.Count == 1 ? tutte[0] : null;
    }

    public static RiepilogoRicercaDocumenti Riepilogo(IReadOnlyCollection<RigaRicercaDocumento> righe)
    {
        var ricavi = righe.Where(r => !r.Costo).ToList();
        var costi = righe.Where(r => r.Costo).ToList();
        var perArea = righe.GroupBy(r => r.Area)
            .Select(g => new TotaleAreaRicerca(g.Key, g.Where(r => !r.Costo).Sum(r => r.Importo), g.Where(r => r.Costo).Sum(r => r.Importo)))
            .OrderBy(t => OrdineArea(t.Area)).ThenBy(t => t.Area, StringComparer.Ordinal).ToList();
        return new RiepilogoRicercaDocumenti(
            ricavi.Sum(r => r.Importo),
            ricavi.Where(r => !VociDocumentiVendita.ENotaCredito(r.TipoDocumento)).Select(r => (r.TipoDocumento, r.Numero)).Distinct().Count(),
            ricavi.Where(r => VociDocumentiVendita.ENotaCredito(r.TipoDocumento)).Select(r => (r.TipoDocumento, r.Numero)).Distinct().Count(),
            costi.Sum(r => r.Importo),
            costi.Select(r => (r.TipoDocumento, r.Numero)).Distinct().Count(),
            ricavi.Where(r => r.CodiceControparte is not null).Select(r => r.CodiceControparte!).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            costi.Where(r => r.CodiceControparte is not null).Select(r => r.CodiceControparte!).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            righe.Count, perArea);
    }

    /// <summary>Posizione dell'area nell'ordine del Consuntivo per area; le altre in fondo.</summary>
    public static int OrdineArea(string area)
    {
        for (var i = 0; i < MotoreConsuntivo.AreeStandard.Count; i++)
            if (string.Equals(MotoreConsuntivo.AreeStandard[i].Codice, area, StringComparison.OrdinalIgnoreCase)) return i;
        return 100;
    }
}
