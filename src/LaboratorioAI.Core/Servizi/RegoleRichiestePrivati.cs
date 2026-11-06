using System.Globalization;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Regole del modulo per i clienti privati (05/11/2026, Mod. Com. 05B; Condizioni Mod. Com. 05A, Art. 6.1 e 8.1 delle Condizioni):
/// tempi di consegna proposti, prezzi IVA inclusa e numerazione.
/// </summary>
public static class RegoleRichiestePrivati
{
    /// <summary>Codice contabilità del cliente generico «Spettabile cliente» in ArcLab: da lì i controlli e i prezzi.</summary>
    public const string CodiceClientePrivati = "MAF2";

    public const int GiorniProveInterne = 10;
    public const int GiorniLegionellaEsterne = 20;
    public const decimal SupplementoGiudizioPredefinito = 50m;

    /// <summary>
    /// Giorni lavorativi di un controllo: 20 se contiene Legionella o una prova affidata a un laboratorio
    /// esterno, altrimenti 10.
    /// </summary>
    public static int TempiControllo(string? nomeControllo, IEnumerable<MetodoRichiestaPrivato> metodi)
    {
        var lista = metodi.ToList();
        if (ELegionella(nomeControllo) || lista.Any(m => ELegionella(m.Denominazione) || ELegionella(m.Metodo)))
            return GiorniLegionellaEsterne;
        if (lista.Any(m => m.IdLabEsterno is not null))
            return GiorniLegionellaEsterne;
        return GiorniProveInterne;
    }

    /// <summary>Tempi della richiesta: il più lungo tra i controlli (10 se non ce ne sono).</summary>
    public static int TempiRichiesta(IEnumerable<int> tempiControlli)
    {
        var l = tempiControlli.ToList();
        return l.Count == 0 ? GiorniProveInterne : l.Max();
    }

    public static bool ELegionella(string? testo) =>
        !string.IsNullOrWhiteSpace(testo) && testo.Contains("legionell", StringComparison.OrdinalIgnoreCase);

    public static decimal IvaInclusa(decimal imponibile, decimal aliquota) =>
        Math.Round(imponibile * (1 + aliquota / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>Ricalcola prezzi di riga, totali e supplemento.</summary>
    public static void Ricalcola(RichiestaAnalisiPrivato r)
    {
        foreach (var riga in r.Righe)
            riga.PrezzoIvaInclusa = IvaInclusa(riga.PrezzoImponibile, r.AliquotaIva);
        var supplemento = r.GiudizioRichiesto ? r.SupplementoGiudizio : 0m;
        r.TotaleImponibile = r.Righe.Sum(x => x.PrezzoImponibile) + Scorpora(supplemento, r.AliquotaIva);
        r.TotaleIvaInclusa = r.Righe.Sum(x => x.PrezzoIvaInclusa) + supplemento;
    }

    /// <summary>Importo senza IVA di un prezzo IVA inclusa (es. supplemento 50 € → 40,98 €).</summary>
    public static decimal Scorpora(decimal ivaInclusa, decimal aliquota) =>
        Math.Round(ivaInclusa / (1 + aliquota / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>IVA compresa nel totale.</summary>
    public static decimal Iva(RichiestaAnalisiPrivato r) => r.TotaleIvaInclusa - r.TotaleImponibile;

    // Documenti consegnati al cliente privato (06/11/2026).
    public const string ModuloOrdine = "Mod. Com. 05B Rev. 0";
    public const string CondizioniPrivati = "Mod. Com. 05A Rev. 0";
    public const string InformativaPrivacy = "Mod. Com. 05C Rev. 0";

    public static string NumeroFormattato(int anno, int numero) => $"PRIV-{anno}/{numero:000}";

    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    public static string Euro(decimal v) => v.ToString("N2", It);
}
