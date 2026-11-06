using LaboratorioAI.Core.Provvigioni;

namespace LaboratorioAI.Core.Entita;

// Schema `provv`: provvigioni dei commerciali. Le tabelle Azw* sono copie di lavoro dei dati contabili,
// riscritte a ogni importazione (AZW resta in sola lettura); Trimestri e Movimenti sono il risultato
// congelato alla chiusura e non si modificano più.

/// <summary>Commerciale (Ludo, Carloni). Il collegamento ai codici venditore di AZW è in <see cref="CodiceVenditoreCommerciale"/>.</summary>
public class Commerciale
{
    public int IdCommerciale { get; set; }
    public string Nome { get; set; } = null!;

    /// <summary>Utente applicativo che vede solo i propri dati (permesso <c>provvigioni.proprie</c>).</summary>
    public int? IdUtente { get; set; }

    /// <summary>
    /// Codice della persona in ArcLab (<c>mansione.IdMansione</c>) usato in <c>commessa.CodCommessa</c>, il «rif. del cliente»
    /// della commessa. Serve a riconoscere le commesse di consulenza e formazione del commerciale.
    /// </summary>
    public int? IdMansioneArcLab { get; set; }

    public bool Attivo { get; set; } = true;
}

/// <summary>Codice venditore (CODVET di AZW) assegnato a un commerciale, con validità.</summary>
public class CodiceVenditoreCommerciale
{
    public int IdCodiceVenditore { get; set; }
    public int IdCommerciale { get; set; }
    public string CodiceVenditore { get; set; } = null!;
    public DateTime ValidoDal { get; set; }
    public DateTime? ValidoAl { get; set; }
}

/// <summary>Fattura di laboratorio importata da AZW (solo la quota di perimetro: centro 04 e voci di analisi).</summary>
public class AzwFattura
{
    public int IdAzwFattura { get; set; }

    /// <summary>CODCF|anno documento|numero documento.</summary>
    public string Chiave { get; set; } = null!;
    public string CodiceCliente { get; set; } = null!;
    public string? RagioneSociale { get; set; }
    public string? CodiceVenditore { get; set; }
    public DateTime DataFattura { get; set; }
    public decimal NettoPerimetro { get; set; }
    public decimal NettoTotale { get; set; }

    /// <summary>Totale IVA inclusa dal partitario (se assente: netto totale × 1,22).</summary>
    public decimal Lordo { get; set; }
    public bool LordoStimato { get; set; }
}

public class AzwIncasso
{
    public int IdAzwIncasso { get; set; }
    public string ChiaveFattura { get; set; } = null!;
    public DateTime DataIncasso { get; set; }
    public decimal ImportoLordo { get; set; }
}

public class AzwNotaCredito
{
    public int IdAzwNotaCredito { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public string Riferimento { get; set; } = null!;
    public DateTime Data { get; set; }
    public decimal NettoPerimetro { get; set; }

    /// <summary>Imponibile totale della nota (tutti i servizi): serve ai commerciali che non lavorano solo sul laboratorio.</summary>
    public decimal NettoTotale { get; set; }
}

/// <summary>Esito di una importazione da AZW (diagnostica e controllo dell'aggiornamento del file).</summary>
public class ImportazioneAzw
{
    public int IdImportazione { get; set; }
    public DateTime DataOra { get; set; }
    public string PercorsoFile { get; set; } = null!;
    public DateTime? DataModificaFile { get; set; }
    public bool Riuscita { get; set; }
    public int Fatture { get; set; }
    public int Incassi { get; set; }
    public int NoteCredito { get; set; }
    public string? Messaggio { get; set; }
}

/// <summary>Cliente dell'elenco "in sviluppo": obiettivo di ricavi di laboratorio per l'anno.</summary>
public class VoceElencoSviluppo
{
    public int IdElencoSviluppo { get; set; }
    public int IdCommerciale { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public int Anno { get; set; }

    /// <summary>Primo giorno del trimestre di inserimento: contano gli incassi da questa data.</summary>
    public DateTime ValidoDal { get; set; }
    public decimal Obiettivo { get; set; }
    public string? Motivo { get; set; }
    public string? AzionePrevista { get; set; }
    public string? ApprovatoDa { get; set; }
    public string? Note { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
}

public enum StatoTrimestre
{
    Aperto = 1,
    Chiuso = 2
}

/// <summary>Trimestre di calcolo. Da chiuso, i movimenti non cambiano più.</summary>
public class TrimestreProvvigioni
{
    public int IdTrimestre { get; set; }
    public int Anno { get; set; }
    public int Numero { get; set; }
    public StatoTrimestre Stato { get; set; } = StatoTrimestre.Aperto;
    public DateTime? DataChiusura { get; set; }
    public int? IdUtenteChiusura { get; set; }

    /// <summary>Regole usate per il calcolo, in JSON (riproducibilità).</summary>
    public string? RegoleUsate { get; set; }
    public string VersioneApplicazione { get; set; } = null!;
}

/// <summary>Movimento di provvigione calcolato. Per i trimestri chiusi è definitivo.</summary>
public class MovimentoProvvigioneArchiviato
{
    public int IdMovimento { get; set; }
    public int IdTrimestre { get; set; }
    public int IdCommerciale { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public string Riferimento { get; set; } = null!;
    public DateTime Data { get; set; }
    public TipoMovimento Tipo { get; set; }
    public VoceProvvigione Voce { get; set; }
    public CategoriaCliente Categoria { get; set; }
    public decimal Base { get; set; }
    public decimal Aliquota { get; set; }
    public decimal Importo { get; set; }
}

/// <summary>Premio annuale calcolato per un commerciale (provvisorio finché l'anno non è chiuso).</summary>
public class PremioProvvigioni
{
    public int IdPremio { get; set; }
    public int IdCommerciale { get; set; }
    public int Anno { get; set; }
    public decimal BaseIncassata { get; set; }
    public decimal Premio { get; set; }
    public DateTime DataCalcolo { get; set; }
}

/// <summary>
/// Convenzione pluriennale di un commerciale (Carloni), registrata dalla schermata «Convenzioni».
/// Il fatturato del cliente nei cinque anni dalla data di inizio è compensato con le aliquote dei punti 1-3 dell'accordo.
/// </summary>
public class ConvenzioneProvvigioni
{
    public int IdConvenzione { get; set; }
    public int IdCommerciale { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public DateTime DataInizio { get; set; }
    public OrigineConvenzione Origine { get; set; }
    public string? Descrizione { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }

    /// <summary>Inserita a mano o creata dalle commesse continuative di ArcLab.</summary>
    public FonteConvenzione Fonte { get; set; } = FonteConvenzione.Manuale;

    /// <summary>La data viene da una commessa caricata in blocco (01/01/2022): la data reale va inserita.</summary>
    public bool DataDaVerificare { get; set; }

    /// <summary>Falso finché l'origine è solo la proposta automatica.</summary>
    public bool OrigineConfermata { get; set; }
}

public enum FonteConvenzione
{
    Manuale = 0,
    ArcLab = 1
}

/// <summary>Importo netto di una fattura per centro di costo e voce AZW: serve ad applicare le basi di calcolo.</summary>
public class AzwRigaFattura
{
    public int IdAzwRigaFattura { get; set; }
    public string ChiaveFattura { get; set; } = null!;
    public string Centro { get; set; } = null!;
    public string Voce { get; set; } = null!;
    /// <summary>COMMESSA della riga AZW = chi ha svolto il lavoro (spettanze dei consulenti, 19/10/2026).</summary>
    public string? Commessa { get; set; }
    public decimal Netto { get; set; }
}

/// <summary>Importo netto di una nota di credito per centro di costo e voce AZW.</summary>
public class AzwRigaNotaCredito
{
    public int IdAzwRigaNotaCredito { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public string Riferimento { get; set; } = null!;
    public DateTime Data { get; set; }
    public string Centro { get; set; } = null!;
    public string Voce { get; set; } = null!;
    public string? Commessa { get; set; }
    public decimal Netto { get; set; }
}

/// <summary>
/// Base di calcolo: quali righe di fattura contano per una voce (insieme di centri di costo e voci AZW).
/// Una base usata da un accordo attivo non si modifica: si duplica.
/// </summary>
public class BaseCalcolo
{
    public int IdBaseCalcolo { get; set; }
    public string Nome { get; set; } = null!;
    public string? Descrizione { get; set; }

    /// <summary>Se vero contano tutte le righe, gli elementi sono ignorati.</summary>
    public bool TuttoIlFatturato { get; set; }

    /// <summary>
    /// Centri e voci inclusi, separati da punto e virgola: <c>04:ANA;04:LEV;05:*</c>. <c>*</c> = tutte le voci del centro,
    /// anche future. Testo unico (e non righe figlie) così una modifica non richiede cancellazioni.
    /// </summary>
    public string Elementi { get; set; } = "";

    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }

    public IReadOnlyList<(string Centro, string Voce)> ElencoElementi() =>
        Elementi.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split(':', 2))
            .Select(p => (p[0].Trim(), p.Length > 1 ? p[1].Trim() : "*"))
            .ToList();

    /// <summary>True se una riga con questo centro e questa voce rientra nella base.</summary>
    public bool Include(string centro, string voce)
    {
        if (TuttoIlFatturato) return true;
        foreach (var (c, v) in ElencoElementi())
            if (c == centro && (v == "*" || v == voce)) return true;
        return false;
    }
}

/// <summary>Versione dell'accordo provvigionale di un commerciale. In bozza si modifica; attivo è definitivo (si duplica).</summary>
public class AccordoProvvigioni
{
    public int IdAccordo { get; set; }
    public int IdCommerciale { get; set; }
    public string Nome { get; set; } = null!;
    public TipoAccordo Tipo { get; set; }
    public StatoAccordo Stato { get; set; } = StatoAccordo.Bozza;
    public DateTime ValidoDal { get; set; }

    /// <summary>Impostata automaticamente quando si attiva la versione successiva.</summary>
    public DateTime? ValidoAl { get; set; }

    public string? RiferimentoDocumento { get; set; }

    /// <summary>Durata in mesi del primo anno del cliente nuovo (tipo Laboratorio).</summary>
    public int MesiPrimoAnno { get; set; } = 12;

    /// <summary>Mesi senza fatture oltre i quali il cliente che torna è di nuovo nuovo (tipo Laboratorio).</summary>
    public int MesiPausa { get; set; } = 24;

    /// <summary>I rapporti iniziati prima di questa data sono clienti esistenti (tipo Laboratorio). Vuota = inizio validità.</summary>
    public DateTime? DataClientiEsistenti { get; set; }

    /// <summary>Durata in anni delle convenzioni (tipo Convenzioni).</summary>
    public int AnniConvenzione { get; set; } = 5;

    /// <summary>Fine del rapporto di lavoro, se nota (tipo Convenzioni: esclude le voci anni 2-5 e segnalate).</summary>
    public DateTime? DataFineRapporto { get; set; }

    /// <summary>Proporzione di soglie e premio se cambia l'orario (1 = tempo pieno).</summary>
    public decimal FattoreOrario { get; set; } = 1m;

    public string? Note { get; set; }
    public DateTime DataCreazione { get; set; }
    public List<VoceAccordo> Voci { get; set; } = new();
    public List<SogliaAccordo> Soglie { get; set; } = new();
}

public class VoceAccordo
{
    public int IdVoceAccordo { get; set; }
    public int IdAccordo { get; set; }
    public VoceProvvigione Voce { get; set; }

    /// <summary>Percentuale (5 = 5%).</summary>
    public decimal Percentuale { get; set; }

    public int IdBaseCalcolo { get; set; }

    /// <summary>Spiegazione di come si applica la voce.</summary>
    public string? Descrizione { get; set; }
}

public class SogliaAccordo
{
    public int IdSogliaAccordo { get; set; }
    public int IdAccordo { get; set; }
    public decimal Soglia { get; set; }
    public decimal Premio { get; set; }
}

/// <summary>
/// Commessa di consulenza o formazione copiata da ArcLab (sola lettura) a ogni importazione:
/// riferimento (commerciale) e tipo di rapporto servono a convenzioni e clienti propri.
/// </summary>
public class CommessaArcLab
{
    public int IdCommessaArcLab { get; set; }

    /// <summary><c>commessa.IdCommessa</c> di ArcLab.</summary>
    public int IdCommessa { get; set; }

    /// <summary><c>commessa.Cod_Cliente</c> = CODCF di AZW.</summary>
    public string CodiceCliente { get; set; } = null!;
    public int IdTipoCommessa { get; set; }

    /// <summary><c>commessa.TipoStampa</c>: 1 continuativo (convenzione), 2 occasionale, 3 terza parte.</summary>
    public short? TipoRapporto { get; set; }
    public DateTime? DataCommessa { get; set; }
    public string? Descrizione { get; set; }

    /// <summary><c>commessa.CodCommessa</c> convertito in numero = <c>mansione.IdMansione</c> del riferimento (consulenza e formazione; per le analisi, tipo 3, solo se l'amministrazione lo inserisce).</summary>
    public int? IdMansioneRiferimento { get; set; }

    /// <summary>
    /// <c>Tipi_Incarico.ID_Tipo_Incarico</c> ricavato dalla descrizione dell'incarico (come nei rapportini): con
    /// <c>cons.VociIncarico</c> dà la voce di fattura AZW del servizio venduto (626, HAC, PRY…).
    /// </summary>
    public int? IdTipoIncarico { get; set; }
}

/// <summary>
/// Chi porta clienti a MARE.A: un ente (CNA, Confesercenti…), uno studio, una persona, un consulente interno o MARE.A stessa.
/// La percentuale non è qui ma nelle sue <see cref="CondizioneSegnalatore"/> (un ente può avere più tipi di contratto).
/// </summary>
public class Segnalatore
{
    public int IdSegnalatore { get; set; }
    public string Nome { get; set; } = null!;
    public TipoSegnalatore Tipo { get; set; } = TipoSegnalatore.Associazione;
    public string? Note { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
    public List<CondizioneSegnalatore> Condizioni { get; set; } = new();
}

public enum TipoSegnalatore
{
    Associazione = 1,
    StudioProfessionale = 2,
    Persona = 3,
    Consulente = 4,
    Azienda = 5,
    Altro = 9
}

/// <summary>Su quale fatturato si calcola un compenso: quello della commessa (default) o tutta un'area (base di calcolo).</summary>
public enum AmbitoCompenso
{
    /// <summary>Le voci di fattura del servizio della commessa ArcLab (tipo di incarico → voce); per le analisi, l'area analisi.</summary>
    Commessa = 1,

    /// <summary>Tutte le righe del cliente nella base di calcolo indicata (es. «Area formazione»).</summary>
    Area = 2
}

/// <summary>
/// Condizione economica di un segnalatore, es. «Confesercenti PS – Contratto A, 5%». Il compenso matura sugli incassi,
/// finché il cliente continua a comprare il servizio: si ferma dopo <see cref="MesiInterruzione"/> senza fatture
/// nell'ambito, e comunque dopo <see cref="AnniDurataMassima"/> anni se indicato.
/// </summary>
public class CondizioneSegnalatore
{
    public int IdCondizioneSegnalatore { get; set; }
    public int IdSegnalatore { get; set; }
    public string Nome { get; set; } = null!;
    public decimal Percentuale { get; set; }
    public AmbitoCompenso Ambito { get; set; } = AmbitoCompenso.Commessa;

    /// <summary>Base di calcolo per l'ambito «Area»; ignorata per «Commessa».</summary>
    public int? IdBaseCalcolo { get; set; }
    public int MesiInterruzione { get; set; } = 24;
    public int? AnniDurataMassima { get; set; }
    public string? Note { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
}

/// <summary>
/// Cliente portato da un segnalatore, con la condizione che si applica. Indipendente dal commerciale: vale anche per
/// i clienti senza commerciale. Per cambiare condizione si chiude la segnalazione (DataFine) e se ne apre un'altra.
/// </summary>
public class Segnalazione
{
    public int IdSegnalazione { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public int IdCondizioneSegnalatore { get; set; }
    public DateTime DataInizio { get; set; }
    public DateTime? DataFine { get; set; }

    /// <summary><c>commessa.IdCommessa</c> di ArcLab: serve quando la condizione è «sulla commessa».</summary>
    public int? IdCommessa { get; set; }
    public string? Note { get; set; }

    /// <summary>Falso = annullata (inserita per errore). Nessuna cancellazione fisica.</summary>
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
}

/// <summary>
/// Eccezione per un cliente di un commerciale: la provvigione si calcola su tutta l'area (base di calcolo)
/// invece che sulle sole voci delle sue commesse. Con data di inizio, motivo e autore.
/// </summary>
public class EccezioneAreaCommerciale
{
    public int IdEccezioneArea { get; set; }
    public int IdCommerciale { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public int IdBaseCalcolo { get; set; }
    public DateTime DataInizio { get; set; }
    public DateTime? DataFine { get; set; }
    public string Motivo { get; set; } = null!;
    public string? CreataDa { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
}

/// <summary>Movimento di compenso a un segnalatore congelato alla chiusura del trimestre.</summary>
public class MovimentoSegnalatoreArchiviato
{
    public int IdMovimentoSegnalatore { get; set; }
    public int IdTrimestre { get; set; }
    public int IdSegnalazione { get; set; }
    public int IdSegnalatore { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public string Riferimento { get; set; } = null!;
    public DateTime Data { get; set; }
    public TipoMovimento Tipo { get; set; }
    public decimal Base { get; set; }
    public decimal Aliquota { get; set; }
    public decimal Importo { get; set; }
}
