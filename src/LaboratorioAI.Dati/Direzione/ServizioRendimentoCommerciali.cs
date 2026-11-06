using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Provvigioni;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Direzione;

/// <summary>
/// Rendimento dei commerciali (cruscotto Direzione, fase 3, 13/10/2026). Raccoglie i dati già presenti in LabAI
/// (contabilità analitica e fatture AZW importate, provvigioni calcolate) e li passa a <see cref="MotoreRendimentoCommerciali"/>.
/// Solo lettura.
/// </summary>
public class ServizioRendimentoCommerciali
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly ServizioProvvigioniLab _provvigioni;

    public ServizioRendimentoCommerciali(ContestoLaboratorioAI ctx, ServizioProvvigioniLab provvigioni)
    {
        _ctx = ctx;
        _provvigioni = provvigioni;
    }

    public sealed record Risultato(int[] Anni, int AnnoRiferimento, IReadOnlyList<RendimentoCommerciale> Commerciali);

    /// <param name="idCommerciale">Un solo commerciale, oppure tutti quelli attivi.</param>
    public async Task<Risultato> CalcolaAsync(int? idCommerciale, int? dal, int? al, CancellationToken ct = default)
    {
        var annoMax = DateTime.Today.Year;
        var commerciali = await _ctx.Commerciali.AsNoTracking()
            .Where(c => idCommerciale == null ? c.Attivo : c.IdCommerciale == idCommerciale)
            .OrderBy(c => c.Nome).ToListAsync(ct);

        var vociAzw = await _ctx.ClassificazioniVoci.AsNoTracking().ToListAsync(ct);
        var vociCosto = commerciali.ToDictionary(c => c.IdCommerciale, c =>
            MotoreRendimentoCommerciali.TrovaVoceCosto(c.Nome, vociAzw.Select(v => (v.CodiceVoce, v.Descrizione, v.Ricavo))));
        var codiciVoce = vociCosto.Values.Where(v => v is not null).Select(v => v!.Value.Codice).ToList();

        // Costo del personale per voce, anno e centro.
        var costi = await _ctx.MovimentiAnalitici.AsNoTracking().Where(m => codiciVoce.Contains(m.Voce))
            .GroupBy(m => new { m.Voce, m.Anno, m.Centro }).Select(g => new { g.Key.Voce, g.Key.Anno, g.Key.Centro, Importo = g.Sum(x => x.Importo) })
            .ToListAsync(ct);

        // Periodo: di norma da due anni prima del primo anno di costo (per vedere i clienti prima del suo arrivo) all'anno in corso.
        var primoCosto = costi.Count > 0 ? costi.Min(c => c.Anno) : annoMax - 3;
        var a2 = Math.Clamp(al ?? annoMax, 2018, annoMax);
        var a1 = Math.Clamp(dal ?? Math.Max(2018, primoCosto - 2), 2018, a2);
        var anni = Enumerable.Range(a1, a2 - a1 + 1).ToArray();
        var annoRif = Math.Min(a2, annoMax - 1);

        // Margine di contribuzione % per centro e anno (stesse regole degli Indicatori economici).
        var mov = (await _ctx.MovimentiAnalitici.AsNoTracking().Where(m => m.Anno >= a1 && m.Anno <= a2)
                .GroupBy(m => new { m.Anno, m.Centro, m.Voce, m.Conto })
                .Select(g => new { g.Key.Anno, g.Key.Centro, g.Key.Voce, g.Key.Conto, Importo = g.Sum(x => x.Importo) }).ToListAsync(ct))
            .Select(r => new MovimentoIndicatori(r.Anno, r.Centro, r.Voce, r.Conto, r.Importo)).ToList();
        var vociInd = vociAzw.Select(v => new VoceIndicatori(v.CodiceVoce, v.Ricavo, v.TipoCosto, v.Macrocategoria, v.MateriaPrima)).ToList();
        var centriCosto = costi.Select(c => c.Centro).Distinct().ToList();
        var marginePerCentroAnno = new Dictionary<(string, int), decimal?>();
        foreach (var centro in centriCosto)
            foreach (var i in MotoreIndicatoriDirezione.Calcola(mov, vociInd, anni, centro))
                marginePerCentroAnno[(centro, i.Anno)] = i.Fatturato > 0 ? i.MargineContribuzione / i.Fatturato : null;

        // Clienti acquisiti da un nostro cliente (14/10/2026): non contano fra i persi.
        var acquisiti = (await _ctx.ClientiTolti.AsNoTracking()
                .Where(t => !t.Annullata && t.Motivo == Core.Entita.MotivoUscitaCliente.AcquisitoDaCliente)
                .Select(t => t.CodiceCliente).ToListAsync(ct))
            .Select(c => c.Trim()).ToHashSet();

        var anagrafica = (await _ctx.AzwClienti.AsNoTracking().Select(c => new { c.CodiceCliente, c.RagioneSociale, c.Provincia, c.CodiceVenditore }).ToListAsync(ct))
            .GroupBy(c => c.CodiceCliente.Trim()).ToDictionary(g => g.Key, g => g.First());

        var risultati = new List<RendimentoCommerciale>();
        foreach (var c in commerciali)
        {
            var avvisi = new List<string>();
            var voce = vociCosto[c.IdCommerciale];
            if (voce is null) avvisi.Add($"Nessuna voce di costo AZW con il nome «{c.Nome}»: il costo dello stipendio è zero (in AZW serve una voce di personale intestata alla persona).");
            var costiC = voce is null ? [] : costi.Where(x => x.Voce == voce.Value.Codice).ToList();
            var aree = MotoreRendimentoCommerciali.Pesi(costiC.Select(x => (x.Centro, x.Importo)));
            int? annoInizio = costiC.Where(x => x.Importo != 0).Select(x => (int?)x.Anno).Min();

            // Provvigioni e portato dal modulo provvigioni (stesso calcolo della pagina Provvigioni).
            var calcolo = await _provvigioni.CalcolaAsync(c.IdCommerciale, ct: ct);
            avvisi.AddRange(calcolo.Avvisi.Select(a => "Provvigioni: " + a));
            var movimenti = calcolo.Movimenti;
            var basi = movimenti.Where(m => m.Tipo != TipoMovimento.SviluppoObiettivo).ToList();

            // Seguito: clienti con il suo codice venditore oggi in AZW.
            var codici = (await _ctx.CodiciVenditore.AsNoTracking().Where(x => x.IdCommerciale == c.IdCommerciale).Select(x => x.CodiceVenditore).ToListAsync(ct))
                .Select(x => x.Trim().ToUpperInvariant()).ToHashSet();
            var seguiti = anagrafica.Where(a => a.Value.CodiceVenditore is string v && codici.Contains(v.Trim().ToUpperInvariant())).Select(a => a.Key).ToList();
            if (codici.Count == 0) avvisi.Add("Il commerciale non ha codici venditore: il fatturato «seguito» è zero.");
            var righeSeguito = (await _ctx.FatturatoClienti.AsNoTracking().Where(x => x.Anno >= a1 - 1 && x.Anno <= a2 && seguiti.Contains(x.CodiceCliente))
                    .GroupBy(x => new { x.Anno, x.CodiceCliente }).Select(g => new { g.Key.Anno, g.Key.CodiceCliente, Importo = g.Sum(x => x.Importo) }).ToListAsync(ct))
                .Select(x => new FatturatoClienteAnno(x.Anno, x.CodiceCliente, x.Importo)).ToList();
            var seguito = MotoreTerritorio.Totale(righeSeguito, anni, acquisiti);

            var fattureSeguito = await _ctx.AzwFatture.AsNoTracking().Where(f => seguiti.Contains(f.CodiceCliente) && f.DataFattura.Year >= a1)
                .Select(f => new { f.Chiave, Anno = f.DataFattura.Year, f.Lordo }).ToListAsync(ct);
            var chiavi = fattureSeguito.Select(f => f.Chiave).ToList();
            var incassi = (await _ctx.AzwIncassi.AsNoTracking().Where(i => chiavi.Contains(i.ChiaveFattura))
                    .GroupBy(i => i.ChiaveFattura).Select(g => new { g.Key, Importo = g.Sum(x => x.ImportoLordo) }).ToListAsync(ct))
                .ToDictionary(x => x.Key, x => x.Importo);

            var lista = new List<RendimentoAnno>();
            foreach (var a in anni)
            {
                var basiAnno = basi.Where(m => m.Data.Year == a).ToList();
                var s = seguito.First(x => x.Anno == a);
                var fattAnno = fattureSeguito.Where(f => f.Anno == a).ToList();
                var margini = centriCosto.ToDictionary(x => x, x => marginePerCentroAnno.TryGetValue((x, a), out var m) ? m : null);
                lista.Add(new RendimentoAnno(a,
                    CostoStipendio: -costiC.Where(x => x.Anno == a).Sum(x => x.Importo),
                    Provvigioni: movimenti.Where(m => m.Data.Year == a).Sum(m => m.Importo) + calcolo.Premi.Where(p => p.Anno == a).Sum(p => p.Premio),
                    Portato: basiAnno.Sum(m => m.Base),
                    PortatoNuovi: basiAnno.Where(m => m.Categoria == CategoriaCliente.Nuovo).Sum(m => m.Base),
                    ClientiPortati: basiAnno.GroupBy(m => m.Cliente).Count(g => g.Sum(m => m.Base) > 0),
                    Seguito: s.Fatturato, ClientiSeguiti: s.ClientiAttivi, ClientiSeguitiNuovi: s.ClientiNuovi, ClientiSeguitiPersi: s.ClientiPersi,
                    MargineContribuzionePercento: MotoreRendimentoCommerciali.MargineMedio(aree, margini),
                    FatturatoLordoSeguito: fattAnno.Sum(f => f.Lordo),
                    IncassatoLordoSeguito: fattAnno.Sum(f => incassi.TryGetValue(f.Chiave, out var i) ? i : 0m),
                    ClientiSeguitiAcquisiti: s.ClientiAcquisiti));
            }

            var clientiPortati = basi.Where(m => m.Data.Year == annoRif || m.Data.Year == annoRif - 1)
                .GroupBy(m => m.Cliente.Trim())
                .Select(g =>
                {
                    anagrafica.TryGetValue(g.Key, out var an);
                    return new ClienteConfronto(g.Key, (an?.RagioneSociale ?? "").Trim() is { Length: > 0 } n ? n : "Cliente " + g.Key,
                        ProvinceItaliane.Esiste(an?.Provincia) ? ProvinceItaliane.Sigla(an?.Provincia) : null,
                        g.Where(m => m.Data.Year == annoRif - 1).Sum(m => m.Base), g.Where(m => m.Data.Year == annoRif).Sum(m => m.Base));
                })
                .OrderByDescending(x => x.AnnoRiferimento).ThenByDescending(x => x.AnnoPrima).ToList();

            risultati.Add(new RendimentoCommerciale(c.IdCommerciale, c.Nome, voce?.Codice, voce?.Descrizione, aree, annoInizio, lista, clientiPortati, avvisi));
        }
        return new Risultato(anni, annoRif, risultati);
    }
}
