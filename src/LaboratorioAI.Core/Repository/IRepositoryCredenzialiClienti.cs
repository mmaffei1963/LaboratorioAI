namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Password del portale (17/11/2026): dati del cliente ArcLab usati per cambio e recupero della password, SOLA LETTURA.
/// La partita IVA va restituita solo al servizio delle password, che la confronta in memoria e la scarta.
/// </summary>
public interface IRepositoryCredenzialiClienti
{
    Task<CredenzialiClienteArcLab?> DatiAsync(int idCliente, CancellationToken ct = default);
}

/// <summary>Codice contabilità, partita IVA ed email del cliente in ArcLab (Clienti.email).</summary>
public record CredenzialiClienteArcLab(int IdCliente, string? CodiceContabilita, string? RagioneSociale, string? PartitaIva, string? Email)
{
    /// <summary>Indirizzi email validi del campo Clienti.email (separati da ; , spazi o a capo).</summary>
    public IReadOnlyList<string> IndirizziEmail =>
        (Email ?? "").Split(new[] { ';', ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Contains('@') && x.IndexOf('@') > 0 && x.IndexOf('.', x.IndexOf('@')) > x.IndexOf('@') + 1)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
