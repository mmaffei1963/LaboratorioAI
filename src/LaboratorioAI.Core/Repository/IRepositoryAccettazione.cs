using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Dati di riferimento ArcLab (SOLA LETTURA) per la maschera di accettazione.</summary>
public interface IRepositoryAccettazione
{
    /// <summary>Insediamenti di un cliente, con la città.</summary>
    Task<IReadOnlyList<VoceAnagrafica>> InsediamentiPerClienteAsync(int idCliente, CancellationToken ct = default);

    /// <summary>Commesse di uno specifico insediamento, con l'IdControllo/controllo associato e il prezzo.</summary>
    Task<IReadOnlyList<VoceCommessa>> CommessePerInsediamentoAsync(int idInsediamento, CancellationToken ct = default);

    /// <summary>Tecniche di prelievo (tabella ArcLab TecnicaPrelievo).</summary>
    Task<IReadOnlyList<VoceAnagrafica>> TecnichePrelievoAsync(CancellationToken ct = default);

    /// <summary>Mappa matrice→tecnica di prelievo (Matrice.Id_IOCampionamento): Id = IdMatrice, Nome = Tecnica.</summary>
    Task<IReadOnlyList<VoceAnagrafica>> MatriciTecnicaAsync(CancellationToken ct = default);

    /// <summary>Imballaggi (tabella ArcLab Imballaggio).</summary>
    Task<IReadOnlyList<VoceAnagrafica>> ImballaggiAsync(CancellationToken ct = default);

    /// <summary>Prelevatori (attività 1) e ricevitori (attività 2), con l'IdMansione usato da ArcLab.</summary>
    Task<IReadOnlyList<VoceOperatore>> OperatoriAsync(CancellationToken ct = default);

    /// <summary>Termometri di ricevimento (tabella ArcLab TermometriRicevimento).</summary>
    Task<IReadOnlyList<VoceAnagrafica>> TermometriAsync(CancellationToken ct = default);

    /// <summary>Laboratori esterni (tabella ArcLab LabEsterno): nome + indirizzo.</summary>
    Task<IReadOnlyList<VoceAnagrafica>> LaboratoriEsterniAsync(CancellationToken ct = default);

    /// <summary>Codice contabilità del cliente (Clienti.CodContabilita), prefisso del CodLotto.</summary>
    Task<string?> CodContabilitaAsync(int idCliente, CancellationToken ct = default);

    /// <summary>Intestazione MARE.A (tabella ArcLab Marchi) per i documenti.</summary>
    Task<IntestazioneMarea> IntestazioneAsync(CancellationToken ct = default);

    /// <summary>Un laboratorio esterno con nome e indirizzo separati.</summary>
    Task<LabEsternoInfo?> LabEsternoAsync(int idLabEsterno, CancellationToken ct = default);

    /// <summary>Indirizzo completo di cliente/insediamento (per la catena di custodia).</summary>
    Task<IndirizzoDestinatario?> IndirizzoDestinatarioAsync(int idInsediamento, CancellationToken ct = default);

    /// <summary>Righe metodo (rigametodo) di un controllo, da caricare come prove del campione.</summary>
    Task<IReadOnlyList<RigaMetodoControllo>> RigheMetodoPerControlloAsync(int idControllo, CancellationToken ct = default);

    /// <summary>Verifica (sola lettura) che i riferimenti ArcLab dell'accettazione esistano ancora.</summary>
    Task<EsitoVerificaImportabilita> VerificaImportabilitaAsync(RiferimentiDaVerificare rif, CancellationToken ct = default);
}

/// <summary>Riferimenti ArcLab da verificare prima dell'import del tracciato.</summary>
public record RiferimentiDaVerificare
{
    public int? IdCliente { get; init; }
    public int? IdInsediamento { get; init; }
    public int? IdCommessa { get; init; }
    public int? IdPrelevatore { get; init; }
    public int? IdRicevitore { get; init; }
    public IReadOnlyList<int> Matrici { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> Termometri { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> LabEsterni { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> RigheMetodo { get; init; } = Array.Empty<int>();
}

/// <summary>Esito della verifica di importabilità: se non ci sono problemi, l'XML aggancerà tutto.</summary>
public record EsitoVerificaImportabilita(bool Ok, IReadOnlyList<string> Problemi);

/// <summary>Commessa con il controllo associato.</summary>
public record VoceCommessa
{
    public int Id { get; init; }
    public string Nome { get; init; } = "";
    public int? IdControllo { get; init; }
    public string? Controllo { get; init; }
    public decimal? Prezzo { get; init; }
}

/// <summary>Riga metodo di un controllo (unità della prova; la chiave è IdRigametodo).</summary>
public record RigaMetodoControllo
{
    public int IdRigametodo { get; init; }
    public int? IdMetodo { get; init; }
    public int? IdControllo { get; init; }
    public string? DenominazioneRiga { get; init; }
    public string? Parametro { get; init; }
    public string? LaboratorioEsterno { get; init; }
}

/// <summary>Operatore (prelevatore/ricevitore): l'Id è l'IdMansione atteso da ArcLab.</summary>
public record VoceOperatore
{
    public int Id { get; init; }
    public string Nome { get; init; } = "";
    /// <summary>1 = prelevatore, 2 = ricevitore.</summary>
    public int Attivita { get; init; }
}
