namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>
/// Dove finiscono i documenti della manualistica (11/11/2026).
///
/// Stessa impostazione di DVR e piscine: il file sta sul server, nel
/// database restano percorso e impronta. Un documento consegnato a un
/// cliente deve poter essere ritrovato uguale anni dopo, e un database
/// pieno di .docx non si gestisce.
/// </summary>
public class OpzioniManualistica
{
    public const string Sezione = "Manualistica";

    /// <summary>
    /// Cartella di destinazione dei documenti prodotti. Sotto si crea
    /// una cartella per check list: <c>&lt;Id&gt;_&lt;ragione sociale&gt;</c>.
    /// </summary>
    public string CartellaArchivio { get; set; } = "";
}
