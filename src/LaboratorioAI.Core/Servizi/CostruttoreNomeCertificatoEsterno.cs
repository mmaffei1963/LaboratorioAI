using System.Text.RegularExpressions;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Compone il nome file di un certificato esterno secondo la convenzione MARE.A:
/// <c>GG-MM-AA-lotto-campione-idInsediamento-accettazione-cliente-prodotto-matrice-EST-LAB-RdP-(AVV|STD)</c>.
/// Solo logica pura (nessun accesso a dati), testabile in isolamento.
/// </summary>
public static class CostruttoreNomeCertificatoEsterno
{
    private static readonly Regex Vietati = new(@"[\\/:*?""<>|\r\n\t]+", RegexOptions.Compiled);
    private static readonly Regex TrattiniMultipli = new(@"-{2,}", RegexOptions.Compiled);

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

    /// <summary>Compone il nome file (senza estensione).</summary>
    public static string Componi(DatiNomeCertificatoEsterno d)
    {
        string[] parti =
        [
            d.Data.ToString("dd-MM-yy"),
            Pulisci(d.Lotto),
            Pulisci(d.Campione),
            Pulisci(d.IdInsediamento),
            Pulisci(d.Accettazione),
            Pulisci(d.Cliente),
            Pulisci(d.Prodotto),
            Pulisci(d.Matrice),
            "EST",
            Pulisci(d.LabAcronimo),
            Pulisci(d.RdPEsterno),
            d.ConAvviso ? "AVV" : "STD",
        ];
        var nome = string.Join("-", parti.Where(p => !string.IsNullOrEmpty(p)));
        return TrattiniMultipli.Replace(nome, "-");
    }
}
