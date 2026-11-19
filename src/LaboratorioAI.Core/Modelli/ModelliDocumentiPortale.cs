namespace LaboratorioAI.Core.Modelli;

/// <summary>
/// Documenti del portale (19/11/2026): riga della tabella Entries di Marea (portale Palomar), sola lettura.
/// </summary>
public record DocumentoMarea
{
    public int Id { get; init; }
    public DateTime? DataUpload { get; init; }
    public string? Tipo { get; init; }
    public string? Titolo { get; init; }
    public string? Percorso { get; init; }
    public string? NomeFile { get; init; }
    public DateTime? ValidoFinoA { get; init; }
    public DateTime? DataUltimoDownload { get; init; }
    public int? IdInsediamento { get; init; }
    public string? Insediamento { get; init; }
    public bool? IsPositivo { get; init; }
    public string? NomeCampione { get; init; }
    public DateTime? DataPrelievo { get; init; }
    public string? VerbalePrelievo { get; init; }
    public string? LuogoPrelievo { get; init; }
    public string? PuntoPrelievo { get; init; }
    public bool ByCliente { get; init; }
    public string? Filtri { get; init; }
    public DateTime? DataFirma { get; init; }
    /// <summary>Id ArcLab del cliente proprietario (Marea.Clienti.IDArcLab).</summary>
    public int? IdClienteArcLab { get; init; }
}

/// <summary>Aree dei documenti come su Palomar, ricavate dal tipo (il campo Filtri contiene i tag del cliente, non l'area).</summary>
public static class AreeDocumenti
{
    public const string Laboratorio = "Laboratorio";
    public static readonly string[] Tutte = [Laboratorio, "HACCP", "Sicurezza", "Formazione", "Privacy", "Legionella", "Piscine", "Altro"];

    public static string Area(string? tipo)
    {
        var t = (tipo ?? "").Trim().ToUpperInvariant();
        if (t.StartsWith("CERTIFICATO") || t == "OFFERTA") return Laboratorio;
        if (t.StartsWith("HACCP")) return "HACCP";
        if (t.StartsWith("SICUREZZA")) return "Sicurezza";
        if (t.StartsWith("ATTESTATO")) return "Formazione";
        if (t.StartsWith("PRIVACY")) return "Privacy";
        if (t.StartsWith("PREVENZIONE LEGIONELLOSI")) return "Legionella";
        if (t.StartsWith("PISCINE")) return "Piscine";
        return "Altro";
    }

    /// <summary>Certificati: per loro serve la firma digitale (DataFirma) e non si mostra la scadenza.</summary>
    public static bool Certificato(string? tipo) => (tipo ?? "").Trim().StartsWith("Certificato", StringComparison.OrdinalIgnoreCase);
}
