using System.Text;
using LaboratorioAI.Tracciabilita.Core.Importazione;
using LaboratorioAI.Tracciabilita.Dati;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Importazione;

/// <summary>
/// Controllo di sola lettura: cerca i lotti (stesso articolo, fornitore e codice) che hanno
/// più arrivi in date molto distanti. Può essere un codice riusato o un errore di registrazione.
/// </summary>
public static class ControlloCodiciRipetuti
{
    public const int GiorniSoglia = 45;

    public sealed record Voce(
        string Articolo, string? Fornitore, string Codice, int Arrivi, DateOnly Primo, DateOnly Ultimo,
        int Giorni, decimal Totale, string Unita, decimal Giacenza, string Righe);

    public static async Task<List<Voce>> TrovaAsync(ContestoTracciabilita db, int idAzienda)
    {
        var acc = await (from a in db.Accettazioni.AsNoTracking()
                         where a.IdAzienda == idAzienda && a.IdLotto != null
                         select new { IdLotto = a.IdLotto!.Value, a.DataArrivo, a.Quantita, a.UnitaMisura, a.NoteReclami })
            .ToListAsync();
        var lotti = await (from l in db.Lotti.AsNoTracking()
                           where l.IdAzienda == idAzienda
                           join ar in db.Articoli.AsNoTracking() on l.IdArticolo equals ar.IdArticolo
                           join f in db.Fornitori.AsNoTracking() on l.IdFornitore equals f.IdFornitore into fj
                           from f in fj.DefaultIfEmpty()
                           select new { l.IdLotto, l.Codice, Articolo = ar.Descrizione, Fornitore = f != null ? f.RagioneSociale : null })
            .ToDictionaryAsync(x => x.IdLotto);
        var giacenze = await db.MovimentiLotto.AsNoTracking().Where(m => m.IdAzienda == idAzienda)
            .GroupBy(m => m.IdLotto).Select(g => new { Id = g.Key, S = g.Sum(m => m.Quantita) })
            .ToDictionaryAsync(x => x.Id, x => x.S);

        var risultato = new List<Voce>();
        foreach (var g in acc.GroupBy(a => a.IdLotto))
        {
            if (!lotti.TryGetValue(g.Key, out var l)) continue;
            var date = g.Select(x => x.DataArrivo).OrderBy(d => d).ToList();
            var giorni = date[^1].DayNumber - date[0].DayNumber;
            if (g.Count() < 2 || giorni < GiorniSoglia) continue;
            var righe = string.Join(" ", g.OrderBy(x => x.DataArrivo)
                .Select(x => System.Text.RegularExpressions.Regex.Match(x.NoteReclami ?? "", @"riga (\d+)") is { Success: true } m ? m.Groups[1].Value : "?"));
            risultato.Add(new Voce(l.Articolo, l.Fornitore, l.Codice, g.Count(), date[0], date[^1], giorni,
                g.Sum(x => x.Quantita), g.First().UnitaMisura, giacenze.GetValueOrDefault(g.Key), righe));
        }
        return risultato.OrderByDescending(v => v.Giorni).ThenBy(v => v.Articolo).ToList();
    }

    public static string ScriviCsv(IEnumerable<Voce> voci)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ARTICOLO;FORNITORE;CODICE LOTTO;N. ARRIVI;PRIMO ARRIVO;ULTIMO ARRIVO;GIORNI TRA PRIMO E ULTIMO;TOTALE ARRIVATO;UNITA;GIACENZA ORA;RIGHE ACCESS");
        foreach (var v in voci)
            sb.AppendLine(string.Join(';', Campo(v.Articolo), Campo(v.Fornitore ?? ""), Campo(v.Codice), v.Arrivi,
                v.Primo.ToString("dd/MM/yyyy"), v.Ultimo.ToString("dd/MM/yyyy"), v.Giorni,
                v.Totale.ToString("0.##"), Campo(v.Unita), v.Giacenza.ToString("0.##"), Campo(v.Righe)));
        return sb.ToString();
    }

    // Come negli altri file: niente formule per Excel e virgolette dove servono.
    private static string Campo(string c)
    {
        if (c.Length > 0 && "=+-@".Contains(c[0])) c = "'" + c;
        return c.Contains(';') || c.Contains('"') || c.Contains('\n') ? "\"" + c.Replace("\"", "\"\"") + "\"" : c;
    }
}
