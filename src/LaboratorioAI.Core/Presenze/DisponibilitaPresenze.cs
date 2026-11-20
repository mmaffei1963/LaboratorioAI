#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace LaboratorioAI.Core.Presenze;

/// <summary>Stato di un lavoratore in questo momento, come lo vedono i colleghi («Chi c'è adesso»).</summary>
public enum StatoDisponibilita
{
    /// <summary>L'ultima timbratura di oggi è un'entrata.</summary>
    InServizio,
    /// <summary>L'ultima timbratura di oggi è un'uscita (pausa o fine giornata).</summary>
    Uscito,
    /// <summary>Oggi si lavora ma non ha ancora timbrato.</summary>
    NonTimbrato,
    /// <summary>Assenza per la giornata intera, di qualunque causale (la causale non si mostra).</summary>
    Assente,
    /// <summary>Oggi non è previsto (festività, chiusura, giorno senza ore da contratto).</summary>
    NonPrevisto,
}

/// <summary>Stato di un lavoratore oggi: <paramref name="Ora"/> è l'orario dell'ultima timbratura (HH:mm), <paramref name="Luogo"/> solo se in servizio.</summary>
public sealed record DisponibilitaGiorno(StatoDisponibilita Stato, string? Ora, string? Luogo, bool PermessoParziale, string? Motivo);

/// <summary>Una riga della pagina «Chi c'è adesso».</summary>
public sealed record RigaDisponibilita(int IdLavoratore, string Cognome, string Colore, int IdSede, string Sede, DisponibilitaGiorno Giorno);

/// <summary>
/// Regole di «Chi c'è adesso» (20/11/2026). Parte dal calcolo del giorno già esistente.
/// Riservatezza: dell'assenza si mostra solo che c'è (mai la causale); l'eventuale permesso di poche ore è «parziale» senza causale.
/// Una timbratura fa fede più di un'assenza registrata: chi ha timbrato è in servizio.
/// </summary>
public static class DisponibilitaPresenze
{
    private const double Tolleranza = 0.01;

    public static DisponibilitaGiorno Valuta(CalcoloGiorno g)
    {
        var timbrature = g.Timbrature.OrderBy(t => t.Ora, StringComparer.Ordinal).ToList();
        var oreAssenza = g.Assenze.Sum(a => a.Ore);
        var haAssenze = g.Assenze.Count > 0 && oreAssenza > 0;
        var assenzaIntera = haAssenze && (g.Contratto <= 0 || oreAssenza >= g.Contratto - Tolleranza);
        var parziale = haAssenze && !assenzaIntera;

        if (timbrature.Count > 0)
        {
            var ultima = timbrature[^1];
            if (ultima.Tipo == "E")
                return new DisponibilitaGiorno(StatoDisponibilita.InServizio, ultima.Ora, string.IsNullOrWhiteSpace(ultima.Luogo) ? null : ultima.Luogo!.Trim(), parziale, null);
            return new DisponibilitaGiorno(StatoDisponibilita.Uscito, ultima.Ora, null, parziale, null);
        }

        if (assenzaIntera)
            return new DisponibilitaGiorno(StatoDisponibilita.Assente, null, null, false, null);

        if (g.Festivo)
            return new DisponibilitaGiorno(StatoDisponibilita.NonPrevisto, null, null, false, string.IsNullOrEmpty(g.FestivoDescrizione) ? "Festività" : g.FestivoDescrizione);

        if (g.ChiusuraDescrizione.Length > 0 && g.Chiusura >= g.Contratto - Tolleranza && g.Chiusura > 0)
            return new DisponibilitaGiorno(StatoDisponibilita.NonPrevisto, null, null, false, g.ChiusuraDescrizione);

        if (g.Contratto <= 0)
            return new DisponibilitaGiorno(StatoDisponibilita.NonPrevisto, null, null, false, g.Weekend ? "Fine settimana" : "Non lavora oggi");

        return new DisponibilitaGiorno(StatoDisponibilita.NonTimbrato, null, null, parziale, null);
    }

    /// <summary>Quanti sono in servizio in questo momento.</summary>
    public static int InServizio(IEnumerable<RigaDisponibilita> righe) => righe.Count(r => r.Giorno.Stato == StatoDisponibilita.InServizio);

    /// <summary>Testo breve dello stato, per la pagina.</summary>
    public static string Descrizione(StatoDisponibilita stato) => stato switch
    {
        StatoDisponibilita.InServizio => "In servizio",
        StatoDisponibilita.Uscito => "Uscito/a",
        StatoDisponibilita.NonTimbrato => "Non ancora timbrato",
        StatoDisponibilita.Assente => "Assente",
        _ => "Non previsto oggi",
    };
}
