namespace LaboratorioAI.Core.Enumerazioni;

/// <summary>Contesto in cui è stata eseguita l'analisi AI.</summary>
public enum ModalitaAnalisi
{
    /// <summary>Campione non ancora certificato, a supporto della validazione.</summary>
    Validazione = 1,

    /// <summary>Certificato già emesso, verifica a posteriori.</summary>
    Certificato = 2
}

/// <summary>Giudizio complessivo espresso dall'analisi AI.</summary>
public enum GiudizioAnalisi
{
    /// <summary>Non è stato possibile dedurre un giudizio dal testo prodotto.</summary>
    NonDeterminato = 0,
    Conforme       = 1,
    Borderline     = 2,
    NonConforme    = 3
}

/// <summary>Esito di un'operazione registrata nell'audit trail.</summary>
public enum EsitoOperazione
{
    Successo = 1,
    Errore   = 2,
    Negato   = 3
}

/// <summary>Area funzionale dell'applicazione.</summary>
public enum AreaFunzionale
{
    Validazione       = 1,
    Consultazione     = 2,
    Metodi            = 3,
    Commesse          = 4,
    LaboratoriEsterni = 5,
    Dvr               = 6,
    Portale           = 7,
    Amministrazione   = 8,
    Piscine           = 9,
    Prefatturato      = 10,
    Nutrizionale      = 11,
    Taratura          = 12,
    Incubatori        = 13,
    Bilance           = 14,
    TempLog           = 15,
    Scadenziario      = 16,
    Accettazione      = 17,
    SoloEsterni       = 18,
    Controlli         = 19,
    Mappatura         = 20,
    RicercaErrori     = 21,
    CertificatiEsterni = 22,
    Preventivi        = 23
}

/// <summary>Stato di un DVR piscina salvato nel database.</summary>
public enum StatoDvrPiscina
{
    /// <summary>Bozza modificabile, non ancora definitiva.</summary>
    Bozza = 1,

    /// <summary>Documento definitivo.</summary>
    Definitivo = 2
}
