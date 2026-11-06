using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.MonitorTemperature;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Areas.MonitorTemperature.Controllers
{
    // Sincronizzazione dell'anagrafica sonde da ArcLab (LabAnalisiDati), sola lettura.
    // NOTA: a differenza del progetto originale, qui NON viene effettuato alcun accesso
    // diretto al database SQLite di Econorma (C:\Econorma\Temp\database.db): quella parte
    // resta di competenza esclusiva del programma di acquisizione EconormaAcquisizione,
    // che continua a lavorare separatamente (vedi HANDOFF_MonitorTemperature.md).
    [Area("MonitorTemperature")]
    [RichiedePermesso(Permessi.MonitorTemperatureVisualizza)]
    public class SondeController : Controller
    {
        private readonly ContestoMonitorTemperature _context;
        private readonly IRepositoryArcLabSonde _arcLabRepository;
        private readonly IServizioAudit _audit;
        private readonly RepositoryMonitorSonde _monitor;

        public SondeController(
            ContestoMonitorTemperature context,
            IRepositoryArcLabSonde arcLabRepository,
            IServizioAudit audit,
            RepositoryMonitorSonde monitor)
        {
            _monitor = monitor;
            _context = context;
            _arcLabRepository = arcLabRepository;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            var sonde = await _arcLabRepository.GetSondeAttiveAsync();
            // Mappa in LabAI (codice Econorma e seriale usato da EconormaAcquisizione), per confrontarla con ArcLab.
            ViewBag.Mappa = (await _context.MappaSonde.AsNoTracking().ToListAsync())
                .GroupBy(x => x.IdApparecchioSonda)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IdMappa).First());
            // 03/11/2026: il seriale da confrontare è quello attivo per EconormaAcquisizione (StoricoSerialiSonde),
            // non quello scritto in MappaSonde alla prima sincronizzazione (mai aggiornato, non usato da nessuno).
            ViewBag.Seriali = await _monitor.SerialiAttiviAsync();
            // Limiti usati dal monitoraggio (allarmi e Monitor controllo) per apparecchiatura, da confrontare con ArcLab.
            ViewBag.Limiti = ConfrontoLimitiSonde.Confronta(sonde, await _monitor.ApparecchiatureAsync())
                .ToDictionary(x => x.CodiceApparecchiatura, StringComparer.OrdinalIgnoreCase);
            return View(sonde.ToList());
        }

        /// <summary>Anteprima: limiti del monitoraggio diversi da ArcLab (nessuna modifica).</summary>
        [RichiedePermesso(Permessi.MonitorTemperatureLimiti)]
        public async Task<IActionResult> AllineaLimiti()
        {
            var sonde = await _arcLabRepository.GetSondeAttiveAsync();
            return View(ConfrontoLimitiSonde.Confronta(sonde, await _monitor.ApparecchiatureAsync()));
        }

        /// <summary>
        /// Copia i limiti di ArcLab (sola lettura) nelle apparecchiature scelte del monitoraggio. Il confronto è
        /// rifatto qui: si aggiornano solo quelle ancora diverse. Gli allarmi già registrati non cambiano.
        /// </summary>
        [HttpPost]
        [RichiedePermesso(Permessi.MonitorTemperatureLimiti)]
        public async Task<IActionResult> AllineaLimiti(List<string>? codici)
        {
            var scelti = (codici ?? new()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sonde = await _arcLabRepository.GetSondeAttiveAsync();
            var confronto = ConfrontoLimitiSonde.Confronta(sonde, await _monitor.ApparecchiatureAsync());
            var daAggiornare = confronto.Where(x => x.Esito == EsitoConfrontoLimiti.Diversi && scelti.Contains(x.CodiceApparecchiatura)).ToList();
            var n = await _monitor.AggiornaLimitiAsync(daAggiornare);
            await _audit.RegistraAsync(AreaFunzionale.MonitorTemperature, "monitortemperature.limiti.allinea", EsitoOperazione.Successo,
                dettaglio: new
                {
                    Aggiornate = n,
                    Apparecchiature = daAggiornare.Select(x => new { x.CodiceApparecchiatura, x.Sonde, Prima = $"{x.MinMonitor} / {x.MaxMonitor}", Dopo = $"{x.MinArcLab} / {x.MaxArcLab}" })
                });
            TempData["Messaggio"] = n == 0
                ? "Nessuna apparecchiatura da aggiornare."
                : $"Limiti aggiornati per {n} apparecchiature: da ora Monitor controllo e allarmi usano quelli di ArcLab.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Aggiorna la mappa sonde da ArcLab (sola lettura su ArcLab). Solo con il pulsante e conferma (POST).</summary>
        [HttpPost]
        public async Task<IActionResult> SincronizzaSonde()
        {
            var sondeArcLab = (await _arcLabRepository.GetSondeAttiveAsync()).ToList();

            int inserite = 0;
            int giaPresenti = 0;

            foreach (var sonda in sondeArcLab)
            {
                string codice = NormalizzaCodiceSonda(EstraiCodiceSonda(sonda.NomeSonda));

                var mappaEsistente = await _context.MappaSonde
                    .FirstOrDefaultAsync(x => x.IdApparecchioSonda == sonda.IdApparecchioSonda);

                if (mappaEsistente != null)
                {
                    giaPresenti++;
                    mappaEsistente.CodiceEconorma = codice;
                    continue;
                }

                _context.MappaSonde.Add(new LaboratorioAI.Dati.MonitorTemperature.Models.MappaSonda
                {
                    IdApparecchioSonda = sonda.IdApparecchioSonda,
                    SerialeSonda = sonda.SerialeSonda ?? "",
                    CodiceEconorma = codice,
                    Attiva = true,
                    DataCreazione = DateTime.Now
                });

                inserite++;
            }

            await _context.SaveChangesAsync();
            // Unica operazione del Monitor che modifica dati (la mappa sonde nel database di LabAI; ArcLab solo letto).
            await _audit.RegistraAsync(AreaFunzionale.MonitorTemperature, "monitortemperature.sonde.sincronizza", EsitoOperazione.Successo,
                dettaglio: new { LetteDaArcLab = sondeArcLab.Count, Inserite = inserite, Aggiornate = giaPresenti });

            ViewBag.Inserite = inserite;
            ViewBag.GiaPresenti = giaPresenti;

            var risultato = await _context.MappaSonde
                .OrderBy(x => x.CodiceEconorma)
                .ToListAsync();

            return View(risultato);
        }

        private static string EstraiCodiceSonda(string nomeSonda)
        {
            if (string.IsNullOrWhiteSpace(nomeSonda)) return "";

            const string testo = "Sonda ";

            int posizione = nomeSonda.LastIndexOf(testo, StringComparison.OrdinalIgnoreCase);

            if (posizione < 0) return nomeSonda.Trim();

            return nomeSonda.Substring(posizione + testo.Length).Trim();
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
