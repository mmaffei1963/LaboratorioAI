namespace LaboratorioAI.Core.Modelli;

/// <summary>Gruppo omogeneo di strumento, per raggruppare lo scadenziario.</summary>
public enum GruppoStrumento
{
    Termometri,
    IncubatoriTermostati,
    FrigoCongelatori,
    Altro
}

/// <summary>Classifica uno strumento in un gruppo omogeneo dalle parole chiave del nome.</summary>
public static class ClassificatoreStrumento
{
    public static GruppoStrumento Classifica(string? nome)
    {
        var n = (nome ?? "").ToLowerInvariant();
        bool Ha(params string[] k) => k.Any(x => n.Contains(x));

        // Frigo/congelatori (anche "frigotermostato" → prevale frigo)
        if (Ha("congelat", "frigo", "freezer")) return GruppoStrumento.FrigoCongelatori;
        // Incubatori / termostati / stufe / celle
        if (Ha("incubat", "termostat", "stufa", "cella", "camera", "bagno")) return GruppoStrumento.IncubatoriTermostati;
        // Termometri / sonde / registratori
        if (Ha("termometr", "sonda", "econorma", "registrator", "datalogg", "data logg")) return GruppoStrumento.Termometri;
        return GruppoStrumento.Altro;
    }

    public static string Etichetta(GruppoStrumento g) => g switch
    {
        GruppoStrumento.Termometri => "Termometri",
        GruppoStrumento.IncubatoriTermostati => "Incubatori / Termostati / Stufe",
        GruppoStrumento.FrigoCongelatori => "Frigoriferi / Congelatori",
        _ => "Altro"
    };
}

/// <summary>Riga dello scadenziario delle tarature per uno strumento interno (da ArcLab).</summary>
public record VoceScadenziarioInterno
{
    public int IdApparecchio { get; init; }
    public int? IdStrumento { get; init; }
    public string Strumento { get; init; } = "";
    public string? Attivita { get; init; }
    public string? NomePiano { get; init; }
    public DateTime? UltimaTaratura { get; init; }
    public DateTime? Scadenza { get; init; }
    public int? FreqGiorni { get; init; }
    /// <summary>Giorni alla scadenza (negativo = scaduta); null se mai eseguita.</summary>
    public int? GiorniAllaScadenza => Scadenza is DateTime s ? (int)(s.Date - DateTime.Today).TotalDays : null;
    public GruppoStrumento Gruppo { get; init; }
}

/// <summary>Dati di una riga per la generazione dell'etichetta termometro.</summary>
public record RigaEtichetta
{
    /// <summary>Testo della prima riga dopo "Cod.Termometro:".</summary>
    public string Codice { get; init; } = "";
    /// <summary>Etichetta della seconda riga: "N°Rapporto taratura:" (esterni) o nome strumento (interni).</summary>
    public string RigaRapportoEtichetta { get; init; } = "N°Rapporto taratura:";
    /// <summary>Valore della seconda riga (n° rapporto per esterni, vuoto/IdApparecchio per interni).</summary>
    public string RigaRapportoValore { get; init; } = "";
    public DateTime? DataTaratura { get; init; }
    public DateTime? Scadenza { get; init; }
    /// <summary>Anno che determina il colore di sfondo (di norma l'anno della taratura).</summary>
    public int AnnoColore { get; init; }
}
