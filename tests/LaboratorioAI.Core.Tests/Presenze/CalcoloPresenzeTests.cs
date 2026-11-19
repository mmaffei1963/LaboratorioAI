#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LaboratorioAI.Core.Presenze;
using Xunit;

namespace LaboratorioAI.Core.Tests.Presenze;

/// <summary>
/// Test del motore di calcolo delle presenze. I primi sono portati da test_presenze.py dell'app di Vaccarini;
/// gli altri coprono festività, ore, causali, contenuto delle celle e totali.
/// </summary>
public class CalcoloPresenzeTests
{
    private static readonly double[] OreStandard = { 8, 8, 8, 8, 8, 0, 0 };
    private static readonly DateOnly Lunedi = new(2026, 1, 12);   // lunedì
    private static readonly DateOnly Oggi = new(2026, 6, 1);

    private static LavoratorePresenze Lavoratore(double[]? ore = null, DateOnly? attivoDal = null) =>
        new("ROSSI", "#FFC000", ore ?? OreStandard, attivoDal);

    private static List<TimbraturaGiorno> T(params (string Tipo, string Ora)[] timbrature) =>
        timbrature.Select(x => new TimbraturaGiorno(x.Ora, x.Tipo)).ToList();

    private static readonly List<AssenzaGiorno> NessunaAssenza = new();

    private static CalcoloGiorno Calcola(
        LavoratorePresenze lavoratore, DateOnly giorno, List<TimbraturaGiorno> timbrature,
        List<AssenzaGiorno>? assenze = null, VoceCalendario? calendario = null,
        DateOnly? oggi = null, DateOnly? inizio = null) =>
        MotoreCalcoloPresenze.CalcolaGiorno(
            lavoratore, giorno, timbrature, assenze ?? NessunaAssenza, calendario, oggi ?? Oggi, inizio);

    // ---------------------------------------------------------------- orari

    [Theory]
    [InlineData("08:00", "08:00")]
    [InlineData("08:07", "08:00")]
    [InlineData("08:08", "08:15")]
    [InlineData("08:22", "08:15")]
    [InlineData("08:23", "08:30")]
    [InlineData("12:52", "12:45")]
    [InlineData("12:53", "13:00")]
    [InlineData("23:53", "24:00")]
    public void Arrotonda_al_quarto_d_ora_piu_vicino(string ora, string atteso)
    {
        Assert.Equal(atteso, MotoreCalcoloPresenze.MinutiInHhmm(MotoreCalcoloPresenze.Arrotonda(ora)));
    }

    [Theory]
    [InlineData(6.5, "6:30")]
    [InlineData(0.0, "0:00")]
    [InlineData(8.0, "8:00")]
    [InlineData(0.25, "0:15")]
    [InlineData(-0.25, "-0:15")]
    [InlineData(10.75, "10:45")]
    public void OreFmt_mostra_ore_e_minuti(double ore, string atteso)
    {
        Assert.Equal(atteso, MotoreCalcoloPresenze.OreFmt(ore));
    }

    [Fact]
    public void OreFmt_di_null_e_vuoto()
    {
        Assert.Equal("", MotoreCalcoloPresenze.OreFmt(null));
    }

    // ---------------------------------------------------------------- festività

    [Fact]
    public void Pasqua_negli_anni_noti()
    {
        Assert.Equal(new DateOnly(2025, 4, 20), MotoreCalcoloPresenze.Pasqua(2025));
        Assert.Equal(new DateOnly(2026, 4, 5), MotoreCalcoloPresenze.Pasqua(2026));
        Assert.Equal(new DateOnly(2027, 3, 28), MotoreCalcoloPresenze.Pasqua(2027));
    }

    [Fact]
    public void San_Francesco_e_festa_nazionale_dal_2026()
    {
        var f2025 = MotoreCalcoloPresenze.FestivitaNazionali(2025);
        var f2026 = MotoreCalcoloPresenze.FestivitaNazionali(2026);
        var f2027 = MotoreCalcoloPresenze.FestivitaNazionali(2027);
        Assert.Equal(12, f2025.Count);
        Assert.Equal(13, f2026.Count);
        Assert.False(f2025.Any(x => x.Giorno == new DateOnly(2025, 10, 4)));
        Assert.True(f2026.Any(x => x.Giorno == new DateOnly(2026, 10, 4) && x.Descrizione == "San Francesco d'Assisi"));
        Assert.True(f2027.Any(x => x.Giorno == new DateOnly(2027, 10, 4)));
    }

    [Fact]
    public void Le_festivita_sono_in_ordine_di_data_e_comprendono_Pasquetta()
    {
        var f = MotoreCalcoloPresenze.FestivitaNazionali(2027);
        for (var i = 1; i < f.Count; i++)
            Assert.True(f[i - 1].Giorno < f[i].Giorno, "ordine di data");
        Assert.True(f.Any(x => x.Giorno == new DateOnly(2027, 3, 29) && x.Descrizione == "Lunedì dell'Angelo"));
    }

    [Fact]
    public void I_patroni_delle_sedi_nel_2027_cadono_di_venerdi()
    {
        // Cattolica: San Pio V (30 aprile); Pesaro: San Terenzio (24 settembre)
        Assert.Equal(DayOfWeek.Friday, new DateOnly(2027, 4, 30).DayOfWeek);
        Assert.Equal(DayOfWeek.Friday, new DateOnly(2027, 9, 24).DayOfWeek);
    }

    // ---------------------------------------------------------------- calcolo giornaliero

    [Fact]
    public void Giornata_normale_con_pausa()
    {
        var g = Calcola(Lavoratore(), Lunedi,
            T(("E", "08:03"), ("U", "12:31"), ("E", "13:58"), ("U", "18:06")));
        Assert.Equal(8.5, g.Lavorate, 6);   // 8:00-12:30 + 14:00-18:00
        Assert.Equal(0.5, g.Straordinario, 6);
        Assert.Equal(0.0, g.NonLavorate, 6);
    }

    [Fact]
    public void Ore_mancanti_e_permesso()
    {
        var timbrature = T(("E", "08:00"), ("U", "12:00"), ("E", "14:00"), ("U", "16:00"));
        var senza = Calcola(Lavoratore(), Lunedi, timbrature);
        Assert.Equal(2.0, senza.NonLavorate, 6);

        var con = Calcola(Lavoratore(), Lunedi, timbrature, new List<AssenzaGiorno> { new("PER", 2) });
        Assert.Equal(0.0, con.NonLavorate, 6);
        Assert.Equal(0.0, con.Straordinario, 6);
    }

    [Fact]
    public void Chiusura_aziendale_e_festivo()
    {
        var ponte = new VoceCalendario("CH", "Ponte", null);
        var chiusura = Calcola(Lavoratore(), Lunedi, new List<TimbraturaGiorno>(), calendario: ponte);
        Assert.Equal(8.0, chiusura.Chiusura, 6);
        Assert.Equal(0.0, chiusura.NonLavorate, 6);

        var epifania = new VoceCalendario("FE", "Epifania", null);
        var festivo = Calcola(Lavoratore(), Lunedi, T(("E", "09:00"), ("U", "11:00")), calendario: epifania);
        Assert.Equal(0.0, festivo.Contratto, 6);
        Assert.Equal(2.0, festivo.Straordinario, 6);
    }

    [Fact]
    public void Chiusura_con_ore_limitate_non_supera_il_contratto()
    {
        var mezza = new VoceCalendario("CH", "Mezza giornata", 4);
        var g = Calcola(Lavoratore(), Lunedi, new List<TimbraturaGiorno>(), calendario: mezza);
        Assert.Equal(4.0, g.Chiusura, 6);
        Assert.Equal(4.0, g.NonLavorate, 6);

        var lunga = new VoceCalendario("CH", "Ponte", 12);
        var g2 = Calcola(Lavoratore(), Lunedi, new List<TimbraturaGiorno>(), calendario: lunga);
        Assert.Equal(8.0, g2.Chiusura, 6);
    }

    [Fact]
    public void Part_time_e_weekend()
    {
        var partTime = Lavoratore(new double[] { 4, 8, 8, 8, 8, 0, 0 });
        Assert.Equal(4.0, Calcola(partTime, Lunedi, new List<TimbraturaGiorno>()).NonLavorate, 6);
        Assert.Equal(0.0, Calcola(partTime, Lunedi, new List<TimbraturaGiorno>()).Straordinario, 6);

        var sabato = new DateOnly(2026, 1, 17);
        var g = Calcola(Lavoratore(), sabato, T(("E", "09:00"), ("U", "12:00")));
        Assert.Equal(3.0, g.Straordinario, 6);
        Assert.True(g.Weekend);
    }

    [Fact]
    public void Anomalie_e_giorno_in_corso()
    {
        var g = Calcola(Lavoratore(), Lunedi, T(("E", "08:00")));
        Assert.Contains("Manca la timbratura di uscita", g.Anomalie);
        Assert.Equal(8.0, g.NonLavorate, 6);

        var oggi = Calcola(Lavoratore(), Oggi, T(("E", "08:00")));
        Assert.True(oggi.InCorso);
        Assert.Equal(0.0, oggi.NonLavorate, 6);

        var futuro = Calcola(Lavoratore(), new DateOnly(2026, 7, 1), new List<TimbraturaGiorno>());
        Assert.Equal(0.0, futuro.NonLavorate, 6);
        Assert.True(futuro.Futuro);
    }

    [Fact]
    public void Uscita_senza_entrata_ed_entrate_doppie_sono_anomalie()
    {
        var g = Calcola(Lavoratore(), Lunedi,
            T(("U", "08:00"), ("E", "09:00"), ("E", "10:00"), ("U", "12:00")));
        Assert.Equal(2, g.Anomalie.Count);
        Assert.Equal("Uscita 08:00 senza entrata", g.Anomalie[0]);
        Assert.Equal("Entrata 10:00 senza uscita precedente", g.Anomalie[1]);
        Assert.Equal(2.0, g.Lavorate, 6);   // vale l'ultima entrata (10:00-12:00)
    }

    [Fact]
    public void Uscita_prima_dell_entrata_non_conta_ore()
    {
        var g = Calcola(Lavoratore(), Lunedi, T(("E", "12:00"), ("U", "10:00")));
        // le timbrature si ordinano per orario: l'uscita delle 10:00 resta senza entrata
        Assert.Equal(0.0, g.Lavorate, 6);
        Assert.True(g.Anomalie.Count > 0);
    }

    [Fact]
    public void Assunzione_in_corso_d_anno()
    {
        var l = Lavoratore(attivoDal: new DateOnly(2026, 2, 1));
        Assert.Equal(0.0, Calcola(l, Lunedi, new List<TimbraturaGiorno>()).Contratto, 6);
        Assert.Equal(8.0, Calcola(l, new DateOnly(2026, 2, 2), new List<TimbraturaGiorno>()).Contratto, 6);
    }

    [Fact]
    public void Prima_dell_avvio_dei_conteggi_non_ci_sono_ore_mancanti()
    {
        var g = Calcola(Lavoratore(), new DateOnly(2026, 12, 15), new List<TimbraturaGiorno>(),
            inizio: new DateOnly(2027, 1, 1), oggi: new DateOnly(2027, 1, 20));
        Assert.True(g.PrimaInizio);
        Assert.Equal(0.0, g.Contratto, 6);
        Assert.Equal(0.0, g.NonLavorate, 6);

        var dopo = Calcola(Lavoratore(), new DateOnly(2027, 1, 12), new List<TimbraturaGiorno>(),
            inizio: new DateOnly(2027, 1, 1), oggi: new DateOnly(2027, 1, 20));
        Assert.False(dopo.PrimaInizio);
        Assert.Equal(8.0, dopo.NonLavorate, 6);
    }

    [Fact]
    public void Il_calcolo_non_dipende_dall_ordine_di_inserimento_delle_timbrature()
    {
        var a = Calcola(Lavoratore(), Lunedi, T(("U", "12:00"), ("E", "08:00")));
        var b = Calcola(Lavoratore(), Lunedi, T(("E", "08:00"), ("U", "12:00")));
        Assert.Equal(b.Lavorate, a.Lavorate, 6);
        Assert.Equal(4.0, a.Lavorate, 6);
    }

    // ---------------------------------------------------------------- giorni

    [Fact]
    public void Giorni_del_mese()
    {
        Assert.Equal(28, MotoreCalcoloPresenze.GiorniMese(2027, 2).Count());
        Assert.Equal(31, MotoreCalcoloPresenze.GiorniMese(2027, 1).Count());
        Assert.Equal(new DateOnly(2027, 1, 1), MotoreCalcoloPresenze.GiorniMese(2027, 1).First());
    }

    [Fact]
    public void Giorni_lavorativi_saltano_weekend_festivi_e_chiusure()
    {
        var calendario = new Dictionary<DateOnly, VoceCalendario>
        {
            [new DateOnly(2027, 1, 6)] = new("FE", "Epifania"),
            [new DateOnly(2027, 1, 8)] = new("CH", "Ponte"),
        };
        var giorni = MotoreCalcoloPresenze
            .GiorniLavorativi(Lavoratore(), new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 11), calendario)
            .ToList();
        // lun 4, mar 5, (mer 6 festivo), gio 7, (ven 8 chiusura), (sab 9, dom 10), lun 11
        Assert.Equal(4, giorni.Count);
        Assert.Equal(new DateOnly(2027, 1, 7), giorni[2]);
        Assert.Equal(new DateOnly(2027, 1, 11), giorni[3]);
    }

    [Fact]
    public void Le_ore_da_contratto_seguono_il_giorno_della_settimana()
    {
        var l = Lavoratore(new double[] { 1, 2, 3, 4, 5, 6, 7 });
        Assert.Equal(1.0, MotoreCalcoloPresenze.OreContratto(l, new DateOnly(2027, 1, 4)), 6);   // lunedì
        Assert.Equal(6.0, MotoreCalcoloPresenze.OreContratto(l, new DateOnly(2027, 1, 9)), 6);   // sabato
        Assert.Equal(7.0, MotoreCalcoloPresenze.OreContratto(l, new DateOnly(2027, 1, 10)), 6);  // domenica
    }

    [Fact]
    public void Servono_sette_valori_di_ore_da_contratto()
    {
        Assert.Throws<ArgumentException>(() => new LavoratorePresenze("ROSSI", "#FFC000", new double[] { 8, 8 }));
    }

    // ---------------------------------------------------------------- causali e parsing

    [Fact]
    public void Causali_con_descrizione_e_sigla()
    {
        Assert.Equal(8, CausaliPresenze.Tutte.Count);
        Assert.Equal("Ferie", CausaliPresenze.Descrizione("FER"));
        Assert.Equal("F", CausaliPresenze.Sigla("FER"));
        Assert.Equal("Permesso L. 104", CausaliPresenze.Descrizione("104"));
        Assert.True(CausaliPresenze.Esiste("MAL"));
        Assert.False(CausaliPresenze.Esiste("XXX"));
        Assert.False(CausaliPresenze.Esiste(null));
        Assert.Throws<ArgumentException>(() => CausaliPresenze.Descrizione("XXX"));
    }

    [Theory]
    [InlineData("8", 8.0)]
    [InlineData("7,5", 7.5)]
    [InlineData("7.5", 7.5)]
    [InlineData("7:30", 7.5)]
    [InlineData(" 1:30 ", 1.5)]
    [InlineData("0:15", 0.25)]
    public void ParseOre_accetta_i_formati_digitati(string testo, double atteso)
    {
        var ore = ParsingPresenze.ParseOre(testo);
        Assert.NotNull(ore);
        Assert.Equal(atteso, ore!.Value, 6);
    }

    [Fact]
    public void ParseOre_vuoto_e_null_e_non_valido_solleva_errore()
    {
        Assert.Null(ParsingPresenze.ParseOre(""));
        Assert.Null(ParsingPresenze.ParseOre("   "));
        Assert.Null(ParsingPresenze.ParseOre(null));
        Assert.Throws<FormatException>(() => ParsingPresenze.ParseOre("abc"));
    }

    [Theory]
    [InlineData("08:05", true, "08:05")]
    [InlineData("23:59", true, "23:59")]
    [InlineData("00:00", true, "00:00")]
    [InlineData("8:05", false, "")]
    [InlineData("24:00", false, "")]
    [InlineData("12:60", false, "")]
    [InlineData("abc", false, "")]
    [InlineData("", false, "")]
    public void TryValidaOra_richiede_il_formato_HH_mm(string testo, bool valido, string atteso)
    {
        Assert.Equal(valido, ParsingPresenze.TryValidaOra(testo, out var ora));
        Assert.Equal(atteso, ora);
    }

    // ---------------------------------------------------------------- contenuto delle celle

    [Fact]
    public void Cella_straordinario_carattere_rosso_senza_colore()
    {
        var l = Lavoratore();
        var g = Calcola(l, Lunedi, new List<TimbraturaGiorno>
        {
            new("08:00", "E", "Cliente X"), new("18:00", "U", null),
        });
        var c = ContenutoCellaPresenze.Costruisci(l, g);
        Assert.NotNull(c);
        Assert.Equal(2.0, c!.Ore!.Value, 6);
        Assert.True(c.CarattereRosso);
        Assert.True(c.Grassetto);
        Assert.Null(c.Sfondo);
        Assert.Equal("Straordinario 2:00\nFuori sede: Cliente X", c.Nota);
    }

    [Fact]
    public void Cella_ore_non_lavorate_grigio_corsivo()
    {
        var l = Lavoratore();
        var g = Calcola(l, Lunedi, T(("E", "08:00"), ("U", "12:00")));
        var c = ContenutoCellaPresenze.Costruisci(l, g);
        Assert.Equal(4.0, c!.Ore!.Value, 6);
        Assert.Equal(ContenutoCellaPresenze.Grigio, c.Sfondo);
        Assert.True(c.Corsivo);
        Assert.False(c.Grassetto);
        Assert.Equal("", c.Nota);
    }

    [Fact]
    public void Cella_chiusura_aziendale_rossa_con_descrizione()
    {
        var l = Lavoratore();
        var g = Calcola(l, Lunedi, new List<TimbraturaGiorno>(), calendario: new VoceCalendario("CH", "Ponte", null));
        var c = ContenutoCellaPresenze.Costruisci(l, g);
        Assert.Equal(8.0, c!.Ore!.Value, 6);
        Assert.Equal(ContenutoCellaPresenze.Rosso, c.Sfondo);
        Assert.True(c.Grassetto);
        Assert.Equal("Ponte", c.Nota);
    }

    [Fact]
    public void Cella_chiusura_senza_descrizione_usa_il_testo_standard()
    {
        var l = Lavoratore();
        var g = Calcola(l, Lunedi, new List<TimbraturaGiorno>(), calendario: new VoceCalendario("CH", "", null));
        Assert.Equal("Chiusura aziendale", ContenutoCellaPresenze.Costruisci(l, g)!.Nota);
    }

    [Fact]
    public void Cella_assenza_nel_colore_del_lavoratore_con_motivo_nel_commento()
    {
        var l = Lavoratore();
        var g = Calcola(l, Lunedi, new List<TimbraturaGiorno>(),
            new List<AssenzaGiorno> { new("FER", 8), new("PER", 0) });
        var c = ContenutoCellaPresenze.Costruisci(l, g);
        Assert.Equal(8.0, c!.Ore!.Value, 6);
        Assert.Equal("FFFFC000", c.Sfondo);
        Assert.True(c.Grassetto);
        Assert.Equal("Ferie 8:00; Permesso 0:00", c.Nota);
    }

    [Fact]
    public void Cella_assenza_con_note_e_malattia()
    {
        var l = Lavoratore();
        var g = Calcola(l, Lunedi, new List<TimbraturaGiorno>(),
            new List<AssenzaGiorno> { new("MAL", 8, "certificato") });
        Assert.Equal("Malattia 8:00 (certificato)", ContenutoCellaPresenze.Costruisci(l, g)!.Nota);
    }

    [Fact]
    public void Cella_festivo_e_fine_settimana()
    {
        var l = Lavoratore();
        var fe = ContenutoCellaPresenze.Costruisci(l,
            Calcola(l, Lunedi, new List<TimbraturaGiorno>(), calendario: new VoceCalendario("FE", "Epifania")));
        Assert.Equal("FE", fe!.Testo);
        Assert.Equal(ContenutoCellaPresenze.Giallo, fe.Sfondo);
        Assert.Null(fe.Ore);

        var fs = ContenutoCellaPresenze.Costruisci(l,
            Calcola(l, new DateOnly(2026, 1, 17), new List<TimbraturaGiorno>()));
        Assert.Equal("FS", fs!.Testo);
        Assert.Equal(ContenutoCellaPresenze.Giallo, fs.Sfondo);
    }

    [Fact]
    public void Cella_giorno_senza_dati_non_ha_valore()
    {
        var l = Lavoratore();
        // giorno feriale futuro: nessuna ora mancante
        var g = Calcola(l, new DateOnly(2026, 7, 1), new List<TimbraturaGiorno>());
        var c = ContenutoCellaPresenze.Costruisci(l, g);
        Assert.NotNull(c);
        Assert.False(c!.HaValore);
    }

    [Fact]
    public void Cella_prima_dell_avvio_solo_fine_settimana_e_festivita()
    {
        var l = Lavoratore();
        var oggi = new DateOnly(2027, 1, 20);
        var inizio = new DateOnly(2027, 1, 1);

        var feriale = Calcola(l, new DateOnly(2026, 12, 15), new List<TimbraturaGiorno>(), oggi: oggi, inizio: inizio);
        Assert.Null(ContenutoCellaPresenze.Costruisci(l, feriale));

        var sabato = Calcola(l, new DateOnly(2026, 12, 12), new List<TimbraturaGiorno>(), oggi: oggi, inizio: inizio);
        Assert.Equal("FS", ContenutoCellaPresenze.Costruisci(l, sabato)!.Testo);

        var natale = Calcola(l, new DateOnly(2026, 12, 25), new List<TimbraturaGiorno>(),
            calendario: new VoceCalendario("FE", "Natale"), oggi: oggi, inizio: inizio);
        Assert.Equal("FE", ContenutoCellaPresenze.Costruisci(l, natale)!.Testo);
    }

    [Fact]
    public void Cella_con_anomalia_riporta_da_controllare()
    {
        var l = Lavoratore();
        var g = Calcola(l, Lunedi, T(("E", "08:00")));
        var c = ContenutoCellaPresenze.Costruisci(l, g);
        Assert.Equal(8.0, c!.Ore!.Value, 6);
        Assert.True(c.Nota.Contains("Da controllare: Manca la timbratura di uscita"));
    }

    [Fact]
    public void Colore_argb_dal_formato_esadecimale()
    {
        Assert.Equal("FFFFC000", ContenutoCellaPresenze.ColoreArgb("#FFC000"));
        Assert.Equal("FF92D050", ContenutoCellaPresenze.ColoreArgb("#92d050"));
    }

    // ---------------------------------------------------------------- periodo e totali

    [Fact]
    public void Totali_del_mese_ignorano_le_ore_da_contratto_dei_giorni_futuri()
    {
        var l = Lavoratore();
        var dal = new DateOnly(2026, 6, 1);
        var al = new DateOnly(2026, 6, 5);   // lun-ven
        var timbrature = new[]
        {
            (Giorno: new DateOnly(2026, 6, 1), T: new TimbraturaGiorno("08:00", "E")),
            (Giorno: new DateOnly(2026, 6, 1), T: new TimbraturaGiorno("17:00", "U")),
            (Giorno: new DateOnly(2026, 6, 2), T: new TimbraturaGiorno("08:00", "E")),
            (Giorno: new DateOnly(2026, 6, 2), T: new TimbraturaGiorno("12:00", "U")),
        }.ToLookup(x => x.Giorno, x => x.T);
        var assenze = new[]
        {
            (Giorno: new DateOnly(2026, 6, 3), A: new AssenzaGiorno("FER", 8)),
        }.ToLookup(x => x.Giorno, x => x.A);

        var giorni = MotoreCalcoloPresenze.CalcolaPeriodo(
            l, dal, al, timbrature, assenze, new Dictionary<DateOnly, VoceCalendario>(),
            oggi: new DateOnly(2026, 6, 3), inizio: null);
        var tot = MotoreCalcoloPresenze.CalcolaTotali(giorni);

        Assert.Equal(5, giorni.Count);
        // contratto fino a oggi (3 giugno): lun, mar, mer = 24 ore; giovedì e venerdì sono futuri
        Assert.Equal(24.0, tot.Contratto, 6);
        Assert.Equal(13.0, tot.Lavorate, 6);       // 9 + 4
        Assert.Equal(1.0, tot.Straordinario, 6);   // lunedì 9 ore
        Assert.Equal(4.0, tot.NonLavorate, 6);     // martedì 4 ore mancanti; mercoledì è oggi e ha l'assenza
        Assert.Equal(8.0, tot.Causali["FER"], 6);
        Assert.Equal(0, tot.GiorniConAnomalie);
    }
}
