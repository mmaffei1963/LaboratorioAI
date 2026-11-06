namespace LaboratorioAI.Core.Entita;

// Schema `provv`: spettanze dei consulenti a percentuale (19/10/2026, claude/analisi-spettanze-consulenti.md).
// Il calcolo usa le copie di lavoro AZW del modulo provvigioni (fatture, righe con COMMESSA, incassi, note di credito)
// e le fatture dei consulenti a MARE.A (acconti). AZW resta in sola lettura.

/// <summary>Consulente pagato a percentuale (De Angelis, Bacchiani, Remedia).</summary>
public class ConsulentePercentuale
{
    public int IdConsulentePercentuale { get; set; }
    public string Nome { get; set; } = null!;
    /// <summary>ZONA del cliente in AZW (ANACF.ZONA) = responsabile del cliente: BAC, DEA, REM.</summary>
    public string CodiceZona { get; set; } = null!;
    /// <summary>COMMESSA delle righe di fattura AZW = chi ha svolto il lavoro: BACCHI, VALERI, REMEDI.</summary>
    public string CodiceCommessa { get; set; } = null!;
    /// <summary>Voce di costo AZW delle sue fatture a MARE.A (BAC, VAL, REM).</summary>
    public string? VoceCosto { get; set; }
    /// <summary>Codice fornitore AZW (0530, 0510, 0829): le sue fatture sono gli acconti.</summary>
    public string? CodiceFornitore { get; set; }
    /// <summary>Centri di costo delle sue fatture che sono docenze pagate a ore (es. «FOR»): non contano come acconti.</summary>
    public string? CentriDocenzeOrarie { get; set; }
    /// <summary>Utente che vede solo i propri dati (permesso <c>spettanze.proprie</c>).</summary>
    public int? IdUtente { get; set; }
    public bool Attivo { get; set; } = true;
    public string? Note { get; set; }
}

/// <summary>
/// Versione del contratto (art. 4) con le percentuali. Il contratto valido alla data del movimento (incasso o nota) decide
/// le percentuali; prima del primo contratto non si calcola nulla. Nessuna cancellazione: si annulla.
/// </summary>
public class ContrattoConsulente
{
    public int IdContrattoConsulente { get; set; }
    public int IdConsulentePercentuale { get; set; }
    public DateTime ValidoDal { get; set; }
    public DateTime? ValidoAl { get; set; }
    /// <summary>Documento di riferimento (es. «Contratto firmato il 09/01/2026»).</summary>
    public string? Riferimento { get; set; }
    /// <summary>a) gestione dei contratti attribuiti (lavoro svolto da lui).</summary>
    public decimal PercConsulenza { get; set; } = 50m;
    /// <summary>b) campionamento (analisi con la sua commessa).</summary>
    public decimal PercCampionamento { get; set; } = 10m;
    /// <summary>
    /// 22/10/2026: b) campionamento, ma per le analisi esterne (gruppo «Analisi esterne», ANE): es. 10% contro il 20% di
    /// ANA e legionelle. Vuoto = come <see cref="PercCampionamento"/> (contratti già inseriti).
    /// </summary>
    public decimal? PercCampionamentoEsterne { get; set; }
    /// <summary>c) analisi interne dei suoi clienti.</summary>
    public decimal PercAnalisiClienti { get; set; } = 10m;
    /// <summary>d) primo anno di servizi svolti da altri ai suoi clienti.</summary>
    public decimal PercPrimoAnnoServizi { get; set; } = 10m;
    /// <summary>e) formazione svolta da altri ai suoi clienti.</summary>
    public decimal PercFormazioneAltri { get; set; } = 10m;
    /// <summary>f) docenze sue: percentuale del fatturato del corso.</summary>
    public decimal PercDocenza { get; set; } = 50m;
    /// <summary>g) tarature su attrezzature del cliente; vuoto = non previste dal contratto.</summary>
    public decimal? PercTarature { get; set; }
    public int MesiPrimoAnno { get; set; } = 12;
    public bool Annullato { get; set; }
    public string? Note { get; set; }
    public string ModificatoDa { get; set; } = "";
    public DateTime DataModifica { get; set; }
}

public enum GruppoVociSpettanze { Analisi = 1, AnalisiEsterne = 2, Formazione = 3, Consulenza = 4, Tarature = 5 }

/// <summary>Voci AZW (VOCICOST) di un gruppo, separate da «;». Modificabili da Mirko.</summary>
public class VociGruppoSpettanze
{
    public int IdVociGruppoSpettanze { get; set; }
    public GruppoVociSpettanze Gruppo { get; set; }
    public string Voci { get; set; } = "";
    public string ModificatoDa { get; set; } = "";
    public DateTime DataModifica { get; set; }
}

/// <summary>Riga di una fattura di un consulente a MARE.A (fattura fornitore AZW), copia di lavoro riscritta a ogni importazione.</summary>
public class AzwFatturaConsulente
{
    public int IdAzwFatturaConsulente { get; set; }
    public string CodiceFornitore { get; set; } = null!;
    public string Documento { get; set; } = null!;
    public DateTime DataRegistrazione { get; set; }
    public string Centro { get; set; } = "";
    public string Voce { get; set; } = "";
    /// <summary>Imponibile (costo positivo, rettifiche negative).</summary>
    public decimal Importo { get; set; }
}

/// <summary>Anno chiuso: il calcolo è congelato e non cambia più.</summary>
public class AnnoSpettanzeChiuso
{
    public int IdAnnoSpettanzeChiuso { get; set; }
    public int IdConsulentePercentuale { get; set; }
    public int Anno { get; set; }
    public decimal Maturato { get; set; }
    public decimal Acconti { get; set; }
    public decimal DocenzeOrarie { get; set; }
    public decimal Conguaglio { get; set; }
    public DateTime DataChiusura { get; set; }
    public string ChiusoDa { get; set; } = "";
    public DateTime? DataImportazioneAzw { get; set; }
    public string? Note { get; set; }
}

/// <summary>Movimento di un anno chiuso (congelato).</summary>
public class MovimentoSpettanzaArchiviato
{
    public int IdMovimentoSpettanza { get; set; }
    public int IdAnnoSpettanzeChiuso { get; set; }
    public DateTime Data { get; set; }
    public string Voce { get; set; } = "";
    public string CodiceCliente { get; set; } = "";
    public string Documento { get; set; } = "";
    public string VoceAzw { get; set; } = "";
    public string? Commessa { get; set; }
    public string Origine { get; set; } = "";
    public decimal Base { get; set; }
    public decimal Percentuale { get; set; }
    public decimal Importo { get; set; }
}
