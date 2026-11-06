using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.IA;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// "Prelievo vocale": pagina mobile per raccogliere in campo i dati del prelievo (lotto → campioni)
/// dettandoli a voce, con controllo dei campi obbligatori e riassunto esportabile da digitare poi
/// in ArcLab. La cattura è tutta lato client (offline). La strutturazione del dettato libero con l'AI
/// (fase 2) richiede rete e passa da qui.
/// </summary>
[RichiedePermesso(Permessi.AccettazioneEsegui)]
public class PrelievoController : Controller
{
    private readonly IClientAnthropic _ia;
    private readonly IRepositoryAnagrafiche _anagrafiche;

    public PrelievoController(IClientAnthropic ia, IRepositoryAnagrafiche anagrafiche)
    {
        _ia = ia;
        _anagrafiche = anagrafiche;
    }

    /// <summary>
    /// Elenchi per le tendine (13/10/2026), letti da ArcLab in sola lettura: clienti con campioni negli
    /// ultimi 3 anni e i loro insediamenti, controlli attivi, matrici, imballaggi e,
    /// per ogni insediamento, i punti e i luoghi di prelievo già usati.
    /// La pagina li tiene sul dispositivo, così le tendine funzionano anche offline; resta sempre
    /// possibile scrivere a mano un valore che non è in elenco.
    /// </summary>
    [HttpGet("api/prelievo/anagrafiche")]
    public async Task<IActionResult> Anagrafiche(CancellationToken ct)
    {
        var clienti = await _anagrafiche.ClientiConCampioniAsync(DateTime.Today.AddYears(-3), ct);
        var idClienti = clienti.Select(c => c.IdCliente).ToHashSet();
        var insediamenti = (await _anagrafiche.InsediamentiAsync(null, ct)).Where(i => idClienti.Contains(i.IdCliente));
        var controlli = await _anagrafiche.ControlliAsync(ct);
        var matrici = await _anagrafiche.MatriciAsync(ct);
        var imballaggi = await _anagrafiche.ImballaggiAsync(ct);
        // Punti di prelievo già usati negli ultimi 2 anni, i 25 più frequenti per insediamento.
        var punti = (await _anagrafiche.PuntiPrelievoUsatiAsync(DateTime.Today.AddYears(-2), 25, ct))
            .GroupBy(p => p.IdInsediamento)
            .ToDictionary(g => g.Key.ToString(), g => g.Select(p => p.Punto).ToList());
        // Luoghi di prelievo: stessa logica, quelli già usati per l'insediamento.
        var luoghi = (await _anagrafiche.LuoghiPrelievoUsatiAsync(DateTime.Today.AddYears(-2), 25, ct))
            .GroupBy(p => p.IdInsediamento)
            .ToDictionary(g => g.Key.ToString(), g => g.Select(p => p.Punto).ToList());
        static string T(string? s) => (s ?? "").Trim();
        return Json(new
        {
            aggiornato = DateTime.UtcNow,
            clienti = clienti.Where(c => T(c.RagioneSociale).Length > 0).Select(c => new { id = c.IdCliente, nome = T(c.RagioneSociale) }),
            insediamenti = insediamenti.Select(i => new { id = i.Id, nome = T(i.Nome), idCliente = i.IdCliente }),
            controlli = controlli.Select(c => new { id = c.Id, nome = T(c.Nome) }),
            matrici = matrici.Select(m => T(m.Nome)).Where(n => n.Length > 0).Distinct(),
            imballaggi = imballaggi.Select(m => T(m.Nome)).Where(n => n.Length > 0).Distinct(),
            luoghi,
            punti
        });
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Prelievo vocale";
        return View();
    }

    public record RichiestaStruttura(string? Testo);

    /// <summary>
    /// Struttura un dettato libero nei campi del prelievo (lotti → campioni) usando l'AI, e restituisce
    /// anche le domande sui dati mancanti/ambigui. Ritorna direttamente il JSON prodotto dal modello.
    /// </summary>
    [HttpPost("api/prelievo/struttura")]
    public async Task<IActionResult> Struttura([FromBody] RichiestaStruttura req, CancellationToken ct)
    {
        var testo = (req?.Testo ?? "").Trim();
        if (testo.Length == 0)
            return BadRequest(new { messaggio = "Detta o scrivi qualcosa da strutturare." });

        var prompt = Prompt(testo);
        RispostaModello risposta;
        try
        {
            risposta = await _ia.InterrogaAsync(prompt, ct);
        }
        catch (InvalidOperationException ex)   // chiave non configurata
        {
            return BadRequest(new { messaggio = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { messaggio = "Servizio AI non raggiungibile: " + ex.Message });
        }

        var json = EstraiJson(risposta.Testo);
        if (json is null)
            return StatusCode(502, new { messaggio = "Risposta AI non interpretabile.", grezzo = risposta.Testo });

        return Content(json, "application/json");
    }

    // Isola l'oggetto JSON dalla risposta (togliendo eventuali code fence o testo attorno).
    private static string? EstraiJson(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        int i = testo.IndexOf('{');
        int j = testo.LastIndexOf('}');
        if (i < 0 || j <= i) return null;
        return testo.Substring(i, j - i + 1);
    }

    private static string Prompt(string dettato) => $$"""
        Sei un assistente che struttura i dati di un PRELIEVO di campioni per un laboratorio di analisi,
        a partire da un dettato vocale libero in italiano. Devi restituire ESCLUSIVAMENTE un oggetto JSON
        valido (nessun testo prima o dopo, nessun markdown).

        Struttura di un prelievo: uno o più LOTTI; ogni lotto identifica cliente, insediamento/località e
        un controllo (pacchetto di analisi). Ogni lotto contiene uno o più CAMPIONI, tutti con lo stesso
        controllo.

        Schema JSON da produrre:
        {
          "lotti": [
            {
              "cliente": "",        // ragione sociale o nome del cliente
              "localita": "",       // insediamento/località del cliente
              "controllo": "",      // id numerico del controllo se detto, altrimenti descrizione
              "note": "",
              "campioni": [
                {
                  "denominazione": "",   // descrizione completa del campione + numeri di lotto (campo critico)
                  "matrice": "",
                  "imballaggio": "",
                  "temperatura": "",     // SOLO valore numerico in °C (es. "-18", "4", "20"); vuoto se non detto
                  "dataOra": "",         // formato "AAAA-MM-GGTHH:MM" se presente, altrimenti vuoto
                  "punto": "",           // punto di prelievo
                  "puntoSconosciuto": false,
                  "shelfLife": false,
                  "ritardo": "",         // giorni di ritardo analisi, solo se shelf life
                  "luogo": "",           // luogo di prelievo se diverso dall'insediamento (nome + indirizzo)
                  "noteCampione": ""
                }
              ]
            }
          ],
          "domande": []   // elenco di domande in italiano sui dati OBBLIGATORI mancanti o ambigui
        }

        Regole:
        - Campi obbligatori per lotto: cliente, localita, controllo. Per campione: denominazione, matrice, temperatura.
        - Per ogni obbligatorio mancante o ambiguo aggiungi una domanda chiara in "domande"
          (es. "Del campione 2 manca la matrice", "Qual è la temperatura di prelievo del campione 1?").
        - Non inventare valori: lascia vuoto ciò che non è stato detto.
        - temperatura deve contenere solo un numero (senza "°C"); se è stato detto "ambiente" lascia vuoto e aggiungi una domanda.
        - Riconosci più campioni e più lotti se il dettato li distingue ("primo campione", "secondo lotto", ecc.).

        Dettato da strutturare:
        {{dettato}}
        """;
}
