using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

[RichiedePermesso(Permessi.MetodiConsulta)]
public class MetodiController : Controller
{
    private readonly IRepositoryMetodi _metodi;
    private readonly IContestoUtente _utente;

    public MetodiController(IRepositoryMetodi metodi, IContestoUtente utente)
    {
        _metodi = metodi;
        _utente = utente;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Metodi e Controlli";
        return View();
    }

    [HttpGet("api/metodi/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var metodi = await _metodi.MetodiAttiviAsync(ct);

        return Json(metodi.Select(m => new
        {
            m.IdMetodo,
            m.RiferimentoNormativo,
            m.NomeParametro,
            m.DenominazioneRiga,
            m.Acronimo, m.RifMetodo, m.DettagliMetodo, m.UnitaMisura,   // 05/11/2026
            m.Accreditato,
            m.LaboratorioEsterno,
            m.AccreditatoEsterno,
            m.TestoRicerca
        }));
    }

    [HttpGet("api/metodi/controlli")]
    public async Task<IActionResult> Controlli(CancellationToken ct) =>
        Json(await _metodi.ControlliAsync(ct));

    /// <summary>Controlli che soddisfano la logica indicata sui metodi selezionati.</summary>
    [HttpPost("api/metodi/cerca-controlli")]
    public async Task<IActionResult> CercaControlli(
        [FromBody] RichiestaControlli richiesta, CancellationToken ct)
    {
        if (richiesta.IdMetodi.Count == 0)
            return BadRequest(new { messaggio = "Selezionare almeno un metodo." });

        var logica = Enum.TryParse<LogicaRicercaControlli>(richiesta.Logica, true, out var l)
            ? l : LogicaRicercaControlli.Tutti;

        var controlli = await _metodi.ControlliPerMetodiAsync(richiesta.IdMetodi, logica, ct);

        return Json(controlli);
    }

    /// <summary>
    /// Metodi che compongono un controllo. Il quadro dei prezzi è incluso
    /// solo per gli utenti che dispongono del permesso relativo.
    /// </summary>
    [HttpGet("api/metodi/controllo/{idControllo:int}")]
    public async Task<IActionResult> Controllo(int idControllo, CancellationToken ct)
    {
        var metodi = await _metodi.MetodiDelControlloAsync(idControllo, ct);

        if (metodi.Count == 0)
            return NotFound(new { messaggio = "Controllo inesistente o privo di metodi." });

        var prezzi = _utente.HaPermesso(Permessi.MetodiPrezzi)
            ? await _metodi.PrezziControlloAsync(idControllo, ct)
            : null;

        return Json(new
        {
            IdControllo = idControllo,
            Controllo = metodi[0].Controllo,
            Metodi = metodi.Select(m => new
            {
                m.IdMetodo,
                m.DenominazioneRiga,
                m.RiferimentoNormativo,
                m.NomeParametro,
                m.Accreditato,
                m.LaboratorioEsterno,
                m.AccreditatoEsterno,
                m.Attivo
            }),
            Prezzi = prezzi is null || !prezzi.Disponibili ? null : new
            {
                prezzi.PrezzoMinimo,
                prezzi.ClientePrezzoMinimo,
                prezzi.ClientiAlMinimo,
                prezzi.PrezzoMassimo,
                prezzi.ClientePrezzoMassimo,
                prezzi.ClientiAlMassimo,
                prezzi.ClientiTotali,
                prezzi.PrezzoMedio,
                prezzi.PrezzoUnico
            }
        });
    }

    public record RichiestaControlli(List<int> IdMetodi, string Logica);

    // ── Controlli per matrice (12/11/2026) ─────────────────────────────

    [HttpGet("api/metodi/matrici")]
    public async Task<IActionResult> Matrici(CancellationToken ct) => Json(await _metodi.MatriciAsync(ct));

    /// <summary>
    /// Controlli usati sui campioni delle matrici indicate nel periodo (data ricevimento), dal più usato, contando solo le
    /// commesse di laboratorio attive con campioni. Prezzi (minimo, medio pesato per cliente, massimo) solo con il permesso.
    /// </summary>
    [HttpGet("api/metodi/controlli-per-matrice")]
    public async Task<IActionResult> ControlliPerMatrice([FromQuery] string? matrici, [FromQuery] DateTime? dal, [FromQuery] DateTime? al, CancellationToken ct)
    {
        var (righe, errore, da, a) = await CercaPerMatriceAsync(matrici, dal, al, ct);
        if (errore is not null) return BadRequest(new { messaggio = errore });
        var prezzi = _utente.HaPermesso(Permessi.MetodiPrezzi);
        return Json(new
        {
            dal = da, al = a, prezzi,
            controlli = righe.Select(c => new
            {
                c.IdControllo, c.Controllo, c.Campioni, c.Clienti, c.Commesse,
                PrezzoMinimo = prezzi ? c.PrezzoMinimo : null,
                PrezzoMedio = prezzi ? c.PrezzoMedio : null,
                PrezzoMassimo = prezzi ? c.PrezzoMassimo : null
            })
        });
    }

    [HttpGet("api/metodi/controlli-per-matrice/excel")]
    public async Task<IActionResult> ControlliPerMatriceExcel([FromQuery] string? matrici, [FromQuery] DateTime? dal, [FromQuery] DateTime? al, CancellationToken ct)
    {
        var (righe, errore, da, a) = await CercaPerMatriceAsync(matrici, dal, al, ct);
        if (errore is not null) return BadRequest(new { messaggio = errore });
        var ids = ParseIds(matrici);
        var nomi = (await _metodi.MatriciAsync(ct)).Where(m => ids.Contains(m.Id)).Select(m => m.Nome).ToList();
        var file = EsportatoreControlliPerMatrice.Genera(righe, nomi, da, a, _utente.HaPermesso(Permessi.MetodiPrezzi));
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Controlli_per_matrice_{da:yyyyMMdd}-{a:yyyyMMdd}.xlsx");
    }

    private static List<int> ParseIds(string? testo) =>
        (testo ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n != 0).Distinct().ToList();

    private async Task<(IReadOnlyList<ControlloPerMatrice> Righe, string? Errore, DateTime Dal, DateTime Al)> CercaPerMatriceAsync(
        string? matrici, DateTime? dal, DateTime? al, CancellationToken ct)
    {
        var a = (al ?? DateTime.Today).Date;
        var da = (dal ?? a.AddYears(-1).AddDays(1)).Date;
        var ids = ParseIds(matrici);
        if (ids.Count == 0) return ([], "Scegli almeno una matrice.", da, a);
        if (a < da) return ([], "La data «al» è precedente alla data «dal».", da, a);
        if (ids.Count > 200) return ([], "Troppe matrici: restringi la scelta.", da, a);
        var righe = await _metodi.CampioniPerMatriciAsync(ids, da, a, ct);
        return (StatistichePrezzi.PerMatrice(righe), null, da, a);
    }
}
