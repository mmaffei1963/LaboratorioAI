using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Core.Validazione;

/// <summary>Esito della scelta del giudizio: IdGiudizio da riportare oppure il motivo per cui non si può validare.</summary>
public record SceltaGiudizio(int? IdGiudizio, bool Automatico, string? Errore);

/// <summary>
/// Regole della validazione da LaboratorioAI (decise da Mirko il 18/10/2026; claude/definizione-validazione-giudizio.md).
/// Un campione, un solo giudizio, mai più di uno.
/// </summary>
public static class RegoleGiudizio
{
    /// <summary>«1 - Senza Giudizio professionale».</summary>
    public const int SenzaGiudizio = 212;

    /// <summary>Motivo per cui il campione non si può validare (null = si può).</summary>
    public static string? MotivoBlocco(StatoCampioneArcLab s)
    {
        if (s.AnalisiChiusa) return "In ArcLab l'analisi di questo campione è già chiusa.";
        if (s.Giudizi.Count > 0) return $"In ArcLab il campione ha già un giudizio ({string.Join(", ", s.Giudizi.Select(g => g.CodGiudizio))}): un campione, un solo giudizio.";
        if (s.IdCertificato is > 0) return "Il campione ha già un certificato in ArcLab.";
        if (s.Prove == 0) return "Il campione non ha prove.";
        if (s.ProveNonVerificate > 0) return $"Ci sono ancora {s.ProveNonVerificate} prove con risultato non verificato in ArcLab.";
        return null;
    }

    /// <summary>
    /// Cliente senza richiesta di giudizio (GiudizioConf = 0): se non si sceglie, 212 in automatico.
    /// Cliente con richiesta (GiudizioConf = 1): scelta obbligatoria; il 212 è ammesso solo se scelto a mano.
    /// Il giudizio scelto deve essere tra quelli proposti per la matrice (o il 212).
    /// </summary>
    public static SceltaGiudizio Scegli(StatoCampioneArcLab s, int? idScelto, IEnumerable<GiudizioArcLab> proposti)
    {
        if (idScelto is null)
            return s.GiudizioConf
                ? new(null, false, "Il cliente richiede il giudizio di conformità: scegli il giudizio.")
                : new(SenzaGiudizio, true, null);
        if (idScelto == SenzaGiudizio || proposti.Any(p => p.IdGiudizio == idScelto)) return new(idScelto, false, null);
        return new(null, false, "Il giudizio scelto non è tra quelli previsti per la matrice del campione.");
    }

    /// <summary>Colori dei limiti che chiedono una conferma in più prima di validare.</summary>
    public static bool ChiedeConferma(string? colore) => colore is "rosso" or "viola";
}
