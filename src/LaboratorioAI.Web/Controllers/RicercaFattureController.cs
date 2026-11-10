using System.Globalization;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Ricerca delle fatture di vendita per descrizione (10/11/2026): legge la tabella di lavoro dir.VociDocumentiVenditaAzw
/// (testo completo delle fatture AZW dal 2019, ricomposto all'importazione). Note di credito in negativo. Solo lettura.
/// </summary>
[RichiedePermesso(Permessi.RicercaFattureConsulta)]
public class RicercaFattureController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IServizioAudit _audit;
    private static readonly CompareInfo Confronto = CultureInfo.GetCultureInfo("it-IT").CompareInfo;

    public RicercaFattureController(ContestoLaboratorioAI ctx, IServizioAudit audit) { _ctx = ctx; _audit = audit; }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Ricerca fatture per descrizione";
        return View();
    }

    public record Criteri(DateTime Dal, DateTime Al, string? Testo, string? Cliente, string? Tipo);

    private async Task<List<VoceDocumentoVenditaAzw>> CercaAsync(Criteri c, CancellationToken ct)
    {
        var q = _ctx.VociDocumentiVenditaAzw.AsNoTracking().Where(v => v.DataDocumento >= c.Dal.Date && v.DataDocumento <= c.Al.Date);
        if (c.Tipo == "fatture") q = q.Where(v => v.TipoDocumento == VociDocumentiVendita.Fattura);
        else if (c.Tipo == "note") q = q.Where(v => v.TipoDocumento == VociDocumentiVendita.NotaCredito);
        if (!string.IsNullOrWhiteSpace(c.Cliente))
        {
            var cl = c.Cliente.Trim();
            q = q.Where(v => v.CodiceCliente == cl || (v.RagioneSociale != null && v.RagioneSociale.Contains(cl)));
        }
        var voci = await q.OrderBy(v => v.DataDocumento).ThenBy(v => v.AnnoDocumento).ThenBy(v => v.NumeroDocumento).ThenBy(v => v.NumeroRiga).ToListAsync(ct);

        // Parole separate da «;» o «,»: basta che la voce ne contenga una (senza badare a maiuscole e accenti).
        var parole = VociDocumentiVendita.Parole(c.Testo);
        if (parole.Count == 0) return voci;
        const CompareOptions op = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        return voci.Where(v => parole.Any(p => Confronto.IndexOf(v.Descrizione, p, op) >= 0)).ToList();
    }

    [HttpGet("api/ricerca-fatture/cerca")]
    public async Task<IActionResult> Cerca([FromQuery] Criteri c, CancellationToken ct)
    {
        if (c.Al < c.Dal) return BadRequest(new { messaggio = "La data «al» è precedente alla data «dal»." });
        if (VociDocumentiVendita.Parole(c.Testo).Count == 0 && string.IsNullOrWhiteSpace(c.Cliente))
            return BadRequest(new { messaggio = "Scrivi almeno una parola da cercare (o un cliente)." });
        var voci = await CercaAsync(c, ct);
        var ultima = await _ctx.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita).OrderByDescending(i => i.DataOra)
            .Select(i => (DateTime?)i.DataOra).FirstOrDefaultAsync(ct);
        return Ok(new
        {
            voci = voci.Take(5000).Select(v => new
            {
                v.DataDocumento, v.TipoDocumento, Numero = $"{v.NumeroDocumento}/{v.AnnoDocumento}", v.CodiceCliente, v.RagioneSociale,
                v.Descrizione, v.Quantita, v.Prezzo, v.Sconto, v.Importo, v.Centro, v.Voce
            }),
            troncato = voci.Count > 5000,
            totale = voci.Sum(v => v.Importo),
            fatture = voci.Where(v => v.TipoDocumento == VociDocumentiVendita.Fattura).Select(v => (v.AnnoDocumento, v.NumeroDocumento)).Distinct().Count(),
            note = voci.Where(v => v.TipoDocumento == VociDocumentiVendita.NotaCredito).Select(v => (v.AnnoDocumento, v.NumeroDocumento)).Distinct().Count(),
            clienti = voci.Select(v => v.CodiceCliente).Distinct().Count(),
            numeroVoci = voci.Count,
            aggiornatoAl = ultima is DateTime u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : (DateTime?)null
        });
    }

    [HttpGet("api/ricerca-fatture/excel")]
    public async Task<IActionResult> Excel([FromQuery] Criteri c, CancellationToken ct)
    {
        var voci = await CercaAsync(c, ct);
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "ricercafatture.excel", EsitoOperazione.Successo,
            dettaglio: new { c.Dal, c.Al, c.Testo, c.Cliente, Voci = voci.Count }, ct: ct);
        var file = EsportatoreRicercaFatture.Genera(voci, c.Dal, c.Al, c.Testo ?? "");
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Fatture_{c.Dal:yyyyMMdd}-{c.Al:yyyyMMdd}.xlsx");
    }
}
