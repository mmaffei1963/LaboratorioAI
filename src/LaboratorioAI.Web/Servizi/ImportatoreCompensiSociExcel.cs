using System.Globalization;
using ClosedXML.Excel;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.Direzione;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <summary>Confronto di un socio tra il foglio Excel e il calcolo di LaboratorioAI dopo l'importazione.</summary>
public sealed record ConfrontoImportazione(string Nome, decimal TotaleExcel, decimal TotaleCalcolato, decimal DifferenzaExcel,
    decimal DifferenzaCalcolata, decimal PercepitoExcel, decimal PercepitoAzw);

public sealed record EsitoImportazioneCompensi(int Anno, string Foglio, List<ConfrontoImportazione> Confronto, List<string> Avvisi);

/// <summary>
/// Compensi soci (21/10/2026): carica un anno dal vecchio file Excel (foglio nel formato «2026x2025»), per il controllo
/// del primo anno. Legge i valori salvati da Excel (non le formule). Righe dei soci da 4 in giù (nome in colonna B),
/// % RSPP in C10, % indennità laboratorio in C14, base laboratorio in B15, premi area laboratorio sotto «PREMIO AREA LAB».
/// I rimborsi entrano come un unico rimborso per socio (già con le maggiorazioni del foglio); il recupero è la colonna M;
/// la base del laboratorio diventa «manuale». Se il percepito del foglio è diverso da AZW si aggiunge una rettifica con nota.
/// </summary>
public class ImportatoreCompensiSociExcel
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    public const string DescrizioneRimborsoImportato = "Totale rimborsi dal foglio Excel";

    private readonly ContestoLaboratorioAI _ctx;
    private readonly ServizioCompensiSoci _servizio;

    public ImportatoreCompensiSociExcel(ContestoLaboratorioAI ctx, ServizioCompensiSoci servizio)
    {
        _ctx = ctx; _servizio = servizio;
    }

    private static decimal Num(IXLCell c)
    {
        var v = c.HasFormula ? c.CachedValue : c.Value;
        if (v.IsNumber) return (decimal)v.GetNumber();
        if (v.IsText)
        {
            var t = v.GetText().Replace("€", "").Replace(" ", "").Trim();
            if (decimal.TryParse(t, NumberStyles.Number, It, out var d)) return d;
        }
        return 0m;
    }

    private static string Txt(IXLCell c)
    {
        var v = c.HasFormula ? c.CachedValue : c.Value;
        return v.IsText ? v.GetText().Trim() : v.IsBlank ? "" : v.ToString(It).Trim();
    }

    private static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public async Task<EsitoImportazioneCompensi> ImportaAsync(Stream file, int anno, string? nomeFoglio, string? chi, CancellationToken ct)
    {
        using var libro = new XLWorkbook(file);
        var atteso = $"{anno + 1}x{anno}";
        var ws = !string.IsNullOrWhiteSpace(nomeFoglio) ? libro.Worksheets.FirstOrDefault(w => w.Name.Trim() == nomeFoglio.Trim())
               : libro.Worksheets.FirstOrDefault(w => w.Name.Trim() == atteso);
        if (ws is null) throw new InvalidOperationException($"Nel file non c'è il foglio «{(string.IsNullOrWhiteSpace(nomeFoglio) ? atteso : nomeFoglio)}».");
        var avvisi = new List<string>();

        await _servizio.AssicuraDatiInizialiAsync(ct);
        var soci = await _ctx.Soci.Where(s => s.Attivo).ToListAsync(ct);

        // Righe dei soci
        var righe = new List<(Socio Socio, decimal A, decimal C, decimal D, decimal E, decimal G, decimal H, decimal K, decimal L,
            decimal M, decimal N, decimal O, decimal P, decimal Q)>();
        for (var r = 4; r <= 20; r++)
        {
            var nome = Txt(ws.Cell(r, 2));
            if (nome.Length == 0) { if (righe.Count > 0) break; continue; }
            var socio = soci.FirstOrDefault(s => s.Nome.StartsWith(nome, StringComparison.OrdinalIgnoreCase));
            if (socio is null) { avvisi.Add($"Riga {r}: «{nome}» non corrisponde a nessun socio, saltata."); continue; }
            righe.Add((socio, Num(ws.Cell(r, 1)), Num(ws.Cell(r, 3)), Num(ws.Cell(r, 4)), Num(ws.Cell(r, 5)), Num(ws.Cell(r, 7)),
                Num(ws.Cell(r, 8)), Num(ws.Cell(r, 11)), Num(ws.Cell(r, 12)), Num(ws.Cell(r, 13)), Num(ws.Cell(r, 14)),
                Num(ws.Cell(r, 15)), Num(ws.Cell(r, 16)), Num(ws.Cell(r, 17))));
        }
        if (righe.Count == 0) throw new InvalidOperationException("Nel foglio non ho trovato le righe dei soci (nome in colonna B dalla riga 4).");

        // Premi area laboratorio: righe sotto «PREMIO AREA LAB» con nome (A) e importo (B).
        var premi = new List<(string Nome, decimal Importo)>();
        var rigaPremi = ws.RangeUsed()?.RowsUsed().FirstOrDefault(x => Txt(x.Cell(1)).StartsWith("PREMIO AREA LAB", StringComparison.OrdinalIgnoreCase));
        if (rigaPremi is not null)
            for (var r = rigaPremi.RowNumber() + 1; r <= rigaPremi.RowNumber() + 20; r++)
            {
                var n = Txt(ws.Cell(r, 1));
                if (n.Length == 0) break;
                premi.Add((n, Num(ws.Cell(r, 2))));
            }

        // Anno: si crea se manca; un anno chiuso non si tocca.
        var a = await _ctx.AnniCompensi.FirstOrDefaultAsync(x => x.Anno == anno, ct);
        if (a is null) { await _servizio.CreaAnnoAsync(anno, ct); a = await _ctx.AnniCompensi.FirstAsync(x => x.Anno == anno, ct); }
        if (a.Chiuso) throw new InvalidOperationException($"L'anno {anno} è chiuso: riaprilo prima di importare.");
        var ora = DateTime.UtcNow;

        var baseTP = righe.Max(x => x.C);
        a.BaseTempoPieno = baseTP;
        var sommaAmm = righe.Sum(x => x.D);
        if (sommaAmm > 0) a.CompensoAmministratore = sommaAmm;
        var resp = righe.Max(x => x.E); if (resp > 0) a.CompensoResponsabileArea = resp;
        var assic = righe.Max(x => x.L); if (assic > 0) a.AssicurazioneAuto = assic;
        a.Premio = righe.GroupBy(x => x.N).OrderByDescending(g => g.Count()).First().Key;
        var pRspp = Num(ws.Cell(10, 3)); if (pRspp > 0) a.PercentualeRspp = pRspp <= 1 ? pRspp * 100m : pRspp;
        var pLab = Num(ws.Cell(14, 3)); if (pLab > 0) a.PercentualeIndennitaLaboratorio = pLab <= 1 ? pLab * 100m : pLab;
        var baseLab = Num(ws.Cell(15, 2));
        if (baseLab > 0) { a.BaseLaboratorioManuale = baseLab; a.NotaBaseLaboratorio = $"Base usata nel foglio Excel «{ws.Name}»"; }
        a.TrasferteDaAzw = false;   // nel foglio le trasferte sono già nel totale dei rimborsi (colonna K)
        a.DataUltimaModifica = ora;

        // Premi: si disattivano quelli dell'anno e si caricano quelli del foglio. Un premio va a un socio se il suo nome
        // è l'inizio del nome del socio (es. «DANI» → Daniele) e quel socio ha in colonna H proprio la somma dei suoi premi.
        foreach (var vecchio in await _ctx.PremiAreaLaboratorio.Where(x => x.Anno == anno && x.Attivo).ToListAsync(ct)) vecchio.Attivo = false;
        var premiSocio = new Dictionary<int, decimal>();
        foreach (var (nome, importo) in premi)
        {
            var socio = righe.Select(x => x.Socio).FirstOrDefault(s => nome.Length >= 3 && s.Nome.StartsWith(nome, StringComparison.OrdinalIgnoreCase));
            _ctx.PremiAreaLaboratorio.Add(new PremioAreaLaboratorio
            {
                Anno = anno, Nome = nome, Importo = importo, IdSocio = socio?.IdSocio, DataCreazione = ora,
                Note = $"Dal foglio Excel «{ws.Name}»"
            });
            if (socio is not null) premiSocio[socio.IdSocio] = premiSocio.GetValueOrDefault(socio.IdSocio) + importo;
        }

        // Dati dei soci
        var dati = await _ctx.CompensiSociAnno.Where(x => x.Anno == anno).ToListAsync(ct);
        var categoria = await _ctx.CategorieRimborsi.OrderBy(c => c.Maggiorazione).ThenByDescending(c => c.Ordine).FirstAsync(c => c.Attivo, ct);
        var dal = new DateTime(anno, 1, 1); var al = new DateTime(anno, 12, 31);
        foreach (var vecchio in await _ctx.RimborsiSpese.Where(r => r.Attivo && r.Data >= dal && r.Data <= al
                     && r.Descrizione.StartsWith(DescrizioneRimborsoImportato)).ToListAsync(ct))
            vecchio.Attivo = false;
        foreach (var x in righe)
        {
            var d = dati.FirstOrDefault(y => y.IdSocio == x.Socio.IdSocio);
            if (d is null) { d = new CompensoSocioAnno { Anno = anno, IdSocio = x.Socio.IdSocio, DataCreazione = ora }; _ctx.CompensiSociAnno.Add(d); dati.Add(d); }
            d.OreSettimanali = baseTP > 0 ? R2(x.C / baseTP * a.OreTempoPieno) : a.OreTempoPieno;
            d.MesiAmministratore = a.CompensoAmministratore > 0 ? (int)Math.Round(x.D / (a.CompensoAmministratore / 12m)) : 0;
            d.ResponsabileArea = x.E > 0;
            d.PercentualeAutoPropria = assic > 0 ? R2(x.L / assic * 100m) : 0m;
            d.QuotaRspp = x.G > 0;
            d.IndennitaLaboratorio = x.H > 0 && x.H != premiSocio.GetValueOrDefault(x.Socio.IdSocio);
            d.Premio = x.N == a.Premio ? null : x.N;
            d.RecuperoManuale = R2(x.M);
            d.RettificaPercepito = null; d.NotaRettificaPercepito = null;
            d.DataUltimaModifica = ora;
            if (x.K != 0)
                _ctx.RimborsiSpese.Add(new RimborsoSpesa
                {
                    IdSocio = x.Socio.IdSocio, Data = al, IdCategoriaRimborso = categoria.IdCategoriaRimborso,
                    Descrizione = $"{DescrizioneRimborsoImportato} «{ws.Name}» (già con le maggiorazioni)", Importo = R2(x.K),
                    Maggiorazione = 0m, DataCreazione = ora, DataUltimaModifica = ora, InseritoDa = chi
                });
        }
        await _ctx.SaveChangesAsync(ct);

        // Percepito: se il foglio è diverso da AZW si allinea con una rettifica (si vede nella pagina, con la nota).
        var calcolo = await _servizio.CalcolaAsync(anno, ct) ?? throw new InvalidOperationException("Calcolo non riuscito.");
        var rettifiche = false;
        foreach (var x in righe)
        {
            var conto = calcolo.Soci.FirstOrDefault(c => c.IdSocio == x.Socio.IdSocio);
            if (conto is null || R2(x.P) == conto.PercepitoAzw) continue;
            var d = dati.First(y => y.IdSocio == x.Socio.IdSocio);
            d.RettificaPercepito = R2(x.P) - conto.PercepitoAzw;
            d.NotaRettificaPercepito = $"Allineato al foglio Excel «{ws.Name}» ({R2(x.P).ToString("N2", It)} €)";
            avvisi.Add($"{x.Socio.Nome}: percepito nel foglio {R2(x.P).ToString("N2", It)} €, in AZW {conto.PercepitoAzw.ToString("N2", It)} €: aggiunta una rettifica.");
            rettifiche = true;
        }
        if (rettifiche)
        {
            await _ctx.SaveChangesAsync(ct);
            calcolo = await _servizio.CalcolaAsync(anno, ct) ?? calcolo;
        }

        var confronto = righe.Select(x =>
        {
            var c = calcolo.Soci.First(s => s.IdSocio == x.Socio.IdSocio);
            return new ConfrontoImportazione(x.Socio.Nome, R2(x.O), c.Totale, R2(x.Q), c.Differenza, R2(x.P), c.PercepitoAzw);
        }).ToList();
        return new EsitoImportazioneCompensi(anno, ws.Name, confronto, avvisi);
    }
}
