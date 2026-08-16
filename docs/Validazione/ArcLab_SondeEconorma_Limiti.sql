/* ============================================================================
   ArcLab (LabAnalisiDati) — SOLA LETTURA
   Elenco sonde Econorma con lo strumento/ambiente associato e i limiti
   (VN, INC, MAX, MIN) presi dal PianoInterventi "IO 04 02" (fallback "Taratura").

   Serve a popolare la tabella di mappatura Canale <-> Strumento del modulo
   "Monitoraggio TempLog" di Laboratorio AI.

   Logica:
     - la SONDA e' un'apparecchiatura il cui NomeStrum contiene "Econorma ... Sonda T<n>";
     - il suo Associato1 e' il testo dell'AMBIENTE monitorato, che termina con "Cod: <IdApparecchio>";
     - su quell'IdApparecchio si cerca in PianoInterventi il piano attivo IO 04 02 (o NomePiano "Taratura");
     - VN = ValTeorico ; INC = (LimSupInt - LimInfInt)/2 ; MAX = VN+INC ; MIN = VN-INC.

   Le sonde senza piano risolto compaiono comunque (VN/INC a NULL): sono quelle
   da sistemare (turnover: sonda nuova, ambiente cambiato, piano mancante).
   ============================================================================ */

SELECT
    -- token canale ricavato dal nome (Sonda T1 -> T01), utile per la mappatura
    'T' + RIGHT('0' + ch.Numero, 2)                     AS CanaleIpotesi,
    s.IdStrumento                                       AS IdStrumentoSonda,
    LTRIM(RTRIM(s.NomeStrum))                           AS SondaNomeStrum,
    aSonda.IdApparecchio                                AS IdApparecchioSonda,
    aSonda.NonInUso                                     AS SondaNonInUso,
    aSonda.Associato1                                   AS AmbienteTesto,
    amb.IdApparecchioAmbiente,
    pi.CodPiano,
    pi.NomePiano,
    pi.ValTeorico                                       AS VN,
    pi.LimInfInt,
    pi.LimSupInt,
    CAST((pi.LimSupInt - pi.LimInfInt) / 2.0 AS decimal(10,3)) AS INC,
    pi.ValTeorico + (pi.LimSupInt - pi.LimInfInt) / 2.0 AS MAXlim,
    pi.ValTeorico - (pi.LimSupInt - pi.LimInfInt) / 2.0 AS MINlim
FROM Strumenti s
    INNER JOIN apparecchiature aSonda
        ON aSonda.IdStrumento = s.IdStrumento
    /* numero della sonda: testo dopo "Sonda T" fino al primo spazio (es. "1", "8", "12") */
    CROSS APPLY (
        SELECT LTRIM(LEFT(coda, CHARINDEX(' ', coda + ' ') - 1)) AS Numero
        FROM (SELECT SUBSTRING(s.NomeStrum, PATINDEX('%Sonda T%', s.NomeStrum) + 7, 10) AS coda) t
    ) ch
    /* estrae il numero dopo 'Cod:' dall'Associato1 della sonda = IdApparecchio ambiente */
    CROSS APPLY (
        SELECT TRY_CONVERT(int, LTRIM(RTRIM(
            RIGHT(aSonda.Associato1,
                  LEN(aSonda.Associato1) - CHARINDEX('Cod:', aSonda.Associato1) - 3)))) AS IdApparecchioAmbiente
    ) amb
    /* piano dell'ambiente: SOLO TARATURA (mai MAPPATURA).
       Match tollerante sul CodPiano (ignora spazi/trattini/punti -> "IO0402"). */
    OUTER APPLY (
        SELECT TOP 1 p.CodPiano, p.NomePiano, p.ValTeorico, p.LimInfInt, p.LimSupInt
        FROM PianoInterventi p
        WHERE p.IdApparecchio = amb.IdApparecchioAmbiente
          AND p.Attivo = 1
          AND p.NomePiano LIKE '%TARATURA%'
          AND p.NomePiano NOT LIKE '%MAPPATURA%'
        ORDER BY CASE
            WHEN REPLACE(REPLACE(REPLACE(p.CodPiano,' ',''),'-',''),'.','') LIKE '%IO0402%'
            THEN 0 ELSE 1 END
    ) pi
WHERE s.NomeStrum LIKE '%Econorma%Sonda T%'
ORDER BY CanaleIpotesi;
