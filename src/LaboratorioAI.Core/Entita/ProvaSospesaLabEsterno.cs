namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Prova dell'elenco ACCREDIA di un laboratorio esterno monitorato con sospensione attiva (asterisco sulla riga
/// della prova nella banca dati ACCREDIA dei laboratori di prova). Letta ogni giorno dall'Agente ACCREDIA
/// (08/10/2026). Nessuna cancellazione: quando la prova non risulta più sospesa si valorizza
/// <see cref="DataFineSospensione"/>, così resta lo storico (quando l'abbiamo vista e quando è finita).
/// La riga si riconosce da <see cref="ChiaveProva"/> (impronta di matrice + prova + norma + tecnica).
/// </summary>
public class ProvaSospesaLabEsterno
{
    public int IdProvaSospesa { get; set; }

    /// <summary>Laboratorio esterno monitorato (<see cref="AccreditamentoLabEsterno"/>).</summary>
    public int IdAccreditamento { get; set; }

    /// <summary>SHA-256 (esadecimale) del testo normalizzato di matrice, prova, norma e tecnica.</summary>
    public string ChiaveProva { get; set; } = "";

    public string? Matrice { get; set; }
    public string? Prova { get; set; }
    public string? Norma { get; set; }
    public string? Tecnica { get; set; }

    /// <summary>Primo controllo in cui la prova è risultata sospesa (UTC): non è l'inizio della sospensione per ACCREDIA.</summary>
    public DateTime DataRilevazione { get; set; }

    /// <summary>Ultimo controllo in cui la prova risultava ancora sospesa (UTC).</summary>
    public DateTime DataUltimaLettura { get; set; }

    /// <summary>Primo controllo in cui la prova non risultava più sospesa (UTC); null = sospensione in corso.</summary>
    public DateTime? DataFineSospensione { get; set; }
}
