using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Offerte sul portale (17/11/2026): commesse di laboratorio attive da ArcLab, SOLA LETTURA.</summary>
public interface IRepositoryOfferteClienti
{
    /// <summary>
    /// Commesse di laboratorio (IdTipoCommessa 3) attive (senza scadenza incarico, non chiuse), con controllo e prezzo
    /// maggiore di zero, comprese le «non fatturare». Di un cliente, o di tutti se <paramref name="idCliente"/> è nullo
    /// (esclusa MARE.A stessa, MIRKO1).
    /// </summary>
    Task<IReadOnlyList<CommessaOffertaArcLab>> CommesseAsync(int? idCliente, CancellationToken ct = default);

    /// <summary>Dati del cliente per la testata dell'offerta.</summary>
    Task<ClienteOffertaArcLab?> ClienteAsync(int idCliente, CancellationToken ct = default);
}
