using System.Globalization;
using System.Text.RegularExpressions;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Core.Limiti;

/// <summary>Colore delle etichette in «Da Validare», dal meno al più grave.</summary>
public enum ColoreRilievo { Verde = 0, Grigio = 1, Arancione = 2, Rosso = 3, Viola = 4 }

/// <summary>Un rilievo su un campione. Regola + IdParametro/CodiceScheda servono a «Visto» ed eccezioni (passo 3).</summary>
public record RilievoCampione(ColoreRilievo Colore, string Regola, string Testo, int? IdProva, int? IdParametro, string? CodiceScheda);

public record EsitoCampione(ColoreRilievo Colore, IReadOnlyList<RilievoCampione> Rilievi)
{
    /// <summary>
    /// 26/10/2026: almeno un parametro è stato confrontato con una scheda limite. Verde senza confronto = «Nessun limite
    /// di legge» (es. superfici, parametri senza criterio), da non confondere con «Nei limiti».
    /// </summary>
    public bool Confrontato { get; init; } = true;
}

/// <summary>Categorie confermate di una matrice ArcLab degli Alimenti.</summary>
public record CategorieMatrice(bool Nessuna, IReadOnlySet<string> Chiavi);

/// <summary>
/// Motore delle segnalazioni di «Da Validare» (fase 1b, 18/10/2026; definizione claude/definizione-limiti-fase1b.md).
/// Solo regole: confronto del valore puntuale con le schede VALIDATE e controlli tecnici. Nessun accesso a database.
/// </summary>
public static class MotoreSegnalazioni
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string N(decimal v) => v.ToString("#,##0.######", It);
    private static string N(double v) => ((decimal)v).ToString("#,##0.######", It);

    public static Dictionary<int, EsitoCampione> Calcola(
        IReadOnlyList<ProvaPerSegnalazioni> prove,
        IReadOnlyList<LimiteNormativo> schedeValidate,
        IReadOnlyDictionary<int, CategorieMatrice> categorieMatrici,
        IReadOnlyList<TempoMassimoProva> tempiMassimi,
        Func<int, RilievoCampione, bool>? daIgnorare = null)
    {
        // schede per parametro ArcLab
        var perParametro = PerParametro(schedeValidate);

        var esiti = new Dictionary<int, EsitoCampione>();
        foreach (var campione in prove.GroupBy(p => p.IdCampione))
        {
            var r = new List<RilievoCampione>();
            var confrontato = false;
            var valide = campione.Where(p => !p.Esclusa).ToList();
            var primo = campione.First();
            var famiglia = RegoleLimiti.FamigliaDaMatrice(primo.Matrice);

            // ── controlli sul campione ──
            if (primo.DataPrelievo is DateTime dp && primo.DataRicezione is DateTime dr && dp.Date > dr.Date)
                r.Add(new(ColoreRilievo.Viola, "prelievo-dopo-ricezione", $"Data di prelievo ({dp:dd/MM/yyyy}) successiva alla ricezione ({dr:dd/MM/yyyy}).", null, null, null));
            // Ripetizioni ammesse (20/10/2026): 5 unità campionarie (n = 5), 9 per l'istamina (n = 9). Altro numero → viola.
            foreach (var g in valide.Where(p => p.IdParametro is int).GroupBy(p => p.IdParametro!.Value).Where(g => g.Count() > 1))
            {
                var attese = RipetizioniAttese(g.First().NomeParametro);
                if (g.Count() != attese)
                    r.Add(new(ColoreRilievo.Viola, "parametro-ripetuto",
                        $"Parametro ripetuto {g.Count()} volte (ammesso {attese}, una per unità campionaria): {g.First().NomeParametro}.", null, g.Key, null));
            }
            if (famiglia == FamigliaNormativa.AcqueConsumoUmano) ControllaEColi(valide, r);

            foreach (var p in valide)
            {
                // ── controlli tecnici sulla prova ──
                if (p.ValoreCalcolato is null)
                {
                    r.Add(new(ColoreRilievo.Grigio, "risultato-mancante", $"Risultato mancante: {p.NomeParametro}.", p.IdProva, p.IdParametro, null));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(p.UnitaMisura) && !SenzaUnita(p, perParametro))
                    r.Add(new(ColoreRilievo.Viola, "unita-mancante", $"Unità di misura mancante: {p.NomeParametro}.", p.IdProva, p.IdParametro, null));
                if (!p.RegoleArrotondamento)
                    r.Add(new(ColoreRilievo.Viola, "arrotondamento", $"Metodo senza regole di arrotondamento (decimali/arrotonda): {p.NomeParametro}.", p.IdProva, p.IdParametro, null));
                if (p.IdLabEsterno is int && string.IsNullOrWhiteSpace(p.FraseLabEsterno))
                    r.Add(new(ColoreRilievo.Viola, "subappalto-frase", $"Prova in subappalto ({p.LabEsterno}) senza la frase del laboratorio esterno: {p.NomeParametro}.", p.IdProva, p.IdParametro, null));
                // solo prove eseguite internamente: per quelle in subappalto i tempi dipendono dal laboratorio esterno (19/10/2026)
                if (p.IdLabEsterno is null && p.DataInizioProva is DateTime di && p.DataRicezione is DateTime ric && OreMassime(tempiMassimi, famiglia, p.IdParametro) is int ore)
                {
                    // ricezione senza ora: si conta dalla fine del giorno, per non dare falsi allarmi.
                    // Ritardo programmato (Campione.RitardoAnalisi, es. shelf life): si conta dalla data prevista di inizio.
                    var ritardo = p.RitardoAnalisi is > 0 ? p.RitardoAnalisi.Value : 0;
                    var da = (ric.TimeOfDay == TimeSpan.Zero ? ric.Date.AddDays(1) : ric).AddDays(ritardo);
                    var trascorse = (di - da).TotalHours;
                    if (trascorse > ore)
                        r.Add(new(ColoreRilievo.Viola, "tempi-inizio", ritardo > 0
                            ? $"{p.NomeParametro}: prova iniziata il {di:dd/MM/yyyy HH:mm}, oltre {ore} ore dall'inizio previsto ({ric.Date.AddDays(ritardo):dd/MM/yyyy}: ricezione + {ritardo} giorni di ritardo programmato)."
                            : $"{p.NomeParametro}: prova iniziata il {di:dd/MM/yyyy HH:mm}, oltre {ore} ore dalla ricezione.", p.IdProva, p.IdParametro, null));
                }

                // ── limiti ──
                var (applicabili, senzaCategoria) = Applicabili(p, famiglia, perParametro, categorieMatrici);
                if (senzaCategoria)
                {
                    r.Add(new(ColoreRilievo.Grigio, "matrice-senza-categoria", $"Matrice «{p.Matrice}» senza categoria: abbinarla in Limiti normativi → Categorie e matrici ({p.NomeParametro}).", p.IdProva, p.IdParametro, null));
                    continue;
                }
                if (applicabili.Count == 0) continue;
                if (ConfrontaLimiti(p, applicabili, r)) confrontato = true;
            }

            var finali = daIgnorare is null ? r : r.Where(x => !daIgnorare(campione.Key, x)).ToList();
            var colore = finali.Count == 0 ? ColoreRilievo.Verde : finali.Max(x => x.Colore);
            esiti[campione.Key] = new EsitoCampione(colore, finali.OrderByDescending(x => x.Colore).ToList()) { Confrontato = confrontato };
        }
        return esiti;
    }

    /// <summary>Schede per parametro ArcLab (abbinamenti attivi).</summary>
    private static Dictionary<int, List<(LimiteNormativo S, AbbinamentoLimite A)>> PerParametro(IReadOnlyList<LimiteNormativo> schede)
    {
        var perParametro = new Dictionary<int, List<(LimiteNormativo S, AbbinamentoLimite A)>>();
        foreach (var s in schede)
            foreach (var a in s.Abbinamenti.Where(a => a.Attivo))
            {
                if (!perParametro.TryGetValue(a.IdParametroArcLab, out var l)) perParametro[a.IdParametroArcLab] = l = [];
                l.Add((s, a));
            }
        return perParametro;
    }

    /// <summary>
    /// Schede che si applicano alla prova: stesso parametro, stessa famiglia, matrice abbinata, in vigore alla data di
    /// prelievo (o ricezione) e, dove serve, della categoria della matrice. <c>SenzaCategoria</c>: matrice non ancora abbinata.
    /// </summary>
    private static (List<(LimiteNormativo S, AbbinamentoLimite A)> Applicabili, bool SenzaCategoria) Applicabili(
        ProvaPerSegnalazioni p, FamigliaNormativa famiglia,
        Dictionary<int, List<(LimiteNormativo S, AbbinamentoLimite A)>> perParametro,
        IReadOnlyDictionary<int, CategorieMatrice> categorieMatrici)
    {
        if (p.IdParametro is not int idPar || !perParametro.TryGetValue(idPar, out var candidati)) return ([], false);
        var data = p.DataPrelievo ?? p.DataRicezione;
        var applicabili = candidati.Where(c => c.S.Famiglia == famiglia
                && (c.A.IdMatriceArcLab is null || c.A.IdMatriceArcLab == p.IdMatrice)
                && (c.S.ValidoDal is null || data is null || c.S.ValidoDal <= data)
                && (c.S.ValidoAl is null || data is null || c.S.ValidoAl >= data)).ToList();
        if (applicabili.Count == 0) return ([], false);
        if (!RegoleLimiti.CategoriaDaFamiglia(famiglia))
        {
            if (p.IdMatrice is not int im || !categorieMatrici.TryGetValue(im, out var cat)) return ([], true);
            if (cat.Nessuna) return ([], false);
            applicabili = applicabili.Where(c => cat.Chiavi.Contains(RegoleLimiti.ChiaveCategoria(c.S.MatriceCategoria))).ToList();
        }
        return (applicabili, false);
    }

    /// <summary>
    /// 05/11/2026: limiti di legge validati che si applicano a ogni prova (chiave IdProva), descritti per il prospetto
    /// dell'analisi AI: «≤ 0,4 mg/l (PIS-002, Accordo Stato-Regioni 16/01/2003, All. 1)». Stessa regola del controllo automatico.
    /// </summary>
    public static Dictionary<int, IReadOnlyList<string>> LimitiPerProva(
        IReadOnlyList<ProvaPerSegnalazioni> prove, IReadOnlyList<LimiteNormativo> schedeValidate,
        IReadOnlyDictionary<int, CategorieMatrice> categorieMatrici)
    {
        var perParametro = PerParametro(schedeValidate);
        var esito = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var p in prove.Where(p => !p.Esclusa))
        {
            var famiglia = RegoleLimiti.FamigliaDaMatrice(p.Matrice);
            var (applicabili, _) = Applicabili(p, famiglia, perParametro, categorieMatrici);
            if (applicabili.Count == 0) continue;
            esito[p.IdProva] = applicabili
                .Select(c => $"{RegoleLimiti.Descrivi(c.S)} ({Rif(c.S)}{(string.IsNullOrWhiteSpace(c.S.Condizioni) ? "" : "; " + c.S.Condizioni)})")
                .Distinct().ToList();
        }
        return esito;
    }

    /// <summary>Ore massime ricezione → inizio prova: prima la riga del parametro, poi quella della famiglia; null = nessun controllo.</summary>
    public static int? OreMassime(IEnumerable<TempoMassimoProva> tempi, FamigliaNormativa famiglia, int? idParametro)
    {
        var attivi = tempi.Where(t => t.Attivo && t.Famiglia == famiglia && t.Ore > 0).ToList();
        return (idParametro is int id ? attivi.FirstOrDefault(t => t.IdParametroArcLab == id) : null)?.Ore
            ?? attivi.FirstOrDefault(t => t.IdParametroArcLab is null)?.Ore;
    }

    private enum Esito { Ok, Superato, TraMeM, LoqSopra, NonConfrontabile, NonApplicabile }

    private static (Esito E, string? Motivo) Valuta(ProvaPerSegnalazioni p, LimiteNormativo s, AbbinamentoLimite a)
    {
        var v = (decimal)p.ValoreCalcolato!.Value * (a.FattoreUnita ?? 1m);
        if (s.Operatore == OperatoreLimite.AssenteIn)
        {
            var qs = NumeroIn(s.Unita); var qp = NumeroIn(p.UnitaMisura);
            if (qs is not null && qp is not null && qs != qp)
                return (Esito.NonConfrontabile, $"quantità diversa (norma {s.Unita}, prova {p.UnitaMisura})");
            // «< x» (non rilevato) = assente; altrimenti presenza = valore ≥ 1
            return (!p.MinoreDi && v >= 1m ? Esito.Superato : Esito.Ok, null);
        }
        // 26/10/2026 (Mirko Maffei): negli alimenti e nei molluschi MPN e ufc vengono da metodi e norme diverse e non si
        // confrontano mai (l'MPN vale solo per i molluschi bivalvi vivi). Nelle acque restano confrontabili (conteggi).
        if (s.Famiglia is FamigliaNormativa.Alimenti or FamigliaNormativa.Molluschi && IsMpn(s.Unita) != IsMpn(p.UnitaMisura)
            && !string.IsNullOrWhiteSpace(s.Unita) && !string.IsNullOrWhiteSpace(p.UnitaMisura))
            return (Esito.NonApplicabile, null);   // la scheda non si applica: nessun rilievo
        if (!string.IsNullOrWhiteSpace(s.Unita) && !string.IsNullOrWhiteSpace(p.UnitaMisura) && a.FattoreUnita is null
            && !ControlliSchede.UnitaCompatibili(s.Unita, p.UnitaMisura))
            return (Esito.NonConfrontabile, $"unità diversa (norma {s.Unita}, prova {p.UnitaMisura})");
        var lim = s.Valore ?? 0m;
        if (p.MinoreDi)   // «< x»: il valore è sotto x (x = limite di quantificazione)
        {
            // 05/11/2026 (Bertozzi, 9713/01 Enterobatteriacee in 5 UC): nei piani a 3 classi (n, c, m, M) un «< x» con x
            // tra m e M non è un errore da correggere: il valore è comunque sotto M. Viola solo se x supera M.
            if (s.RegolaValutazione == RegolaValutazioneLimite.PianoCampionamento && s.MMaiuscola is decimal mmLoq
                && s.Operatore == OperatoreLimite.ValoreMassimo)
                return (v > mmLoq ? Esito.LoqSopra : Esito.Ok, null);
            var sopra = s.Operatore switch
            {
                OperatoreLimite.ValoreMassimo => v > lim,
                OperatoreLimite.MinoreDi => v > lim,
                _ => false
            };
            return (sopra ? Esito.LoqSopra : Esito.Ok, null);
        }
        if (s.RegolaValutazione == RegolaValutazioneLimite.PianoCampionamento && s.MMaiuscola is decimal mm)
        {
            var m = s.M ?? lim;
            return (v > mm ? Esito.Superato : v > m ? Esito.TraMeM : Esito.Ok, null);
        }
        var superato = s.Operatore switch
        {
            OperatoreLimite.ValoreMassimo => v > lim,
            OperatoreLimite.MinoreDi => v >= lim,
            OperatoreLimite.ValoreMinimo => v < lim && !p.MaggioreDi,
            OperatoreLimite.Intervallo => v < lim || (s.ValoreMax is decimal mx && v > mx),
            _ => false
        };
        return (superato ? Esito.Superato : Esito.Ok, null);
    }

    private static string Rif(LimiteNormativo s) => string.IsNullOrWhiteSpace(s.Riferimento) ? s.CodiceScheda : $"{s.CodiceScheda}, {s.Riferimento}";

    private static string Valore(ProvaPerSegnalazioni p) =>
        (p.MinoreDi ? "< " : p.MaggioreDi ? "> " : "") + N(p.ValoreCalcolato!.Value) + (string.IsNullOrWhiteSpace(p.UnitaMisura) ? "" : " " + p.UnitaMisura);

    /// <returns>True se almeno una scheda è stata davvero confrontata (non «non confrontabile»).</returns>
    private static bool ConfrontaLimiti(ProvaPerSegnalazioni p, List<(LimiteNormativo S, AbbinamentoLimite A)> applicabili, List<RilievoCampione> r)
    {
        var valutate = applicabili.Select(c => (c.S, c.A, V: Valuta(p, c.S, c.A))).ToList();
        var confrontato = valutate.Any(x => x.V.E is not (Esito.NonConfrontabile or Esito.NonApplicabile));

        foreach (var x in valutate.Where(x => x.V.E == Esito.NonConfrontabile).GroupBy(x => x.V.Motivo).Select(g => g.First()))
            r.Add(new(ColoreRilievo.Grigio, "non-confrontabile", $"{p.NomeParametro}: {x.V.Motivo} — confronto con {x.S.CodiceScheda} non eseguito.", p.IdProva, p.IdParametro, x.S.CodiceScheda));
        foreach (var x in valutate.Where(x => x.V.E == Esito.LoqSopra))
            r.Add(new(ColoreRilievo.Viola, "loq-sopra-limite", $"{p.NomeParametro}: risultato {Valore(p)} con limite di quantificazione sopra il limite {RegoleLimiti.Descrivi(x.S)} ({Rif(x.S)}).", p.IdProva, p.IdParametro, x.S.CodiceScheda));

        // linee guida a più soglie (Legionella): solo la soglia più alta superata
        var lg = valutate.Where(x => x.S.TipoLimite == TipoLimiteNormativo.LineaGuida && x.V.E == Esito.Superato).OrderByDescending(x => x.S.Valore).FirstOrDefault();
        if (lg.S is not null)
            r.Add(new(ColoreRilievo.Arancione, "linea-guida", $"{p.NomeParametro} {Valore(p)}: supera {RegoleLimiti.Descrivi(lg.S)}"
                + (string.IsNullOrWhiteSpace(lg.S.TestoSegnalazione) ? "" : $" — {lg.S.TestoSegnalazione}") + $" ({Rif(lg.S)}).", p.IdProva, p.IdParametro, lg.S.CodiceScheda));

        // limiti di sicurezza, con la regola della fase (decisione A del 18/10/2026)
        var sic = valutate.Where(x => x.S.TipoLimite == TipoLimiteNormativo.Sicurezza && x.V.E is Esito.Ok or Esito.Superato or Esito.TraMeM).ToList();
        var superati = sic.Where(x => x.V.E == Esito.Superato).ToList();
        if (superati.Count > 0)
        {
            var nonSuperati = sic.Where(x => x.V.E != Esito.Superato).ToList();
            var condSup = superati.Select(x => RegoleLimiti.Compatta(RegoleLimiti.NormalizzaTesto(x.S.Condizioni))).ToHashSet();
            var dipendeDallaFase = nonSuperati.Any(x => !condSup.Contains(RegoleLimiti.Compatta(RegoleLimiti.NormalizzaTesto(x.S.Condizioni))));
            var peggiore = superati.OrderByDescending(x => x.S.Valore ?? decimal.MinValue).First();
            if (dipendeDallaFase)
                r.Add(new(ColoreRilievo.Arancione, "fase", $"{p.NomeParametro} {Valore(p)}: supera {RegoleLimiti.Descrivi(peggiore.S)} ({Rif(peggiore.S)}"
                    + (string.IsNullOrWhiteSpace(peggiore.S.Condizioni) ? "" : $", {peggiore.S.Condizioni}") + ") ma non "
                    + string.Join(", ", nonSuperati.Select(x => RegoleLimiti.Descrivi(x.S)).Distinct()) + " — dipende dalla fase: verificare se il campione è alla produzione o in commercio.",
                    p.IdProva, p.IdParametro, peggiore.S.CodiceScheda));
            else
                r.Add(new(ColoreRilievo.Rosso, "limite-sicurezza", $"{p.NomeParametro} {Valore(p)}: supera il limite di legge {RegoleLimiti.Descrivi(peggiore.S)} ({Rif(peggiore.S)}).",
                    p.IdProva, p.IdParametro, peggiore.S.CodiceScheda));
        }
        else
        {
            // tra m e M (piano di campionamento) e ≥ 80% di un limite di sicurezza
            foreach (var x in sic.Where(x => x.V.E == Esito.TraMeM))
                r.Add(new(ColoreRilievo.Arancione, "tra-m-e-M", $"{p.NomeParametro} {Valore(p)}: tra m e M di {RegoleLimiti.Descrivi(x.S)} ({Rif(x.S)}): accettabile solo se entro c unità del lotto.", p.IdProva, p.IdParametro, x.S.CodiceScheda));
            if (!p.MinoreDi && sic.All(x => x.V.E == Esito.Ok))
                foreach (var x in sic.Where(x => x.S.Operatore is OperatoreLimite.ValoreMassimo or OperatoreLimite.MinoreDi && x.S.Valore is > 0m
                             && x.S.RegolaValutazione == RegolaValutazioneLimite.Puntuale
                             && (decimal)p.ValoreCalcolato!.Value * (x.A.FattoreUnita ?? 1m) >= 0.8m * x.S.Valore!.Value).Take(1))
                    r.Add(new(ColoreRilievo.Arancione, "vicino-limite", $"{p.NomeParametro} {Valore(p)}: oltre l'80% del limite di legge {RegoleLimiti.Descrivi(x.S)} ({Rif(x.S)}).", p.IdProva, p.IdParametro, x.S.CodiceScheda));
        }

        // parametri indicatori (acque D.Lgs. 18/2023, piscine): superare il valore di parametro è una non conformità → rosso (20/10/2026)
        foreach (var x in valutate.Where(x => x.S.TipoLimite == TipoLimiteNormativo.Indicatore && x.V.E is Esito.Superato)
                     .GroupBy(x => x.S.CodiceScheda).Select(g => g.First()))
            r.Add(new(ColoreRilievo.Rosso, "indicatore", $"{p.NomeParametro} {Valore(p)}: non conforme al valore di parametro {RegoleLimiti.Descrivi(x.S)} — parametro indicatore ({Rif(x.S)})"
                + (string.IsNullOrWhiteSpace(x.S.TestoSegnalazione) ? "." : $": {x.S.TestoSegnalazione}"), p.IdProva, p.IdParametro, x.S.CodiceScheda));

        // igiene di processo e riferimenti: arancione
        foreach (var x in valutate.Where(x => x.S.TipoLimite is TipoLimiteNormativo.IgieneProcesso or TipoLimiteNormativo.Riferimento
                     && x.V.E is Esito.Superato or Esito.TraMeM).GroupBy(x => x.S.CodiceScheda).Select(g => g.First()))
            r.Add(new(ColoreRilievo.Arancione, "da-segnalare", $"{p.NomeParametro} {Valore(p)}: "
                + (x.V.E == Esito.TraMeM ? "tra m e M di " : "supera ") + $"{RegoleLimiti.Descrivi(x.S)} — {RegoleLimiti.NomeTipo(x.S.TipoLimite).ToLowerInvariant()} ({Rif(x.S)})"
                + (string.IsNullOrWhiteSpace(x.S.TestoSegnalazione) ? "." : $": {x.S.TestoSegnalazione}"), p.IdProva, p.IdParametro, x.S.CodiceScheda));
        return confrontato;
    }

    /// <summary>Quante volte può comparire lo stesso parametro: 9 per l'istamina, 5 per gli altri (unità campionarie).</summary>
    public static int RipetizioniAttese(string? nomeParametro) =>
        RegoleLimiti.NormalizzaTesto(nomeParametro).Contains("istamin") ? 9 : 5;

    // 26/10/2026: anche l'attività dell'acqua (aw) è adimensionale.
    private static readonly Regex AdimensionaleRx = new(@"\bph\b|\baw\b|attivita dell'?\s?acqua|odore|sapore|aspetto|colore|presenza|assenza", RegexOptions.Compiled);

    private static bool IsMpn(string? unita) => RegoleLimiti.NormalizzaTesto(unita).Contains("mpn") || RegoleLimiti.NormalizzaTesto(unita).Contains("npp");

    /// <summary>Parametri senza unità per natura: pH, caratteri organolettici, o schede validate senza unità.</summary>
    private static bool SenzaUnita(ProvaPerSegnalazioni p, Dictionary<int, List<(LimiteNormativo S, AbbinamentoLimite A)>> perParametro) =>
        AdimensionaleRx.IsMatch(RegoleLimiti.NormalizzaTesto(p.NomeParametro))
        || (p.IdParametro is int id && perParametro.TryGetValue(id, out var l) && l.Any(x => string.IsNullOrWhiteSpace(x.S.Unita)));

    private static readonly Regex NumeroRx = new(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);
    private static decimal? NumeroIn(string? s)
    {
        var m = NumeroRx.Match(s ?? "");
        return m.Success && decimal.TryParse(m.Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static void ControllaEColi(List<ProvaPerSegnalazioni> prove, List<RilievoCampione> r)
    {
        static string Nome(ProvaPerSegnalazioni p) => RegoleLimiti.NormalizzaTesto(p.NomeParametro);
        var ecoli = prove.FirstOrDefault(p => Nome(p).Contains("coli") && !Nome(p).Contains("coliform") && p.ValoreCalcolato is not null);
        var coliformi = prove.FirstOrDefault(p => Nome(p).Contains("coliform") && !Nome(p).Contains("fecal") && p.ValoreCalcolato is not null);
        if (ecoli is null || coliformi is null) return;
        if (!ControlliSchede.UnitaCompatibili(ecoli.UnitaMisura, coliformi.UnitaMisura)) return;
        if (ecoli.ValoreCalcolato > coliformi.ValoreCalcolato && !coliformi.MaggioreDi)
            r.Add(new(ColoreRilievo.Viola, "ecoli-coliformi", $"E. coli ({Valore(ecoli)}) maggiore dei coliformi totali ({Valore(coliformi)}): incoerente.", ecoli.IdProva, ecoli.IdParametro, null));
    }
}
