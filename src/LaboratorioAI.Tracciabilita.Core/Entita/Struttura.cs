using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;

namespace LaboratorioAI.Tracciabilita.Core.Entita;

/// <summary>
/// Azienda cliente che paga il canone. È il confine di isolamento dei dati:
/// ogni tabella operativa porta <c>IdAzienda</c>.
/// </summary>
public class Azienda : IDellAzienda
{
    public int IdAzienda { get; set; }
    public string RagioneSociale { get; set; } = null!;
    public string? PartitaIva { get; set; }
    public string? Email { get; set; }
    public ProfiloSettore Settore { get; set; } = ProfiloSettore.Generico;
    public StatoServizio Stato { get; set; } = StatoServizio.Attivo;
    public DateTime? DataAttivazione { get; set; }

    /// <summary>Quando il cliente ha chiesto la chiusura: da qui decorre il periodo in sola lettura.</summary>
    public DateTime? DataInizioChiusura { get; set; }

    /// <summary>Dopo questa data i dati possono essere anonimizzati o eliminati (come da contratto).</summary>
    public DateTime? DataFineConservazione { get; set; }

    public int MaxInsediamenti { get; set; } = 1;
    public int MaxUtenti { get; set; } = 6;

    /// <summary>Collegamento logico facoltativo al cliente su ArcLab (solo lettura, mai scritto).</summary>
    public int? IdClienteArcLab { get; set; }

    public string? Note { get; set; }
    public DateTime DataCreazione { get; set; }
}

/// <summary>Scelte di funzionamento dell'azienda (una riga per azienda).</summary>
public class ImpostazioniAzienda : IDellAzienda
{
    public int IdImpostazioni { get; set; }
    public int IdAzienda { get; set; }

    public FormatoLotto FormatoLotto { get; set; } = FormatoLotto.GiornoDellAnno;

    /// <summary>Aggiunge la sigla della sede al lotto (necessario con più insediamenti).</summary>
    public bool IncludiSiglaSedeNelLotto { get; set; }

    /// <summary>Permette più lotti nello stesso giorno (progressivo giornaliero).</summary>
    public bool PiuLottiAlGiorno { get; set; }

    /// <summary>Se vero, nei consumi la quantità è obbligatoria e si attiva il controllo di quadratura.</summary>
    public bool QuantitaConsumiObbligatorie { get; set; }

    /// <summary>Come si registrano i kg consumati: nessuno, a mano per lotto, o calcolati dalla ricetta.</summary>
    public ModoQuantitaConsumi ModoQuantitaConsumi { get; set; } = ModoQuantitaConsumi.SoloLotti;

    public CriterioProposta CriterioProposta { get; set; } = CriterioProposta.ScadenzaPiuVicina;

    /// <summary>Giorni di anticipo per segnalare lotti e certificati in scadenza.</summary>
    public int GiorniPreavvisoScadenza { get; set; } = 7;
}

/// <summary>Stabilimento o sede operativa di un'azienda.</summary>
public class Insediamento : IDellAzienda
{
    public int IdInsediamento { get; set; }
    public int IdAzienda { get; set; }
    public string Nome { get; set; } = null!;

    /// <summary>Sigla breve usata nei codici dei lotti (es. CAT).</summary>
    public string Sigla { get; set; } = null!;

    public string? Indirizzo { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
}

/// <summary>Persona che accede al servizio. Senza azienda = personale MARE.A (gestore).</summary>
public class Utente : IDellAziendaOpzionale
{
    public int IdUtente { get; set; }
    public int? IdAzienda { get; set; }
    public string Email { get; set; } = null!;
    public string Nome { get; set; } = null!;

    /// <summary>Impronta della password (mai la password).</summary>
    public string? ImprontaPassword { get; set; }

    public bool Attivo { get; set; } = true;
    public bool EGestore { get; set; }
    public DateTime? UltimoAccesso { get; set; }

    /// <summary>Password sbagliate di fila: si azzera con un accesso riuscito.</summary>
    public int TentativiFalliti { get; set; }

    /// <summary>Fino a quando l'accesso è sospeso dopo troppi tentativi falliti (UTC).</summary>
    public DateTime? BloccatoFinoUtc { get; set; }

    /// <summary>Impronta (SHA-256) del link di invito o di reimpostazione. Il link in chiaro non si conserva.</summary>
    public string? ImprontaInvito { get; set; }

    /// <summary>Scadenza del link di invito (UTC). Dopo l'uso o la scadenza il link non vale più.</summary>
    public DateTime? ScadenzaInvitoUtc { get; set; }

    public DateTime DataCreazione { get; set; }
}

/// <summary>Ruolo di un utente su un insediamento (o su tutti, se <see cref="IdInsediamento"/> è vuoto).</summary>
public class UtenteInsediamento : IDellAzienda
{
    public int IdUtenteInsediamento { get; set; }
    public int IdAzienda { get; set; }
    public int IdUtente { get; set; }
    public int? IdInsediamento { get; set; }
    public RuoloAzienda Ruolo { get; set; }
}

/// <summary>Chi ha fatto cosa e quando. Si scrive e basta.</summary>
public class VoceAudit : IDellAziendaOpzionale, IAppendOnly
{
    public long IdAudit { get; set; }
    public int? IdAzienda { get; set; }
    public int? IdInsediamento { get; set; }
    public int? IdUtente { get; set; }
    public string Azione { get; set; } = null!;
    public string Entita { get; set; } = null!;
    public string? IdEntita { get; set; }
    public string? Prima { get; set; }
    public string? Dopo { get; set; }
    public string? IndirizzoIp { get; set; }
    public DateTime DataOra { get; set; }
}

/// <summary>Richiesta di scarico di tutti i dati dell'azienda (ZIP con CSV, JSON e allegati).</summary>
public class Esportazione : IDellAzienda
{
    public int IdEsportazione { get; set; }
    public int IdAzienda { get; set; }
    public int? IdUtente { get; set; }
    public StatoEsportazione Stato { get; set; } = StatoEsportazione.Richiesta;
    public DateTime DataRichiesta { get; set; }
    public DateTime? DataPronta { get; set; }
    public DateTime? Scadenza { get; set; }
    public string? PercorsoFile { get; set; }
    public string? ImprontaSha256 { get; set; }
    public string? Errore { get; set; }
}
