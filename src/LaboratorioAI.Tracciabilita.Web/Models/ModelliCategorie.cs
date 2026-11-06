using System.ComponentModel.DataAnnotations;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class RigaCategoria
{
    public int IdCategoria { get; set; }
    public string Nome { get; set; } = "";
    public string? Profilo { get; set; }
    public int Articoli { get; set; }
    public bool Attivo { get; set; }
}

public class ModelloElencoCategorie
{
    public string? Cerca { get; set; }
    public bool Tutte { get; set; }
    public List<RigaCategoria> Righe { get; set; } = new();
}

public class OpzioneProfilo
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
}

public class ModelloCategoria
{
    public int IdCategoria { get; set; }
    [Required(ErrorMessage = "Scrivi il nome della categoria.")]
    [StringLength(120)]
    [Display(Name = "Nome della categoria (matrice)")]
    public string? Nome { get; set; }
    [Display(Name = "Profilo di controllo (cosa si controlla all'arrivo)")]
    public int? IdProfiloControllo { get; set; }
    [StringLength(500)]
    public string? Note { get; set; }
    [Display(Name = "In uso (togli la spunta se non serve più)")]
    public bool Attivo { get; set; } = true;
    public int ArticoliAttivi { get; set; }
    public List<OpzioneProfilo> Profili { get; set; } = new();
}

public class RigaProfilo
{
    public int IdProfiloControllo { get; set; }
    public string Nome { get; set; } = "";
    public string Controlli { get; set; } = "";
    public int Categorie { get; set; }
    public bool Attivo { get; set; }
}

public class RigaControlloProfilo
{
    public TipoControllo Controllo { get; set; }
    public string Nome { get; set; } = "";
    public bool Presente { get; set; }
    public bool Obbligatorio { get; set; }
    [StringLength(300)]
    public string? Istruzione { get; set; }
}

public class ModelloProfilo
{
    public int IdProfiloControllo { get; set; }
    [Required(ErrorMessage = "Scrivi il nome del profilo.")]
    [StringLength(120)]
    [Display(Name = "Nome del profilo")]
    public string? Nome { get; set; }
    [Display(Name = "In uso")]
    public bool Attivo { get; set; } = true;
    public int CategorieAttive { get; set; }
    public List<RigaControlloProfilo> Controlli { get; set; } = new();
}
