using System.Text.RegularExpressions;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Amministrazione → Sigle laboratori esterni. Per ogni laboratorio esterno di ArcLab (letto in sola
/// lettura) si gestiscono la sigla usata nel nome dei certificati esterni per Palomar e le parole chiave
/// che lo riconoscono nel PDF. Nessuna cancellazione fisica: una sigla non più valida si disattiva.
/// </summary>
[RichiedePermesso(Permessi.AmministrazioneConfigurazioni)]
public class SigleLaboratoriController : Controller
{
    private static readonly Regex SiglaValida = new("^[A-Z0-9]{1,20}$", RegexOptions.Compiled);

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryLaboratoriEsterni _labArcLab;
    private readonly IServizioAudit _audit;
    private readonly IContestoUtente _utente;

    public SigleLaboratoriController(ContestoLaboratorioAI ctx, IRepositoryLaboratoriEsterni labArcLab,
        IServizioAudit audit, IContestoUtente utente)
    {
        _ctx = ctx; _labArcLab = labArcLab; _audit = audit; _utente = utente;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Sigle laboratori esterni";
        return View();
    }

    /// <summary>Tutti i laboratori esterni ArcLab con l'eventuale sigla salvata o, se manca, una proposta.</summary>
    [HttpGet("api/sigle-laboratori")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var labs = await _labArcLab.LaboratoriConFraseAsync(ct);
        var salvate = await _ctx.SigleLaboratoriEsterni.AsNoTracking().ToDictionaryAsync(s => s.IdLabEsternoArcLab, ct);

        var righe = labs.Select(l =>
        {
            salvate.TryGetValue(l.Id, out var s);
            return new
            {
                idLabEsterno = l.Id,
                nome = l.Nome,
                salvata = s is not null,
                sigla = s?.Sigla ?? CostruttoreNomeCertificatoEsterno.AcronimoDa(l.Nome),
                paroleChiave = s?.ParoleChiave ?? ParolaProposta(l.Nome),
                attivo = s?.Attivo ?? true
            };
        })
        .OrderBy(r => r.salvata).ThenBy(r => r.nome) // prima quelle ancora da salvare
        .ToList();
        return Json(righe);
    }

    public record SiglaDto(int IdLabEsterno, string Sigla, string? ParoleChiave, bool Attivo);

    [HttpPost("api/sigle-laboratori")]
    public async Task<IActionResult> Salva([FromBody] SiglaDto d, CancellationToken ct)
    {
        var sigla = (d.Sigla ?? "").Trim().ToUpperInvariant();
        if (!SiglaValida.IsMatch(sigla))
            return BadRequest(new { messaggio = "La sigla deve contenere solo lettere e cifre (niente trattini o spazi), massimo 20 caratteri." });
        // In ArcLab gli id possono essere negativi (numerazione casuale stile Access): vale solo lo zero come "assente".
        if (d.IdLabEsterno == 0) return BadRequest(new { messaggio = "Laboratorio non valido." });

        var parole = string.Join(", ", (d.ParoleChiave ?? "")
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant()).Distinct());
        if (parole.Length > 500) return BadRequest(new { messaggio = "Parole chiave troppo lunghe (massimo 500 caratteri)." });

        var ora = DateTime.UtcNow;
        var riga = await _ctx.SigleLaboratoriEsterni.FirstOrDefaultAsync(s => s.IdLabEsternoArcLab == d.IdLabEsterno, ct);
        if (riga is null)
        {
            riga = new SiglaLaboratorioEsterno { IdLabEsternoArcLab = d.IdLabEsterno, DataCreazione = ora };
            _ctx.SigleLaboratoriEsterni.Add(riga);
        }
        riga.Sigla = sigla;
        riga.ParoleChiave = parole.Length == 0 ? null : parole;
        riga.Attivo = d.Attivo;
        riga.DataUltimaModifica = ora;
        riga.IdUtente = _utente.IdUtente;

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "sigle-laboratori.salvata",
            tipoEntita: "SiglaLaboratorioEsterno", idEntita: d.IdLabEsterno.ToString(),
            dettaglio: new { riga.Sigla, riga.ParoleChiave, riga.Attivo }, ct: ct);
        return Json(new { ok = true, sigla = riga.Sigla, paroleChiave = riga.ParoleChiave });
    }

    /// <summary>Proposta di parola chiave: la voce riconosciuta dalle sigle predefinite, altrimenti il nome.</summary>
    private static string ParolaProposta(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "";
        var l = nome.ToLowerInvariant();
        var chiave = CostruttoreNomeCertificatoEsterno.Acronimi.Keys.FirstOrDefault(k => l.Contains(k));
        return chiave ?? l.Trim();
    }
}
