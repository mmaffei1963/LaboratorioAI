using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Portale.Autenticazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Portale.Controllers;

[ApiController]
[Route("api/campioni")]
[RichiedeSessione]
public class CampioniController : ControllerBase
{
    private readonly IRepositoryPortale _repository;
    private readonly IContestoSessione _sessione;

    public CampioniController(IRepositoryPortale repository, IContestoSessione sessione)
    {
        _repository = repository;
        _sessione = sessione;
    }

    /// <summary>
    /// Determina il cliente di cui mostrare i dati.
    ///
    /// Per le sessioni cliente è sempre quello della sessione, ignorando
    /// qualunque valore giunga dalla richiesta. Solo le sessioni operatore
    /// possono indicare un cliente diverso.
    /// </summary>
    private int? ClienteDaUsare(int? richiesto) =>
        _sessione.Operatore ? richiesto : _sessione.IdCliente;

    [HttpGet("clienti")]
    [RichiedeSessione(SoloOperatore = true)]
    public async Task<IActionResult> Clienti(CancellationToken ct) =>
        Ok(await _repository.ClientiAsync(ct));

    [HttpGet("filtri")]
    public async Task<IActionResult> Filtri(int? idCliente, CancellationToken ct)
    {
        var filtri = await _repository.FiltriAsync(ClienteDaUsare(idCliente), ct);

        return Ok(new
        {
            filtri.Insediamenti,
            filtri.Matrici,
            filtri.Controlli,
            _sessione.Operatore
        });
    }

    [HttpPost("cerca")]
    public async Task<IActionResult> Cerca(
        [FromBody] CriteriPortale criteri, CancellationToken ct)
    {
        var idCliente = ClienteDaUsare(criteri.IdCliente);

        var campioni = await _repository.CercaCampioniAsync(idCliente, criteri, ct);

        return Ok(new
        {
            Sintesi = new SintesiPortale
            {
                Totale = campioni.Count,
                Certificati = campioni.Count(c => c.Stato == StatoCampione.Certificato),
                InCorso = campioni.Count(c => c.Stato == StatoCampione.InCorso)
            },
            Troncato = campioni.Count >= criteri.Limite,
            Campioni = campioni.Select(c => new
            {
                c.IdCampione,
                c.NumeroCampione,
                c.RagioneSociale,
                c.NomeInsediamento,
                c.DenominazioneCampione,
                c.Matrice,
                c.Controllo,
                c.LuogoPrelievo,
                c.PuntoPrelievo,
                c.VerbalePrelievo,
                DataRicezione = c.DataRicezione?.ToString("dd/MM/yyyy"),
                DataPrelievo  = c.DataPrelievo?.ToString("dd/MM/yyyy"),
                Stato = c.Stato.ToString()
            })
        });
    }

    /// <summary>
    /// Risultati analitici di un campione.
    ///
    /// Riservato alle sessioni operatore: si tratta di dati gestionali non
    /// validati, che non devono essere esposti al cliente. Il documento
    /// che fa fede resta il rapporto di prova firmato.
    /// </summary>
    [HttpGet("{idCampione:int}/prove")]
    [RichiedeSessione(SoloOperatore = true)]
    public async Task<IActionResult> Prove(int idCampione, CancellationToken ct)
    {
        var prove = await _repository.ProveAsync(idCampione, null, ct);

        if (prove.Count == 0)
            return NotFound(new { messaggio = "Nessuna prova registrata per questo campione." });

        return Ok(prove.Select(p => new
        {
            Parametro = p.DenominazioneRiga ?? p.Parametro,
            p.RiferimentoNormativo,
            Valore = p.ValoreFormattato,
            Intervallo = p.IntervalloFormattato,
            p.UnitaMisura,
            p.VerificaValore,
            p.LaboratorioEsterno,
            p.Accreditato
        }));
    }
}
