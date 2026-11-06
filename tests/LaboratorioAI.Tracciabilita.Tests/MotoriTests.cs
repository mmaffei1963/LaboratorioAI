using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Motori;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class GeneratoreCodiceLottoTests
{
    [Fact]
    public void GiornoDellAnno_23_ottobre_2026_e_296()
    {
        var codice = GeneratoreCodiceLotto.Genera(FormatoLotto.GiornoDellAnno, new DateOnly(2026, 10, 23), null, 1, false);
        Assert.Equal("296", codice);
    }

    [Theory]
    [InlineData(2, false, "CAT", "296-2")]
    [InlineData(1, true, "CAT", "296-CAT")]
    [InlineData(2, true, "cat", "296-CAT-2")]
    [InlineData(1, true, null, "296")]
    public void GiornoDellAnno_con_progressivo_e_sigla(int progressivo, bool sigla, string? siglaSede, string atteso)
    {
        var codice = GeneratoreCodiceLotto.Genera(FormatoLotto.GiornoDellAnno, new DateOnly(2026, 10, 23), siglaSede, progressivo, sigla);
        Assert.Equal(atteso, codice);
    }

    [Fact]
    public void FormatoDataSedeProgressivo()
    {
        var codice = GeneratoreCodiceLotto.Genera(FormatoLotto.DataSedeProgressivo, new DateOnly(2026, 10, 23), "Cat", 3, false);
        Assert.Equal("261023-CAT-03", codice);
    }

    [Fact]
    public void Progressivo_zero_non_ammesso()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GeneratoreCodiceLotto.Genera(FormatoLotto.GiornoDellAnno, new DateOnly(2026, 1, 1), null, 0, false));
    }

    [Theory]
    [InlineData(" ca-t! ", "CAT")]
    [InlineData("abcdefghi", "ABCDEF")]
    [InlineData("  ", null)]
    [InlineData("--", null)]
    public void Sigla_normalizzata(string? ingresso, string? atteso) =>
        Assert.Equal(atteso, GeneratoreCodiceLotto.NormalizzaSigla(ingresso));
}

public class SelettoreLottiTests
{
    private static readonly DateOnly Giorno = new(2026, 10, 23);
    private static readonly IngredienteRichiesto Farina = new(42, "Farina");

    private static LottoInUsoCandidato Lotto(int id, int cat, string codice, int giorniFa, int? finitoGiorniFa = null,
        DateOnly? scadenza = null, StatoLotto stato = StatoLotto.Attivo) =>
        new(id, cat, codice, new DateTime(2026, 10, 23, 8, 0, 0).AddDays(-giorniFa),
            finitoGiorniFa is { } f ? new DateTime(2026, 10, 23, 18, 0, 0).AddDays(-f) : null,
            scadenza, stato);

    [Fact]
    public void Propone_il_lotto_in_uso()
    {
        var r = SelettoreLotti.Proponi(new[] { Farina }, new[] { Lotto(1, 42, "F1", 3) }, Giorno, CriterioProposta.OrdineDiArrivo);
        var p = Assert.Single(r);
        Assert.False(p.Mancante);
        Assert.Equal(1, Assert.Single(p.Lotti).IdLotto);
    }

    [Fact]
    public void Nessun_lotto_segnala_ingrediente_mancante()
    {
        var r = SelettoreLotti.Proponi(new[] { Farina }, Array.Empty<LottoInUsoCandidato>(), Giorno, CriterioProposta.OrdineDiArrivo);
        Assert.True(r[0].Mancante);
        Assert.Contains(r[0].Avvisi, a => a.Contains("Nessun lotto"));
    }

    [Fact]
    public void Lotto_finito_prima_del_giorno_non_si_propone()
    {
        var r = SelettoreLotti.Proponi(new[] { Farina }, new[] { Lotto(1, 42, "F1", 10, finitoGiorniFa: 2) }, Giorno, CriterioProposta.OrdineDiArrivo);
        Assert.True(r[0].Mancante);
    }

    [Fact]
    public void Lotto_non_ancora_iniziato_non_si_propone()
    {
        var r = SelettoreLotti.Proponi(new[] { Farina }, new[] { Lotto(1, 42, "F1", -2) }, Giorno, CriterioProposta.OrdineDiArrivo);
        Assert.True(r[0].Mancante);
    }

    [Theory]
    [InlineData(StatoLotto.Bloccato)]
    [InlineData(StatoLotto.Richiamato)]
    public void Lotti_bloccati_o_richiamati_mai_proposti(StatoLotto stato)
    {
        var r = SelettoreLotti.Proponi(new[] { Farina }, new[] { Lotto(1, 42, "F1", 3, stato: stato) }, Giorno, CriterioProposta.OrdineDiArrivo);
        Assert.True(r[0].Mancante);
        Assert.Contains(r[0].Avvisi, a => a.Contains("F1"));
    }

    [Fact]
    public void Lotto_scaduto_si_propone_con_avviso()
    {
        var r = SelettoreLotti.Proponi(new[] { Farina }, new[] { Lotto(1, 42, "F1", 3, scadenza: new DateOnly(2026, 10, 1)) }, Giorno, CriterioProposta.OrdineDiArrivo);
        var lotto = Assert.Single(r[0].Lotti);
        Assert.Contains(lotto.Avvisi, a => a.Contains("scaduto"));
    }

    [Fact]
    public void Piu_lotti_in_uso_si_propongono_tutti_con_avviso()
    {
        var lotti = new[] { Lotto(1, 42, "F1", 5, scadenza: new DateOnly(2027, 1, 1)), Lotto(2, 42, "F2", 1, scadenza: new DateOnly(2026, 12, 1)) };
        var r = SelettoreLotti.Proponi(new[] { Farina }, lotti, Giorno, CriterioProposta.ScadenzaPiuVicina);
        Assert.Equal(new[] { 2, 1 }, r[0].Lotti.Select(l => l.IdLotto));
        Assert.Contains(r[0].Avvisi, a => a.Contains("Più lotti"));
    }

    [Fact]
    public void Ordine_di_arrivo_mette_il_piu_vecchio_per_primo()
    {
        var lotti = new[] { Lotto(1, 42, "F1", 1, scadenza: new DateOnly(2026, 12, 1)), Lotto(2, 42, "F2", 5, scadenza: new DateOnly(2027, 1, 1)) };
        var r = SelettoreLotti.Proponi(new[] { Farina }, lotti, Giorno, CriterioProposta.OrdineDiArrivo);
        Assert.Equal(new[] { 2, 1 }, r[0].Lotti.Select(l => l.IdLotto));
    }

    [Fact]
    public void Categorie_diverse_non_si_mescolano()
    {
        var sale = new IngredienteRichiesto(7, "Sale");
        var r = SelettoreLotti.Proponi(new[] { Farina, sale }, new[] { Lotto(1, 42, "F1", 3), Lotto(2, 7, "S1", 3) }, Giorno, CriterioProposta.OrdineDiArrivo);
        Assert.Equal(1, r[0].Lotti.Single().IdLotto);
        Assert.Equal(2, r[1].Lotti.Single().IdLotto);
    }
}

public class MotoreRintracciabilitaTests
{
    // Farina(1) + Acqua(2) -> Impasto(10) [produzione 100]; Impasto(10) + Sale(3) -> Piadina(20) [produzione 101]
    // Piadina(20) spedita a cliente 500 e 501; Farina(1) usata anche per Pane(30) [produzione 102] spedito a 502.
    private static MotoreRintracciabilita Motore() => new(
        new[]
        {
            new ArcoConsumo(100, 1, 10, new DateOnly(2026, 10, 20), 5m),
            new ArcoConsumo(100, 2, 10, new DateOnly(2026, 10, 20), 3m),
            new ArcoConsumo(101, 10, 20, new DateOnly(2026, 10, 21), 8m),
            new ArcoConsumo(101, 3, 20, new DateOnly(2026, 10, 21), 1m),
            new ArcoConsumo(102, 1, 30, new DateOnly(2026, 10, 22), 4m),
        },
        new[]
        {
            new ArcoSpedizione(900, 20, 500, new DateOnly(2026, 10, 22), "DDT1", 50m),
            new ArcoSpedizione(901, 20, 501, new DateOnly(2026, 10, 22), "DDT2", 30m),
            new ArcoSpedizione(902, 30, 502, new DateOnly(2026, 10, 23), "DDT3", 20m),
        });

    [Fact]
    public void Indietro_dalla_piadina_arriva_agli_ingredienti_dei_semilavorati()
    {
        var r = Motore().Indietro(20);
        Assert.Equal(new[] { 1, 2, 3, 10, 20 }, r.Lotti.Select(l => l.IdLotto).OrderBy(x => x));
        Assert.Equal(new[] { 100, 101 }, r.Produzioni);
        Assert.Equal(2, r.Lotti.Single(l => l.IdLotto == 1).Livello);
        Assert.Equal(1, r.Lotti.Single(l => l.IdLotto == 3).Livello);
    }

    [Fact]
    public void Avanti_dalla_farina_trova_tutti_i_prodotti_e_i_clienti()
    {
        var r = Motore().Avanti(1);
        Assert.Equal(new[] { 1, 10, 20, 30 }, r.Lotti.Select(l => l.IdLotto).OrderBy(x => x));
        Assert.Equal(new[] { 500, 501, 502 }, r.Clienti);
        Assert.Equal(3, r.Spedizioni.Count);
        Assert.Equal(new[] { 100, 101, 102 }, r.Produzioni);
    }

    [Fact]
    public void Avanti_dal_sale_non_raggiunge_il_pane()
    {
        var r = Motore().Avanti(3);
        Assert.DoesNotContain(r.Lotti, l => l.IdLotto == 30);
        Assert.Equal(new[] { 500, 501 }, r.Clienti);
    }

    [Fact]
    public void Lotto_senza_collegamenti_restituisce_solo_se_stesso()
    {
        var r = Motore().Avanti(999);
        Assert.Single(r.Lotti);
        Assert.Empty(r.Clienti);
    }

    [Fact]
    public void Un_ciclo_nei_dati_non_blocca_la_ricerca()
    {
        var m = new MotoreRintracciabilita(new[]
        {
            new ArcoConsumo(1, 1, 2, new DateOnly(2026, 1, 1), null),
            new ArcoConsumo(2, 2, 1, new DateOnly(2026, 1, 2), null),
        }, Array.Empty<ArcoSpedizione>());
        var r = m.Avanti(1);
        Assert.Equal(2, r.Lotti.Count);
    }

    [Fact]
    public void Catena_troppo_lunga_viene_segnalata_come_troncata()
    {
        var archi = Enumerable.Range(1, 10).Select(i => new ArcoConsumo(i, i, i + 1, new DateOnly(2026, 1, 1), null));
        var r = new MotoreRintracciabilita(archi, Array.Empty<ArcoSpedizione>(), maxLivelli: 3).Avanti(1);
        Assert.True(r.Troncato);
        Assert.Equal(4, r.Lotti.Count);
    }
}

public class GiacenzaETestStorni
{
    [Fact]
    public void La_giacenza_e_la_somma_dei_movimenti()
    {
        var mov = new[]
        {
            new MovimentoGiacenza(1, 1, 100m), new MovimentoGiacenza(1, 1, -30m), new MovimentoGiacenza(1, 2, 10m),
        };
        Assert.Equal(70m, Giacenza.DelLotto(mov, 1, 1));
        Assert.Equal(80m, Giacenza.DelLotto(mov, 1));
    }

    [Fact]
    public void Giacenze_negative_individuate()
    {
        var mov = new[] { new MovimentoGiacenza(1, 1, 10m), new MovimentoGiacenza(1, 1, -15m), new MovimentoGiacenza(2, 1, 5m) };
        var neg = Giacenza.Negative(mov);
        var n = Assert.Single(neg);
        Assert.Equal((1, 1, -5m), n);
    }

    private static MovimentoLotto Originale() => new()
    {
        IdMovimento = 7, IdAzienda = 1, IdInsediamento = 2, IdLotto = 3, Tipo = TipoMovimento.Consumo,
        Quantita = -12.5m, UnitaMisura = "kg", IdProduzione = 9
    };

    [Fact]
    public void Lo_storno_annulla_la_quantita_e_rimanda_all_originale()
    {
        var oraUtc = new DateTime(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);
        var s = StornoMovimento.Crea(Originale(), oraUtc, 5, " errore di digitazione ");
        Assert.Equal(TipoMovimento.Storno, s.Tipo);
        Assert.Equal(12.5m, s.Quantita);
        Assert.Equal(7, s.IdMovimentoStornato);
        Assert.Equal(9, s.IdProduzione);
        Assert.Equal("errore di digitazione", s.Nota);
        Assert.Equal(0m, Giacenza.DelLotto(new[]
        {
            new MovimentoGiacenza(3, 2, -12.5m), new MovimentoGiacenza(3, 2, s.Quantita)
        }, 3));
    }

    [Fact]
    public void Storno_senza_motivo_o_di_uno_storno_o_di_movimento_non_registrato_non_ammesso()
    {
        var ora = DateTime.UtcNow;
        Assert.Throws<ArgumentException>(() => StornoMovimento.Crea(Originale(), ora, null, " "));
        var storno = Originale(); storno.Tipo = TipoMovimento.Storno;
        Assert.Throws<InvalidOperationException>(() => StornoMovimento.Crea(storno, ora, null, "x"));
        var nuovo = Originale(); nuovo.IdMovimento = 0;
        Assert.Throws<InvalidOperationException>(() => StornoMovimento.Crea(nuovo, ora, null, "x"));
    }
}

public class ControlliCoerenzaTests
{
    [Fact]
    public void Produzioni_con_ingredienti_senza_lotto()
    {
        var r = ControlliCoerenza.ProduzioniIncomplete(new[]
        {
            new ProduzioneDaControllare(1, new DateOnly(2026, 10, 23), new[] { 1, 2, 3 }, new[] { 1, 2, 3 }),
            new ProduzioneDaControllare(2, new DateOnly(2026, 10, 23), new[] { 1, 2, 3 }, new[] { 1 }),
        });
        var x = Assert.Single(r);
        Assert.Equal(2, x.IdProduzione);
        Assert.Equal(new[] { 2, 3 }, x.CategorieMancanti);
    }

    [Fact]
    public void Lotti_in_uso_scaduti_bloccati_o_richiamati()
    {
        var oggi = new DateOnly(2026, 10, 23);
        var r = ControlliCoerenza.LottiInUsoDaVerificare(new[]
        {
            new LottoInUsoDaControllare(1, 1, "A", null, StatoLotto.Bloccato, true),
            new LottoInUsoDaControllare(2, 1, "B", new DateOnly(2026, 10, 1), StatoLotto.Attivo, true),
            new LottoInUsoDaControllare(3, 1, "C", new DateOnly(2026, 10, 1), StatoLotto.Attivo, false),   // non più in uso
            new LottoInUsoDaControllare(4, 1, "D", new DateOnly(2027, 1, 1), StatoLotto.Attivo, true),     // regolare
            new LottoInUsoDaControllare(5, 1, "E", null, StatoLotto.Richiamato, true),
        }, oggi);
        Assert.Equal(new[] { 1, 2, 5 }, r.Select(x => x.IdLotto));
    }
}
