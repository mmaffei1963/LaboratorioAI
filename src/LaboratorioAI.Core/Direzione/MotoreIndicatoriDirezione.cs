namespace LaboratorioAI.Core.Direzione;

/// <summary>Movimento sommato (per anno) usato dal motore: centro, voce, conto, importo con segno gestionale.</summary>
public record MovimentoIndicatori(int Anno, string Centro, string Voce, string Conto, decimal Importo);

/// <summary>Classificazione di una voce per il motore.</summary>
public record VoceIndicatori(string Codice, bool Ricavo, string? TipoCosto, string Macrocategoria, bool MateriaPrima);

/// <summary>Indicatori economici di un anno (MAREA o un'area). Gli importi di costo sono positivi.</summary>
public record IndicatoriAnno
{
    public int Anno { get; init; }
    public decimal Fatturato { get; init; }
    public decimal SpeseFisse { get; init; }
    /// <summary>Solo per un'area: quota di 1/5 delle spese fisse dell'Amministrazione già inclusa in <see cref="SpeseFisse"/>.</summary>
    public decimal QuotaAmministrazione { get; init; }
    public decimal SpeseVariabili { get; init; }
    public decimal MateriePrime { get; init; }
    public decimal MargineContribuzione { get; init; }
    public decimal Personale { get; init; }
    public decimal ValoreAggiunto { get; init; }
    public decimal Ebitda { get; init; }
    public decimal Ammortamenti { get; init; }
    public decimal Ebit { get; init; }
    public decimal Imposte { get; init; }
    public decimal UtileAnteImposte { get; init; }
    public decimal UtileNetto { get; init; }
    /// <summary>Costi di voci senza tipo (né fisse né variabili): da classificare.</summary>
    public decimal CostiNonClassificati { get; init; }
    public decimal? FatturatoPareggio { get; init; }

    public decimal? MarginalitaPercento => Rapporto(UtileAnteImposte, Fatturato);
    public decimal? IncidenzaVariabili => Rapporto(SpeseVariabili, Fatturato);
    public decimal? IncidenzaPersonale => Rapporto(Personale, Fatturato);
    public decimal? Ros => Rapporto(Ebit, Fatturato);
    public decimal? PareggioPercento => FatturatoPareggio is decimal p ? Rapporto(p, Fatturato) : null;

    private static decimal? Rapporto(decimal a, decimal b) => b == 0 ? null : a / b;
}

/// <summary>
/// Indicatori economici del cruscotto Direzione (13/10/2026), funzione pura.
///
/// Stesse grandezze e stesse regole di classificazione del report Power BI «AZW_TS_VERSION» (Indicatori MAREA e
/// Indicatori area), con due correzioni di definizione (decise in fase 1, vedi analisi-cruscotto-direzione.md):
/// <list type="bullet">
/// <item>Valore aggiunto ed EBITDA escludono ammortamenti e imposte (in Power BI l'EBITDA coincide con l'utile netto e
/// l'EBIT toglie due volte gli ammortamenti).</item>
/// <item>La quota di 1/5 delle spese fisse dell'Amministrazione (centro 03) si aggiunge solo a un'area diversa da 03,
/// non al totale MAREA (in Power BI si somma anche al totale, dove le spese di amministrazione sono già comprese).</item>
/// </list>
/// Il resto coincide con Power BI al centesimo (fatturato, spese variabili, materie prime, margine di contribuzione,
/// personale, ammortamenti, imposte, utile ante imposte, utile di bilancio).
/// </summary>
public static class MotoreIndicatoriDirezione
{
    public static readonly HashSet<string> ContiAmmortamenti = new(StringComparer.OrdinalIgnoreCase)
        { "FS56800", "FT56900", "FT56901", "FP56500", "FP56502" };
    public static readonly HashSet<string> ContiImposte = new(StringComparer.OrdinalIgnoreCase) { "MX82504", "MX82502" };
    public const string CentroAmministrazione = "03";
    public const decimal QuotaAmministrazioneAree = 0.2m;

    /// <param name="centro">null = tutta MAREA; altrimenti il centro di costo dell'area.</param>
    public static IReadOnlyList<IndicatoriAnno> Calcola(IEnumerable<MovimentoIndicatori> movimenti,
        IEnumerable<VoceIndicatori> voci, IEnumerable<int> anni, string? centro = null)
    {
        var perVoce = voci.GroupBy(v => v.Codice, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var tutti = movimenti.ToList();
        var area = string.IsNullOrWhiteSpace(centro) ? null : centro.Trim();
        var risultato = new List<IndicatoriAnno>();

        foreach (var anno in anni.Distinct().OrderBy(a => a))
        {
            decimal fatt = 0, fissi = 0, variabili = 0, mp = 0, pers = 0, amm = 0, imp = 0, tot = 0, nonClass = 0, costiEsterni = 0;
            decimal fissiAmministrazione = 0;
            foreach (var m in tutti.Where(m => m.Anno == anno))
            {
                var v = perVoce.TryGetValue(m.Voce, out var vv) ? vv : new VoceIndicatori(m.Voce, false, null, ClassificatoreVociAzw.MacroAltri, false);
                var costo = !v.Ricavo;
                if (costo && v.TipoCosto == ClassificatoreVociAzw.Fisso && m.Centro == CentroAmministrazione) fissiAmministrazione -= m.Importo;
                if (area is not null && !string.Equals(m.Centro, area, StringComparison.OrdinalIgnoreCase)) continue;

                tot += m.Importo;
                var ammortamento = ContiAmmortamenti.Contains(m.Conto);
                var imposta = ContiImposte.Contains(m.Conto);
                if (v.Ricavo) { fatt += m.Importo; continue; }
                if (v.TipoCosto == ClassificatoreVociAzw.Fisso) fissi -= m.Importo;
                else if (v.TipoCosto == ClassificatoreVociAzw.Variabile) variabili -= m.Importo;
                else nonClass -= m.Importo;
                if (v.MateriaPrima) mp -= m.Importo;
                if (ammortamento) amm -= m.Importo;
                if (imposta) imp -= m.Importo;
                if (v.Macrocategoria == ClassificatoreVociAzw.MacroPersonale) pers -= m.Importo;
                else if (!ammortamento && !imposta) costiEsterni -= m.Importo;
            }

            var quota = area is not null && area != CentroAmministrazione ? Math.Round(fissiAmministrazione * QuotaAmministrazioneAree, 2) : 0m;
            fissi += quota;
            var utileNetto = tot - quota;
            var utileAnte = utileNetto + imp;
            var va = fatt - costiEsterni - quota;
            var ebitda = va - pers;
            var mdc = fatt - variabili;
            risultato.Add(new IndicatoriAnno
            {
                Anno = anno,
                Fatturato = fatt,
                SpeseFisse = fissi,
                QuotaAmministrazione = quota,
                SpeseVariabili = variabili,
                MateriePrime = mp,
                MargineContribuzione = mdc,
                Personale = pers,
                ValoreAggiunto = va,
                Ebitda = ebitda,
                Ammortamenti = amm,
                Ebit = ebitda - amm,
                Imposte = imp,
                UtileAnteImposte = utileAnte,
                UtileNetto = utileNetto,
                CostiNonClassificati = nonClass,
                // Fatturato di pareggio = spese fisse ÷ (margine di contribuzione ÷ fatturato), come in Power BI.
                FatturatoPareggio = mdc > 0 && fatt != 0 ? Math.Round(fissi * fatt / mdc, 2) : null
            });
        }
        return risultato;
    }
}
