using ClosedXML.Excel;
using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.Direzione;

namespace LaboratorioAI.Web.Servizi;

/// <summary>Compensi soci (21/10/2026): fogli Excel del prospetto, della base laboratorio, dei rimborsi e dei percepiti.</summary>
public static class EsportazioneCompensi
{
    private const string Euro = "#,##0.00;[Red]-#,##0.00";

    /// <summary>Le righe del prospetto: etichetta e valore per socio (come le colonne del vecchio foglio Excel).</summary>
    public static readonly (string Etichetta, Func<ContoSocio, decimal> Valore, bool Totale)[] Righe =
    [
        ("Base", c => c.Base, false),
        ("Amministratore", c => c.Amministratore, false),
        ("Responsabile d'area", c => c.ResponsabileArea, false),
        ("Fisso", c => c.Fisso, true),
        ("RSPP", c => c.Rspp, false),
        ("Indennità laboratorio", c => c.IndennitaLaboratorio, false),
        ("Fisso + accessori", c => c.FissoEAccessori, true),
        ("Rimborsi trasferte", c => c.RimborsiTrasferte, false),
        ("Altri rimborsi spese", c => c.Rimborsi, false),
        ("Assicurazione auto", c => c.Assicurazione, false),
        ("Recupero anno prima", c => c.Recupero, false),
        ("Premio", c => c.Premio, false),
        ("TOTALE", c => c.Totale, true),
        ("Compensi percepiti", c => c.Percepito, false),
        ("DIFFERENZA", c => c.Differenza, true),
    ];

    public static void Prospetto(XLWorkbook libro, ProspettoCompensi p)
    {
        var ws = libro.AddWorksheet("Prospetto");
        var r = p.Risultato;
        ws.Cell(1, 1).Value = $"Compensi soci {p.Anno.Anno}" + (p.Anno.Chiuso ? " (anno chiuso)" : "");
        ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Cell(3, 1).Value = "Voce";
        for (var i = 0; i < r.Soci.Count; i++) ws.Cell(3, 2 + i).Value = r.Soci[i].Nome;
        ws.Cell(3, 2 + r.Soci.Count).Value = "Totale";
        ws.Range(3, 1, 3, 2 + r.Soci.Count).Style.Font.Bold = true;
        var riga = 4;
        foreach (var (etichetta, valore, totale) in Righe)
        {
            ws.Cell(riga, 1).Value = etichetta;
            for (var i = 0; i < r.Soci.Count; i++) ws.Cell(riga, 2 + i).Value = valore(r.Soci[i]);
            ws.Cell(riga, 2 + r.Soci.Count).Value = valore(r.Totali);
            if (totale) ws.Range(riga, 1, riga, 2 + r.Soci.Count).Style.Font.Bold = true;
            riga++;
        }
        ws.Range(4, 2, riga, 2 + r.Soci.Count).Style.NumberFormat.Format = Euro;
        riga++;
        var a = p.Anno;
        void Par(string t, decimal v) { ws.Cell(riga, 1).Value = t; ws.Cell(riga, 2).Value = v; riga++; }
        Par("Base a tempo pieno", a.BaseTempoPieno);
        Par("Ore settimanali del tempo pieno", a.OreTempoPieno);
        Par("Compenso amministratore (anno)", a.CompensoAmministratore);
        Par("Responsabile d'area", a.CompensoResponsabileArea);
        Par("Assicurazione auto (100%)", a.AssicurazioneAuto);
        Par("Premio", a.Premio);
        Par("% RSPP", a.PercentualeRspp);
        Par("% indennità laboratorio", a.PercentualeIndennitaLaboratorio);
        Par("Base laboratorio usata", r.BaseLaboratorioUsata);
        Par("Base laboratorio calcolata da AZW", r.BaseLaboratorioCalcolata);
        Par("Indennità laboratorio lorda", r.IndennitaLaboratorioLorda);
        Par("Premi area laboratorio", r.TotalePremiLaboratorio);
        ws.Columns().AdjustToContents();
    }

    public static void BaseLaboratorio(XLWorkbook libro, ProspettoCompensi p)
    {
        var ws = libro.AddWorksheet("Base laboratorio");
        ws.Cell(1, 1).Value = "Voce"; ws.Cell(1, 2).Value = "Descrizione"; ws.Cell(1, 3).Value = "Importo AZW";
        ws.Cell(1, 4).Value = "%"; ws.Cell(1, 5).Value = "Quota";
        ws.Range(1, 1, 1, 5).Style.Font.Bold = true;
        var r = 2;
        foreach (var v in p.Risultato.VociLaboratorio)
        {
            ws.Cell(r, 1).Value = v.Voce; ws.Cell(r, 2).Value = v.Descrizione; ws.Cell(r, 3).Value = v.ImportoAzw;
            ws.Cell(r, 4).Value = v.Percentuale; ws.Cell(r, 5).Value = v.Quota; r++;
        }
        ws.Cell(r, 1).Value = "Totale"; ws.Cell(r, 5).Value = p.Risultato.BaseLaboratorioCalcolata; ws.Row(r).Style.Font.Bold = true;
        r += 2;
        ws.Cell(r, 1).Value = "Premi area laboratorio"; ws.Cell(r, 1).Style.Font.Bold = true; r++;
        foreach (var x in p.Risultato.Premi)
        {
            ws.Cell(r, 1).Value = x.Nome; ws.Cell(r, 3).Value = x.Importo;
            if (x.IdSocio is int id) ws.Cell(r, 2).Value = "socio: " + p.Soci.FirstOrDefault(s => s.IdSocio == id)?.Nome;
            r++;
        }
        ws.Range(2, 3, r, 5).Style.NumberFormat.Format = Euro;
        ws.Columns().AdjustToContents();
    }

    public static void Rimborsi(XLWorkbook libro, List<RimborsoSpesa> rimborsi)
    {
        var ws = libro.AddWorksheet("Rimborsi");
        string[] h = ["Socio", "Data", "Categoria", "Descrizione", "Km", "Importo", "Maggiorazione %", "Importo rimborsato"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Range(1, 1, 1, h.Length).Style.Font.Bold = true;
        var r = 2;
        foreach (var x in rimborsi)
        {
            ws.Cell(r, 1).Value = x.Socio?.Nome; ws.Cell(r, 2).Value = x.Data; ws.Cell(r, 3).Value = x.Categoria?.Nome;
            ws.Cell(r, 4).Value = x.Descrizione; if (x.Km is decimal km) ws.Cell(r, 5).Value = km;
            ws.Cell(r, 6).Value = x.Importo; ws.Cell(r, 7).Value = x.Maggiorazione;
            ws.Cell(r, 8).Value = MotoreCompensiSoci.ImportoRimborso(x.Importo, x.Maggiorazione); r++;
        }
        ws.Column(2).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Range(2, 6, Math.Max(r, 2), 6).Style.NumberFormat.Format = Euro;
        ws.Range(2, 8, Math.Max(r, 2), 8).Style.NumberFormat.Format = Euro;
        ws.Columns().AdjustToContents();
    }

    public static void Percepiti(XLWorkbook libro, ProspettoCompensi p)
    {
        var ws = libro.AddWorksheet("Percepiti per mese");
        ws.Cell(1, 1).Value = "Socio";
        for (var m = 1; m <= 12; m++) ws.Cell(1, 1 + m).Value = new DateTime(2000, m, 1).ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
        ws.Cell(1, 14).Value = "Totale";
        ws.Range(1, 1, 1, 14).Style.Font.Bold = true;
        var r = 2;
        foreach (var c in p.Risultato.Soci)
        {
            ws.Cell(r, 1).Value = c.Nome;
            for (var m = 1; m <= 12; m++)
            {
                var v = p.PercepitiMese.Where(x => x.IdSocio == c.IdSocio && x.Mese == m).Sum(x => x.Importo);
                if (v != 0) ws.Cell(r, 1 + m).Value = v;
            }
            ws.Cell(r, 14).Value = p.PercepitiMese.Where(x => x.IdSocio == c.IdSocio).Sum(x => x.Importo);
            r++;
        }
        ws.Range(2, 2, r, 14).Style.NumberFormat.Format = Euro;
        ws.Columns().AdjustToContents();
    }

    /// <summary>22/10/2026: accessori e compensi maturati non pagati per area, con fatturato e margine del consuntivo.</summary>
    public static void Aree(XLWorkbook libro, IReadOnlyList<AreaAccessori> aree)
    {
        var ws = libro.AddWorksheet("Accessori per area");
        string[] h = ["Area", "Fatturato", "Margine prima AMM", "Margine dopo AMM", "Indennità laboratorio", "Premi area lab", "RSPP",
            "Responsabile d'area", "Premio soci", "Assicurazione", "Amministratore", "Totale accessori", "% su fatturato", "% su margine",
            "Accessori con quota AMM", "% su margine dopo AMM", "Rimborsi (a parte)", "Differenza da pagare", "Margine rettificato",
            "Quota AMM differenza", "Margine dopo AMM rettificato"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var a in aree)
        {
            var c = 1;
            ws.Cell(r, c++).Value = a.Etichetta; ws.Cell(r, c++).Value = a.Fatturato; ws.Cell(r, c++).Value = a.Margine; ws.Cell(r, c++).Value = a.MargineConAmm;
            ws.Cell(r, c++).Value = a.IndennitaLaboratorio; ws.Cell(r, c++).Value = a.PremiLaboratorio; ws.Cell(r, c++).Value = a.Rspp;
            ws.Cell(r, c++).Value = a.ResponsabileArea; ws.Cell(r, c++).Value = a.Premio; ws.Cell(r, c++).Value = a.Assicurazione;
            ws.Cell(r, c++).Value = a.Amministratore; ws.Cell(r, c++).Value = a.TotaleAccessori;
            if (a.SuFatturato is decimal sf) ws.Cell(r, c).Value = sf; c++;
            if (a.SuMargine is decimal sm) ws.Cell(r, c).Value = sm; c++;
            ws.Cell(r, c++).Value = a.AccessoriConAmm;
            if (a.SuMargineConAmm is decimal sa) ws.Cell(r, c).Value = sa; c++;
            ws.Cell(r, c++).Value = a.Rimborsi; ws.Cell(r, c++).Value = a.MaturatoNonPagato; ws.Cell(r, c++).Value = a.MargineRettificato;
            ws.Cell(r, c++).Value = a.QuotaMaturatoAmm; ws.Cell(r, c).Value = a.MargineConAmmRettificato;
            r++;
        }
        ws.Range(2, 2, r, 21).Style.NumberFormat.Format = Euro;
        foreach (var col in new[] { 13, 14, 16 }) ws.Range(2, col, r, col).Style.NumberFormat.Format = "0.0%";
        ws.Columns().AdjustToContents();
    }
}
