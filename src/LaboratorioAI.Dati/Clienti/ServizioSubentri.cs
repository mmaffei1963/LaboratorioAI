using LaboratorioAI.Core.Andamento;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Clienti;

public sealed record RiepilogoSubentri(
    RisultatoSubentri Risultato,
    /// <summary>Ultima verifica VIES per P.IVA normalizzata.</summary>
    IReadOnlyDictionary<string, VerificaPartitaIva> Vies,
    IReadOnlySet<string> Intermediari,
    /// <summary>Decisioni valide per insediamento perso (la più recente prima).</summary>
    IReadOnlyDictionary<int, List<SubentroInsediamento>> Decisioni,
    DateTime? UltimaImportazione,
    int InsediamentiCopiati,
    AmbitoSubentri Ambito = AmbitoSubentri.Legionella);

public sealed record InsediamentoTrovato(int IdInsediamento, string? CodiceCliente, string RagioneSociale, string? Nome, string? Indirizzo, string? Citta);

/// <summary>
/// «Cambi di gestione» (subentri, 12/10/2026): prepara i dati per <see cref="MotoreSubentri"/> dalla copia di lavoro
/// degli insediamenti ArcLab (riscritta a ogni importazione AZW) e gestisce le decisioni delle persone e le verifiche
/// VIES. Scrive solo nel database di LabAI.
/// </summary>
public sealed class ServizioSubentri
{
    /// <summary>Una P.IVA già verificata non si richiede a VIES prima di questi giorni.</summary>
    public const int GiorniValiditaVies = 30;

    private readonly ContestoLaboratorioAI _db;
    private readonly ClientVies _vies;
    private readonly MotoreSubentri _motore = new();

    public ServizioSubentri(ContestoLaboratorioAI db, ClientVies vies)
    {
        _db = db;
        _vies = vies;
    }

    public async Task<RiepilogoSubentri> CalcolaAsync(DateTime oggi, FinestraConfronto? finestra = null, CancellationToken ct = default) =>
        await CalcolaAsync(oggi, finestra, AmbitoSubentri.Legionella, ct);

    /// <summary>Calcolo per un servizio (13/10/2026): Legionella, analisi di laboratorio o un'area della consulenza.</summary>
    public async Task<RiepilogoSubentri> CalcolaAsync(DateTime oggi, FinestraConfronto? finestra, AmbitoSubentri ambito, CancellationToken ct = default)
    {
        var insediamenti = (await _db.InsediamentiArcLab.AsNoTracking().ToListAsync(ct))
            .Select(i => new InsediamentoSubentri(i.IdInsediamento, i.IdClienteArcLab, i.CodiceCliente, i.RagioneSociale,
                i.PartitaIva, i.NomeInsediamento, i.Indirizzo, i.Citta)).ToList();
        var dal = oggi.Year - 3;
        var campioni = await AttivitaAsync(ambito, dal, ct);
        var decisioni = await _db.SubentriInsediamenti.AsNoTracking().Where(d => !d.Annullata)
            .OrderBy(d => d.DataDecisione).ThenBy(d => d.IdSubentroInsediamento).ToListAsync(ct);

        var risultato = _motore.Calcola(insediamenti, campioni,
            decisioni.Select(d => new DecisioneSubentro(d.IdInsediamentoPrecedente, d.IdInsediamentoSuccessivo, d.Confermato)), oggi, finestra);

        // Clienti tolti dalla lista dal commerciale (14/10/2026: acquisiti, cessati, non recuperabili): le loro sedi non si propongono.
        var tolti = (await _db.ClientiTolti.AsNoTracking().Where(t => !t.Annullata).Select(t => t.CodiceCliente).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        if (tolti.Count > 0)
            risultato = risultato with { Persi = risultato.Persi.Where(p => p.Perso.Insediamento.CodiceCliente is not string c || !tolti.Contains(c)).ToList() };

        var vies = (await _db.VerifichePartitaIva.AsNoTracking().ToListAsync(ct))
            .GroupBy(v => v.PartitaIva).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.DataVerifica).First());
        var intermediari = (await _db.AttributiClienti.AsNoTracking().Where(a => a.Intermediario).Select(a => a.CodiceCliente).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var ultima = await _db.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita)
            .OrderByDescending(i => i.DataOra).Select(i => (DateTime?)i.DataOra).FirstOrDefaultAsync(ct);

        return new RiepilogoSubentri(risultato, vies, intermediari,
            decisioni.GroupBy(d => d.IdInsediamentoPrecedente).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.DataDecisione).ToList()),
            ultima, insediamenti.Count, ambito);
    }

    /// <summary>
    /// Attività per sede e mese del servizio scelto. Laboratorio: presenza = campioni (per «analisi esclusa la Legionella»
    /// solo quelli non di Legionella), ambito = campioni del servizio. Consulenza: prestazioni dell'area (voce del tipo di
    /// incarico, cons.VociIncarico), sia come presenza sia come ambito.
    /// </summary>
    private async Task<List<AttivitaInsediamentoMese>> AttivitaAsync(AmbitoSubentri ambito, int dalAnno, CancellationToken ct)
    {
        if (!AmbitiSubentri.DiConsulenza(ambito))
        {
            var righe = await _db.CampioniInsediamentiArcLab.AsNoTracking().Where(c => c.Anno >= dalAnno).ToListAsync(ct);
            return righe.Select(c => ambito switch
                {
                    AmbitoSubentri.Analisi => new AttivitaInsediamentoMese(c.IdInsediamento, c.Anno, c.Mese, c.Campioni, c.Campioni, c.Valore, c.UltimoCampione, c.PrimoCampione),
                    AmbitoSubentri.AnalisiSenzaLegionella => new AttivitaInsediamentoMese(c.IdInsediamento, c.Anno, c.Mese,
                        c.Campioni - c.CampioniLegionella, c.Campioni - c.CampioniLegionella, c.Valore, c.UltimoCampione, c.PrimoCampione),
                    _ => new AttivitaInsediamentoMese(c.IdInsediamento, c.Anno, c.Mese, c.Campioni, c.CampioniLegionella, c.Valore, c.UltimoCampione, c.PrimoCampione)
                })
                .Where(a => a.Presenza > 0 || a.Ambito > 0).ToList();
        }

        var fasce = await _db.VociIncarico.AsNoTracking().Where(f => f.Attivo).ToListAsync(ct);
        string? Voce(int? id) => id is int t ? fasce.FirstOrDefault(f => t >= f.IdTipoIncaricoDa && t <= f.IdTipoIncaricoA)?.Voce : null;
        bool Conta(PrestazioneInsediamentoArcLab p)
        {
            var area = AmbitiSubentri.AreaDaVoce(Voce(p.IdTipoIncarico), p.IdTipoCommessa);
            return ambito == AmbitoSubentri.Consulenza ? area != AmbitoSubentri.Formazione : area == ambito;
        }
        var prestazioni = await _db.PrestazioniInsediamentiArcLab.AsNoTracking().Where(c => c.Anno >= dalAnno).ToListAsync(ct);
        return prestazioni.Where(Conta)
            .GroupBy(p => (p.IdInsediamento, p.Anno, p.Mese))
            .Select(g => new AttivitaInsediamentoMese(g.Key.IdInsediamento, g.Key.Anno, g.Key.Mese,
                g.Sum(x => x.Prestazioni), g.Sum(x => x.Prestazioni), g.Sum(x => x.Valore),
                g.Max(x => x.UltimaPrestazione), g.Min(x => x.PrimaPrestazione)))
            .ToList();
    }

    // ── Decisioni ─────────────────────────────────────────────────────

    /// <summary>
    /// Registra una decisione. Confermare un abbinamento (o «nessun subentro», successivo vuoto) annulla le conferme
    /// precedenti dello stesso insediamento. Restituisce un messaggio d'errore o null.
    /// </summary>
    public async Task<string?> DecidiAsync(int idPrecedente, int? idSuccessivo, bool conferma, bool manuale, string? note,
        int? idUtente, string? utente, CancellationToken ct = default)
    {
        var prec = await _db.InsediamentiArcLab.AsNoTracking().FirstOrDefaultAsync(i => i.IdInsediamento == idPrecedente, ct);
        if (prec is null) return "Insediamento non trovato: ripetere l'importazione o ricaricare la pagina.";
        InsediamentoArcLab? succ = null;
        if (idSuccessivo is int ids)
        {
            if (ids == idPrecedente) return "L'insediamento non può subentrare a sé stesso.";
            succ = await _db.InsediamentiArcLab.AsNoTracking().FirstOrDefaultAsync(i => i.IdInsediamento == ids, ct);
            if (succ is null) return "Insediamento del nuovo gestore non trovato.";
        }
        else if (!conferma) return "Indicare l'abbinamento da scartare.";

        var ora = DateTime.UtcNow;
        if (conferma)
        {
            foreach (var d in await _db.SubentriInsediamenti.Where(d => d.IdInsediamentoPrecedente == idPrecedente && d.Confermato && !d.Annullata).ToListAsync(ct))
            {
                d.Annullata = true; d.DataAnnullamento = ora; d.AnnullataDa = Tronca(utente, 100);
            }
        }
        _db.SubentriInsediamenti.Add(new SubentroInsediamento
        {
            IdInsediamentoPrecedente = idPrecedente,
            IdInsediamentoSuccessivo = idSuccessivo,
            CodiceClientePrecedente = prec.CodiceCliente,
            CodiceClienteSuccessivo = succ?.CodiceCliente,
            Confermato = conferma,
            Manuale = manuale,
            Note = Tronca(note, 1000),
            DataDecisione = ora,
            IdUtente = idUtente,
            NomeUtente = Tronca(utente, 100)
        });
        await _db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Annulla una decisione presa per errore (nessuna cancellazione fisica).</summary>
    public async Task<SubentroInsediamento?> AnnullaAsync(int id, string? utente, CancellationToken ct = default)
    {
        var d = await _db.SubentriInsediamenti.FirstOrDefaultAsync(x => x.IdSubentroInsediamento == id, ct);
        if (d is null || d.Annullata) return null;
        d.Annullata = true;
        d.DataAnnullamento = DateTime.UtcNow;
        d.AnnullataDa = Tronca(utente, 100);
        await _db.SaveChangesAsync(ct);
        return d;
    }

    /// <summary>Ricerca di un insediamento per abbinarlo a mano (nome, ragione sociale, codice, città o indirizzo).</summary>
    public async Task<List<InsediamentoTrovato>> CercaAsync(string testo, CancellationToken ct = default)
    {
        var t = (testo ?? "").Trim();
        if (t.Length < 2) return [];
        return await _db.InsediamentiArcLab.AsNoTracking()
            .Where(i => i.CodiceCliente == t || i.RagioneSociale.Contains(t) || (i.NomeInsediamento != null && i.NomeInsediamento.Contains(t))
                        || (i.Citta != null && i.Citta.Contains(t)) || (i.Indirizzo != null && i.Indirizzo.Contains(t)))
            .OrderBy(i => i.RagioneSociale).Take(30)
            .Select(i => new InsediamentoTrovato(i.IdInsediamento, i.CodiceCliente, i.RagioneSociale, i.NomeInsediamento, i.Indirizzo, i.Citta))
            .ToListAsync(ct);
    }

    // ── VIES ──────────────────────────────────────────────────────────

    /// <summary>Verifica una P.IVA italiana su VIES e salva l'esito. Null se la P.IVA non è verificabile (estera o fittizia).</summary>
    public async Task<VerificaPartitaIva?> VerificaPartitaIvaAsync(string? partitaIva, CancellationToken ct = default)
    {
        var p = MotoreSubentri.PartitaIvaValida(partitaIva);
        if (p is null || p.Length != 11 || !p.All(char.IsDigit)) return null;
        var esito = await _vies.VerificaAsync("IT", p, ct);
        var v = new VerificaPartitaIva
        {
            Paese = "IT", PartitaIva = p, Valida = esito.Valida,
            Nome = Tronca(esito.Nome, 300), Indirizzo = Tronca(esito.Indirizzo, 500), Errore = Tronca(esito.Errore, 300),
            DataVerifica = DateTime.UtcNow
        };
        _db.VerifichePartitaIva.Add(v);
        await _db.SaveChangesAsync(ct);
        return v;
    }

    /// <summary>
    /// Verifica in blocco le P.IVA indicate non controllate negli ultimi <see cref="GiorniValiditaVies"/> giorni,
    /// una alla volta con una breve pausa (il servizio europeo limita le richieste). Restituisce quante ne ha verificate.
    /// </summary>
    public async Task<(int Verificate, int Valide, int NonTrovate, int Errori)> VerificaInBloccoAsync(IEnumerable<string?> partiteIva,
        int massimo, CancellationToken ct = default)
    {
        var limite = DateTime.UtcNow.AddDays(-GiorniValiditaVies);
        var recenti = (await _db.VerifichePartitaIva.AsNoTracking().Where(v => v.DataVerifica >= limite && v.Valida != null)
            .Select(v => v.PartitaIva).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var daFare = partiteIva.Select(MotoreSubentri.PartitaIvaValida)
            .Where(p => p is { Length: 11 } && p.All(char.IsDigit) && !recenti.Contains(p))
            .Distinct().Take(massimo).ToList();
        int valide = 0, non = 0, err = 0;
        foreach (var p in daFare)
        {
            var v = await VerificaPartitaIvaAsync(p, ct);
            if (v?.Valida == true) valide++; else if (v?.Valida == false) non++; else err++;
            await Task.Delay(400, ct);
        }
        return (daFare.Count, valide, non, err);
    }

    private static string? Tronca(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }
}
