using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>
/// "Solo esterni": confronto (sola lettura) fra i certificati esterni caricati nel portale
/// (dbo.Entries) e i certificati/campioni di ArcLab, per anno + lotto/campione + cliente.
/// </summary>
public interface IRepositorySoloEsterni
{
    Task<IReadOnlyList<RigaSoloEsterni>> CercaAsync(CriteriSoloEsterni criteri, CancellationToken ct = default);
}

/// <summary>Criteri di ricerca della maschera "Solo esterni".</summary>
public record CriteriSoloEsterni
{
    /// <summary>Intervallo su Entries.DataUpload (estremi inclusi).</summary>
    public DateOnly? DataDa { get; init; }
    public DateOnly? DataA { get; init; }
    /// <summary>Scorciatoia mese intero: se valorizzati, impostano il periodo su tutto il mese.</summary>
    public int? Anno { get; init; }
    public int? Mese { get; init; }
    /// <summary>Filtro sulla ragione sociale del cliente (contiene).</summary>
    public string? RagioneSociale { get; init; }
    /// <summary>Se true, solo gli esterni SENZA certificato interno corrispondente.</summary>
    public bool SoloSenzaInterno { get; init; }
    /// <summary>"fatturati" | "nonfatturati" | null = tutti (in base a Certificato.DataFattura).</summary>
    public string? StatoFattura { get; init; }
    /// <summary>"con" | "senza" | null = tutti (corrispondente su Campione ArcLab).</summary>
    public string? StatoCampione { get; init; }
    public int Limite { get; init; } = 1000;
}

/// <summary>Riga di risultato: un certificato esterno con l'esito del confronto.</summary>
public record RigaSoloEsterni
{
    public int IdEntry { get; init; }
    public int? Anno { get; init; }
    public string? Lotto { get; init; }
    public string? Campione { get; init; }
    public int? IdClienteArcLab { get; init; }
    public string? RagioneSociale { get; init; }
    public DateTime DataUpload { get; init; }
    public string? NomeFile { get; init; }
    /// <summary>Esiste un certificato interno caricato con stesso anno+lotto+campione.</summary>
    public bool HaInterno { get; init; }
    /// <summary>Certificato ArcLab agganciato (NCertificato / IdCertificato), se trovato.</summary>
    public string? NCertificato { get; init; }
    public int? IdCertificato { get; init; }
    public DateTime? DataFattura { get; init; }
    public bool Fatturato => DataFattura.HasValue;
    /// <summary>Campione ArcLab corrispondente (stesso anno + lotto/campione + cliente), se trovato.</summary>
    public int? IdCampione { get; init; }
    // Commessa del campione (via Campione → Lotto → commessa)
    public int? IdCommessa { get; init; }
    public int? IdControllo { get; init; }
    public string? Controllo { get; init; }
    public decimal? PrezzoControllo { get; init; }
}
