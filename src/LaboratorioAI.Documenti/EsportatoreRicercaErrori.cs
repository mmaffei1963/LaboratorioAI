using ClosedXML.Excel;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Documenti;

/// <summary>Dati per l'esportazione unica multi-foglio del cruscotto Ricerca errori.</summary>
public class DatiRicercaErrori
{
    public int AnnoDa { get; init; }
    public int AnnoA { get; init; }
    public decimal SogliaPct { get; init; }
    public string PeriodoStampa { get; init; } = "";

    public IReadOnlyList<RigaDuplicatoRapporto>? Duplicati { get; init; }
    public IReadOnlyList<RigaMetodoArrotondamento>? Arrotondamento { get; init; }
    public IReadOnlyList<RigaMetodoMargine>? Margine { get; init; }
    public IReadOnlyList<RigaMetodoDoppio>? Doppi { get; init; }
    public IReadOnlyList<RigaLottoErrore>? LottiRicevitore { get; init; }
    public IReadOnlyList<RigaLottoErrore>? LottiVettore { get; init; }
    public IReadOnlyList<RigaCoerenzaPrelevatore>? Coerenza { get; init; }
    public IReadOnlyList<RigaCampioneStampa>? Stampa { get; init; }
}

/// <summary>Esporta in Excel le verifiche della pagina "Ricerca errori".</summary>
public interface IEsportatoreRicercaErrori
{
    byte[] Duplicati(IReadOnlyList<RigaDuplicatoRapporto> righe, int annoDa, int annoA);
    byte[] Arrotondamento(IReadOnlyList<RigaMetodoArrotondamento> righe);
    byte[] Margine(IReadOnlyList<RigaMetodoMargine> righe, decimal sogliaPct);
    byte[] Doppi(IReadOnlyList<RigaMetodoDoppio> righe);
    byte[] Lotti(IReadOnlyList<RigaLottoErrore> righe, string titolo, int annoDa, int annoA);
    byte[] Coerenza(IReadOnlyList<RigaCoerenzaPrelevatore> righe, int annoDa, int annoA);
    byte[] Stampa(IReadOnlyList<RigaCampioneStampa> righe, string periodo);
    /// <summary>Un unico workbook con un foglio per ogni controllo presente nei dati.</summary>
    byte[] Tutto(DatiRicercaErrori dati);
    string NomeFile(string sezione);
}

/// <inheritdoc />
public class EsportatoreRicercaErrori : IEsportatoreRicercaErrori
{
    // ── Metodi pubblici a singolo foglio (un workbook ciascuno) ─────
    public byte[] Duplicati(IReadOnlyList<RigaDuplicatoRapporto> righe, int annoDa, int annoA)
        => Singolo(wb => FoglioDuplicati(wb, righe, annoDa, annoA));

    public byte[] Arrotondamento(IReadOnlyList<RigaMetodoArrotondamento> righe)
        => Singolo(wb => FoglioArrotondamento(wb, righe));

    public byte[] Margine(IReadOnlyList<RigaMetodoMargine> righe, decimal sogliaPct)
        => Singolo(wb => FoglioMargine(wb, righe, sogliaPct));

    public byte[] Doppi(IReadOnlyList<RigaMetodoDoppio> righe)
        => Singolo(wb => FoglioDoppi(wb, righe));

    public byte[] Lotti(IReadOnlyList<RigaLottoErrore> righe, string titolo, int annoDa, int annoA)
        => Singolo(wb => FoglioLotti(wb, righe, titolo, titolo, annoDa, annoA));

    public byte[] Coerenza(IReadOnlyList<RigaCoerenzaPrelevatore> righe, int annoDa, int annoA)
        => Singolo(wb => FoglioCoerenza(wb, righe, annoDa, annoA));

    public byte[] Stampa(IReadOnlyList<RigaCampioneStampa> righe, string periodo)
        => Singolo(wb => FoglioStampa(wb, righe, periodo));

    public byte[] Tutto(DatiRicercaErrori d) => Singolo(wb =>
    {
        if (d.Duplicati is not null) FoglioDuplicati(wb, d.Duplicati, d.AnnoDa, d.AnnoA);
        if (d.Arrotondamento is not null) FoglioArrotondamento(wb, d.Arrotondamento);
        if (d.Margine is not null) FoglioMargine(wb, d.Margine, d.SogliaPct);
        if (d.Doppi is not null) FoglioDoppi(wb, d.Doppi);
        if (d.LottiRicevitore is not null) FoglioLotti(wb, d.LottiRicevitore, "Senza ricevitore", "Lotti senza ricevitore", d.AnnoDa, d.AnnoA);
        if (d.LottiVettore is not null) FoglioLotti(wb, d.LottiVettore, "Senza vettore", "Lotti senza vettore", d.AnnoDa, d.AnnoA);
        if (d.Coerenza is not null) FoglioCoerenza(wb, d.Coerenza, d.AnnoDa, d.AnnoA);
        if (d.Stampa is not null) FoglioStampa(wb, d.Stampa, d.PeriodoStampa);
        if (!wb.Worksheets.Any()) wb.Worksheets.Add("Nessun controllo");
    });

    public string NomeFile(string sezione) => $"RicercaErrori_{sezione}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

    // ── Fogli per controllo (aggiungono un foglio al workbook) ──────
    private static void FoglioDuplicati(XLWorkbook wb, IReadOnlyList<RigaDuplicatoRapporto> righe, int annoDa, int annoA)
    {
        string[] intest =
        {
            "Id certificato", "N° certificato", "Data certificato", "Id campione", "Cod campione",
            "Id parametro", "Parametro", "Id tipo par.", "Id riga metodo", "Denominazione riga",
            "Id prova", "Valore calcolato", "Maggiore di", "Minore di", "Occorrenze"
        };
        Foglio(wb, "Doppioni", "Doppioni rapporti di prova",
            $"Anni {annoDa}–{annoA} · {righe.Count} righe", intest, righe.Count, (f, r0) =>
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

    private static void FoglioArrotondamento(XLWorkbook wb, IReadOnlyList<RigaMetodoArrotondamento> righe)
    {
        string[] intest =
        {
            "Id metodo", "Id parametro", "Parametro", "Id tipo metodo", "Rif. metodo", "Dettagli metodo",
            "Rif. biblio", "Id unità", "Cifre significative", "Decimali max"
        };
        Foglio(wb, "Arrotondamento", "Metodi senza cifre significative / decimali max",
            $"{righe.Count} metodi attivi", intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdMetodo;
                f.Cell(r, 2).Value = x.IdParametro;
                f.Cell(r, 3).Value = x.NomeParametro;
                f.Cell(r, 4).Value = x.IdTipoMetodo;
                f.Cell(r, 5).Value = x.RifMetodo;
                f.Cell(r, 6).Value = x.DettagliMetodo;
                f.Cell(r, 7).Value = x.RifBiblio;
                f.Cell(r, 8).Value = x.IdUnita;
                f.Cell(r, 9).Value = x.CifreSignificative.HasValue ? x.CifreSignificative.Value.ToString() : "(vuoto)";
                f.Cell(r, 10).Value = x.DecimaliMax.HasValue ? x.DecimaliMax.Value.ToString() : "(vuoto)";
                r++;
            }
        });
    }

    private static void FoglioMargine(XLWorkbook wb, IReadOnlyList<RigaMetodoMargine> righe, decimal sogliaPct)
    {
        string[] intest =
        {
            "Id metodo", "Id parametro", "Parametro", "Id tipo metodo", "Rif. metodo", "Dettagli metodo",
            "Rif. biblio", "Id unità", "Costo a noi €", "Costo metodo €", "Margine %"
        };
        Foglio(wb, "Margine", "Metodi con margine sotto soglia",
            $"Soglia {sogliaPct:0.#}% · {righe.Count} metodi attivi", intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdMetodo;
                f.Cell(r, 2).Value = x.IdParametro;
                f.Cell(r, 3).Value = x.NomeParametro;
                f.Cell(r, 4).Value = x.IdTipoMetodo;
                f.Cell(r, 5).Value = x.RifMetodo;
                f.Cell(r, 6).Value = x.DettagliMetodo;
                f.Cell(r, 7).Value = x.RifBiblio;
                f.Cell(r, 8).Value = x.IdUnita;
                if (x.CostoAnoi is decimal ca) f.Cell(r, 9).Value = ca;
                if (x.CostoMetodoEuro is decimal cm) f.Cell(r, 10).Value = cm;
                if (x.MarginePct is decimal mp) f.Cell(r, 11).Value = mp;
                r++;
            }
            f.Column(9).Style.NumberFormat.Format = "#,##0.00";
            f.Column(10).Style.NumberFormat.Format = "#,##0.00";
            f.Column(11).Style.NumberFormat.Format = "#,##0.00";
        });
    }

    private static void FoglioDoppi(XLWorkbook wb, IReadOnlyList<RigaMetodoDoppio> righe)
    {
        string[] intest =
        {
            "Id metodo", "Id parametro", "Parametro", "Id tipo metodo", "Rif. metodo", "Dettagli metodo",
            "Rif. biblio", "Id unità", "N° metodi nel gruppo"
        };
        Foglio(wb, "Metodi doppi", "Metodi potenzialmente doppi",
            $"{righe.Count} metodi coinvolti", intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdMetodo;
                f.Cell(r, 2).Value = x.IdParametro;
                f.Cell(r, 3).Value = x.NomeParametro;
                f.Cell(r, 4).Value = x.IdTipoMetodo;
                f.Cell(r, 5).Value = x.RifMetodo;
                f.Cell(r, 6).Value = x.DettagliMetodo;
                f.Cell(r, 7).Value = x.RifBiblio;
                f.Cell(r, 8).Value = x.IdUnita;
                f.Cell(r, 9).Value = x.NMetodi;
                r++;
            }
        });
    }

    private static void FoglioLotti(XLWorkbook wb, IReadOnlyList<RigaLottoErrore> righe, string nomeFoglio, string titolo, int annoDa, int annoA)
    {
        string[] intest = { "Id lotto", "Cod lotto", "Data ricezione", "Id commessa", "Cliente" };
        Foglio(wb, nomeFoglio, titolo, $"Anni {annoDa}–{annoA} · {righe.Count} lotti", intest, righe.Count, (f, r0) =>
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

    private static void FoglioCoerenza(XLWorkbook wb, IReadOnlyList<RigaCoerenzaPrelevatore> righe, int annoDa, int annoA)
    {
        string[] intest =
        {
            "Id lotto", "Cod lotto", "Data ricezione", "Id campione", "Cod campione",
            "Id prelevatore", "Prelevatore", "Id vettore", "Vettore", "Verbale prelievo", "Anomalia"
        };
        Foglio(wb, "Coerenza", "Coerenza prelevatore / vettore / verbale",
            $"Anni {annoDa}–{annoA} · {righe.Count} campioni", intest, righe.Count, (f, r0) =>
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

    private static void FoglioStampa(XLWorkbook wb, IReadOnlyList<RigaCampioneStampa> righe, string periodo)
    {
        string[] intest =
        {
            "Id lotto", "Cod lotto", "Data ric.", "Id campione", "Cod campione", "Cliente",
            "Id prova", "Id riga metodo", "Denominazione riga", "Parametro", "Verifica valore",
            "Data inizio prova", "Data fine prova", "Arrotonda", "Max decimali", "Anomalia"
        };
        Foglio(wb, "Stampa", "Campioni «chiusi» non pronti alla stampa",
            $"{periodo} · {righe.Count} prove", intest, righe.Count, (f, r0) =>
        {
            int r = r0;
            foreach (var x in righe)
            {
                f.Cell(r, 1).Value = x.IdLotto;
                f.Cell(r, 2).Value = x.CodLotto;
                if (x.DataRic is DateTime dr) f.Cell(r, 3).Value = dr;
                f.Cell(r, 4).Value = x.IdCampione;
                f.Cell(r, 5).Value = x.CodCampione;
                f.Cell(r, 6).Value = x.RagioneSociale;
                f.Cell(r, 7).Value = x.IdProva;
                f.Cell(r, 8).Value = x.IdRigaMetodo;
                f.Cell(r, 9).Value = x.DenominazioneRiga;
                f.Cell(r, 10).Value = x.NomeParametro;
                f.Cell(r, 11).Value = x.VerificaValore ? "Sì" : "NO";
                if (x.DataInizioProva is DateTime di) f.Cell(r, 12).Value = di;
                if (x.DataFineProva is DateTime df) f.Cell(r, 13).Value = df;
                f.Cell(r, 14).Value = x.Arrotonda.HasValue ? x.Arrotonda.Value.ToString() : "(vuoto)";
                f.Cell(r, 15).Value = x.MaxDecimali.HasValue ? x.MaxDecimali.Value.ToString() : "(vuoto)";
                f.Cell(r, 16).Value = string.Join(" + ", new[]
                {
                    !x.VerificaValore         ? "valore non verificato" : null,
                    x.DataInizioProva is null ? "data inizio mancante"  : null,
                    x.DataFineProva  is null  ? "data fine mancante"    : null,
                    x.Arrotonda   is null     ? "Arrotonda mancante"    : null,
                    x.MaxDecimali is null     ? "MaxDecimali mancante"  : null
                }.Where(s => s != null));
                r++;
            }
            f.Column(3).Style.DateFormat.Format = "dd/MM/yyyy";
            f.Column(12).Style.DateFormat.Format = "dd/MM/yyyy";
            f.Column(13).Style.DateFormat.Format = "dd/MM/yyyy";
        });
    }

    // ── Helper comuni ───────────────────────────────────────────────
    private static void Foglio(XLWorkbook wb, string nomeFoglio, string titolo, string sottotitoloBase,
        string[] intestazioni, int nRighe, Action<IXLWorksheet, int> riempi)
    {
        var foglio = wb.Worksheets.Add(NomeValido(wb, nomeFoglio));
        int colonne = intestazioni.Length;

        var riga = StileFoglio.Titolo(foglio, titolo,
            $"{sottotitoloBase} · estratto il {DateTime.Now:dd/MM/yyyy HH:mm}", colonne);
        for (int i = 0; i < colonne; i++) foglio.Cell(riga, i + 1).Value = intestazioni[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        riempi(foglio, riga + 1);

        int ultima = riga + nRighe;
        StileFoglio.Rifinisci(foglio, riga, ultima, colonne);
        if (nRighe > 0) foglio.Range(riga, 1, ultima, colonne).SetAutoFilter();
    }

    private static byte[] Singolo(Action<XLWorkbook> componi)
    {
        using var wb = new XLWorkbook();
        componi(wb);
        using var flusso = new MemoryStream();
        wb.SaveAs(flusso);
        return flusso.ToArray();
    }

    /// <summary>Nome foglio valido e univoco (ClosedXML: max 31 caratteri, niente duplicati).</summary>
    private static string NomeValido(XLWorkbook wb, string nome)
    {
        var n = nome.Length > 31 ? nome[..31] : nome;
        var baseName = n;
        int k = 2;
        while (wb.Worksheets.Any(w => string.Equals(w.Name, n, StringComparison.OrdinalIgnoreCase)))
        {
            var suffisso = " (" + k++ + ")";
            n = (baseName.Length + suffisso.Length > 31 ? baseName[..(31 - suffisso.Length)] : baseName) + suffisso;
        }
        return n;
    }
}
