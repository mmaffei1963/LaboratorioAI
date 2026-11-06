using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;

namespace LaboratorioAI.Core.CicloAnalitico;

/// <summary>Una prova di un campione non ancora uscito (dati ArcLab, sola lettura).</summary>
public sealed class ProvaInAttesa
{
    public int IdCampione { get; set; }
    public string NumeroCampione { get; set; } = "";
    public string? RagioneSociale { get; set; }
    public string? Matrice { get; set; }
    public string? Controllo { get; set; }
    public string? DenominazioneCampione { get; set; }
    public DateTime? DataRicezione { get; set; }
    public int? RitardoAnalisi { get; set; }
    public string? NomeParametro { get; set; }
    /// <summary>prove.VerificaValore = 1: risultato verificato.</summary>
    public bool Verificata { get; set; }
    public int? IdLabEsterno { get; set; }
    public string? LabEsterno { get; set; }
    /// <summary>prove.EsclusodaRdp: non conta.</summary>
    public bool Esclusa { get; set; }
}

public enum StatoRitardo
{
    /// <summary>Almeno una prova interna senza risultato verificato.</summary>
    ParametriMancanti = 1,
    /// <summary>Mancano solo prove affidate a laboratori esterni.</summary>
    Subappalto = 2,
    /// <summary>Tutti i risultati verificati, ma il rapporto di prova non è emesso.</summary>
    RapportoDaEmettere = 3,
}

/// <summary>Impostazioni dell'avviso (salvate in app.Configurazioni come JSON).</summary>
public sealed class ImpostazioniRitardi
{
    /// <summary>Giorni della settimana dell'avviso (DayOfWeek: 1 = lunedì … 5 = venerdì, 0 = domenica).</summary>
    public List<int> GiorniAvviso { get; set; } = [5];
    /// <summary>Soglia in giorni per famiglia (codice enum <see cref="FamigliaNormativa"/>).</summary>
    public Dictionary<string, int> Soglie { get; set; } = new();
    /// <summary>Soglia in giorni per le prove in subappalto.</summary>
    public int SogliaSubappalto { get; set; } = 20;

    public static readonly IReadOnlyDictionary<FamigliaNormativa, int> SogliePredefinite = new Dictionary<FamigliaNormativa, int>
    {
        [FamigliaNormativa.Legionella] = 14, [FamigliaNormativa.AcqueConsumoUmano] = 7, [FamigliaNormativa.Molluschi] = 5,
        [FamigliaNormativa.Alimenti] = 7, [FamigliaNormativa.Piscine] = 7, [FamigliaNormativa.Scarichi] = 15,
        [FamigliaNormativa.SuperficiAria] = 5, [FamigliaNormativa.Altro] = 10,
    };

    public int Soglia(FamigliaNormativa f) =>
        Soglie.TryGetValue(f.ToString(), out var s) ? s : SogliePredefinite.GetValueOrDefault(f, 10);
}

/// <summary>Un campione in ritardo.</summary>
public sealed record CampioneInRitardo(
    int IdCampione, string NumeroCampione, string? RagioneSociale, string? Matrice, string? Controllo, string? Denominazione,
    FamigliaNormativa Famiglia, StatoRitardo Stato, DateTime DataRicezione, int RitardoProgrammato, DateTime DataInizio,
    int GiorniTrascorsi, int Soglia, IReadOnlyList<string> ProveMancanti, IReadOnlyList<string> LaboratoriEsterni)
{
    public int GiorniRitardo => GiorniTrascorsi - Soglia;
}

/// <summary>
/// Avviso campioni in ritardo (22/10/2026). Un campione non uscito (senza rapporto di prova e non chiuso) è in ritardo se
/// dalla data di inizio sono passati più giorni della soglia del suo tipo. Inizio = data di ricezione + giorni di ritardo
/// programmato (Campione.RitardoAnalisi, es. shelf life): 10/10 + 5 = 15/10, come in «Da Validare» e nella scheda di
/// accettazione. Tipo = famiglia della matrice (stessa regola dei limiti normativi); per le prove in subappalto vale la
/// soglia del subappalto.
/// </summary>
public static class MotoreRitardiCampioni
{
    public static DateTime DataInizio(DateTime dataRicezione, int? ritardo) => dataRicezione.Date.AddDays(ritardo is > 0 ? ritardo.Value : 0);

    public static List<CampioneInRitardo> Calcola(IEnumerable<ProvaInAttesa> prove, ImpostazioniRitardi imp, DateTime oggi,
        Func<int, bool>? nascosto = null, bool soloInRitardo = true)
    {
        var r = new List<CampioneInRitardo>();
        foreach (var g in prove.GroupBy(p => p.IdCampione))
        {
            if (nascosto?.Invoke(g.Key) == true) continue;
            var primo = g.First();
            if (primo.DataRicezione is not DateTime ric) continue;
            var valide = g.Where(p => !p.Esclusa).ToList();
            if (valide.Count == 0) continue;
            var interne = valide.Where(p => p.IdLabEsterno is null && !p.Verificata).ToList();
            var esterne = valide.Where(p => p.IdLabEsterno is not null && !p.Verificata).ToList();
            var stato = interne.Count > 0 ? StatoRitardo.ParametriMancanti : esterne.Count > 0 ? StatoRitardo.Subappalto : StatoRitardo.RapportoDaEmettere;
            var famiglia = RegoleLimiti.FamigliaDaMatrice(primo.Matrice);
            var soglia = stato == StatoRitardo.Subappalto ? imp.SogliaSubappalto : imp.Soglia(famiglia);
            var inizio = DataInizio(ric, primo.RitardoAnalisi);
            var trascorsi = (oggi.Date - inizio).Days;
            if (soloInRitardo && trascorsi <= soglia) continue;
            var mancanti = interne.Concat(esterne).Select(p => (p.NomeParametro ?? "?") + (p.IdLabEsterno is not null ? $" ({p.LabEsterno})" : ""))
                .Distinct().ToList();
            var labs = esterne.Select(p => p.LabEsterno ?? "esterno").Distinct().ToList();
            r.Add(new CampioneInRitardo(g.Key, primo.NumeroCampione, primo.RagioneSociale, primo.Matrice, primo.Controllo, primo.DenominazioneCampione,
                famiglia, stato, ric.Date, primo.RitardoAnalisi is > 0 ? primo.RitardoAnalisi.Value : 0, inizio, trascorsi, soglia, mancanti, labs));
        }
        return r.OrderBy(x => x.Stato).ThenByDescending(x => x.GiorniRitardo).ToList();
    }
}
