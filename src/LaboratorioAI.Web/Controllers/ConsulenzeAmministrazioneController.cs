using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Amministrazione → Consulenze: elenco dei documenti tipo (documentazione di riferimento dei
/// rapportini) e registro delle deleghe operative per cliente. Nessuna cancellazione fisica:
/// le voci non più valide si disattivano. Clienti letti da ArcLab in sola lettura.
/// </summary>
[RichiedePermesso(Permessi.RapportiniGestione)]
public class ConsulenzeAmministrazioneController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IRepositoryConsulenze _arclab;
    private readonly IServizioAudit _audit;
    private readonly IContestoUtente _utente;

    public ConsulenzeAmministrazioneController(ContestoLaboratorioAI ctx, IRepositoryAnagrafiche anagrafiche,
        IRepositoryConsulenze arclab, IServizioAudit audit, IContestoUtente utente)
    {
        _ctx = ctx; _anagrafiche = anagrafiche; _arclab = arclab; _audit = audit; _utente = utente;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Consulenze: documenti e deleghe";
        return View();
    }

    // ── Documenti tipo ──────────────────────────────────────────────

    [HttpGet("api/consulenze-admin/documenti-tipo")]
    public async Task<IActionResult> DocumentiTipo(CancellationToken ct) =>
        Json(await _ctx.DocumentiTipo.AsNoTracking().OrderByDescending(x => x.Attivo)
            .ThenBy(x => x.Ordine).ThenBy(x => x.Nome).ToListAsync(ct));

    public record DocumentoTipoDto(int? IdDocumentoTipo, string? Nome, string? Area, bool RichiamaLaboratorio,
        bool PianoAnalitico, int Ordine, bool Attivo);

    [HttpPost("api/consulenze-admin/documenti-tipo")]
    public async Task<IActionResult> SalvaDocumentoTipo([FromBody] DocumentoTipoDto d, CancellationToken ct)
    {
        var nome = (d.Nome ?? "").Trim();
        if (nome.Length == 0 || nome.Length > 200) return BadRequest(new { messaggio = "Nome obbligatorio (max 200 caratteri)." });
        var area = string.IsNullOrWhiteSpace(d.Area) ? null : d.Area.Trim();
        if (area?.Length > 50) return BadRequest(new { messaggio = "Area troppo lunga (max 50 caratteri)." });

        DocumentoTipo? x;
        if (d.IdDocumentoTipo is int id && id > 0)
        {
            x = await _ctx.DocumentiTipo.FirstOrDefaultAsync(t => t.IdDocumentoTipo == id, ct);
            if (x is null) return NotFound(new { messaggio = "Documento tipo non trovato." });
        }
        else
        {
            if (await _ctx.DocumentiTipo.AnyAsync(t => t.Nome == nome, ct))
                return BadRequest(new { messaggio = "Esiste già un documento con questo nome." });
            x = new DocumentoTipo();
            _ctx.DocumentiTipo.Add(x);
        }
        x.Nome = nome; x.Area = area; x.RichiamaLaboratorio = d.RichiamaLaboratorio; x.PianoAnalitico = d.PianoAnalitico;
        x.Ordine = d.Ordine; x.Attivo = d.Attivo;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "consulenze.documentotipo.salva", EsitoOperazione.Successo,
            "DocumentoTipo", x.IdDocumentoTipo.ToString(), new { x.Nome, x.Area, x.RichiamaLaboratorio, x.PianoAnalitico, x.Attivo }, ct);
        return Json(x);
    }

    // ── Deleghe operative per cliente ───────────────────────────────

    [HttpGet("api/consulenze-admin/clienti")]
    public async Task<IActionResult> Clienti([FromQuery] string? filtro, CancellationToken ct)
    {
        var f = (filtro ?? "").Trim();
        if (f.Length < 2) return Json(Array.Empty<object>());
        var clienti = await _anagrafiche.ClientiAsync(ct);
        // Ricerca tollerante e ordinata per pertinenza (15/10/2026); un numero cerca anche l'Id ArcLab.
        var trovati = RicercaNomi.Cerca(clienti, c => c.Nome, f, 50);
        if (int.TryParse(f, out var id) && clienti.FirstOrDefault(c => c.Id == id) is { } perId && !trovati.Contains(perId)) trovati.Insert(0, perId);
        return Json(trovati.Select(c => new { c.Id, Nome = c.Nome.Trim() }));
    }

    [HttpGet("api/consulenze-admin/deleghe")]
    public async Task<IActionResult> Deleghe([FromQuery] string? testo, [FromQuery] bool ancheNonAttive, CancellationToken ct)
    {
        var q = _ctx.DelegheClienti.AsNoTracking().Include(x => x.Insediamenti).AsQueryable();
        if (!ancheNonAttive) q = q.Where(x => x.Attivo);
        if (!string.IsNullOrWhiteSpace(testo))
        {
            var t = testo.Trim();
            q = int.TryParse(t, out var id)
                ? q.Where(x => x.IdClienteArcLab == id || x.RagioneSociale.Contains(t))
                : q.Where(x => x.RagioneSociale.Contains(t) || x.Estremi.Contains(t));
        }
        var righe = await q.OrderBy(x => x.RagioneSociale).ThenByDescending(x => x.IdDelegaCliente).Take(1000).ToListAsync(ct);
        var oggi = DateTime.Today;
        return Json(righe.Select(x => new
        {
            x.IdDelegaCliente, x.IdClienteArcLab, x.RagioneSociale, x.Estremi,
            DataSottoscrizione = x.DataSottoscrizione?.ToString("yyyy-MM-dd"),
            DataFine = x.DataFine?.ToString("yyyy-MM-dd"),
            DataEmissione = x.DataEmissione?.ToString("yyyy-MM-dd"),
            x.Note, x.Attivo, InEssere = x.ValidaAl(oggi),
            x.IdOrganicoArcLab, x.Consulente, Stato = x.Stato.ToString(), x.Generata, x.IdRapportino,
            HaWord = x.PercorsoWord != null, HaPdf = x.PercorsoPdf != null, HaFirmata = x.PercorsoFirmata != null,
            x.TuttiInsediamenti,
            Sedi = x.TuttiInsediamenti || !x.Insediamenti.Any(i => i.Attivo) ? "tutte le sedi" : string.Join("; ", x.Insediamenti.Where(i => i.Attivo).Select(i => i.Nome)),
            IdInsediamenti = x.Insediamenti.Where(i => i.Attivo).Select(i => i.IdInsediamentoArcLab).ToList()
        }));
    }

    public record DelegaDto(int? IdDelegaCliente, int IdCliente, string? Estremi, DateTime? DataSottoscrizione,
        DateTime? DataFine, string? Note, bool Attivo, int? IdOrganico = null,
        bool TuttiInsediamenti = true, int[]? IdInsediamenti = null);

    [HttpPost("api/consulenze-admin/deleghe")]
    public async Task<IActionResult> SalvaDelega([FromBody] DelegaDto d, CancellationToken ct)
    {
        // Delega generata dal programma: qui si cambiano solo fine validità, note e stato attivo
        // (la firma si registra con "Registra firma").
        if (d.IdDelegaCliente is int idg && idg > 0)
        {
            var g = await _ctx.DelegheClienti.FirstOrDefaultAsync(t => t.IdDelegaCliente == idg, ct);
            if (g is { Generata: true })
            {
                if (d.DataFine is DateTime f && g.DataSottoscrizione is DateTime s0 && f.Date < s0.Date)
                    return BadRequest(new { messaggio = "La data di fine è precedente alla sottoscrizione." });
                g.DataFine = d.DataFine?.Date;
                g.Note = string.IsNullOrWhiteSpace(d.Note) ? null : (d.Note.Trim().Length > 500 ? d.Note.Trim()[..500] : d.Note.Trim());
                g.Attivo = d.Attivo;
                g.IdUtenteModifica = _utente.IdUtente;
                g.DataModifica = DateTime.UtcNow;
                await _ctx.SaveChangesAsync(ct);
                await _audit.RegistraAsync(AreaFunzionale.Consulenze, "consulenze.delega.salva", EsitoOperazione.Successo,
                    "DelegaCliente", g.IdDelegaCliente.ToString(), new { g.DataFine, g.Attivo }, ct);
                return Json(new { g.IdDelegaCliente });
            }
        }

        if (d.IdCliente == 0) return BadRequest(new { messaggio = "Scegli il cliente." });
        var estremi = (d.Estremi ?? "").Trim();
        if (estremi.Length == 0 || estremi.Length > 300)
            return BadRequest(new { messaggio = "Indica gli estremi della delega come vanno riportati nel rapporto (max 300 caratteri)." });
        if (d.DataSottoscrizione is not DateTime ds) return BadRequest(new { messaggio = "Indica la data di sottoscrizione." });
        if (d.IdOrganico is not int idOrg || idOrg == 0) return BadRequest(new { messaggio = "Scegli il consulente delegato: la delega vale per cliente e consulente." });
        var consulente = (await _arclab.ConsulentiAsync(ct)).FirstOrDefault(c => c.IdOrganico == idOrg);
        if (consulente is null) return BadRequest(new { messaggio = "Consulente non trovato tra il personale in servizio." });
        if (d.DataFine is DateTime df && df.Date < ds.Date) return BadRequest(new { messaggio = "La data di fine è precedente alla sottoscrizione." });
        var cliente = (await _anagrafiche.ClientiAsync(ct)).FirstOrDefault(c => c.Id == d.IdCliente);
        if (cliente is null) return BadRequest(new { messaggio = "Cliente non trovato in ArcLab." });

        DelegaCliente? x;
        if (d.IdDelegaCliente is int id && id > 0)
        {
            x = await _ctx.DelegheClienti.Include(t => t.Insediamenti).FirstOrDefaultAsync(t => t.IdDelegaCliente == id, ct);
            if (x is null) return NotFound(new { messaggio = "Delega non trovata." });
        }
        else
        {
            x = new DelegaCliente();
            _ctx.DelegheClienti.Add(x);
        }

        // Sedi operative: tutte, oppure quelle scelte (devono essere del cliente).
        var ids = (d.IdInsediamenti ?? Array.Empty<int>()).Where(i => i != 0).Distinct().ToList();
        if (!d.TuttiInsediamenti && ids.Count == 0)
            return BadRequest(new { messaggio = "Scegli le sedi operative per cui vale la delega, oppure \"tutte le sedi\"." });
        var insCliente = await _arclab.InsediamentiAsync(d.IdCliente, ct);
        var scelti = d.TuttiInsediamenti ? new List<InsediamentoRapportino>() : insCliente.Where(i => ids.Contains(i.IdInsediamento)).ToList();
        if (scelti.Count != (d.TuttiInsediamenti ? 0 : ids.Count))
            return BadRequest(new { messaggio = "Una o più sedi scelte non appartengono al cliente." });
        // Sedi precedenti disattivate (nessuna cancellazione fisica), poi si aggiungono quelle scelte ora.
        foreach (var vecchia in x.Insediamenti.Where(t => t.Attivo)) vecchia.Attivo = false;
        x.TuttiInsediamenti = d.TuttiInsediamenti;
        foreach (var i in scelti)
            x.Insediamenti.Add(new DelegaInsediamento { IdInsediamentoArcLab = i.IdInsediamento, Nome = i.Nome.Length > 255 ? i.Nome[..255] : i.Nome });

        x.IdClienteArcLab = d.IdCliente;
        x.RagioneSociale = cliente.Nome.Trim().Length > 150 ? cliente.Nome.Trim()[..150] : cliente.Nome.Trim();
        x.Estremi = estremi;
        x.DataSottoscrizione = ds.Date;
        x.DataFine = d.DataFine?.Date;
        x.Note = string.IsNullOrWhiteSpace(d.Note) ? null : (d.Note.Trim().Length > 500 ? d.Note.Trim()[..500] : d.Note.Trim());
        x.Attivo = d.Attivo;
        x.IdOrganicoArcLab = consulente.IdOrganico;
        x.Consulente = consulente.Nome.Length > 120 ? consulente.Nome[..120] : consulente.Nome;
        x.Stato = StatoDelega.Sottoscritta;
        x.IdUtenteModifica = _utente.IdUtente;
        x.DataModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "consulenze.delega.salva", EsitoOperazione.Successo,
            "DelegaCliente", x.IdDelegaCliente.ToString(), new { x.IdClienteArcLab, x.Estremi, x.DataSottoscrizione, x.DataFine, x.Attivo }, ct);
        return Json(new { x.IdDelegaCliente });
    }

    [HttpGet("api/consulenze-admin/clienti/{idCliente:int}/insediamenti")]
    public async Task<IActionResult> Insediamenti(int idCliente, CancellationToken ct) =>
        Json(await _arclab.InsediamentiAsync(idCliente, ct));

    // ── Dati dei consulenti (qualifica, iscrizione all'Albo) ────────

    [HttpGet("api/consulenze-admin/consulenti")]
    public async Task<IActionResult> Consulenti(CancellationToken ct)
    {
        var organico = await _arclab.ConsulentiAsync(ct);
        var dati = await _ctx.ConsulentiDati.AsNoTracking().ToDictionaryAsync(x => x.IdOrganicoArcLab, ct);
        return Json(organico.Select(o =>
        {
            dati.TryGetValue(o.IdOrganico, out var dd);
            return new { o.IdOrganico, o.Nome, Qualifica = dd?.Qualifica, IscrizioneAlbo = dd?.IscrizioneAlbo, Compilato = dd is not null };
        }).OrderBy(x => x.Compilato ? 0 : 1).ThenBy(x => x.Nome));
    }

    public record ConsulenteDatiDto(int IdOrganico, string? Qualifica, string? IscrizioneAlbo);

    [HttpPost("api/consulenze-admin/consulenti")]
    public async Task<IActionResult> SalvaConsulente([FromBody] ConsulenteDatiDto d, CancellationToken ct)
    {
        var o = (await _arclab.ConsulentiAsync(ct)).FirstOrDefault(c => c.IdOrganico == d.IdOrganico);
        if (o is null) return BadRequest(new { messaggio = "Consulente non trovato tra il personale in servizio." });
        var qual = (d.Qualifica ?? "").Trim();
        var albo = (d.IscrizioneAlbo ?? "").Trim();
        if (qual.Length == 0) return BadRequest(new { messaggio = "Indica la qualifica." });
        if (qual.Length > 150 || albo.Length > 200) return BadRequest(new { messaggio = "Testo troppo lungo." });
        var x = await _ctx.ConsulentiDati.FirstOrDefaultAsync(t => t.IdOrganicoArcLab == d.IdOrganico, ct);
        if (x is null) { x = new ConsulenteDati { IdOrganicoArcLab = d.IdOrganico }; _ctx.ConsulentiDati.Add(x); }
        x.Nome = o.Nome.Length > 120 ? o.Nome[..120] : o.Nome;
        x.Qualifica = qual;
        x.IscrizioneAlbo = albo.Length == 0 ? null : albo;
        x.IdUtenteModifica = _utente.IdUtente;
        x.DataModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "consulenze.consulente.salva", EsitoOperazione.Successo,
            "ConsulenteDati", d.IdOrganico.ToString(), new { x.Qualifica, x.IscrizioneAlbo }, ct);
        return Json(new { ok = true });
    }
}
