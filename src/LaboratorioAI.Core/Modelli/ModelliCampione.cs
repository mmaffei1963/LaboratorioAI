namespace LaboratorioAI.Core.Modelli;

/// <summary>Campione in attesa di validazione, come compare nell'elenco.</summary>
public record CampioneDaValidare
{
    public int IdCampione { get; init; }
    public string NumeroCampione { get; init; } = "";
    public string? RagioneSociale { get; init; }
    public string? NomeInsediamento { get; init; }
    public string? DenominazioneCampione { get; init; }
    public string? Matrice { get; init; }
    public DateTime? DataRicezione { get; init; }
    public int GiorniInLaboratorio { get; init; }
    public int NumeroProve { get; init; }
    public string? Operatori { get; init; }

    /// <summary>Semaforo di anzianità, calcolato sui giorni di giacenza.</summary>
    public LivelloAnzianita Anzianita => GiorniInLaboratorio switch
    {
        <= 5  => LivelloAnzianita.Regolare,
        <= 10 => LivelloAnzianita.Attenzione,
        _     => LivelloAnzianita.Critico
    };
}

public enum LivelloAnzianita { Regolare, Attenzione, Critico }

/// <summary>Dati identificativi e descrittivi di un campione.</summary>
public record DettaglioCampione
{
    public int IdCampione { get; init; }
    public int IdLotto { get; init; }
    public string NumeroCampione { get; init; } = "";
    public string? RagioneSociale { get; init; }
    public string? NomeInsediamento { get; init; }
    public string? DenominazioneCampione { get; init; }
    public string? DenominazioneProdotto { get; init; }
    public string? Matrice { get; init; }
    public string? Controllo { get; init; }
    public string? LuogoPrelievo { get; init; }
    public string? PuntoPrelievo { get; init; }
    public string? VerbalePrelievo { get; init; }
    public DateTime? DataRicezione { get; init; }
    public DateTime? DataPrelievo { get; init; }
    public int? IdCertificato { get; init; }
    public int GiorniInLaboratorio { get; init; }
}

/// <summary>Singola prova eseguita su un campione.</summary>
public record ProvaCampione
{
    public int IdProva { get; init; }

    /// <summary>Identificativo della riga metodo (rigametodo) collegata alla prova.</summary>
    public int IdRigaMetodo { get; init; }

    public string? Parametro { get; init; }
    public string? DenominazioneRiga { get; init; }
    public string? RiferimentoNormativo { get; init; }
    public decimal? ValoreCalcolato { get; init; }
    public bool MaggioreDi { get; init; }
    public bool MinoreDi { get; init; }
    public decimal? LimiteInferiore { get; init; }
    public decimal? LimiteSuperiore { get; init; }
    public string? UnitaMisura { get; init; }
    public bool? VerificaValore { get; init; }
    public string? LaboratorioEsterno { get; init; }

    /// <summary>Numero di accreditamento (ACCREDIA) del laboratorio esterno.</summary>
    public string? NumeroLaboratorioEsterno { get; init; }

    /// <summary>Metodo accreditato presso MARE.A (Metodo.Accreditato).</summary>
    public bool Accreditato { get; init; }

    /// <summary>Metodo accreditato presso il laboratorio esterno che ha eseguito la prova (labEstMetodo.AccrLabEsterno per la coppia metodo + laboratorio).</summary>
    public bool AccreditatoEsterno { get; init; }

    public string? Note { get; init; }

    /// <summary>
    /// Valore formattato per la presentazione, con i prefissi di rivelabilità.
    /// Restituisce «N/D» se il risultato non è ancora disponibile.
    /// </summary>
    public string ValoreFormattato
    {
        get
        {
            if (ValoreCalcolato is null) return "N/D";

            var numero = ValoreCalcolato.Value.ToString("0.####");
            if (MinoreDi)   return $"< {numero}";
            if (MaggioreDi) return $"> {numero}";
            return numero;
        }
    }

    /// <summary>Intervallo di riferimento, se presente.</summary>
    public string? IntervalloFormattato
    {
        get
        {
            if (LimiteInferiore is null && LimiteSuperiore is null) return null;
            var inf = LimiteInferiore?.ToString("0.####") ?? "—";
            var sup = LimiteSuperiore?.ToString("0.####") ?? "—";
            return $"{inf} — {sup}";
        }
    }
}

/// <summary>Campione con l'elenco completo delle sue prove.</summary>
public record CampioneCompleto
{
    public required DettaglioCampione Campione { get; init; }
    public required IReadOnlyList<ProvaCampione> Prove { get; init; }
}
