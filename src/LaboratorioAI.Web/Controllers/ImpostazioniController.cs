using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Amministrazione → Impostazioni documenti. Punto unico per le configurazioni condivise tra i
/// moduli (revisione Condizioni Generali usata da offerte e Ordini di Accettazione, riferimento
/// modulo OA, indirizzi per le notifiche di errore, cartella di archiviazione). Pensata per essere
/// estesa con nuove voci di configurazione.
/// </summary>
[RichiedePermesso(Permessi.AmministrazioneConfigurazioni)]
public class ImpostazioniController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;

    public ImpostazioniController(ContestoLaboratorioAI ctx, IContestoUtente utente, IServizioAudit audit)
    {
        _ctx = ctx; _utente = utente; _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Impostazioni documenti";
        return View();
    }

    /// <summary>Definizione delle voci di configurazione modificabili da questa maschera.</summary>
    private static readonly (string Chiave, string Etichetta, string Categoria, string Descrizione, string Default)[] Voci =
    {
        ("Preventivi.RevisioneCondizioni",
            "Revisione Condizioni Generali di Fornitura (condivisa offerte + Ordini di Accettazione)",
            "Documenti", "Revisione corrente delle Condizioni Generali di Fornitura", "Mod. Com. 05 Rev. 19"),
        ("OrdineAccettazione.ModuloRevisione",
            "Riferimento modulo Ordine di Accettazione",
            "Documenti", "Codice/rev. del modulo di Sistema Qualità dell'Ordine di Accettazione", "Mod. Acc. 01 Rev. 0 del 16/09/2026"),
        ("OrdineAccettazione.EmailErrori",
            "Indirizzi per le mail di errore (Ordini di Accettazione) — separati da ;",
            "Documenti", "Destinatari delle notifiche di errore dell'invio Ordini di Accettazione",
            "mmaffei@mare-a.com; dbertozzi@mare-a.com; refertazione@mare-a.com"),
        ("OrdineAccettazione.CartellaOutput",
            "Cartella di archiviazione dei PDF Ordine di Accettazione (vuota = predefinita)",
            "Documenti", "Percorso base di archiviazione dei PDF (per l'invio al portale)", ""),
    };

    [HttpGet("api/impostazioni/documenti")]
    public async Task<IActionResult> Leggi(CancellationToken ct)
    {
        var presenti = await _ctx.Configurazioni.AsNoTracking()
            .Where(c => Voci.Select(v => v.Chiave).Contains(c.Chiave))
            .ToDictionaryAsync(c => c.Chiave, c => c.Valore, ct);

        var voci = Voci.Select(v => new
        {
            v.Chiave,
            v.Etichetta,
            Valore = presenti.TryGetValue(v.Chiave, out var val) && !string.IsNullOrWhiteSpace(val) ? val : v.Default
        });
        return Json(voci);
    }

    [HttpPost("api/impostazioni/documenti")]
    public async Task<IActionResult> Salva([FromBody] Dictionary<string, string?> valori, CancellationToken ct)
    {
        var ora = DateTime.UtcNow;
        foreach (var v in Voci)
        {
            if (valori is null || !valori.TryGetValue(v.Chiave, out var nuovo)) continue;
            var val = (nuovo ?? "").Trim();

            var cfg = await _ctx.Configurazioni.FirstOrDefaultAsync(x => x.Chiave == v.Chiave, ct);
            if (cfg is null)
                _ctx.Configurazioni.Add(new Configurazione
                {
                    Chiave = v.Chiave, Valore = val, Descrizione = v.Descrizione, Categoria = v.Categoria,
                    DataModifica = ora, IdUtenteModifica = _utente.IdUtente
                });
            else { cfg.Valore = val; cfg.DataModifica = ora; cfg.IdUtenteModifica = _utente.IdUtente; }
        }
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "impostazioni.documenti.salvate",
            EsitoOperazione.Successo, dettaglio: new { Chiavi = valori?.Keys }, ct: ct);
        return Json(new { ok = true });
    }
}
