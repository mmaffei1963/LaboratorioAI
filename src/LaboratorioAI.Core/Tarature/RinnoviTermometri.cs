using System.Globalization;
using System.Text;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Tarature;

/// <summary>Tipo di termometro del cliente (03/11/2026). Gli «a infrarossi» sono pirometri.</summary>
public enum TipoTermometro
{
    NonIndicato = 0,
    Sonda = 1,
    Gabbia = 2,
    Pirometro = 3,
    Datalogger = 4,
    Altro = 5
}

/// <summary>Servizio reso al cliente (03/11/2026): solo taratura del suo termometro, o termometro nuovo fornito da MARE.A e tarato.</summary>
public enum ServizioTaraturaTermometro
{
    SoloTaratura = 1,
    FornituraETaratura = 2
}

/// <summary>
/// Scadenziario dei termometri clienti (03/11/2026): stato delle tarature e proposta di rinnovo.
/// Nell'archivio non c'è un'identificazione del singolo termometro (Tipologia = SONDA, PIROMETRO…),
/// quindi il programma non decide da solo quale taratura nuova sostituisce quale vecchia: propone le
/// tarature in uso dello stesso cliente e della stessa tipologia, e l'operatore spunta quelle giuste
/// (come le colonne «Rinnovato» e «Dismesso» del vecchio foglio Excel).
/// </summary>
public static class RinnoviTermometri
{
    public const string InUso = "inuso";
    public const string Rinnovata = "rinnovata";
    public const string Dismessa = "dismessa";

    /// <summary>Stato: rinnovata (sostituita da una taratura più recente), dismessa (fuori dalle scadenze) o in uso.</summary>
    public static string Stato(TermometroEsterno t) =>
        t.DataRinnovo is not null ? Rinnovata : t.EscludiScadenze ? Dismessa : InUso;

    /// <summary>Scaduta da oltre <paramref name="mesi"/> mesi (conta la data di scadenza, non quella di taratura).</summary>
    public static bool ScadutaDaOltre(DateTime? scadenza, DateTime oggi, int mesi = 12) =>
        scadenza is DateTime s && s.Date < oggi.Date.AddMonths(-mesi);

    private static readonly HashSet<string> FormeSocietarie = new(StringComparer.Ordinal)
    {
        "SRL", "SRLS", "SNC", "SAS", "SPA", "SS", "SCARL", "SOC", "COOP", "DI", "E", "C", "F", "LLI", "FLLI", "DITTA"
    };

    /// <summary>Cliente per il confronto: maiuscole, senza accenti né punteggiatura, senza forma societaria.</summary>
    public static string NormalizzaCliente(string? cliente)
    {
        var parole = Parole(cliente);
        // «S.R.L.» diventa «S R L»: si ricompongono le lettere singole consecutive prima di togliere le forme.
        var unite = new List<string>();
        var sigla = new StringBuilder();
        foreach (var p in parole)
        {
            if (p.Length == 1) { sigla.Append(p); continue; }
            if (sigla.Length > 0) { unite.Add(sigla.ToString()); sigla.Clear(); }
            unite.Add(p);
        }
        if (sigla.Length > 0) unite.Add(sigla.ToString());
        return string.Join(' ', unite.Where(p => !FormeSocietarie.Contains(p)));
    }

    /// <summary>Stesso cliente: uguale dopo la normalizzazione, oppure uno è l'inizio dell'altro (almeno 4 caratteri).</summary>
    public static bool StessoCliente(string? a, string? b)
    {
        var x = NormalizzaCliente(a);
        var y = NormalizzaCliente(b);
        if (x.Length == 0 || y.Length == 0) return false;
        if (x == y) return true;
        var (corto, lungo) = x.Length <= y.Length ? (x, y) : (y, x);
        return corto.Length >= 4 && lungo.StartsWith(corto + " ", StringComparison.Ordinal);
    }

    /// <summary>Etichetta del tipo per schermate e filtri.</summary>
    public static string Etichetta(TipoTermometro t) => t switch
    {
        TipoTermometro.Sonda => "a sonda",
        TipoTermometro.Gabbia => "a gabbia",
        TipoTermometro.Pirometro => "pirometro",
        TipoTermometro.Datalogger => "datalogger",
        TipoTermometro.Altro => "altro",
        _ => ""
    };

    public static string Etichetta(ServizioTaraturaTermometro s) =>
        s == ServizioTaraturaTermometro.FornituraETaratura ? "fornitura + taratura" : "solo taratura";

    /// <summary>
    /// Tipo ricavato dalla tipologia scritta a mano (archivio Excel, 03/11/2026): SONDA → a sonda, GABBIA → a gabbia,
    /// PIROMETRO o INFRAROSSI → pirometro, DATALOGGER/LOGGER → datalogger. Altrimenti non indicato.
    /// </summary>
    public static TipoTermometro TipoDaTipologia(string? tipologia)
    {
        var parole = Parole(tipologia).ToList();
        bool Ha(params string[] p) => parole.Any(w => p.Any(x => w.StartsWith(x, StringComparison.Ordinal)));
        if (Ha("DATALOGGER", "LOGGER") || (parole.Contains("DATA") && Ha("LOGGER"))) return TipoTermometro.Datalogger;
        if (Ha("PIROMETR", "INFRAROSS", "IR")) return TipoTermometro.Pirometro;
        if (Ha("GABBIA")) return TipoTermometro.Gabbia;
        if (Ha("SONDA", "SONDE")) return TipoTermometro.Sonda;
        return TipoTermometro.NonIndicato;
    }

    /// <summary>Tipo della taratura: quello indicato, altrimenti ricavato dalla tipologia.</summary>
    public static TipoTermometro Tipo(TermometroEsterno t) =>
        t.TipoTermometro != TipoTermometro.NonIndicato ? t.TipoTermometro : TipoDaTipologia(t.Tipologia);

    /// <summary>
    /// Stessa tipologia: si confronta la prima parola («SONDA» = «Sonda Testo 905»), perché le tarature fatte
    /// in Laboratorio AI hanno come tipologia l'identificazione dello strumento.
    /// </summary>
    public static bool StessaTipologia(string? a, string? b)
    {
        var x = Parole(a).FirstOrDefault();
        var y = Parole(b).FirstOrDefault();
        return x is not null && x == y;
    }

    /// <summary>
    /// Tarature che la nuova potrebbe rinnovare: stesso cliente e stessa tipologia, ancora in uso, eseguite prima
    /// (o senza data), escluse la nuova stessa e quelle eliminate. Le più recenti per prime.
    /// </summary>
    public static IReadOnlyList<TermometroEsterno> Candidati(TermometroEsterno nuova, IEnumerable<TermometroEsterno> archivio) =>
        archivio.Where(t => t.IdTermometroEsterno != nuova.IdTermometroEsterno
                            && t.Attivo && t.DataCancellazione is null
                            && Stato(t) == InUso
                            && (t.DataTaratura is null || nuova.DataTaratura is null || t.DataTaratura.Value.Date <= nuova.DataTaratura.Value.Date)
                            && StessoCliente(t.Cliente, nuova.Cliente)
                            && StessoTipo(t, nuova))
            .OrderByDescending(t => t.DataTaratura)
            .ToList();

    /// <summary>Stesso tipo di termometro: col tipo indicato (o ricavato) su entrambe si confronta quello, altrimenti la tipologia.</summary>
    public static bool StessoTipo(TermometroEsterno a, TermometroEsterno b)
    {
        var ta = Tipo(a); var tb = Tipo(b);
        if (ta != TipoTermometro.NonIndicato && tb != TipoTermometro.NonIndicato) return ta == tb;
        return StessaTipologia(a.Tipologia, b.Tipologia);
    }

    private static IEnumerable<string> Parole(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) yield break;
        var sb = new StringBuilder();
        foreach (var c in testo.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            else if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
        }
        if (sb.Length > 0) yield return sb.ToString();
    }
}
