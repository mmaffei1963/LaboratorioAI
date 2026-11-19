using LaboratorioAI.Portale.Autenticazione;
using LaboratorioAI.Portale.Servizi;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Portale.Controllers;

/// <summary>
/// Documenti (19/11/2026): documenti del cliente pubblicati su Palomar (Marea.Entries), scarico e «segna non letto».
/// Il cliente vede solo i suoi documenti (vincolo dalla sessione, anche sullo scarico); l'operatore sceglie il cliente.
/// </summary>
[ApiController]
[Route("api/documenti")]
[RichiedeSessione]
public class DocumentiController : ControllerBase
{
    private readonly ServizioDocumentiPortale _servizio;
    private readonly IContestoSessione _sessione;

    public DocumentiController(ServizioDocumentiPortale servizio, IContestoSessione sessione)
    {
        _servizio = servizio;
        _sessione = sessione;
    }

    private int? ClienteDaUsare(int? richiesto) => _sessione.Operatore ? richiesto : _sessione.IdCliente;

    [HttpGet]
    public async Task<IActionResult> Elenco(int? idCliente, CancellationToken ct)
    {
        if (ClienteDaUsare(idCliente) is not int id)
            return BadRequest(new { messaggio = "Scegli un cliente per vedere i suoi documenti." });
        var righe = await _servizio.DocumentiAsync(id, ct);
        return Ok(righe.Select(r => new
        {
            r.Documento.Id,
            r.Area,
            r.Certificato,
            r.Letto,
            r.Estensione,
            DataUpload = r.Documento.DataUpload?.ToString("yyyy-MM-dd"),
            OraUpload = r.Documento.DataUpload?.ToString("HH:mm"),
            Tipo = r.Documento.Tipo?.Trim(),
            Titolo = r.Documento.Titolo?.Trim(),
            r.Documento.IdInsediamento,
            Insediamento = r.Documento.Insediamento?.Trim(),
            r.Documento.IsPositivo,
            Campione = r.Documento.NomeCampione?.Trim(),
            DataPrelievo = r.Documento.DataPrelievo?.ToString("yyyy-MM-dd"),
            Verbale = r.Documento.VerbalePrelievo?.Trim(),
            Luogo = r.Documento.LuogoPrelievo?.Trim(),
            Punto = r.Documento.PuntoPrelievo?.Trim(),
            // La scadenza non vale per i certificati.
            Scadenza = r.Certificato ? null : r.Documento.ValidoFinoA?.ToString("yyyy-MM-dd"),
            r.Documento.ByCliente,
            Tag = r.Documento.Filtri?.Trim()
        }));
    }

    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> Scarica(int id, int? idCliente, CancellationToken ct)
    {
        var (file, errore) = await _servizio.ScaricaAsync(id, ClienteDaUsare(idCliente), ct);
        if (file is null) return NotFound(new { messaggio = errore });
        return File(file.Contenuto, file.TipoContenuto, file.NomeFile);
    }

    [HttpPost("{id:int}/non-letto")]
    public async Task<IActionResult> NonLetto(int id, int? idCliente, CancellationToken ct)
    {
        // Solo il cliente: l'operatore non cambia lo stato di lettura del cliente.
        if (_sessione.Operatore)
            return BadRequest(new { messaggio = "Lo stato letto/non letto è del cliente: l'accesso operatore non lo modifica." });
        if (_sessione.IdCliente is not int cliente) return BadRequest(new { messaggio = "Sessione senza cliente." });
        return await _servizio.SegnaNonLettoAsync(id, cliente, ct)
            ? Ok(new { ok = true })
            : NotFound(new { messaggio = "Documento non trovato." });
    }
}
