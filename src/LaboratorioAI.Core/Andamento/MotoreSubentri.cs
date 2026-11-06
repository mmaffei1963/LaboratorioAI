using System.Globalization;
using System.Text;

namespace LaboratorioAI.Core.Andamento;

// «Cambi di gestione» (subentri, 12/10/2026). Per ogni insediamento che faceva Legionella nei 12 mesi precedenti e non
// manda più campioni negli ultimi 12, cerca un altro cliente nello stesso posto (stessa città + stesso indirizzo, oppure
// stesso nome dell'insediamento) arrivato DOPO, e classifica l'esito. Funzione pura: nessun accesso al database.
// Regole decise con Mirko dopo le prove in SSMS del 12/10/2026 (docs\AndamentoClienti\Verifica_subentri_Legionella_v2.sql).

/// <summary>Insediamento ArcLab (copia di lavoro) con il suo cliente.</summary>
public sealed record InsediamentoSubentri(
    int IdInsediamento, int IdCliente, string? CodiceCliente, string RagioneSociale, string? PartitaIva,
    string? Nome, string? Indirizzo, string? Citta);

/// <summary>
/// Attività di un insediamento in un mese per il servizio scelto (13/10/2026: non più solo Legionella).
/// <paramref name="Presenza"/> = attività che dice se la sede è ancora nostra (es. qualsiasi campione di laboratorio);
/// <paramref name="Ambito"/> = attività del servizio analizzato (es. campioni di Legionella, prestazioni di sicurezza).
/// Una sede è «persa» se aveva attività dell'ambito nel periodo di confronto e nessuna presenza nel periodo attuale.
/// </summary>
public sealed record AttivitaInsediamentoMese(int IdInsediamento, int Anno, int Mese, int Presenza, int Ambito,
    decimal Valore, DateTime Ultima, DateTime Prima);

public enum EsitoSubentro
{
    /// <summary>Un altro cliente, arrivato dopo, manda campioni dallo stesso posto: non è perso.</summary>
    SubentratoAttivo = 1,
    /// <summary>Stessa P.IVA su due anagrafiche, l'altra è attiva.</summary>
    AnagraficaDoppia = 2,
    /// <summary>C'è un nuovo gestore in ArcLab ma non manda campioni: da chiamare per primo.</summary>
    SubentratoNonAttivo = 3,
    /// <summary>Nessun subentro trovato: da ricontattare.</summary>
    NessunSubentro = 4,
    /// <summary>Lo stesso cliente manda campioni da un altro insediamento: non è perso.</summary>
    ClienteAttivoAltrove = 5
}

public enum AffidabilitaSubentro { Alta = 1, Media = 2, Bassa = 3 }

/// <summary>Decisione di una persona su un abbinamento (dal database di LabAI).</summary>
public sealed record DecisioneSubentro(int IdInsediamentoPrecedente, int? IdInsediamentoSuccessivo, bool Confermato);

/// <summary>Riepilogo di un insediamento nei periodi di confronto.</summary>
/// <summary>Riepilogo di un insediamento nei periodi: Campioni* = presenza (campioni o prestazioni), Ambito* = servizio analizzato.</summary>
public sealed record StatoInsediamento(
    InsediamentoSubentri Insediamento, int CampioniUltimi, int CampioniPrecedenti, int AmbitoUltimi, int AmbitoPrecedenti,
    decimal ValoreUltimi, decimal ValorePrecedenti, DateTime? PrimoCampione, DateTime? UltimoCampione, DateTime? UltimaAttivitaAmbito);

public sealed record CandidatoSubentro(StatoInsediamento Stato, AffidabilitaSubentro Affidabilita, bool StessoNome, bool StessoIndirizzo, bool StessaPartitaIva);

public sealed record RisultatoSubentro(
    StatoInsediamento Perso,
    EsitoSubentro Esito,
    CandidatoSubentro? Successore,
    IReadOnlyList<CandidatoSubentro> AltriCandidati,
    /// <summary>Campioni degli ultimi 12 mesi dello stesso cliente da altri insediamenti.</summary>
    int CampioniClienteAltrove,
    /// <summary>Vero se una persona ha confermato l'abbinamento (o «nessun subentro»).</summary>
    bool Confermato,
    /// <summary>Abbinamento solo per indirizzo non ancora confermato: va controllato a mano.</summary>
    bool DaVerificare,
    bool SenzaIndirizzo)
{
    /// <summary>Esiti per cui il cliente o il posto non è perso.</summary>
    public bool NonPerso => Esito is EsitoSubentro.SubentratoAttivo or EsitoSubentro.AnagraficaDoppia or EsitoSubentro.ClienteAttivoAltrove
                            && !DaVerificare;
}

/// <summary>Esiti e periodi usati: attuale = InizioUltimi..UltimoMese, confronto = InizioPrecedenti..FinePrecedenti.</summary>
public sealed record RisultatoSubentri(IReadOnlyList<RisultatoSubentro> Persi, int UltimoMese, int InizioUltimi, int InizioPrecedenti,
    int FinePrecedenti, FinestraConfronto Finestra)
{
    public string Attuale => DescrizionePeriodi.Intervallo(InizioUltimi, UltimoMese);
    public string Confronto => DescrizionePeriodi.Intervallo(InizioPrecedenti, FinePrecedenti);
}

public sealed class MotoreSubentri
{
    /// <summary>
    /// Calcola gli esiti. Periodi a mesi interi prima del mese in corso, come in «Clienti da seguire»: periodo attuale
    /// (ultimi 12, 6 o 3 mesi, oppure da inizio anno) contro gli stessi mesi di 1 o 2 anni prima (stagionalità).
    /// Persa = Legionella nel periodo di confronto e nessun campione nel periodo attuale. Predefinito: 12 mesi, 1 anno prima.
    /// </summary>
    public RisultatoSubentri Calcola(IEnumerable<InsediamentoSubentri> insediamenti, IEnumerable<AttivitaInsediamentoMese> campioni,
        IEnumerable<DecisioneSubentro> decisioni, DateTime oggi, FinestraConfronto? finestra = null)
    {
        finestra ??= FinestraConfronto.Predefinita;
        var fine = new DateTime(oggi.Year, oggi.Month, 1).AddMonths(-1);
        int Chiave(DateTime d) => d.Year * 100 + d.Month;
        var durata = finestra.DurataMesi(fine.Month);
        var spostamento = 12 * Math.Clamp(finestra.AnniIndietro, 1, 3);
        var ultimo = Chiave(fine);
        var inizioUltimi = Chiave(fine.AddMonths(-(durata - 1)));
        var inizioPrecedenti = Chiave(fine.AddMonths(-(durata - 1) - spostamento));
        var finePrecedenti = Chiave(fine.AddMonths(-spostamento));

        var perIns = campioni.GroupBy(c => c.IdInsediamento).ToDictionary(g => g.Key, g => g.ToList());
        var elenco = insediamenti.ToList();
        var stati = new Dictionary<int, StatoInsediamento>();
        foreach (var i in elenco)
        {
            perIns.TryGetValue(i.IdInsediamento, out var righe);
            righe ??= [];
            bool InUltimi(AttivitaInsediamentoMese r) { var k = r.Anno * 100 + r.Mese; return k >= inizioUltimi && k <= ultimo; }
            bool InPrecedenti(AttivitaInsediamentoMese r) { var k = r.Anno * 100 + r.Mese; return k >= inizioPrecedenti && k <= finePrecedenti; }
            stati[i.IdInsediamento] = new StatoInsediamento(i,
                righe.Where(InUltimi).Sum(r => r.Presenza), righe.Where(InPrecedenti).Sum(r => r.Presenza),
                righe.Where(InUltimi).Sum(r => r.Ambito), righe.Where(InPrecedenti).Sum(r => r.Ambito),
                righe.Where(InUltimi).Sum(r => r.Valore), righe.Where(InPrecedenti).Sum(r => r.Valore),
                righe.Where(r => r.Presenza > 0).Select(r => (DateTime?)r.Prima).DefaultIfEmpty(null).Min(),
                righe.Where(r => r.Presenza > 0).Select(r => (DateTime?)r.Ultima).DefaultIfEmpty(null).Max(),
                righe.Where(r => r.Ambito > 0).Select(r => (DateTime?)r.Ultima).DefaultIfEmpty(null).Max());
        }

        // Campioni degli ultimi 12 mesi per cliente (tutti i suoi insediamenti).
        var campioniCliente = stati.Values.GroupBy(s => s.Insediamento.IdCliente).ToDictionary(g => g.Key, g => g.Sum(s => s.CampioniUltimi));

        // Indici per città: per indirizzo e per nome.
        var chiavi = elenco.ToDictionary(i => i.IdInsediamento, i => (Citta: ChiaveCitta(i.Citta), Via: ChiaveVia(i.Indirizzo), Nome: ChiaveNome(i.Nome)));
        var perVia = elenco.Where(i => chiavi[i.IdInsediamento].Via.Length >= 6)
            .ToLookup(i => (chiavi[i.IdInsediamento].Citta, chiavi[i.IdInsediamento].Via));
        var perNome = elenco.Where(i => chiavi[i.IdInsediamento].Nome.Length >= 4)
            .ToLookup(i => (chiavi[i.IdInsediamento].Citta, chiavi[i.IdInsediamento].Nome));

        var decise = decisioni.ToList();
        var risultati = new List<RisultatoSubentro>();
        foreach (var p in stati.Values.Where(s => s.AmbitoPrecedenti > 0 && s.CampioniUltimi == 0))
        {
            var k = chiavi[p.Insediamento.IdInsediamento];
            var piva = PartitaIvaValida(p.Insediamento.PartitaIva);
            var trovati = perVia[(k.Citta, k.Via)].Concat(perNome[(k.Citta, k.Nome)])
                .Where(a => a.IdCliente != p.Insediamento.IdCliente)
                .DistinctBy(a => a.IdInsediamento)
                .Select(a =>
                {
                    var ka = chiavi[a.IdInsediamento];
                    var stessoNome = k.Nome.Length >= 4 && ka.Nome == k.Nome;
                    var stessaVia = k.Via.Length >= 6 && ka.Via == k.Via;
                    var contiene = k.Nome.Length >= 4 && ka.Nome.Length >= 4 && (ka.Nome.Contains(k.Nome) || k.Nome.Contains(ka.Nome));
                    var aff = stessoNome ? AffidabilitaSubentro.Alta : stessaVia && contiene ? AffidabilitaSubentro.Media : AffidabilitaSubentro.Bassa;
                    var stessaPiva = piva is not null && PartitaIvaValida(a.PartitaIva) == piva;
                    return new CandidatoSubentro(stati[a.IdInsediamento], aff, stessoNome, stessaVia, stessaPiva);
                })
                .Where(c => c.StessaPartitaIva || VenutoDopo(c.Stato, p))
                .ToList();

            // Decisioni delle persone: scarti esclusi; un abbinamento confermato (anche manuale) vince su tutto.
            var scartati = decise.Where(d => d.IdInsediamentoPrecedente == p.Insediamento.IdInsediamento && !d.Confermato && d.IdInsediamentoSuccessivo is not null)
                .Select(d => d.IdInsediamentoSuccessivo!.Value).ToHashSet();
            trovati.RemoveAll(c => scartati.Contains(c.Stato.Insediamento.IdInsediamento));
            var conferma = decise.LastOrDefault(d => d.IdInsediamentoPrecedente == p.Insediamento.IdInsediamento && d.Confermato);

            var altrove = campioniCliente.GetValueOrDefault(p.Insediamento.IdCliente) - p.CampioniUltimi;
            var senzaIndirizzo = k.Via.Length < 6;
            var ordinati = trovati
                .OrderBy(c => c.Stato.CampioniUltimi > 0 ? 0 : 1).ThenBy(c => c.Affidabilita)
                .ThenByDescending(c => c.Stato.UltimoCampione ?? DateTime.MinValue).ToList();

            CandidatoSubentro? scelto;
            bool confermato;
            if (conferma is not null)
            {
                confermato = true;
                scelto = conferma.IdInsediamentoSuccessivo is int ids && stati.TryGetValue(ids, out var st)
                    ? ordinati.FirstOrDefault(c => c.Stato.Insediamento.IdInsediamento == ids)
                      ?? new CandidatoSubentro(st, AffidabilitaSubentro.Alta, false, false,
                          piva is not null && PartitaIvaValida(st.Insediamento.PartitaIva) == piva)
                    : null;
            }
            else
            {
                confermato = false;
                scelto = ordinati.FirstOrDefault();
            }

            EsitoSubentro esito;
            if (scelto is not null && scelto.Stato.CampioniUltimi > 0)
                esito = scelto.StessaPartitaIva ? EsitoSubentro.AnagraficaDoppia : EsitoSubentro.SubentratoAttivo;
            else if (altrove > 0) esito = EsitoSubentro.ClienteAttivoAltrove;
            else if (scelto is not null) esito = EsitoSubentro.SubentratoNonAttivo;
            else esito = EsitoSubentro.NessunSubentro;

            var daVerificare = !confermato && scelto is not null && scelto.Affidabilita == AffidabilitaSubentro.Bassa
                               && esito is EsitoSubentro.SubentratoAttivo or EsitoSubentro.SubentratoNonAttivo;
            risultati.Add(new RisultatoSubentro(p, esito, scelto,
                ordinati.Where(c => c != scelto).Take(5).ToList(), Math.Max(0, altrove), confermato, daVerificare, senzaIndirizzo));
        }

        return new RisultatoSubentri(
            risultati.OrderBy(r => r.Esito).ThenByDescending(r => r.Perso.ValorePrecedenti).ToList(),
            ultimo, inizioUltimi, inizioPrecedenti, finePrecedenti, finestra);
    }

    /// <summary>
    /// Il candidato è arrivato dopo: ha campioni successivi all'ultimo del perso; se non ne ha, ha un codice contabile
    /// (numerico) più alto, cioè un'anagrafica più recente. L'Id interno di ArcLab non segue l'ordine nel tempo.
    /// </summary>
    private static bool VenutoDopo(StatoInsediamento c, StatoInsediamento p)
    {
        if (c.UltimoCampione is DateTime uc)
            return p.UltimoCampione is not DateTime up || uc > up;
        if (int.TryParse(c.Insediamento.CodiceCliente, out var cc) && int.TryParse(p.Insediamento.CodiceCliente, out var cp))
            return cc > cp;
        return false;
    }

    // ── Chiavi di confronto ───────────────────────────────────────────

    public static string? PartitaIvaValida(string? piva)
    {
        var s = new string((piva ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (s.StartsWith("IT", StringComparison.Ordinal)) s = s[2..];
        if (s.Length < 5 || s.All(ch => ch == '9') || s.All(ch => ch == '0') || s.StartsWith("9999999", StringComparison.Ordinal)) return null;
        return s;
    }

    public static string ChiaveCitta(string? s) => Pulisci(s);

    public static string ChiaveVia(string? s)
    {
        var t = SenzaAccenti(s).ToUpperInvariant().Replace("VIALE", "VLE").Replace("V.LE", "VLE").Replace("PIAZZA", "PZA");
        return Pulisci(t);
    }

    public static string ChiaveNome(string? s)
    {
        var t = SenzaAccenti(s).ToUpperInvariant().Replace("HOTEL", "").Replace("ALBERGO", "").Replace("RESIDENCE", "");
        return Pulisci(t);
    }

    private static string Pulisci(string? s) =>
        new string(SenzaAccenti(s).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string SenzaAccenti(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var n = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(n.Length);
        foreach (var ch in n)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>Servizio su cui si cercano le sedi perse (13/10/2026).</summary>
public enum AmbitoSubentri
{
    Legionella = 1,
    Analisi = 2,
    AnalisiSenzaLegionella = 3,
    Consulenza = 10,
    Sicurezza = 11,
    IgieneAlimentare = 12,
    Ambiente = 13,
    Privacy = 14,
    Formazione = 15
}

public static class AmbitiSubentri
{
    public static readonly AmbitoSubentri[] Tutti =
    [
        AmbitoSubentri.Legionella, AmbitoSubentri.Analisi, AmbitoSubentri.AnalisiSenzaLegionella,
        AmbitoSubentri.Consulenza, AmbitoSubentri.Sicurezza, AmbitoSubentri.IgieneAlimentare, AmbitoSubentri.Ambiente,
        AmbitoSubentri.Privacy, AmbitoSubentri.Formazione
    ];

    public static bool DiConsulenza(AmbitoSubentri a) => a >= AmbitoSubentri.Consulenza;

    public static string Nome(AmbitoSubentri a) => a switch
    {
        AmbitoSubentri.Legionella => "Legionella",
        AmbitoSubentri.Analisi => "tutte le analisi di laboratorio",
        AmbitoSubentri.AnalisiSenzaLegionella => "analisi di laboratorio esclusa la Legionella",
        AmbitoSubentri.Consulenza => "tutta la consulenza (esclusa la formazione)",
        AmbitoSubentri.Sicurezza => "consulenza sicurezza sul lavoro",
        AmbitoSubentri.IgieneAlimentare => "consulenza igiene alimentare (HACCP)",
        AmbitoSubentri.Ambiente => "consulenza ambiente e rifiuti",
        AmbitoSubentri.Privacy => "consulenza privacy",
        AmbitoSubentri.Formazione => "formazione",
        _ => a.ToString()
    };

    /// <summary>Parola per l'attività: campioni (laboratorio) o prestazioni (consulenza).</summary>
    public static string Unita(AmbitoSubentri a) => DiConsulenza(a) ? "prestazioni" : "campioni";

    /// <summary>
    /// Area di consulenza dalla voce di fattura del tipo di incarico (cons.VociIncarico) e dal tipo di commessa:
    /// 626/RSP sicurezza, HAC/AT igiene alimentare, RIF ambiente, PRY privacy, F62/FHA/FON o commessa tipo 4 formazione.
    /// Null = consulenza senza area (conta solo in «tutta la consulenza»).
    /// </summary>
    public static AmbitoSubentri? AreaDaVoce(string? voce, int idTipoCommessa)
    {
        if (idTipoCommessa == 4) return AmbitoSubentri.Formazione;
        return (voce ?? "").Trim().ToUpperInvariant() switch
        {
            "626" or "RSP" => AmbitoSubentri.Sicurezza,
            "HAC" or "AT" => AmbitoSubentri.IgieneAlimentare,
            "RIF" => AmbitoSubentri.Ambiente,
            "PRY" => AmbitoSubentri.Privacy,
            "F62" or "FHA" or "FON" => AmbitoSubentri.Formazione,
            _ => null
        };
    }
}
