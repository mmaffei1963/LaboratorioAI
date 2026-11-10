namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>
/// Il passo della check list di ogni domanda, deciso una volta (10/11/2026).
///
/// E' l'unica cosa che dai modelli non si ricava: dove una domanda
/// compare nel documento non dice in quale schermata la si chiede al
/// cliente. La tabella nasce dalla rassegna fatta sui 32 modelli
/// approvati e viaggia con il codice, cosi' in produzione le domande
/// nascono gia' al posto giusto e la rassegna non si rifa'.
///
/// Si applica solo quando la domanda viene creata: se qualcuno sposta un
/// passo dalla pagina, la sua scelta resta. Una domanda che qui non c'e'
/// nasce senza passo e si vede nel filtro «senza passo»: e' voluto, perche'
/// un campo nuovo in un modello nuovo va guardato da una persona, non
/// sistemato da una regola.
///
/// Per rigenerarla: /api/manualistica/voci?tipo=PRIVACY, campi «codice» e
/// «gruppo».
/// </summary>
public static class PassiDecisi
{
    private static readonly IReadOnlyDictionary<string, string> PerTipo_PRIVACY =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // 1. Cliente, sedi, data e revisione
            ["SEDE_TRATTAMENTO_CAP_COMUNE"]                           = "CLIENTE",
            ["SEDE_LEGALE_CAP_COMUNE"]                                = "CLIENTE",
            ["INFORMAZIONI_FORNITE_DA"]                               = "CLIENTE",

            // 2. Struttura e finalita'
            ["DVR_LUOGO"]                                             = "STRUTTURA",
            ["SCHERMO_LUOGO"]                                         = "STRUTTURA",

            // 3. Autorizzati
            ["AUTORIZZATO_QUALIFICA"]                                 = "AUTORIZZATI",
            ["BACKUP_AUTORIZZATO_FUNZIONE"]                           = "AUTORIZZATI",
            ["BACKUP_AUTORIZZATO_QUALIFICA"]                          = "AUTORIZZATI",
            ["CREDENZIALI_AUTORIZZATO_FUNZIONE"]                      = "AUTORIZZATI",
            ["CREDENZIALI_AUTORIZZATO_QUALIFICA"]                     = "AUTORIZZATI",
            ["SOSTITUTO_LR_FUNZIONE"]                                 = "AUTORIZZATI",
            ["SOSTITUTO_LR_QUALIFICA"]                                = "AUTORIZZATI",

            // 4. Responsabili
            ["RESP_TRATTAMENTO_1"]                                    = "RESPONSABILI",
            ["RESP_TRATTAMENTO_2"]                                    = "RESPONSABILI",
            ["RESP_TRATTAMENTO_3"]                                    = "RESPONSABILI",
            ["RESP_TRATTAMENTO_4"]                                    = "RESPONSABILI",
            ["RESP_TRATTAMENTO_5"]                                    = "RESPONSABILI",
            ["RESP_TRATTAMENTO_6"]                                    = "RESPONSABILI",
            ["RESP_TRATTAMENTO_7"]                                    = "RESPONSABILI",
            ["RESP_INTERESSATI_1"]                                    = "RESPONSABILI",
            ["RESP_INTERESSATI_2"]                                    = "RESPONSABILI",
            ["RESP_INTERESSATI_3"]                                    = "RESPONSABILI",
            ["RESP_DATI_1_1"]                                         = "RESPONSABILI",
            ["RESP_DATI_2_1"]                                         = "RESPONSABILI",
            ["RESP_DATI_3_1"]                                         = "RESPONSABILI",
            ["RESP_DATI_1_2"]                                         = "RESPONSABILI",
            ["RESP_DATI_2_2"]                                         = "RESPONSABILI",
            ["RESP_DATI_3_2"]                                         = "RESPONSABILI",
            ["RESP_DATI_2_3"]                                         = "RESPONSABILI",

            // 5. Sistemi, archivi e misure
            ["CRM_FORNITORE"]                                         = "SISTEMI",
            ["CRS_FORNITORE"]                                         = "SISTEMI",
            ["CRO_FORNITORE"]                                         = "SISTEMI",
            ["MISURA_MINACCE_UMANE_VOCE"]                             = "SISTEMI",
            ["MISURE_ANTINCENDIO_VOCE"]                               = "SISTEMI",
            ["MISURE_EVENTI_NATURALI_VOCE"]                           = "SISTEMI",
            ["BACKUP_SERVER_LUOGO"]                                   = "SISTEMI",
            ["BACKUP_DISPOSITIVO"]                                    = "SISTEMI",
            ["BACKUP_DISPOSITIVO_CUSTODIA"]                           = "SISTEMI",
            ["BACKUP_CLOUD_FORNITORE"]                                = "SISTEMI",
            ["ARCHIVIO_CARTACEO_LUOGO_1"]                             = "SISTEMI",
            ["ARCHIVIO_CARTACEO_ACCESSIBILE_A_1"]                     = "SISTEMI",
            ["ARCHIVIO_CARTACEO_ACCESSIBILE_A_2"]                     = "SISTEMI",
            ["ARCHIVIO_CARTACEO_ACCESSIBILE_A_3"]                     = "SISTEMI",
            ["ARCHIVIO_CARTACEO_CATEGORIE_DI_INTERESSATI_1"]          = "SISTEMI",
            ["ARCHIVIO_CARTACEO_CATEGORIE_DI_INTERESSATI_2"]          = "SISTEMI",
            ["ARCHIVIO_CARTACEO_CATEGORIE_DI_INTERESSATI_3"]          = "SISTEMI",
            ["ARCHIVIO_CARTACEO_RIGA_1"]                              = "SISTEMI",
            ["ARCHIVIO_CARTACEO_RIGA_2"]                              = "SISTEMI",
            ["ARCHIVIO_CARTACEO_RIGA_3"]                              = "SISTEMI",
            ["ARCHIVIO_CARTACEO_RIGA_1#2"]                            = "SISTEMI",
            ["ARCHIVIO_CARTACEO_RIGA_2#2"]                            = "SISTEMI",
            ["ARCHIVIO_CARTACEO_RIGA_3#2"]                            = "SISTEMI",
            ["BUSTE_PAGA_RICEZIONE"]                                  = "SISTEMI",
            ["BUSTE_PAGA_CONSERVAZIONE"]                              = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_LUOGO_1"]                          = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_ACCESSIBILE_A_1"]                  = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_ACCESSIBILE_A_2"]                  = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_ACCESSIBILE_A_3"]                  = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_CATEGORIE_DI_INTERESSATI_1"]       = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_CATEGORIE_DI_INTERESSATI_2"]       = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_CATEGORIE_DI_INTERESSATI_3"]       = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_RIGA_1"]                           = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_RIGA_2"]                           = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_RIGA_3"]                           = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_RIGA_1#2"]                         = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_RIGA_2#2"]                         = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_RIGA_3#2"]                         = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_SISTEMA_OPERATIVO_1"]              = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_ANTIVIRUS_1"]                      = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_BACK_UP_1"]                        = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_FREQUENZA_BACK_UP_1"]              = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_LUOGO_DI_CONSERVAZIONE_1"]         = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_GESTIONALI_INSTALLATI_PRODOTTO_1"] = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_RIGA_2#3"]                         = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_MISURE_DI_SICUREZZA_GESTIONALI_1"] = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_MISURE_DI_SICUREZZA_GESTIONALI_2"] = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_MANUTENZIONE_GESTIONALI_1"]        = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_MANUTENZIONE_GESTIONALI_2"]        = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_BACKUP_GESTIONALI_1"]              = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_BACKUP_GESTIONALI_2"]              = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_FREQUENZA_1"]                      = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_FREQUENZA_2"]                      = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_LUOGO_DI_CONSERVAZIONE_1#2"]       = "SISTEMI",
            ["ARCHIVIO_INFORMATICO_LUOGO_DI_CONSERVAZIONE_2"]         = "SISTEMI",
            ["BACKUP_FREQUENZA"]                                      = "SISTEMI"
        };

    /// <summary>Il passo deciso per una domanda, o null se non c'e'.</summary>
    public static string? Per(string codiceTipo, string codiceVoce) =>
        codiceTipo == "PRIVACY" && PerTipo_PRIVACY.TryGetValue(codiceVoce, out var passo) ? passo : null;
}
