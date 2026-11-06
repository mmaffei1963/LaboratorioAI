namespace LaboratorioAI.Core.Modelli;

/// <summary>Insediamento di un cliente ArcLab con recapiti, per il rapportino.</summary>
public class InsediamentoRapportino
{
    public int IdInsediamento { get; init; }
    public string Nome { get; init; } = "";
    public string? Indirizzo { get; init; }
    public string? Email { get; init; }
    // Parti dell'indirizzo (per la delega: via/CAP e comune separati)
    public string? Via { get; init; }
    public string? Cap { get; init; }
    public string? Citta { get; init; }
    public string? Provincia { get; init; }
}

/// <summary>Commessa di consulenza ArcLab (IdTipoCommessa = 5) di un cliente, attiva o no.</summary>
public class CommessaConsulenza
{
    public int IdCommessa { get; init; }
    public int IdInsediamento { get; init; }
    public string? NomeInsediamento { get; init; }
    public string? DescrIncarico { get; init; }
    public decimal? PrezzoControllo { get; init; }
    public DateTime? DataCommessa { get; init; }
    public DateTime? DataScadenzaInc { get; init; }
    public string? NContratto { get; init; }
    /// <summary>Tipi_Incarico.ID_Tipo_Incarico (collegato sul testo di DescrIncarico, come la query Access).</summary>
    public int? IdTipoIncarico { get; init; }
    /// <summary>Attiva = DataScadenzaInc non impostata.</summary>
    public bool Attiva => DataScadenzaInc is null;
}

/// <summary>Persona dell'organico ArcLab in servizio (FineProf non impostata).</summary>
public class ConsulenteArcLab
{
    public int IdOrganico { get; init; }
    public string? CognomeOrg { get; init; }
    public string? NomeOrg { get; init; }
    /// <summary>Organico.commessa: codice commessa AZW della persona.</summary>
    public string? Commessa { get; init; }
    public string Nome => $"{CognomeOrg} {NomeOrg}".Trim();
}

/// <summary>Tipi_Incarico di ArcLab.</summary>
public class TipoIncaricoArcLab
{
    public int IdTipoIncarico { get; init; }
    public int? IdTipoCommessa { get; init; }
    public string Descrizione { get; init; } = "";
}

/// <summary>ContattoCliente di ArcLab (referente presso un insediamento).</summary>
public class ContattoArcLab
{
    public int IdContatto { get; init; }
    public string? Nome { get; init; }
    public string? Cognome { get; init; }
    public string? Mansione { get; init; }
    public string? Telefono { get; init; }
    public string? Cellulare { get; init; }
    public string? Email { get; init; }
    public string NomeCompleto => $"{Nome} {Cognome}".Trim();
}

/// <summary>Dati anagrafici del cliente in ArcLab (tabella Clienti).</summary>
public class AnagraficaClienteArcLab
{
    public int IdCliente { get; init; }
    public string? CodiceCliente { get; init; }
    public string? RagioneSociale { get; init; }
    public string? PartitaIva { get; init; }
    public string? CodiceFiscale { get; init; }
    public string? Indirizzo { get; init; }
    public string? Cap { get; init; }
    public string? Citta { get; init; }
    public string? Provincia { get; init; }
    public string? Email { get; init; }
    /// <summary>Codici cliente AZW usati nelle commesse ArcLab del cliente (Cod_Cliente), dal più recente.</summary>
    public List<string> CodiciCommesse { get; set; } = new();
}

