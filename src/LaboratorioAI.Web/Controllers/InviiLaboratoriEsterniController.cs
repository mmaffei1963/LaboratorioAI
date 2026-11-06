using System.Net;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Invio campioni ai laboratori esterni (06/11/2026): dalla query Access di MARE.A, l'Excel dei campioni non certificati
/// con prove del laboratorio non ancora esitate né escluse, nel periodo di data analisi scelto, inviato per e-mail al
/// laboratorio con copia (e risposte) a refertazione@mare-a.com. ArcLab solo in lettura; registro degli invii qui.
/// </summary>
[RichiedePermesso(Permessi.LaboratoriEsterniInvio)]
public class InviiLaboratoriEsterniController : Controller
{
    /// <summary>Centro Ricerche Marine, il laboratorio usato nel 90% degli invii: preselezionato.</summary>
    public const int IdLabPredefinito = -867474552;
    public const string IndirizzoRefertazione = "refertazione@mare-a.com";

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryLaboratoriEsterni _repo;
    private readonly IServizioEmail _email;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly ILogger<InviiLaboratoriEsterniController> _log;

    public InviiLaboratoriEsterniController(ContestoLaboratorioAI ctx, IRepositoryLaboratoriEsterni repo, IServizioEmail email,
        IContestoUtente utente, IServizioAudit audit, ILogger<InviiLaboratoriEsterniController> log)
    {
        _ctx = ctx; _repo = repo; _email = email; _utente = utente; _audit = audit; _log = log;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Invio campioni ai laboratori esterni";
        return View();
    }

    // ── Laboratori (ArcLab) con i loro indirizzi (LaboratorioAI) ──

    [HttpGet("api/invii-lab-esterni/laboratori")]
    public async Task<IActionResult> Laboratori(CancellationToken ct)
    {
        var labs = await _repo.LaboratoriAsync(ct);
        var contatti = await _ctx.ContattiLaboratoriEsterni.AsNoTracking().Where(c => c.Attivo).ToDictionaryAsync(c => c.IdLabEsternoArcLab, ct);
        return Ok(new
        {
            predefinito = IdLabPredefinito,
            copiaFissa = IndirizzoRefertazione,
            laboratori = labs.Select(l =>
            {
                contatti.TryGetValue(l.Id, out var c);
                return new { l.Id, l.Nome, EmailDestinatari = c?.EmailDestinatari, EmailCopia = c?.EmailCopia, c?.Note };
            })
        });
    }

    public record RichiestaContatto(int IdLabEsterno, string? Nome, string? EmailDestinatari, string? EmailCopia, string? Note);

    [HttpPost("api/invii-lab-esterni/contatti")]
    public async Task<IActionResult> SalvaContatto([FromBody] RichiestaContatto req, CancellationToken ct)
    {
        var dest = Indirizzi(req.EmailDestinatari);
        var copia = Indirizzi(req.EmailCopia);
        var errati = dest.Concat(copia).Where(e => !IndirizziEmail.Formato.IsMatch(e)).ToList();
        if (errati.Count > 0) return BadRequest(new { messaggio = "Indirizzi non validi: " + string.Join(", ", errati) });

        var ora = DateTime.UtcNow;
        var c = await _ctx.ContattiLaboratoriEsterni.FirstOrDefaultAsync(x => x.IdLabEsternoArcLab == req.IdLabEsterno, ct);
        if (c is null)
        {
            c = new ContattoLaboratorioEsterno { IdLabEsternoArcLab = req.IdLabEsterno, DataCreazione = ora };
            _ctx.ContattiLaboratoriEsterni.Add(c);
        }
        c.NomeLaboratorio = Tronca(req.Nome?.Trim() is { Length: > 0 } n ? n : $"Laboratorio {req.IdLabEsterno}", 300)!;
        c.EmailDestinatari = dest.Count == 0 ? null : string.Join("; ", dest);
        c.EmailCopia = copia.Count == 0 ? null : string.Join("; ", copia);
        c.Note = Tronca(req.Note?.Trim(), 1000);
        c.Attivo = true; c.DataCancellazione = null;
        c.DataUltimaModifica = ora; c.IdUtente = _utente.IdUtente;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.LaboratoriEsterni, "labesterni.contatti", EsitoOperazione.Successo,
            tipoEntita: nameof(ContattoLaboratorioEsterno), idEntita: req.IdLabEsterno.ToString(),
            dettaglio: new { c.NomeLaboratorio, c.EmailDestinatari, c.EmailCopia }, ct: ct);
        return Ok(new { c.EmailDestinatari, c.EmailCopia });
    }

    // ── Anteprima ──

    [HttpGet("api/invii-lab-esterni/anteprima")]
    public async Task<IActionResult> Anteprima([FromQuery] int idLab, [FromQuery] DateTime dal, [FromQuery] DateTime al, CancellationToken ct)
    {
        if (al < dal) return BadRequest(new { messaggio = "La data «al» è precedente alla data «dal»." });
        var prove = await _repo.ProveDaInviareAsync(idLab, dal, al, ct);
        var ids = prove.Select(p => p.IdProva).Distinct().ToList();
        var gia = await GiaInviateAsync(idLab, ids, ct);

        var campioni = prove.GroupBy(p => p.IdCampione).Select(g =>
        {
            var f = g.First();
            var inviate = g.Where(p => gia.ContainsKey(p.IdProva)).Select(p => gia[p.IdProva]).ToList();
            return new
            {
                idCampione = g.Key, f.NumeroCampione, f.ClienteInsediamento, f.DenominazioneCampione, f.DataAnalisi,
                f.TemperaturaPrelievo, f.DataRicezione,
                prove = EsportatoreInvioLaboratorioEsterno.Righe(g).Select(p => new { p.Prova, p.RifBiblio, p.RifMetodo }),
                idProve = g.Select(p => p.IdProva).ToList(),
                giaInviato = inviate.Count == 0 ? null : inviate.Max()!.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                tutteInviate = inviate.Count == g.Count()
            };
        }).OrderBy(c => c.DataAnalisi).ThenBy(c => c.NumeroCampione).ToList();
        return Ok(campioni);
    }

    /// <summary>Data dell'ultimo invio riuscito per prova, allo stesso laboratorio.</summary>
    private async Task<Dictionary<int, DateTime>> GiaInviateAsync(int idLab, List<int> idProve, CancellationToken ct)
    {
        if (idProve.Count == 0) return new();
        var righe = await _ctx.RigheInvioLaboratorioEsterno.AsNoTracking()
            .Where(r => idProve.Contains(r.IdProvaArcLab) && r.Invio!.IdLabEsternoArcLab == idLab && r.Invio.Riuscito)
            .Select(r => new { r.IdProvaArcLab, r.Invio!.DataInvio })
            .ToListAsync(ct);
        return righe.GroupBy(r => r.IdProvaArcLab).ToDictionary(g => g.Key, g => DateTime.SpecifyKind(g.Max(x => x.DataInvio), DateTimeKind.Utc));
    }

    /// <summary>Scarica l'Excel senza inviarlo (per controllo).</summary>
    [HttpGet("api/invii-lab-esterni/excel")]
    public async Task<IActionResult> Excel([FromQuery] int idLab, [FromQuery] DateTime dal, [FromQuery] DateTime al,
        [FromQuery] string? campioni, CancellationToken ct)
    {
        var (prove, nome) = await SelezioneAsync(idLab, dal, al, campioni, ct);
        var file = EsportatoreInvioLaboratorioEsterno.Genera(prove, nome, dal, al);
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            EsportatoreInvioLaboratorioEsterno.NomeFile(nome, DateTime.Now));
    }

    private async Task<(List<Core.Modelli.ProvaDaInviare> Prove, string NomeLab)> SelezioneAsync(
        int idLab, DateTime dal, DateTime al, string? campioni, CancellationToken ct)
    {
        var prove = (await _repo.ProveDaInviareAsync(idLab, dal, al, ct)).ToList();
        if (!string.IsNullOrWhiteSpace(campioni))
        {
            var scelti = campioni.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var v) ? v : 0).Where(v => v != 0).ToHashSet();
            prove = prove.Where(p => scelti.Contains(p.IdCampione)).ToList();
        }
        var nome = prove.FirstOrDefault()?.LabEsterno?.Trim()
                   ?? (await _repo.LaboratoriAsync(ct)).FirstOrDefault(l => l.Id == idLab)?.Nome?.Trim()
                   ?? $"Laboratorio {idLab}";
        return (prove, nome);
    }

    // ── Invio ──

    public record RichiestaInvio(int IdLab, DateTime Dal, DateTime Al, List<int>? Campioni, string? Oggetto, string? Testo);

    [HttpPost("api/invii-lab-esterni/invia")]
    public async Task<IActionResult> Invia([FromBody] RichiestaInvio req, CancellationToken ct)
    {
        if (req.Campioni is null || req.Campioni.Count == 0)
            return BadRequest(new { messaggio = "Nessun campione selezionato." });
        var contatto = await _ctx.ContattiLaboratoriEsterni.AsNoTracking().FirstOrDefaultAsync(c => c.IdLabEsternoArcLab == req.IdLab && c.Attivo, ct);
        var dest = Indirizzi(contatto?.EmailDestinatari);
        if (dest.Count == 0)
            return BadRequest(new { messaggio = "Per questo laboratorio non ci sono indirizzi e-mail: inseriscili in «Indirizzi dei laboratori»." });
        var copia = Indirizzi(contatto?.EmailCopia).Append(IndirizzoRefertazione).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var (prove, nome) = await SelezioneAsync(req.IdLab, req.Dal, req.Al, string.Join(",", req.Campioni), ct);
        if (prove.Count == 0) return BadRequest(new { messaggio = "Nessuna prova da inviare per i campioni scelti (forse già esitate o certificate)." });

        var ora = DateTime.UtcNow;
        var oraLocale = DateTime.Now;
        var file = EsportatoreInvioLaboratorioEsterno.Genera(prove, nome, req.Dal, req.Al);
        var nomeFile = EsportatoreInvioLaboratorioEsterno.NomeFile(nome, oraLocale);
        var oggetto = string.IsNullOrWhiteSpace(req.Oggetto) ? OggettoPredefinito(oraLocale) : req.Oggetto.Trim();
        var testo = string.IsNullOrWhiteSpace(req.Testo) ? TestoPredefinito : req.Testo.Trim();
        var nCampioni = prove.Select(p => p.IdCampione).Distinct().Count();

        var invio = new InvioLaboratorioEsterno
        {
            IdLabEsternoArcLab = req.IdLab, NomeLaboratorio = Tronca(nome, 300)!, Dal = req.Dal.Date, Al = req.Al.Date,
            Destinatari = Tronca(string.Join("; ", dest), 1000)!, Copia = Tronca(string.Join("; ", copia), 1000),
            Oggetto = Tronca(oggetto, 300)!, Testo = Tronca(testo, 4000), NomeFile = nomeFile, FileExcel = file,
            NumeroCampioni = nCampioni, NumeroProve = prove.Count, DataInvio = ora,
            IdUtente = _utente.IdUtente, NomeUtente = Tronca(_utente.NomeCompleto ?? _utente.Email, 100),
            Righe = prove.Select(p => new RigaInvioLaboratorioEsterno
            {
                IdCampioneArcLab = p.IdCampione, IdProvaArcLab = p.IdProva, NumeroCampione = Tronca(p.NumeroCampione, 50)!
            }).ToList()
        };

        try
        {
            var corpo = "<p>" + WebUtility.HtmlEncode(testo).Replace("\n", "<br>") + "</p>" +
                        $"<p style=\"color:#555\">{nCampioni} campioni, {EsportatoreInvioLaboratorioEsterno.Righe(prove).Count} prove – file allegato: {WebUtility.HtmlEncode(nomeFile)}</p>" +
                        "<p>MARE.A s.r.l. – Laboratorio di Analisi<br>Via E. Toti, 2 – 47841 Cattolica (RN) – tel. 0541 830442</p>";
            invio.MessageId = Tronca(await _email.InviaConAllegatiAsync(dest, oggetto, corpo,
                new[] { new AllegatoEmail(nomeFile, file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") },
                ct, rispondiA: new[] { IndirizzoRefertazione }, copia: copia), 300);
            invio.Riuscito = true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Invio campioni al laboratorio esterno {Lab} non riuscito", nome);
            invio.Riuscito = false;
            invio.Errore = Tronca(ex.Message, 2000);
        }

        _ctx.InviiLaboratoriEsterni.Add(invio);
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.LaboratoriEsterni, "labesterni.invio",
            invio.Riuscito ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            tipoEntita: nameof(InvioLaboratorioEsterno), idEntita: invio.IdInvioLaboratorioEsterno.ToString(),
            dettaglio: new { nome, invio.NumeroCampioni, invio.NumeroProve, invio.Destinatari, invio.Errore }, ct: ct);

        if (!invio.Riuscito)
            return StatusCode(502, new { messaggio = "E-mail non inviata: " + invio.Errore + " (l'invio è registrato come non riuscito; puoi riprovare)." });
        return Ok(new { invio.IdInvioLaboratorioEsterno, invio.NumeroCampioni, invio.NumeroProve, invio.Destinatari, invio.Copia });
    }

    // ── Registro ──

    [HttpGet("api/invii-lab-esterni/registro")]
    public async Task<IActionResult> Registro(CancellationToken ct)
    {
        var righe = await _ctx.InviiLaboratoriEsterni.AsNoTracking().OrderByDescending(i => i.DataInvio).Take(200)
            .Select(i => new { i.IdInvioLaboratorioEsterno, i.NomeLaboratorio, i.DataInvio, i.Dal, i.Al, i.NumeroCampioni, i.NumeroProve,
                               i.Destinatari, i.Copia, i.NomeUtente, i.Riuscito, i.Errore })
            .ToListAsync(ct);
        return Ok(righe.Select(i => new
        {
            i.IdInvioLaboratorioEsterno, i.NomeLaboratorio, DataInvio = DateTime.SpecifyKind(i.DataInvio, DateTimeKind.Utc),
            i.Dal, i.Al, i.NumeroCampioni, i.NumeroProve, i.Destinatari, i.Copia, i.NomeUtente, i.Riuscito, i.Errore
        }));
    }

    [HttpGet("api/invii-lab-esterni/registro/{id:int}/excel")]
    public async Task<IActionResult> ExcelInviato(int id, CancellationToken ct)
    {
        var i = await _ctx.InviiLaboratoriEsterni.AsNoTracking().FirstOrDefaultAsync(x => x.IdInvioLaboratorioEsterno == id, ct);
        if (i?.FileExcel is null) return NotFound();
        return File(i.FileExcel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", i.NomeFile);
    }

    [HttpGet("api/invii-lab-esterni/testi")]
    public IActionResult Testi() => Ok(new { oggetto = OggettoPredefinito(DateTime.Now), testo = TestoPredefinito });

    // ── Helper ──

    public static string OggettoPredefinito(DateTime data) => $"MARE.A – Campioni inviati il {data:dd/MM/yyyy}";

    public const string TestoPredefinito =
        "In allegato l'elenco dei campioni inviati con le prove richieste. Per qualsiasi chiarimento rispondete a questa e-mail.";

    private static List<string> Indirizzi(string? testo) =>
        (testo ?? "").Split(new[] { ';', ',', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string? Tronca(string? s, int max) => s is null ? null : (s.Length > max ? s[..max] : s);
}
