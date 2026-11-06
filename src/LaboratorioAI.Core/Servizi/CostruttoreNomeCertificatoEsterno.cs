using System.Text.RegularExpressions;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Compone il nome file di un certificato esterno secondo la convenzione MARE.A:
/// <c>GG-MM-AA-lotto-campione-idInsediamento-cliente-prodotto-matrice-idMatrice-EST-LAB-idLabEsterno-RdP-(AVV|STD)</c>.
/// GG-MM-AA = data di ricezione del campione. idMatrice e idLabEsterno servono a Palomar per il portale:
/// se mancanti il nome contiene un segnaposto evidente (IDMATRICE / IDLAB) che va sostituito prima del salvataggio.
/// Solo logica pura (nessun accesso a dati), testabile in isolamento.
/// </summary>
public static class CostruttoreNomeCertificatoEsterno
{
    private static readonly Regex Vietati = new(@"[\\/:*?""<>|\r\n\t]+", RegexOptions.Compiled);
    private static readonly Regex TrattiniMultipli = new(@"-{2,}", RegexOptions.Compiled);
    private static readonly Regex SpaziMultipli = new(@"\s{2,}", RegexOptions.Compiled);
    private static readonly Regex SeparatoriEVietati = new(@"[-\\/:*?""<>|\r\n\t]+", RegexOptions.Compiled);

    /// <summary>Segnaposto usati quando un id non è determinabile: il salvataggio li rifiuta.</summary>
    public const string SegnapostoIdMatrice = "IDMATRICE";
    public const string SegnapostoIdLab = "IDLAB";

    /// <summary>True se il nome contiene ancora un segnaposto da completare a mano.</summary>
    public static bool HaSegnaposti(string? nome) =>
        !string.IsNullOrEmpty(nome)
        && (nome.Contains(SegnapostoIdMatrice, StringComparison.Ordinal) || nome.Contains(SegnapostoIdLab, StringComparison.Ordinal));

    /// <summary>Acronimi laboratorio (configurabili in produzione).</summary>
    public static readonly IReadOnlyDictionary<string, string> Acronimi =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sicural"] = "SIC",
            ["epta nord"] = "EP",
            ["eptanord"] = "EP",
            ["centro ricerche marine"] = "CRM",
            ["c.s.a"] = "CSA",
            ["csa"] = "CSA",
            ["venezie"] = "IZSVE",
            ["togo rosati"] = "IZSUM",
            ["arpam"] = "ARPAM",
            ["bioagrifood"] = "BAF",
            ["agribiosearch"] = "ABS",
        };

    /// <summary>Ricava l'acronimo dal nome laboratorio; ripiego con le prime lettere.</summary>
    public static string AcronimoDa(string? laboratorio)
    {
        if (string.IsNullOrWhiteSpace(laboratorio)) return "EXT";
        var l = laboratorio.ToLowerInvariant();
        foreach (var kv in Acronimi)
            if (l.Contains(kv.Key)) return kv.Value;
        var sigla = new string(laboratorio.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        return string.IsNullOrEmpty(sigla) ? "EXT" : sigla;
    }

    /// <summary>Sostituisce i caratteri vietati nei nomi file con "-" e normalizza i trattini.</summary>
    public static string Pulisci(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var v = Vietati.Replace(s.Trim(), "-");
        v = TrattiniMultipli.Replace(v, "-");
        return v.Trim('-');
    }

    /// <summary>
    /// Testo libero (cliente, prodotto, matrice): trattini, barre e caratteri vietati diventano spazi,
    /// così nel nome ogni "-" è SOLO un separatore di campo ("e/o" → "e o", "ERDIS - ENTE" → "ERDIS ENTE").
    /// </summary>
    public static string PulisciTesto(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var v = SeparatoriEVietati.Replace(s, " ");
        return SpaziMultipli.Replace(v, " ").Trim();
    }

    /// <summary>
    /// Codice (RdP esterno): trattini, barre e caratteri vietati diventano "_" ("2604327-001" → "2604327_001"),
    /// per lo stesso motivo. Solo il nome file: in ArcLab il numero resta quello originale.
    /// </summary>
    public static string PulisciCodice(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var v = SeparatoriEVietati.Replace(s.Trim(), "_");
        return v.Replace(" ", "").Trim('_');
    }

    /// <summary>
    /// Compone il nome file (senza estensione). Il nome ha sempre 15 campi separati da "-":
    /// 1-3 data, 4 lotto, 5 campione, 6 idInsediamento, 7 cliente, 8 prodotto, 9 matrice, 10 idMatrice,
    /// 11 EST, 12 sigla laboratorio, 13 idLabEsterno, 14 RdP esterno, 15 AVV/STD.
    /// </summary>
    public static string Componi(DatiNomeCertificatoEsterno d)
    {
        string[] parti =
        [
            d.Data.ToString("dd-MM-yy"),
            Pulisci(d.Lotto),
            Pulisci(d.Campione),
            FormattaId(d.IdInsediamento),
            PulisciTesto(d.Cliente),
            PulisciTesto(d.Prodotto),
            PulisciTesto(d.Matrice),
            Valore(d.IdMatrice, SegnapostoIdMatrice),
            "EST",
            PulisciCodice(d.LabAcronimo),
            Valore(d.IdLabEsterno, SegnapostoIdLab),
            PulisciCodice(d.RdPEsterno),
            d.ConAvviso ? "AVV" : "STD",
        ];
        var nome = string.Join("-", parti.Where(p => !string.IsNullOrEmpty(p)));
        return TrattiniMultipli.Replace(nome, "-");
    }

    /// <summary>
    /// Campo id obbligatorio per Palomar: mai omesso (sposterebbe le posizioni), al più segnaposto.
    /// In ArcLab alcuni id sono NEGATIVI (numerazione casuale stile Access, es. -914812082): il segno "-"
    /// romperebbe la regola "ogni trattino è un separatore", quindi un id negativo si scrive con la
    /// lettera N al posto del meno (N914812082). Palomar lo riconverte in negativo.
    /// </summary>
    private static string Valore(string? id, string segnaposto) => FormattaId(id) is { Length: > 0 } v ? v : segnaposto;

    /// <summary>Id nel nome file: negativo → "N" + valore assoluto; altri caratteri vietati/trattini tolti.</summary>
    public static string FormattaId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";
        var t = id.Trim();
        if (t.StartsWith('-')) return "N" + PulisciCodice(t[1..]);
        return PulisciCodice(t);
    }
}
