using System.ComponentModel.DataAnnotations;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class RigaQuadro
{
    public int IdOrdine { get; set; }
    public string Numero { get; set; } = "";
    public DateOnly DataOrdine { get; set; }
    public DateOnly? DataConsegnaPrevista { get; set; }
    public string Fornitore { get; set; } = "";
    public StatoOrdine Stato { get; set; }
    public string Articoli { get; set; } = "";
}

public class ModelloQuadroConsegne
{
    /// <summary>da-consegnare (ordini inviati), bozze, ricevuti, tutti.</summary>
    public string Vista { get; set; } = "da-consegnare";
    public string? Cerca { get; set; }
    public List<RigaQuadro> Righe { get; set; } = new();
}

public class ModelloNuovoOrdine
{
    [Required(ErrorMessage = "Scegli il fornitore.")]
    [Display(Name = "Fornitore")] public int? IdFornitore { get; set; }
    [Display(Name = "Consegna prevista")] public DateOnly? DataConsegnaPrevista { get; set; }
    [Display(Name = "Note per il fornitore")] public string? Note { get; set; }

    /// <summary>Solo al primo ordine: da che numero continuare la numerazione.</summary>
    [Display(Name = "Numero del primo ordine")] public int? NumeroIniziale { get; set; }
    public bool PrimoOrdine { get; set; }
    public int NumeroProposto { get; set; }
    public List<OpzioneAnagrafica> Fornitori { get; set; } = new();
}

public class RigaOrdineVista
{
    public int IdOrdineRiga { get; set; }
    public string Articolo { get; set; } = "";
    public string? Matrice { get; set; }
    public decimal Quantita { get; set; }
    public string UnitaMisura { get; set; } = "";
    public int? Colli { get; set; }
    public string? Note { get; set; }
}

public class OpzioneArticoloOrdine
{
    public int IdArticolo { get; set; }
    public string Descrizione { get; set; } = "";
    public string UnitaMisura { get; set; } = "";
    public int? IdCategoria { get; set; }
    public bool DelFornitore { get; set; }
}

public class ModelloSchedaOrdine
{
    public int IdOrdine { get; set; }
    public string Numero { get; set; } = "";
    public DateOnly DataOrdine { get; set; }
    public DateOnly? DataConsegnaPrevista { get; set; }
    public StatoOrdine Stato { get; set; }
    public string? Note { get; set; }
    public string Fornitore { get; set; } = "";
    public string? EmailFornitore { get; set; }
    public List<RigaOrdineVista> Righe { get; set; } = new();

    // Per aggiungere una riga.
    public List<OpzioneArticoloOrdine> Articoli { get; set; } = new();
    public List<OpzioneAnagrafica> Categorie { get; set; } = new();
    public int? IdArticolo { get; set; }
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; }
    public int? Colli { get; set; }
    public int? IdCategoria { get; set; }
    public string? NoteRiga { get; set; }
}

public class ModelloStampaOrdine
{
    public string Azienda { get; set; } = "";
    public string? PartitaIvaAzienda { get; set; }
    public string Sede { get; set; } = "";
    public string? IndirizzoSede { get; set; }
    public string Numero { get; set; } = "";
    public DateOnly DataOrdine { get; set; }
    public DateOnly? DataConsegnaPrevista { get; set; }
    public string? Note { get; set; }
    public string Fornitore { get; set; } = "";
    public string? IndirizzoFornitore { get; set; }
    public string? TelefonoFornitore { get; set; }
    public List<RigaOrdineVista> Righe { get; set; } = new();
}
