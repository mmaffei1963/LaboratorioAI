using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.MonitorTemperature;
using LaboratorioAI.Dati.MonitorTemperature.Models;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Areas.MonitorTemperature.Controllers
{
    [Area("MonitorTemperature")]
    [RichiedePermesso(Permessi.MonitorTemperatureVisualizza)]
    public class MonitorController : Controller
    {
        private readonly ContestoMonitorTemperature _context;
        private readonly IEsportatoreMonitorTemperature _esportatore;
        private readonly IServizioAudit _audit;

        public MonitorController(ContestoMonitorTemperature context, IEsportatoreMonitorTemperature esportatore, IServizioAudit audit)
        {
            _context = context;
            _esportatore = esportatore;
            _audit = audit;
        }

        // Dashboard: ultima lettura per ogni sonda attiva.
        public async Task<IActionResult> Index()
        {
            var statoAttuale = await CaricaStatoAttualeAsync();

            return View(statoAttuale);
        }

        /// <summary>Esporta in Excel la dashboard (ultima lettura per sonda) attualmente a video.</summary>
        public async Task<IActionResult> EsportaExcelDashboard()
        {
            var statoAttuale = await CaricaStatoAttualeAsync();

            string sottotitolo = $"Stato attuale — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";
            var contenuto = _esportatore.Genera(statoAttuale, "Monitor Temperature — dashboard", sottotitolo);
            await _audit.RegistraAsync(AreaFunzionale.MonitorTemperature, "monitortemperature.esportazione.dashboard", EsitoOperazione.Successo,
                dettaglio: new { Righe = statoAttuale.Count });

            return File(
                contenuto,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _esportatore.NomeFile("MonitorTemperature_Dashboard"));
        }

        /// <summary>Esporta in Excel lo storico di dettaglio di una apparecchiatura (stessi filtri della vista Storico).</summary>
        public async Task<IActionResult> EsportaExcelStorico(string codice, string periodo = "tutto")
        {
            var query = _context.StoricoTemperature.Where(x => x.CodiceApparecchiatura == codice);

            DateTime? dataInizio = periodo switch
            {
                "24h" => DateTime.Now.AddHours(-24),
                "7g" => DateTime.Now.AddDays(-7),
                "30g" => DateTime.Now.AddDays(-30),
                _ => null
            };

            if (dataInizio.HasValue)
            {
                query = query.Where(x => x.DataOra >= dataInizio.Value);
            }

            var storico = await query.OrderByDescending(x => x.DataOra).ToListAsync();

            string sottotitolo = $"Apparecchiatura: {codice} — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";
            var contenuto = _esportatore.Genera(storico, "Storico temperature", sottotitolo);
            await _audit.RegistraAsync(AreaFunzionale.MonitorTemperature, "monitortemperature.esportazione.storico", EsitoOperazione.Successo,
                "Apparecchiatura", codice, new { periodo, Righe = storico.Count });

            return File(
                contenuto,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _esportatore.NomeFile($"Storico_{codice}"));
        }

        private async Task<List<StoricoTemperatura>> CaricaStatoAttualeAsync()
        {
            var statoAttuale = await _context.StoricoTemperature
                .FromSqlRaw(@"
                    WITH Ultime AS
                    (
                        SELECT
                            v.IdMisura,
                            v.DataOra,
                            v.IdSonda,
                            v.CodiceSonda,
                            v.DescrizioneSonda,
                            v.IdApparecchiatura,
                            v.CodiceApparecchiatura,
                            v.Apparecchiatura,
                            v.TemperaturaGrezza,
                            v.OffsetTemperatura,
                            v.TemperaturaCorretta,
                            v.LimiteMin,
                            v.LimiteMax,
                            v.AllarmeCalcolato,

                            ROW_NUMBER() OVER
                            (
                                PARTITION BY v.IdSonda
                                ORDER BY
                                    v.DataOra DESC,
                                    v.IdMisura DESC
                            ) AS rn

                        FROM temperature.vw_StoricoTemperature v

                        INNER JOIN temperature.Sonde s
                            ON v.IdSonda = s.IdSonda

                        INNER JOIN temperature.Apparecchiature a
                            ON v.IdApparecchiatura =
                               a.IdApparecchiatura

                        WHERE
                            s.Attiva = 1
                            AND a.Attiva = 1
                    )

                    SELECT
                        IdMisura,
                        DataOra,
                        IdSonda,
                        CodiceSonda,
                        DescrizioneSonda,
                        IdApparecchiatura,
                        CodiceApparecchiatura,
                        Apparecchiatura,
                        TemperaturaGrezza,
                        OffsetTemperatura,
                        TemperaturaCorretta,
                        LimiteMin,
                        LimiteMax,
                        AllarmeCalcolato

                    FROM Ultime

                    WHERE rn = 1;
                ")
                .AsNoTracking()
                .ToListAsync();

            return statoAttuale
                .OrderBy(x =>
                {
                    string numero = x.CodiceSonda.Replace("T", "").Trim();
                    return int.TryParse(numero, out int n) ? n : 9999;
                })
                .ToList();
        }

        public async Task<IActionResult> Storico(
            string codice,
            string periodo = "tutto")
        {
            var query = _context.StoricoTemperature
                .Where(x => x.CodiceApparecchiatura == codice);

            DateTime? dataInizio = periodo switch
            {
                "24h" => DateTime.Now.AddHours(-24),
                "7g" => DateTime.Now.AddDays(-7),
                "30g" => DateTime.Now.AddDays(-30),
                _ => null
            };

            if (dataInizio.HasValue)
            {
                query = query.Where(x => x.DataOra >= dataInizio.Value);
            }

            var storico = await query
                .OrderByDescending(x => x.DataOra)
                .ToListAsync();

            ViewBag.CodiceApparecchiatura = codice;
            ViewBag.Periodo = periodo;

            return View(storico);
        }

        public async Task<IActionResult> Monitor(
            string periodo = "24h",
            string? codiceSonda = null,
            DateTime? dataDa = null,
            DateTime? dataA = null)
        {
            var query = _context.StoricoTemperature.AsQueryable();

            bool filtroPersonalizzato = dataDa.HasValue || dataA.HasValue;

            if (filtroPersonalizzato)
            {
                if (dataDa.HasValue)
                {
                    DateTime inizio = dataDa.Value.Date;
                    query = query.Where(x => x.DataOra >= inizio);
                }

                if (dataA.HasValue)
                {
                    DateTime fineEsclusiva = dataA.Value.Date.AddDays(1);
                    query = query.Where(x => x.DataOra < fineEsclusiva);
                }
            }
            else
            {
                DateTime? dataInizio = periodo switch
                {
                    "24h" => DateTime.Now.AddHours(-24),
                    "48h" => DateTime.Now.AddHours(-48),
                    "7g" => DateTime.Now.AddDays(-7),
                    "30g" => DateTime.Now.AddDays(-30),
                    _ => null
                };

                if (dataInizio.HasValue)
                {
                    query = query.Where(x => x.DataOra >= dataInizio.Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(codiceSonda)
                && !string.Equals(codiceSonda, "TUTTE", StringComparison.OrdinalIgnoreCase))
            {
                string codiceNormalizzato = NormalizzaCodiceSonda(codiceSonda);
                query = query.Where(x => x.CodiceSonda == codiceNormalizzato);
            }

            var storico = await query
                .OrderBy(x => x.DataOra)
                .ToListAsync();

            var sondeDisponibili = await _context.StoricoTemperature
                .Select(x => x.CodiceSonda)
                .Distinct()
                .ToListAsync();

            sondeDisponibili = sondeDisponibili
                .OrderBy(x =>
                {
                    string numero = x.Replace("T", "").Trim();
                    return int.TryParse(numero, out int n) ? n : 9999;
                })
                .ToList();

            ViewBag.Periodo = periodo;
            ViewBag.CodiceSonda = string.IsNullOrWhiteSpace(codiceSonda)
                ? "TUTTE"
                : NormalizzaCodiceSonda(codiceSonda);
            ViewBag.DataDa = dataDa;
            ViewBag.DataA = dataA;
            ViewBag.FiltroPersonalizzato = filtroPersonalizzato;
            ViewBag.SondeDisponibili = sondeDisponibili;

            return View(storico);
        }

        /// <summary>Esporta in Excel il Monitor generale attualmente a video (stessi filtri di Monitor).</summary>
        public async Task<IActionResult> EsportaExcelMonitor(
            string periodo = "24h",
            string? codiceSonda = null,
            DateTime? dataDa = null,
            DateTime? dataA = null)
        {
            var query = _context.StoricoTemperature.AsQueryable();

            bool filtroPersonalizzato = dataDa.HasValue || dataA.HasValue;

            if (filtroPersonalizzato)
            {
                if (dataDa.HasValue)
                {
                    DateTime inizio = dataDa.Value.Date;
                    query = query.Where(x => x.DataOra >= inizio);
                }

                if (dataA.HasValue)
                {
                    DateTime fineEsclusiva = dataA.Value.Date.AddDays(1);
                    query = query.Where(x => x.DataOra < fineEsclusiva);
                }
            }
            else
            {
                DateTime? dataInizio = periodo switch
                {
                    "24h" => DateTime.Now.AddHours(-24),
                    "48h" => DateTime.Now.AddHours(-48),
                    "7g" => DateTime.Now.AddDays(-7),
                    "30g" => DateTime.Now.AddDays(-30),
                    _ => null
                };

                if (dataInizio.HasValue)
                {
                    query = query.Where(x => x.DataOra >= dataInizio.Value);
                }
            }

            string codiceNormalizzato = "TUTTE";

            if (!string.IsNullOrWhiteSpace(codiceSonda)
                && !string.Equals(codiceSonda, "TUTTE", StringComparison.OrdinalIgnoreCase))
            {
                codiceNormalizzato = NormalizzaCodiceSonda(codiceSonda);
                query = query.Where(x => x.CodiceSonda == codiceNormalizzato);
            }

            var storico = await query.OrderBy(x => x.DataOra).ToListAsync();

            string sottotitolo = $"Sonda: {codiceNormalizzato} — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";
            var contenuto = _esportatore.Genera(storico, "Monitor generale — dati originali (Misure)", sottotitolo);
            await _audit.RegistraAsync(AreaFunzionale.MonitorTemperature, "monitortemperature.esportazione.monitor", EsitoOperazione.Successo,
                dettaglio: new { periodo, Sonda = codiceNormalizzato, dataDa, dataA, Righe = storico.Count });

            return File(
                contenuto,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _esportatore.NomeFile("MonitorGenerale"));
        }

        private static string NormalizzaCodiceSonda(string codice)
        {
            if (string.IsNullOrWhiteSpace(codice)) return "";

            codice = codice.Trim().ToUpperInvariant();

            if (codice.StartsWith("T"))
            {
                string parteNumerica = codice.Substring(1);

                if (int.TryParse(parteNumerica, out int numero))
                {
                    return $"T{numero}";
                }
            }

            return codice;
        }
    }
}
