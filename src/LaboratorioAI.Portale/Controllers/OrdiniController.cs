using LaboratorioAI.Portale.Autenticazione;
using LaboratorioAI.Portale.Servizi;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Portale.Controllers;

/// <summary>
/// Ordini di accettazione (18/11/2026): lotti ricevuti del cliente e PDF dell'Ordine. Il cliente vede solo i suoi lotti
/// (vincolo dalla sessione, anche sul PDF); l'operatore sceglie il cliente.
/// </summary>
[ApiController]
[Route("api/ordini")]
[RichiedeSessione]
public class OrdiniController : ControllerBase
{
    private readonly ServizioOrdiniPortale _servizio;
    private readonly IContestoSessione _sessione;

    public OrdiniController(ServizioOrdiniPortale servizio, IContestoSessione sessione)
    {
        _servizio = servizio;
        _sessione = sessione;
    }

    private int? ClienteDaUsare(int? richiesto) => _sessione.Operatore ? richiesto : _sessione.IdCliente;

    [HttpGet]
    public async Task<IActionResult> Lotti(int? idCliente, DateTime? dal, DateTime? al, int? idInsediamento, string? lotto, CancellationToken ct)
    {
        if (ClienteDaUsare(idCliente) is not int id)
            return BadRequest(new { messaggio = "Scegli un cliente per vedere i suoi Ordini di accettazione." });
        var lotti = await _servizio.LottiAsync(id, dal, al, idInsediamento, lotto, ct);
        return Ok(lotti.Select(l => new
        {
            l.IdLotto, l.CodLotto, DataRicezione = l.DataRicezione?.ToString("dd/MM/yyyy"), l.Insediamento, l.InsediamentoCitta,
            l.NumeroCampioni, l.Pronto
        }));
    }

    [HttpGet("{idLotto:int}/pdf")]
    public async Task<IActionResult> Pdf(int idLotto, int? idCliente, CancellationToken ct)
    {
        // Il cliente: sempre e solo i propri lotti. L'operatore: il cliente scelto, se indicato.
        var (documento, errore) = await _servizio.DocumentoAsync(idLotto, ClienteDaUsare(idCliente), ct);
        if (documento is null) return NotFound(new { messaggio = errore });
        return File(documento.Contenuto, "application/pdf", documento.NomeFile);
    }
}
