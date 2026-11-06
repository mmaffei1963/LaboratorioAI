using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LaboratorioAI.Tracciabilita.Dati;

public static class EstensioniServizi
{
    /// <summary>
    /// Registra il database «Tracciabilita». Il sito deve registrare a parte
    /// <c>IContestoAzienda</c> (chi sta lavorando e per quale azienda).
    /// </summary>
    public static IServiceCollection AggiungiTracciabilitaDati(this IServiceCollection servizi, string stringaConnessione)
    {
        if (string.IsNullOrWhiteSpace(stringaConnessione))
            throw new InvalidOperationException("Manca la stringa di connessione «Tracciabilita».");

        servizi.AddDbContext<ContestoTracciabilita>(o => o
            .UseSqlServer(stringaConnessione, s => s
                .MigrationsHistoryTable("__EFMigrationsHistory", ContestoTracciabilita.Schema))
            .AddInterceptors(new ContestoSessioneInterceptor()));

        servizi.AddScoped<FabbricaContestoGestore>();

        return servizi;
    }
}
