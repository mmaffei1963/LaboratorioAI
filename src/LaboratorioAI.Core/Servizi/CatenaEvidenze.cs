using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Calcolo e verifica delle impronte concatenate del registro delle evidenze (Approvazione Condizioni, 09/10/2026).
/// Impronta = SHA-256 (esadecimale maiuscolo) del testo:
///   ImprontaPrecedente | IdRichiesta | IdCliente | Tipo | DataUtc (ISO 8601, 7 decimali) | NomeUtente | IndirizzoIp | UserAgent | Dati
/// con «|» come separatore e campi vuoti per i null. L'Id della riga non entra (si conosce solo dopo l'inserimento):
/// l'ordine è dato dalla catena stessa. Solo logica, nessun accesso ai dati.
/// </summary>
public static class CatenaEvidenze
{
    /// <summary>Impronta «precedente» della prima riga del registro.</summary>
    public static readonly string Origine = new('0', 64);

    public static string Calcola(EventoApprovazione e) =>
        Sha256(string.Join("|",
            e.ImprontaPrecedente,
            e.IdRichiestaApprovazione?.ToString(CultureInfo.InvariantCulture) ?? "",
            e.IdClienteArcLab?.ToString(CultureInfo.InvariantCulture) ?? "",
            e.Tipo,
            DateTime.SpecifyKind(e.DataUtc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture),
            e.NomeUtente ?? "",
            e.IndirizzoIp ?? "",
            e.UserAgent ?? "",
            e.Dati ?? ""));

    /// <summary>SHA-256 di un testo UTF-8, in esadecimale maiuscolo.</summary>
    public static string Sha256(string testo) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(testo)));

    /// <summary>SHA-256 di un contenuto binario (es. PDF), in esadecimale maiuscolo.</summary>
    public static string Sha256(byte[] dati) => Convert.ToHexString(SHA256.HashData(dati));

    /// <summary>Esito della verifica: righe controllate e, se rotta, la prima riga che non torna.</summary>
    public sealed record EsitoVerifica(int RigheControllate, long? PrimaRigaErrata, string? Motivo)
    {
        public bool Integra => PrimaRigaErrata is null;
    }

    /// <summary>
    /// Verifica l'intera catena: le righe devono arrivare in ordine di Id. Per ogni riga ricalcola l'impronta e controlla
    /// che la «precedente» coincida con l'impronta della riga prima.
    /// </summary>
    public static EsitoVerifica Verifica(IEnumerable<EventoApprovazione> righeInOrdine)
    {
        var attesaPrecedente = Origine;
        var n = 0;
        foreach (var r in righeInOrdine)
        {
            n++;
            if (!string.Equals(r.ImprontaPrecedente, attesaPrecedente, StringComparison.OrdinalIgnoreCase))
                return new EsitoVerifica(n, r.IdEventoApprovazione, "l'impronta precedente non coincide con la riga prima: una riga è stata tolta o inserita");
            if (!string.Equals(Calcola(r), r.Impronta, StringComparison.OrdinalIgnoreCase))
                return new EsitoVerifica(n, r.IdEventoApprovazione, "il contenuto della riga non corrisponde alla sua impronta: la riga è stata modificata");
            attesaPrecedente = r.Impronta;
        }
        return new EsitoVerifica(n, null, null);
    }
}

/// <summary>
/// Clausole da approvare specificamente secondo la Rev. 19 (Specifica v0.4 §5, Casella 2): usate per precompilare
/// l'elenco di una nuova revisione. Restano modificabili finché la revisione è in bozza.
/// </summary>
public static class ClausoleCondizioniPredefinite
{
    public static readonly IReadOnlyList<(string Articolo, string ContenutoBreve)> Rev19 = new[]
    {
        ("3.3", "rinnovo tacito annuale dell'Offerta e sua revisione con preavviso di 30 giorni"),
        ("4.2", "conferma dei dati dell'Ordine di Accettazione in assenza di segnalazione prima dell'inizio delle prove e comunque entro 24 ore"),
        ("5.4", "esclusione di responsabilità per prelievo, conservazione, trasporto e dati forniti dal Cliente"),
        ("7.3", "esclusione di responsabilità per il mancato ricevimento della comunicazione anticipata dei risultati fuori limite"),
        ("8.2", "limitazione di responsabilità per le prove in subappalto"),
        ("9.1", "pubblicazione nel Portale clienti quale consegna del Rapporto di Prova"),
        ("9.4", "natura indicativa dei tempi di consegna"),
        ("9.8", "Portale quale servizio di cortesia, onere di scaricare i Rapporti di Prova entro 30 giorni ed esclusione di responsabilità per indisponibilità dello storico"),
        ("11.2", "termine di 10 giorni per la contestazione dei risultati"),
        ("13.2", "esclusioni di responsabilità"),
        ("13.3", "limitazione del risarcimento"),
        ("13.4", "manleva"),
        ("14.2", "sospensione del servizio e dell'accesso al Portale per ritardato pagamento"),
        ("18.1", "foro esclusivo di Rimini"),
    };
}
