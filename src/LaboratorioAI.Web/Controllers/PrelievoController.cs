using LaboratorioAI.Core.Costanti;
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

    public PrelievoController(IClientAnthropic ia) => _ia = ia;

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
