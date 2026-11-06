namespace LaboratorioAI.Core.Direzione;

public enum LivelloTerritorio { Regione = 1, Provincia = 2, Comune = 3 }

/// <summary>Fatturato di un cliente in un anno (già filtrato per area, voce, commessa…).</summary>
public record FatturatoClienteAnno(int Anno, string CodiceCliente, decimal Importo);

/// <summary>Sede del cliente (anagrafica AZW).</summary>
public record SedeCliente(string? Provincia, string? Comune);

/// <summary>Valori di una zona in un anno.</summary>
/// <param name="ClientiAcquisiti">Clienti usciti perché acquisiti da un altro nostro cliente (14/10/2026): non contano fra i persi.</param>
public record ValoriZonaAnno(int Anno, decimal Fatturato, int ClientiAttivi, int ClientiNuovi, int ClientiPersi, int ClientiAcquisiti = 0);

/// <summary>Una zona (regione, provincia o comune) con i valori per anno.</summary>
public record ZonaTerritorio(string Chiave, string Etichetta, string? Regione, IReadOnlyList<ValoriZonaAnno> Anni)
{
    public decimal Totale => Anni.Sum(a => a.Fatturato);
}

/// <summary>Gruppo del confronto «sede di Pesaro» con la crescita media annua prima e dopo l'apertura.</summary>
public record GruppoConfronto(string Nome, IReadOnlyList<ValoriZonaAnno> Anni, decimal? CrescitaPrima, decimal? CrescitaDopo);

/// <summary>
/// Andamento del fatturato per territorio (cruscotto Direzione, 13/10/2026), funzione pura. Il territorio è la sede del
/// cliente in AZW (ragionamento per azienda, non per insediamento). Un cliente è «attivo» in un anno se il suo
/// fatturato netto dell'anno è positivo; «nuovo» se attivo e non attivo l'anno prima; «perso» se attivo l'anno prima
/// e non in questo.
/// </summary>
public static class MotoreTerritorio
{
    public const string NonLegato = "Non legato a un cliente";

    /// <param name="righe">Fatturato per cliente e anno; per calcolare nuovi e persi servono anche i dati dell'anno prima del primo.</param>
    /// <param name="acquisiti">Clienti tolti dalla lista perché acquisiti da un nostro cliente: escono come «acquisiti», non come persi.</param>
    public static IReadOnlyList<ZonaTerritorio> Calcola(IEnumerable<FatturatoClienteAnno> righe,
        IReadOnlyDictionary<string, SedeCliente> sedi, IReadOnlyList<int> anni, LivelloTerritorio livello, IReadOnlySet<string>? acquisiti = null)
    {
        var perCliente = Somma(righe);
        string Chiave(string cliente)
        {
            if (cliente.Length == 0) return NonLegato;
            sedi.TryGetValue(cliente, out var s);
            var prov = ProvinceItaliane.Sigla(s?.Provincia);
            return livello switch
            {
                LivelloTerritorio.Regione => ProvinceItaliane.Regione(prov),
                LivelloTerritorio.Provincia => ProvinceItaliane.Esiste(prov) ? prov : ProvinceItaliane.Estero,
                _ => (ProvinceItaliane.Esiste(prov) ? prov : "??") + "|" + ((s?.Comune ?? "").Trim().ToUpperInvariant() is { Length: > 0 } c ? c : "(comune non indicato)")
            };
        }

        var zone = perCliente.Keys.Select(k => k.Cliente).Distinct().GroupBy(Chiave);
        var risultato = new List<ZonaTerritorio>();
        foreach (var z in zone)
        {
            var clienti = z.ToList();
            var valori = Valori(perCliente, clienti, anni, contaClienti: z.Key != NonLegato, acquisiti);
            string etichetta; string? regione = null;
            if (z.Key == NonLegato || livello == LivelloTerritorio.Regione) etichetta = z.Key;
            else if (livello == LivelloTerritorio.Provincia)
            {
                etichetta = z.Key == ProvinceItaliane.Estero ? z.Key : ProvinceItaliane.NomeProvincia(z.Key);
                regione = z.Key == ProvinceItaliane.Estero ? null : ProvinceItaliane.Regione(z.Key);
            }
            else
            {
                var p = z.Key.Split('|');
                etichetta = $"{p[1]} ({p[0]})";
                regione = ProvinceItaliane.Esiste(p[0]) ? ProvinceItaliane.Regione(p[0]) : null;
            }
            risultato.Add(new ZonaTerritorio(z.Key, etichetta, regione, valori));
        }
        return risultato.OrderByDescending(z => z.Totale).ToList();
    }

    /// <summary>Totale di tutti i clienti (riga finale).</summary>
    public static IReadOnlyList<ValoriZonaAnno> Totale(IEnumerable<FatturatoClienteAnno> righe, IReadOnlyList<int> anni, IReadOnlySet<string>? acquisiti = null)
    {
        var perCliente = Somma(righe);
        var clienti = perCliente.Keys.Select(k => k.Cliente).Where(c => c.Length > 0).Distinct().ToList();
        var conClienti = Valori(perCliente, clienti, anni, true, acquisiti);
        var nonLegato = perCliente.Where(k => k.Key.Cliente.Length == 0).GroupBy(k => k.Key.Anno).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
        return conClienti.Select(v => v with { Fatturato = v.Fatturato + (nonLegato.TryGetValue(v.Anno, out var n) ? n : 0m) }).ToList();
    }

    /// <summary>
    /// Confronto per la sede di Pesaro (aperta nel <paramref name="annoApertura"/>): Pesaro e Urbino, Marche sud (AN, MC,
    /// FM, AP) e le zone di controllo Rimini e Forlì-Cesena, con la crescita media annua dal <paramref name="annoInizio"/>
    /// all'anno prima dell'apertura e da lì all'<paramref name="ultimoAnnoCompleto"/>.
    /// </summary>
    public static IReadOnlyList<GruppoConfronto> ConfrontoPesaro(IEnumerable<FatturatoClienteAnno> righe,
        IReadOnlyDictionary<string, SedeCliente> sedi, int annoInizio, int annoApertura, int ultimoAnnoCompleto, IReadOnlySet<string>? acquisiti = null)
    {
        var gruppi = new (string Nome, string[] Province)[]
        {
            ("Pesaro e Urbino (PU)", ["PU"]),
            ("Marche sud (AN, MC, FM, AP)", ["AN", "MC", "FM", "AP"]),
            ("Rimini (RN) — confronto", ["RN"]),
            ("Forlì-Cesena (FC) — confronto", ["FC"]),
        };
        var perCliente = Somma(righe);
        var anni = Enumerable.Range(annoInizio, ultimoAnnoCompleto - annoInizio + 1).ToList();
        var risultato = new List<GruppoConfronto>();
        foreach (var (nome, province) in gruppi)
        {
            var clienti = perCliente.Keys.Select(k => k.Cliente).Distinct()
                .Where(c => c.Length > 0 && sedi.TryGetValue(c, out var s) && province.Contains(ProvinceItaliane.Sigla(s.Provincia))).ToList();
            var valori = Valori(perCliente, clienti, anni, true, acquisiti);
            decimal? Cagr(int da, int a)
            {
                var v0 = valori.FirstOrDefault(v => v.Anno == da)?.Fatturato ?? 0m;
                var v1 = valori.FirstOrDefault(v => v.Anno == a)?.Fatturato ?? 0m;
                if (v0 <= 0 || v1 <= 0 || a <= da) return null;
                return (decimal)(Math.Pow((double)(v1 / v0), 1.0 / (a - da)) - 1);
            }
            risultato.Add(new GruppoConfronto(nome, valori, Cagr(annoInizio, annoApertura - 1), Cagr(annoApertura - 1, ultimoAnnoCompleto)));
        }
        return risultato;
    }

    private static Dictionary<(string Cliente, int Anno), decimal> Somma(IEnumerable<FatturatoClienteAnno> righe)
    {
        var d = new Dictionary<(string, int), decimal>();
        foreach (var r in righe)
        {
            var k = (r.CodiceCliente.Trim(), r.Anno);
            d[k] = (d.TryGetValue(k, out var x) ? x : 0m) + r.Importo;
        }
        return d;
    }

    private static List<ValoriZonaAnno> Valori(Dictionary<(string Cliente, int Anno), decimal> perCliente, List<string> clienti,
        IReadOnlyList<int> anni, bool contaClienti, IReadOnlySet<string>? acquisiti = null)
    {
        bool Attivo(string c, int a) => perCliente.TryGetValue((c, a), out var v) && v > 0;
        var lista = new List<ValoriZonaAnno>();
        foreach (var a in anni)
        {
            decimal f = 0; int att = 0, nuovi = 0, persi = 0, acq = 0;
            foreach (var c in clienti)
            {
                if (perCliente.TryGetValue((c, a), out var v)) f += v;
                if (!contaClienti) continue;
                bool ora = Attivo(c, a), prima = Attivo(c, a - 1);
                if (ora) att++;
                if (ora && !prima) nuovi++;
                if (!ora && prima) { if (acquisiti is not null && acquisiti.Contains(c)) acq++; else persi++; }
            }
            lista.Add(new ValoriZonaAnno(a, f, att, nuovi, persi, acq));
        }
        return lista;
    }
}
