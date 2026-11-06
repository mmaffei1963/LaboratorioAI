using LaboratorioAI.Core.Andamento;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Clienti;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Provvigioni;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// «Clienti da seguire»: clienti con fatturato di laboratorio in calo, sezione Legionella, clienti che comprano solo
/// consulenza, promemoria; scheda cliente con registro delle azioni e proposta di assegnazione al commerciale.
/// Legge AZW (già importato) e la copia dei campioni ArcLab; scrive solo nel database di LabAI.
/// </summary>
[RichiedePermesso(Permessi.ClientiSeguire)]
public class ClientiDaSeguireController : Controller
{
    private readonly ContestoLaboratorioAI _db;
    private readonly ServizioClientiDaSeguire _servizio;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IEsportatoreClientiDaSeguire _esportatore;
    private readonly ImportatoreAzw _importatore;
    private readonly ServizioProvvigioniLab _provvigioni;
    private readonly ServizioSubentri _subentri;

    public ClientiDaSeguireController(ContestoLaboratorioAI db, ServizioClientiDaSeguire servizio, IContestoUtente utente,
        IServizioAudit audit, IEsportatoreClientiDaSeguire esportatore, ImportatoreAzw importatore, ServizioProvvigioniLab provvigioni,
        ServizioSubentri subentri)
    {
        _subentri = subentri;
        _importatore = importatore;
        _provvigioni = provvigioni;
        _db = db;
        _servizio = servizio;
        _utente = utente;
        _audit = audit;
        _esportatore = esportatore;
    }

    // ── Elenco ────────────────────────────────────────────────────────

    /// <summary>Periodo: "12", "6", "3" mesi o "anno" (da gennaio). Confronto: 1 o 2 anni prima.</summary>
    public static FinestraConfronto Finestra(string? periodo, int? confronto) => new()
    {
        Mesi = periodo switch { "6" => 6, "3" => 3, _ => 12 },
        DaInizioAnno = periodo == "anno",
        AnniIndietro = confronto == 2 ? 2 : 1
    };

    public async Task<IActionResult> Index(string? vista, string? semaforo, string? stato, string? filtro, string? commerciale,
        string? responsabile, string? intermediari, string? periodo, int? confronto, CancellationToken ct)
    {
        ViewData["Titolo"] = "Clienti da seguire";
        vista = vista is "legionella" or "consulenza" or "promemoria" or "tolti" ? vista : "clienti";
        periodo = periodo is "6" or "3" or "anno" ? periodo : "12";
        confronto = confronto == 2 ? 2 : 1;
        var r = await _servizio.CalcolaAsync(DateTime.Today, Finestra(periodo, confronto), ct);
        var mio = await CommercialeUtenteAsync(ct);

        InfoCliente I(string c) => r.Info.TryGetValue(c, out var x) ? x : new InfoCliente { Codice = c, RagioneSociale = $"Cliente {c}" };
        bool Passa(string cod)
        {
            var i = I(cod);
            if (i.Tolto) return false;   // tolto dalla lista dal commerciale (14/10/2026)
            if (!string.IsNullOrWhiteSpace(filtro))
            {
                var t = filtro.Trim();
                if (!(cod.Contains(t, StringComparison.OrdinalIgnoreCase) || i.RagioneSociale.Contains(t, StringComparison.OrdinalIgnoreCase)
                      || (i.Citta ?? "").Contains(t, StringComparison.OrdinalIgnoreCase))) return false;
            }
            if (!string.IsNullOrEmpty(commerciale))
            {
                if (commerciale == "nessuno" && (i.CommercialeAzw is not null || i.Assegnato is not null)) return false;
                if (commerciale == "miei" && !(mio is not null && (i.CommercialeAzw == mio.Nome || i.Assegnato == mio.Nome))) return false;
                if (commerciale is not ("nessuno" or "miei") && i.CommercialeAzw != commerciale && i.Assegnato != commerciale) return false;
            }
            if (!string.IsNullOrEmpty(responsabile) && !string.Equals(i.Zona, responsabile, StringComparison.OrdinalIgnoreCase)) return false;
            if (intermediari == "esclusi" && i.Intermediario) return false;
            if (intermediari == "solo" && !i.Intermediario) return false;
            return true;
        }

        var conSemaforo = r.Risultato.Clienti.Where(c => c.Semaforo != Semaforo.Nessuno).ToList();
        semaforo ??= "da-seguire";
        var clienti = conSemaforo.Where(c => semaforo switch
        {
            "rosso" => c.Semaforo == Semaforo.Rosso,
            "giallo" => c.Semaforo == Semaforo.Giallo,
            "verde" => c.Semaforo == Semaforo.Verde,
            "tutti" => true,
            _ => c.Semaforo is Semaforo.Rosso or Semaforo.Giallo
        }).Where(c => Passa(c.Cliente)).ToList();

        stato ??= "da-seguire";
        var legionella = r.Risultato.Legionella.Where(l => stato switch
        {
            "perso" => l.Stato == StatoLegionella.Perso,
            "ritardo" => l.Stato == StatoLegionella.InRitardo,
            "calo" => l.Stato == StatoLegionella.InCalo,
            "ricordare" => l.Stato == StatoLegionella.DaRicordare,
            "nuovo" => l.Stato == StatoLegionella.Nuovo,
            "regolare" => l.Stato == StatoLegionella.Regolare,
            "tutti" => true,
            _ => l.Stato is StatoLegionella.Perso or StatoLegionella.InRitardo or StatoLegionella.InCalo or StatoLegionella.DaRicordare
        }).Where(l => Passa(l.Cliente)).ToList();

        var consulenza = r.Risultato.Clienti.Where(c => c.SoloConsulenza && Passa(c.Cliente)).OrderByDescending(c => c.AltroUltimi).ToList();
        var promemoria = (await _servizio.PromemoriaAsync(DateTime.Today.AddDays(7), ct)).Where(a => Passa(a.CodiceCliente)).ToList();
        var daRicordare = r.Risultato.Legionella.Where(l => l.Stato == StatoLegionella.DaRicordare && Passa(l.Cliente))
            .OrderBy(l => l.ProssimoAtteso).ToList();

        var tolti = await _servizio.ToltiAsync(r.Info, ct);
        var commerciali = await _db.Commerciali.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome).Select(c => c.Nome).ToListAsync(ct);
        var responsabili = await _db.ResponsabiliCliente.AsNoTracking().Where(x => x.Attivo).OrderBy(x => x.Sigla)
            .Select(x => new ValueTuple<string, string>(x.Sigla, x.Nome ?? x.Sigla)).ToListAsync(ct);

        return View(new ModelloClientiDaSeguire(r, vista, semaforo, stato, filtro, commerciale, responsabile, intermediari, periodo, confronto.Value,
            clienti, legionella, consulenza, promemoria, daRicordare, commerciali, responsabili, mio?.Nome,
            Conteggi: new Dictionary<string, int>
            {
                ["rosso"] = conSemaforo.Count(c => c.Semaforo == Semaforo.Rosso),
                ["giallo"] = conSemaforo.Count(c => c.Semaforo == Semaforo.Giallo),
                ["verde"] = conSemaforo.Count(c => c.Semaforo == Semaforo.Verde),
                ["perso"] = r.Risultato.Legionella.Count(l => l.Stato == StatoLegionella.Perso),
                ["ricordare"] = r.Risultato.Legionella.Count(l => l.Stato == StatoLegionella.DaRicordare),
                ["consulenza"] = r.Risultato.Clienti.Count(c => c.SoloConsulenza),
                ["promemoria"] = promemoria.Count(a => a.ProssimoContatto <= DateTime.Today),
                ["tolti"] = tolti.Count(t => !t.Info.Rientrato)
            },
            PerditaRossi: conSemaforo.Where(c => c.Semaforo == Semaforo.Rosso).Sum(c => c.Perdita))
        {
            // Cambi di gestione (12/10/2026): esito dei subentri per i clienti con sedi Legionella perse.
            Subentri = vista == "legionella" ? await SubentriPerClienteAsync(Finestra(periodo, confronto), ct) : null,
            Tolti = vista == "tolti"
                ? tolti.Where(t => string.IsNullOrWhiteSpace(filtro) || t.Info.RagioneSociale.Contains(filtro.Trim(), StringComparison.OrdinalIgnoreCase)
                                   || t.Uscita.CodiceCliente.Contains(filtro.Trim(), StringComparison.OrdinalIgnoreCase)).ToList()
                : new()
        });
    }

    public async Task<IActionResult> Esporta(string? periodo, int? confronto, CancellationToken ct)
    {
        var r = await _servizio.CalcolaAsync(DateTime.Today, Finestra(periodo, confronto), ct);
        await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.esporta", EsitoOperazione.Successo, ct: ct);
        return File(_esportatore.Genera(r), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _esportatore.NomeFile());
    }

    // ── Scheda cliente ────────────────────────────────────────────────

    public async Task<IActionResult> Scheda(string id, string? periodo, int? confronto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id)) return RedirectToAction(nameof(Index));
        var s = await _servizio.SchedaAsync(id.Trim(), DateTime.Today, Finestra(periodo, confronto), ct);
        if (s is null) return NotFound();
        ViewData["Titolo"] = s.Info.RagioneSociale;
        var commerciali = await _db.Commerciali.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome).ToListAsync(ct);
        var mio = await CommercialeUtenteAsync(ct);
        // Commerciale proposto (14/10/2026): chi sta usando la pagina se è un commerciale, altrimenti il commerciale
        // di laboratorio (accordo provvigioni di tipo Laboratorio attivo), perché qui si seguono le analisi.
        var idAttivi = commerciali.Select(c => c.IdCommerciale).ToList();
        var predefinito = mio?.IdCommerciale
            ?? await _db.AccordiProvvigioni.AsNoTracking()
                .Where(a => a.Stato == StatoAccordo.Attivo && a.Tipo == TipoAccordo.Laboratorio && idAttivi.Contains(a.IdCommerciale))
                .OrderByDescending(a => a.ValidoDal).Select(a => (int?)a.IdCommerciale).FirstOrDefaultAsync(ct);
        return View(new ModelloSchedaCliente(s, commerciali, mio, _utente.HaPermesso(Permessi.ClientiGestisci),
            periodo is "6" or "3" or "anno" ? periodo : "12", confronto == 2 ? 2 : 1) { CommercialePredefinito = predefinito });
    }

    [HttpPost]
    public async Task<IActionResult> Azione(string codice, DateTime? dataAzione, TipoAzioneCliente tipo, EsitoAzioneCliente esito,
        string? note, DateTime? prossimoContatto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codice)) return RedirectToAction(nameof(Index));
        var data = dataAzione?.Date ?? DateTime.Today;
        if (data > DateTime.Today.AddDays(1) || data.Year < 2000)
        {
            TempData["Messaggio"] = "Data dell'azione non valida.";
            return RedirectToAction(nameof(Scheda), new { id = codice });
        }
        var a = await _servizio.RegistraAzioneAsync(codice.Trim(), data, tipo, esito, note, prossimoContatto,
            _utente.IdUtente, _utente.NomeCompleto ?? _utente.Email, ct);
        await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.azione", EsitoOperazione.Successo, "AzioneCliente",
            a.IdAzioneCliente.ToString(), new { a.CodiceCliente, a.Tipo, a.Esito, a.ProssimoContatto }, ct);
        TempData["Messaggio"] = "Azione registrata.";
        return RedirectToAction(nameof(Scheda), new { id = codice });
    }

    [HttpPost]
    public async Task<IActionResult> AnnullaAzione(int id, string codice, CancellationToken ct)
    {
        var a = await _db.AzioniClienti.AsNoTracking().FirstOrDefaultAsync(x => x.IdAzioneCliente == id, ct);
        // Annulla chi l'ha inserita, oppure chi gestisce la pagina.
        if (a is not null && (a.IdUtente == _utente.IdUtente || _utente.HaPermesso(Permessi.ClientiGestisci)))
        {
            await _servizio.AnnullaAzioneAsync(id, _utente.NomeCompleto ?? _utente.Email, ct);
            await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.azione.annulla", EsitoOperazione.Successo, "AzioneCliente", id.ToString(), ct: ct);
            TempData["Messaggio"] = "Azione annullata.";
        }
        else TempData["Messaggio"] = "Si può annullare solo un'azione inserita da te.";
        return RedirectToAction(nameof(Scheda), new { id = codice });
    }

    [HttpPost]
    public async Task<IActionResult> Proponi(string codice, int? idCommerciale, string? motivo, CancellationToken ct)
    {
        var gestore = _utente.HaPermesso(Permessi.ClientiGestisci);
        var mio = await CommercialeUtenteAsync(ct);
        // Il commerciale propone per sé; chi gestisce può proporre per qualsiasi commerciale.
        var id = gestore && idCommerciale is int scelto ? scelto : mio?.IdCommerciale;
        if (id is null)
        {
            TempData["Messaggio"] = "Il tuo utente non è collegato a un commerciale (Amministrazione → Provvigioni).";
            return RedirectToAction(nameof(Scheda), new { id = codice });
        }
        var scheda = await _servizio.SchedaAsync(codice, DateTime.Today, null, ct);
        var errore = await _servizio.ProponiAssegnazioneAsync(codice, id.Value, motivo, scheda?.Andamento?.LabUltimi,
            _utente.NomeCompleto ?? _utente.Email, ct);
        if (errore is null)
            await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.assegnazione.proposta", EsitoOperazione.Successo, "Cliente", codice,
                new { IdCommerciale = id, motivo }, ct);
        TempData["Messaggio"] = errore ?? "Assegnazione proposta: vale dopo l'approvazione.";
        return RedirectToAction(nameof(Scheda), new { id = codice });
    }

    // ── Clienti tolti dalla lista (14/10/2026) ────────────────────────

    [HttpPost]
    public async Task<IActionResult> Togli(string codice, MotivoUscitaCliente motivo, string? successore, string? note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codice)) return RedirectToAction(nameof(Index));
        // Dalla tendina può arrivare «Ragione sociale — codice»: si tiene il codice.
        if (successore is not null && successore.LastIndexOf(" — ", StringComparison.Ordinal) is int k and >= 0) successore = successore[(k + 3)..];
        var errore = await _servizio.TogliAsync(codice, motivo, successore, note, _utente.IdUtente, _utente.NomeCompleto ?? _utente.Email, ct);
        if (errore is null)
            await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.togli", EsitoOperazione.Successo, "Cliente", codice.Trim(),
                new { motivo, successore, note }, ct);
        TempData["Messaggio"] = errore ?? (motivo == MotivoUscitaCliente.AcquisitoDaCliente
            ? "Cliente tolto dalla lista: il suo storico ora si somma a quello del cliente che l'ha acquisito."
            : "Cliente tolto dalla lista. Se torna a lavorare con noi ricompare da solo.");
        return RedirectToAction(nameof(Scheda), new { id = codice.Trim() });
    }

    [HttpPost]
    public async Task<IActionResult> Rimetti(string codice, CancellationToken ct)
    {
        var n = await _servizio.RimettiAsync(codice.Trim(), _utente.NomeCompleto ?? _utente.Email, ct);
        if (n > 0) await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.rimetti", EsitoOperazione.Successo, "Cliente", codice.Trim(), ct: ct);
        TempData["Messaggio"] = n > 0 ? "Cliente rimesso in lista." : "Il cliente non era tolto dalla lista.";
        return RedirectToAction(nameof(Scheda), new { id = codice.Trim() });
    }

    [HttpGet]
    public async Task<IActionResult> CercaClienti(string? q, CancellationToken ct) =>
        Json((await _servizio.CercaClientiAsync(q ?? "", ct)).Select(c => new { codice = c.Codice, nome = c.Nome, citta = c.Citta }));

    [HttpPost]
    public async Task<IActionResult> Attributi(string codice, bool intermediario, string? note, CancellationToken ct)
    {
        await _servizio.SalvaAttributiAsync(codice, intermediario, note, _utente.NomeCompleto ?? _utente.Email, ct);
        await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.attributi", EsitoOperazione.Successo, "Cliente", codice,
            new { intermediario }, ct);
        TempData["Messaggio"] = "Dati del cliente salvati.";
        return RedirectToAction(nameof(Scheda), new { id = codice });
    }

    // ── Importazione (gestione) ───────────────────────────────────────

    /// <summary>Importa da AZW (sola lettura) fatture, incassi e note, e da ArcLab commesse e campioni (sola lettura).</summary>
    [HttpPost, RichiedePermesso(Permessi.ClientiGestisci)]
    public async Task<IActionResult> Importa(string? vista, string? periodo, int? confronto, CancellationToken ct)
    {
        var esito = await _importatore.ImportaAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.importazione.azw",
            esito.Riuscita ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            dettaglio: new { esito.Fatture, esito.Incassi, esito.NoteCredito, esito.Messaggio, Da = "Clienti da seguire" }, ct: ct);
        var nuove = esito.Riuscita ? await _provvigioni.SincronizzaConvenzioniAsync(ct) : 0;
        var campioni = esito.Riuscita ? await _db.CampioniMensiliArcLab.CountAsync(ct) : 0;
        TempData["Messaggio"] = esito.Riuscita
            ? $"Importazione riuscita: {esito.Fatture} fatture, {esito.Incassi} incassi, {esito.NoteCredito} note di credito, {campioni} righe di campioni ArcLab."
              + (nuove > 0 ? $" Nuove convenzioni da ArcLab: {nuove} (da controllare in Provvigioni → Convenzioni)." : "")
            : $"Importazione fallita: {esito.Messaggio}";
        return RedirectToAction(nameof(Index), new { vista, periodo, confronto });
    }

    // ── Assegnazioni e soglie (gestione) ──────────────────────────────

    [RichiedePermesso(Permessi.ClientiGestisci)]
    public async Task<IActionResult> Assegnazioni(CancellationToken ct)
    {
        ViewData["Titolo"] = "Clienti da seguire: assegnazioni e soglie";
        var elenco = await _servizio.AssegnazioniAsync(ct);
        var imp = await _servizio.ImpostazioniAsync(ct);
        var basi = await _db.BasiCalcolo.AsNoTracking().Where(b => b.Attivo).OrderBy(b => b.Nome).ToListAsync(ct);
        return View(new ModelloAssegnazioni(elenco, imp, basi));
    }

    [HttpPost, RichiedePermesso(Permessi.ClientiGestisci)]
    public async Task<IActionResult> Decidi(int id, bool approva, string? nota, CancellationToken ct)
    {
        var a = await _servizio.DecidiAssegnazioneAsync(id, approva, nota, _utente.NomeCompleto ?? _utente.Email, ct);
        if (a is not null)
            await _audit.RegistraAsync(AreaFunzionale.Clienti, approva ? "clienti.assegnazione.approva" : "clienti.assegnazione.rifiuta",
                EsitoOperazione.Successo, "AssegnazioneCliente", id.ToString(), new { a.CodiceCliente, a.IdCommerciale, nota }, ct);
        TempData["Messaggio"] = a is null ? "Proposta non trovata o già decisa." : approva ? "Assegnazione approvata." : "Proposta rifiutata.";
        return RedirectToAction(nameof(Assegnazioni));
    }

    [HttpPost, RichiedePermesso(Permessi.ClientiGestisci)]
    public async Task<IActionResult> ChiudiAssegnazione(int id, string? nota, CancellationToken ct)
    {
        var a = await _servizio.ChiudiAssegnazioneAsync(id, nota, _utente.NomeCompleto ?? _utente.Email, ct);
        if (a is not null)
            await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.assegnazione.chiudi", EsitoOperazione.Successo,
                "AssegnazioneCliente", id.ToString(), new { a.CodiceCliente, a.IdCommerciale, nota }, ct);
        TempData["Messaggio"] = a is null ? "Assegnazione non trovata o non in corso." : "Assegnazione chiusa da oggi.";
        return RedirectToAction(nameof(Assegnazioni));
    }

    [HttpPost, RichiedePermesso(Permessi.ClientiGestisci)]
    public async Task<IActionResult> Soglie(string? minimoFatturato, string? sogliaRosso, string? sogliaGiallo, string? valoreAnalisiPerse,
        int campioniTrimestreMinimo, int legionellaTolleranzaGiorni, int legionellaPreavvisoGiorni, string? legionellaSogliaCalo, string? soloConsulenzaMinimo,
        string? soloConsulenzaLabMassimo, string? testoControlloLegionella, string? vociLegionella, int? idBaseCalcoloLaboratorio, CancellationToken ct)
    {
        var v = new ImpostazioniAndamento
        {
            MinimoFatturato = Numero(minimoFatturato) ?? -1m,
            SogliaRosso = Numero(sogliaRosso) ?? 1m,
            SogliaGiallo = Numero(sogliaGiallo) ?? 1m,
            ValoreAnalisiPerse = Numero(valoreAnalisiPerse) ?? -1m,
            CampioniTrimestreMinimo = campioniTrimestreMinimo,
            LegionellaTolleranzaGiorni = legionellaTolleranzaGiorni,
            LegionellaPreavvisoGiorni = legionellaPreavvisoGiorni,
            LegionellaSogliaCalo = Numero(legionellaSogliaCalo) ?? 1m,
            SoloConsulenzaMinimo = Numero(soloConsulenzaMinimo) ?? -1m,
            SoloConsulenzaLabMassimo = Numero(soloConsulenzaLabMassimo) ?? -1m,
            TestoControlloLegionella = testoControlloLegionella ?? "",
            VociLegionella = vociLegionella ?? "",
            IdBaseCalcoloLaboratorio = idBaseCalcoloLaboratorio
        };
        if (v.MinimoFatturato < 0 || v.SogliaRosso >= 0 || v.SogliaGiallo >= 0 || v.SogliaRosso > v.SogliaGiallo || v.ValoreAnalisiPerse < 0
            || v.CampioniTrimestreMinimo < 1 || v.LegionellaTolleranzaGiorni < 0 || v.LegionellaPreavvisoGiorni < 0
            || v.LegionellaSogliaCalo >= 0 || v.LegionellaSogliaCalo <= -100
            || v.SoloConsulenzaMinimo < 0 || v.SoloConsulenzaLabMassimo < 0)
        {
            TempData["Messaggio"] = "Valori non validi: le soglie del rosso, del giallo e della Legionella «in calo» sono percentuali negative (il rosso più bassa del giallo), gli altri valori non negativi.";
            return RedirectToAction(nameof(Assegnazioni));
        }
        await _servizio.SalvaImpostazioniAsync(v, _utente.NomeCompleto ?? _utente.Email, ct);
        await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.soglie", EsitoOperazione.Successo, "ImpostazioniAndamento", null,
            new { v.MinimoFatturato, v.SogliaRosso, v.SogliaGiallo, v.ValoreAnalisiPerse, v.CampioniTrimestreMinimo,
                  v.LegionellaTolleranzaGiorni, v.LegionellaPreavvisoGiorni, v.LegionellaSogliaCalo, v.SoloConsulenzaMinimo, v.SoloConsulenzaLabMassimo }, ct);
        TempData["Messaggio"] = "Soglie salvate: valgono da subito.";
        return RedirectToAction(nameof(Assegnazioni));
    }

    // ── Utilità ───────────────────────────────────────────────────────

    private async Task<SubentriLegionella?> SubentriPerClienteAsync(FinestraConfronto finestra, CancellationToken ct)
    {
        var r = await _subentri.CalcolaAsync(DateTime.Today, finestra, ct);
        if (r.InsediamentiCopiati == 0) return null;
        var persi = r.Risultato.Persi;
        return new SubentriLegionella(TestiSubentri.PerCliente(r), persi.Count, persi.Count(p => p.NonPerso),
            persi.Count(p => p.Esito == EsitoSubentro.SubentratoNonAttivo), persi.Count(p => p.DaVerificare));
    }

    private async Task<Commerciale?> CommercialeUtenteAsync(CancellationToken ct) =>
        _utente.IdUtente is int id
            ? await _db.Commerciali.AsNoTracking().FirstOrDefaultAsync(c => c.IdUtente == id && c.Attivo, ct)
            : null;

    private static decimal? Numero(string? s) =>
        decimal.TryParse((s ?? "").Trim().Replace(" ", "").Replace("%", "").Replace("€", "").Replace(',', '.'),
            System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
}

public sealed record ModelloClientiDaSeguire(
    RiepilogoClientiDaSeguire Riepilogo, string Vista, string Semaforo, string Stato, string? Filtro, string? Commerciale,
    string? Responsabile, string? Intermediari, string Periodo, int Confronto,
    List<AndamentoCliente> Clienti, List<AndamentoLegionella> Legionella, List<AndamentoCliente> Consulenza,
    List<AzioneCliente> Promemoria, List<AndamentoLegionella> DaRicordare,
    List<string> Commerciali, List<(string Sigla, string Nome)> Responsabili, string? MioCommerciale,
    Dictionary<string, int> Conteggi, decimal PerditaRossi)
{
    /// <summary>Cambi di gestione per cliente (solo nella vista Legionella).</summary>
    public SubentriLegionella? Subentri { get; init; }

    /// <summary>Clienti tolti dalla lista (solo nella vista «tolti», 14/10/2026).</summary>
    public List<(InfoCliente Info, ClienteTolto Uscita)> Tolti { get; init; } = new();

    public InfoCliente Info(string codice) =>
        Riepilogo.Info.TryGetValue(codice, out var i) ? i : new InfoCliente { Codice = codice, RagioneSociale = $"Cliente {codice}" };
}

public sealed record ModelloSchedaCliente(SchedaClienteDaSeguire Scheda, List<Commerciale> Commerciali, Commerciale? MioCommerciale, bool PuoGestire,
    string Periodo, int Confronto)
{
    /// <summary>Commerciale scelto in partenza nella proposta di assegnazione.</summary>
    public int? CommercialePredefinito { get; init; }
}

public sealed record ModelloAssegnazioni(
    List<(AssegnazioneCliente Assegnazione, string Commerciale, string Cliente)> Elenco,
    ImpostazioniAndamento Impostazioni, List<BaseCalcolo> Basi);

/// <summary>Riassunto dei cambi di gestione per la sezione Legionella (12/10/2026).</summary>
public sealed record SubentriLegionella(Dictionary<string, (string Testo, string Classe)> PerCliente, int Sedi, int NonPerse, int NuoviDaChiamare, int DaVerificare);
