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
    Preventivi        = 23,
    MonitorTemperature = 24,
    Statistiche       = 25,
    Provvigioni       = 26,
    Consulenze        = 27,
    Clienti           = 28,
    /// <summary>Condizioni Generali di Fornitura e firme dei clienti (09/10/2026).</summary>
    Contratti         = 29,
    /// <summary>Cruscotto riservato a soci e direzione (13/10/2026).</summary>
    Direzione         = 30,
    /// <summary>Limiti normativi per la validazione dei Rapporti di Prova (17/10/2026).</summary>
    LimitiNormativi   = 31,
    /// <summary>Manualistica per i clienti: privacy e, a seguire, piscine, legionellosi, HACCP (01/11/2026).</summary>
    Manualistica      = 32
}

/// <summary>Stato di un DVR piscina salvato nel database.</summary>
public enum StatoDvrPiscina
{
    /// <summary>Bozza modificabile, non ancora definitiva.</summary>
    Bozza = 1,

    /// <summary>Documento definitivo.</summary>
    Definitivo = 2
}

/// <summary>
/// Classificazione di un metodo (LabEstMetodo di ArcLab) rispetto all'elenco prove ACCREDIA
/// confermato per il laboratorio esterno. Vedi <see cref="Entita.VerificaMetodoAccreditamento"/>.
/// </summary>
public enum StatoVerificaMetodo
{
    /// <summary>Non ancora classificato, o classificato su una revisione dell'elenco ormai superata.</summary>
    DaVerificare = 0,

    /// <summary>Un operatore ha verificato sul PDF che il metodo è nello scope di accreditamento.</summary>
    Accreditato = 1,

    /// <summary>Un operatore ha verificato sul PDF che il metodo NON è nello scope di accreditamento.</summary>
    NonAccreditato = 2
}

/// <summary>
/// Stato dell'accreditamento di un laboratorio esterno secondo la banca dati ACCREDIA dei laboratori di prova
/// (08/10/2026): letto ogni giorno dall'Agente ACCREDIA con due ricerche (tutti i laboratori; solo quelli con
/// «Sospensione Attiva: Sì»). Vedi <see cref="Entita.AccreditamentoLabEsterno.StatoAccredia"/>.
/// </summary>
public enum StatoAccreditamentoAccredia
{
    /// <summary>Presente nell'elenco dei laboratori accreditati, senza sospensione.</summary>
    Accreditato = 1,

    /// <summary>Presente fra i laboratori con sospensione temporanea attiva (asterisco sul numero).</summary>
    Sospeso = 2,

    /// <summary>Numero e sede non presenti nell'elenco: accreditamento revocato, ritirato, rinunciato o scaduto.</summary>
    NonTrovato = 3
}
