using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Entita;

/// <summary>Documento DVR Legionellosi prodotto dal sistema.</summary>
public class VoceRegistroDvr
{
    public int IdVoce { get; set; }

    // Riferimenti logici ad ArcLab
    public int? IdCliente { get; set; }
    public int? IdInsediamento { get; set; }

    /// <summary>Denominazioni al momento della generazione, per leggibilità del registro.</summary>
    public string RagioneSociale { get; set; } = null!;
    public string? NomeInsediamento { get; set; }

    public string Regione { get; set; } = null!;

    public DateTime DataGenerazione { get; set; }

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }

    public string NomeFile { get; set; } = null!;
    public string? PercorsoArchivio { get; set; }

    public EsitoOperazione Esito { get; set; }
    public string? Note { get; set; }

    /// <summary>Identificativo del lotto di generazione massiva. Nullo per le generazioni singole.</summary>
    public Guid? IdLotto { get; set; }

    public string VersioneApplicazione { get; set; } = null!;
}
