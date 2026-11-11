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
/// Ricerca dei documenti AZW per descrizione (10/11/2026, estesa l'11/11/2026). Ricavi: voci delle fatture e note di
/// credito di vendita (dir.VociDocumentiVenditaAzw, testo completo dal 2019). Costi: registrazioni della contabilità
/// analitica con voce di costo (dir.RegistrazioniAnalitiche), con il fornitore. Filtri per area, cliente/fornitore,
/// commessa (chi ha fatto il lavoro) e zona del cliente alla data del documento (chi lo gestisce). Solo lettura.
/// </summary>
[RichiedePermesso(Permessi.RicercaFattureConsulta)]
public class RicercaFattureController : Controller
{
    private const int MassimoRighe = 5000;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IServizioAudit _audit;

    public RicercaFattureController(ContestoLaboratorioAI ctx, IServizioAudit audit) { _ctx = ctx; _audit = audit; }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Ricerca fatture per descrizione";
        return View();
    }

    /// <param name="Tipo">ricavi (predefinito), fatture, note, costi, tutti.</param>
    /// <param name="Aree">Codici delle aree separati da virgola; vuoto = tutte.</param>
    /// <param name="Commessa">Codice commessa AZW; «-» = senza commessa.</param>
    /// <param name="Zona">Sigla della zona del cliente; «-» = senza zona. Esclude i costi.</param>
    public record Criteri(DateTime Dal, DateTime Al, string? Testo, string? Cliente, string? Tipo, string? Aree, string? Commessa, string? Zona);

    private sealed record Nomi(Dictionary<string, string?> Aree, Dictionary<string, string?> Commesse, Dictionary<string, string?> Zone);

    private async Task<List<RigaRicercaDocumento>> CercaAsync(Criteri c, CancellationToken ct)
    {
        var dal = c.Dal.Date; var al = c.Al.Date;
        var tipo = (c.Tipo ?? "ricavi").Trim().ToLowerInvariant();
        var conRicavi = tipo is "ricavi" or "fatture" or "note" or "tutti" or "";
        var conCosti = (tipo is "costi" or "tutti") && string.IsNullOrWhiteSpace(c.Zona);
        var aree = (c.Aree ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cliente = c.Cliente?.Trim();
        var righe = new List<RigaRicercaDocumento>();

        if (conRicavi)
        {
            var q = _ctx.VociDocumentiVenditaAzw.AsNoTracking().Where(v => v.DataDocumento >= dal && v.DataDocumento <= al);
            if (tipo == "fatture") q = q.Where(v => v.TipoDocumento == VociDocumentiVendita.Fattura);
            else if (tipo == "note") q = q.Where(v => v.TipoDocumento == VociDocumentiVendita.NotaCredito);
            if (!string.IsNullOrEmpty(cliente)) q = q.Where(v => v.CodiceCliente == cliente || (v.RagioneSociale != null && v.RagioneSociale.Contains(cliente)));
            var voci = await q.ToListAsync(ct);

            // Zona alla data del documento: dallo storico; se un cliente non ne ha ancora, la zona attuale dell'anagrafica.
            var storico = (await _ctx.StoricoZoneClienti.AsNoTracking().ToListAsync(ct))
                .GroupBy(z => z.CodiceCliente, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<StoricoZonaCliente>)g.ToList(), StringComparer.OrdinalIgnoreCase);
            var zoneAttuali = (await _ctx.AzwClienti.AsNoTracking().Select(a => new { a.CodiceCliente, a.Zona }).ToListAsync(ct))
                .GroupBy(a => a.CodiceCliente, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Zona, StringComparer.OrdinalIgnoreCase);

            // Commessa di riserva dalla contabilità analitica delle fatture (stessa chiave delle provvigioni: cliente|anno|numero).
            var analitica = new Dictionary<string, List<(string Centro, string Commessa)>>(StringComparer.OrdinalIgnoreCase);
            if (voci.Any(v => string.IsNullOrWhiteSpace(v.Commessa) && v.TipoDocumento == VociDocumentiVendita.Fattura))
            {
                var chiavi = voci.Where(v => string.IsNullOrWhiteSpace(v.Commessa)).Select(Chiave).Distinct().ToList();
                foreach (var gruppo in chiavi.Chunk(1000))
                {
                    var g = gruppo.ToList();
                    var rr = await _ctx.AzwRigheFattura.AsNoTracking().Where(r => g.Contains(r.ChiaveFattura) && r.Commessa != null)
                        .Select(r => new { r.ChiaveFattura, r.Centro, r.Commessa }).ToListAsync(ct);
                    foreach (var r in rr)
                    {
                        if (!analitica.TryGetValue(r.ChiaveFattura, out var l)) analitica[r.ChiaveFattura] = l = [];
                        l.Add((r.Centro, r.Commessa!));
                    }
                }
            }

            foreach (var v in voci)
            {
                var commessa = string.IsNullOrWhiteSpace(v.Commessa) ? null : v.Commessa.Trim().ToUpperInvariant();
                if (commessa is null && analitica.TryGetValue(Chiave(v), out var la))
                    commessa = RicercaDocumentiAzw.CommessaDiRiserva(la, v.Centro)?.Trim().ToUpperInvariant();
                var zona = storico.TryGetValue(v.CodiceCliente, out var st) && st.Count > 0
                    ? RicercaDocumentiAzw.ZonaAl(st, v.DataDocumento)
                    : zoneAttuali.GetValueOrDefault(v.CodiceCliente);
                righe.Add(new RigaRicercaDocumento(false, v.TipoDocumento, v.DataDocumento, $"{v.NumeroDocumento}/{v.AnnoDocumento}",
                    v.CodiceCliente, v.RagioneSociale, v.Descrizione, v.Quantita, v.Prezzo, v.Sconto, v.Importo,
                    v.Centro ?? "", MotoreConsuntivo.AreaDi(v.Centro ?? ""), v.Voce, commessa, zona));
            }
        }

        if (conCosti)
        {
            var vociRicavo = await _ctx.ClassificazioniVoci.AsNoTracking().Where(v => v.Ricavo).Select(v => v.CodiceVoce).ToListAsync(ct);
            var q = _ctx.RegistrazioniAnalitiche.AsNoTracking()
                .Where(r => r.DataRegistrazione >= dal && r.DataRegistrazione <= al && !vociRicavo.Contains(r.Voce));
            if (!string.IsNullOrEmpty(cliente))
                q = q.Where(r => r.CodiceControparte == cliente || (r.RagioneSocialeControparte != null && r.RagioneSocialeControparte.Contains(cliente)));
            foreach (var r in await q.ToListAsync(ct))
            {
                var numero = string.IsNullOrWhiteSpace(r.NumeroDocumento)
                    ? $"reg. {r.NumeroRegistrazione}/{r.AnnoRegistrazione}"
                    : $"{r.NumeroDocumento} (reg. {r.NumeroRegistrazione}/{r.AnnoRegistrazione})";
                righe.Add(new RigaRicercaDocumento(true, r.TipoDocumento ?? "", r.DataRegistrazione, numero,
                    r.CodiceControparte, r.RagioneSocialeControparte, r.Descrizione ?? "", null, null, null, -r.Importo,
                    r.Centro, MotoreConsuntivo.AreaDi(r.Centro), r.Voce,
                    string.IsNullOrWhiteSpace(r.Commessa) ? null : r.Commessa.Trim().ToUpperInvariant(), null));
            }
        }

        var parole = VociDocumentiVendita.Parole(c.Testo);
        return righe
            .Where(r => aree.Count == 0 || aree.Contains(r.Area))
            .Where(r => RicercaDocumentiAzw.Corrisponde(r.Commessa, c.Commessa))
            .Where(r => r.Costo || RicercaDocumentiAzw.Corrisponde(r.Zona, c.Zona))
            .Where(r => RicercaDocumentiAzw.ContieneTesto(r, parole))
            .OrderBy(r => r.Data).ThenBy(r => r.Costo).ThenBy(r => r.Numero, StringComparer.Ordinal)
            .ToList();

        static string Chiave(VoceDocumentoVenditaAzw v) => $"{v.CodiceCliente}|{v.AnnoDocumento}|{v.NumeroDocumento}";
    }

    private async Task<Nomi> NomiAsync(CancellationToken ct)
    {
        var aree = MotoreConsuntivo.AreeStandard.ToDictionary(a => a.Codice, a => (string?)a.Etichetta, StringComparer.OrdinalIgnoreCase);
        foreach (var c in await _ctx.CentriCostoAzw.AsNoTracking().ToListAsync(ct))
        {
            var area = MotoreConsuntivo.AreaDi(c.Codice);
            if (!aree.ContainsKey(area)) aree[area] = c.Descrizione;
        }
        var commesse = (await _ctx.CommesseAzw.AsNoTracking().ToListAsync(ct))
            .GroupBy(x => x.Codice.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First().Descrizione, StringComparer.OrdinalIgnoreCase);
        var zone = (await _ctx.ResponsabiliCliente.AsNoTracking().ToListAsync(ct))
            .GroupBy(x => x.Sigla.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First().Nome, StringComparer.OrdinalIgnoreCase);
        return new Nomi(aree, commesse, zone);
    }

    private static string? Etichetta(Dictionary<string, string?> nomi, string? codice) =>
        string.IsNullOrWhiteSpace(codice) ? null
        : nomi.TryGetValue(codice, out var n) && !string.IsNullOrWhiteSpace(n) ? $"{n} ({codice})" : codice;

    [HttpGet("api/ricerca-fatture/opzioni")]
    public async Task<IActionResult> Opzioni(CancellationToken ct)
    {
        var nomi = await NomiAsync(ct);
        var zoneUsate = await _ctx.AzwClienti.AsNoTracking().Where(a => a.Zona != null).Select(a => a.Zona!).Distinct().ToListAsync(ct);
        var zone = zoneUsate.Concat(nomi.Zone.Keys).Select(z => z.Trim().ToUpperInvariant()).Where(z => z.Length > 0)
            .Distinct().OrderBy(z => nomi.Zone.GetValueOrDefault(z) ?? z, StringComparer.CurrentCultureIgnoreCase)
            .Select(z => new { codice = z, nome = Etichetta(nomi.Zone, z) });
        var aree = nomi.Aree.OrderBy(a => RicercaDocumentiAzw.OrdineArea(a.Key)).ThenBy(a => a.Key, StringComparer.Ordinal)
            .Select(a => new { codice = a.Key, nome = a.Value ?? a.Key });
        var commesse = nomi.Commesse.OrderBy(c => c.Value ?? c.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(c => new { codice = c.Key, nome = Etichetta(nomi.Commesse, c.Key) });
        var ultima = await _ctx.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita).OrderByDescending(i => i.DataOra)
            .Select(i => (DateTime?)i.DataOra).FirstOrDefaultAsync(ct);
        return Ok(new { aree, commesse, zone, aggiornatoAl = ultima is DateTime u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : (DateTime?)null });
    }

    /// <summary>Suggerimenti per il campo cliente/fornitore: clienti dall'anagrafica AZW, fornitori dalle registrazioni.</summary>
    [HttpGet("api/ricerca-fatture/controparti")]
    public async Task<IActionResult> Controparti([FromQuery] string? q, CancellationToken ct)
    {
        var t = (q ?? "").Trim();
        if (t.Length < 2) return Ok(Array.Empty<object>());
        var clienti = await _ctx.AzwClienti.AsNoTracking()
            .Where(a => a.CodiceCliente == t || (a.RagioneSociale != null && a.RagioneSociale.Contains(t)))
            .OrderBy(a => a.RagioneSociale).Take(15).Select(a => new { codice = a.CodiceCliente, nome = a.RagioneSociale, tipo = "Cliente" })
            .ToListAsync(ct);
        var fornitori = await _ctx.RegistrazioniAnalitiche.AsNoTracking()
            .Where(r => r.ClienteFornitore == "F" && r.CodiceControparte != null &&
                        (r.CodiceControparte == t || (r.RagioneSocialeControparte != null && r.RagioneSocialeControparte.Contains(t))))
            .Select(r => new { codice = r.CodiceControparte!, nome = r.RagioneSocialeControparte, tipo = "Fornitore" })
            .Distinct().OrderBy(r => r.nome).Take(15).ToListAsync(ct);
        return Ok(clienti.Concat(fornitori));
    }

    [HttpGet("api/ricerca-fatture/cerca")]
    public async Task<IActionResult> Cerca([FromQuery] Criteri c, CancellationToken ct)
    {
        if (c.Al < c.Dal) return BadRequest(new { messaggio = "La data «al» è precedente alla data «dal»." });
        var righe = await CercaAsync(c, ct);
        var nomi = await NomiAsync(ct);
        var riepilogo = RicercaDocumentiAzw.Riepilogo(righe);
        return Ok(new
        {
            righe = righe.Take(MassimoRighe).Select(r => new
            {
                r.Costo, r.TipoDocumento, data = r.Data, r.Numero, r.CodiceControparte, r.Controparte, r.Descrizione,
                r.Quantita, r.Prezzo, r.Sconto, r.Importo, r.Centro, area = nomi.Aree.GetValueOrDefault(r.Area) ?? r.Area, r.Voce,
                commessa = Etichetta(nomi.Commesse, r.Commessa), zona = Etichetta(nomi.Zone, r.Zona)
            }),
            troncato = righe.Count > MassimoRighe,
            massimo = MassimoRighe,
            riepilogo = new
            {
                riepilogo.Ricavi, riepilogo.Fatture, riepilogo.NoteCredito, riepilogo.Costi, riepilogo.DocumentiCosto,
                riepilogo.Clienti, riepilogo.Fornitori, riepilogo.Righe, riepilogo.Differenza,
                perArea = riepilogo.PerArea.Select(a => new { area = nomi.Aree.GetValueOrDefault(a.Area) ?? a.Area, a.Ricavi, a.Costi })
            }
        });
    }

    [HttpGet("api/ricerca-fatture/excel")]
    public async Task<IActionResult> Excel([FromQuery] Criteri c, CancellationToken ct)
    {
        if (c.Al < c.Dal) return BadRequest(new { messaggio = "La data «al» è precedente alla data «dal»." });
        var righe = await CercaAsync(c, ct);
        var nomi = await NomiAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "ricercafatture.excel", EsitoOperazione.Successo,
            dettaglio: new { c.Dal, c.Al, c.Testo, c.Cliente, c.Tipo, c.Aree, c.Commessa, c.Zona, Righe = righe.Count }, ct: ct);
        var filtri = string.Join(" — ", new[]
        {
            string.IsNullOrWhiteSpace(c.Testo) ? null : $"testo «{c.Testo}»",
            string.IsNullOrWhiteSpace(c.Cliente) ? null : $"cliente/fornitore «{c.Cliente}»",
            string.IsNullOrWhiteSpace(c.Aree) ? null : "aree " + string.Join(", ", c.Aree.Split(',').Select(a => nomi.Aree.GetValueOrDefault(a.Trim()) ?? a.Trim())),
            string.IsNullOrWhiteSpace(c.Commessa) ? null : "commessa " + (c.Commessa == RicercaDocumentiAzw.Nessuno ? "nessuna" : Etichetta(nomi.Commesse, c.Commessa)),
            string.IsNullOrWhiteSpace(c.Zona) ? null : "responsabile " + (c.Zona == RicercaDocumentiAzw.Nessuno ? "nessuno" : Etichetta(nomi.Zone, c.Zona)),
        }.Where(x => x is not null));
        var file = EsportatoreRicercaFatture.Genera(righe, RicercaDocumentiAzw.Riepilogo(righe), c.Dal, c.Al, filtri,
            a => nomi.Aree.GetValueOrDefault(a) ?? a, x => Etichetta(nomi.Commesse, x), x => Etichetta(nomi.Zone, x));
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Documenti_AZW_{c.Dal:yyyyMMdd}-{c.Al:yyyyMMdd}.xlsx");
    }
}
