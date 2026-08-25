using ClosedXML.Excel;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Documenti;

/// <summary>Esporta in Excel le quattro verifiche della pagina "Ricerca errori".</summary>
public interface IEsportatoreRicercaErrori
{
    byte[] Duplicati(IReadOnlyList<RigaDuplicatoRapporto> righe, int annoDa, int annoA);
    byte[] Arrotondamento(IReadOnlyList<RigaMetodoArrotondamento> righe);
    byte[] Margine(IReadOnlyList<RigaMetodoMargine> righe, decimal sogliaPct);
    byte[] Doppi(IReadOnlyList<RigaMetodoDoppio> righe);
    byte[] Lotti(IReadOnlyList<RigaLottoErrore> righe, string titolo, int annoDa, int annoA);
    byte[] Coerenza(IReadOnlyList<RigaCoerenzaPrelevatore> righe, int annoDa, int annoA);
    string NomeFile(string sezione);
}

/// <inheritdoc />
public class EsportatoreRicercaErrori : IEsportatoreRicercaErrori
{
    public byte[] Duplicati(IReadOnlyList<RigaDuplicatoRapporto> righe, int annoDa, int annoA)
    {
        string[] intest =
        {
            "Id certificato", "N° certificato", "Data certificato", "Id campione", "Cod campione",
            "Id parametro", "Parametro", "Id tipo par.", "Id riga metodo", "Denominazione riga",
            "Id prova", "Valore calcolato", "Maggiore di", "Minore di", "Occorrenze"
        };
        return Costruisci("Doppioni rapporti di prova",
            $"Anni {annoDa}–{annoA} · {righe.Count} righe · estratto il {DateTime.Now:dd/MM/yyyy HH:mm}",
            intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdCertificato;
                f.Cell(r, 2).Value = x.NCertificato;
                if (x.DataCertificato is DateTime d) f.Cell(r, 3).Value = d;
                f.Cell(r, 4).Value = x.IdCampione;
                f.Cell(r, 5).Value = x.CodCampione;
                f.Cell(r, 6).Value = x.IdParametro;
                f.Cell(r, 7).Value = x.NomeParametro;
                f.Cell(r, 8).Value = x.IdTipoParametro;
                f.Cell(r, 9).Value = x.IdRigaMetodo;
                f.Cell(r, 10).Value = x.DenominazioneRiga;
                f.Cell(r, 11).Value = x.IdProva;
                if (x.ValoreCalcolato is double v) f.Cell(r, 12).Value = v;
                f.Cell(r, 13).Value = x.MaggioreDi ? "Sì" : "";
                f.Cell(r, 14).Value = x.MinoreDi ? "Sì" : "";
                f.Cell(r, 15).Value = x.Occorrenze;
                r++;
            }
            f.Column(3).Style.DateFormat.Format = "dd/MM/yyyy";
        });
    }

    public byte[] Arrotondamento(IReadOnlyList<RigaMetodoArrotondamento> righe)
    {
        string[] intest =
        {
            "Id metodo", "Id parametro", "Id tipo metodo", "Rif. metodo", "Dettagli metodo",
            "Rif. biblio", "Id unità", "Cifre significative", "Decimali max"
        };
        return Costruisci("Metodi senza cifre significative / decimali max",
            $"{righe.Count} metodi attivi · estratto il {DateTime.Now:dd/MM/yyyy HH:mm}",
            intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdMetodo;
                f.Cell(r, 2).Value = x.IdParametro;
                f.Cell(r, 3).Value = x.IdTipoMetodo;
                f.Cell(r, 4).Value = x.RifMetodo;
                f.Cell(r, 5).Value = x.DettagliMetodo;
                f.Cell(r, 6).Value = x.RifBiblio;
                f.Cell(r, 7).Value = x.IdUnita;
                f.Cell(r, 8).Value = x.CifreSignificative.HasValue ? x.CifreSignificative.Value.ToString() : "(vuoto)";
                f.Cell(r, 9).Value = x.DecimaliMax.HasValue ? x.DecimaliMax.Value.ToString() : "(vuoto)";
                r++;
            }
        });
    }

    public byte[] Margine(IReadOnlyList<RigaMetodoMargine> righe, decimal sogliaPct)
    {
        string[] intest =
        {
            "Id metodo", "Id parametro", "Id tipo metodo", "Rif. metodo", "Dettagli metodo",
            "Rif. biblio", "Id unità", "Costo a noi €", "Costo metodo €", "Margine %"
        };
        return Costruisci("Metodi con margine sotto soglia",
            $"Soglia {sogliaPct:0.#}% · {righe.Count} metodi attivi · estratto il {DateTime.Now:dd/MM/yyyy HH:mm}",
            intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdMetodo;
                f.Cell(r, 2).Value = x.IdParametro;
                f.Cell(r, 3).Value = x.IdTipoMetodo;
                f.Cell(r, 4).Value = x.RifMetodo;
                f.Cell(r, 5).Value = x.DettagliMetodo;
                f.Cell(r, 6).Value = x.RifBiblio;
                f.Cell(r, 7).Value = x.IdUnita;
                if (x.CostoAnoi is decimal ca) f.Cell(r, 8).Value = ca;
                if (x.CostoMetodoEuro is decimal cm) f.Cell(r, 9).Value = cm;
                if (x.MarginePct is decimal mp) f.Cell(r, 10).Value = mp;
                r++;
            }
            f.Column(8).Style.NumberFormat.Format = "#,##0.00";
            f.Column(9).Style.NumberFormat.Format = "#,##0.00";
            f.Column(10).Style.NumberFormat.Format = "#,##0.00";
        });
    }

    public byte[] Doppi(IReadOnlyList<RigaMetodoDoppio> righe)
    {
        string[] intest =
        {
            "Id metodo", "Id parametro", "Id tipo metodo", "Rif. metodo", "Dettagli metodo",
            "Rif. biblio", "Id unità", "N° metodi nel gruppo"
        };
        return Costruisci("Metodi potenzialmente doppi",
            $"{righe.Count} metodi coinvolti · estratto il {DateTime.Now:dd/MM/yyyy HH:mm}",
            intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdMetodo;
                f.Cell(r, 2).Value = x.IdParametro;
                f.Cell(r, 3).Value = x.IdTipoMetodo;
                f.Cell(r, 4).Value = x.RifMetodo;
                f.Cell(r, 5).Value = x.DettagliMetodo;
                f.Cell(r, 6).Value = x.RifBiblio;
                f.Cell(r, 7).Value = x.IdUnita;
                f.Cell(r, 8).Value = x.NMetodi;
                r++;
            }
        });
    }

    public byte[] Lotti(IReadOnlyList<RigaLottoErrore> righe, string titolo, int annoDa, int annoA)
    {
        string[] intest = { "Id lotto", "Cod lotto", "Data ricezione", "Id commessa", "Cliente" };
        return Costruisci(titolo,
            $"Anni {annoDa}–{annoA} · {righe.Count} lotti · estratto il {DateTime.Now:dd/MM/yyyy HH:mm}",
            intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdLotto;
                f.Cell(r, 2).Value = x.CodLotto;
                if (x.DataRic is DateTime d) f.Cell(r, 3).Value = d;
                f.Cell(r, 4).Value = x.IdCommessa;
                f.Cell(r, 5).Value = x.RagioneSociale;
                r++;
            }
            f.Column(3).Style.DateFormat.Format = "dd/MM/yyyy";
        });
    }

    public byte[] Coerenza(IReadOnlyList<RigaCoerenzaPrelevatore> righe, int annoDa, int annoA)
    {
        string[] intest =
        {
            "Id lotto", "Cod lotto", "Data ricezione", "Id campione", "Cod campione",
            "Id prelevatore", "Prelevatore", "Id vettore", "Vettore", "Verbale prelievo", "Anomalia"
        };
        return Costruisci("Coerenza prelevatore interno / vettore / verbale",
            $"Anni {annoDa}–{annoA} · {righe.Count} campioni · estratto il {DateTime.Now:dd/MM/yyyy HH:mm}",
            intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdLotto;
                f.Cell(r, 2).Value = x.CodLotto;
                if (x.DataRic is DateTime d) f.Cell(r, 3).Value = d;
                f.Cell(r, 4).Value = x.IdCampione;
                f.Cell(r, 5).Value = x.CodCampione;
                f.Cell(r, 6).Value = x.IdPrelevatore;
                f.Cell(r, 7).Value = x.Prelevatore;
                f.Cell(r, 8).Value = x.IdVettore;
                f.Cell(r, 9).Value = x.Vettore;
                f.Cell(r, 10).Value = x.BuonoPrelievo;
                f.Cell(r, 11).Value = string.Join(" + ", new[]
                {
                    x.AnomaliaVettore ? "categoria vettore diversa dal prelevatore" : null,
                    x.AnomaliaVerbale ? "verbale non ammesso (prelievo cliente/terza parte)" : null
                }.Where(s => s != null));
                r++;
            }
            f.Column(3).Style.DateFormat.Format = "dd/MM/yyyy";
        });
    }

    public string NomeFile(string sezione) => $"RicercaErrori_{sezione}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

    // ── Helper comune ───────────────────────────────────────────────
    private static byte[] Costruisci(string titolo, string sottotitolo, string[] intestazioni,
        int nRighe, Action<IXLWorksheet, int> riempi)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Ricerca errori");
        int colonne = intestazioni.Length;

        var riga = StileFoglio.Titolo(foglio, titolo, sottotitolo, colonne);
        for (int i = 0; i < colonne; i++) foglio.Cell(riga, i + 1).Value = intestazioni[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        riempi(foglio, riga + 1);

        int ultima = riga + nRighe;
        StileFoglio.Rifinisci(foglio, riga, ultima, colonne);
        if (nRighe > 0) foglio.Range(riga, 1, ultima, colonne).SetAutoFilter();

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }
}
