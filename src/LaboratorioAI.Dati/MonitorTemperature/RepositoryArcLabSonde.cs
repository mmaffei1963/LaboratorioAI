using Dapper;
using LaboratorioAI.Dati.ArcLab;
using LaboratorioAI.Dati.MonitorTemperature.Models;

namespace LaboratorioAI.Dati.MonitorTemperature
{
    /// <summary>
    /// Lettura delle sonde di temperatura attive da ArcLab (LabAnalisiDati), usata
    /// per la sincronizzazione dell'anagrafica MappaSonde. Sola lettura, nessuna
    /// eccezione: usa <see cref="IFabbricaConnessioniArcLab"/> come tutto il resto
    /// dell'applicazione.
    /// </summary>
    public interface IRepositoryArcLabSonde
    {
        Task<IEnumerable<SondaArcLab>> GetSondeAttiveAsync(CancellationToken ct = default);
    }

    /// <inheritdoc />
    public class RepositoryArcLabSonde : IRepositoryArcLabSonde
    {
        private readonly IFabbricaConnessioniArcLab _fabbrica;

        public RepositoryArcLabSonde(IFabbricaConnessioniArcLab fabbrica)
        {
            _fabbrica = fabbrica;
        }

        public async Task<IEnumerable<SondaArcLab>> GetSondeAttiveAsync(CancellationToken ct = default)
        {
            const string sql = @"
                SELECT
                    sonda.IdApparecchio AS IdApparecchioSonda,
                    s.IdStrumento AS IdStrumentoSonda,
                    s.NomeStrum AS NomeSonda,
                    sonda.nSerieApp AS SerialeSonda,

                    app.IdApparecchio AS IdApparecchioAssociato,
                    app.IdStrumento AS IdStrumentoAssociato,
                    sa.NomeStrum AS NomeApparecchiatura,
                    app.nSerieApp AS SerialeApparecchiatura,

                    p.IdPianoInt,
                    p.NomePiano,
                    p.ValImpostato,
                    p.LimInfInt,
                    p.LimSupInt,
                    p.IncMaxAmmessa,

                    sonda.Associato1

                FROM dbo.Apparecchiature sonda

                INNER JOIN dbo.Strumenti s
                    ON sonda.IdStrumento = s.IdStrumento

                INNER JOIN dbo.Apparecchiature app
                    ON app.IdApparecchio =
                        TRY_CAST(
                            LTRIM(
                                SUBSTRING(
                                    sonda.Associato1,
                                    CHARINDEX('Cod:', sonda.Associato1) + 4,
                                    20
                                )
                            ) AS INT
                        )

                INNER JOIN dbo.Strumenti sa
                    ON app.IdStrumento = sa.IdStrumento

                LEFT JOIN dbo.PianoInterventi p
                    ON p.IdApparecchio = app.IdApparecchio
                    AND p.IdAttività = 26
                    AND p.NomePiano LIKE 'TARATURA%'
                    AND p.Attivo = 1

                WHERE
                    s.NomeStrum LIKE 'Econorma FT 105\RF - Sonda%'
                    AND ISNULL(sonda.NonInUso, 0) = 0
                    AND ISNULL(app.NonInUso, 0) = 0
                    AND CHARINDEX('Cod:', sonda.Associato1) > 0

                ORDER BY
                    s.NomeStrum;
            ";

            using var connessione = await _fabbrica.ApriAsync(ct);

            var risultato = await connessione.QueryAsync<SondaArcLab>(
                new CommandDefinition(sql, cancellationToken: ct));

            return risultato;
        }
    }
}
