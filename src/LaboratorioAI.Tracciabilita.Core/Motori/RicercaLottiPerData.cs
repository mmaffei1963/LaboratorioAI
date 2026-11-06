namespace LaboratorioAI.Tracciabilita.Core.Motori;

/// <summary>Un lotto che è stato messo in uso (impegnato) in un certo giorno: base della ricerca per data.</summary>
public sealed record LottoImpegnato(
    int IdLotto, int IdCategoria, int IdFornitore, string Fornitore, string Codice, string Articolo,
    DateOnly DataImpegno, DateOnly? DataScadenza);

/// <summary>I lotti proposti per un fornitore (i più recenti impegnati fino al giorno richiesto).</summary>
public sealed record LottiDelFornitore(int IdFornitore, string Fornitore, DateOnly UltimoImpegno, IReadOnlyList<LottoImpegnato> Lotti);

/// <summary>
/// Riproduce la «Ricerca Lotto Per Data» del vecchio programma: per un ingrediente (che può coprire più matrici)
/// e un giorno, per ogni fornitore ancora attivo propone gli ultimi N lotti con data di impegno non successiva al giorno.
/// Un fornitore è «attivo» se il suo ultimo lotto impegnato non è più vecchio di <c>giorniAttivita</c> giorni rispetto
/// all'ultimo lotto impegnato (da chiunque) per quell'ingrediente fino al giorno richiesto: così i fornitori dismessi
/// (es. un vecchio mulino) escono da soli, mentre un ingrediente poco usato (es. il Baking) resta visibile anche se
/// l'ultima consegna è di mesi fa. Funzione pura, senza accesso al db.
/// </summary>
public static class RicercaLottiPerData
{
    public const int GiorniAttivitaPredefiniti = 90;

    public static IReadOnlyList<LottiDelFornitore> Cerca(
        IEnumerable<LottoImpegnato> candidati,
        IReadOnlyCollection<int> categorie,
        DateOnly giorno,
        int lottiPerFornitore,
        int giorniAttivita = GiorniAttivitaPredefiniti,
        IReadOnlyCollection<int>? fornitoriEsclusi = null)
    {
        if (lottiPerFornitore < 1) lottiPerFornitore = 1;
        var utili = candidati
            .Where(c => categorie.Contains(c.IdCategoria) && c.DataImpegno <= giorno)
            .Where(c => fornitoriEsclusi is null || !fornitoriEsclusi.Contains(c.IdFornitore))
            .ToList();

        if (utili.Count == 0) return Array.Empty<LottiDelFornitore>();
        var inizioAttivita = utili.Max(c => c.DataImpegno).AddDays(-giorniAttivita);

        var risultato = new List<LottiDelFornitore>();
        foreach (var gruppo in utili.GroupBy(c => c.IdFornitore))
        {
            // Lo stesso lotto può comparire più volte (più accettazioni o più matrici): lo teniamo una volta.
            var lotti = gruppo.GroupBy(c => c.IdLotto).Select(g => g.OrderByDescending(x => x.DataImpegno).First())
                .OrderByDescending(c => c.DataImpegno).ThenByDescending(c => c.IdLotto).ToList();
            var ultimo = lotti[0].DataImpegno;
            if (ultimo < inizioAttivita) continue;
            risultato.Add(new LottiDelFornitore(gruppo.Key, lotti[0].Fornitore, ultimo, lotti.Take(lottiPerFornitore).ToList()));
        }
        return risultato.OrderByDescending(r => r.UltimoImpegno).ThenBy(r => r.Fornitore, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>I fornitori che avrebbero lotti, attivi o no: serve alla pagina per mostrare quali sono stati scartati.</summary>
    public static IReadOnlyList<(int IdFornitore, string Fornitore, DateOnly UltimoImpegno)> FornitoriScartati(
        IEnumerable<LottoImpegnato> candidati, IReadOnlyCollection<int> categorie, DateOnly giorno, int giorniAttivita = GiorniAttivitaPredefiniti)
    {
        var utili = candidati.Where(c => categorie.Contains(c.IdCategoria) && c.DataImpegno <= giorno).ToList();
        if (utili.Count == 0) return Array.Empty<(int, string, DateOnly)>();
        var inizio = utili.Max(c => c.DataImpegno).AddDays(-giorniAttivita);
        return utili
            .GroupBy(c => c.IdFornitore)
            .Select(g => (g.Key, g.First().Fornitore, g.Max(x => x.DataImpegno)))
            .Where(x => x.Item3 < inizio)
            .OrderByDescending(x => x.Item3).ToList();
    }
}
