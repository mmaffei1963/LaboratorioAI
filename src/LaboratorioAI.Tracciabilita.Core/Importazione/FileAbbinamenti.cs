using System.Text;

namespace LaboratorioAI.Tracciabilita.Core.Importazione;

public sealed record SceltaFornitore(string Dicitura, string Scelta);   // Scelta: codice fornitore, NUOVO o SALTA
public sealed record SceltaArticolo(string Dicitura, string NomeArticolo, string UnitaMisura, bool Salta);

/// <summary>
/// File CSV (separatore «;», UTF-8 con BOM) che si apre e si corregge in Excel:
/// la prova li scrive con le proposte, l'importazione li rilegge con le scelte.
/// </summary>
public static class FileAbbinamenti
{
    public const string Nuovo = "NUOVO";
    public const string Salta = "SALTA";

    public static readonly UTF8Encoding Utf8ConBom = new(true);

    public static string ScriviFornitori(IEnumerable<PropostaFornitore> proposte)
    {
        var sb = new StringBuilder();
        Riga(sb, "Dicitura nel vecchio programma", "Righe", "Esito", "Proposta", "Codice proposto", "SCELTA (codice, NUOVO oppure SALTA)");
        foreach (var p in proposte)
            Riga(sb, p.Dicitura, p.Righe.ToString(), p.Esito.ToString(), p.Candidato?.RagioneSociale ?? "", p.Candidato?.Codice ?? "",
                p.Esito == EsitoAbbinamento.Sicuro ? p.Candidato!.Codice : "");
        return sb.ToString();
    }

    public static string ScriviArticoli(IEnumerable<GruppoArticolo> gruppi)
    {
        var sb = new StringBuilder();
        Riga(sb, "Dicitura nel vecchio programma", "Righe", "NOME ARTICOLO (uguale = stesso articolo)", "Unità", "SALTA (x)");
        foreach (var g in gruppi)
        {
            var nome = Maiuscola(g.Varianti[0]);
            foreach (var v in g.Varianti)
                Riga(sb, v, "", nome, "kg", "");
        }
        return sb.ToString();
    }

    public static IReadOnlyList<SceltaFornitore> LeggiFornitori(string testo) =>
        Righe(testo).Skip(1).Where(c => c.Count >= 6 && c[0].Length > 0)
            .Select(c => new SceltaFornitore(c[0], c[5].Trim())).ToList();

    public static IReadOnlyList<SceltaArticolo> LeggiArticoli(string testo) =>
        Righe(testo).Skip(1).Where(c => c.Count >= 5 && c[0].Length > 0)
            .Select(c => new SceltaArticolo(c[0], c[2].Trim(), string.IsNullOrWhiteSpace(c[3]) ? "kg" : c[3].Trim(), c[4].Trim().Length > 0))
            .ToList();

    public static string Maiuscola(string s) =>
        string.IsNullOrWhiteSpace(s) ? s : char.ToUpperInvariant(s.Trim()[0]) + s.Trim()[1..];

    private static void Riga(StringBuilder sb, params string[] campi) =>
        sb.AppendLine(string.Join(';', campi.Select(Campo)));

    private static string Campo(string c)
    {
        c ??= "";
        // Contro le formule di Excel: un testo che inizia con = + - @ non deve essere letto come formula.
        if (c.Length > 0 && "=+-@".Contains(c[0])) c = "'" + c;
        return c.Contains(';') || c.Contains('"') || c.Contains('\n') ? "\"" + c.Replace("\"", "\"\"") + "\"" : c;
    }

    public static List<List<string>> Righe(string testo)
    {
        var righe = new List<List<string>>();
        var riga = new List<string>();
        var campo = new StringBuilder();
        var tra = false;
        testo = testo.TrimStart('﻿');

        for (var i = 0; i < testo.Length; i++)
        {
            var c = testo[i];
            if (tra)
            {
                if (c == '"' && i + 1 < testo.Length && testo[i + 1] == '"') { campo.Append('"'); i++; }
                else if (c == '"') tra = false;
                else campo.Append(c);
            }
            else if (c == '"') tra = true;
            else if (c == ';') { riga.Add(Toglie(campo)); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < testo.Length && testo[i + 1] == '\n') i++;
                riga.Add(Toglie(campo));
                if (riga.Any(x => x.Length > 0)) righe.Add(riga);
                riga = new List<string>();
            }
            else campo.Append(c);
        }
        if (campo.Length > 0 || riga.Count > 0)
        {
            riga.Add(Toglie(campo));
            if (riga.Any(x => x.Length > 0)) righe.Add(riga);
        }
        return righe;
    }

    private static string Toglie(StringBuilder campo)
    {
        var t = campo.ToString().Trim();
        campo.Clear();
        return t.StartsWith('\'') ? t[1..] : t;
    }
}
