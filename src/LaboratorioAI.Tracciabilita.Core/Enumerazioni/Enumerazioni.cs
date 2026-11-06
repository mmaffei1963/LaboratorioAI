namespace LaboratorioAI.Tracciabilita.Core.Enumerazioni;

/// <summary>Stato del servizio a canone per un'azienda cliente.</summary>
public enum StatoServizio
{
    Attivo = 1,
    Sospeso = 2,
    /// <summary>Il cliente ha chiesto la chiusura: sola lettura, può scaricare i dati.</summary>
    InChiusura = 3,
    Chiuso = 4
}

/// <summary>Profilo di settore: decide i campi aggiuntivi proposti all'azienda.</summary>
public enum ProfiloSettore
{
    Generico = 0,
    Piadine = 1,
    Ittico = 2,
    Gastronomia = 3,
    Ristorazione = 4
}

/// <summary>Ruolo di un utente dentro l'azienda (per insediamento).</summary>
public enum RuoloAzienda
{
    Consultazione = 1,
    Ricevimento = 2,
    Produzione = 3,
    Qualita = 4,
    AmministratoreAzienda = 5
}

public enum TipoArticolo
{
    MateriaPrima = 1,
    Semilavorato = 2,
    ProdottoFinito = 3,
    Imballo = 4
}

/// <summary>Come nasce un lotto: arrivato da un fornitore o prodotto in azienda.</summary>
public enum TipoLotto
{
    Ingresso = 1,
    Semilavorato = 2,
    Finito = 3
}

public enum StatoLotto
{
    Attivo = 1,
    Bloccato = 2,
    Esaurito = 3,
    Richiamato = 4
}

/// <summary>Tipo di movimento del registro dei lotti. La quantità ha il segno.</summary>
public enum TipoMovimento
{
    Carico = 1,
    Consumo = 2,
    Produzione = 3,
    TrasferimentoUscita = 4,
    TrasferimentoEntrata = 5,
    Spedizione = 6,
    Scarto = 7,
    Reso = 8,
    Rettifica = 9,
    /// <summary>Annulla un altro movimento (non si modifica mai un movimento).</summary>
    Storno = 10
}

/// <summary>Come è stato registrato il consumo di un lotto in una produzione.</summary>
public enum OrigineConsumo
{
    /// <summary>Registrato dal programma senza conferma puntuale (es. importazione).</summary>
    Proposto = 1,
    /// <summary>L'operatore ha confermato la proposta senza cambiarla.</summary>
    Confermato = 2,
    /// <summary>L'operatore ha corretto la proposta.</summary>
    Modificato = 3
}

/// <summary>Come l'azienda registra i kg degli ingredienti usati in produzione.</summary>
public enum ModoQuantitaConsumi
{
    /// <summary>Si registra solo quali lotti sono stati usati, senza quantità (la giacenza degli ingressi non cala).</summary>
    SoloLotti = 1,
    /// <summary>L'operatore scrive i kg usati per ogni lotto a fine produzione.</summary>
    Manuale = 2,
    /// <summary>I kg si calcolano dalla ricetta in base alla quantità prodotta (l'operatore può correggerli).</summary>
    Teorica = 3
}

public enum FormatoLotto
{
    /// <summary>Numero del giorno dell'anno (come nel vecchio programma Access). Predefinito.</summary>
    GiornoDellAnno = 1,
    /// <summary>AAMMGG-sigla sede-progressivo giornaliero.</summary>
    DataSedeProgressivo = 2
}

public enum CriterioProposta
{
    ScadenzaPiuVicina = 1,
    OrdineDiArrivo = 2
}

public enum GiudizioAccettazione
{
    Accettato = 1,
    AccettatoConRiserva = 2,
    Respinto = 3
}

public enum StatoOrdine
{
    Bozza = 1,
    Inviato = 2,
    Ricevuto = 3,
    Annullato = 4
}

public enum TipoControllo
{
    Etichettatura = 1,
    Confezione = 2,
    Temperatura = 3,
    Organolettico = 4,
    PrelievoAnalisi = 5,
    Certificati = 6,
    Pallet = 7
}

public enum TipoRichiamo
{
    Richiamo = 1,
    Simulazione = 2
}

public enum TipoAllegato
{
    DocumentoTrasporto = 1,
    CertificatoBio = 2,
    Analisi = 3,
    Altro = 4
}

public enum TipoCampo
{
    Testo = 1,
    Numero = 2,
    Data = 3,
    Elenco = 4
}

public enum StatoEsportazione
{
    Richiesta = 1,
    InCorso = 2,
    Pronta = 3,
    Scaricata = 4,
    Scaduta = 5,
    Errore = 6
}
