using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

[RichiedePermesso(Permessi.ConsultazioneCampioni)]
public class ConsultazioneController : Controller
{
    private const string MimeXlsx =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IRepositoryCampioni _campioni;
    private readonly IRepositoryCertificati _certificati;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IServizioAnalisi _analisi;
    private readonly IContestoUtente _utente;
    private readonly IEsportatoreConsultazione _esportatore;

    public ConsultazioneController(
        IRepositoryCampioni campioni,
        IRepositoryCertificati certificati,
        IRepositoryAnagrafiche anagrafiche,
        IServizioAnalisi analisi,
        IContestoUtente utente,
        IEsportatoreConsultazione esportatore)
    {
        _campioni = campioni;
        _certificati = certificati;
        _anagrafiche = anagrafiche;
        _analisi = analisi;
        _utente = utente;
        _esportatore = esportatore;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Consultazione";
        return View();
    }

    public IActionResult Certificati()
    {
        ViewData["Titolo"] = "Certificati emessi";
        return View();
    }

    // ═══════════════════════════════════════════════════════════════
    // ANAGRAFICHE PER I FILTRI
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/anagrafiche/clienti")]
    public async Task<IActionResult> Clienti(CancellationToken ct) =>
        Json(await _anagrafiche.ClientiAsync(ct));

    [HttpGet("api/anagrafiche/insediamenti")]
    public async Task<IActionResult> Insediamenti(int? idCliente, CancellationToken ct) =>
        Json(await _anagrafiche.InsediamentiAsync(idCliente, ct));

    [HttpGet("api/anagrafiche/matrici")]
    public async Task<IActionResult> Matrici(CancellationToken ct) =>
        Json(await _anagrafiche.MatriciAsync(ct));

    [HttpGet("api/anagrafiche/controlli")]
    public async Task<IActionResult> Controlli(CancellationToken ct) =>
        Json(await _anagrafiche.ControlliAsync(ct));

    [HttpGet("api/anagrafiche/laboratori-esterni")]
    public async Task<IActionResult> LaboratoriEsterni(CancellationToken ct) =>
        Json(await _anagrafiche.LaboratoriEsterniAsync(ct));

    [HttpGet("api/anagrafiche/prelevatori")]
    public async Task<IActionResult> Prelevatori(CancellationToken ct) =>
        Json(await _anagrafiche.PrelevatoriAsync(ct));

    [HttpGet("api/anagrafiche/luoghi")]
    public async Task<IActionResult> Luoghi(CancellationToken ct) =>
        Json(await _anagrafiche.LuoghiPrelievoAsync(ct));

    // ═══════════════════════════════════════════════════════════════
    // RICERCA CAMPIONI
    // ═══════════════════════════════════════════════════════════════

    [HttpPost("api/consultazione/cerca")]
    public async Task<IActionResult> Cerca(
        [FromBody] CriteriRicercaCampioni criteri, CancellationToken ct)
    {
        if (criteri.Vuoto)
            return BadRequest(new { messaggio = "Impostare almeno un criterio di ricerca." });

        var esito = await _campioni.CercaAsync(criteri, ct);
        var totali = await _campioni.TotaliRicercaAsync(criteri, ct);

        // Il badge dell'archivio analisi si mostra solo a chi può eseguirle
        var archivio = _utente.HaPermesso(Permessi.ValidazioneAnalizza)
            ? await _analisi.ArchivioAsync(esito.Elementi.Select(e => e.IdCampione).ToList(), ct)
            : new Dictionary<int, SintesiAnalisi>();

        return Json(new
        {
            esito.Totale,
            esito.Troncato,
            TotaleCompleto = totali.Totale,
            totali.TotalePrezzo,
            Campioni = esito.Elementi.Select(c => new
            {
                c.IdCampione,
                c.NumeroCampione,
                c.RagioneSociale,
                c.CodContabilita,
                c.Email,
                c.NomeInsediamento,
                c.DenominazioneCampione,
                c.Matrice,
                c.IdControllo,
                c.Controllo,
                c.LuogoPrelievo,
                c.PuntoPrelievo,
                c.VerbalePrelievo,
                c.Prelevatore,
                c.PrezzoControllo,
                DataRicezione = c.DataRicezione?.ToString("dd/MM/yyyy"),
                DataPrelievo  = c.DataPrelievo?.ToString("dd/MM/yyyy"),
                c.GiorniInLaboratorio,
                c.NumeroProve,
                Stato = c.Stato.ToString(),
                HaAnalisi = archivio.ContainsKey(c.IdCampione)
            })
        });
    }

    /// <summary>Esporta in Excel l'intero risultato della ricerca (non solo le righe mostrate).</summary>
    [HttpPost("api/consultazione/esporta")]
    public async Task<IActionResult> Esporta(
        [FromBody] CriteriRicercaCampioni criteri, CancellationToken ct)
    {
        if (criteri.Vuoto)
            return BadRequest(new { messaggio = "Impostare almeno un criterio di ricerca." });

        // Alza il limite per esportare tutti i campioni corrispondenti, non i soli 500 a video.
        var criteriEstesi = criteri with { Limite = 100_000 };
        var esito = await _campioni.CercaAsync(criteriEstesi, ct);
        var contenuto = _esportatore.Genera(esito.Elementi, criteriEstesi);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile(criteriEstesi));
    }

    /// <summary>Identificativi tecnici a partire dai codici lotto e campione.</summary>
    [HttpGet("api/consultazione/identificativi")]
    public async Task<IActionResult> Identificativi(
        int codiceLotto, int codiceCampione, CancellationToken ct)
    {
        var esito = await _campioni.IdentificativiAsync(codiceLotto, codiceCampione, ct);

        return esito is null
            ? NotFound(new { messaggio = "Nessun campione con questi codici." })
            : Json(esito);
    }

    /// <summary>Elenco delle singole prove di un campione, con accreditamento e id tecnici.</summary>
    [HttpGet("api/consultazione/campione/{idCampione:int}/prove")]
    public async Task<IActionResult> ProveCampione(int idCampione, CancellationToken ct)
    {
        var completo = await _campioni.CompletoAsync(idCampione, ct, includiProveEscluse: true);
        if (completo is null)
            return NotFound(new { messaggio = "Campione non trovato." });

        return Json(completo.Prove.Select(p => new
        {
            p.IdProva,
            p.IdRigaMetodo,
            Metodo = p.DenominazioneRiga ?? p.Parametro,
            p.LaboratorioEsterno,
            p.NumeroLaboratorioEsterno,
            p.Accreditato,
            p.AccreditatoEsterno
        }));
    }

    // ═══════════════════════════════════════════════════════════════
    // CERTIFICATI
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/certificati/cerca")]
    [RichiedePermesso(Permessi.ConsultazioneCertificati)]
    public async Task<IActionResult> CercaCertificati(string? testo, CancellationToken ct)
    {
        var righe = await _certificati.CercaAsync(testo ?? "", 100, ct);

        return Json(righe.Select(c => new
        {
            c.IdCertificato,
            c.IdCampione,
            c.NumeroCertificato,
            DataCertificato = c.DataCertificato?.ToString("dd/MM/yyyy"),
            c.NumeroCampione,
            c.RagioneSociale,
            c.DenominazioneCampione,
            c.Matrice,
            c.GiorniRisposta
        }));
    }

    [HttpGet("api/certificati/{idCertificato:int}")]
    [RichiedePermesso(Permessi.ConsultazioneCertificati)]
    public async Task<IActionResult> Certificato(int idCertificato, CancellationToken ct)
    {
        var completo = await _certificati.CompletoAsync(idCertificato, ct);
        if (completo is null) return NotFound(new { messaggio = "Certificato non trovato." });

        var ultima = await _analisi.UltimaAsync(completo.Campione.IdCampione, ct);

        return Json(new
        {
            Certificato = new
            {
                completo.Certificato.IdCertificato,
                completo.Certificato.NumeroCertificato,
                DataCertificato = completo.Certificato.DataCertificato?.ToString("dd/MM/yyyy"),
                completo.Certificato.GiorniRisposta
            },
            Campione = new
            {
                completo.Campione.IdCampione,
                completo.Campione.NumeroCampione,
                completo.Campione.RagioneSociale,
                completo.Campione.NomeInsediamento,
                completo.Campione.DenominazioneCampione,
                completo.Campione.Matrice,
                completo.Campione.Controllo
            },
            Prove = completo.Prove.Select(p => new
            {
                Parametro = p.DenominazioneRiga ?? p.Parametro,
                p.RiferimentoNormativo,
                Valore = p.ValoreFormattato,
                Intervallo = p.IntervalloFormattato,
                p.UnitaMisura,
                p.VerificaValore,
                p.LaboratorioEsterno,
                p.NumeroLaboratorioEsterno,
                p.Accreditato,
                p.AccreditatoEsterno
            }),
            AnalisiArchiviata = ultima is null ? null : new
            {
                ultima.IdAnalisi,
                Data = ultima.DataAnalisi.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                Giudizio = ultima.Giudizio.ToString()
            }
        });
    }
}
