using System.ComponentModel.DataAnnotations;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class RigaCliente
{
    public int IdCliente { get; set; }
    public string RagioneSociale { get; set; } = "";
    public string? Citta { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public bool Attivo { get; set; }
}

public class ModelloElencoClienti
{
    public string? Cerca { get; set; }
    public bool ConNonAttivi { get; set; }
    public int Totale { get; set; }
    public List<RigaCliente> Righe { get; set; } = new();
}

public class ModelloCliente
{
    public int IdCliente { get; set; }
    [Required(ErrorMessage = "La ragione sociale è obbligatoria.")]
    [Display(Name = "Ragione sociale")] public string? RagioneSociale { get; set; }
    [Display(Name = "Partita IVA")] public string? PartitaIva { get; set; }
    [Display(Name = "Indirizzo")] public string? Indirizzo { get; set; }
    [Display(Name = "CAP")] public string? Cap { get; set; }
    [Display(Name = "Città")] public string? Citta { get; set; }
    [Display(Name = "Provincia")] public string? Provincia { get; set; }
    [Display(Name = "Telefono")] public string? Telefono { get; set; }
    [EmailAddress(ErrorMessage = "Indirizzo e-mail non valido.")]
    [Display(Name = "E-mail (serve per avvisarlo in caso di richiamo)")] public string? Email { get; set; }
    [Display(Name = "Note")] public string? Note { get; set; }
    [Display(Name = "In uso (togli la spunta se non è più un cliente)")] public bool Attivo { get; set; } = true;
}

public class RigaSpedizione
{
    public int IdSpedizione { get; set; }
    public DateOnly Data { get; set; }
    public string Cliente { get; set; } = "";
    public string? NumeroDocumento { get; set; }
    public string Lotti { get; set; } = "";
    public bool Annullata { get; set; }
}

public class ModelloElencoSpedizioni
{
    public string? Cerca { get; set; }
    public DateOnly? Dal { get; set; }
    public DateOnly? Al { get; set; }
    public int Pagina { get; set; } = 1;
    public int PerPagina { get; set; } = 50;
    public int Totale { get; set; }
    public List<RigaSpedizione> Righe { get; set; } = new();
    public int Pagine => Math.Max(1, (Totale + PerPagina - 1) / PerPagina);
}

public class OpzioneLottoSpedibile
{
    public int IdLotto { get; set; }
    public string Testo { get; set; } = "";
}

public class OpzioneCliente
{
    public int IdCliente { get; set; }
    public string Nome { get; set; } = "";
}

public class ModelloNuovaSpedizione
{
    public DateOnly Data { get; set; }
    public int? IdCliente { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateOnly? DataDocumento { get; set; }
    public string? Note { get; set; }
    public List<OpzioneCliente> Clienti { get; set; } = new();
    public List<OpzioneLottoSpedibile> Lotti { get; set; } = new();
    public int RigheVuote { get; set; } = 8;
}

public class RigaSchedaSpedizione
{
    public int IdLotto { get; set; }
    public string Lotto { get; set; } = "";
    public string Prodotto { get; set; } = "";
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; }
}

public class ModelloSchedaSpedizione
{
    public int IdSpedizione { get; set; }
    public DateOnly Data { get; set; }
    public string Cliente { get; set; } = "";
    public string? NumeroDocumento { get; set; }
    public DateOnly? DataDocumento { get; set; }
    public string? Note { get; set; }
    public bool Annullata { get; set; }
    public List<RigaSchedaSpedizione> Righe { get; set; } = new();
}
