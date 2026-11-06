using System.ComponentModel.DataAnnotations;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class ModelloAccesso
{
    [Required(ErrorMessage = "Inserisci la tua e-mail.")]
    [EmailAddress(ErrorMessage = "L'e-mail non sembra corretta.")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Inserisci la password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    public string? ReturnUrl { get; set; }
}
