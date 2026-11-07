using System.Globalization;
using System.IO.Compression;
using System.Text;
using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Servizi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>
/// «Scarica tutti i miei dati»: un file ZIP con un CSV per argomento (si aprono con Excel) e un LEGGIMI.
/// Solo per la propria azienda; riservato a Qualità e Amministratore azienda; ogni scarico va nel registro attività.
/// </summary>
[Authorize]
[Route("Dati")]
public class DatiController : Controller
{
    private static readonly CultureInfo It = new("it-IT");
    private readonly ContestoTracciabilita _db;
    private readonly PermessiUtente _permessi;

    public DatiController(ContestoTracciabilita db, PermessiUtente permessi)
    {
        _db = db;
        _permessi = permessi;
    }

    private bool EGestore => User.HasClaim("tr:gestore", "true");

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        ViewBag.PuoScaricare = await _permessi.PuoConfigurareAsync(ct);
        return View();
    }

    [HttpPost("Scarica")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scarica(CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        if (!await _permessi.PuoConfigurareAsync(ct)) return Forbid();

        var azienda = await _db.Aziende.AsNoTracking().FirstOrDefaultAsync(ct);
        var fornitori = await _db.Fornitori.AsNoTracking().ToListAsync(ct);
        var clienti = await _db.Clienti.AsNoTracking().ToListAsync(ct);
        var categorie = await _db.CategorieMateriaPrima.AsNoTracking().ToListAsync(ct);
        var articoli = await _db.Articoli.AsNoTracking().ToListAsync(ct);
        var sedi = await _db.Insediamenti.AsNoTracking().ToListAsync(ct);
        var lotti = await _db.Lotti.AsNoTracking().ToListAsync(ct);
        var accettazioni = await _db.Accettazioni.AsNoTracking().ToListAsync(ct);
        var controlli = await _db.AccettazioniControlli.AsNoTracking().ToListAsync(ct);
        var produzioni = await _db.Produzioni.AsNoTracking().ToListAsync(ct);
        var consumi = await _db.ProduzioniConsumi.AsNoTracking().ToListAsync(ct);
        var spedizioni = await _db.Spedizioni.AsNoTracking().ToListAsync(ct);
        var righeSped = await _db.SpedizioniRighe.AsNoTracking().ToListAsync(ct);
        var ricette = await _db.Ricette.AsNoTracking().ToListAsync(ct);
        var versioni = await _db.RicetteVersioni.AsNoTracking().ToListAsync(ct);
        var righeRicetta = await _db.RicetteRighe.AsNoTracking().ToListAsync(ct);
        var richiami = await _db.Richiami.AsNoTracking().ToListAsync(ct);

        var nomeFornitore = fornitori.ToDictionary(f => f.IdFornitore, f => f.RagioneSociale);
        var nomeCliente = clienti.ToDictionary(c => c.IdCliente, c => c.RagioneSociale);
        var nomeCategoria = categorie.ToDictionary(c => c.IdCategoria, c => c.Nome);
        var nomeArticolo = articoli.ToDictionary(a => a.IdArticolo, a => a.Descrizione);
        var nomeSede = sedi.ToDictionary(s => s.IdInsediamento, s => s.Nome);
        var lottoPer = lotti.ToDictionary(l => l.IdLotto);

        string Forn(int? id) => id is { } k && nomeFornitore.TryGetValue(k, out var n) ? n : "";
        string Art(int? id) => id is { } k && nomeArticolo.TryGetValue(k, out var n) ? n : "";
        string Cat(int? id) => id is { } k && nomeCategoria.TryGetValue(k, out var n) ? n : "";
        string Sede(int id) => nomeSede.TryGetValue(id, out var n) ? n : "";
        string CodLotto(int? id) => id is { } k && lottoPer.TryGetValue(k, out var l) ? l.Codice : "";

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            Aggiungi(zip, "LEGGIMI.txt", Leggimi(azienda?.RagioneSociale ?? ""));

            Aggiungi(zip, "01_Fornitori.csv", Csv(
                new[] { "Ragione sociale", "Partita IVA", "Indirizzo", "CAP", "Città", "Provincia", "Telefono", "E-mail", "Note", "Attivo" },
                fornitori.OrderBy(f => f.RagioneSociale).Select(f => new object?[] { f.RagioneSociale, f.PartitaIva, f.Indirizzo, f.Cap, f.Citta, f.Provincia, f.Telefono, f.Email, f.Note, f.Attivo })));

            Aggiungi(zip, "02_Clienti.csv", Csv(
                new[] { "Ragione sociale", "Partita IVA", "Indirizzo", "CAP", "Città", "Provincia", "Telefono", "E-mail", "Note", "Attivo" },
                clienti.OrderBy(c => c.RagioneSociale).Select(c => new object?[] { c.RagioneSociale, c.PartitaIva, c.Indirizzo, c.Cap, c.Citta, c.Provincia, c.Telefono, c.Email, c.Note, c.Attivo })));

            Aggiungi(zip, "03_Categorie_materie_prime.csv", Csv(
                new[] { "Categoria", "Note", "In uso" },
                categorie.OrderBy(c => c.Nome).Select(c => new object?[] { c.Nome, c.Note, c.Attivo })));

            Aggiungi(zip, "04_Articoli.csv", Csv(
                new[] { "Codice", "Descrizione", "Tipo", "Categoria", "Unità", "Bio", "Note", "Attivo" },
                articoli.OrderBy(a => a.Descrizione).Select(a => new object?[] { a.Codice, a.Descrizione, a.Tipo.ToString(), Cat(a.IdCategoria), a.UnitaMisura, a.Bio, a.Note, a.Attivo })));

            var righeRic = new List<object?[]>();
            foreach (var r in ricette.OrderBy(x => x.Nome))
                foreach (var v in versioni.Where(x => x.IdRicetta == r.IdRicetta).OrderBy(x => x.Numero))
                    foreach (var rr in righeRicetta.Where(x => x.IdRicettaVersione == v.IdRicettaVersione).OrderBy(x => x.Ordine))
                        righeRic.Add(new object?[] { r.Nome, r.Attiva, v.Numero, v.ValidaDal, v.ValidaAl, v.QuantitaProdottaTeorica, rr.Etichetta ?? Cat(rr.IdCategoria), Cat(rr.IdCategoria), rr.QuantitaTeorica, rr.UnitaMisura, rr.LottiPerFornitore });
            Aggiungi(zip, "05_Ricette.csv", Csv(
                new[] { "Prodotto", "Ricetta in uso", "Versione", "Valida dal", "Valida al", "Kg prodotti di riferimento", "Ingrediente", "Categoria", "Quantità", "Unità", "Lotti per fornitore" },
                righeRic));

            Aggiungi(zip, "06_Lotti_ingresso_e_prodotti.csv", Csv(
                new[] { "Tipo", "Codice lotto", "Giorno lotto interno", "Articolo", "Fornitore", "Categoria", "Scadenza", "È TMC", "Data produzione fornitore", "Stato", "Motivo blocco" },
                lotti.OrderBy(l => l.IdLotto).Select(l => new object?[] { l.Tipo.ToString(), l.Codice, l.GiornoLottoInterno, Art(l.IdArticolo), Forn(l.IdFornitore), Cat(l.IdCategoria), l.DataScadenza, l.ETmc, l.DataProduzioneFornitore, l.Stato.ToString(), l.MotivoBlocco })));

            var righeAcc = new List<object?[]>();
            foreach (var a in accettazioni.OrderBy(x => x.DataArrivo).ThenBy(x => x.IdAccettazione))
            {
                var ctrl = controlli.Where(c => c.IdAccettazione == a.IdAccettazione).ToList();
                var nonConformi = string.Join("; ", ctrl.Where(c => c.Conforme == false).Select(c => c.Controllo.ToString()));
                righeAcc.Add(new object?[] { Sede(a.IdInsediamento), a.DataArrivo, a.TipoDocumento, a.NumeroDocumento, a.DataDocumento, Forn(a.IdFornitore), Art(a.IdArticolo), CodLotto(a.IdLotto), a.Quantita, a.UnitaMisura, a.Vettore, a.Giudizio.ToString(), nonConformi, a.NoteReclami });
            }
            Aggiungi(zip, "07_Arrivi_merce.csv", Csv(
                new[] { "Sede", "Data arrivo", "Tipo documento", "N. documento", "Data documento", "Fornitore", "Articolo", "Lotto", "Quantità", "Unità", "Vettore", "Giudizio", "Controlli non conformi", "Note e reclami" },
                righeAcc));

            var righeCtrl = new List<object?[]>();
            foreach (var c in controlli.OrderBy(x => x.IdAccettazione).ThenBy(x => x.Controllo))
            {
                var a = accettazioni.FirstOrDefault(x => x.IdAccettazione == c.IdAccettazione);
                righeCtrl.Add(new object?[] { a?.DataArrivo, a?.NumeroDocumento, Forn(a?.IdFornitore), Art(a?.IdArticolo), CodLotto(a?.IdLotto), c.Controllo.ToString(), c.Conforme is null ? "" : (c.Conforme == true ? "Conforme" : "Non conforme"), c.Valore, c.Note });
            }
            Aggiungi(zip, "08_Controlli_all_arrivo.csv", Csv(
                new[] { "Data arrivo", "N. documento", "Fornitore", "Articolo", "Lotto", "Controllo", "Esito", "Valore", "Note" },
                righeCtrl));

            var righeProd = new List<object?[]>();
            foreach (var p in produzioni.OrderBy(x => x.Giorno).ThenBy(x => x.IdProduzione))
            {
                var cons = consumi.Where(c => c.IdProduzione == p.IdProduzione).ToList();
                if (cons.Count == 0) cons.Add(new ProduzioneConsumo());
                foreach (var c in cons)
                {
                    lottoPer.TryGetValue(c.IdLottoIngresso, out var li);
                    righeProd.Add(new object?[] { Sede(p.IdInsediamento), p.Giorno, Art(p.IdArticolo), CodLotto(p.IdLottoProdotto), p.Quantita, p.UnitaMisura, p.Annullata, p.Note,
                        Cat(c.IdCategoria), li?.Codice, Art(li?.IdArticolo), Forn(li?.IdFornitore), li?.DataScadenza, c.Quantita, c.UnitaMisura });
                }
            }
            Aggiungi(zip, "09_Produzioni_e_lotti_usati.csv", Csv(
                new[] { "Sede", "Giorno", "Prodotto", "Lotto prodotto", "Quantità prodotta", "Unità", "Annullata", "Note", "Ingrediente", "Lotto usato", "Articolo usato", "Fornitore", "Scadenza lotto usato", "Quantità usata", "Unità usata" },
                righeProd));

            var righeSpe = new List<object?[]>();
            foreach (var s in spedizioni.OrderBy(x => x.DataSpedizione).ThenBy(x => x.IdSpedizione))
            {
                var righe = righeSped.Where(r => r.IdSpedizione == s.IdSpedizione).ToList();
                if (righe.Count == 0) righe.Add(new SpedizioneRiga());
                foreach (var r in righe)
                {
                    lottoPer.TryGetValue(r.IdLotto, out var li);
                    righeSpe.Add(new object?[] { Sede(s.IdInsediamento), s.DataSpedizione, s.NumeroDocumento, s.DataDocumento, nomeCliente.GetValueOrDefault(s.IdCliente, ""), s.Annullata, s.Note, Art(li?.IdArticolo), li?.Codice, r.Quantita, r.UnitaMisura });
                }
            }
            Aggiungi(zip, "10_Spedizioni.csv", Csv(
                new[] { "Sede", "Data", "N. documento", "Data documento", "Cliente", "Annullata", "Note", "Prodotto", "Lotto", "Quantità", "Unità" },
                righeSpe));

            Aggiungi(zip, "11_Richiami_e_simulazioni.csv", Csv(
                new[] { "Tipo", "Lotto di partenza", "Motivo", "Data avvio", "Data chiusura", "Secondi impiegati", "Esito" },
                richiami.OrderBy(r => r.DataAvvio).Select(r => new object?[] { r.Tipo.ToString(), CodLotto(r.IdLottoOrigine), r.Motivo, r.DataAvvio.ToLocalTime(), r.DataChiusura?.ToLocalTime(), r.SecondiImpiegati, r.Esito })));
        }

        _db.Audit.Add(new VoceAudit
        {
            IdUtente = _db.IdUtenteCorrente, Azione = "DatiScaricati", Entita = "Azienda", IdEntita = (azienda?.IdAzienda ?? 0).ToString(),
            Dopo = "Scarico completo dei dati (ZIP con CSV)", IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        var nome = "Tracciato_dati_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + ".zip";
        return File(memoria.ToArray(), "application/zip", nome);
    }

    private static void Aggiungi(ZipArchive zip, string nome, string testo)
    {
        var voce = zip.CreateEntry(nome, CompressionLevel.Optimal);
        using var flusso = voce.Open();
        var byteTesto = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(testo)).ToArray();
        flusso.Write(byteTesto, 0, byteTesto.Length);
    }

    /// <summary>CSV per Excel italiano: separatore «;», date gg/mm/aaaa, decimali con la virgola, UTF-8.</summary>
    private static string Csv(string[] intestazioni, IEnumerable<object?[]> righe)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", intestazioni.Select(Cella)));
        foreach (var r in righe) sb.AppendLine(string.Join(";", r.Select(v => Cella(Testo(v)))));
        return sb.ToString();
    }

    private static string Testo(object? v) => v switch
    {
        null => "",
        bool b => b ? "Sì" : "No",
        DateOnly d => d.ToString("dd/MM/yyyy", It),
        DateTime d => d.ToString("dd/MM/yyyy HH:mm", It),
        decimal m => m.ToString("0.###", It),
        IFormattable f => f.ToString(null, It),
        _ => v.ToString() ?? ""
    };

    private static string Cella(string s)
    {
        // Evita che Excel interpreti un testo come formula.
        if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@') && !decimal.TryParse(s, NumberStyles.Any, It, out _)) s = "'" + s;
        return s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    private static string Leggimi(string azienda) =>
        "TRACCIATO – COPIA DEI DATI DI " + azienda.ToUpperInvariant() + "\r\n" +
        "Scaricata il " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", It) + "\r\n\r\n" +
        "Questo file ZIP contiene tutti i dati inseriti in Tracciato, un file per argomento.\r\n" +
        "I file .csv si aprono con Excel (doppio clic dopo aver estratto lo ZIP). Se i caratteri accentati\r\n" +
        "non si vedono bene, in Excel usare Dati > Da testo/CSV e scegliere «UTF-8».\r\n\r\n" +
        "01 Fornitori, 02 Clienti, 03 Categorie di materie prime, 04 Articoli, 05 Ricette,\r\n" +
        "06 Lotti (di ingresso e prodotti), 07 Arrivi merce, 08 Controlli fatti all'arrivo,\r\n" +
        "09 Produzioni con i lotti usati (una riga per ogni lotto usato), 10 Spedizioni (una riga per lotto),\r\n" +
        "11 Richiami e simulazioni.\r\n\r\n" +
        "Le date sono nel formato gg/mm/aaaa e le ore sono quelle italiane.\r\n\r\n" +
        "IMPORTANTE: il servizio è in prova e MARE.A non garantisce il backup dei dati.\r\n" +
        "Conservate una copia di questo file in un luogo sicuro e scaricatelo a intervalli regolari\r\n" +
        "(ad esempio una volta alla settimana).\r\n";
}
