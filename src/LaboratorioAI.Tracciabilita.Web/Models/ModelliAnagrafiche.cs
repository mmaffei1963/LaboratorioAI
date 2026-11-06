using System.ComponentModel.DataAnnotations;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class RigaFornitore
{
    public int IdFornitore { get; set; }
    public string RagioneSociale { get; set; } = "";
    public string? Citta { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public bool Attivo { get; set; }
}

public class ModelloElencoFornitori
{
    public string? Cerca { get; set; }
    public bool ConNonAttivi { get; set; }
    public int Totale { get; set; }
    public List<RigaFornitore> Righe { get; set; } = new();
}

public class ModelloFornitore
{
    public int IdFornitore { get; set; }
    [Required(ErrorMessage = "La ragione sociale è obbligatoria.")]
    [Display(Name = "Ragione sociale")] public string? RagioneSociale { get; set; }
    [Display(Name = "Partita IVA")] public string? PartitaIva { get; set; }
    [Display(Name = "Indirizzo")] public string? Indirizzo { get; set; }
    [Display(Name = "CAP")] public string? Cap { get; set; }
    [Display(Name = "Città")] public string? Citta { get; set; }
    [Display(Name = "Provincia")] public string? Provincia { get; set; }
    [Display(Name = "Telefono")] public string? Telefono { get; set; }
    [EmailAddress(ErrorMessage = "Indirizzo e-mail non valido.")]
    [Display(Name = "E-mail (per gli ordini)")] public string? Email { get; set; }
    [Display(Name = "Note")] public string? Note { get; set; }
    [Display(Name = "In uso (togli la spunta se non è più un fornitore)")] public bool Attivo { get; set; } = true;
}

public class RigaArticolo
{
    public int IdArticolo { get; set; }
    public string Descrizione { get; set; } = "";
    public string? Codice { get; set; }
    public TipoArticolo Tipo { get; set; }
    public string? Categoria { get; set; }
    public string UnitaMisura { get; set; } = "";
    public bool Attivo { get; set; }
    public string? Fornitori { get; set; }
}

public class ModelloElencoArticoli
{
    public string? Cerca { get; set; }
    public bool ConNonAttivi { get; set; }
    public int Totale { get; set; }
    public List<RigaArticolo> Righe { get; set; } = new();
}

public class OpzioneAnagrafica
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
}

public class ModelloArticolo
{
    public int IdArticolo { get; set; }
    [Required(ErrorMessage = "La descrizione è obbligatoria.")]
    [Display(Name = "Descrizione")] public string? Descrizione { get; set; }
    [Display(Name = "Codice")] public string? Codice { get; set; }
    [Display(Name = "Tipo")] public TipoArticolo Tipo { get; set; } = TipoArticolo.MateriaPrima;
    [Display(Name = "Categoria (matrice abituale)")] public int? IdCategoria { get; set; }
    [Required(ErrorMessage = "L'unità di misura è obbligatoria.")]
    [Display(Name = "Unità di misura")] public string? UnitaMisura { get; set; } = "kg";
    [Display(Name = "Biologico")] public bool Bio { get; set; }
    [Display(Name = "Note")] public string? Note { get; set; }
    [Display(Name = "In uso")] public bool Attivo { get; set; } = true;
    public List<int> FornitoriScelti { get; set; } = new();
    public List<OpzioneAnagrafica> Categorie { get; set; } = new();
    public List<OpzioneAnagrafica> Fornitori { get; set; } = new();
}
