using LaboratorioAI.Core.Offerte;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Portale.Autenticazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Portale.Controllers;

/// <summary>
/// Offerte (17/11/2026): l'offerta di laboratorio del cliente costruita dalle sue commesse ArcLab attive, a video e in PDF.
/// Il cliente vede solo la propria; l'operatore sceglie il cliente.
/// </summary>
[ApiController]
[Route("api/offerte")]
[RichiedeSessione]
public class OfferteController : ControllerBase
{
    private readonly ServizioOfferteClienti _servizio;
    private readonly IContestoSessione _sessione;
    private readonly IWebHostEnvironment _ambiente;

    public OfferteController(ServizioOfferteClienti servizio, IContestoSessione sessione, IWebHostEnvironment ambiente)
    {
        _servizio = servizio;
        _sessione = sessione;
        _ambiente = ambiente;
    }

    /// <summary>Per le sessioni cliente sempre il cliente della sessione; l'operatore deve indicarne uno.</summary>
    private int? ClienteDaUsare(int? richiesto) => _sessione.Operatore ? richiesto : _sessione.IdCliente;

    [HttpGet]
    public async Task<IActionResult> Offerta(int? idCliente, CancellationToken ct)
    {
        if (ClienteDaUsare(idCliente) is not int id)
            return BadRequest(new { messaggio = "Scegli un cliente per vedere la sua offerta." });
        var o = await _servizio.OffertaAsync(id, ct);
        if (o is null) return NotFound(new { messaggio = "Cliente non trovato." });

        return Ok(new
        {
            Cliente = o.Cliente.RagioneSociale,
            GenerataIl = o.GenerataIl.ToString("dd/MM/yyyy"),
            Testi = new
            {
                TestiOffertaCliente.Oggetto,
                TestiOffertaCliente.IndirizzoCondizioni,
                TestiOffertaCliente.Presentazione,
                TestiOffertaCliente.TitoloAllegato,
                TestiOffertaCliente.TitoloDettaglio,
                NotaSubappalto = GeneratoreOffertaCliente.NotaSubappalto(o.Dettagli.SelectMany(d => d.Metodi).Where(m => m.IdLabEsterno is not null)
                    .Select(m => m.IdLabEsterno).Distinct().Count()),
                Condizioni = TestiOffertaCliente.Condizioni.Select(c => new { c.Titolo, c.Paragrafi })
            },
            Righe = o.Righe.Select(r => new
            {
                r.IdControllo,
                r.Incarico,
                r.Prezzo,
                DataIncarico = r.DataIncarico?.ToString("dd/MM/yyyy"),
                UltimaModifica = r.UltimaModifica?.ToString("dd/MM/yyyy"),
                r.EtichettaModifica,
                r.Insediamenti
            }),
            Dettagli = Dettagli(o)
        });
    }

    /// <summary>Metodi per controllo, con simboli § dei laboratori esterni numerati su tutta l'offerta e le loro frasi.</summary>
    private static object Dettagli(LaboratorioAI.Core.Modelli.OffertaCliente o)
    {
        var numero = new Dictionary<int, int>();
        var frasi = new List<string>();
        foreach (var m in o.Dettagli.SelectMany(d => d.Metodi))
        {
            if (m.IdLabEsterno is not int id || numero.ContainsKey(id)) continue;
            numero[id] = numero.Count + 1;
            frasi.Add(GeneratoreOffertaCliente.FraseSubappalto(m.LabEsternoFrase, m.LabEsterno));
        }
        string Simbolo(int n) => numero.Count > 1 ? "§" + n : "§";
        return new
        {
            Controlli = o.Dettagli.Select(d => new
            {
                d.IdControllo,
                d.Titolo,
                Metodi = d.Metodi.Select(m => new
                {
                    Denominazione = m.DettagliMetodo ?? m.Descrizione ?? m.DenominazioneRiga ?? "—",
                    Metodo = m.RifBiblio ?? "—",
                    Accr = m.IdLabEsterno is int id ? Simbolo(numero[id]) : (m.Accreditato ? "Sì" : "—")
                })
            }),
            Frasi = frasi.Select((f, i) => $"{Simbolo(i + 1)} {f}")
        };
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(int? idCliente, CancellationToken ct)
    {
        if (ClienteDaUsare(idCliente) is not int id)
            return BadRequest(new { messaggio = "Scegli un cliente per scaricare la sua offerta." });
        var o = await _servizio.OffertaAsync(id, ct);
        if (o is null) return NotFound(new { messaggio = "Cliente non trovato." });
        var pdf = GeneratoreOffertaCliente.GeneraPdf(o, Path.Combine(_ambiente.WebRootPath, "images", "preventivo"));
        return File(pdf, "application/pdf", GeneratoreOffertaCliente.NomeFile(o));
    }
}
