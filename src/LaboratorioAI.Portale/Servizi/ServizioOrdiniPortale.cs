using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Portale.Servizi;

/// <summary>
/// Ordini di accettazione sul portale (18/11/2026): elenco dei lotti ricevuti del cliente e PDF dell'Ordine, lo stesso
/// documento dell'anteprima di LaboratorioAI (stesso repository e stesso generatore). Destinato a sostituire la
/// pubblicazione di Palomar. I privati (MAF2) restano solo in LaboratorioAI. Sola lettura su ArcLab.
/// Revisione delle Condizioni: quella pubblicata in vigore alla data di ricezione del lotto (Condizioni e firme), se
/// registrata; altrimenti quella delle impostazioni, come in LaboratorioAI.
/// </summary>
public class ServizioOrdiniPortale
{
    // Stesse chiavi e valori predefiniti di OrdiniAccettazioneController (Web).
    private const string ChiaveRevisione = "Preventivi.RevisioneCondizioni";
    private const string RevisioneDefault = "Mod. Com. 05 Rev. 19";
    private const string ChiaveModuloOA = "OrdineAccettazione.ModuloRevisione";
    private const string ModuloDefault = "Mod. Acc. 01 Rev. 0 del 16/09/2026";

    public record Documento(byte[] Contenuto, string NomeFile);

    private readonly IRepositoryOrdineAccettazione _repo;
    private readonly IGeneratoreOrdineAccettazione _generatore;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IWebHostEnvironment _ambiente;

    public ServizioOrdiniPortale(IRepositoryOrdineAccettazione repo, IGeneratoreOrdineAccettazione generatore,
        IRepositoryAnagrafiche anagrafiche, ContestoLaboratorioAI ctx, IWebHostEnvironment ambiente)
    {
        _repo = repo; _generatore = generatore; _anagrafiche = anagrafiche; _ctx = ctx; _ambiente = ambiente;
    }

    /// <summary>Lotti ricevuti dal cliente nel periodo (al massimo 2000, i più recenti), filtrabili per insediamento e codice lotto.</summary>
    public async Task<IReadOnlyList<LottoCandidato>> LottiAsync(int idCliente, DateTime? dal, DateTime? al, int? idInsediamento,
        string? codLotto, CancellationToken ct)
    {
        var lotti = await _repo.LottiRicevutiAsync(dal?.Date, al?.Date.AddDays(1), new FiltroLotti(idCliente, idInsediamento), ct);
        var cod = codLotto?.Trim();
        return string.IsNullOrEmpty(cod) ? lotti : lotti.Where(l => l.CodLotto.Contains(cod, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// PDF dell'Ordine del lotto, solo se il lotto è del cliente indicato (null = operatore senza vincolo), non è di un
    /// privato ed è pronto. Restituisce un messaggio d'errore al posto del documento.
    /// </summary>
    public async Task<(Documento? Documento, string? Errore)> DocumentoAsync(int idLotto, int? idCliente, CancellationToken ct)
    {
        var ordine = await _repo.LeggiPerLottoAsync(idLotto, ct);
        // Lotto inesistente o di un altro cliente: stessa risposta, per non rivelare quali lotti esistono.
        if (ordine is null || (idCliente is not null && ordine.IdClienteArcLab != idCliente) || GeneratoreOrdineAccettazione.EPrivato(ordine))
            return (null, "Ordine non trovato.");
        if (!ordine.Pronto)
            return (null, $"L'Ordine del lotto {ordine.CodLotto} è in preparazione: riprova più tardi.");

        ordine.RevisioneCondizioni = await RevisioneAllaDataAsync(ordine.DataRicezione ?? DateTime.Today, ct);
        ordine.RiferimentoModulo = await ConfigAsync(ChiaveModuloOA, ModuloDefault, ct);
        var insediamenti = ordine.IdClienteArcLab is int idc
            ? await _anagrafiche.InsediamentiConEmailAsync(idc, ct)
            : Array.Empty<InsediamentoContatto>();

        var pdf = _generatore.GeneraPdf(ordine, _ambiente.WebRootPath, insediamenti);
        return (new Documento(pdf, _generatore.NomeFile(ordine, "pdf")), null);
    }

    /// <summary>Revisione pubblicata delle Condizioni in vigore alla data; se non registrata, quella delle impostazioni.</summary>
    private async Task<string> RevisioneAllaDataAsync(DateTime data, CancellationToken ct)
    {
        var codice = await _ctx.RevisioniCondizioni.AsNoTracking()
            .Where(r => r.Attivo && r.Stato == StatoRevisioneCondizioni.Pubblicata && r.DataEntrataVigore != null && r.DataEntrataVigore <= data)
            .OrderByDescending(r => r.DataEntrataVigore).ThenByDescending(r => r.NumeroRevisione)
            .Select(r => r.Codice).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(codice) ? await ConfigAsync(ChiaveRevisione, RevisioneDefault, ct) : codice;
    }

    private async Task<string> ConfigAsync(string chiave, string predefinito, CancellationToken ct)
    {
        var valore = await _ctx.Configurazioni.AsNoTracking().Where(x => x.Chiave == chiave).Select(x => x.Valore).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(valore) ? predefinito : valore;
    }
}
