using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Modelli;

/// <summary>Richiesta di analisi AI su un campione.</summary>
public record RichiestaAnalisi
{
    public required int IdCampione { get; init; }
    public int? IdCertificato { get; init; }
    public ModalitaAnalisi Modalita { get; init; } = ModalitaAnalisi.Validazione;

    /// <summary>Se vero, riesegue l'analisi anche se ne esiste già una in archivio.</summary>
    public bool ForzaRiesecuzione { get; init; }
}

/// <summary>Esito di un'analisi AI.</summary>
public record EsitoAnalisi
{
    public required int IdAnalisi { get; init; }
    public required string Testo { get; init; }
    public required GiudizioAnalisi Giudizio { get; init; }
    public required DateTime DataAnalisi { get; init; }
    public int NumeroProve { get; init; }

    /// <summary>Vero se il risultato proviene dall'archivio anziché da una nuova esecuzione.</summary>
    public bool DaArchivio { get; init; }

    public string? EseguitaDa { get; init; }
}

/// <summary>Sintesi di un'analisi presente in archivio.</summary>
public record SintesiAnalisi
{
    public required int IdAnalisi { get; init; }
    public required int IdCampione { get; init; }
    public required DateTime DataAnalisi { get; init; }
    public required GiudizioAnalisi Giudizio { get; init; }
    public string? EseguitaDa { get; init; }
}

/// <summary>Esito dell'analisi massiva su più campioni.</summary>
public record EsitoAnalisiMassiva
{
    public required IReadOnlyList<RigaAnalisiMassiva> Righe { get; init; }

    public int Conformi     => Righe.Count(r => r.Giudizio == GiudizioAnalisi.Conforme);
    public int NonConformi  => Righe.Count(r => r.Giudizio == GiudizioAnalisi.NonConforme);
    public int Borderline   => Righe.Count(r => r.Giudizio == GiudizioAnalisi.Borderline);
    public int NonRiusciti  => Righe.Count(r => r.Errore is not null);
}

/// <summary>Singolo esito all'interno di un'analisi massiva.</summary>
public record RigaAnalisiMassiva
{
    public required int IdCampione { get; init; }
    public required string NumeroCampione { get; init; }
    public string? RagioneSociale { get; init; }
    public string? DenominazioneCampione { get; init; }
    public GiudizioAnalisi Giudizio { get; init; }
    public int? IdAnalisi { get; init; }
    public bool DaArchivio { get; init; }
    public string? Errore { get; init; }
}
