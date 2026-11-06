using System.Data;
using System.Globalization;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.MonitorTemperature.Models;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace LaboratorioAI.Web.Areas.MonitorTemperature.Controllers
{
    // Differenze fra il dato originale (Misure) e quello di controllo (Misure1).
    // Query diretta sul db MonitorTemperature (connstring centralizzata, non hardcodata).
    [Area("MonitorTemperature")]
    [RichiedePermesso(Permessi.MonitorTemperatureVisualizza)]
    public class DifferenzeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IEsportatoreMonitorTemperature _esportatore;
        private readonly IServizioAudit _audit;

        public DifferenzeController(IConfiguration configuration, IEsportatoreMonitorTemperature esportatore, IServizioAudit audit)
        {
            _configuration = configuration;
            _esportatore = esportatore;
            _audit = audit;
        }

        public async Task<IActionResult> Index(
            string periodo = "24h",
            string soglia = "1.0",
            string vista = "grafici",
            string? codiceSonda = null,
            DateTime? dataDa = null,
            DateTime? dataA = null)
        {
            if (vista != "grafici" && vista != "tabella") vista = "grafici";

            var esito = await InterrogaAsync(periodo, soglia, codiceSonda, dataDa, dataA);

            ViewBag.Periodo = esito.Periodo;
            ViewBag.Soglia = esito.SogliaValore;
            ViewBag.Vista = vista;
            ViewBag.CodiceSonda = esito.CodiceNormalizzato;
            ViewBag.DataDa = dataDa;
            ViewBag.DataA = dataA;
            ViewBag.FiltroPersonalizzato = esito.FiltroPersonalizzato;
            ViewBag.SondeDisponibili = esito.SondeDisponibili;

            return View(esito.Righe);
        }

        /// <summary>Esporta in Excel la tabella differenze attualmente a video (stessi filtri di Index).</summary>
        public async Task<IActionResult> EsportaExcel(
            string periodo = "24h",
            string soglia = "1.0",
            string? codiceSonda = null,
            DateTime? dataDa = null,
            DateTime? dataA = null)
        {
            var esito = await InterrogaAsync(periodo, soglia, codiceSonda, dataDa, dataA);

            string sottotitolo = $"Soglia: {esito.SogliaValore:0.0} °C — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";
            var contenuto = _esportatore.GeneraDifferenze(esito.Righe, "Monitor Differenze — Misure vs Misure1", sottotitolo);
            await _audit.RegistraAsync(AreaFunzionale.MonitorTemperature, "monitortemperature.esportazione.differenze", EsitoOperazione.Successo,
                dettaglio: new { periodo, Soglia = esito.SogliaValore, Sonda = codiceSonda, dataDa, dataA, Righe = esito.Righe.Count() });

            return File(
                contenuto,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _esportatore.NomeFile("MonitorDifferenze"));
        }

        private async Task<(
            List<DifferenzaTemperatura> Righe,
            decimal SogliaValore,
            string Periodo,
            string CodiceNormalizzato,
            bool FiltroPersonalizzato,
            List<string> SondeDisponibili)> InterrogaAsync(
                string periodo, string? soglia, string? codiceSonda, DateTime? dataDa, DateTime? dataA)
        {
            decimal sogliaValore = ParseSoglia(soglia);
            if (sogliaValore < 0) sogliaValore = 0;

            bool filtroPersonalizzato = dataDa.HasValue || dataA.HasValue;

            DateTime? dataInizio = null;
            DateTime? dataFineEsclusiva = null;

            if (filtroPersonalizzato)
            {
                if (dataDa.HasValue) dataInizio = dataDa.Value.Date;
                if (dataA.HasValue) dataFineEsclusiva = dataA.Value.Date.AddDays(1);
            }
            else
            {
                switch (periodo)
                {
                    case "48h":
                        dataInizio = DateTime.Now.AddHours(-48);
                        break;
                    case "7g":
                        dataInizio = DateTime.Now.AddDays(-7);
                        break;
                    case "30g":
                        dataInizio = DateTime.Now.AddDays(-30);
                        break;
                    case "tutto":
                        dataInizio = null;
                        break;
                    default:
                        periodo = "24h";
                        dataInizio = DateTime.Now.AddHours(-24);
                        break;
                }
            }

            string codiceNormalizzato =
                string.IsNullOrWhiteSpace(codiceSonda) || string.Equals(codiceSonda, "TUTTE", StringComparison.OrdinalIgnoreCase)
                    ? "TUTTE"
                    : NormalizzaCodiceSonda(codiceSonda);

            string connectionString = _configuration.GetConnectionString("MonitorTemperature")
                ?? throw new InvalidOperationException("Connection string MonitorTemperature non trovata.");

            var risultato = new List<DifferenzaTemperatura>();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        m.IdMisura,
                        m.DataOra,
                        m.IdSonda,
                        v.CodiceSonda,
                        v.Apparecchiatura,
                        v.CodiceApparecchiatura,
                        ISNULL(a.Tipo, '') AS TipoApparecchiatura,
                        CAST(m.TemperaturaGrezza + ISNULL(m.OffsetApplicato, 0) AS DECIMAL(7,3)) AS TemperaturaOriginale,
                        CAST(m1.TemperaturaGrezza + ISNULL(m1.OffsetApplicato, 0) AS DECIMAL(7,3)) AS TemperaturaControllo,
                        CAST(
                            (m1.TemperaturaGrezza + ISNULL(m1.OffsetApplicato, 0))
                            - (m.TemperaturaGrezza + ISNULL(m.OffsetApplicato, 0))
                            AS DECIMAL(7,3)
                        ) AS Differenza,
                        v.LimiteMin,
                        v.LimiteMax

                    FROM temperature.Misure m

                    INNER JOIN temperature.Misure1 m1
                        ON m1.IdSonda = m.IdSonda
                        AND m1.DataOra = m.DataOra
                        AND m1.TemperaturaGrezza = m.TemperaturaGrezza

                    INNER JOIN temperature.vw_StoricoTemperature v
                        ON v.IdMisura = m.IdMisura

                    INNER JOIN temperature.Sonde s
                        ON s.IdSonda = m.IdSonda

                    INNER JOIN temperature.Apparecchiature a
                        ON a.IdApparecchiatura = v.IdApparecchiatura

                    WHERE
                        (@DataInizio IS NULL OR m.DataOra >= @DataInizio)
                        AND (@DataFineEsclusiva IS NULL OR m.DataOra < @DataFineEsclusiva)
                        AND (@CodiceSonda = 'TUTTE' OR v.CodiceSonda = @CodiceSonda)
                        AND s.Attiva = 1
                        AND a.Attiva = 1

                    ORDER BY
                        m.DataOra;
                ";

                command.Parameters.Add("@DataInizio", SqlDbType.DateTime2).Value =
                    dataInizio.HasValue ? dataInizio.Value : DBNull.Value;

                command.Parameters.Add("@DataFineEsclusiva", SqlDbType.DateTime2).Value =
                    dataFineEsclusiva.HasValue ? dataFineEsclusiva.Value : DBNull.Value;

                command.Parameters.Add("@CodiceSonda", SqlDbType.NVarChar, 50).Value = codiceNormalizzato;

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string apparecchiatura = reader["Apparecchiatura"].ToString() ?? "";
                    string tipoApparecchiatura = reader["TipoApparecchiatura"].ToString() ?? "";
                    decimal temperaturaOriginale = Convert.ToDecimal(reader["TemperaturaOriginale"]);
                    decimal temperaturaControllo = Convert.ToDecimal(reader["TemperaturaControllo"]);

                    decimal? limiteMin = reader["LimiteMin"] == DBNull.Value ? null : Convert.ToDecimal(reader["LimiteMin"]);
                    decimal? limiteMax = reader["LimiteMax"] == DBNull.Value ? null : Convert.ToDecimal(reader["LimiteMax"]);

                    bool congelatore = EUnCongelatore(tipoApparecchiatura, apparecchiatura);

                    decimal fuoriOriginale = CalcolaFuoriLimite(temperaturaOriginale, limiteMin, limiteMax, congelatore);
                    decimal fuoriControllo = CalcolaFuoriLimite(temperaturaControllo, limiteMin, limiteMax, congelatore);

                    risultato.Add(new DifferenzaTemperatura
                    {
                        IdMisura = Convert.ToInt64(reader["IdMisura"]),
                        DataOra = Convert.ToDateTime(reader["DataOra"]),
                        IdSonda = Convert.ToInt32(reader["IdSonda"]),
                        CodiceSonda = reader["CodiceSonda"].ToString() ?? "",
                        Apparecchiatura = apparecchiatura,
                        CodiceApparecchiatura = reader["CodiceApparecchiatura"].ToString() ?? "",
                        TipoApparecchiatura = tipoApparecchiatura,
                        Congelatore = congelatore,
                        TemperaturaOriginale = temperaturaOriginale,
                        TemperaturaControllo = temperaturaControllo,
                        Differenza = Convert.ToDecimal(reader["Differenza"]),
                        LimiteMin = limiteMin,
                        LimiteMax = limiteMax,
                        FuoriLimiteOriginale = fuoriOriginale,
                        FuoriLimiteControllo = fuoriControllo
                    });
                }
            }

            var sondeDaMostrare = risultato
                .GroupBy(x => x.IdSonda)
                .Where(g => g.Max(x => Math.Abs(x.Differenza)) >= sogliaValore)
                .Select(g => g.Key)
                .ToHashSet();

            risultato = risultato
                .Where(x => sondeDaMostrare.Contains(x.IdSonda))
                .OrderBy(x => x.DataOra)
                .ToList();

            var sondeDisponibili = new List<string>();

            await using (var commandSonde = connection.CreateCommand())
            {
                commandSonde.CommandText = @"
                    SELECT DISTINCT CodiceSonda
                    FROM temperature.Sonde
                    WHERE Attiva = 1 AND CodiceSonda LIKE 'T%'
                    ORDER BY CodiceSonda;
                ";

                await using var readerSonde = await commandSonde.ExecuteReaderAsync();

                while (await readerSonde.ReadAsync())
                {
                    string codice = readerSonde["CodiceSonda"].ToString() ?? "";

                    if (!string.IsNullOrWhiteSpace(codice))
                    {
                        sondeDisponibili.Add(NormalizzaCodiceSonda(codice));
                    }
                }
            }

            sondeDisponibili = sondeDisponibili
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x =>
                {
                    string numero = x.Replace("T", "").Trim();
                    return int.TryParse(numero, out int n) ? n : 9999;
                })
                .ToList();

            return (risultato, sogliaValore, periodo, codiceNormalizzato, filtroPersonalizzato, sondeDisponibili);
        }

        private static decimal ParseSoglia(string? valore)
        {
            if (string.IsNullOrWhiteSpace(valore)) return 1.0m;

            string normalizzato = valore.Trim().Replace(",", ".");

            if (decimal.TryParse(normalizzato, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out decimal risultato))
            {
                return risultato;
            }

            return 1.0m;
        }

        private static decimal CalcolaFuoriLimite(decimal temperatura, decimal? limiteMin, decimal? limiteMax, bool congelatore)
        {
            if (limiteMax.HasValue && temperatura > limiteMax.Value)
            {
                return Math.Round(temperatura - limiteMax.Value, 3);
            }

            if (!congelatore && limiteMin.HasValue && temperatura < limiteMin.Value)
            {
                return Math.Round(limiteMin.Value - temperatura, 3);
            }

            return 0m;
        }

        private static bool EUnCongelatore(string tipo, string descrizione)
        {
            return (tipo ?? "").Contains("congel", StringComparison.OrdinalIgnoreCase)
                || (descrizione ?? "").Contains("congel", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizzaCodiceSonda(string codice)
        {
            if (string.IsNullOrWhiteSpace(codice)) return "";

            codice = codice.Trim().ToUpperInvariant();

            if (codice.StartsWith("T"))
            {
                string parteNumerica = codice.Substring(1);
                if (int.TryParse(parteNumerica, out int numero)) return $"T{numero}";
            }

            return codice;
        }
    }
}
