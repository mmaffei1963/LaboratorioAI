using System.Globalization;

namespace LaboratorioAI.Core.Provvigioni;

/// <summary>
/// Motore di calcolo delle provvigioni del commerciale di laboratorio (Ludo).
///
/// Funzione pura: nessun accesso a database o file. Riceve fatture, incassi, note di credito
/// ed elenco "in sviluppo" già letti e restituisce i movimenti. Questo lo rende testabile e
/// riproducibile: lo stesso input dà sempre lo stesso risultato (requisito per la chiusura del trimestre).
///
/// Regole (analisi-modulo-provvigioni §3):
///  - Base: quota di perimetro dell'incasso = incasso lordo × netto di laboratorio ÷ lordo della fattura.
///  - Categoria cliente alla data della fattura: Nuovo (primo anno dalla prima fattura, dopo ≥ 24 mesi di pausa),
///    Mantenimento (dal 13° mese), Esistente (rapporto iniziato prima dell'accordo).
///  - Una sola voce per cliente: 1 = 5%, 2 = 3%, 3 = 2% solo per Esistente in elenco e a obiettivo raggiunto.
///  - Note di credito: dedotte nel trimestre della nota, con l'aliquota della categoria alla data della nota.
///  - Premio annuale: incassato dell'anno dai clienti Nuovi (al netto delle note), a gradino.
/// </summary>
public sealed class MotoreProvvigioniLab
{
    public RisultatoProvvigioniLab Calcola(InputProvvigioniLab input)
    {
        var regole = input.Regole;
        var avvisi = new List<string>();
        var movimenti = new List<MovimentoProvvigione>();

        var fatture = input.Fatture.ToDictionary(f => f.ChiaveFattura, StringComparer.Ordinal);
        var inizi = CalcolaInizioRapporti(input.Fatture, regole);

        // Voce 4: per cliente, inizio dei servizi extra laboratorio procurati (prima commessa con il suo riferimento).
        var inizioExtra = (input.IncarichiExtra ?? Array.Empty<IncaricoExtraLaboratorio>())
            .GroupBy(x => x.Cliente, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Min(x => x.DataInizio.Date), StringComparer.Ordinal);
        bool InFinestraExtra(string cliente, DateTime data) =>
            regole.AliquotaExtraLaboratorio > 0m
            && inizioExtra.TryGetValue(cliente, out var da)
            && data.Date >= da && data.Date < da.AddMonths(regole.MesiExtraLaboratorio);

        // ── Incassi → voci 1 e 2 ──────────────────────────────────────
        // Gli incassi per cliente e categoria servono anche a voce 3 e premio; la quota si calcola per voce,
        // perché ogni voce può avere una base di calcolo diversa (incasso lordo × netto nella base ÷ lordo fattura).
        var quoteIncasso = new List<QuotaIncasso>();
        foreach (var inc in input.Incassi)
        {
            if (!fatture.TryGetValue(inc.ChiaveFattura, out var f))
            {
                avvisi.Add($"Incasso del {inc.DataIncasso:dd/MM/yyyy} senza fattura di laboratorio: {inc.ChiaveFattura}");
                continue;
            }
            if (f.LordoFattura <= 0m) { avvisi.Add($"Fattura con lordo non positivo: {f.ChiaveFattura}"); continue; }

            var categoria = Classifica(f.Cliente, f.DataFattura, inizi, regole);
            var qi = new QuotaIncasso(f, inc.DataIncasso, inc.ImportoLordo, categoria);
            quoteIncasso.Add(qi);

            if (inc.DataIncasso < regole.DataInizioCalcolo) continue;

            // Voce 4, indipendente dalla categoria di laboratorio: conta la data della fattura.
            if (InFinestraExtra(f.Cliente, f.DataFattura))
            {
                var qe = qi.Quota(VoceProvvigione.ExtraLaboratorio);
                if (qe != 0m)
                    movimenti.Add(new MovimentoProvvigione(
                        Trimestre(inc.DataIncasso), f.Cliente, f.ChiaveFattura, inc.DataIncasso,
                        TipoMovimento.Incasso, VoceProvvigione.ExtraLaboratorio, categoria,
                        Arrotonda(qe), regole.AliquotaExtraLaboratorio, Arrotonda(qe * regole.AliquotaExtraLaboratorio)));
            }
            var voce = VoceDi(categoria);
            if (voce is null) continue; // Esistente: la voce 3 si calcola a parte

            var quota = qi.Quota(voce.Value);
            if (quota == 0m) continue;   // nessuna riga della fattura nella base di questa voce
            var aliquota = AliquotaDi(voce.Value, regole);
            movimenti.Add(new MovimentoProvvigione(
                Trimestre(inc.DataIncasso), f.Cliente, f.ChiaveFattura, inc.DataIncasso,
                TipoMovimento.Incasso, voce.Value, categoria,
                Arrotonda(quota), aliquota, Arrotonda(quota * aliquota)));
        }

        // ── Note di credito ───────────────────────────────────────────
        var noteClassificate = new List<(NotaCreditoLab Nota, CategoriaCliente Categoria)>();
        foreach (var n in input.NoteCredito)
        {
            var categoria = Classifica(n.Cliente, n.Data, inizi, regole);
            noteClassificate.Add((n, categoria));
            if (n.Data < regole.DataInizioCalcolo) continue;
            if (InFinestraExtra(n.Cliente, n.Data))
            {
                var ne = n.NettoDi(VoceProvvigione.ExtraLaboratorio);
                if (ne != 0m)
                {
                    var be = -Math.Abs(ne);
                    movimenti.Add(new MovimentoProvvigione(
                        Trimestre(n.Data), n.Cliente, n.Riferimento, n.Data,
                        TipoMovimento.NotaCredito, VoceProvvigione.ExtraLaboratorio, categoria,
                        Arrotonda(be), regole.AliquotaExtraLaboratorio, Arrotonda(be * regole.AliquotaExtraLaboratorio)));
                }
            }
            var voce = VoceDi(categoria);
            if (voce is null) continue;
            var nettoNota = n.NettoDi(voce.Value);
            if (nettoNota == 0m) continue;
            var aliquota = AliquotaDi(voce.Value, regole);
            var baseNota = -Math.Abs(nettoNota);
            movimenti.Add(new MovimentoProvvigione(
                Trimestre(n.Data), n.Cliente, n.Riferimento, n.Data,
                TipoMovimento.NotaCredito, voce.Value, categoria,
                Arrotonda(baseNota), aliquota, Arrotonda(baseNota * aliquota)));
        }

        // ── Voce 3: sviluppo, solo a obiettivo raggiunto ──────────────
        var avanzamenti = new List<AvanzamentoObiettivo>();
        foreach (var ob in input.Obiettivi)
        {
            var (mov, avanzamento) = CalcolaSviluppo(ob, quoteIncasso, noteClassificate, regole);
            movimenti.AddRange(mov);
            avanzamenti.Add(avanzamento);
        }

        // ── Premio annuale ────────────────────────────────────────────
        var premi = CalcolaPremi(quoteIncasso, noteClassificate, regole);

        var ordinati = movimenti
            .OrderBy(m => m.Data).ThenBy(m => m.Cliente, StringComparer.Ordinal).ThenBy(m => m.Riferimento, StringComparer.Ordinal)
            .ToList();
        return new RisultatoProvvigioniLab(ordinati, premi, avvisi, avanzamenti);
    }

    // ── Storia dei rapporti ───────────────────────────────────────────

    /// <summary>Per ogni cliente, le date di inizio dei rapporti: nuovo inizio dopo una pausa di oltre MesiPausa mesi.</summary>
    internal static Dictionary<string, List<DateTime>> CalcolaInizioRapporti(IEnumerable<FatturaLab> fatture, RegoleProvvigioniLab regole)
    {
        var risultato = new Dictionary<string, List<DateTime>>(StringComparer.Ordinal);
        foreach (var gruppo in fatture.Where(f => f.NettoPerimetro > 0m).GroupBy(f => f.Cliente, StringComparer.Ordinal))
        {
            var date = gruppo.Select(f => f.DataFattura.Date).Distinct().OrderBy(d => d).ToList();
            var inizi = new List<DateTime> { date[0] };
            var precedente = date[0];
            foreach (var d in date.Skip(1))
            {
                if (d > precedente.AddMonths(regole.MesiPausa)) inizi.Add(d);
                precedente = d;
            }
            risultato[gruppo.Key] = inizi;
        }
        return risultato;
    }

    internal static CategoriaCliente Classifica(string cliente, DateTime data, Dictionary<string, List<DateTime>> inizi, RegoleProvvigioniLab regole)
    {
        if (!inizi.TryGetValue(cliente, out var lista)) return CategoriaCliente.Esistente;
        DateTime? inizio = null;
        foreach (var s in lista) if (s <= data.Date) inizio = s;
        if (inizio is null || inizio.Value < regole.DataInizioAccordo) return CategoriaCliente.Esistente;

        var mesi = (data.Year - inizio.Value.Year) * 12 + data.Month - inizio.Value.Month - (data.Day < inizio.Value.Day ? 1 : 0);
        return mesi < regole.MesiPrimoAnno ? CategoriaCliente.Nuovo : CategoriaCliente.Mantenimento;
    }

    private static VoceProvvigione? VoceDi(CategoriaCliente c) => c switch
    {
        CategoriaCliente.Nuovo => VoceProvvigione.Nuovo,
        CategoriaCliente.Mantenimento => VoceProvvigione.Mantenimento,
        _ => null
    };

    private static decimal AliquotaDi(VoceProvvigione v, RegoleProvvigioniLab r) => v switch
    {
        VoceProvvigione.Nuovo => r.AliquotaNuovo,
        VoceProvvigione.Mantenimento => r.AliquotaMantenimento,
        _ => r.AliquotaSviluppo
    };

    // ── Voce 3 ────────────────────────────────────────────────────────

    /// <summary>
    /// Il 2% matura sull'incassato dell'anno (dal trimestre di inserimento) del cliente in elenco, solo se la somma
    /// cumulata raggiunge l'obiettivo concordato. Per trimestre: target = (cumulato ≥ obiettivo) ? cumulato × 2% : 0;
    /// movimento = target − target del trimestre precedente (può essere negativo se una nota di credito riporta sotto soglia).
    /// Conta solo la quota di fatture di clienti "Esistente": per i clienti nuovi valgono le voci 1 e 2.
    /// </summary>
    private static (List<MovimentoProvvigione> Movimenti, AvanzamentoObiettivo Avanzamento) CalcolaSviluppo(
        ObiettivoSviluppo ob,
        List<QuotaIncasso> incassi,
        List<(NotaCreditoLab Nota, CategoriaCliente Categoria)> note,
        RegoleProvvigioniLab regole)
    {
        const VoceProvvigione v = VoceProvvigione.Sviluppo;
        var movimenti = new List<MovimentoProvvigione>();
        var inizio = new DateTime(ob.Anno, 1, 1);
        if (ob.ValidoDal > inizio) inizio = ob.ValidoDal.Date;
        var fine = new DateTime(ob.Anno, 12, 31);

        var inc = incassi.Where(i => i.Fattura.Cliente == ob.Cliente && i.Categoria == CategoriaCliente.Esistente
                                     && i.Data.Date >= inizio && i.Data.Date <= fine).ToList();
        var nc = note.Where(n => n.Nota.Cliente == ob.Cliente && n.Categoria == CategoriaCliente.Esistente
                                 && n.Nota.Data.Date >= inizio && n.Nota.Data.Date <= fine).ToList();

        decimal targetPrecedente = 0m;
        decimal cumulatoPrecedente = 0m;
        for (var q = 1; q <= 4; q++)
        {
            var finoA = new DateTime(ob.Anno, q * 3, DateTime.DaysInMonth(ob.Anno, q * 3));
            if (finoA < inizio) continue;
            var cumulato = inc.Where(i => i.Data.Date <= finoA).Sum(i => i.Quota(v))
                         - nc.Where(n => n.Nota.Data.Date <= finoA).Sum(n => Math.Abs(n.Nota.NettoDi(v)));
            var target = cumulato >= ob.Obiettivo ? Arrotonda(cumulato * regole.AliquotaSviluppo) : 0m;
            var delta = target - targetPrecedente;
            if (delta != 0m && finoA >= regole.DataInizioCalcolo)
            {
                movimenti.Add(new MovimentoProvvigione(
                    $"{ob.Anno}-T{q}", ob.Cliente, $"Obiettivo {ob.Anno}", finoA,
                    TipoMovimento.SviluppoObiettivo, VoceProvvigione.Sviluppo, CategoriaCliente.Esistente,
                    Arrotonda(cumulato - cumulatoPrecedente), regole.AliquotaSviluppo, delta));
            }
            targetPrecedente = target;
            cumulatoPrecedente = cumulato;
        }

        var cumulatoFinale = inc.Sum(i => i.Quota(v)) - nc.Sum(n => Math.Abs(n.Nota.NettoDi(v)));
        var raggiunto = cumulatoFinale >= ob.Obiettivo;
        var avanzamento = new AvanzamentoObiettivo(
            ob.Cliente, ob.Anno, ob.Obiettivo, Arrotonda(cumulatoFinale), raggiunto,
            raggiunto ? Arrotonda(cumulatoFinale * regole.AliquotaSviluppo) : 0m);
        return (movimenti, avanzamento);
    }

    // ── Premio ────────────────────────────────────────────────────────

    private static List<RisultatoPremio> CalcolaPremi(
        List<QuotaIncasso> incassi,
        List<(NotaCreditoLab Nota, CategoriaCliente Categoria)> note,
        RegoleProvvigioniLab regole)
    {
        // Il premio usa la base della voce Nuovo.
        const VoceProvvigione v = VoceProvvigione.Nuovo;
        var anni = incassi.Where(i => i.Categoria == CategoriaCliente.Nuovo && i.Data >= regole.DataInizioCalcolo)
                          .Select(i => i.Data.Year).Distinct().OrderBy(y => y).ToList();
        var premi = new List<RisultatoPremio>();
        foreach (var anno in anni)
        {
            var baseIncasso = incassi.Where(i => i.Categoria == CategoriaCliente.Nuovo && i.Data.Year == anno).Sum(i => i.Quota(v))
                            - note.Where(n => n.Categoria == CategoriaCliente.Nuovo && n.Nota.Data.Year == anno).Sum(n => Math.Abs(n.Nota.NettoDi(v)));
            decimal premio = 0m;
            decimal? prossima = null;
            foreach (var s in regole.SoglieEPremi.OrderBy(s => s.Soglia))
            {
                var soglia = s.Soglia * regole.FattoreOrario;
                if (baseIncasso >= soglia) premio = Arrotonda(s.Premio * regole.FattoreOrario);
                else { prossima = soglia; break; }
            }
            premi.Add(new RisultatoPremio(anno, Arrotonda(baseIncasso), premio, prossima));
        }
        return premi;
    }

    // ── Utilità ───────────────────────────────────────────────────────

    /// <summary>Incasso di una fattura con la categoria del cliente; la quota dipende dalla base della voce.</summary>
    private sealed record QuotaIncasso(FatturaLab Fattura, DateTime Data, decimal ImportoLordo, CategoriaCliente Categoria)
    {
        public decimal Quota(VoceProvvigione voce) => ImportoLordo * Fattura.NettoDi(voce) / Fattura.LordoFattura;
    }

    /// <summary>
    /// Arrotondamento al centesimo "al pari" (bancario): sui molti incassi con importi tondi il 5% cade spesso su mezzi
    /// centesimi, e arrotondare sempre per eccesso gonfierebbe il totale di qualche decina di centesimi.
    /// </summary>
    private static decimal Arrotonda(decimal v) => Math.Round(v, 2, MidpointRounding.ToEven);

    private static string Trimestre(DateTime d) =>
        string.Create(CultureInfo.InvariantCulture, $"{d.Year}-T{(d.Month - 1) / 3 + 1}");
}
