namespace LaboratorioAI.Core.Entita;

// Presenze (19/11/2026), schema «presenze». Definizione: claude/definizione-presenze.md
// Riscrittura in .NET dell'app «Presenze» di Vaccarini. Il calcolo è in LaboratorioAI.Core.Presenze (motore puro).
// Nessuna cancellazione fisica: timbrature e assenze si annullano; il registro delle modifiche è solo in aggiunta.
// I giorni di calendario sono «date» (il giorno di una timbratura è quello di Europe/Rome); gli istanti sono in UTC.

/// <summary>Sede di lavoro (Cattolica, Pesaro): serve al calendario aziendale (patroni diversi).</summary>
public class SedeAziendale
{
    public int IdSede { get; set; }
    public string Nome { get; set; } = "";
    public bool Attiva { get; set; } = true;
}

/// <summary>Persona che timbra. È collegata al suo utente di LaboratorioAI (Entra ID) per e-mail.</summary>
public class Lavoratore
{
    public int IdLavoratore { get; set; }
    public int IdUtente { get; set; }
    public Utente? Utente { get; set; }
    public int IdSede { get; set; }
    public SedeAziendale? Sede { get; set; }
    /// <summary>Come compare nel prospetto mensile (univoco).</summary>
    public string Cognome { get; set; } = "";
    /// <summary>Colore del prospetto, «#RRGGBB».</summary>
    public string Colore { get; set; } = "#FFC000";
    public int Ordine { get; set; }
    /// <summary>Primo giorno in cui il lavoratore conta le ore (assunzione). Vuoto = da sempre.</summary>
    public DateOnly? AttivoDal { get; set; }

    // Ore da contratto per giorno della settimana.
    public decimal OreLunedi { get; set; } = 8m;
    public decimal OreMartedi { get; set; } = 8m;
    public decimal OreMercoledi { get; set; } = 8m;
    public decimal OreGiovedi { get; set; } = 8m;
    public decimal OreVenerdi { get; set; } = 8m;
    public decimal OreSabato { get; set; }
    public decimal OreDomenica { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    /// <summary>Le sette ore da contratto nell'ordine del motore di calcolo: lunedì = posizione 0.</summary>
    public double[] OreContratto() =>
        new[] { OreLunedi, OreMartedi, OreMercoledi, OreGiovedi, OreVenerdi, OreSabato, OreDomenica }
            .Select(x => (double)x).ToArray();
}

/// <summary>Una timbratura di entrata o uscita. Il minuto è quello vero: l'arrotondamento si fa nel calcolo.</summary>
public class Timbratura
{
    public int IdTimbratura { get; set; }
    public int IdLavoratore { get; set; }
    public Lavoratore? Lavoratore { get; set; }
    /// <summary>Istante della timbratura in UTC, al minuto.</summary>
    public DateTime DataOraUtc { get; set; }
    /// <summary>Giorno di calendario in Europe/Rome.</summary>
    public DateOnly Giorno { get; set; }
    /// <summary>«E» entrata, «U» uscita.</summary>
    public string Tipo { get; set; } = "";
    /// <summary>Luogo facoltativo, testo libero (es. nome del cliente). Nessuna geolocalizzazione.</summary>
    public string? Luogo { get; set; }
    /// <summary>«App» (timbrata dall'interessato) oppure «Manuale» (inserita o corretta da chi gestisce, con motivo nel registro).</summary>
    public string Origine { get; set; } = OrigineTimbratura.App;
    public bool Annullata { get; set; }
    public DateTime? DataAnnullamento { get; set; }
    public string? MotivoAnnullamento { get; set; }
    public int? IdUtenteCreazione { get; set; }
    public DateTime DataCreazione { get; set; }
}

public static class OrigineTimbratura
{
    public const string App = "App";
    public const string Manuale = "Manuale";
}

/// <summary>Assenza di un giorno (ferie, permesso, malattia...). Nell'app originale si cancellava: qui si annulla.</summary>
public class Assenza
{
    public int IdAssenza { get; set; }
    public int IdLavoratore { get; set; }
    public Lavoratore? Lavoratore { get; set; }
    public DateOnly Giorno { get; set; }
    /// <summary>Codice della causale (FER, PER, 104, SEG, MAL, MAT, DS, ALT): vedi <c>CausaliPresenze</c>.</summary>
    public string Causale { get; set; } = "";
    public decimal Ore { get; set; }
    /// <summary>Obbligatoria per la causale «ALT».</summary>
    public string? Note { get; set; }
    public bool Annullata { get; set; }
    public DateTime? DataAnnullamento { get; set; }
    public string? MotivoAnnullamento { get; set; }
    public int? IdUtenteCreazione { get; set; }
    public DateTime DataCreazione { get; set; }
}

/// <summary>Giorno speciale del calendario aziendale: festività («FE») o chiusura aziendale («CH»).</summary>
public class GiornoCalendarioAziendale
{
    public int IdGiornoCalendario { get; set; }
    /// <summary>Vuoto = vale per tutte le sedi (festività nazionali, chiusure generali).</summary>
    public int? IdSede { get; set; }
    public SedeAziendale? Sede { get; set; }
    public DateOnly Giorno { get; set; }
    /// <summary>«FE» festività, «CH» chiusura aziendale.</summary>
    public string Tipo { get; set; } = "";
    public string? Descrizione { get; set; }
    /// <summary>Solo per le chiusure: ore coperte. Vuoto = tutta la giornata di contratto.</summary>
    public decimal? Ore { get; set; }
}

/// <summary>Impostazioni del modulo (una sola riga).</summary>
public class ImpostazionePresenze
{
    public int IdImpostazione { get; set; }
    /// <summary>L'app conta le ore dal questo giorno (1/1/2027): prima si vedono solo festivi e fine settimana.</summary>
    public DateOnly InizioConteggi { get; set; }
}

/// <summary>Registro delle modifiche: chi, quando, su chi, valore prima e dopo, motivo. Solo in aggiunta.</summary>
public class ModificaPresenze
{
    public long IdModifica { get; set; }
    public DateTime QuandoUtc { get; set; }
    public int? IdUtenteAutore { get; set; }
    public int? IdLavoratore { get; set; }
    /// <summary>Es. «timbratura.aggiungi», «timbratura.annulla», «assenza.aggiungi», «assenza.annulla», «lavoratore.modifica».</summary>
    public string Azione { get; set; } = "";
    /// <summary>Giorno interessato, se c'è (colonna vera: l'originale lo cercava nel JSON).</summary>
    public DateOnly? Giorno { get; set; }
    /// <summary>Valore prima e dopo, in JSON.</summary>
    public string? Prima { get; set; }
    public string? Dopo { get; set; }
    public string? Motivo { get; set; }
}
