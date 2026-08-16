using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Motore di calcolo della taratura dei misuratori di incubatori/celle
/// (Modulo Statistico 04-02), verificato sui casi VAL del foglio storico.
/// </summary>
public interface IServizioTaraturaIncubatori
{
    RisultatoTaraturaIncubatore Calcola(DatiTaraturaIncubatore dati);
}
