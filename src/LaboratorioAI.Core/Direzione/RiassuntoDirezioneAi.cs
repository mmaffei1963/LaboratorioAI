using System.Globalization;
using System.Text;

namespace LaboratorioAI.Core.Direzione;

/// <summary>Fatturato di un cliente nell'anno di riferimento e in quello prima (elenchi per il commento AI).</summary>
public record ClienteConfronto(string Codice, string Nome, string? Provincia, decimal AnnoPrima, decimal AnnoRiferimento)
{
    public decimal Differenza => AnnoRiferimento - AnnoPrima;
}

/// <summary>Progressivo dell'anno in corso rispetto allo stesso periodo dell'anno prima.</summary>
public record ProgressivoAnno(int Anno, int UltimoMese, decimal Fatturato, decimal StessoPeriodoAnnoPrima);

/// <summary>Dati della pagina Andamento territorio già calcolati dal programma, da far commentare all'AI.</summary>
public record DatiCommentoTerritorio(
    string DescrizioneFiltri,
    string? AggiornamentoDati,
    IReadOnlyList<int> Anni,
    LivelloTerritorio Livello,
    IReadOnlyList<ZonaTerritorio> Zone,
    IReadOnlyList<ValoriZonaAnno> Totale,
    IReadOnlyList<GruppoConfronto> Pesaro,
    int AnnoAperturaPesaro,
    int AnnoInizioPesaro,
    int UltimoAnnoCompleto,
    int AnnoRiferimento,
    IReadOnlyList<ClienteConfronto> Clienti,
    ProgressivoAnno? Progressivo)
{
    /// <summary>Clienti acquisiti da un nostro cliente (codice → nome di chi li ha acquisiti): non sono persi (14/10/2026).</summary>
    public IReadOnlyDictionary<string, string>? Acquisiti { get; init; }
}

/// <summary>Indicatori di un'area (o di MAREA) per il commento AI della pagina Indicatori economici.</summary>
public record DatiCommentoIndicatori(
    string DescrizioneFiltri,
    string? AggiornamentoDati,
    string NomeArea,
    bool Area,
    IReadOnlyList<IndicatoriAnno> Anni,
    IReadOnlyList<(string Area, IndicatoriAnno Valori)> AltreAree,
    int AnnoInCorso,
    int? UltimoMeseAnnoInCorso);

/// <summary>
/// Commento AI del cruscotto Direzione (13/10/2026). Il programma calcola tutti i numeri; qui si prepara il testo che
/// l'AI commenta. Le regole per l'AI sono nelle istruzioni di sistema: commentare solo i numeri ricevuti, senza
/// inventarne. Funzioni pure, senza accesso ai dati.
/// </summary>
public static class RiassuntoDirezioneAi
{
    /// <summary>Quanti clienti per elenco (deciso da Mirko Maffei il 13/10/2026: i primi 30, con il nome).</summary>
    public const int ClientiPerElenco = 30;

    /// <summary>Zone riportate una per una; le altre sono sommate in «Altre zone».</summary>
    public const int ZoneMassime = 25;

    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public const string Sistema = """
        Sei l'analista economico di MARE.A srl (laboratorio di analisi accreditato ACCREDIA, sede a Cattolica RN, sede a Pesaro dal 2022;
        aree: laboratorio, sicurezza e HACCP, formazione, consulenza esterna, area sanitaria). Scrivi per i soci e la direzione.

        Regole, da rispettare sempre:
        1. Usa SOLO i numeri presenti nei dati che ricevi. Non inventare cifre, clienti, cause o fatti esterni.
        2. Puoi fare solo calcoli semplici e verificabili (differenze, percentuali) sui numeri ricevuti; quando lo fai, dillo.
        3. Se un'ipotesi non è dimostrata dai dati, presentala come domanda da approfondire, non come fatto.
        4. L'anno in corso è parziale: non confrontarlo con gli anni interi, usa il progressivo quando c'è.
        5. Il territorio è la sede del cliente in AZW, non il luogo dove si lavora. Il commerciale è quello attuale del cliente, senza storico.
        6. Niente giudizi personali: valuta solo risultati economici e numeri (anche quelli di un commerciale), mai il carattere o l'impegno delle persone.
        7. Scrivi in italiano, tono professionale e diretto, frasi brevi, importi arrotondati alle migliaia (es. 390 k€).

        Formato della risposta (Markdown semplice: titoli con ##, elenchi con -, grassetto con **):
        ## In sintesi
        (3-5 punti, i fatti più importanti)
        ## Dove cresciamo e dove perdiamo
        ## Clienti
        (concentrazione, nuovi, persi, chi cresce e chi cala, con i nomi)
        ## Sede di Pesaro
        (solo per la pagina territorio; confronta sempre con Rimini e Forlì-Cesena)
        ## Si ripaga?
        (solo per la pagina rendimento commerciali, al posto di «Sede di Pesaro»: costo, margine portato, contributo e cumulato per ciascuno)
        ## Da approfondire
        (3-6 domande concrete per la direzione o i commerciali)
        Massimo 600 parole. Nessuna premessa e nessuna chiusura di cortesia.
        """;

    // ── Andamento territorio ─────────────────────────────────────────
    public static string Territorio(DatiCommentoTerritorio d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PAGINA: Andamento territorio (fatturato per sede del cliente).");
        sb.AppendLine($"Filtri: {d.DescrizioneFiltri}");
        if (d.AggiornamentoDati is not null) sb.AppendLine($"Dati AZW: {d.AggiornamentoDati}");
        sb.AppendLine($"Anno di riferimento per i confronti (ultimo anno completo nel periodo): {d.AnnoRiferimento}.");
        if (d.Progressivo is { } p && d.Anni.Contains(p.Anno))
            sb.AppendLine($"Attenzione: il {p.Anno} è parziale (dati fino a {NomeMese(p.UltimoMese)}).");
        sb.AppendLine();

        sb.AppendLine("## Totale per anno (fatturato netto; clienti attivi / nuovi / persi; acquisiti da un nostro cliente, non persi)");
        foreach (var v in d.Totale) sb.AppendLine($"- {v.Anno}: {E(v.Fatturato)}; clienti {v.ClientiAttivi} / {v.ClientiNuovi} / {v.ClientiPersi}"
            + (v.ClientiAcquisiti > 0 ? $"; acquisiti {v.ClientiAcquisiti}" : ""));
        if (d.Progressivo is { } pr)
            sb.AppendLine($"- Progressivo {pr.Anno} (gennaio–{NomeMese(pr.UltimoMese)}): {E(pr.Fatturato)} contro {E(pr.StessoPeriodoAnnoPrima)} nello stesso periodo del {pr.Anno - 1} ({P(Var(pr.StessoPeriodoAnnoPrima, pr.Fatturato))}).");
        sb.AppendLine();

        var nomeLivello = d.Livello switch { LivelloTerritorio.Regione => "regione", LivelloTerritorio.Comune => "comune", _ => "provincia" };
        sb.AppendLine($"## Fatturato per {nomeLivello} ({string.Join(", ", d.Anni)}); var. = {d.AnnoRiferimento} su {d.AnnoRiferimento - 1}; clienti attivi nel {d.AnnoRiferimento}");
        foreach (var z in d.Zone.Take(ZoneMassime))
            sb.AppendLine(RigaZona(z.Etichetta + (z.Regione is { } r && d.Livello != LivelloTerritorio.Regione ? $" [{r}]" : ""), z.Anni, d.AnnoRiferimento));
        var altre = d.Zone.Skip(ZoneMassime).ToList();
        if (altre.Count > 0)
        {
            var somma = d.Anni.Select(a => new ValoriZonaAnno(a,
                altre.Sum(z => z.Anni.FirstOrDefault(v => v.Anno == a)?.Fatturato ?? 0m),
                altre.Sum(z => z.Anni.FirstOrDefault(v => v.Anno == a)?.ClientiAttivi ?? 0), 0, 0)).ToList();
            sb.AppendLine(RigaZona($"Altre {altre.Count} zone", somma, d.AnnoRiferimento));
        }
        sb.AppendLine();

        if (d.Pesaro.Count > 0)
        {
            sb.AppendLine($"## Sede di Pesaro (aperta nel {d.AnnoAperturaPesaro}); crescita media annua prima = {d.AnnoInizioPesaro}–{d.AnnoAperturaPesaro - 1}, dopo = {d.AnnoAperturaPesaro - 1}–{d.UltimoAnnoCompleto}");
            sb.AppendLine("(Il confronto ignora i filtri di regione e provincia; 2020–2021 risentono del Covid.)");
            foreach (var g in d.Pesaro)
                sb.AppendLine($"- {g.Nome}: " + string.Join("; ", g.Anni.Select(a => $"{a.Anno} {E(a.Fatturato)} ({a.ClientiAttivi} clienti)"))
                    + $" — crescita annua prima {P(g.CrescitaPrima)}, dopo {P(g.CrescitaDopo)}");
            sb.AppendLine();
        }

        AppendClienti(sb, d.Clienti, d.AnnoRiferimento, d.Acquisiti);
        return sb.ToString();
    }

    private static void AppendClienti(StringBuilder sb, IReadOnlyList<ClienteConfronto> clienti, int anno, IReadOnlyDictionary<string, string>? acquisiti = null)
    {
        bool Acquisito(ClienteConfronto c) => acquisiti is not null && acquisiti.ContainsKey(c.Codice);
        var conFatturato = clienti.Where(c => c.AnnoRiferimento > 0).OrderByDescending(c => c.AnnoRiferimento).ToList();
        var totale = conFatturato.Sum(c => c.AnnoRiferimento);
        sb.AppendLine($"## Concentrazione {anno}");
        if (totale > 0)
        {
            sb.AppendLine($"- Clienti con fatturato positivo: {conFatturato.Count}; fatturato dei clienti {E(totale)}");
            foreach (var n in new[] { 1, 10, ClientiPerElenco })
                if (conFatturato.Count >= n)
                    sb.AppendLine($"- Primi {n}: {E(conFatturato.Take(n).Sum(c => c.AnnoRiferimento))} = {P(conFatturato.Take(n).Sum(c => c.AnnoRiferimento) / totale, segno: false)} del totale");
        }
        else sb.AppendLine("- Nessun cliente con fatturato positivo.");
        sb.AppendLine();

        Elenco(sb, $"Primi {ClientiPerElenco} clienti per fatturato {anno}", conFatturato, anno);
        Elenco(sb, $"Maggiori crescite {anno} su {anno - 1}",
            clienti.Where(c => c.Differenza > 0 && c.AnnoPrima > 0).OrderByDescending(c => c.Differenza).ToList(), anno);
        Elenco(sb, $"Maggiori cali {anno} su {anno - 1} (clienti ancora attivi)",
            clienti.Where(c => c.Differenza < 0 && c.AnnoRiferimento > 0).OrderBy(c => c.Differenza).ToList(), anno);
        Elenco(sb, $"Clienti nuovi nel {anno} (nessun fatturato nel {anno - 1})",
            clienti.Where(c => c.AnnoRiferimento > 0 && c.AnnoPrima <= 0).OrderByDescending(c => c.AnnoRiferimento).ToList(), anno, conteggio: true);
        Elenco(sb, $"Clienti persi nel {anno} (fatturato nel {anno - 1}, nessuno nel {anno})",
            clienti.Where(c => c.AnnoPrima > 0 && c.AnnoRiferimento <= 0 && !Acquisito(c)).OrderByDescending(c => c.AnnoPrima).ToList(), anno, conteggio: true);
        var acq = clienti.Where(c => c.AnnoPrima > 0 && c.AnnoRiferimento <= 0 && Acquisito(c)).OrderByDescending(c => c.AnnoPrima).ToList();
        if (acq.Count > 0)
        {
            sb.AppendLine($"## Clienti usciti nel {anno} perché acquisiti da un nostro cliente (non persi: il lavoro continua con chi li ha acquisiti) — in tutto {acq.Count}");
            foreach (var c in acq.Take(ClientiPerElenco))
                sb.AppendLine($"- {c.Nome} ({c.Provincia ?? "prov. n.d."}): {anno - 1} {E(c.AnnoPrima)}, acquisito da {acquisiti![c.Codice]}");
            sb.AppendLine();
        }
    }

    private static void Elenco(StringBuilder sb, string titolo, List<ClienteConfronto> lista, int anno, bool conteggio = false)
    {
        sb.AppendLine($"## {titolo}" + (conteggio ? $" — in tutto {lista.Count}, fatturato {E(lista.Sum(c => c.AnnoRiferimento))} (anno prima {E(lista.Sum(c => c.AnnoPrima))})" : ""));
        if (lista.Count == 0) { sb.AppendLine("- nessuno"); sb.AppendLine(); return; }
        foreach (var c in lista.Take(ClientiPerElenco))
            sb.AppendLine($"- {c.Nome} ({c.Provincia ?? "prov. n.d."}): {anno - 1} {E(c.AnnoPrima)} → {anno} {E(c.AnnoRiferimento)} ({P(Var(c.AnnoPrima, c.AnnoRiferimento))})");
        if (lista.Count > ClientiPerElenco) sb.AppendLine($"- … altri {lista.Count - ClientiPerElenco}");
        sb.AppendLine();
    }

    private static string RigaZona(string nome, IReadOnlyList<ValoriZonaAnno> anni, int annoRif)
    {
        var u = anni.FirstOrDefault(v => v.Anno == annoRif);
        var p = anni.FirstOrDefault(v => v.Anno == annoRif - 1);
        return $"- {nome}: " + string.Join("; ", anni.Select(a => $"{a.Anno} {E(a.Fatturato)}"))
            + $" | var. {P(Var(p?.Fatturato ?? 0m, u?.Fatturato ?? 0m))} | clienti {u?.ClientiAttivi ?? 0}";
    }

    // ── Indicatori economici ─────────────────────────────────────────
    public static string Indicatori(DatiCommentoIndicatori d)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"PAGINA: Indicatori economici — {d.NomeArea}.");
        sb.AppendLine($"Filtri: {d.DescrizioneFiltri}");
        if (d.AggiornamentoDati is not null) sb.AppendLine($"Dati AZW: {d.AggiornamentoDati}");
        if (d.Anni.Any(a => a.Anno == d.AnnoInCorso))
            sb.AppendLine($"Attenzione: il {d.AnnoInCorso} è parziale" + (d.UltimoMeseAnnoInCorso is int m ? $" (movimenti fino a {NomeMese(m)})." : "."));
        sb.AppendLine("Definizioni: spese fisse/variabili dalla classificazione delle voci AZW (stesse regole di Power BI); "
            + "valore aggiunto, EBITDA ed EBIT escludono ammortamenti e imposte"
            + (d.Area ? "; per un'area le spese fisse comprendono 1/5 delle spese fisse dell'Amministrazione." : "; il totale MAREA non comprende quote figurative."));
        sb.AppendLine("Non c'è la pagina della sede di Pesaro: ometti quella sezione della risposta.");
        sb.AppendLine();

        sb.AppendLine("## Indicatori per anno");
        foreach (var (nome, sel, pct) in Righe(d.Area))
            sb.AppendLine($"- {nome}: " + string.Join("; ", d.Anni.Select(a => $"{a.Anno} {(sel(a) is decimal v ? (pct ? P(v, segno: false) : E(v)) : "n.d.")}")));
        sb.AppendLine();

        if (d.AltreAree.Count > 0)
        {
            var anno = d.AltreAree[0].Valori.Anno;
            sb.AppendLine($"## Confronto fra le aree nel {anno} (con 1/5 dell'Amministrazione nelle spese fisse)");
            foreach (var (area, v) in d.AltreAree)
                sb.AppendLine($"- {area}: fatturato {E(v.Fatturato)}, margine di contribuzione {E(v.MargineContribuzione)}, personale {E(v.Personale)}, "
                    + $"EBITDA {E(v.Ebitda)}, utile ante imposte {E(v.UtileAnteImposte)} (marginalità {P(v.MarginalitaPercento, segno: false)})");
        }
        return sb.ToString();
    }

    private static IEnumerable<(string, Func<IndicatoriAnno, decimal?>, bool)> Righe(bool area)
    {
        yield return ("Fatturato", r => r.Fatturato, false);
        yield return (area ? "Spese fisse (con 1/5 Amministrazione)" : "Spese fisse", r => r.SpeseFisse, false);
        yield return ("Spese variabili", r => r.SpeseVariabili, false);
        yield return ("Materie prime", r => r.MateriePrime, false);
        yield return ("Margine di contribuzione", r => r.MargineContribuzione, false);
        yield return ("Incidenza variabili su fatturato", r => r.IncidenzaVariabili, true);
        yield return ("Personale", r => r.Personale, false);
        yield return ("Incidenza personale su fatturato", r => r.IncidenzaPersonale, true);
        yield return ("Valore aggiunto", r => r.ValoreAggiunto, false);
        yield return ("EBITDA", r => r.Ebitda, false);
        yield return ("Ammortamenti", r => r.Ammortamenti, false);
        yield return ("EBIT", r => r.Ebit, false);
        yield return ("ROS", r => r.Ros, true);
        yield return ("Imposte", r => r.Imposte, false);
        yield return ("Utile ante imposte", r => r.UtileAnteImposte, false);
        yield return ("Marginalità lorda", r => r.MarginalitaPercento, true);
        yield return ("Utile netto", r => r.UtileNetto, false);
        yield return ("Fatturato di pareggio", r => r.FatturatoPareggio, false);
        yield return ("Costi non classificati", r => r.CostiNonClassificati, false);
    }

    // ── Rendimento commerciali ───────────────────────────────────────
    public static string Commerciali(string descrizioneFiltri, string? aggiornamentoDati, IReadOnlyList<int> anni, int annoRiferimento,
        IReadOnlyList<RendimentoCommerciale> commerciali)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PAGINA: Rendimento commerciali.");
        sb.AppendLine($"Filtri: {descrizioneFiltri}");
        if (aggiornamentoDati is not null) sb.AppendLine($"Dati AZW: {aggiornamentoDati}");
        var annoMax = anni.Count > 0 ? anni[^1] : annoRiferimento;
        if (annoMax > annoRiferimento) sb.AppendLine($"Attenzione: il {annoMax} è parziale (anno in corso).");
        sb.AppendLine("Definizioni: costo = voce personale AZW della persona (costo completo) + provvigioni calcolate da LaboratorioAI; "
            + "portato = incassi su cui è stata calcolata una provvigione (al netto delle note; esiste solo da quando c'è l'accordo provvigioni); "
            + "seguito = fatturato dei clienti che oggi hanno il suo codice venditore in AZW (senza storico: negli anni prima del suo arrivo mostra come andavano quei clienti); "
            + "margine portato = portato × margine di contribuzione % delle aree in cui è registrato il suo costo; contributo = margine portato − costo.");
        sb.AppendLine("Non c'è la sezione «Sede di Pesaro»: usa al suo posto «## Si ripaga?».");
        sb.AppendLine();

        foreach (var c in commerciali)
        {
            sb.AppendLine($"## {c.Nome}" + (c.AnnoInizio is int i ? $" (costo in AZW dal {i})" : " (nessun costo in AZW)"));
            if (c.VoceCosto is not null)
                sb.AppendLine($"Voce di costo {c.VoceCosto}; aree: " + string.Join(", ", c.Aree.Select(a => $"{a.Centro} {P(a.Peso, segno: false)}")));
            foreach (var a in c.Avvisi) sb.AppendLine($"Avviso: {a}");
            foreach (var r in c.Anni)
                sb.AppendLine($"- {r.Anno}: costo {E(r.CostoTotale)} (stipendio {E(r.CostoStipendio)}, provvigioni {E(r.Provvigioni)}); "
                    + $"portato {E(r.Portato)} (nuovi {E(r.PortatoNuovi)}, clienti {r.ClientiPortati}); "
                    + $"seguito {E(r.Seguito)} (clienti {r.ClientiSeguiti}, nuovi {r.ClientiSeguitiNuovi}, persi {r.ClientiSeguitiPersi}"
                    + (r.ClientiSeguitiAcquisiti > 0 ? $", acquisiti da un nostro cliente {r.ClientiSeguitiAcquisiti}" : "") + "); "
                    + $"incidenza costo su portato {P(r.IncidenzaSuPortato, segno: false)}, su seguito {P(r.IncidenzaSuSeguito, segno: false)}; "
                    + $"€ portati per € di costo {(r.PortatoPerEuro is decimal pe ? pe.ToString("0.00", It) : "n.d.")}; "
                    + $"margine di contribuzione aree {P(r.MargineContribuzionePercento, segno: false)}, margine portato {(r.MarginePortato is decimal mp ? E(mp) : "n.d.")}, "
                    + $"contributo {(r.Contributo is decimal ct ? E(ct) : "n.d.")}; incassato sulle fatture dell'anno dei seguiti {P(r.PercentualeIncassata, segno: false)}");
            if (c.ContributoCumulato.Count > 0)
                sb.AppendLine("Contributo cumulato: " + string.Join("; ", c.ContributoCumulato.Select(x => $"{x.Anno} {E(x.Cumulato)}")));
            Elenco(sb, $"Clienti portati da {c.Nome} (base provvigioni {annoRiferimento} e {annoRiferimento - 1})",
                c.ClientiPortati.ToList(), annoRiferimento);
        }
        return sb.ToString();
    }

    // ── Formattazione ────────────────────────────────────────────────
    public static decimal? Var(decimal prima, decimal dopo) => prima > 0 ? (dopo - prima) / prima : null;

    private static string E(decimal v) => Math.Round(v, 0).ToString("#,##0", It) + " €";

    private static string P(decimal? v, bool segno = true) => v is not decimal x ? "n.d."
        : (segno && x >= 0 ? "+" : "") + (x * 100).ToString("0.0", It) + "%";

    private static string NomeMese(int m) => m is >= 1 and <= 12 ? It.DateTimeFormat.GetMonthName(m) : "?";
}
