using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

[RichiedePermesso(Permessi.LaboratoriEsterniConsulta)]
public class LaboratoriEsterniController : Controller
{
    private readonly IRepositoryLaboratoriEsterni _laboratori;
    private readonly IEsportatoreLaboratoriEsterni _esportatore;
    private readonly IServizioAudit _audit;

    public LaboratoriEsterniController(
        IRepositoryLaboratoriEsterni laboratori,
        IEsportatoreLaboratoriEsterni esportatore,
        IServizioAudit audit)
    {
        _laboratori = laboratori;
        _esportatore = esportatore;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Laboratori Esterni";
        return View();
    }

    [HttpGet("api/laboratori-esterni/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct) =>
        Json(await _laboratori.LaboratoriAsync(ct));

    [HttpPost("api/laboratori-esterni/interroga")]
    public async Task<IActionResult> Interroga(
        [FromBody] CriteriLaboratoriEsterni criteri, CancellationToken ct)
    {
        var esito = await _laboratori.InterrogaAsync(criteri, ct);

        return Json(new
        {
            esito.TotaleProve,
            esito.Certificate,
            esito.InCorso,
            esito.LaboratoriDistinti,
            esito.Troncato,
            Riepilogo = esito.Riepilogo.Select(r => new
            {
                r.LaboratorioEsterno,
                r.Matrice,
                r.DenominazioneRiga,
                r.RiferimentoNormativo,
                r.NumeroProve,
                r.NumeroCertificate
            }),
            Prove = esito.Prove.Select(p => new
            {
                p.IdCampione,
                p.NumeroCampione,
                p.RagioneSociale,
                p.NomeInsediamento,
                DataRicezione = p.DataRicezione?.ToString("dd/MM/yyyy"),
                DataAnalisi   = p.DataAnalisi?.ToString("dd/MM/yyyy"),
                p.DenominazioneCampione,
                p.Matrice,
                p.DenominazioneRiga,
                p.RiferimentoNormativo,
                p.LaboratorioEsterno,
                p.AccreditatoEsterno,
                Stato = p.Stato.ToString()
            })
        });
    }

    /// <summary>Esporta l'interrogazione corrente in formato Excel.</summary>
    [HttpPost("api/laboratori-esterni/esporta")]
    [RichiedePermesso(Permessi.LaboratoriEsterniEsporta)]
    public async Task<IActionResult> Esporta(
        [FromBody] CriteriLaboratoriEsterni criteri, CancellationToken ct)
    {
        // L'esportazione non è limitata come la visualizzazione a schermo
        var criteriEstesi = criteri with { Limite = 20000 };

        var esito = await _laboratori.InterrogaAsync(criteriEstesi, ct);
        var contenuto = _esportatore.Genera(esito, criteriEstesi);

        await _audit.RegistraAsync(
            AreaFunzionale.LaboratoriEsterni,
            "esportazione.excel",
            EsitoOperazione.Successo,
            dettaglio: new
            {
                Dal = criteri.DataDa.ToString("yyyy-MM-dd"),
                Al = criteri.DataA.ToString("yyyy-MM-dd"),
                Prove = esito.TotaleProve
            },
            ct: ct);

        return File(
            contenuto,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _esportatore.NomeFile(criteriEstesi));
    }
}
