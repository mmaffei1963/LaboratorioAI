using DocumentFormat.OpenXml.Packaging;
using LaboratorioAI.Core.Manualistica;
using LaboratorioAI.Documenti.Dvr;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>
/// Controllo dei marcatori di un modello della manualistica (01/11/2026).
///
/// Apre il .docx, raccoglie i tag dei controlli contenuto — corpo,
/// intestazioni e piè di pagina — e li passa al vocabolario, che dice
/// se il motore li sa interpretare tutti.
///
/// Il file non viene modificato: si apre in sola lettura su una copia in
/// memoria. L'esito non si salva: si ricalcola ogni volta che serve, così
/// resta allineato al vocabolario anche quando il motore cresce.
/// </summary>
public static class ControlloMarcatori
{
    /// <summary>
    /// Verifica un modello.
    /// </summary>
    /// <param name="nomeFile">Nome del file, per il riepilogo.</param>
    /// <param name="contenutoDocx">Il modello.</param>
    /// <param name="codiciFunzione">Codici delle funzioni aziendali del tipo di manuale.</param>
    /// <param name="campiNoti">Campi già visti: quelli nuovi diventano un avviso, non un errore.</param>
    /// <param name="richiedeMarcatori">Falso per i modelli di servizio, che di marcatori non ne hanno.</param>
    public static EsitoControlloModello Verifica(
        string nomeFile,
        byte[] contenutoDocx,
        IReadOnlyCollection<string> codiciFunzione,
        IReadOnlySet<string>? campiNoti = null,
        bool richiedeMarcatori = true)
    {
        List<string> tag;
        try
        {
            using var flusso = new MemoryStream(contenutoDocx, writable: false);
            using var documento = WordprocessingDocument.Open(flusso, isEditable: false);
            tag = ControlliContenuto.Tutti(documento)
                .Select(ControlliContenuto.Tag)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .ToList();
        }
        catch (Exception ex)
        {
            return new EsitoControlloModello(
                nomeFile, 0,
                new[] { $"Il file non si apre come documento Word: {ex.Message}" },
                Array.Empty<string>(),
                new Dictionary<string, int>());
        }

        return VocabolarioMarcatori.Verifica(nomeFile, tag, codiciFunzione, campiNoti, richiedeMarcatori);
    }

    /// <summary>
    /// I campi (<c>CAMPO:</c>) presenti in un modello. Serve a costruire
    /// l'insieme dei campi già noti, con cui gli altri modelli si
    /// controllano senza segnalare come nuovo ciò che nuovo non è.
    /// </summary>
    public static IReadOnlyCollection<string> Campi(byte[] contenutoDocx)
    {
        try
        {
            using var flusso = new MemoryStream(contenutoDocx, writable: false);
            using var documento = WordprocessingDocument.Open(flusso, isEditable: false);
            return ControlliContenuto.Tutti(documento)
                .Select(ControlliContenuto.Tag)
                .Where(t => t is not null && t.StartsWith(VocabolarioMarcatori.Campo + ":", StringComparison.Ordinal))
                .Select(t => t![(VocabolarioMarcatori.Campo.Length + 1)..])
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
