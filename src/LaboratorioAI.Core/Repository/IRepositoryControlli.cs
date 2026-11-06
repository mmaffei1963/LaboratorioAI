namespace LaboratorioAI.Core.Repository;

/// <summary>Cataloghi ArcLab (SOLA LETTURA) per la proposta di controlli.</summary>
public interface IRepositoryControlli
{
    /// <summary>Catalogo metodi da cui il proponente sceglie (metodo + unità di misura).</summary>
    Task<IReadOnlyList<VoceMetodo>> MetodiCatalogoAsync(string? filtro, CancellationToken ct = default);

    /// <summary>Controlli ArcLab che hanno ESATTAMENTE lo stesso insieme di metodi (potenziali duplicati).</summary>
    Task<IReadOnlyList<VoceControlloEsistente>> ControlliConStessiMetodiAsync(IReadOnlyList<int> idMetodi, int? escludiIdControllo = null, CancellationToken ct = default);

    /// <summary>Controlli ATTIVI (per la modifica di un controllo esistente).</summary>
    Task<IReadOnlyList<VoceControlloEsistente>> ControlliAttiviAsync(CancellationToken ct = default);

    /// <summary>Righe metodo correnti di un controllo esistente (per precaricare la modifica).</summary>
    Task<IReadOnlyList<VoceMetodo>> RigheDiControlloAsync(int idControllo, CancellationToken ct = default);

    /// <summary>Vocabolario delle sigle di laboratorio disponibili (Sigla_Metodo non superate).</summary>
    Task<IReadOnlyList<string>> SigleDisponibiliAsync(CancellationToken ct = default);

    /// <summary>Nome corrente di un controllo ArcLab (per la guardia sul riutilizzo del nome).</summary>
    Task<string?> NomeControlloAsync(int idControllo, CancellationToken ct = default);

    /// <summary>
    /// Stato di importazione in ArcLab delle proposte, letto dal registro dbo.ImportDaLaboratorioAI.
    /// Restituisce vuoto se il registro non esiste ancora (ambiente non ancora predisposto).
    /// </summary>
    Task<IReadOnlyList<VoceImport>> StatiImportAsync(IReadOnlyList<string> codiciLocali, CancellationToken ct = default);
}

/// <summary>Riga del registro import: proposta (per CodiceLocale) → controllo creato in ArcLab.</summary>
public record VoceImport(string CodiceLocale, int IdControllo);

/// <summary>Controllo ArcLab esistente (per la segnalazione di duplicati).</summary>
public record VoceControlloEsistente(int IdControllo, string? Controllo);

/// <summary>Voce del catalogo (una configurazione di riga metodo, da rigametodo).</summary>
public record VoceMetodo
{
    public int Id { get; init; }                 // IdMetodo
    public string? Descrizione { get; init; }    // NomeParametro / RifMetodo
    public string? Acronimo { get; init; }       // metodo.Acronimo (sigla), se presente
    public string? DenominazioneRiga { get; init; }
    public string? RifBiblio { get; init; }
    /// <summary>metodo.DettagliMetodo: la denominazione usata nei Rapporti di Prova ufficiali (es. "Alluminio").</summary>
    public string? DettagliMetodo { get; init; }
    public decimal? Costo { get; init; }         // metodo.CostoMetodoEuro
    public int? IdUnita { get; init; }
    public string? UnitaMisura { get; init; }
    public int? IdLabEsterno { get; init; }
    public string? LabEsterno { get; init; }
    public int? CifreSignificative { get; init; }// rigametodo.Arrotonda
    public int? MaxDecimali { get; init; }
    public bool Accreditato { get; init; }       // metodo.Accreditato
    public string? LabEsternoFrase { get; init; }// LabEsterno.Frase (per il subappalto)
}
