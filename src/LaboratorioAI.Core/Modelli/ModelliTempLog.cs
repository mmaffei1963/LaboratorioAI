namespace LaboratorioAI.Core.Modelli;

/// <summary>Limiti di accettabilità di un canale, letti dalla taratura in ArcLab.</summary>
public record LimitiCanale
{
    public string Canale { get; init; } = "";
    public int? IdStrumento { get; init; }
    public string? Sonda { get; init; }
    public string? Ambiente { get; init; }
    public int? IdApparecchioAmbiente { get; init; }
    /// <summary>Valore teorico (setpoint) dell'apparecchiatura.</summary>
    public double? VN { get; init; }
    /// <summary>Limite minimo ammesso = LimInfInt del piano di taratura.</summary>
    public double? Min { get; init; }
    /// <summary>Limite massimo ammesso = LimSupInt del piano di taratura.</summary>
    public double? Max { get; init; }
    /// <summary>True se sono disponibili entrambi i limiti (canale monitorabile).</summary>
    public bool LimitiDisponibili => Min.HasValue && Max.HasValue;
    /// <summary>Motivo per cui i limiti mancano (ambiente non associato, piano assente, ecc.).</summary>
    public string? MotivoMancanza { get; init; }
}

/// <summary>Singola lettura oraria di un canale.</summary>
public record LetturaTempLog
{
    public string Ora { get; init; } = "";
    public double? Valore { get; init; }
}

/// <summary>Stato di una lettura rispetto ai limiti.</summary>
public enum StatoLettura { Vuota, InRange, SopraMax, SottoMin, SenzaLimiti }

/// <summary>Lettura valutata rispetto ai limiti del canale.</summary>
public record LetturaValutata
{
    public string Ora { get; init; } = "";
    public double? Valore { get; init; }
    public StatoLettura Stato { get; init; }
}

/// <summary>Una escursione: sforamento di uno o più campioni consecutivi oltre i limiti.</summary>
public record Escursione
{
    public string Canale { get; init; } = "";
    public string? Sonda { get; init; }
    public string? Ambiente { get; init; }
    public DateTime? Data { get; init; }               // giorno del monitoraggio (dal file)
    public string Tipo { get; init; } = "";           // "Sopra il massimo" | "Sotto il minimo"
    public string OraInizio { get; init; } = "";
    public string OraFine { get; init; } = "";
    public int NumeroLetture { get; init; }
    public double ValorePeggiore { get; init; }        // valore più lontano dal limite
    public double ScostamentoMax { get; init; }        // |valore peggiore − limite superato|
    public double LimiteSuperato { get; init; }
}

/// <summary>Esito del monitoraggio di un canale.</summary>
public record EsitoCanale
{
    public string Canale { get; init; } = "";
    public int? IdStrumento { get; init; }
    public string? Sonda { get; init; }
    public string? Ambiente { get; init; }
    public double? VN { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public bool LimitiDisponibili { get; init; }
    public string? MotivoMancanza { get; init; }

    public IReadOnlyList<LetturaValutata> Letture { get; init; } = new List<LetturaValutata>();
    public int NumeroLetture { get; init; }
    public int NumeroSopra { get; init; }
    public int NumeroSotto { get; init; }
    public double? ValoreMedio { get; init; }
    public double? ValoreMin { get; init; }
    public double? ValoreMax { get; init; }
    public bool Conforme { get; init; }                // nessuna escursione (solo se limiti disponibili)
    public IReadOnlyList<Escursione> Escursioni { get; init; } = new List<Escursione>();
}

/// <summary>Contenuto del file notturno Econorma già interpretato.</summary>
public record FileTempLog
{
    public DateTime? DataDal { get; init; }
    public DateTime? DataAl { get; init; }
    public string? NomeFile { get; init; }
    /// <summary>Etichette dei canali presenti nel file, in ordine (T01, T02, …).</summary>
    public IReadOnlyList<string> Canali { get; init; } = new List<string>();
    /// <summary>Letture per canale: chiave = etichetta canale.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<LetturaTempLog>> Letture { get; init; }
        = new Dictionary<string, IReadOnlyList<LetturaTempLog>>();
}

/// <summary>Risultato completo del monitoraggio di un file.</summary>
public record RisultatoMonitoraggio
{
    public DateTime? DataDal { get; init; }
    public DateTime? DataAl { get; init; }
    public string? NomeFile { get; init; }
    public IReadOnlyList<EsitoCanale> Canali { get; init; } = new List<EsitoCanale>();
    public IReadOnlyList<Escursione> Escursioni { get; init; } = new List<Escursione>();
    /// <summary>Canali del file senza limiti risolvibili (da sistemare in ArcLab / da mappare).</summary>
    public IReadOnlyList<string> DaSistemare { get; init; } = new List<string>();
    public int NumeroCanaliMonitorati { get; init; }
    public int NumeroCanaliConEscursioni { get; init; }
    public bool TuttoConforme { get; init; }
}
