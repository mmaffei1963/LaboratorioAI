using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Offerte;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Offerte sul portale (17/11/2026): costruisce l'offerta di laboratorio di un cliente dalle sue commesse ArcLab
/// (regole A-D) e l'elenco delle anomalie di tutti i clienti. Sola lettura su ArcLab.
/// </summary>
public class ServizioOfferteClienti
{
    private readonly IRepositoryOfferteClienti _offerte;
    private readonly IRepositoryControlli _controlli;

    public ServizioOfferteClienti(IRepositoryOfferteClienti offerte, IRepositoryControlli controlli)
    {
        _offerte = offerte;
        _controlli = controlli;
    }

    /// <summary>Offerta del cliente; null se il cliente non esiste.</summary>
    public async Task<OffertaCliente?> OffertaAsync(int idCliente, CancellationToken ct = default)
    {
        var cliente = await _offerte.ClienteAsync(idCliente, ct);
        if (cliente is null) return null;

        var commesse = await _offerte.CommesseAsync(idCliente, ct);
        var esito = RegoleOfferteClienti.Applica(commesse);

        var dettagli = new List<DettaglioControlloOfferta>();
        foreach (var g in commesse.GroupBy(c => c.IdControllo).OrderBy(g => g.Key))
        {
            var metodi = await _controlli.RigheDiControlloAsync(g.Key, ct);
            var nome = g.Select(c => c.Controllo).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))?.Trim();
            dettagli.Add(new DettaglioControlloOfferta(g.Key, nome is null ? $"{g.Key}" : $"{g.Key} – {nome}", metodi));
        }

        return new OffertaCliente(cliente, esito.Righe, dettagli, DateTime.Now);
    }

    /// <summary>Anomalie B e D di tutti i clienti.</summary>
    public async Task<IReadOnlyList<AnomaliaOfferta>> AnomalieAsync(CancellationToken ct = default) =>
        RegoleOfferteClienti.Applica(await _offerte.CommesseAsync(null, ct)).Anomalie;
}
