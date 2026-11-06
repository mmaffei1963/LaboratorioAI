using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Triage e rinomina dei certificati (Rapporti di Prova) dei laboratori esterni:
/// estrazione AI → aggancio ArcLab (sola lettura) → nome proposto + criticità →
/// conferma avviso → deposito nella cartella di prelievo Palomar e smistamento.
/// Due sorgenti: caricamento dal browser e cartella di rete (PDF già sul server).
/// </summary>
[RichiedePermesso(Permessi.CertificatiEsterniGestione)]
public class CertificatiEsterniController : Controller
{
    private readonly IServizioCertificatiEsterni _servizio;
    private readonly IConfiguration _config;
    private readonly IServizioAudit _audit;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly ServizioRegistroDocumenti _registro;
    private readonly IRepositoryCertificatiEsterni _arclab;

    public CertificatiEsterniController(IServizioCertificatiEsterni servizio, IConfiguration config,
        IServizioAudit audit, ContestoLaboratorioAI ctx, IContestoUtente utente, ServizioRegistroDocumenti registro,
        IRepositoryCertificatiEsterni arclab)
    {
        _registro = registro;
        _arclab = arclab;
        _servizio = servizio;
        _config = config;
        _audit = audit;
        _ctx = ctx;
        _utente = utente;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Titolo"] = "Certificati esterni";
        ViewData["CartellaArrivo"] = await CartellaArrivoAsync(ct);
        ViewData["CartellaPalomar"] = await CartellaPalomarAsync(ct) ?? "";
        ViewData["CartellaPalomarMisti"] = await CartellaPalomarMistiAsync(ct) ?? "";
        ViewData["CartellaArchivio"] = _config["CertificatiEsterni:CartellaArchivio"] ?? "";
        return View();
    }

    // ── Cartella di rete (PDF originali) ────────────────────────────
    [HttpGet("api/certificati-esterni/cartella")]
    public async Task<IActionResult> Cartella([FromQuery] string? percorso, CancellationToken ct)
    {
        var cartella = string.IsNullOrWhiteSpace(percorso)
            ? await CartellaArrivoAsync(ct)
            : percorso!;
        try
        {
            var files = Directory.Exists(cartella)
                ? new DirectoryInfo(cartella).GetFiles("*.pdf")
                    .OrderByDescending(f => f.LastWriteTime).Take(200)
                    .Select(f => new { f.Name, Percorso = f.FullName }).ToList()
                : null;
            return Json(new { cartella, accessibile = files != null, files });
        }
        catch (Exception ex)
        {
            return Json(new { cartella, accessibile = false, errore = ex.Message, files = (object?)null });
        }
    }

    // ── Analisi ─────────────────────────────────────────────────────
    [HttpPost("api/certificati-esterni/analizza")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Analizza(IFormFile? file, [FromForm] bool rianalizza, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Nessun file caricato." });
        var pdf = await LeggiAsync(file, ct);
        try { return Json(await _servizio.AnalizzaAsync(pdf, file.FileName, rianalizza, ct)); }
        catch (Exception ex) { return BadRequest(new { messaggio = "Analisi non riuscita: " + ex.Message }); }
    }

    [HttpPost("api/certificati-esterni/analizza-percorso")]
    public async Task<IActionResult> AnalizzaPercorso([FromBody] RichiestaPercorso req, CancellationToken ct)
    {
        if (!ValidoPdf(req.Percorso)) return BadRequest(new { messaggio = "File non trovato o non è un PDF." });
        var pdf = await System.IO.File.ReadAllBytesAsync(req.Percorso, ct);
        try { return Json(await _servizio.AnalizzaAsync(pdf, Path.GetFileName(req.Percorso), req.Rianalizza, ct)); }
        catch (Exception ex) { return BadRequest(new { messaggio = "Analisi non riuscita: " + ex.Message }); }
    }

    // ── Conferma (scrittura file rinominato) ────────────────────────
    // Ogni conferma scrive una riga nel registro di controllo (log.RegistroDocumenti):
    //   Ok       = copia in Palomar verificata (e, da cartella, originale spostato in "processati");
    //   Parziale = file NON verificato in Palomar, oppure originale non spostato;
    //   Errore   = eccezione, nessun file prodotto.
    // Se Palomar non è verificato l'originale resta in Arrivo, così si può ritentare.
    [HttpPost("api/certificati-esterni/conferma")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Conferma(IFormFile? file, [FromForm] string? nomeFinale,
        [FromForm] string? labAcronimo, [FromForm] string? data,
        [FromForm] string? cartellaPalomar, [FromForm] string? cartellaArchivio,
        [FromForm] int? idCampione, [FromForm] string? numeroCertificato, [FromForm] string? dataCertificato,
        [FromForm] string? laboratorio, [FromForm] string? idEsecuzione,
        [FromForm] string? tipo, [FromForm] int? idLabEsterno, [FromForm] string? cartellaPalomarMisti, CancellationToken ct)
    {
        var tipoCert = TipoDa(tipo);
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Nessun file." });
        if (string.IsNullOrWhiteSpace(nomeFinale)) return BadRequest(new { messaggio = "Nome file mancante." });
        if (CostruttoreNomeCertificatoEsterno.HaSegnaposti(nomeFinale))
            return BadRequest(new { messaggio = "Nel nome ci sono ancora IDMATRICE o IDLAB: sostituirli con gli id corretti (servono a Palomar)." });

        var (esecuzione, notificaSubito) = Esecuzione(idEsecuzione);
        var nome = PulisciNomeFile(nomeFinale!) + ".pdf";
        try
        {
            var pdf = await LeggiAsync(file, ct);
            var s = await SalvaRinominatoAsync(pdf, nome, labAcronimo, data,
                tipoCert == TipoCertificatoEsterno.Misto ? cartellaPalomarMisti : cartellaPalomar, cartellaArchivio, tipoCert, ct);
            var info = new InfoCertificato(tipoCert, idCampione, idLabEsterno);
            if (!s.PalomarOk)
            {
                var msg = "NON consegnato a Palomar: " + s.ErrorePalomar + (s.Archivio is null ? "" : " (copia d'archivio scritta)");
                await RegistraAsync(esecuzione, "upload", nome, EsitoDocumento.Parziale, msg, file.FileName, s, info, ct);
                if (notificaSubito) await _registro.NotificaProblemiAsync(ServizioRegistroDocumenti.AmbitoCertificato, esecuzione, ct);
                return BadRequest(new { messaggio = msg, esito = "Parziale" });
            }

            // Misto: nessun import in ArcLab (i valori si inseriscono a mano nel certificato MARE.A).
            var importProgrammato = tipoCert != TipoCertificatoEsterno.Misto
                && await RegistraDaImportareAsync(idCampione, numeroCertificato, dataCertificato, laboratorio, nome, ct);
            await RegistraAsync(esecuzione, "upload", nome, EsitoDocumento.Ok,
                MessaggioOk(tipoCert, false, importProgrammato), file.FileName, s, info, ct);
            var percorsi = s.Percorsi;
            await _audit.RegistraAsync(AreaFunzionale.CertificatiEsterni, "certificatiesterni.salva", EsitoOperazione.Successo,
                tipoEntita: "CertificatoEsterno", idEntita: nome, dettaglio: new { nome, percorsi, sorgente = "upload", importProgrammato, tipo = tipoCert?.ToString() }, ct: ct);
            return Json(new { nome, percorsi, importProgrammato, esito = "Ok", filePalomar = s.Palomar, tipo = tipoCert?.ToString() });
        }
        catch (Exception ex)
        {
            _ctx.ChangeTracker.Clear();
            await RegistraAsync(esecuzione, "upload", nome, EsitoDocumento.Errore, "Salvataggio non riuscito: " + ex.Message, file.FileName, null,
                new InfoCertificato(tipoCert, idCampione, idLabEsterno), ct);
            if (notificaSubito) await _registro.NotificaProblemiAsync(ServizioRegistroDocumenti.AmbitoCertificato, esecuzione, ct);
            return BadRequest(new { messaggio = "Salvataggio non riuscito: " + ex.Message, esito = "Errore" });
        }
    }

    [HttpPost("api/certificati-esterni/conferma-percorso")]
    public async Task<IActionResult> ConfermaPercorso([FromBody] RichiestaConfermaPercorso req, CancellationToken ct)
    {
        if (!ValidoPdf(req.Percorso)) return BadRequest(new { messaggio = "File originale non trovato." });
        if (string.IsNullOrWhiteSpace(req.NomeFinale)) return BadRequest(new { messaggio = "Nome file mancante." });
        if (CostruttoreNomeCertificatoEsterno.HaSegnaposti(req.NomeFinale))
            return BadRequest(new { messaggio = "Nel nome ci sono ancora IDMATRICE o IDLAB: sostituirli con gli id corretti (servono a Palomar)." });

        var (esecuzione, notificaSubito) = Esecuzione(req.IdEsecuzione);
        var nome = PulisciNomeFile(req.NomeFinale) + ".pdf";
        var tipoCert = TipoDa(req.Tipo);
        var info = new InfoCertificato(tipoCert, req.IdCampione, req.IdLabEsterno);
        try
        {
            var pdf = await System.IO.File.ReadAllBytesAsync(req.Percorso, ct);
            var s = await SalvaRinominatoAsync(pdf, nome, req.LabAcronimo, req.Data,
                tipoCert == TipoCertificatoEsterno.Misto ? req.CartellaPalomarMisti : req.CartellaPalomar, req.CartellaArchivio, tipoCert, ct);
            if (!s.PalomarOk)
            {
                // L'originale resta in Arrivo: si può ritentare.
                var msg = "NON consegnato a Palomar: " + s.ErrorePalomar + ". Originale lasciato nella cartella di arrivo.";
                await RegistraAsync(esecuzione, "cartella", nome, EsitoDocumento.Parziale, msg, req.Percorso, s, info, ct);
                if (notificaSubito) await _registro.NotificaProblemiAsync(ServizioRegistroDocumenti.AmbitoCertificato, esecuzione, ct);
                return BadRequest(new { messaggio = msg, esito = "Parziale" });
            }

            // Sposta l'originale nella cartella dei processati (12/10/2026: separata per solo esterni e misti, da
            // Impostazioni documenti; se non impostata <Arrivo>\processati). Se non riesce, il file in Palomar resta
            // valido ma va segnalato: l'originale rimasto in arrivo potrebbe essere rielaborato.
            string? originaleSpostato = null, avviso = null;
            try
            {
                var dirOrig = Path.GetDirectoryName(req.Percorso)!;
                var processati = await CartellaProcessatiAsync(tipoCert, ct) ?? Path.Combine(dirOrig, "processati");
                Directory.CreateDirectory(processati);
                var destOrig = Path.Combine(processati, Path.GetFileName(req.Percorso));
                if (System.IO.File.Exists(destOrig)) System.IO.File.Delete(destOrig);
                System.IO.File.Move(req.Percorso, destOrig);
                originaleSpostato = destOrig;
            }
            catch (Exception exMove)
            {
                avviso = "Salvato in Palomar, ma originale non spostato nella cartella dei processati (permessi cartella?): " + exMove.Message;
            }

            var importProgrammato = tipoCert != TipoCertificatoEsterno.Misto
                && await RegistraDaImportareAsync(req.IdCampione, req.NumeroCertificato, req.DataCertificato, req.Laboratorio, nome, ct);
            await RegistraAsync(esecuzione, "cartella", nome,
                avviso is null ? EsitoDocumento.Ok : EsitoDocumento.Parziale,
                avviso ?? MessaggioOk(tipoCert, true, importProgrammato),
                req.Percorso, s, info, ct);
            if (avviso is not null && notificaSubito)
                await _registro.NotificaProblemiAsync(ServizioRegistroDocumenti.AmbitoCertificato, esecuzione, ct);
            var percorsi = s.Percorsi;
            await _audit.RegistraAsync(AreaFunzionale.CertificatiEsterni, "certificatiesterni.salva", EsitoOperazione.Successo,
                tipoEntita: "CertificatoEsterno", idEntita: nome,
                dettaglio: new { nome, percorsi, originaleSpostato, sorgente = "cartella", importProgrammato, tipo = tipoCert?.ToString() }, ct: ct);
            return Json(new { nome, percorsi, originaleSpostato, avviso, importProgrammato,
                esito = avviso is null ? "Ok" : "Parziale", filePalomar = s.Palomar, tipo = tipoCert?.ToString() });
        }
        catch (Exception ex)
        {
            _ctx.ChangeTracker.Clear();
            await RegistraAsync(esecuzione, "cartella", nome, EsitoDocumento.Errore, "Salvataggio non riuscito: " + ex.Message, req.Percorso, null, info, ct);
            if (notificaSubito) await _registro.NotificaProblemiAsync(ServizioRegistroDocumenti.AmbitoCertificato, esecuzione, ct);
            return BadRequest(new { messaggio = "Salvataggio non riuscito: " + ex.Message, esito = "Errore" });
        }
    }

    // ── Fine di un "Salva tutti": riepilogo + mail se ci sono problemi ──
    [HttpPost("api/certificati-esterni/fine-esecuzione")]
    public async Task<IActionResult> FineEsecuzione([FromBody] RichiestaFineEsecuzione req, CancellationToken ct)
    {
        if (!IdEsecuzioneValido(req.IdEsecuzione)) return BadRequest(new { messaggio = "Esecuzione non valida." });
        var righe = await _registro.EsecuzioneAsync(req.IdEsecuzione!, ct);
        var mail = await _registro.NotificaProblemiAsync(ServizioRegistroDocumenti.AmbitoCertificato, req.IdEsecuzione!, ct);
        return Json(new
        {
            idEsecuzione = req.IdEsecuzione,
            totale = righe.Count,
            ok = righe.Count(r => r.Esito == EsitoDocumento.Ok),
            parziali = righe.Count(r => r.Esito == EsitoDocumento.Parziale),
            errori = righe.Count(r => r.Esito == EsitoDocumento.Errore),
            emailInviata = mail
        });
    }

    // ── Registro di controllo + PDF ancora fermi nella cartella di arrivo ──
    // Le righe annullate (12/10/2026) restano nel registro: si nascondono, salvo "mostra annullati".
    // Per i certificati misti si legge ArcLab (sola lettura) per dire se i valori sono stati inseriti.
    [HttpGet("api/certificati-esterni/registro")]
    public async Task<IActionResult> Registro([FromQuery] int? giorni, [FromQuery] bool soloProblemi,
        [FromQuery] bool mostraAnnullati, [FromQuery] bool soloDaInserire, CancellationToken ct)
    {
        var tutte = await _registro.ElencoAsync(ServizioRegistroDocumenti.AmbitoCertificato, giorni ?? 7, false, ct);

        // Annullamenti: la riga più recente per ogni riga annullata (l'elenco è dal più recente).
        var annulli = tutte.Where(r => r.Esito == EsitoDocumento.Annullato && r.IdRegistroCollegato != null)
            .GroupBy(r => r.IdRegistroCollegato!.Value).ToDictionary(g => g.Key, g => g.First());
        var righe = tutte.Where(r => r.Esito != EsitoDocumento.Annullato).ToList();
        if (!mostraAnnullati) righe = righe.Where(r => !annulli.ContainsKey(r.IdRegistroDocumento)).ToList();
        if (soloProblemi) righe = righe.Where(r => r.Esito is EsitoDocumento.Errore or EsitoDocumento.Parziale).ToList();

        // Un problema è "risolto" se lo stesso nome è stato poi consegnato con esito Ok (e non annullato).
        var nomi = righe.Where(x => x.Esito != EsitoDocumento.Ok && x.Riferimento != null)
            .Select(x => x.Riferimento!).Distinct().ToList();
        var nomiOk = (await _ctx.RegistroDocumenti.AsNoTracking()
            .Where(r => r.Ambito == ServizioRegistroDocumenti.AmbitoCertificato && r.Esito == EsitoDocumento.Ok)
            .Where(r => r.Riferimento != null && nomi.Contains(r.Riferimento))
            .Select(r => new { r.Riferimento, r.IdRegistroDocumento })
            .ToListAsync(ct))
            .Where(o => !annulli.ContainsKey(o.IdRegistroDocumento)).ToList();

        // Misti consegnati e non annullati: valori inseriti in ArcLab?
        var misti = righe.Where(r => r.Tipo == TipoCertificatoEsterno.Misto && r.PalomarVerificato
                                     && r.IdCampioneArcLab is int idc && idc != 0 && !annulli.ContainsKey(r.IdRegistroDocumento)).ToList();
        var stati = new Dictionary<int, StatoInserimentoArcLab>();
        string? erroreArcLab = null;
        if (misti.Count > 0)
        {
            try
            {
                var prove = (await _arclab.ProveDeiCampioniAsync(misti.Select(m => m.IdCampioneArcLab!.Value), ct)).ToLookup(p => p.IdCampione);
                foreach (var m in misti)
                    stati[m.IdRegistroDocumento] = ClassificatoreCertificatoEsterno.StatoInserimento(prove[m.IdCampioneArcLab!.Value], m.IdLabEsternoArcLab);
            }
            catch (Exception ex) { erroreArcLab = "Controllo ArcLab non riuscito: " + ex.Message; }
        }
        static bool DaInserire(StatoInserimentoArcLab st) => st.Prove > 0 && !st.Completo;
        if (soloDaInserire) righe = righe.Where(r => stati.TryGetValue(r.IdRegistroDocumento, out var st) && DaInserire(st)).ToList();

        var arrivo = await CartellaArrivoAsync(ct);
        int? inArrivo = null;
        try { if (Directory.Exists(arrivo)) inArrivo = Directory.GetFiles(arrivo, "*.pdf").Length; } catch { }

        return Json(new
        {
            cartellaArrivo = arrivo,
            inArrivo,
            daInserire = stati.Values.Count(DaInserire),
            erroreArcLab,
            righe = righe.Select(r =>
            {
                stati.TryGetValue(r.IdRegistroDocumento, out var st);
                annulli.TryGetValue(r.IdRegistroDocumento, out var an);
                return new
                {
                    r.IdRegistroDocumento,
                    r.IdEsecuzione,
                    DataOra = DateTime.SpecifyKind(r.DataOra, DateTimeKind.Utc),
                    r.Origine,
                    r.Riferimento,
                    Esito = r.Esito.ToString(),
                    r.Messaggio,
                    r.FileOrigine,
                    r.FilePalomar,
                    r.PalomarVerificato,
                    StatoAttuale = r.Esito != EsitoDocumento.Ok && nomiOk.Any(o => o.Riferimento == r.Riferimento && o.IdRegistroDocumento > r.IdRegistroDocumento)
                        ? "Inviato" : null,
                    Tipo = r.Tipo?.ToString(),
                    StatoArcLab = st is null ? null : st.Prove == 0 ? "NonVerificabile" : st.Completo ? "Inserito" : "DaInserire",
                    DettaglioArcLab = st is null ? null
                        : st.Prove == 0 ? "Nessuna prova esterna del laboratorio sul campione in ArcLab: controllare a mano."
                        : st.Completo ? $"Valori inseriti in ArcLab ({st.Inserite}/{st.Prove})."
                        : $"Da inserire in ArcLab ({st.Inserite}/{st.Prove} inseriti): " + string.Join(", ", st.Mancanti) + ".",
                    Annullato = an is not null,
                    AnnullatoIl = an is null ? (DateTime?)null : DateTime.SpecifyKind(an.DataOra, DateTimeKind.Utc),
                    MotivoAnnullamento = an?.Messaggio
                };
            })
        });
    }

    // ── Annullamento di una riga del registro (12/10/2026) ──
    // Nessuna cancellazione: si aggiunge una riga "Annullato" collegata. Inoltre, se possibile:
    //  - il file rinominato ancora nella cartella Palomar (non prelevato) viene spostato in <Arrivo>\annullati;
    //  - l'originale torna dalla cartella dei processati alla cartella di arrivo, per rilavorarlo;
    //  - il certificato esce dalla coda di import ArcLab, se non ancora importato.
    [HttpPost("api/certificati-esterni/registro/{id:int}/annulla")]
    public async Task<IActionResult> Annulla(int id, [FromBody] RichiestaAnnulla req, CancellationToken ct)
    {
        var motivo = (req.Motivo ?? "").Trim();
        if (motivo.Length < 3) return BadRequest(new { messaggio = "Scrivi il motivo dell'annullamento." });
        var riga = await _ctx.RegistroDocumenti.AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdRegistroDocumento == id && r.Ambito == ServizioRegistroDocumenti.AmbitoCertificato, ct);
        if (riga is null) return NotFound(new { messaggio = "Riga del registro non trovata." });
        if (riga.Esito == EsitoDocumento.Annullato) return BadRequest(new { messaggio = "Questa è già una riga di annullamento." });
        if (await _ctx.RegistroDocumenti.AnyAsync(r => r.IdRegistroCollegato == id && r.Esito == EsitoDocumento.Annullato, ct))
            return BadRequest(new { messaggio = "Riga già annullata." });

        var note = new List<string>();
        var arrivo = await CartellaArrivoAsync(ct);

        // 1) File rinominato in Palomar
        if (!string.IsNullOrWhiteSpace(riga.FilePalomar))
        {
            if (System.IO.File.Exists(riga.FilePalomar))
            {
                if (string.IsNullOrWhiteSpace(arrivo))
                    note.Add("file lasciato nella cartella Palomar (cartella di arrivo non configurata): toglierlo a mano");
                else
                {
                    try
                    {
                        var dir = Path.Combine(arrivo, "annullati");
                        Directory.CreateDirectory(dir);
                        var dest = PercorsoLibero(Path.Combine(dir, Path.GetFileName(riga.FilePalomar)));
                        System.IO.File.Move(riga.FilePalomar, dest);
                        note.Add("file tolto dalla cartella Palomar (spostato in " + dest + ")");
                    }
                    catch (Exception ex) { note.Add("file NON tolto dalla cartella Palomar: " + ex.Message); }
                }
            }
            else if (riga.PalomarVerificato)
                note.Add("il file non è più nella cartella Palomar (già prelevato da Palomar o tolto a mano): se è già sul portale o in ArcLab va tolto lì a mano");
        }

        // 2) Originale di nuovo in arrivo
        if (req.RimettiInArrivo && riga.Origine == "cartella" && !string.IsNullOrWhiteSpace(riga.FileOrigine))
        {
            try
            {
                var nomeOrig = Path.GetFileName(riga.FileOrigine);
                var cartelle = new[]
                {
                    await CartellaProcessatiAsync(riga.Tipo, ct),
                    await CartellaProcessatiAsync(riga.Tipo == TipoCertificatoEsterno.Misto ? TipoCertificatoEsterno.SoloEsterni : TipoCertificatoEsterno.Misto, ct),
                    Path.Combine(Path.GetDirectoryName(riga.FileOrigine)!, "processati")
                };
                var processato = cartelle.Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => Path.Combine(c!, nomeOrig)).FirstOrDefault(System.IO.File.Exists);
                if (System.IO.File.Exists(riga.FileOrigine))
                    note.Add("originale già nella cartella di arrivo");
                else if (processato is not null)
                {
                    System.IO.File.Move(processato, riga.FileOrigine);
                    note.Add("originale rimesso nella cartella di arrivo");
                }
                else note.Add("originale non trovato tra i processati");
            }
            catch (Exception ex) { note.Add("originale non spostato: " + ex.Message); }
        }

        // 3) Coda di import ArcLab
        if (!string.IsNullOrWhiteSpace(riga.Riferimento))
        {
            var ora = DateTime.UtcNow;
            var coda = await _ctx.CertificatiEsterniDaImportare
                .Where(x => x.Attivo && x.DataCancellazione == null && x.NomeFile == riga.Riferimento).ToListAsync(ct);
            foreach (var c in coda)
            {
                if (c.Stato == StatoCertificatoEsterno.DaImportare)
                {
                    c.Attivo = false;
                    c.DataCancellazione = ora;
                    c.DataUltimaModifica = ora;
                    note.Add("tolto dalla coda di import ArcLab");
                }
                else note.Add("ATTENZIONE: già importato in ArcLab, va tolto a mano in ArcLab");
            }
            if (coda.Count > 0) await _ctx.SaveChangesAsync(ct);
        }

        var testoNote = note.Count == 0 ? "" : " [" + string.Join("; ", note) + "]";
        await _registro.RegistraAsync(new RegistroDocumento
        {
            Ambito = ServizioRegistroDocumenti.AmbitoCertificato,
            IdEsecuzione = ServizioRegistroDocumenti.NuovaEsecuzione(),
            Origine = "annullamento",
            Riferimento = riga.Riferimento,
            Esito = EsitoDocumento.Annullato,
            Messaggio = motivo + testoNote,
            FileOrigine = riga.FileOrigine,
            FilePalomar = riga.FilePalomar,
            Tipo = riga.Tipo,
            IdCampioneArcLab = riga.IdCampioneArcLab,
            IdLabEsternoArcLab = riga.IdLabEsternoArcLab,
            IdRegistroCollegato = id
        }, ct);
        await _audit.RegistraAsync(AreaFunzionale.CertificatiEsterni, "certificatiesterni.annulla", EsitoOperazione.Successo,
            tipoEntita: "RegistroDocumento", idEntita: id.ToString(), dettaglio: new { riga.Riferimento, motivo, note }, ct: ct);
        return Json(new { messaggio = "Annullato" + (note.Count == 0 ? "." : ": " + string.Join("; ", note) + ".") });
    }

    // ── Helper ──────────────────────────────────────────────────────
    /// <summary>Esito della scrittura: Palomar (verificata) + copia d'archivio.</summary>
    private sealed record EsitoSalvataggio(string? Palomar, bool PalomarOk, string? ErrorePalomar, string? Archivio)
    {
        public List<string> Percorsi => new[] { PalomarOk ? Palomar : null, Archivio }.OfType<string>().ToList();
    }

    /// <summary>Dati del certificato da ricordare nel registro (tipo, campione e laboratorio ArcLab).</summary>
    private sealed record InfoCertificato(TipoCertificatoEsterno? Tipo, int? IdCampione, int? IdLabEsterno);

    private static TipoCertificatoEsterno? TipoDa(string? tipo) =>
        Enum.TryParse<TipoCertificatoEsterno>(tipo, true, out var t) && Enum.IsDefined(t) ? t : null;

    private static string MessaggioOk(TipoCertificatoEsterno? tipo, bool daCartella, bool importProgrammato) =>
        tipo == TipoCertificatoEsterno.Misto
            ? "Misto: in Palomar INTERNI_ESTERNI" + (daCartella ? ", originale spostato" : "") + "; nessun import ArcLab, valori da inserire a mano"
            : "In Palomar" + (daCartella ? ", originale spostato" : "") + (importProgrammato ? "; import ArcLab programmato" : "");

    private static string PercorsoLibero(string percorso)
    {
        if (!System.IO.File.Exists(percorso)) return percorso;
        var dir = Path.GetDirectoryName(percorso)!;
        var nome = Path.GetFileNameWithoutExtension(percorso);
        var est = Path.GetExtension(percorso);
        return Path.Combine(dir, $"{nome}_{DateTime.Now:yyyyMMdd-HHmmss}{est}");
    }

    /// <summary>
    /// Cartella di prelievo Palomar (11/10/2026): prima Impostazioni documenti (CertificatiEsterni.CartellaPalomar), poi
    /// il file di configurazione (CertificatiEsterni:CartellaPalomar), infine la cartella concordata con Palomar.
    /// </summary>
    private async Task<string?> CartellaPalomarAsync(CancellationToken ct)
    {
        var db = (await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == "CertificatiEsterni.CartellaPalomar", ct))?.Valore;
        if (!string.IsNullOrWhiteSpace(db)) return db.Trim();
        var file = _config["CertificatiEsterni:CartellaPalomar"];
        return !string.IsNullOrWhiteSpace(file) ? file : @"\\SRVMAREA\marea\ArcLab\dati\Caricamento_Certificati_Esterni";
    }

    /// <summary>
    /// Cartella Palomar dei certificati misti (12/10/2026, es. ...\Palomar\INTERNI_ESTERNI): Impostazioni documenti
    /// (CertificatiEsterni.CartellaPalomarMisti), poi il file di configurazione. Nessun valore predefinito:
    /// se manca, il salvataggio di un misto non riesce (meglio che finire tra i solo esterni).
    /// </summary>
    private async Task<string?> CartellaPalomarMistiAsync(CancellationToken ct)
    {
        var db = (await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == "CertificatiEsterni.CartellaPalomarMisti", ct))?.Valore;
        if (!string.IsNullOrWhiteSpace(db)) return db.Trim();
        var file = _config["CertificatiEsterni:CartellaPalomarMisti"];
        return string.IsNullOrWhiteSpace(file) ? null : file;
    }

    /// <summary>
    /// Cartella di arrivo dei certificati originali da rinominare (12/10/2026): prima Impostazioni documenti
    /// (CertificatiEsterni.CartellaArrivo), poi il file di configurazione.
    /// </summary>
    private async Task<string> CartellaArrivoAsync(CancellationToken ct)
    {
        var db = (await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == "CertificatiEsterni.CartellaArrivo", ct))?.Valore;
        return !string.IsNullOrWhiteSpace(db) ? db.Trim() : (_config["CertificatiEsterni:CartellaArrivo"] ?? "");
    }

    /// <summary>
    /// Cartella dove spostare gli originali già lavorati (12/10/2026): Impostazioni documenti
    /// CertificatiEsterni.CartellaProcessatiEsterni / CartellaProcessatiMisti. Null = <Arrivo>\processati.
    /// </summary>
    private async Task<string?> CartellaProcessatiAsync(TipoCertificatoEsterno? tipo, CancellationToken ct)
    {
        var chiave = tipo == TipoCertificatoEsterno.Misto ? "CertificatiEsterni.CartellaProcessatiMisti" : "CertificatiEsterni.CartellaProcessatiEsterni";
        var db = (await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == chiave, ct))?.Valore;
        return string.IsNullOrWhiteSpace(db) ? null : db.Trim();
    }

    private async Task<EsitoSalvataggio> SalvaRinominatoAsync(byte[] pdf, string nome, string? labAcronimo, string? data,
        string? palomarOverride, string? archivioOverride, TipoCertificatoEsterno? tipo, CancellationToken ct)
    {
        // 1) Archivio smistato <Archivio>\<LAB>\<data> (se configurato). Un errore qui è un errore vero.
        string? destArchivio = null;
        var archivio = string.IsNullOrWhiteSpace(archivioOverride) ? _config["CertificatiEsterni:CartellaArchivio"] : archivioOverride;
        if (!string.IsNullOrWhiteSpace(archivio))
        {
            var lab = string.IsNullOrWhiteSpace(labAcronimo) ? "ALTRO" : PulisciNomeFile(labAcronimo!);
            var giorno = string.IsNullOrWhiteSpace(data) ? DateTime.Now.ToString("yyyy-MM-dd") : PulisciNomeFile(data!);
            var dir = Path.Combine(archivio, lab, giorno);
            Directory.CreateDirectory(dir);
            destArchivio = Path.Combine(dir, nome);
            await System.IO.File.WriteAllBytesAsync(destArchivio, pdf, ct);
        }

        // 2) Cartella di prelievo Palomar (misti: INTERNI_ESTERNI): scrittura + verifica (esistenza e dimensione).
        var misto = tipo == TipoCertificatoEsterno.Misto;
        var palomar = !string.IsNullOrWhiteSpace(palomarOverride) ? palomarOverride
            : misto ? await CartellaPalomarMistiAsync(ct) : await CartellaPalomarAsync(ct);
        if (misto && string.IsNullOrWhiteSpace(palomar))
            return new EsitoSalvataggio(null, false, "Cartella Palomar dei certificati misti (INTERNI_ESTERNI) non impostata: Impostazioni documenti.", destArchivio);
        var (ok, percorso, errore) = ServizioRegistroDocumenti.ScriviVerificato(palomar, nome, pdf);
        return new EsitoSalvataggio(percorso, ok, errore, destArchivio);
    }

    private Task RegistraAsync(string esecuzione, string origine, string nome, EsitoDocumento esito, string messaggio,
        string? fileOrigine, EsitoSalvataggio? s, InfoCertificato info, CancellationToken ct) =>
        _registro.RegistraAsync(new RegistroDocumento
        {
            Ambito = ServizioRegistroDocumenti.AmbitoCertificato,
            IdEsecuzione = esecuzione,
            Origine = origine,
            Riferimento = nome,
            Esito = esito,
            Messaggio = messaggio,
            FileOrigine = fileOrigine,
            FileArchivio = s?.Archivio,
            FilePalomar = s?.Palomar,
            PalomarVerificato = s?.PalomarOk == true,
            Tipo = info.Tipo,
            IdCampioneArcLab = info.IdCampione is int idc && idc != 0 ? idc : null,
            IdLabEsternoArcLab = info.IdLabEsterno is int il && il != 0 ? il : null
        }, ct);

    /// <summary>
    /// Esecuzione a cui appartiene la conferma. Se il client non la passa (salvataggio isolato) se ne crea
    /// una nuova e l'eventuale mail di errore parte subito; altrimenti parte a fine "Salva tutti".
    /// </summary>
    private static (string Id, bool NotificaSubito) Esecuzione(string? idEsecuzione) =>
        IdEsecuzioneValido(idEsecuzione) ? (idEsecuzione!, false) : (ServizioRegistroDocumenti.NuovaEsecuzione(), true);

    private static bool IdEsecuzioneValido(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 40 && id.All(c => char.IsLetterOrDigit(c) || c == '-');

    /// <summary>
    /// Registra il certificato esterno nella staging di import ArcLab, se agganciato a un campione.
    /// Niente campione = niente import. Evita doppioni per stesso campione + numero.
    /// </summary>
    private async Task<bool> RegistraDaImportareAsync(int? idCampione, string? numeroCertificato,
        string? dataCertificato, string? laboratorio, string nomeFile, CancellationToken ct)
    {
        // Gli id ArcLab possono essere negativi (numerazione casuale stile Access): solo 0/assente = niente campione.
        if (idCampione is not int idc || idc == 0) return false;
        var numero = (numeroCertificato ?? "").Trim();
        if (string.IsNullOrWhiteSpace(numero)) return false;

        var esiste = await _ctx.CertificatiEsterniDaImportare.AnyAsync(
            x => x.Attivo && x.DataCancellazione == null && x.IdCampioneArcLab == idc && x.NumeroCertificato == numero, ct);
        if (esiste) return false;

        var dataCert = DateTime.TryParse(dataCertificato, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d.Date : DateTime.Today;

        var ora = DateTime.UtcNow;
        _ctx.CertificatiEsterniDaImportare.Add(new CertificatoEsternoDaImportare
        {
            IdCampioneArcLab = idc,
            NumeroCertificato = numero.Length > 50 ? numero[..50] : numero,
            DataCertificato = dataCert,
            DataSped = DateTime.Now,
            NomeFile = nomeFile,
            Laboratorio = string.IsNullOrWhiteSpace(laboratorio) ? null : laboratorio!.Trim(),
            Stato = StatoCertificatoEsterno.DaImportare,
            DataCreazione = ora,
            DataUltimaModifica = ora,
            IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    private static async Task<byte[]> LeggiAsync(IFormFile file, CancellationToken ct)
    {
        await using var s = file.OpenReadStream();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static bool ValidoPdf(string? percorso) =>
        !string.IsNullOrWhiteSpace(percorso)
        && percorso!.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
        && System.IO.File.Exists(percorso);

    private static string PulisciNomeFile(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
        return s.Trim().Trim('.');
    }
}

public record RichiestaPercorso
{
    public string Percorso { get; init; } = "";
    public bool Rianalizza { get; init; }
}

public record RichiestaConfermaPercorso
{
    public string Percorso { get; init; } = "";
    public string NomeFinale { get; init; } = "";
    public string? LabAcronimo { get; init; }
    public string? Data { get; init; }
    public string? CartellaPalomar { get; init; }
    public string? CartellaArchivio { get; init; }
    /// <summary>Cartella Palomar dei misti (INTERNI_ESTERNI); vuota = da Impostazioni documenti.</summary>
    public string? CartellaPalomarMisti { get; init; }
    /// <summary>"SoloEsterni" o "Misto" (12/10/2026).</summary>
    public string? Tipo { get; init; }
    public int? IdLabEsterno { get; init; }

    // Aggancio import ArcLab (certificato → campione).
    public int? IdCampione { get; init; }
    public string? NumeroCertificato { get; init; }
    public string? DataCertificato { get; init; }
    public string? Laboratorio { get; init; }

    /// <summary>Esecuzione ("Salva tutti") a cui appartiene la conferma; vuota = salvataggio isolato.</summary>
    public string? IdEsecuzione { get; init; }
}

public record RichiestaAnnulla
{
    public string? Motivo { get; init; }
    /// <summary>Riporta l'originale da "processati" alla cartella di arrivo, per rilavorarlo.</summary>
    public bool RimettiInArrivo { get; init; } = true;
}

public record RichiestaFineEsecuzione
{
    public string? IdEsecuzione { get; init; }
}
