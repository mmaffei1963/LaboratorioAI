namespace LaboratorioAI.Tracciabilita.Web.Autenticazione;

/// <summary>Nomi dei dati che la sessione porta con sé (cookie firmato e cifrato).</summary>
public static class Rivendicazioni
{
    public const string IdUtente = "tr:utente";
    public const string IdAzienda = "tr:azienda";
    public const string Gestore = "tr:gestore";
}
