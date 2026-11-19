#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LaboratorioAI.Core.Presenze;
using Xunit;

namespace LaboratorioAI.Core.Tests.Presenze;

public class RegolePresenzeTests
{
    private static readonly IReadOnlyDictionary<DateOnly, VoceCalendario> SenzaCalendario =
        new Dictionary<DateOnly, VoceCalendario>();

    private static LavoratorePresenze Lavoratore() =>
        new("ROSSI", "#FFC000", new double[] { 8, 8, 8, 8, 8, 0, 0 });

    // ---------------------------------------------------------------- fuso orario

    [Fact]
    public void Orario_invernale_e_estivo_di_Roma()
    {
        // 15 gennaio: UTC+1. 15 luglio: UTC+2.
        var inverno = FusoPresenze.ToUtc(new DateOnly(2027, 1, 15), "08:30");
        var estate = FusoPresenze.ToUtc(new DateOnly(2027, 7, 15), "08:30");
        Assert.Equal(new DateTime(2027, 1, 15, 7, 30, 0, DateTimeKind.Utc), inverno!.Value);
        Assert.Equal(new DateTime(2027, 7, 15, 6, 30, 0, DateTimeKind.Utc), estate!.Value);
        Assert.Equal("08:30", FusoPresenze.OraLocale(inverno.Value));
        Assert.Equal("08:30", FusoPresenze.OraLocale(estate.Value));
    }

    [Fact]
    public void Il_giorno_cambia_a_mezzanotte_locale_non_UTC()
    {
        // 22:30 UTC del 30 giugno = 00:30 del 1 luglio a Roma (estate).
        var utc = new DateTime(2027, 6, 30, 22, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2027, 7, 1), FusoPresenze.GiornoLocale(utc));
        Assert.Equal("00:30", FusoPresenze.OraLocale(utc));
        // 22:30 UTC del 15 gennaio = 23:30 dello stesso giorno (inverno).
        var inverno = new DateTime(2027, 1, 15, 22, 30, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2027, 1, 15), FusoPresenze.GiornoLocale(inverno));
    }

    [Fact]
    public void Salto_dell_ora_legale_ora_inesistente()
    {
        // 28 marzo 2027: alle 02:00 le lancette vanno alle 03:00, quindi le 02:30 non esistono.
        Assert.Null(FusoPresenze.ToUtc(new DateOnly(2027, 3, 28), "02:30"));
        Assert.NotNull(FusoPresenze.ToUtc(new DateOnly(2027, 3, 28), "03:30"));
    }

    [Fact]
    public void Fine_dell_ora_legale_ora_doppia_vale_la_prima()
    {
        // 31 ottobre 2027: le 02:30 capitano due volte; la prima è ancora ora legale (UTC+2) = 00:30 UTC.
        var utc = FusoPresenze.ToUtc(new DateOnly(2027, 10, 31), "02:30");
        Assert.Equal(new DateTime(2027, 10, 31, 0, 30, 0, DateTimeKind.Utc), utc!.Value);
    }

    [Fact]
    public void Andata_e_ritorno_UTC_locale()
    {
        for (var h = 0; h < 24; h++)
            foreach (var giorno in new[] { new DateOnly(2027, 2, 3), new DateOnly(2027, 8, 20) })
            {
                var ora = $"{h:00}:15";
                var utc = FusoPresenze.ToUtc(giorno, ora);
                Assert.Equal(ora, FusoPresenze.OraLocale(utc!.Value));
                Assert.Equal(giorno, FusoPresenze.GiornoLocale(utc.Value));
            }
    }

    [Fact]
    public void Ora_non_valida_solleva_errore()
    {
        Assert.Throws<ArgumentException>(() => FusoPresenze.ToUtc(new DateOnly(2027, 1, 1), "8:30"));
    }

    [Fact]
    public void Al_minuto_azzera_i_secondi()
    {
        var t = new DateTime(2027, 1, 15, 8, 30, 59, 700, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2027, 1, 15, 8, 30, 0, DateTimeKind.Utc), FusoPresenze.AlMinuto(t));
    }

    // ---------------------------------------------------------------- patroni

    [Fact]
    public void Patroni_delle_sedi()
    {
        var cattolica = RegolePresenze.Patrono("Cattolica", 2027);
        var pesaro = RegolePresenze.Patrono("pesaro", 2027);
        Assert.Equal(new DateOnly(2027, 4, 30), cattolica!.Giorno);
        Assert.Equal(new DateOnly(2027, 9, 24), pesaro!.Giorno);
        Assert.Null(RegolePresenze.Patrono("Altrove", 2027));
    }

    // ---------------------------------------------------------------- testi e validazioni

    [Fact]
    public void Testi_ripuliti_e_tagliati()
    {
        Assert.Null(RegolePresenze.NormalizzaTesto(null, 10));
        Assert.Null(RegolePresenze.NormalizzaTesto("   ", 10));
        Assert.Equal("cliente", RegolePresenze.NormalizzaTesto("  cliente ", 120));
        Assert.Equal(120, RegolePresenze.NormalizzaTesto(new string('x', 300), RegolePresenze.MaxLuogo)!.Length);
    }

    [Fact]
    public void Tipo_motivo_e_ora()
    {
        Assert.Null(RegolePresenze.ValidaTipo("E"));
        Assert.Null(RegolePresenze.ValidaTipo("U"));
        Assert.NotNull(RegolePresenze.ValidaTipo("X"));
        Assert.NotNull(RegolePresenze.ValidaTipo(null));
        Assert.NotNull(RegolePresenze.ValidaMotivo(""));
        Assert.NotNull(RegolePresenze.ValidaMotivo("  "));
        Assert.Null(RegolePresenze.ValidaMotivo("dimenticata uscita"));
        Assert.Null(RegolePresenze.ValidaOra("08:05", out var ora));
        Assert.Equal("08:05", ora);
        Assert.NotNull(RegolePresenze.ValidaOra("8.05", out _));
    }

    // ---------------------------------------------------------------- assenze

    [Fact]
    public void Giornata_intera_su_piu_giorni_salta_weekend_festivi_e_chiusure()
    {
        var calendario = new Dictionary<DateOnly, VoceCalendario>
        {
            [new DateOnly(2027, 1, 6)] = new("FE", "Epifania"),
            [new DateOnly(2027, 1, 8)] = new("CH", "Ponte"),
        };
        var esito = RegolePresenze.PianificaAssenze(Lavoratore(), "FER", null,
            new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 11), true, null, calendario);
        Assert.True(esito.Valido);
        Assert.Equal(4, esito.Giorni.Count);   // lun 4, mar 5, gio 7, lun 11
        Assert.True(esito.Giorni.All(g => g.Ore == 8.0));
        Assert.Equal(new DateOnly(2027, 1, 7), esito.Giorni[2].Giorno);
    }

    [Fact]
    public void Assenza_a_ore_di_un_solo_giorno_vale_anche_senza_contratto()
    {
        var sabato = new DateOnly(2027, 1, 9);
        var esito = RegolePresenze.PianificaAssenze(Lavoratore(), "PER", null, sabato, sabato, false, "2:30", SenzaCalendario);
        Assert.True(esito.Valido);
        Assert.Equal(1, esito.Giorni.Count);
        Assert.Equal(2.5, esito.Giorni[0].Ore, 6);
    }

    [Fact]
    public void Giornata_intera_di_un_giorno_senza_contratto_non_ha_giorni()
    {
        var sabato = new DateOnly(2027, 1, 9);
        var esito = RegolePresenze.PianificaAssenze(Lavoratore(), "FER", null, sabato, sabato, true, null, SenzaCalendario);
        Assert.False(esito.Valido);
        Assert.Equal("Nessun giorno lavorativo nel periodo indicato.", esito.Errore);
    }

    [Fact]
    public void Ore_per_ogni_giorno_dell_intervallo_con_ore_fisse()
    {
        var esito = RegolePresenze.PianificaAssenze(Lavoratore(), "PER", null,
            new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 5), false, "3", SenzaCalendario);
        Assert.True(esito.Valido);
        Assert.Equal(2, esito.Giorni.Count);
        Assert.True(esito.Giorni.All(g => g.Ore == 3.0));
    }

    [Fact]
    public void Giornata_intera_usa_le_ore_del_giorno_della_settimana()
    {
        var l = new LavoratorePresenze("BIANCHI", "#92D050", new double[] { 4, 8, 8, 8, 8, 0, 0 });
        var esito = RegolePresenze.PianificaAssenze(l, "FER", null,
            new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 5), true, null, SenzaCalendario);
        Assert.Equal(4.0, esito.Giorni[0].Ore, 6);
        Assert.Equal(8.0, esito.Giorni[1].Ore, 6);
    }

    [Theory]
    [InlineData("XXX", null, "2027-01-04", "2027-01-04", "2", "Causale non valida.")]
    [InlineData("ALT", "  ", "2027-01-04", "2027-01-04", "2", "Per la causale 'Altro' descrivi il motivo nelle note.")]
    [InlineData("FER", null, "2027-01-05", "2027-01-04", "2", "Intervallo di date non valido (massimo 2 mesi).")]
    [InlineData("FER", null, "2027-01-04", "2027-03-10", "2", "Intervallo di date non valido (massimo 2 mesi).")]
    [InlineData("PER", null, "2027-01-04", "2027-01-04", "", "Indica le ore (es. 2 oppure 2:30).")]
    [InlineData("PER", null, "2027-01-04", "2027-01-04", "abc", "Indica le ore (es. 2 oppure 2:30).")]
    [InlineData("PER", null, "2027-01-04", "2027-01-04", "0", "Indica le ore (es. 2 oppure 2:30).")]
    [InlineData("PER", null, "2027-01-04", "2027-01-04", "25", "Indica le ore (es. 2 oppure 2:30).")]
    public void Errori_di_validazione_delle_assenze(string causale, string? note, string dal, string al, string ore, string atteso)
    {
        var esito = RegolePresenze.PianificaAssenze(Lavoratore(), causale, note,
            DateOnly.Parse(dal), DateOnly.Parse(al), false, ore, SenzaCalendario);
        Assert.False(esito.Valido);
        Assert.Equal(atteso, esito.Errore);
    }

    [Fact]
    public void Intervallo_di_due_mesi_esatti_e_ammesso_il_giorno_dopo_no()
    {
        var dal = new DateOnly(2027, 1, 4);
        var ok = RegolePresenze.PianificaAssenze(Lavoratore(), "FER", null, dal, dal.AddDays(62), true, null, SenzaCalendario);
        var troppo = RegolePresenze.PianificaAssenze(Lavoratore(), "FER", null, dal, dal.AddDays(63), true, null, SenzaCalendario);
        Assert.True(ok.Valido);
        Assert.False(troppo.Valido);
    }

    [Fact]
    public void Le_note_si_tagliano_a_200_caratteri_e_la_causale_altro_le_richiede()
    {
        var lunga = new string('n', 500);
        var esito = RegolePresenze.PianificaAssenze(Lavoratore(), "ALT", lunga,
            new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 4), true, null, SenzaCalendario);
        Assert.True(esito.Valido);
        Assert.Equal(200, esito.Note!.Length);
    }
}
