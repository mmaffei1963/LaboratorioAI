using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Limiti;

/// <summary>Matrice ArcLab con le prove degli ultimi 3 anni (per l'abbinamento alle categorie).</summary>
public record MatriceArcLab(int IdMatrice, string Nome, FamigliaNormativa Famiglia, int Prove);

/// <summary>
/// Fase 1b (18/10/2026): categorie normative e abbinamento delle matrici ArcLab. Le categorie nascono dal testo
/// «Matrice / categoria» delle schede degli Alimenti; ogni matrice ArcLab degli alimenti si abbina a una o più
/// categorie (proposta AI o a mano, poi confermata). Per Legionella, Acque e Molluschi basta la famiglia.
/// </summary>
public class ServizioCategorieLimiti
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryLimitiArcLab _arclab;

    public ServizioCategorieLimiti(ContestoLaboratorioAI ctx, IRepositoryLimitiArcLab arclab) { _ctx = ctx; _arclab = arclab; }

    /// <summary>Crea le categorie che mancano e disattiva quelle senza più schede. Restituisce le categorie attive.</summary>
    public async Task<List<CategoriaNormativa>> SincronizzaAsync(CancellationToken ct)
    {
        var testi = await _ctx.LimitiNormativi.AsNoTracking()
            .Where(s => s.Stato != StatoSchedaLimite.Respinta && s.Stato != StatoSchedaLimite.Superata && s.MatriceCategoria != null)
            .Select(s => new { s.Famiglia, s.MatriceCategoria }).Distinct().ToListAsync(ct);
        var richieste = testi.Where(t => !RegoleLimiti.CategoriaDaFamiglia(t.Famiglia))
            .Select(t => (t.Famiglia, Nome: t.MatriceCategoria!.Trim(), Chiave: RegoleLimiti.ChiaveCategoria(t.MatriceCategoria)))
            .Where(t => t.Chiave.Length > 0)
            .GroupBy(t => (t.Famiglia, t.Chiave)).Select(g => g.First()).ToList();
        var esistenti = await _ctx.CategorieNormative.ToListAsync(ct);
        foreach (var r in richieste.Where(r => !esistenti.Any(e => e.Famiglia == r.Famiglia && e.Chiave == r.Chiave)))
        {
            var c = new CategoriaNormativa
            {
                Famiglia = r.Famiglia, Nome = r.Nome.Length > 500 ? r.Nome[..500] : r.Nome, Chiave = r.Chiave, Attiva = true, DataCreazione = DateTime.UtcNow
            };
            _ctx.CategorieNormative.Add(c);
            esistenti.Add(c);
        }
        foreach (var e in esistenti)
            e.Attiva = richieste.Any(r => r.Famiglia == e.Famiglia && r.Chiave == e.Chiave);
        await _ctx.SaveChangesAsync(ct);
        return esistenti.Where(e => e.Attiva).OrderBy(e => e.Famiglia).ThenBy(e => e.Nome).ToList();
    }

    /// <summary>Matrici ArcLab degli ultimi 3 anni che richiedono l'abbinamento (famiglie senza categoria automatica).</summary>
    public async Task<List<MatriceArcLab>> MatriciDaAbbinareAsync(CancellationToken ct) =>
        (await _arclab.ParametriUsatiAsync(3, ct))
            .Where(p => p.IdMatrice is int && !string.IsNullOrWhiteSpace(p.Matrice))
            .GroupBy(p => p.IdMatrice!.Value)
            .Select(g => new MatriceArcLab(g.Key, g.First().Matrice!.Trim(), RegoleLimiti.FamigliaDaMatrice(g.First().Matrice), g.Sum(x => x.Prove)))
            .Where(m => !RegoleLimiti.CategoriaDaFamiglia(m.Famiglia))
            .OrderByDescending(m => m.Prove).ToList();

    public async Task AggiungiAsync(int idMatrice, string nomeMatrice, int? idCategoria, bool conferma, OperatoreLimiti op, string? nota, CancellationToken ct)
    {
        var esiste = await _ctx.MatriciCategorie.FirstOrDefaultAsync(a => a.IdMatriceArcLab == idMatrice && a.IdCategoriaNormativa == idCategoria
            && a.Stato != StatoAbbinamentoMatrice.Respinto, ct);
        if (esiste is null)
        {
            esiste = new AbbinamentoMatriceCategoria
            {
                IdMatriceArcLab = idMatrice, NomeMatrice = nomeMatrice.Length > 300 ? nomeMatrice[..300] : nomeMatrice, IdCategoriaNormativa = idCategoria,
                Stato = StatoAbbinamentoMatrice.Proposto, PropostoDa = op.Nome, DataProposta = DateTime.UtcNow, Nota = nota
            };
            _ctx.MatriciCategorie.Add(esiste);
        }
        if (conferma && esiste.Stato != StatoAbbinamentoMatrice.Confermato)
        {
            esiste.Stato = StatoAbbinamentoMatrice.Confermato;
            esiste.ConfermatoDa = op.Nome; esiste.IdUtenteConferma = op.IdUtente; esiste.DataConferma = DateTime.UtcNow;
        }
        // «nessuna categoria» confermata esclude le categorie della matrice, e viceversa
        if (conferma)
        {
            var altri = await _ctx.MatriciCategorie.Where(a => a.IdMatriceArcLab == idMatrice && a.Stato != StatoAbbinamentoMatrice.Respinto
                && (idCategoria == null ? a.IdCategoriaNormativa != null : a.IdCategoriaNormativa == null)).ToListAsync(ct);
            foreach (var a in altri) Respingi(a, op);
        }
        await _ctx.SaveChangesAsync(ct);
    }

    private static void Respingi(AbbinamentoMatriceCategoria a, OperatoreLimiti op)
    {
        a.Stato = StatoAbbinamentoMatrice.Respinto;
        a.ConfermatoDa = op.Nome; a.IdUtenteConferma = op.IdUtente; a.DataConferma = DateTime.UtcNow;
    }

    public async Task<int> ConfermaAsync(IEnumerable<int> ids, OperatoreLimiti op, CancellationToken ct)
    {
        var l = ids.Distinct().ToList();
        var abb = await _ctx.MatriciCategorie.Where(a => l.Contains(a.IdAbbinamentoMatriceCategoria) && a.Stato == StatoAbbinamentoMatrice.Proposto).ToListAsync(ct);
        foreach (var a in abb)
        {
            a.Stato = StatoAbbinamentoMatrice.Confermato;
            a.ConfermatoDa = op.Nome; a.IdUtenteConferma = op.IdUtente; a.DataConferma = DateTime.UtcNow;
        }
        await _ctx.SaveChangesAsync(ct);
        return abb.Count;
    }

    public async Task<int> RespingiAsync(IEnumerable<int> ids, OperatoreLimiti op, CancellationToken ct)
    {
        var l = ids.Distinct().ToList();
        var abb = await _ctx.MatriciCategorie.Where(a => l.Contains(a.IdAbbinamentoMatriceCategoria) && a.Stato != StatoAbbinamentoMatrice.Respinto).ToListAsync(ct);
        foreach (var a in abb) Respingi(a, op);
        await _ctx.SaveChangesAsync(ct);
        return abb.Count;
    }

    public async Task<string?> AccodaPropostaAsync(OperatoreLimiti op, CancellationToken ct)
    {
        if (await _ctx.GiriVerificaLimiti.AnyAsync(g => g.Tipo == TipoVerificaLimiti.AbbinamentoMatrici
                && (g.Stato == StatoGiroVerifica.InCoda || g.Stato == StatoGiroVerifica.InCorso), ct))
            return "La proposta degli abbinamenti è già in coda o in corso.";
        if ((await SincronizzaAsync(ct)).Count == 0)
            return "Non ci sono ancora categorie: servono schede degli Alimenti (proposte o validate) con il campo «Matrice / categoria». "
                + "Estrai prima il Reg. 2073/2005 (e il 2023/915) nella scheda Fonti, oppure lavora nell'ambiente dove le schede ci sono.";
        _ctx.GiriVerificaLimiti.Add(new GiroVerificaLimiti
        {
            Tipo = TipoVerificaLimiti.AbbinamentoMatrici, Stato = StatoGiroVerifica.InCoda, DataRichiesta = DateTime.UtcNow,
            RichiestoDa = op.Nome, IdUtenteRichiesta = op.IdUtente, Esito = "In coda"
        });
        await _ctx.SaveChangesAsync(ct);
        return null;
    }
}
