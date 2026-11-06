using System.Globalization;
using System.Text;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.IA;

/// <summary>
/// Compone il prospetto sottoposto al modello.
///
/// Isolato in una classe propria perché è il punto più delicato del
/// sistema: la qualità del giudizio dipende quasi interamente da come
/// vengono presentati i dati. Ogni modifica va valutata rieseguendo
/// l'analisi su campioni di riferimento.
/// </summary>
public static class CostruttoreProspetto
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("it-IT");

    public static string Componi(
        DettaglioCampione campione,
        IReadOnlyList<ProvaCampione> prove,
        ModalitaAnalisi modalita,
        IReadOnlyDictionary<int, IReadOnlyList<string>>? limitiDiLegge = null)
    {
        limitiDiLegge ??= new Dictionary<int, IReadOnlyList<string>>();
        var sb = new StringBuilder();

        sb.AppendLine("Sei un esperto di laboratorio accreditato ISO/IEC 17025.");
        sb.AppendLine(modalita == ModalitaAnalisi.Certificato
            ? "Stai analizzando un certificato già emesso per verifica di conformità."
            : "Stai supportando il Responsabile di Laboratorio nella validazione pre-emissione. "
              + "Indica chiaramente se il campione può essere certificato o se vi sono criticità.");
        sb.AppendLine();

        sb.AppendLine("LEGENDA DEI VALORI");
        sb.AppendLine("- \"< valore\"  = risultato sotto il limite di rivelabilità. Indica assenza; "
                      + "è conforme se il limite normativo è superiore.");
        sb.AppendLine("- \"> valore\"  = risultato oltre il limite massimo misurabile.");
        sb.AppendLine("- \"N/D\"       = dato non ancora disponibile nel sistema. "
                      + "Segnalalo come DATO MANCANTE, non come non conformità.");
        sb.AppendLine();

        // 05/11/2026 (piscina 9379/01): l'AI citava un «intervallo di riferimento configurato» che non c'era
        // e giudicava CONFORME un cloro combinato 0,64 con limite 0,4. Ora riceve i limiti di legge validati
        // in Limiti normativi e, se non è sicura, deve dirlo (richiesta di Mirko Maffei).
        sb.AppendLine("LIMITI DI LEGGE");
        sb.AppendLine("Accanto a una prova, \"[limite di legge: …]\" è il limite normativo validato dal laboratorio per "
                      + "questa matrice (con la scheda e il riferimento). È il riferimento principale del giudizio: "
                      + "un valore fuori da quel limite è NON CONFORME.");
        sb.AppendLine("\"[riferimento: …]\" è invece un intervallo di accettabilità configurato sul metodo, non "
                      + "necessariamente di legge. MARE.A non riporta i limiti normativi sul rapporto di prova.");
        sb.AppendLine("Cita un limite di legge o un intervallo di riferimento SOLO se compare nella riga della prova. "
                      + "Non scrivere mai che un valore rientra in un intervallo \"configurato\" o \"di riferimento\" "
                      + "se la riga non lo riporta.");
        sb.AppendLine("Se per una prova non è indicato alcun limite: puoi usare un limite di legge che conosci con "
                      + "certezza per questa matrice, citando la norma; se non sei sicuro del limite o della norma "
                      + "applicabile, scrivi per quel parametro DA VERIFICARE e spiega cosa non sai. "
                      + "Non dichiarare CONFORME un parametro solo perché non hai un limite.");
        sb.AppendLine("È molto più grave dichiarare CONFORME un campione che non lo è che ammettere di non sapere: "
                      + "nel dubbio scegli sempre DA VERIFICARE.");
        sb.AppendLine("Se un valore è prossimo a un limite di legge, verificane la robustezza e segnalalo come BORDERLINE.");
        // 20/10/2026: regole fisse, perché due campioni uguali ricevevano giudizi diversi.
        sb.AppendLine("BORDERLINE si usa SOLO quando un valore misurato (non preceduto da \"<\") è almeno l'80% "
                      + "di un limite di legge certo e applicabile a questa matrice. Non è BORDERLINE: un risultato "
                      + "\"< valore\" (non rilevato), un parametro senza limite di legge (es. tamponi di superficie, "
                      + "per cui esistono solo valori guida), un dubbio sul limite di rivelabilità del metodo.");
        // 26/10/2026 (Mirko Maffei): nell'acqua di rete la conta delle colonie non ha un valore numerico di legge.
        sb.AppendLine("ACQUE DESTINATE AL CONSUMO UMANO (D.Lgs. 18/2023): per l'acqua di rete (distribuzione, rubinetto) il "
                      + "conteggio delle colonie a 22 °C (e a 36 °C) è un parametro indicatore \"senza variazioni anomale\": "
                      + "non ha un valore numerico di legge, quindi non è mai BORDERLINE né NON CONFORME per il solo valore. "
                      + "I valori 100/mL a 22 °C e 20/mL a 36 °C valgono solo per l'acqua confezionata in bottiglie o contenitori. "
                      + "Se il valore ti sembra alto, citalo solo tra i valori da monitorare.");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine($"Cliente:      {campione.RagioneSociale}");
        if (!string.IsNullOrWhiteSpace(campione.NomeInsediamento))
            sb.AppendLine($"Insediamento: {campione.NomeInsediamento}");

        var denominazione = string.Join(' ', new[]
        {
            campione.DenominazioneCampione,
            campione.DenominazioneProdotto
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        sb.AppendLine($"Campione:     {campione.NumeroCampione} — {denominazione}");
        sb.AppendLine($"Matrice:      {campione.Matrice}");

        if (!string.IsNullOrWhiteSpace(campione.Controllo))
            sb.AppendLine($"Controllo:    {campione.Controllo}");

        if (campione.DataRicezione is not null)
            sb.AppendLine($"Ricezione:    {campione.DataRicezione:dd/MM/yyyy} "
                          + $"({campione.GiorniInLaboratorio} giorni in laboratorio)");

        if (!string.IsNullOrWhiteSpace(campione.LuogoPrelievo))
            sb.AppendLine($"Luogo:        {campione.LuogoPrelievo}");
        if (!string.IsNullOrWhiteSpace(campione.PuntoPrelievo))
            sb.AppendLine($"Punto:        {campione.PuntoPrelievo}");

        sb.AppendLine();
        sb.AppendLine("RISULTATI ANALITICI");
        sb.AppendLine(ComponiTabella(prove, limitiDiLegge));
        sb.AppendLine("---");
        sb.AppendLine();

        sb.AppendLine("Fornisci la risposta in italiano, con questa struttura:");
        sb.AppendLine();
        sb.AppendLine("1. GIUDIZIO PER PARAMETRO");
        sb.AppendLine("   Per ciascun parametro: CONFORME / NON CONFORME / BORDERLINE / DA VERIFICARE / DATO MANCANTE, "
                      + "con una motivazione sintetica.");
        sb.AppendLine();
        sb.AppendLine("2. ANOMALIE E VALORI DA MONITORARE");
        sb.AppendLine();
        sb.AppendLine("3. GIUDIZIO COMPLESSIVO");
        sb.AppendLine("   Indica esplicitamente CONFORME, NON CONFORME, BORDERLINE oppure DA VERIFICARE, "
                      + "e se il campione è idoneo alla certificazione. CONFORME solo se per ogni parametro "
                      + "rilevante sei certo del limite applicabile; se anche un solo parametro è DA VERIFICARE "
                      + "e nessuno è NON CONFORME, il giudizio complessivo è DA VERIFICARE.");
        sb.AppendLine();
        sb.AppendLine("4. AZIONI RACCOMANDATE");
        sb.AppendLine();
        sb.AppendLine("Sii preciso, professionale e conciso.");
        sb.AppendLine();
        sb.AppendLine("Chiudi la risposta con UNA sola ultima riga, esattamente in questa forma:");
        sb.AppendLine("ESITO FINALE: CONFORME   oppure   ESITO FINALE: NON CONFORME   oppure   ESITO FINALE: BORDERLINE"
                      + "   oppure   ESITO FINALE: DA VERIFICARE");

        return sb.ToString();
    }

    private static string ComponiTabella(IReadOnlyList<ProvaCampione> prove, IReadOnlyDictionary<int, IReadOnlyList<string>> limitiDiLegge)
    {
        if (prove.Count == 0) return "(nessuna prova registrata)";

        var sb = new StringBuilder();

        foreach (var prova in prove)
        {
            var nome = prova.DenominazioneRiga
                       ?? prova.Parametro
                       ?? "(parametro non identificato)";

            sb.Append("- ").Append(nome).Append(": ").Append(prova.ValoreFormattato);

            if (!string.IsNullOrWhiteSpace(prova.UnitaMisura))
                sb.Append(' ').Append(prova.UnitaMisura);

            var intervallo = prova.IntervalloFormattato;
            if (intervallo is not null)
                sb.Append("  [riferimento: ").Append(intervallo).Append(']');

            if (limitiDiLegge.TryGetValue(prova.IdProva, out var limiti) && limiti.Count > 0)
                sb.Append("  [limite di legge: ").Append(string.Join(" | ", limiti)).Append(']');

            if (!string.IsNullOrWhiteSpace(prova.RiferimentoNormativo))
                sb.Append("  — metodo: ").Append(prova.RiferimentoNormativo);

            if (!string.IsNullOrWhiteSpace(prova.LaboratorioEsterno))
                sb.Append("  — eseguito da: ").Append(prova.LaboratorioEsterno);

            if (!string.IsNullOrWhiteSpace(prova.Note))
                sb.Append("  — nota: ").Append(prova.Note);

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Deduce il giudizio complessivo dal testo prodotto dal modello.
    ///
    /// Si limita alla sezione «giudizio complessivo» per non farsi
    /// influenzare dai giudizi sui singoli parametri, e valuta i casi
    /// dal più grave al più lieve.
    /// </summary>
    public static GiudizioAnalisi DeduciGiudizio(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return GiudizioAnalisi.NonDeterminato;

        var maiuscolo = testo.ToUpperInvariant();

        // 20/10/2026: prima la riga finale strutturata «ESITO FINALE: …» (l'ultima, se ce ne sono più d'una);
        // la ricerca nel testo libero sbagliava frasi come «conforme, nessun valore borderline».
        var esito = maiuscolo.LastIndexOf("ESITO FINALE:", StringComparison.Ordinal);
        if (esito >= 0)
        {
            var riga = maiuscolo[(esito + "ESITO FINALE:".Length)..];
            var fine = riga.IndexOfAny(['\r', '\n']);
            if (fine >= 0) riga = riga[..fine];
            riga = riga.Replace("*", "").Trim();
            if (riga.StartsWith("NON CONFORME", StringComparison.Ordinal)) return GiudizioAnalisi.NonConforme;
            if (riga.StartsWith("DA VERIFICARE", StringComparison.Ordinal)) return GiudizioAnalisi.NonDeterminato;   // 05/11/2026
            if (riga.StartsWith("BORDERLINE", StringComparison.Ordinal)) return GiudizioAnalisi.Borderline;
            if (riga.StartsWith("CONFORME", StringComparison.Ordinal)) return GiudizioAnalisi.Conforme;
        }

        var posizione = maiuscolo.IndexOf("GIUDIZIO COMPLESSIVO", StringComparison.Ordinal);
        var blocco = posizione >= 0
            ? maiuscolo[posizione..Math.Min(posizione + 400, maiuscolo.Length)]
            : maiuscolo[..Math.Min(600, maiuscolo.Length)];

        if (blocco.Contains("NON CONFORME", StringComparison.Ordinal)) return GiudizioAnalisi.NonConforme;
        if (blocco.Contains("DA VERIFICARE", StringComparison.Ordinal)) return GiudizioAnalisi.NonDeterminato;
        if (blocco.Contains("BORDERLINE",   StringComparison.Ordinal)) return GiudizioAnalisi.Borderline;
        if (blocco.Contains("CONFORME",     StringComparison.Ordinal)) return GiudizioAnalisi.Conforme;

        return GiudizioAnalisi.NonDeterminato;
    }
}
