namespace LaboratorioAI.Core.Modelli;

/// <summary>Dati per generare il Rapporto di Taratura di una bilancia (Modulo 45, EURAMET cg-18).</summary>
public record DatiRapportoBilancia
{
    public string NumeroRapporto { get; init; } = "";
    public DateOnly DataTaratura { get; init; }
    public DateOnly? DataScadenza { get; init; }
    public string? RiferimentoIstruzione { get; init; }
    public string RedattoDa { get; init; } = "";
    public string? Committente { get; init; }
    /// <summary>Insediamento del cliente (03/11/2026): stampato in testata solo se indicato.</summary>
    public string? Insediamento { get; init; }

    public string BilanciaIdentificazione { get; init; } = "";
    public string? BilanciaModello { get; init; }

    public double Portata { get; init; }
    public double UnitaFormato { get; init; }
    public double CoefficienteSensibilitaTermica { get; init; }
    public double TemperaturaTaratura { get; init; }
    public double TemperaturaUtilizzo { get; init; }

    public double CampoTaratura { get; init; }
    public double TempoStabilizzazione { get; init; }
    public double TempoIntervalloLettura { get; init; }
    public double MaxVariazioneTemperatura { get; init; }
    public string? Tipo { get; init; }
    public string? Sda { get; init; }
    public string? Operatore { get; init; }

    public string? PesieraIdentificazione { get; init; }
    /// <summary>Masse della pesiera utilizzata (per la tabella nel report).</summary>
    public IReadOnlyList<RigaPesieraRapporto> Pesiera { get; init; } = new List<RigaPesieraRapporto>();

    /// <summary>Dati grezzi della prova di eccentricità (per il dettaglio nel report).</summary>
    public ProvaEccentricita? Eccentricita { get; init; }
    /// <summary>Letture della prova di ripetibilità (per il dettaglio nel report).</summary>
    public IReadOnlyList<double> Ripetibilita { get; init; } = new List<double>();
    /// <summary>Termometro/strumento di misura temperatura usato.</summary>
    public string? TermometroUtilizzato { get; init; }

    public required RisultatoBilancia Risultato { get; init; }
}

/// <summary>Riga della pesiera utilizzata da mostrare nel rapporto.</summary>
public record RigaPesieraRapporto
{
    public double ValoreNominale { get; init; }
    public double ValoreConvenzionale { get; init; }
    public double IncertezzaEstesa { get; init; }   // U (k=2) [mg]
    public double IncertezzaTipo { get; init; }      // u [mg]
    public string? CertificatoLat { get; init; }
    public DateTime? DataCertificato { get; init; }
    public DateTime? DataScadenza { get; init; }
    public int? CentroLat { get; init; }
}
