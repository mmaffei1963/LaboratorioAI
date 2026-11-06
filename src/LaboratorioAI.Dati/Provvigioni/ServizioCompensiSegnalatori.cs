using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Provvigioni;

/// <summary>
/// Compensi ai segnalatori: collega segnalazioni e condizioni ai dati AZW importati e al motore di calcolo.
/// Indipendente dai commerciali. L'ambito «commessa» usa le voci di fattura del servizio della commessa ArcLab
/// (tipo di incarico → voce, <c>cons.VociIncarico</c>); per le commesse di analisi (tipo 3) l'area analisi.
/// </summary>
public sealed class ServizioCompensiSegnalatori
{
    private readonly ContestoLaboratorioAI _db;
    private readonly MotoreCompensiSegnalatori _motore = new();

    public ServizioCompensiSegnalatori(ContestoLaboratorioAI db) => _db = db;

    /// <summary>Calcolo «a caldo» di tutte le segnalazioni attive. Non scrive nulla.</summary>
    public async Task<CalcoloSegnalatori> CalcolaAsync(CancellationToken ct = default)
    {
        var segnalazioni = await _db.Segnalazioni.AsNoTracking().Where(s => s.Attivo).ToListAsync(ct);
        var condizioni = await _db.CondizioniSegnalatori.AsNoTracking().ToDictionaryAsync(c => c.IdCondizioneSegnalatore, ct);
        var basi = await _db.BasiCalcolo.AsNoTracking().ToDictionaryAsync(b => b.IdBaseCalcolo, ct);
        var areaAnalisi = basi.Values.FirstOrDefault(b => b.Nome == ServizioProvvigioniLab.BaseAreaAnalisi);
        var avvisi = new List<string>();

        var idCommesse = segnalazioni.Where(s => s.IdCommessa != null).Select(s => s.IdCommessa!.Value).Distinct().ToList();
        var commesse = (await _db.CommesseArcLab.AsNoTracking().Where(c => idCommesse.Contains(c.IdCommessa)).ToListAsync(ct))
            .GroupBy(c => c.IdCommessa).ToDictionary(g => g.Key, g => g.First());
        var fasce = await _db.VociIncarico.AsNoTracking().Where(f => f.Attivo).ToListAsync(ct);
        string? VoceDi(int? t) => t is int x
            ? fasce.FirstOrDefault(f => x >= f.IdTipoIncaricoDa && x <= f.IdTipoIncaricoA)?.Voce?.Trim().ToUpperInvariant()
            : null;

        // Per ogni segnalazione: quali righe (centro, voce) rientrano nell'ambito.
        var ambiti = new Dictionary<int, Func<string, string, bool>>();
        var valide = new List<SegnalazioneCompenso>();
        foreach (var s in segnalazioni)
        {
            if (!condizioni.TryGetValue(s.IdCondizioneSegnalatore, out var c)) continue;
            Func<string, string, bool>? ambito = null;
            if (c.Ambito == AmbitoCompenso.Area)
            {
                if (c.IdBaseCalcolo is int ib && basi.TryGetValue(ib, out var b)) ambito = b.Include;
                else avvisi.Add($"Condizione «{c.Nome}»: manca la base di calcolo dell'area.");
            }
            else if (s.IdCommessa is not int idc)
                avvisi.Add($"Cliente {s.CodiceCliente}: la condizione «{c.Nome}» è sulla commessa ma la commessa non è indicata.");
            else if (!commesse.TryGetValue(idc, out var cm))
                avvisi.Add($"Cliente {s.CodiceCliente}: commessa ArcLab {idc} non trovata (importa di nuovo da AZW).");
            else if (cm.IdTipoCommessa == 3)
            {
                if (areaAnalisi is not null) ambito = areaAnalisi.Include;
            }
            else if (VoceDi(cm.IdTipoIncarico) is { Length: > 0 } voce)
                ambito = (_, v) => string.Equals((v ?? "").Trim(), voce, StringComparison.OrdinalIgnoreCase);
            else
                avvisi.Add($"Cliente {s.CodiceCliente}: la commessa {idc} ha un tipo di incarico senza voce di fattura (cons.VociIncarico).");
            if (ambito is null) continue;
            ambiti[s.IdSegnalazione] = ambito;
            valide.Add(new SegnalazioneCompenso(s.IdSegnalazione, s.CodiceCliente, s.DataInizio, s.DataFine,
                c.Percentuale / 100m, c.MesiInterruzione, c.AnniDurataMassima));
        }

        var clienti = valide.Select(v => v.Cliente).Distinct().ToList();
        var fattureCliente = _db.AzwFatture.AsNoTracking().Where(f => clienti.Contains(f.CodiceCliente));
        var fatture = await fattureCliente.ToListAsync(ct);
        var righe = (await _db.AzwRigheFattura.AsNoTracking()
                .Join(fattureCliente, r => r.ChiaveFattura, f => f.Chiave, (r, f) => r).ToListAsync(ct))
            .ToLookup(r => r.ChiaveFattura, StringComparer.Ordinal);
        var incassi = await _db.AzwIncassi.AsNoTracking()
            .Join(fattureCliente, i => i.ChiaveFattura, f => f.Chiave, (i, f) => i).ToListAsync(ct);
        var righeNote = await _db.AzwRigheNoteCredito.AsNoTracking().Where(n => clienti.Contains(n.CodiceCliente)).ToListAsync(ct);

        var fs = new List<FatturaSegnalazione>();
        var ns = new List<NotaSegnalazione>();
        foreach (var v in valide)
        {
            var ambito = ambiti[v.IdSegnalazione];
            foreach (var f in fatture.Where(f => f.CodiceCliente == v.Cliente))
            {
                var netto = righe[f.Chiave].Where(r => ambito(r.Centro, r.Voce)).Sum(r => r.Netto);
                if (netto != 0m) fs.Add(new FatturaSegnalazione(v.IdSegnalazione, f.Chiave, f.DataFattura, netto, f.Lordo));
            }
            foreach (var g in righeNote.Where(n => n.CodiceCliente == v.Cliente).GroupBy(n => (n.Riferimento, n.Data)))
            {
                var netto = g.Where(r => ambito(r.Centro, r.Voce)).Sum(r => r.Netto);
                if (netto != 0m) ns.Add(new NotaSegnalazione(v.IdSegnalazione, g.Key.Riferimento, g.Key.Data, netto));
            }
        }
        var chiavi = fs.Select(f => f.ChiaveFattura).ToHashSet(StringComparer.Ordinal);
        var r = _motore.Calcola(new InputCompensiSegnalatori(valide, fs,
            incassi.Where(i => chiavi.Contains(i.ChiaveFattura)).Select(i => new IncassoFattura(i.ChiaveFattura, i.DataIncasso, i.ImportoLordo)).ToList(),
            ns));

        var segnalatoreDi = segnalazioni.Where(s => condizioni.ContainsKey(s.IdCondizioneSegnalatore))
            .ToDictionary(s => s.IdSegnalazione, s => condizioni[s.IdCondizioneSegnalatore].IdSegnalatore);
        return new CalcoloSegnalatori(r with { Avvisi = avvisi.Concat(r.Avvisi).Distinct().ToList() }, segnalatoreDi);
    }

    /// <summary>Trimestri: chiusi dai movimenti archiviati, aperti dal calcolo attuale.</summary>
    public async Task<List<RigaTrimestre>> RigheTrimestriAsync(CalcoloSegnalatori live, int? idSegnalatore, CancellationToken ct = default)
    {
        var righe = new List<RigaTrimestre>();
        var chiusi = await _db.TrimestriProvvigioni.AsNoTracking().Where(t => t.Stato == StatoTrimestre.Chiuso).ToListAsync(ct);
        var idChiusi = chiusi.Select(t => t.IdTrimestre).ToList();
        var archiviati = await _db.MovimentiSegnalatori.AsNoTracking()
            .Where(m => idChiusi.Contains(m.IdTrimestre) && (idSegnalatore == null || m.IdSegnalatore == idSegnalatore)).ToListAsync(ct);
        foreach (var t in chiusi.OrderBy(t => t.Anno).ThenBy(t => t.Numero))
        {
            var mov = archiviati.Where(m => m.IdTrimestre == t.IdTrimestre).ToList();
            righe.Add(new RigaTrimestre($"{t.Anno}-T{t.Numero}", t.Anno, t.Numero,
                mov.Where(m => m.Tipo != TipoMovimento.NotaCredito).Sum(m => m.Base),
                mov.Where(m => m.Tipo == TipoMovimento.NotaCredito).Sum(m => m.Base),
                mov.Sum(m => m.Importo), Chiuso: true, Chiudibile: false));
        }
        var etichetteChiuse = righe.Select(r => r.Etichetta).ToHashSet();
        foreach (var g in live.Movimenti(idSegnalatore).GroupBy(m => m.Trimestre).OrderBy(g => g.Key))
        {
            if (etichetteChiuse.Contains(g.Key)) continue;
            var anno = int.Parse(g.Key[..4]);
            var num = int.Parse(g.Key[^1..]);
            var fine = new DateTime(anno, num * 3, DateTime.DaysInMonth(anno, num * 3));
            righe.Add(new RigaTrimestre(g.Key, anno, num,
                g.Where(m => m.Tipo != TipoMovimento.NotaCredito).Sum(m => m.Base),
                g.Where(m => m.Tipo == TipoMovimento.NotaCredito).Sum(m => m.Base),
                g.Sum(m => m.Importo), Chiuso: false, Chiudibile: fine < DateTime.Today));
        }
        return righe.OrderBy(r => r.Anno).ThenBy(r => r.Numero).ToList();
    }

    /// <summary>Archivia i movimenti del trimestre (chiamato dalla chiusura, dentro la sua transazione).</summary>
    public async Task<int> ArchiviaTrimestreAsync(int idTrimestre, string etichetta, CancellationToken ct = default)
    {
        await _db.MovimentiSegnalatori.Where(m => m.IdTrimestre == idTrimestre).ExecuteDeleteAsync(ct);
        var calcolo = await CalcolaAsync(ct);
        var n = 0;
        foreach (var m in calcolo.Risultato.Movimenti.Where(m => m.Trimestre == etichetta))
        {
            if (!calcolo.SegnalatoreDi.TryGetValue(m.IdSegnalazione, out var idS)) continue;
            _db.MovimentiSegnalatori.Add(new MovimentoSegnalatoreArchiviato
            {
                IdTrimestre = idTrimestre, IdSegnalazione = m.IdSegnalazione, IdSegnalatore = idS,
                CodiceCliente = m.Cliente, Riferimento = m.Riferimento, Data = m.Data, Tipo = m.Tipo,
                Base = m.Base, Aliquota = m.Aliquota, Importo = m.Importo
            });
            n++;
        }
        return n;
    }
}

/// <summary>Risultato del calcolo e, per ogni segnalazione, il suo segnalatore.</summary>
public sealed record CalcoloSegnalatori(RisultatoCompensiSegnalatori Risultato, IReadOnlyDictionary<int, int> SegnalatoreDi)
{
    public IEnumerable<MovimentoSegnalatore> Movimenti(int? idSegnalatore) =>
        idSegnalatore is null ? Risultato.Movimenti
            : Risultato.Movimenti.Where(m => SegnalatoreDi.TryGetValue(m.IdSegnalazione, out var s) && s == idSegnalatore);
}
