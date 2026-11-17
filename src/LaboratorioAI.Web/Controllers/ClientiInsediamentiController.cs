using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.ArcLab;
using LaboratorioAI.Dati.ArcLab.Repository;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Clienti e insediamenti (17/11/2026): insediamenti ArcLab con campioni recenti che sul portale clienti (Marea.Insediamenti,
/// Marea.Clienti) mancano, sono disattivati o sono collegati al cliente sbagliato. Sola lettura su entrambi i database.
/// Stesso permesso del Controllo pubblicazione certificati.
/// </summary>
[RichiedePermesso(Permessi.CertificatiControlloPubblicazione)]
public class ClientiInsediamentiController : Controller
{
    private readonly RepositoryControlloPubblicazione _repository;
    private readonly IServizioAudit _audit;

    public ClientiInsediamentiController(IFabbricaConnessioniArcLab fabbrica, IServizioAudit audit)
    {
        _repository = new RepositoryControlloPubblicazione(fabbrica);
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Clienti e insediamenti";
        return View();
    }

    [HttpGet("api/clienti-insediamenti/cerca")]
    public async Task<IActionResult> Cerca([FromQuery] int mesi = 12, CancellationToken ct = default)
    {
        mesi = Math.Clamp(mesi, 1, 60);
        var righe = await _repository.AnagraficheNonAllineateAsync(mesi, ct);
        return Ok(new
        {
            mesi,
            righe = righe.Select(a => new
            {
                a.IdCliente, a.RagioneSociale, a.CodContabilita, a.IdInsediamento, a.InsediamentoArcLab, a.Campioni, a.UltimoRicevimento,
                a.Certificati, a.UltimaSpedizione, a.IdInsediamentoPortale, a.InsediamentoPortale, a.IdClientePortale, a.ClientePortale,
                a.Problema, descrizione = a.Descrizione, a.ClienteAttivoArcLab, a.InsediamentoInUsoArcLab,
                statoArcLab = a.StatoArcLab, disattivatoSulPortale = a.DisattivatoSulPortale
            })
        });
    }

    [HttpGet("api/clienti-insediamenti/excel")]
    public async Task<IActionResult> Excel([FromQuery] int mesi = 12, [FromQuery] bool nascondiDisattivati = false,
        [FromQuery] bool nascondiNonAttiviArcLab = false, CancellationToken ct = default)
    {
        mesi = Math.Clamp(mesi, 1, 60);
        var righe = await _repository.AnagraficheNonAllineateAsync(mesi, ct);
        if (nascondiDisattivati) righe = righe.Where(a => !a.DisattivatoSulPortale).ToList();
        if (nascondiNonAttiviArcLab) righe = righe.Where(a => a.ClienteAttivoArcLab && a.InsediamentoInUsoArcLab).ToList();
        await _audit.RegistraAsync(AreaFunzionale.CertificatiEsterni, "clientiinsediamenti.excel", EsitoOperazione.Successo,
            dettaglio: new { mesi, nascondiDisattivati, nascondiNonAttiviArcLab, righe.Count }, ct: ct);
        var file = EsportatoreControlloPubblicazione.GeneraAnagrafiche(righe, mesi);
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Clienti_insediamenti_portale_{DateTime.Today:yyyyMMdd}.xlsx");
    }
}
