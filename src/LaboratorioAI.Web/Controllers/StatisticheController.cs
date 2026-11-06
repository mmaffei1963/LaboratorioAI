using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Ricerca trasversale sull'archivio a livello di singola prova (parametro
/// analizzato): cliente, insediamento, commessa, metodo, valore calcolato.
/// Sola lettura su ArcLab.
/// </summary>
[RichiedePermesso(Permessi.StatisticheVisualizza)]
public class StatisticheController : Controller
{
    private const string MimeXlsx =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IRepositoryStatistiche _repository;
    private readonly IEsportatoreStatistiche _esportatore;
    private readonly IServizioAudit _audit;

    public StatisticheController(
        IRepositoryStatistiche repository, IEsportatoreStatistiche esportatore, IServizioAudit audit)
    {
        _repository = repository;
        _esportatore = esportatore;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Statistiche";
        return View();
    }

    [HttpPost("api/statistiche/interroga")]
    public async Task<IActionResult> Interroga(
        [FromBody] CriteriStatistiche criteri, CancellationToken ct)
    {
        if (criteri.DataRicDa == default || criteri.DataRicA == default)
            return BadRequest(new { messaggio = "Indicare il periodo (data ricezione)." });

        var esito = await _repository.InterrogaAsync(criteri, ct);

        return Json(new
        {
            esito.Totale,
            esito.Troncato,
            Righe = esito.Righe.Select(r => new
            {
                r.IdCampione,
                r.IdProva,
                r.NumeroCampione,
                r.IdCliente,
                r.Cliente,
                r.IdInsediamento,
                r.Insediamento,
                r.Citta,
                r.Provincia,
                r.Cap,
                r.Indirizzo,
                r.IdCommessa,
                r.Commessa,
                r.NCertificato,
                r.DenominazioneCampione,
                r.Matrice,
                DataRicezione = r.DataRicezione?.ToString("dd/MM/yyyy"),
                DataPrelievo = r.DataPrelievo?.ToString("dd/MM/yyyy"),
                r.PuntoPrelievo,
                r.TempPrelievo,
                r.LottoProduzione,
                DataProduzione = r.DataProduzione?.ToString("dd/MM/yyyy"),
                r.Lavorazione,
                r.Parametro,
                r.Metodo,
                r.RiferimentoNormativo,
                r.Accreditato,
                r.ValoreCalcolato,
                r.MinoreDi,
                r.UnitaMisura
            })
        });
    }

    [HttpPost("api/statistiche/esporta")]
    public async Task<IActionResult> Esporta(
        [FromBody] CriteriStatistiche criteri, CancellationToken ct)
    {
        if (criteri.DataRicDa == default || criteri.DataRicA == default)
            return BadRequest(new { messaggio = "Indicare il periodo (data ricezione)." });

        // Alza il limite per esportare tutte le righe corrispondenti, non le sole a video.
        var criteriEstesi = criteri with { Limite = 100_000 };
        var esito = await _repository.InterrogaAsync(criteriEstesi, ct);
        var contenuto = _esportatore.Genera(esito.Righe, criteriEstesi);

        await _audit.RegistraAsync(
            AreaFunzionale.Statistiche, "statistiche.esportazione.excel", EsitoOperazione.Successo,
            dettaglio: new { Righe = esito.Righe.Count }, ct: ct);

        return File(contenuto, MimeXlsx, _esportatore.NomeFile(criteriEstesi));
    }
}
