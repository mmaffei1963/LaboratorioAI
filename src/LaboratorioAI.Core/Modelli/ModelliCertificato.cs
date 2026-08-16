namespace LaboratorioAI.Core.Modelli;

/// <summary>Certificato emesso, come compare nei risultati di ricerca.</summary>
public record RisultatoRicercaCertificato
{
    public int IdCertificato { get; init; }
    public int IdCampione { get; init; }
    public string? NumeroCertificato { get; init; }
    public DateTime? DataCertificato { get; init; }
    public string NumeroCampione { get; init; } = "";
    public string? RagioneSociale { get; init; }
    public string? DenominazioneCampione { get; init; }
    public string? Matrice { get; init; }
    public int? GiorniRisposta { get; init; }
}

/// <summary>Certificato con i dati del campione e le prove.</summary>
public record CertificatoCompleto
{
    public required RisultatoRicercaCertificato Certificato { get; init; }
    public required DettaglioCampione Campione { get; init; }
    public required IReadOnlyList<ProvaCampione> Prove { get; init; }
}
