using System.Security.Cryptography;
using System.Text;

namespace LaboratorioAI.Tracciabilita.Core.Sicurezza;

/// <summary>
/// Link monouso con cui una persona sceglie la propria password (primo accesso o reimpostazione).
/// Nel database resta solo l'impronta del codice: chi leggesse il database non può ricostruire il link.
/// </summary>
public static class InvitoUtente
{
    public static readonly TimeSpan Durata = TimeSpan.FromDays(7);

    /// <summary>Crea un codice casuale (da dare alla persona) e la sua impronta (da conservare).</summary>
    public static (string Codice, string Impronta, DateTime ScadenzaUtc) Crea(DateTime adesso)
    {
        var codice = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (codice, ImprontaDi(codice), adesso + Durata);
    }

    public static string ImprontaDi(string codice) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codice ?? "")));

    public static bool Valido(string? improntaSalvata, DateTime? scadenzaUtc, string codice, DateTime adesso)
    {
        if (string.IsNullOrEmpty(improntaSalvata) || scadenzaUtc is null || string.IsNullOrEmpty(codice)) return false;
        if (adesso > scadenzaUtc) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(improntaSalvata), Encoding.ASCII.GetBytes(ImprontaDi(codice)));
    }

    private static string Base64Url(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
