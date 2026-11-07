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
    public string? Documento { get; set; }
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

    /// <summary>Per ogni lotto di ingresso, il documento con cui è arrivato (tipo, numero e data).</summary>
    public static async Task<Dictionary<int, string>> DocumentiPerLottoAsync(this ContestoTracciabilita db, IReadOnlyCollection<int> idLotti, CancellationToken ct)
    {
        // Il legame è sull'accettazione (Accettazione.IdLotto); nei dati importati Lotto.IdAccettazione può mancare.
        var righe = await db.Accettazioni.AsNoTracking()
            .Where(a => a.IdLotto != null && idLotti.Contains(a.IdLotto.Value))
            .OrderBy(a => a.IdAccettazione)
            .Select(a => new { IdLotto = a.IdLotto!.Value, a.TipoDocumento, a.NumeroDocumento, a.DataDocumento })
            .ToListAsync(ct);
        var esito = new Dictionary<int, string>();
        foreach (var r in righe)
        {
            var testo = TestoDocumento(r.TipoDocumento, r.NumeroDocumento, r.DataDocumento);
            if (testo is not null && !esito.ContainsKey(r.IdLotto)) esito[r.IdLotto] = testo;
        }
        return esito;
    }
}
