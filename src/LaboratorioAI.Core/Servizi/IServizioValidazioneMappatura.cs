using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Validazione periodica del motore di calcolo della mappatura sui casi congelati.</summary>
public interface IServizioValidazioneMappatura
{
    EsitoValidazioneCalcoli Esegui(string versioneApplicazione);
}
