namespace LaboratorioAI.Core.Entita;

/// <summary>Stato di una proposta di controllo verso ArcLab (via import supervisionato).</summary>
public enum StatoProposta { Bozza = 0, DaApprovare = 1, Approvato = 2, Trasmesso = 3, Importato = 4 }

/// <summary>
/// Proposta di un nuovo controllo, creata da consulente/cliente. NON scrive su ArcLab:
/// è una copia di staging da cui si genera un XML per l'import supervisionato (routine Palomar).
/// Il proponente indica solo il NOME e sceglie i METODI; il prezzo si somma dai metodi scelti.
/// </summary>
public class PropostaControllo
{
    public int IdPropostaControllo { get; set; }

    /// <summary>Chiave locale stabile (GUID) per idempotenza dell'import.</summary>
    public string CodiceLocale { get; set; } = Guid.NewGuid().ToString("N");

    public StatoProposta Stato { get; set; } = StatoProposta.Bozza;

    /// <summary>Nome del controllo proposto (Controllo.Controllo).</summary>
    public string? Controllo { get; set; }
    public string? Note { get; set; }

    /// <summary>Se valorizzato, è la MODIFICA del controllo ArcLab con questo IdControllo (non un nuovo controllo).</summary>
    public int? IdControlloEsistenteArcLab { get; set; }

    /// <summary>Chi propone: "consulente" o "cliente".</summary>
    public string? Origine { get; set; }

    public DateTime? DataTrasmissione { get; set; }
    public string? NomeFileTracciato { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public int? IdUtente { get; set; }

    public List<PropostaRigaMetodo> Righe { get; set; } = new();
}

/// <summary>Una riga metodo della proposta: un metodo scelto dal catalogo ArcLab (snapshot).</summary>
public class PropostaRigaMetodo
{
    public int IdPropostaRigaMetodo { get; set; }
    public int IdPropostaControllo { get; set; }
    public PropostaControllo? Proposta { get; set; }

    public int Progressivo { get; set; }
    public int IdMetodoArcLab { get; set; }
    /// <summary>Descrizione metodo (NomeParametro / RifMetodo).</summary>
    public string? Metodo { get; set; }
    /// <summary>Denominazione della riga (da rigametodo; non modificabile dal proponente).</summary>
    public string? DenominazioneRiga { get; set; }
    /// <summary>Riferimento bibliografico del metodo (metodo.RifBiblio).</summary>
    public string? RifBiblio { get; set; }
    /// <summary>Costo del metodo (snapshot di metodo.CostoMetodoEuro).</summary>
    public decimal? CostoMetodoEuro { get; set; }
    /// <summary>Sconto riga (rigametodo.ScontoRiga). Al momento della proposta = 0 (lo imposta il laboratorio).</summary>
    public double ScontoRiga { get; set; }
    public int? IdUnitaArcLab { get; set; }
    public string? UnitaMisura { get; set; }
    /// <summary>Laboratorio esterno (rigametodo.IdLabEsterno → LabEsterno).</summary>
    public int? IdLabEsternoArcLab { get; set; }
    public string? LabEsterno { get; set; }
    /// <summary>Cifre significative (rigametodo.Arrotonda).</summary>
    public int? CifreSignificative { get; set; }
    public int? MaxDecimali { get; set; }
}
