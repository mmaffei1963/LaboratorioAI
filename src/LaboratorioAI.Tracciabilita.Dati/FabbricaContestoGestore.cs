using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Dati;

/// <summary>
/// Crea un contesto con i poteri del gestore. Serve per le operazioni che avvengono prima di sapere
/// per quale azienda si lavora (l'accesso, la verifica della sessione, l'avvio). Da usare solo
/// per queste operazioni, mai per rispondere a richieste di un'azienda.
/// </summary>
public sealed class FabbricaContestoGestore
{
    private readonly DbContextOptions<ContestoTracciabilita> _opzioni;

    public FabbricaContestoGestore(DbContextOptions<ContestoTracciabilita> opzioni) => _opzioni = opzioni;

    public ContestoTracciabilita Crea() => new(_opzioni, new ContestoGestore());
}
