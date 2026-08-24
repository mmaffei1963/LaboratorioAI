using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.LaboratorioAI.Import;
using LaboratorioAI.Documenti;
using LaboratorioAI.IA;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Archivio delle materie prime nutrizionali. Catalogo unico con più set di
/// valori per fonte/priorità; scrittura solo sul database proprio, mai su ArcLab.
/// </summary>
[RichiedePermesso(Permessi.NutrizionaleMateriePrime)]
public class MateriePrimeController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IEstrattoreSchedaNutrizionale _estrattore;
    private readonly IImportatoreUsda _usda;
    private readonly IImportatoreCnf _cnf;
    private readonly IServizioAudit _audit;

    public MateriePrimeController(
        ContestoLaboratorioAI ctx, IEstrattoreSchedaNutrizionale estrattore,
        IImportatoreUsda usda, IImportatoreCnf cnf, IServizioAudit audit)
    {
        _ctx = ctx;
        _estrattore = estrattore;
        _usda = usda;
        _cnf = cnf;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Materie prime";
        return View();
    }

    // ── Elenco con ricerca ─────────────────────────────────────────
    [HttpGet("api/materie-prime/elenco")]
    public async Task<IActionResult> Elenco(string? q, int? tipo, string? fonte, CancellationToken ct)
    {
        var query = _ctx.MateriePrime.AsNoTracking().Where(m => m.Attivo);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(m =>
                EF.Functions.Like(m.Denominazione, $"%{t}%") ||
                (m.Marca != null && EF.Functions.Like(m.Marca, $"%{t}%")) ||
                (m.Fornitore != null && EF.Functions.Like(m.Fornitore, $"%{t}%")));
        }
        if (tipo is int tp) query = query.Where(m => (int)m.Tipo == tp);

        // Provenienza (in base al codice interno, così resta rapido).
        query = fonte switch
        {
            "scheda" => query.Where(m => m.Tipo == TipoMateriaPrima.Specifica),
            "crea" => query.Where(m => m.Tipo == TipoMateriaPrima.Generica && m.CodiceInterno != null
                        && !m.CodiceInterno.StartsWith("USDA-") && !m.CodiceInterno.StartsWith("CNF-")),
            "usda" => query.Where(m => m.CodiceInterno != null && m.CodiceInterno.StartsWith("USDA-")),
            "cnf" => query.Where(m => m.CodiceInterno != null && m.CodiceInterno.StartsWith("CNF-")),
            _ => query
        };

        // 1) Pagina di materie prime (indicizzata su Denominazione).
        var pagina = await query
            .OrderBy(m => m.Denominazione)
            .Take(500)
            .Select(m => new
            {
                m.IdMateriaPrima,
                m.Denominazione,
                Tipo = (int)m.Tipo,
                TipoTesto = m.Tipo == TipoMateriaPrima.Specifica ? "Specifica" : "Generica",
                m.Marca,
                m.Fornitore,
                m.Categoria
            })
            .ToListAsync(ct);

        // 2) Set di valori corrente solo per la pagina (niente window su tutta la tabella).
        var ids = pagina.Select(x => x.IdMateriaPrima).ToList();
        var correnti = await _ctx.ValoriNutrizionali.AsNoTracking()
            .Where(v => v.Corrente && ids.Contains(v.IdMateriaPrima))
            .Select(v => new
            {
                v.IdMateriaPrima, v.Grassi, v.GrassiSaturi, v.Carboidrati, v.Zuccheri,
                v.Proteine, v.Sale, v.Fibre, v.Acqua,
                Fonte = v.Fonte != null ? v.Fonte.Descrizione : null
            })
            .ToListAsync(ct);
        var mappa = correnti
            .GroupBy(v => v.IdMateriaPrima)
            .ToDictionary(g => g.Key, g => g.First());

        var righe = pagina.Select(m =>
        {
            mappa.TryGetValue(m.IdMateriaPrima, out var c);
            return new
            {
                m.IdMateriaPrima, m.Denominazione, m.Tipo, m.TipoTesto, m.Marca, m.Fornitore, m.Categoria,
                Corrente = c is null ? null : new
                {
                    c.Grassi, c.GrassiSaturi, c.Carboidrati, c.Zuccheri,
                    c.Proteine, c.Sale, c.Fibre, c.Acqua, c.Fonte
                }
            };
        });

        return Json(righe);
    }

    // ── Dettaglio (materia prima + set corrente) ───────────────────
    [HttpGet("api/materie-prime/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var m = await _ctx.MateriePrime.AsNoTracking()
            .Include(x => x.Valori).ThenInclude(v => v.Fonte)
            .FirstOrDefaultAsync(x => x.IdMateriaPrima == id, ct);
        if (m is null) return NotFound(new { messaggio = "Materia prima non trovata." });

        var v = m.Valori.FirstOrDefault(x => x.Corrente) ?? m.Valori.FirstOrDefault();

        return Json(new
        {
            m.IdMateriaPrima,
            m.Denominazione,
            Tipo = (int)m.Tipo,
            m.Marca,
            m.Fornitore,
            m.Categoria,
            m.CodiceInterno,
            m.Note,
            Valori = v is null ? null : new
            {
                v.Grassi, v.GrassiSaturi, v.Carboidrati, v.Zuccheri,
                v.Proteine, v.Sale, v.Fibre, v.Acqua, v.EnergiaKj, v.EnergiaKcal,
                v.Sodio, v.GrassiTrans, v.GrassiMonoinsaturi, v.GrassiPolinsaturi,
                v.Colesterolo, v.VitaminaD, v.Calcio, v.Ferro, v.Potassio,
                v.Priorita, v.RiferimentoDocumento,
                Fonte = v.Fonte is null ? null : new { v.Fonte.IdFonte, Tipo = (int)v.Fonte.Tipo, v.Fonte.Descrizione, v.Fonte.Riferimento, v.Fonte.Url }
            }
        });
    }

    // ── Fonti (per la tendina) ─────────────────────────────────────
    [HttpGet("api/materie-prime/fonti")]
    public async Task<IActionResult> Fonti(CancellationToken ct)
    {
        var fonti = await _ctx.Fonti.AsNoTracking()
            .Where(f => f.Attivo)
            .OrderBy(f => f.Descrizione)
            .Select(f => new { f.IdFonte, Tipo = (int)f.Tipo, f.Descrizione })
            .ToListAsync(ct);
        return Json(fonti);
    }

    // ── Import CREA (foglio Etichetta_100g) ────────────────────────
    [HttpPost("api/materie-prime/importa-crea")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> ImportaCrea(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { messaggio = "Nessun file caricato." });

        List<RigaCrea> righe;
        try
        {
            using var s = file.OpenReadStream();
            righe = ImportatoreCrea.Leggi(s).ToList();
        }
        catch (Exception ex)
        {
            return BadRequest(new { messaggio = $"File non valido: {ex.Message}" });
        }

        // Fonte CREA: una sola, riutilizzata.
        var fonte = await _ctx.Fonti.FirstOrDefaultAsync(f => f.Tipo == TipoFonte.DatabaseCrea, ct);
        if (fonte is null)
        {
            fonte = new Fonte
            {
                Tipo = TipoFonte.DatabaseCrea,
                Descrizione = "Database CREA — Tabelle di composizione degli alimenti",
                Url = righe.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.FonteUrl))?.FonteUrl,
                PrioritaDefault = 2
            };
            _ctx.Fonti.Add(fonte);
        }

        // Generiche esistenti, per codice CREA (upsert idempotente).
        var esistenti = await _ctx.MateriePrime
            .Include(m => m.Valori)
            .Where(m => m.Tipo == TipoMateriaPrima.Generica && m.CodiceInterno != null)
            .ToDictionaryAsync(m => m.CodiceInterno!, ct);

        var ora = DateTime.UtcNow;
        int nuove = 0, aggiornate = 0;

        foreach (var r in righe)
        {
            if (string.IsNullOrWhiteSpace(r.Codice)) continue;

            if (!esistenti.TryGetValue(r.Codice, out var m))
            {
                m = new MateriaPrima
                {
                    Tipo = TipoMateriaPrima.Generica,
                    CodiceInterno = r.Codice,
                    DataCreazione = ora,
                    DataUltimaModifica = ora,
                    Attivo = true
                };
                _ctx.MateriePrime.Add(m);
                esistenti[r.Codice] = m;
                nuove++;
            }
            else
            {
                m.DataUltimaModifica = ora;
                m.Attivo = true;
                aggiornate++;
            }

            m.Denominazione = r.Alimento.Length > 255 ? r.Alimento[..255] : r.Alimento;
            m.Categoria = r.Categoria;

            var set = m.Valori.FirstOrDefault(v => v.Corrente);
            if (set is null)
            {
                set = new ValoreNutrizionale { Corrente = true, DataCreazione = ora, DataAcquisizione = ora };
                m.Valori.Add(set);
            }
            set.Grassi = r.Grassi ?? 0m;
            set.GrassiSaturi = r.GrassiSaturi ?? 0m;
            set.Carboidrati = r.Carboidrati ?? 0m;
            set.Zuccheri = r.Zuccheri ?? 0m;
            set.Proteine = r.Proteine ?? 0m;
            set.Sale = r.Sale ?? 0m;
            set.Fibre = r.Fibre ?? 0m;
            set.Acqua = r.Acqua ?? 0m;
            set.EnergiaKj = r.EnergiaKj;
            set.EnergiaKcal = r.EnergiaKcal;
            set.Priorita = 2;
            set.RiferimentoDocumento = r.FonteUrl;
            set.Fonte = fonte;
        }

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Nutrizionale, "materieprime.import.crea", EsitoOperazione.Successo,
            dettaglio: new { Lette = righe.Count, Nuove = nuove, Aggiornate = aggiornate }, ct: ct);
        return Json(new EsitoImportCrea { Lette = righe.Count, Nuove = nuove, Aggiornate = aggiornate });
    }

    // ── Import USDA FoodData Central (da cartella CSV sul disco) ────
    [HttpPost("api/materie-prime/importa-usda")]
    public async Task<IActionResult> ImportaUsda([FromBody] RichiestaImportUsda req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Cartella))
            return BadRequest(new { messaggio = "Indica la cartella dei file CSV USDA." });

        try
        {
            var esito = await _usda.ImportaAsync(req.Cartella.Trim(), ct);
            return Json(esito);
        }
        catch (Exception ex)
        {
            return BadRequest(new { messaggio = "Import non riuscito: " + ex.Message });
        }
    }

    // ── Import CNF (Canadian Nutrient File, da cartella CSV) ───────
    [HttpPost("api/materie-prime/importa-cnf")]
    public async Task<IActionResult> ImportaCnf([FromBody] RichiestaImportUsda req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Cartella))
            return BadRequest(new { messaggio = "Indica la cartella dei file CSV CNF." });
        try
        {
            var esito = await _cnf.ImportaAsync(req.Cartella.Trim(), ct);
            return Json(esito);
        }
        catch (Exception ex)
        {
            return BadRequest(new { messaggio = "Import non riuscito: " + ex.Message });
        }
    }

    // ── Estrazione AI da scheda tecnica PDF (proposta, non salva) ──
    [HttpPost("api/materie-prime/estrai-scheda")]
    [RequestSizeLimit(20_971_520)]
    public async Task<IActionResult> EstraiScheda(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { messaggio = "Nessun file caricato." });

        byte[] pdf;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, ct);
            pdf = ms.ToArray();
        }

        try
        {
            var esito = await _estrattore.EstraiAsync(pdf, ct);
            await _audit.RegistraAsync(
                AreaFunzionale.Nutrizionale, "materieprime.estrazione.ai", EsitoOperazione.Successo,
                dettaglio: new { File = file.FileName }, ct: ct);
            return Json(esito);
        }
        catch (Exception ex)
        {
            return BadRequest(new { messaggio = "Estrazione non riuscita: " + ex.Message });
        }
    }

    // ── Eliminazione logica (resta in archivio) ────────────────────
    [HttpPost("api/materie-prime/{id:int}/elimina")]
    public async Task<IActionResult> Elimina(int id, CancellationToken ct)
    {
        var m = await _ctx.MateriePrime.FirstOrDefaultAsync(x => x.IdMateriaPrima == id, ct);
        if (m is null) return NotFound(new { messaggio = "Materia prima non trovata." });

        m.Attivo = false;
        m.DataCancellazione = DateTime.UtcNow;
        m.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Nutrizionale, "materieprime.eliminata", EsitoOperazione.Successo,
            tipoEntita: "MateriaPrima", idEntita: id.ToString(),
            dettaglio: new { m.Denominazione }, ct: ct);
        return Json(new { ok = true });
    }

    // ── Salvataggio (nuovo o modifica) ─────────────────────────────
    [HttpPost("api/materie-prime/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaMateriaPrima req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Denominazione))
            return BadRequest(new { messaggio = "La denominazione è obbligatoria." });
        if (req.Valori is null)
            return BadRequest(new { messaggio = "I valori nutrizionali sono obbligatori." });

        var ora = DateTime.UtcNow;

        MateriaPrima m;
        if (req.Id is int id)
        {
            m = await _ctx.MateriePrime.Include(x => x.Valori)
                    .FirstOrDefaultAsync(x => x.IdMateriaPrima == id, ct)
                ?? throw new InvalidOperationException("Materia prima non trovata.");
            m.DataUltimaModifica = ora;
        }
        else
        {
            m = new MateriaPrima { DataCreazione = ora, DataUltimaModifica = ora };
            _ctx.MateriePrime.Add(m);
        }

        m.Denominazione = req.Denominazione.Trim();
        m.Tipo = (TipoMateriaPrima)req.Tipo;
        m.Marca = Vuoto(req.Marca);
        m.Fornitore = Vuoto(req.Fornitore);
        m.Categoria = Vuoto(req.Categoria);
        m.CodiceInterno = Vuoto(req.CodiceInterno);
        m.Note = Vuoto(req.Note);

        // Fonte: usa quella scelta o ne crea una nuova dalla descrizione.
        int? idFonte = req.Fonte?.IdFonte;
        if (idFonte is null && req.Fonte is not null && !string.IsNullOrWhiteSpace(req.Fonte.Descrizione))
        {
            var nuova = new Fonte
            {
                Tipo = (TipoFonte)req.Fonte.Tipo,
                Descrizione = req.Fonte.Descrizione!.Trim(),
                Riferimento = Vuoto(req.Fonte.Riferimento),
                Url = Vuoto(req.Fonte.Url),
                PrioritaDefault = req.Priorita
            };
            _ctx.Fonti.Add(nuova);
            await _ctx.SaveChangesAsync(ct);
            idFonte = nuova.IdFonte;
        }

        // Set di valori: aggiorna quello corrente, o ne aggiunge uno nuovo.
        var set = m.Valori.FirstOrDefault(v => v.Corrente);
        if (set is null)
        {
            set = new ValoreNutrizionale { Corrente = true, DataCreazione = ora, DataAcquisizione = ora };
            m.Valori.Add(set);
        }

        var d = req.Valori;
        set.Grassi = d.Grassi; set.GrassiSaturi = d.GrassiSaturi;
        set.Carboidrati = d.Carboidrati; set.Zuccheri = d.Zuccheri;
        set.Proteine = d.Proteine; set.Sale = d.Sale;
        set.Fibre = d.Fibre; set.Acqua = d.Acqua;
        set.EnergiaKj = d.EnergiaKj; set.EnergiaKcal = d.EnergiaKcal;
        set.Sodio = d.Sodio; set.GrassiTrans = d.GrassiTrans;
        set.GrassiMonoinsaturi = d.GrassiMonoinsaturi; set.GrassiPolinsaturi = d.GrassiPolinsaturi;
        set.Colesterolo = d.Colesterolo; set.VitaminaD = d.VitaminaD;
        set.Calcio = d.Calcio; set.Ferro = d.Ferro; set.Potassio = d.Potassio;
        set.Priorita = req.Priorita;
        set.RiferimentoDocumento = Vuoto(req.RiferimentoDocumento);
        set.IdFonte = idFonte;

        await _ctx.SaveChangesAsync(ct);

        // Validazione non bloccante: somma componenti ~ 100.
        var avvisi = new List<string>();
        decimal somma = d.Grassi + d.Carboidrati + d.Proteine + d.Sale + d.Fibre + d.Acqua;
        if (somma > 0m && Math.Abs(somma - 100m) > 5m)
            avvisi.Add($"La somma dei componenti è {somma:0.#} g/100 g (attesa ~100): verifica i valori.");

        await _audit.RegistraAsync(
            AreaFunzionale.Nutrizionale, "materieprime.salvata", EsitoOperazione.Successo,
            tipoEntita: "MateriaPrima", idEntita: m.IdMateriaPrima.ToString(),
            dettaglio: new { m.Denominazione }, ct: ct);
        return Json(new { m.IdMateriaPrima, avvisi });
    }

    private static string? Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// ── DTO di richiesta ───────────────────────────────────────────────
public record ValoriNutrizionaliDto
{
    public decimal Grassi { get; init; }
    public decimal GrassiSaturi { get; init; }
    public decimal Carboidrati { get; init; }
    public decimal Zuccheri { get; init; }
    public decimal Proteine { get; init; }
    public decimal Sale { get; init; }
    public decimal Fibre { get; init; }
    public decimal Acqua { get; init; }
    public decimal? EnergiaKj { get; init; }
    public decimal? EnergiaKcal { get; init; }

    // Nutrienti aggiuntivi USA/Canada (per 100 g)
    public decimal? Sodio { get; init; }
    public decimal? GrassiTrans { get; init; }
    public decimal? GrassiMonoinsaturi { get; init; }
    public decimal? GrassiPolinsaturi { get; init; }
    public decimal? Colesterolo { get; init; }
    public decimal? VitaminaD { get; init; }
    public decimal? Calcio { get; init; }
    public decimal? Ferro { get; init; }
    public decimal? Potassio { get; init; }
}

public record FonteDto
{
    public int? IdFonte { get; init; }
    public int Tipo { get; init; }
    public string? Descrizione { get; init; }
    public string? Riferimento { get; init; }
    public string? Url { get; init; }
}

public record RichiestaImportUsda
{
    public string? Cartella { get; init; }
}

public record RichiestaSalvaMateriaPrima
{
    public int? Id { get; init; }
    public string Denominazione { get; init; } = "";
    public int Tipo { get; init; }
    public string? Marca { get; init; }
    public string? Fornitore { get; init; }
    public string? Categoria { get; init; }
    public string? CodiceInterno { get; init; }
    public string? Note { get; init; }
    public int Priorita { get; init; } = 1;
    public string? RiferimentoDocumento { get; init; }
    public ValoriNutrizionaliDto? Valori { get; init; }
    public FonteDto? Fonte { get; init; }
}
