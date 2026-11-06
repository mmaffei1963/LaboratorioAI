using System.Globalization;
using System.Text;

namespace LaboratorioAI.Core.Manualistica;

/// <summary>
/// Riconoscimento del modello dal nome del file (04/11/2026).
///
/// I nomi dei file dei modelli non contengono il codice del documento
/// («Responsabile_del_trattamento_-_PAGHE_marcato.docx» è RESP_PAGHE),
/// quindi il riconoscimento si basa su una tabella di parti di nome
/// distintive, verificata sui 32 modelli privacy del 04/11/2026
/// (docs\Privacy\FILE PRIVACY SLUG): li riconosce tutti, senza
/// ambiguità e senza doppioni.
///
/// Se il nome cambia e non corrisponde più, il programma non indovina:
/// chiede di scegliere il modello dall'elenco. Assegnare il file al
/// modello sbagliato sarebbe peggio che non assegnarlo, perché il
/// documento finirebbe al cliente con il testo di un altro.
/// </summary>
public static class RiconoscimentoModelli
{
    /// <summary>Parti di nome distintive per codice modello. Vince la corrispondenza più lunga.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> Alias =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["REGISTRO"] = ["REGISTRO_DELLE_ATTIVITA"],
            ["NOMINE"] = ["AUTORIZZATI_AL_TRATTAMENTO_NOMINE"],
            ["FORMAZIONE"] = ["AUTORIZZATI_AL_TRATTAMENTO_FORMAZIONE"],
            // «Discipinare» è il refuso presente nel nome del file di origine.
            ["DISCIPLINARE"] = ["DISCIPLINARE_AUTORIZZATI", "DISCIPINARE_AUTORIZZATI"],
            ["INFORMATIVA_CLIENTI"] = ["INFORMATIVA_CLIENTI"],
            ["INFORMATIVA_LAVORATORI"] = ["INFORMATIVA_LAVORATORI"],
            ["MODULO_CONSENSI"] = ["MODULO_CONSENSI"],
            ["INFORMATIVA_CV"] = ["CURRICULUM_VITAE"],
            ["RESP_BACKUP_CREDENZIALI"] = ["RESPONSABILE_BACKUP"],
            ["PROC_DATA_BREACH"] = ["PROCEDURA_VIOLAZIONE"],
            ["ISTRUZIONI"] = ["ISTRUZIONI_DOCUMENTAZIONE"],
            // Modello di servizio. Il file è «Carta_intestata_NO_ACCREDIA_riepilogo.docx»:
            // nessun altro modello contiene «RIEPILOGO» nel nome.
            ["CARTA_RIEPILOGO"] = ["CARTA_RIEPILOGO", "RIEPILOGO"],
            ["DPIA_VIDEO"] = ["DPIA_VIDEOSORVEGLIANZA"],
            ["CARTELLO_VIDEO"] = ["CARTELLO_VIDEOSORVEGLIANZA"],
            ["CARTELLO_PISCINA"] = ["CARTELLO_TELECAMERA"],
            ["DISP_CAMERIERE"] = ["DISPOSIZIONI_INTERNE_CAMERIERE"],
            ["DISP_MANUTENTORE"] = ["DISPOSIZIONI_INTERNE_MANUTENTORE"],
            ["IMPEGNO_DITTA_PIANI"] = ["IMPEGNO_RISERVATEZZA"],
            ["RESP_CONSULENTE_MAREA"] = ["TRATTAMENTO_CONSULENTE_MARE_A"],
            ["RESP_CONSULENTE"] = ["TRATTAMENTO_CONSULENTE"],
            ["RESP_COMMERCIALISTA"] = ["TRATTAMENTO_COMMERCIALISTA"],
            ["RESP_PAGHE_E_COMMERCIALISTA"] = ["TRATTAMENTO_PAGHE_E_COMMERCIALISTA"],
            ["RESP_PAGHE"] = ["TRATTAMENTO_PAGHE"],
            ["RESP_WEB_AGENCY"] = ["TRATTAMENTO_WEB_AGENCY"],
            ["RESP_SOFTWARE_HOUSE"] = ["TRATTAMENTO_SOFTWARE_HOUSE"],
            ["RESP_TECNICO_COMPUTER"] = ["TRATTAMENTO_TECNICO_COMPUTER"],
            ["RESP_CHIAVI_ELETTRONICHE"] = ["TRATTAMENTO_CHIAVI_ELETTRONICHE"],
            ["RESP_IMPIANTO_VIDEOSORVEGLIANZA"] = ["TRATTAMENTO_IMPIANTO_VIDEOSORVEGLIANZA"],
            ["RESP_STABILIMENTO_BALNEARE"] = ["TRATTAMENTO_STABILIMENTO_BALNEARE"],
            ["RESP_EXPERIENCE"] = ["TRATTAMENTO_EXPERIENCE"],
            ["RESP_DITTA_ESTERNA_SERVIZI_AI_PIANI"] = ["TRATTAMENTO_DITTA_ESTERNA"],
            ["CONTRATTO"] = ["PRIVACY_CONTRATTO"]
        };

    /// <summary>
    /// Codice del modello corrispondente al nome del file, oppure null
    /// se non si riconosce o se due modelli corrispondono allo stesso
    /// modo (nel dubbio non si decide).
    /// </summary>
    public static string? DaNomeFile(string nomeFile)
    {
        var n = Normalizza(nomeFile);
        if (n.Length == 0) return null;

        string? migliore = null;
        var lunghezza = 0;
        var pari = false;

        foreach (var (codice, alias) in Alias)
            foreach (var a in alias)
            {
                if (!n.Contains(a, StringComparison.Ordinal)) continue;
                if (a.Length > lunghezza) { migliore = codice; lunghezza = a.Length; pari = false; }
                else if (a.Length == lunghezza && codice != migliore) pari = true;
            }

        return pari ? null : migliore;
    }

    /// <summary>Maiuscole, senza accenti, con un solo trattino basso fra le parole.</summary>
    private static string Normalizza(string testo)
    {
        var senzaEstensione = testo;
        var punto = senzaEstensione.LastIndexOf('.');
        if (punto > 0) senzaEstensione = senzaEstensione[..punto];

        var scomposto = senzaEstensione.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(scomposto.Length);
        var separatore = false;

        foreach (var c in scomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
            {
                if (separatore && sb.Length > 0) sb.Append('_');
                separatore = false;
                sb.Append(char.ToUpperInvariant(c));
            }
            else separatore = true;
        }

        return sb.ToString();
    }
}
