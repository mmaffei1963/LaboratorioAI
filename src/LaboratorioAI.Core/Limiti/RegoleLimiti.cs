using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Limiti;

/// <summary>
/// Regole pure dei limiti normativi (17/10/2026): famiglia dalla matrice ArcLab, codice scheda, controllo della
/// citazione, chi può validare. Nessun accesso a database: si provano con i test.
/// </summary>
public static class RegoleLimiti
{
    /// <summary>Prefisso del codice scheda per famiglia (LEG-001, ACQ-001, MOL-001…).</summary>
    public static string Prefisso(FamigliaNormativa f) => f switch
    {
        FamigliaNormativa.Legionella => "LEG",
        FamigliaNormativa.AcqueConsumoUmano => "ACQ",
        FamigliaNormativa.Molluschi => "MOL",
        FamigliaNormativa.Alimenti => "ALI",
        FamigliaNormativa.Piscine => "PIS",
        FamigliaNormativa.Scarichi => "SCA",
        FamigliaNormativa.SuperficiAria => "SUP",
        _ => "ALT"
    };

    public static string NomeFamiglia(FamigliaNormativa f) => f switch
    {
        FamigliaNormativa.Legionella => "Legionella (acqua sanitaria)",
        FamigliaNormativa.AcqueConsumoUmano => "Acque destinate al consumo umano",
        FamigliaNormativa.Molluschi => "Molluschi bivalvi e gasteropodi vivi",
        FamigliaNormativa.Alimenti => "Alimenti",
        FamigliaNormativa.Piscine => "Piscine",
        FamigliaNormativa.Scarichi => "Scarichi",
        FamigliaNormativa.SuperficiAria => "Superfici e aria",
        _ => "Altro"
    };

    /// <summary>Nome breve della famiglia usato nel JSON dell'AI (18/10/2026: l'AI assegna la famiglia a ogni scheda).</summary>
    public static string CodiceFamiglia(FamigliaNormativa f) => f switch
    {
        FamigliaNormativa.AcqueConsumoUmano => "Acque",
        _ => f.ToString()
    };

    public static FamigliaNormativa? DaCodiceFamiglia(string? codice)
    {
        if (string.IsNullOrWhiteSpace(codice)) return null;
        foreach (var f in Enum.GetValues<FamigliaNormativa>())
            if (string.Equals(CodiceFamiglia(f), codice.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.ToString(), codice.Trim(), StringComparison.OrdinalIgnoreCase)) return f;
        return null;
    }

    /// <summary>Cosa comprende ogni famiglia, per le istruzioni all'AI.</summary>
    public static string DescrizioneFamiglia(FamigliaNormativa f) => f switch
    {
        FamigliaNormativa.Legionella => "Legionella nell'acqua sanitaria e negli impianti idrici",
        FamigliaNormativa.AcqueConsumoUmano => "acque destinate al consumo umano: rete, pozzo, ghiaccio, acque usate nelle imprese alimentari",
        FamigliaNormativa.Molluschi => "molluschi bivalvi vivi, echinodermi, tunicati e gasteropodi marini vivi (anche zone di produzione e biotossine)",
        FamigliaNormativa.Alimenti => "tutti gli altri alimenti (carni, latte e derivati, uova, prodotti della pesca diversi dai molluschi vivi, alimenti pronti, per lattanti, vegetali, ecc.)",
        FamigliaNormativa.Piscine => "acque di piscina",
        FamigliaNormativa.Scarichi => "scarichi e acque reflue",
        FamigliaNormativa.SuperficiAria => "superfici e aria degli ambienti",
        _ => "altro"
    };

    /// <summary>
    /// Famiglie scelte per un'estrazione in coda, scritte nell'esito «In coda (famiglie: 3,4)» così il servizio in
    /// background le ritrova senza un campo in più nel database. Null = tutte.
    /// </summary>
    public static string TestoCoda(IReadOnlyCollection<FamigliaNormativa>? famiglie) =>
        famiglie is { Count: > 0 } ? $"In coda (famiglie: {string.Join(",", famiglie.Select(f => (int)f))})" : "In coda";

    public static List<FamigliaNormativa>? FamiglieDaCoda(string? esito)
    {
        if (string.IsNullOrEmpty(esito)) return null;
        var i = esito.IndexOf("famiglie:", StringComparison.Ordinal);
        if (i < 0) return null;
        var parte = new string(esito[(i + 9)..].TakeWhile(c => char.IsDigit(c) || c == ',' || c == ' ').ToArray());
        var l = parte.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var n) ? n : -1).Where(n => Enum.IsDefined(typeof(FamigliaNormativa), n))
            .Select(n => (FamigliaNormativa)n).Distinct().ToList();
        return l.Count > 0 ? l : null;
    }

    /// <summary>Famiglie della fase 1 (42% delle prove 2023-2026).</summary>
    public static readonly FamigliaNormativa[] FamiglieFase1 =
        [FamigliaNormativa.Legionella, FamigliaNormativa.AcqueConsumoUmano, FamigliaNormativa.Molluschi];

    /// <summary>
    /// Famiglia normativa dal nome della matrice ArcLab: stessa regola dell'analisi del perimetro (16/10/2026).
    /// </summary>
    public static FamigliaNormativa FamigliaDaMatrice(string? matrice)
    {
        var m = (matrice ?? "").Trim().ToLowerInvariant();
        if (m == "acqua sanitaria") return FamigliaNormativa.Legionella;
        if (m is "acqua destinata al consumo umano" or "ghiaccio" or "acqua di pozzo" or "acqua"
            or "acqua di processo" or "acqua di rete (tecnologica)") return FamigliaNormativa.AcqueConsumoUmano;
        if (m.Contains("molluschi bivalvi vivi") || m.Contains("gasteropodi vivi")) return FamigliaNormativa.Molluschi;
        if (m.Contains("piscina")) return FamigliaNormativa.Piscine;
        if (m.Contains("reflu") || m.Contains("scarico")) return FamigliaNormativa.Scarichi;
        if (m.Contains("superfic") || m.Contains("aria") || m.Contains("petri")) return FamigliaNormativa.SuperficiAria;
        if (m.StartsWith("acqua") || m.Contains("falda") || m.Contains("mangim") || m.Contains("controllo qualit")
            || m == "altri" || m.Contains("allergeni") || m.Length == 0) return FamigliaNormativa.Altro;
        return FamigliaNormativa.Alimenti;
    }

    /// <summary>Prossimo codice libero della famiglia, dati i codici già usati (anche di schede respinte o superate).</summary>
    public static string ProssimoCodice(FamigliaNormativa f, IEnumerable<string> codiciEsistenti)
    {
        var pre = Prefisso(f) + "-";
        var max = 0;
        foreach (var c in codiciEsistenti)
            if (c.StartsWith(pre, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(c[pre.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > max)
                max = n;
        return pre + (max + 1).ToString("D3", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Testo normalizzato per il confronto delle citazioni: minuscolo, senza accenti, apostrofi e virgolette unificati,
    /// trattini e spazi (anche a capo e spazi non separabili) ridotti a uno spazio. Serve perché il testo estratto da
    /// PDF e HTML va a capo e usa caratteri tipografici diversi da quelli che l'AI riscrive.
    /// </summary>
    public static string NormalizzaTesto(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        var spazio = false;
        foreach (var ch0 in s.Normalize(NormalizationForm.FormD))
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch0);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            var ch = ch0 switch
            {
                '‘' or '’' or '´' or '`' => '\'',
                '“' or '”' or '«' or '»' => '"',
                '‐' or '‑' or '‒' or '–' or '—' or '−' => '-',
                'µ' => 'μ',
                _ => ch0
            };
            if (char.IsWhiteSpace(ch) || ch == ' ' || ch == '­')
            {
                if (ch == '­') continue;          // trattino morbido: si ignora
                if (!spazio && sb.Length > 0) { sb.Append(' '); spazio = true; }
                continue;
            }
            // «-» a fine riga che spezza una parola (es. «concen-\ntrazione») lo toglie il confronto compatto
            sb.Append(char.ToLowerInvariant(ch));
            spazio = false;
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Forma senza spazi né punteggiatura: tollera a capo, trattini di sillabazione, punti delle migliaia.</summary>
    public static string Compatta(string normalizzato)
    {
        var sb = new StringBuilder(normalizzato.Length);
        foreach (var ch in normalizzato)
            if (char.IsLetterOrDigit(ch) || ch is '<' or '>' or '≤' or '≥' or '%') sb.Append(ch);
        return sb.ToString();
    }

    /// <summary>
    /// La citazione è presente nel testo salvato della fonte? Prima il confronto normalizzato, poi quello compatto
    /// (senza spazi e punteggiatura). Citazioni troppo corte (meno di 10 caratteri utili, o senza cifre o senza un nome) non valgono come prova.
    /// </summary>
    public static bool CitazionePresente(string? citazione, string testoNormalizzato, string testoCompatto)
    {
        var c = NormalizzaTesto(citazione).Trim('"', '\'', ' ', '.', '…');
        if (c.EndsWith("...")) c = c[..^3].TrimEnd();
        var cc = Compatta(c);
        // 18/10/2026: righe di tabella brevi («Antimonio | 10 | μg/l» = 14 caratteri utili) sono valide: bastano
        // 10 caratteri con almeno una cifra e quattro lettere (il nome del parametro). Sotto, non provano nulla.
        // 18/10/2026: righe ancora più corte valgono solo se dopo il valore c'è l'unità («Rame | 2,0 | mg/l»):
        // «Nitrati | 15» (una percentuale di prestazione del metodo) non è una prova.
        if (!cc.Any(char.IsDigit) || cc.Count(char.IsLetter) < 4) return false;
        var lettereDopoValore = cc.Length - 1 - cc.LastIndexOf(cc.Last(char.IsDigit));
        if (cc.Length < 10 && !(cc.Length >= 7 && lettereDopoValore >= 2)) return false;
        if (testoNormalizzato.Contains(c, StringComparison.Ordinal) || testoCompatto.Contains(cc, StringComparison.Ordinal)) return true;
        return CitazioneATratti(c, testoCompatto);
    }

    /// <summary>
    /// Citazione di una riga di tabella o con parti omesse («Salmonella | 5 | 0 | Assente in 25 g», «… »): ogni tratto
    /// (almeno 3 caratteri utili) deve trovarsi nel testo, nell'ordine, entro 1.500 caratteri dal primo.
    /// </summary>
    private static bool CitazioneATratti(string citazioneNormalizzata, string testoCompatto)
    {
        var tratti = citazioneNormalizzata.Split(new[] { '|', '…', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(t => t.Split("...", StringSplitOptions.RemoveEmptyEntries))
            .Select(Compatta).Where(t => t.Length >= 3).ToList();
        if (tratti.Count < 2) return false;
        // si prova da ogni posizione in cui compare il primo tratto
        for (var inizio = testoCompatto.IndexOf(tratti[0], StringComparison.Ordinal); inizio >= 0;
             inizio = testoCompatto.IndexOf(tratti[0], inizio + 1, StringComparison.Ordinal))
        {
            var pos = inizio + tratti[0].Length;
            var ok = true;
            foreach (var t in tratti.Skip(1))
            {
                var i = testoCompatto.IndexOf(t, pos, StringComparison.Ordinal);
                if (i < 0 || i - inizio > 1500) { ok = false; break; }
                pos = i + t.Length;
            }
            if (ok) return true;
        }
        return false;
    }

    /// <summary>Impronta SHA-256 del testo normalizzato: due letture uguali danno la stessa impronta.</summary>
    public static string Impronta(string testo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizzaTesto(testo))));

    /// <summary>
    /// Può validare? Serve il permesso e non deve essere chi ha proposto o modificato la scheda (§ 6.4 della procedura).
    /// Le proposte dell'AI non hanno un utente proponente: le può validare qualunque validatore.
    /// </summary>
    public static string? MotivoNonValidabile(LimiteNormativo s, int idUtente)
    {
        if (s.Stato is not (StatoSchedaLimite.Proposta or StatoSchedaLimite.DaRiverificare))
            return "Si validano solo le schede «Proposta» o «Da riverificare».";
        if (s.IdUtenteProposta == idUtente)
            return "Hai proposto tu questa scheda: deve validarla un altro validatore.";
        if (s.IdUtenteModifica == idUtente)
            return "Hai modificato tu questa scheda: deve validarla un altro validatore.";
        return null;
    }

    /// <summary>Simbolo dell'operatore per elenchi e dettagli.</summary>
    public static string Descrivi(LimiteNormativo s)
    {
        static string N(decimal? v) => v is decimal d ? d.ToString("#,##0.######", CultureInfo.GetCultureInfo("it-IT")) : "?";
        var u = string.IsNullOrWhiteSpace(s.Unita) ? "" : " " + s.Unita;
        var baseTesto = s.Operatore switch
        {
            OperatoreLimite.ValoreMassimo => $"≤ {N(s.Valore)}{u}",
            OperatoreLimite.MinoreDi => $"< {N(s.Valore)}{u}",
            OperatoreLimite.ValoreMinimo => $"≥ {N(s.Valore)}{u}",
            OperatoreLimite.Intervallo => $"{N(s.Valore)} – {N(s.ValoreMax)}{u}",
            OperatoreLimite.AssenteIn => $"assente in {s.Unita ?? "?"}",
            _ => "?"
        };
        if (s.RegolaValutazione == RegolaValutazioneLimite.PianoCampionamento)
            baseTesto += $" (n={s.N?.ToString() ?? "?"}, c={s.C?.ToString() ?? "?"}, m={N(s.M)}, M={N(s.MMaiuscola)})";
        return baseTesto;
    }

    public static string NomeTipo(TipoLimiteNormativo t) => t switch
    {
        TipoLimiteNormativo.Sicurezza => "Limite di sicurezza",
        TipoLimiteNormativo.IgieneProcesso => "Igiene di processo",
        TipoLimiteNormativo.Indicatore => "Parametro indicatore",
        TipoLimiteNormativo.LineaGuida => "Linea guida",
        _ => "Riferimento"
    };

    public static string NomeStato(StatoSchedaLimite s) => s switch
    {
        StatoSchedaLimite.Proposta => "Proposta",
        StatoSchedaLimite.Validata => "Validata",
        StatoSchedaLimite.DaRiverificare => "Da riverificare",
        StatoSchedaLimite.Respinta => "Respinta",
        _ => "Superata"
    };

    /// <summary>Chiave della categoria normativa (fase 1b): testo normalizzato e compattato, al massimo 500 caratteri.</summary>
    public static string ChiaveCategoria(string? matriceCategoria)
    {
        var c = Compatta(NormalizzaTesto(matriceCategoria));
        return c.Length > 500 ? c[..500] : c;
    }

    /// <summary>Famiglie in cui la categoria si ricava dalla sola famiglia della matrice (nessun abbinamento a mano).</summary>
    public static bool CategoriaDaFamiglia(FamigliaNormativa f) => f is not FamigliaNormativa.Alimenti;
}
