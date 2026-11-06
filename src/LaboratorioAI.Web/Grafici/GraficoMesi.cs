using System.Globalization;
using System.Net;
using System.Text;

namespace LaboratorioAI.Web.Grafici;

/// <summary>
/// Grafico a barre mensile in SVG, generato sul server (nessuna libreria JavaScript). Una o due serie impilate,
/// con la linea che separa i 12 mesi precedenti dagli ultimi 12.
/// </summary>
public static class GraficoMesi
{
    private static readonly string[] Nomi = { "gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic" };

    /// <param name="mesi">Anno e mese di ogni barra, in ordine.</param>
    /// <param name="serie1">Valori della serie principale (in basso).</param>
    /// <param name="serie2">Valori della seconda serie impilata (facoltativa).</param>
    /// <param name="formato">Formato dei valori nel suggerimento (es. "€ #,##0" o "#,##0 campioni").</param>
    public static string Barre(IReadOnlyList<(int Anno, int Mese)> mesi, IReadOnlyList<decimal> serie1, IReadOnlyList<decimal>? serie2,
        string colore1, string? colore2, string nome1, string? nome2, string unita, string titolo, IReadOnlyList<bool>? evidenziati = null)
    {
        const int larg = 760, alt = 190, sx = 46, dx = 8, su = 14, giu = 30;
        var n = mesi.Count;
        if (n == 0) return "";
        var totali = Enumerable.Range(0, n).Select(i => Math.Max(0m, serie1[i]) + (serie2 is null ? 0m : Math.Max(0m, serie2[i]))).ToList();
        var massimo = Scala(totali.DefaultIfEmpty(0m).Max());
        var areaL = larg - sx - dx;
        var areaA = alt - su - giu;
        var passo = (double)areaL / n;
        var barra = Math.Max(4, passo * 0.7);
        var it = CultureInfo.GetCultureInfo("it-IT");
        string Y(decimal v) => (su + areaA - (double)(v / massimo) * areaA).ToString("0.#", CultureInfo.InvariantCulture);
        string D(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        var s = new StringBuilder();
        s.Append($"<svg viewBox=\"0 0 {larg} {alt}\" role=\"img\" aria-label=\"{WebUtility.HtmlEncode(titolo)}\" style=\"width:100%;height:auto;max-height:230px\">");
        // Griglia: 0, metà, massimo.
        foreach (var q in new[] { 0m, massimo / 2, massimo })
        {
            s.Append($"<line x1=\"{sx}\" x2=\"{larg - dx}\" y1=\"{Y(q)}\" y2=\"{Y(q)}\" stroke=\"#DDDDDD\" stroke-width=\"1\"/>");
            s.Append($"<text x=\"{sx - 5}\" y=\"{Y(q)}\" dy=\"3\" text-anchor=\"end\" font-size=\"10\" fill=\"#777\">{q.ToString("#,##0", it)}</text>");
        }
        for (var i = 0; i < n; i++)
        {
            var x = sx + passo * i + (passo - barra) / 2;
            var v1 = Math.Max(0m, serie1[i]);
            var v2 = serie2 is null ? 0m : Math.Max(0m, serie2[i]);
            var etichetta = $"{Nomi[mesi[i].Mese - 1]} {mesi[i].Anno}: {nome1} {serie1[i].ToString("#,##0", it)}{unita}"
                            + (serie2 is null ? "" : $", {nome2} {serie2[i].ToString("#,##0", it)}{unita}");
            var opaco = evidenziati is not null && i < evidenziati.Count && !evidenziati[i] ? " opacity=\"0.35\"" : "";
            s.Append($"<g{opaco}><title>").Append(WebUtility.HtmlEncode(etichetta)).Append("</title>");
            if (v1 > 0)
                s.Append($"<rect x=\"{D(x)}\" y=\"{Y(v1)}\" width=\"{D(barra)}\" height=\"{D((double)(v1 / massimo) * areaA)}\" fill=\"{colore1}\" rx=\"1.5\"/>");
            if (v2 > 0)
                s.Append($"<rect x=\"{D(x)}\" y=\"{Y(v1 + v2)}\" width=\"{D(barra)}\" height=\"{D((double)(v2 / massimo) * areaA)}\" fill=\"{colore2}\" rx=\"1.5\"/>");
            // Rettangolo trasparente per il suggerimento anche sui mesi a zero.
            s.Append($"<rect x=\"{D(sx + passo * i)}\" y=\"{su}\" width=\"{D(passo)}\" height=\"{areaA}\" fill=\"transparent\"/></g>");
            if (mesi[i].Mese is 1 or 4 or 7 or 10 || i == 0)
                s.Append($"<text x=\"{D(sx + passo * i + passo / 2)}\" y=\"{alt - giu + 13}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#777\">{Nomi[mesi[i].Mese - 1]} {mesi[i].Anno % 100:00}</text>");
        }
        if (n is 24 or 36)
        {
            var nomiAnni = n == 36 ? new[] { "due anni prima", "anno prima", "ultimi 12 mesi" } : new[] { "12 mesi precedenti", "ultimi 12 mesi" };
            for (var k = 0; k < nomiAnni.Length; k++)
            {
                if (k > 0)
                {
                    var xm = D(sx + passo * 12 * k);
                    s.Append($"<line x1=\"{xm}\" x2=\"{xm}\" y1=\"{su - 4}\" y2=\"{alt - giu}\" stroke=\"#444\" stroke-dasharray=\"4 3\"/>");
                }
                s.Append($"<text x=\"{D(sx + passo * (12 * k + 6))}\" y=\"{alt - 4}\" text-anchor=\"middle\" font-size=\"10\" fill=\"#444\">{nomiAnni[k]}</text>");
            }
        }
        s.Append("</svg>");
        return s.ToString();
    }

    /// <summary>Massimo dell'asse arrotondato a un valore «tondo».</summary>
    private static decimal Scala(decimal max)
    {
        if (max <= 0) return 1m;
        var p = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)max)));
        foreach (var m in new[] { 1m, 2m, 2.5m, 5m, 10m })
            if (m * p >= max) return m * p;
        return 10m * p;
    }
}
