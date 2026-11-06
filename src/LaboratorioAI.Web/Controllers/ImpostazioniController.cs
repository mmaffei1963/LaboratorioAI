using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Amministrazione → Impostazioni documenti. Punto unico per le configurazioni condivise tra i
/// moduli (revisione Condizioni Generali usata da offerte e Ordini di Accettazione, riferimento
/// modulo OA, indirizzi per le notifiche di errore, cartella di archiviazione). Pensata per essere
/// estesa con nuove voci di configurazione.
/// </summary>
[RichiedePermesso(Permessi.AmministrazioneConfigurazioni)]
public class ImpostazioniController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;

    public ImpostazioniController(ContestoLaboratorioAI ctx, IContestoUtente utente, IServizioAudit audit)
    {
        _ctx = ctx; _utente = utente; _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Impostazioni documenti";
        return View();
    }

    /// <summary>Definizione delle voci di configurazione modificabili da questa maschera.</summary>
    private static readonly (string Chiave, string Etichetta, string Categoria, string Descrizione, string Default)[] Voci =
    {
        ("Preventivi.RevisioneCondizioni",
            "Revisione Condizioni Generali di Fornitura (condivisa offerte + Ordini di Accettazione)",
            "Documenti", "Revisione corrente delle Condizioni Generali di Fornitura", "Mod. Com. 05 Rev. 19"),
        ("PreventiviConsulenza.RevisioneCondizioni",
            "Revisione Condizioni Generali di consulenza e formazione (contratti serie C)",
            "Documenti", "Revisione richiamata nei contratti di consulenza", "Mod. Com. 05-Cons Rev. 0"),
        ("OrdineAccettazione.ModuloRevisione",
            "Riferimento modulo Ordine di Accettazione",
            "Documenti", "Codice/rev. del modulo di Sistema Qualità dell'Ordine di Accettazione", "Mod. Acc. 01 Rev. 0 del 16/09/2026"),
        ("OrdineAccettazione.EmailErrori",
            "Indirizzi per le mail di errore (Ordini di Accettazione e Certificati esterni) — separati da ;",
            "Documenti", "Destinatari del riepilogo inviato quando un Ordine di Accettazione o un certificato esterno non arriva nella cartella Palomar",
            "mmaffei@mare-a.com; dbertozzi@mare-a.com; refertazione@mare-a.com"),
        ("OrdineAccettazione.CartellaOutput",
            "Cartella di archiviazione dei PDF Ordine di Accettazione (vuota = predefinita)",
            "Documenti", "Percorso base di archiviazione dei PDF degli Ordini (anche quelli da firmare dei privati)", ""),
        ("OrdineAccettazione.TestoVerificaPrivati",
            "Ordine da firmare (privati) — riquadro «Verifica e accettazione»",
            "Documenti", "Testo per i clienti privati senza Portale (al posto del rimando al Portale).", LaboratorioAI.Documenti.TestiCondizioniAccettazione.VerificaPrivati),
        ("OrdineAccettazione.TestoCondizioniPrivati",
            "Ordine da firmare (privati) — Condizioni Generali di Fornitura",
            "Documenti", "Testo delle Condizioni per i privati (preparato dalla Qualità). [revisione] viene sostituito con la revisione corrente.", LaboratorioAI.Documenti.TestiCondizioniAccettazione.CondizioniPrivati),
        ("OrdineAccettazione.TestoFirmaPrivati",
            "Ordine da firmare (privati) — dichiarazione sopra le firme",
            "Documenti", "Dichiarazione che il cliente sottoscrive alla consegna del campione (Ordine + Condizioni).", LaboratorioAI.Documenti.TestiCondizioniAccettazione.FirmaPrivati),
        ("CertificatiEsterni.CartellaArrivo",
            "Cartella di arrivo dei Certificati Esterni da rinominare (originali)",
            "Documenti", @"Cartella dove arrivano i PDF originali dei laboratori esterni, proposta in «Da cartella di rete». Es. \\192.168.23.1\marea\ArcLab\LaboratorioAI\CertificatiEsterni\Arrivo. Se vuota vale l'impostazione CertificatiEsterni:CartellaArrivo del file di configurazione.",
            ""),
        ("CertificatiEsterni.CartellaPalomar",
            "Cartella di prelievo Palomar per i Certificati Esterni SOLO ESTERNI",
            "Documenti", @"Ogni certificato esterno rinominato con sole prove esterne viene copiato qui e verificato; Palomar compila ArcLab (fatturato interscambio). Es. D:\marea\ArcLab\LaboratorioAI\CertificatiEsterni\Palomar\ESTERNI. Se vuota vale l'impostazione CertificatiEsterni:CartellaPalomar del file di configurazione.",
            @"\\SRVMAREA\marea\ArcLab\dati\Caricamento_Certificati_Esterni"),
        ("CertificatiEsterni.CartellaPalomarMisti",
            "Cartella di prelievo Palomar per i Certificati Esterni MISTI (campione con anche prove nostre)",
            "Documenti", @"Certificati esterni di campioni che hanno anche prove fatte da noi: Palomar li porta sul portale senza compilare ArcLab, i valori si inseriscono a mano nel certificato MARE.A. Es. D:\marea\ArcLab\LaboratorioAI\CertificatiEsterni\Palomar\INTERNI_ESTERNI. Se vuota i misti non si possono salvare.",
            ""),
        ("CertificatiEsterni.CartellaProcessatiEsterni",
            "Cartella degli originali già lavorati — Certificati Esterni SOLO ESTERNI",
            "Documenti", @"Dopo il salvataggio da cartella di rete l'originale viene spostato qui (es. D:\marea\ArcLab\LaboratorioAI\CertificatiEsterni\Processati\ESTERNI). Se vuota: sottocartella ""processati"" della cartella di arrivo.",
            ""),
        ("CertificatiEsterni.CartellaProcessatiMisti",
            "Cartella degli originali già lavorati — Certificati Esterni MISTI",
            "Documenti", @"Come sopra, per i certificati misti (es. D:\marea\ArcLab\LaboratorioAI\CertificatiEsterni\Processati\INTERNI_ESTERNI). Se vuota: sottocartella ""processati"" della cartella di arrivo.",
            ""),
        ("Rapportini.CartellaOutput",
            "Cartella di archiviazione dei PDF dei rapportini di consulenza (vuota = predefinita)",
            "Documenti", "Percorso base di archiviazione dei rapportini; se vuota App_Data\\Rapportini", ""),
        ("Rapportini.CartellaPalomar",
            "Cartella di prelievo Palomar per i rapportini di consulenza",
            "Documenti", @"Ogni rapportino inviato viene copiato qui e verificato; se vuota l'invio non riesce. In produzione: D:\MAREA\LaboratorioAI\Archivio\Rapportini\Palomar",
            ""),
        ("Rapportini.TestoFonteInformazioni",
            "Rapportini — Fonte delle informazioni",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.FonteInformazioni),
        ("Rapportini.TestoPianoAnalitico",
            "Rapportini — Piano di autocontrollo analitico",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.PianoAnalitico),
        ("Rapportini.TestoModalitaConsegna",
            "Rapportini — Modalità di consegna",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.ModalitaConsegna),
        ("Rapportini.TestoResponsabilita",
            "Rapportini — Esito e responsabilità: informazioni del Cliente",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.Responsabilita),
        ("Rapportini.TestoNonCertificazione",
            "Rapportini — Esito e responsabilità: non è una certificazione",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.NonCertificazione),
        ("Rapportini.TestoDecisioniCliente",
            "Rapportini — Esito e responsabilità: decisioni del Cliente",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.DecisioniCliente),
        ("Rapportini.TestoAnalisiLaboratorio",
            "Rapportini — Esito e responsabilità: analisi del Laboratorio",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.AnalisiLaboratorio),
        ("Rapportini.TestoOsservazioni",
            "Rapportini — Osservazioni (prestazioni da fatturare)",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.Osservazioni),
        ("Rapportini.TestoRendicontazione",
            "Rapportini — Osservazioni (convenzione o prepagato)",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.Rendicontazione),
        ("Rapportini.TestoCondizioni",
            "Rapportini — Richiamo alle Condizioni (MOD. CF 01)",
            "Rapportini", "Testo fisso del Rapporto di intervento di consulenza (modello MOD. CF 05)", LaboratorioAI.Documenti.TestiRapportino.Predefiniti.Condizioni),
        ("Deleghe.LegaleRappresentanteMarea",
            "Deleghe operative — legale rappresentante di MARE.A (firma)",
            "Deleghe", "Nome riportato nel riquadro di firma per MARE.A", "Daniele Vaccarini"),
        ("Deleghe.ModelloWord",
            "Deleghe operative — modello Word (vuoto = modello predefinito del programma)",
            "Deleghe", @"Percorso completo del modello .docx con i segnaposto {{…}}; es. D:\MAREA\LaboratorioAI\Modelli\Delega_operativa_modello.docx", ""),
        ("Deleghe.CartellaArchivio",
            "Deleghe operative — cartella di archivio (Word, PDF e copie firmate; vuota = predefinita)",
            "Deleghe", @"In produzione: D:\MAREA\LaboratorioAI\Archivio\Deleghe", ""),
    };

    [HttpGet("api/impostazioni/documenti")]
    public async Task<IActionResult> Leggi(CancellationToken ct)
    {
        var presenti = await _ctx.Configurazioni.AsNoTracking()
            .Where(c => Voci.Select(v => v.Chiave).Contains(c.Chiave))
            .ToDictionaryAsync(c => c.Chiave, c => c.Valore, ct);

        var voci = Voci.Select(v => new
        {
            v.Chiave,
            v.Etichetta,
            Valore = presenti.TryGetValue(v.Chiave, out var val) && !string.IsNullOrWhiteSpace(val) ? val : v.Default
        });
        return Json(voci);
    }

    [HttpPost("api/impostazioni/documenti")]
    public async Task<IActionResult> Salva([FromBody] Dictionary<string, string?> valori, CancellationToken ct)
    {
        var ora = DateTime.UtcNow;
        foreach (var v in Voci)
        {
            if (valori is null || !valori.TryGetValue(v.Chiave, out var nuovo)) continue;
            var val = (nuovo ?? "").Trim();

            var cfg = await _ctx.Configurazioni.FirstOrDefaultAsync(x => x.Chiave == v.Chiave, ct);
            if (cfg is null)
                _ctx.Configurazioni.Add(new Configurazione
                {
                    Chiave = v.Chiave, Valore = val, Descrizione = v.Descrizione, Categoria = v.Categoria,
                    DataModifica = ora, IdUtenteModifica = _utente.IdUtente
                });
            else { cfg.Valore = val; cfg.DataModifica = ora; cfg.IdUtenteModifica = _utente.IdUtente; }
        }
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "impostazioni.documenti.salvate",
            EsitoOperazione.Successo, dettaglio: new { Chiavi = valori?.Keys }, ct: ct);
        return Json(new { ok = true });
    }
}
