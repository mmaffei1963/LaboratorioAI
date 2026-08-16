using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Motore di calcolo dei valori nutrizionali di una ricetta, conforme al
/// Reg. UE 1169/2011. Servizio di puro calcolo: nessuna dipendenza esterna,
/// deterministico e testabile.
/// </summary>
public interface IServizioCalcoloNutrizionale
{
    /// <summary>Calcola la dichiarazione nutrizionale a partire da ricetta e ingredienti.</summary>
    RisultatoNutrizionale Calcola(DatiCalcoloNutrizionale dati);
}
