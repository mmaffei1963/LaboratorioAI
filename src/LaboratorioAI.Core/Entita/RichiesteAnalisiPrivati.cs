namespace LaboratorioAI.Core.Entita;

// Richieste di analisi dei clienti privati (05/11/2026): il modulo «Richiesta di analisi e accettazione delle
// condizioni – Clienti privati» (Mod. Com. 05B; le Condizioni sono il Mod. Com. 05A, consegnato a parte) compilato e firmato al banco alla consegna del campione.
// Le anagrafiche dei privati non sono in ArcLab: si scrivono qui. Da ArcLab si leggono (sola lettura) matrici,
// contenitori (Imballaggio), controlli e prezzi delle commesse del cliente generico «Spettabile cliente» (MAF2).

/// <summary>Quando paga il cliente privato.</summary>
public enum PagamentoPrivato
{
    /// <summary>Anticipato, alla consegna del Campione.</summary>
    AllaConsegnaCampione = 1,
    /// <summary>Alla consegna del Rapporto di Prova.</summary>
    AllaConsegnaRapporto = 2
}

public class RichiestaAnalisiPrivato
{
    public int IdRichiestaAnalisiPrivato { get; set; }
    public int Numero { get; set; }
    public int Anno { get; set; }

    // 1. Dati del cliente
    public string NomeCognome { get; set; } = "";
    public string? CodiceFiscale { get; set; }
    public string? Indirizzo { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public bool RitiroInLaboratorio { get; set; }
    public PagamentoPrivato Pagamento { get; set; } = PagamentoPrivato.AllaConsegnaCampione;

    // 2. Campione consegnato (il prelievo è sempre eseguito dal cliente)
    public int? IdMatriceArcLab { get; set; }
    public string? DescrizioneCampione { get; set; }
    public string? PuntoPrelievo { get; set; }
    /// <summary>Data e ora del prelievo dichiarate dal cliente (UTC).</summary>
    public DateTime? DataOraPrelievo { get; set; }
    public int? IdImballaggioArcLab { get; set; }
    public string? Contenitore { get; set; }
    /// <summary>true = fornito dal Laboratorio, false = del Cliente, null = non indicato.</summary>
    public bool? ContenitoreDelLaboratorio { get; set; }
    /// <summary>Temperatura di ricevimento in °C (a cura del Laboratorio).</summary>
    public decimal? TemperaturaRicevimento { get; set; }

    // 3. Prove richieste e prezzo
    /// <summary>Tempi di consegna in giorni lavorativi (proposti dal programma, modificabili).</summary>
    public int TempiConsegnaGiorni { get; set; }
    /// <summary>Giudizio di conformità e pareri richiesti (predefinito: no).</summary>
    public bool GiudizioRichiesto { get; set; }
    /// <summary>06/11/2026: comunicazione anticipata dei fuori limite richiesta (predefinito: no).</summary>
    public bool ComunicazioneFuoriLimite { get; set; }
    /// <summary>06/11/2026: consenso facoltativo alle comunicazioni commerciali (null = lo sceglie il cliente sul modulo).</summary>
    public bool? ConsensoComunicazioni { get; set; }
    public string? NormaGiudizio { get; set; }
    /// <summary>Supplemento per il giudizio di conformità, IVA inclusa.</summary>
    public decimal SupplementoGiudizio { get; set; }
    public decimal AliquotaIva { get; set; } = 22m;
    /// <summary>Totale senza IVA: prove (prezzi delle commesse ArcLab) più l'eventuale supplemento scorporato.</summary>
    public decimal TotaleImponibile { get; set; }
    /// <summary>Totale da pagare, IVA inclusa, compreso l'eventuale supplemento per il giudizio.</summary>
    public decimal TotaleIvaInclusa { get; set; }

    public string? Note { get; set; }

    public int? IdUtenteCreatore { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public List<RigaRichiestaAnalisiPrivato> Righe { get; set; } = new();
}

/// <summary>Un controllo richiesto, con prezzo e metodi copiati da ArcLab al momento della richiesta.</summary>
public class RigaRichiestaAnalisiPrivato
{
    public int IdRigaRichiestaAnalisiPrivato { get; set; }
    public int IdRichiestaAnalisiPrivato { get; set; }
    public RichiestaAnalisiPrivato? Richiesta { get; set; }

    public int Progressivo { get; set; }
    public int? IdControlloArcLab { get; set; }
    public int? IdCommessaArcLab { get; set; }
    public string Descrizione { get; set; } = "";
    /// <summary>Prezzo della commessa, senza IVA.</summary>
    public decimal PrezzoImponibile { get; set; }
    public decimal PrezzoIvaInclusa { get; set; }
    /// <summary>Giorni lavorativi proposti per questo controllo (10, oppure 20 per Legionella e prove esterne).</summary>
    public int TempiGiorni { get; set; }
    /// <summary>Metodi del controllo (JSON di <see cref="MetodoRichiestaPrivato"/>), copiati da ArcLab.</summary>
    public string? MetodiJson { get; set; }
}

/// <summary>Metodo di un controllo richiesto, come stampato nel modulo.</summary>
public record MetodoRichiestaPrivato(
    string Denominazione, string? Metodo, bool Accreditato,
    int? IdLabEsterno, string? LabEsterno, string? LabEsternoFrase);
