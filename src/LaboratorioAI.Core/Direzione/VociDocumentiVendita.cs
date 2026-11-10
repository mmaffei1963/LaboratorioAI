using System.Globalization;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Direzione;

/// <summary>Riga di un documento di vendita come letta da AZW (TABFST01 + TABFST02).</summary>
public sealed record RigaDocumentoAzw(
    string TipoDocumento, int AnnoDocumento, string NumeroDocumento, DateTime DataDocumento,
    string CodiceCliente, string? RagioneSociale, int NumeroRiga, int TipoRiga, string? CodiceArticolo,
    string? Testo, string? Testo2, string? Note, decimal Quantita, decimal Prezzo,
    decimal Sconto1, decimal Sconto2, decimal Sconto3, string? Centro, string? Voce, string? Commessa);

/// <summary>
/// Ricompone le voci dei documenti di vendita AZW (10/11/2026). In AZW il testo della fattura è spezzato: una riga
/// con quantità e prezzo (TIPRIG 0) seguita da righe di solo testo (TIPRIG 1). Ogni voce riunisce la riga con il
/// prezzo e le righe di testo che la seguono; l'importo è quantità × prezzo × (1 − sconti), negativo per le note di credito.
/// </summary>
public static class VociDocumentiVendita
{
    public const string Fattura = "FE";
    public const string NotaCredito = "CE";
    public static readonly DateTime Dal = new(2019, 1, 1);

    public static bool ENotaCredito(string tipo) => string.Equals(tipo, NotaCredito, StringComparison.OrdinalIgnoreCase);

    public static List<VoceDocumentoVenditaAzw> Componi(IEnumerable<RigaDocumentoAzw> righe)
    {
        var voci = new List<VoceDocumentoVenditaAzw>();
        foreach (var doc in righe.GroupBy(r => (r.TipoDocumento, r.AnnoDocumento, r.NumeroDocumento)))
        {
            VoceDocumentoVenditaAzw? corrente = null;
            var testi = new List<string>();
            void Chiudi()
            {
                if (corrente is null) return;
                var d = string.Join(" ", testi.Where(t => t.Length > 0));
                corrente.Descrizione = d.Length > 4000 ? d[..4000] : d;
                voci.Add(corrente);
                corrente = null; testi.Clear();
            }
            foreach (var r in doc.OrderBy(r => r.NumeroRiga))
            {
                var testo = Unisci(r.Testo, r.Testo2, r.Note);
                if (r.TipoRiga == 1 && corrente is not null) { testi.Add(testo); continue; }
                Chiudi();
                var qta = r.Quantita == 0m && r.Prezzo != 0m ? 1m : r.Quantita;
                var importo = Math.Round(qta * r.Prezzo * Fattore(r.Sconto1) * Fattore(r.Sconto2) * Fattore(r.Sconto3), 2, MidpointRounding.AwayFromZero);
                if (ENotaCredito(r.TipoDocumento)) importo = -Math.Abs(importo);
                corrente = new VoceDocumentoVenditaAzw
                {
                    TipoDocumento = r.TipoDocumento, AnnoDocumento = r.AnnoDocumento, NumeroDocumento = r.NumeroDocumento,
                    DataDocumento = r.DataDocumento.Date, CodiceCliente = r.CodiceCliente, RagioneSociale = r.RagioneSociale,
                    NumeroRiga = r.NumeroRiga, CodiceArticolo = r.CodiceArticolo, Quantita = r.Quantita, Prezzo = r.Prezzo,
                    Sconto = TestoSconto(r.Sconto1, r.Sconto2, r.Sconto3), Importo = importo,
                    Centro = r.Centro, Voce = r.Voce, Commessa = r.Commessa
                };
                testi.Add(testo);
            }
            Chiudi();
        }
        // Voci senza testo né importo (righe vuote) non servono alla ricerca.
        voci.RemoveAll(v => v.Importo == 0m && string.IsNullOrWhiteSpace(v.Descrizione));
        return voci;
    }

    private static string Unisci(params string?[] parti) =>
        string.Join(" ", parti.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static decimal Fattore(decimal sconto) => sconto is > 0m and < 100m ? 1m - sconto / 100m : 1m;

    private static string? TestoSconto(params decimal[] sconti)
    {
        var s = sconti.Where(x => x is > 0m and < 100m).Select(x => x.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"))).ToList();
        return s.Count == 0 ? null : string.Join("+", s);
    }

    /// <summary>Parole da cercare: separate da punto e virgola o virgola; una voce corrisponde se contiene almeno una parola.</summary>
    public static IReadOnlyList<string> Parole(string? testo) =>
        (testo ?? "").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
