using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti.Manualistica;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Manualistica: i modelli documentali da cui si generano i documenti
/// per i clienti (01/11/2026).
///
/// Un modello si carica come revisione in bozza, si controlla, si manda
/// in approvazione e diventa utilizzabile quando viene approvato.
/// Resta registrato chi lo ha caricato e chi lo ha approvato.
/// </summary>
[RichiedeUnoDeiPermessi(Permessi.ManualisticaModelliGestisci, Permessi.ManualisticaModelliApprova,
    Permessi.ManualisticaConsulta)]
public class ManualisticaController : Controller
{
    private const string MimeDocx =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>Limite per file: i modelli più grandi stanno sotto il megabyte, il Registro arriva a 800 KB.</summary>
    private const long DimensioneMassima = 20 * 1024 * 1024;

    private readonly ServizioModelliManualistica _modelli;
    private readonly ServizioVociChecklist _voci;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;

    public ManualisticaController(ServizioModelliManualistica modelli, ServizioVociChecklist voci,
        IContestoUtente utente, IServizioAudit audit)
    {
        _modelli = modelli;
        _voci = voci;
        _utente = utente;
        _audit = audit;
    }

    private string Upn => _utente.Email ?? "sconosciuto";

    public IActionResult Modelli()
    {
        ViewData["Titolo"] = "Manualistica: modelli";
        return View();
    }

    /// <summary>
    /// L'elenco dei modelli di un tipo di manuale. Senza indicazione si
    /// usa il primo tipo attivo: la pagina non conosce più «PRIVACY»,
    /// così gli altri manuali compaiono da soli quando arrivano.
    /// </summary>
    [HttpGet("api/manualistica/modelli")]
    public async Task<IActionResult> Elenco(string? tipo, CancellationToken ct)
    {
        var tipi = await _modelli.TipiAsync(ct);
        if (tipi.Count == 0) return Json(new { Tipi = Array.Empty<object>(), Tipo = (string?)null, Modelli = Array.Empty<object>() });

        var scelto = tipi.FirstOrDefault(t => t.Codice == tipo) ?? tipi[0];

        var righe = await _modelli.ElencoAsync(scelto.Codice, ct);

        // Approvare richiede il permesso e l'essere indicati per questo tipo di manuale.
        var indicato = await _modelli.PuoApprovareAsync(Upn, scelto.Codice, ct);
        var approvatori = (await _modelli.ApprovatoriAsync(ct))
            .FirstOrDefault(t => t.Codice == scelto.Codice);

        return Json(new
        {
            Tipi = tipi.Select(t => new { t.Codice, t.Nome }),
            Tipo = scelto.Codice,
            NomeTipo = scelto.Nome,
            PuoCaricare = _utente.HaPermesso(Permessi.ManualisticaModelliGestisci),
            PuoApprovare = _utente.HaPermesso(Permessi.ManualisticaModelliApprova) && indicato,
            ChiApprova = approvatori?.Approvatori.Select(a => a.NomeCompleto) ?? Enumerable.Empty<string>(),
            PuoIndicareApprovatori = _utente.HaPermesso(Permessi.ManualisticaApprovatori),
            Io = Upn,
            ApprovazioniNecessarie = ServizioModelliManualistica.ApprovazioniNecessarie,
            Modelli = righe.Select(r => new
            {
                r.IdModelloDocumento, r.Codice, r.Nome, r.CondizioneInclusione, r.Ordine,
                r.IdRevisione, r.NumeroRevisione,
                Stato = r.Stato?.ToString(),
                CaricataIl = r.CaricataIlUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                r.CaricataDa,
                r.NumeroRevisioneApprovata,
                Approvazioni = r.Approvazioni.Select(a => new
                {
                    a.Approvatore, a.Approvata, a.Note,
                    Data = a.DataUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                })
            })
        });
    }

    /// <summary>
    /// Carica uno o più modelli. Ogni file è indipendente: quelli che
    /// non vanno non fermano gli altri, e il riepilogo dice file per
    /// file com'è andata.
    /// </summary>
    [HttpPost("api/manualistica/modelli/carica")]
    [RichiedePermesso(Permessi.ManualisticaModelliGestisci)]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<IActionResult> Carica([FromForm] List<IFormFile> file, [FromForm] string? codiceModello,
        [FromForm] string? tipo, CancellationToken ct)
    {
        if (file is null || file.Count == 0) return BadRequest("Nessun file.");

        var tipi = await _modelli.TipiAsync(ct);
        if (tipi.Count == 0) return BadRequest("Nessun tipo di manuale configurato.");
        var scelto = (tipi.FirstOrDefault(t => t.Codice == tipo) ?? tipi[0]).Codice;
        if (file.Count > 1 && !string.IsNullOrWhiteSpace(codiceModello))
            return BadRequest("Il modello si indica solo quando si carica un file per volta.");

        var esiti = new List<EsitoCaricamentoModello>();

        foreach (var f in file)
        {
            if (f.Length == 0 || f.Length > DimensioneMassima)
            {
                esiti.Add(new EsitoCaricamentoModello(f.FileName, false, null, null, null,
                    f.Length == 0 ? "Il file è vuoto." : "Il file supera i 20 MB.", null));
                continue;
            }

            using var memoria = new MemoryStream();
            await f.CopyToAsync(memoria, ct);

            esiti.Add(await _modelli.CaricaAsync(
                scelto, f.FileName, memoria.ToArray(),
                string.IsNullOrWhiteSpace(codiceModello) ? null : codiceModello, Upn, ct));
        }

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "modelli.carica",
            esiti.Any(e => e.Salvato) ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            dettaglio: new
            {
                Tipo = scelto,
                File = esiti.Count,
                Salvati = esiti.Count(e => e.Salvato),
                Codici = esiti.Where(e => e.Salvato).Select(e => e.CodiceModello)
            }, ct: ct);

        return Json(new
        {
            Salvati = esiti.Count(e => e.Salvato),
            Totale = esiti.Count,
            Esiti = esiti.Select(e => new
            {
                e.NomeFile, e.Salvato, e.CodiceModello, e.IdRevisione, e.NumeroRevisione, e.Motivo,
                Controllo = e.Controllo is null ? null : new
                {
                    e.Controllo.Ok, e.Controllo.NumeroControlli, e.Controllo.Errori, e.Controllo.Avvisi,
                    e.Controllo.TagPerPrefisso
                }
            })
        });
    }

    [HttpGet("api/manualistica/modelli/{idRevisione:int}/controllo")]
    public async Task<IActionResult> Controllo(int idRevisione, CancellationToken ct)
    {
        var esito = await _modelli.VerificaAsync(idRevisione, ct);
        if (esito is null) return NotFound();
        return Json(new
        {
            esito.NomeFile, esito.Ok, esito.NumeroControlli, esito.Errori, esito.Avvisi, esito.TagPerPrefisso
        });
    }

    [HttpPost("api/manualistica/modelli/{idRevisione:int}/in-approvazione")]
    [RichiedePermesso(Permessi.ManualisticaModelliGestisci)]
    public async Task<IActionResult> InApprovazione(int idRevisione, CancellationToken ct)
    {
        var (ok, errore) = await _modelli.InviaInApprovazioneAsync(idRevisione, ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "modelli.in_approvazione",
            ok ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            idEntita: idRevisione.ToString(), dettaglio: new { Errore = errore }, ct: ct);
        return ok ? Json(new { Ok = true }) : BadRequest(errore);
    }

    [HttpPost("api/manualistica/modelli/{idRevisione:int}/decidi")]
    [RichiedePermesso(Permessi.ManualisticaModelliApprova)]
    public async Task<IActionResult> Decidi(int idRevisione, [FromBody] DecisioneModello decisione, CancellationToken ct)
    {
        var (ok, errore) = await _modelli.DecidiAsync(idRevisione, Upn, decisione.Approva, decisione.Note, ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, decisione.Approva ? "modelli.approva" : "modelli.respingi",
            ok ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            idEntita: idRevisione.ToString(), dettaglio: new { Errore = errore }, ct: ct);
        return ok ? Json(new { Ok = true }) : BadRequest(errore);
    }

    // ── Voci della check list ──────────────────────────────────────

    [RichiedePermesso(Permessi.ManualisticaImpostazioni)]
    public IActionResult Voci()
    {
        ViewData["Titolo"] = "Manualistica: voci della check list";
        return View();
    }

    /// <summary>
    /// Cosa verrebbe fuori leggendo i modelli approvati, senza scrivere
    /// niente. Si guarda prima: le segnalazioni dicono dove i dizionari
    /// dei modelli non tornano, e quelle si correggono nei modelli.
    /// </summary>
    [HttpGet("api/manualistica/voci/anteprima")]
    [RichiedePermesso(Permessi.ManualisticaImpostazioni)]
    public async Task<IActionResult> AnteprimaVoci(string? tipo, CancellationToken ct)
    {
        var scelto = await TipoSceltoAsync(tipo, ct);
        if (scelto is null) return BadRequest("Nessun tipo di manuale configurato.");

        var esito = await _voci.EstraiAsync(scelto.Codice, scrivi: false, ct: ct);
        return Json(Risposta(scelto, esito));
    }

    [HttpPost("api/manualistica/voci/estrai")]
    [RichiedePermesso(Permessi.ManualisticaImpostazioni)]
    public async Task<IActionResult> EstraiVoci(string? tipo, CancellationToken ct)
    {
        var scelto = await TipoSceltoAsync(tipo, ct);
        if (scelto is null) return BadRequest("Nessun tipo di manuale configurato.");

        var esito = await _voci.EstraiAsync(scelto.Codice, scrivi: true, Upn, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "voci.estrai", EsitoOperazione.Successo,
            dettaglio: new
            {
                Tipo = scelto.Codice,
                esito.ModelliLetti, esito.VociTrovate,
                esito.VociCreate, esito.OpzioniCreate, esito.OpzioniDisattivate,
                Segnalazioni = esito.Segnalazioni.Count
            }, ct: ct);

        return Json(Risposta(scelto, esito));
    }

    /// <summary>L'elenco delle domande, con il passo assegnato e quello proposto.</summary>
    [HttpGet("api/manualistica/voci")]
    [RichiedePermesso(Permessi.ManualisticaImpostazioni)]
    public async Task<IActionResult> ElencoVoci(string? tipo, CancellationToken ct)
    {
        var scelto = await TipoSceltoAsync(tipo, ct);
        if (scelto is null) return BadRequest("Nessun tipo di manuale configurato.");

        var voci = await _voci.ElencoAsync(scelto.Codice, ct);
        return Json(new
        {
            Tipo = scelto.Codice,
            Passi = ServizioVociChecklist.Passi.Select(p => new { p.Codice, p.Nome }),
            Voci = voci.Select(v => new
            {
                v.IdVoceChecklist, v.Codice, v.Etichetta, v.Gruppo, v.PassoProposto,
                v.Opzioni, v.OpzioniLegateAlModello, v.Attivo
            })
        });
    }

    [HttpGet("api/manualistica/voci/{idVoce:int}/opzioni")]
    [RichiedePermesso(Permessi.ManualisticaImpostazioni)]
    public async Task<IActionResult> OpzioniVoce(int idVoce, CancellationToken ct)
    {
        var opzioni = await _voci.OpzioniAsync(idVoce, ct);
        return Json(opzioni.Select(o => new { o.Valore, o.Documento, o.Attivo, o.DaCompilazione }));
    }

    [HttpPost("api/manualistica/voci/passo")]
    [RichiedePermesso(Permessi.ManualisticaImpostazioni)]
    public async Task<IActionResult> ImpostaPasso([FromBody] AssegnazionePasso scelta, CancellationToken ct)
    {
        var quante = await _voci.ImpostaPassoAsync(scelta.IdVoci ?? [], scelta.Passo, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "voci.passo", EsitoOperazione.Successo,
            dettaglio: new { scelta.Passo, Domande = quante }, ct: ct);

        return Json(new { Quante = quante });
    }

    private async Task<TipoManualeVoce?> TipoSceltoAsync(string? tipo, CancellationToken ct)
    {
        var tipi = await _modelli.TipiAsync(ct);
        return tipi.Count == 0 ? null : tipi.FirstOrDefault(t => t.Codice == tipo) ?? tipi[0];
    }

    private static object Risposta(TipoManualeVoce tipo, EsitoEstrazioneVoci e) => new
    {
        Tipo = tipo.Codice,
        tipo.Nome,
        e.ModelliLetti, e.VociTrovate, e.OpzioniTrovate, e.OpzioniLegateAlModello,
        e.VociCreate, e.VociAggiornate,
        e.OpzioniCreate, e.OpzioniRiattivate, e.OpzioniDisattivate,
        e.Scritto,
        Segnalazioni = e.Segnalazioni.Select(s => new { s.Codice, s.Problema, s.Dettaglio })
    };

    // ── Chi approva ────────────────────────────────────────────────

    [RichiedePermesso(Permessi.ManualisticaApprovatori)]
    public IActionResult Approvatori()
    {
        ViewData["Titolo"] = "Manualistica: chi approva";
        return View();
    }

    [HttpGet("api/manualistica/approvatori")]
    [RichiedePermesso(Permessi.ManualisticaApprovatori)]
    public async Task<IActionResult> ElencoApprovatori(CancellationToken ct)
    {
        var tipi = await _modelli.ApprovatoriAsync(ct);
        var utenti = await _modelli.UtentiAttiviAsync(ct);
        return Json(new
        {
            Tipi = tipi.Select(t => new
            {
                t.IdTipoManuale, t.Codice, t.Nome,
                Approvatori = t.Approvatori.Select(a => new { a.IdUtente, a.Email, a.NomeCompleto })
            }),
            Utenti = utenti.Select(u => new { u.IdUtente, u.Email, u.NomeCompleto })
        });
    }

    [HttpPost("api/manualistica/approvatori")]
    [RichiedePermesso(Permessi.ManualisticaApprovatori)]
    public async Task<IActionResult> ImpostaApprovatore([FromBody] SceltaApprovatore scelta, CancellationToken ct)
    {
        var (ok, errore) = await _modelli.ImpostaApprovatoreAsync(
            scelta.IdTipoManuale, scelta.IdUtente, scelta.Abilitato, Upn, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, scelta.Abilitato ? "approvatori.aggiungi" : "approvatori.togli",
            ok ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            idEntita: $"{scelta.IdTipoManuale}/{scelta.IdUtente}",
            dettaglio: new { Errore = errore }, ct: ct);

        return ok ? Json(new { Ok = true }) : BadRequest(errore);
    }

    [HttpGet("api/manualistica/modelli/{idRevisione:int}/scarica")]
    public async Task<IActionResult> Scarica(int idRevisione, CancellationToken ct)
    {
        var file = await _modelli.ScaricaAsync(idRevisione, ct);
        if (file is null) return NotFound();
        return File(file.Value.Contenuto, MimeDocx, file.Value.NomeFile);
    }
}

/// <summary>Esito dell'esame di una revisione.</summary>
public record DecisioneModello(bool Approva, string? Note);

/// <summary>Chi approva un tipo di manuale.</summary>
public record SceltaApprovatore(int IdTipoManuale, int IdUtente, bool Abilitato);

/// <summary>Il passo da assegnare a una o piu' domande; null lo toglie.</summary>
public record AssegnazionePasso(int[]? IdVoci, string? Passo);
