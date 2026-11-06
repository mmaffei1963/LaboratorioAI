using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Certificati esterni solo esterni o misti (12/10/2026), funzioni pure.
/// Misto = il campione ha almeno una prova fatta da noi (riga di metodo senza laboratorio esterno):
/// il certificato esterno non genera un certificato ArcLab, i suoi valori vanno inseriti a mano
/// nel certificato MARE.A insieme ai nostri parametri.
/// </summary>
public static class ClassificatoreCertificatoEsterno
{
    /// <summary>Tipo dalle prove del campione; null se in ArcLab il campione non ha prove (non determinabile).</summary>
    public static TipoCertificatoEsterno? Classifica(IEnumerable<ProvaCampioneArcLab> prove)
    {
        var elenco = prove.ToList();
        if (elenco.Count == 0) return null;
        return elenco.Any(p => !p.Esterna) ? TipoCertificatoEsterno.Misto : TipoCertificatoEsterno.SoloEsterni;
    }

    /// <summary>
    /// Prove del certificato: quelle affidate al laboratorio <paramref name="idLabEsterno"/>;
    /// se il laboratorio non è noto, tutte le prove esterne del campione.
    /// </summary>
    public static IReadOnlyList<ProvaCampioneArcLab> ProveDelCertificato(IEnumerable<ProvaCampioneArcLab> prove, int? idLabEsterno)
    {
        var esterne = prove.Where(p => p.Esterna).ToList();
        if (idLabEsterno is int id && id != 0)
        {
            var delLab = esterne.Where(p => p.IdLabEsterno == id).ToList();
            if (delLab.Count > 0) return delLab;
        }
        return esterne;
    }

    /// <summary>
    /// Stato dell'inserimento in ArcLab dei valori di un certificato misto: quante prove del certificato
    /// hanno già il risultato e quali parametri mancano.
    /// </summary>
    public static StatoInserimentoArcLab StatoInserimento(IEnumerable<ProvaCampioneArcLab> prove, int? idLabEsterno)
    {
        var delCert = ProveDelCertificato(prove, idLabEsterno);
        var mancanti = delCert.Where(p => !p.ValoreInserito)
            .Select(p => string.IsNullOrWhiteSpace(p.Parametro) ? $"prova {p.IdProva}" : p.Parametro!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new StatoInserimentoArcLab(delCert.Count, delCert.Count(p => p.ValoreInserito), mancanti);
    }

    /// <summary>Elenco leggibile dei parametri (senza doppioni, in ordine).</summary>
    public static IReadOnlyList<string> Parametri(IEnumerable<ProvaCampioneArcLab> prove) =>
        prove.Select(p => (p.Parametro ?? "").Trim()).Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>Esito del controllo dei valori in ArcLab per un certificato misto.</summary>
public record StatoInserimentoArcLab(int Prove, int Inserite, IReadOnlyList<string> Mancanti)
{
    /// <summary>True se tutte le prove del certificato hanno il risultato in ArcLab.</summary>
    public bool Completo => Prove > 0 && Inserite == Prove;
}
