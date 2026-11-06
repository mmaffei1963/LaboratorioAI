namespace LaboratorioAI.Core.Direzione;

/// <summary>
/// 22/10/2026 (Mirko Maffei): protezione della contabilità analitica importata da AZW. Se il file AZW letto è più vecchio
/// o più povero di quanto già importato (es. copia notturna uscita con il solo esercizio precedente), la contabilità
/// analitica non si riscrive: restano i dati già presenti e l'importazione lo segnala. Il resto dell'importazione procede.
/// </summary>
public static class ControlloContabilitaAnalitica
{
    /// <summary>Di quanti giorni può tornare indietro l'ultima registrazione (registrazioni cancellate o spostate in AZW).</summary>
    public const int GiorniTolleranza = 7;

    /// <summary>Quota minima di righe rispetto a quelle già importate (sotto, si blocca).</summary>
    public const decimal QuotaMinimaRighe = 0.9m;

    /// <summary>Null = si può riscrivere; altrimenti il motivo per cui non si riscrive.</summary>
    public static string? Verifica(DateTime? ultimaAttuale, int righeAttuali, DateTime? ultimaNuova, int righeNuove)
    {
        if (righeAttuali == 0 || ultimaAttuale is null) return null;
        if (righeNuove == 0 || ultimaNuova is null)
            return $"il file AZW non contiene registrazioni (già importate {righeAttuali:N0}, fino al {ultimaAttuale:dd/MM/yyyy})";
        if (ultimaNuova.Value.Date < ultimaAttuale.Value.Date.AddDays(-GiorniTolleranza))
            return $"il file AZW arriva al {ultimaNuova:dd/MM/yyyy}, i dati già importati al {ultimaAttuale:dd/MM/yyyy}";
        if (righeNuove < righeAttuali * QuotaMinimaRighe)
            return $"il file AZW ha {righeNuove:N0} registrazioni, quelle già importate sono {righeAttuali:N0}";
        return null;
    }
}
