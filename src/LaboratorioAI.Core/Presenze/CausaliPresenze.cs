#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace LaboratorioAI.Core.Presenze;

/// <summary>Causale di assenza: codice (come nel database), descrizione e sigla breve per il prospetto.</summary>
public sealed record CausalePresenza(string Codice, string Descrizione, string Sigla);

/// <summary>Elenco delle causali di assenza. Ferie e Permesso (FER, PER) sono quelle che scalano i residui.</summary>
public static class CausaliPresenze
{
    public const string Ferie = "FER";
    public const string Permesso = "PER";
    public const string Altro = "ALT";

    /// <summary>Descrizione della chiusura aziendale (quando la voce di calendario non ne ha una).</summary>
    public const string Chiusura = "Chiusura aziendale";

    public static IReadOnlyList<CausalePresenza> Tutte { get; } = new List<CausalePresenza>
    {
        new(Ferie, "Ferie", "F"),
        new(Permesso, "Permesso", "P"),
        new("104", "Permesso L. 104", "104"),
        new("SEG", "Seggio elettorale", "SE"),
        new("MAL", "Malattia", "M"),
        new("MAT", "Congedo matrimoniale", "MAT"),
        new("DS", "Donazione sangue", "DS"),
        new(Altro, "Altro", "ALT"),
    };

    public static bool Esiste(string? codice) =>
        codice is not null && Tutte.Any(c => c.Codice == codice);

    /// <summary>Descrizione estesa. Solleva <see cref="ArgumentException"/> se il codice non esiste.</summary>
    public static string Descrizione(string codice) => Trova(codice).Descrizione;

    /// <summary>Sigla breve. Solleva <see cref="ArgumentException"/> se il codice non esiste.</summary>
    public static string Sigla(string codice) => Trova(codice).Sigla;

    private static CausalePresenza Trova(string codice)
    {
        foreach (var c in Tutte)
            if (c.Codice == codice) return c;
        throw new ArgumentException($"Causale di assenza sconosciuta: {codice}", nameof(codice));
    }
}
