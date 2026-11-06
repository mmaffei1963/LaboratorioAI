using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LaboratorioAI.Tracciabilita.Dati;

/// <summary>
/// Ogni volta che si apre una connessione, comunica a SQL Server per quale azienda si lavora
/// (SESSION_CONTEXT). È ciò che permette alla sicurezza a livello di riga del database di
/// filtrare le righe anche se l'applicazione sbagliasse.
/// </summary>
public sealed class ContestoSessioneInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        Imposta(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await ImpostaAsync(connection, eventData, cancellationToken);
    }

    private static (object? IdAzienda, int Gestore, int Eliminazione)? Valori(ConnectionEndEventData eventData) =>
        eventData.Context is ContestoTracciabilita c
            ? (c.IdAziendaCorrente is { } id ? id : null, c.Gestore ? 1 : 0, c.ConsentiEliminazioneFisica ? 1 : 0)
            : null;

    private static void Imposta(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (Valori(eventData) is not { } v) return;
        foreach (var comando in Comandi(connection, v))
        {
            using (comando) comando.ExecuteNonQuery();
        }
    }

    private static async Task ImpostaAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct)
    {
        if (Valori(eventData) is not { } v) return;
        foreach (var comando in Comandi(connection, v))
        {
            await using (comando) await comando.ExecuteNonQueryAsync(ct);
        }
    }

    private static IEnumerable<DbCommand> Comandi(DbConnection connection, (object? IdAzienda, int Gestore, int Eliminazione) v)
    {
        yield return Comando(connection, "IdAzienda", v.IdAzienda);
        yield return Comando(connection, "Gestore", v.Gestore);
        yield return Comando(connection, "ConsentiEliminazione", v.Eliminazione);
    }

    private static DbCommand Comando(DbConnection connection, string chiave, object? valore)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key = @k, @value = @v, @read_only = 0;";

        var k = cmd.CreateParameter();
        k.ParameterName = "@k";
        k.DbType = DbType.String;
        k.Value = chiave;
        cmd.Parameters.Add(k);

        var p = cmd.CreateParameter();
        p.ParameterName = "@v";
        p.Value = valore ?? DBNull.Value;
        cmd.Parameters.Add(p);
        return cmd;
    }
}
