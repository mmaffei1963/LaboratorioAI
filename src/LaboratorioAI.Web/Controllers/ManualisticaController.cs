using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;

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
    private readonly ServizioCompilazioni _compilazioni;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IWebHostEnvironment _ambiente;

    public ManualisticaController(ServizioModelliManualistica modelli, ServizioVociChecklist voci,
        ServizioCompilazioni compilazioni, IContestoUtente utente, IServizioAudit audit,
        IWebHostEnvironment ambiente)
    {
        _modelli = modelli;
        _voci = voci;
        _compilazioni = compilazioni;
        _utente = utente;
        _audit = audit;
        _ambiente = ambiente;
    }

    private string Upn => _utente.Email ?? "sconosciuto";

    /// <summary>
    /// Se la schermata deve mostrare i campi scrivibili. La stessa
    /// regola la applica il servizio: qui si decide solo cosa far
    /// vedere.
    /// </summary>
    private bool PuoCompilare(CompilazioneAperta aperta) =>
        _utente.HaPermesso(Permessi.ManualisticaGenera)
        && aperta.Stato is StatoCompilazione.Bozza or StatoCompilazione.Generata;

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

    // ── Compilazioni ───────────────────────────────────────────────

    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public IActionResult Compilazioni()
    {
        ViewData["Titolo"] = "Manualistica: check list dei clienti";
        return View();
    }

    [HttpGet("api/manualistica/compilazioni")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> ElencoCompilazioni(string? tipo, CancellationToken ct)
    {
        var scelto = await TipoSceltoAsync(tipo, ct);
        if (scelto is null) return BadRequest("Nessun tipo di manuale configurato.");

        var righe = await _compilazioni.ElencoAsync(scelto.Codice, ct);

        return Json(new
        {
            Tipo = scelto.Codice,
            scelto.Nome,
            PuoCompilare = _utente.HaPermesso(Permessi.ManualisticaGenera),
            Compilazioni = righe.Select(c => new
            {
                c.IdCompilazione, c.RagioneSociale, c.Risposte,
                Data = c.DataDocumenti.ToString("dd/MM/yyyy"),
                Revisione = "Rev. " + c.Revisione.ToString("00"),
                Stato = c.Stato.ToString(),
                c.CreatoDa,
                Tocco = (c.ModificatoIlUtc ?? c.CreatoIlUtc).ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            })
        });
    }

    /// <summary>I clienti di ArcLab fra cui scegliere. Sola lettura.</summary>
    [HttpGet("api/manualistica/compilazioni/clienti")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> ClientiArcLab(CancellationToken ct)
    {
        var clienti = await _compilazioni.ClientiAsync(ct);
        return Json(clienti.Select(c => new
        {
            c.Id, c.Nome, c.PartitaIva, c.Comune, c.HaEmail, c.Insediamenti
        }));
    }

    [HttpPost("api/manualistica/compilazioni")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> CreaCompilazione([FromBody] NuovaCompilazione dati, CancellationToken ct)
    {
        var scelto = await TipoSceltoAsync(dati.Tipo, ct);
        if (scelto is null) return BadRequest("Nessun tipo di manuale configurato.");

        var esito = await _compilazioni.CreaAsync(
            scelto.Codice, dati.IdClienteArcLab, dati.RagioneSociale,
            dati.DataDocumenti ?? DateTime.Today, dati.Revisione ?? 0, Upn, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "compilazione.crea",
            esito.IdCompilazione is null ? EsitoOperazione.Errore : EsitoOperazione.Successo,
            idEntita: esito.IdCompilazione?.ToString(),
            dettaglio: new { Tipo = scelto.Codice, dati.IdClienteArcLab, esito.Errore }, ct: ct);

        return esito.Errore is null
            ? Json(new { esito.IdCompilazione })
            : BadRequest(esito.Errore);
    }

    /// <summary>La schermata in cui si compila la check list di un cliente.</summary>
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> Compila(int id, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        ViewData["Titolo"] = $"Check list: {aperta.RagioneSociale}";
        ViewData["IdCompilazione"] = id;
        return View();
    }

    [HttpGet("api/manualistica/compilazioni/{id:int}")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> ApriCompilazione(int id, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        return Json(new
        {
            aperta.IdCompilazione, aperta.RagioneSociale,
            Data = aperta.DataDocumenti.ToString("dd/MM/yyyy"),
            Revisione = "Rev. " + aperta.Revisione.ToString("00"),
            Stato = aperta.Stato.ToString(),
            PuoCompilare = PuoCompilare(aperta),
            Passi = ServizioVociChecklist.Passi.Select(p => new { p.Codice, p.Nome }),
            Risposte = aperta.Risposte
        });
    }

    [HttpGet("api/manualistica/compilazioni/{id:int}/passo/{passo}")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> DomandeDelPasso(int id, string passo, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        var domande = await _compilazioni.DomandeAsync(aperta.CodiceTipo, passo, ct);

        return Json(domande.Select(d => new
        {
            d.Codice, d.Etichetta, Tipo = d.Tipo.ToString(), d.Obbligatoria,
            Opzioni = d.Opzioni.Select(o => new { o.Valore, o.Testo, o.Predefinita })
        }));
    }

    [HttpPost("api/manualistica/compilazioni/{id:int}/valore")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> SalvaValore(int id, [FromBody] RispostaChecklist risposta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(risposta.Codice)) return BadRequest("Manca la domanda.");

        var ok = await _compilazioni.SalvaValoreAsync(
            id, risposta.Codice, risposta.Valore, risposta.ScrittoAMano, Upn, ct);

        return ok ? Json(new { Ok = true }) : BadRequest("La check list non è modificabile.");
    }

    // ── Sedi del trattamento ──────────────────────────────

    /// <summary>Le sedi gia' inserite e gli insediamenti che ArcLab propone.</summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/sedi")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> Sedi(int id, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        var sedi = await _compilazioni.SediAsync(id, ct);
        var proposte = await _compilazioni.ProposteSediAsync(id, ct);

        return Json(new
        {
            Sedi = sedi,
            Proposte = proposte,
            PuoCompilare = PuoCompilare(aperta)
        });
    }

    /// <summary>Aggiunge o corregge una sede.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/sedi")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> SalvaSede(int id, [FromBody] SedeInviata sede, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sede.Indirizzo) && string.IsNullOrWhiteSpace(sede.Comune))
            return BadRequest("Una sede senza indirizzo né comune non serve a niente.");

        var idSede = await _compilazioni.SalvaSedeAsync(
            id, sede.Id, sede.Nome, sede.Indirizzo, sede.Cap, sede.Comune, sede.Provincia, Upn, ct);

        return idSede is null
            ? BadRequest("La check list non è modificabile.")
            : Json(new { Ok = true, Id = idSede });
    }

    /// <summary>Porta dentro gli insediamenti scelti fra quelli proposti.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/sedi/importa")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> ImportaSedi(int id, [FromBody] SediScelte scelte, CancellationToken ct)
    {
        var quante = await _compilazioni.ImportaSediAsync(id, scelte.Codici ?? [], Upn, ct);
        return Json(new { Ok = true, Quante = quante });
    }

    /// <summary>Toglie una sede dalla check list.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/sedi/{idSede:int}/togli")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> TogliSede(int id, int idSede, CancellationToken ct)
    {
        var ok = await _compilazioni.TogliSedeAsync(id, idSede, Upn, ct);
        return ok ? Json(new { Ok = true }) : BadRequest("La check list non è modificabile.");
    }

    // ── Autorizzati al trattamento ──────────────────────

    /// <summary>Gli autorizzati inseriti, con qualifiche e funzioni fra cui scegliere.</summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/autorizzati")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> Autorizzati(int id, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        var persone = await _compilazioni.AutorizzatiAsync(id, ct);
        var scelte = await _compilazioni.SceltePersoneAsync(aperta.CodiceTipo, ct);

        return Json(new
        {
            Autorizzati = persone,
            scelte.Qualifiche,
            scelte.Funzioni,
            PuoCompilare = PuoCompilare(aperta)
        });
    }

    /// <summary>Aggiunge o corregge un autorizzato.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/autorizzati")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> SalvaAutorizzato(
        int id, [FromBody] AutorizzatoInviato persona, CancellationToken ct)
    {
        if (persona.IdQualifica <= 0) return BadRequest("Manca la qualifica.");

        var idPersona = await _compilazioni.SalvaAutorizzatoAsync(
            id, persona.Id, persona.IdQualifica, persona.Nome, persona.Cognome,
            persona.Quantita, persona.Funzioni ?? [], Upn, ct);

        return idPersona is null
            ? BadRequest("La check list non è modificabile.")
            : Json(new { Ok = true, Id = idPersona });
    }

    /// <summary>Toglie un autorizzato dalla check list.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/autorizzati/{idPersona:int}/togli")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> TogliAutorizzato(int id, int idPersona, CancellationToken ct)
    {
        var ok = await _compilazioni.TogliAutorizzatoAsync(id, idPersona, Upn, ct);
        return ok ? Json(new { Ok = true }) : BadRequest("La check list non è modificabile.");
    }

    // ── Archivi, gestionali e caselle ──────────────────

    /// <summary>Archivi e gestionali della check list, con le voci delle tendine.</summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/archivi")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> Archivi(
        int id, [FromServices] ServizioArchiviCompilazione archivi, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        var scelte = await _compilazioni.SceltePersoneAsync(aperta.CodiceTipo, ct);

        return Json(new
        {
            Archivi = await archivi.ArchiviAsync(id, ct),
            Gestionali = await archivi.GestionaliAsync(id, ct),
            Caselle = await archivi.CaselleAsync(id, ct),
            scelte.Funzioni,
            Tendine = await archivi.DizionarioAsync(aperta.CodiceTipo, ct),
            PuoCompilare = PuoCompilare(aperta)
        });
    }

    /// <summary>Aggiunge o corregge un archivio.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/archivi")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> SalvaArchivio(
        int id, [FromBody] ArchivioInviato a,
        [FromServices] ServizioArchiviCompilazione archivi, CancellationToken ct)
    {
        var idArchivio = await archivi.SalvaArchivioAsync(
            id, a.Id, a.Tipo ?? "", a.Luogo, a.SistemaOperativo, a.Antivirus, a.Backup,
            a.FrequenzaBackup, a.LuogoConservazione,
            a.Accessi ?? [], a.Interessati ?? [], a.CategorieDati ?? [], a.Gestionali ?? [],
            Upn, ct);

        return idArchivio is null
            ? BadRequest("La check list non è modificabile, oppure il tipo di archivio non è valido.")
            : Json(new { Ok = true, Id = idArchivio });
    }

    /// <summary>Toglie un archivio.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/archivi/{idArchivio:int}/togli")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> TogliArchivio(
        int id, int idArchivio, [FromServices] ServizioArchiviCompilazione archivi, CancellationToken ct)
    {
        var ok = await archivi.TogliArchivioAsync(id, idArchivio, Upn, ct);
        return ok ? Json(new { Ok = true }) : BadRequest("La check list non è modificabile.");
    }

    /// <summary>Aggiunge o corregge un gestionale.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/gestionali")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> SalvaGestionale(
        int id, [FromBody] GestionaleInviato g,
        [FromServices] ServizioArchiviCompilazione archivi, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(g.Prodotto)) return BadRequest("Manca il nome del gestionale.");

        var idGestionale = await archivi.SalvaGestionaleAsync(
            id, g.Id, g.Prodotto, g.MisureSicurezza, g.Manutenzione,
            g.Backup, g.Frequenza, g.LuogoConservazione, Upn, ct);

        return idGestionale is null
            ? BadRequest("La check list non è modificabile.")
            : Json(new { Ok = true, Id = idGestionale });
    }

    /// <summary>Toglie un gestionale.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/gestionali/{idGestionale:int}/togli")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> TogliGestionale(
        int id, int idGestionale, [FromServices] ServizioArchiviCompilazione archivi, CancellationToken ct)
    {
        var ok = await archivi.TogliGestionaleAsync(id, idGestionale, Upn, ct);
        return ok ? Json(new { Ok = true }) : BadRequest("La check list non è modificabile.");
    }

    /// <summary>Aggiunge o corregge una casella di posta.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/caselle")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> SalvaCasella(
        int id, [FromBody] CasellaInviata c,
        [FromServices] ServizioArchiviCompilazione archivi, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(c.Indirizzo)) return BadRequest("Manca l'indirizzo.");

        var idCasella = await archivi.SalvaCasellaAsync(id, c.Id, c.Indirizzo, c.Funzioni ?? [], Upn, ct);

        return idCasella is null
            ? BadRequest("La check list non è modificabile.")
            : Json(new { Ok = true, Id = idCasella });
    }

    /// <summary>Toglie una casella di posta.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/caselle/{idCasella:int}/togli")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> TogliCasella(
        int id, int idCasella, [FromServices] ServizioArchiviCompilazione archivi, CancellationToken ct)
    {
        var ok = await archivi.TogliCasellaAsync(id, idCasella, Upn, ct);
        return ok ? Json(new { Ok = true }) : BadRequest("La check list non è modificabile.");
    }

    // ── Firme ─────────────────────────────────────

    /// <summary>Le firme raccolte, senza le immagini.</summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/firme")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> Firme(int id, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        return Json(new
        {
            Firme = await _compilazioni.FirmeAsync(id, ct),
            PuoCompilare = PuoCompilare(aperta)
        });
    }

    /// <summary>L'immagine di una firma, per rivederla.</summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/firme/{ruolo}/immagine")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> ImmagineFirma(int id, string ruolo, CancellationToken ct)
    {
        if (!Enum.TryParse<RuoloFirmaManualistica>(ruolo, out var quale)) return BadRequest();

        var png = await _compilazioni.ImmagineFirmaAsync(id, quale, ct);
        return png is null ? NotFound() : File(png, "image/png");
    }

    /// <summary>Carica la firma di un ruolo.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/firme/{ruolo}")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> CaricaFirma(
        int id, string ruolo, IFormFile? file, string? nome, CancellationToken ct)
    {
        if (!Enum.TryParse<RuoloFirmaManualistica>(ruolo, out var quale)) return BadRequest("Ruolo sconosciuto.");
        if (file is null || file.Length == 0) return BadRequest("Manca il file della firma.");
        if (file.Length > 2 * 1024 * 1024) return BadRequest("La firma non deve superare i 2 MB.");

        using var memoria = new MemoryStream();
        await file.CopyToAsync(memoria, ct);
        var png = memoria.ToArray();

        // Si accetta solo PNG, e lo si verifica dai byte: l'estensione
        // del file non dice niente, e un JPEG qui dentro uscirebbe come
        // un rettangolo nero nel documento.
        if (png.Length < 8 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47)
            return BadRequest("La firma deve essere un file PNG.");

        var ok = await _compilazioni.SalvaFirmaAsync(id, quale, png, nome, Upn, ct);
        if (!ok) return BadRequest("La check list non è modificabile.");

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "firma.carica", EsitoOperazione.Successo,
            dettaglio: new { IdCompilazione = id, Ruolo = quale.ToString() }, ct: ct);

        return Json(new { Ok = true });
    }

    /// <summary>Toglie una firma.</summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/firme/{ruolo}/togli")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> TogliFirma(int id, string ruolo, CancellationToken ct)
    {
        if (!Enum.TryParse<RuoloFirmaManualistica>(ruolo, out var quale)) return BadRequest();

        var ok = await _compilazioni.TogliFirmaAsync(id, quale, Upn, ct);
        if (!ok) return BadRequest("La check list non è modificabile.");

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "firma.togli", EsitoOperazione.Successo,
            dettaglio: new { IdCompilazione = id, Ruolo = quale.ToString() }, ct: ct);

        return Json(new { Ok = true });
    }

    /// <summary>I documenti che si possono generare per questa compilazione.</summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/documenti")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> DocumentiGenerabili(int id, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        var modelli = await _compilazioni.ModelliGenerabiliAsync(aperta.CodiceTipo, ct);
        return Json(modelli.Select(m => new { m.Codice, m.Nome, Revisione = "Rev. " + m.Revisione.ToString("00") }));
    }

    /// <summary>
    /// Cosa succederebbe generando: gli avvisi da rileggere, o il
    /// motivo per cui non si puo'. Si guarda prima di scaricare.
    /// </summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/genera/{codice}/controllo")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> ControlloGenerazione(int id, string codice, CancellationToken ct)
    {
        var esito = await _compilazioni.GeneraAsync(id, codice, Upn, archivia: false, ct);
        if (esito is null) return NotFound();

        return Json(new { Ok = esito.Errore is null, esito.Errore, esito.Avvisi, esito.NomeFile });
    }

    [HttpGet("api/manualistica/compilazioni/{id:int}/genera/{codice}")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> Genera(int id, string codice, CancellationToken ct)
    {
        var esito = await _compilazioni.GeneraAsync(id, codice, Upn, archivia: true, ct);
        if (esito is null) return NotFound();
        if (esito.Contenuto is null) return BadRequest(esito.Errore);

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "compilazione.genera", EsitoOperazione.Successo,
            idEntita: id.ToString(),
            dettaglio: new { Documento = codice, Avvisi = esito.Avvisi.Count }, ct: ct);

        return File(esito.Contenuto, MimeDocx, esito.NomeFile);
    }

    // ── Archivio, PDF e invio ────────────────────────

    /// <summary>I documenti gia' prodotti per questa check list.</summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/documenti/archivio")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> Archivio(
        int id, [FromServices] ServizioArchivioManualistica archivio, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        return Json(new
        {
            Documenti = await archivio.ElencoAsync(id, ct),
            Invii = await _compilazioni.InviiAsync(id, ct),
            Stato = aperta.Stato.ToString(),
            Riapribile = _utente.HaPermesso(Permessi.ManualisticaGenera)
                         && aperta.Stato is StatoCompilazione.Inviata or StatoCompilazione.InAttesaFirma,
            archivio.Configurato,
            Indirizzi = await _compilazioni.IndirizziPropostiAsync(id, ct),
            PuoCompilare = _utente.HaPermesso(Permessi.ManualisticaGenera)
        });
    }

    /// <summary>Riscarica un documento archiviato.</summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/documenti/archivio/{idDocumento:int}")]
    [RichiedeUnoDeiPermessi(Permessi.ManualisticaGenera, Permessi.ManualisticaConsulta)]
    public async Task<IActionResult> ScaricaArchiviato(
        int id, int idDocumento, [FromServices] ServizioArchivioManualistica archivio, CancellationToken ct)
    {
        var documento = await archivio.LeggiAsync(id, idDocumento, ct);
        if (documento is null) return NotFound("Il file non si trova più nell'archivio.");

        var tipo = documento.Value.NomeFile.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf" : MimeDocx;

        return File(documento.Value.Contenuto, tipo, documento.Value.NomeFile);
    }

    /// <summary>Genera un documento gia' convertito in PDF.</summary>
    [HttpGet("api/manualistica/compilazioni/{id:int}/genera/{codice}/pdf")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> GeneraPdf(int id, string codice, CancellationToken ct)
    {
        var esito = await _compilazioni.GeneraPdfAsync(id, codice, _ambiente.WebRootPath, Upn, ct);
        if (esito is null) return NotFound();
        if (esito.Contenuto is null) return BadRequest(esito.Errore);

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "compilazione.genera.pdf", EsitoOperazione.Successo,
            idEntita: id.ToString(), dettaglio: new { Documento = codice }, ct: ct);

        return File(esito.Contenuto, "application/pdf", esito.NomeFile);
    }

    /// <summary>
    /// Manda al cliente documenti gia' archiviati.
    ///
    /// Si inviano solo documenti che sono gia' nell'archivio: quello
    /// che il cliente riceve dev'essere esattamente quello che
    /// risulta consegnato.
    /// </summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/documenti/invia")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> InviaDocumenti(
        int id, [FromBody] InvioRichiesto richiesta,
        [FromServices] ServizioArchivioManualistica archivio,
        [FromServices] IServizioEmail email, CancellationToken ct)
    {
        var aperta = await _compilazioni.ApriAsync(id, ct);
        if (aperta is null) return NotFound();

        var destinatari = (richiesta.Destinatari ?? "")
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim()).Where(d => d.Contains('@')).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (destinatari.Count == 0) return BadRequest("Manca un indirizzo a cui mandarli.");
        if (richiesta.Documenti is null || richiesta.Documenti.Length == 0)
            return BadRequest("Non hai scelto nessun documento.");

        var allegati = new List<AllegatoEmail>();
        foreach (var idDocumento in richiesta.Documenti.Distinct())
        {
            var documento = await archivio.LeggiAsync(id, idDocumento, ct);
            if (documento is null) continue;

            var tipo = documento.Value.NomeFile.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf" : MimeDocx;

            allegati.Add(new AllegatoEmail(documento.Value.NomeFile, documento.Value.Contenuto, tipo));
        }

        if (allegati.Count == 0) return BadRequest("Nessuno dei documenti scelti si trova nell'archivio.");

        var oggetto = $"Documentazione privacy — {aperta.RagioneSociale}";
        var corpo = string.IsNullOrWhiteSpace(richiesta.Messaggio)
            ? "<p>In allegato la documentazione privacy.</p>"
            : "<p>" + System.Net.WebUtility.HtmlEncode(richiesta.Messaggio).Replace("\n", "<br />") + "</p>";

        var formati = allegati
            .Select(a => Path.GetExtension(a.NomeFile).TrimStart('.').ToUpperInvariant())
            .ToList();

        string? idMessaggio;
        try
        {
            idMessaggio = await email.InviaConAllegatiAsync(destinatari, oggetto, corpo, allegati, ct);
        }
        catch (Exception ex)
        {
            // Anche il tentativo fallito resta scritto: un invio mai
            // arrivato non deve sembrare mai tentato.
            await _compilazioni.RegistraInvioAsync(id, destinatari, formati, null, ex.Message, Upn, ct);

            await _audit.RegistraAsync(
                AreaFunzionale.Manualistica, "compilazione.invia", EsitoOperazione.Errore,
                idEntita: id.ToString(), dettaglio: new { Errore = ex.Message }, ct: ct);

            return BadRequest($"L'invio non è riuscito: {ex.Message}");
        }

        await _compilazioni.RegistraInvioAsync(id, destinatari, formati, idMessaggio, null, Upn, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "compilazione.invia", EsitoOperazione.Successo,
            idEntita: id.ToString(),
            dettaglio: new { Destinatari = destinatari, Allegati = allegati.Count, MessageId = idMessaggio },
            ct: ct);

        return Json(new { Ok = true, Quanti = allegati.Count, Destinatari = destinatari });
    }

    /// <summary>
    /// Riapre una check list consegnata, per correggerla. I documenti
    /// gia' prodotti restano nell'archivio.
    /// </summary>
    [HttpPost("api/manualistica/compilazioni/{id:int}/riapri")]
    [RichiedePermesso(Permessi.ManualisticaGenera)]
    public async Task<IActionResult> Riapri(int id, CancellationToken ct)
    {
        var ok = await _compilazioni.RiapriAsync(id, Upn, ct);
        if (!ok) return BadRequest("Questa check list non si può riaprire.");

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, "compilazione.riapri", EsitoOperazione.Successo,
            idEntita: id.ToString(), ct: ct);

        return Json(new { Ok = true });
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
                v.IdVoceChecklist, v.Codice, v.Etichetta, v.Gruppo,
                v.PassoProposto, v.SpegnimentoProposto,
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

    [HttpPost("api/manualistica/voci/attivo")]
    [RichiedePermesso(Permessi.ManualisticaImpostazioni)]
    public async Task<IActionResult> ImpostaAttivo([FromBody] AccensioneVoci scelta, CancellationToken ct)
    {
        var quante = await _voci.ImpostaAttivoAsync(scelta.IdVoci ?? [], scelta.Attivo, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Manualistica, scelta.Attivo ? "voci.accendi" : "voci.spegni",
            EsitoOperazione.Successo, dettaglio: new { Domande = quante }, ct: ct);

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

/// <summary>Spegne o riaccende una o piu' domande.</summary>
public record AccensioneVoci(int[]? IdVoci, bool Attivo);

/// <summary>Una risposta della check list.</summary>
public record RispostaChecklist(string Codice, string? Valore, bool ScrittoAMano);

/// <summary>Una sede del trattamento inviata dalla schermata.</summary>
public record SedeInviata(
    int Id, string? Nome, string? Indirizzo, string? Cap, string? Comune, string? Provincia);

/// <summary>Gli insediamenti scelti fra quelli proposti da ArcLab.</summary>
public record SediScelte(string[]? Codici);

/// <summary>Un autorizzato inviato dalla schermata.</summary>
/// <summary>Un archivio inviato dalla schermata.</summary>
public record ArchivioInviato(
    int Id, string? Tipo, string? Luogo, string? SistemaOperativo, string? Antivirus,
    string? Backup, string? FrequenzaBackup, string? LuogoConservazione,
    string[]? Accessi, string[]? Interessati, string[]? CategorieDati, int[]? Gestionali);

/// <summary>Un gestionale inviato dalla schermata.</summary>
public record GestionaleInviato(
    int Id, string? Prodotto, string? MisureSicurezza, string? Manutenzione,
    string? Backup, string? Frequenza, string? LuogoConservazione);

/// <summary>Una casella di posta inviata dalla schermata.</summary>
public record CasellaInviata(int Id, string? Indirizzo, string[]? Funzioni);

/// <summary>Richiesta di invio dei documenti al cliente.</summary>
public record InvioRichiesto(string? Destinatari, int[]? Documenti, string? Messaggio);

public record AutorizzatoInviato(
    int Id, int IdQualifica, string? Nome, string? Cognome, int Quantita, string[]? Funzioni);

/// <summary>Una check list nuova: da un cliente di ArcLab, o a mano per un cliente nuovo.</summary>
public record NuovaCompilazione(
    string? Tipo, int? IdClienteArcLab, string? RagioneSociale, DateTime? DataDocumenti, int? Revisione);
