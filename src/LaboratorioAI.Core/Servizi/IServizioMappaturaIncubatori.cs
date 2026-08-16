using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Motore di calcolo della mappatura di apparecchiature termostatiche
/// (Modulo Statistico 04-03): budget d'incertezza, intervallo ammesso, medie per
/// ripiano e test statistici (F, Cochran, Kruskal-Wallis) per il giudizio.
/// </summary>
public interface IServizioMappaturaIncubatori
{
    RisultatoMappatura Calcola(DatiMappatura dati);
}
