using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Limiti normativi, fase 1a (17/10/2026): citazioni, codici scheda, famiglie, chi valida, testo delle fonti.</summary>
public class LimitiNormativiTest
{
    private const string Testo = "Concentrazione di Legionella\n> 1.000 UFC/L e\n≤ 10.000 UFC/L: verificare che siano in atto le misure di control-\nlo elencate nella valutazione del rischio.";

    private static bool Presente(string citazione)
    {
        var n = RegoleLimiti.NormalizzaTesto(Testo);
        return RegoleLimiti.CitazionePresente(citazione, n, RegoleLimiti.Compatta(n));
    }

    [Fact] public void Citazione_TolleraACapoSillabazioneEVirgolette() =>
        Assert.True(Presente("“> 1.000 UFC/L e ≤ 10.000 UFC/L: verificare che siano in atto le misure di controllo”"));

    [Fact] public void Citazione_InventataOCorta_Scartata()
    {
        Assert.False(Presente("> 100 UFC/L e ≤ 1.000 UFC/L: verificare"));
        Assert.False(Presente("1.000 UFC"));
    }

    [Fact] public void Codice_ProssimoLiberoDellaFamiglia()
    {
        Assert.Equal("LEG-010", RegoleLimiti.ProssimoCodice(FamigliaNormativa.Legionella, ["LEG-001", "LEG-009", "ACQ-020"]));
        Assert.Equal("MOL-001", RegoleLimiti.ProssimoCodice(FamigliaNormativa.Molluschi, []));
    }

    [Theory]
    [InlineData("Acqua sanitaria", FamigliaNormativa.Legionella)]
    [InlineData("Ghiaccio", FamigliaNormativa.AcqueConsumoUmano)]
    [InlineData("Molluschi bivalvi vivi - vongole", FamigliaNormativa.Molluschi)]
    [InlineData("Tampone superficie", FamigliaNormativa.SuperficiAria)]
    [InlineData("Formaggio", FamigliaNormativa.Alimenti)]
    public void Famiglia_DallaMatriceArcLab(string matrice, FamigliaNormativa attesa) =>
        Assert.Equal(attesa, RegoleLimiti.FamigliaDaMatrice(matrice));

    [Fact] public void Validazione_NonDaChiProponeOCorregge()
    {
        var s = new LimiteNormativo { Stato = StatoSchedaLimite.Proposta, IdUtenteProposta = 5 };
        Assert.NotNull(RegoleLimiti.MotivoNonValidabile(s, 5));
        Assert.Null(RegoleLimiti.MotivoNonValidabile(s, 6));
        s.IdUtenteModifica = 6;
        Assert.NotNull(RegoleLimiti.MotivoNonValidabile(s, 6));
        s.Stato = StatoSchedaLimite.Validata;
        Assert.NotNull(RegoleLimiti.MotivoNonValidabile(s, 7));
    }

    [Fact] public void Html_TabelleLeggibiliEDataVigente()
    {
        var t = TestoFonti.DaHtml("<html><head><script>x=1</script></head><body><table><tr><td>Legionella</td><td>&gt; 1.000&nbsp;UFC/L</td></tr></table><p>Testo vigente al 25/02/2024</p></body></html>");
        Assert.Contains("Legionella | > 1.000 UFC/L", t);
        Assert.DoesNotContain("x=1", t);
        Assert.Equal(new DateTime(2024, 2, 25), TestoFonti.DataAggiornamento(t));
    }

    [Fact] public void Impronta_UgualeSeCambiaSoloLaSpaziatura() =>
        Assert.Equal(RegoleLimiti.Impronta("A  b\n c"), RegoleLimiti.Impronta("a b c"));

    [Fact] public void PaginaDiControlloDelSito_NonEUnaFonte()
    {
        const string blocco = "Si prega di abilitare i cookie\n\nQuesto sito web utilizza un servizio di sicurezza per proteggersi dagli attacchi online. Il servizio richiede i cookie completi\nSupporto per la visualizzazione del sito web.\n\nPagina di convalida del browser\n\nwww.salute.gov.it utilizza un servizio di sicurezza per la protezione contro gli attacchi online.\nID di riferimento 3a8802b85dbbf9dd33fbb9f858196862";
        Assert.True(TestoFonti.PaginaDiBlocco(blocco));
        Assert.False(TestoFonti.PaginaDiBlocco(string.Concat(Enumerable.Repeat(Testo + "\n", 60))));
    }

    [Theory]
    [InlineData("https://eur-lex.europa.eu/legal-content/IT/TXT/HTML/?uri=CELEX:02005R2073-20240225", "Cellar", "32005R2073")]
    [InlineData("https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX:32020L2184", "Cellar", "32020L2184")]
    [InlineData("https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX%3A32019R0627", "Cellar", "32019R0627")]
    [InlineData("https://www.normattiva.it/uri-res/N2Ls?urn:nir:stato:decreto.legislativo:2023-02-23;18", "Normattiva", "urn:nir:stato:decreto.legislativo:2023-02-23;18")]
    [InlineData("https://www.salute.gov.it/imgs/C_17_pubblicazioni_2362_allegato.pdf", null, null)]
    public void CanaleAutomatico_DallIndirizzo(string url, string? canale, string? codice)
    {
        var c = TestoFonti.CanaleAutomatico(url);
        Assert.Equal(canale, c.Canale);
        Assert.Equal(codice, c.Codice);
    }

    [Fact] public void Html_EurLex_RigaDiTabellaSenzaSegniDelConsolidato()
    {
        const string html = "<table><tr>\n <td valign=\"top\">\n  <p>1.17.  Molluschi bivalvi vivi</p>\n </td>\n <td><p><span>Salmonella</span></p></td><td><p>5 </p></td><td><p>0 </p></td>"
            + "<td><p><span><a href=\"x\"><span>►M9</span></a></span> Non rilevabile<span> ◄ </span> in 25 g</p></td></tr>"
            + "<tr><td><p>1.25. Molluschi</p></td><td><p>E. coli ( 7 )</p></td><td><p>230 MPN/100 g</p></td></tr></table>";
        var t = TestoFonti.DaHtml(html);
        Assert.Contains("1.17. Molluschi bivalvi vivi | Salmonella | 5 | 0 | Non rilevabile in 25 g", t);
        Assert.Contains("1.25. Molluschi | E. coli | 230 MPN/100 g", t);
        var n = RegoleLimiti.NormalizzaTesto(t);
        Assert.True(RegoleLimiti.CitazionePresente("Molluschi bivalvi vivi … Salmonella … Non rilevabile in 25 g", n, RegoleLimiti.Compatta(n)));
        Assert.False(RegoleLimiti.CitazionePresente("Molluschi bivalvi vivi | Salmonella | 5 | 0 | Assente in 25 g", n, RegoleLimiti.Compatta(n)));
    }

    [Fact] public void Citazione_RigaDiTabellaBreve()
    {
        var n = RegoleLimiti.NormalizzaTesto("Antimonio | 10 | \u03BCg/l\nArsenico | 10 | \u03BCg/l");
        var c = RegoleLimiti.Compatta(n);
        Assert.True(RegoleLimiti.CitazionePresente("Antimonio | 10 | \u00B5g/l", n, c));
        Assert.False(RegoleLimiti.CitazionePresente("Antimonio | 5 | \u00B5g/l", n, c));
        Assert.False(RegoleLimiti.CitazionePresente("10 \u00B5g/l", n, c));
    }

    [Fact] public void Citazione_RigaCortissima_SoloConUnita()
    {
        var n = RegoleLimiti.NormalizzaTesto("Rame | 2,0 | mg/l\nNitrati | 15");
        var c = RegoleLimiti.Compatta(n);
        Assert.True(RegoleLimiti.CitazionePresente("Rame | 2,0 | mg/l", n, c));
        Assert.False(RegoleLimiti.CitazionePresente("Nitrati | 15", n, c));
    }

    [Fact] public void FamiglieInCoda_AndataERitorno()
    {
        var t = RegoleLimiti.TestoCoda([FamigliaNormativa.Molluschi, FamigliaNormativa.Alimenti]);
        Assert.Equal([FamigliaNormativa.Molluschi, FamigliaNormativa.Alimenti], RegoleLimiti.FamiglieDaCoda(t));
        Assert.Null(RegoleLimiti.FamiglieDaCoda(RegoleLimiti.TestoCoda(null)));
        Assert.Equal(FamigliaNormativa.AcqueConsumoUmano, RegoleLimiti.DaCodiceFamiglia("acque"));
        Assert.Null(RegoleLimiti.DaCodiceFamiglia("boh"));
    }

    private static LimiteNormativo Sc(int id, string par, decimal? v, string unita, string cit) => new()
    {
        IdLimiteNormativo = id, CodiceScheda = "ACQ-" + id, Parametro = par, Valore = v, Unita = unita, Citazione = cit,
        Operatore = OperatoreLimite.ValoreMassimo, TipoLimite = TipoLimiteNormativo.Sicurezza, Famiglia = FamigliaNormativa.AcqueConsumoUmano
    };

    [Fact] public void ControlliAutomatici_TrovanoValoreSbagliatoAbbinamentoEContraddizioni()
    {
        var schede = new List<SchedaDaControllare>
        {
            new(Sc(1, "Arsenico", 10, "µg/l", "Arsenico | 10 | μg/l"), [("Arsenico", ["µg/L"], null)]),
            new(Sc(3, "Nitrati", 50, "mg/l", "Nitrati | 5 | mg/l"), [("Piombo", ["µg/l"], null)]),
            new(Sc(5, "Arsenico", 5, "µg/l", "Arsenico 5 µg/l"), [("Arsenico", ["µg/l"], null)]),
            new(Sc(6, "Escherichia coli (E. coli)", 0, "numero/100 ml", "Escherichia coli (E. coli) | 0 | numero/100 ml"), [("E. coli", ["UFC/100 ml"], null)]),
        };
        var r = ControlliSchede.Esegui(schede);
        Assert.Contains(r, x => x.IdLimiteNormativo == 3 && x.Regola == "valore-citazione");
        Assert.Contains(r, x => x.IdLimiteNormativo == 3 && x.Regola == "nome-arclab");
        Assert.Contains(r, x => x.IdLimiteNormativo == 1 && x.Regola == "contraddizione");
        Assert.DoesNotContain(r, x => x.IdLimiteNormativo == 6);
        Assert.DoesNotContain(r, x => x.IdLimiteNormativo == 1 && x.Regola != "contraddizione");
    }

    [Fact] public void NumeriInTesto_ItalianoEInglese()
    {
        var n = ControlliSchede.NumeriInTesto("> 1.000 UFC/L e ≤ 10.000; 0,5 mg/l; 2.5 µg");
        Assert.Contains(1000m, n); Assert.Contains(10000m, n); Assert.Contains(0.5m, n); Assert.Contains(2.5m, n);
    }
}
