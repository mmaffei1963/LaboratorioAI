using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Validazione periodica del motore bilance sul caso EURAMET congelato.</summary>
public interface IServizioValidazioneBilancia
{
    EsitoValidazioneCalcoli Esegui(string versioneApplicazione);
}
