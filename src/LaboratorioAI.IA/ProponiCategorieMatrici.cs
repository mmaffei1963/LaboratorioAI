using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Limiti;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LaboratorioAI.IA;

/// <summary>
/// Fase 1b (18/10/2026): l'AI propone a quali categorie normative appartiene ogni matrice ArcLab degli alimenti ancora
/// senza abbinamento. Le proposte vanno confermate da una persona nella pagina «Categorie e matrici».
/// </summary>
public class ProponiCategorieMatrici
{
    private const int MatriciPerChiamata = 60, TokenUscita = 12000, TimeoutSecondi = 600;
    private readonly IClientAnthropic _ia;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly ServizioCategorieLimiti _categorie;
    private readonly ILogger<ProponiCategorieMatrici> _log;

    public ProponiCategorieMatrici(IClientAnthropic ia, ContestoLaboratorioAI ctx, ServizioCategorieLimiti categorie, ILogger<ProponiCategorieMatrici> log)
    {
        _ia = ia; _ctx = ctx; _categorie = categorie; _log = log;
    }

    public class PropostaMatrice
    {
        public int IdMatrice { get; set; }
        public List<int>? Categorie { get; set; }
        public string? Nota { get; set; }
    }

    private const string Istruzioni = """
        Sei un esperto di sicurezza alimentare (Reg. CE 2073/2005, Reg. UE 2023/915) in un laboratorio di prova italiano.
        Devi abbinare le MATRICI del gestionale del laboratorio (come le scrivono gli operatori in accettazione) alle
        CATEGORIE di alimenti delle norme, elencate nel messaggio di sistema con il loro numero.

        Per ogni matrice indica TUTTE le categorie a cui un campione di quella matrice appartiene normalmente.
        Esempi: «Prosciutto cotto» → alimenti pronti favorevoli a Listeria + prodotti a base di carne cotti;
        «Mozzarella» → formaggi freschi/molli + alimenti pronti. Se la matrice è generica o ambigua scegli le categorie
        plausibili e spiegalo in «nota». Se nessuna categoria si applica (es. superfici, materie non alimentari) usa una
        lista vuota.

        Rispondi ESCLUSIVAMENTE con un array JSON:
        [ { "idMatrice": numero, "categorie": [numeri delle categorie], "nota": "breve motivazione o dubbio, oppure null" } ]
        Usa solo numeri di categoria presenti nell'elenco.
        """;

    public async Task EseguiAsync(int idGiro, CancellationToken ct)
    {
        var giro = await _ctx.GiriVerificaLimiti.FirstAsync(g => g.IdGiroVerificaLimiti == idGiro, ct);
        var categorie = await _categorie.SincronizzaAsync(ct);
        if (categorie.Count == 0) throw new InvalidOperationException("Nessuna categoria: prima estrarre le schede degli Alimenti.");

        // parametri di esempio per categoria (aiutano l'AI a capire di cosa si parla)
        var schede = await _ctx.LimitiNormativi.AsNoTracking()
            .Where(s => s.Stato != StatoSchedaLimite.Respinta && s.Stato != StatoSchedaLimite.Superata && s.MatriceCategoria != null)
            .Select(s => new { s.Famiglia, s.MatriceCategoria, s.Parametro }).ToListAsync(ct);
        var sistema = "CATEGORIE (numero | famiglia | categoria della norma | parametri):\n" + string.Join("\n", categorie.Select(c =>
            $"{c.IdCategoriaNormativa} | {RegoleLimiti.CodiceFamiglia(c.Famiglia)} | {c.Nome} | " + string.Join(", ", schede
                .Where(s => s.Famiglia == c.Famiglia && RegoleLimiti.ChiaveCategoria(s.MatriceCategoria) == c.Chiave)
                .Select(s => s.Parametro).Distinct().Take(8))));
        var idCategorie = categorie.Select(c => c.IdCategoriaNormativa).ToHashSet();

        var gia = await _ctx.MatriciCategorie.Where(a => a.Stato != StatoAbbinamentoMatrice.Respinto).Select(a => a.IdMatriceArcLab).Distinct().ToListAsync(ct);
        var matrici = (await _categorie.MatriciDaAbbinareAsync(ct)).Where(m => !gia.Contains(m.IdMatrice)).ToList();
        int proposte = 0, nessuna = 0, fatte = 0, tokenIn = 0, tokenOut = 0;
        foreach (var blocco in matrici.Chunk(MatriciPerChiamata))
        {
            giro.Esito = $"Proposta in corso: {fatte}/{matrici.Count} matrici…";
            await _ctx.SaveChangesAsync(ct);
            var messaggio = Istruzioni + "\n\nMATRICI (idMatrice | nome | prove in 3 anni):\n" + string.Join("\n", blocco.Select(m => $"{m.IdMatrice} | {m.Nome} | {m.Prove}"));
            var r = await _ia.InterrogaLungaAsync(sistema, messaggio, TokenUscita, TimeoutSecondi, ct);
            tokenIn += r.TokenInput + r.TokenCacheScritti + r.TokenCacheLetti; tokenOut += r.TokenOutput;
            var lette = Leggi(r.Testo);
            foreach (var m in blocco)
            {
                fatte++;
                var p = lette.FirstOrDefault(x => x.IdMatrice == m.IdMatrice);
                if (p is null) continue;
                var nota = string.IsNullOrWhiteSpace(p.Nota) ? null : (p.Nota.Length > 1000 ? p.Nota[..1000] : p.Nota.Trim());
                var cats = (p.Categorie ?? []).Where(idCategorie.Contains).Distinct().ToList();
                if (cats.Count == 0)
                {
                    _ctx.MatriciCategorie.Add(Nuovo(m, null, nota)); nessuna++;
                }
                else foreach (var c in cats) { _ctx.MatriciCategorie.Add(Nuovo(m, c, nota)); proposte++; }
            }
            await _ctx.SaveChangesAsync(ct);
        }
        giro.Stato = StatoGiroVerifica.Completato;
        giro.DataFine = DateTime.UtcNow;
        giro.SchedeControllate = matrici.Count;
        giro.Esito = $"{matrici.Count} matrici esaminate: {proposte} abbinamenti proposti, {nessuna} «nessuna categoria». Token {tokenIn:N0}+{tokenOut:N0}.";
        await _ctx.SaveChangesAsync(ct);
        _log.LogInformation("Proposta abbinamenti matrici: {Esito}", giro.Esito);
    }

    private static AbbinamentoMatriceCategoria Nuovo(MatriceArcLab m, int? idCategoria, string? nota) => new()
    {
        IdMatriceArcLab = m.IdMatrice, NomeMatrice = m.Nome.Length > 300 ? m.Nome[..300] : m.Nome, IdCategoriaNormativa = idCategoria,
        Stato = StatoAbbinamentoMatrice.Proposto, PropostoDa = "AI", DataProposta = DateTime.UtcNow, Nota = nota
    };

    private static readonly JsonSerializerOptions Opzioni = new() { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };

    public static List<PropostaMatrice> Leggi(string testo)
    {
        var i = testo.IndexOf('['); var f = testo.LastIndexOf(']');
        if (i >= 0 && f > i)
            try { return JsonSerializer.Deserialize<List<PropostaMatrice>>(testo[i..(f + 1)], Opzioni) ?? []; } catch (JsonException) { }
        if (i < 0) return [];
        var corpo = testo[i..];
        for (var k = corpo.LastIndexOf('}'); k > 0; k = corpo.LastIndexOf('}', k - 1))
            try { return JsonSerializer.Deserialize<List<PropostaMatrice>>(corpo[..(k + 1)] + "]", Opzioni) ?? []; } catch (JsonException) { }
        return [];
    }
}
