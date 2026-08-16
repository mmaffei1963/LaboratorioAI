using System.Reflection;
using LaboratorioAI.Core.Servizi;

namespace LaboratorioAI.Web.Servizi;

/// <inheritdoc />
public class ServizioVersione : IServizioVersione
{
    public string Versione { get; }
    public string VersioneEstesa { get; }
    public string Ambiente { get; }

    public ServizioVersione(IWebHostEnvironment ambiente)
    {
        var assembly = Assembly.GetEntryAssembly();

        var informazionale = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        // Rimuove l'eventuale hash di commit aggiunto dal SDK
        VersioneEstesa = informazionale;
        Versione = informazionale.Split('+')[0];

        Ambiente = ambiente.EnvironmentName switch
        {
            "Production"  => "Produzione",
            "Staging"     => "Collaudo",
            "Development" => "Sviluppo",
            var altro     => altro
        };
    }
}
