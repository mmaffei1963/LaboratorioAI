using System.Collections.Concurrent;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti.Dvr;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// Coordina la generazione massiva dei DVR.
///
/// L'elaborazione prosegue in secondo piano mentre l'interfaccia interroga
/// periodicamente lo stato di avanzamento: una generazione su centinaia di
/// strutture supererebbe qualunque tempo di attesa accettabile in una
/// singola richiesta.
/// </summary>
public interface IServizioLottiDvr
{
    Guid Avvia(IReadOnlyList<SchedaDvr> schede, int? idUtente, string? versione);
    StatoLottoDvr? Stato(Guid idLotto);
}

/// <inheritdoc />
public class ServizioLottiDvr : IServizioLottiDvr
{
    private readonly IServiceProvider _servizi;
    private readonly ILogger<ServizioLottiDvr> _log;

    /// <summary>
    /// Stato dei lotti in memoria. Sufficiente per un'operazione che dura
    /// pochi minuti: un riavvio dell'applicazione durante l'elaborazione
    /// interrompe il lotto, ma i documenti già prodotti restano in archivio
    /// e registrati a database.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, StatoLottoDvr> _lotti = new();

    public ServizioLottiDvr(IServiceProvider servizi, ILogger<ServizioLottiDvr> log)
    {
        _servizi = servizi;
        _log = log;
    }

    public Guid Avvia(IReadOnlyList<SchedaDvr> schede, int? idUtente, string? versione)
    {
        var idLotto = Guid.NewGuid();

        _lotti[idLotto] = new StatoLottoDvr
        {
            IdLotto = idLotto,
            Totale = schede.Count
        };

        _ = Task.Run(() => ElaboraAsync(idLotto, schede, idUtente, versione));

        return idLotto;
    }

    public StatoLottoDvr? Stato(Guid idLotto) =>
        _lotti.TryGetValue(idLotto, out var stato) ? stato : null;

    private async Task ElaboraAsync(
        Guid idLotto, IReadOnlyList<SchedaDvr> schede, int? idUtente, string? versione)
    {
        var righe = new List<RigaLottoDvr>(schede.Count);
        var riusciti = 0;
        var falliti = 0;

        // Un ambito proprio: l'elaborazione sopravvive alla richiesta HTTP
        // che l'ha avviata, quindi non può usarne i servizi
        using var ambito = _servizi.CreateScope();
        var generatore = ambito.ServiceProvider.GetRequiredService<IGeneratoreDvr>();
        var contesto = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();

        foreach (var scheda in schede)
        {
            var descrizione = $"{scheda.RagioneSociale} — {scheda.NomeInsediamento}";

            AggiornaStato(idLotto, s => s with { ElaborazioneCorrente = descrizione });

            try
            {
                var esito = await generatore.GeneraAsync(scheda);

                righe.Add(new RigaLottoDvr
                {
                    RagioneSociale = scheda.RagioneSociale,
                    NomeInsediamento = scheda.NomeInsediamento,
                    Riuscito = esito.Riuscito,
                    NomeFile = esito.NomeFile,
                    Messaggio = esito.Messaggio
                });

                if (esito.Riuscito) riusciti++; else falliti++;

                await RegistraAsync(contesto, scheda, esito, idUtente, versione, idLotto);
            }
            catch (Exception ex)
            {
                // Una struttura che fallisce non interrompe il lotto
                _log.LogError(ex, "Generazione non riuscita per {Struttura}", descrizione);
                falliti++;

                righe.Add(new RigaLottoDvr
                {
                    RagioneSociale = scheda.RagioneSociale,
                    NomeInsediamento = scheda.NomeInsediamento,
                    Riuscito = false,
                    Messaggio = ex.Message
                });
            }

            var completati = riusciti + falliti;
            AggiornaStato(idLotto, s => s with
            {
                Completati = completati,
                Riusciti = riusciti,
                Falliti = falliti,
                Righe = righe.ToList()
            });
        }

        AggiornaStato(idLotto, s => s with
        {
            Concluso = true,
            ElaborazioneCorrente = null,
            Righe = righe
        });

        _log.LogInformation(
            "Lotto DVR {Id} concluso: {Riusciti} prodotti, {Falliti} non riusciti.",
            idLotto, riusciti, falliti);
    }

    private static async Task RegistraAsync(
        ContestoLaboratorioAI contesto,
        SchedaDvr scheda,
        RisultatoGenerazioneDvr esito,
        int? idUtente,
        string? versione,
        Guid idLotto)
    {
        contesto.RegistroDvr.Add(new VoceRegistroDvr
        {
            IdCliente = scheda.IdCliente,
            IdInsediamento = scheda.IdInsediamento,
            RagioneSociale = scheda.RagioneSociale,
            NomeInsediamento = scheda.NomeInsediamento,
            Regione = scheda.Regione.ToString(),
            DataGenerazione = DateTime.UtcNow,
            IdUtente = idUtente,
            NomeFile = esito.NomeFile ?? "(non prodotto)",
            PercorsoArchivio = esito.PercorsoCompleto,
            Esito = esito.Riuscito ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            Note = esito.Messaggio,
            IdLotto = idLotto,
            VersioneApplicazione = versione ?? "n/d"
        });

        await contesto.SaveChangesAsync();
    }

    private void AggiornaStato(Guid idLotto, Func<StatoLottoDvr, StatoLottoDvr> modifica)
    {
        _lotti.AddOrUpdate(idLotto,
            _ => new StatoLottoDvr { IdLotto = idLotto, Totale = 0 },
            (_, esistente) => modifica(esistente));
    }
}
