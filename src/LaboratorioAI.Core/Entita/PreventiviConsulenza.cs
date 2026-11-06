namespace LaboratorioAI.Core.Entita;

// Schema `cons`: preventivi/contratti di consulenza e formazione. Le attività corrispondono alle descrizioni
// incarico di ArcLab (Tipi_Incarico, sola lettura): da un contratto accettato si compila la commessa in ArcLab.

/// <summary>Come si esprime il prezzo di un'attività.</summary>
public enum ModalitaPrezzoConsulenza
{
    /// <summary>Prezzo unico (es. DUVRI, piano di emergenza).</summary>
    UnaTantum = 1,

    /// <summary>Canone: prezzo del primo anno e prezzo degli anni successivi (es. DVR, HACCP).</summary>
    Annuale = 2,

    /// <summary>Tariffa oraria, quantità = ore stimate.</summary>
    Oraria = 3,

    /// <summary>Pacchetto di ore a prezzo complessivo.</summary>
    PacchettoOre = 4,

    /// <summary>Prezzo per unità (partecipante, campione, rifiuto…); quantità = numero di unità.</summary>
    PerUnita = 5
}

/// <summary>
/// Come si paga un'analisi di laboratorio dentro un pacchetto o un contratto di consulenza (decisione del 6/10/2026):
/// compresa nel canone (il prezzo non compare nel documento) oppure a parte (prezzo indicato, fatturata quando
/// si esegue secondo le Condizioni Generali del Laboratorio: comprende il caso «per conoscenza»).
/// </summary>
public enum ModalitaAnalisiConsulenza
{
    CompresaNelCanone = 1,
    APagamento = 2
}

public enum StatoPreventivoConsulenza
{
    Bozza = 0,
    Inviato = 1,
    Accettato = 2,
    Rifiutato = 3,
    Annullato = 4
}

/// <summary>
/// Attività che si può preventivare: una per descrizione incarico di ArcLab (Tipi_Incarico.ID_Tipo_Incarico).
/// Laboratorio AI aggiunge testo per l'offerta, modalità di prezzo, prezzi proposti e contenuti spuntabili.
/// </summary>
public class AttivitaConsulenza
{
    public int IdAttivitaConsulenza { get; set; }

    /// <summary>Tipi_Incarico.ID_Tipo_Incarico (ArcLab). Unico.</summary>
    public int IdTipoIncarico { get; set; }

    /// <summary>Tipi_Incarico.IDTipoCommessa: 4 formazione, 5 consulenza.</summary>
    public int? IdTipoCommessa { get; set; }

    /// <summary>Tipi_Incarico.DescrIncarico, copiata alla sincronizzazione.</summary>
    public string Descrizione { get; set; } = null!;

    public ModalitaPrezzoConsulenza ModalitaPrezzo { get; set; } = ModalitaPrezzoConsulenza.UnaTantum;

    /// <summary>Unità per la modalità «per unità» (partecipante, campione…).</summary>
    public string? UnitaMisura { get; set; }

    /// <summary>Testo standard dell'offerta (primo anno o prestazione).</summary>
    public string? TestoOfferta { get; set; }

    /// <summary>Testo standard degli anni successivi (solo canoni).</summary>
    public string? TestoSuccessivi { get; set; }

    public decimal? PrezzoProposto { get; set; }
    public decimal? PrezzoSuccessiviProposto { get; set; }
    public int Ordine { get; set; }

    /// <summary>Falso se la descrizione non è più in ArcLab o non si usa nei preventivi.</summary>
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }

    /// <summary>
    /// 29/10/2026 (Mirko Maffei): attività «personalizzata», solo di Laboratorio AI (non è in ArcLab): nel preventivo
    /// titolo, testo, modalità di prezzo e prezzo sono liberi. Ha IdTipoIncarico negativo (−IdTipoCommessa) e
    /// «Allinea con ArcLab» non la disattiva.
    /// </summary>
    public bool Personalizzata { get; set; }

    public List<ContenutoAttivita> Contenuti { get; set; } = new();
}

/// <summary>Voce spuntabile di un'attività (es. per il DVR: rischio chimico, biologico, rumore…).</summary>
public class ContenutoAttivita
{
    public int IdContenutoAttivita { get; set; }
    public int IdAttivitaConsulenza { get; set; }

    /// <summary>Chiave stabile usata dai pacchetti (es. «chimico»).</summary>
    public string? Chiave { get; set; }
    public string Testo { get; set; } = null!;
    public bool Predefinito { get; set; } = true;
    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;
}

/// <summary>Pacchetto di settore (ex modello Word: ristorante, bar, albergo…): attività e contenuti già scelti, prezzi proposti.</summary>
public class PacchettoConsulenza
{
    public int IdPacchettoConsulenza { get; set; }
    public string Nome { get; set; } = null!;
    public string? Descrizione { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
    /// <summary>Ultima modifica (UTC) e chi l'ha fatta, mostrate nell'elenco dei pacchetti.</summary>
    public DateTime? DataUltimaModifica { get; set; }
    public string? UltimaModificaDa { get; set; }
    public List<RigaPacchettoConsulenza> Righe { get; set; } = new();
    /// <summary>Analisi di laboratorio del pacchetto: controlli ArcLab ben definiti, come nei preventivi analisi.</summary>
    public List<AnalisiPacchettoConsulenza> Analisi { get; set; } = new();
}

/// <summary>
/// Analisi di laboratorio di un pacchetto di settore: un controllo ArcLab (<see cref="IdControlloArcLab"/>) oppure
/// un gruppo di controlli con un nome commerciale e un prezzo unico (<see cref="IdControlliOrigine"/>, es. "123-456").
/// Si salva solo il riferimento: metodi e prezzo si leggono da ArcLab quando il pacchetto entra in un preventivo.
/// </summary>
public class AnalisiPacchettoConsulenza
{
    public int IdAnalisiPacchetto { get; set; }
    public int IdPacchettoConsulenza { get; set; }
    public int Ordine { get; set; }
    /// <summary>Controllo ArcLab (riga semplice).</summary>
    public int? IdControlloArcLab { get; set; }
    /// <summary>Solo per i gruppi: Id ArcLab dei controlli raggruppati, separati da "-".</summary>
    public string? IdControlliOrigine { get; set; }
    /// <summary>Nome del controllo (copia da ArcLab) o nome commerciale del gruppo.</summary>
    public string Descrizione { get; set; } = null!;
    public ModalitaAnalisiConsulenza Modalita { get; set; } = ModalitaAnalisiConsulenza.APagamento;
    /// <summary>Numero di analisi all'anno (facoltativo).</summary>
    public decimal? QuantitaAnnua { get; set; }
    /// <summary>Prezzo per analisi fissato nel pacchetto (facoltativo). Vuoto = come nei preventivi analisi: commessa del cliente, altrimenti listino.</summary>
    public decimal? Prezzo { get; set; }
    public string? Note { get; set; }

    /// <summary>Id dei controlli ArcLab a cui si riferisce la riga (uno o più).</summary>
    public IReadOnlyList<int> IdControlli() => AnalisiConsulenza.IdControlli(IdControlloArcLab, IdControlliOrigine);
}

/// <summary>Utilità comuni alle analisi di pacchetti e preventivi di consulenza.</summary>
public static class AnalisiConsulenza
{
    public static IReadOnlyList<int> IdControlli(int? idControllo, string? idControlliOrigine)
    {
        if (!string.IsNullOrWhiteSpace(idControlliOrigine))
            return idControlliOrigine.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).Distinct().ToList();
        return idControllo is int id ? new[] { id } : Array.Empty<int>();
    }

    /// <summary>Riferimento stampato accanto al nome: "ID 123" oppure "ID 123-456".</summary>
    public static string Riferimento(int? idControllo, string? idControlliOrigine) =>
        !string.IsNullOrWhiteSpace(idControlliOrigine) ? "ID " + idControlliOrigine : idControllo is int id ? "ID " + id : "";
}

public class RigaPacchettoConsulenza
{
    public int IdRigaPacchetto { get; set; }
    public int IdPacchettoConsulenza { get; set; }
    public int IdAttivitaConsulenza { get; set; }
    public int Ordine { get; set; }

    /// <summary>Descrizione aggiuntiva (es. «Tampone superficiale»).</summary>
    public string? DescrizioneLibera { get; set; }

    /// <summary>Chiavi dei contenuti inclusi, separate da punto e virgola. Vuoto = i predefiniti.</summary>
    public string? Contenuti { get; set; }
    public ModalitaPrezzoConsulenza? ModalitaPrezzo { get; set; }
    public decimal Quantita { get; set; } = 1m;
    public decimal Prezzo { get; set; }
    public decimal? PrezzoSuccessivi { get; set; }

    /// <summary>Prezzo indicato «per conoscenza» (es. analisi di laboratorio): non entra nei totali.</summary>
    public bool Informativa { get; set; }
}

/// <summary>
/// Preventivo/contratto di consulenza (serie separata C-anno/numero). Anagrafica mista: cliente ArcLab
/// (con sede e referente) oppure dati inseriti dal consulente; per i clienti esistenti SDI e PEC vengono da AZW.
/// </summary>
public class PreventivoConsulenza
{
    public int IdPreventivoConsulenza { get; set; }
    public int Anno { get; set; }
    public int? Numero { get; set; }
    public StatoPreventivoConsulenza Stato { get; set; } = StatoPreventivoConsulenza.Bozza;

    // Cliente
    public int? IdClienteArcLab { get; set; }
    public string? CodiceClienteAzw { get; set; }
    public string? RagioneSociale { get; set; }
    public string? PartitaIva { get; set; }
    public string? CodiceFiscale { get; set; }
    public string? Indirizzo { get; set; }
    public string? Cap { get; set; }
    public string? Citta { get; set; }
    public string? Provincia { get; set; }
    public string? Email { get; set; }
    public string? Pec { get; set; }
    public string? CodiceSdi { get; set; }
    /// <summary>29/10/2026: telefono del committente, ripreso dall'anagrafica AZW (ANACF) e modificabile.</summary>
    public string? Telefono { get; set; }

    // Sede del servizio (Insediamento ArcLab)
    public int? IdInsediamentoArcLab { get; set; }
    public string? SedeNome { get; set; }
    public string? SedeIndirizzo { get; set; }

    // Referente presso la sede (ContattoCliente ArcLab)
    public int? IdContattoArcLab { get; set; }
    public string? ReferenteNome { get; set; }
    public string? ReferenteMansione { get; set; }
    public string? ReferenteTelefono { get; set; }
    public string? ReferenteEmail { get; set; }

    public string? SettoreAttivita { get; set; }
    public int? IdPacchettoOrigine { get; set; }
    public string? Oggetto { get; set; }

    // Condizioni
    public DateTime? DataOfferta { get; set; }
    public int ValiditaGiorni { get; set; } = 30;
    public DateTime? DataInizio { get; set; }
    public int DurataAnni { get; set; } = 2;
    public int NumeroRate { get; set; } = 1;
    public string? CondizioniPagamento { get; set; }
    public string? RevisioneCondizioni { get; set; }

    // Persone MARE.A
    public int? IdUtenteConsulente { get; set; }
    public int? IdOrganicoConsulente { get; set; }
    public string? ConsulenteNome { get; set; }
    public int? IdCommerciale { get; set; }
    /// <summary>Nome del commerciale (copia), stampato come «Riferimento commerciale» solo se indicato.</summary>
    public string? CommercialeNome { get; set; }
    /// <summary>Responsabile cliente MARE.A: dalla zona dell'anagrafica AZW (ANACF.ZONA) tramite la tabella delle sigle, modificabile.</summary>
    public string? ResponsabileCliente { get; set; }
    /// <summary>Sigla della zona in AZW da cui è stato ricavato il responsabile cliente.</summary>
    public string? ZonaCliente { get; set; }

    public decimal TotalePrimoAnno { get; set; }
    public decimal TotaleAnniSuccessivi { get; set; }

    // ── 29/10/2026 (osservazioni di Ludo): le «fasi» sono sostituite dai blocchi di quotazione, quanti se ne vuole.
    // Ogni blocco ha il suo totale e i totali dei blocchi non si sommano. TotalePrimoAnno e TotaleAnniSuccessivi sono
    // quelli del primo blocco (o di tutto il preventivo se non ci sono blocchi).

    public string? Note { get; set; }
    public DateTime? DataAccettazione { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public List<RigaPreventivoConsulenza> Righe { get; set; } = new();
    /// <summary>Blocchi di quotazione (29/10/2026): vuoto = un'unica quotazione, come prima.</summary>
    public List<BloccoPreventivoConsulenza> Blocchi { get; set; } = new();
    /// <summary>Analisi di laboratorio del contratto (controlli ArcLab con copia dei metodi).</summary>
    public List<AnalisiPreventivoConsulenza> Analisi { get; set; } = new();

    public string NumeroCompleto => Numero is int n ? $"C-{Anno}/{n:000}" : "C-bozza";
}

/// <summary>Riga del preventivo: un'attività con i suoi contenuti e prezzi (copia, stabile anche se la tabella cambia).</summary>
public class RigaPreventivoConsulenza
{
    public int IdRigaPreventivoConsulenza { get; set; }
    public int IdPreventivoConsulenza { get; set; }
    public int Progressivo { get; set; }
    public int IdAttivitaConsulenza { get; set; }
    public int IdTipoIncarico { get; set; }
    public int? IdTipoCommessa { get; set; }
    public string DescrizioneAttivita { get; set; } = null!;
    public string? DescrizioneLibera { get; set; }

    /// <summary>Testo dell'offerta e contenuti scelti, copiati alla composizione (modificabili).</summary>
    public string? TestoOfferta { get; set; }
    public string? TestoSuccessivi { get; set; }

    /// <summary>Contenuti inclusi, una riga ciascuno.</summary>
    public string? Contenuti { get; set; }
    public ModalitaPrezzoConsulenza ModalitaPrezzo { get; set; }
    public string? UnitaMisura { get; set; }
    public decimal Quantita { get; set; } = 1m;
    public decimal Prezzo { get; set; }
    public decimal? PrezzoSuccessivi { get; set; }
    public bool Informativa { get; set; }
    /// <summary>29/10/2026: numero del blocco di quotazione (<see cref="BloccoPreventivoConsulenza.Numero"/>). Conta solo se il preventivo ha blocchi.</summary>
    public int Blocco { get; set; } = 1;

    /// <summary>Attività personalizzata (solo Laboratorio AI): non ha un incarico ArcLab.</summary>
    public bool Personalizzata => IdTipoIncarico < 0;
}

/// <summary>
/// Blocco di quotazione di un preventivo di consulenza (29/10/2026, osservazioni di Ludo): un gruppo di attività con
/// titolo, descrizione e un proprio totale. I blocchi sono alternativi o aggiuntivi a scelta del cliente: i loro totali
/// non si sommano. Senza blocchi il preventivo è un'unica quotazione.
/// </summary>
public class BloccoPreventivoConsulenza
{
    public int IdBloccoPreventivoConsulenza { get; set; }
    public int IdPreventivoConsulenza { get; set; }
    /// <summary>Numero del blocco nel preventivo (1, 2, 3…): ordine di stampa e riferimento delle righe.</summary>
    public int Numero { get; set; }
    public string? Titolo { get; set; }
    public string? Descrizione { get; set; }
}

/// <summary>
/// Analisi di laboratorio in un contratto di consulenza: come una riga del preventivo analisi (controllo ArcLab o
/// gruppo di controlli), con copia dei metodi al momento dell'inserimento così il documento resta stabile.
/// </summary>
public class AnalisiPreventivoConsulenza
{
    public int IdAnalisiPreventivoConsulenza { get; set; }
    public int IdPreventivoConsulenza { get; set; }
    public int Progressivo { get; set; }
    public int? IdControlloArcLab { get; set; }
    public string? IdControlliOrigine { get; set; }
    public string Descrizione { get; set; } = null!;
    public ModalitaAnalisiConsulenza Modalita { get; set; } = ModalitaAnalisiConsulenza.APagamento;
    public decimal? QuantitaAnnua { get; set; }
    /// <summary>Da dove viene il prezzo: commessa del cliente, listino (somma dei metodi) o scritto a mano.</summary>
    public OriginePrezzoPreventivo OriginePrezzo { get; set; }
    /// <summary>Prezzo calcolato come nei preventivi analisi (commessa del cliente, altrimenti listino), per analisi.</summary>
    public decimal PrezzoBase { get; set; }
    /// <summary>Prezzo applicato per analisi (IVA esclusa). Per le analisi comprese nel canone non si stampa.</summary>
    public decimal Prezzo { get; set; }
    public string? Note { get; set; }
    public List<MetodoAnalisiConsulenza> Metodi { get; set; } = new();

    public IReadOnlyList<int> IdControlli() => AnalisiConsulenza.IdControlli(IdControlloArcLab, IdControlliOrigine);
}

/// <summary>Metodo/parametro di un'analisi del contratto (copia dei dati ArcLab, come <see cref="RigaMetodoPreventivo"/>).</summary>
public class MetodoAnalisiConsulenza
{
    public int IdMetodoAnalisiConsulenza { get; set; }
    public int IdAnalisiPreventivoConsulenza { get; set; }
    public int Progressivo { get; set; }
    public int? IdMetodoArcLab { get; set; }
    public string? Descrizione { get; set; }
    public string? Denominazione { get; set; }
    public string? RiferimentoNormativo { get; set; }
    public string? UnitaMisura { get; set; }
    public bool Accreditato { get; set; }
    public int? IdLabEsternoArcLab { get; set; }
    public string? LabEsterno { get; set; }
    public string? LabEsternoFrase { get; set; }
}

public class AzwCliente
{
    public int IdAzwCliente { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public string? RagioneSociale { get; set; }
    public string? PartitaIva { get; set; }
    public string? CodiceFiscale { get; set; }
    public string? Indirizzo { get; set; }
    public string? Cap { get; set; }
    public string? Citta { get; set; }
    public string? Provincia { get; set; }
    public string? Email { get; set; }
    public string? Pec { get; set; }
    public string? CodiceSdi { get; set; }
    public string? Telefono { get; set; }
    /// <summary>ANACF.ZONA: sigla del responsabile cliente (vedi <see cref="ResponsabileCliente"/>).</summary>
    public string? Zona { get; set; }
    /// <summary>ANACF.CODVET: codice venditore attuale (13/10/2026, cruscotto Direzione). Non ha storico.</summary>
    public string? CodiceVenditore { get; set; }
}

/// <summary>
/// Sigla della zona AZW (ANACF.ZONA) → nome del responsabile cliente MARE.A. Gestita in Amministrazione;
/// nessuna cancellazione: le sigle non più usate si disattivano.
/// </summary>
public class ResponsabileCliente
{
    public int IdResponsabileCliente { get; set; }
    public string Sigla { get; set; } = null!;
    public string? Nome { get; set; }
    public string? Email { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime? DataUltimaModifica { get; set; }
}
