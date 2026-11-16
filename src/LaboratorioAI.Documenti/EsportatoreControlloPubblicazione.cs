using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Excel del controllo pubblicazione certificati (16/11/2026): un foglio per ciascun controllo.</summary>
public static class EsportatoreControlloPubblicazione
{
    public static byte[] Genera(EsitoControlloPubblicazione e, DateOnly dal, DateOnly al, bool escludiEsterni)
    {
        using var cartella = new XLWorkbook();
        var periodo = $"Dal {dal:dd/MM/yyyy} al {al:dd/MM/yyyy}" + (escludiEsterni ? " — certificati esterni con nome regolare esclusi da «Solo portale»" : "");

        Foglio(cartella, "Date diverse", "Certificati con date di spedizione, caricamento e firma diverse", periodo,
            new[] { "N. certificato", "Spedizione ArcLab", "Caricamento portale", "Firma", "Giorni caricamento", "Giorni firma", "Nota", "Tipo", "Nome file" },
            e.DateDiverse.Select(d => new object?[] { d.NCert, d.DataSped, d.DataUpload, d.DataFirma, d.GiorniUpload, d.GiorniFirma,
                d.FirmatoDopoPubblicazione ? "firmato dopo la pubblicazione" : "", d.Tipo, d.NomeFile }));
        Foglio(cartella, "Solo ArcLab", "Spediti in ArcLab ma non sul portale", periodo,
            new[] { "N. certificato", "Spedizione ArcLab", "Tipo certificato" },
            e.SoloArcLab.Select(s => new object?[] { s.NCert, s.DataSped, s.IdTipoCertificato }));
        Foglio(cartella, "Solo portale", "Sul portale ma non spediti in ArcLab", periodo,
            new[] { "Tipo", "Nome file", "Titolo", "Caricamento", "Firma", "Certificato ArcLab", "Spedizione ArcLab" },
            e.SoloPortale.Select(s => new object?[] { s.Tipo, s.NomeFile, s.Titolo, s.DataUpload, s.DataFirma, s.CertificatoArcLab, s.DataSpedArcLab }));
        Foglio(cartella, "Caricati più volte", "Certificati caricati più volte (la versione inglese non conta come doppione)", periodo,
            new[] { "N. certificato", "Spedizione ArcLab", "Caricamenti", "Di cui inglese", "Primo caricamento", "Ultimo caricamento" },
            e.Doppioni.Select(d => new object?[] { d.NCert, d.DataSped, d.Caricamenti, d.DiCuiInglese, d.PrimoUpload, d.UltimoUpload }));

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    private static void Foglio(XLWorkbook cartella, string nome, string titolo, string sotto, string[] intest, IEnumerable<object?[]> righe)
    {
        var f = cartella.Worksheets.Add(nome);
        var riga = StileFoglio.Titolo(f, titolo, sotto, intest.Length);
        for (var i = 0; i < intest.Length; i++) f.Cell(riga, i + 1).Value = intest[i];
        StileFoglio.Intestazione(f, riga, intest.Length, StileFoglio.Rosso);
        var prima = riga + 1; var r = prima;
        foreach (var valori in righe)
        {
            for (var i = 0; i < valori.Length; i++)
            {
                var c = f.Cell(r, i + 1);
                switch (valori[i])
                {
                    case DateTime d:
                        c.Value = d;
                        c.Style.DateFormat.Format = d.TimeOfDay == TimeSpan.Zero ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm";
                        break;
                    case int n: c.Value = n; break;
                    case null: break;
                    default: c.Value = valori[i]!.ToString(); break;
                }
            }
            r++;
        }
        StileFoglio.Rifinisci(f, prima, Math.Max(prima, r - 1), intest.Length);
    }
}
