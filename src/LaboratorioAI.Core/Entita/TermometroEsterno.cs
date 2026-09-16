namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Termometro di un cliente NON codificato in ArcLab: storico e scadenziario delle
/// tarature, importato dal foglio Excel «TARATURE» e poi gestito in applicazione.
/// </summary>
public class TermometroEsterno
{
    public int IdTermometroEsterno { get; set; }

    /// <summary>N° rapporto di prova (identifica anche il "Cod.Termometro" in etichetta).</summary>
    public int? NumeroRapporto { get; set; }
    public string? Tipologia { get; set; }
    public DateTime? DataTaratura { get; set; }
    public DateTime? DataScadenza { get; set; }
    public string? Cliente { get; set; }
    public string? Riferimento { get; set; }
    public string? Note { get; set; }
    public DateTime? DataConsegna { get; set; }
    public string? OperatoreConsegna { get; set; }
    public string? RifFattura { get; set; }
    /// <summary>Già fatturato (colonna «FATT» = «Sì» nel foglio). L'amministrazione lo imposta al momento della fattura.</summary>
    public bool Fatturato { get; set; }

    /// <summary>Se true, il termometro non viene considerato nei filtri/valutazioni per scadenza.</summary>
    public bool EscludiScadenze { get; set; }

    /// <summary>Se il termometro proviene da una taratura eseguita in app (modulo Taratura), il suo Id.</summary>
    public int? IdTaraturaOrigine { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public int? IdUtente { get; set; }
}
