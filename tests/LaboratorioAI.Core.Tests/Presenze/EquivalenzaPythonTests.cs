#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using LaboratorioAI.Core.Presenze;
using Xunit;

namespace LaboratorioAI.Core.Tests.Presenze;

/// <summary>
/// Prova di equivalenza con l'app «Presenze» di Vaccarini: i casi in <see cref="CasiEquivalenzaPython"/>
/// sono stati generati chiamando le funzioni ORIGINALI Python (calcola_giorno, contenuto, calcola_periodo, totali).
/// Il motore .NET deve dare gli stessi identici risultati, giorno per giorno e cella per cella.
/// Per rigenerare i casi: EquivalenzaPython/genera_casi.py.
/// </summary>
public class EquivalenzaPythonTests
{
    private const double Tolleranza = 1e-9;

    private static readonly Lazy<CasiDto> Casi = new(() =>
        JsonSerializer.Deserialize<CasiDto>(
            CasiEquivalenzaPython.Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!);

    public static IEnumerable<object[]> IdGiorni() => Casi.Value.Giorni.Select(c => new object[] { c.Id });

    public static IEnumerable<object[]> IdPeriodi() => Casi.Value.Periodi.Select(c => new object[] { c.Id });

    // ---------------------------------------------------------------- giorni singoli

    [Theory]
    [MemberData(nameof(IdGiorni))]
    public void Giorno_come_nell_originale(string id)
    {
        var caso = Casi.Value.Giorni.Single(c => c.Id == id);
        var lavoratore = Lavoratore(caso.Utente);
        var timbrature = caso.Timbrature.Select(t => new TimbraturaGiorno(t.Ora, t.Tipo, t.Luogo)).ToList();
        var assenze = caso.Assenze.Select(a => new AssenzaGiorno(a.Causale, a.Ore, a.Note)).ToList();
        VoceCalendario? calendario = caso.Cal is null
            ? null
            : new VoceCalendario(caso.Cal.Tipo, caso.Cal.Descrizione, caso.Cal.Ore);

        var giorno = MotoreCalcoloPresenze.CalcolaGiorno(
            lavoratore, Data(caso.Giorno), timbrature, assenze, calendario,
            Data(caso.Oggi), caso.Inizio is null ? null : Data(caso.Inizio));

        ControllaGiorno(id, giorno, caso.Atteso);
        ControllaCella(id, ContenutoCellaPresenze.Costruisci(lavoratore, giorno), caso.Cella);
    }

    // ---------------------------------------------------------------- mesi interi

    [Theory]
    [MemberData(nameof(IdPeriodi))]
    public void Mese_come_nell_originale(string id)
    {
        var caso = Casi.Value.Periodi.Single(c => c.Id == id);
        var lavoratore = Lavoratore(caso.Utente);
        var timbrature = caso.Timbrature.ToLookup(
            t => Data(t.Data!), t => new TimbraturaGiorno(t.Ora, t.Tipo, t.Luogo));
        var assenze = caso.Assenze.ToLookup(
            a => Data(a.Data!), a => new AssenzaGiorno(a.Causale, a.Ore, a.Note));
        var calendario = caso.Calendario.ToDictionary(
            v => Data(v.Giorno!), v => new VoceCalendario(v.Tipo, v.Descrizione, v.Ore));

        var giorni = MotoreCalcoloPresenze.CalcolaPeriodo(
            lavoratore, Data(caso.Dal), Data(caso.Al), timbrature, assenze, calendario,
            Data(caso.Oggi), caso.Inizio is null ? null : Data(caso.Inizio));

        Assert.Equal(caso.Giorni.Count, giorni.Count);
        for (var i = 0; i < giorni.Count; i++)
        {
            var atteso = caso.Giorni[i];
            var contesto = $"{id} {atteso.Giorno}";
            Assert.True(atteso.Giorno == giorni[i].Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                $"{contesto}: data");
            ControllaGiorno(contesto, giorni[i], atteso.Atteso);
            ControllaCella(contesto, ContenutoCellaPresenze.Costruisci(lavoratore, giorni[i]), atteso.Cella);
        }

        var totali = MotoreCalcoloPresenze.CalcolaTotali(giorni);
        Vicini(id + " totali contratto", caso.Totali.Contratto, totali.Contratto);
        Vicini(id + " totali lavorate", caso.Totali.Lavorate, totali.Lavorate);
        Vicini(id + " totali straordinario", caso.Totali.Straordinario, totali.Straordinario);
        Vicini(id + " totali non lavorate", caso.Totali.NonLavorate, totali.NonLavorate);
        Vicini(id + " totali chiusura", caso.Totali.Chiusura, totali.Chiusura);
        Assert.True(caso.Totali.GiorniConAnomalie == totali.GiorniConAnomalie, id + " giorni con anomalie");
        Assert.True(caso.Totali.Causali.Count == totali.Causali.Count, id + " numero di causali");
        foreach (var (causale, ore) in caso.Totali.Causali)
        {
            Assert.True(totali.Causali.ContainsKey(causale), $"{id} causale {causale} mancante");
            Vicini($"{id} causale {causale}", ore, totali.Causali[causale]);
        }
    }

    [Fact]
    public void I_casi_coprono_le_situazioni_principali()
    {
        // controllo di sanità sui dati generati: devono esserci tutte le varianti di cella e di anomalia
        var celle = Casi.Value.Giorni.Select(c => c.Cella).Where(c => c is not null).Select(c => c!).ToList();
        Assert.True(celle.Any(c => c.CarattereRossoOStraordinario()), "nessuno straordinario nei casi");
        Assert.True(celle.Any(c => c.Sfondo == ContenutoCellaPresenze.Grigio), "nessuna ora non lavorata nei casi");
        Assert.True(celle.Any(c => c.Sfondo == ContenutoCellaPresenze.Rosso), "nessuna chiusura nei casi");
        Assert.True(celle.Any(c => c.Testo == "FE"), "nessuna festività nei casi");
        Assert.True(celle.Any(c => c.Testo == "FS"), "nessun fine settimana nei casi");
        Assert.True(celle.Any(c => c.Nota.Contains("Fuori sede")), "nessun fuori sede nei casi");
        Assert.True(celle.Any(c => c.Nota.Contains("Da controllare")), "nessuna anomalia nei casi");
        Assert.True(Casi.Value.Giorni.Any(c => c.Atteso.PrimaInizio), "nessun giorno precedente all'avvio");
        Assert.True(Casi.Value.Giorni.Any(c => c.Atteso.InCorso), "nessun giorno in corso");
        Assert.True(Casi.Value.Giorni.Any(c => c.Atteso.Futuro), "nessun giorno futuro");
        Assert.True(Casi.Value.Giorni.Count >= 200, "pochi casi giorno");
        Assert.True(Casi.Value.Periodi.Count >= 10, "pochi casi mese");
    }

    // ---------------------------------------------------------------- confronti

    private static void ControllaGiorno(string contesto, CalcoloGiorno g, AttesoDto a)
    {
        Vicini(contesto + " contratto", a.Contratto, g.Contratto);
        Vicini(contesto + " lavorate", a.Lavorate, g.Lavorate);
        Vicini(contesto + " ore assenze", a.OreAssenze, g.OreAssenze);
        Vicini(contesto + " chiusura", a.Chiusura, g.Chiusura);
        Vicini(contesto + " straordinario", a.Straordinario, g.Straordinario);
        Vicini(contesto + " non lavorate", a.NonLavorate, g.NonLavorate);
        Assert.True(a.Festivo == g.Festivo, contesto + " festivo");
        Assert.True(a.Weekend == g.Weekend, contesto + " weekend");
        Assert.True(a.InCorso == g.InCorso, contesto + " in corso");
        Assert.True(a.Futuro == g.Futuro, contesto + " futuro");
        Assert.True(a.PrimaInizio == g.PrimaInizio, contesto + " prima inizio");
        Assert.True(a.FestivoDescrizione == g.FestivoDescrizione, contesto + " descrizione festivo");
        Assert.True(a.ChiusuraDescrizione == g.ChiusuraDescrizione, contesto + " descrizione chiusura");

        var attese = string.Join(" | ", a.Coppie.Select(c => $"{c[0]}-{c[1]}"));
        var reali = string.Join(" | ", g.Coppie.Select(c => $"{c.Entrata}-{c.Uscita}"));
        Assert.True(attese == reali, $"{contesto} coppie: atteso [{attese}], ottenuto [{reali}]");

        var anomalieAttese = string.Join(" | ", a.Anomalie);
        var anomalieReali = string.Join(" | ", g.Anomalie);
        Assert.True(anomalieAttese == anomalieReali,
            $"{contesto} anomalie: atteso [{anomalieAttese}], ottenuto [{anomalieReali}]");
    }

    private static void ControllaCella(string contesto, ContenutoCella? reale, CellaDto? atteso)
    {
        if (atteso is null)
        {
            Assert.True(reale is null, contesto + " cella: attesa nessuna cella");
            return;
        }
        Assert.True(reale is not null, contesto + " cella: attesa una cella");
        var r = reale!;
        if (atteso.Ore is null)
            Assert.True(r.Ore is null, contesto + " cella: ore attese vuote");
        else
        {
            Assert.True(r.Ore is not null, contesto + " cella: ore attese " + atteso.Ore);
            Vicini(contesto + " cella ore", atteso.Ore.Value, r.Ore!.Value);
        }
        Assert.True(atteso.Testo == r.Testo, $"{contesto} cella testo: atteso [{atteso.Testo}], ottenuto [{r.Testo}]");
        Assert.True(atteso.Sfondo == r.Sfondo, $"{contesto} cella sfondo: atteso [{atteso.Sfondo}], ottenuto [{r.Sfondo}]");
        Assert.True(atteso.Rosso == r.CarattereRosso, contesto + " cella carattere rosso");
        Assert.True(atteso.Grassetto == r.Grassetto, contesto + " cella grassetto");
        Assert.True(atteso.Corsivo == r.Corsivo, contesto + " cella corsivo");
        Assert.True(atteso.Nota == r.Nota, $"{contesto} cella nota: atteso [{atteso.Nota}], ottenuto [{r.Nota}]");
    }

    private static void Vicini(string campo, double atteso, double reale) =>
        Assert.True(Math.Abs(atteso - reale) < Tolleranza, $"{campo}: atteso {atteso}, ottenuto {reale}");

    private static DateOnly Data(string testo) =>
        DateOnly.ParseExact(testo, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static LavoratorePresenze Lavoratore(UtenteDto u) =>
        new("ROSSI", u.Colore, u.Ore, u.AttivoDal is null ? null : Data(u.AttivoDal));
}

// ---------------------------------------------------------------- strutture del file dei casi

internal sealed class CasiDto
{
    public List<CasoGiornoDto> Giorni { get; set; } = new();
    public List<CasoPeriodoDto> Periodi { get; set; } = new();
}

internal sealed class UtenteDto
{
    public string Colore { get; set; } = "";
    public List<double> Ore { get; set; } = new();
    public string? AttivoDal { get; set; }
}

internal sealed class TimbraturaDto
{
    public string? Data { get; set; }
    public string Ora { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string? Luogo { get; set; }
}

internal sealed class AssenzaDto
{
    public string? Data { get; set; }
    public string Causale { get; set; } = "";
    public double Ore { get; set; }
    public string? Note { get; set; }
}

internal sealed class VoceCalendarioDto
{
    public string? Giorno { get; set; }
    public string Tipo { get; set; } = "";
    public string? Descrizione { get; set; }
    public double? Ore { get; set; }
}

internal sealed class AttesoDto
{
    public double Contratto { get; set; }
    public double Lavorate { get; set; }
    public List<List<int>> Coppie { get; set; } = new();
    public double OreAssenze { get; set; }
    public double Chiusura { get; set; }
    public bool Festivo { get; set; }
    public string FestivoDescrizione { get; set; } = "";
    public string ChiusuraDescrizione { get; set; } = "";
    public bool Weekend { get; set; }
    public double Straordinario { get; set; }
    public double NonLavorate { get; set; }
    public List<string> Anomalie { get; set; } = new();
    public bool InCorso { get; set; }
    public bool Futuro { get; set; }
    public bool PrimaInizio { get; set; }
}

internal sealed class CellaDto
{
    public double? Ore { get; set; }
    public string? Testo { get; set; }
    public string? Sfondo { get; set; }
    public bool Rosso { get; set; }
    public bool Grassetto { get; set; }
    public bool Corsivo { get; set; }
    public string Nota { get; set; } = "";

    public bool CarattereRossoOStraordinario() => Rosso && Ore is not null;
}

internal sealed class CasoGiornoDto
{
    public string Id { get; set; } = "";
    public UtenteDto Utente { get; set; } = new();
    public string Giorno { get; set; } = "";
    public string Oggi { get; set; } = "";
    public string? Inizio { get; set; }
    public List<TimbraturaDto> Timbrature { get; set; } = new();
    public List<AssenzaDto> Assenze { get; set; } = new();
    public VoceCalendarioDto? Cal { get; set; }
    public AttesoDto Atteso { get; set; } = new();
    public CellaDto? Cella { get; set; }
}

internal sealed class GiornoPeriodoDto
{
    public string Giorno { get; set; } = "";
    public AttesoDto Atteso { get; set; } = new();
    public CellaDto? Cella { get; set; }
}

internal sealed class TotaliDto
{
    public double Contratto { get; set; }
    public double Lavorate { get; set; }
    public double Straordinario { get; set; }
    public double NonLavorate { get; set; }
    public double Chiusura { get; set; }
    public Dictionary<string, double> Causali { get; set; } = new();
    public int GiorniConAnomalie { get; set; }
}

internal sealed class CasoPeriodoDto
{
    public string Id { get; set; } = "";
    public UtenteDto Utente { get; set; } = new();
    public string Dal { get; set; } = "";
    public string Al { get; set; } = "";
    public string Oggi { get; set; } = "";
    public string? Inizio { get; set; }
    public List<TimbraturaDto> Timbrature { get; set; } = new();
    public List<AssenzaDto> Assenze { get; set; } = new();
    public List<VoceCalendarioDto> Calendario { get; set; } = new();
    public List<GiornoPeriodoDto> Giorni { get; set; } = new();
    public TotaliDto Totali { get; set; } = new();
}
