using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.MonitorTemperature;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Areas.MonitorTemperature.Controllers
{
    // Grafici sui dati originali (Misure).
    [Area("MonitorTemperature")]
    [RichiedePermesso(Permessi.MonitorTemperatureVisualizza)]
    public class GraficiController : Controller
    {
        private readonly ContestoMonitorTemperature _context;
        private readonly IEsportatoreMonitorTemperature _esportatore;
        private readonly IServizioAudit _audit;

        public GraficiController(ContestoMonitorTemperature context, IEsportatoreMonitorTemperature esportatore, IServizioAudit audit)
        {
            _context = context;
            _esportatore = esportatore;
            _audit = audit;
        }

        public async Task<IActionResult> Index(
            string periodo = "24h",
            string? codiceSonda = null,
            DateTime? dataDa = null,
            DateTime? dataA = null)
        {
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

            var query = _context.StoricoTemperature
                .AsNoTracking()
                .Where(x => x.CodiceSonda.StartsWith("T"));

            if (dataInizio.HasValue)
            {
                query = query.Where(x => x.DataOra >= dataInizio.Value);
            }

            if (dataFineEsclusiva.HasValue)
            {
                query = query.Where(x => x.DataOra < dataFineEsclusiva.Value);
            }

            if (codiceNormalizzato != "TUTTE")
            {
                query = query.Where(x => x.CodiceSonda == codiceNormalizzato);
            }

            var dati = await query.OrderBy(x => x.DataOra).ToListAsync();

            var sondeDisponibili = await _context.StoricoTemperature
                .AsNoTracking()
                .Where(x => x.CodiceSonda.StartsWith("T"))
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
            ViewBag.CodiceSonda = codiceNormalizzato;
            ViewBag.DataDa = dataDa;
            ViewBag.DataA = dataA;
            ViewBag.FiltroPersonalizzato = filtroPersonalizzato;
            ViewBag.SondeDisponibili = sondeDisponibili;

            return View(dati);
        }

        /// <summary>Esporta in Excel la tabella attualmente a video (stessi filtri di Index).</summary>
        public async Task<IActionResult> EsportaExcel(
            string periodo = "24h",
            string? codiceSonda = null,
            DateTime? dataDa = null,
            DateTime? dataA = null)
        {
            var dati = await InterrogaAsync(periodo, codiceSonda, dataDa, dataA);

            string codiceNormalizzato = string.IsNullOrWhiteSpace(codiceSonda)
                || string.Equals(codiceSonda, "TUTTE", StringComparison.OrdinalIgnoreCase)
                ? "TUTTE" : NormalizzaCodiceSonda(codiceSonda);

            string sottotitolo = $"Sonda: {codiceNormalizzato} — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";
            var contenuto = _esportatore.Genera(dati, "Monitor Grafici — dati originali (Misure)", sottotitolo);
            await _audit.RegistraAsync(AreaFunzionale.MonitorTemperature, "monitortemperature.esportazione.grafici", EsitoOperazione.Successo,
                dettaglio: new { periodo, Sonda = codiceNormalizzato, dataDa, dataA, Righe = dati.Count });

            return File(
                contenuto,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _esportatore.NomeFile("MonitorGrafici"));
        }

        private async Task<List<LaboratorioAI.Dati.MonitorTemperature.Models.StoricoTemperatura>> InterrogaAsync(
            string periodo, string? codiceSonda, DateTime? dataDa, DateTime? dataA)
        {
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
                dataInizio = periodo switch
                {
                    "48h" => DateTime.Now.AddHours(-48),
                    "7g" => DateTime.Now.AddDays(-7),
                    "30g" => DateTime.Now.AddDays(-30),
                    "tutto" => null,
                    _ => DateTime.Now.AddHours(-24)
                };
            }

            string codiceNormalizzato = string.IsNullOrWhiteSpace(codiceSonda)
                || string.Equals(codiceSonda, "TUTTE", StringComparison.OrdinalIgnoreCase)
                ? "TUTTE" : NormalizzaCodiceSonda(codiceSonda);

            var query = _context.StoricoTemperature
                .AsNoTracking()
                .Where(x => x.CodiceSonda.StartsWith("T"));

            if (dataInizio.HasValue) query = query.Where(x => x.DataOra >= dataInizio.Value);
            if (dataFineEsclusiva.HasValue) query = query.Where(x => x.DataOra < dataFineEsclusiva.Value);
            if (codiceNormalizzato != "TUTTE") query = query.Where(x => x.CodiceSonda == codiceNormalizzato);

            return await query.OrderBy(x => x.DataOra).ToListAsync();
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
