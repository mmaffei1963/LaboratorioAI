using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Motore di calcolo della taratura di bilance analitiche (Modulo 45, EURAMET
/// Calibration Guide N. 18): prova di eccentricità, ripetibilità, linearità e
/// budget d'incertezza per punto di carico. Verificato sull'esempio EURAMET.
/// </summary>
public interface IServizioBilancia
{
    RisultatoBilancia Calcola(DatiBilancia dati);
}
