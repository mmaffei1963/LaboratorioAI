using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LaboratorioAI.Tracciabilita.Dati;

/// <summary>
/// Protezioni che vivono dentro SQL Server e che EF Core non sa descrivere con le migrazioni:
/// <list type="bullet">
/// <item>trigger che vietano di modificare o cancellare <c>MovimentiLotto</c> e <c>Audit</c>;</item>
/// <item>sicurezza a livello di riga (RLS): ogni sessione vede solo l'azienda indicata in SESSION_CONTEXT.</item>
/// </list>
/// Tutto è ripetibile senza danni: si può eseguire a ogni avvio. La sicurezza a livello di riga
/// nasce SPENTA (<c>STATE = OFF</c>) e si accende con <paramref name="attivaRls"/> solo dopo il collaudo
/// su un database di prova.
///
/// Attenzione: gli utenti SQL con ruoli molto alti (sysadmin, db_owner) possono aggirare o spegnere
/// queste protezioni. L'account con cui l'applicazione lavora di norma dovrebbe avere solo
/// lettura e scrittura sui dati, e un account separato dovrebbe fare le migrazioni.
/// Da verificare sul database di prova prima della produzione.
/// </summary>
public static class VincoliSicurezzaSql
{
    private const string NomeFunzione = "trac.fn_FiltroAzienda";
    private const string NomePolitica = "trac.PoliticaAzienda";

    public static async Task ApplicaAsync(ContestoTracciabilita db, bool attivaRls, CancellationToken ct = default)
    {
        if (!db.Gestore)
            throw new InvalidOperationException("I vincoli di sicurezza si applicano con il contesto del gestore.");

        foreach (var istruzione in Triggers())
            await db.Database.ExecuteSqlRawAsync(istruzione, ct);

        await ApplicaRlsAsync(db, attivaRls, ct);
    }

    /// <summary>Le istruzioni dei trigger (una per volta: CREATE TRIGGER deve stare da solo in un blocco).</summary>
    internal static IEnumerable<string> Triggers()
    {
        yield return TriggerSolaAggiunta("MovimentiLotto", "IdMovimento", "TR_MovimentiLotto_SolaAggiunta",
            "MovimentiLotto è un registro in sola aggiunta: non si modifica né si cancella. Registrare uno storno.");
        yield return TriggerSolaAggiunta("Audit", "IdAudit", "TR_Audit_SolaAggiunta",
            "Audit è un registro in sola aggiunta: non si modifica né si cancella.");
    }

    // La cancellazione fisica passa solo se la sessione l'ha dichiarato (chiusura del servizio, solo gestore).
    private static string TriggerSolaAggiunta(string tabella, string chiave, string nomeTrigger, string messaggio) => $@"
CREATE OR ALTER TRIGGER trac.[{nomeTrigger}] ON trac.[{tabella}]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM inserted)
       AND CAST(SESSION_CONTEXT(N'ConsentiEliminazione') AS int) = 1
       AND CAST(SESSION_CONTEXT(N'Gestore') AS int) = 1
    BEGIN
        DELETE t FROM trac.[{tabella}] t INNER JOIN deleted d ON d.[{chiave}] = t.[{chiave}];
        RETURN;
    END;
    THROW 51000, N'{messaggio}', 1;
END;";

    private static async Task ApplicaRlsAsync(ContestoTracciabilita db, bool attiva, CancellationToken ct)
    {
        // Tabelle con la colonna IdAzienda, ricavate dal modello (non da un elenco scritto a mano).
        var tabelle = db.Model.GetEntityTypes()
            .Where(t => t.FindProperty("IdAzienda") is not null && t.GetTableName() is not null)
            .Select(t => t.GetTableName()!)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        // La funzione: una riga è visibile al gestore o all'azienda indicata nella sessione.
        // Si crea solo se manca, perché non si può modificare mentre la politica la usa.
        await db.Database.ExecuteSqlRawAsync($@"
IF OBJECT_ID(N'{NomeFunzione}') IS NULL
    EXEC(N'CREATE FUNCTION {NomeFunzione}(@IdAzienda int)
    RETURNS TABLE
    WITH SCHEMABINDING
    AS
    RETURN SELECT 1 AS Consentito
    WHERE CAST(SESSION_CONTEXT(N''Gestore'') AS int) = 1
       OR (@IdAzienda IS NOT NULL AND @IdAzienda = CAST(SESSION_CONTEXT(N''IdAzienda'') AS int));');", ct);

        // Quanti predicati ha oggi la politica (filtro + 2 blocchi per tabella).
        var attesi = tabelle.Count * 3;
        var esistenti = await Scalare(db, $@"
SELECT COUNT(*) FROM sys.security_predicates p
INNER JOIN sys.security_policies s ON s.object_id = p.object_id
WHERE s.name = N'PoliticaAzienda' AND s.schema_id = SCHEMA_ID(N'trac');", ct);

        if (esistenti != attesi)
        {
            await db.Database.ExecuteSqlRawAsync($"DROP SECURITY POLICY IF EXISTS {NomePolitica};", ct);

            var predicati = string.Join(",\n", tabelle.Select(t => $@"
    ADD FILTER PREDICATE {NomeFunzione}([IdAzienda]) ON trac.[{t}],
    ADD BLOCK PREDICATE {NomeFunzione}([IdAzienda]) ON trac.[{t}] AFTER INSERT,
    ADD BLOCK PREDICATE {NomeFunzione}([IdAzienda]) ON trac.[{t}] AFTER UPDATE"));

            // Appena creata resta spenta: si accende dopo il collaudo.
            await db.Database.ExecuteSqlRawAsync(
                $"CREATE SECURITY POLICY {NomePolitica}{predicati}\nWITH (STATE = OFF, SCHEMABINDING = ON);", ct);
        }

        await db.Database.ExecuteSqlRawAsync(
            $"ALTER SECURITY POLICY {NomePolitica} WITH (STATE = {(attiva ? "ON" : "OFF")});", ct);
    }

    private static async Task<int> Scalare(ContestoTracciabilita db, string sql, CancellationToken ct)
    {
        var connessione = db.Database.GetDbConnection();
        var eraChiusa = connessione.State != System.Data.ConnectionState.Open;
        if (eraChiusa) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = connessione.CreateCommand();
            cmd.CommandText = sql;
            var r = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt32(r ?? 0);
        }
        finally
        {
            if (eraChiusa) await db.Database.CloseConnectionAsync();
        }
    }
}
