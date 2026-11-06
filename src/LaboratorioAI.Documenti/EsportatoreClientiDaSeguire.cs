using ClosedXML.Excel;
using LaboratorioAI.Core.Andamento;
using LaboratorioAI.Dati.Clienti;

namespace LaboratorioAI.Documenti;

/// <summary>Esporta in Excel la pagina «Clienti da seguire»: clienti con semaforo, Legionella, solo consulenza, analisi perse.</summary>
public interface IEsportatoreClientiDaSeguire
{
    byte[] Genera(RiepilogoClientiDaSeguire riepilogo);
    string NomeFile();
}

public class EsportatoreClientiDaSeguire : IEsportatoreClientiDaSeguire
{
    private const string Euro = "#,##0";
    private const string Perc = "0%";

    public string NomeFile() => $"ClientiDaSeguire_{DateTime.Now:yyyyMMdd}.xlsx";

    public static string Etichetta(Semaforo s) => s switch
    {
        Semaforo.Rosso => "ROSSO", Semaforo.Giallo => "GIALLO", Semaforo.Verde => "VERDE", _ => ""
    };

    public static string Etichetta(StatoLegionella s) => s switch
    {
        StatoLegionella.Perso => "Perso", StatoLegionella.InRitardo => "In ritardo", StatoLegionella.InCalo => "In calo",
        StatoLegionella.DaRicordare => "Da ricordare", StatoLegionella.Nuovo => "Nuovo", _ => "Regolare"
    };

    public byte[] Genera(RiepilogoClientiDaSeguire riepilogo)
    {
        var r = riepilogo.Risultato;
        var info = riepilogo.Info;
        InfoCliente I(string cod) => info.TryGetValue(cod, out var x) ? x : new InfoCliente { Codice = cod, RagioneSociale = $"Cliente {cod}" };
        var periodo = $"Periodo {DescrizionePeriodi.Completa(r)} (Legionella: ultimi 12 mesi contro i 12 precedenti). Estratto il {DateTime.Now:dd/MM/yyyy HH:mm}.";
        using var cartella = new XLWorkbook();

        // ── Clienti ──
        {
            var f = cartella.Worksheets.Add("Clienti");
            string[] h = { "Codice", "Ragione sociale", "Città", "Commerciale", "Assegnato a", "Responsabile", "Semaforo", "Motivi",
                "Analisi periodo di confronto €", "Analisi periodo attuale €", "Variazione", "Perdita €", "Analisi da inizio anno €", "Stesso periodo di confronto €",
                "Campioni confronto", "Campioni attuale", "Ultimo campione", "Giorni dall'ultimo", "Valore analisi perse €", "Altri servizi periodo attuale €",
                "Ultima azione", "Prossimo contatto" };
            var riga = StileFoglio.Titolo(f, "Clienti da seguire", periodo, h.Length);
            for (var i = 0; i < h.Length; i++) f.Cell(riga, i + 1).Value = h[i];
            StileFoglio.Intestazione(f, riga, h.Length, StileFoglio.Rosso);
            var prima = riga + 1;
            foreach (var c in r.Clienti.Where(x => x.Semaforo != Semaforo.Nessuno))
            {
                riga++;
                var i = I(c.Cliente);
                f.Cell(riga, 1).Value = c.Cliente;
                f.Cell(riga, 2).Value = i.RagioneSociale;
                f.Cell(riga, 3).Value = i.Citta ?? "";
                f.Cell(riga, 4).Value = i.CommercialeAzw ?? i.CodiceVenditore ?? "";
                f.Cell(riga, 5).Value = i.Assegnato ?? "";
                f.Cell(riga, 6).Value = i.Responsabile ?? i.Zona ?? "";
                f.Cell(riga, 7).Value = Etichetta(c.Semaforo);
                f.Cell(riga, 8).Value = string.Join(". ", c.Motivi);
                f.Cell(riga, 9).Value = c.LabPrecedenti;
                f.Cell(riga, 10).Value = c.LabUltimi;
                if (c.Variazione is decimal v) f.Cell(riga, 11).Value = v;
                f.Cell(riga, 12).Value = c.Perdita;
                f.Cell(riga, 13).Value = c.LabAnnoInCorso;
                f.Cell(riga, 14).Value = c.LabAnnoPrecedenteAllaData;
                f.Cell(riga, 15).Value = c.CampioniPrecedenti;
                f.Cell(riga, 16).Value = c.CampioniUltimi;
                if (c.UltimoCampione is DateTime u) f.Cell(riga, 17).Value = u;
                if (c.GiorniDallUltimo is int g) f.Cell(riga, 18).Value = g;
                f.Cell(riga, 19).Value = c.ValoreAnalisiPerse;
                f.Cell(riga, 20).Value = c.AltroUltimi;
                if (i.UltimaAzione is { } a) f.Cell(riga, 21).Value = $"{a.DataAzione:dd/MM/yyyy} {a.Tipo} – {a.Esito}";
                if (i.ProssimoContatto is DateTime p) f.Cell(riga, 22).Value = p;
                var colore = c.Semaforo switch
                {
                    Semaforo.Rosso => XLColor.FromHtml("#F4B6B6"), Semaforo.Giallo => XLColor.FromHtml("#FFE699"), _ => XLColor.FromHtml("#C6EFCE")
                };
                f.Cell(riga, 7).Style.Fill.BackgroundColor = colore;
            }
            foreach (var col in new[] { 9, 10, 12, 13, 14, 19, 20 }) f.Column(col).Style.NumberFormat.Format = Euro;
            f.Column(11).Style.NumberFormat.Format = Perc;
            f.Column(17).Style.NumberFormat.Format = "dd/mm/yyyy";
            f.Column(22).Style.NumberFormat.Format = "dd/mm/yyyy";
            StileFoglio.Rifinisci(f, prima - 1, riga, h.Length);
            if (riga >= prima) f.Range(prima - 1, 1, riga, h.Length).SetAutoFilter();
        }

        // ── Legionella ──
        {
            var f = cartella.Worksheets.Add("Legionella");
            string[] h = { "Codice", "Ragione sociale", "Città", "Responsabile", "Intermediario", "Stato", "Campioni prec.", "Campioni ultimi",
                "Fatturato Legionella prec. €", "Fatturato Legionella ultimi €", "Mesi abituali", "Intervallo atteso (gg)",
                "Ultimo campionamento", "Prossimo atteso", "Giorni al prossimo", "Telefono", "Ultima azione" };
            var riga = StileFoglio.Titolo(f, "Legionella", periodo, h.Length);
            for (var i = 0; i < h.Length; i++) f.Cell(riga, i + 1).Value = h[i];
            StileFoglio.Intestazione(f, riga, h.Length, StileFoglio.Rosso);
            var prima = riga + 1;
            foreach (var l in r.Legionella)
            {
                riga++;
                var i = I(l.Cliente);
                f.Cell(riga, 1).Value = l.Cliente;
                f.Cell(riga, 2).Value = i.RagioneSociale;
                f.Cell(riga, 3).Value = i.Citta ?? "";
                f.Cell(riga, 4).Value = i.Responsabile ?? i.Zona ?? "";
                f.Cell(riga, 5).Value = i.Intermediario ? "sì" : "";
                f.Cell(riga, 6).Value = Etichetta(l.Stato);
                f.Cell(riga, 7).Value = l.CampioniPrecedenti;
                f.Cell(riga, 8).Value = l.CampioniUltimi;
                f.Cell(riga, 9).Value = l.FatturatoPrecedenti;
                f.Cell(riga, 10).Value = l.FatturatoUltimi;
                f.Cell(riga, 11).Value = NomiMesi(l.MesiAbituali);
                f.Cell(riga, 12).Value = l.IntervalloAtteso;
                f.Cell(riga, 13).Value = l.UltimoCampionamento;
                f.Cell(riga, 14).Value = l.ProssimoAtteso;
                f.Cell(riga, 15).Value = l.GiorniAlProssimo;
                f.Cell(riga, 16).Value = i.Telefono ?? "";
                if (i.UltimaAzione is { } a) f.Cell(riga, 17).Value = $"{a.DataAzione:dd/MM/yyyy} {a.Tipo} – {a.Esito}";
            }
            f.Column(9).Style.NumberFormat.Format = Euro;
            f.Column(10).Style.NumberFormat.Format = Euro;
            f.Column(13).Style.NumberFormat.Format = "dd/mm/yyyy";
            f.Column(14).Style.NumberFormat.Format = "dd/mm/yyyy";
            StileFoglio.Rifinisci(f, prima - 1, riga, h.Length);
            if (riga >= prima) f.Range(prima - 1, 1, riga, h.Length).SetAutoFilter();
        }

        // ── Solo consulenza ──
        {
            var f = cartella.Worksheets.Add("Solo consulenza");
            string[] h = { "Codice", "Ragione sociale", "Città", "Commerciale", "Responsabile", "Altri servizi periodo attuale €", "Analisi periodo attuale €", "Analisi periodo di confronto €" };
            var riga = StileFoglio.Titolo(f, "Clienti che comprano altri servizi ma poche analisi", periodo, h.Length);
            for (var i = 0; i < h.Length; i++) f.Cell(riga, i + 1).Value = h[i];
            StileFoglio.Intestazione(f, riga, h.Length, StileFoglio.Rosso);
            var prima = riga + 1;
            foreach (var c in r.Clienti.Where(x => x.SoloConsulenza).OrderByDescending(x => x.AltroUltimi))
            {
                riga++;
                var i = I(c.Cliente);
                f.Cell(riga, 1).Value = c.Cliente;
                f.Cell(riga, 2).Value = i.RagioneSociale;
                f.Cell(riga, 3).Value = i.Citta ?? "";
                f.Cell(riga, 4).Value = i.CommercialeAzw ?? i.CodiceVenditore ?? "";
                f.Cell(riga, 5).Value = i.Responsabile ?? i.Zona ?? "";
                f.Cell(riga, 6).Value = c.AltroUltimi;
                f.Cell(riga, 7).Value = c.LabUltimi;
                f.Cell(riga, 8).Value = c.LabPrecedenti;
            }
            foreach (var col in new[] { 6, 7, 8 }) f.Column(col).Style.NumberFormat.Format = Euro;
            StileFoglio.Rifinisci(f, prima - 1, riga, h.Length);
            if (riga >= prima) f.Range(prima - 1, 1, riga, h.Length).SetAutoFilter();
        }

        // ── Analisi perse ──
        {
            var f = cartella.Worksheets.Add("Analisi perse");
            string[] h = { "Codice", "Ragione sociale", "Semaforo", "Id controllo", "Controllo", "Campioni periodo di confronto", "Valore a listino €" };
            var riga = StileFoglio.Titolo(f, "Controlli fatti nel periodo di confronto e non in quello attuale",
                "Un controllo sostituito in ArcLab da uno con codice nuovo compare come perso: leggere insieme ai campioni. " + periodo, h.Length);
            for (var i = 0; i < h.Length; i++) f.Cell(riga, i + 1).Value = h[i];
            StileFoglio.Intestazione(f, riga, h.Length, StileFoglio.Rosso);
            var prima = riga + 1;
            foreach (var c in r.Clienti.Where(x => x.Semaforo != Semaforo.Nessuno))
                foreach (var p in c.AnalisiPerse)
                {
                    riga++;
                    f.Cell(riga, 1).Value = c.Cliente;
                    f.Cell(riga, 2).Value = I(c.Cliente).RagioneSociale;
                    f.Cell(riga, 3).Value = Etichetta(c.Semaforo);
                    if (p.IdControllo is int id) f.Cell(riga, 4).Value = id;
                    f.Cell(riga, 5).Value = p.Controllo;
                    f.Cell(riga, 6).Value = p.Campioni;
                    f.Cell(riga, 7).Value = p.ValoreListino;
                }
            f.Column(7).Style.NumberFormat.Format = Euro;
            StileFoglio.Rifinisci(f, prima - 1, riga, h.Length);
            if (riga >= prima) f.Range(prima - 1, 1, riga, h.Length).SetAutoFilter();
        }

        using var ms = new MemoryStream();
        cartella.SaveAs(ms);
        return ms.ToArray();
    }

    private static readonly string[] Mesi = { "gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic" };
    public static string NomiMesi(IEnumerable<int> mesi) => string.Join(", ", mesi.Select(m => Mesi[(m - 1) % 12]));
}
