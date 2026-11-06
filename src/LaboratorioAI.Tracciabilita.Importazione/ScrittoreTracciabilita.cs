using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Importazione;
using LaboratorioAI.Tracciabilita.Dati;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Importazione;

public sealed class RisultatoScrittura
{
    public int FornitoriCreati, FornitoriGiaPresenti;
    public int CategorieCreate, CategorieGiaPresenti;
    public int ArticoliCreati, ArticoliGiaPresenti;
    public int ArticoliFornitoriCreati, MaterieSenzaArticolo;
    public int LottiCreati, LottiGiaPresenti;
    public int AccettazioniCreate, AccettazioniGiaPresenti;
    public int MovimentiCreati;
    public int LottiChiusiPerStorico, LottiInMagazzino;
    public int ChiusureDaDataNonAttendibile;
    public int ProfiliCreati, ProfiliGiaPresenti, CategorieConProfilo;
    public int ArticoliConCategoria;
    public int ControlliCreati, ScadenzeImpostate, GiudiziRespinti, GiudiziConRiserva;
}

/// <summary>
/// Scrive nel database «Tracciabilita» (solo quello: mai ArcLab né AZW).
/// Si può rilanciare: ciò che esiste già (fornitori per codice, articoli per nome, lotti, accettazioni) non si duplica.
/// Tutto avviene in una sola transazione: se qualcosa va storto non resta niente a metà.
/// </summary>
public static class ScrittoreTracciabilita
{
    private const int DimensioneLotto = 400;

    public static async Task<RisultatoScrittura> ScriviAsync(
        ContestoTracciabilita db, int idAzienda, string siglaSede,
        DatiAccess dati, PianoImportazione piano, DateTime adesso, CancellationToken ct = default)
    {
        var r = new RisultatoScrittura();

        var azienda = await db.Aziende.FirstOrDefaultAsync(a => a.IdAzienda == idAzienda, ct)
            ?? throw new InvalidOperationException($"L'azienda {idAzienda} non esiste.");
        var sede = await db.Insediamenti.FirstOrDefaultAsync(i => i.IdAzienda == idAzienda && i.Sigla == siglaSede, ct)
            ?? throw new InvalidOperationException($"La sede «{siglaSede}» non esiste per l'azienda {azienda.RagioneSociale}.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // ── Fornitori ──────────────────────────────────────────────
        var fornitori = (await db.Fornitori.Where(f => f.IdAzienda == idAzienda && f.CodiceEsterno != null).ToListAsync(ct))
            .ToDictionary(f => f.CodiceEsterno!, f => f.IdFornitore, StringComparer.OrdinalIgnoreCase);
        var nuoviFornitori = new List<(string Chiave, Fornitore F)>();

        foreach (var f in dati.Fornitori)
        {
            if (fornitori.ContainsKey(f.Codice)) { r.FornitoriGiaPresenti++; continue; }
            nuoviFornitori.Add((f.Codice, new Fornitore
            {
                IdAzienda = idAzienda, RagioneSociale = f.RagioneSociale, PartitaIva = f.PartitaIva, Indirizzo = f.Indirizzo,
                Cap = f.Cap, Citta = f.Localita, Provincia = f.Provincia, Telefono = f.Telefono, CodiceEsterno = f.Codice
            }));
        }
        foreach (var n in piano.FornitoriNuovi)
        {
            if (fornitori.ContainsKey(n.Chiave)) { r.FornitoriGiaPresenti++; continue; }
            nuoviFornitori.Add((n.Chiave, new Fornitore { IdAzienda = idAzienda, RagioneSociale = n.RagioneSociale, CodiceEsterno = n.Chiave }));
        }
        db.Fornitori.AddRange(nuoviFornitori.Select(x => x.F));
        await db.SaveChangesAsync(ct);
        foreach (var (chiave, f) in nuoviFornitori) fornitori[chiave] = f.IdFornitore;
        r.FornitoriCreati = nuoviFornitori.Count;

        // ── Categorie (le «matrici») ───────────────────────────────
        var nomiCategorie = piano.Categorie.Categorie.Count > 0
            ? piano.Categorie.Categorie
            : dati.Matrici.Select(m => m.Trim()).Where(m => m.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var categorieEsistenti = await db.CategorieMateriaPrima.Where(c => c.IdAzienda == idAzienda).ToListAsync(ct);
        var categorie = categorieEsistenti.GroupBy(c => c.Nome, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var nome in nomiCategorie)
        {
            if (categorie.ContainsKey(nome)) { r.CategorieGiaPresenti++; continue; }
            var nuova = new CategoriaMateriaPrima { IdAzienda = idAzienda, Nome = nome };
            db.CategorieMateriaPrima.Add(nuova);
            categorie[nome] = nuova;
            r.CategorieCreate++;
        }
        await db.SaveChangesAsync(ct);

        // ── Profili di controllo (matrici con gli stessi controlli = un profilo) ──
        var profiliEsistenti = (await db.ProfiliControllo.Where(p => p.IdAzienda == idAzienda).ToListAsync(ct))
            .GroupBy(p => p.Nome, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var pp in piano.Categorie.Profili)
        {
            if (profiliEsistenti.ContainsKey(pp.Nome)) { r.ProfiliGiaPresenti++; continue; }
            var profilo = new ProfiloControllo { IdAzienda = idAzienda, Nome = pp.Nome };
            foreach (var (tipo, istruzione) in pp.Righe)
                profilo.Righe.Add(new ProfiloControlloRiga { IdAzienda = idAzienda, Controllo = tipo, Obbligatorio = true, Istruzione = istruzione });
            db.ProfiliControllo.Add(profilo);
            profiliEsistenti[pp.Nome] = profilo;
            r.ProfiliCreati++;
        }
        await db.SaveChangesAsync(ct);
        foreach (var (nomeCat, nomeProfilo) in piano.Categorie.ProfiloPerCategoria)
        {
            if (categorie.TryGetValue(nomeCat, out var cat) && cat.IdProfiloControllo is null && profiliEsistenti.TryGetValue(nomeProfilo, out var prof))
            {
                cat.IdProfiloControllo = prof.IdProfiloControllo;
                r.CategorieConProfilo++;
            }
        }
        await db.SaveChangesAsync(ct);

        // ── Articoli ───────────────────────────────────────────────
        var articoliEsistenti = await db.Articoli.Where(a => a.IdAzienda == idAzienda).ToListAsync(ct);
        int? IdCategoriaDi(string nomeArticolo) =>
            piano.Categorie.CategoriaPerArticolo.TryGetValue(nomeArticolo, out var nc) && categorie.TryGetValue(nc, out var c) ? c.IdCategoria : null;
        foreach (var ae in articoliEsistenti.Where(a => a.IdCategoria is null))
            if (IdCategoriaDi(ae.Descrizione) is { } idc) { ae.IdCategoria = idc; r.ArticoliConCategoria++; }
        var articoli = articoliEsistenti
            .GroupBy(a => a.Descrizione, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().IdArticolo, StringComparer.OrdinalIgnoreCase);
        var nuoviArticoli = new List<Articolo>();
        foreach (var g in piano.Righe.GroupBy(x => x.NomeArticolo, StringComparer.OrdinalIgnoreCase))
        {
            if (articoli.ContainsKey(g.Key)) { r.ArticoliGiaPresenti++; continue; }
            nuoviArticoli.Add(new Articolo
            {
                IdAzienda = idAzienda, Descrizione = g.Key, Tipo = TipoArticolo.MateriaPrima, UnitaMisura = g.First().UnitaMisura,
                Bio = g.Key.Contains("bio", StringComparison.OrdinalIgnoreCase),
                IdCategoria = IdCategoriaDi(g.Key)
            });
            if (IdCategoriaDi(g.Key) is not null) r.ArticoliConCategoria++;
        }
        db.Articoli.AddRange(nuoviArticoli);
        await db.SaveChangesAsync(ct);
        foreach (var a in nuoviArticoli) articoli[a.Descrizione] = a.IdArticolo;
        r.ArticoliCreati = nuoviArticoli.Count;

        // ── Quale fornitore vende quale materia (Mat-fornitori) ────
        var perChiave = articoli.GroupBy(kv => NomiNormalizzati.ChiaveArticolo(kv.Key)).ToDictionary(g => g.Key, g => g.First().Value);
        var legami = (await db.ArticoliFornitori.Where(x => x.IdAzienda == idAzienda).Select(x => new { x.IdArticolo, x.IdFornitore }).ToListAsync(ct))
            .Select(x => (x.IdArticolo, x.IdFornitore)).ToHashSet();
        foreach (var m in dati.MaterieFornitori)
        {
            if (!perChiave.TryGetValue(NomiNormalizzati.ChiaveArticolo(m.Materia), out var idArt) ||
                !fornitori.TryGetValue(m.CodiceFornitore, out var idForn))
            {
                r.MaterieSenzaArticolo++;
                continue;
            }
            if (!legami.Add((idArt, idForn))) continue;
            db.ArticoliFornitori.Add(new ArticoloFornitore { IdAzienda = idAzienda, IdArticolo = idArt, IdFornitore = idForn });
            r.ArticoliFornitoriCreati++;
        }
        await db.SaveChangesAsync(ct);

        // ── Lotti ──────────────────────────────────────────────────
        (int, int, string) ChiaveLotto(RigaPronta x) => (articoli[x.NomeArticolo], fornitori[x.ChiaveFornitore], x.CodiceLotto.ToLowerInvariant());

        var lottiEsistenti = (await db.Lotti.Where(l => l.IdAzienda == idAzienda && l.Tipo == TipoLotto.Ingresso && l.IdFornitore != null).ToListAsync(ct))
            .GroupBy(l => (l.IdArticolo, l.IdFornitore!.Value, l.Codice.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.First().IdLotto);
        var nuoviLotti = new List<((int, int, string) Chiave, Lotto L)>();
        foreach (var g in piano.Righe.GroupBy(ChiaveLotto))
        {
            if (lottiEsistenti.ContainsKey(g.Key)) { r.LottiGiaPresenti++; continue; }
            var tutteChiuse = g.All(x => x.DataChiusura is not null);
            // Matrice del lotto: la più frequente tra le sue righe; se manca vale la categoria dell'articolo.
            var matriceLotto = g.Where(x => x.IdMatrice is > 0).GroupBy(x => x.IdMatrice!.Value)
                .OrderByDescending(x => x.Count()).ThenBy(x => x.Key).Select(x => (int?)x.Key).FirstOrDefault();
            int? idCategoriaLotto = matriceLotto is { } mid && piano.Categorie.CategoriaPerMatrice.TryGetValue(mid, out var ncl) && categorie.TryGetValue(ncl, out var cl)
                ? cl.IdCategoria
                : IdCategoriaDi(g.First().NomeArticolo);
            nuoviLotti.Add((g.Key, new Lotto
            {
                IdAzienda = idAzienda, IdArticolo = g.Key.Item1, IdFornitore = g.Key.Item2, Codice = g.First().CodiceLotto,
                IdCategoria = idCategoriaLotto,
                Tipo = TipoLotto.Ingresso, Stato = tutteChiuse ? StatoLotto.Esaurito : StatoLotto.Attivo,
                EnteBio = g.Select(x => x.EnteBio).FirstOrDefault(e => e is not null),
                // Più arrivi dello stesso lotto: si tiene la data più vicina (la più prudente).
                DataScadenza = g.Where(x => x.DataScadenza is not null).Select(x => x.DataScadenza).Min(),
                DataProduzioneFornitore = g.Where(x => x.DataProduzioneFornitore is not null).Select(x => x.DataProduzioneFornitore).Min()
            }));
            if (g.Any(x => x.DataScadenza is not null)) r.ScadenzeImpostate++;
            if (tutteChiuse) r.LottiChiusiPerStorico++; else r.LottiInMagazzino++;
        }
        db.Lotti.AddRange(nuoviLotti.Select(x => x.L));
        await db.SaveChangesAsync(ct);
        foreach (var (chiave, l) in nuoviLotti) lottiEsistenti[chiave] = l.IdLotto;
        r.LottiCreati = nuoviLotti.Count;

        // ── Accettazioni e movimenti ───────────────────────────────
        static string ChiaveAcc(int forn, int art, DateOnly arrivo, string? doc, decimal q, int lotto) =>
            ChiaveAccettazione.Crea(forn, art, arrivo, doc, q, lotto);

        var gia = (await db.Accettazioni.Where(a => a.IdAzienda == idAzienda)
                .Select(a => new { a.IdFornitore, a.IdArticolo, a.DataArrivo, a.NumeroDocumento, a.Quantita, a.IdLotto }).ToListAsync(ct))
            .Where(a => a.IdLotto != null)
            .Select(a => ChiaveAcc(a.IdFornitore, a.IdArticolo, a.DataArrivo, a.NumeroDocumento, a.Quantita, a.IdLotto!.Value))
            .ToHashSet();

        var daScrivere = new List<(RigaPronta Riga, Accettazione A)>();
        foreach (var x in piano.Righe)
        {
            var (idArt, idForn, _) = ChiaveLotto(x);
            var idLotto = lottiEsistenti[ChiaveLotto(x)];
            if (!gia.Add(ChiaveAcc(idForn, idArt, x.DataArrivo, x.NumeroDocumento, x.Quantita, idLotto)))
            {
                r.AccettazioniGiaPresenti++;
                continue;
            }
            daScrivere.Add((x, new Accettazione
            {
                IdAzienda = idAzienda, IdInsediamento = sede.IdInsediamento, IdFornitore = idForn, IdArticolo = idArt,
                DataArrivo = x.DataArrivo, TipoDocumento = x.TipoDocumento, NumeroDocumento = x.NumeroDocumento, DataDocumento = x.DataDocumento,
                Quantita = ChiaveAccettazione.Quantita(x.Quantita), UnitaMisura = x.UnitaMisura, Giudizio = x.Giudizio, IdLotto = idLotto,
                NoteReclami = $"Importata dal vecchio programma (ordine n. {x.Numero})" + (x.Note is null ? "" : " — " + x.Note)
            }));
        }

        for (var i = 0; i < daScrivere.Count; i += DimensioneLotto)
        {
            var blocco = daScrivere.Skip(i).Take(DimensioneLotto).ToList();
            db.Accettazioni.AddRange(blocco.Select(b => b.A));
            await db.SaveChangesAsync(ct);

            foreach (var (riga, acc) in blocco)
            {
                if (riga.Giudizio == GiudizioAccettazione.Respinto) r.GiudiziRespinti++;
                else if (riga.Giudizio == GiudizioAccettazione.AccettatoConRiserva) r.GiudiziConRiserva++;
                foreach (var c in riga.Controlli ?? Array.Empty<ControlloPronto>())
                {
                    db.AccettazioniControlli.Add(new AccettazioneControllo
                    {
                        IdAzienda = idAzienda, IdAccettazione = acc.IdAccettazione, Controllo = c.Tipo, Conforme = c.Conforme, Valore = c.Valore
                    });
                    r.ControlliCreati++;
                }
                if (riga.Quantita > 0)
                {
                    db.MovimentiLotto.Add(Movimento(idAzienda, sede.IdInsediamento, acc, TipoMovimento.Carico, riga.Quantita, riga.DataArrivo,
                        $"Carico importato dal vecchio programma (ordine n. {riga.Numero})"));
                    r.MovimentiCreati++;
                }
                if (riga.DataChiusura is { } chiusura && riga.Quantita > 0)
                {
                    db.MovimentiLotto.Add(Movimento(idAzienda, sede.IdInsediamento, acc, TipoMovimento.Rettifica, -riga.Quantita, chiusura,
                        "Storico importato: lotto già utilizzato nel vecchio programma" +
                        (riga.ChiusuraDaDataNonAttendibile ? " (data di impegno non attendibile: chiuso alla data di arrivo)" : "")));
                    r.MovimentiCreati++;
                }
                if (riga.ChiusuraDaDataNonAttendibile) r.ChiusureDaDataNonAttendibile++;
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
        r.AccettazioniCreate = daScrivere.Count;

        db.Audit.Add(new VoceAudit
        {
            IdAzienda = idAzienda,
            Azione = "ImportazioneAccess",
            Entita = "Azienda",
            IdEntita = idAzienda.ToString(),
            Dopo = $"Importati dal vecchio programma: {r.AccettazioniCreate} accettazioni, {r.LottiCreati} lotti, {r.FornitoriCreati} fornitori, {r.ArticoliCreati} articoli",
            DataOra = adesso
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return r;
    }

    private static MovimentoLotto Movimento(int idAzienda, int idSede, Accettazione acc, TipoMovimento tipo, decimal q, DateOnly giorno, string nota) =>
        new()
        {
            IdAzienda = idAzienda, IdInsediamento = idSede, IdLotto = acc.IdLotto!.Value, Tipo = tipo, Quantita = q,
            UnitaMisura = acc.UnitaMisura, DataMovimento = new DateTime(giorno.Year, giorno.Month, giorno.Day, 0, 0, 0, DateTimeKind.Utc),
            IdAccettazione = acc.IdAccettazione, Nota = nota
        };
}
