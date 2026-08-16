namespace LaboratorioAI.Portale.Servizi;

/// <summary>Parametri di funzionamento del portale.</summary>
public class OpzioniPortale
{
    public const string Sezione = "Portale";

    /// <summary>Codice riservato all'accesso operatore MARE.A.</summary>
    public string CodiceOperatore { get; set; } = "MAREA";

    /// <summary>
    /// Credenziale dell'accesso operatore.
    ///
    /// Non deve comparire nella configurazione versionata: va impostata
    /// come variabile d'ambiente <c>Portale__CredenzialeOperatore</c> o
    /// in configurazione protetta sul server.
    ///
    /// Se assente, l'accesso operatore risulta disabilitato: è una scelta
    /// deliberata, preferibile a un valore predefinito noto.
    /// </summary>
    public string? CredenzialeOperatore { get; set; }

    public bool OperatoreAbilitato => !string.IsNullOrWhiteSpace(CredenzialeOperatore);

    public int DurataSessioneOre { get; set; } = 8;

    // ── Protezione da tentativi ripetuti ──────────────────────────
    public int FinestraTentativiMinuti { get; set; } = 15;
    public int TentativiMassimiPerCodice { get; set; } = 8;
    public int TentativiMassimiPerIndirizzo { get; set; } = 25;

    /// <summary>Giorni di conservazione delle sessioni scadute prima della rimozione.</summary>
    public int GiorniConservazioneSessioni { get; set; } = 30;
}
