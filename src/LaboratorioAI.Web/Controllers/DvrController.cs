using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti.Dvr;
using LaboratorioAI.Web.Autorizzazione;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

[RichiedePermesso(Permessi.DvrGenera)]
public class DvrController : Controller
{
    private readonly IRepositoryDvr _strutture;
    private readonly IGeneratoreDvr _generatore;
    private readonly IServizioLottiDvr _lotti;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;
    private readonly IServizioAudit _audit;

    public DvrController(
        IRepositoryDvr strutture,
        IGeneratoreDvr generatore,
        IServizioLottiDvr lotti,
        ContestoLaboratorioAI ctx,
        IContestoUtente utente,
        IServizioVersione versione,
        IServizioAudit audit)
    {
        _strutture = strutture;
        _generatore = generatore;
        _lotti = lotti;
        _ctx = ctx;
        _utente = utente;
        _versione = versione;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "DVR Legionella";
        return View();
    }

    // ═══════════════════════════════════════════════════════════════
    // STRUTTURE
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/dvr/strutture")]
    public async Task<IActionResult> Strutture(string? filtro, CancellationToken ct)
    {
        var strutture = await _strutture.StruttureAsync(filtro, ct);

        return Json(strutture.Select(s => new
        {
            s.IdCliente,
            s.IdInsediamento,
            s.RagioneSociale,
            s.NomeInsediamento,
            s.Indirizzo,
            s.Comune,
            s.Provincia,
            s.Cap,
            UltimoCampionamento = s.UltimoCampionamento?.ToString("dd/MM/yyyy"),
            RegionePresunta = s.RegionePresunta?.ToString()
        }));
    }

    /// <summary>Dati di una struttura, con i punti di prelievo già usati in passato.</summary>
    [HttpGet("api/dvr/struttura/{idInsediamento:int}")]
    public async Task<IActionResult> Struttura(int idInsediamento, CancellationToken ct)
    {
        var struttura = await _strutture.StrutturaAsync(idInsediamento, ct);
        if (struttura is null) return NotFound(new { messaggio = "Struttura non trovata." });

        var punti = await _strutture.PuntiStoriciAsync(idInsediamento, ct);

        return Json(new
        {
            struttura.IdCliente,
            struttura.IdInsediamento,
            struttura.RagioneSociale,
            struttura.NomeInsediamento,
            struttura.Indirizzo,
            struttura.Comune,
            struttura.Provincia,
            struttura.Cap,
            struttura.PartitaIva,
            RegionePresunta = struttura.RegionePresunta?.ToString(),
            PuntiPrelievo = punti
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // GENERAZIONE
    // ═══════════════════════════════════════════════════════════════

    [HttpPost("api/dvr/genera")]
    public async Task<IActionResult> Genera([FromBody] SchedaDvr scheda, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(scheda.RagioneSociale))
            return BadRequest(new { messaggio = "Indicare la ragione sociale." });

        var esito = await _generatore.GeneraAsync(scheda, ct);

        _ctx.RegistroDvr.Add(new VoceRegistroDvr
        {
            IdCliente = scheda.IdCliente,
            IdInsediamento = scheda.IdInsediamento,
            RagioneSociale = scheda.RagioneSociale,
            NomeInsediamento = scheda.NomeInsediamento,
            Regione = scheda.Regione.ToString(),
            DataGenerazione = DateTime.UtcNow,
            IdUtente = _utente.IdUtente,
            NomeFile = esito.NomeFile ?? "(non prodotto)",
            PercorsoArchivio = esito.PercorsoCompleto,
            Esito = esito.Riuscito ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            Note = esito.Messaggio,
            VersioneApplicazione = _versione.Versione
        });

        await _ctx.SaveChangesAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Dvr,
            "dvr.generato",
            esito.Riuscito ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            "Insediamento", scheda.IdInsediamento?.ToString(),
            new { scheda.RagioneSociale, esito.NomeFile },
            ct);

        return esito.Riuscito
            ? Json(new
            {
                esito.NomeFile,
                CampiNonPopolati = esito.CampiNonPopolati
            })
            : BadRequest(new { messaggio = esito.Messaggio });
    }

    [HttpPost("api/dvr/genera-lotto")]
    [RichiedePermesso(Permessi.DvrLotto)]
    public IActionResult GeneraLotto([FromBody] List<SchedaDvr> schede)
    {
        if (schede.Count == 0)
            return BadRequest(new { messaggio = "Nessuna struttura selezionata." });

        var idLotto = _lotti.Avvia(schede, _utente.IdUtente, _versione.Versione);

        return Json(new { IdLotto = idLotto });
    }

    [HttpGet("api/dvr/lotto/{idLotto:guid}")]
    [RichiedePermesso(Permessi.DvrLotto)]
    public IActionResult StatoLotto(Guid idLotto)
    {
        var stato = _lotti.Stato(idLotto);

        return stato is null
            ? NotFound(new { messaggio = "Lotto inesistente o già rimosso." })
            : Json(stato);
    }

    // ═══════════════════════════════════════════════════════════════
    // REGISTRO E DIAGNOSI
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/dvr/registro")]
    [RichiedePermesso(Permessi.DvrRegistro)]
    public async Task<IActionResult> Registro(int limite = 200, CancellationToken ct = default)
    {
        var righe = await _ctx.RegistroDvr
            .Include(v => v.Utente)
            .OrderByDescending(v => v.IdVoce)
            .Take(limite)
            .Select(v => new
            {
                v.IdVoce,
                v.RagioneSociale,
                v.NomeInsediamento,
                v.Regione,
                Data = v.DataGenerazione,
                Operatore = v.Utente!.NomeCompleto,
                v.NomeFile,
                Esito = v.Esito.ToString(),
                v.Note
            })
            .ToListAsync(ct);

        return Json(righe.Select(r => new
        {
            r.IdVoce,
            r.RagioneSociale,
            r.NomeInsediamento,
            r.Regione,
            Data = r.Data.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            r.Operatore,
            r.NomeFile,
            r.Esito,
            r.Note
        }));
    }

    /// <summary>
    /// Elenca i controlli presenti nel modello.
    ///
    /// Va usato dopo ogni modifica ai modelli documentali: confrontando
    /// l'esito con i campi della scheda si individuano i controlli che
    /// resterebbero senza valore.
    /// </summary>
    [HttpGet("api/dvr/ispeziona/{regione}")]
    public async Task<IActionResult> Ispeziona(string regione, CancellationToken ct)
    {
        if (!Enum.TryParse<RegioneDvr>(regione, true, out var valore))
            return BadRequest(new { messaggio = "Regione non riconosciuta." });

        try
        {
            var controlli = await _generatore.IspezionaModelloAsync(valore, ct);
            return Json(new { Regione = valore.ToString(), Controlli = controlli });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { messaggio = ex.Message });
        }
    }
}
