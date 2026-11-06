using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Limiti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Limiti normativi (17/10/2026, fase 1a): schede limite e fonti ufficiali, estrazione con l'AI e validazione umana.
/// Procedura «Gestione dei limiti normativi» Rev. 0. Le segnalazioni in «Da Validare» arrivano con la fase 1b.
/// </summary>
[RichiedePermesso(Permessi.LimitiConsulta)]
public class LimitiNormativiController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly ServizioLimitiNormativi _limiti;
    private readonly IRepositoryLimitiArcLab _arclab;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IMemoryCache _cache;
    private readonly ServizioVerificaLimiti _verifica;

    public LimitiNormativiController(ContestoLaboratorioAI ctx, ServizioLimitiNormativi limiti, IRepositoryLimitiArcLab arclab,
        IContestoUtente utente, IServizioAudit audit, IMemoryCache cache, ServizioVerificaLimiti verifica, ServizioCategorieLimiti categorie)
    {
        _ctx = ctx; _limiti = limiti; _arclab = arclab; _utente = utente; _audit = audit; _cache = cache; _verifica = verifica; _categorie = categorie;
    }
    private readonly ServizioCategorieLimiti _categorie;

    private OperatoreLimiti Operatore => new(_utente.IdUtente ?? 0, _utente.NomeCompleto ?? _utente.Email ?? "?");

    private Task Audit(string azione, string tipo, int id, object? dettaglio, CancellationToken ct, EsitoOperazione esito = EsitoOperazione.Successo) =>
        _audit.RegistraAsync(AreaFunzionale.LimitiNormativi, azione, esito, tipo, id.ToString(), dettaglio, ct);

    [HttpGet("/LimitiNormativi")]
    public IActionResult Index()
    {
        ViewData["Titolo"] = "Limiti normativi";
        ViewData["PuoValidare"] = _utente.HaPermesso(Permessi.LimitiValida);
        ViewData["IdUtente"] = _utente.IdUtente ?? 0;
        return View();
    }

    // ── Opzioni per i menu della pagina ──────────────────────────

    private static object Voci<T>(Func<T, string> nome) where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new { valore = Convert.ToInt32(v), nome = nome(v) });

    [HttpGet("/api/limiti/opzioni")]
    public IActionResult Opzioni() => Json(new
    {
        famiglie = Voci<FamigliaNormativa>(RegoleLimiti.NomeFamiglia),
        famiglieFase1 = RegoleLimiti.FamiglieFase1.Select(f => (int)f),
        tipiFonte = Voci<TipoFonteNormativa>(t => t switch
        {
            TipoFonteNormativa.EurLex => "EUR-Lex", TipoFonteNormativa.Normattiva => "Normattiva",
            TipoFonteNormativa.GazzettaUfficiale => "Gazzetta Ufficiale", TipoFonteNormativa.MinisteroSalute => "Ministero della Salute",
            TipoFonteNormativa.BollettinoRegionale => "Bollettino regionale", _ => "Altro"
        }),
        tipiLimite = Voci<TipoLimiteNormativo>(RegoleLimiti.NomeTipo),
        operatori = Voci<OperatoreLimite>(o => o switch
        {
            OperatoreLimite.ValoreMassimo => "≤ valore massimo", OperatoreLimite.MinoreDi => "< minore di",
            OperatoreLimite.ValoreMinimo => "≥ valore minimo", OperatoreLimite.Intervallo => "intervallo min – max",
            _ => "assente in (quantità)"
        }),
        regole = Voci<RegolaValutazioneLimite>(r => r == RegolaValutazioneLimite.Puntuale ? "Valore puntuale" : "Piano di campionamento (n, c, m, M)"),
        stati = Voci<StatoSchedaLimite>(RegoleLimiti.NomeStato)
    });

    // ── Schede ───────────────────────────────────────────────────

    private static object Riga(LimiteNormativo s) => new
    {
        id = s.IdLimiteNormativo, codice = s.CodiceScheda, versione = s.Versione, famiglia = (int)s.Famiglia,
        famigliaNome = RegoleLimiti.NomeFamiglia(s.Famiglia), parametro = s.Parametro, matrice = s.MatriceCategoria,
        limite = RegoleLimiti.Descrivi(s), tipo = (int)s.TipoLimite, tipoNome = RegoleLimiti.NomeTipo(s.TipoLimite),
        stato = (int)s.Stato, statoNome = RegoleLimiti.NomeStato(s.Stato), riferimento = s.Riferimento,
        propostaDa = s.PropostaDa, dataProposta = s.DataProposta, validataDa = s.ValidataDa, dataValidazione = s.DataValidazione,
        abbinamenti = s.Abbinamenti.Count(a => a.Attivo),
        // 18/10/2026: per la validazione rapida in tabella
        citazione = s.Citazione, condizioni = s.Condizioni, testoSegnalazione = s.TestoSegnalazione,
        abbinamentiNomi = s.Abbinamenti.Where(a => a.Attivo).Select(a => $"{a.NomeParametro ?? "?"} ({a.IdParametroArcLab})").ToList()
    };

    [HttpGet("/api/limiti/schede")]
    public async Task<IActionResult> Schede([FromQuery] int? famiglia, [FromQuery] bool storico = false, CancellationToken ct = default)
    {
        var q = _ctx.LimitiNormativi.AsNoTracking().Include(x => x.Abbinamenti).AsQueryable();
        if (famiglia is int f) q = q.Where(x => (int)x.Famiglia == f);
        if (!storico) q = q.Where(x => x.Stato != StatoSchedaLimite.Respinta && x.Stato != StatoSchedaLimite.Superata);
        var elenco = await q.OrderBy(x => x.CodiceScheda).ThenByDescending(x => x.Versione).ToListAsync(ct);
        // titolo breve della fonte (senza caricare testi e copie salvate)
        var fonti = await _ctx.VersioniFonti.AsNoTracking()
            .Select(v => new { v.IdVersioneFonte, v.Fonte!.Titolo }).ToDictionaryAsync(v => v.IdVersioneFonte, v => v.Titolo, ct);
        var idUtente = _utente.IdUtente ?? 0;
        var puoValidare = _utente.HaPermesso(Permessi.LimitiValida);
        return Json(elenco.Select(x => new
        {
            riga = Riga(x),
            validabile = puoValidare && RegoleLimiti.MotivoNonValidabile(x, idUtente) is null,
            fonte = x.IdVersioneFonte is int iv && fonti.TryGetValue(iv, out var t) ? TitoloBreve(t) : "a mano"
        }));
    }

    /// <summary>«D.Lgs. 23 febbraio 2023, n. 18 – Qualità…» → «D.Lgs. 23 febbraio 2023, n. 18».</summary>
    private static string TitoloBreve(string t)
    {
        var i = t.IndexOf(" – ", StringComparison.Ordinal);
        return i > 0 ? t[..i] : (t.Length > 60 ? t[..60] + "…" : t);
    }

    public record RespingiPiu(List<int> Ids, string? Testo);

    /// <summary>
    /// Validazione rapida (18/10/2026): più schede insieme dalla tabella, con le stesse regole della validazione singola
    /// (non chi le ha proposte o corrette, citazione o nota obbligatoria). Ogni scheda ha la sua riga nel registro attività.
    /// </summary>
    [HttpPost("/api/limiti/schede/valida")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> ValidaInsieme([FromBody] RespingiPiu r, CancellationToken ct)
    {
        if (r.Ids is not { Count: > 0 }) return BadRequest(new { messaggio = "Nessuna scheda selezionata." });
        int ok = 0; var errori = new List<string>();
        foreach (var id in r.Ids.Distinct().Take(500))
        {
            var (s, err) = await _limiti.ValidaAsync(id, r.Testo, Operatore, ct);
            if (s is null) { errori.Add($"{id}: {err}"); continue; }
            ok++;
            await Audit("limiti.scheda.validata", "LimiteNormativo", id,
                new { s.CodiceScheda, s.Versione, Limite = RegoleLimiti.Descrivi(s), Nota = r.Testo, Insieme = true }, ct);
        }
        return Json(new { messaggio = $"{ok} schede validate." + (errori.Count > 0 ? $" Non validate: {errori.Count} ({string.Join("; ", errori.Take(3))})." : "") });
    }

    [HttpPost("/api/limiti/schede/respingi")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> RespingiInsieme([FromBody] RespingiPiu r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Testo)) return BadRequest(new { messaggio = "Scrivi il motivo." });
        if (r.Ids is not { Count: > 0 }) return BadRequest(new { messaggio = "Nessuna scheda selezionata." });
        int ok = 0; var errori = new List<string>();
        foreach (var id in r.Ids.Distinct().Take(500))
        {
            var (s, err) = await _limiti.RespingiAsync(id, r.Testo, Operatore, ct);
            if (s is null) { errori.Add($"{id}: {err}"); continue; }
            ok++;
            await Audit("limiti.scheda.respinta", "LimiteNormativo", id, new { s.CodiceScheda, s.Versione, Motivo = r.Testo, Insieme = true }, ct);
        }
        return Json(new { messaggio = $"{ok} schede respinte." + (errori.Count > 0 ? $" Non respinte: {errori.Count} ({string.Join("; ", errori.Take(3))})." : "") });
    }

    [HttpGet("/api/limiti/schede/{id:int}")]
    public async Task<IActionResult> Scheda(int id, CancellationToken ct)
    {
        var s = await _limiti.SchedaAsync(id, ct);
        if (s is null) return NotFound(new { messaggio = "Scheda non trovata." });
        var versioni = await _ctx.LimitiNormativi.AsNoTracking().Where(x => x.CodiceScheda == s.CodiceScheda)
            .OrderByDescending(x => x.Versione).ToListAsync(ct);
        var motivo = _utente.HaPermesso(Permessi.LimitiValida) ? RegoleLimiti.MotivoNonValidabile(s, _utente.IdUtente ?? 0) : "Serve il permesso di validazione.";
        var v = s.IdVersioneFonte is int idv
            ? await _ctx.VersioniFonti.AsNoTracking().Where(x => x.IdVersioneFonte == idv).Select(x => new
            {
                x.IdVersioneFonte, Titolo = x.Fonte!.Titolo, Url = x.Fonte.Url, x.DataAggiornamentoDichiarata, x.DataConsultazione, x.Origine, x.NomeFile
            }).FirstOrDefaultAsync(ct)
            : null;
        return Json(new
        {
            riga = Riga(s),
            dati = ServizioLimitiNormativi.DaScheda(s),
            s.Condizioni, s.Citazione, s.TestoSegnalazione, s.Note, s.NoteValidazione, s.ModificataDa, s.DataModifica,
            fonte = v is null ? null : new
            {
                idVersione = v.IdVersioneFonte, titolo = v.Titolo, url = v.Url, v.DataAggiornamentoDichiarata,
                v.DataConsultazione, v.Origine, v.NomeFile
            },
            versioni = versioni.Select(x => new { id = x.IdLimiteNormativo, x.Versione, limite = RegoleLimiti.Descrivi(x), statoNome = RegoleLimiti.NomeStato(x.Stato), x.PropostaDa, x.DataProposta, x.ValidataDa, x.DataValidazione, x.NoteValidazione }),
            puoValidare = motivo is null,
            motivoNonValidabile = motivo,
            puoCorreggere = _utente.HaPermesso(Permessi.LimitiValida) && s.Stato is StatoSchedaLimite.Proposta or StatoSchedaLimite.DaRiverificare or StatoSchedaLimite.Validata
        });
    }

    [HttpPost("/api/limiti/schede")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> Crea([FromBody] DatiScheda d, CancellationToken ct)
    {
        var errore = ServizioLimitiNormativi.Controlla(d);
        if (errore is not null) return BadRequest(new { messaggio = errore });
        var s = await _limiti.CreaAsync(d, Operatore, ct);
        await Audit("limiti.scheda.creata", "LimiteNormativo", s.IdLimiteNormativo, new { s.CodiceScheda, s.Parametro }, ct);
        return Json(new { id = s.IdLimiteNormativo, messaggio = $"Scheda {s.CodiceScheda} creata come «Proposta»: deve validarla un altro validatore." });
    }

    [HttpPost("/api/limiti/schede/{id:int}/correggi")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> Correggi(int id, [FromBody] DatiScheda d, CancellationToken ct)
    {
        var errore = ServizioLimitiNormativi.Controlla(d);
        if (errore is not null) return BadRequest(new { messaggio = errore });
        var (s, err) = await _limiti.CorreggiAsync(id, d, Operatore, ct);
        if (s is null) return BadRequest(new { messaggio = err });
        await Audit("limiti.scheda.corretta", "LimiteNormativo", s.IdLimiteNormativo, new { s.CodiceScheda, s.Versione, Da = id }, ct);
        return Json(new
        {
            id = s.IdLimiteNormativo,
            messaggio = s.IdLimiteNormativo == id ? "Correzione salvata: la scheda dovrà validarla un altro validatore."
                : $"Creata la versione {s.Versione} di {s.CodiceScheda} come «Proposta»: la versione validata resta in vigore finché la nuova non è validata."
        });
    }

    public record Nota(string? Testo);

    [HttpPost("/api/limiti/schede/{id:int}/valida")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> Valida(int id, [FromBody] Nota n, CancellationToken ct)
    {
        var (s, err) = await _limiti.ValidaAsync(id, n.Testo, Operatore, ct);
        if (s is null) return BadRequest(new { messaggio = err });
        await Audit("limiti.scheda.validata", "LimiteNormativo", id, new { s.CodiceScheda, s.Versione, Limite = RegoleLimiti.Descrivi(s) }, ct);
        return Json(new { messaggio = $"{s.CodiceScheda} v{s.Versione} validata." });
    }

    [HttpPost("/api/limiti/schede/{id:int}/respingi")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> Respingi(int id, [FromBody] Nota n, CancellationToken ct)
    {
        var (s, err) = await _limiti.RespingiAsync(id, n.Testo, Operatore, ct);
        if (s is null) return BadRequest(new { messaggio = err });
        await Audit("limiti.scheda.respinta", "LimiteNormativo", id, new { s.CodiceScheda, s.Versione, Motivo = n.Testo }, ct);
        return Json(new { messaggio = $"{s.CodiceScheda} v{s.Versione} respinta." });
    }

    [HttpPost("/api/limiti/schede/{id:int}/ritira")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> Ritira(int id, [FromBody] Nota n, CancellationToken ct)
    {
        var (s, err) = await _limiti.RitiraAsync(id, n.Testo, Operatore, ct);
        if (s is null) return BadRequest(new { messaggio = err });
        await Audit("limiti.scheda.ritirata", "LimiteNormativo", id, new { s.CodiceScheda, s.Versione, Motivo = n.Testo }, ct);
        return Json(new { messaggio = $"{s.CodiceScheda} v{s.Versione} ritirata: non è più usata nei controlli (resta nello storico)." });
    }

    // ── Parametri ArcLab per gli abbinamenti ─────────────────────

    [HttpGet("/api/limiti/parametri-arclab")]
    public async Task<IActionResult> ParametriArcLab([FromQuery] int famiglia, CancellationToken ct)
    {
        var tutti = await _cache.GetOrCreateAsync("limiti.parametri-arclab", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await _arclab.ParametriUsatiAsync(3, ct);
        }) ?? [];
        var f = (FamigliaNormativa)famiglia;
        var elenco = tutti.Where(p => RegoleLimiti.FamigliaDaMatrice(p.Matrice) == f)
            .GroupBy(p => p.IdParametro)
            .Select(g => new
            {
                id = g.Key, nome = g.First().NomeParametro.Trim(),
                unita = string.Join(", ", g.Select(x => x.UnitaMisura?.Trim()).Where(u => !string.IsNullOrEmpty(u)).Distinct().Take(4)),
                prove = g.Sum(x => x.Prove)
            })
            .OrderByDescending(x => x.prove);
        return Json(elenco);
    }

    // ── Fonti ────────────────────────────────────────────────────

    [HttpGet("/api/limiti/fonti")]
    public async Task<IActionResult> Fonti(CancellationToken ct)
    {
        var fonti = await _ctx.FontiNormative.AsNoTracking().OrderBy(f => f.Famiglia).ThenBy(f => f.Titolo).ToListAsync(ct);
        var versioni = await _ctx.VersioniFonti.AsNoTracking()
            .OrderByDescending(v => v.IdVersioneFonte)
            .Select(v => new
            {
                v.IdVersioneFonte, v.IdFonteNormativa, v.DataAggiornamentoDichiarata, v.DataConsultazione, v.Origine, v.NomeFile,
                Caratteri = v.TestoEstratto.Length, Breve = v.TestoEstratto.Length <= 5000 ? v.TestoEstratto : null, StatoEstrazione = (int)v.StatoEstrazione, v.EsitoEstrazione, v.DataEstrazione,
                v.SchedeProposte, v.SchedeScartate, v.CaricataDa
            }).ToListAsync(ct);
        return Json(new
        {
            caratteriMassimi = TestoFonti.CaratteriMassimi,
            fonti = fonti.Select(f => new
            {
                id = f.IdFonteNormativa, famiglia = (int)f.Famiglia, famigliaNome = RegoleLimiti.NomeFamiglia(f.Famiglia), f.Titolo, f.Ente,
                tipoFonte = (int)f.TipoFonte, f.Url, f.Note, f.Attiva, f.DataUltimaVerifica, f.EsitoUltimaVerifica,
                canale = TestoFonti.CanaleAutomatico(f.Url).Canale,
                versioni = versioni.Where(v => v.IdFonteNormativa == f.IdFonteNormativa).Take(5).Select(v => new
                {
                    v.IdVersioneFonte, v.DataAggiornamentoDichiarata, v.DataConsultazione, v.Origine, v.NomeFile, v.Caratteri,
                    v.StatoEstrazione, v.EsitoEstrazione, v.DataEstrazione, v.SchedeProposte, v.SchedeScartate, v.CaricataDa,
                    // 17/10/2026: versioni salvate prima del controllo anti-bot che contengono solo la pagina di verifica
                    nonValida = v.Breve is not null && TestoFonti.PaginaDiBlocco(v.Breve)
                })
            })
        });
    }

    public record DatiFonte(int? Id, FamigliaNormativa Famiglia, string? Titolo, string? Ente, TipoFonteNormativa TipoFonte, string? Url, string? Note, bool Attiva = true);

    [HttpPost("/api/limiti/fonti")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> SalvaFonte([FromBody] DatiFonte d, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(d.Titolo)) return BadRequest(new { messaggio = "Indicare il titolo della fonte." });
        if (!string.IsNullOrWhiteSpace(d.Url) && !(Uri.TryCreate(d.Url.Trim(), UriKind.Absolute, out var u) && u.Scheme is "http" or "https"))
            return BadRequest(new { messaggio = "Indirizzo non valido (deve iniziare con https://)." });
        FonteNormativa f;
        if (d.Id is int id)
        {
            f = await _ctx.FontiNormative.FirstOrDefaultAsync(x => x.IdFonteNormativa == id, ct) ?? throw new KeyNotFoundException();
            f.DataUltimaModifica = DateTime.UtcNow; f.UltimaModificaDa = Operatore.Nome;
        }
        else
        {
            f = new FonteNormativa { DataCreazione = DateTime.UtcNow, CreataDa = Operatore.Nome };
            _ctx.FontiNormative.Add(f);
        }
        f.Famiglia = d.Famiglia; f.Titolo = d.Titolo.Trim(); f.Ente = d.Ente?.Trim(); f.TipoFonte = d.TipoFonte;
        f.Url = string.IsNullOrWhiteSpace(d.Url) ? null : d.Url.Trim(); f.Note = d.Note?.Trim(); f.Attiva = d.Attiva;
        await _ctx.SaveChangesAsync(ct);
        await Audit(d.Id is null ? "limiti.fonte.creata" : "limiti.fonte.modificata", "FonteNormativa", f.IdFonteNormativa, new { f.Titolo, f.Url, f.Attiva }, ct);
        return Json(new { id = f.IdFonteNormativa, messaggio = "Fonte salvata." });
    }

    [HttpPost("/api/limiti/fonti/fase1")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> AggiungiFase1(CancellationToken ct)
    {
        var n = await _limiti.AggiungiFontiFase1Async(Operatore, ct);
        await Audit("limiti.fonti.fase1", "FonteNormativa", 0, new { Aggiunte = n }, ct);
        return Json(new { messaggio = n == 0 ? "Le fonti della fase 1 c'erano già." : $"Aggiunte {n} fonti: verificane gli indirizzi, poi «Scarica»." });
    }

    [HttpPost("/api/limiti/fonti/{id:int}/scarica")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> Scarica(int id, CancellationToken ct)
    {
        var e = await _limiti.ScaricaAsync(id, Operatore, ct);
        await Audit("limiti.fonte.scaricata", "FonteNormativa", id, new { e.Messaggio, e.IdVersioneFonte, e.Nuova }, ct,
            e.Riuscita ? EsitoOperazione.Successo : EsitoOperazione.Errore);
        return e.Riuscita ? Json(new { messaggio = e.Messaggio, idVersione = e.IdVersioneFonte, nuova = e.Nuova }) : BadRequest(new { messaggio = e.Messaggio });
    }

    [HttpPost("/api/limiti/fonti/{id:int}/carica")]
    [RichiedePermesso(Permessi.LimitiValida)]
    [RequestSizeLimit(40_000_000)]
    public async Task<IActionResult> Carica(int id, IFormFile? file, [FromForm] DateTime? data, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Scegli il file (PDF, pagina HTML salvata o testo)." });
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var dataUtc = data is DateTime dd ? DateTime.SpecifyKind(dd.Date, DateTimeKind.Utc) : (DateTime?)null;
        var e = await _limiti.CaricaAsync(id, ms.ToArray(), file.FileName, file.ContentType, dataUtc, Operatore, ct);
        await Audit("limiti.fonte.caricata", "FonteNormativa", id, new { file.FileName, e.Messaggio, e.IdVersioneFonte }, ct,
            e.Riuscita ? EsitoOperazione.Successo : EsitoOperazione.Errore);
        return e.Riuscita ? Json(new { messaggio = e.Messaggio, idVersione = e.IdVersioneFonte, nuova = e.Nuova }) : BadRequest(new { messaggio = e.Messaggio });
    }

    /// <summary>Famiglie da estrarre (18/10/2026): vuoto o null = tutte.</summary>
    public record SceltaFamiglie(List<FamigliaNormativa>? Famiglie);

    [HttpPost("/api/limiti/versioni/{id:int}/estrai")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> Estrai(int id, [FromBody] SceltaFamiglie? scelta, CancellationToken ct)
    {
        var famiglie = scelta?.Famiglie?.Where(f => Enum.IsDefined(f)).Distinct().ToList();
        var err = await _limiti.AccodaEstrazioneAsync(id, ct, famiglie);
        if (err is not null) return BadRequest(new { messaggio = err });
        await Audit("limiti.estrazione.accodata", "VersioneFonte", id, new { Famiglie = famiglie?.Select(f => f.ToString()) }, ct);
        return Json(new { messaggio = "Estrazione con l'AI avviata: può richiedere alcuni minuti, la pagina si aggiorna da sola." });
    }

    [HttpGet("/api/limiti/versioni/{id:int}/file")]
    public async Task<IActionResult> FileVersione(int id, CancellationToken ct)
    {
        var v = await _ctx.VersioniFonti.AsNoTracking().FirstOrDefaultAsync(x => x.IdVersioneFonte == id, ct);
        if (v?.ContenutoOriginale is null) return NotFound();
        var tipo = v.TipoContenuto ?? "application/octet-stream";
        // l'HTML salvato si scarica, non si apre dentro LaboratorioAI
        var nome = v.NomeFile ?? $"fonte-{v.IdFonteNormativa}-v{v.IdVersioneFonte}" + (tipo.Contains("pdf") ? ".pdf" : tipo.Contains("html") ? ".html" : ".txt");
        return tipo.Contains("pdf") ? File(v.ContenutoOriginale, tipo) : File(v.ContenutoOriginale, "application/octet-stream", nome);
    }

    [HttpGet("/api/limiti/versioni/{id:int}/testo")]
    public async Task<IActionResult> TestoVersione(int id, CancellationToken ct)
    {
        var t = await _ctx.VersioniFonti.AsNoTracking().Where(x => x.IdVersioneFonte == id).Select(x => x.TestoEstratto).FirstOrDefaultAsync(ct);
        return t is null ? NotFound() : Content(t, "text/plain; charset=utf-8");
    }

    // ── Verifica delle schede validate (18/10/2026) ──────────────

    private static string NomeGravita(GravitaRilievo g) => g switch
    {
        GravitaRilievo.Errato => "Errato", GravitaRilievo.Dubbio => "Dubbio", GravitaRilievo.Avviso => "Avviso", _ => "Conforme"
    };

    private static string NomeRegola(string r) => r switch
    {
        "citazione-mancante" => "Citazione mancante",
        "valore-citazione" => "Valore non nella citazione",
        "senza-arclab" => "Senza parametri ArcLab",
        "nome-arclab" => "Parametro ArcLab diverso",
        "unita" => "Unità diverse da ArcLab",
        "tipo-famiglia" => "Tipo di limite",
        "contraddizione" => "Limiti in contraddizione",
        "revisione-ai" => "Revisione AI",
        _ => r
    };

    [HttpGet("/api/limiti/verifica")]
    public async Task<IActionResult> Verifica([FromQuery] int? giro, [FromQuery] bool chiusi = false, CancellationToken ct = default)
    {
        var giri = await _ctx.GiriVerificaLimiti.AsNoTracking().OrderByDescending(g => g.IdGiroVerificaLimiti).Take(30).ToListAsync(ct);
        var titoli = await _ctx.FontiNormative.AsNoTracking().ToDictionaryAsync(f => f.IdFonteNormativa, f => TitoloBreve(f.Titolo), ct);
        var q = _ctx.RilieviVerificaLimiti.AsNoTracking().AsQueryable();
        if (giro is int idg) q = q.Where(r => r.IdGiroVerificaLimiti == idg);
        else if (!chiusi) q = q.Where(r => !r.Chiuso);
        var rilievi = await q.OrderByDescending(r => r.Gravita).ThenBy(r => r.IdLimiteNormativo).Take(2000).ToListAsync(ct);
        var idsSchede = rilievi.Select(r => r.IdLimiteNormativo).Distinct().ToList();
        var schede = await _ctx.LimitiNormativi.AsNoTracking().Where(x => idsSchede.Contains(x.IdLimiteNormativo)).ToDictionaryAsync(x => x.IdLimiteNormativo, ct);
        var tipiGiro = giri.ToDictionary(g => g.IdGiroVerificaLimiti, g => g.Tipo);
        // fonti con schede validate: per lanciare la revisione
        var perFonte = await _verifica.SchedeInVigore().Where(x => x.IdVersioneFonte != null)
            .GroupBy(x => x.VersioneFonte!.IdFonteNormativa).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync(ct);
        return Json(new
        {
            fonti = perFonte.Select(f => new
            {
                id = f.Id, titolo = titoli.GetValueOrDefault(f.Id, "?"), schede = f.N,
                ultima = giri.Where(g => g.IdFonteNormativa == f.Id).Select(g => new { g.DataRichiesta, stato = (int)g.Stato, g.Esito }).FirstOrDefault()
            }).OrderBy(f => f.titolo),
            giri = giri.Select(g => new
            {
                id = g.IdGiroVerificaLimiti, tipo = g.Tipo switch
                {
                    TipoVerificaLimiti.Automatica => "Controlli automatici", TipoVerificaLimiti.RevisioneAi => "Revisione AI", _ => "Proposta abbinamenti matrici"
                },
                fonte = g.IdFonteNormativa is int f ? titoli.GetValueOrDefault(f, "?") : "tutte le schede validate",
                g.DataRichiesta, g.DataFine, g.RichiestoDa, stato = (int)g.Stato, g.Esito, g.Modello
            }),
            rilievi = rilievi.Where(r => schede.ContainsKey(r.IdLimiteNormativo)).Select(r =>
            {
                var x = schede[r.IdLimiteNormativo];
                return new
                {
                    id = r.IdRilievoVerificaLimite, giro = r.IdGiroVerificaLimiti, gravita = (int)r.Gravita, gravitaNome = NomeGravita(r.Gravita),
                    regola = NomeRegola(r.Regola), r.Motivo, r.Citazione, r.CitazioneTrovata, r.Chiuso, r.ChiusoDa, r.DataChiusura, r.NotaChiusura,
                    idScheda = x.IdLimiteNormativo, codice = x.CodiceScheda, versione = x.Versione, parametro = x.Parametro,
                    matrice = x.MatriceCategoria, limite = RegoleLimiti.Descrivi(x), statoScheda = RegoleLimiti.NomeStato(x.Stato),
                    automatico = tipiGiro.GetValueOrDefault(r.IdGiroVerificaLimiti) == TipoVerificaLimiti.Automatica
                };
            })
        });
    }

    [HttpPost("/api/limiti/verifica/automatica")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> ControlliAutomatici(CancellationToken ct)
    {
        var g = await _verifica.ControlliAutomaticiAsync(Operatore, ct);
        await Audit("limiti.verifica.automatica", "GiroVerificaLimiti", g.IdGiroVerificaLimiti, new { g.Esito }, ct);
        return Json(new { messaggio = g.Esito, giro = g.IdGiroVerificaLimiti });
    }

    [HttpPost("/api/limiti/verifica/revisione/{idFonte:int}")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> Revisione(int idFonte, CancellationToken ct)
    {
        var err = await _verifica.AccodaRevisioneAsync(idFonte, Operatore, ct);
        if (err is not null) return BadRequest(new { messaggio = err });
        await Audit("limiti.verifica.revisione-accodata", "FonteNormativa", idFonte, null, ct);
        return Json(new { messaggio = "Revisione con l'AI avviata: richiede alcuni minuti, la pagina si aggiorna da sola." });
    }

    [HttpPost("/api/limiti/verifica/chiudi")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> ChiudiRilievi([FromBody] RespingiPiu r, CancellationToken ct)
    {
        if (r.Ids is not { Count: > 0 }) return BadRequest(new { messaggio = "Nessun rilievo selezionato." });
        var err = await _verifica.ChiudiAsync(r.Ids, r.Testo, Operatore, ct);
        if (err is not null) return BadRequest(new { messaggio = err });
        await Audit("limiti.verifica.rilievi-chiusi", "RilievoVerificaLimite", r.Ids.FirstOrDefault(), new { r.Ids, Nota = r.Testo }, ct);
        return Json(new { messaggio = $"{r.Ids.Count} rilievi chiusi." });
    }

    /// <summary>Excel di un giro di verifica: registrazione da conservare (tutti i rilievi, anche i conformi).</summary>
    [HttpGet("/api/limiti/verifica/{idGiro:int}/excel")]
    public async Task<IActionResult> VerificaExcel(int idGiro, CancellationToken ct)
    {
        var g = await _ctx.GiriVerificaLimiti.AsNoTracking().FirstOrDefaultAsync(x => x.IdGiroVerificaLimiti == idGiro, ct);
        if (g is null) return NotFound();
        var rilievi = await _ctx.RilieviVerificaLimiti.AsNoTracking().Where(r => r.IdGiroVerificaLimiti == idGiro)
            .OrderByDescending(r => r.Gravita).ThenBy(r => r.IdLimiteNormativo).ToListAsync(ct);
        var ids = rilievi.Select(r => r.IdLimiteNormativo).Distinct().ToList();
        var schede = await _ctx.LimitiNormativi.AsNoTracking().Where(x => ids.Contains(x.IdLimiteNormativo)).ToDictionaryAsync(x => x.IdLimiteNormativo, ct);
        var fonte = g.IdFonteNormativa is int f ? (await _ctx.FontiNormative.AsNoTracking().FirstAsync(x => x.IdFonteNormativa == f, ct)).Titolo : "tutte le schede validate";

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Verifica");
        ws.Cell(1, 1).Value = $"Verifica schede limite n. {g.IdGiroVerificaLimiti} – {(g.Tipo == TipoVerificaLimiti.Automatica ? "controlli automatici" : "revisione con l'AI")}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"Fonte: {fonte} · richiesta da {g.RichiestoDa} il {g.DataRichiesta.ToLocalTime():dd/MM/yyyy HH:mm}" + (g.Modello is null ? "" : $" · modello {g.Modello}");
        ws.Cell(3, 1).Value = g.Esito ?? "";
        var intest = new[] { "Esito", "Controllo", "Scheda", "Ver.", "Parametro", "Categoria", "Limite", "Stato scheda", "Motivo", "Citazione del revisore", "Citazione trovata", "Chiuso da", "Il", "Nota" };
        for (var c = 0; c < intest.Length; c++) { ws.Cell(5, c + 1).Value = intest[c]; ws.Cell(5, c + 1).Style.Font.Bold = true; }
        var riga = 6;
        foreach (var r in rilievi)
        {
            if (!schede.TryGetValue(r.IdLimiteNormativo, out var x)) continue;
            object?[] v = [NomeGravita(r.Gravita), NomeRegola(r.Regola), x.CodiceScheda, x.Versione, x.Parametro, x.MatriceCategoria, RegoleLimiti.Descrivi(x),
                RegoleLimiti.NomeStato(x.Stato), r.Motivo, r.Citazione, r.CitazioneTrovata is null ? "" : r.CitazioneTrovata.Value ? "sì" : "no",
                r.ChiusoDa, r.DataChiusura?.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), r.NotaChiusura];
            for (var c = 0; c < v.Length; c++) ws.Cell(riga, c + 1).Value = v[c]?.ToString() ?? "";
            riga++;
        }
        ws.Columns().AdjustToContents(5, riga, 10, 70);
        ws.SheetView.FreezeRows(5);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        await Audit("limiti.verifica.excel", "GiroVerificaLimiti", idGiro, null, ct);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"VerificaLimiti_{idGiro}_{g.DataRichiesta:yyyyMMdd}.xlsx");
    }

    // ── Fase 1b: categorie normative e abbinamento delle matrici ArcLab (18/10/2026) ──

    [HttpGet("/api/limiti/categorie")]
    public async Task<IActionResult> Categorie(CancellationToken ct)
    {
        var categorie = await _categorie.SincronizzaAsync(ct);
        var schede = await _ctx.LimitiNormativi.AsNoTracking()
            .Where(x => x.Stato != StatoSchedaLimite.Respinta && x.Stato != StatoSchedaLimite.Superata && x.MatriceCategoria != null)
            .Select(x => new { x.Famiglia, x.MatriceCategoria, x.Parametro, x.Stato }).ToListAsync(ct);
        List<MatriceArcLab> matrici; string? avviso = null;
        try { matrici = await _categorie.MatriciDaAbbinareAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { matrici = []; avviso = "Matrici ArcLab non lette: " + ex.Message; }
        var abb = await _ctx.MatriciCategorie.AsNoTracking().Where(a => a.Stato != StatoAbbinamentoMatrice.Respinto).ToListAsync(ct);
        var nomi = categorie.ToDictionary(c => c.IdCategoriaNormativa, c => c.Nome);
        var giro = await _ctx.GiriVerificaLimiti.AsNoTracking().Where(g => g.Tipo == TipoVerificaLimiti.AbbinamentoMatrici)
            .OrderByDescending(g => g.IdGiroVerificaLimiti).Select(g => new { g.DataRichiesta, stato = (int)g.Stato, g.Esito }).FirstOrDefaultAsync(ct);
        return Json(new
        {
            avviso, giro,
            categorie = categorie.Select(c => new
            {
                id = c.IdCategoriaNormativa, famigliaNome = RegoleLimiti.NomeFamiglia(c.Famiglia), nome = c.Nome,
                parametri = schede.Where(x => x.Famiglia == c.Famiglia && RegoleLimiti.ChiaveCategoria(x.MatriceCategoria) == c.Chiave)
                    .Select(x => x.Parametro).Distinct().Take(10),
                validate = schede.Count(x => x.Famiglia == c.Famiglia && RegoleLimiti.ChiaveCategoria(x.MatriceCategoria) == c.Chiave
                    && (x.Stato == StatoSchedaLimite.Validata || x.Stato == StatoSchedaLimite.DaRiverificare)),
                matrici = abb.Count(a => a.IdCategoriaNormativa == c.IdCategoriaNormativa && a.Stato == StatoAbbinamentoMatrice.Confermato)
            }),
            matrici = matrici.Select(m => new
            {
                id = m.IdMatrice, nome = m.Nome, prove = m.Prove,
                abbinamenti = abb.Where(a => a.IdMatriceArcLab == m.IdMatrice).Select(a => new
                {
                    id = a.IdAbbinamentoMatriceCategoria, idCategoria = a.IdCategoriaNormativa,
                    categoria = a.IdCategoriaNormativa is int ic ? nomi.GetValueOrDefault(ic, "(categoria non più attiva)") : "nessuna categoria",
                    confermato = a.Stato == StatoAbbinamentoMatrice.Confermato, a.PropostoDa, a.Nota, a.ConfermatoDa
                })
            })
        });
    }

    [HttpPost("/api/limiti/categorie/proponi")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> ProponiCategorie(CancellationToken ct)
    {
        var err = await _categorie.AccodaPropostaAsync(Operatore, ct);
        if (err is not null) return BadRequest(new { messaggio = err });
        await Audit("limiti.categorie.proposta-accodata", "AbbinamentoMatriceCategoria", 0, null, ct);
        return Json(new { messaggio = "Proposta con l'AI avviata per le matrici senza abbinamento: richiede alcuni minuti, la pagina si aggiorna da sola." });
    }

    public record DatiAbbinaMatrice(int IdMatrice, string NomeMatrice, int? IdCategoria, string? Nota);

    [HttpPost("/api/limiti/categorie/abbina")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> AbbinaMatrice([FromBody] DatiAbbinaMatrice d, CancellationToken ct)
    {
        await _categorie.AggiungiAsync(d.IdMatrice, d.NomeMatrice, d.IdCategoria, true, Operatore, d.Nota, ct);
        await Audit("limiti.categorie.abbinata", "AbbinamentoMatriceCategoria", d.IdMatrice, new { d.NomeMatrice, d.IdCategoria }, ct);
        return Json(new { messaggio = "Abbinamento confermato." });
    }

    [HttpPost("/api/limiti/categorie/conferma")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> ConfermaCategorie([FromBody] RespingiPiu r, CancellationToken ct)
    {
        var n = await _categorie.ConfermaAsync(r.Ids ?? [], Operatore, ct);
        await Audit("limiti.categorie.confermate", "AbbinamentoMatriceCategoria", 0, new { r.Ids }, ct);
        return Json(new { messaggio = $"{n} abbinamenti confermati." });
    }

    [HttpPost("/api/limiti/categorie/respingi")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> RespingiCategorie([FromBody] RespingiPiu r, CancellationToken ct)
    {
        var n = await _categorie.RespingiAsync(r.Ids ?? [], Operatore, ct);
        await Audit("limiti.categorie.respinte", "AbbinamentoMatriceCategoria", 0, new { r.Ids }, ct);
        return Json(new { messaggio = $"{n} abbinamenti tolti." });
    }

    // ── Fase 1b: tempi massimi ricezione → inizio prova (18/10/2026) ──

    [HttpGet("/api/limiti/tempi")]
    public async Task<IActionResult> Tempi([FromServices] ServizioSegnalazioniLimiti segnalazioni, CancellationToken ct) =>
        Json((await segnalazioni.TempiAsync(ct)).Select(t => new
        {
            id = t.IdTempoMassimoProva, famiglia = (int)t.Famiglia, famigliaNome = RegoleLimiti.NomeFamiglia(t.Famiglia),
            idParametro = t.IdParametroArcLab, t.NomeParametro, t.Ore, t.Attivo, t.Nota, t.ModificatoDa, t.DataModifica
        }));

    public record DatiTempo(int? Id, int Famiglia, int? IdParametro, string? NomeParametro, int Ore, bool Attivo, string? Nota);

    [HttpPost("/api/limiti/tempi")]
    [RichiedePermesso(Permessi.LimitiValida)]
    public async Task<IActionResult> SalvaTempo([FromServices] ServizioSegnalazioniLimiti segnalazioni, [FromBody] DatiTempo d, CancellationToken ct)
    {
        var err = await segnalazioni.SalvaTempoAsync(d.Id, (FamigliaNormativa)d.Famiglia, d.IdParametro, d.NomeParametro, d.Ore, d.Attivo, d.Nota, Operatore, ct);
        if (err is not null) return BadRequest(new { messaggio = err });
        await Audit(d.Id is null ? "limiti.tempi.creato" : "limiti.tempi.modificato", "TempoMassimoProva", d.Id ?? 0, d, ct);
        return Json(new { messaggio = "Tempo massimo salvato. I colori di «Da Validare» si aggiornano entro 2 minuti (o subito con Aggiorna)." });
    }
}
