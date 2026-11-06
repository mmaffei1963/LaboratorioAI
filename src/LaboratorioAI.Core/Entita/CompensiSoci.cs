namespace LaboratorioAI.Core.Entita;

// Compensi soci (21/10/2026), schema «soci». Definizione: claude/definizione-compensi-soci.md
// Riservato ai soci: si vede solo con il permesso «compensisoci.consulta» E un socio attivo collegato all'utente.
// Nessuna cancellazione fisica: le righe si disattivano (Attivo = false).

/// <summary>Socio di MARE.A, collegato al suo utente di LaboratorioAI e ai suoi codici in AZW.</summary>
public class Socio
{
    public int IdSocio { get; set; }
    public string Nome { get; set; } = "";
    /// <summary>E-mail dell'utente LaboratorioAI (Entra ID): serve a riconoscere il socio collegato.</summary>
    public string Email { get; set; } = "";
    /// <summary>Voce di costo AZW dei suoi compensi (MAR, MIR, STE, GIU, DAN): «compensi percepiti».</summary>
    public string VoceCompensiAzw { get; set; } = "";
    /// <summary>Commessa AZW delle sue fatture RSPP (voce RSP): MARCO, STEFAN. Vuota se non fa l'RSPP.</summary>
    public string? CommessaRsppAzw { get; set; }
    /// <summary>22/10/2026: nome in Operatori_commerciale di AZW per le trasferte; vuoto = uguale al nome del socio.</summary>
    public string? OperatoreTrasferteAzw { get; set; }
    /// <summary>Solo chi è «gestore» (Mirko Maffei) modifica parametri, dati dei soci, premi e chiusure.</summary>
    public bool Gestore { get; set; }
    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
}

/// <summary>Regole di un anno di competenza (es. 2026 = compensi del 2026, calcolati nel 2027).</summary>
public class AnnoCompensi
{
    public int IdAnnoCompensi { get; set; }
    public int Anno { get; set; }
    /// <summary>Compenso base annuo a tempo pieno.</summary>
    public decimal BaseTempoPieno { get; set; }
    /// <summary>Ore settimanali del tempo pieno (40 = 8 ore × 5 giorni).</summary>
    public decimal OreTempoPieno { get; set; } = 40m;
    /// <summary>Compenso annuo dell'amministratore (diviso per mesi tra chi lo è stato).</summary>
    public decimal CompensoAmministratore { get; set; }
    public decimal CompensoResponsabileArea { get; set; }
    /// <summary>Assicurazione per l'uso dell'auto propria, al 100% di uso.</summary>
    public decimal AssicurazioneAuto { get; set; }
    public decimal Premio { get; set; }
    /// <summary>Quota del fatturato RSPP del socio, in percentuale (50 = 50%).</summary>
    public decimal PercentualeRspp { get; set; }
    /// <summary>Indennità di direzione del laboratorio, in percentuale sulla base del laboratorio (5 = 5%).</summary>
    public decimal PercentualeIndennitaLaboratorio { get; set; }
    /// <summary>Base del laboratorio scritta a mano al posto di quella calcolata da AZW (nota obbligatoria).</summary>
    public decimal? BaseLaboratorioManuale { get; set; }
    public string? NotaBaseLaboratorio { get; set; }
    public bool Chiuso { get; set; }
    public DateTime? DataChiusura { get; set; }
    public string? ChiusoDa { get; set; }
    /// <summary>
    /// 22/10/2026: se vero (predefinito) i rimborsi delle trasferte con mezzo privato si leggono da AZW. Falso per un anno
    /// caricato dal vecchio Excel, dove le trasferte sono già nel totale dei rimborsi.
    /// </summary>
    public bool TrasferteDaAzw { get; set; } = true;
    /// <summary>Alla chiusura: base del laboratorio calcolata da AZW in quel momento (le cifre AZW non cambiano più).</summary>
    public decimal? BaseLaboratorioAzwChiusura { get; set; }
    public string? Note { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
}

/// <summary>Percentuale con cui una voce di ricavo AZW entra nella base del laboratorio, per anno.</summary>
public class PercentualeVoceLaboratorio
{
    public int IdPercentualeVoceLaboratorio { get; set; }
    public int Anno { get; set; }
    public string Voce { get; set; } = "";
    /// <summary>100 = tutta, 50 = metà.</summary>
    public decimal Percentuale { get; set; }
    public bool Attivo { get; set; } = true;
}

/// <summary>Dati di un socio per un anno (ore, incarichi, uso auto, premio) e, alla chiusura, le cifre AZW usate.</summary>
public class CompensoSocioAnno
{
    public int IdCompensoSocioAnno { get; set; }
    public int Anno { get; set; }
    public int IdSocio { get; set; }
    public Socio? Socio { get; set; }
    /// <summary>Ore settimanali: la base è base tempo pieno × ore / ore del tempo pieno (34 ore = 85%).</summary>
    public decimal OreSettimanali { get; set; } = 40m;
    public int MesiAmministratore { get; set; }
    public bool ResponsabileArea { get; set; }
    /// <summary>Percentuale di uso dell'auto propria (0 = usa sempre l'auto aziendale).</summary>
    public decimal PercentualeAutoPropria { get; set; } = 100m;
    public bool QuotaRspp { get; set; }
    /// <summary>Riceve l'indennità di direzione del laboratorio (meno i premi dell'area laboratorio).</summary>
    public bool IndennitaLaboratorio { get; set; }
    /// <summary>Premio del socio se diverso da quello dell'anno.</summary>
    public decimal? Premio { get; set; }
    /// <summary>Recupero dell'anno prima scritto a mano: solo per il primo anno (dopo è la differenza dell'anno prima).</summary>
    public decimal? RecuperoManuale { get; set; }
    /// <summary>Correzione dei compensi percepiti letti da AZW (+/−), con nota obbligatoria.</summary>
    public decimal? RettificaPercepito { get; set; }
    public string? NotaRettificaPercepito { get; set; }
    public string? Note { get; set; }
    // Alla chiusura dell'anno: cifre AZW fissate.
    public decimal? RsppAzwChiusura { get; set; }
    public decimal? PercepitoAzwChiusura { get; set; }
    /// <summary>
    /// 22/10/2026: ripartizione del compenso sulle aree per le analisi: quota fissa all'amministrazione (es. 16.000 €) e il
    /// resto in percentuale («04=100», «05=50;FOR=50»). Vuoti = valori di partenza per socio.
    /// </summary>
    public decimal? QuotaFissaAmministrazione { get; set; }
    public string? RipartizioneAree { get; set; }
    /// <summary>22/10/2026: dove va la differenza ancora da pagare («06=100»); vuoto = valore di partenza (Vaccarini → Igiene) o come sopra.</summary>
    public string? RipartizioneDifferenza { get; set; }
    /// <summary>22/10/2026: rimborsi trasferte da AZW fissati alla chiusura.</summary>
    public decimal? TrasferteAzwChiusura { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
}

/// <summary>Premio dell'area laboratorio (dipendenti o soci): il totale si toglie dall'indennità di direzione del laboratorio.</summary>
public class PremioAreaLaboratorio
{
    public int IdPremioAreaLaboratorio { get; set; }
    public int Anno { get; set; }
    public string Nome { get; set; } = "";
    public decimal Importo { get; set; }
    /// <summary>Se il premio va a un socio, finisce nella sua colonna «indennità laboratorio».</summary>
    public int? IdSocio { get; set; }
    public string? Note { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
}

/// <summary>Categoria dei rimborsi spese, con eventuale maggiorazione (es. acquisti esteri senza fattura +30%).</summary>
public class CategoriaRimborso
{
    public int IdCategoriaRimborso { get; set; }
    public string Nome { get; set; } = "";
    /// <summary>Maggiorazione in percentuale sull'importo (30 = +30%).</summary>
    public decimal Maggiorazione { get; set; }
    /// <summary>Se vero si chiedono i km percorsi (trasferte).</summary>
    public bool ConKm { get; set; }
    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;
}

/// <summary>Rimborso spese di un socio. La maggiorazione è copiata dalla categoria al momento dell'inserimento.</summary>
public class RimborsoSpesa
{
    public int IdRimborsoSpesa { get; set; }
    public int IdSocio { get; set; }
    public Socio? Socio { get; set; }
    /// <summary>Data della spesa: l'anno decide in quale conguaglio entra.</summary>
    public DateTime Data { get; set; }
    public int IdCategoriaRimborso { get; set; }
    public CategoriaRimborso? Categoria { get; set; }
    public string Descrizione { get; set; } = "";
    public decimal Importo { get; set; }
    public decimal Maggiorazione { get; set; }
    public decimal? Km { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public string? InseritoDa { get; set; }
    public RicevutaRimborso? Ricevuta { get; set; }
}

/// <summary>Ricevuta (PDF o foto) di un rimborso, facoltativa.</summary>
public class RicevutaRimborso
{
    public int IdRicevutaRimborso { get; set; }
    public int IdRimborsoSpesa { get; set; }
    public string NomeFile { get; set; } = "";
    public string TipoContenuto { get; set; } = "";
    public int Dimensione { get; set; }
    public byte[] Contenuto { get; set; } = Array.Empty<byte>();
    public DateTime DataCaricamento { get; set; }
}

/// <summary>
/// 22/10/2026: trasferta registrata in AZW (tabella Viaggi di AZW_07_Dati, con MEZZI e Operatori_commerciale).
/// Copia di lavoro riscritta a ogni importazione AZW (sola lettura su AZW). Rimborso = km × costo al km + autostrada +
/// vitto e alloggio + costi diversi; contano solo le trasferte con mezzo privato (TipoMezzo = 2), per data della trasferta.
/// </summary>
public class TrasfertaAzw
{
    public const int TipoMezzoPrivato = 2;

    public int IdTrasfertaAzw { get; set; }
    public int IdViaggioAzw { get; set; }
    public int? IdOperatoreAzw { get; set; }
    public string? Operatore { get; set; }
    public DateTime Data { get; set; }
    public decimal Km { get; set; }
    public decimal CostoKm { get; set; }
    public decimal Autostrada { get; set; }
    public decimal VittoAlloggio { get; set; }
    public decimal CostiDiversi { get; set; }
    public int? IdMezzoAzw { get; set; }
    public string? Mezzo { get; set; }
    public int? TipoMezzo { get; set; }
    public string? Note { get; set; }

    public bool MezzoPrivato => TipoMezzo == TipoMezzoPrivato;
    public decimal CostoChilometrico => Math.Round(Km * CostoKm, 2, MidpointRounding.AwayFromZero);
    public decimal Rimborso => CostoChilometrico + Autostrada + VittoAlloggio + CostiDiversi;
}
