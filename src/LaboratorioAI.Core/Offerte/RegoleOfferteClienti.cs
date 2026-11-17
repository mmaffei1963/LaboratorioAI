using System.Text.RegularExpressions;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Offerte;

/// <summary>
/// Offerte sul portale (17/11/2026): in ArcLab le commesse sono per insediamento, l'offerta è per cliente. Le commesse
/// dello stesso cliente con lo stesso controllo (IDControllo) si confrontano su prezzo e descrizione incarico:
/// A) prezzo e descrizione uguali → una riga sola;
/// B) prezzo uguale, descrizione diversa → tutte le righe, anomalia;
/// C) prezzo e descrizione diversi → tutte le righe;
/// D) prezzo diverso, descrizione uguale → una riga con il prezzo più alto, anomalia.
/// </summary>
public static class RegoleOfferteClienti
{
    public const string CasoB = "B";
    public const string CasoD = "D";

    /// <summary>
    /// 17/11/2026 (Mirko Maffei): l'aggiornamento ISTAT 2026 è stato applicato in ArcLab il 21/01/2026. Gli incarichi
    /// precedenti hanno come ultima modifica quella data, con l'etichetta; quelli successivi la loro data di incarico.
    /// </summary>
    public static readonly DateTime DataAggiornamentoIstat2026 = new(2026, 1, 21);
    public const string EtichettaIstat2026 = "Aggiornamento ISTAT 2026";

    /// <summary>Ultima modifica ed etichetta di partenza, prima dello storico delle variazioni.</summary>
    public static (DateTime? Data, string? Etichetta) UltimaModificaIniziale(DateTime? dataIncarico) =>
        dataIncarico is null || dataIncarico < DataAggiornamentoIstat2026
            ? (DataAggiornamentoIstat2026, EtichettaIstat2026)
            : (dataIncarico, null);

    private static readonly Regex Spazi = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Descrizione confrontabile: spazi compattati, maiuscole e minuscole indifferenti.</summary>
    public static string Normalizza(string? testo) => Spazi.Replace((testo ?? "").Trim(), " ").ToUpperInvariant();

    public static EsitoRegoleOfferta Applica(IEnumerable<CommessaOffertaArcLab> commesse)
    {
        var righe = new List<RigaOffertaCliente>();
        var anomalie = new List<AnomaliaOfferta>();

        foreach (var gruppo in commesse.GroupBy(c => (c.IdCliente, c.IdControllo)).OrderBy(g => g.Key.IdCliente).ThenBy(g => g.Key.IdControllo))
        {
            var primo = gruppo.First();
            var righeGruppo = new List<(RigaOffertaCliente Riga, List<CommessaOffertaArcLab> Commesse)>();

            foreach (var perDescrizione in gruppo.GroupBy(c => Normalizza(c.Incarico)))
            {
                var elenco = perDescrizione.ToList();
                var massimo = elenco.Max(c => c.Prezzo);
                var scelte = elenco.Where(c => c.Prezzo == massimo).ToList();
                if (elenco.Select(c => c.Prezzo).Distinct().Count() > 1)
                    anomalie.Add(new AnomaliaOfferta(primo.IdCliente, primo.RagioneSociale, primo.CodContabilita, primo.IdControllo, primo.Controllo,
                        CasoD, $"Stessa descrizione con prezzi diversi ({string.Join(" / ", elenco.Select(c => c.Prezzo).Distinct().OrderBy(p => p).Select(Euro))}): sul portale va il più alto, {Euro(massimo)}.",
                        Ordina(elenco)));

                var dataIncarico = scelte.Where(c => c.DataCommessa is not null).Select(c => c.DataCommessa).Min();
                var (ultima, etichetta) = UltimaModificaIniziale(dataIncarico);   // storico delle variazioni nella seconda consegna
                var riga = new RigaOffertaCliente(
                    primo.IdControllo,
                    scelte.OrderBy(c => c.DataCommessa ?? DateTime.MaxValue).First().Incarico,
                    massimo,
                    dataIncarico,
                    ultima,
                    etichetta,
                    elenco.Select(c => (c.Insediamento ?? "").Trim()).Where(s => s.Length > 0).Distinct().OrderBy(s => s).ToList(),
                    elenco.Select(c => c.IdCommessa).OrderBy(i => i).ToList());
                righeGruppo.Add((riga, elenco));
            }

            // B: descrizioni diverse con lo stesso prezzo.
            foreach (var stessoPrezzo in righeGruppo.GroupBy(r => r.Riga.Prezzo).Where(g => g.Count() > 1))
                anomalie.Add(new AnomaliaOfferta(primo.IdCliente, primo.RagioneSociale, primo.CodContabilita, primo.IdControllo, primo.Controllo,
                    CasoB, $"Stesso prezzo ({Euro(stessoPrezzo.Key)}) con descrizioni diverse: sul portale compaiono tutte.",
                    Ordina(stessoPrezzo.SelectMany(r => r.Commesse))));

            righe.AddRange(righeGruppo.Select(r => r.Riga).OrderBy(r => r.Incarico, StringComparer.CurrentCultureIgnoreCase));
        }

        return new EsitoRegoleOfferta(righe, anomalie);
    }

    private static List<CommessaOffertaArcLab> Ordina(IEnumerable<CommessaOffertaArcLab> c) =>
        c.OrderBy(x => x.Insediamento).ThenBy(x => x.IdCommessa).ToList();

    private static string Euro(decimal v) => v.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("it-IT")) + " €";
}
