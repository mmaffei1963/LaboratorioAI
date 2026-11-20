using LaboratorioAI.Tracciabilita.Dati;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Models;

/// <summary>Una riga della scheda di rintracciabilità: un lotto di ingresso usato per un ingrediente.</summary>
public class RigaSchedaRintracciabilita
{
    public string Articolo { get; set; } = "";
    public string Fornitore { get; set; } = "";
    public string Lotto { get; set; } = "";
    public DateOnly? Scadenza { get; set; }
    /// <summary>Le bolle del lotto (una o più), ognuna con tipo, data e numero.</summary>
    public List<string> Documenti { get; set; } = new();
    public DateOnly? ImpiegatoDal { get; set; }
}

/// <summary>Un ingrediente della ricetta con i lotti usati.</summary>
public class GruppoSchedaRintracciabilita
{
    public string Ingrediente { get; set; } = "";
    public List<RigaSchedaRintracciabilita> Righe { get; set; } = new();
}

/// <summary>Scheda stampabile (una pagina A4): prodotto, giorno di produzione, lotto prodotto e lotti di ingresso usati.</summary>
public class ModelloSchedaRintracciabilita
{
    public string Azienda { get; set; } = "";
    public string Sede { get; set; } = "";
    public string Prodotto { get; set; } = "";
    public DateOnly Giorno { get; set; }
    public string? LottoProduzione { get; set; }
    public string? Quantita { get; set; }
    public string? Note { get; set; }
    public bool Annullata { get; set; }
    public DateTime Stampata { get; set; } = DateTime.Now;
    public List<GruppoSchedaRintracciabilita> Gruppi { get; set; } = new();
}

public static class SchedaRintracciabilitaAiuti
{
    /// <summary>Testo del documento di arrivo, come nella scheda del vecchio programma: «Bolla del 05/10/26 N°2300».</summary>
    public static string? TestoDocumento(string? tipo, string? numero, DateOnly? data)
    {
        var parti = new List<string>();
        if (!string.IsNullOrWhiteSpace(tipo)) parti.Add(tipo.Trim());
        if (data is { } d) parti.Add("del " + d.ToString("dd/MM/yy"));
        if (!string.IsNullOrWhiteSpace(numero)) parti.Add("N°" + numero.Trim());
        return parti.Count == 0 ? null : string.Join(" ", parti);
    }

    /// <summary>
    /// Per ogni lotto di ingresso, le bolle (forniture) con cui è arrivato e che erano impegnate al giorno indicato.
    /// Un lotto può arrivare più volte con bolle diverse: nella scheda compaiono tutte, non solo la prima.
    /// Una fornitura conta se è arrivata entro il giorno e: ha un impegno proprio non successivo al giorno; oppure è la prima
    /// del lotto; oppure, per lotti registrati prima dell'impegno per fornitura, non ha impegni propri.
    /// </summary>
    public static async Task<Dictionary<int, List<string>>> DocumentiPerLottoAsync(
        this ContestoTracciabilita db, IReadOnlyCollection<int> idLotti, DateOnly giorno, CancellationToken ct)
    {
        var forniture = await db.Accettazioni.AsNoTracking()
            .Where(a => !a.Annullata && a.IdLotto != null && idLotti.Contains(a.IdLotto.Value))
            .OrderBy(a => a.IdAccettazione)
            .Select(a => new { a.IdAccettazione, IdLotto = a.IdLotto!.Value, a.DataArrivo, a.TipoDocumento, a.NumeroDocumento, a.DataDocumento })
            .ToListAsync(ct);
        var usi = await db.LottiInUso.AsNoTracking().Where(u => idLotti.Contains(u.IdLotto))
            .Select(u => new { u.IdLotto, u.IdAccettazione, u.Inizio }).ToListAsync(ct);

        var esito = new Dictionary<int, List<string>>();
        foreach (var gruppo in forniture.GroupBy(f => f.IdLotto))
        {
            var elenco = gruppo.ToList();
            var usiLotto = usi.Where(u => u.IdLotto == gruppo.Key).ToList();
            var regimeNuovo = usiLotto.Any(u => u.IdAccettazione != null);
            for (var i = 0; i < elenco.Count; i++)
            {
                var f = elenco[i];
                if (f.DataArrivo > giorno) continue;
                var propri = usiLotto.Where(u => u.IdAccettazione == f.IdAccettazione).Select(u => DateOnly.FromDateTime(u.Inizio.ToLocalTime())).ToList();
                bool conta = propri.Count > 0 ? propri.Min() <= giorno : (i == 0 || !regimeNuovo);
                if (!conta) continue;
                var testo = TestoDocumento(f.TipoDocumento, f.NumeroDocumento, f.DataDocumento);
                if (testo is null) continue;
                if (!esito.TryGetValue(gruppo.Key, out var lista)) esito[gruppo.Key] = lista = new List<string>();
                if (!lista.Contains(testo)) lista.Add(testo);
            }
        }
        return esito;
    }
}
