namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Committente dei rapporti di taratura (03/11/2026): cliente e insediamento ArcLab in campi propri, separati
/// dall'identificazione dello strumento. Vuoto = taratura interna MARE.A.
/// </summary>
internal static class CommittenteTaratura
{
    public static string? Testo(string? v, int max = 255)
    {
        v = v?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }

    /// <summary>
    /// Rapporti salvati prima del 03/11/2026: il cliente era scelto nella casella dell'identificazione dello
    /// strumento («Origine: Cliente»), quindi il suo nome è lì. Se il committente non è indicato lo si ricava da
    /// quello strumento; se lo strumento era del cliente ma senza anagrafica, nel rapporto resta il generico «Cliente»
    /// (<paramref name="generico"/>), nel modulo il campo resta vuoto.
    /// </summary>
    public static string? DaStrumentoVecchio(string? committente, bool generico, params (bool Interno, int? IdClienteArcLab, string? Identificazione)[] strumenti)
    {
        if (!string.IsNullOrWhiteSpace(committente)) return committente;
        foreach (var s in strumenti)
            if (!s.Interno && s.IdClienteArcLab is not null && !string.IsNullOrWhiteSpace(s.Identificazione))
                return s.Identificazione;
        return generico && strumenti.Any(s => !s.Interno) ? "Cliente" : null;
    }

    /// <summary>Identificazioni già usate per lo stesso cliente (per riproporle), senza doppioni.</summary>
    public static IReadOnlyList<object> Distinti(IEnumerable<(string? Identificazione, string? Codice, DateTime Data)> righe) =>
        righe.Where(r => !string.IsNullOrWhiteSpace(r.Identificazione))
            .GroupBy(r => r.Identificazione!.Trim().ToUpperInvariant())
            .Select(g => g.OrderByDescending(r => r.Data).First())
            .OrderBy(r => r.Identificazione)
            .Select(r => (object)new { identificazione = r.Identificazione!.Trim(), codice = r.Codice })
            .ToList();
}
