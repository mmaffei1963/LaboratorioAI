using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Importazione;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class LetturaOrdineTests
{
    [Theory]
    [InlineData("26/09/2022", 2022, 9, 26)]
    [InlineData("07/2022", 2022, 7, 31)]      // mese/anno: ultimo giorno del mese
    [InlineData("02/2024", 2024, 2, 29)]
    [InlineData("301223", 2023, 12, 30)]
    [InlineData(" 6/9/2022 ", 2022, 9, 6)]
    public void Date_leggibili(string testo, int a, int m, int g) =>
        Assert.Equal(new DateOnly(a, m, g), LetturaOrdine.Data(testo));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("boh")]
    [InlineData("31/02/2022")]
    public void Date_non_leggibili_restano_vuote(string? testo) => Assert.Null(LetturaOrdine.Data(testo));

    [Theory]
    [InlineData("Conforme - Accettato - 100", GiudizioAccettazione.Accettato)]
    [InlineData("Non Conforme - Respinto NCG - 20", GiudizioAccettazione.Respinto)]
    [InlineData("Non Conforme - NCL- 75", GiudizioAccettazione.AccettatoConRiserva)]
    [InlineData(null, GiudizioAccettazione.Accettato)]
    public void Giudizi(string? testo, GiudizioAccettazione atteso) => Assert.Equal(atteso, LetturaOrdine.Giudizio(testo));

    [Theory]
    [InlineData("Kg", "kg")]
    [InlineData("Litri", "l")]
    [InlineData("Pezzi", "pz")]
    [InlineData("", null)]
    [InlineData("scatole", null)]
    public void Unita(string? testo, string? atteso) => Assert.Equal(atteso, LetturaOrdine.Unita(testo));

    [Theory]
    [InlineData("Non Richiesto", true)]
    [InlineData("Prelievo non richiesto", true)]
    [InlineData("Controllo Pallet  non richiesto", true)]
    [InlineData("Non applicabile", true)]
    [InlineData("x", true)]
    [InlineData("Non deve contenere impurità", false)]
    [InlineData("Pallet controllato", false)]
    public void Controlli_non_richiesti(string testo, bool atteso) => Assert.Equal(atteso, LetturaOrdine.ControlloNonRichiesto(testo));

    private static RigaMagazzino R(string? integrita = null, string? sottovuoto = null, string? contr = null, string? accT = null) =>
        new(1, null, null, null, "f", "a", 1, "kg", "L", null, null, null,
            Integrita: integrita, Sottovuoto: sottovuoto, ControlloTemperatura: contr, AccettazioneTemperatura: accT);

    [Fact]
    public void Integra_e_confezione()
    {
        var c = Assert.Single(LetturaOrdine.Controlli(R("Integra", "Sfusa")));
        Assert.Equal(TipoControllo.Confezione, c.Tipo);
        Assert.True(c.Conforme);
        Assert.Equal("Sfusa", c.Valore);
    }

    [Fact]
    public void Non_integra_e_non_conforme() =>
        Assert.False(Assert.Single(LetturaOrdine.Controlli(R("Non Integra"))).Conforme);

    [Fact]
    public void Temperatura_conforme()
    {
        var c = Assert.Single(LetturaOrdine.Controlli(R(contr: "Eseguito per contatto con mano", accT: "Conforme")));
        Assert.Equal(TipoControllo.Temperatura, c.Tipo);
        Assert.True(c.Conforme);
    }

    [Fact]
    public void Riga_senza_controlli() => Assert.Empty(LetturaOrdine.Controlli(R()));
}

public class PianoCategorieTests
{
    private static RigaPronta P(string articolo, int? idMatrice) =>
        new(1, "F", articolo, "L", new DateOnly(2025, 1, 1), null, null, null, 1, "kg", null, false, null, IdMatrice: idMatrice);

    private static DatiAccess D(MatriceAccess[] matrici, RigaControlliMatrice[] righe, TestoControllo[] testi) =>
        new(Array.Empty<RigaMagazzino>(), Array.Empty<FornitoreAccess>(), Array.Empty<MateriaFornitoreAccess>(),
            matrici.Select(m => m.Nome).ToList(), matrici, righe, testi);

    private static readonly TestoControllo[] Testi =
    {
        new(TipoControllo.Temperatura, 1, "Non Richiesto"),
        new(TipoControllo.Temperatura, 2, "Max 7°C"),
        new(TipoControllo.Confezione, 1, "Integra")
    };

    [Fact]
    public void Categoria_dell_articolo_e_la_piu_frequente()
    {
        var dati = D(new[] { new MatriceAccess(1, "Farina"), new MatriceAccess(2, "Lievito") }, Array.Empty<RigaControlliMatrice>(), Testi);
        var piano = PianificaCategorie.Costruisci(dati, new[] { P("Farina 00", 1), P("Farina 00", 1), P("Farina 00", 2) });
        Assert.Equal("Farina", piano.CategoriaPerArticolo["Farina 00"]);
        Assert.Contains(piano.Segnalazioni, s => s.Contains("più matrici"));
    }

    [Fact]
    public void Matrici_con_stessi_controlli_hanno_un_solo_profilo_e_i_non_richiesti_non_entrano()
    {
        var dati = D(new[] { new MatriceAccess(1, "Sale"), new MatriceAccess(2, "Zucchero"), new MatriceAccess(3, "Mozzarella") },
            new[]
            {
                new RigaControlliMatrice(1, null, 1, 1, null, null, null, null),
                new RigaControlliMatrice(2, null, 1, 1, null, null, null, null),
                new RigaControlliMatrice(3, null, 1, 2, null, null, null, null)
            }, Testi);
        var piano = PianificaCategorie.Costruisci(dati, new[] { P("a", 1), P("b", 2), P("c", 3) });
        Assert.Equal(2, piano.Profili.Count);
        var due = piano.Profili.Single(p => p.Categorie.Count == 2);
        Assert.Equal(new[] { TipoControllo.Confezione }, due.Righe.Select(r => r.Tipo));   // «Non Richiesto» escluso
        Assert.Equal(piano.ProfiloPerCategoria["Sale"], piano.ProfiloPerCategoria["Zucchero"]);
        Assert.NotEqual(piano.ProfiloPerCategoria["Sale"], piano.ProfiloPerCategoria["Mozzarella"]);
    }

    [Fact]
    public void Stesso_nome_su_due_matrici_diventa_una_categoria()
    {
        var dati = D(new[] { new MatriceAccess(1, "Lievito"), new MatriceAccess(2, "lievito") }, Array.Empty<RigaControlliMatrice>(), Testi);
        var piano = PianificaCategorie.Costruisci(dati, new[] { P("x", 2) });
        Assert.Single(piano.Categorie);
        Assert.Contains(piano.Segnalazioni, s => s.Contains("unite"));
    }

    [Fact]
    public void Matrice_usata_ma_senza_nome_viene_segnalata()
    {
        var dati = D(Array.Empty<MatriceAccess>(), Array.Empty<RigaControlliMatrice>(), Testi);
        var piano = PianificaCategorie.Costruisci(dati, new[] { P("x", 32) });
        Assert.Contains("Matrice senza nome (#32)", piano.Categorie);
    }

    [Fact]
    public void Matrice_rinominata_con_era_N_riprende_il_vecchio_numero()
    {
        var dati = D(new[] { new MatriceAccess(115, "xFarina 00 in sacchi(era 32)"), new MatriceAccess(1, "Farina 0 in sacchi") },
            new[] { new RigaControlliMatrice(32, null, 1, 1, null, null, null, null) }, Testi);
        var piano = PianificaCategorie.Costruisci(dati, new[] { P("Farina bianca", 32) });
        Assert.Contains("Farina 00 in sacchi", piano.Categorie);
        Assert.DoesNotContain(piano.Categorie, c => c.Contains("(era"));
        Assert.Equal("Farina 00 in sacchi", piano.CategoriaPerArticolo["Farina bianca"]);
        Assert.True(piano.ProfiloPerCategoria.ContainsKey("Farina 00 in sacchi"));   // ha i controlli della vecchia #32
    }

    [Fact]
    public void Matrice_zero_vale_nessuna_matrice()
    {
        var dati = D(new[] { new MatriceAccess(1, "Sale") }, Array.Empty<RigaControlliMatrice>(), Testi);
        var piano = PianificaCategorie.Costruisci(dati, new[] { P("x", 0) });
        Assert.Empty(piano.CategoriaPerArticolo);
        Assert.DoesNotContain(piano.Categorie, c => c.Contains("senza nome"));
    }
}
