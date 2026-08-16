using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Accesso SOLA LETTURA ad ArcLab per sonde Econorma e limiti di taratura.</summary>
public interface IRepositoryTempLog
{
    /// <summary>
    /// Elenca le sonde Econorma con l'ambiente associato e i limiti della taratura
    /// (VN/Min/Max) presi dal PianoInterventi "TARATURA". Serve a proporre/sincronizzare
    /// la mappatura canale→strumento e a rileggere i limiti aggiornati.
    /// </summary>
    Task<IReadOnlyList<SondaEconorma>> SondeEconormaAsync(CancellationToken ct = default);

    /// <summary>Limiti di taratura per un insieme di IdStrumento (rilettura al caricamento).</summary>
    Task<IReadOnlyList<LimitiTaratura>> LimitiPerStrumentiAsync(
        IReadOnlyList<int> idStrumenti, CancellationToken ct = default);
}

/// <summary>Riga sonda Econorma risolta da ArcLab.</summary>
public record SondaEconorma
{
    public int IdStrumento { get; init; }
    public string Sonda { get; init; } = "";
    public int? IdApparecchioSonda { get; init; }
    public bool SondaNonInUso { get; init; }
    /// <summary>Canale ipotizzato dal nome ("Sonda T1" → "T01").</summary>
    public string? CanaleIpotesi { get; init; }
    public string? Ambiente { get; init; }
    public int? IdApparecchioAmbiente { get; init; }
    public string? CodPiano { get; init; }
    public string? NomePiano { get; init; }
    public double? VN { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
}

/// <summary>Limiti di taratura per un IdStrumento (rilettura runtime).</summary>
public record LimitiTaratura
{
    public int IdStrumento { get; init; }
    public string? Ambiente { get; init; }
    public double? VN { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public string? MotivoMancanza { get; init; }
}
