using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LaboratorioAI.Dati.Limiti;

/// <summary>
/// Fase 1b passo 2 (18/10/2026): colori e segnalazioni dei campioni in «Da Validare».
/// Legge da ArcLab (sola lettura) le prove dei campioni da validare e le confronta con le schede limite VALIDATE.
/// Risultato in cache per 2 minuti. Tempi massimi ricezione → inizio prova nella tabella limiti.TempiMassimiProva
/// (scheda «Tempi massimi» di Limiti normativi); al primo uso si crea con i valori approvati da Mirko il 18/10/2026.
/// </summary>
public class ServizioSegnalazioniLimiti
{
    public static readonly IReadOnlyDictionary<FamigliaNormativa, int> TempiPredefiniti = new Dictionary<FamigliaNormativa, int>
    {
        [FamigliaNormativa.AcqueConsumoUmano] = 24,
        [FamigliaNormativa.Legionella] = 48,
        [FamigliaNormativa.Molluschi] = 24,
        [FamigliaNormativa.Alimenti] = 48,
        [FamigliaNormativa.Piscine] = 24,
    };

    private const string ChiaveCache = "segnalazioni-limiti";
    private static readonly SemaphoreSlim Blocco = new(1, 1);

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryLimitiArcLab _arclab;
    private readonly IMemoryCache _cache;

    public ServizioSegnalazioniLimiti(ContestoLaboratorioAI ctx, IRepositoryLimitiArcLab arclab, IMemoryCache cache)
    { _ctx = ctx; _arclab = arclab; _cache = cache; }

    // ── Tempi massimi ricezione → inizio prova ──

    /// <summary>Tutte le righe (anche disattivate). Se la tabella è vuota la crea con i valori predefiniti.</summary>
    public async Task<List<TempoMassimoProva>> TempiAsync(CancellationToken ct)
    {
        if (!await _ctx.TempiMassimiProva.AnyAsync(ct))
        {
            var ora = DateTime.UtcNow;
            foreach (var (f, ore) in TempiPredefiniti)
                _ctx.TempiMassimiProva.Add(new TempoMassimoProva
                {
                    Famiglia = f, Ore = ore, Attivo = true, Nota = "Valore iniziale", DataCreazione = ora, DataModifica = ora, ModificatoDa = "Sistema (valori iniziali)"
                });
            await _ctx.SaveChangesAsync(ct);
        }
        return await _ctx.TempiMassimiProva.AsNoTracking().OrderBy(t => t.Famiglia).ThenBy(t => t.IdParametroArcLab != null).ThenBy(t => t.NomeParametro).ToListAsync(ct);
    }

    /// <summary>Crea (id null) o modifica una riga. Restituisce un messaggio d'errore o null.</summary>
    public async Task<string?> SalvaTempoAsync(int? id, FamigliaNormativa famiglia, int? idParametro, string? nomeParametro, int ore, bool attivo,
        string? nota, OperatoreLimiti op, CancellationToken ct)
    {
        if (ore is < 1 or > 720) return "Le ore devono essere tra 1 e 720 (30 giorni).";
        if (!Enum.IsDefined(famiglia)) return "Famiglia non valida.";
        if (attivo && await _ctx.TempiMassimiProva.AnyAsync(t => t.Attivo && t.Famiglia == famiglia && t.IdParametroArcLab == idParametro
                && t.IdTempoMassimoProva != (id ?? 0), ct))
            return idParametro is null
                ? "Esiste già una riga attiva per questa famiglia: modifica quella."
                : "Esiste già una riga attiva per questo parametro nella famiglia: modifica quella.";
        TempoMassimoProva t;
        if (id is int i)
        {
            t = await _ctx.TempiMassimiProva.FirstOrDefaultAsync(x => x.IdTempoMassimoProva == i, ct) ?? throw new InvalidOperationException("Riga non trovata.");
        }
        else
        {
            t = new TempoMassimoProva { DataCreazione = DateTime.UtcNow };
            _ctx.TempiMassimiProva.Add(t);
        }
        t.Famiglia = famiglia;
        t.IdParametroArcLab = idParametro;
        t.NomeParametro = idParametro is null ? null : Taglia(nomeParametro, 300);
        t.Ore = ore;
        t.Attivo = attivo;
        t.Nota = Taglia(nota, 500);
        t.ModificatoDa = op.Nome;
        t.IdUtenteModifica = op.IdUtente;
        t.DataModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        _cache.Remove(ChiaveCache);
        return null;
    }

    private static string? Taglia(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();

    /// <summary>Esiti per IdCampione. In cache 2 minuti; «aggiorna» la svuota.</summary>
    public async Task<Dictionary<int, EsitoCampione>> CalcolaAsync(bool aggiorna, CancellationToken ct)
    {
        if (!aggiorna && _cache.TryGetValue(ChiaveCache, out Dictionary<int, EsitoCampione>? c) && c is not null) return c;
        await Blocco.WaitAsync(ct);
        try
        {
            if (!aggiorna && _cache.TryGetValue(ChiaveCache, out c) && c is not null) return c;
            var prove = await _arclab.ProveDaValidareAsync(ct);
            var schede = await _ctx.LimitiNormativi.AsNoTracking()
                .Where(s => s.Stato == StatoSchedaLimite.Validata || s.Stato == StatoSchedaLimite.DaRiverificare)
                .Include(s => s.Abbinamenti).ToListAsync(ct);
            var categorie = await CategorieMatriciAsync(ct);
            var esiti = MotoreSegnalazioni.Calcola(prove, schede, categorie, await TempiAsync(ct));
            _cache.Set(ChiaveCache, esiti, TimeSpan.FromMinutes(2));
            return esiti;
        }
        finally { Blocco.Release(); }
    }

    /// <summary>
    /// 05/11/2026: limiti di legge validati che si applicano alle prove di un campione (chiave IdProva), per il
    /// prospetto dell'analisi AI. Vuoto se non ce ne sono (es. famiglia senza schede validate).
    /// </summary>
    public async Task<Dictionary<int, IReadOnlyList<string>>> LimitiCampioneAsync(int idCampione, CancellationToken ct)
    {
        var prove = await _arclab.ProveCampioneAsync(idCampione, ct);
        if (prove.Count == 0) return new();
        var schede = await _ctx.LimitiNormativi.AsNoTracking()
            .Where(s => s.Stato == StatoSchedaLimite.Validata || s.Stato == StatoSchedaLimite.DaRiverificare)
            .Include(s => s.Abbinamenti).ToListAsync(ct);
        return MotoreSegnalazioni.LimitiPerProva(prove, schede, await CategorieMatriciAsync(ct));
    }

    /// <summary>
    /// 05/11/2026: norme che hanno limiti validati per questa matrice ArcLab, per la tendina «giudizio di
    /// conformità rispetto a» delle richieste dei privati. Stessa regola del controllo automatico: famiglia della
    /// matrice, matrice abbinata alla scheda o categoria della matrice. Il titolo della fonte, altrimenti il riferimento.
    /// </summary>
    public async Task<IReadOnlyList<string>> NormePerMatriceAsync(int? idMatrice, string? matrice, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(matrice)) return [];
        var famiglia = RegoleLimiti.FamigliaDaMatrice(matrice);
        var schede = await _ctx.LimitiNormativi.AsNoTracking()
            .Where(s => s.Famiglia == famiglia
                        && (s.Stato == StatoSchedaLimite.Validata || s.Stato == StatoSchedaLimite.DaRiverificare))
            .Include(s => s.Abbinamenti)
            .Include(s => s.VersioneFonte!).ThenInclude(v => v.Fonte)
            .ToListAsync(ct);

        HashSet<string>? chiavi = null;
        if (!RegoleLimiti.CategoriaDaFamiglia(famiglia))
        {
            if (idMatrice is not int im || !(await CategorieMatriciAsync(ct)).TryGetValue(im, out var cat) || cat.Nessuna)
                chiavi = [];
            else chiavi = cat.Chiavi.ToHashSet();
        }

        var oggi = DateTime.Today;
        return schede
            .Where(s => (s.ValidoDal is null || s.ValidoDal <= oggi) && (s.ValidoAl is null || s.ValidoAl >= oggi))
            .Where(s => s.Abbinamenti.Any(a => a.Attivo && (a.IdMatriceArcLab is null || a.IdMatriceArcLab == idMatrice)))
            .Where(s => chiavi is null || chiavi.Contains(RegoleLimiti.ChiaveCategoria(s.MatriceCategoria))
                        || (idMatrice is int m && s.Abbinamenti.Any(a => a.Attivo && a.IdMatriceArcLab == m)))
            .Select(s => s.VersioneFonte?.Fonte?.Titolo is { Length: > 0 } t ? t.Trim() : s.Riferimento?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .ToList()!;
    }

    private async Task<Dictionary<int, CategorieMatrice>> CategorieMatriciAsync(CancellationToken ct)
    {
        var righe = await (from a in _ctx.MatriciCategorie.AsNoTracking()
                           where a.Stato == StatoAbbinamentoMatrice.Confermato
                           join c in _ctx.CategorieNormative.AsNoTracking() on a.IdCategoriaNormativa equals (int?)c.IdCategoriaNormativa into cc
                           from c in cc.DefaultIfEmpty()
                           select new { a.IdMatriceArcLab, a.IdCategoriaNormativa, Chiave = c == null ? null : c.Chiave }).ToListAsync(ct);
        return righe.GroupBy(r => r.IdMatriceArcLab).ToDictionary(g => g.Key, g => new CategorieMatrice(
            g.Any(r => r.IdCategoriaNormativa is null),
            g.Where(r => r.Chiave is not null).Select(r => r.Chiave!).ToHashSet()));
    }
}
