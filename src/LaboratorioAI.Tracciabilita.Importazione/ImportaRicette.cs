using System.Text;
using System.Text.Json;
using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Dati;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Importazione;

/// <summary>Un ingrediente di una ricetta Access: una matrice e quanti lotti per fornitore proporre.</summary>
public sealed record IngredienteRicettaAccess(int IdMatrice, string? NomeMatrice, int LottiPerFornitore);

/// <summary>Una ricetta scelta da Mirko nel foglio «ricette_da_confermare.csv».</summary>
public sealed class RicettaDaImportare
{
    public int Numero { get; init; }
    public string Nome { get; init; } = "";
    public string MacroAccess { get; init; } = "";
    public List<IngredienteRicettaAccess> Ingredienti { get; init; } = new();
}

/// <summary>
/// Importa le ricette dalle macro del vecchio Access. Legge tre file nella cartella di lavoro
/// (RicetteAccess): ricette_da_macro.json (ingredienti per macro), matrici.txt (numero e nome della matrice)
/// e ricette_da_confermare.csv (nome scelto e SI/NO). Non tocca il file Access.
/// </summary>
public static class ImportaRicette
{
    public sealed class Piano
    {
        public List<RicettaDaImportare> Ricette { get; } = new();
        public List<string> Avvisi { get; } = new();
    }

    public sealed class Risultato
    {
        public int RicetteCreate, RicetteGiaPresenti, RicetteCompletate, ArticoliCreati, ArticoliRiusati, IngredientiCreati, CategorieCreate;
        public List<string> Segnalazioni { get; } = new();
    }

    static ImportaRicette() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static string LeggiTesto(string percorso)
    {
        var byte_ = File.ReadAllBytes(percorso);
        try { return new UTF8Encoding(false, true).GetString(byte_).TrimStart('﻿'); }
        catch (DecoderFallbackException) { return Encoding.GetEncoding(1252).GetString(byte_); }
    }

    public static Piano Prepara(string cartellaRicette)
    {
        var piano = new Piano();
        var fileCsv = Path.Combine(cartellaRicette, "ricette_da_confermare.csv");
        var fileJson = Path.Combine(cartellaRicette, "ricette_da_macro.json");
        var fileMatrici = Path.Combine(cartellaRicette, "matrici.txt");
        foreach (var f in new[] { fileCsv, fileJson, fileMatrici })
            if (!File.Exists(f)) throw new FileNotFoundException("Manca il file " + f);

        var matrici = new Dictionary<int, string>();
        foreach (var riga in LeggiTesto(fileMatrici).Split('\n'))
        {
            var p = riga.Split('|', 2);
            if (p.Length == 2 && int.TryParse(p[0].Trim(), out var id)) matrici[id] = p[1].Trim();
        }

        using var json = JsonDocument.Parse(File.ReadAllText(fileJson, Encoding.UTF8));
        var macro = json.RootElement.EnumerateArray().ToList();

        var righeCsv = LeggiTesto(fileCsv).Split('\n').Select(r => r.TrimEnd('\r')).Where(r => r.Length > 0).Skip(1).ToList();
        foreach (var riga in righeCsv)
        {
            var c = riga.Split(';');
            if (c.Length < 6 || !int.TryParse(c[0].Trim(), out var n)) continue;
            if (!c[2].Trim().Equals("SI", StringComparison.OrdinalIgnoreCase)) continue;
            var nome = string.Join(' ', c[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (nome.Length == 0) { piano.Avvisi.Add($"Riga {n}: nome vuoto, saltata."); continue; }
            if (n < 1 || n > macro.Count) { piano.Avvisi.Add($"Riga {n}: non corrisponde a nessuna macro, saltata."); continue; }
            var m = macro[n - 1];
            var nomeMacro = m.GetProperty("macro").GetString() ?? "";
            if (!c[5].Trim().Equals(nomeMacro.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                piano.Avvisi.Add($"Riga {n} («{nome}»): la macro nel foglio («{c[5].Trim()}») non è quella attesa («{nomeMacro}»): saltata.");
                continue;
            }
            var ingredienti = new List<IngredienteRicettaAccess>();
            foreach (var g in m.GetProperty("ingredienti").EnumerateArray().GroupBy(i => i.GetProperty("idMatrice").GetInt32()))
            {
                var top = g.Select(i => i.TryGetProperty("top", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 2).DefaultIfEmpty(2).Max();
                ingredienti.Add(new IngredienteRicettaAccess(g.Key, matrici.GetValueOrDefault(g.Key), Math.Clamp(top, 1, 10)));
            }
            piano.Ricette.Add(new RicettaDaImportare { Numero = n, Nome = nome, MacroAccess = nomeMacro, Ingredienti = ingredienti });
        }

        foreach (var g in piano.Ricette.GroupBy(r => r.Nome, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            piano.Avvisi.Add($"Il nome «{g.Key}» compare più volte (righe {string.Join(", ", g.Select(x => x.Numero))}): importo solo la prima.");
        return piano;
    }

    public static async Task<Risultato> ScriviAsync(ContestoTracciabilita db, int idAzienda, Piano piano, DateOnly oggi, CancellationToken ct = default)
    {
        var r = new Risultato();
        var azienda = await db.Aziende.FirstOrDefaultAsync(a => a.IdAzienda == idAzienda, ct)
            ?? throw new InvalidOperationException($"L'azienda {idAzienda} non esiste.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var categorie = (await db.CategorieMateriaPrima.Where(c => c.IdAzienda == idAzienda).ToListAsync(ct))
            .GroupBy(c => c.Nome.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var ricetteEsistenti = (await db.Ricette.Where(x => x.IdAzienda == idAzienda).Select(x => x.Nome).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var articoli = (await db.Articoli.Where(a => a.IdAzienda == idAzienda && a.Tipo == TipoArticolo.ProdottoFinito).ToListAsync(ct))
            .GroupBy(a => a.Descrizione.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var rc in piano.Ricette)
        {
            // Le categorie che mancano in Tracciato si creano (senza profilo di controllo: si abbina poi da «Categorie e controlli»).
            foreach (var ing in rc.Ingredienti.Where(i => i.NomeMatrice is not null))
            {
                var nomeCat = ing.NomeMatrice!.Trim();
                if (categorie.ContainsKey(nomeCat)) continue;
                var nuova = new CategoriaMateriaPrima { IdAzienda = idAzienda, Nome = nomeCat };
                db.CategorieMateriaPrima.Add(nuova);
                await db.SaveChangesAsync(ct);
                categorie[nomeCat] = nuova;
                r.CategorieCreate++;
            }

            Ricetta? ricetta;
            RicettaVersione versione;
            var nuovaRicetta = false;
            if (ricetteEsistenti.Contains(rc.Nome))
            {
                // Ricetta già presente: si aggiungono solo gli ingredienti che mancano.
                ricetta = await db.Ricette.Include(x => x.Versioni).ThenInclude(v => v.Righe)
                    .FirstOrDefaultAsync(x => x.IdAzienda == idAzienda && x.Nome == rc.Nome, ct);
                versione = ricetta?.Versioni.OrderByDescending(v => v.Numero).FirstOrDefault()!;
                if (ricetta is null || versione is null) { r.RicetteGiaPresenti++; continue; }
            }
            else
            {
                if (!articoli.TryGetValue(rc.Nome, out var articolo))
                {
                    articolo = new Articolo { IdAzienda = idAzienda, Descrizione = rc.Nome, Tipo = TipoArticolo.ProdottoFinito, UnitaMisura = "kg" };
                    db.Articoli.Add(articolo);
                    await db.SaveChangesAsync(ct);
                    articoli[rc.Nome] = articolo;
                    r.ArticoliCreati++;
                }
                else r.ArticoliRiusati++;
                versione = new RicettaVersione { IdAzienda = idAzienda, Numero = 1, ValidaDal = oggi };
                ricetta = new Ricetta { IdAzienda = idAzienda, IdArticolo = articolo.IdArticolo, Nome = rc.Nome };
                ricetta.Versioni.Add(versione);
                db.Ricette.Add(ricetta);
                nuovaRicetta = true;
            }

            var ordine = versione.Righe.Count == 0 ? 0 : versione.Righe.Max(x => x.Ordine);
            var aggiunti = 0;
            foreach (var ing in rc.Ingredienti)
            {
                if (ing.NomeMatrice is null) { r.Segnalazioni.Add($"«{rc.Nome}»: la matrice {ing.IdMatrice} non esiste nell'elenco delle matrici: ingrediente saltato."); continue; }
                var cat = categorie[ing.NomeMatrice.Trim()];
                if (versione.Righe.Any(x => x.IdCategoria == cat.IdCategoria)) continue;
                versione.Righe.Add(new RicettaRiga { IdAzienda = idAzienda, IdCategoria = cat.IdCategoria, Ordine = ++ordine, LottiPerFornitore = ing.LottiPerFornitore });
                aggiunti++;
            }
            await db.SaveChangesAsync(ct);
            r.IngredientiCreati += aggiunti;
            ricetteEsistenti.Add(rc.Nome);
            if (nuovaRicetta) r.RicetteCreate++;
            else if (aggiunti > 0) r.RicetteCompletate++;
            else r.RicetteGiaPresenti++;
        }
        await tx.CommitAsync(ct);
        return r;
    }
}
