using System.Globalization;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Presenze;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Presenze;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

public sealed record ModelloTimbra(Lavoratore? Lavoratore, string Prossimo, CalcoloGiorno? Oggi);

public sealed record ModelloMese(Lavoratore Lavoratore, int Anno, int Mese, ProspettoLavoratore Prospetto,
    IReadOnlyList<Lavoratore> Elenco, DateOnly Precedente, DateOnly Successivo, bool Proprio);

public sealed record ModelloGiorno(Lavoratore Lavoratore, DateOnly Data, CalcoloGiorno Calcolo, DatiGiorno Dati, bool Proprio);

public sealed record ModelloAssenza(Lavoratore Lavoratore, DateOnly Data, bool Proprio);

public sealed record ModelloRegistro(Lavoratore? Lavoratore, IReadOnlyList<VoceRegistro> Voci, IReadOnlyList<Lavoratore> Elenco);

public sealed record ModelloElencoLavoratori(IReadOnlyList<Lavoratore> Lavoratori);

public sealed record ModelloLavoratore(ModuloLavoratore Modulo, IReadOnlyList<SedeAziendale> Sedi, IReadOnlyList<Utente> Utenti);

/// <summary>
/// Presenze (19/11/2026): timbrature, assenze e prospetto mensile, riscrittura in .NET dell'app di Vaccarini.
/// Ognuno timbra dal proprio terminale con l'accesso Microsoft 365 («presenze.timbra»). Chi ha «presenze.gestisci»
/// vede e corregge anche le presenze degli altri e gestisce i lavoratori. Le correzioni hanno sempre il motivo e finiscono
/// nel registro delle modifiche. Definizione: claude/definizione-presenze.md
/// </summary>
[RichiedePermesso(Permessi.PresenzeTimbra)]
public class PresenzeController : Controller
{
    private readonly ServizioPresenze _servizio;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;

    public PresenzeController(ServizioPresenze servizio, IContestoUtente utente, IServizioAudit audit)
    {
        _servizio = servizio; _utente = utente; _audit = audit;
    }

    private bool PuoGestire => _utente.HaPermesso(Permessi.PresenzeGestisci);

    private async Task<Lavoratore?> MioAsync(CancellationToken ct) =>
        _utente.IdUtente is int id ? await _servizio.LavoratoreDaUtenteAsync(id, ct) : null;

    /// <summary>
    /// Il lavoratore su cui si opera: il proprio, oppure (solo con «presenze.gestisci») quello indicato.
    /// Restituisce null se non si può: in quel caso <paramref name="rifiuto"/> contiene la risposta da dare.
    /// </summary>
    private async Task<(Lavoratore? Lavoratore, bool Proprio, IActionResult? Rifiuto)> BersaglioAsync(int? idLavoratore, CancellationToken ct)
    {
        var mio = await MioAsync(ct);
        if (idLavoratore is null || idLavoratore == mio?.IdLavoratore)
            return mio is null ? (null, true, RedirectToAction(nameof(Index))) : (mio, true, null);
        if (!PuoGestire) return (null, false, StatusCode(403));
        var altro = await _servizio.LavoratoreAsync(idLavoratore.Value, ct);
        return altro is null ? (null, false, NotFound()) : (altro, false, null);
    }

    private static bool TryData(string? testo, out DateOnly data) =>
        DateOnly.TryParseExact(testo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out data);

    private static int? IdPerRotta(Lavoratore l, bool proprio) => proprio ? null : l.IdLavoratore;

    private async Task RegistraAuditAsync(string azione, Lavoratore l, string? dettaglio, CancellationToken ct) =>
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, azione, tipoEntita: "Lavoratore",
            idEntita: l.IdLavoratore.ToString(CultureInfo.InvariantCulture), dettaglio: dettaglio, ct: ct);

    // ═══════════════════════════════════════════════════════════
    // Timbra
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Titolo"] = "Presenze";
        var l = await MioAsync(ct);
        if (l is null) return View(new ModelloTimbra(null, "E", null));
        var oggi = FusoPresenze.GiornoLocale(DateTime.UtcNow);
        var prospetto = await _servizio.CalcolaAsync(l, oggi, oggi, ct);
        var prossimo = await _servizio.ProssimoTipoAsync(l, oggi, ct);
        return View(new ModelloTimbra(l, prossimo, prospetto.Giorni[0]));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Timbra(string? tipo, string? luogo, CancellationToken ct)
    {
        var l = await MioAsync(ct);
        if (l is null) return RedirectToAction(nameof(Index));
        var errore = RegolePresenze.ValidaTipo(tipo);
        if (errore is not null) { TempData["Errore"] = errore; return RedirectToAction(nameof(Index)); }
        var t = await _servizio.TimbraAsync(l, tipo!, luogo, _utente.IdUtente, ct);
        var ora = FusoPresenze.OraLocale(t.DataOraUtc);
        var contata = MotoreCalcoloPresenze.MinutiInHhmm(MotoreCalcoloPresenze.Arrotonda(ora));
        TempData["Esito"] = $"{(tipo == "E" ? "Entrata" : "Uscita")} registrata alle {ora} (conteggiata {contata}).";
        await RegistraAuditAsync("presenze.timbra", l, $"{tipo} {ora}", ct);
        return RedirectToAction(nameof(Index));
    }

    // ═══════════════════════════════════════════════════════════
    // Mese e giorno
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Mese(int? anno, int? mese, int? lavoratore, CancellationToken ct)
    {
        var (l, proprio, rifiuto) = await BersaglioAsync(lavoratore, ct);
        if (rifiuto is not null) return rifiuto;
        var oggi = FusoPresenze.GiornoLocale(DateTime.UtcNow);
        var a = anno ?? oggi.Year;
        var m = mese ?? oggi.Month;
        if (a < 2000 || a > 2100 || m < 1 || m > 12) return BadRequest();
        var dal = new DateOnly(a, m, 1);
        var al = dal.AddMonths(1).AddDays(-1);
        var prospetto = await _servizio.CalcolaAsync(l!, dal, al, ct);
        var elenco = PuoGestire ? await _servizio.ElencoLavoratoriAsync(true, ct) : new List<Lavoratore>();
        ViewData["Titolo"] = proprio ? "Le mie presenze" : $"Presenze di {l!.Cognome}";
        return View(new ModelloMese(l!, a, m, prospetto, elenco, dal.AddDays(-1), al.AddDays(1), proprio));
    }

    public async Task<IActionResult> Giorno(string? data, int? lavoratore, CancellationToken ct)
    {
        if (!TryData(data, out var giorno)) return BadRequest();
        var (l, proprio, rifiuto) = await BersaglioAsync(lavoratore, ct);
        if (rifiuto is not null) return rifiuto;
        var prospetto = await _servizio.CalcolaAsync(l!, giorno, giorno, ct);
        var dati = await _servizio.GiornoAsync(l!, giorno, ct);
        ViewData["Titolo"] = $"Presenze del {giorno:dd/MM/yyyy}";
        return View(new ModelloGiorno(l!, giorno, prospetto.Giorni[0], dati, proprio));
    }

    // ═══════════════════════════════════════════════════════════
    // Correzioni di timbrature
    // ═══════════════════════════════════════════════════════════

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaTimbratura(string? data, int? lavoratore, int? idTimbratura, string? ora, string? tipo,
        string? luogo, string? motivo, CancellationToken ct)
    {
        if (!TryData(data, out var giorno)) return BadRequest();
        var (l, proprio, rifiuto) = await BersaglioAsync(lavoratore, ct);
        if (rifiuto is not null) return rifiuto;
        var errore = idTimbratura is int id
            ? await _servizio.ModificaTimbraturaAsync(l!, id, ora, tipo, luogo, motivo, _utente.IdUtente, ct)
            : await _servizio.AggiungiTimbraturaAsync(l!, giorno, ora, tipo, luogo, motivo, _utente.IdUtente, ct);
        if (errore is null)
        {
            TempData["Esito"] = "Timbratura salvata.";
            await RegistraAuditAsync(idTimbratura is null ? "presenze.timbratura.aggiungi" : "presenze.timbratura.modifica", l!, data, ct);
        }
        else TempData["Errore"] = errore;
        return RedirectToAction(nameof(Giorno), new { data = giorno.ToString("yyyy-MM-dd"), lavoratore = IdPerRotta(l!, proprio) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AnnullaTimbratura(string? data, int? lavoratore, int idTimbratura, string? motivo, CancellationToken ct)
    {
        if (!TryData(data, out var giorno)) return BadRequest();
        var (l, proprio, rifiuto) = await BersaglioAsync(lavoratore, ct);
        if (rifiuto is not null) return rifiuto;
        var (errore, _) = await _servizio.AnnullaTimbraturaAsync(l!, idTimbratura, motivo, _utente.IdUtente, ct);
        if (errore is null)
        {
            TempData["Esito"] = "Timbratura annullata.";
            await RegistraAuditAsync("presenze.timbratura.annulla", l!, data, ct);
        }
        else TempData["Errore"] = errore;
        return RedirectToAction(nameof(Giorno), new { data = giorno.ToString("yyyy-MM-dd"), lavoratore = IdPerRotta(l!, proprio) });
    }

    // ═══════════════════════════════════════════════════════════
    // Assenze
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Assenza(string? data, int? lavoratore, CancellationToken ct)
    {
        var (l, proprio, rifiuto) = await BersaglioAsync(lavoratore, ct);
        if (rifiuto is not null) return rifiuto;
        var giorno = TryData(data, out var d) ? d : FusoPresenze.GiornoLocale(DateTime.UtcNow);
        ViewData["Titolo"] = proprio ? "Nuova assenza" : $"Nuova assenza di {l!.Cognome}";
        return View(new ModelloAssenza(l!, giorno, proprio));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NuovaAssenza(int? lavoratore, string? dal, string? al, string? causale, string? durata,
        string? ore, string? note, CancellationToken ct)
    {
        var (l, proprio, rifiuto) = await BersaglioAsync(lavoratore, ct);
        if (rifiuto is not null) return rifiuto;
        var rotta = new { lavoratore = IdPerRotta(l!, proprio), data = dal };
        if (!TryData(dal, out var primo) || !TryData(string.IsNullOrWhiteSpace(al) ? dal : al, out var ultimo))
        {
            TempData["Errore"] = "Date non valide.";
            return RedirectToAction(nameof(Assenza), rotta);
        }
        var (errore, giorni, codice) = await _servizio.AggiungiAssenzeAsync(l!, causale, note, primo, ultimo,
            durata == "intera", ore, _utente.IdUtente, ct);
        if (errore is not null)
        {
            TempData["Errore"] = errore;
            return RedirectToAction(nameof(Assenza), rotta);
        }
        TempData["Esito"] = $"Assenza inserita ({CausaliPresenze.Descrizione(codice!)}) per {giorni.Count} giorn{(giorni.Count == 1 ? "o" : "i")}.";
        await RegistraAuditAsync("presenze.assenza.aggiungi", l!, $"{codice} {giorni[0]:yyyy-MM-dd}..{giorni[^1]:yyyy-MM-dd}", ct);
        if (giorni.Count == 1)
            return RedirectToAction(nameof(Giorno), new { data = giorni[0].ToString("yyyy-MM-dd"), lavoratore = IdPerRotta(l!, proprio) });
        return RedirectToAction(nameof(Mese), new { anno = giorni[0].Year, mese = giorni[0].Month, lavoratore = IdPerRotta(l!, proprio) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AnnullaAssenza(int? lavoratore, int idAssenza, string? motivo, CancellationToken ct)
    {
        var (l, proprio, rifiuto) = await BersaglioAsync(lavoratore, ct);
        if (rifiuto is not null) return rifiuto;
        var (errore, giorno) = await _servizio.AnnullaAssenzaAsync(l!, idAssenza, motivo, _utente.IdUtente, ct);
        if (errore is not null)
        {
            TempData["Errore"] = errore;
            return RedirectToAction(nameof(Mese), new { lavoratore = IdPerRotta(l!, proprio) });
        }
        TempData["Esito"] = "Assenza annullata.";
        await RegistraAuditAsync("presenze.assenza.annulla", l!, giorno?.ToString("yyyy-MM-dd"), ct);
        return RedirectToAction(nameof(Giorno), new { data = giorno!.Value.ToString("yyyy-MM-dd"), lavoratore = IdPerRotta(l!, proprio) });
    }

    // ═══════════════════════════════════════════════════════════
    // Registro
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Registro(int? lavoratore, CancellationToken ct)
    {
        ViewData["Titolo"] = "Registro delle modifiche";
        // Chi non gestisce vede solo il proprio registro; chi gestisce vede tutto o filtra per lavoratore.
        if (PuoGestire)
        {
            var elenco = await _servizio.ElencoLavoratoriAsync(false, ct);
            var scelto = lavoratore is int id ? elenco.FirstOrDefault(x => x.IdLavoratore == id) : null;
            var voci = await _servizio.RegistroAsync(lavoratore, null, 300, ct);
            return View(new ModelloRegistro(scelto, voci, elenco));
        }
        var mio = await MioAsync(ct);
        if (mio is null) return RedirectToAction(nameof(Index));
        return View(new ModelloRegistro(mio, await _servizio.RegistroAsync(mio.IdLavoratore, null, 300, ct), new List<Lavoratore>()));
    }

    // ═══════════════════════════════════════════════════════════
    // Lavoratori (solo chi gestisce)
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Lavoratori(CancellationToken ct)
    {
        if (!PuoGestire) return StatusCode(403);
        ViewData["Titolo"] = "Lavoratori delle presenze";
        return View(new ModelloElencoLavoratori(await _servizio.ElencoLavoratoriAsync(false, ct)));
    }

    public async Task<IActionResult> ModificaLavoratore(int? id, CancellationToken ct)
    {
        if (!PuoGestire) return StatusCode(403);
        var modulo = new ModuloLavoratore
        {
            OreLunedi = "8", OreMartedi = "8", OreMercoledi = "8", OreGiovedi = "8", OreVenerdi = "8", OreSabato = "0", OreDomenica = "0",
            Colore = "#FFC000", Attivo = true,
        };
        if (id is int idl)
        {
            var l = await _servizio.LavoratoreAsync(idl, ct);
            if (l is null) return NotFound();
            modulo = new ModuloLavoratore
            {
                IdLavoratore = l.IdLavoratore, IdUtente = l.IdUtente, IdSede = l.IdSede, Cognome = l.Cognome, Colore = l.Colore, Ordine = l.Ordine,
                AttivoDal = l.AttivoDal?.ToString("yyyy-MM-dd"), Attivo = l.Attivo,
                OreLunedi = Ore(l.OreLunedi), OreMartedi = Ore(l.OreMartedi), OreMercoledi = Ore(l.OreMercoledi), OreGiovedi = Ore(l.OreGiovedi),
                OreVenerdi = Ore(l.OreVenerdi), OreSabato = Ore(l.OreSabato), OreDomenica = Ore(l.OreDomenica),
            };
        }
        return await VistaLavoratoreAsync(modulo, ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaLavoratore(ModuloLavoratore modulo, CancellationToken ct)
    {
        if (!PuoGestire) return StatusCode(403);
        var errore = await _servizio.SalvaLavoratoreAsync(modulo, _utente.IdUtente, ct);
        if (errore is null)
        {
            TempData["Esito"] = "Lavoratore salvato.";
            return RedirectToAction(nameof(Lavoratori));
        }
        TempData["Errore"] = errore;
        return await VistaLavoratoreAsync(modulo, ct);
    }

    private async Task<IActionResult> VistaLavoratoreAsync(ModuloLavoratore modulo, CancellationToken ct)
    {
        ViewData["Titolo"] = modulo.IdLavoratore is null ? "Nuovo lavoratore" : "Modifica lavoratore";
        var sedi = await _servizio.SediAsync(ct);
        var utenti = await _servizio.UtentiLiberiAsync(modulo.IdLavoratore, ct);
        return View("ModificaLavoratore", new ModelloLavoratore(modulo, sedi, utenti));
    }

    private static string Ore(decimal ore) => ore.ToString("0.##", CultureInfo.InvariantCulture);
}
