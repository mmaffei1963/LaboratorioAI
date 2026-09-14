using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Fonte dati DB del monitoraggio temperature: i due misuratori Econorma.</summary>
public enum FonteTempLogDb { Misure = 1, Misure1 = 2 }

/// <summary>
/// Legge le misure di temperatura dal database MonitorTemperature (Econorma), producendo la
/// stessa struttura del file notturno (<see cref="FileTempLog"/>) così da riusare l'identica
/// pipeline di monitoraggio (escursioni, report, validazione).
/// </summary>
public interface ILettoreDbTempLog
{
    /// <summary>Letture di un misuratore nell'intervallo [dal, al), per canale (CodiceSonda).</summary>
    Task<FileTempLog> LeggiAsync(FonteTempLogDb fonte, DateTime dal, DateTime al, CancellationToken ct = default);

    /// <summary>Limiti per canale da MonitorTemperature: Apparecchiature (LimiteMin/Max, ValoreNominale) via AssegnazioneSonde.</summary>
    Task<IReadOnlyList<LimitiCanale>> LimitiAsync(IReadOnlyList<string> canali, CancellationToken ct = default);
}
