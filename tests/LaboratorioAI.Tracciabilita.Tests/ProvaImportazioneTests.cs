using LaboratorioAI.Tracciabilita.Core.Importazione;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class ProvaImportazioneTests
{
    private static readonly DateTime Oggi = new(2026, 10, 24);

    private static RigaMagazzino R(int n, string? forn = "Molino Naldoni srl", string? art = "farina", decimal? q = 10,
        string? lotto = "A1", DateTime? doc = null, DateTime? imp = null) =>
        new(n, "Bolla", doc ?? new DateTime(2025, 5, 1), "1", forn, art, q, "kg", lotto, imp ?? new DateTime(2025, 5, 2), "Impegno", null);

    private static DatiAccess D(params RigaMagazzino[] righe) =>
        new(righe, new[] { new FornitoreAccess("1", "Molino Naldoni S.r.l.", null, null, null, null, null, null) },
            Array.Empty<MateriaFornitoreAccess>(), Array.Empty<string>());

    [Fact]
    public void Una_riga_pulita_non_genera_segnalazioni()
    {
        var p = Prova.Prepara(D(R(1)), Oggi);
        Assert.Empty(p.Segnalazioni);
        Assert.Equal(EsitoAbbinamento.Sicuro, p.Fornitori.Single().Esito);
    }

    [Fact]
    public void Le_righe_sospette_sono_segnalate_con_il_numero_di_riga()
    {
        var p = Prova.Prepara(D(
            R(1, q: 0),
            R(2, imp: new DateTime(2036, 1, 1)),
            R(3, doc: new DateTime(2001, 1, 1)),
            R(4, lotto: " "),
            R(5, doc: new DateTime(2025, 6, 1), imp: new DateTime(2025, 5, 1))), Oggi);

        Assert.Contains(p.Segnalazioni, s => s.Problema.StartsWith("Quantità vuota") && s.Esempi.Contains(1));
        Assert.Contains(p.Segnalazioni, s => s.Problema.StartsWith("Data di impegno implausibile") && s.Esempi.Contains(2));
        Assert.Contains(p.Segnalazioni, s => s.Problema.StartsWith("Data del documento implausibile") && s.Esempi.Contains(3));
        Assert.Contains(p.Segnalazioni, s => s.Problema == "Codice lotto vuoto" && s.Esempi.Contains(4));
        Assert.Contains(p.Segnalazioni, s => s.Problema.StartsWith("Impegno precedente") && s.Esempi.Contains(5));
    }

    [Fact]
    public void Lo_stesso_codice_lotto_di_fornitori_diversi_e_contato()
    {
        var p = Prova.Prepara(D(R(1, forn: "Molino Naldoni srl", lotto: "L1"), R(2, forn: "Altra Ditta", lotto: "l1"), R(3, lotto: "L2")), Oggi);
        Assert.Equal(1, p.CodiciLottoCondivisi);
    }

    [Fact]
    public void I_file_di_abbinamento_si_scrivono_e_si_rileggono()
    {
        var proposte = new[]
        {
            new PropostaFornitore("Ditta; \"strana\"", 3, null, EsitoAbbinamento.Nessuno, 0),
            new PropostaFornitore("Molino Naldoni srl", 9, new FornitoreNoto("1", "Molino Naldoni S.r.l."), EsitoAbbinamento.Sicuro, 1),
        };
        var csv = FileAbbinamenti.ScriviFornitori(proposte);
        var scelte = FileAbbinamenti.LeggiFornitori(csv);
        Assert.Equal("Ditta; \"strana\"", scelte[0].Dicitura);
        Assert.Equal("", scelte[0].Scelta);
        Assert.Equal("1", scelte[1].Scelta);   // gli abbinamenti sicuri arrivano già compilati
    }

    [Fact]
    public void Gli_articoli_dello_stesso_gruppo_ricevono_lo_stesso_nome_proposto()
    {
        var g = AbbinamentoNomi.RaggruppaArticoli(new[] { ("farina BIO integrale", 5), ("Farina integrale bio", 2) });
        var scelte = FileAbbinamenti.LeggiArticoli(FileAbbinamenti.ScriviArticoli(g));
        Assert.Equal(2, scelte.Count);
        Assert.Equal(scelte[0].NomeArticolo, scelte[1].NomeArticolo);
        Assert.All(scelte, s => Assert.False(s.Salta));
    }

    [Fact]
    public void Un_testo_che_sembra_una_formula_non_diventa_formula()
    {
        var csv = FileAbbinamenti.ScriviFornitori(new[] { new PropostaFornitore("=SOMMA(A1)", 1, null, EsitoAbbinamento.Nessuno, 0) });
        Assert.Contains("'=SOMMA(A1)", csv);
        Assert.Equal("=SOMMA(A1)", FileAbbinamenti.LeggiFornitori(csv)[0].Dicitura);
    }
}
