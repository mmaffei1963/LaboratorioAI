namespace LaboratorioAI.Core.Repository;

/// <summary>Combinazione matrice + parametro (+ controllo) usata in ArcLab, con il numero di prove.</summary>
public class ParametroUsatoArcLab
{
    public int IdParametro { get; set; }
    public string NomeParametro { get; set; } = "";
    public int? IdMatrice { get; set; }
    public string? Matrice { get; set; }
    public int? IdControllo { get; set; }
    public string? Controllo { get; set; }
    public string? UnitaMisura { get; set; }
    public int Prove { get; set; }
}

/// <summary>Prova di un campione in «Da Validare», con i dati che servono al motore delle segnalazioni (fase 1b).</summary>
public class ProvaPerSegnalazioni
{
    public int IdCampione { get; set; }
    public int IdProva { get; set; }
    public int? IdParametro { get; set; }
    public string? NomeParametro { get; set; }
    public int? IdMatrice { get; set; }
    public string? Matrice { get; set; }
    public DateTime? DataPrelievo { get; set; }
    public DateTime? DataRicezione { get; set; }
    public DateTime? DataInizioProva { get; set; }
    /// <summary>Campione.RitardoAnalisi: giorni programmati tra accettazione e inizio analisi (es. shelf life).</summary>
    public int? RitardoAnalisi { get; set; }
    public double? ValoreCalcolato { get; set; }
    public bool MaggioreDi { get; set; }
    public bool MinoreDi { get; set; }
    public string? UnitaMisura { get; set; }
    /// <summary>La riga del metodo ha MaxDecimali e Arrotonda (regole di arrotondamento).</summary>
    public bool RegoleArrotondamento { get; set; }
    public int? IdLabEsterno { get; set; }
    public string? LabEsterno { get; set; }
    public string? FraseLabEsterno { get; set; }
    public bool Esclusa { get; set; }
}

/// <summary>Letture da ArcLab per i limiti normativi (SOLA LETTURA).</summary>
public interface IRepositoryLimitiArcLab
{
    /// <summary>Prove dei campioni in «Da Validare» (stesse condizioni dell'elenco), per il motore delle segnalazioni.</summary>
    Task<IReadOnlyList<ProvaPerSegnalazioni>> ProveDaValidareAsync(CancellationToken ct = default);

    /// <summary>05/11/2026: le prove di un campione qualsiasi (per i limiti di legge nel prospetto dell'analisi AI).</summary>
    Task<IReadOnlyList<ProvaPerSegnalazioni>> ProveCampioneAsync(int idCampione, CancellationToken ct = default);

    /// <summary>Combinazioni matrice/parametro/controllo/unità con prove negli ultimi <paramref name="anni"/> anni.</summary>
    Task<IReadOnlyList<ParametroUsatoArcLab>> ParametriUsatiAsync(int anni = 3, CancellationToken ct = default);
}
