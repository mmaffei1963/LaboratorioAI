using LaboratorioAI.Tracciabilita.Core.Importazione;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class PianificaImportazioneTests
{
    private static readonly DateTime Oggi = new(2026, 10, 24);

    private static RigaMagazzino R(int n, string forn = "Molino Naldoni srl", string art = "farina", string? lotto = "A1",
        decimal? q = 10, DateTime? doc = null, DateTime? imp = null, bool senzaImpegno = false) =>
        new(n, "Bolla", doc ?? new DateTime(2025, 5, 1), "7", forn, art, q, "kg", lotto, senzaImpegno ? null : imp ?? new DateTime(2025, 5, 3), "Impegno", null);

    private static DatiAccess D(params RigaMagazzino[] righe) =>
        new(righe, new[] { new FornitoreAccess("F1", "Molino Naldoni S.r.l.", null, null, null, null, null, null) },
            Array.Empty<MateriaFornitoreAccess>(), Array.Empty<string>());

    private static SceltaFornitore SF(string d, string s) => new(d, s);
    private static SceltaArticolo SA(string d, string nome, bool salta = false) => new(d, nome, "kg", salta);

    [Fact]
    public void Un_lotto_con_impegno_entra_chiuso_alla_data_di_impegno()
    {
        var p = PianificaImportazione.Costruisci(D(R(1)), new[] { SF("Molino Naldoni srl", "F1") }, new[] { SA("farina", "Farina 00") }, Oggi);
        var r = Assert.Single(p.Righe);
        Assert.Equal(new DateOnly(2025, 5, 3), r.DataChiusura);
        Assert.False(r.ChiusuraDaDataNonAttendibile);
        Assert.Equal("F1", r.ChiaveFornitore);
        Assert.Equal("Farina 00", r.NomeArticolo);
    }

    [Fact]
    public void Un_lotto_senza_impegno_resta_in_magazzino()
    {
        var p = PianificaImportazione.Costruisci(D(R(1, senzaImpegno: true)), new[] { SF("Molino Naldoni srl", "F1") }, new[] { SA("farina", "Farina") }, Oggi);
        Assert.Null(p.Righe.Single().DataChiusura);
    }

    [Fact]
    public void Un_impegno_assurdo_chiude_il_lotto_alla_data_di_arrivo_e_lo_segnala()
    {
        var p = PianificaImportazione.Costruisci(D(R(1, imp: new DateTime(2036, 1, 1))), new[] { SF("Molino Naldoni srl", "F1") }, new[] { SA("farina", "Farina") }, Oggi);
        var r = p.Righe.Single();
        Assert.Equal(r.DataArrivo, r.DataChiusura);
        Assert.True(r.ChiusuraDaDataNonAttendibile);
    }

    [Fact]
    public void Senza_data_documento_si_usa_l_impegno_e_senza_nessuna_data_si_scarta()
    {
        var senzaDoc = new RigaMagazzino(1, null, null, null, "Molino Naldoni srl", "farina", 5, "kg", "A", new DateTime(2024, 2, 2), null, null);
        var senzaDate = new RigaMagazzino(2, null, null, null, "Molino Naldoni srl", "farina", 5, "kg", "B", null, null, null);
        var p = PianificaImportazione.Costruisci(D(senzaDoc, senzaDate), new[] { SF("Molino Naldoni srl", "F1") }, new[] { SA("farina", "Farina") }, Oggi);
        Assert.Equal(new DateOnly(2024, 2, 2), p.Righe.Single().DataArrivo);
        Assert.Equal(2, p.Scartate.Single().Numero);
    }

    [Fact]
    public void Se_manca_una_scelta_l_importazione_non_e_pronta()
    {
        var p = PianificaImportazione.Costruisci(D(R(1)), new[] { SF("Molino Naldoni srl", "") }, new[] { SA("farina", "Farina") }, Oggi);
        Assert.Empty(p.Righe);
        Assert.Contains(p.NonRisolte, x => x.Contains("Molino Naldoni"));
    }

    [Fact]
    public void Un_codice_fornitore_inesistente_non_e_accettato()
    {
        var p = PianificaImportazione.Costruisci(D(R(1)), new[] { SF("Molino Naldoni srl", "XX") }, new[] { SA("farina", "Farina") }, Oggi);
        Assert.NotEmpty(p.NonRisolte);
    }

    [Fact]
    public void Nuovo_crea_un_fornitore_e_salta_esclude_le_righe()
    {
        var dati = D(R(1, forn: "Ditta Nuova"), R(2, forn: "Ditta Esclusa"));
        var p = PianificaImportazione.Costruisci(dati,
            new[] { SF("Ditta Nuova", "NUOVO"), SF("Ditta Esclusa", "SALTA") }, new[] { SA("farina", "Farina") }, Oggi);
        Assert.Single(p.Righe);
        Assert.Single(p.FornitoriNuovi);
        Assert.StartsWith("NUOVO:", p.Righe[0].ChiaveFornitore);
        Assert.Equal(2, p.Scartate.Single().Numero);
        Assert.Empty(p.NonRisolte);
    }

    [Fact]
    public void Articoli_con_lo_stesso_nome_scelto_condividono_il_lotto_distinto()
    {
        var dati = D(R(1, art: "farina BIO", lotto: "L1"), R(2, art: "Farina bio integrale", lotto: "L1"));
        var p = PianificaImportazione.Costruisci(dati, new[] { SF("Molino Naldoni srl", "F1") },
            new[] { SA("farina BIO", "Farina bio"), SA("Farina bio integrale", "Farina bio") }, Oggi);
        Assert.Equal(1, p.LottiDistinti);
        Assert.Equal(2, p.Righe.Count);
    }

    [Fact]
    public void Quantita_vuota_entra_con_zero_e_negativa_si_scarta()
    {
        var p = PianificaImportazione.Costruisci(D(R(1, q: null), R(2, q: -5, lotto: "B")),
            new[] { SF("Molino Naldoni srl", "F1") }, new[] { SA("farina", "Farina") }, Oggi);
        Assert.Equal(0, p.Righe.Single().Quantita);
        Assert.Equal(2, p.Scartate.Single().Numero);
    }

    [Fact]
    public void Excel_che_toglie_gli_zeri_iniziali_dal_codice_non_blocca_l_importazione()
    {
        var dati = new DatiAccess(new[] { R(1, forn: "Granarolo spa") },
            new[] { new FornitoreAccess("04119190371", "Granarolo S.p.A.", null, null, null, null, null, null) },
            Array.Empty<MateriaFornitoreAccess>(), Array.Empty<string>());
        var p = PianificaImportazione.Costruisci(dati, new[] { SF("Granarolo spa", "4119190371") }, new[] { SA("farina", "Farina") }, Oggi);
        Assert.Empty(p.NonRisolte);
        Assert.Equal("04119190371", p.Righe.Single().ChiaveFornitore);   // si usa sempre il codice originale
    }

    [Fact]
    public void Con_due_codici_che_differiscono_solo_per_gli_zeri_non_si_indovina()
    {
        var dati = new DatiAccess(new[] { R(1) },
            new[] { new FornitoreAccess("0123", "A", null, null, null, null, null, null), new FornitoreAccess("123", "B", null, null, null, null, null, null) },
            Array.Empty<MateriaFornitoreAccess>(), Array.Empty<string>());
        Assert.Equal("123", PianificaImportazione.TrovaCodice(dati.Fornitori, "123"));      // esatto
        Assert.Null(PianificaImportazione.TrovaCodice(dati.Fornitori, "00123"));            // ambiguo
    }

    [Fact]
    public void La_chiave_dell_accettazione_non_cambia_se_il_database_aggiunge_i_decimali()
    {
        var d = new DateOnly(2025, 5, 1);
        Assert.Equal(ChiaveAccettazione.Crea(1, 2, d, " 7 ", 15180m, 3), ChiaveAccettazione.Crea(1, 2, d, "7", 15180.00m, 3));
        Assert.NotEqual(ChiaveAccettazione.Crea(1, 2, d, "7", 15180m, 3), ChiaveAccettazione.Crea(1, 2, d, "7", 15180.5m, 3));
    }
}
