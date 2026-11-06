namespace LaboratorioAI.Core.Andamento;

/// <summary>Fatturato di un cliente in un mese, al netto delle note di credito.</summary>
/// <param name="Laboratorio">Analisi (base di calcolo del laboratorio).</param>
/// <param name="Legionella">Parte di <paramref name="Laboratorio"/> sulle voci della Legionella.</param>
/// <param name="Altro">Tutto il resto (consulenza, formazione, sicurezza…).</param>
public sealed record FatturatoMensile(string Cliente, int Anno, int Mese, decimal Laboratorio, decimal Legionella, decimal Altro);

/// <summary>Campioni di laboratorio di un cliente in un mese per controllo (copia di ArcLab).</summary>
public sealed record CampioniMensili(string Cliente, int Anno, int Mese, int? IdControllo, string? Controllo,
    int Campioni, decimal ValoreListino, DateTime UltimoCampione, bool Legionella);

/// <summary>Soglie del semaforo (percentuali come frazioni: −0,20 = −20%).</summary>
public sealed record SoglieAndamento
{
    public decimal MinimoFatturato { get; init; } = 500m;
    public decimal SogliaRosso { get; init; } = -0.20m;
    public decimal SogliaGiallo { get; init; } = -0.10m;
    public decimal ValoreAnalisiPerse { get; init; } = 300m;
    public int CampioniTrimestreMinimo { get; init; } = 3;
    public int LegionellaTolleranzaGiorni { get; init; } = 30;
    public int LegionellaPreavvisoGiorni { get; init; } = 30;
    /// <summary>Legionella «in calo»: variazione dei campioni (frazione, −0,20 = −20%).</summary>
    public decimal LegionellaSogliaCalo { get; init; } = -0.20m;
    public decimal SoloConsulenzaMinimo { get; init; } = 1000m;
    public decimal SoloConsulenzaLabMassimo { get; init; } = 500m;
}

/// <summary>
/// Finestra del confronto: il periodo attuale (ultimi <see cref="Mesi"/> mesi interi, oppure da gennaio se
/// <see cref="DaInizioAnno"/>) contro gli stessi mesi di <see cref="AnniIndietro"/> anni prima.
/// </summary>
public sealed record FinestraConfronto
{
    public int Mesi { get; init; } = 12;
    public bool DaInizioAnno { get; init; }
    public int AnniIndietro { get; init; } = 1;

    public static FinestraConfronto Predefinita { get; } = new();

    /// <summary>Durata effettiva in mesi, data la fine del periodo (1–12).</summary>
    public int DurataMesi(int meseFine) => DaInizioAnno ? meseFine : Math.Clamp(Mesi, 1, 12);
}

public enum Semaforo
{
    /// <summary>Fatturato di laboratorio sotto il minimo: nessun semaforo.</summary>
    Nessuno = 0,
    Verde = 1,
    Giallo = 2,
    Rosso = 3
}

public enum StatoLegionella
{
    Perso = 1,
    InRitardo = 2,
    InCalo = 3,
    DaRicordare = 4,
    Nuovo = 5,
    Regolare = 6
}

/// <summary>Controllo fatto nei 12 mesi precedenti e mai negli ultimi 12.</summary>
public sealed record AnalisiPersa(int? IdControllo, string Controllo, int Campioni, decimal ValoreListino);

/// <summary>Andamento di un cliente: confronto fra gli ultimi 12 mesi interi e i 12 precedenti.</summary>
public sealed record AndamentoCliente
{
    public string Cliente { get; init; } = "";
    public decimal LabPrecedenti { get; init; }
    public decimal LabUltimi { get; init; }
    /// <summary>Variazione come frazione; null se nei 12 mesi precedenti non c'era fatturato.</summary>
    public decimal? Variazione { get; init; }
    public decimal Perdita => LabPrecedenti - LabUltimi;
    public decimal LabAnnoPrecedenteAllaData { get; init; }
    public decimal LabAnnoInCorso { get; init; }
    public decimal? VariazioneAnno { get; init; }
    public int CampioniPrecedenti { get; init; }
    public int CampioniUltimi { get; init; }
    public decimal? VariazioneCampioni { get; init; }
    public DateTime? UltimoCampione { get; init; }
    public int? GiorniDallUltimo { get; init; }
    public int? IntervalloTipico { get; init; }
    public bool InRitardo { get; init; }
    public int CampioniTrimestreAnnoPrima { get; init; }
    public int CampioniUltimoTrimestre { get; init; }
    public bool FermoUltimoTrimestre { get; init; }
    public IReadOnlyList<AnalisiPersa> AnalisiPerse { get; init; } = Array.Empty<AnalisiPersa>();
    public decimal ValoreAnalisiPerse { get; init; }
    public decimal AltroUltimi { get; init; }
    public decimal AltroPrecedenti { get; init; }
    public bool SoloConsulenza { get; init; }
    public IReadOnlyList<string> Motivi { get; init; } = Array.Empty<string>();
    public Semaforo Semaforo { get; init; }
    /// <summary>
    /// 05/11/2026 (richiesta di Zatterin): analisi «in corso», cioè campioni ricevuti dall'ultimo mese intero in poi.
    /// Le analisi di un mese si fatturano nei primi giorni del mese dopo, quindi non sono ancora nel fatturato del
    /// periodo. Valore a listino, solo informativo: non entra nel semaforo.
    /// </summary>
    public int CampioniInCorso { get; init; }
    public decimal ValoreInCorso { get; init; }
}

/// <summary>Situazione Legionella di un cliente con campionamenti negli ultimi 24 mesi.</summary>
public sealed record AndamentoLegionella
{
    public string Cliente { get; init; } = "";
    public int CampioniPrecedenti { get; init; }
    public int CampioniUltimi { get; init; }
    public decimal FatturatoPrecedenti { get; init; }
    public decimal FatturatoUltimi { get; init; }
    /// <summary>Mesi dell'anno in cui il cliente campiona (1–12), dagli ultimi 24 mesi.</summary>
    public IReadOnlyList<int> MesiAbituali { get; init; } = Array.Empty<int>();
    public int MesiConCampionamenti { get; init; }
    public decimal PuntiPerCampionamento { get; init; }
    public int IntervalloAtteso { get; init; }
    public DateTime UltimoCampionamento { get; init; }
    public int GiorniDallUltimo { get; init; }
    public DateTime ProssimoAtteso { get; init; }
    public int GiorniAlProssimo { get; init; }
    public StatoLegionella Stato { get; init; }
}

public sealed record RisultatoAndamento(
    DateTime DataRiferimento,
    /// <summary>Ultimo mese intero considerato (anno·100 + mese).</summary>
    int UltimoMese,
    /// <summary>Primo mese del periodo attuale e del periodo di confronto (anno·100 + mese).</summary>
    int InizioPeriodo,
    int InizioConfronto,
    int FineConfronto,
    FinestraConfronto Finestra,
    /// <summary>Fatturato minimo applicato al periodo (la soglia annua in proporzione alla durata).</summary>
    decimal MinimoApplicato,
    IReadOnlyList<AndamentoCliente> Clienti,
    IReadOnlyList<AndamentoLegionella> Legionella);

/// <summary>
/// Calcolo dei segnali «clienti da seguire». Funzione pura, senza database.
///
/// Periodi: ultimi 12 mesi interi prima del mese della data di riferimento e i 12 precedenti (così la stagionalità
/// non inganna); da inizio anno contro lo stesso periodo dell'anno prima. Semaforo: ROSSO se nessuna analisi negli
/// ultimi 12 mesi o variazione ≤ soglia rossa; GIALLO se variazione ≤ soglia gialla, campioni in ritardo, fermo
/// negli ultimi tre mesi o analisi perse sopra soglia con fatturato in calo; VERDE altrimenti. Solo per i clienti
/// con fatturato di laboratorio nei 12 mesi precedenti almeno pari al minimo.
/// </summary>
public sealed class MotoreAndamentoClienti
{
    public RisultatoAndamento Calcola(IReadOnlyList<FatturatoMensile> fatturato, IReadOnlyList<CampioniMensili> campioni,
        SoglieAndamento soglie, DateTime dataRiferimento, FinestraConfronto? finestra = null)
    {
        finestra ??= FinestraConfronto.Predefinita;
        var oggi = dataRiferimento.Date;
        var fine = Mese(oggi.Year, oggi.Month) - 1;            // ultimo mese intero
        var durata = finestra.DurataMesi(MeseDi(fine));
        var iniUltimi = fine - durata + 1;
        var spostamento = 12 * Math.Clamp(finestra.AnniIndietro, 1, 3);
        var iniPrec = iniUltimi - spostamento;
        var finePrec = fine - spostamento;
        var minimo = Math.Round(soglie.MinimoFatturato * durata / 12m, 2);
        var minimoConsulenza = Math.Round(soglie.SoloConsulenzaMinimo * durata / 12m, 2);
        var massimoLabConsulenza = Math.Round(soglie.SoloConsulenzaLabMassimo * durata / 12m, 2);
        var anno = Anno(fine);
        var meseFine = MeseDi(fine);
        int AnnoInCorsoDa = anno * 12;                          // gennaio dell'anno dell'ultimo mese
        var trimestre = (Da: fine - 2, A: fine);
        var trimestrePrima = (Da: fine - 14, A: fine - 12);

        var fPer = fatturato.ToLookup(f => f.Cliente, StringComparer.Ordinal);
        var cPer = campioni.ToLookup(c => c.Cliente, StringComparer.Ordinal);
        var codici = fPer.Select(g => g.Key).Concat(cPer.Select(g => g.Key)).Distinct(StringComparer.Ordinal).ToList();

        var clienti = new List<AndamentoCliente>();
        foreach (var cod in codici)
        {
            var f = fPer[cod].ToList();
            var c = cPer[cod].ToList();
            decimal SommaF(Func<FatturatoMensile, decimal> v, int da, int a) =>
                f.Where(x => Tra(Mese(x.Anno, x.Mese), da, a)).Sum(v);
            int SommaC(int da, int a) => c.Where(x => Tra(Mese(x.Anno, x.Mese), da, a)).Sum(x => x.Campioni);

            var labP = SommaF(x => x.Laboratorio, iniPrec, finePrec);
            var labU = SommaF(x => x.Laboratorio, iniUltimi, fine);
            var annoP = SommaF(x => x.Laboratorio, AnnoInCorsoDa - spostamento, AnnoInCorsoDa - spostamento + meseFine - 1);
            var annoU = SommaF(x => x.Laboratorio, AnnoInCorsoDa, AnnoInCorsoDa + meseFine - 1);
            var altroP = SommaF(x => x.Altro, iniPrec, finePrec);
            var altroU = SommaF(x => x.Altro, iniUltimi, fine);
            var campP = SommaC(iniPrec, finePrec);
            var campU = SommaC(iniUltimi, fine);
            var inCorso = c.Where(x => Mese(x.Anno, x.Mese) >= fine).ToList();
            var trimP = SommaC(trimestrePrima.Da, trimestrePrima.A);
            var trimU = SommaC(trimestre.Da, trimestre.A);

            DateTime? ultimo = c.Count > 0 ? c.Max(x => x.UltimoCampione).Date : null;
            int? giorni = ultimo is { } u ? (int)(oggi - u).TotalDays : null;
            var mesiAttivi = c.Where(x => Tra(Mese(x.Anno, x.Mese), fine - 23, fine) && x.Campioni > 0)
                .Select(x => Mese(x.Anno, x.Mese)).Distinct().Count();
            int? intervallo = mesiAttivi > 0 ? (int)Math.Round(730m / mesiAttivi) : null;
            var inRitardo = mesiAttivi >= 3 && giorni is int g && g > Math.Max(60, 2 * intervallo!.Value);
            var fermo = trimP >= soglie.CampioniTrimestreMinimo && trimU == 0;

            var perse = c.Where(x => Tra(Mese(x.Anno, x.Mese), iniPrec, finePrec))
                .GroupBy(x => x.IdControllo)
                .Where(gr => !c.Any(x => x.IdControllo == gr.Key && Tra(Mese(x.Anno, x.Mese), iniUltimi, fine) && x.Campioni > 0))
                .Select(gr => new AnalisiPersa(gr.Key, gr.Select(x => x.Controllo).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "(senza nome)",
                    gr.Sum(x => x.Campioni), gr.Sum(x => x.ValoreListino)))
                .Where(p => p.Campioni > 0)
                .OrderByDescending(p => p.ValoreListino).ThenBy(p => p.Controllo, StringComparer.Ordinal)
                .ToList();
            var valorePerse = perse.Sum(p => p.ValoreListino);

            decimal? Var(decimal prima, decimal dopo) => prima > 0m ? dopo / prima - 1m : null;
            var variazione = Var(labP, labU);
            var varCamp = campP > 0 ? (decimal)campU / campP - 1m : (decimal?)null;

            var motivi = new List<string>();
            var semaforo = Semaforo.Nessuno;
            if (labP >= minimo && labP > 0m)
            {
                var rosso = labU <= 0m || variazione <= soglie.SogliaRosso;
                if (labU <= 0m) motivi.Add("Nessuna analisi nel periodo");
                else if (variazione <= soglie.SogliaRosso) motivi.Add("Calo forte");
                else if (variazione <= soglie.SogliaGiallo) motivi.Add("In calo");
                if (campP > 0 && varCamp <= soglie.SogliaRosso) motivi.Add("Meno campioni");
                if (variazione <= soglie.SogliaGiallo && varCamp is decimal vc && vc > -0.05m)
                    motivi.Add("Stessi campioni, meno €: prezzo o tipo di analisi");
                if (inRitardo) motivi.Add("Campioni in ritardo");
                if (fermo) motivi.Add("Fermo negli ultimi tre mesi");
                if (valorePerse >= soglie.ValoreAnalisiPerse) motivi.Add("Analisi perse");
                if (campP == 0 && campU == 0) motivi.Add("Nessun campione in ArcLab");
                var giallo = variazione <= soglie.SogliaGiallo || inRitardo || fermo
                             || (valorePerse >= soglie.ValoreAnalisiPerse && labU < labP);
                semaforo = rosso ? Semaforo.Rosso : giallo ? Semaforo.Giallo : Semaforo.Verde;
            }

            clienti.Add(new AndamentoCliente
            {
                Cliente = cod, LabPrecedenti = labP, LabUltimi = labU, Variazione = variazione,
                LabAnnoPrecedenteAllaData = annoP, LabAnnoInCorso = annoU, VariazioneAnno = Var(annoP, annoU),
                CampioniPrecedenti = campP, CampioniUltimi = campU, VariazioneCampioni = varCamp,
                UltimoCampione = ultimo, GiorniDallUltimo = giorni, IntervalloTipico = intervallo, InRitardo = inRitardo,
                CampioniTrimestreAnnoPrima = trimP, CampioniUltimoTrimestre = trimU, FermoUltimoTrimestre = fermo,
                AnalisiPerse = perse, ValoreAnalisiPerse = valorePerse,
                AltroPrecedenti = altroP, AltroUltimi = altroU,
                SoloConsulenza = altroU >= minimoConsulenza && labU < massimoLabConsulenza,
                Motivi = motivi, Semaforo = semaforo,
                CampioniInCorso = inCorso.Sum(x => x.Campioni), ValoreInCorso = inCorso.Sum(x => x.ValoreListino)
            });
        }

        // La Legionella segue il ritmo dei campionamenti: confronto fisso ultimi 12 mesi contro i 12 precedenti.
        var legionella = CalcolaLegionella(fPer, campioni.Where(x => x.Legionella).ToList(), soglie, oggi, fine - 23, fine - 12, fine - 11, fine);
        int Etichetta(int m) => Anno(m) * 100 + MeseDi(m);
        return new RisultatoAndamento(oggi, Etichetta(fine), Etichetta(iniUltimi), Etichetta(iniPrec), Etichetta(finePrec), finestra, minimo,
            clienti.OrderByDescending(x => x.Perdita).ThenBy(x => x.Cliente, StringComparer.Ordinal).ToList(), legionella);
    }

    private static List<AndamentoLegionella> CalcolaLegionella(ILookup<string, FatturatoMensile> fPer, List<CampioniMensili> leg,
        SoglieAndamento soglie, DateTime oggi, int iniPrec, int finePrec, int iniUltimi, int fine)
    {
        var risultato = new List<AndamentoLegionella>();
        foreach (var g in leg.Where(x => Tra(Mese(x.Anno, x.Mese), iniPrec, fine) && x.Campioni > 0)
                     .GroupBy(x => x.Cliente, StringComparer.Ordinal))
        {
            var righe = g.ToList();
            var campP = righe.Where(x => Tra(Mese(x.Anno, x.Mese), iniPrec, finePrec)).Sum(x => x.Campioni);
            var campU = righe.Where(x => Tra(Mese(x.Anno, x.Mese), iniUltimi, fine)).Sum(x => x.Campioni);
            var f = fPer[g.Key];
            var fattP = f.Where(x => Tra(Mese(x.Anno, x.Mese), iniPrec, finePrec)).Sum(x => x.Legionella);
            var fattU = f.Where(x => Tra(Mese(x.Anno, x.Mese), iniUltimi, fine)).Sum(x => x.Legionella);
            var mesi = righe.Select(x => Mese(x.Anno, x.Mese)).Distinct().Count();
            var intervallo = (int)Math.Min(365m, Math.Max(30m, Math.Round(730m / mesi)));
            // L'ultimo campionamento può cadere anche nel mese in corso (dopo l'ultimo mese intero).
            var ultimo = leg.Where(x => x.Cliente == g.Key).Max(x => x.UltimoCampione).Date;
            var giorni = (int)(oggi - ultimo).TotalDays;
            var prossimo = ultimo.AddDays(intervallo);
            var alProssimo = (int)(prossimo - oggi).TotalDays;

            StatoLegionella stato;
            if (campP > 0 && campU == 0 && fattU <= 0m) stato = StatoLegionella.Perso;
            else if (campP == 0) stato = StatoLegionella.Nuovo;
            else if (giorni > intervallo + soglie.LegionellaTolleranzaGiorni) stato = StatoLegionella.InRitardo;
            else if (campU <= campP * (1m + soglie.LegionellaSogliaCalo)) stato = StatoLegionella.InCalo;
            else if (alProssimo <= soglie.LegionellaPreavvisoGiorni) stato = StatoLegionella.DaRicordare;
            else stato = StatoLegionella.Regolare;

            risultato.Add(new AndamentoLegionella
            {
                Cliente = g.Key, CampioniPrecedenti = campP, CampioniUltimi = campU,
                FatturatoPrecedenti = fattP, FatturatoUltimi = fattU,
                MesiAbituali = righe.Select(x => x.Mese).Distinct().OrderBy(m => m).ToList(),
                MesiConCampionamenti = mesi,
                PuntiPerCampionamento = Math.Round((decimal)righe.Sum(x => x.Campioni) / mesi, 1),
                IntervalloAtteso = intervallo, UltimoCampionamento = ultimo, GiorniDallUltimo = giorni,
                ProssimoAtteso = prossimo, GiorniAlProssimo = alProssimo, Stato = stato
            });
        }
        return risultato.OrderBy(x => x.Stato).ThenByDescending(x => x.FatturatoPrecedenti)
            .ThenBy(x => x.Cliente, StringComparer.Ordinal).ToList();
    }

    // Mesi come numero progressivo (anno × 12 + mese − 1) per confronti e intervalli.
    private static int Mese(int anno, int mese) => anno * 12 + mese - 1;
    private static int Anno(int progressivo) => progressivo / 12;
    private static int MeseDi(int progressivo) => progressivo % 12 + 1;
    private static bool Tra(int m, int da, int a) => m >= da && m <= a;
}

/// <summary>Descrizioni in italiano dei periodi confrontati (per pagine ed esportazioni).</summary>
public static class DescrizionePeriodi
{
    private static readonly string[] Mesi = { "gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic" };

    /// <summary>Intervallo di mesi, es. «apr–set 2026» o «ott 2025–set 2026».</summary>
    public static string Intervallo(int da, int a)
    {
        var (ad, md) = (da / 100, da % 100);
        var (aa, ma) = (a / 100, a % 100);
        if (da == a) return $"{Mesi[md - 1]} {ad}";
        return ad == aa ? $"{Mesi[md - 1]}–{Mesi[ma - 1]} {aa}" : $"{Mesi[md - 1]} {ad}–{Mesi[ma - 1]} {aa}";
    }

    public static string Attuale(RisultatoAndamento r) => Intervallo(r.InizioPeriodo, r.UltimoMese);
    public static string Confronto(RisultatoAndamento r) => Intervallo(r.InizioConfronto, r.FineConfronto);
    public static string Completa(RisultatoAndamento r) => $"{Attuale(r)} contro {Confronto(r)}";
}
