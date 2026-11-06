using System.Net;
using System.Text;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.IA;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Proposta di nuovi controlli (consulenti/clienti). Scrive in tabelle di staging
/// (schema «controlli»), MAI su ArcLab. All'approvazione invia un'email con tutti i dati
/// utili per l'inserimento manuale del controllo in ArcLab (nessun import automatico).
/// </summary>
[RichiedePermesso(Permessi.ControlliProponi)]
public class ControlliController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryControlli _catalogo;
    private readonly IClientAnthropic _ia;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IServizioEmail _email;
    private readonly IConfiguration _config;
    private readonly ILogger<ControlliController> _log;

    public ControlliController(
        ContestoLaboratorioAI ctx, IRepositoryControlli catalogo,
        IClientAnthropic ia, IContestoUtente utente, IServizioAudit audit,
        IServizioEmail email, IConfiguration config, ILogger<ControlliController> log)
    {
        _ctx = ctx; _catalogo = catalogo; _ia = ia; _utente = utente; _audit = audit;
        _email = email; _config = config; _log = log;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Proposta controlli";
        return View();
    }

    // ── Catalogo metodi (ArcLab, sola lettura) ──
    [HttpGet("api/controlli/metodi")]
    public async Task<IActionResult> Metodi([FromQuery] string? filtro, CancellationToken ct) =>
        Json(await _catalogo.MetodiCatalogoAsync(filtro, ct));

    /// <summary>Controlli attivi, per la modifica di un controllo esistente (ricerca per nome o codice).</summary>
    [HttpGet("api/controlli/attivi")]
    public async Task<IActionResult> Attivi(CancellationToken ct) => Json(await _catalogo.ControlliAttiviAsync(ct));

    /// <summary>Righe metodo correnti di un controllo esistente (precarica la modifica).</summary>
    [HttpGet("api/controlli/righe-esistente")]
    public async Task<IActionResult> RigheEsistente([FromQuery] int idControllo, CancellationToken ct) =>
        Json(await _catalogo.RigheDiControlloAsync(idControllo, ct));

    /// <summary>Controlli ArcLab esistenti con lo stesso insieme di metodi (possibili duplicati).</summary>
    [HttpPost("api/controlli/duplicati")]
    public async Task<IActionResult> Duplicati([FromBody] ElencoMetodiDto req, CancellationToken ct)
    {
        var ids = (req.IdMetodi ?? new()).Where(i => i > 0).Distinct().ToList();
        var trovati = await _catalogo.ControlliConStessiMetodiAsync(ids, req.EscludiIdControllo, ct);
        return Json(trovati.Select(t => new { t.IdControllo, t.Controllo }));
    }

    /// <summary>Proposta di nome "intelligente" per il controllo, in base ai parametri scelti (AI).</summary>
    [HttpPost("api/controlli/proponi-nome")]
    public async Task<IActionResult> ProponiNome([FromBody] ElencoParametriDto req, CancellationToken ct)
    {
        var parametri = (req.Parametri ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (parametri.Count == 0) return BadRequest(new { messaggio = "Aggiungi prima qualche metodo." });

        var sigle = await _catalogo.SigleDisponibiliAsync(ct);
        var vocabolario = sigle.Count > 0
            ? "\n\nVocabolario di sigle di laboratorio da preferire quando pertinenti: " + string.Join(", ", sigle.Distinct()) + "."
            : "";

        var prompt =
            "Sei un esperto di laboratorio di analisi. Proponi UN solo nome breve, chiaro e professionale " +
            "(massimo 8 parole, in italiano) per un pacchetto di analisi (\"controllo\"). Il nome deve far capire " +
            "quali parametri contiene, usando SIGLE brevi: usa la sigla indicata tra parentesi quadre quando è " +
            "presente accanto al parametro; per gli altri usa sigle o simboli standard di laboratorio (es. Pb, Cd, " +
            "Hg, As, pH, EC per Escherichia coli). Parametri/metodi del controllo:\n- " +
            string.Join("\n- ", parametri) + vocabolario +
            "\n\nRispondi esclusivamente con il nome proposto, senza virgolette e senza spiegazioni.";
        try
        {
            var r = await _ia.InterrogaAsync(prompt, ct);
            var nome = (r.Testo ?? "").Trim().Trim('"').Split('\n')[0].Trim();
            return Json(new { nome });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { messaggio = "Suggerimento non disponibile: " + ex.Message });
        }
    }

    // ── Registro e dettaglio ──
    [HttpGet("api/controlli/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var lista = await _ctx.ProposteControllo.AsNoTracking()
            .Where(p => p.Attivo && p.DataCancellazione == null)
            .OrderByDescending(p => p.DataCreazione).Take(300)
            .Select(p => new
            {
                p.IdPropostaControllo, p.CodiceLocale, p.Stato,
                p.Controllo, p.Origine, p.DataCreazione,
                NumeroMetodi = p.Righe.Count,
                // Lo sconto riga in fase di proposta è sempre 0 (lo imposta il laboratorio), quindi
                // il totale è la somma dei costi; il calcolo con sconto è replicato lato client.
                Prezzo = p.Righe.Sum(r => r.CostoMetodoEuro ?? 0m)
            }).ToListAsync(ct);

        // Stato di importazione da ArcLab (registro dbo.ImportDaLaboratorioAI, sola lettura).
        // Se ArcLab è lento/irraggiungibile il registro si carica comunque, senza lo stato import.
        var codici = lista.Select(x => x.CodiceLocale).ToList();
        Dictionary<string, int> importati;
        try
        {
            importati = (await _catalogo.StatiImportAsync(codici, ct))
                .GroupBy(x => x.CodiceLocale).ToDictionary(g => g.Key, g => g.First().IdControllo);
        }
        catch
        {
            importati = new Dictionary<string, int>();
        }

        // Riconciliazione: le proposte presenti nel registro passano a stato Importato (sul DB proprio).
        if (importati.Count > 0)
        {
            var codiciImportati = importati.Keys.ToList();
            var daAggiornare = await _ctx.ProposteControllo
                .Where(p => codiciImportati.Contains(p.CodiceLocale) && p.Stato != StatoProposta.Importato)
                .ToListAsync(ct);
            if (daAggiornare.Count > 0)
            {
                foreach (var p in daAggiornare) { p.Stato = StatoProposta.Importato; p.DataUltimaModifica = DateTime.UtcNow; }
                await _ctx.SaveChangesAsync(ct);
            }
        }

        var risposta = lista.Select(x => new
        {
            x.IdPropostaControllo, x.CodiceLocale,
            Stato = importati.ContainsKey(x.CodiceLocale) ? nameof(StatoProposta.Importato) : x.Stato.ToString(),
            x.Controllo, x.Origine, x.DataCreazione, x.NumeroMetodi, x.Prezzo,
            Importato = importati.ContainsKey(x.CodiceLocale),
            IdControlloArcLab = importati.TryGetValue(x.CodiceLocale, out var idc) ? (int?)idc : null
        });
        return Json(risposta);
    }

    [HttpGet("api/controlli/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var p = await _ctx.ProposteControllo.AsNoTracking()
            .Include(x => x.Righe)
            .FirstOrDefaultAsync(x => x.IdPropostaControllo == id, ct);
        if (p is null) return NotFound();
        return Json(new
        {
            p.IdPropostaControllo, p.CodiceLocale, Stato = p.Stato.ToString(),
            p.Controllo, p.Note, p.Origine, p.IdControlloEsistenteArcLab,
            Righe = p.Righe.OrderBy(r => r.Progressivo).Select(r => new
            {
                r.Progressivo, r.IdMetodoArcLab, r.Metodo, r.DenominazioneRiga, r.RifBiblio,
                r.CostoMetodoEuro, r.ScontoRiga, r.IdUnitaArcLab, r.UnitaMisura,
                r.IdLabEsternoArcLab, r.LabEsterno, r.CifreSignificative, r.MaxDecimali
            })
        });
    }

    // ── Salvataggio ──
    [HttpPost("api/controlli/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaProposta req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Controllo))
            return BadRequest(new { messaggio = "Indicare il nome del controllo." });
        var righe = req.Righe ?? new();
        if (righe.Count == 0)
            return BadRequest(new { messaggio = "Aggiungi almeno un metodo al controllo." });
        if (righe.Any(r => r.IdMetodoArcLab <= 0))
            return BadRequest(new { messaggio = "Ogni riga deve riferirsi a un metodo valido." });
        // Cifre significative (arrotonda) e MaxDecimali devono essere compilate per ogni riga.
        var senzaArrot = righe.Where(r => r.CifreSignificative is null || r.MaxDecimali is null)
                              .Select(r => r.DenominazioneRiga ?? r.Metodo ?? "metodo").ToList();
        if (senzaArrot.Count > 0)
            return BadRequest(new { messaggio = "Cifre significative / MaxDecimali mancanti su: " + string.Join(", ", senzaArrot) + "." });
        // Un controllo può contenere metodi di un solo laboratorio esterno.
        var labEsterni = righe.Where(r => r.IdLabEsternoArcLab.HasValue)
                              .Select(r => r.IdLabEsternoArcLab!.Value).Distinct().ToList();
        if (labEsterni.Count > 1)
            return BadRequest(new { messaggio = "Un controllo può contenere metodi di un solo laboratorio esterno." });
        // Se si parte da un controllo esistente, il nuovo controllo non può riusarne il nome tale e quale.
        if (req.IdControlloEsistenteArcLab is int idEsist and > 0)
        {
            var nomeOrig = await _catalogo.NomeControlloAsync(idEsist, ct);
            if (!string.IsNullOrWhiteSpace(nomeOrig) &&
                string.Equals(nomeOrig.Trim(), req.Controllo!.Trim(), StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { messaggio = $"Il controllo di partenza si chiama «{nomeOrig.Trim()}»: cambia il nome per proporne uno nuovo." });
        }

        var ora = DateTime.UtcNow;
        PropostaControllo p;
        if (req.IdPropostaControllo is int id and > 0)
        {
            p = await _ctx.ProposteControllo.Include(x => x.Righe)
                    .FirstOrDefaultAsync(x => x.IdPropostaControllo == id, ct)
                ?? throw new InvalidOperationException("Proposta non trovata.");
            if (p.Stato is StatoProposta.Trasmesso or StatoProposta.Importato)
                return BadRequest(new { messaggio = "Proposta già trasmessa: non modificabile." });
            _ctx.ProposteRigheMetodo.RemoveRange(p.Righe);
            p.Righe.Clear();
            p.DataUltimaModifica = ora;
        }
        else
        {
            p = new PropostaControllo { DataCreazione = ora, DataUltimaModifica = ora, Stato = StatoProposta.Bozza };
            if (!string.IsNullOrWhiteSpace(req.CodiceLocale)) p.CodiceLocale = req.CodiceLocale!.Trim();
            _ctx.ProposteControllo.Add(p);
        }

        p.Controllo = req.Controllo!.Trim();
        p.Note = req.Note;
        p.IdControlloEsistenteArcLab = req.IdControlloEsistenteArcLab;
        p.Origine = string.IsNullOrWhiteSpace(req.Origine) ? "consulente" : req.Origine;
        p.IdUtente = _utente.IdUtente;
        if (req.Stato is int s && Enum.IsDefined(typeof(StatoProposta), s)) p.Stato = (StatoProposta)s;

        int prog = 1;
        foreach (var rDto in righe)
            p.Righe.Add(new PropostaRigaMetodo
            {
                Progressivo = rDto.Progressivo > 0 ? rDto.Progressivo : prog++,
                IdMetodoArcLab = rDto.IdMetodoArcLab,
                Metodo = rDto.Metodo,
                DenominazioneRiga = rDto.DenominazioneRiga,
                RifBiblio = rDto.RifBiblio,
                CostoMetodoEuro = rDto.CostoMetodoEuro,
                ScontoRiga = rDto.ScontoRiga,
                IdUnitaArcLab = rDto.IdUnitaArcLab,
                UnitaMisura = rDto.UnitaMisura,
                IdLabEsternoArcLab = rDto.IdLabEsternoArcLab,
                LabEsterno = rDto.LabEsterno,
                CifreSignificative = rDto.CifreSignificative,
                MaxDecimali = rDto.MaxDecimali
            });

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Controlli, "proposta.salvata", EsitoOperazione.Successo,
            tipoEntita: "PropostaControllo", idEntita: p.IdPropostaControllo.ToString(),
            dettaglio: new { p.Controllo, Metodi = p.Righe.Count, p.IdControlloEsistenteArcLab, Stato = p.Stato.ToString() }, ct: ct);
        return Json(new { p.IdPropostaControllo, p.CodiceLocale, Stato = p.Stato.ToString() });
    }

    [HttpPost("api/controlli/{id:int}/elimina")]
    public async Task<IActionResult> Elimina(int id, CancellationToken ct)
    {
        var p = await _ctx.ProposteControllo.FirstOrDefaultAsync(x => x.IdPropostaControllo == id, ct);
        if (p is null) return NotFound();
        if (p.Stato is StatoProposta.Trasmesso or StatoProposta.Importato)
            return BadRequest(new { messaggio = "Proposta già trasmessa: non eliminabile." });
        p.Attivo = false; p.DataCancellazione = DateTime.UtcNow; p.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Controlli, "proposta.eliminata", EsitoOperazione.Successo,
            tipoEntita: "PropostaControllo", idEntita: id.ToString(),
            dettaglio: new { p.Controllo }, ct: ct);
        return Ok();
    }

    // ── Approvazione: segna la proposta approvata e invia subito l'email con i dati per
    // l'inserimento manuale in ArcLab (nessun import automatico). ──
    [HttpPost("api/controlli/{id:int}/approva")]
    [RichiedePermesso(Permessi.ControlliApprova)]
    public async Task<IActionResult> Approva(int id, CancellationToken ct)
    {
        var p = await _ctx.ProposteControllo.Include(x => x.Righe)
            .FirstOrDefaultAsync(x => x.IdPropostaControllo == id, ct);
        if (p is null) return NotFound(new { messaggio = "Proposta non trovata." });
        if (p.Righe.Count == 0) return BadRequest(new { messaggio = "Nessun metodo: aggiungine almeno uno." });
        if (p.Stato is StatoProposta.Trasmesso or StatoProposta.Importato)
            return BadRequest(new { messaggio = "Proposta già trasmessa: non è più possibile approvarla di nuovo." });

        p.Stato = StatoProposta.Approvato;
        p.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Controlli, "proposta.approvata", EsitoOperazione.Successo,
            tipoEntita: "PropostaControllo", idEntita: id.ToString(),
            dettaglio: new { p.Controllo, Metodi = p.Righe.Count }, ct: ct);

        var destinatari = _config.GetSection("Controlli:EmailResponsabili").Get<string[]>() ?? Array.Empty<string>();
        try
        {
            await _email.InviaAsync(
                destinatari,
                $"Nuovo controllo da inserire in ArcLab: {p.Controllo}",
                CorpoEmailApprovazione(p), ct);

            p.Stato = StatoProposta.Trasmesso;
            p.DataTrasmissione = DateTime.UtcNow;
            p.DataUltimaModifica = DateTime.UtcNow;
            await _ctx.SaveChangesAsync(ct);
            await _audit.RegistraAsync(
                AreaFunzionale.Controlli, "proposta.trasmessa", EsitoOperazione.Successo,
                tipoEntita: "PropostaControllo", idEntita: id.ToString(),
                dettaglio: new { Destinatari = destinatari }, ct: ct);
        }
        catch (Exception ex)
        {
            // La proposta resta Approvato (non Trasmesso): l'utente può ritentare l'invio
            // ripremendo "Approva", che è idempotente finché non si passa a Trasmesso/Importato.
            _log.LogError(ex, "Invio email proposta controllo {Id} non riuscito", id);
            await _audit.RegistraAsync(
                AreaFunzionale.Controlli, "proposta.trasmessa", EsitoOperazione.Errore,
                tipoEntita: "PropostaControllo", idEntita: id.ToString(),
                dettaglio: new { Errore = ex.Message }, ct: ct);
            return StatusCode(502, new
            {
                p.IdPropostaControllo,
                Stato = p.Stato.ToString(),
                messaggio = "Proposta approvata, ma l'invio dell'email non è riuscito: " + ex.Message + ". Riprova con \"Approva\"."
            });
        }

        return Json(new { p.IdPropostaControllo, Stato = p.Stato.ToString() });
    }

    /// <summary>Corpo HTML dell'email inviata all'approvazione, con tutti i dati per creare
    /// manualmente il controllo (e la relativa commessa, se nuova) in ArcLab.</summary>
    private static string CorpoEmailApprovazione(PropostaControllo p)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#222\">");
        sb.Append($"<h2 style=\"margin:0 0 4px\">{E(p.Controllo)}</h2>");
        sb.Append($"<p style=\"margin:0 0 12px;color:#555\">Codice locale: <b>{E(p.CodiceLocale)}</b> &middot; Origine: {E(p.Origine)}</p>");

        if (p.IdControlloEsistenteArcLab is int idEsist)
            sb.Append($"<p><b>Modifica del controllo ArcLab esistente ID {idEsist}</b>: creare un nuovo controllo con i metodi sottostanti (non sovrascrivere l'esistente).</p>");
        else
            sb.Append("<p>Nuovo controllo: nessun controllo di partenza in ArcLab.</p>");

        if (!string.IsNullOrWhiteSpace(p.Note))
            sb.Append($"<p><b>Note:</b> {E(p.Note)}</p>");

        sb.Append("<table style=\"border-collapse:collapse;width:100%;margin-top:8px\" cellpadding=\"6\">");
        sb.Append("<thead><tr style=\"background:#f0f0f0;text-align:left\">");
        foreach (var intestazione in new[] { "#", "Metodo", "Denominazione riga", "Rif. biblio", "U.M.", "Lab esterno", "Cifre sign.", "Max decimali", "Costo €", "Sconto %" })
            sb.Append($"<th style=\"border:1px solid #ddd\">{E(intestazione)}</th>");
        sb.Append("</tr></thead><tbody>");

        decimal totale = 0;
        foreach (var r in p.Righe.OrderBy(r => r.Progressivo))
        {
            totale += r.CostoMetodoEuro ?? 0m;
            sb.Append("<tr>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{r.Progressivo}</td>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{E(r.Metodo)} (ID {r.IdMetodoArcLab})</td>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{E(r.DenominazioneRiga)}</td>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{E(r.RifBiblio)}</td>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{E(r.UnitaMisura)}</td>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{E(r.LabEsterno)}</td>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{r.CifreSignificative}</td>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{r.MaxDecimali}</td>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{r.CostoMetodoEuro:0.00}</td>");
            sb.Append($"<td style=\"border:1px solid #ddd\">{r.ScontoRiga:0.##}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append($"<p style=\"margin-top:10px\"><b>Totale (senza sconti riga): {totale:0.00} €</b></p>");
        sb.Append("<p style=\"color:#888;font-size:12px;margin-top:16px\">Email generata automaticamente da Laboratorio AI all'approvazione della proposta. Il controllo NON viene inserito automaticamente in ArcLab: va creato a mano.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }
}

// ── DTO ──
public record RichiestaSalvaProposta
{
    public int? IdPropostaControllo { get; init; }
    public string? CodiceLocale { get; init; }
    public int? Stato { get; init; }
    public string? Controllo { get; init; }
    public string? Note { get; init; }
    public string? Origine { get; init; }
    public int? IdControlloEsistenteArcLab { get; init; }
    public List<RigaMetodoDto>? Righe { get; init; }
}

public record ElencoMetodiDto { public List<int>? IdMetodi { get; init; } public int? EscludiIdControllo { get; init; } }
public record ElencoParametriDto { public List<string>? Parametri { get; init; } }

public record RigaMetodoDto
{
    public int Progressivo { get; init; }
    public int IdMetodoArcLab { get; init; }
    public string? Metodo { get; init; }
    public string? DenominazioneRiga { get; init; }
    public string? RifBiblio { get; init; }
    public decimal? CostoMetodoEuro { get; init; }
    public double ScontoRiga { get; init; }
    public int? IdUnitaArcLab { get; init; }
    public string? UnitaMisura { get; init; }
    public int? IdLabEsternoArcLab { get; init; }
    public string? LabEsterno { get; init; }
    public int? CifreSignificative { get; init; }
    public int? MaxDecimali { get; init; }
}
