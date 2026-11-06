using System.Net;
using System.Text;
using LaboratorioAI.Tracciabilita.Web.Models;

namespace LaboratorioAI.Tracciabilita.Web.Servizi;

/// <summary>Il testo dell'ordine come e-mail (HTML semplice, leggibile da qualsiasi programma di posta).</summary>
public static class CorpoOrdine
{
    public static string Oggetto(ModelloStampaOrdine o) => $"Ordine n. {o.Numero} del {o.DataOrdine:dd/MM/yyyy} - {o.Azienda}";

    public static string Html(ModelloStampaOrdine o)
    {
        static string E(string? t) => WebUtility.HtmlEncode(t ?? "");
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2933\">");
        sb.Append($"<p>Spett.le <strong>{E(o.Fornitore)}</strong>,</p>");
        sb.Append($"<p>vi trasmettiamo l'ordine <strong>n. {E(o.Numero)}</strong> del {o.DataOrdine:dd/MM/yyyy}");
        if (o.DataConsegnaPrevista is { } d) sb.Append($", con consegna prevista il <strong>{d:dd/MM/yyyy}</strong>");
        sb.Append(".</p>");
        sb.Append("<table style=\"border-collapse:collapse;min-width:420px\"><thead><tr>");
        foreach (var h in new[] { "Articolo", "Quantità", "Colli", "Note" })
            sb.Append($"<th style=\"text-align:left;border-bottom:2px solid #0b5d6b;padding:4px 10px\">{h}</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var r in o.Righe)
        {
            sb.Append("<tr>");
            sb.Append($"<td style=\"border-bottom:1px solid #d5dde3;padding:4px 10px\">{E(r.Articolo)}{(r.Matrice is null ? "" : " <em>(" + E(r.Matrice) + ")</em>")}</td>");
            sb.Append($"<td style=\"border-bottom:1px solid #d5dde3;padding:4px 10px\">{r.Quantita:0.##} {E(r.UnitaMisura)}</td>");
            sb.Append($"<td style=\"border-bottom:1px solid #d5dde3;padding:4px 10px\">{r.Colli}</td>");
            sb.Append($"<td style=\"border-bottom:1px solid #d5dde3;padding:4px 10px\">{E(r.Note)}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        if (!string.IsNullOrWhiteSpace(o.Note)) sb.Append($"<p><strong>Note:</strong> {E(o.Note)}</p>");
        sb.Append("<p>Vi chiediamo di confermare l'ordine rispondendo a questa e-mail.</p>");
        sb.Append($"<p>Cordiali saluti<br/>{E(o.Azienda)}{(string.IsNullOrWhiteSpace(o.Sede) ? "" : " - " + E(o.Sede))}</p></div>");
        return sb.ToString();
    }
}
