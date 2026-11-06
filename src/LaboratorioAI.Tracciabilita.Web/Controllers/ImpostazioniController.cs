using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Come l'azienda vuole lavorare: per ora i kg consumati in produzione.</summary>
[Authorize]
[Route("Impostazioni")]
public class ImpostazioniController : Controller
{
    private readonly ContestoTracciabilita _db;
    public ImpostazioniController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var imp = await _db.ImpostazioniAziende.AsNoTracking().FirstOrDefaultAsync(ct);
        return View(new ModelloImpostazioni
        {
            ModoConsumi = imp?.ModoQuantitaConsumi ?? ModoQuantitaConsumi.SoloLotti,
            ConsumiObbligatori = imp?.QuantitaConsumiObbligatorie ?? false
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [Servizi.RichiedeConfigurazione]
    public async Task<IActionResult> Salva(ModelloImpostazioni modello, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        if (!Enum.IsDefined(modello.ModoConsumi))
        {
            TempData["Errore"] = "Scegli una modalità valida.";
            return RedirectToAction(nameof(Index));
        }
        var imp = await _db.ImpostazioniAziende.FirstOrDefaultAsync(ct);
        if (imp is null)
        {
            imp = new ImpostazioniAzienda();
            _db.ImpostazioniAziende.Add(imp);
        }
        var prima = $"{imp.ModoQuantitaConsumi}, obbligatorie={imp.QuantitaConsumiObbligatorie}";
        imp.ModoQuantitaConsumi = modello.ModoConsumi;
        // Con «solo lotti» non ci sono quantità da rendere obbligatorie.
        imp.QuantitaConsumiObbligatorie = modello.ModoConsumi != ModoQuantitaConsumi.SoloLotti && modello.ConsumiObbligatori;
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = _db.IdUtenteCorrente, Azione = "ImpostazioniModificate", Entita = "ImpostazioniAzienda",
            IdEntita = imp.IdImpostazioni.ToString(), Prima = prima,
            Dopo = $"{imp.ModoQuantitaConsumi}, obbligatorie={imp.QuantitaConsumiObbligatorie}",
            IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = "Impostazioni salvate.";
        return RedirectToAction(nameof(Index));
    }
}
