using System.Globalization;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Presenze;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Presenze;
using LaboratorioAI.Web.Autorizzazione;
using LaboratorioAI.Web.Esportazioni;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

public sealed record ModelloTimbra(Lavoratore? Lavoratore, string Prossimo, CalcoloGiorno? Oggi);

public sealed record ModelloMese(Lavoratore Lavoratore, int Anno, int Mese, ProspettoLavoratore Prospetto,
    IReadOnlyList<Lavoratore> Elenco, DateOnly Precedente, DateOnly Successivo, bool Proprio);

public sealed record ModelloGiorno(Lavoratore Lavoratore, DateOnly Data, CalcoloGiorno Calcolo, DatiGiorno Dati, bool Proprio);

public sealed record ModelloAssenza(Lavoratore Lavoratore, DateOnly Data, bool Proprio);

public sealed record ModelloRegistro(Lavoratore? Lavoratore, IReadOnlyList<VoceRegistro> Voci, IReadOnlyList<Lavoratore> Elenco);

public sealed record ModelloElencoLavoratori(IReadOnlyList<Lavoratore> Lavoratori);

public sealed record ModelloProspetto(int Anno, int Mese, IReadOnlyList<RigaProspetto> Righe, IReadOnlyList<SedeAziendale> Sedi,
    int? IdSede, int? IdMio, DateOnly Precedente, DateOnly Successivo, bool PuoGestire);

public sealed record ModelloFerie(int Anno, DatiFerie Dati, IReadOnlyList<SedeAziendale> Sedi, int? IdSede, int? IdMio);

public sealed record ModelloRiepilogo(int Anno, int Mese, IReadOnlyList<RigaProspetto> Righe, IReadOnlyList<PresenzaOggi> Presenti,
    DateOnly Precedente, DateOnly Successivo);

public sealed record ModelloCalendario(int Anno, IReadOnlyList<RigaCalendario> Righe, IReadOnlyList<SedeAziendale> Sedi, bool PuoModificare);

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

    // ═══════════════════════════════════════════════════════════
    // Prospetto mensile (come il vecchio CALENDARIO): tutti i lavoratori, un mese. Si apre in modifica solo la propria riga.
    // ═══════════════════════════════════════════════════════════

    private static bool MeseValido(int anno, int mese) => anno is >= 2000 and <= 2100 && mese is >= 1 and <= 12;

    public async Task<IActionResult> Prospetto(int? anno, int? mese, int? sede, CancellationToken ct)
    {
        var oggi = FusoPresenze.GiornoLocale(DateTime.UtcNow);
        var a = anno ?? oggi.Year;
        var m = mese ?? oggi.Month;
        if (!MeseValido(a, m)) return BadRequest();
        var righe = await _servizio.ProspettoMensileAsync(a, m, sede, ct);
        var sedi = await _servizio.SediAsync(ct);
        var mio = await MioAsync(ct);
        var dal = new DateOnly(a, m, 1);
        ViewData["Titolo"] = "Prospetto mensile";
        return View(new ModelloProspetto(a, m, righe, sedi, sede, mio?.IdLavoratore, dal.AddDays(-1), dal.AddMonths(1), PuoGestire));
    }

    public async Task<IActionResult> EsportaProspetto(int anno, int mese, int? sede, CancellationToken ct)
    {
        // Il file contiene i commenti delle celle (motivi delle assenze, luoghi, anomalie): lo esporta solo chi gestisce.
        if (!PuoGestire) return StatusCode(403);
        if (!MeseValido(anno, mese)) return BadRequest();
        var righe = await _servizio.ProspettoMensileAsync(anno, mese, sede, ct);
        var nomeSede = sede is int s ? (await _servizio.SediAsync(ct)).FirstOrDefault(x => x.IdSede == s)?.Nome : null;
        var contenuto = EsportaProspettoExcel.Crea(anno, mese, nomeSede, righe.Select(r => r.Prospetto).ToList());
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "presenze.esporta", tipoEntita: "Presenze",
            idEntita: $"{anno}-{mese:00}", dettaglio: nomeSede, ct: ct);
        var nomeFile = $"Presenze_{anno}-{mese:00}{(nomeSede is null ? "" : "_" + nomeSede.Replace(' ', '_'))}.xlsx";
        return File(contenuto, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeFile);
    }

    // ═══════════════════════════════════════════════════════════
    // Riepilogo del mese e chi è in sede (solo chi gestisce)
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Riepilogo(int? anno, int? mese, CancellationToken ct)
    {
        if (!PuoGestire) return StatusCode(403);
        var oggi = FusoPresenze.GiornoLocale(DateTime.UtcNow);
        var a = anno ?? oggi.Year;
        var m = mese ?? oggi.Month;
        if (!MeseValido(a, m)) return BadRequest();
        var righe = await _servizio.ProspettoMensileAsync(a, m, null, ct);
        var presenti = await _servizio.PresentiOggiAsync(ct);
        var dal = new DateOnly(a, m, 1);
        ViewData["Titolo"] = "Riepilogo presenze";
        return View(new ModelloRiepilogo(a, m, righe, presenti, dal.AddDays(-1), dal.AddMonths(1)));
    }

    // ═══════════════════════════════════════════════════════════
    // Calendario aziendale: lo vedono tutti, lo modifica solo chi gestisce (con il motivo e il registro)
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Calendario(int? anno, CancellationToken ct)
    {
        var a = anno ?? FusoPresenze.GiornoLocale(DateTime.UtcNow).Year;
        if (a is < 2000 or > 2100) return BadRequest();
        ViewData["Titolo"] = "Calendario aziendale";
        return View(new ModelloCalendario(a, await _servizio.CalendarioAnnoAsync(a, ct), await _servizio.SediAsync(ct), PuoGestire));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AggiungiCalendario(int anno, string? tipo, int? sede, string? dal, string? al, string? descrizione,
        string? ore, string? motivo, CancellationToken ct)
    {
        if (!PuoGestire) return StatusCode(403);
        if (!TryData(dal, out var primo) || !TryData(string.IsNullOrWhiteSpace(al) ? dal : al, out var ultimo))
        {
            TempData["Errore"] = "Date non valide.";
            return RedirectToAction(nameof(Calendario), new { anno });
        }
        var (errore, giorni) = await _servizio.AggiungiCalendarioAsync(tipo, sede, primo, ultimo, descrizione, ore, motivo, _utente.IdUtente, ct);
        if (errore is null)
        {
            TempData["Esito"] = $"Calendario aggiornato: {giorni} giorn{(giorni == 1 ? "o" : "i")}.";
            await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "presenze.calendario.aggiungi", tipoEntita: "CalendarioPresenze",
                dettaglio: $"{tipo} {primo:yyyy-MM-dd}..{ultimo:yyyy-MM-dd}", ct: ct);
        }
        else TempData["Errore"] = errore;
        return RedirectToAction(nameof(Calendario), new { anno = errore is null ? primo.Year : anno });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminaCalendario(int idGiornoCalendario, int anno, string? motivo, CancellationToken ct)
    {
        if (!PuoGestire) return StatusCode(403);
        var (errore, annoVoce) = await _servizio.EliminaCalendarioAsync(idGiornoCalendario, motivo, _utente.IdUtente, ct);
        if (errore is null)
        {
            TempData["Esito"] = "Voce tolta dal calendario.";
            await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "presenze.calendario.elimina", tipoEntita: "CalendarioPresenze",
                idEntita: idGiornoCalendario.ToString(CultureInfo.InvariantCulture), ct: ct);
        }
        else TempData["Errore"] = errore;
        return RedirectToAction(nameof(Calendario), new { anno = annoVoce ?? anno });
    }

    // ═══════════════════════════════════════════════════════════
    // Calendario ferie: ferie e permessi di tutti, per vedere chi è assente nello stesso periodo (visibile a tutti)
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Ferie(int? anno, int? sede, CancellationToken ct)
    {
        var a = anno ?? FusoPresenze.GiornoLocale(DateTime.UtcNow).Year;
        if (a is < 2000 or > 2100) return BadRequest();
        var dati = await _servizio.FerieAnnoAsync(a, sede, ct);
        var sedi = await _servizio.SediAsync(ct);
        var mio = await MioAsync(ct);
        ViewData["Titolo"] = "Calendario ferie";
        return View(new ModelloFerie(a, dati, sedi, sede, mio?.IdLavoratore));
    }
}
