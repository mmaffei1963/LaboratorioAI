namespace LaboratorioAI.Core.Entita;

/// <summary>Stato di un lotto di accettazione nel flusso verso ArcLab (via Palomar).</summary>
public enum StatoAccettazione { Bozza = 0, DaTrasmettere = 1, Trasmesso = 2, Importato = 3 }

/// <summary>
/// Testata di un'accettazione campioni raccolta fuori sede (consulente o cliente).
/// NON scrive su ArcLab: è una copia di staging da cui si genera il tracciato per Palomar.
/// </summary>
public class AccettazioneLotto
{
    public int IdAccettazioneLotto { get; set; }

    /// <summary>Chiave locale stabile (GUID) per idempotenza offline e sincronizzazione.</summary>
    public string CodiceLocale { get; set; } = Guid.NewGuid().ToString("N");

    public StatoAccettazione Stato { get; set; } = StatoAccettazione.Bozza;

    /// <summary>Codice lotto definitivo (formato CODContab-A00001), assegnato una sola volta al salvataggio.</summary>
    public string? CodLotto { get; set; }

    // ── Riferimenti alle anagrafiche ArcLab (per snapshot; nessuna FK reale) ──
    public int? IdClienteArcLab { get; set; }
    public string? Cliente { get; set; }
    public int? IdInsediamentoArcLab { get; set; }
    public string? Insediamento { get; set; }
    public int? IdCommessaArcLab { get; set; }
    public string? Commessa { get; set; }

    /// <summary>Data/ora di ricezione dei campioni in laboratorio.</summary>
    public DateTime DataConferimento { get; set; }
    public int? IdPrelevatore { get; set; }
    /// <summary>Nome del prelevatore interno, oppure "Cliente" / "Terza parte" (IdPrelevatore null in quei casi).</summary>
    public string? Prelevatore { get; set; }
    /// <summary>Vettore: automatico. = prelevatore se interno, altrimenti "Cliente".</summary>
    public string? Vettore { get; set; }
    /// <summary>Verbale di prelievo: obbligatorio se prelevatore interno, vietato se cliente/terza parte.</summary>
    public string? VerbalePrelievo { get; set; }
    public int? IdRicevitore { get; set; }
    public string? Ricevitore { get; set; }
    public string? Note { get; set; }

    // ── Origine e tracciabilità ─────────────────────────────────────
    /// <summary>Chi ha accettato: "consulente" o "cliente".</summary>
    public string? Origine { get; set; }
    public DateTime? DataTrasmissione { get; set; }
    public string? NomeFileTracciato { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public int? IdUtente { get; set; }

    public List<AccettazioneCampione> Campioni { get; set; } = new();
}

/// <summary>Contatore globale (atomico) per il progressivo del CodLotto delle accettazioni fuori sede.</summary>
public class ContatoreAccettazione
{
    public int Id { get; set; }
    /// <summary>Ultimo valore assegnato; l'incremento avviene con UPDATE ... OUTPUT (atomico).</summary>
    public long ProssimoValore { get; set; }
}

/// <summary>Un campione dell'accettazione.</summary>
public class AccettazioneCampione
{
    public int IdAccettazioneCampione { get; set; }
    public int IdAccettazioneLotto { get; set; }
    public AccettazioneLotto? Lotto { get; set; }

    public int Progressivo { get; set; }
    public int? IdMatriceArcLab { get; set; }
    public string? Matrice { get; set; }
    /// <summary>Denominazione del campione.</summary>
    public string? Descrizione { get; set; }
    public string? EtichettaCliente { get; set; }
    public string? SiglaCliente { get; set; }
    public string? Identificativo { get; set; }
    public string? Imballaggio { get; set; }
    /// <summary>Procedura di prelievo (es. "IO 7/02").</summary>
    public string? ProceduraPrelievo { get; set; }
    /// <summary>N. camera Legionella.</summary>
    public string? CameraLegionella { get; set; }
    public DateTime? DataPrevistaConsegna { get; set; }
    /// <summary>Shelf life: giorni di ritardo dell'inizio analisi rispetto alla ricezione.</summary>
    public int? RitardoAnalisiGiorni { get; set; }
    /// <summary>Data e ora del prelievo (obbligatoria).</summary>
    public DateTime? DataPrelievo { get; set; }
    /// <summary>Ora del prelievo derivata da DataPrelievo (per compatibilità RDP).</summary>
    public string? OraPrelievo { get; set; }
    public string? PuntoPrelievo { get; set; }
    /// <summary>Luogo di prelievo (tab prelievo di ArcLab).</summary>
    public string? LuogoPrelievo { get; set; }
    /// <summary>Temperatura di prelievo (°C, numerica, ammette negativi) — obbligatoria.</summary>
    public double? Temperatura { get; set; }
    /// <summary>Temperatura di ricezione: "Ambiente" oppure un intero da -20 a +10.</summary>
    public string? TempRicezione { get; set; }
    /// <summary>Termometro: automatico secondo prelevatore e temp. ricezione (da TermometriRicevimento).</summary>
    public int? IdTermometroArcLab { get; set; }
    public string? Termometro { get; set; }
    /// <summary>Condizioni di trasporto: automatico (surgelato / refrigerato / temp. ambiente).</summary>
    public string? CondizioniTrasporto { get; set; }
    public string? Note { get; set; }

    // ── Laboratorio esterno (tab "laboratorio esterno") ─────────────
    /// <summary>Laboratorio esterno scelto per le prove affidate all'esterno (LabEsterno.IdLabEsterno).</summary>
    public int? IdLabEsternoArcLab { get; set; }
    public string? LabEsterno { get; set; }
    /// <summary>Data di invio al laboratorio esterno.</summary>
    public DateTime? DataInvioLab { get; set; }
    /// <summary>Note per il laboratorio esterno.</summary>
    public string? NoteLabEsterno { get; set; }
    /// <summary>Consegna prevista dal lab esterno, in giorni ("Consegnare entro … gg").</summary>
    public int? ConsegnaPrevistaLabEstGiorni { get; set; }
    /// <summary>Se true, il certificato non è richiesto per questo campione.</summary>
    public bool CertificatoNonRichiesto { get; set; }

    public List<AccettazioneProva> Prove { get; set; } = new();
}

/// <summary>Una prova/analisi richiesta su un campione.</summary>
public class AccettazioneProva
{
    public int IdAccettazioneProva { get; set; }
    public int IdAccettazioneCampione { get; set; }
    public AccettazioneCampione? Campione { get; set; }

    public int? IdControlloArcLab { get; set; }
    public string? Controllo { get; set; }
    public int? IdMetodoArcLab { get; set; }
    /// <summary>Riga metodo ArcLab (rigametodo): è la chiave della prova (prove.IdRigaMetodo).</summary>
    public int? IdRigaMetodoArcLab { get; set; }
    /// <summary>Denominazione della riga metodo (come nel listino ArcLab).</summary>
    public string? Metodo { get; set; }
    public string? Parametro { get; set; }
    public string? LaboratorioEsterno { get; set; }
    public string? Note { get; set; }
}
