namespace LaboratorioAI.Dati.ArcLab;

/// <summary>
/// Costanti dipendenti dalla struttura dati di ArcLab.
///
/// Raccolte qui perché sono conoscenza sul gestionale, non sul dominio:
/// se ArcLab cambia, si interviene in un punto solo.
/// </summary>
internal static class CostantiArcLab
{
    /// <summary>
    /// Riga di metodo tecnica presente su ogni campione, da escludere
    /// dai conteggi e dagli elenchi delle prove.
    /// </summary>
    public const int IdRigaMetodoDaEscludere = 18963;

    /// <summary>Giorni oltre i quali un campione privo di prove è considerato spedito in originale.</summary>
    public const int GiorniPerSpeditoOriginale = 10;

    /// <summary>Tipo commessa corrispondente alle attività di laboratorio.</summary>
    public const int TipoCommessaLaboratorio = 3;

    /// <summary>
    /// CTE (da anteporre con WITH) con l'insieme dei certificati esterni caricati nel portale
    /// (Marea.Entries), parsato UNA sola volta: anno + lotto/campione + cliente. Va poi agganciato
    /// con <see cref="JoinEstPortale"/>. Molto più veloce di una EXISTS correlata per riga.
    /// </summary>
    public const string CteEstPortale = """
        EstPortale AS (
            SELECT DISTINCT
                2000 + CAST(SUBSTRING(en.NomeFile, 7, 2) AS int) AS Anno,
                TRY_CAST(LEFT(SUBSTRING(en.NomeFile,10,4000), NULLIF(CHARINDEX('-', SUBSTRING(en.NomeFile,10,4000)),0)-1) AS int) AS Lotto,
                TRY_CAST(LEFT(STUFF(SUBSTRING(en.NomeFile,10,4000),1,CHARINDEX('-',SUBSTRING(en.NomeFile,10,4000)),''),
                              NULLIF(CHARINDEX('-', STUFF(SUBSTRING(en.NomeFile,10,4000),1,CHARINDEX('-',SUBSTRING(en.NomeFile,10,4000)),'')+'-'),0)-1) AS int) AS Campione,
                LTRIM(RTRIM(CONVERT(nvarchar(50), mc.CodContabilita))) AS CodContab
            FROM [Marea].dbo.Entries en
                INNER JOIN [Marea].dbo.Clienti mc ON mc.Id = en.IDCliente
            WHERE en.Tipo LIKE '%Certificato Esterno%'
              AND en.NomeFile LIKE '[0-9][0-9]-[0-9][0-9]-[0-9][0-9]-%'
        )
        """;

    /// <summary>LEFT JOIN alla CTE EstPortale (alias lo, ca, cl richiesti). ep.Anno non nullo ⇒ esterno nel portale.</summary>
    public const string JoinEstPortale = """
        LEFT JOIN EstPortale ep
            ON ep.Anno = YEAR(lo.DataRic)
           AND ep.Lotto = TRY_CAST(lo.CodLotto AS int)
           AND ep.Campione = ca.CodCampione
           AND ep.CodContab COLLATE DATABASE_DEFAULT = LTRIM(RTRIM(CONVERT(nvarchar(50), cl.CODContabilita)))
        """;
}
