using System.Text;
using LaboratorioAI.Tracciabilita.Core.Importazione;

namespace LaboratorioAI.Tracciabilita.Importazione;

public static class RapportoProva
{
    public static string Scrivi(PianoProva p, DateTime quando)
    {
        var sb = new StringBuilder();
        var righe = p.Dati.Magazzino.Count;
        sb.AppendLine($"PROVA DI IMPORTAZIONE — {quando:dd/MM/yyyy HH:mm}");
        sb.AppendLine("Non è stato scritto nulla in nessun database.");
        sb.AppendLine();
        sb.AppendLine("CONTENUTO DEL VECCHIO PROGRAMMA");
        sb.AppendLine($"  Righe di magazzino (arrivi di merce): {righe}");
        sb.AppendLine($"  Fornitori in anagrafica: {p.Dati.Fornitori.Count}");
        sb.AppendLine($"  Abbinamenti materia-fornitore: {p.Dati.MaterieFornitori.Count}");
        sb.AppendLine($"  Matrici (categorie di materie prime): {p.Dati.Matrici.Count}");
        var date = p.Dati.Magazzino.Where(r => r.DataDocumento is { Year: >= 2004 }).Select(r => r.DataDocumento!.Value).ToList();
        if (date.Count > 0) sb.AppendLine($"  Periodo coperto: da {date.Min():dd/MM/yyyy} a {date.Max():dd/MM/yyyy}");
        sb.AppendLine();

        sb.AppendLine("FORNITORI");
        foreach (var e in new[] { EsitoAbbinamento.Sicuro, EsitoAbbinamento.Probabile, EsitoAbbinamento.Nessuno })
        {
            var gruppo = p.Fornitori.Where(x => x.Esito == e).ToList();
            sb.AppendLine($"  {Etichetta(e)}: {gruppo.Count} diciture ({gruppo.Sum(x => x.Righe)} righe)");
        }
        sb.AppendLine("  Da guardare nel file abbinamenti_fornitori.csv (colonna SCELTA).");
        sb.AppendLine();

        sb.AppendLine("ARTICOLI");
        var diciture = p.Articoli.Sum(g => g.Varianti.Count);
        sb.AppendLine($"  Diciture diverse: {diciture}");
        sb.AppendLine($"  Dopo la pulizia semplice: {p.Articoli.Count} articoli proposti");
        sb.AppendLine($"  Gruppi con più modi di scrivere la stessa cosa: {p.Articoli.Count(g => g.Varianti.Count > 1)}");
        sb.AppendLine($"  Coppie molto simili da valutare (stesso articolo?): {p.Duplicati.Count}");
        sb.AppendLine($"  Articoli che compaiono una sola volta: {p.Articoli.Count(g => g.Righe == 1)}");
        sb.AppendLine("  Da guardare nel file abbinamenti_articoli.csv: uguale NOME ARTICOLO = stesso articolo.");
        foreach (var (a, b, punteggio) in p.Duplicati.Take(30))
            sb.AppendLine($"    simili al {punteggio:P0}: «{a.Varianti[0]}» e «{b.Varianti[0]}»");
        sb.AppendLine();

        sb.AppendLine("LOTTI");
        sb.AppendLine($"  Codici di lotto usati da fornitori diversi: {p.CodiciLottoCondivisi} (il lotto si identifica con articolo + fornitore + codice, quindi non è un problema)");
        sb.AppendLine();

        sb.AppendLine("RIGHE SOSPETTE (numero = posizione nella tabella MAGAZZINO)");
        if (p.Segnalazioni.Count == 0) sb.AppendLine("  Nessuna.");
        foreach (var s in p.Segnalazioni)
            sb.AppendLine($"  {s.Problema}: {s.Righe} righe (es. n. {string.Join(", ", s.Esempi)})");
        return sb.ToString();
    }

    private static string Etichetta(EsitoAbbinamento e) => e switch
    {
        EsitoAbbinamento.Sicuro => "Abbinati da soli (sicuri)",
        EsitoAbbinamento.Probabile => "Probabili, da confermare",
        _ => "Senza abbinamento, da scegliere"
    };
}
