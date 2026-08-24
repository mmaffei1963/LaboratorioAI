namespace LaboratorioAI.Core.Modelli;

/// <summary>
/// Criteri di selezione delle prove affidate a laboratori esterni.
///
/// Il periodo si applica alla data di analisi, calcolata come data di
/// ricezione più il ritardo previsto per il campione: è la data in cui
/// il campione deve essere consegnato al laboratorio esterno.
/// </summary>
public record CriteriLaboratoriEsterni
{
    public DateOnly DataDa { get; init; }
    public DateOnly DataA { get; init; }

    public int? IdLaboratorioEsterno { get; init; }
    public int? IdMatrice { get; init; }
    public int? IdCliente { get; init; }
    public int? IdInsediamento { get; init; }

    /// <summary>Numero lotto (o "lotto/campione") da filtrare. Accetta "/" o "-".</summary>
    public string? NumeroLotto { get; init; }

    /// <summary>Esclude i campioni per cui è già stato emesso il certificato.</summary>
    public bool SoloNonCertificati { get; init; }

    public int Limite { get; init; } = 1000;
}

/// <summary>Prova affidata a un laboratorio esterno.</summary>
public record ProvaEsterna
{
    public int IdCampione { get; init; }
    public string NumeroCampione { get; init; } = "";
    public string? RagioneSociale { get; init; }
    public string? NomeInsediamento { get; init; }
    public DateTime? DataRicezione { get; init; }
    public DateTime? DataAnalisi { get; init; }
    public DateTime? DataPrelievo { get; init; }
    public string? DenominazioneCampione { get; init; }
    public string? Matrice { get; init; }
    public string? DenominazioneRiga { get; init; }
    public string? RiferimentoNormativo { get; init; }
    public string? LaboratorioEsterno { get; init; }

    /// <summary>Il metodo è accreditato presso il laboratorio esterno (labEstMetodo.AccrLabEsterno).</summary>
    public bool AccreditatoEsterno { get; init; }

    public int? IdCertificato { get; init; }
    /// <summary>Esiste un certificato esterno caricato nel portale per questo campione.</summary>
    public bool EsternoPresente { get; init; }

    // IdCertificato nullo da solo può essere solo un ritardo: è "spedito in originale" solo
    // se esiste già il certificato esterno nel portale ma non il nostro (EsternoPresente).
    public StatoCampione Stato =>
        IdCertificato is not null ? StatoCampione.Certificato
        : EsternoPresente         ? StatoCampione.SpeditoOriginale
        :                           StatoCampione.InCorso;
}

/// <summary>Conteggio delle prove per laboratorio, matrice e metodo.</summary>
public record RiepilogoMetodoEsterno
{
    public string? LaboratorioEsterno { get; init; }
    public string? Matrice { get; init; }
    public string? DenominazioneRiga { get; init; }
    public string? RiferimentoNormativo { get; init; }
    public int NumeroProve { get; init; }
    public int NumeroCertificate { get; init; }
}

/// <summary>Esito completo dell'interrogazione sui laboratori esterni.</summary>
public record EsitoLaboratoriEsterni
{
    public required IReadOnlyList<ProvaEsterna> Prove { get; init; }
    public required IReadOnlyList<RiepilogoMetodoEsterno> Riepilogo { get; init; }
    public bool Troncato { get; init; }

    // I totali sono calcolati sul riepilogo (che copre l'intero risultato,
    // senza il limite dell'elenco a video): così le card sono sempre complete.
    public int TotaleProve      => Riepilogo.Sum(r => r.NumeroProve);
    public int Certificate      => Riepilogo.Sum(r => r.NumeroCertificate);
    public int InCorso          => TotaleProve - Certificate;
    public int LaboratoriDistinti =>
        Riepilogo.Select(r => r.LaboratorioEsterno).Distinct().Count(n => n is not null);
}
