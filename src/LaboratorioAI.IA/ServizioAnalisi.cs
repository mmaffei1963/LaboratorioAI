using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LaboratorioAI.IA;

/// <inheritdoc cref="IServizioAnalisi" />
public class ServizioAnalisi : IServizioAnalisi
{
    private readonly IRepositoryCampioni _campioni;
    private readonly IClientAnthropic _client;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;
    private readonly IServizioAudit _audit;
    private readonly ILogger<ServizioAnalisi> _log;
    private readonly LaboratorioAI.Dati.Limiti.ServizioSegnalazioniLimiti _limiti;

    public ServizioAnalisi(
        IRepositoryCampioni campioni,
        IClientAnthropic client,
        ContestoLaboratorioAI ctx,
        IContestoUtente utente,
        IServizioVersione versione,
        IServizioAudit audit,
        ILogger<ServizioAnalisi> log,
        LaboratorioAI.Dati.Limiti.ServizioSegnalazioniLimiti limiti)
    {
        _limiti = limiti;
        _campioni = campioni;
        _client = client;
        _ctx = ctx;
        _utente = utente;
        _versione = versione;
        _audit = audit;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════════
    // ANALISI SINGOLA
    // ═══════════════════════════════════════════════════════════════

    public async Task<EsitoAnalisi> AnalizzaAsync(
        RichiestaAnalisi richiesta, CancellationToken ct = default)
    {
        // L'archivio ha la precedenza: evita di pagare due volte la stessa analisi
        if (!richiesta.ForzaRiesecuzione)
        {
            var archiviata = await UltimaAsync(richiesta.IdCampione, ct);
            if (archiviata is not null) return archiviata with { DaArchivio = true };
        }

        var campione = await _campioni.CompletoAsync(richiesta.IdCampione, ct)
            ?? throw new InvalidOperationException(
                $"Campione {richiesta.IdCampione} non trovato in ArcLab.");

        if (campione.Prove.Count == 0)
            throw new InvalidOperationException(
                "Il campione non ha prove registrate: analisi non eseguibile.");

        // 05/11/2026: limiti di legge validati (Limiti normativi) applicabili alle prove del campione.
        IReadOnlyDictionary<int, IReadOnlyList<string>> limitiDiLegge;
        try { limitiDiLegge = await _limiti.LimitiCampioneAsync(richiesta.IdCampione, ct); }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Limiti di legge non letti per il campione {IdCampione}: analisi senza limiti.", richiesta.IdCampione);
            limitiDiLegge = new Dictionary<int, IReadOnlyList<string>>();
        }

        var prospetto = CostruttoreProspetto.Componi(
            campione.Campione, campione.Prove, richiesta.Modalita, limitiDiLegge);

        var risposta = await _client.InterrogaAsync(prospetto, ct);
        var giudizio = CostruttoreProspetto.DeduciGiudizio(risposta.Testo);

        var analisi = await ArchiviaAsync(richiesta, campione, risposta, giudizio, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Validazione,
            "analisi.eseguita",
            EsitoOperazione.Successo,
            "Campione", richiesta.IdCampione.ToString(),
            new { analisi.IdAnalisi, Giudizio = giudizio.ToString(), campione.Prove.Count },
            ct);

        return new EsitoAnalisi
        {
            IdAnalisi = analisi.IdAnalisi,
            Testo = risposta.Testo,
            Giudizio = giudizio,
            DataAnalisi = analisi.DataAnalisi,
            NumeroProve = campione.Prove.Count,
            DaArchivio = false,
            EseguitaDa = _utente.NomeCompleto
        };
    }

    private async Task<AnalisiAi> ArchiviaAsync(
        RichiestaAnalisi richiesta,
        CampioneCompleto campione,
        RispostaModello risposta,
        GiudizioAnalisi giudizio,
        CancellationToken ct)
    {
        var costo = _client is ClientAnthropic concreto
            ? concreto.StimaCosto(risposta.TokenInput, risposta.TokenOutput)
            : (decimal?)null;

        var analisi = new AnalisiAi
        {
            IdCampione = richiesta.IdCampione,
            IdCertificato = richiesta.IdCertificato,
            Modalita = richiesta.Modalita,
            DataAnalisi = DateTime.UtcNow,
            IdUtente = _utente.IdUtente,

            // Fotografia dei dati descrittivi: l'analisi deve restare
            // leggibile anche se il campione viene poi modificato
            RagioneSociale = campione.Campione.RagioneSociale,
            DenominazioneCampione = campione.Campione.DenominazioneCampione,
            Matrice = campione.Campione.Matrice,
            DataRicezione = campione.Campione.DataRicezione is { } d
                ? DateOnly.FromDateTime(d) : null,
            NumeroProve = campione.Prove.Count,

            Giudizio = giudizio,
            Testo = risposta.Testo,

            ModelloAi = risposta.Modello,
            VersioneApplicazione = _versione.Versione,
            TokenInput = risposta.TokenInput,
            TokenOutput = risposta.TokenOutput,
            CostoStimato = costo
        };

        _ctx.Analisi.Add(analisi);
        await _ctx.SaveChangesAsync(ct);

        return analisi;
    }

    // ═══════════════════════════════════════════════════════════════
    // ANALISI MASSIVA
    // ═══════════════════════════════════════════════════════════════

    public async Task<EsitoAnalisiMassiva> AnalizzaMassivoAsync(
        IReadOnlyList<int> idCampioni,
        IProgress<int>? avanzamento = null,
        CancellationToken ct = default)
    {
        var righe = new List<RigaAnalisiMassiva>(idCampioni.Count);
        var completati = 0;

        // Esecuzione sequenziale: le chiamate al modello sono soggette
        // a limitazione di frequenza e il parallelismo darebbe errori 429.
        foreach (var idCampione in idCampioni)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var esito = await AnalizzaAsync(
                    new RichiestaAnalisi { IdCampione = idCampione }, ct);

                var campione = await _campioni.CompletoAsync(idCampione, ct);

                righe.Add(new RigaAnalisiMassiva
                {
                    IdCampione = idCampione,
                    NumeroCampione = campione?.Campione.NumeroCampione ?? idCampione.ToString(),
                    RagioneSociale = campione?.Campione.RagioneSociale,
                    DenominazioneCampione = campione?.Campione.DenominazioneCampione,
                    Giudizio = esito.Giudizio,
                    IdAnalisi = esito.IdAnalisi,
                    DaArchivio = esito.DaArchivio
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Un campione che fallisce non deve interrompere l'intero lotto
                _log.LogWarning(ex, "Analisi non riuscita per il campione {Id}", idCampione);

                righe.Add(new RigaAnalisiMassiva
                {
                    IdCampione = idCampione,
                    NumeroCampione = idCampione.ToString(),
                    Giudizio = GiudizioAnalisi.NonDeterminato,
                    Errore = ex.Message
                });
            }

            avanzamento?.Report(++completati);
        }

        await _audit.RegistraAsync(
            AreaFunzionale.Validazione,
            "analisi.massiva",
            EsitoOperazione.Successo,
            dettaglio: new { Campioni = idCampioni.Count, NonRiusciti = righe.Count(r => r.Errore is not null) },
            ct: ct);

        return new EsitoAnalisiMassiva { Righe = righe };
    }

    // ═══════════════════════════════════════════════════════════════
    // ARCHIVIO
    // ═══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyDictionary<int, SintesiAnalisi>> ArchivioAsync(
        IReadOnlyList<int> idCampioni, CancellationToken ct = default)
    {
        if (idCampioni.Count == 0)
            return new Dictionary<int, SintesiAnalisi>();

        // Ultima analisi per ciascun campione.
        // Si seleziona la riga il cui IdAnalisi è il massimo per quel campione
        // (IdAnalisi è INT IDENTITY, quindi il massimo è l'analisi più recente).
        // Questa forma è tradotta correttamente da EF Core, a differenza di
        // GroupBy(...).Select(g => g.First()) che genera un errore di traduzione.
        var righe = await _ctx.Analisi
            .Where(a => idCampioni.Contains(a.IdCampione))
            .Where(a => a.IdAnalisi == _ctx.Analisi
                .Where(b => b.IdCampione == a.IdCampione)
                .Max(b => b.IdAnalisi))
            .Select(a => new SintesiAnalisi
            {
                IdAnalisi = a.IdAnalisi,
                IdCampione = a.IdCampione,
                DataAnalisi = a.DataAnalisi,
                Giudizio = a.Giudizio,
                EseguitaDa = a.Utente!.NomeCompleto
            })
            .ToListAsync(ct);

        return righe.ToDictionary(r => r.IdCampione);
    }

    public async Task<EsitoAnalisi?> UltimaAsync(int idCampione, CancellationToken ct = default)
    {
        var analisi = await _ctx.Analisi
            .Include(a => a.Utente)
            .Where(a => a.IdCampione == idCampione)
            .OrderByDescending(a => a.IdAnalisi)
            .FirstOrDefaultAsync(ct);

        if (analisi is null) return null;

        return new EsitoAnalisi
        {
            IdAnalisi = analisi.IdAnalisi,
            Testo = analisi.Testo,
            Giudizio = analisi.Giudizio,
            DataAnalisi = analisi.DataAnalisi,
            NumeroProve = analisi.NumeroProve ?? 0,
            DaArchivio = true,
            EseguitaDa = analisi.Utente?.NomeCompleto
        };
    }

    public async Task<IReadOnlyList<SintesiAnalisi>> StoricoAsync(
        int idCampione, CancellationToken ct = default) =>
        await _ctx.Analisi
            .Where(a => a.IdCampione == idCampione)
            .OrderByDescending(a => a.IdAnalisi)
            .Select(a => new SintesiAnalisi
            {
                IdAnalisi = a.IdAnalisi,
                IdCampione = a.IdCampione,
                DataAnalisi = a.DataAnalisi,
                Giudizio = a.Giudizio,
                EseguitaDa = a.Utente!.NomeCompleto
            })
            .ToListAsync(ct);
}
