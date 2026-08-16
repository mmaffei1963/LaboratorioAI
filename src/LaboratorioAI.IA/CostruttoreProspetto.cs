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
        ModalitaAnalisi modalita)
    {
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

        sb.AppendLine("INTERVALLO DI RIFERIMENTO");
        sb.AppendLine("Gli intervalli riportati sono i limiti di accettabilità configurati sulla prova, "
                      + "non necessariamente limiti di legge. MARE.A non riporta i limiti normativi "
                      + "sul rapporto di prova.");
        sb.AppendLine("Se un valore è prossimo a un limite normativo che conosci, verificane la robustezza "
                      + "e segnalalo come BORDERLINE.");
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
        sb.AppendLine(ComponiTabella(prove));
        sb.AppendLine("---");
        sb.AppendLine();

        sb.AppendLine("Fornisci la risposta in italiano, con questa struttura:");
        sb.AppendLine();
        sb.AppendLine("1. GIUDIZIO PER PARAMETRO");
        sb.AppendLine("   Per ciascun parametro: CONFORME / NON CONFORME / BORDERLINE / DATO MANCANTE, "
                      + "con una motivazione sintetica.");
        sb.AppendLine();
        sb.AppendLine("2. ANOMALIE E VALORI DA MONITORARE");
        sb.AppendLine();
        sb.AppendLine("3. GIUDIZIO COMPLESSIVO");
        sb.AppendLine("   Indica esplicitamente CONFORME, NON CONFORME oppure BORDERLINE, "
                      + "e se il campione è idoneo alla certificazione.");
        sb.AppendLine();
        sb.AppendLine("4. AZIONI RACCOMANDATE");
        sb.AppendLine();
        sb.AppendLine("Sii preciso, professionale e conciso.");

        return sb.ToString();
    }

    private static string ComponiTabella(IReadOnlyList<ProvaCampione> prove)
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

        var posizione = maiuscolo.IndexOf("GIUDIZIO COMPLESSIVO", StringComparison.Ordinal);
        var blocco = posizione >= 0
            ? maiuscolo[posizione..Math.Min(posizione + 400, maiuscolo.Length)]
            : maiuscolo[..Math.Min(600, maiuscolo.Length)];

        if (blocco.Contains("NON CONFORME", StringComparison.Ordinal)) return GiudizioAnalisi.NonConforme;
        if (blocco.Contains("BORDERLINE",   StringComparison.Ordinal)) return GiudizioAnalisi.Borderline;
        if (blocco.Contains("CONFORME",     StringComparison.Ordinal)) return GiudizioAnalisi.Conforme;

        return GiudizioAnalisi.NonDeterminato;
    }
}
