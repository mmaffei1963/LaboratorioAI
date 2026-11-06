using System.Text.RegularExpressions;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.Accreditamenti;

/// <summary>Metodo la cui verifica in LabAI non coincide con il flag di accreditamento inserito in ArcLab.</summary>
public sealed record DiscrepanzaMetodoEsterno(
    int IdAccreditamento, string Laboratorio, int IdLabEstMetodo, string? CodMetodo, string? RiferimentoNormativo,
    string? NomeParametro, StatoVerificaMetodo Verifica, bool FlagArcLab, DateTime? DataVerificaUtc, string? Note);

/// <summary>Laboratorio con numero ACCREDIA in ArcLab (stampato sui certificati) diverso da quello monitorato.</summary>
public sealed record NumeroAccrediaDiverso(
    int IdAccreditamento, string Laboratorio, string? NumeroArcLab, string NumeroMonitorato, string Sede);

/// <summary>
/// Laboratorio monitorato con prove sospese o non più presente nella banca dati ACCREDIA. <see cref="Prove"/>: prove
/// con sospensione in corso (vuota se non ancora lette dall'agente).
/// </summary>
public sealed record StatoAccrediaNonValido(
    int IdAccreditamento, string Laboratorio, string Numero, string Sede, StatoAccreditamentoAccredia Stato, DateTime? DataStatoUtc,
    IReadOnlyList<ProvaSospesaConMetodi> Prove);

/// <summary>Prova sospesa su ACCREDIA e metodi che affidiamo al laboratorio con la stessa norma (possibile corrispondenza).</summary>
public sealed record ProvaSospesaConMetodi(
    string? Matrice, string? Prova, string? Norma, string? Tecnica, DateTime DataRilevazioneUtc, IReadOnlyList<MetodoCoinvolto> Metodi);

/// <summary>Metodo affidato al laboratorio (ArcLab) che cita la stessa norma della prova sospesa.</summary>
public sealed record MetodoCoinvolto(int IdLabEstMetodo, string? CodMetodo, string? NomeParametro, string? RiferimentoNormativo);

/// <summary>Ultimo controllo ACCREDIA registrato (agente o pulsante manuale) e se è troppo vecchio.</summary>
public sealed record StatoControlloAccredia(DateTime? UltimoControlloUtc, string? Modo, bool Fermo, int GiorniMassimi);

/// <summary>Le cose «da sistemare» sugli accreditamenti dei laboratori in subappalto.</summary>
public sealed record AnomalieAccreditamenti(
    IReadOnlyList<DiscrepanzaMetodoEsterno> Discrepanze,
    IReadOnlyList<NumeroAccrediaDiverso> NumeriDiversi,
    StatoControlloAccredia Controllo,
    IReadOnlyList<StatoAccrediaNonValido> StatiNonValidi)
{
    /// <summary>Totale per il contatore della home: sospesi/non trovati + discrepanze + numeri diversi + 1 se il controllo è fermo.</summary>
    public int Totale => StatiNonValidi.Count + Discrepanze.Count + NumeriDiversi.Count + (Controllo.Fermo ? 1 : 0);
}

/// <summary>
/// Controlli sugli accreditamenti dei laboratori in subappalto (08/10/2026, proposte 2, 6 e 7 del documento
/// «Accreditamento dei laboratori in subappalto in Laboratorio AI»). Tutto in sola lettura: ArcLab non si
/// modifica mai da qui; le correzioni si fanno nel gestionale.
/// 1. Discrepanze: metodi classificati in LabAI sulla revisione ACCREDIA confermata (Accreditato / Non accreditato)
///    in modo diverso dal flag <c>labEstMetodo.AccrLabEsterno</c> di ArcLab, che è quello usato da certificati e ricerche.
/// 2. Numeri diversi: <c>LabEsterno.NumeroAccredia</c> di ArcLab (stampato sui certificati) diverso dal numero
///    monitorato (numerazione ACCREDIA 2025). Si confrontano solo le cifre, senza zeri iniziali.
/// 3. Controllo fermo: nessun controllo ACCREDIA (agente o pulsante «Esegui controllo ora») registrato da
///    più di <see cref="GiorniMassimiSenzaControllo"/> giorni. In sviluppo l'agente invia alla produzione, quindi
///    qui il controllo risulta di norma fermo.
/// </summary>
public sealed class ServizioAnomalieAccreditamenti
{
    public const int GiorniMassimiSenzaControllo = 3;

    private const string AzioneAgente = "accreditamento.controllo-da-agente";
    private const string AzioneManuale = "accreditamento.controllo-manuale";

    private readonly ContestoLaboratorioAI _db;
    private readonly IRepositoryMetodiLabEsterno _arcLab;

    public ServizioAnomalieAccreditamenti(ContestoLaboratorioAI db, IRepositoryMetodiLabEsterno arcLab)
    {
        _db = db;
        _arcLab = arcLab;
    }

    public async Task<AnomalieAccreditamenti> CalcolaAsync(CancellationToken ct = default)
    {
        var laboratori = await _db.Set<AccreditamentoLabEsterno>().AsNoTracking()
            .Where(a => a.Attivo && a.DataCancellazione == null)
            .OrderBy(a => a.NomeLaboratorio).ToListAsync(ct);

        // ── 1. Discrepanze verifica / flag ArcLab ──
        var discrepanze = new List<DiscrepanzaMetodoEsterno>();
        var conRevisione = laboratori.Where(l => l.UltimaRevisioneConfermata is not null).ToList();
        if (conRevisione.Count > 0)
        {
            var ids = conRevisione.Select(l => l.IdAccreditamento).ToList();
            var revisioni = await _db.Set<RevisioneAccreditamentoEsterno>().AsNoTracking()
                .Where(r => ids.Contains(r.IdAccreditamento))
                .Select(r => new { r.IdAccreditamento, r.NumeroRevisione, r.IdRevisione }).ToListAsync(ct);
            var verifiche = await _db.Set<VerificaMetodoAccreditamento>().AsNoTracking()
                .Where(v => ids.Contains(v.IdAccreditamento) && v.Stato != StatoVerificaMetodo.DaVerificare)
                .ToListAsync(ct);

            foreach (var lab in conRevisione)
            {
                // Valgono solo le classificazioni fatte sulla revisione oggi confermata (come nella finestra «Verifica metodi»).
                var idRevisione = revisioni.FirstOrDefault(r => r.IdAccreditamento == lab.IdAccreditamento
                                                             && r.NumeroRevisione == lab.UltimaRevisioneConfermata)?.IdRevisione;
                var valide = verifiche.Where(v => v.IdAccreditamento == lab.IdAccreditamento && v.IdRevisioneVerificata == idRevisione)
                    .ToDictionary(v => v.IdLabEstMetodoArcLab);
                if (idRevisione is null || valide.Count == 0) continue;

                foreach (var m in await _arcLab.MetodiAffidatiAsync(lab.IdLabEsternoArcLab, ct))
                {
                    if (!valide.TryGetValue(m.IdLabEstMetodo, out var v)) continue;
                    var accreditatoSecondoVerifica = v.Stato == StatoVerificaMetodo.Accreditato;
                    if (accreditatoSecondoVerifica == m.AccreditatoArcLab) continue;
                    discrepanze.Add(new DiscrepanzaMetodoEsterno(lab.IdAccreditamento, lab.NomeLaboratorio, m.IdLabEstMetodo,
                        m.CodMetodo, m.RiferimentoNormativo, m.NomeParametro, v.Stato, m.AccreditatoArcLab, v.DataVerifica, v.Note));
                }
            }
        }

        // ── 2. Numero ACCREDIA di ArcLab diverso da quello monitorato ──
        var numeri = new List<NumeroAccrediaDiverso>();
        if (laboratori.Count > 0)
        {
            var arcLab = (await _arcLab.LaboratoriAsync(laboratori.Select(l => l.IdLabEsternoArcLab).Distinct().ToList(), ct))
                .ToDictionary(x => x.IdLabEsterno);
            foreach (var lab in laboratori)
            {
                arcLab.TryGetValue(lab.IdLabEsternoArcLab, out var a);
                if (SoloCifre(a?.NumeroAccredia) == SoloCifre(lab.NumeroAccreditamento) && SoloCifre(a?.NumeroAccredia) != "") continue;
                numeri.Add(new NumeroAccrediaDiverso(lab.IdAccreditamento, lab.NomeLaboratorio,
                    string.IsNullOrWhiteSpace(a?.NumeroAccredia) ? null : a!.NumeroAccredia!.Trim(), lab.NumeroAccreditamento, lab.Sede));
            }
        }

        // ── 3. Ultimo controllo ACCREDIA registrato ──
        var ultimo = await _db.Audit.AsNoTracking()
            // Confronto esplicito: un array.Contains qui viene compilato (C# recente) come Contains su ReadOnlySpan,
            // che EF Core 8 non sa tradurre (errore TypeLoadException in esecuzione).
            .Where(x => x.Area == AreaFunzionale.LaboratoriEsterni && (x.Azione == AzioneAgente || x.Azione == AzioneManuale))
            .OrderByDescending(x => x.DataOra)
            .Select(x => new { x.DataOra, x.Azione }).FirstOrDefaultAsync(ct);
        var fermo = laboratori.Count > 0
            && (ultimo is null || ultimo.DataOra < DateTime.UtcNow.AddDays(-GiorniMassimiSenzaControllo));
        var controllo = new StatoControlloAccredia(ultimo?.DataOra,
            ultimo is null ? null : ultimo.Azione == AzioneAgente ? "agente" : "manuale",
            fermo, GiorniMassimiSenzaControllo);

        // ── 4. Stato nella banca dati ACCREDIA: prove sospese o laboratorio non più trovato ──
        var stati = new List<StatoAccrediaNonValido>();
        var nonValidi = laboratori
            .Where(l => l.StatoAccredia is StatoAccreditamentoAccredia.Sospeso or StatoAccreditamentoAccredia.NonTrovato)
            .ToList();
        if (nonValidi.Count > 0)
        {
            var idSospesi = nonValidi.Select(l => l.IdAccreditamento).ToList();
            var proveInCorso = await _db.Set<ProvaSospesaLabEsterno>().AsNoTracking()
                .Where(p => idSospesi.Contains(p.IdAccreditamento) && p.DataFineSospensione == null)
                .OrderBy(p => p.Prova).ToListAsync(ct);
            foreach (var l in nonValidi)
            {
                var proveLab = proveInCorso.Where(p => p.IdAccreditamento == l.IdAccreditamento).ToList();
                var metodi = proveLab.Count == 0 ? Array.Empty<MetodoLabEsterno>()
                    : await _arcLab.MetodiAffidatiAsync(l.IdLabEsternoArcLab, ct);
                var prove = proveLab.Select(p => new ProvaSospesaConMetodi(p.Matrice, p.Prova, p.Norma, p.Tecnica, p.DataRilevazione,
                        metodi.Where(m => StessaNorma(m.RiferimentoNormativo, p.Norma))
                            .Select(m => new MetodoCoinvolto(m.IdLabEstMetodo, m.CodMetodo, m.NomeParametro, m.RiferimentoNormativo))
                            .ToList()))
                    .ToList();
                stati.Add(new StatoAccrediaNonValido(l.IdAccreditamento, l.NomeLaboratorio, l.NumeroAccreditamento, l.Sede,
                    l.StatoAccredia!.Value, l.DataStatoAccredia, prove));
            }
        }

        return new AnomalieAccreditamenti(discrepanze, numeri, controllo, stati);
    }

    /// <summary>
    /// Possibile corrispondenza fra il riferimento normativo di un nostro metodo e la norma di una prova sospesa:
    /// tutti i «codici» del nostro riferimento (gruppi di cifre di almeno 2 caratteri, esclusi gli anni 1900-2099,
    /// es. 16266, 970 e 66 di «AOAC 970.66») compaiono fra quelli della norma ACCREDIA. È un suggerimento per chi
    /// verifica, non una decisione: la stessa norma può valere per prove o matrici diverse.
    /// </summary>
    public static bool StessaNorma(string? nostroRiferimento, string? normaAccredia)
    {
        var nostri = Codici(nostroRiferimento);
        if (nostri.Count == 0) return false;
        var loro = Codici(normaAccredia);
        return nostri.All(loro.Contains);
    }

    private static HashSet<string> Codici(string? testo) =>
        Regex.Matches(testo ?? "", @"\d+")
            .Select(m => m.Value.TrimStart('0'))
            .Where(c => c.Length >= 2 && !(c.Length == 4 && (c.StartsWith("19") || c.StartsWith("20"))))
            .ToHashSet();

    /// <summary>Solo le cifre, senza zeri iniziali: «00253», «253 L» e «0253» sono lo stesso numero.</summary>
    public static string SoloCifre(string? s) => Regex.Replace(s ?? "", @"\D", "").TrimStart('0');
}
