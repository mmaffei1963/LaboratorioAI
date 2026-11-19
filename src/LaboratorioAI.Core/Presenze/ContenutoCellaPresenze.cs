#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace LaboratorioAI.Core.Presenze;

/// <summary>
/// Cosa mostrare nella cella lavoratore/giorno del prospetto mensile (e nell'esportazione Excel).
/// Colori in formato ARGB esadecimale («FFFFFF00»).
/// </summary>
public sealed class ContenutoCella
{
    /// <summary>Valore numerico in ore (decimali), oppure null.</summary>
    public double? Ore { get; init; }

    /// <summary>Valore testuale («FE», «FS»), oppure null.</summary>
    public string? Testo { get; init; }

    /// <summary>Colore di sfondo, oppure null (nessun colore).</summary>
    public string? Sfondo { get; init; }

    public bool CarattereRosso { get; init; }
    public bool Grassetto { get; init; }
    public bool Corsivo { get; init; }

    /// <summary>Commento della cella (più righe separate da «\n»); stringa vuota se non c'è.</summary>
    public string Nota { get; init; } = "";

    public bool HaValore => Ore.HasValue || Testo is not null;
}

/// <summary>
/// Legenda del prospetto (come il CALENDARIO in uso, portata da excel_master.contenuto dell'app di Vaccarini):
/// FS/FE giallo · chiusure aziendali rosso · assenze nel colore del lavoratore (ore sempre in cella, motivo nel commento) ·
/// straordinari in carattere rosso senza colore di cella · ore non lavorate grigio corsivo.
/// </summary>
public static class ContenutoCellaPresenze
{
    public const string Giallo = "FFFFFF00";
    public const string Rosso = "FFFF0000";
    public const string Grigio = "FFD9D9D9";

    /// <summary>"#ffc000" → "FFFFC000".</summary>
    public static string ColoreArgb(string colore) =>
        "FF" + colore.TrimStart('#').ToUpperInvariant();

    /// <summary>
    /// Contenuto della cella per un giorno calcolato, oppure null quando non c'è nulla da mostrare
    /// (giorni feriali precedenti all'avvio dei conteggi). Quando c'è una cella ma nessun valore,
    /// <see cref="ContenutoCella.HaValore"/> è false.
    /// </summary>
    public static ContenutoCella? Costruisci(LavoratorePresenze lavoratore, CalcoloGiorno g)
    {
        double? ore = null;
        string? testo = null;
        string? sfondo = null;
        bool rosso = false, grassetto = false, corsivo = false;
        var nota = new List<string>();

        if (g.PrimaInizio)
        {
            // prima dell'avvio dell'app: solo fine settimana e festività
            if (g.Festivo)
                return new ContenutoCella { Testo = "FE", Sfondo = Giallo };
            if (g.Weekend)
                return new ContenutoCella { Testo = "FS", Sfondo = Giallo };
            return null;
        }

        var totaleAssenze = R2(g.OreAssenze);
        var haAssenze = g.Assenze.Count > 0;

        if (g.Chiusura > 0)
        {
            ore = R2(g.Chiusura + totaleAssenze);
            sfondo = Rosso;
            grassetto = true;
            nota.Add(string.IsNullOrEmpty(g.ChiusuraDescrizione) ? CausaliPresenze.Chiusura : g.ChiusuraDescrizione);
            if (haAssenze)
                nota.Add(DescrizioneAssenze(g.Assenze));
        }
        else if (haAssenze)
        {
            // sempre le ore in cella (anche per la malattia), il motivo nel commento
            ore = totaleAssenze;
            sfondo = ColoreArgb(lavoratore.Colore);
            grassetto = true;
            nota.Add(DescrizioneAssenze(g.Assenze));
        }
        else if (g.Straordinario > 0)
        {
            ore = R2(g.Straordinario);
            rosso = true;
            grassetto = true;
            nota.Add($"Straordinario {MotoreCalcoloPresenze.OreFmt(g.Straordinario)}");
        }
        else if (g.NonLavorate > 0)
        {
            ore = R2(g.NonLavorate);
            sfondo = Grigio;
            corsivo = true;
        }
        else if (g.Festivo)
        {
            testo = "FE";
            sfondo = Giallo;
        }
        else if (g.Weekend && g.Contratto == 0)
        {
            testo = "FS";
            sfondo = Giallo;
        }

        if (g.Straordinario > 0 && !rosso)
            nota.Add($"Straordinario {MotoreCalcoloPresenze.OreFmt(g.Straordinario)}");
        if (g.NonLavorate > 0 && sfondo != Grigio)
            nota.Add($"Ore non lavorate da giustificare: {MotoreCalcoloPresenze.OreFmt(g.NonLavorate)}");

        var luoghi = g.Timbrature
            .Where(t => !string.IsNullOrEmpty(t.Luogo))
            .Select(t => t.Luogo!)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        if (luoghi.Count > 0 && (ore.HasValue || testo is not null))
            nota.Add("Fuori sede: " + string.Join("; ", luoghi));
        if (g.Anomalie.Count > 0)
            nota.Add("Da controllare: " + string.Join("; ", g.Anomalie));

        return new ContenutoCella
        {
            Ore = ore,
            Testo = testo,
            Sfondo = sfondo,
            CarattereRosso = rosso,
            Grassetto = grassetto,
            Corsivo = corsivo,
            Nota = string.Join("\n", nota),
        };
    }

    /// <summary>«Ferie 8:00; Permesso 2:00 (visita)».</summary>
    public static string DescrizioneAssenze(IEnumerable<AssenzaGiorno> assenze) =>
        string.Join("; ", assenze.Select(a =>
            $"{CausaliPresenze.Descrizione(a.Causale)} {MotoreCalcoloPresenze.OreFmt(a.Ore)}"
            + (string.IsNullOrEmpty(a.Note) ? "" : $" ({a.Note})")));

    private static double R2(double x) =>
        Math.Round(x + 1e-9, 2, MidpointRounding.ToEven);
}
