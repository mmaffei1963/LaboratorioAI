namespace LaboratorioAI.Tracciabilita.Web.Servizi;

/// <summary>
/// Indirizzo con cui il sito si apre dall'esterno (per i link degli inviti e il QR delle etichette).
/// Se in configurazione c'è «Tracciabilita:UrlPubblico» (es. https://tracciato.mare-a.com) si usa quello,
/// altrimenti l'indirizzo della richiesta (in sviluppo http://localhost:5082).
/// </summary>
public static class UrlPubblico
{
    public static string Base(HttpContext http)
    {
        var configurato = http.RequestServices.GetService<IConfiguration>()?["Tracciabilita:UrlPubblico"]?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(configurato) ? $"{http.Request.Scheme}://{http.Request.Host}" : configurato;
    }
}
