using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Validazione periodica del motore di calcolo della taratura: esegue il calcolo
/// sui casi di riferimento congelati e confronta ogni parametro con il valore
/// atteso indipendente (foglio storico), entro tolleranza.
/// </summary>
public interface IServizioValidazioneTaratura
{
    EsitoValidazioneCalcoli Esegui(string versioneApplicazione);
}
