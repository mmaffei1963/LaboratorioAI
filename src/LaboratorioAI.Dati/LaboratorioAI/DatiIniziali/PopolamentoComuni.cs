using System.Globalization;
using System.Reflection;
using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.DatiIniziali;

/// <summary>
/// Carica l'elenco ISTAT dei comuni dalla risorsa incorporata (04/11/2026).
///
/// Si puo' rieseguire: confronta per codice ISTAT, aggiunge i nuovi,
/// corregge quelli cambiati e spegne quelli spariti dall'elenco, senza
/// cancellare niente — i documenti vecchi citano ancora i comuni soppressi.
/// I CAP si riallineano allo stesso modo.
///
/// Per aggiornare l'elenco basta sostituire comuni-istat.csv e ricompilare.
/// Fonte: elenco pubblico «comuni-json» (dati ISTAT con CAP), la stessa
/// usata dalla versione di prova, cosi' i due programmi propongono gli
/// stessi comuni.
/// </summary>
public static class PopolamentoComuni
{
    private const string Risorsa = "LaboratorioAI.Dati.Anagrafiche.comuni-istat.csv";

    private sealed record Voce(
        string CodiceIstat, string Nome, string SiglaProvincia, string Provincia,
        string CodiceRegione, string Regione, string? CodiceCatastale, IReadOnlyList<string> Cap);

    public static async Task EseguiAsync(ContestoLaboratorioAI ctx, CancellationToken ct = default)
    {
        var dallElenco = Leggi();
        if (dallElenco.Count == 0) return;   // risorsa mancante: meglio non toccare niente

        var esistenti = await ctx.Comuni
            .Include(c => c.Cap)
            .ToDictionaryAsync(c => c.CodiceIstat, ct);

        foreach (var v in dallElenco)
        {
            if (!esistenti.TryGetValue(v.CodiceIstat, out var c))
            {
                c = new Comune { CodiceIstat = v.CodiceIstat };
                ctx.Comuni.Add(c);
            }

            // ISTAT cambia nomi, accorpa province, assegna CAP: la riga si riallinea.
            c.Nome = v.Nome;
            c.SiglaProvincia = v.SiglaProvincia;
            c.Provincia = v.Provincia;
            c.CodiceRegione = v.CodiceRegione;
            c.Regione = v.Regione;
            c.CodiceCatastale = v.CodiceCatastale;
            c.Attivo = true;

            AllineaCap(ctx, c, v.Cap);
        }

        var nellElenco = dallElenco.Select(v => v.CodiceIstat).ToHashSet(StringComparer.Ordinal);
        foreach (var c in esistenti.Values.Where(c => c.Attivo && !nellElenco.Contains(c.CodiceIstat)))
            c.Attivo = false;

        await ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// I CAP di un comune, nell'ordine dell'elenco: il primo e' quello
    /// proposto. Qui si cancella davvero, perche' un CAP tolto dal
    /// comune non e' un dato storico: e' un errore da correggere, e i
    /// documenti gia' prodotti conservano il loro per conto proprio.
    /// </summary>
    private static void AllineaCap(ContestoLaboratorioAI ctx, Comune c, IReadOnlyList<string> cap)
    {
        foreach (var vecchio in c.Cap.Where(x => !cap.Contains(x.Cap)).ToList())
        {
            c.Cap.Remove(vecchio);
            ctx.Remove(vecchio);
        }

        for (var i = 0; i < cap.Count; i++)
        {
            var esistente = c.Cap.FirstOrDefault(x => x.Cap == cap[i]);
            if (esistente is null) c.Cap.Add(new ComuneCap { Cap = cap[i], Ordine = i });
            else esistente.Ordine = i;
        }
    }

    private static List<Voce> Leggi()
    {
        var voci = new List<Voce>(8000);

        using var flusso = Assembly.GetExecutingAssembly().GetManifestResourceStream(Risorsa);
        if (flusso is null) return voci;

        using var lettore = new StreamReader(flusso);
        lettore.ReadLine();   // intestazione

        while (lettore.ReadLine() is { } riga)
        {
            if (riga.Length == 0) continue;
            var p = riga.Split(';');
            if (p.Length < 8) continue;

            voci.Add(new Voce(
                p[0], p[1],
                p[2].ToUpper(CultureInfo.InvariantCulture), p[3],
                p[4], p[5],
                string.IsNullOrWhiteSpace(p[6]) ? null : p[6],
                p[7].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
        }

        return voci;
    }
}
