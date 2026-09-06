using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;

namespace LaboratorioAI.IA;

/// <inheritdoc cref="IServizioCertificatiEsterni" />
public class ServizioCertificatiEsterni : IServizioCertificatiEsterni
{
    private readonly IEstrattoreCertificatoEsterno _estrattore;
    private readonly IRepositoryCertificatiEsterni _repo;

    public ServizioCertificatiEsterni(IEstrattoreCertificatoEsterno estrattore, IRepositoryCertificatiEsterni repo)
    {
        _estrattore = estrattore;
        _repo = repo;
    }

    public async Task<AnteprimaCertificatoEsterno> AnalizzaAsync(byte[] pdf, CancellationToken ct = default)
    {
        var est = await _estrattore.EstraiAsync(pdf, ct);
        var avvertenze = new List<string>();

        CampioneArcLab? camp = null;
        if (!string.IsNullOrWhiteSpace(est.Lotto) && !string.IsNullOrWhiteSpace(est.Campione))
        {
            camp = await _repo.CercaCampioneAsync(est.Lotto!, est.Campione!, null, ct);
            if (camp is null)
                avvertenze.Add($"Codice {est.Lotto}/{est.Campione} non trovato in archivio ArcLab: verificare a mano.");
        }
        else
        {
            avvertenze.Add("Nostro lotto/campione non individuato nel certificato: inserimento manuale.");
        }

        var acronimo = CostruttoreNomeCertificatoEsterno.AcronimoDa(est.Laboratorio);

        string? nomeStd = null, nomeAvv = null;
        if (camp is not null)
        {
            var dati = new DatiNomeCertificatoEsterno
            {
                Data = DateOnly.FromDateTime(camp.DataRicezione),
                Lotto = camp.Lotto,
                Campione = camp.Campione,
                IdInsediamento = camp.IdInsediamento.ToString(),
                Cliente = camp.RagioneSociale,
                Prodotto = camp.DenominazioneProdotto,
                Matrice = camp.Matrice,
                LabAcronimo = acronimo,
                RdPEsterno = est.RdPEsterno ?? "",
            };
            nomeStd = CostruttoreNomeCertificatoEsterno.Componi(dati with { ConAvviso = false });
            nomeAvv = CostruttoreNomeCertificatoEsterno.Componi(dati with { ConAvviso = true });
        }

        return new AnteprimaCertificatoEsterno
        {
            Estrazione = est,
            Campione = camp,
            LabAcronimo = acronimo,
            NomeStandard = nomeStd,
            NomeConAvviso = nomeAvv,
            Criticita = est.Criticita,
            Avvertenze = avvertenze,
            AvvisoSuggerito = est.NonConforme || est.Criticita.Count > 0,
        };
    }
}
