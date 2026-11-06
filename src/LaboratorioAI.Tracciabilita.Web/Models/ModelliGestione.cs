using System.ComponentModel.DataAnnotations;
using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class ModelloImpostaPassword
{
    public string Codice { get; set; } = "";
    public string? Nome { get; set; }

    [Required(ErrorMessage = "Scegli una password.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Ripeti la password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Le due password non coincidono.")]
    public string? Conferma { get; set; }
}

public class ModelloNuovaAzienda
{
    [Required(ErrorMessage = "Indica la ragione sociale."), StringLength(200)]
    [Display(Name = "Ragione sociale")]
    public string? RagioneSociale { get; set; }

    [StringLength(20)]
    [Display(Name = "Partita IVA")]
    public string? PartitaIva { get; set; }

    [EmailAddress(ErrorMessage = "L'e-mail non sembra corretta.")]
    [Display(Name = "E-mail dell'azienda")]
    public string? Email { get; set; }

    [Display(Name = "Settore")]
    public ProfiloSettore Settore { get; set; } = ProfiloSettore.Piadine;

    [Required(ErrorMessage = "Dai un nome alla prima sede."), StringLength(100)]
    [Display(Name = "Nome della prima sede")]
    public string? NomeSede { get; set; } = "Sede principale";

    [Required(ErrorMessage = "Indica la sigla della sede (max 6 lettere o cifre)."), StringLength(12)]
    [Display(Name = "Sigla della sede")]
    public string? SiglaSede { get; set; }

    [Range(1, 50)]
    [Display(Name = "Sedi consentite")]
    public int MaxInsediamenti { get; set; } = 1;

    [Range(1, 100)]
    [Display(Name = "Utenti consentiti")]
    public int MaxUtenti { get; set; } = 6;
}

public class ModelloNuovoInsediamento
{
    [Required(ErrorMessage = "Dai un nome alla sede."), StringLength(100)]
    public string? Nome { get; set; }

    [Required(ErrorMessage = "Indica la sigla (max 6 lettere o cifre)."), StringLength(12)]
    public string? Sigla { get; set; }

    [StringLength(300)]
    public string? Indirizzo { get; set; }
}

public class ModelloNuovoUtente
{
    [Required(ErrorMessage = "Indica il nome."), StringLength(150)]
    public string? Nome { get; set; }

    [Required(ErrorMessage = "Indica l'e-mail."), EmailAddress(ErrorMessage = "L'e-mail non sembra corretta."), StringLength(256)]
    public string? Email { get; set; }

    public RuoloAzienda Ruolo { get; set; } = RuoloAzienda.AmministratoreAzienda;

    /// <summary>Vuoto = tutte le sedi.</summary>
    public int? IdInsediamento { get; set; }
}

public class RigaUtente
{
    public int IdUtente { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Attivo { get; set; }
    public DateTime? UltimoAccesso { get; set; }
    public bool PasswordImpostata { get; set; }
    public bool InvitoPendente { get; set; }
    public string Ruoli { get; set; } = "";
}

public class ModelloAziendaDettaglio
{
    public Azienda Azienda { get; set; } = null!;
    public List<Insediamento> Insediamenti { get; set; } = new();
    public List<RigaUtente> Utenti { get; set; } = new();
    public ModelloNuovoInsediamento NuovaSede { get; set; } = new();
    public ModelloNuovoUtente NuovoUtente { get; set; } = new();
    public string? LinkInvito { get; set; }
    public string? LinkInvitoPer { get; set; }
}

public class RigaAzienda
{
    public int IdAzienda { get; set; }
    public string RagioneSociale { get; set; } = "";
    public ProfiloSettore Settore { get; set; }
    public StatoServizio Stato { get; set; }
    public int Sedi { get; set; }
    public int Utenti { get; set; }
}

public class RigaRegistro
{
    public DateTime DataOra { get; set; }
    public string? Azienda { get; set; }
    public string? Utente { get; set; }
    public string Azione { get; set; } = "";
    public string? Dettaglio { get; set; }
    public string? IndirizzoIp { get; set; }
}
