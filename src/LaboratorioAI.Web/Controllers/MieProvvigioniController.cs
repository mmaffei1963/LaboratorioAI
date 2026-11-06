using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Provvigioni;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// «Le mie provvigioni»: il commerciale vede solo il proprio lavoro. Il commerciale è quello collegato
/// all'utente connesso (<c>provv.Commerciali.IdUtente</c>): nessun parametro in ingresso può cambiarlo,
/// quindi un commerciale non può vedere i dati di un altro.
/// </summary>
[RichiedePermesso(Permessi.ProvvigioniProprie)]
public class MieProvvigioniController : Controller
{
    private readonly ContestoLaboratorioAI _db;
    private readonly ServizioProvvigioniLab _servizio;
    private readonly IContestoUtente _utente;

    public MieProvvigioniController(ContestoLaboratorioAI db, ServizioProvvigioniLab servizio, IContestoUtente utente)
    {
        _db = db;
        _servizio = servizio;
        _utente = utente;
    }

    /// <summary>Aggiornamento da AZW richiesto dal commerciale (finché non è attiva l'importazione notturna): al massimo ogni 15 minuti.</summary>
    [HttpPost]
    public async Task<IActionResult> Aggiorna(int? anno, [FromServices] ImportatoreAzw importatore, [FromServices] IServizioAudit audit, CancellationToken ct)
    {
        var (ok, msg) = await importatore.AggiornaSeVecchiaAsync(TimeSpan.FromMinutes(15), ct);
        if (ok && msg.StartsWith("Dati aggiornati", StringComparison.Ordinal)) await _servizio.SincronizzaConvenzioniAsync(ct);
        await audit.RegistraAsync(Core.Enumerazioni.AreaFunzionale.Provvigioni, "provvigioni.proprie.aggiorna-azw",
            ok ? Core.Enumerazioni.EsitoOperazione.Successo : Core.Enumerazioni.EsitoOperazione.Errore, dettaglio: new { msg }, ct: ct);
        TempData[ok ? "Messaggio" : "Errore"] = msg;
        return RedirectToAction(nameof(Index), new { anno });
    }

    public async Task<IActionResult> Index(int? anno, CancellationToken ct)
    {
        ViewData["Titolo"] = "Le mie provvigioni";
        var annoScelto = anno ?? DateTime.Today.Year;

        var idUtente = _utente.IdUtente;
        var commerciale = idUtente is null ? null
            : await _db.Commerciali.AsNoTracking().FirstOrDefaultAsync(c => c.IdUtente == idUtente && c.Attivo, ct);
        if (commerciale is null)
            return View(new ModelloMieProvvigioni(null, annoScelto, null, false, new(), null, new(), new()));

        var ultimo = await _db.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita)
            .OrderByDescending(i => i.DataOra).FirstOrDefaultAsync(ct);
        var attivo = await _servizio.CalcoloAttivoAsync(commerciale.IdCommerciale, ct);
        if (!attivo || ultimo is null)
            return View(new ModelloMieProvvigioni(commerciale, annoScelto, ultimo, attivo, new(), null, new(), new()));

        var risultato = await _servizio.CalcolaAsync(commerciale.IdCommerciale, ct: ct);
        var trimestri = (await _servizio.RigheTrimestriAsync(commerciale.IdCommerciale, risultato, ct))
            .Where(t => t.Anno == annoScelto).ToList();

        var movAnno = risultato.Movimenti.Where(m => m.Data.Year == annoScelto).ToList();
        var codici = movAnno.Select(m => m.Cliente).Concat(risultato.Obiettivi.Select(o => o.Cliente)).Distinct().ToList();
        var nomi = await _db.AzwFatture.AsNoTracking().Where(f => codici.Contains(f.CodiceCliente))
            .GroupBy(f => f.CodiceCliente).Select(g => new { Codice = g.Key, Nome = g.Max(x => x.RagioneSociale) })
            .ToDictionaryAsync(x => x.Codice, x => x.Nome ?? "", ct);

        var clienti = movAnno.GroupBy(m => m.Cliente)
            .Select(g => new RigaClienteMia(
                g.Key, nomi.GetValueOrDefault(g.Key, ""), g.OrderBy(m => m.Data).Last().Voce,
                g.Sum(m => m.Base), g.Sum(m => m.Importo)))
            .OrderByDescending(c => c.Provvigione).ToList();

        // Elenco in sviluppo approvato, con l'avanzamento calcolato dal motore.
        var voci = await _db.ElencoSviluppo.AsNoTracking()
            .Where(v => v.IdCommerciale == commerciale.IdCommerciale && v.Anno == annoScelto && v.Attivo
                        && v.ApprovatoDa != null && v.ApprovatoDa != "").ToListAsync(ct);
        var obiettivi = voci.Select(v =>
        {
            var av = risultato.Obiettivi.FirstOrDefault(o => o.Cliente == v.CodiceCliente && o.Anno == v.Anno);
            return new RigaObiettivoMia(v.CodiceCliente, nomi.GetValueOrDefault(v.CodiceCliente, ""), v.AzionePrevista,
                v.Obiettivo, av?.Cumulato ?? 0m, av?.Raggiunto ?? false, av?.Provvigione ?? 0m);
        }).OrderBy(o => o.Nome).ToList();

        var premio = risultato.Premi.FirstOrDefault(p => p.Anno == annoScelto);
        return View(new ModelloMieProvvigioni(commerciale, annoScelto, ultimo, true, trimestri, premio, clienti, obiettivi));
    }
}

public sealed record RigaClienteMia(string Codice, string Nome, VoceProvvigione Voce, decimal Base, decimal Provvigione);

public sealed record RigaObiettivoMia(string Codice, string Nome, string? Azione, decimal Obiettivo, decimal Cumulato, bool Raggiunto, decimal Provvigione);

public sealed record ModelloMieProvvigioni(
    Commerciale? Commerciale,
    int Anno,
    ImportazioneAzw? UltimaImportazione,
    bool CalcoloAttivo,
    List<RigaTrimestre> Trimestri,
    RisultatoPremio? Premio,
    List<RigaClienteMia> Clienti,
    List<RigaObiettivoMia> Obiettivi);
