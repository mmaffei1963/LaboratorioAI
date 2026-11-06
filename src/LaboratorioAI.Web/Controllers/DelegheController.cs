using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Deleghe operative per i consulenti: generazione dal modello Word (Word + PDF), legata o no a un
/// rapportino, solo per clienti con una commessa di consulenza attiva in ArcLab (letto in SOLA LETTURA).
/// La delega generata entra nel registro come "Da firmare"; vale solo dopo la registrazione della firma.
/// </summary>
[RichiedePermesso(Permessi.RapportiniCompila)]
public class DelegheController : Controller
{
    private const string ChiaveModello = "Deleghe.ModelloWord";
    private const string ChiaveArchivio = "Deleghe.CartellaArchivio";
    private const string ChiaveLrMarea = "Deleghe.LegaleRappresentanteMarea";
    public const string LrMareaPredefinito = "Daniele Vaccarini";
    /// <summary>Testo stampato al posto delle sedi quando la delega vale per tutte.</summary>
    public const string TutteLeSedi = "Tutte le sedi operative del Cliente";
    private const string ModelloPredefinito = "docx/Delega_operativa_modello.docx";

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryConsulenze _arclab;
    private readonly IGeneratoreDelega _generatore;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IWebHostEnvironment _ambiente;

    public DelegheController(ContestoLaboratorioAI ctx, IRepositoryConsulenze arclab, IGeneratoreDelega generatore,
        IContestoUtente utente, IServizioAudit audit, IWebHostEnvironment ambiente)
    {
        _ctx = ctx; _arclab = arclab; _generatore = generatore; _utente = utente; _audit = audit; _ambiente = ambiente;
    }

    /// <summary>
    /// Dati per il modulo di generazione: cliente contrattualizzato?, legale rappresentante dell'ultima
    /// delega del cliente, qualifica/albo del consulente, eventuale delega già in essere o da firmare.
    /// </summary>
    [HttpGet("api/deleghe/proposta")]
    public async Task<IActionResult> Proposta([FromQuery] int idCliente, [FromQuery] int? idOrganico, CancellationToken ct)
    {
        var commesse = await _arclab.CommesseConsulenzaAsync(idCliente, ct);
        var contrattualizzato = commesse.Any(c => c.Attiva);
        var ultima = await _ctx.DelegheClienti.AsNoTracking()
            .Where(x => x.IdClienteArcLab == idCliente && x.LegaleRappCognome != null)
            .OrderByDescending(x => x.IdDelegaCliente).FirstOrDefaultAsync(ct);
        ConsulenteDati? dati = null;
        object? esistente = null;
        if (idOrganico is int ido)
        {
            dati = await _ctx.ConsulentiDati.AsNoTracking().FirstOrDefaultAsync(x => x.IdOrganicoArcLab == ido, ct);
            var deleghe = await _ctx.DelegheClienti.AsNoTracking()
                .Where(x => x.IdClienteArcLab == idCliente && x.IdOrganicoArcLab == ido && x.Attivo)
                .OrderByDescending(x => x.IdDelegaCliente).ToListAsync(ct);
            var inEssere = deleghe.FirstOrDefault(x => x.ValidaAl(DateTime.Today));
            var daFirmare = deleghe.FirstOrDefault(x => x.Stato == StatoDelega.DaFirmare);
            if (inEssere is not null) esistente = new { stato = "InEssere", inEssere.IdDelegaCliente, inEssere.Estremi };
            else if (daFirmare is not null) esistente = new { stato = "DaFirmare", daFirmare.IdDelegaCliente, daFirmare.Estremi };
        }
        return Json(new
        {
            contrattualizzato,
            legaleRappNome = ultima?.LegaleRappNome,
            legaleRappCognome = ultima?.LegaleRappCognome,
            qualifica = dati?.Qualifica,
            albo = dati?.IscrizioneAlbo,
            esistente
        });
    }

    public record GeneraDto(int IdCliente, int IdInsediamento, int IdOrganico, string? LegaleRappNome,
        string? LegaleRappCognome, int? IdRapportino, int[]? IdInsediamenti = null, bool TuttiInsediamenti = false);

    [HttpPost("api/deleghe/genera")]
    public async Task<IActionResult> Genera([FromBody] GeneraDto d, CancellationToken ct)
    {
        if (d.IdCliente == 0) return BadRequest(new { messaggio = "Scegli il cliente." });
        var idsIns = (d.IdInsediamenti ?? Array.Empty<int>()).Append(d.IdInsediamento).Where(i => i != 0).Distinct().ToList();
        if (!d.TuttiInsediamenti && idsIns.Count == 0)
            return BadRequest(new { messaggio = "Scegli le sedi operative (insediamenti) per cui vale la delega, oppure \"tutte le sedi\"." });
        if (d.IdOrganico == 0) return BadRequest(new { messaggio = "Scegli il consulente." });
        var lrNome = (d.LegaleRappNome ?? "").Trim();
        var lrCognome = (d.LegaleRappCognome ?? "").Trim();
        if (lrNome.Length == 0 || lrCognome.Length == 0)
            return BadRequest(new { messaggio = "Indica nome e cognome del legale rappresentante del cliente." });
        if (lrNome.Length > 80 || lrCognome.Length > 80) return BadRequest(new { messaggio = "Nome o cognome troppo lunghi." });

        // Solo clienti contrattualizzati: almeno una commessa di consulenza attiva in ArcLab.
        var commesse = await _arclab.CommesseConsulenzaAsync(d.IdCliente, ct);
        if (!commesse.Any(c => c.Attiva))
            return BadRequest(new { messaggio = "Il cliente non ha commesse di consulenza attive in ArcLab: la delega si genera solo per clienti con un contratto di consulenza in essere." });

        var cli = await _arclab.AnagraficaClienteAsync(d.IdCliente, ct);
        if (cli is null) return BadRequest(new { messaggio = "Cliente non trovato in ArcLab." });
        var tuttiIns = await _arclab.InsediamentiAsync(d.IdCliente, ct);
        var sceltiIns = d.TuttiInsediamenti ? new List<Core.Modelli.InsediamentoRapportino>()
            : tuttiIns.Where(i => idsIns.Contains(i.IdInsediamento)).ToList();
        if (!d.TuttiInsediamenti && sceltiIns.Count != idsIns.Count)
            return BadRequest(new { messaggio = "Una o più sedi scelte non appartengono al cliente." });
        var cons = (await _arclab.ConsulentiAsync(ct)).FirstOrDefault(c => c.IdOrganico == d.IdOrganico);
        if (cons is null) return BadRequest(new { messaggio = "Consulente non trovato tra il personale in servizio." });
        var dati = await _ctx.ConsulentiDati.AsNoTracking().FirstOrDefaultAsync(x => x.IdOrganicoArcLab == d.IdOrganico, ct);
        if (string.IsNullOrWhiteSpace(dati?.Qualifica))
            return BadRequest(new { messaggio = $"Mancano qualifica e iscrizione all'Albo di {cons.Nome}: inseriscile in Amministrazione → Consulenze: documenti e deleghe." });

        var modello = await PercorsoModelloAsync(ct);
        if (!System.IO.File.Exists(modello)) return BadRequest(new { messaggio = "Modello Word della delega non trovato: " + modello });

        var oggi = DateTime.Today;
        var delega = new DelegaCliente
        {
            IdClienteArcLab = d.IdCliente,
            RagioneSociale = Taglia((cli.RagioneSociale ?? "").Trim(), 150)!,
            Estremi = "(in generazione)",
            Stato = StatoDelega.DaFirmare,
            Generata = true,
            DataEmissione = oggi,
            IdInsediamentoArcLab = sceltiIns.FirstOrDefault()?.IdInsediamento,
            TuttiInsediamenti = d.TuttiInsediamenti,
            SedeLegale = Taglia(Unisci(", ", cli.Indirizzo, cli.Cap), 300),
            SedeLegaleComune = Taglia(Comune(cli.Citta, cli.Provincia), 100),
            PartitaIva = Taglia(cli.PartitaIva?.Trim(), 30),
            CodiceFiscale = Taglia(cli.CodiceFiscale?.Trim(), 30),
            SedeOperativa = Taglia(d.TuttiInsediamenti ? TutteLeSedi : string.Join("; ", sceltiIns.Select(i => i.Nome)), 400),
            SedeOperativaComune = d.TuttiInsediamenti ? null : Taglia(string.Join("; ", sceltiIns.Select(i => Comune(i.Citta, i.Provincia)).Where(c => c != null).Distinct()), 100),
            LegaleRappNome = lrNome,
            LegaleRappCognome = lrCognome,
            IdOrganicoArcLab = cons.IdOrganico,
            Consulente = Taglia(cons.Nome, 120),
            ConsulenteQualifica = Taglia(dati!.Qualifica, 150),
            ConsulenteAlbo = Taglia(dati.IscrizioneAlbo, 200),
            IdRapportino = d.IdRapportino is int idr && idr > 0 ? idr : null,
            Attivo = true,
            IdUtenteCreazione = _utente.IdUtente,
            IdUtenteModifica = _utente.IdUtente,
            DataModifica = DateTime.UtcNow
        };
        foreach (var i in sceltiIns)
            delega.Insediamenti.Add(new DelegaInsediamento
            {
                IdInsediamentoArcLab = i.IdInsediamento,
                Nome = Taglia(i.Nome, 255) ?? "",
                Sede = Taglia(Unisci(" – ", i.Nome, Unisci(", ", i.Via, i.Cap)), 400),
                Comune = Taglia(Comune(i.Citta, i.Provincia), 100)
            });
        _ctx.DelegheClienti.Add(delega);
        await _ctx.SaveChangesAsync(ct);   // serve l'Id per il numero della delega

        try
        {
            delega.Estremi = $"Delega operativa n. {delega.IdDelegaCliente} del {oggi:dd/MM/yyyy}";
            var valori = Valori(delega, cons.NomeOrg, cons.CognomeOrg, await ConfigAsync(ChiaveLrMarea, LrMareaPredefinito, ct));
            var sedi = d.TuttiInsediamenti
                ? new List<(string, string?)> { (TutteLeSedi, null) }
                : delega.Insediamenti.Select(i => (i.Sede ?? i.Nome, i.Comune)).ToList();
            var word = _generatore.GeneraWord(modello, valori, sedi);
            var pdf = _generatore.GeneraPdf(word, _ambiente.WebRootPath);

            var dir = await CartellaAsync(oggi, ct);
            var baseNome = NomeBase(delega);
            delega.PercorsoWord = Path.Combine(dir, baseNome + ".docx");
            delega.PercorsoPdf = Path.Combine(dir, baseNome + ".pdf");
            await System.IO.File.WriteAllBytesAsync(delega.PercorsoWord, word, ct);
            await System.IO.File.WriteAllBytesAsync(delega.PercorsoPdf, pdf, ct);
            await _ctx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Nessun documento: la riga resta nel registro come non attiva, con il motivo (nessuna cancellazione).
            delega.Attivo = false;
            delega.Note = Taglia("Generazione non riuscita: " + ex.Message, 500);
            await _ctx.SaveChangesAsync(ct);
            return BadRequest(new { messaggio = "Generazione della delega non riuscita: " + ex.Message });
        }

        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "delega.genera", EsitoOperazione.Successo,
            "DelegaCliente", delega.IdDelegaCliente.ToString(),
            new { delega.IdClienteArcLab, delega.IdOrganicoArcLab, delega.IdRapportino, delega.Estremi }, ct);
        return Json(new { delega.IdDelegaCliente, delega.Estremi });
    }

    [HttpGet("api/deleghe/{id:int}/word")]
    public Task<IActionResult> Word(int id, CancellationToken ct) => FileAsync(id, d => d.PercorsoWord,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ct);

    [HttpGet("api/deleghe/{id:int}/pdf")]
    public Task<IActionResult> Pdf(int id, CancellationToken ct) => FileAsync(id, d => d.PercorsoPdf, "application/pdf", ct);

    [HttpGet("api/deleghe/{id:int}/firmata")]
    public Task<IActionResult> Firmata(int id, CancellationToken ct) => FileAsync(id, d => d.PercorsoFirmata, null, ct);

    private async Task<IActionResult> FileAsync(int id, Func<DelegaCliente, string?> percorso, string? mime, CancellationToken ct)
    {
        var d = await _ctx.DelegheClienti.AsNoTracking().FirstOrDefaultAsync(x => x.IdDelegaCliente == id, ct);
        var p = d is null ? null : percorso(d);
        if (string.IsNullOrWhiteSpace(p) || !System.IO.File.Exists(p)) return NotFound(new { messaggio = "File non disponibile." });
        var tipo = mime ?? (Path.GetExtension(p).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        });
        return PhysicalFile(p, tipo, Path.GetFileName(p));
    }

    /// <summary>Registra la firma: data di sottoscrizione e (facoltativa) copia firmata. Da qui la delega vale.</summary>
    [HttpPost("api/deleghe/{id:int}/firma")]
    [RichiedePermesso(Permessi.RapportiniGestione)]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Firma(int id, [FromForm] DateTime? dataSottoscrizione, IFormFile? file, CancellationToken ct)
    {
        var d = await _ctx.DelegheClienti.FirstOrDefaultAsync(x => x.IdDelegaCliente == id, ct);
        if (d is null) return NotFound(new { messaggio = "Delega non trovata." });
        if (dataSottoscrizione is not DateTime ds) return BadRequest(new { messaggio = "Indica la data di sottoscrizione." });
        if (ds.Date > DateTime.Today) return BadRequest(new { messaggio = "La data di sottoscrizione è nel futuro." });
        if (d.DataEmissione is DateTime de && ds.Date < de.Date)
            return BadRequest(new { messaggio = "La data di sottoscrizione è precedente all'emissione della delega." });

        if (file is not null && file.Length > 0)
        {
            var est = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (est is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
                return BadRequest(new { messaggio = "La copia firmata deve essere un PDF o un'immagine (JPG, PNG)." });
            var dir = await CartellaAsync(d.DataEmissione ?? DateTime.Today, ct);
            var dest = Path.Combine(dir, NomeBase(d) + "_FIRMATA" + est);
            await using (var fs = System.IO.File.Create(dest)) await file.CopyToAsync(fs, ct);
            d.PercorsoFirmata = dest;
        }
        d.DataSottoscrizione = ds.Date;
        d.Stato = StatoDelega.Sottoscritta;
        d.IdUtenteModifica = _utente.IdUtente;
        d.DataModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "delega.firma", EsitoOperazione.Successo,
            "DelegaCliente", id.ToString(), new { DataSottoscrizione = ds.Date, Allegato = d.PercorsoFirmata is not null }, ct);
        return Json(new { messaggio = "Firma registrata: la delega è in essere dalla data di sottoscrizione." });
    }

    // ── Helper ──

    /// <summary>Valori dei segnaposto {{…}} del modello Word.</summary>
    public static Dictionary<string, string?> Valori(DelegaCliente d, string? consNome, string? consCognome, string lrMarea) => new()
    {
        ["NUMERO"] = d.IdDelegaCliente.ToString(),
        ["DATA_EMISSIONE"] = (d.DataEmissione ?? DateTime.Today).ToString("dd/MM/yyyy"),
        ["RAGIONE_SOCIALE"] = d.RagioneSociale,
        ["SEDE_LEGALE"] = d.SedeLegale,
        ["SEDE_LEGALE_COMUNE"] = d.SedeLegaleComune,
        ["PARTITA_IVA"] = d.PartitaIva,
        ["CODICE_FISCALE"] = d.CodiceFiscale,
        ["SEDE_OPERATIVA"] = d.SedeOperativa,
        ["SEDE_OPERATIVA_COMUNE"] = d.SedeOperativaComune,
        ["LR_NOME"] = d.LegaleRappNome,
        ["LR_COGNOME"] = d.LegaleRappCognome,
        ["CONS_NOME"] = consNome?.Trim(),
        ["CONS_COGNOME"] = consCognome?.Trim(),
        ["CONS_QUALIFICA"] = d.ConsulenteQualifica,
        ["CONS_ALBO"] = string.IsNullOrWhiteSpace(d.ConsulenteAlbo) ? "—" : d.ConsulenteAlbo,
        ["CONSULENTE"] = $"{consNome?.Trim()} {consCognome?.Trim()}".Trim(),
        ["LR_MAREA"] = lrMarea
    };

    private async Task<string> PercorsoModelloAsync(CancellationToken ct)
    {
        var p = await ConfigAsync(ChiaveModello, "", ct);
        return string.IsNullOrWhiteSpace(p) ? Path.Combine(_ambiente.WebRootPath, ModelloPredefinito) : p;
    }

    private async Task<string> CartellaAsync(DateTime data, CancellationToken ct)
    {
        var baseDir = await ConfigAsync(ChiaveArchivio, "", ct);
        if (string.IsNullOrWhiteSpace(baseDir)) baseDir = Path.Combine(_ambiente.ContentRootPath, "App_Data", "Deleghe");
        var dir = Path.Combine(baseDir, data.ToString("yyyy"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string NomeBase(DelegaCliente d) =>
        $"DELEGA_{d.IdDelegaCliente}_{Slug(d.RagioneSociale, 30)}_{Slug(d.Consulente, 20)}";

    private async Task<string> ConfigAsync(string chiave, string predefinito, CancellationToken ct)
    {
        var c = await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == chiave, ct);
        return string.IsNullOrWhiteSpace(c?.Valore) ? predefinito : c!.Valore!.Trim();
    }

    private static string? Unisci(string sep, params string?[] parti)
    {
        var v = parti.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).ToList();
        return v.Count == 0 ? null : string.Join(sep, v);
    }

    private static string? Comune(string? citta, string? prov) =>
        string.IsNullOrWhiteSpace(citta) ? null : citta.Trim() + (string.IsNullOrWhiteSpace(prov) ? "" : $" ({prov.Trim()})");

    /// <summary>Solo lettere e cifre, il resto "_" (per i nomi file).</summary>
    private static string Slug(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "NA";
        var r = System.Text.RegularExpressions.Regex.Replace(s.Trim(), @"[^\p{L}\p{Nd}]+", "_").Trim('_');
        if (r.Length > max) r = r[..max].Trim('_');
        return r.Length == 0 ? "NA" : r;
    }

    private static string? Taglia(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
