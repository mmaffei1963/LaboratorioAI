namespace LaboratorioAI.Dati.Accredia;

/// <summary>
/// Una voce inviata dall'agente esterno (<c>strumenti/AgenteAccredia</c>): l'esito del controllo
/// ACCREDIA per un singolo laboratorio/sede, già scaricato altrove. <see cref="NumeroRevisione"/>
/// nullo significa "nessuna revisione trovata" (o controllo fallito lato agente); in quel caso
/// <see cref="PdfBase64"/> è sempre nullo.
/// </summary>
public record VoceImportazioneAccredia(
    string NumeroAccreditamento,
    string Sede,
    int? NumeroRevisione,
    DateTime? DataEmissione,
    string? PdfBase64,
    // Stato nella banca dati ACCREDIA («Accreditato», «Sospeso», «NonTrovato»), dall'08/10/2026; null se l'agente
    // non è riuscito a leggerlo o è una versione precedente (in quel caso lo stato salvato non cambia).
    string? StatoAccreditamento = null,
    // Prove con sospensione attiva (righe con asterisco), dall'08/10/2026: lista vuota = nessuna prova sospesa;
    // null = non lette (agente precedente o lettura del dettaglio non riuscita): le prove salvate non cambiano.
    IReadOnlyList<ProvaSospesaLetta>? ProveSospese = null);

/// <summary>Una riga con asterisco dell'elenco prove ACCREDIA di un laboratorio (prova con sospensione attiva).</summary>
public record ProvaSospesaLetta(string? Matrice, string? Prova, string? Norma, string? Tecnica);

/// <summary>
/// Variante "offline" di <see cref="IClientAccredia"/> che non chiama la rete: risponde con dati
/// già raccolti altrove (l'agente <c>strumenti/AgenteAccredia</c>, eseguito su un PC con accesso
/// diretto a certificati.accredia.it) e passati come payload di una richiesta HTTP in ingresso.
///
/// Introdotta il 29/09/2026 al posto del relay "pull" (<c>ClientAccrediaViaRelay</c>, rimosso):
/// quel relay avrebbe richiesto che il server di produzione raggiungesse un servizio in ascolto
/// sul PC di Mirko — cosa che, essendo la produzione ospitata fuori dalla rete aziendale (vedi
/// commento rimosso in ClientAccrediaViaRelay.cs), avrebbe significato esporre quel PC verso
/// internet. Con questa variante è l'agente a chiamare (in uscita, come un client qualunque)
/// l'endpoint di importazione già pubblico del server — nessuna porta da aprire da nessuna parte.
///
/// Riusa <see cref="LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.ServizioAccreditamentiEsterni"/>
/// così com'è (stessa logica di confronto/salvataggio/hash, nessuna duplicazione): viene costruito
/// al volo con questo client al posto di quello registrato in DI, solo per la singola richiesta di
/// importazione.
/// </summary>
public class ClientAccrediaDaImportazione : IClientAccredia
{
    private readonly Dictionary<(string Numero, string Sede), VoceImportazioneAccredia> _voci;

    public ClientAccrediaDaImportazione(IEnumerable<VoceImportazioneAccredia> voci) =>
        _voci = voci.ToDictionary(v => (v.NumeroAccreditamento, v.Sede));

    public Task<UltimaRevisioneAccredia?> UltimaRevisioneAsync(
        string numeroAccreditamento, string sede, CancellationToken ct = default)
    {
        if (!_voci.TryGetValue((numeroAccreditamento, sede), out var voce) || voce.NumeroRevisione is null)
            return Task.FromResult<UltimaRevisioneAccredia?>(null);

        return Task.FromResult<UltimaRevisioneAccredia?>(
            new UltimaRevisioneAccredia(voce.NumeroRevisione.Value, voce.DataEmissione));
    }

    public Task<byte[]> ScaricaPdfRevisioneAsync(
        string numeroAccreditamento, string sede, int numeroRevisione, CancellationToken ct = default)
    {
        if (!_voci.TryGetValue((numeroAccreditamento, sede), out var voce) || voce.PdfBase64 is null)
            throw new InvalidOperationException(
                $"L'agente non ha incluso il PDF per {numeroAccreditamento}/{sede} (revisione {numeroRevisione}).");

        return Task.FromResult(Convert.FromBase64String(voce.PdfBase64));
    }
}
