using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Ricerca dei comuni italiani (11/11/2026).
///
/// Serve dovunque si scriva un indirizzo: comune, sigla della provincia
/// e CAP vengono dall'elenco ISTAT invece che dalla tastiera, cosi' nello
/// stesso pacchetto di documenti un luogo si scrive sempre allo stesso
/// modo. Sta fuori dalla manualistica perche' gli indirizzi servono anche
/// a preventivi, ordini e rapportini.
/// </summary>
[ApiController]
public class ComuniController : ControllerBase
{
    private readonly ContestoLaboratorioAI _db;

    public ComuniController(ContestoLaboratorioAI db) => _db = db;

    /// <summary>
    /// I comuni il cui nome comincia per <paramref name="q"/>, con i loro
    /// CAP. Si cerca per inizio e non ovunque nel nome: chi scrive «mar»
    /// cerca Marina di Ravenna, non Lido di Camaiore.
    /// </summary>
    [HttpGet("api/comuni")]
    public async Task<IActionResult> Cerca(string? q, CancellationToken ct)
    {
        var cerca = (q ?? "").Trim();
        if (cerca.Length < 2) return Ok(Array.Empty<object>());

        var comuni = await _db.Comuni
            .Where(c => c.Attivo && c.Nome.StartsWith(cerca))
            .OrderBy(c => c.Nome)
            .Take(20)
            .Select(c => new
            {
                c.Nome,
                c.SiglaProvincia,
                c.Provincia,
                c.Regione,
                Cap = c.Cap.OrderBy(x => x.Cap).Select(x => x.Cap).ToList()
            })
            .ToListAsync(ct);

        return Ok(comuni);
    }
}
