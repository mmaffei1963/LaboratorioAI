using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace LaboratorioAI.Dati.ArcLab;

/// <summary>Apre connessioni verso il database gestionale ArcLab.</summary>
public interface IFabbricaConnessioniArcLab
{
    Task<IDbConnection> ApriAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public class FabbricaConnessioniArcLab : IFabbricaConnessioniArcLab
{
    private readonly string _stringaConnessione;

    public FabbricaConnessioniArcLab(IConfiguration configurazione)
    {
        _stringaConnessione = configurazione.GetConnectionString("ArcLab")
            ?? throw new InvalidOperationException(
                "Stringa di connessione 'ArcLab' non configurata.");
    }

    public async Task<IDbConnection> ApriAsync(CancellationToken ct = default)
    {
        var connessione = new SqlConnection(_stringaConnessione);
        await connessione.OpenAsync(ct);
        return connessione;
    }
}
