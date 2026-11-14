using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.MonitorTemperature;
using LaboratorioAI.Dati.MonitorTemperature.Models;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Areas.MonitorTemperature.Controllers
{
    /// <summary>
    /// Monitor Controllo: valori di controllo (dati corretti, Misure1) con
    /// grafici a mosaico colorati per sonda, come "Monitor Grafici".
    /// Misure e Misure1 originali NON vengono mai modificate da questa vista.
    /// </summary>
    [Area("MonitorTemperature")]
    [RichiedePermesso(Permessi.MonitorTemperatureVisualizza)]
    public class MonitorControlloController : Controller
    {
        private readonly ContestoMonitorTemperature _context;
        private readonly IEsportatoreMonitorTemperature _esportatore;
        private readonly IServizioAudit _audit;

        public MonitorControlloController(
            ContestoMonitorTemperature context,
            IEsportatoreMonitorTemperature esportatore,
            IServizioAudit audit)
        {
            _context = context;
            _esportatore = esportatore;
            _audit = audit;
        }

        // 14/11/2026: riscritta per i timeout in produzione (oltre 30 s con ~100.000 misure). Prima univa Misure1 con
        // Misure (su IdSonda + DataOra, senza indice su Misure) e poi con la vista vw_StoricoTemperature, che rilegge
        // tutta Misure. Sonda, apparecchiatura e limiti si prendono direttamente da Sonde / AssegnazioneSonde /
        // Apparecchiature (stessi join della vista). Filtro sulle date semplice (sempre valorizzato) + RECOMPILE.
        private const string SqlBase = @"
            SELECT
                m1.IdMisura1 AS IdMisura,
                m1.DataOra,
                m1.IdSonda,
                s.CodiceSonda,
                s.Descrizione AS DescrizioneSonda,
                a.IdApparecchiatura,
                a.CodiceApparecchiatura,
                a.Descrizione AS Apparecchiatura,
                m1.TemperaturaGrezza,
                m1.OffsetApplicato AS OffsetTemperatura,
                CAST(m1.TemperaturaGrezza + ISNULL(m1.OffsetApplicato, 0) AS DECIMAL(7,3)) AS TemperaturaCorretta,
                a.LimiteMin,
                a.LimiteMax,
                CAST(m1.StatoAllarme AS INT) AS AllarmeCalcolato

            FROM temperature.Misure1 m1

            INNER JOIN temperature.Sonde s
                ON s.IdSonda = m1.IdSonda

            INNER JOIN temperature.AssegnazioneSonde ass
                ON ass.IdSonda = s.IdSonda

            INNER JOIN temperature.Apparecchiature a
                ON a.IdApparecchiatura = ass.IdApparecchiatura

            WHERE
                m1.DataOra >= @DataInizio
                AND m1.DataOra < @DataFineEsclusiva
                AND (@CodiceSonda = 'TUTTE' OR s.CodiceSonda = @CodiceSonda)
                AND s.Attiva = 1
                AND a.Attiva = 1
                AND s.CodiceSonda LIKE 'T%'

            ORDER BY m1.DataOra
            OPTION (RECOMPILE);
        ";

        public async Task<IActionResult> Index(
            string periodo = "24h",
            string? codiceSonda = null,
            DateTime? dataDa = null,
            DateTime? dataA = null,
            string? sondaSelezionata = null)
        {
            var (dataInizio, dataFineEsclusiva, filtroPersonalizzato) =
                CalcolaIntervallo(periodo, dataDa, dataA);

            string codiceNormalizzato = NormalizzaOTutte(codiceSonda);

            var dati = await InterrogaAsync(dataInizio, dataFineEsclusiva, codiceNormalizzato);

            // 14/11/2026: elenco sonde dalla tabella Sonde (prima dalla vista su tutto lo storico delle misure).
            var sondeDisponibili = await _context.Database
                .SqlQueryRaw<string>("SELECT DISTINCT CodiceSonda AS Value FROM temperature.Sonde WHERE Attiva = 1 AND CodiceSonda LIKE 'T%'")
                .ToListAsync();

            sondeDisponibili = OrdinaCodiciSonda(sondeDisponibili);

            // Click su una card: mostra sotto la tabella storico di dettaglio
            // per quella sola sonda (stesso periodo), via postback lato server.
            List<StoricoTemperatura>? storicoSelezionato = null;
            string? sondaSelezionataNormalizzata = null;

            if (!string.IsNullOrWhiteSpace(sondaSelezionata)
                && !string.Equals(sondaSelezionata, "TUTTE", StringComparison.OrdinalIgnoreCase))
            {
                sondaSelezionataNormalizzata = NormalizzaCodiceSonda(sondaSelezionata);

                storicoSelezionato = (await InterrogaAsync(
                        dataInizio, dataFineEsclusiva, sondaSelezionataNormalizzata))
                    .OrderByDescending(x => x.DataOra)
                    .ToList();
            }

            ViewBag.Periodo = periodo;
            ViewBag.CodiceSonda = codiceNormalizzato;
            ViewBag.DataDa = dataDa;
            ViewBag.DataA = dataA;
            ViewBag.FiltroPersonalizzato = filtroPersonalizzato;
            ViewBag.SondeDisponibili = sondeDisponibili;
            ViewBag.SondaSelezionata = sondaSelezionataNormalizzata;
            ViewBag.StoricoSelezionato = storicoSelezionato;

            return View(dati);
        }

        /// <summary>Esporta in Excel la tabella attualmente a video (stessi filtri di Index).</summary>
        public async Task<IActionResult> EsportaExcel(
            string periodo = "24h",
            string? codiceSonda = null,
            DateTime? dataDa = null,
            DateTime? dataA = null)
        {
            var (dataInizio, dataFineEsclusiva, _) = CalcolaIntervallo(periodo, dataDa, dataA);
            string codiceNormalizzato = NormalizzaOTutte(codiceSonda);

            var dati = await InterrogaAsync(dataInizio, dataFineEsclusiva, codiceNormalizzato);

            string sottotitolo = $"Sonda: {codiceNormalizzato} — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";
            var contenuto = _esportatore.Genera(dati, "Monitor Controllo — dati corretti (Misure1)", sottotitolo);
            await _audit.RegistraAsync(AreaFunzionale.MonitorTemperature, "monitortemperature.esportazione.controllo", EsitoOperazione.Successo,
                dettaglio: new { periodo, Sonda = codiceNormalizzato, dataDa, dataA, Righe = dati.Count });

            return File(
                contenuto,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _esportatore.NomeFile("MonitorControllo"));
        }

        private async Task<List<StoricoTemperatura>> InterrogaAsync(
            DateTime? dataInizio, DateTime? dataFineEsclusiva, string codiceSonda)
        {
            return await _context.StoricoTemperature
                .FromSqlRaw(
                    SqlBase,
                    // Date sempre valorizzate: «tutto» = dal 1900, nessuna fine = fino al 9999 (filtro semplice, usa gli indici).
                    new SqlParameter("@DataInizio", System.Data.SqlDbType.DateTime) { Value = dataInizio ?? new DateTime(1900, 1, 1) },
                    new SqlParameter("@DataFineEsclusiva", System.Data.SqlDbType.DateTime) { Value = dataFineEsclusiva ?? new DateTime(9999, 12, 31) },
                    new SqlParameter("@CodiceSonda", codiceSonda))
                .AsNoTracking()
                .ToListAsync();
        }

        private static (DateTime? DataInizio, DateTime? DataFineEsclusiva, bool FiltroPersonalizzato) CalcolaIntervallo(
            string periodo, DateTime? dataDa, DateTime? dataA)
        {
            bool filtroPersonalizzato = dataDa.HasValue || dataA.HasValue;

            if (filtroPersonalizzato)
            {
                DateTime? inizio = dataDa.HasValue ? dataDa.Value.Date : null;
                DateTime? fineEsclusiva = dataA.HasValue ? dataA.Value.Date.AddDays(1) : null;
                return (inizio, fineEsclusiva, true);
            }

            DateTime? dataInizio = periodo switch
            {
                "48h" => DateTime.Now.AddHours(-48),
                "7g" => DateTime.Now.AddDays(-7),
                "30g" => DateTime.Now.AddDays(-30),
                "tutto" => null,
                _ => DateTime.Now.AddHours(-24)
            };

            return (dataInizio, null, false);
        }

        private static string NormalizzaOTutte(string? codiceSonda) =>
            string.IsNullOrWhiteSpace(codiceSonda)
            || string.Equals(codiceSonda, "TUTTE", StringComparison.OrdinalIgnoreCase)
                ? "TUTTE"
                : NormalizzaCodiceSonda(codiceSonda);

        private static List<string> OrdinaCodiciSonda(List<string> codici) =>
            codici
                .OrderBy(x =>
                {
                    string numero = x.Replace("T", "").Trim();
                    return int.TryParse(numero, out int n) ? n : 9999;
                })
                .ToList();

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
