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

    public List<CampioneAccettazione> Campioni { get; init; } = new();

    // ── Totali ─────────────────────────────────────────────────────
    public decimal TotaleImponibile =>
        Campioni.Sum(c => c.Prezzo ?? 0m);

    public decimal TotaleIva =>
        Math.Round(TotaleImponibile * AliquotaIva / 100m, 2, MidpointRounding.AwayFromZero);

    public decimal TotaleConIva => TotaleImponibile + TotaleIva;
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
