using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Portale.Autenticazione;

/// <summary>Sessione associata alla richiesta in corso.</summary>
public interface IContestoSessione
{
    SessioneAttiva? Sessione { get; }
    bool Autenticato { get; }
    bool Operatore { get; }

    /// <summary>
    /// Cliente di cui la sessione può vedere i dati.
    /// Nullo per le sessioni operatore, che vedono tutti i clienti.
    /// </summary>
    int? IdCliente { get; }
}

/// <inheritdoc />
public class ContestoSessione : IContestoSessione
{
    public const string ChiaveContesto = "SessionePortale";

    private readonly IHttpContextAccessor _accessor;

    public ContestoSessione(IHttpContextAccessor accessor) => _accessor = accessor;

    public SessioneAttiva? Sessione =>
        _accessor.HttpContext?.Items.TryGetValue(ChiaveContesto, out var valore) == true
            ? valore as SessioneAttiva
            : null;

    public bool Autenticato => Sessione is not null;
    public bool Operatore => Sessione?.Operatore == true;
    public int? IdCliente => Sessione?.IdCliente;
}
