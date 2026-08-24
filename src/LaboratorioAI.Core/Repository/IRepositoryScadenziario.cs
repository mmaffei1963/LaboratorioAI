using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Accesso SOLA LETTURA ad ArcLab per lo scadenziario delle tarature degli strumenti interni.</summary>
public interface IRepositoryScadenziario
{
    /// <summary>
    /// Strumenti interni con taratura (IdAttività 25/26) in scadenza entro
    /// <paramref name="giorniPreavviso"/> giorni oppure mai eseguita, solo piani attivi
    /// e apparecchiature in uso. Ogni voce è già classificata per gruppo.
    /// </summary>
    Task<IReadOnlyList<VoceScadenziarioInterno>> ScadenziarioInterniAsync(
        int giorniPreavviso = 30, CancellationToken ct = default);
}
