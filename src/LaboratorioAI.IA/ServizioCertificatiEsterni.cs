using System.Globalization;
using System.Security.Cryptography;
using LaboratorioAI.Core.Entita;
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
    private readonly IRepositorySigleLaboratori _sigle;

    public ServizioCertificatiEsterni(IEstrattoreCertificatoEsterno estrattore, IRepositoryCertificatiEsterni repo,
        IArchivioEstrazioniCertificato archivio, IRepositorySigleLaboratori sigle)
    {
        _estrattore = estrattore;
        _repo = repo;
        _archivio = archivio;
        _sigle = sigle;
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

        // Data di emissione del certificato: prima deterministica (regex sul PDF), poi quella del modello.
        // Serve a scegliere il lotto giusto (i numeri ripartono ogni anno) e va in certificato.DataCertificato.
        var dataEmissione = ParseData(EstrattoreDataEmissione.Estrai(pdf)) ?? ParseData(est.DataEmissione);

        CampioneArcLab? camp = null;
        if (!string.IsNullOrWhiteSpace(est.Lotto) && !string.IsNullOrWhiteSpace(est.Campione))
        {
            camp = await _repo.CercaCampioneAsync(est.Lotto!, est.Campione!, dataEmissione, ct);
            if (camp is null && dataEmissione is not null)
            {
                // Data di emissione forse letta male (precedente alla ricezione): riprova senza vincolo, con avviso.
                camp = await _repo.CercaCampioneAsync(est.Lotto!, est.Campione!, null, ct);
                if (camp is not null)
                    avvertenze.Add($"Nessun lotto {est.Lotto} ricevuto entro la data di emissione letta ({dataEmissione:dd/MM/yyyy}): agganciato il più recente, verificare cliente e anno.");
            }
            if (camp is null)
                avvertenze.Add($"Codice {est.Lotto}/{est.Campione} non trovato in archivio ArcLab: verificare a mano.");
            else if (dataEmissione is null)
                avvertenze.Add("Data di emissione non individuata: agganciato il lotto più recente con quel numero, verificare cliente e anno.");
        }
        else
        {
            avvertenze.Add("Nostro lotto/campione non individuato nel certificato: inserimento manuale.");
        }

        // Sigle gestite in Amministrazione → Sigle laboratori esterni (una per laboratorio ArcLab).
        var sigle = (await _sigle.ElencoAttiveAsync(ct)).ToDictionary(x => x.IdLabEsternoArcLab);
        var labPdf = (est.Laboratorio ?? "").ToLowerInvariant();
        bool NelPdf(SiglaLaboratorioEsterno sg) => labPdf.Length > 0 && sg.ElencoParole().Any(p => labPdf.Contains(p));
        var riconosciutiDalPdf = sigle.Values.Where(NelPdf).ToList();

        // Senza campione la sigla serve solo alla sottocartella di archivio.
        var acronimo = riconosciutiDalPdf.Count == 1
            ? riconosciutiDalPdf[0].Sigla
            : CostruttoreNomeCertificatoEsterno.AcronimoDa(est.Laboratorio);

        string? nomeStd = null, nomeAvv = null;
        string? dataNome = null;
        int? idLab = null;
        if (camp is not null)
        {
            // idLabEsterno: fra i laboratori delle prove del campione in ArcLab, quello riconosciuto nel PDF
            // dalle parole chiave; se il campione ne ha uno solo, quello. Se in ArcLab il campione non ha
            // prove esterne, si prova col solo riconoscimento dal PDF. Altrimenti segnaposto IDLAB.
            var labs = await _repo.LabEsterniDelCampioneAsync(camp.IdCampione, ct);
            var perParole = labs.Where(l => sigle.TryGetValue(l.IdLabEsterno, out var sg) && NelPdf(sg)).ToList();
            if (perParole.Count == 1) idLab = perParole[0].IdLabEsterno;
            else if (labs.Count == 1) idLab = labs[0].IdLabEsterno;
            else if (labs.Count == 0 && riconosciutiDalPdf.Count == 1) idLab = riconosciutiDalPdf[0].IdLabEsternoArcLab;

            if (idLab is null)
                avvertenze.Add(labs.Count == 0
                    ? "Nessun laboratorio esterno associato alle prove del campione in ArcLab: sostituire IDLAB con l'id laboratorio."
                    : "Laboratorio esterno non determinabile (controllare le parole chiave in Amministrazione → Sigle laboratori esterni), sostituire IDLAB con uno di: "
                      + string.Join(", ", labs.Select(l => $"{l.IdLabEsterno} = {l.Nome}")) + ".");
            else if (sigle.TryGetValue(idLab.Value, out var siglaLab))
                acronimo = siglaLab.Sigla;
            else
            {
                var nomeLab = labs.FirstOrDefault(l => l.IdLabEsterno == idLab)?.Nome ?? est.Laboratorio;
                acronimo = CostruttoreNomeCertificatoEsterno.AcronimoDa(nomeLab);
                avvertenze.Add($"Manca la sigla per il laboratorio {nomeLab} (id {idLab}): usata \"{acronimo}\" provvisoria. Aggiungila in Amministrazione → Sigle laboratori esterni.");
            }
            if (camp.IdMatrice is null)
                avvertenze.Add("Matrice assente sul campione in ArcLab: sostituire IDMATRICE con l'id matrice.");

            // Data del nome = ricezione del campione (Lotto.DataRic): anno coerente con il lotto,
            // così l'aggancio al portale (anno del nome = anno di ricezione) funziona anche a cavallo d'anno.
            var data = DateOnly.FromDateTime(camp.DataRicezione);
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
                IdMatrice = camp.IdMatrice?.ToString(),
                LabAcronimo = acronimo,
                IdLabEsterno = idLab?.ToString(),
                RdPEsterno = est.RdPEsterno ?? "",
            };
            nomeStd = CostruttoreNomeCertificatoEsterno.Componi(dati with { ConAvviso = false });
            nomeAvv = CostruttoreNomeCertificatoEsterno.Componi(dati with { ConAvviso = true });
        }

        // Solo esterni o misto (12/10/2026): un campione con prove fatte da noi non genera un certificato ArcLab
        // dal certificato esterno (va nella cartella Palomar INTERNI_ESTERNI, valori inseriti a mano).
        string? tipo = null;
        IReadOnlyList<string> parInterni = new List<string>(), parEsterni = new List<string>();
        if (camp is not null)
        {
            var prove = await _repo.ProveDeiCampioniAsync(new[] { camp.IdCampione }, ct);
            var t = ClassificatoreCertificatoEsterno.Classifica(prove);
            tipo = t?.ToString();
            parInterni = ClassificatoreCertificatoEsterno.Parametri(prove.Where(p => !p.Esterna));
            parEsterni = ClassificatoreCertificatoEsterno.Parametri(prove.Where(p => p.Esterna));
            if (t == TipoCertificatoEsterno.Misto)
                avvertenze.Add("Certificato MISTO: il campione ha anche prove fatte da noi (" + string.Join(", ", parInterni.Take(6))
                    + (parInterni.Count > 6 ? "…" : "") + "). Va nella cartella INTERNI_ESTERNI, senza import in ArcLab: i valori si inseriscono a mano nel nostro certificato.");
            else if (t is null)
                avvertenze.Add("Il campione non ha prove in ArcLab: non posso dire se è solo esterno o misto, sceglilo a mano.");
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
            DataEmissione = dataEmissione?.ToString("yyyy-MM-dd"),
            IdLabEsterno = idLab,
            TipoProposto = tipo,
            ParametriInterni = parInterni,
            ParametriEsterni = parEsterni,
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
