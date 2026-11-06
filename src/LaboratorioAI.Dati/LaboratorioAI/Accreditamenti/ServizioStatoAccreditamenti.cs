using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Dati.Accredia;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.Accreditamenti;

/// <summary>Cambio di stato di un laboratorio rispetto al controllo precedente (per la mail e il registro).</summary>
public sealed record VariazioneStatoAccredia(
    int IdAccreditamento, string Laboratorio, string Numero, string Sede,
    StatoAccreditamentoAccredia? Prima, StatoAccreditamentoAccredia Ora);

/// <summary>Prova sospesa comparsa (Nuova = true) o non più sospesa rispetto al controllo precedente.</summary>
public sealed record VariazioneProvaSospesa(
    string Laboratorio, string Numero, string Sede, bool Nuova, string? Matrice, string? Prova, string? Norma, string? Tecnica);

/// <summary>Esito dell'aggiornamento: cambi di stato del laboratorio e prove sospese nuove o terminate.</summary>
public sealed record EsitoStatoAccredia(
    IReadOnlyList<VariazioneStatoAccredia> Variazioni, IReadOnlyList<VariazioneProvaSospesa> VariazioniProve)
{
    public bool Vuoto => Variazioni.Count == 0 && VariazioniProve.Count == 0;
}

/// <summary>
/// Salva lo stato ACCREDIA (accreditato / sospeso / non trovato) dei laboratori monitorati, ricevuto dall'Agente
/// ACCREDIA insieme alle revisioni (08/10/2026). Solo database di LabAI. Le voci senza stato (agente di versione
/// precedente o lettura non riuscita) lasciano lo stato com'era.
/// </summary>
public sealed class ServizioStatoAccreditamenti
{
    private readonly ContestoLaboratorioAI _db;

    public ServizioStatoAccreditamenti(ContestoLaboratorioAI db) => _db = db;

    public async Task<EsitoStatoAccredia> AggiornaAsync(
        IEnumerable<VoceImportazioneAccredia> voci, CancellationToken ct = default)
    {
        var elenco = voci.ToList();
        var proveRicevute = elenco
            .Where(v => v.ProveSospese is not null)
            .GroupBy(v => (v.NumeroAccreditamento.Trim(), v.Sede.Trim().ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.First().ProveSospese!);
        var perChiave = elenco
            .Where(v => Enum.TryParse<StatoAccreditamentoAccredia>(v.StatoAccreditamento, ignoreCase: true, out _))
            .GroupBy(v => (v.NumeroAccreditamento.Trim(), v.Sede.Trim().ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => Enum.Parse<StatoAccreditamentoAccredia>(g.First().StatoAccreditamento!, ignoreCase: true));
        if (perChiave.Count == 0 && proveRicevute.Count == 0)
            return new EsitoStatoAccredia(Array.Empty<VariazioneStatoAccredia>(), Array.Empty<VariazioneProvaSospesa>());

        var laboratori = await _db.Set<AccreditamentoLabEsterno>().Where(a => a.Attivo).ToListAsync(ct);
        var variazioni = new List<VariazioneStatoAccredia>();
        var adesso = DateTime.UtcNow;
        var variazioniProve = new List<VariazioneProvaSospesa>();
        var idLab = laboratori.Select(l => l.IdAccreditamento).ToList();
        var proveSalvate = await _db.Set<ProvaSospesaLabEsterno>()
            .Where(p => idLab.Contains(p.IdAccreditamento)).ToListAsync(ct);
        foreach (var lab in laboratori)
        {
            var chiaveLab = (lab.NumeroAccreditamento.Trim(), lab.Sede.Trim().ToUpperInvariant());
            if (proveRicevute.TryGetValue(chiaveLab, out var prove))
                AggiornaProve(lab, prove, proveSalvate.Where(p => p.IdAccreditamento == lab.IdAccreditamento).ToList(),
                    adesso, variazioniProve);
            if (!perChiave.TryGetValue(chiaveLab, out var stato)) continue;
            if (lab.StatoAccredia != stato)
                variazioni.Add(new VariazioneStatoAccredia(lab.IdAccreditamento, lab.NomeLaboratorio, lab.NumeroAccreditamento,
                    lab.Sede, lab.StatoAccredia, stato));
            lab.StatoAccredia = stato;
            lab.DataStatoAccredia = adesso;
        }
        await _db.SaveChangesAsync(ct);
        return new EsitoStatoAccredia(variazioni, variazioniProve);
    }

    /// <summary>
    /// Allinea le prove sospese salvate di un laboratorio a quelle appena lette: nuove → inserite; ancora sospese →
    /// data di ultima lettura; non più presenti → data di fine sospensione (nessuna cancellazione); ricomparse dopo
    /// una fine → nuova sospensione sulla stessa riga (si azzera la fine e si aggiorna la rilevazione).
    /// </summary>
    private void AggiornaProve(AccreditamentoLabEsterno lab, IReadOnlyList<ProvaSospesaLetta> lette,
        List<ProvaSospesaLabEsterno> salvate, DateTime adesso, List<VariazioneProvaSospesa> variazioni)
    {
        var perChiave = salvate.ToDictionary(p => p.ChiaveProva);
        var viste = new HashSet<string>();
        foreach (var l in lette)
        {
            var chiave = ChiaveProva(l);
            if (!viste.Add(chiave)) continue;
            if (perChiave.TryGetValue(chiave, out var p))
            {
                if (p.DataFineSospensione is not null)
                {
                    p.DataFineSospensione = null;
                    p.DataRilevazione = adesso;
                    variazioni.Add(Variazione(lab, p, nuova: true));
                }
                p.DataUltimaLettura = adesso;
                continue;
            }
            var nuova = new ProvaSospesaLabEsterno
            {
                IdAccreditamento = lab.IdAccreditamento, ChiaveProva = chiave,
                Matrice = Tronca(l.Matrice, 1000), Prova = Tronca(l.Prova, 1000), Norma = Tronca(l.Norma, 1000), Tecnica = Tronca(l.Tecnica, 300),
                DataRilevazione = adesso, DataUltimaLettura = adesso
            };
            _db.Add(nuova);
            variazioni.Add(Variazione(lab, nuova, nuova: true));
        }
        foreach (var p in salvate.Where(p => p.DataFineSospensione is null && !viste.Contains(p.ChiaveProva)))
        {
            p.DataFineSospensione = adesso;
            variazioni.Add(Variazione(lab, p, nuova: false));
        }
    }

    private static VariazioneProvaSospesa Variazione(AccreditamentoLabEsterno lab, ProvaSospesaLabEsterno p, bool nuova) =>
        new(lab.NomeLaboratorio, lab.NumeroAccreditamento, lab.Sede, nuova, p.Matrice, p.Prova, p.Norma, p.Tecnica);

    /// <summary>Impronta della prova: testo di matrice, prova, norma e tecnica senza spazi doppi, maiuscolo.</summary>
    public static string ChiaveProva(ProvaSospesaLetta p)
    {
        static string N(string? s) => Regex.Replace(s ?? "", @"\s+", " ").Trim().ToUpperInvariant();
        var testo = string.Join("|", N(p.Matrice), N(p.Prova), N(p.Norma), N(p.Tecnica));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(testo)));
    }

    private static string? Tronca(string? s, int n) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length <= n ? s.Trim() : s.Trim()[..n];

    public static string Etichetta(StatoAccreditamentoAccredia? s) => s switch
    {
        StatoAccreditamentoAccredia.Accreditato => "accreditato",
        StatoAccreditamentoAccredia.Sospeso => "sospensione attiva su alcune prove",
        StatoAccreditamentoAccredia.NonTrovato => "NON TROVATO nella banca dati (revocato, ritirato o rinunciato)",
        _ => "non ancora verificato"
    };
}
