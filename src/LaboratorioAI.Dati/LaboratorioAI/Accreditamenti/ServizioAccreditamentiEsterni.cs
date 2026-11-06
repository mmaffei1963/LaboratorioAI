using System.Security.Cryptography;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Accredia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LaboratorioAI.Dati.LaboratorioAI.Accreditamenti;

/// <inheritdoc cref="IServizioAccreditamentiEsterni" />
public class ServizioAccreditamentiEsterni : IServizioAccreditamentiEsterni
{
    private readonly ContestoLaboratorioAI _db;
    private readonly IClientAccredia _accredia;
    private readonly IEstrattoreTestoPdf _estrattoreTesto;
    private readonly IConfiguration _config;
    private readonly ILogger<ServizioAccreditamentiEsterni> _log;

    public ServizioAccreditamentiEsterni(
        ContestoLaboratorioAI db, IClientAccredia accredia, IEstrattoreTestoPdf estrattoreTesto,
        IConfiguration config, ILogger<ServizioAccreditamentiEsterni> log)
    {
        _db = db;
        _accredia = accredia;
        _estrattoreTesto = estrattoreTesto;
        _config = config;
        _log = log;
    }

    public async Task<IReadOnlyList<RisultatoControlloAccreditamento>> ControllaTuttiAsync(CancellationToken ct = default)
    {
        var laboratori = await _db.Set<AccreditamentoLabEsterno>()
            .Where(a => a.Attivo)
            .ToListAsync(ct);

        var risultati = new List<RisultatoControlloAccreditamento>();

        foreach (var lab in laboratori)
        {
            try
            {
                var ultima = await _accredia.UltimaRevisioneAsync(lab.NumeroAccreditamento, lab.Sede, ct);
                if (ultima is null)
                {
                    risultati.Add(new RisultatoControlloAccreditamento(
                        lab.IdAccreditamento, lab.NomeLaboratorio, EsitoControlloAccreditamento.Errore,
                        null, "Nessuna revisione trovata su ACCREDIA (pagina non riconosciuta)."));
                    continue;
                }

                var giaNota = await _db.Set<RevisioneAccreditamentoEsterno>()
                    .FirstOrDefaultAsync(r => r.IdAccreditamento == lab.IdAccreditamento
                                && r.NumeroRevisione == ultima.NumeroRevisione, ct);

                if (giaNota is not null)
                {
                    // Correzione della data di emissione (08/10/2026): fino ad allora il feed era letto in formato
                    // americano (05/10 → 10 maggio, 25/09 → vuota). Se la data letta ora è diversa, si aggiorna.
                    if (ultima.DataEmissione is DateTime nuova && giaNota.DataEmissione?.Date != nuova.Date)
                    {
                        _log.LogInformation("Data di emissione corretta per {Laboratorio} rev. {Rev}: {Vecchia} → {Nuova}",
                            lab.NomeLaboratorio, giaNota.NumeroRevisione, giaNota.DataEmissione, nuova);
                        giaNota.DataEmissione = nuova;
                        await _db.SaveChangesAsync(ct);
                    }
                    risultati.Add(new RisultatoControlloAccreditamento(
                        lab.IdAccreditamento, lab.NomeLaboratorio, EsitoControlloAccreditamento.Invariato,
                        ultima.NumeroRevisione, null));
                    continue;
                }

                // Nuova revisione: scarica il PDF, lo salva e registra la riga "da confermare".
                var pdf = await _accredia.ScaricaPdfRevisioneAsync(lab.NumeroAccreditamento, lab.Sede, ultima.NumeroRevisione, ct);
                var hash = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
                var percorsoRelativo = SalvaPdf(lab, ultima.NumeroRevisione, pdf);
                var testoEstratto = _estrattoreTesto.EstraiTesto(pdf);

                _db.Set<RevisioneAccreditamentoEsterno>().Add(new RevisioneAccreditamentoEsterno
                {
                    IdAccreditamento = lab.IdAccreditamento,
                    NumeroRevisione = ultima.NumeroRevisione,
                    DataEmissione = ultima.DataEmissione,
                    PercorsoFile = percorsoRelativo,
                    HashFileSha256 = hash,
                    DataRilevazione = DateTime.UtcNow,
                    Confermata = false,
                    TestoEstratto = string.IsNullOrWhiteSpace(testoEstratto) ? null : testoEstratto
                });
                await _db.SaveChangesAsync(ct);

                risultati.Add(new RisultatoControlloAccreditamento(
                    lab.IdAccreditamento, lab.NomeLaboratorio, EsitoControlloAccreditamento.NuovaRevisioneTrovata,
                    ultima.NumeroRevisione, null));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Controllo accreditamento fallito per {Laboratorio} ({Numero}/{Sede})",
                    lab.NomeLaboratorio, lab.NumeroAccreditamento, lab.Sede);
                risultati.Add(new RisultatoControlloAccreditamento(
                    lab.IdAccreditamento, lab.NomeLaboratorio, EsitoControlloAccreditamento.Errore,
                    null, ex.Message));
            }
        }

        return risultati;
    }

    public async Task ConfermaRevisioneAsync(int idRevisione, int idUtente, string? note, CancellationToken ct = default)
    {
        var revisione = await _db.Set<RevisioneAccreditamentoEsterno>().FirstOrDefaultAsync(r => r.IdRevisione == idRevisione, ct)
            ?? throw new InvalidOperationException($"Revisione {idRevisione} non trovata.");

        revisione.Confermata = true;
        revisione.DataConferma = DateTime.UtcNow;
        revisione.IdUtenteConferma = idUtente;
        revisione.NoteConferma = note;

        var lab = await _db.Set<AccreditamentoLabEsterno>().FirstOrDefaultAsync(a => a.IdAccreditamento == revisione.IdAccreditamento, ct);
        if (lab is not null && revisione.NumeroRevisione > (lab.UltimaRevisioneConfermata ?? 0))
        {
            lab.UltimaRevisioneConfermata = revisione.NumeroRevisione;
            lab.DataUltimaModifica = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<AccreditamentoLabEsterno>> ElencoAsync(CancellationToken ct = default) =>
        _db.Set<AccreditamentoLabEsterno>()
            .Where(a => a.Attivo)
            .OrderBy(a => a.NomeLaboratorio)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<AccreditamentoLabEsterno>)t.Result, ct);

    public Task<IReadOnlyList<RevisioneAccreditamentoEsterno>> RevisioniDaConfermareAsync(CancellationToken ct = default) =>
        _db.Set<RevisioneAccreditamentoEsterno>()
            .Where(r => !r.Confermata)
            .OrderByDescending(r => r.DataRilevazione)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<RevisioneAccreditamentoEsterno>)t.Result, ct);

    public Task<IReadOnlyList<RevisioneAccreditamentoEsterno>> StoricoRevisioniAsync(int idAccreditamento, CancellationToken ct = default) =>
        _db.Set<RevisioneAccreditamentoEsterno>()
            .Where(r => r.IdAccreditamento == idAccreditamento)
            .OrderByDescending(r => r.NumeroRevisione)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<RevisioneAccreditamentoEsterno>)t.Result, ct);

    public async Task<IReadOnlyList<RisultatoRiestrazioneTesto>> RiestraiTestoMancanteAsync(CancellationToken ct = default)
    {
        var cartellaBase = _config["AccreditamentiEsterni:CartellaArchivio"];
        if (string.IsNullOrWhiteSpace(cartellaBase))
            throw new InvalidOperationException(
                "Configurazione mancante: AccreditamentiEsterni:CartellaArchivio (appsettings).");

        // Join lato client (non tramite Dapper/ArcLab, è tutto nel db proprio LaboratorioAI):
        // servono nome laboratorio e numero revisione solo per il riepilogo mostrato all'operatore.
        var revisioni = await _db.Set<RevisioneAccreditamentoEsterno>()
            .Where(r => r.TestoEstratto == null)
            .Join(_db.Set<AccreditamentoLabEsterno>(),
                r => r.IdAccreditamento, a => a.IdAccreditamento,
                (r, a) => new { Revisione = r, a.NomeLaboratorio })
            .ToListAsync(ct);

        var risultati = new List<RisultatoRiestrazioneTesto>();

        foreach (var voce in revisioni)
        {
            var revisione = voce.Revisione;
            try
            {
                if (string.IsNullOrWhiteSpace(revisione.PercorsoFile))
                {
                    risultati.Add(new RisultatoRiestrazioneTesto(
                        revisione.IdRevisione, voce.NomeLaboratorio, revisione.NumeroRevisione,
                        EsitoRiestrazioneTesto.FileNonTrovato, null, "Nessun percorso file registrato per questa revisione."));
                    continue;
                }

                var percorsoCompleto = Path.Combine(cartellaBase, revisione.PercorsoFile);
                if (!File.Exists(percorsoCompleto))
                {
                    risultati.Add(new RisultatoRiestrazioneTesto(
                        revisione.IdRevisione, voce.NomeLaboratorio, revisione.NumeroRevisione,
                        EsitoRiestrazioneTesto.FileNonTrovato, null, $"File non trovato: {percorsoCompleto}"));
                    continue;
                }

                var pdf = await File.ReadAllBytesAsync(percorsoCompleto, ct);
                var testoEstratto = _estrattoreTesto.EstraiTesto(pdf);

                if (string.IsNullOrWhiteSpace(testoEstratto))
                {
                    risultati.Add(new RisultatoRiestrazioneTesto(
                        revisione.IdRevisione, voce.NomeLaboratorio, revisione.NumeroRevisione,
                        EsitoRiestrazioneTesto.NessunTestoEstratto, 0, null));
                    continue;
                }

                revisione.TestoEstratto = testoEstratto;
                await _db.SaveChangesAsync(ct);

                risultati.Add(new RisultatoRiestrazioneTesto(
                    revisione.IdRevisione, voce.NomeLaboratorio, revisione.NumeroRevisione,
                    EsitoRiestrazioneTesto.TestoEstratto, testoEstratto.Length, null));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ri-estrazione testo fallita per la revisione {IdRevisione} ({Laboratorio})",
                    revisione.IdRevisione, voce.NomeLaboratorio);
                risultati.Add(new RisultatoRiestrazioneTesto(
                    revisione.IdRevisione, voce.NomeLaboratorio, revisione.NumeroRevisione,
                    EsitoRiestrazioneTesto.Errore, null, ex.Message));
            }
        }

        return risultati;
    }

    public async Task<IReadOnlyList<RisultatoRiscaricoPdf>> RiscaricaPdfCorrottiAsync(CancellationToken ct = default)
    {
        var cartellaBase = _config["AccreditamentiEsterni:CartellaArchivio"];
        if (string.IsNullOrWhiteSpace(cartellaBase))
            throw new InvalidOperationException(
                "Configurazione mancante: AccreditamentiEsterni:CartellaArchivio (appsettings).");

        var revisioni = await _db.Set<RevisioneAccreditamentoEsterno>()
            .Join(_db.Set<AccreditamentoLabEsterno>(),
                r => r.IdAccreditamento, a => a.IdAccreditamento,
                (r, a) => new { Revisione = r, Laboratorio = a })
            .ToListAsync(ct);

        var risultati = new List<RisultatoRiscaricoPdf>();

        foreach (var voce in revisioni)
        {
            var revisione = voce.Revisione;
            var lab = voce.Laboratorio;

            if (!FileCorrottoOMancante(cartellaBase, revisione.PercorsoFile))
                continue;

            try
            {
                var pdf = await _accredia.ScaricaPdfRevisioneAsync(lab.NumeroAccreditamento, lab.Sede, revisione.NumeroRevisione, ct);
                var hash = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
                var percorsoRelativo = SalvaPdf(lab, revisione.NumeroRevisione, pdf);
                var testoEstratto = _estrattoreTesto.EstraiTesto(pdf);

                revisione.PercorsoFile = percorsoRelativo;
                revisione.HashFileSha256 = hash;
                revisione.TestoEstratto = string.IsNullOrWhiteSpace(testoEstratto) ? null : testoEstratto;
                await _db.SaveChangesAsync(ct);

                risultati.Add(new RisultatoRiscaricoPdf(
                    revisione.IdRevisione, lab.NomeLaboratorio, revisione.NumeroRevisione,
                    EsitoRiscaricoPdf.PdfRiscaricato, testoEstratto.Length > 0 ? testoEstratto.Length : null, null));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ri-scarico PDF fallito per la revisione {IdRevisione} ({Laboratorio}/{Numero}/{Sede})",
                    revisione.IdRevisione, lab.NomeLaboratorio, lab.NumeroAccreditamento, lab.Sede);
                risultati.Add(new RisultatoRiscaricoPdf(
                    revisione.IdRevisione, lab.NomeLaboratorio, revisione.NumeroRevisione,
                    EsitoRiscaricoPdf.Errore, null, ex.Message));
            }
        }

        return risultati;
    }

    /// <summary>
    /// Un PDF valido inizia sempre con la firma ASCII "%PDF"; un file più corto di 1KB con quella
    /// firma non esiste in pratica (il più piccolo PDF ACCREDIA osservato supera i 150KB), quindi
    /// entrambe le condizioni segnalano con sicurezza un download interrotto o fallito.
    /// </summary>
    private static bool FileCorrottoOMancante(string cartellaBase, string? percorsoFile)
    {
        if (string.IsNullOrWhiteSpace(percorsoFile))
            return true;

        var percorsoCompleto = Path.Combine(cartellaBase, percorsoFile);
        var info = new FileInfo(percorsoCompleto);
        if (!info.Exists || info.Length < 1024)
            return true;

        Span<byte> intestazione = stackalloc byte[4];
        using var stream = File.OpenRead(percorsoCompleto);
        var letti = stream.Read(intestazione);
        return letti < 4 || intestazione[0] != 0x25 || intestazione[1] != 0x50
            || intestazione[2] != 0x44 || intestazione[3] != 0x46; // "%PDF"
    }

    /// <summary>
    /// Salva il PDF sotto CartellaArchivio/{numero}_{sede}_{nome laboratorio}/Revisione_{numero}.pdf
    /// e ne restituisce il percorso relativo. Il nome del laboratorio nella cartella serve solo a
    /// ritrovarla più facilmente a occhio: l'accoppiamento vero resta numero+sede (univoco).
    /// </summary>
    private string SalvaPdf(AccreditamentoLabEsterno lab, int numeroRevisione, byte[] pdf)
    {
        var cartellaBase = _config["AccreditamentiEsterni:CartellaArchivio"];
        if (string.IsNullOrWhiteSpace(cartellaBase))
            throw new InvalidOperationException(
                "Configurazione mancante: AccreditamentiEsterni:CartellaArchivio (appsettings).");

        var sottocartella = $"{lab.NumeroAccreditamento}_{lab.Sede}_{NomeCartellaValido(lab.NomeLaboratorio)}";
        var cartella = Path.Combine(cartellaBase, sottocartella);
        Directory.CreateDirectory(cartella);

        var nomeFile = $"Revisione_{numeroRevisione}.pdf";
        var percorsoCompleto = Path.Combine(cartella, nomeFile);
        File.WriteAllBytes(percorsoCompleto, pdf);

        return Path.Combine(sottocartella, nomeFile);
    }

    /// <summary>Nome laboratorio ripulito dai caratteri non ammessi in un nome di cartella Windows.</summary>
    private static string NomeCartellaValido(string nome)
    {
        var pulito = nome;
        foreach (var carattere in Path.GetInvalidFileNameChars())
            pulito = pulito.Replace(carattere, '_');

        return pulito.Trim().Replace(' ', '_');
    }
}
