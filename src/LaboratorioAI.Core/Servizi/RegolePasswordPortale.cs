namespace LaboratorioAI.Core.Servizi;

/// <summary>Password del portale clienti (17/11/2026): regole minime per la password personale.</summary>
public static class RegolePasswordPortale
{
    public const int LunghezzaMinima = 8;
    public const int LunghezzaMassima = 128;
    /// <summary>Minuti di validità del codice di recupero inviato per email.</summary>
    public const int ValiditaCodiceMinuti = 15;
    public const int TentativiMassimiCodice = 5;
    public const int AttesaReinvioSecondi = 60;
    public const int InviiMassimiOraPerCliente = 5;
    public const int InviiMassimiOraPerIndirizzo = 10;

    /// <summary>Messaggio d'errore, o null se la password nuova va bene.</summary>
    public static string? Valida(string? nuova, string? ripetuta, string? partitaIva, string? codiceCliente)
    {
        if (string.IsNullOrEmpty(nuova)) return "Scrivi la password nuova.";
        if (nuova != ripetuta) return "Le due password nuove non coincidono.";
        if (nuova.Length < LunghezzaMinima) return $"La password deve avere almeno {LunghezzaMinima} caratteri.";
        if (nuova.Length > LunghezzaMassima) return $"La password può avere al massimo {LunghezzaMassima} caratteri.";
        if (nuova.Trim().Length != nuova.Length) return "La password non può iniziare o finire con uno spazio.";
        if (Uguale(nuova, partitaIva)) return "La password nuova deve essere diversa dalla partita IVA.";
        if (Uguale(nuova, codiceCliente)) return "La password nuova deve essere diversa dal codice cliente.";
        return null;
    }

    private static bool Uguale(string a, string? b) =>
        !string.IsNullOrWhiteSpace(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
