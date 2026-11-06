namespace LaboratorioAI.Tracciabilita.Core.Importazione;

public sealed record Segnalazione(string Problema, int Righe, IReadOnlyList<int> Esempi);

public sealed class PianoProva
{
    public required DatiAccess Dati { get; init; }
    public required IReadOnlyList<PropostaFornitore> Fornitori { get; init; }
    public required IReadOnlyList<GruppoArticolo> Articoli { get; init; }
    public required IReadOnlyList<(GruppoArticolo A, GruppoArticolo B, double Punteggio)> Duplicati { get; init; }
    public required IReadOnlyList<Segnalazione> Segnalazioni { get; init; }
    public required int CodiciLottoCondivisi { get; init; }
}

/// <summary>Prepara la «prova»: cosa c'è nel vecchio programma, cosa si abbina da solo, cosa va guardato. Non scrive nulla.</summary>
public static class Prova
{
    public static PianoProva Prepara(DatiAccess dati, DateTime oggi)
    {
        var anagrafica = dati.Fornitori.Select(f => new FornitoreNoto(f.Codice, f.RagioneSociale)).ToList();

        var fornitori = dati.Magazzino
            .Where(r => !string.IsNullOrWhiteSpace(r.Fornitore))
            .GroupBy(r => r.Fornitore!.Trim())
            .Select(g => AbbinamentoNomi.ProponiFornitore(g.Key, g.Count(), anagrafica))
            .OrderByDescending(p => p.Esito).ThenByDescending(p => p.Righe)
            .ToList();

        var articoli = AbbinamentoNomi.RaggruppaArticoli(
            dati.Magazzino.Where(r => !string.IsNullOrWhiteSpace(r.Articolo))
                .GroupBy(r => r.Articolo!.Trim()).Select(g => (g.Key, g.Count())));

        var condivisi = dati.Magazzino
            .Where(r => !string.IsNullOrWhiteSpace(r.Lotto))
            .GroupBy(r => r.Lotto!.Trim().ToLowerInvariant())
            .Count(g => g.Select(r => NomiNormalizzati.ChiaveFornitore(r.Fornitore)).Distinct().Count() > 1);

        return new PianoProva
        {
            Dati = dati,
            Fornitori = fornitori,
            Articoli = articoli,
            Duplicati = AbbinamentoNomi.DuplicatiProbabili(articoli),
            Segnalazioni = Controlla(dati.Magazzino, oggi),
            CodiciLottoCondivisi = condivisi
        };
    }

    private static IReadOnlyList<Segnalazione> Controlla(IReadOnlyList<RigaMagazzino> righe, DateTime oggi)
    {
        var limite = oggi.AddDays(31);
        var regole = new (string Problema, Func<RigaMagazzino, bool> Vale)[]
        {
            ("Fornitore vuoto", r => string.IsNullOrWhiteSpace(r.Fornitore)),
            ("Articolo vuoto", r => string.IsNullOrWhiteSpace(r.Articolo)),
            ("Codice lotto vuoto", r => string.IsNullOrWhiteSpace(r.Lotto)),
            ("Quantità vuota o zero", r => r.Quantita is null or 0),
            ("Quantità negativa", r => r.Quantita < 0),
            ("Data del documento mancante", r => r.DataDocumento is null),
            ("Data del documento implausibile (prima del 2004 o nel futuro)", r => r.DataDocumento is { } d && (d.Year < 2004 || d > limite)),
            ("Data di impegno mancante (lotto mai messo in uso?)", r => r.DataImpegno is null),
            ("Data di impegno implausibile (prima del 2004 o oltre un anno nel futuro)", r => r.DataImpegno is { } d && (d.Year < 2004 || d > oggi.AddYears(1))),
            ("Impegno precedente alla data del documento", r => r.DataImpegno is { } i && r.DataDocumento is { } d && i < d),
        };

        return regole
            .Select(x =>
            {
                var coinvolte = righe.Where(x.Vale).ToList();
                return new Segnalazione(x.Problema, coinvolte.Count, coinvolte.Take(5).Select(r => r.Numero).ToList());
            })
            .Where(s => s.Righe > 0)
            .ToList();
    }
}
