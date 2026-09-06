using System.Globalization;
using System.Security.Cryptography;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;

namespace LaboratorioAI.IA;

/// <inheritdoc cref="IServizioCertificatiEsterni" />
public class ServizioCertificatiEsterni : IServizioCertificatiEsterni
{
    private readonly IEstrattoreCertificatoEsterno _estrattore;
    private readonly IRepositoryCertificatiEsterni _repo;
    private readonly IArchivioEstrazioniCertificato _archivio;

    public ServizioCertificatiEsterni(IEstrattoreCertificatoEsterno estrattore, IRepositoryCertificatiEsterni repo,
        IArchivioEstrazioniCertificato archivio)
    {
        _estrattore = estrattore;
        _repo = repo;
        _archivio = archivio;
    }

    public async Task<AnteprimaCertificatoEsterno> AnalizzaAsync(byte[] pdf, string? nomeFileOriginale = null,
        bool forza = false, CancellationToken ct = default)
    {
        // Cache per impronta del PDF: riusa l'estrazione se già presente (salvo forza).
        var hash = Convert.ToHexString(SHA256.HashData(pdf));
        EstrazioneCertificatoEsterno? est = forza ? null : await _archivio.LeggiPerHashAsync(hash, ct);
        bool daCache = est is not null;
        if (est is null)
        {
            est = await _estrattore.EstraiAsync(pdf, ct);
            await _archivio.SalvaAsync(hash, nomeFileOriginale, est, null, ct);
        }

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
        string? dataNome = null;
        if (camp is not null)
        {
            // Data del nome = emissione del certificato: prima deterministica (regex sul PDF),
            // poi quella del modello, infine la ricezione del campione.
            var data = ParseData(EstrattoreDataEmissione.Estrai(pdf))
                       ?? ParseData(est.DataEmissione)
                       ?? DateOnly.FromDateTime(camp.DataRicezione);
            dataNome = data.ToString("yyyy-MM-dd");
            var dati = new DatiNomeCertificatoEsterno
            {
                Data = data,
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
            DaCache = daCache,
            DataNome = dataNome,
        };
    }

    private static DateOnly? ParseData(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return DateOnly.FromDateTime(dt);
        if (DateTime.TryParse(s, new CultureInfo("it-IT"), DateTimeStyles.None, out var dt2)) return DateOnly.FromDateTime(dt2);
        return null;
    }
}
