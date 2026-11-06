using System.Globalization;
using System.Text.RegularExpressions;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Limiti;

/// <summary>Scheda da controllare con i dati che servono ai controlli automatici.</summary>
public record SchedaDaControllare(LimiteNormativo Scheda, IReadOnlyList<(string Nome, IReadOnlyList<string> Unita, decimal? Fattore)> ParametriArcLab);

/// <summary>Esito di un controllo automatico.</summary>
public record RilievoAutomatico(int IdLimiteNormativo, GravitaRilievo Gravita, string Regola, string Motivo);

/// <summary>
/// Controlli automatici sulle schede validate (18/10/2026): funzioni pure, senza database né AI. Segnalano i casi da
/// riguardare; la decisione resta a chi valida.
/// </summary>
public static class ControlliSchede
{
    public static List<RilievoAutomatico> Esegui(IReadOnlyList<SchedaDaControllare> schede)
    {
        var r = new List<RilievoAutomatico>();
        foreach (var x in schede)
        {
            var s = x.Scheda;
            // 1. Citazione e valore
            if (string.IsNullOrWhiteSpace(s.Citazione))
                r.Add(new(s.IdLimiteNormativo, GravitaRilievo.Dubbio, "citazione-mancante", "La scheda non ha la citazione della norma: il valore non è verificabile sul testo."));
            else if (MotivoValoreNonInCitazione(s) is string m)
                r.Add(new(s.IdLimiteNormativo, GravitaRilievo.Dubbio, "valore-citazione", m));

            // 2. Parametri ArcLab
            if (x.ParametriArcLab.Count == 0)
                r.Add(new(s.IdLimiteNormativo, GravitaRilievo.Avviso, "senza-arclab", "Nessun parametro ArcLab abbinato: la scheda non potrà segnalare nulla."));
            foreach (var p in x.ParametriArcLab)
            {
                if (!NomiSimili(s.Parametro, p.Nome))
                    r.Add(new(s.IdLimiteNormativo, GravitaRilievo.Dubbio, "nome-arclab",
                        $"Parametro ArcLab «{p.Nome}» poco simile a «{s.Parametro}»: controllare l'abbinamento."));
                if (s.Operatore != OperatoreLimite.AssenteIn && !string.IsNullOrWhiteSpace(s.Unita) && p.Unita.Count > 0 && p.Fattore is null
                    && !p.Unita.Any(u => UnitaCompatibili(s.Unita, u)))
                    r.Add(new(s.IdLimiteNormativo, GravitaRilievo.Avviso, "unita",
                        $"Unità della scheda «{s.Unita}» diversa da quella di ArcLab per «{p.Nome}» ({string.Join(", ", p.Unita)}) e nessun fattore di conversione."));
            }

            // 3. Tipo di limite e famiglia
            if (s.TipoLimite == TipoLimiteNormativo.Indicatore && s.Famiglia != FamigliaNormativa.AcqueConsumoUmano && s.Famiglia != FamigliaNormativa.Piscine)
                r.Add(new(s.IdLimiteNormativo, GravitaRilievo.Avviso, "tipo-famiglia", "«Parametro indicatore» fuori dalle acque: controllare il tipo di limite."));
            if (s.TipoLimite == TipoLimiteNormativo.IgieneProcesso && s.Famiglia is not (FamigliaNormativa.Alimenti or FamigliaNormativa.Molluschi))
                r.Add(new(s.IdLimiteNormativo, GravitaRilievo.Avviso, "tipo-famiglia", "«Igiene di processo» fuori dagli alimenti: controllare il tipo di limite."));
            if (s.Famiglia == FamigliaNormativa.Legionella && s.TipoLimite == TipoLimiteNormativo.Sicurezza)
                r.Add(new(s.IdLimiteNormativo, GravitaRilievo.Avviso, "tipo-famiglia", "Legionella come «Limite di sicurezza»: le linee guida danno soglie di intervento (Linea guida)."));
        }

        // 4. Contraddizioni: stesso parametro, famiglia e categoria con valori diversi (le linee guida a più soglie no)
        foreach (var g in schede.Select(x => x.Scheda)
                     .Where(s => s.TipoLimite != TipoLimiteNormativo.LineaGuida)
                     .GroupBy(s => (s.Famiglia, P: Chiave(s.Parametro), M: Chiave(s.MatriceCategoria), C: Chiave(s.Condizioni), s.RegolaValutazione)))
        {
            var valori = g.Select(s => (s.Operatore, s.Valore, U: Chiave(s.Unita))).Distinct().ToList();
            if (valori.Count < 2) continue;
            var codici = string.Join(", ", g.Select(s => s.CodiceScheda).Distinct());
            foreach (var s in g)
                r.Add(new(s.IdLimiteNormativo, GravitaRilievo.Dubbio, "contraddizione",
                    $"Stesso parametro e categoria con limiti diversi ({codici}): {string.Join(" / ", g.Select(RegoleLimiti.Descrivi).Distinct())}."));
        }
        return r;
    }

    private static string Chiave(string? s) => RegoleLimiti.Compatta(RegoleLimiti.NormalizzaTesto(s));

    private static readonly Regex NumeroRx = new(@"\d{1,3}(?:[.\s]\d{3})+(?:,\d+)?|\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    /// <summary>Numeri scritti nel testo, letti sia all'italiana (1.000,5) sia all'inglese (1000.5).</summary>
    public static HashSet<decimal> NumeriInTesto(string? testo)
    {
        var l = new HashSet<decimal>();
        if (string.IsNullOrEmpty(testo)) return l;
        foreach (Match m in NumeroRx.Matches(testo.Replace(' ', ' ')))
        {
            var t = m.Value;
            var it = t.Replace(" ", "").Replace(".", "").Replace(',', '.');
            if (decimal.TryParse(it, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)) l.Add(a);
            var en = t.Replace(" ", "").Replace(",", "");
            if (decimal.TryParse(en, NumberStyles.Number, CultureInfo.InvariantCulture, out var b)) l.Add(b);
            // «10 000» letto anche come due numeri separati
            foreach (var parte in t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (decimal.TryParse(parte.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var c)) l.Add(c);
        }
        // potenze: «10^3», «10³»
        foreach (Match m in Regex.Matches(testo, @"10\s*(?:\^|E)\s*(\d)|10([²³⁴⁵⁶])"))
        {
            var e = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
                : "²³⁴⁵⁶".IndexOf(m.Groups[2].Value[0]) + 2;
            l.Add((decimal)Math.Pow(10, e));
        }
        return l;
    }

    /// <summary>Il valore (o m/M del piano) compare nella citazione? Null se sì.</summary>
    public static string? MotivoValoreNonInCitazione(LimiteNormativo s)
    {
        var testo = s.Citazione ?? "";
        if (s.Operatore == OperatoreLimite.AssenteIn)
        {
            var n = RegoleLimiti.NormalizzaTesto(testo);
            return n.Contains("assen") || n.Contains("non rilevabil") || n.Contains("non rilevat")
                ? null : "Limite «assente in» ma nella citazione non c'è «assente» o «non rilevabile».";
        }
        var numeri = NumeriInTesto(testo);
        var attesi = new[] { s.Valore, s.ValoreMax, s.M, s.MMaiuscola }.Where(v => v is not null).Select(v => v!.Value).ToList();
        if (attesi.Count == 0) return null;
        var mancanti = attesi.Where(v => !numeri.Contains(v)).ToList();
        return mancanti.Count == 0 ? null
            : $"Nella citazione non compare il valore {string.Join(", ", mancanti.Select(v => v.ToString("#,##0.######", CultureInfo.GetCultureInfo("it-IT"))))}.";
    }

    private static readonly HashSet<string> ParoleVuote = ["spp", "totali", "totale", "presunti", "presunta", "conta", "conteggio", "delle", "della", "degli",
        "del", "dei", "nel", "nei", "come", "espresso", "espressi", "ricerca", "determinazione"];

    private static IEnumerable<string> Parole(string? s) =>
        RegoleLimiti.NormalizzaTesto(s).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => new string(p.Where(char.IsLetterOrDigit).ToArray()))
            .Where(p => p.Length >= 3 && !ParoleVuote.Contains(p));

    /// <summary>I due nomi hanno almeno una parola significativa in comune (o uno contiene l'altro)?</summary>
    public static bool NomiSimili(string? a, string? b)
    {
        var ca = RegoleLimiti.Compatta(RegoleLimiti.NormalizzaTesto(a)); var cb = RegoleLimiti.Compatta(RegoleLimiti.NormalizzaTesto(b));
        if (ca.Length == 0 || cb.Length == 0) return true;
        if (ca.Contains(cb) || cb.Contains(ca)) return true;
        var pa = Parole(a).ToHashSet();
        return Parole(b).Any(p => pa.Contains(p) || pa.Any(q => q.Length >= 5 && p.Length >= 5 && (q.StartsWith(p[..5]) || p.StartsWith(q[..5]))));
    }

    /// <summary>Forma confrontabile di un'unità: «μg/L» = «ug/l», «UFC/100 ml» = «cfu/100ml».</summary>
    public static string NormalizzaUnita(string? u)
    {
        // 20/10/2026: la specificazione della matrice dopo l'unità non cambia l'unità
        // («MPN/100 g di carne e liquido intervalvare» = «MPN/100g»).
        var t = Regex.Replace(RegoleLimiti.NormalizzaTesto(u), @"\s+(di|della|del|dei)\s+(carne|polpa|parte edibile|tessuto|prodotto)\b.*$", "");
        var n = t.Replace("μ", "u").Replace("µ", "u").Replace("mcg", "ug").Replace("cfu", "ufc")
            .Replace("npp", "mpn").Replace(" ", "").Replace("litro", "l").Replace("litri", "l")
            // conteggi: «numero/100 ml», UFC e MPN sono confrontabili come conteggio per volume o massa
            .Replace("numero", "ufc").Replace("n.", "ufc").Replace("mpn", "ufc");
        return n;
    }

    public static bool UnitaCompatibili(string? scheda, string? arclab) =>
        string.IsNullOrWhiteSpace(arclab) || NormalizzaUnita(scheda) == NormalizzaUnita(arclab);
}
