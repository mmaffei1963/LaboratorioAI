using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Ordine di Accettazione: documento generato da LaboratorioAI leggendo i lotti già ricevuti in
/// ArcLab (LabAnalisiDati, SOLA LETTURA). Riepilogo campioni + prezzi + allegato tecnico, su carta
/// intestata (Word) o PDF. Il PDF è poi pubblicato sul portale Palomar (invio mail massive Brevo).
/// Generazione manuale (bottone) e schedulata, entrambe con guardia anti-doppione per lotto.
/// MAI scrittura su ArcLab.
/// </summary>
[RichiedePermesso(Permessi.OrdiniAccettazioneGestione)]
public class OrdiniAccettazioneController : Controller
{
    private const string ModelloCartaIntestata = "docx/Carta_intestata_Marea.docx";
    private const string ChiaveRevisione = "Preventivi.RevisioneCondizioni";      // condivisa con le offerte
    private const string RevisioneDefault = "Mod. Com. 05 Rev. 19";
    private const string ChiaveModuloOA = "OrdineAccettazione.ModuloRevisione";
    private const string ModuloDefault = "Mod. Acc. 01 Rev. 0 del 16/09/2026";
    private const string ChiaveCartella = "OrdineAccettazione.CartellaOutput";     // se vuota → App_Data/OrdiniAccettazione

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryOrdineAccettazione _repo;
    private readonly IGeneratoreOrdineAccettazione _generatore;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IWebHostEnvironment _ambiente;

    public OrdiniAccettazioneController(
        ContestoLaboratorioAI ctx, IRepositoryOrdineAccettazione repo, IGeneratoreOrdineAccettazione generatore,
        IContestoUtente utente, IServizioAudit audit, IWebHostEnvironment ambiente)
    {
        _ctx = ctx; _repo = repo; _generatore = generatore; _utente = utente; _audit = audit; _ambiente = ambiente;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Ordini di Accettazione";
        return View();
    }

    private async Task<string> ConfigAsync(string chiave, string predefinito, CancellationToken ct)
    {
        var c = await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == chiave, ct);
        return string.IsNullOrWhiteSpace(c?.Valore) ? predefinito : c!.Valore!;
    }

    private Task<string> RevisioneCorrenteAsync(CancellationToken ct) => ConfigAsync(ChiaveRevisione, RevisioneDefault, ct);
    private Task<string> ModuloCorrenteAsync(CancellationToken ct) => ConfigAsync(ChiaveModuloOA, ModuloDefault, ct);

    /// <summary>Popola i campi di intestazione dipendenti dalla configurazione (revisione + modulo).</summary>
    private async Task ApplicaImpostazioniAsync(OrdineAccettazione ordine, CancellationToken ct)
    {
        ordine.RevisioneCondizioni = await RevisioneCorrenteAsync(ct);
        ordine.RiferimentoModulo = await ModuloCorrenteAsync(ct);
    }

    // ── Impostazioni (revisione Condizioni condivisa + riferimento Mod. Acc.) ──
    [HttpGet("api/ordini-accettazione/impostazioni")]
    public async Task<IActionResult> LeggiImpostazioni(CancellationToken ct) =>
        Json(new
        {
            RevisioneCondizioni = await RevisioneCorrenteAsync(ct),
            ModuloRevisione = await ModuloCorrenteAsync(ct)
        });

    [HttpPost("api/ordini-accettazione/impostazioni")]
    [RichiedePermesso(Permessi.AmministrazioneConfigurazioni)]
    public async Task<IActionResult> SalvaImpostazioni([FromBody] ImpostazioniOrdine req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.RevisioneCondizioni))
            await SalvaConfigAsync(ChiaveRevisione, req.RevisioneCondizioni!.Trim(),
                "Revisione corrente delle Condizioni Generali di Fornitura (condivisa offerte/OA)", "Preventivi", ct);
        if (!string.IsNullOrWhiteSpace(req.ModuloRevisione))
            await SalvaConfigAsync(ChiaveModuloOA, req.ModuloRevisione!.Trim(),
                "Riferimento modulo (Mod. Acc.) dell'Ordine di Accettazione", "Accettazione", ct);

        await _audit.RegistraAsync(AreaFunzionale.Accettazione, "ordineaccettazione.impostazioni",
            EsitoOperazione.Successo, dettaglio: new { req.RevisioneCondizioni, req.ModuloRevisione }, ct: ct);
        return Json(new { ok = true });
    }

    private async Task SalvaConfigAsync(string chiave, string valore, string descrizione, string categoria, CancellationToken ct)
    {
        var cfg = await _ctx.Configurazioni.FirstOrDefaultAsync(x => x.Chiave == chiave, ct);
        if (cfg is null)
            _ctx.Configurazioni.Add(new Configurazione
            {
                Chiave = chiave, Valore = valore, Descrizione = descrizione, Categoria = categoria,
                DataModifica = DateTime.UtcNow, IdUtenteModifica = _utente.IdUtente
            });
        else { cfg.Valore = valore; cfg.DataModifica = DateTime.UtcNow; cfg.IdUtenteModifica = _utente.IdUtente; }
        await _ctx.SaveChangesAsync(ct);
    }

    public record ImpostazioniOrdine
    {
        public string? RevisioneCondizioni { get; init; }
        public string? ModuloRevisione { get; init; }
    }

    // ── Elenco lotti candidati (con flag "già emesso") ──────────────
    [HttpGet("api/ordini-accettazione/lotti")]
    public async Task<IActionResult> Lotti([FromQuery] DateTime? dal, [FromQuery] DateTime? al, CancellationToken ct)
    {
        // @al inclusivo sul giorno → esclusivo il giorno dopo.
        var alEsclusivo = al?.Date.AddDays(1);
        var lotti = await _repo.LottiRicevutiAsync(dal?.Date, alEsclusivo, ct);

        var ids = lotti.Select(l => l.IdLotto).ToList();
        var emessi = await _ctx.OrdiniAccettazioneEmessi.AsNoTracking()
            .Where(e => ids.Contains(e.IdLotto))
            .Select(e => e.IdLotto)
            .ToListAsync(ct);
        var setEmessi = emessi.ToHashSet();

        return Json(lotti.Select(l => new
        {
            l.IdLotto,
            l.CodLotto,
            DataRicezione = l.DataRicezione?.ToString("yyyy-MM-dd"),
            l.Cliente,
            l.NumeroCampioni,
            GiaEmesso = setEmessi.Contains(l.IdLotto)
        }));
    }

    // ── Generazione/anteprima del documento (Word o PDF), senza registrare l'emissione ──
    [HttpGet("api/ordini-accettazione/{idLotto:int}/documento")]
    public async Task<IActionResult> Documento(int idLotto, [FromQuery] string? formato, CancellationToken ct)
    {
        var ordine = await _repo.LeggiPerLottoAsync(idLotto, ct);
        if (ordine is null)
            return NotFound(new { messaggio = $"Lotto {idLotto} non trovato o senza campioni in ArcLab." });
        await ApplicaImpostazioniAsync(ordine, ct);

        var pdf = string.Equals(formato, "pdf", StringComparison.OrdinalIgnoreCase);
        byte[] contenuto;
        string estensione, mime;
        if (pdf)
        {
            contenuto = _generatore.GeneraPdf(ordine, _ambiente.WebRootPath);
            estensione = "pdf";
            mime = "application/pdf";
        }
        else
        {
            var modello = Path.Combine(_ambiente.WebRootPath, ModelloCartaIntestata);
            if (!System.IO.File.Exists(modello))
                return NotFound(new { messaggio = "Modello di carta intestata non trovato: " + ModelloCartaIntestata });
            contenuto = _generatore.GeneraWord(ordine, modello);
            estensione = "docx";
            mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        }

        await _audit.RegistraAsync(AreaFunzionale.Accettazione, "ordineaccettazione.documento",
            EsitoOperazione.Successo, tipoEntita: "Lotto", idEntita: idLotto.ToString(),
            dettaglio: new { ordine.CodLotto, Formato = estensione }, ct: ct);

        return File(contenuto, mime, _generatore.NomeFile(ordine, estensione));
    }

    // ── Registra l'emissione (guardia anti-doppione) ────────────────
    [HttpPost("api/ordini-accettazione/{idLotto:int}/emetti")]
    public async Task<IActionResult> Emetti(int idLotto, [FromQuery] string? origine, CancellationToken ct)
    {
        var esito = await RegistraEmissioneAsync(idLotto, origine ?? "manuale", ct);
        if (esito.Ordine is null) return NotFound(new { messaggio = esito.Messaggio });
        return Json(new { esito.Doppione, esito.Messaggio, idLotto });
    }

    // ── Generazione schedulata: registra tutti i lotti del periodo non ancora emessi ──
    [HttpPost("api/ordini-accettazione/genera-schedulati")]
    public async Task<IActionResult> GeneraSchedulati([FromQuery] DateTime? dal, [FromQuery] DateTime? al, CancellationToken ct)
    {
        var alEsclusivo = al?.Date.AddDays(1);
        var lotti = await _repo.LottiRicevutiAsync(dal?.Date, alEsclusivo, ct);

        int nuovi = 0, gia = 0, errori = 0;
        foreach (var l in lotti)
        {
            try
            {
                var esito = await RegistraEmissioneAsync(l.IdLotto, "schedulato", ct);
                if (esito.Ordine is null) { errori++; continue; }
                if (esito.Doppione) gia++; else nuovi++;
            }
            catch { errori++; }
        }

        await _audit.RegistraAsync(AreaFunzionale.Accettazione, "ordineaccettazione.schedulato",
            EsitoOperazione.Successo, dettaglio: new { Nuovi = nuovi, GiaEmessi = gia, Errori = errori }, ct: ct);

        return Json(new { nuovi, giaEmessi = gia, errori, totale = lotti.Count });
    }

    /// <summary>Archivia il PDF su disco in &lt;cartella&gt;/AAAA/MM/ e ritorna il percorso completo.</summary>
    private async Task<string> SalvaPdfAsync(OrdineAccettazione ordine, string nomeFile, byte[] pdf, CancellationToken ct)
    {
        var baseDir = await ConfigAsync(ChiaveCartella, "", ct);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Path.Combine(_ambiente.ContentRootPath, "App_Data", "OrdiniAccettazione");

        var data = ordine.DataRicezione ?? DateTime.Today;
        var dir = Path.Combine(baseDir, data.ToString("yyyy"), data.ToString("MM"));
        Directory.CreateDirectory(dir);

        var percorso = Path.Combine(dir, nomeFile);
        await System.IO.File.WriteAllBytesAsync(percorso, pdf, ct);
        return percorso;
    }

    private record EsitoEmissione(OrdineAccettazioneEmesso? Ordine, bool Doppione, string Messaggio);

    /// <summary>
    /// Registra l'emissione dell'Ordine di Accettazione per un lotto. Idempotente: se già presente
    /// (per IdLotto), non duplica. Legge la testata da ArcLab per lo snapshot (cliente/campioni).
    /// </summary>
    private async Task<EsitoEmissione> RegistraEmissioneAsync(int idLotto, string origine, CancellationToken ct)
    {
        var esistente = await _ctx.OrdiniAccettazioneEmessi.FirstOrDefaultAsync(e => e.IdLotto == idLotto, ct);
        if (esistente is not null)
            return new EsitoEmissione(esistente, true, $"Ordine di Accettazione già emesso per il lotto {esistente.CodLotto} il {esistente.DataEmissione:dd/MM/yyyy}.");

        var ordine = await _repo.LeggiPerLottoAsync(idLotto, ct);
        if (ordine is null)
            return new EsitoEmissione(null, false, $"Lotto {idLotto} non trovato o senza campioni in ArcLab.");
        await ApplicaImpostazioniAsync(ordine, ct);

        // Genera il PDF (carta intestata) e archivialo su disco; calcola l'impronta.
        var pdf = _generatore.GeneraPdf(ordine, _ambiente.WebRootPath);
        var nomeFile = _generatore.NomeFile(ordine, "pdf");
        var percorso = await SalvaPdfAsync(ordine, nomeFile, pdf, ct);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf));

        var registro = new OrdineAccettazioneEmesso
        {
            IdLotto = idLotto,
            CodLotto = ordine.CodLotto,
            Cliente = ordine.ClienteRagioneSociale,
            IdInsediamento = ordine.IdInsediamento,
            NumeroCampioni = ordine.Campioni.Count,
            DataEmissione = DateTime.UtcNow,
            Origine = origine,
            IdUtente = _utente.IdUtente,
            NomeFile = nomeFile,
            PercorsoFile = percorso,
            Hash = hash,
            StatoInvio = StatoInvioOrdine.DaInviare
        };
        _ctx.OrdiniAccettazioneEmessi.Add(registro);
        try
        {
            await _ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Corsa concorrente (indice univoco su IdLotto): trattala come doppione.
            _ctx.Entry(registro).State = EntityState.Detached;
            var g = await _ctx.OrdiniAccettazioneEmessi.AsNoTracking().FirstOrDefaultAsync(e => e.IdLotto == idLotto, ct);
            return new EsitoEmissione(g, true, "Ordine di Accettazione già emesso (registrazione concorrente).");
        }

        return new EsitoEmissione(registro, false, "Ordine di Accettazione registrato come emesso.");
    }
}
