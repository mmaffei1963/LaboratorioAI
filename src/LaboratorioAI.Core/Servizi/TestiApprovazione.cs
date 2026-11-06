using System.Text.Json;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Testi delle tre caselle della pagina di approvazione (Specifica v0.4 §5, 10/10/2026). Unica fonte per la pagina del
/// Portale, il registro delle evidenze e la Ricevuta: il testo approvato è esattamente quello generato qui.
/// </summary>
public static class TestiApprovazione
{
    public static string Titolo(string revisione, string? offerta) =>
        $"Approvazione delle Condizioni Generali di Fornitura {revisione}" + (offerta is null ? "" : $" e accettazione dell'Offerta n. {offerta}");

    public const string Sottotitolo = "Si fa una volta per ogni revisione delle Condizioni. Nessuna firma è necessaria sui singoli campioni.";

    /// <summary>Casella 1 – accettazione (variante con o senza Offerta).</summary>
    public static string Casella1(string revisione, string? offerta) => offerta is not null
        ? $"Accetto l'Offerta n. {offerta}, i prezzi, i metodi e i tempi in essa riportati, e dichiaro di aver letto e di accettare integralmente le Condizioni Generali di Fornitura MARE.A s.r.l. {revisione}, che formano parte integrante del contratto. Prendo atto che il Laboratorio non predispone piani di campionamento e non svolge attività di campionamento accreditata, che la scelta di unità, punti e momento del prelievo spetta al Cliente (Art. 5) e che i risultati si riferiscono esclusivamente al campione così come ricevuto. Autorizzo l'affidamento in subappalto delle prove indicate come tali nell'Offerta (Art. 8.2) e dichiaro di aver preso visione dell'informativa sul trattamento dei dati personali."
        : $"Dichiaro di aver letto e di accettare integralmente le Condizioni Generali di Fornitura MARE.A s.r.l. {revisione}, che si applicano a tutti i Campioni consegnati al Laboratorio. Prendo atto che il Laboratorio non predispone piani di campionamento e non svolge attività di campionamento accreditata, che la scelta di unità, punti e momento del prelievo spetta al Cliente (Art. 5) e che i risultati si riferiscono esclusivamente al campione così come ricevuto. Autorizzo l'affidamento in subappalto delle prove indicate come tali nell'Ordine di Accettazione (Art. 8.2) e dichiaro di aver preso visione dell'informativa sul trattamento dei dati personali.";

    /// <summary>Casella 2 – introduzione all'elenco delle clausole.</summary>
    public static string Casella2Introduzione(string revisione) =>
        $"Ai sensi e per gli effetti degli artt. 1341 e 1342 del codice civile, dichiaro di aver letto attentamente e di approvare specificamente le seguenti clausole delle Condizioni Generali di Fornitura {revisione}:";

    /// <summary>Riga dell'elenco della Casella 2.</summary>
    public static string RigaClausola(ClausolaCondizioni c) => $"Art. {c.Articolo} – {c.ContenutoBreve}";

    /// <summary>Casella 3 – poteri (con i dati dichiarati dal Firmatario).</summary>
    public static string Casella3(string nomeCognome, string qualifica, string? ragioneSociale, string? partitaIva, string email) =>
        $"Dichiaro di essere {nomeCognome}, {qualifica}, e di avere i poteri per impegnare {ragioneSociale ?? "il Cliente"}" +
        (string.IsNullOrWhiteSpace(partitaIva) ? "" : $" (P. IVA {partitaIva})") +
        $". Sono consapevole che dell'approvazione verrà conservata evidenza e che la ricevuta sarà inviata all'indirizzo {email}.";

    /// <summary>Spiegazione accanto al campo del codice (15/10/2026): il codice è la firma elettronica dell'approvazione.</summary>
    public static string FirmaConCodice(string emailMascherata) =>
        $"Per firmare elettronicamente l'approvazione, e in particolare l'approvazione specifica delle clausole elencate al punto 3, " +
        $"inserisci il codice che ti abbiamo inviato all'indirizzo {emailMascherata}. Il codice vale {CodiceOtp.ValiditaMinuti} minuti.";

    public static string OggettoEmailCodice(string revisione) => $"Codice per l'approvazione delle Condizioni Generali {revisione} – MARE.A";

    /// <summary>Corpo dell'e-mail con il codice (HTML semplice; il codice non compare nell'oggetto).</summary>
    public static string CorpoEmailCodice(string codice, string revisione, string? ragioneSociale, string nomeCognome)
    {
        static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#1f2937;max-width:560px">
            <p>Gentile {H(nomeCognome)},</p>
            <p>questo è il codice per firmare l'approvazione delle Condizioni Generali di Fornitura MARE.A s.r.l. {H(revisione)}
            per conto di <strong>{H(ragioneSociale ?? "il Cliente")}</strong>, comprese le clausole approvate specificamente ai sensi
            degli artt. 1341 e 1342 del codice civile:</p>
            <p style="font-size:28px;font-weight:bold;letter-spacing:6px;margin:18px 0">{H(codice)}</p>
            <p>Inseriscilo nella pagina di approvazione entro {CodiceOtp.ValiditaMinuti} minuti. Inserendolo firmi elettronicamente l'approvazione.</p>
            <p style="color:#6b7280;font-size:12px">Se non hai richiesto tu questo codice, ignora questa e-mail e avvisaci rispondendo a refertazione@mare-a.com.
            Non comunicare il codice a nessuno: MARE.A non te lo chiederà mai per telefono.</p>
            <p style="color:#6b7280;font-size:12px">MARE.A s.r.l. · Via E. Toti, 2 · 47841 Cattolica (RN) · Laboratorio accreditato ACCREDIA n° 00729</p>
            </div>
            """;
    }

    /// <summary>Impronta dei testi approvati: le tre caselle e l'elenco delle clausole, nell'ordine mostrato.</summary>
    public static string Impronta(string casella1, string casella2Introduzione, IEnumerable<string> clausole, string casella3) =>
        CatenaEvidenze.Sha256(string.Join("\n", new[] { casella1, casella2Introduzione }.Concat(clausole).Append(casella3)));
}

/// <summary>
/// Controllo minimo di una firma grafica (modalità D): non vuota né ridotta a un punto o a un trattino.
/// Tracciato in JSON: { "puntatore": "mouse|touch|pen", "larghezza": n, "altezza": n, "tratti": [ [ [x, y, ms, pressione], … ], … ] }.
/// </summary>
public static class ControlloFirmaGrafica
{
    public const int PuntiMinimi = 12;
    public const double DurataMinimaMs = 400;
    public const double LarghezzaMinima = 40;
    public const double AltezzaMinima = 12;
    public const int ImmagineMassimaByte = 600_000;

    public sealed record Esito(bool Valida, string? Motivo, string? Puntatore, int Larghezza, int Altezza);

    public static Esito Verifica(string? tracciatoJson, byte[]? png)
    {
        if (png is null || png.Length < 8 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47)
            return new Esito(false, "immagine della firma mancante o non valida", null, 0, 0);
        if (png.Length > ImmagineMassimaByte) return new Esito(false, "immagine della firma troppo grande", null, 0, 0);
        if (string.IsNullOrWhiteSpace(tracciatoJson)) return new Esito(false, "firma vuota", null, 0, 0);
        try
        {
            using var doc = JsonDocument.Parse(tracciatoJson);
            var radice = doc.RootElement;
            var puntatore = radice.TryGetProperty("puntatore", out var p) ? p.GetString() : null;
            var larghezza = radice.TryGetProperty("larghezza", out var l) && l.TryGetInt32(out var lv) ? lv : 0;
            var altezza = radice.TryGetProperty("altezza", out var a) && a.TryGetInt32(out var av) ? av : 0;
            int punti = 0; double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            double minT = double.MaxValue, maxT = double.MinValue;
            foreach (var tratto in radice.GetProperty("tratti").EnumerateArray())
                foreach (var pt in tratto.EnumerateArray())
                {
                    var x = pt[0].GetDouble(); var y = pt[1].GetDouble(); var t = pt[2].GetDouble();
                    punti++;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    minT = Math.Min(minT, t); maxT = Math.Max(maxT, t);
                }
            if (punti < PuntiMinimi || maxT - minT < DurataMinimaMs || maxX - minX < LarghezzaMinima || maxY - minY < AltezzaMinima)
                return new Esito(false, "firma troppo breve o non leggibile", puntatore, larghezza, altezza);
            return new Esito(true, null, puntatore, larghezza, altezza);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or FormatException)
        {
            return new Esito(false, "tracciato della firma non leggibile", null, 0, 0);
        }
    }
}
