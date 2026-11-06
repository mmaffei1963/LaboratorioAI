namespace LaboratorioAI.Core.Repository;

/// <summary>Un metodo affidato a un laboratorio esterno (ArcLab, LabEstMetodo).</summary>
public record MetodoLabEsterno(
    int IdLabEstMetodo,
    int IdMetodo,
    string? CodMetodo,
    string? RiferimentoNormativo,
    string? NomeParametro,
    bool AccreditatoArcLab);

/// <summary>Anagrafica essenziale di un laboratorio esterno in ArcLab (LabEsterno), per il confronto del numero ACCREDIA.</summary>
public record AnagraficaLabEsterno(int IdLabEsterno, string? Nome, string? NumeroAccredia);

/// <summary>
/// Lettura sola lettura da ArcLab dei metodi che risultano affidati a un laboratorio esterno
/// (tabella LabEstMetodo), usata per la verifica dell'accreditamento ACCREDIA. Nessuna scrittura.
/// </summary>
public interface IRepositoryMetodiLabEsterno
{
    Task<IReadOnlyList<MetodoLabEsterno>> MetodiAffidatiAsync(int idLabEsterno, CancellationToken ct = default);

    /// <summary>Nome e numero ACCREDIA registrati in ArcLab per i laboratori indicati.</summary>
    Task<IReadOnlyList<AnagraficaLabEsterno>> LaboratoriAsync(IReadOnlyCollection<int> idLabEsterni, CancellationToken ct = default);
}
