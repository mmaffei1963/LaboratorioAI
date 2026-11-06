using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Portale.Controllers;

/// <summary>
/// Pagina di approvazione delle Condizioni Generali raggiunta con il link personale inviato al Firmatario
/// (Specifica v0.4, fase 2 — 10/10/2026). Nessun accesso con credenziali: il link è un codice casuale di 256 bit,
/// di cui il database conserva solo l'impronta. Dal 15/10/2026 l'approvazione si firma con un codice OTP inviato per e-mail
/// al Firmatario (Cass. ord. 20945/2026: la sola spunta non basta per le clausole vessatorie); la firma grafica è stata tolta.
/// Ogni passo lascia una riga nel registro delle evidenze; la Ricevuta PDF la genera e la invia LaboratorioAI Web.
/// </summary>
[Route("Firma")]
public class FirmaController : Controller
{
    /// <summary>Giorni per cui il link resta utilizzabile dopo l'approvazione, per scaricare la Ricevuta.</summary>
    private const int GiorniLinkDopoApprovazione = 90;

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRegistroEvidenzeApprovazioni _registro;
    private readonly ILogger<FirmaController> _log;
    private readonly IServizioEmail _email;
    private readonly IConfiguration _config;

    public FirmaController(ContestoLaboratorioAI ctx, IRegistroEvidenzeApprovazioni registro, ILogger<FirmaController> log,
        IServizioEmail email, IConfiguration config)
    {
        _ctx = ctx; _registro = registro; _log = log; _email = email; _config = config;
    }

    // ── Utilità ────────────────────────────────────────────────────

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? Agente => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    /// <summary>Trova la richiesta dal codice del link (solo se il link è valido e non scaduto).</summary>
    private async Task<RichiestaApprovazione?> TrovaAsync(string codice, CancellationToken ct, bool traccia = false)
    {
        if (string.IsNullOrWhiteSpace(codice) || codice.Length < 30 || codice.Length > 100) return null;
        var impronta = CatenaEvidenze.Sha256(codice.Trim());
        var q = traccia ? _ctx.RichiesteApprovazione.AsQueryable() : _ctx.RichiesteApprovazione.AsNoTracking();
        var r = await q.Include(x => x.Revisione).ThenInclude(v => v!.Clausole).Include(x => x.Firmatario)
            .FirstOrDefaultAsync(x => x.ImprontaCodiceLink == impronta, ct);
        if (r is null || r.ScadenzaLink is null || r.ScadenzaLink < DateTime.UtcNow) return null;
        if (r.Stato is StatoRichiestaApprovazione.Annullata or StatoRichiestaApprovazione.Rifiutata or StatoRichiestaApprovazione.DaInviare) return null;
        return r;
    }

    private Task<EventoApprovazione> RegistraAsync(RichiestaApprovazione r, string tipo, object? dati, CancellationToken ct) =>
        _registro.AggiungiAsync(tipo, r.IdRichiestaApprovazione, r.IdClienteArcLab, dati,
            r.Firmatario is null ? null : $"{r.Firmatario.Nome} {r.Firmatario.Cognome} (Firmatario)", Ip, Agente, ct);

    private static List<ClausolaCondizioni> Clausole(RichiestaApprovazione r) =>
        r.Revisione!.Clausole.Where(c => c.Attivo).OrderBy(c => c.Ordine).ToList();

    private IActionResult LinkNonValido() => View("NonValido");

    // ── Pagina ─────────────────────────────────────────────────────

    [HttpGet("{codice}")]
    public async Task<IActionResult> Index(string codice, CancellationToken ct)
    {
        var r = await TrovaAsync(codice, ct, traccia: true);
        if (r is null) return LinkNonValido();

        if (r.Stato == StatoRichiestaApprovazione.Inviata)
        {
            r.Stato = StatoRichiestaApprovazione.Aperta;
            r.DataApertura = DateTime.UtcNow;
            r.DataUltimaModifica = r.DataApertura.Value;
        }
        if (r.Stato != StatoRichiestaApprovazione.Approvata)
            await RegistraAsync(r, "pagina.aperta", new { Stato = r.Stato.ToString() }, ct);

        var revisione = r.Revisione!;
        var f = r.Firmatario;
        var modello = new ModelloPaginaFirma
        {
            Codice = codice.Trim(),
            Approvata = r.Stato == StatoRichiestaApprovazione.Approvata,
            RicevutaPronta = r.RicevutaPdf is not null,
            NumeroRicevuta = r.NumeroRicevuta,
            DataApprovazione = r.DataPerfezionamento?.ToLocalTime(),
            Titolo = TestiApprovazione.Titolo(revisione.Codice, r.OffertaRiferimento),
            Revisione = revisione.Codice,
            CosaCambia = revisione.TestoCosaCambia,
            ConOfferta = r.OffertaPdf is not null,
            Offerta = r.OffertaRiferimento,
            Casella1 = TestiApprovazione.Casella1(revisione.Codice, r.OffertaPdf is null ? null : r.OffertaRiferimento),
            Casella2Introduzione = TestiApprovazione.Casella2Introduzione(revisione.Codice),
            Clausole = Clausole(r).Select(c => new ModelloClausola(c.Articolo, c.ContenutoBreve, c.TestoIntegrale)).ToList(),
            RagioneSociale = r.ClienteRagioneSociale,
            PartitaIva = r.ClientePartitaIva,
            Nome = f?.Nome, Cognome = f?.Cognome, Qualifica = f?.Qualifica, Email = f?.Email
        };
        return View("Index", modello);
    }

    [HttpGet("{codice}/condizioni")]
    public async Task<IActionResult> Condizioni(string codice, CancellationToken ct)
    {
        var r = await TrovaAsync(codice, ct);
        if (r is null) return LinkNonValido();
        var pdf = await _ctx.RevisioniCondizioni.AsNoTracking().Where(x => x.IdRevisioneCondizioni == r.IdRevisioneCondizioni)
            .Select(x => x.ContenutoPdf).FirstOrDefaultAsync(ct);
        if (pdf is null) return NotFound();
        if (r.Stato != StatoRichiestaApprovazione.Approvata)
            await RegistraAsync(r, "documento.aperto", new { Documento = "Condizioni", r.Revisione!.Codice, Impronta = r.Revisione.ImprontaPdf }, ct);
        Response.Headers.ContentDisposition = "inline; filename=\"Condizioni_Generali_MAREA.pdf\"";
        return File(pdf, "application/pdf");
    }

    [HttpGet("{codice}/offerta")]
    public async Task<IActionResult> Offerta(string codice, CancellationToken ct)
    {
        var r = await TrovaAsync(codice, ct);
        if (r is null) return LinkNonValido();
        var pdf = await _ctx.RichiesteApprovazione.AsNoTracking().Where(x => x.IdRichiestaApprovazione == r.IdRichiestaApprovazione)
            .Select(x => x.OffertaPdf).FirstOrDefaultAsync(ct);
        if (pdf is null) return NotFound();
        if (r.Stato != StatoRichiestaApprovazione.Approvata)
            await RegistraAsync(r, "documento.aperto", new { Documento = "Offerta", Riferimento = r.OffertaRiferimento, Impronta = r.OffertaImpronta }, ct);
        Response.Headers.ContentDisposition = "inline; filename=\"Offerta_MAREA.pdf\"";
        return File(pdf, "application/pdf");
    }

    [HttpGet("{codice}/ricevuta")]
    public async Task<IActionResult> Ricevuta(string codice, CancellationToken ct)
    {
        var r = await TrovaAsync(codice, ct);
        if (r is null) return LinkNonValido();
        var pdf = await _ctx.RichiesteApprovazione.AsNoTracking().Where(x => x.IdRichiestaApprovazione == r.IdRichiestaApprovazione)
            .Select(x => x.RicevutaPdf).FirstOrDefaultAsync(ct);
        if (pdf is null) return Content("La Ricevuta è in preparazione: riprova tra qualche minuto. Ti arriverà anche per e-mail.");
        return File(pdf, "application/pdf", $"Ricevuta_{r.NumeroRicevuta}.pdf");
    }

    // ── Passi della pagina (registrati uno per uno) ────────────────

    private static readonly HashSet<string> PassiAmmessi = new(StringComparer.Ordinal)
    {
        "casella1.spuntata", "casella1.tolta", "casella2.spuntata", "casella2.tolta", "casella3.spuntata", "casella3.tolta"
    };

    [HttpPost("{codice}/passo")]
    public async Task<IActionResult> Passo(string codice, [FromBody] RichiestaPasso req, CancellationToken ct)
    {
        if (req?.Tipo is null || !PassiAmmessi.Contains(req.Tipo)) return BadRequest(new { messaggio = "Passo non riconosciuto." });
        var r = await TrovaAsync(codice, ct);
        if (r is null) return NotFound(new { messaggio = "Il link non è più valido." });
        if (r.Stato == StatoRichiestaApprovazione.Approvata) return BadRequest(new { messaggio = "Approvazione già registrata." });
        var e = await RegistraAsync(r, req.Tipo, null, ct);
        return Json(new { ok = true, ora = e.DataUtc });
    }

    // ── Controlli comuni a invio del codice e conferma ─────────────

    private sealed record EventoBreve(long IdEventoApprovazione, string Tipo, string? Dati, DateTime DataUtc);

    private async Task<List<EventoBreve>> EventiAsync(RichiestaApprovazione r, CancellationToken ct) =>
        (await _ctx.EventiApprovazione.AsNoTracking().Where(x => x.IdRichiestaApprovazione == r.IdRichiestaApprovazione)
            .OrderBy(x => x.IdEventoApprovazione).Select(x => new { x.IdEventoApprovazione, x.Tipo, x.Dati, x.DataUtc }).ToListAsync(ct))
        .Select(x => new EventoBreve(x.IdEventoApprovazione, x.Tipo, x.Dati, x.DataUtc)).ToList();

    /// <summary>Il server verifica sul registro che i passi obbligatori siano stati fatti davvero (non si fida della pagina).</summary>
    private static List<string> PassiMancanti(RichiestaApprovazione r, List<EventoBreve> eventi)
    {
        bool aperto(string doc) => eventi.Any(x => x.Tipo == "documento.aperto" && x.Dati != null && x.Dati.Contains($"\"Documento\":\"{doc}\""));
        bool spuntata(int n) => eventi.LastOrDefault(x => x.Tipo == $"casella{n}.spuntata" || x.Tipo == $"casella{n}.tolta")?.Tipo == $"casella{n}.spuntata";
        var mancanti = new List<string>();
        if (!aperto("Condizioni")) mancanti.Add("aprire le Condizioni Generali");
        if (r.OffertaPdf is not null && !aperto("Offerta")) mancanti.Add("aprire l'Offerta");
        for (var n = 1; n <= 3; n++) if (!spuntata(n)) mancanti.Add($"spuntare la casella {n}");
        return mancanti;
    }

    private sealed record Dichiarazione(string Nome, string Cognome, string Qualifica, string Email, string Casella1, string Casella2,
        List<string> Clausole, string Casella3, string ImprontaTesti);

    /// <summary>Testi che il Firmatario approva con i dati dichiarati: il codice OTP è legato alla loro impronta.</summary>
    private static (Dichiarazione? d, string? errore) Dichiara(RichiestaApprovazione r, string? nomeIn, string? cognomeIn, string? qualificaIn)
    {
        var nome = nomeIn?.Trim(); var cognome = cognomeIn?.Trim(); var qualifica = qualificaIn?.Trim();
        if (string.IsNullOrEmpty(nome) || string.IsNullOrEmpty(cognome) || string.IsNullOrEmpty(qualifica))
            return (null, "Compila nome, cognome e qualifica.");
        if (nome.Length > 100 || cognome.Length > 100 || qualifica.Length > 150) return (null, "Dati troppo lunghi.");
        var revisione = r.Revisione!;
        var casella1 = TestiApprovazione.Casella1(revisione.Codice, r.OffertaPdf is null ? null : r.OffertaRiferimento);
        var casella2 = TestiApprovazione.Casella2Introduzione(revisione.Codice);
        var righe = Clausole(r).Select(TestiApprovazione.RigaClausola).ToList();
        var email = r.Firmatario?.Email ?? "";
        var casella3 = TestiApprovazione.Casella3($"{nome} {cognome}", qualifica, r.ClienteRagioneSociale, r.ClientePartitaIva, email);
        return (new Dichiarazione(nome, cognome, qualifica, email, casella1, casella2, righe, casella3,
            TestiApprovazione.Impronta(casella1, casella2, righe, casella3)), null);
    }

    /// <summary>Dati registrati con l'ultimo codice inviato.</summary>
    private sealed record DatiOtp(string Sale, string Impronta, string ImprontaTesti, string A, DateTime Scadenza, string? IdMessaggio);

    private static (EventoBreve? evento, DatiOtp? dati) UltimoCodice(List<EventoBreve> eventi)
    {
        var e = eventi.LastOrDefault(x => x.Tipo == "otp.inviato");
        if (e?.Dati is null) return (null, null);
        try { return (e, JsonSerializer.Deserialize<DatiOtp>(e.Dati)); } catch (JsonException) { return (e, null); }
    }

    // ── Codice OTP per e-mail (firma elettronica dell'approvazione, 15/10/2026) ──

    [HttpPost("{codice}/codice")]
    public async Task<IActionResult> InviaCodice(string codice, [FromBody] RichiestaCodice req, CancellationToken ct)
    {
        var r = await TrovaAsync(codice, ct);
        if (r is null) return NotFound(new { messaggio = "Il link non è più valido." });
        if (r.Stato == StatoRichiestaApprovazione.Approvata) return BadRequest(new { messaggio = "Approvazione già registrata." });
        var email = r.Firmatario?.Email?.Trim();
        if (string.IsNullOrEmpty(email)) return BadRequest(new { messaggio = "Manca l'indirizzo e-mail del Firmatario: contatta MARE.A." });

        var eventi = await EventiAsync(r, ct);
        var mancanti = PassiMancanti(r, eventi);
        if (mancanti.Count > 0) return BadRequest(new { messaggio = "Prima di chiedere il codice occorre " + string.Join(", ", mancanti) + "." });
        var (d, errore) = Dichiara(r, req?.Nome, req?.Cognome, req?.Qualifica);
        if (d is null) return BadRequest(new { messaggio = errore });

        var adesso = DateTime.UtcNow;
        var inviati = eventi.Where(x => x.Tipo == "otp.inviato").ToList();
        if (inviati.LastOrDefault() is { } ultimo && (adesso - ultimo.DataUtc).TotalSeconds < CodiceOtp.AttesaReinvioSecondi)
            return BadRequest(new { messaggio = $"Il codice è appena stato inviato: attendi un minuto prima di chiederne un altro." });
        if (inviati.Count(x => x.DataUtc > adesso.AddHours(-1)) >= CodiceOtp.InviiMassimiOra)
            return BadRequest(new { messaggio = "Troppi codici richiesti nell'ultima ora: riprova più tardi o contatta MARE.A." });

        // In sviluppo (Condizioni:EmailDiProva) il codice va all'indirizzo di prova, non al cliente.
        var prova = IndirizziEmail.Elenco(_config["Condizioni:EmailDiProva"]);
        var destinatari = prova.Count > 0 ? prova : new List<string> { email };

        var otp = CodiceOtp.Genera();
        var sale = CodiceOtp.Sale();
        string? idMessaggio;
        try
        {
            idMessaggio = await _email.InviaConAllegatiAsync(destinatari, TestiApprovazione.OggettoEmailCodice(r.Revisione!.Codice),
                TestiApprovazione.CorpoEmailCodice(otp, r.Revisione.Codice, r.ClienteRagioneSociale, $"{d.Nome} {d.Cognome}"),
                Array.Empty<AllegatoEmail>(), ct, IndirizziEmail.Elenco(_config["Condizioni:RispondiA"] is { Length: > 0 } ra ? ra : "refertazione@mare-a.com"));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Invio del codice OTP non riuscito: richiesta {Id}", r.IdRichiestaApprovazione);
            await RegistraAsync(r, "otp.invio.fallito", new { A = CodiceOtp.Maschera(email), Errore = ex.GetType().Name }, ct);
            return StatusCode(502, new { messaggio = "Non è stato possibile inviare il codice. Riprova tra qualche minuto o contatta MARE.A." });
        }

        var scadenza = adesso.AddMinutes(CodiceOtp.ValiditaMinuti);
        await RegistraAsync(r, "otp.inviato", new DatiOtp(sale, CodiceOtp.Impronta(otp, sale, r.IdRichiestaApprovazione), d.ImprontaTesti,
            CodiceOtp.Maschera(email), scadenza, idMessaggio), ct);
        return Json(new { ok = true, a = CodiceOtp.Maschera(email), validitaMinuti = CodiceOtp.ValiditaMinuti });
    }

    // ── Perfezionamento: caselle + codice OTP ──────────────────────

    [HttpPost("{codice}/conferma")]
    public async Task<IActionResult> Conferma(string codice, [FromBody] RichiestaConferma req, CancellationToken ct)
    {
        var r = await TrovaAsync(codice, ct, traccia: true);
        if (r is null) return NotFound(new { messaggio = "Il link non è più valido." });
        if (r.Stato == StatoRichiestaApprovazione.Approvata) return BadRequest(new { messaggio = "Approvazione già registrata." });

        var eventi = await EventiAsync(r, ct);
        var mancanti = PassiMancanti(r, eventi);
        if (mancanti.Count > 0) return BadRequest(new { messaggio = "Prima di confermare occorre " + string.Join(", ", mancanti) + "." });
        var (d, errore) = Dichiara(r, req?.Nome, req?.Cognome, req?.Qualifica);
        if (d is null) return BadRequest(new { messaggio = errore });

        // Codice OTP: l'ultimo inviato, non scaduto, non esaurito, legato agli stessi testi.
        var (eventoOtp, otp) = UltimoCodice(eventi);
        if (eventoOtp is null || otp is null) return BadRequest(new { messaggio = "Chiedi prima il codice con «Inviami il codice»." });
        var adesso = DateTime.UtcNow;
        if (adesso > otp.Scadenza) return BadRequest(new { messaggio = "Il codice è scaduto: chiedine uno nuovo." });
        var errati = eventi.Count(x => x.Tipo == "otp.errato" && x.IdEventoApprovazione > eventoOtp.IdEventoApprovazione);
        if (errati >= CodiceOtp.TentativiMassimi) return BadRequest(new { messaggio = "Troppi tentativi con questo codice: chiedine uno nuovo." });
        if (otp.ImprontaTesti != d.ImprontaTesti)
            return BadRequest(new { messaggio = "Hai cambiato i dati dopo aver chiesto il codice: chiedine uno nuovo." });
        var inserito = CodiceOtp.Normalizza(req!.Codice);
        if (inserito.Length != 6 || !CodiceOtp.Corrisponde(inserito, otp.Sale, r.IdRichiestaApprovazione, otp.Impronta))
        {
            await RegistraAsync(r, "otp.errato", null, ct);
            var restano = CodiceOtp.TentativiMassimi - errati - 1;
            return BadRequest(new { messaggio = restano > 0 ? $"Codice non corretto. Tentativi rimasti: {restano}." : "Codice non corretto. Chiedi un nuovo codice." });
        }
        await RegistraAsync(r, "otp.verificato", new { otp.A, Inviato = eventoOtp.DataUtc }, ct);

        var revisione = r.Revisione!;
        var ora = DateTime.UtcNow;
        r.Stato = StatoRichiestaApprovazione.Approvata;
        r.Modalita = ModalitaApprovazione.CaselleOtp;
        r.DataPerfezionamento = ora;
        r.NomeDichiarato = d.Nome; r.CognomeDichiarato = d.Cognome; r.QualificaDichiarata = d.Qualifica;
        r.ImprontaTesti = d.ImprontaTesti;
        r.ScadenzaLink = ora.AddDays(GiorniLinkDopoApprovazione);   // resta solo per scaricare la Ricevuta
        r.DataUltimaModifica = ora;

        // L'evento salva insieme stato e dati dichiarati (stessa transazione).
        await RegistraAsync(r, "approvazione.perfezionata", new
        {
            Modalita = r.Modalita.ToString(),
            Revisione = revisione.Codice, ImprontaCondizioni = revisione.ImprontaPdf,
            Offerta = r.OffertaPdf is null ? null : new { Riferimento = r.OffertaRiferimento, Impronta = r.OffertaImpronta },
            Dichiarati = new { d.Nome, d.Cognome, d.Qualifica, d.Email },
            Testi = new { d.Casella1, Casella2 = d.Casella2, d.Clausole, d.Casella3 },
            d.ImprontaTesti,
            Otp = new { Canale = "e-mail", otp.A, Inviato = eventoOtp.DataUtc, Verificato = ora, otp.IdMessaggio },
            EventiPrecedenti = eventi.Select(x => new { x.IdEventoApprovazione, x.Tipo, Ora = x.DataUtc })
        }, ct);

        _log.LogInformation("Approvazione perfezionata con codice OTP: richiesta {Id}, cliente {Cliente}",
            r.IdRichiestaApprovazione, r.IdClienteArcLab);
        return Json(new { ok = true, messaggio = "Approvazione registrata. Riceverai la Ricevuta per e-mail entro pochi minuti." });
    }
}

public sealed record ModelloClausola(string Articolo, string ContenutoBreve, string? TestoIntegrale);

public sealed class ModelloPaginaFirma
{
    public string Codice { get; init; } = "";
    public bool Approvata { get; init; }
    public bool RicevutaPronta { get; init; }
    public string? NumeroRicevuta { get; init; }
    public DateTime? DataApprovazione { get; init; }
    public string Titolo { get; init; } = "";
    public string Revisione { get; init; } = "";
    public string? CosaCambia { get; init; }
    public bool ConOfferta { get; init; }
    public string? Offerta { get; init; }
    public string Casella1 { get; init; } = "";
    public string Casella2Introduzione { get; init; } = "";
    public List<ModelloClausola> Clausole { get; init; } = new();
    public string? RagioneSociale { get; init; }
    public string? PartitaIva { get; init; }
    public string? Nome { get; init; }
    public string? Cognome { get; init; }
    public string? Qualifica { get; init; }
    public string? Email { get; init; }
    public string EmailMascherata => CodiceOtp.Maschera(Email);
}

public sealed record RichiestaPasso(string? Tipo);

public sealed record RichiestaCodice(string? Nome, string? Cognome, string? Qualifica);

public sealed record RichiestaConferma(string? Nome, string? Cognome, string? Qualifica, string? Codice);
