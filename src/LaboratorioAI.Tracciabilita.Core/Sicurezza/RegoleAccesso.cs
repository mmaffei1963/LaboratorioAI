namespace LaboratorioAI.Tracciabilita.Core.Sicurezza;

/// <summary>Regole sulle password scelte dagli utenti. Contano la lunghezza e l'assenza di scelte ovvie.</summary>
public static class RegolePassword
{
    public const int LunghezzaMinima = 12;

    private static readonly string[] _troppoComuni =
    {
        "password1234", "passwordpassword", "123456789012", "1234567890123", "qwertyuiop12",
        "qwertyuiopas", "administrator", "tracciato1234", "mareamarea12", "laboratorio12"
    };

    /// <summary>Restituisce i motivi per cui la password non va bene (vuoto = va bene).</summary>
    public static IReadOnlyList<string> Valida(string? password, string? email = null)
    {
        var errori = new List<string>();

        if (string.IsNullOrEmpty(password) || password.Length < LunghezzaMinima)
        {
            errori.Add($"La password deve avere almeno {LunghezzaMinima} caratteri.");
            return errori;
        }

        if (password.Distinct().Count() < 5)
            errori.Add("La password usa troppo pochi caratteri diversi.");

        if (password.All(char.IsDigit))
            errori.Add("La password non può essere fatta solo di numeri.");

        if (_troppoComuni.Contains(password.ToLowerInvariant()))
            errori.Add("La password è troppo comune.");

        var parteLocale = email?.Split('@')[0];
        if (!string.IsNullOrWhiteSpace(parteLocale) && parteLocale.Length >= 4 &&
            password.Contains(parteLocale, StringComparison.OrdinalIgnoreCase))
            errori.Add("La password non deve contenere il nome della tua e-mail.");

        return errori;
    }
}

/// <summary>
/// Blocco temporaneo dell'accesso dopo troppi tentativi falliti. Funzioni pure, senza database,
/// così si possono provare con i test.
/// </summary>
public static class PoliticaBloccoAccesso
{
    public const int TentativiMassimi = 5;
    public static readonly TimeSpan DurataBlocco = TimeSpan.FromMinutes(15);

    public static bool EBloccato(DateTime? bloccatoFinoUtc, DateTime adessoUtc) =>
        bloccatoFinoUtc is { } fino && fino > adessoUtc;

    public static int MinutiRimasti(DateTime? bloccatoFinoUtc, DateTime adessoUtc) =>
        bloccatoFinoUtc is { } fino && fino > adessoUtc
            ? (int)Math.Ceiling((fino - adessoUtc).TotalMinutes)
            : 0;

    /// <summary>Dopo una password sbagliata: un tentativo in più, oppure blocco se si è al massimo.</summary>
    public static (int Tentativi, DateTime? BloccatoFinoUtc) DopoFallimento(int tentativiFalliti, DateTime adessoUtc)
    {
        var tentativi = tentativiFalliti + 1;
        return tentativi >= TentativiMassimi
            ? (0, adessoUtc + DurataBlocco)
            : (tentativi, null);
    }

    public static (int Tentativi, DateTime? BloccatoFinoUtc) DopoSuccesso() => (0, null);
}
