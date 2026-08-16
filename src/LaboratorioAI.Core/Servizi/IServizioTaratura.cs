using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Motore di calcolo della taratura dei termometri (confronto con CDR, budget
/// d'incertezza GUM). Deterministico: nessuna dipendenza esterna, testabile.
/// </summary>
public interface IServizioTaratura
{
    RisultatoTaratura Calcola(DatiTaratura dati);
}
