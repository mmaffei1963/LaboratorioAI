namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Sigla di un laboratorio esterno ArcLab (Amministrazione → Sigle laboratori esterni), usata nel nome
/// dei certificati esterni per Palomar (…-EST-SIGLA-idLabEsterno-…). Una riga per laboratorio ArcLab.
/// Nessuna scrittura su ArcLab: l'id è solo un riferimento a LabEsterno.IdLabEsterno.
/// </summary>
public class SiglaLaboratorioEsterno
{
    public int IdSiglaLaboratorio { get; set; }

    /// <summary>LabEsterno.IdLabEsterno in ArcLab (univoco).</summary>
    public int IdLabEsternoArcLab { get; set; }

    /// <summary>Sigla nel nome file (es. SIC): solo lettere e cifre, maiuscole.</summary>
    public string Sigla { get; set; } = "";

    /// <summary>
    /// Parole che identificano il laboratorio nel certificato PDF, separate da virgola (es. "sicural").
    /// Servono a scegliere il laboratorio quando il campione ha prove affidate a più laboratori.
    /// </summary>
    public string? ParoleChiave { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public int? IdUtente { get; set; }

    /// <summary>Parole chiave normalizzate (minuscolo, senza vuoti).</summary>
    public IReadOnlyList<string> ElencoParole() =>
        (ParoleChiave ?? "")
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToLowerInvariant())
            .Where(p => p.Length > 0)
            .ToList();
}
