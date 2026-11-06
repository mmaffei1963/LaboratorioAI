using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Esito del controllo di un singolo laboratorio.</summary>
public enum EsitoControlloAccreditamento { Invariato, NuovaRevisioneTrovata, Errore }

public record RisultatoControlloAccreditamento(
    int IdAccreditamento,
    string NomeLaboratorio,
    EsitoControlloAccreditamento Esito,
    int? NumeroRevisioneTrovata,
    string? MessaggioErrore);

/// <summary>Esito della ri-estrazione del testo per una singola revisione già presente in archivio.</summary>
public enum EsitoRiestrazioneTesto { TestoEstratto, NessunTestoEstratto, FileNonTrovato, Errore }

public record RisultatoRiestrazioneTesto(
    int IdRevisione,
    string NomeLaboratorio,
    int NumeroRevisione,
    EsitoRiestrazioneTesto Esito,
    int? CaratteriEstratti,
    string? MessaggioErrore);

/// <summary>Esito del ri-scarico da ACCREDIA del PDF di una singola revisione con file corrotto.</summary>
public enum EsitoRiscaricoPdf { PdfRiscaricato, Errore }

public record RisultatoRiscaricoPdf(
    int IdRevisione,
    string NomeLaboratorio,
    int NumeroRevisione,
    EsitoRiscaricoPdf Esito,
    int? CaratteriEstratti,
    string? MessaggioErrore);

/// <summary>
/// Verifica periodica (notturna) delle revisioni dell'elenco prove ACCREDIA dei laboratori
/// esterni attivi, e gestione della conferma umana di una nuova revisione rilevata.
///
/// Non scrive mai su ArcLab: legge solo il numero/sede di accreditamento registrati in
/// <see cref="AccreditamentoLabEsterno"/> (database proprio) e interroga il sito pubblico
/// di ACCREDIA. Il download di un PDF diverso da quello noto NON lo rende automaticamente
/// "ufficiale": resta da confermare finché un operatore non verifica l'elenco.
/// </summary>
public interface IServizioAccreditamentiEsterni
{
    /// <summary>
    /// Controlla tutti i laboratori attivi, scarica il PDF di ogni nuova revisione trovata
    /// e la registra come "da confermare". Non invia email: il chiamante decide se e come
    /// notificare in base ai risultati restituiti.
    /// </summary>
    Task<IReadOnlyList<RisultatoControlloAccreditamento>> ControllaTuttiAsync(CancellationToken ct = default);

    /// <summary>Conferma una revisione rilevata come valida: diventa la revisione ufficiale del laboratorio.</summary>
    Task ConfermaRevisioneAsync(int idRevisione, int idUtente, string? note, CancellationToken ct = default);

    /// <summary>Elenco dei laboratori monitorati con le relative revisioni (confermate e in attesa).</summary>
    Task<IReadOnlyList<AccreditamentoLabEsterno>> ElencoAsync(CancellationToken ct = default);

    /// <summary>Revisioni non ancora confermate, più recenti prima.</summary>
    Task<IReadOnlyList<RevisioneAccreditamentoEsterno>> RevisioniDaConfermareAsync(CancellationToken ct = default);

    /// <summary>Storico delle revisioni di un laboratorio, più recenti prima.</summary>
    Task<IReadOnlyList<RevisioneAccreditamentoEsterno>> StoricoRevisioniAsync(int idAccreditamento, CancellationToken ct = default);

    /// <summary>
    /// Rilegge dal PDF già archiviato (nessuna nuova chiamata ad ACCREDIA) e ri-estrae il testo per
    /// tutte le revisioni con <c>TestoEstratto</c> ancora nullo — quelle rilevate prima che il
    /// fallback pdftotext fosse disponibile. Un uso una tantum, richiamabile finché ce ne sono.
    /// </summary>
    Task<IReadOnlyList<RisultatoRiestrazioneTesto>> RiestraiTestoMancanteAsync(CancellationToken ct = default);

    /// <summary>
    /// Individua le revisioni il cui PDF archiviato è mancante o corrotto (file assente, troppo
    /// piccolo, o privo dell'intestazione "%PDF" tipica di un PDF valido — capita quando un
    /// download da ACCREDIA si interrompe a metà) e lo riscarica da ACCREDIA, sovrascrivendo solo
    /// file/hash/testo estratto: non tocca conferma, note o storico della revisione.
    /// </summary>
    Task<IReadOnlyList<RisultatoRiscaricoPdf>> RiscaricaPdfCorrottiAsync(CancellationToken ct = default);
}
