using System.Text.Json;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti.Piscine;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// DVR Piscine: precompilazione da ArcLab (sola lettura), salvataggio della
/// scheda sul database proprio (payload JSON) e generazione del documento Word
/// dal record salvato.
/// </summary>
[RichiedePermesso(Permessi.PiscineGenera)]
public class PiscineController : Controller
{
    private const string MimeDocx =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IRepositoryPiscine _arclab;
    private readonly IGeneratorePiscine _generatore;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;

    public PiscineController(
        IRepositoryPiscine arclab,
        IGeneratorePiscine generatore,
        ContestoLaboratorioAI ctx,
        IContestoUtente utente,
        IServizioVersione versione)
    {
        _arclab = arclab;
        _generatore = generatore;
        _ctx = ctx;
        _utente = utente;
        _versione = versione;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "DVR Piscine";
        return View();
    }

    // ═══════════════════════════════════════════════════════════════
    // LETTURE ARCLAB (precompilazione, sola lettura)
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/piscine/cerca")]
    public async Task<IActionResult> Cerca(string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Json(Array.Empty<AnagraficaPiscina>());

        return Json(await _arclab.CercaInsediamentiAsync(q, ct));
    }

    [HttpGet("api/piscine/precarica/{idInsediamento:int}")]
    public async Task<IActionResult> Precarica(int idInsediamento, CancellationToken ct)
    {
        var anagrafica = await _arclab.LeggiAnagraficaAsync(idInsediamento, ct);
        if (anagrafica is null)
            return NotFound(new { messaggio = "Insediamento non trovato." });

        var punti = await _arclab.LeggiPuntiPrelievoStoriciAsync(idInsediamento, ct);
        var storiche = await _arclab.LeggiAnalisiStoricheAsync(idInsediamento, ct);

        var scheda = new SchedaPiscina
        {
            Anagrafica = anagrafica,
            PuntiPrelievo = punti,
            DataValutazione = DateOnly.FromDateTime(DateTime.Today),
        };

        return Json(new { scheda, analisiStoriche = storiche });
    }

    /// <summary>
    /// Elenco standard dei parametri di controllo (Tabella A / UNI 10637 / ISTISAN),
    /// fonte centrale riutilizzabile su ogni manuale.
    /// </summary>
    [HttpGet("api/piscine/standard/parametri")]
    public IActionResult ParametriStandard() => Json(StandardPiscine.ParametriTabellaA());

    /// <summary>
    /// Modulo Word VUOTO e stampabile per la raccolta dati durante il sopralluogo.
    /// Non richiede una scheda salvata: è utile da stampare prima della visita.
    /// La sezione specifica Marche è inclusa quando la regione contiene "march".
    /// </summary>
    [HttpGet("api/piscine/scheda-raccolta")]
    public IActionResult SchedaRaccolta(string? regione)
    {
        var marche = (regione ?? "").ToLowerInvariant().Contains("march");
        var documento = GeneratoreSchedaRaccolta.Genera(marche);
        var tipo = marche ? "Marche" : "EmiliaRomagna";
        var nome = $"Scheda_Raccolta_Dati_Piscine_{tipo}_{DateTime.Today:yyyyMMdd}.docx";
        return File(documento, MimeDocx, nome);
    }

    // ═══════════════════════════════════════════════════════════════
    // PERSISTENZA (database proprio, mai ArcLab)
    // ═══════════════════════════════════════════════════════════════

    [HttpPost("api/piscine/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaPiscina req, CancellationToken ct)
    {
        if (req.Scheda is null)
            return BadRequest(new { messaggio = "Scheda mancante." });

        var json = JsonSerializer.Serialize(req.Scheda, JsonOpts);
        var a = req.Scheda.Anagrafica;
        var ora = DateTime.UtcNow;

        DvrPiscina dvr;
        if (req.Id is int id)
        {
            var esistente = await _ctx.DvrPiscine.FirstOrDefaultAsync(x => x.IdDvrPiscina == id, ct);
            if (esistente is null)
                return NotFound(new { messaggio = "DVR piscina non trovato." });
            dvr = esistente;
            dvr.DataUltimaModifica = ora;
        }
        else
        {
            dvr = new DvrPiscina { DataCreazione = ora, DataUltimaModifica = ora };
            _ctx.DvrPiscine.Add(dvr);
        }

        dvr.IdInsediamentoArcLab = a.IdInsediamento;
        dvr.IdClienteArcLab = a.IdCliente;
        dvr.RagioneSociale = a.RagioneSociale;
        dvr.Comune = a.Comune;
        dvr.Provincia = a.Provincia;
        dvr.Regione = a.Regione;
        dvr.Stato = req.Stato;
        dvr.DatiScheda = json;
        dvr.IdUtente = _utente.IdUtente;
        dvr.VersioneApplicazione = _versione.Versione;

        await _ctx.SaveChangesAsync(ct);
        return Json(new { dvr.IdDvrPiscina });
    }

    [HttpGet("api/piscine/elenco")]
    [RichiedePermesso(Permessi.PiscineRegistro)]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var righe = await _ctx.DvrPiscine
            .OrderByDescending(x => x.DataUltimaModifica)
            .Take(500)
            .Select(x => new
            {
                x.IdDvrPiscina,
                x.RagioneSociale,
                x.Comune,
                x.Provincia,
                x.Regione,
                Stato = x.Stato.ToString(),
                DataUltimaModifica = x.DataUltimaModifica.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                Generato = x.DataUltimaGenerazione != null
            })
            .ToListAsync(ct);

        return Json(righe);
    }

    [HttpGet("api/piscine/{id:int}")]
    public async Task<IActionResult> Carica(int id, CancellationToken ct)
    {
        var dvr = await _ctx.DvrPiscine.FirstOrDefaultAsync(x => x.IdDvrPiscina == id, ct);
        if (dvr is null)
            return NotFound(new { messaggio = "DVR piscina non trovato." });

        var scheda = JsonSerializer.Deserialize<SchedaPiscina>(dvr.DatiScheda, JsonOpts);
        return Json(new { id = dvr.IdDvrPiscina, stato = dvr.Stato.ToString(), scheda });
    }

    [HttpPost("api/piscine/genera/{id:int}")]
    public async Task<IActionResult> Genera(int id, CancellationToken ct)
    {
        var dvr = await _ctx.DvrPiscine.FirstOrDefaultAsync(x => x.IdDvrPiscina == id, ct);
        if (dvr is null)
            return NotFound(new { messaggio = "DVR piscina non trovato." });

        var scheda = JsonSerializer.Deserialize<SchedaPiscina>(dvr.DatiScheda, JsonOpts);
        if (scheda is null)
            return Problem("Scheda non deserializzabile.");

        var esito = _generatore.Genera(scheda);
        if (!esito.Riuscito || esito.Documento is null)
            return Problem(esito.Errore ?? "Generazione non riuscita.");

        if (esito.TagNonTrovati.Count > 0)
            Response.Headers["X-Piscine-TagNonTrovati"] = string.Join(",", esito.TagNonTrovati);

        var cod = scheda.Anagrafica.CodContabilita;
        var etichetta = string.IsNullOrWhiteSpace(cod) ? dvr.RagioneSociale : $"{cod}_{dvr.RagioneSociale}";
        var nome = $"DVR_Piscine_{PulisciNomeFile(etichetta)}_{DateTime.Today:yyyyMMdd}.docx";
        dvr.DataUltimaGenerazione = DateTime.UtcNow;
        dvr.NomeFileGenerato = nome;
        dvr.PercorsoArchivio = esito.PercorsoArchiviato;
        await _ctx.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(esito.PercorsoArchiviato))
            Response.Headers["X-Piscine-Archivio"] = Uri.EscapeDataString(esito.PercorsoArchiviato);

        return File(esito.Documento, MimeDocx, nome);
    }

    [HttpPost("api/piscine/genera-regolamento/{id:int}")]
    public async Task<IActionResult> GeneraRegolamento(int id, CancellationToken ct)
    {
        var dvr = await _ctx.DvrPiscine.FirstOrDefaultAsync(x => x.IdDvrPiscina == id, ct);
        if (dvr is null)
            return NotFound(new { messaggio = "DVR piscina non trovato." });

        var scheda = JsonSerializer.Deserialize<SchedaPiscina>(dvr.DatiScheda, JsonOpts);
        if (scheda is null)
            return Problem("Scheda non deserializzabile.");

        var esito = _generatore.GeneraRegolamento(scheda);
        if (!esito.Riuscito || esito.Documento is null)
            return Problem(esito.Errore ?? "Generazione non riuscita.");

        if (!string.IsNullOrWhiteSpace(esito.PercorsoArchiviato))
            Response.Headers["X-Piscine-Archivio"] = Uri.EscapeDataString(esito.PercorsoArchiviato);

        var nome = $"Regolamento_Piscine_{PulisciNomeFile(dvr.RagioneSociale)}_{DateTime.Today:yyyyMMdd}.docx";
        return File(esito.Documento, MimeDocx, nome);
    }

    [HttpPost("api/piscine/genera-manuale/{id:int}")]
    public async Task<IActionResult> GeneraManuale(int id, CancellationToken ct)
    {
        var dvr = await _ctx.DvrPiscine.FirstOrDefaultAsync(x => x.IdDvrPiscina == id, ct);
        if (dvr is null)
            return NotFound(new { messaggio = "DVR piscina non trovato." });

        var scheda = JsonSerializer.Deserialize<SchedaPiscina>(dvr.DatiScheda, JsonOpts);
        if (scheda is null)
            return Problem("Scheda non deserializzabile.");

        // Certificati dell'acqua di piscina: riletti da ArcLab (non salvati nella
        // scheda). Se presenti, il generatore aggiunge la tabella dei certificati.
        var storiche = await _arclab.LeggiAnalisiStoricheAsync(scheda.Anagrafica.IdInsediamento, ct);
        scheda = scheda with { AnalisiStoriche = storiche };

        var esito = _generatore.GeneraManuale(scheda);
        if (!esito.Riuscito || esito.Documento is null)
            return Problem(esito.Errore ?? "Generazione non riuscita.");

        if (!string.IsNullOrWhiteSpace(esito.PercorsoArchiviato))
            Response.Headers["X-Piscine-Archivio"] = Uri.EscapeDataString(esito.PercorsoArchiviato);

        var cod = scheda.Anagrafica.CodContabilita;
        var etichetta = string.IsNullOrWhiteSpace(cod) ? dvr.RagioneSociale : $"{cod}_{dvr.RagioneSociale}";
        // Nelle Marche il documento è un «Registro tecnico funzionale», non un «Manuale».
        var tipo = (scheda.Anagrafica.Regione ?? "").ToLowerInvariant().Contains("march") ? "Registro" : "Manuale";
        var nome = $"{tipo}_Piscine_{PulisciNomeFile(etichetta)}_{DateTime.Today:yyyyMMdd}.docx";
        return File(esito.Documento, MimeDocx, nome);
    }

    /// <summary>
    /// Ripulisce una stringa per usarla come nome file: tiene solo lettere ASCII,
    /// cifre, trattino e underscore (punti, accenti, spazi e simboli → underscore).
    /// Nome ASCII "semplice" per evitare l'header Content-Disposition con
    /// <c>filename*=UTF-8''…</c>, che alcuni browser salvano storpiando il nome.
    /// </summary>
    private static string PulisciNomeFile(string? testo)
    {
        var s = (testo ?? "documento").Trim();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append((c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ? c : '_');
        var pulito = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
        if (pulito.Length == 0) pulito = "documento";
        return pulito.Length > 60 ? pulito[..60].Trim('_') : pulito;
    }
}

/// <summary>Corpo della richiesta di salvataggio di un DVR piscina.</summary>
public record RichiestaSalvaPiscina
{
    /// <summary>Id del record da aggiornare; nullo per un nuovo salvataggio.</summary>
    public int? Id { get; init; }

    /// <summary>Stato con cui salvare (Bozza o Definitivo).</summary>
    public StatoDvrPiscina Stato { get; init; }

    /// <summary>Dati della scheda da serializzare.</summary>
    public SchedaPiscina? Scheda { get; init; }
}
