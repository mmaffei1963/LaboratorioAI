using System.Security.Cryptography;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Codice monouso (OTP) inviato per e-mail al Firmatario per firmare elettronicamente l'approvazione specifica delle
/// clausole delle Condizioni Generali (15/10/2026, dopo Cass. Sez. III ord. 20945/2026: la sola spunta non basta per
/// gli artt. 1341-1342 c.c.). Nel registro delle evidenze resta solo l'impronta del codice, mai il codice in chiaro.
/// </summary>
public static class CodiceOtp
{
    /// <summary>Minuti di validità del codice.</summary>
    public const int ValiditaMinuti = 10;
    /// <summary>Tentativi sbagliati ammessi per ogni codice: poi ne serve uno nuovo.</summary>
    public const int TentativiMassimi = 5;
    /// <summary>Codici inviabili per la stessa richiesta in un'ora.</summary>
    public const int InviiMassimiOra = 5;
    /// <summary>Secondi da attendere prima di poter chiedere un nuovo codice.</summary>
    public const int AttesaReinvioSecondi = 60;

    /// <summary>Codice di 6 cifre (generatore crittografico).</summary>
    public static string Genera() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>Valore casuale che rende l'impronta diversa per ogni invio.</summary>
    public static string Sale() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    /// <summary>Impronta del codice legata alla richiesta: è quella che si conserva.</summary>
    public static string Impronta(string codice, string sale, int idRichiesta) =>
        CatenaEvidenze.Sha256($"{sale}|{idRichiesta}|{Normalizza(codice)}");

    /// <summary>Toglie spazi e trattini che l'utente può aver digitato o incollato.</summary>
    public static string Normalizza(string? codice) =>
        new((codice ?? "").Where(char.IsDigit).ToArray());

    /// <summary>Confronto a tempo costante delle due impronte.</summary>
    public static bool Corrisponde(string codiceInserito, string sale, int idRichiesta, string improntaAttesa)
    {
        var a = System.Text.Encoding.ASCII.GetBytes(Impronta(codiceInserito, sale, idRichiesta));
        var b = System.Text.Encoding.ASCII.GetBytes(improntaAttesa ?? "");
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>Indirizzo mascherato per pagina e Ricevuta: «mario.rossi@cliente.it» → «ma***@cliente.it».</summary>
    public static string Maschera(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "—";
        var e = email.Trim();
        var at = e.IndexOf('@');
        if (at <= 0) return "***";
        var nome = e[..at];
        return (nome.Length <= 2 ? nome[..1] : nome[..2]) + "***" + e[at..];
    }
}
