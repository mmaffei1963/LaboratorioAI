#nullable enable
using System;
using System.Collections.Generic;

namespace LaboratorioAI.Core.Presenze;

/// <summary>
/// Lavoratore, per quanto serve al calcolo delle presenze. Nessun accesso al database:
/// i servizi dello strato Dati costruiscono questo oggetto dalle tabelle <c>presenze.Lavoratori</c>.
/// </summary>
public sealed class LavoratorePresenze
{
    public LavoratorePresenze(string nome, string colore, IReadOnlyList<double> oreContratto, DateOnly? attivoDal = null)
    {
        if (oreContratto is null || oreContratto.Count != 7)
            throw new ArgumentException("Servono 7 valori di ore da contratto (da lunedì a domenica).", nameof(oreContratto));
        Nome = nome;
        Colore = colore;
        OreContrattoPerGiorno = oreContratto;
        AttivoDal = attivoDal;
    }

    /// <summary>Cognome come compare nel prospetto.</summary>
    public string Nome { get; }

    /// <summary>Colore del lavoratore nel formato <c>#RRGGBB</c>.</summary>
    public string Colore { get; }

    /// <summary>Ore da contratto per giorno della settimana: indice 0 = lunedì, 6 = domenica.</summary>
    public IReadOnlyList<double> OreContrattoPerGiorno { get; }

    /// <summary>Prima di questa data le ore da contratto sono zero (assunzione in corso d'anno).</summary>
    public DateOnly? AttivoDal { get; }
}

/// <summary>Una timbratura del giorno: orario locale <c>HH:mm</c> e tipo <c>E</c> (entrata) o <c>U</c> (uscita).</summary>
public sealed record TimbraturaGiorno(string Ora, string Tipo, string? Luogo = null);

/// <summary>Un'assenza del giorno: codice causale (vedi <see cref="CausaliPresenze"/>) e ore.</summary>
public sealed record AssenzaGiorno(string Causale, double Ore, string? Note = null);

/// <summary>Voce del calendario aziendale: <c>FE</c> festività, <c>CH</c> chiusura aziendale (ore null = intera giornata).</summary>
public sealed record VoceCalendario(string Tipo, string? Descrizione = null, double? Ore = null)
{
    public const string Festivita = "FE";
    public const string Chiusura = "CH";
}

/// <summary>Coppia entrata-uscita in minuti dalla mezzanotte, già arrotondati al quarto d'ora.</summary>
public sealed record CoppiaMinuti(int Entrata, int Uscita);

/// <summary>Risultato dell'abbinamento delle timbrature in coppie.</summary>
public sealed record RisultatoCoppie(IReadOnlyList<CoppiaMinuti> Coppie, IReadOnlyList<string> Anomalie, bool Aperta);

/// <summary>Festività nazionale con la sua descrizione.</summary>
public sealed record FestivitaNazionale(DateOnly Giorno, string Descrizione);

/// <summary>Calcolo di un giorno per un lavoratore.</summary>
public sealed class CalcoloGiorno
{
    public DateOnly Data { get; init; }

    /// <summary>Ore da contratto del giorno (0 se festivo, prima dell'assunzione o prima dell'avvio dei conteggi).</summary>
    public double Contratto { get; init; }

    public double Lavorate { get; init; }
    public IReadOnlyList<CoppiaMinuti> Coppie { get; init; } = Array.Empty<CoppiaMinuti>();
    public IReadOnlyList<AssenzaGiorno> Assenze { get; init; } = Array.Empty<AssenzaGiorno>();
    public double OreAssenze { get; init; }

    /// <summary>Ore coperte dalla chiusura aziendale.</summary>
    public double Chiusura { get; init; }

    public bool Festivo { get; init; }
    public string FestivoDescrizione { get; init; } = "";
    public string ChiusuraDescrizione { get; init; } = "";
    public bool Weekend { get; init; }
    public double Straordinario { get; init; }
    public double NonLavorate { get; init; }
    public IReadOnlyList<string> Anomalie { get; init; } = Array.Empty<string>();

    /// <summary>Oggi, con l'ultima timbratura di entrata ancora senza uscita.</summary>
    public bool InCorso { get; init; }

    public bool Futuro { get; init; }

    /// <summary>Giorno precedente alla data di avvio dei conteggi.</summary>
    public bool PrimaInizio { get; init; }

    /// <summary>Timbrature del giorno come passate al calcolo (servono per i luoghi «fuori sede»).</summary>
    public IReadOnlyList<TimbraturaGiorno> Timbrature { get; init; } = Array.Empty<TimbraturaGiorno>();
}

/// <summary>Totali di un periodo (di norma un mese).</summary>
public sealed class TotaliPeriodo
{
    /// <summary>Ore da contratto fino a oggi (i giorni futuri non contano).</summary>
    public double Contratto { get; init; }

    public double Lavorate { get; init; }
    public double Straordinario { get; init; }
    public double NonLavorate { get; init; }
    public double Chiusura { get; init; }

    /// <summary>Ore di assenza per codice causale.</summary>
    public IReadOnlyDictionary<string, double> Causali { get; init; } = new Dictionary<string, double>();

    /// <summary>Numero di giorni con almeno un'anomalia.</summary>
    public int GiorniConAnomalie { get; init; }
}
