using LaboratorioAI.Tracciabilita.Core.Importazione;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class AbbinamentoNomiTests
{
    private static readonly FornitoreNoto[] Anagrafica =
    {
        new("1", "Molino Naldoni S.r.l."),
        new("2", "Padovana Macinazione srl"),
        new("3", "Sidac s.p.a."),
        new("4", "Molino Spadoni Spa"),
    };

    [Theory]
    [InlineData("MOLINO NALDONI SRL")]
    [InlineData("molino naldoni")]
    [InlineData("  Molino   Naldoni s.r.l ")]
    public void Stesso_fornitore_scritto_in_modi_diversi_e_sicuro(string dicitura)
    {
        var p = AbbinamentoNomi.ProponiFornitore(dicitura, 10, Anagrafica);
        Assert.Equal(EsitoAbbinamento.Sicuro, p.Esito);
        Assert.Equal("1", p.Candidato!.Codice);
    }

    [Fact]
    public void Un_errore_di_battitura_e_probabile_non_sicuro()
    {
        var p = AbbinamentoNomi.ProponiFornitore("Padovana Macinazzione", 3, Anagrafica);
        Assert.Equal(EsitoAbbinamento.Probabile, p.Esito);
        Assert.Equal("2", p.Candidato!.Codice);
    }

    [Fact]
    public void Un_nome_ambiguo_o_sconosciuto_non_viene_abbinato()
    {
        Assert.Equal(EsitoAbbinamento.Nessuno, AbbinamentoNomi.ProponiFornitore("Molino", 1, Anagrafica).Esito);
        Assert.Equal(EsitoAbbinamento.Nessuno, AbbinamentoNomi.ProponiFornitore("Ditta Sconosciuta", 1, Anagrafica).Esito);
        Assert.Equal(EsitoAbbinamento.Nessuno, AbbinamentoNomi.ProponiFornitore("", 1, Anagrafica).Esito);
    }

    [Fact]
    public void Articoli_che_differiscono_per_ordine_e_maiuscole_si_riuniscono()
    {
        var gruppi = AbbinamentoNomi.RaggruppaArticoli(new[]
        {
            ("farina BIO integrale", 5), ("Farina integrale bio", 2), ("farina 00 speciale per piada", 9), ("  ", 4)
        });
        Assert.Equal(2, gruppi.Count);
        Assert.Equal(9, gruppi[0].Righe);
        Assert.Equal(7, gruppi[1].Righe);
        Assert.Equal(2, gruppi[1].Varianti.Count);
    }

    [Fact]
    public void Articoli_quasi_uguali_sono_segnalati_come_duplicati_probabili()
    {
        var gruppi = AbbinamentoNomi.RaggruppaArticoli(new[]
        {
            ("sciroppo orzo bio", 3), ("sciroppo orzo biologico", 1), ("olio extravergine", 5)
        });
        var d = AbbinamentoNomi.DuplicatiProbabili(gruppi, 0.5);
        Assert.Single(d);
    }

    [Fact]
    public void Le_diciture_con_accenti_e_apostrofi_si_confrontano()
    {
        Assert.Equal(NomiNormalizzati.ChiaveFornitore("Società Agricola dell'Orto S.r.l."), NomiNormalizzati.ChiaveFornitore("DELL'ORTO"));
    }

    [Fact]
    public void Un_doppione_in_anagrafica_non_impedisce_la_proposta()
    {
        var doppia = new[] { new FornitoreNoto("1", "Pegaso Food & Trade srl"), new FornitoreNoto("2", "PEGASO FOOD & TRADE S.r.l.") };
        var p = AbbinamentoNomi.ProponiFornitore("Pegaso Food & Trade", 5, doppia);
        Assert.Equal(EsitoAbbinamento.Probabile, p.Esito);
        Assert.NotNull(p.Candidato);
    }
}
