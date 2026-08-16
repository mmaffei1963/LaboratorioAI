using System.Globalization;
using System.Text;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.Import;

/// <summary>
/// Importa gli alimenti generici dal Canadian Nutrient File (CNF), formato CSV
/// relazionale: Food_Name.csv + Nutrient_Amount.csv. Valori per 100 g, Sale dal
/// Sodio. Idempotente per codice «CNF-{Food_Code}». Solo database proprio.
/// </summary>
public interface IImportatoreCnf
{
    Task<EsitoImportUsda> ImportaAsync(string cartella, CancellationToken ct = default);
}

/// <inheritdoc />
public class ImportatoreCnf : IImportatoreCnf
{
    private readonly ContestoLaboratorioAI _ctx;

    public ImportatoreCnf(ContestoLaboratorioAI ctx) => _ctx = ctx;

    // Nutrient_Code CNF → campo (stessi numeri INFOODS della USDA).
    private const int NProteine = 203, NGrassi = 204, NCarbo = 205, NEnergiaKcal = 208,
        NAcqua = 255, NEnergiaKj = 268, NZuccheri = 269, NFibre = 291,
        NCalcio = 301, NFerro = 303, NPotassio = 306, NSodio = 307,
        NVitaminaD = 328, NColesterolo = 601, NTrans = 605, NSaturi = 606,
        NMono = 645, NPoli = 646;

    public async Task<EsitoImportUsda> ImportaAsync(string cartella, CancellationToken ct = default)
    {
        var fFood = TrovaFile(cartella, "Food_Name.csv");
        var fNutr = TrovaFile(cartella, "Nutrient_Amount.csv");
        if (fFood is null || fNutr is null)
            throw new InvalidOperationException(
                "Nella cartella indicata non trovo Food_Name.csv e Nutrient_Amount.csv del CNF.");

        var valori = new Dictionary<int, ValoriCnf>();
        foreach (var riga in File.ReadLines(fNutr).Skip(1))
        {
            ct.ThrowIfCancellationRequested();
            var c = Csv(riga);
            if (c.Count < 3) continue;
            if (!int.TryParse(c[0], out var food)) continue;
            if (!int.TryParse(c[1], out var nutr)) continue;
            if (!decimal.TryParse(c[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var amt)) continue;

            if (!Rilevante(nutr)) continue;
            if (!valori.TryGetValue(food, out var v)) { v = new ValoriCnf(); valori[food] = v; }
            Assegna(v, nutr, amt);
        }

        var nomi = new Dictionary<int, string>();
        foreach (var riga in File.ReadLines(fFood).Skip(1))
        {
            var c = Csv(riga);
            if (c.Count < 2) continue;
            if (int.TryParse(c[0], out var food) && !string.IsNullOrWhiteSpace(c[1]))
                nomi[food] = c[1].Trim();
        }

        var fonte = await _ctx.Fonti.FirstOrDefaultAsync(f => f.Tipo == TipoFonte.DatabaseCnf, ct);
        if (fonte is null)
        {
            fonte = new Fonte
            {
                Tipo = TipoFonte.DatabaseCnf,
                Descrizione = "Canadian Nutrient File (CNF)",
                Url = "https://food-nutrition.canada.ca/cnf-fce/",
                PrioritaDefault = 2
            };
            _ctx.Fonti.Add(fonte);
        }

        var esistenti = await _ctx.MateriePrime
            .Include(m => m.Valori)
            .Where(m => m.Tipo == TipoMateriaPrima.Generica && m.CodiceInterno != null && m.CodiceInterno.StartsWith("CNF-"))
            .ToDictionaryAsync(m => m.CodiceInterno!, ct);

        var ora = DateTime.UtcNow;
        int nuove = 0, aggiornate = 0, contatore = 0;

        foreach (var (food, nome) in nomi)
        {
            if (!valori.TryGetValue(food, out var v)) continue;
            var codice = "CNF-" + food;

            if (!esistenti.TryGetValue(codice, out var m))
            {
                m = new MateriaPrima
                {
                    Tipo = TipoMateriaPrima.Generica,
                    CodiceInterno = codice,
                    DataCreazione = ora,
                    DataUltimaModifica = ora,
                    Attivo = true
                };
                _ctx.MateriePrime.Add(m);
                esistenti[codice] = m;
                nuove++;
            }
            else { m.DataUltimaModifica = ora; m.Attivo = true; aggiornate++; }

            m.Denominazione = nome.Length > 255 ? nome[..255] : nome;

            var set = m.Valori.FirstOrDefault(x => x.Corrente);
            if (set is null)
            {
                set = new ValoreNutrizionale { Corrente = true, DataCreazione = ora, DataAcquisizione = ora };
                m.Valori.Add(set);
            }

            set.Grassi = v.Grassi ?? 0m;
            set.GrassiSaturi = v.Saturi ?? 0m;
            set.Carboidrati = v.Carbo ?? 0m;
            set.Zuccheri = v.Zuccheri ?? 0m;
            set.Proteine = v.Proteine ?? 0m;
            set.Fibre = v.Fibre ?? 0m;
            set.Acqua = v.Acqua ?? 0m;
            set.Sale = v.Sodio.HasValue ? v.Sodio.Value * 2.5m / 1000m : 0m;
            set.EnergiaKcal = v.EnergiaKcal;
            set.EnergiaKj = v.EnergiaKj;
            set.Sodio = v.Sodio;
            set.GrassiTrans = v.Trans;
            set.GrassiMonoinsaturi = v.Mono;
            set.GrassiPolinsaturi = v.Poli;
            set.Colesterolo = v.Colesterolo;
            set.VitaminaD = v.VitaminaD;
            set.Calcio = v.Calcio;
            set.Ferro = v.Ferro;
            set.Potassio = v.Potassio;
            set.Priorita = 2;
            set.Fonte = fonte;

            if (++contatore % 1000 == 0)
                await _ctx.SaveChangesAsync(ct);
        }

        await _ctx.SaveChangesAsync(ct);
        return new EsitoImportUsda { Alimenti = valori.Count, Nuove = nuove, Aggiornate = aggiornate };
    }

    private static bool Rilevante(int n) => n is NProteine or NGrassi or NCarbo or NEnergiaKcal
        or NAcqua or NEnergiaKj or NZuccheri or NFibre or NCalcio or NFerro or NPotassio
        or NSodio or NVitaminaD or NColesterolo or NTrans or NSaturi or NMono or NPoli;

    private static void Assegna(ValoriCnf v, int n, decimal a)
    {
        switch (n)
        {
            case NProteine: v.Proteine = a; break;
            case NGrassi: v.Grassi = a; break;
            case NCarbo: v.Carbo = a; break;
            case NEnergiaKcal: v.EnergiaKcal = a; break;
            case NAcqua: v.Acqua = a; break;
            case NEnergiaKj: v.EnergiaKj = a; break;
            case NZuccheri: v.Zuccheri = a; break;
            case NFibre: v.Fibre = a; break;
            case NCalcio: v.Calcio = a; break;
            case NFerro: v.Ferro = a; break;
            case NPotassio: v.Potassio = a; break;
            case NSodio: v.Sodio = a; break;
            case NVitaminaD: v.VitaminaD = a; break;
            case NColesterolo: v.Colesterolo = a; break;
            case NTrans: v.Trans = a; break;
            case NSaturi: v.Saturi = a; break;
            case NMono: v.Mono = a; break;
            case NPoli: v.Poli = a; break;
        }
    }

    private static string? TrovaFile(string cartella, string nome)
    {
        var diretto = Path.Combine(cartella, nome);
        if (File.Exists(diretto)) return diretto;
        if (Directory.Exists(cartella))
            foreach (var sub in Directory.GetDirectories(cartella))
            {
                var p = Path.Combine(sub, nome);
                if (File.Exists(p)) return p;
            }
        return null;
    }

    private static List<string> Csv(string riga)
    {
        var campi = new List<string>();
        var sb = new StringBuilder();
        bool inVirgolette = false;
        for (int i = 0; i < riga.Length; i++)
        {
            char c = riga[i];
            if (inVirgolette)
            {
                if (c == '"')
                {
                    if (i + 1 < riga.Length && riga[i + 1] == '"') { sb.Append('"'); i++; }
                    else inVirgolette = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inVirgolette = true;
                else if (c == ',') { campi.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        campi.Add(sb.ToString());
        return campi;
    }

    private class ValoriCnf
    {
        public decimal? Grassi, Saturi, Trans, Mono, Poli, Carbo, Zuccheri, Proteine, Fibre, Acqua,
            Colesterolo, Sodio, Calcio, Ferro, Potassio, VitaminaD, EnergiaKcal, EnergiaKj;
    }
}
