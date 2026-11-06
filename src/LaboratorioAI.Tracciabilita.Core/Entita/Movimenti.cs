using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;

namespace LaboratorioAI.Tracciabilita.Core.Entita;

public class Ordine : IDellAzienda
{
    public int IdOrdine { get; set; }
    public int IdAzienda { get; set; }
    public int IdInsediamento { get; set; }
    public string Numero { get; set; } = null!;
    public DateOnly DataOrdine { get; set; }
    public DateOnly? DataConsegnaPrevista { get; set; }
    public int IdFornitore { get; set; }
    public StatoOrdine Stato { get; set; } = StatoOrdine.Bozza;
    public string? Note { get; set; }
    public int? IdUtente { get; set; }
    public DateTime DataCreazione { get; set; }
    public List<OrdineRiga> Righe { get; set; } = new();
}

public class OrdineRiga : IDellAzienda
{
    public int IdOrdineRiga { get; set; }
    public int IdAzienda { get; set; }
    public int IdOrdine { get; set; }
    public int IdArticolo { get; set; }
    public decimal Quantita { get; set; }
    public string UnitaMisura { get; set; } = "kg";
    public int? Colli { get; set; }
    public decimal? Prezzo { get; set; }
    public string? Note { get; set; }

    /// <summary>La matrice (categoria) con cui si ordina l'articolo, es. farina 00 in silos o in sacchi.</summary>
    public int? IdCategoria { get; set; }

    /// <summary>Falso = riga tolta dall'ordine (non si cancella).</summary>
    public bool Attiva { get; set; } = true;
}

/// <summary>Arrivo di merce: documento, controlli, giudizio. Dall'accettazione nasce il lotto in ingresso.</summary>
public class Accettazione : IDellAzienda
{
    public int IdAccettazione { get; set; }
    public int IdAzienda { get; set; }
    public int IdInsediamento { get; set; }
    public int? IdOrdineRiga { get; set; }
    public int IdFornitore { get; set; }
    public int IdArticolo { get; set; }
    public DateOnly DataArrivo { get; set; }
    public string? TipoDocumento { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateOnly? DataDocumento { get; set; }
    public string? Vettore { get; set; }
    public decimal Quantita { get; set; }
    public string UnitaMisura { get; set; } = "kg";
    public GiudizioAccettazione Giudizio { get; set; } = GiudizioAccettazione.Accettato;
    public string? NoteReclami { get; set; }

    /// <summary>Lotto in ingresso creato (o ritrovato) da questa accettazione.</summary>
    public int? IdLotto { get; set; }

    public int? IdUtente { get; set; }
    public DateTime DataCreazione { get; set; }
    public List<AccettazioneControllo> Controlli { get; set; } = new();
}

public class AccettazioneControllo : IDellAzienda
{
    public int IdAccettazioneControllo { get; set; }
    public int IdAzienda { get; set; }
    public int IdAccettazione { get; set; }
    public TipoControllo Controllo { get; set; }
    public bool? Conforme { get; set; }

    /// <summary>Valore misurato o numero di riferimento (es. temperatura, n. certificato).</summary>
    public string? Valore { get; set; }

    public string? Note { get; set; }
}

public class Allegato : IDellAzienda
{
    public int IdAllegato { get; set; }
    public int IdAzienda { get; set; }
    public TipoAllegato Tipo { get; set; } = TipoAllegato.Altro;
    public string NomeFile { get; set; } = null!;
    public string? TipoContenuto { get; set; }
    public string Percorso { get; set; } = null!;
    public long Dimensione { get; set; }
    public string? ImprontaSha256 { get; set; }

    /// <summary>Per i certificati: fino a quando sono validi.</summary>
    public DateOnly? ScadenzaValidita { get; set; }

    public int? IdAccettazione { get; set; }
    public int? IdLotto { get; set; }
    public int? IdFornitore { get; set; }
    public int? IdSpedizione { get; set; }
    public int? IdUtente { get; set; }
    public DateTime DataCaricamento { get; set; }
}

/// <summary>
/// Un lotto: di un fornitore (ingresso) o prodotto in azienda.
/// È identificato da azienda + fornitore + articolo + codice (+ giorno, per i lotti interni),
/// non dal solo codice: due fornitori possono usare lo stesso codice.
/// </summary>
public class Lotto : IDellAzienda
{
    public int IdLotto { get; set; }
    public int IdAzienda { get; set; }
    public int IdArticolo { get; set; }

    /// <summary>Vuoto per i lotti prodotti in azienda.</summary>
    public int? IdFornitore { get; set; }

    /// <summary>
    /// La matrice (categoria di ingrediente) con cui il lotto è arrivato: la ricetta cerca i lotti per categoria.
    /// Lo stesso articolo può arrivare in matrici diverse (es. farina 00 in silos o in sacchi).
    /// Se vuota vale la categoria dell'articolo.
    /// </summary>
    public int? IdCategoria { get; set; }

    public string Codice { get; set; } = null!;
    public TipoLotto Tipo { get; set; } = TipoLotto.Ingresso;
    public StatoLotto Stato { get; set; } = StatoLotto.Attivo;

    /// <summary>Giorno di produzione dei lotti interni. Distingue il lotto «296» del 2025 da quello del 2026.</summary>
    public DateOnly? GiornoLottoInterno { get; set; }

    /// <summary>Data di produzione indicata dal fornitore (solo informativa).</summary>
    public DateOnly? DataProduzioneFornitore { get; set; }

    public DateOnly? DataScadenza { get; set; }

    /// <summary>La data indicata è un termine minimo di conservazione (TMC) e non una scadenza.</summary>
    public bool ETmc { get; set; }

    public string? EnteBio { get; set; }
    public string? MotivoBlocco { get; set; }
    public int? IdAccettazione { get; set; }
    public DateTime DataCreazione { get; set; }
}

/// <summary>
/// Registro dei movimenti dei lotti (solo aggiunte). La giacenza è la somma delle quantità.
/// Una correzione è un movimento di tipo <see cref="TipoMovimento.Storno"/>.
/// </summary>
public class MovimentoLotto : IDellAzienda, IAppendOnly
{
    public long IdMovimento { get; set; }
    public int IdAzienda { get; set; }
    public int IdInsediamento { get; set; }
    public int IdLotto { get; set; }
    public TipoMovimento Tipo { get; set; }

    /// <summary>Con il segno: positiva se entra, negativa se esce.</summary>
    public decimal Quantita { get; set; }

    public string UnitaMisura { get; set; } = "kg";
    public DateTime DataMovimento { get; set; }
    public int? IdAccettazione { get; set; }
    public int? IdProduzione { get; set; }
    public int? IdSpedizione { get; set; }
    public int? IdTrasferimento { get; set; }

    /// <summary>Se è uno storno, il movimento che annulla.</summary>
    public long? IdMovimentoStornato { get; set; }

    public string? Nota { get; set; }
    public int? IdUtente { get; set; }
    public DateTime DataCreazione { get; set; }
}

/// <summary>
/// Lotto in uso (l'«impegno» del vecchio programma): per ogni categoria e insediamento,
/// quale lotto è stato iniziato e fino a quando.
/// </summary>
public class LottoInUso : IDellAzienda
{
    public int IdLottoInUso { get; set; }
    public int IdAzienda { get; set; }
    public int IdInsediamento { get; set; }
    public int IdCategoria { get; set; }
    public int IdLotto { get; set; }
    public DateTime Inizio { get; set; }

    /// <summary>Vuota = ancora in uso.</summary>
    public DateTime? Fine { get; set; }

    public int? IdUtente { get; set; }
}

/// <summary>Progressivo giornaliero dei lotti prodotti, per insediamento.</summary>
public class ContatoreLotto : IDellAzienda
{
    public int IdContatore { get; set; }
    public int IdAzienda { get; set; }
    public int IdInsediamento { get; set; }
    public DateOnly Giorno { get; set; }
    public int Ultimo { get; set; }
}

public class Produzione : IDellAzienda
{
    public int IdProduzione { get; set; }
    public int IdAzienda { get; set; }
    public int IdInsediamento { get; set; }
    public int IdRicettaVersione { get; set; }
    public int IdArticolo { get; set; }

    /// <summary>Lotto prodotto. Più produzioni dello stesso prodotto nello stesso giorno possono condividerlo.</summary>
    public int IdLottoProdotto { get; set; }

    /// <summary>Giorno di produzione (giorno di calendario locale, non UTC).</summary>
    public DateOnly Giorno { get; set; }

    public DateTime DataOra { get; set; }
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; }
    public bool Annullata { get; set; }
    public string? Note { get; set; }
    public int? IdUtente { get; set; }
    public List<ProduzioneConsumo> Consumi { get; set; } = new();
}

/// <summary>Quale lotto di ingresso è stato usato in una produzione. È il collegamento che regge un richiamo.</summary>
public class ProduzioneConsumo : IDellAzienda
{
    public int IdProduzioneConsumo { get; set; }
    public int IdAzienda { get; set; }
    public int IdProduzione { get; set; }

    /// <summary>L'ingrediente della ricetta a cui il lotto corrisponde.</summary>
    public int? IdCategoria { get; set; }

    public int IdLottoIngresso { get; set; }
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; }
    public OrigineConsumo Origine { get; set; } = OrigineConsumo.Confermato;
    public int? IdUtente { get; set; }
}

/// <summary>Uscita di prodotto verso un cliente. Il documento di trasporto lo emette il cliente da un altro gestionale.</summary>
public class Spedizione : IDellAzienda
{
    public int IdSpedizione { get; set; }
    public int IdAzienda { get; set; }
    public int IdInsediamento { get; set; }
    public int IdCliente { get; set; }
    public DateOnly DataSpedizione { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateOnly? DataDocumento { get; set; }
    public bool Annullata { get; set; }
    public string? Note { get; set; }
    public int? IdUtente { get; set; }
    public DateTime DataCreazione { get; set; }
    public List<SpedizioneRiga> Righe { get; set; } = new();
}

public class SpedizioneRiga : IDellAzienda
{
    public int IdSpedizioneRiga { get; set; }
    public int IdAzienda { get; set; }
    public int IdSpedizione { get; set; }
    public int IdLotto { get; set; }
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; }
}

/// <summary>Passaggio di lotti da un insediamento a un altro della stessa azienda (non è una vendita).</summary>
public class Trasferimento : IDellAzienda
{
    public int IdTrasferimento { get; set; }
    public int IdAzienda { get; set; }
    public int IdInsediamentoOrigine { get; set; }
    public int IdInsediamentoDestinazione { get; set; }
    public DateOnly Data { get; set; }
    public string? NumeroDocumento { get; set; }
    public string? Note { get; set; }
    public int? IdUtente { get; set; }
    public DateTime DataCreazione { get; set; }
    public List<TrasferimentoRiga> Righe { get; set; } = new();
}

public class TrasferimentoRiga : IDellAzienda
{
    public int IdTrasferimentoRiga { get; set; }
    public int IdAzienda { get; set; }
    public int IdTrasferimento { get; set; }
    public int IdLotto { get; set; }
    public decimal Quantita { get; set; }
    public string UnitaMisura { get; set; } = "kg";
}

/// <summary>Registro dei richiami e delle simulazioni di ritiro.</summary>
public class Richiamo : IDellAzienda
{
    public int IdRichiamo { get; set; }
    public int IdAzienda { get; set; }
    public TipoRichiamo Tipo { get; set; } = TipoRichiamo.Simulazione;
    public int IdLottoOrigine { get; set; }
    public string? Motivo { get; set; }
    public DateTime DataAvvio { get; set; }
    public DateTime? DataChiusura { get; set; }

    /// <summary>Tempo impiegato per ottenere l'elenco dei destinatari (richiesto negli audit).</summary>
    public int? SecondiImpiegati { get; set; }

    /// <summary>Rapporto con lotti, produzioni, spedizioni e clienti coinvolti (JSON).</summary>
    public string? RapportoJson { get; set; }

    public string? Esito { get; set; }
    public int? IdUtente { get; set; }
}
