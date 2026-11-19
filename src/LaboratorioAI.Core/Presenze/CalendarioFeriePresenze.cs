#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace LaboratorioAI.Core.Presenze;

/// <summary>
/// Calendario delle ferie visibile a tutti (stile CALENDARIO Excel): per ogni lavoratore e giorno mostra solo ferie e permessi,
/// le festività, le chiusure aziendali e il fine settimana. Le altre causali (malattia, L. 104, donazione sangue...) non si condividono
/// con i colleghi: sono dati personali e a volte sulla salute.
/// </summary>
public static class CalendarioFeriePresenze
{
    /// <summary>Solo ferie e permessi sono visibili ai colleghi.</summary>
    public static bool Condivisibile(string causale) => causale is CausaliPresenze.Ferie or CausaliPresenze.Permesso;

    /// <summary>
    /// Contenuto della cella lavoratore/giorno nel calendario delle ferie, oppure null se non c'è nulla da mostrare.
    /// <paramref name="assenze"/> sono le assenze del giorno già filtrate con <see cref="Condivisibile"/>.
    /// </summary>
    public static ContenutoCella? Cella(LavoratorePresenze lavoratore, DateOnly giorno, IReadOnlyList<AssenzaGiorno> assenze, VoceCalendario? voce)
    {
        var ore = R2(assenze.Where(a => Condivisibile(a.Causale)).Sum(a => a.Ore));
        if (ore > 0)
        {
            var nota = string.Join("; ", assenze.Where(a => Condivisibile(a.Causale))
                .GroupBy(a => a.Causale)
                .Select(g => $"{CausaliPresenze.Descrizione(g.Key)} {MotoreCalcoloPresenze.OreFmt(g.Sum(a => a.Ore))}"));
            return new ContenutoCella
            {
                Ore = ore, Sfondo = ContenutoCellaPresenze.ColoreArgb(lavoratore.Colore), Grassetto = true, Nota = nota,
            };
        }
        if (voce is not null && voce.Tipo == VoceCalendario.Festivita)
            return new ContenutoCella { Testo = "FE", Sfondo = ContenutoCellaPresenze.Giallo, Nota = voce.Descrizione ?? "" };
        if (voce is not null && voce.Tipo == VoceCalendario.Chiusura)
        {
            var contratto = MotoreCalcoloPresenze.OreContratto(lavoratore, giorno);
            var oreChiusura = voce.Ore ?? contratto;
            if (oreChiusura > 0)
                return new ContenutoCella
                {
                    Ore = R2(oreChiusura), Sfondo = ContenutoCellaPresenze.Rosso, Grassetto = true,
                    Nota = string.IsNullOrEmpty(voce.Descrizione) ? CausaliPresenze.Chiusura : voce.Descrizione,
                };
        }
        if (MotoreCalcoloPresenze.OreContratto(lavoratore, giorno) == 0 &&
            giorno.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return new ContenutoCella { Testo = "FS", Sfondo = ContenutoCellaPresenze.Giallo };
        return null;
    }

    /// <summary>Il lavoratore ha ferie o permessi quel giorno (conta per la riga «assenti»).</summary>
    public static bool Assente(IReadOnlyList<AssenzaGiorno> assenze) =>
        assenze.Any(a => Condivisibile(a.Causale) && a.Ore > 0);

    private static double R2(double x) => Math.Round(x + 1e-9, 2, MidpointRounding.ToEven);
}
