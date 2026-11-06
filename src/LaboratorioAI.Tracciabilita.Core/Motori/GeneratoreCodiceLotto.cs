using System.Globalization;
using System.Text;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Core.Motori;

/// <summary>
/// Costruisce il codice dei lotti prodotti in azienda.
///
/// <para><b>GiornoDellAnno</b> (predefinito, come nel vecchio programma): <c>296</c>.
/// Con più lotti al giorno <c>296-2</c>; con la sigla della sede <c>296-CAT</c>, <c>296-CAT-2</c>.</para>
/// <para><b>DataSedeProgressivo</b>: <c>261023-CAT-01</c>.</para>
///
/// Il codice non contiene l'anno nel formato «giorno dell'anno»: per questo l'identità
/// di un lotto interno comprende anche il giorno di produzione.
/// </summary>
public static class GeneratoreCodiceLotto
{
    public static string Genera(
        FormatoLotto formato,
        DateOnly giorno,
        string? siglaSede,
        int progressivoGiornaliero,
        bool includiSiglaSede)
    {
        if (progressivoGiornaliero < 1)
            throw new ArgumentOutOfRangeException(nameof(progressivoGiornaliero), "Il progressivo parte da 1.");

        var sigla = NormalizzaSigla(siglaSede);

        switch (formato)
        {
            case FormatoLotto.GiornoDellAnno:
            {
                var codice = giorno.DayOfYear.ToString(CultureInfo.InvariantCulture);
                if (includiSiglaSede && sigla is not null)
                    codice += "-" + sigla;
                if (progressivoGiornaliero > 1)
                    codice += "-" + progressivoGiornaliero.ToString(CultureInfo.InvariantCulture);
                return codice;
            }

            case FormatoLotto.DataSedeProgressivo:
            {
                var codice = giorno.ToString("yyMMdd", CultureInfo.InvariantCulture);
                if (sigla is not null)
                    codice += "-" + sigla;
                codice += "-" + progressivoGiornaliero.ToString("00", CultureInfo.InvariantCulture);
                return codice;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato di lotto sconosciuto.");
        }
    }

    /// <summary>Maiuscole, solo lettere e cifre, al massimo 6 caratteri. Vuota → nessuna sigla.</summary>
    public static string? NormalizzaSigla(string? sigla)
    {
        if (string.IsNullOrWhiteSpace(sigla)) return null;

        var sb = new StringBuilder();
        foreach (var c in sigla.Trim())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToUpperInvariant(c));
            if (sb.Length == 6) break;
        }
        return sb.Length == 0 ? null : sb.ToString();
    }
}
