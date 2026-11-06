namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Cruscotto Direzione (13/10/2026). Movimenti della contabilità analitica di AZW
/// (tabella Access <c>dati_importati_preconsuntivo</c>, la stessa di Power BI) sommati per mese, area (centro di costo),
/// voce, commessa (persona) e conto. Tabella di lavoro: riscritta a ogni importazione notturna di AZW.
/// </summary>
public class MovimentoAnaliticoMensile
{
    public int IdMovimentoAnalitico { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }
    /// <summary>Centro di costo AZW (04 Laboratorio, 05 Sicurezza-HACCP-Ambiente, 03 Amministrazione…).</summary>
    public string Centro { get; set; } = "";
    /// <summary>Voce di costo o di ricavo AZW (VOCICOST.CODICE).</summary>
    public string Voce { get; set; } = "";
    /// <summary>Commessa AZW: la persona (chi fa il lavoro, o di cui è lo stipendio). Vuota se non indicata.</summary>
    public string Commessa { get; set; } = "";
    /// <summary>Conto del piano dei conti (servono per ammortamenti e imposte).</summary>
    public string Conto { get; set; } = "";
    /// <summary>Importo con segno gestionale: ricavi positivi, costi negativi (IMPORTO_SEGNO di Power BI).</summary>
    public decimal Importo { get; set; }
    /// <summary>Numero di righe AZW sommate.</summary>
    public int Righe { get; set; }
}

/// <summary>
/// Classificazione di una voce AZW per gli indicatori (fisso/variabile, macrocategoria, materie prime). All'inizio è
/// la stessa di Power BI (query «Mapping_Costi»); poi si modifica da Direzione → Classificazione voci. Le voci nuove
/// trovate nell'importazione vengono aggiunte con una proposta automatica e segnate «da verificare».
/// Nessuna cancellazione: una voce non più usata resta.
/// </summary>
public class ClassificazioneVoce
{
    public int IdClassificazioneVoce { get; set; }
    public string CodiceVoce { get; set; } = "";
    /// <summary>Descrizione AZW (aggiornata a ogni importazione).</summary>
    public string? Descrizione { get; set; }
    /// <summary>VOCICOST.TIPO = R.</summary>
    public bool Ricavo { get; set; }
    /// <summary>"Fisso", "Variabile" oppure null (costo non classificato: non entra né nei fissi né nei variabili).</summary>
    public string? TipoCosto { get; set; }
    /// <summary>Ricavo, Personale, Formazione, Produzione, Commerciali, Amministrative, Generali, Altri costi.</summary>
    public string Macrocategoria { get; set; } = "";
    public bool MateriaPrima { get; set; }
    /// <summary>True se aggiunta in automatico e non ancora confermata da una persona.</summary>
    public bool DaVerificare { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public int? IdUtente { get; set; }
}

/// <summary>
/// Consuntivo per area (20/10/2026): una riga della contabilità analitica di AZW (tabella <c>dati_importati_preconsuntivo</c>,
/// cioè TESTANA + DETTANA), senza somme, per il periodo dal/al a giorni e il dettaglio delle registrazioni.
/// Tabella di lavoro: riscritta a ogni importazione di AZW.
/// </summary>
public class RegistrazioneAnalitica
{
    public int IdRegistrazioneAnalitica { get; set; }
    /// <summary>Data di registrazione contabile (DATREG): è una data di calendario, senza ora.</summary>
    public DateTime DataRegistrazione { get; set; }
    public int AnnoRegistrazione { get; set; }
    public int NumeroRegistrazione { get; set; }
    public string? TipoDocumento { get; set; }
    public string? NumeroDocumento { get; set; }
    public string Centro { get; set; } = "";
    public string Voce { get; set; } = "";
    public string Commessa { get; set; } = "";
    public string Conto { get; set; } = "";
    public string? Descrizione { get; set; }
    /// <summary>Importo con segno: AVERE positivo, DARE negativo (IMPORTO_SEGNO).</summary>
    public decimal Importo { get; set; }
}

/// <summary>Centro di costo AZW (CENTCOST) con la descrizione, per le etichette delle aree. Riscritta a ogni importazione.</summary>
public class CentroCostoAzw
{
    public int IdCentroCosto { get; set; }
    public string Codice { get; set; } = "";
    public string? Descrizione { get; set; }
}

/// <summary>Commessa AZW (COMMESSE): la persona (chi fa il lavoro). Riscritta a ogni importazione.</summary>
public class CommessaAzw
{
    public int IdCommessaAzw { get; set; }
    public string Codice { get; set; } = "";
    public string? Descrizione { get; set; }
}

/// <summary>
/// Fatturato per cliente (13/10/2026, cruscotto Direzione → Andamento territorio): righe di ricavo della contabilità
/// analitica di AZW (DETTANA, voci di tipo R) sul documento del cliente (TESTANA.CODCF), sommate per mese, centro,
/// voce e commessa. Le note di credito sono in negativo. Cliente vuoto = ricavo non legato a un cliente.
/// Tabella di lavoro: riscritta a ogni importazione notturna di AZW.
/// </summary>
public class FatturatoClienteMensile
{
    public int IdFatturatoCliente { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }
    public string CodiceCliente { get; set; } = "";
    public string Centro { get; set; } = "";
    public string Voce { get; set; } = "";
    public string Commessa { get; set; } = "";
    public decimal Importo { get; set; }
}

/// <summary>
/// Commento AI del cruscotto Direzione (13/10/2026): testo scritto dall'AI sui numeri calcolati dal programma, conservato
/// con i filtri e con i dati inviati, per rileggerlo in seguito. Nessuna cancellazione fisica.
/// </summary>
public class CommentoAiDirezione
{
    public int IdCommentoAiDirezione { get; set; }
    /// <summary>"territorio" oppure "indicatori".</summary>
    public string Pagina { get; set; } = "";
    /// <summary>Filtri della pagina in JSON, per riaprire la stessa vista.</summary>
    public string Filtri { get; set; } = "";
    /// <summary>Filtri in parole (es. «2019–2026, Laboratorio, provincia»).</summary>
    public string DescrizioneFiltri { get; set; } = "";
    /// <summary>Esattamente i dati inviati all'AI (verifica di ogni affermazione).</summary>
    public string DatiInviati { get; set; } = "";
    public string Testo { get; set; } = "";
    public string Modello { get; set; } = "";
    public int TokenInput { get; set; }
    public int TokenOutput { get; set; }
    public decimal CostoStimato { get; set; }
    public int? IdUtente { get; set; }
    public string? NomeUtente { get; set; }
    public DateTime DataCreazione { get; set; }
}
