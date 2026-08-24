using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.IA;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Ricette e calcolo nutrizionale (Reg. UE 1169/2011). Usa il catalogo materie
/// prime e il motore di calcolo; scrive solo sul database proprio.
/// </summary>
[RichiedePermesso(Permessi.NutrizionaleRicette)]
public class RicetteController : Controller
{
    private const string MimePdf = "application/pdf";
    private const string MimeRtf = "application/rtf";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IServizioCalcoloNutrizionale _calcolo;
    private readonly IServizioEtichettaUsa _usa;
    private readonly IServizioEtichettaCanada _canada;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IGeneratoreRapportoNutrizionale _generatore;
    private readonly IGeneratoreEtichettaUsa _generatoreUsa;
    private readonly IGeneratoreEtichettaCanada _generatoreCanada;
    private readonly IClientAnthropic _ia;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;
    private readonly IServizioAudit _audit;

    public RicetteController(
        ContestoLaboratorioAI ctx,
        IServizioCalcoloNutrizionale calcolo,
        IServizioEtichettaUsa usa,
        IServizioEtichettaCanada canada,
        IRepositoryAnagrafiche anagrafiche,
        IGeneratoreRapportoNutrizionale generatore,
        IGeneratoreEtichettaUsa generatoreUsa,
        IGeneratoreEtichettaCanada generatoreCanada,
        IClientAnthropic ia,
        IContestoUtente utente,
        IServizioVersione versione,
        IServizioAudit audit)
    {
        _ctx = ctx;
        _calcolo = calcolo;
        _usa = usa;
        _canada = canada;
        _anagrafiche = anagrafiche;
        _generatore = generatore;
        _generatoreUsa = generatoreUsa;
        _generatoreCanada = generatoreCanada;
        _ia = ia;
        _utente = utente;
        _versione = versione;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Ricette nutrizionali";
        return View();
    }

    // ── Elenco ─────────────────────────────────────────────────────
    [HttpGet("api/ricette/elenco")]
    public async Task<IActionResult> Elenco(string? q, CancellationToken ct)
    {
        var query = _ctx.Ricette.AsNoTracking().Where(r => r.Attivo);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(r => EF.Functions.Like(r.Denominazione, $"%{t}%")
                                  || (r.Committente != null && EF.Functions.Like(r.Committente, $"%{t}%")));
        }

        var righe = await query
            .OrderByDescending(r => r.DataUltimaModifica)
            .Take(500)
            .Select(r => new
            {
                r.IdRicetta,
                r.Denominazione,
                r.Committente,
                NRighe = r.Righe.Count,
                DataUltimaModifica = r.DataUltimaModifica.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    // ── Dettaglio ──────────────────────────────────────────────────
    [HttpGet("api/ricette/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var r = await _ctx.Ricette.AsNoTracking()
            .Include(x => x.Righe).ThenInclude(rr => rr.MateriaPrima)
            .FirstOrDefaultAsync(x => x.IdRicetta == id, ct);
        if (r is null) return NotFound(new { messaggio = "Ricetta non trovata." });

        return Json(new
        {
            r.IdRicetta, r.Denominazione, r.IdClienteArcLab, r.Committente, r.Lotto,
            r.UnitaConsumoG, r.UnitaConsumoPezzi, r.CaloPesoPercentuale, r.PesoCottoKg, r.Note, r.RedattoDa,
            Righe = r.Righe.OrderBy(x => x.Ordine).Select(x => new
            {
                x.IdMateriaPrima,
                Denominazione = x.MateriaPrima != null ? x.MateriaPrima.Denominazione : "",
                x.QuantitaKg
            })
        });
    }

    // ── Ricerca materie prime (per aggiungere ingredienti) ─────────
    [HttpGet("api/ricette/materie-prime")]
    public async Task<IActionResult> MateriePrime(string? q, CancellationToken ct)
    {
        var query = _ctx.MateriePrime.AsNoTracking().Where(m => m.Attivo);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(m => EF.Functions.Like(m.Denominazione, $"%{t}%")
                                  || (m.Marca != null && EF.Functions.Like(m.Marca, $"%{t}%")));
        }
        var righe = await query
            .OrderBy(m => m.Denominazione)
            .Take(50)
            .Select(m => new
            {
                m.IdMateriaPrima,
                m.Denominazione,
                m.Marca,
                Provenienza =
                    m.Tipo == Core.Enumerazioni.TipoMateriaPrima.Specifica ? "Scheda tecnica"
                    : m.CodiceInterno != null && m.CodiceInterno.StartsWith("USDA-") ? "USDA"
                    : m.CodiceInterno != null && m.CodiceInterno.StartsWith("CNF-") ? "CNF"
                    : "CREA"
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    // ── Committenti (anagrafica ArcLab, sola lettura) ──────────────
    [HttpGet("api/ricette/clienti")]
    public async Task<IActionResult> Clienti(CancellationToken ct)
    {
        var clienti = await _anagrafiche.ClientiAsync(ct);
        return Json(clienti.Select(c => new { c.Id, c.Nome }));
    }

    // ── Calcolo (anteprima, senza salvare) ─────────────────────────
    [HttpPost("api/ricette/calcola")]
    public async Task<IActionResult> Calcola([FromBody] RichiestaCalcoloRicetta req, CancellationToken ct)
    {
        var dati = await ComponiDatiAsync(req.Righe, req.UnitaConsumoG, req.CaloPesoPercentuale, req.PesoCottoKg, ct);
        if (dati is null)
            return BadRequest(new { messaggio = "Aggiungi almeno un ingrediente con quantità maggiore di zero." });

        return Json(_calcolo.Calcola(dati));
    }

    // ── Salvataggio ────────────────────────────────────────────────
    [HttpPost("api/ricette/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaRicetta req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Denominazione))
            return BadRequest(new { messaggio = "La denominazione è obbligatoria." });

        var ora = DateTime.UtcNow;
        Ricetta r;
        if (req.Id is int id)
        {
            r = await _ctx.Ricette.Include(x => x.Righe).FirstOrDefaultAsync(x => x.IdRicetta == id, ct)
                ?? throw new InvalidOperationException("Ricetta non trovata.");
            _ctx.RigheRicetta.RemoveRange(r.Righe);
            r.Righe.Clear();
            r.DataUltimaModifica = ora;
        }
        else
        {
            r = new Ricetta { DataCreazione = ora, DataUltimaModifica = ora };
            _ctx.Ricette.Add(r);
        }

        r.Denominazione = req.Denominazione.Trim();
        r.IdClienteArcLab = req.IdClienteArcLab;
        r.Committente = string.IsNullOrWhiteSpace(req.Committente) ? null : req.Committente.Trim();
        r.Lotto = string.IsNullOrWhiteSpace(req.Lotto) ? null : req.Lotto.Trim();
        r.UnitaConsumoG = req.UnitaConsumoG <= 0 ? 100m : req.UnitaConsumoG;
        r.UnitaConsumoPezzi = req.UnitaConsumoPezzi;
        r.CaloPesoPercentuale = req.CaloPesoPercentuale;
        r.PesoCottoKg = req.PesoCottoKg;
        r.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        r.RedattoDa = string.IsNullOrWhiteSpace(req.RedattoDa) ? null : req.RedattoDa.Trim();

        int ordine = 1;
        foreach (var riga in req.Righe ?? new List<RigaRicettaDto>())
        {
            if (riga.QuantitaKg <= 0) continue;
            r.Righe.Add(new RigaRicetta
            {
                IdMateriaPrima = riga.IdMateriaPrima,
                QuantitaKg = riga.QuantitaKg,
                Ordine = ordine++
            });
        }

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Nutrizionale, "ricetta.salvata", EsitoOperazione.Successo,
            tipoEntita: "Ricetta", idEntita: r.IdRicetta.ToString(),
            dettaglio: new { r.Denominazione, Righe = r.Righe.Count }, ct: ct);
        return Json(new { r.IdRicetta });
    }

    // ── Generazione del rapporto (PDF o Word) + registrazione ──────
    [HttpPost("api/ricette/{id:int}/rapporto")]
    public async Task<IActionResult> Rapporto(int id, [FromBody] RichiestaRapporto? req, CancellationToken ct)
    {
        var r = await _ctx.Ricette.FirstOrDefaultAsync(x => x.IdRicetta == id, ct);
        if (r is null) return NotFound(new { messaggio = "Ricetta non trovata." });

        var righe = await _ctx.RigheRicetta.AsNoTracking()
            .Where(x => x.IdRicetta == id)
            .OrderBy(x => x.Ordine)
            .Select(x => new RigaRicettaDto { IdMateriaPrima = x.IdMateriaPrima, QuantitaKg = x.QuantitaKg })
            .ToListAsync(ct);

        var dati = await ComponiDatiAsync(righe, r.UnitaConsumoG, r.CaloPesoPercentuale, r.PesoCottoKg, ct);
        if (dati is null)
            return BadRequest(new { messaggio = "La ricetta non ha ingredienti con quantità: salva prima la ricetta." });

        var risultato = _calcolo.Calcola(dati);

        // Priorità: responsabile indicato ora; poi quello salvato sulla ricetta;
        // infine l'utente corrente.
        var redattoDa =
            !string.IsNullOrWhiteSpace(req?.RedattoDa) ? req!.RedattoDa!.Trim()
            : !string.IsNullOrWhiteSpace(r.RedattoDa)  ? r.RedattoDa!
            : _utente.NomeCompleto;

        // Se indicato ora e diverso da quello salvato, aggiorna la ricetta.
        if (!string.IsNullOrWhiteSpace(req?.RedattoDa) &&
            !string.Equals(req!.RedattoDa!.Trim(), r.RedattoDa, StringComparison.Ordinal))
        {
            r.RedattoDa = req.RedattoDa!.Trim();
            r.DataUltimaModifica = DateTime.UtcNow;
        }

        var datiRapporto = new DatiRapportoNutrizionale
        {
            Denominazione = r.Denominazione,
            Committente = r.Committente,
            Lotto = r.Lotto,
            RedattoDa = redattoDa,
            DataRapporto = DateOnly.FromDateTime(DateTime.Today),
            UnitaConsumoG = r.UnitaConsumoG,
            UnitaConsumoPezzi = r.UnitaConsumoPezzi,
            Risultato = risultato
        };

        bool word = string.Equals(req?.Formato, "word", StringComparison.OrdinalIgnoreCase);
        var contenuto = word ? _generatore.GeneraWord(datiRapporto) : _generatore.GeneraPdf(datiRapporto);
        var estensione = word ? "rtf" : "pdf";
        var mime = word ? MimeRtf : MimePdf;
        var nomeFile = _generatore.NomeFile(datiRapporto, estensione);

        // Tracciabilità: snapshot riproducibile del rapporto.
        _ctx.RapportiNutrizionali.Add(new RapportoNutrizionale
        {
            IdRicetta = r.IdRicetta,
            DataRapporto = DateTime.UtcNow,
            IdUtente = _utente.IdUtente,
            RedattoDa = redattoDa,
            Committente = r.Committente,
            Denominazione = r.Denominazione,
            Lotto = r.Lotto,
            Risultati = JsonSerializer.Serialize(risultato, JsonOpts),
            RicettaUsata = JsonSerializer.Serialize(righe, JsonOpts),
            VersioneCalcolo = "Reg. UE 1169/2011",
            VersioneApplicazione = _versione.Versione,
            NomeFileGenerato = nomeFile
        });
        await _ctx.SaveChangesAsync(ct);

        return File(contenuto, mime, nomeFile);
    }

    // ── Anteprima Nutrition Facts (USA) ────────────────────────────
    [HttpPost("api/ricette/{id:int}/nutrition-facts")]
    public async Task<IActionResult> NutritionFacts(int id, [FromBody] RichiestaNutritionFacts? req, CancellationToken ct)
    {
        var r = await _ctx.Ricette.FirstOrDefaultAsync(x => x.IdRicetta == id, ct);
        if (r is null) return NotFound(new { messaggio = "Ricetta non trovata." });

        var righe = await _ctx.RigheRicetta.AsNoTracking()
            .Where(x => x.IdRicetta == id).OrderBy(x => x.Ordine)
            .Select(x => new RigaRicettaDto { IdMateriaPrima = x.IdMateriaPrima, QuantitaKg = x.QuantitaKg })
            .ToListAsync(ct);

        var dati = await ComponiDatiAsync(righe, r.UnitaConsumoG, r.CaloPesoPercentuale, r.PesoCottoKg, ct);
        if (dati is null)
            return BadRequest(new { messaggio = "La ricetta non ha ingredienti con quantità." });

        var risultato = _calcolo.Calcola(dati);
        decimal porzione = req?.PorzioneG is > 0 ? req.PorzioneG!.Value : r.UnitaConsumoG;
        var facts = _usa.Calcola(risultato.Per100g, porzione, req?.ZuccheriAggiuntiPer100g ?? 0m);
        return Json(facts);
    }

    // ── Pannello grafico Nutrition Facts (PDF o Word) ──────────────
    [HttpPost("api/ricette/{id:int}/etichetta-usa")]
    public async Task<IActionResult> EtichettaUsa(int id, [FromBody] RichiestaNutritionFacts? req, CancellationToken ct)
    {
        var r = await _ctx.Ricette.FirstOrDefaultAsync(x => x.IdRicetta == id, ct);
        if (r is null) return NotFound(new { messaggio = "Ricetta non trovata." });

        var righe = await _ctx.RigheRicetta.AsNoTracking()
            .Where(x => x.IdRicetta == id).OrderBy(x => x.Ordine)
            .Select(x => new RigaRicettaDto { IdMateriaPrima = x.IdMateriaPrima, QuantitaKg = x.QuantitaKg })
            .ToListAsync(ct);

        var dati = await ComponiDatiAsync(righe, r.UnitaConsumoG, r.CaloPesoPercentuale, r.PesoCottoKg, ct);
        if (dati is null)
            return BadRequest(new { messaggio = "La ricetta non ha ingredienti con quantità." });

        var risultato = _calcolo.Calcola(dati);
        decimal porzione = req?.PorzioneG is > 0 ? req.PorzioneG!.Value : r.UnitaConsumoG;
        var facts = _usa.Calcola(risultato.Per100g, porzione, req?.ZuccheriAggiuntiPer100g ?? 0m);

        var datiEtichetta = new DatiEtichettaUsa
        {
            Denominazione = r.Denominazione,
            PorzioniPerConfezione = req?.PorzioniPerConfezione ?? r.UnitaConsumoPezzi,
            Facts = facts
        };

        bool word = string.Equals(req?.Formato, "word", StringComparison.OrdinalIgnoreCase);
        var contenuto = word ? _generatoreUsa.GeneraWord(datiEtichetta) : _generatoreUsa.GeneraPdf(datiEtichetta);
        var estensione = word ? "rtf" : "pdf";
        var mime = word ? MimeRtf : MimePdf;
        return File(contenuto, mime, _generatoreUsa.NomeFile(datiEtichetta, estensione));
    }

    // ── Anteprima Nutrition Facts (Canada, bilingue) ───────────────
    [HttpPost("api/ricette/{id:int}/nutrition-facts-ca")]
    public async Task<IActionResult> NutritionFactsCa(int id, [FromBody] RichiestaNutritionFacts? req, CancellationToken ct)
    {
        var (r, risultato, porzione, errore) = await PreparaUsaCa(id, req, ct);
        if (errore is not null) return errore;
        return Json(_canada.Calcola(risultato!.Per100g, porzione));
    }

    // ── Pannello grafico Canada (PDF o Word) ───────────────────────
    [HttpPost("api/ricette/{id:int}/etichetta-ca")]
    public async Task<IActionResult> EtichettaCa(int id, [FromBody] RichiestaNutritionFacts? req, CancellationToken ct)
    {
        var (r, risultato, porzione, errore) = await PreparaUsaCa(id, req, ct);
        if (errore is not null) return errore;

        var facts = _canada.Calcola(risultato!.Per100g, porzione);
        var dati = new DatiEtichettaCanada
        {
            Denominazione = r!.Denominazione,
            PorzioniPerConfezione = req?.PorzioniPerConfezione ?? r.UnitaConsumoPezzi,
            Facts = facts
        };
        bool word = string.Equals(req?.Formato, "word", StringComparison.OrdinalIgnoreCase);
        var contenuto = word ? _generatoreCanada.GeneraWord(dati) : _generatoreCanada.GeneraPdf(dati);
        var estensione = word ? "rtf" : "pdf";
        var mime = word ? MimeRtf : MimePdf;
        return File(contenuto, mime, _generatoreCanada.NomeFile(dati, estensione));
    }

    // ── Verifica congruità AI dell'etichetta Canada (archivio-first) ──
    [HttpPost("api/ricette/{id:int}/verifica-ca")]
    public async Task<IActionResult> VerificaCa(int id, [FromBody] RichiestaNutritionFacts? req, CancellationToken ct)
    {
        var (r, risultato, porzione, errore) = await PreparaUsaCa(id, req, ct);
        if (errore is not null) return errore;

        var facts = _canada.Calcola(risultato!.Per100g, porzione);
        var firma = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "CA|" + r!.Denominazione + "|" + porzione.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "|" + JsonSerializer.Serialize(facts, JsonOpts))));

        if (req?.Forza != true)
        {
            var esistente = await _ctx.VerificheNutrizionali.AsNoTracking()
                .Where(v => v.IdRicetta == id && v.Firma == firma)
                .OrderByDescending(v => v.DataVerifica).FirstOrDefaultAsync(ct);
            if (esistente is not null)
                return Json(new { esistente.Esito, esistente.Testo,
                    dataVerifica = esistente.DataVerifica.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), daArchivio = true });
        }

        var prompt = ComponiPromptVerificaCa(r.Denominazione, facts);
        var risposta = await _ia.InterrogaAsync(prompt, ct);
        var (esito, testo) = SeparaVerdetto(risposta.Testo);

        _ctx.VerificheNutrizionali.Add(new VerificaNutrizionale
        {
            IdRicetta = id, DataVerifica = DateTime.UtcNow, Firma = firma, Denominazione = r.Denominazione,
            Esito = esito, Testo = testo, ModelloAi = risposta.Modello,
            TokenInput = risposta.TokenInput, TokenOutput = risposta.TokenOutput,
            VersioneApplicazione = _versione.Versione, IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);
        return Json(new { esito, testo, dataVerifica = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), daArchivio = false });
    }

    /// <summary>Carica ricetta, calcola il risultato e la porzione; ritorna un errore pronto se manca qualcosa.</summary>
    private async Task<(Ricetta? R, RisultatoNutrizionale? Risultato, decimal Porzione, IActionResult? Errore)>
        PreparaUsaCa(int id, RichiestaNutritionFacts? req, CancellationToken ct)
    {
        var r = await _ctx.Ricette.FirstOrDefaultAsync(x => x.IdRicetta == id, ct);
        if (r is null) return (null, null, 0m, NotFound(new { messaggio = "Ricetta non trovata." }));

        var righe = await _ctx.RigheRicetta.AsNoTracking()
            .Where(x => x.IdRicetta == id).OrderBy(x => x.Ordine)
            .Select(x => new RigaRicettaDto { IdMateriaPrima = x.IdMateriaPrima, QuantitaKg = x.QuantitaKg })
            .ToListAsync(ct);

        var dati = await ComponiDatiAsync(righe, r.UnitaConsumoG, r.CaloPesoPercentuale, r.PesoCottoKg, ct);
        if (dati is null)
            return (r, null, 0m, BadRequest(new { messaggio = "La ricetta non ha ingredienti con quantità." }));

        var risultato = _calcolo.Calcola(dati);
        decimal porzione = req?.PorzioneG is > 0 ? req.PorzioneG!.Value : r.UnitaConsumoG;
        return (r, risultato, porzione, null);
    }

    private static string ComponiPromptVerificaCa(string denom, NutrizioneCanada f)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sei un esperto di etichettatura nutrizionale canadese (Health Canada, FDR).");
        sb.AppendLine("Ti fornisco il Tableau de la valeur nutritive calcolato per un prodotto, per porzione.");
        sb.AppendLine("Verifica la CONGRUITÀ e segnala eventuali problemi:");
        sb.AppendLine("- la dimensione della porzione è ragionevole per quel tipo di prodotto?");
        sb.AppendLine("- le %DV (incl. la %DV combinata di saturi + trans) sono coerenti con le quantità?");
        sb.AppendLine("- ci sono valori sospetti (micronutrienti a zero per un prodotto che dovrebbe averne, valori fuori scala)?");
        sb.AppendLine("NON fornire indicazioni salutistiche o nutrizionali (claim).");
        sb.AppendLine("Rispondi in italiano: la PRIMA riga è un verdetto sintetico, poi 2-4 punti brevi.");
        sb.AppendLine();
        sb.AppendLine($"Prodotto: {denom}");
        sb.AppendLine($"Porzione: {f.PorzioneG} g");
        sb.AppendLine($"Calories: {f.Calorie}");
        sb.AppendLine($"Fat: {f.Grassi.Quantita} ({f.Grassi.Dv}%) — Saturated {f.GrassiSaturi} + Trans {f.GrassiTrans} (comb. {f.SaturiTransDv}%)");
        sb.AppendLine($"Cholesterol: {f.Colesterolo} — Sodium: {f.Sodio.Quantita} ({f.Sodio.Dv}%)");
        sb.AppendLine($"Carbohydrate: {f.Carboidrati} — Fibre {f.Fibre.Quantita} ({f.Fibre.Dv}%), Sugars {f.Zuccheri.Quantita} ({f.Zuccheri.Dv}%)");
        sb.AppendLine($"Protein: {f.Proteine}");
        sb.AppendLine($"Potassium {f.Potassio.Quantita} ({f.Potassio.Dv}%), Calcium {f.Calcio.Quantita} ({f.Calcio.Dv}%), Iron {f.Ferro.Quantita} ({f.Ferro.Dv}%)");
        return sb.ToString();
    }

    // ── Verifica congruità AI dell'etichetta USA (archivio-first) ──
    [HttpPost("api/ricette/{id:int}/verifica-usa")]
    public async Task<IActionResult> VerificaUsa(int id, [FromBody] RichiestaNutritionFacts? req, CancellationToken ct)
    {
        var r = await _ctx.Ricette.FirstOrDefaultAsync(x => x.IdRicetta == id, ct);
        if (r is null) return NotFound(new { messaggio = "Ricetta non trovata." });

        var righe = await _ctx.RigheRicetta.AsNoTracking()
            .Where(x => x.IdRicetta == id).OrderBy(x => x.Ordine)
            .Select(x => new RigaRicettaDto { IdMateriaPrima = x.IdMateriaPrima, QuantitaKg = x.QuantitaKg })
            .ToListAsync(ct);

        var dati = await ComponiDatiAsync(righe, r.UnitaConsumoG, r.CaloPesoPercentuale, r.PesoCottoKg, ct);
        if (dati is null)
            return BadRequest(new { messaggio = "La ricetta non ha ingredienti con quantità." });

        var risultato = _calcolo.Calcola(dati);
        decimal porzione = req?.PorzioneG is > 0 ? req.PorzioneG!.Value : r.UnitaConsumoG;
        var facts = _usa.Calcola(risultato.Per100g, porzione, req?.ZuccheriAggiuntiPer100g ?? 0m);

        var firma = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "USA|" + r.Denominazione + "|" + porzione.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "|" + JsonSerializer.Serialize(facts, JsonOpts))));

        if (req?.Forza != true)
        {
            var esistente = await _ctx.VerificheNutrizionali.AsNoTracking()
                .Where(v => v.IdRicetta == id && v.Firma == firma)
                .OrderByDescending(v => v.DataVerifica)
                .FirstOrDefaultAsync(ct);
            if (esistente is not null)
                return Json(new { esistente.Esito, esistente.Testo,
                    dataVerifica = esistente.DataVerifica.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), daArchivio = true });
        }

        var prompt = ComponiPromptVerificaUsa(r.Denominazione, facts);
        var risposta = await _ia.InterrogaAsync(prompt, ct);
        var (esito, testo) = SeparaVerdetto(risposta.Testo);

        _ctx.VerificheNutrizionali.Add(new VerificaNutrizionale
        {
            IdRicetta = id,
            DataVerifica = DateTime.UtcNow,
            Firma = firma,
            Denominazione = r.Denominazione,
            Esito = esito,
            Testo = testo,
            ModelloAi = risposta.Modello,
            TokenInput = risposta.TokenInput,
            TokenOutput = risposta.TokenOutput,
            VersioneApplicazione = _versione.Versione,
            IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);

        return Json(new { esito, testo, dataVerifica = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), daArchivio = false });
    }

    private static string ComponiPromptVerificaUsa(string denom, NutrizioneUsa f)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sei un esperto di etichettatura nutrizionale FDA (USA, 21 CFR 101.9).");
        sb.AppendLine("Ti fornisco la Nutrition Facts calcolata per un prodotto, riferita alla porzione.");
        sb.AppendLine("Verifica la CONGRUITÀ e segnala eventuali problemi:");
        sb.AppendLine("- la dimensione della porzione è ragionevole per quel tipo di prodotto?");
        sb.AppendLine("- gli «Added Sugars» non superano i «Total Sugars»?");
        sb.AppendLine("- le %DV sono coerenti con le quantità dichiarate?");
        sb.AppendLine("- ci sono valori sospetti (micronutrienti a zero per un prodotto che dovrebbe averne, valori fuori scala)?");
        sb.AppendLine("NON fornire indicazioni salutistiche o nutrizionali (claim).");
        sb.AppendLine("Rispondi in italiano: la PRIMA riga è un verdetto sintetico (es. «Congruente» o «Possibili incongruenze»), poi 2-4 punti brevi.");
        sb.AppendLine();
        sb.AppendLine($"Prodotto: {denom}");
        sb.AppendLine($"Porzione: {f.PorzioneG} g");
        sb.AppendLine($"Calories: {f.Calorie}");
        sb.AppendLine($"Total Fat: {f.Grassi.Quantita} ({f.Grassi.Dv}% DV) — Saturated {f.GrassiSaturi.Quantita}, Trans {f.GrassiTrans}");
        sb.AppendLine($"Cholesterol: {f.Colesterolo.Quantita} ({f.Colesterolo.Dv}%) — Sodium: {f.Sodio.Quantita} ({f.Sodio.Dv}%)");
        sb.AppendLine($"Total Carbohydrate: {f.Carboidrati.Quantita} ({f.Carboidrati.Dv}%) — Fiber {f.Fibre.Quantita}, Total Sugars {f.ZuccheriTotali}, Added Sugars {f.ZuccheriAggiunti.Quantita}");
        sb.AppendLine($"Protein: {f.Proteine}");
        sb.AppendLine($"Vitamin D {f.VitaminaD.Quantita} ({f.VitaminaD.Dv}%), Calcium {f.Calcio.Quantita} ({f.Calcio.Dv}%), Iron {f.Ferro.Quantita} ({f.Ferro.Dv}%), Potassium {f.Potassio.Quantita} ({f.Potassio.Dv}%)");
        return sb.ToString();
    }

    // ── Verifica di plausibilità AI (archivio-first) ───────────────
    [HttpPost("api/ricette/{id:int}/verifica")]
    public async Task<IActionResult> Verifica(int id, [FromBody] RichiestaVerifica? req, CancellationToken ct)
    {
        var r = await _ctx.Ricette.FirstOrDefaultAsync(x => x.IdRicetta == id, ct);
        if (r is null) return NotFound(new { messaggio = "Ricetta non trovata." });

        var righe = await _ctx.RigheRicetta.AsNoTracking()
            .Where(x => x.IdRicetta == id).OrderBy(x => x.Ordine)
            .Select(x => new RigaRicettaDto { IdMateriaPrima = x.IdMateriaPrima, QuantitaKg = x.QuantitaKg })
            .ToListAsync(ct);

        var dati = await ComponiDatiAsync(righe, r.UnitaConsumoG, r.CaloPesoPercentuale, r.PesoCottoKg, ct);
        if (dati is null)
            return BadRequest(new { messaggio = "La ricetta non ha ingredienti con quantità: salva prima la ricetta." });

        var risultato = _calcolo.Calcola(dati);
        var firma = CalcolaFirma(r.Denominazione, risultato.Per100g);

        // Archivio-first: se esiste già una verifica per gli stessi valori, la riuso.
        if (req?.Forza != true)
        {
            var esistente = await _ctx.VerificheNutrizionali.AsNoTracking()
                .Where(v => v.IdRicetta == id && v.Firma == firma)
                .OrderByDescending(v => v.DataVerifica)
                .FirstOrDefaultAsync(ct);
            if (esistente is not null)
                return Json(new
                {
                    esistente.Esito,
                    esistente.Testo,
                    dataVerifica = esistente.DataVerifica.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    daArchivio = true
                });
        }

        var prompt = ComponiPromptVerifica(r.Denominazione, risultato);
        var risposta = await _ia.InterrogaAsync(prompt, ct);
        var (esito, testo) = SeparaVerdetto(risposta.Testo);

        _ctx.VerificheNutrizionali.Add(new VerificaNutrizionale
        {
            IdRicetta = id,
            DataVerifica = DateTime.UtcNow,
            Firma = firma,
            Denominazione = r.Denominazione,
            Esito = esito,
            Testo = testo,
            ModelloAi = risposta.Modello,
            TokenInput = risposta.TokenInput,
            TokenOutput = risposta.TokenOutput,
            VersioneApplicazione = _versione.Versione,
            IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Nutrizionale, "ricetta.verifica.ai", EsitoOperazione.Successo,
            tipoEntita: "Ricetta", idEntita: id.ToString(),
            dettaglio: new { Esito = esito, r.Denominazione }, ct: ct);
        return Json(new { esito, testo, dataVerifica = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), daArchivio = false });
    }

    private static string CalcolaFirma(string denom, Dichiarazione d)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var s = string.Join('|', denom,
            d.EnergiaKcal.ToString("F1", ci), d.Grassi.ToString("F2", ci), d.GrassiSaturi.ToString("F2", ci),
            d.Carboidrati.ToString("F2", ci), d.Zuccheri.ToString("F2", ci), d.Proteine.ToString("F2", ci),
            d.Sale.ToString("F3", ci), d.Fibre.ToString("F2", ci), d.Acqua.ToString("F2", ci));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    }

    private static string ComponiPromptVerifica(string denom, RisultatoNutrizionale r)
    {
        var d = r.Per100g;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Sei un tecnologo alimentare esperto di etichettatura nutrizionale.");
        sb.AppendLine("Ti fornisco la dichiarazione nutrizionale calcolata per 100 g di un prodotto e il suo nome.");
        sb.AppendLine("Valuta se i valori sono PLAUSIBILI e coerenti con quel tipo di prodotto e segnala eventuali");
        sb.AppendLine("anomalie che potrebbero indicare un errore di inserimento dati (valori fuori scala, somma dei");
        sb.AppendLine("componenti molto lontana da 100 g, incongruenze come zuccheri a 0 in un dolce, ecc.).");
        sb.AppendLine("NON riformulare l'etichetta e NON fornire indicazioni salutistiche o nutrizionali (claim).");
        sb.AppendLine("Rispondi in italiano: la PRIMA riga è un verdetto sintetico (es. \"Coerente\" oppure");
        sb.AppendLine("\"Possibili anomalie\"); poi da 2 a 4 punti brevi.");
        sb.AppendLine();
        sb.AppendLine($"Prodotto: {denom}");
        sb.AppendLine("Valori per 100 g:");
        sb.AppendLine($"- Energia: {d.EnergiaKcal.ToString("F0", ci)} kcal / {d.EnergiaKj.ToString("F0", ci)} kJ");
        sb.AppendLine($"- Grassi: {d.Grassi.ToString("F2", ci)} g (di cui saturi {d.GrassiSaturi.ToString("F2", ci)} g)");
        sb.AppendLine($"- Carboidrati: {d.Carboidrati.ToString("F2", ci)} g (di cui zuccheri {d.Zuccheri.ToString("F2", ci)} g)");
        sb.AppendLine($"- Proteine: {d.Proteine.ToString("F2", ci)} g");
        sb.AppendLine($"- Sale: {d.Sale.ToString("F3", ci)} g");
        sb.AppendLine($"- Fibre: {d.Fibre.ToString("F2", ci)} g");
        sb.AppendLine($"- Acqua: {d.Acqua.ToString("F2", ci)} g");
        return sb.ToString();
    }

    private static (string Esito, string Testo) SeparaVerdetto(string? testo)
    {
        var pulito = (testo ?? "").Trim();
        if (pulito.Length == 0) return ("", "");
        var prima = pulito.Split('\n')[0].Trim().TrimStart('#', '*', ' ', '-').Trim();
        if (prima.Length > 120) prima = prima[..120];
        return (prima, pulito);
    }

    // ── Costruzione dati di calcolo dai valori correnti del catalogo ─
    private async Task<DatiCalcoloNutrizionale?> ComponiDatiAsync(
        IReadOnlyList<RigaRicettaDto>? righe, decimal unitaConsumoG,
        decimal? caloPesoPercentuale, decimal? pesoCottoKg, CancellationToken ct)
    {
        var valide = (righe ?? new List<RigaRicettaDto>()).Where(r => r.QuantitaKg > 0).ToList();
        if (valide.Count == 0) return null;

        var ids = valide.Select(r => r.IdMateriaPrima).Distinct().ToList();
        var mp = await _ctx.MateriePrime.AsNoTracking()
            .Where(m => ids.Contains(m.IdMateriaPrima))
            .Select(m => new
            {
                m.IdMateriaPrima,
                m.Denominazione,
                V = m.Valori.Where(v => v.Corrente)
                    .Select(v => new { v.Grassi, v.GrassiSaturi, v.Carboidrati, v.Zuccheri,
                                       v.Proteine, v.Sale, v.Fibre, v.Acqua,
                                       v.Sodio, v.GrassiTrans, v.GrassiMonoinsaturi, v.GrassiPolinsaturi,
                                       v.Colesterolo, v.VitaminaD, v.Calcio, v.Ferro, v.Potassio })
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.IdMateriaPrima, ct);

        var ingredienti = valide.Select(r =>
        {
            mp.TryGetValue(r.IdMateriaPrima, out var m);
            var v = m?.V;
            return new IngredienteCalcolo
            {
                Denominazione = m?.Denominazione ?? "",
                QuantitaKg = r.QuantitaKg,
                Valori = v is null ? new ValoriPer100g() : new ValoriPer100g
                {
                    Grassi = v.Grassi, GrassiSaturi = v.GrassiSaturi,
                    Carboidrati = v.Carboidrati, Zuccheri = v.Zuccheri,
                    Proteine = v.Proteine, Sale = v.Sale, Fibre = v.Fibre, Acqua = v.Acqua,
                    Sodio = v.Sodio, GrassiTrans = v.GrassiTrans,
                    GrassiMonoinsaturi = v.GrassiMonoinsaturi, GrassiPolinsaturi = v.GrassiPolinsaturi,
                    Colesterolo = v.Colesterolo, VitaminaD = v.VitaminaD,
                    Calcio = v.Calcio, Ferro = v.Ferro, Potassio = v.Potassio
                }
            };
        }).ToList();

        return new DatiCalcoloNutrizionale
        {
            Ingredienti = ingredienti,
            UnitaConsumoG = unitaConsumoG <= 0 ? 100m : unitaConsumoG,
            CaloPesoPercentuale = caloPesoPercentuale,
            PesoCottoKg = pesoCottoKg
        };
    }
}

// ── DTO ────────────────────────────────────────────────────────────
public record RigaRicettaDto
{
    public int IdMateriaPrima { get; init; }
    public decimal QuantitaKg { get; init; }
}

public record RichiestaNutritionFacts
{
    public decimal? PorzioneG { get; init; }
    public decimal? ZuccheriAggiuntiPer100g { get; init; }
    public decimal? PorzioniPerConfezione { get; init; }

    /// <summary>"pdf" (predefinito) o "word".</summary>
    public string? Formato { get; init; }

    /// <summary>Verifica congruità: rigenera anche se ne esiste già una in archivio.</summary>
    public bool Forza { get; init; }
}

public record RichiestaVerifica
{
    /// <summary>Se vero, rigenera la verifica anche se ne esiste già una in archivio.</summary>
    public bool Forza { get; init; }
}

public record RichiestaRapporto
{
    /// <summary>"pdf" (predefinito) o "word".</summary>
    public string? Formato { get; init; }
    public string? RedattoDa { get; init; }
}

public record RichiestaCalcoloRicetta
{
    public List<RigaRicettaDto>? Righe { get; init; }
    public decimal UnitaConsumoG { get; init; } = 100m;
    public decimal? CaloPesoPercentuale { get; init; }
    public decimal? PesoCottoKg { get; init; }
}

public record RichiestaSalvaRicetta
{
    public int? Id { get; init; }
    public string Denominazione { get; init; } = "";
    public int? IdClienteArcLab { get; init; }
    public string? Committente { get; init; }
    public string? Lotto { get; init; }
    public decimal UnitaConsumoG { get; init; } = 100m;
    public decimal? UnitaConsumoPezzi { get; init; }
    public decimal? CaloPesoPercentuale { get; init; }
    public decimal? PesoCottoKg { get; init; }
    public string? Note { get; init; }
    public string? RedattoDa { get; init; }
    public List<RigaRicettaDto>? Righe { get; init; }
}
