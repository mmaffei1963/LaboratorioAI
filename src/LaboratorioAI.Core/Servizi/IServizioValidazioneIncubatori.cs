using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Validazione periodica del motore di calcolo delle tarature di incubatori/celle:
/// esegue il calcolo sui casi di riferimento congelati e confronta ogni grandezza
/// con il valore atteso indipendente (foglio storico), entro tolleranza.
/// </summary>
public interface IServizioValidazioneIncubatori
{
    EsitoValidazioneCalcoli Esegui(string versioneApplicazione);
}
