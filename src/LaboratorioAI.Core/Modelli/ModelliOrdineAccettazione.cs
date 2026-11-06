namespace LaboratorioAI.Core.Modelli;

/// <summary>
/// Ordine di Accettazione: documento generato da LaboratorioAI leggendo ArcLab (SOLA LETTURA)
/// per un lotto già ricevuto. Riepilogo dei campioni con prezzi + allegato tecnico per campione.
/// Modellato sull'analogo documento Sicural, adattato ai dati ArcLab (LabAnalisiDati).
///
/// Chiave stabile del documento: <see cref="IdLotto"/> (il CodLotto si ripete negli anni,
/// quindi NON è univoco ed è usato solo come riferimento stampato).
/// </summary>
public class OrdineAccettazione
{
    /// <summary>Chiave univoca del lotto in ArcLab (Lotto.IdLotto). Guardia anti-doppione.</summary>
    public int IdLotto { get; init; }

    /// <summary>Codice lotto stampato come riferimento del documento (Lotto.CodLotto, non univoco).</summary>
    public string CodLotto { get; init; } = "";

    public int IdCommessa { get; init; }

    /// <summary>Insediamento della commessa (commessa.IDInsediamento), usato nel nome file.</summary>
    public int? IdInsediamento { get; init; }

    /// <summary>Cliente ArcLab (Clienti.IDCliente), per leggere tutti i suoi insediamenti/email (recapiti comunicazioni).</summary>
    public int? IdClienteArcLab { get; init; }

    // ── Date del lotto ─────────────────────────────────────────────
    /// <summary>Data di ricezione/accettazione (Lotto.DataRic).</summary>
    public DateTime? DataRicezione { get; init; }

    /// <summary>Ora di ricezione (Lotto.OraRic, solo parte oraria).</summary>
    public TimeSpan? OraRicezione { get; init; }

    /// <summary>Data di prelievo (Lotto.DataPrelievo).</summary>
    public DateTime? DataPrelievo { get; init; }

    // ── Intestatario (commessa → cliente/insediamento) ─────────────
    public string? ClienteRagioneSociale { get; init; }
    public string? ClienteIndirizzo { get; init; }
    public string? ClienteCap { get; init; }
    public string? ClienteCitta { get; init; }
    public string? ClienteProvincia { get; init; }
    public string? ClientePartitaIva { get; init; }
    /// <summary>06/11/2026: codice contabilità del cliente ArcLab; «MAF2» (Spettabile cliente) = cliente privato.</summary>
    public string? ClienteCodContabilita { get; init; }
    /// <summary>Insediamento del cliente (Insediamento.NomeInsed), se disponibile.</summary>
    public string? Insediamento { get; init; }
    public string? InsediamentoIndirizzo { get; init; }
    public string? InsediamentoCap { get; init; }
    public string? InsediamentoCitta { get; init; }
    public string? InsediamentoProvincia { get; init; }
    /// <summary>Email associate all'insediamento (Insediamento.Email), separate da ";".</summary>
    public string? InsediamentoEmail { get; init; }

    // ── Controllo e prezzo (dalla commessa del lotto) ──────────────
    /// <summary>Controllo ordinato (commessa.IDControllo).</summary>
    public int? IdControllo { get; init; }

    /// <summary>Denominazione del controllo (Controllo.Controllo).</summary>
    public string? Controllo { get; init; }

    /// <summary>Prezzo per campione (commessa.PrezzoControllo).</summary>
    public decimal? PrezzoCampione { get; init; }

    /// <summary>Aliquota IVA applicata al riepilogo economico.</summary>
    public decimal AliquotaIva { get; init; } = 22m;

    /// <summary>Revisione delle Condizioni Generali di Fornitura richiamata nel documento.</summary>
    public string RevisioneCondizioni { get; set; } = "Mod. Com. 05 Rev. 19";

    /// <summary>Riferimento del modulo di Sistema Qualità dell'Ordine di Accettazione (es. "Mod. Acc. 01 Rev. 0 del 16/09/2026").</summary>
    public string? RiferimentoModulo { get; set; }

    /// <summary>
    /// Versione da firmare per un cliente privato senza Portale (14/10/2026); null = versione per il Portale.
    /// </summary>
    public FirmaPrivato? Firma { get; set; }

    /// <summary>
    /// Giudizio di conformità richiesto dal Cliente (Clienti.GiudizioConf, sì/no, impostazione per cliente).
    /// Null se il dato non è leggibile: in quel caso la riga non viene stampata.
    /// </summary>
    public bool? GiudizioConformitaRichiesto { get; set; }

    /// <summary>Testo per l'allegato tecnico (Allegato A, Artt. 10.1 e 10.5): giudizio e pareri vanno insieme, un solo flag per cliente.</summary>
    public string? GiudizioConformitaTesto => GiudizioConformitaRichiesto switch
    {
        true => "Richiesti dal Cliente",
        false => "Non richiesti",
        _ => null
    };

    public List<CampioneAccettazione> Campioni { get; init; } = new();

    // ── Totali ─────────────────────────────────────────────────────
    public decimal TotaleImponibile =>
        Campioni.Sum(c => c.Prezzo ?? 0m);

    public decimal TotaleIva =>
        Math.Round(TotaleImponibile * AliquotaIva / 100m, 2, MidpointRounding.AwayFromZero);

    public decimal TotaleConIva => TotaleImponibile + TotaleIva;

    /// <summary>Numero di campioni privi di prove/parametri caricati.</summary>
    public int CampioniSenzaParametri => Campioni.Count(c => c.Analisi.Count == 0);

    /// <summary>
    /// Il documento è generabile solo se ci sono campioni e tutti hanno almeno una prova
    /// (parametri caricati). I lotti non pronti sono esclusi da generazione e schedulazione.
    /// </summary>
    public bool Pronto => Campioni.Count > 0 && CampioniSenzaParametri == 0;
}

/// <summary>Un campione del lotto, con dati identificativi e analisi richieste.</summary>
public class CampioneAccettazione
{
    public int IdCampione { get; init; }

    /// <summary>Codice campione all'interno del lotto (Campione.CodCampione, es. "01").</summary>
    public string CodCampione { get; init; } = "";

    /// <summary>Riferimento del campione: CodLotto-CodCampione (es. "7700-01").</summary>
    public string Riferimento { get; init; } = "";

    /// <summary>Denominazione dichiarata dal cliente (Campione.DenomCampione).</summary>
    public string? Denominazione { get; init; }

    /// <summary>Matrice (Matrice.Matrice).</summary>
    public string? Matrice { get; init; }

    /// <summary>Imballaggio (Imballaggio.Imballaggio).</summary>
    public string? Imballaggio { get; init; }

    /// <summary>Punto di prelievo (Campione.PuntoPrelievo).</summary>
    public string? PuntoPrelievo { get; init; }

    /// <summary>Luogo di prelievo (Campione.LuogoPrelievo): stampato solo se compilato (15/10/2026).</summary>
    public string? LuogoPrelievo { get; init; }

    /// <summary>Temperatura di ricevimento come testo (Campione.TempRic, es. "+4 °C", "AMB").</summary>
    public string? TemperaturaRicevimento { get; init; }

    /// <summary>Priorità: true se Campione.Urgente = 1.</summary>
    public bool Urgente { get; init; }

    /// <summary>Controllo del campione (di norma quello della commessa del lotto).</summary>
    public int? IdControllo { get; init; }
    public string? Controllo { get; init; }

    /// <summary>Prezzo del campione (commessa.PrezzoControllo).</summary>
    public decimal? Prezzo { get; init; }

    /// <summary>Priorità in forma leggibile.</summary>
    public string PrioritaTesto => Urgente ? "Urgente" : "Routine";

    /// <summary>
    /// Autocontrollo dichiarato dal Cliente (Campione.Escludi_statistica = sì). Null se il dato non è
    /// leggibile in ArcLab: in quel caso la riga non viene stampata.
    /// </summary>
    public bool? AutocontrolloDichiarato { get; init; }

    /// <summary>Testo per l'allegato tecnico: il Laboratorio dà atto solo di cosa ha dichiarato il Cliente (Art. 7.2).</summary>
    public string? AutocontrolloTesto => AutocontrolloDichiarato switch
    {
        true => "Dichiarato dal Cliente",
        false => "Non dichiarato dal Cliente",
        _ => null
    };

    /// <summary>
    /// Campione.RichiesteCliente di ArcLab (09/10/2026). Null se il campo non è leggibile: in quel caso la riga sulla
    /// comunicazione anticipata dei fuori limite non viene stampata.
    /// </summary>
    public string? RichiesteCliente { get; init; }

    /// <summary>True se la lettura di Campione.RichiesteCliente è riuscita (anche se il campo è vuoto).</summary>
    public bool RichiesteClienteLette { get; init; }

    private const string FraseComunicazioneAnticipata = "comunicazione anticipata dei fuori limite";

    /// <summary>
    /// Comunicazione anticipata dei fuori limite (RT-08 rev. 06, dal 01/01/2027): «Richiesta» se Campione.RichiesteCliente
    /// contiene «Richiesta Comunicazione anticipata dei fuori limite» (con l'eventuale testo che segue, es. modalità e
    /// referente), altrimenti «Non richiesta». Null se il campo non è stato letto.
    /// </summary>
    public string? ComunicazioneAnticipataTesto
    {
        get
        {
            if (!RichiesteClienteLette) return null;
            var testo = (RichiesteCliente ?? "").Trim();
            var i = testo.IndexOf(FraseComunicazioneAnticipata, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "Non richiesta dal Cliente";
            var dettaglio = testo[(i + FraseComunicazioneAnticipata.Length)..].Trim(' ', ':', '-', '–', ',', ';', '.', '\r', '\n');
            return string.IsNullOrWhiteSpace(dettaglio) ? "Richiesta dal Cliente" : $"Richiesta dal Cliente – {dettaglio}";
        }
    }

    /// <summary>Prelevatore registrato in ArcLab (Campione.IdPrelevatore → mansione → Organico), se presente.</summary>
    public string? Prelevatore { get; init; }

    /// <summary>
    /// Analisi raggruppate: prove identiche (stessa denominazione, metodo, laboratorio) ripetute sul
    /// campione — tipicamente le aliquote (es. 5 aliquote di Salmonella) — diventano una sola riga con
    /// il numero di ripetizioni, nell'ordine della prima comparsa.
    /// </summary>
    public IReadOnlyList<(AnalisiAccettazione Analisi, int Numero)> AnalisiRaggruppate() =>
        Analisi.GroupBy(a => (a.EtichettaParametro, a.Metodo ?? "", a.LaboratorioEsterno ?? "", a.Accreditato))
               .Select(g => (g.First(), g.Count()))
               .ToList();

    /// <summary>Etichetta di una riga raggruppata: "Salmonella spp. (5 aliquote)".</summary>
    public static string EtichettaRaggruppata(AnalisiAccettazione a, int numero) =>
        numero > 1 ? $"{a.EtichettaParametro} ({numero} aliquote)" : a.EtichettaParametro;

    public List<AnalisiAccettazione> Analisi { get; init; } = new();
}

/// <summary>Una prova richiesta sul campione: denominazione + metodo, con eventuale lab esterno.</summary>
public class AnalisiAccettazione
{
    /// <summary>
    /// Denominazione della prova come in ArcLab: Metodo.DettagliMetodo + " - aliquota N"
    /// quando prove.NumeroAliquota è valorizzato. È l'etichetta primaria da mostrare.
    /// </summary>
    public string? Denominazione { get; init; }

    /// <summary>Parametro analizzato (Parametro.NomeParametro), con fallback alla denominazione riga.</summary>
    public string? Parametro { get; init; }

    /// <summary>Riferimento del metodo (Metodo.RifBiblio).</summary>
    public string? Metodo { get; init; }

    /// <summary>Denominazione riga metodo (rigametodo.DenominazioneRiga), usata come fallback del parametro.</summary>
    public string? DenominazioneRiga { get; init; }

    /// <summary>Accreditamento del metodo (Metodo.Accreditato), come nelle offerte/preventivi.</summary>
    public bool Accreditato { get; init; }

    /// <summary>Nome del laboratorio esterno, se la prova è in subappalto (LabEsterno.LabEsterno).</summary>
    public string? LaboratorioEsterno { get; init; }

    /// <summary>Frase da riportare per il subappalto (LabEsterno.Frase).</summary>
    public string? FraseLabEsterno { get; init; }

    /// <summary>Etichetta da mostrare: Denominazione (DettagliMetodo + aliquota); in mancanza, parametro o denominazione riga.</summary>
    public string EtichettaParametro =>
        !string.IsNullOrWhiteSpace(Denominazione) ? Denominazione!.Trim()
        : !string.IsNullOrWhiteSpace(Parametro) ? Parametro!.Trim()
        : (DenominazioneRiga?.Trim() ?? "—");
}


/// <summary>
/// Dati della versione da firmare dell'Ordine di Accettazione per i privati (14/10/2026): testi senza il Portale
/// (da Impostazioni documenti), e-mail e modalità di consegna del Rapporto di Prova, riquadro delle firme.
/// </summary>
public sealed class FirmaPrivato
{
    public string? EmailRapporto { get; init; }
    public Entita.ConsegnaRapportoPrivato Consegna { get; init; } = Entita.ConsegnaRapportoPrivato.AMano;
    public string TestoVerifica { get; init; } = "";
    /// <summary>Testo delle Condizioni: «[revisione]» viene sostituito con la revisione in vigore.</summary>
    public string TestoCondizioni { get; init; } = "";
    /// <summary>Dichiarazione sopra le firme.</summary>
    public string TestoFirma { get; init; } = "";
    /// <summary>Chi accetta per MARE.A (utente che stampa).</summary>
    public string? PerMarea { get; init; }
}
