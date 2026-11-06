using LaboratorioAI.Core.Andamento;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Provvigioni;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Clienti;

/// <summary>Dati di un cliente utili alla pagina (anagrafica AZW e informazioni inserite in LabAI).</summary>
public sealed record InfoCliente
{
    public string Codice { get; init; } = "";
    public string RagioneSociale { get; init; } = "";
    public string? Citta { get; init; }
    public string? Provincia { get; init; }
    public string? Indirizzo { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
    public string? Pec { get; init; }
    public string? Zona { get; init; }
    /// <summary>Nome del responsabile cliente dalla sigla di zona (Amministrazione → Responsabili clienti).</summary>
    public string? Responsabile { get; init; }
    public string? CodiceVenditore { get; init; }
    /// <summary>Commerciale del codice venditore AZW.</summary>
    public string? CommercialeAzw { get; init; }
    /// <summary>Commerciale dell'assegnazione approvata in corso.</summary>
    public string? Assegnato { get; init; }
    public bool PropostaInAttesa { get; init; }
    public bool Intermediario { get; init; }
    public AzioneCliente? UltimaAzione { get; init; }
    /// <summary>Prossimo contatto indicato nell'ultima azione valida.</summary>
    public DateTime? ProssimoContatto { get; init; }
    /// <summary>Uscita dalla lista in essere (14/10/2026), se c'è.</summary>
    public UscitaInfo? Uscita { get; init; }
    /// <summary>Tolto dalla lista ma con attività dopo l'uscita: torna in lista con un avviso.</summary>
    public bool Rientrato { get; init; }
    /// <summary>Tolto e non rientrato: non compare nelle liste.</summary>
    public bool Tolto => Uscita is not null && !Rientrato;
    /// <summary>Clienti acquisiti da questo, il cui storico è sommato qui (ragione sociale e codice).</summary>
    public IReadOnlyList<(string Codice, string Nome)> Assorbiti { get; init; } = Array.Empty<(string, string)>();
}

/// <summary>Uscita di un cliente dalla lista, per le pagine.</summary>
public sealed record UscitaInfo(int IdClienteTolto, MotivoUscitaCliente Motivo, string? Successore, string? NomeSuccessore, DateTime Data,
    string? Da, string? Note)
{
    public string Descrizione => Motivo switch
    {
        MotivoUscitaCliente.AcquisitoDaCliente => $"acquisito da {NomeSuccessore ?? Successore}",
        MotivoUscitaCliente.CessataAttivita => "cessata attività",
        MotivoUscitaCliente.NonRecuperabile => "non recuperabile",
        _ => Note ?? "altro"
    };
}

public sealed record RiepilogoClientiDaSeguire(
    RisultatoAndamento Risultato,
    IReadOnlyDictionary<string, InfoCliente> Info,
    ImpostazioniAndamento Impostazioni,
    DateTime? UltimaImportazione,
    /// <summary>Righe della copia dei campioni ArcLab: zero se l'importazione non li ha ancora caricati.</summary>
    int RigheCampioni);

/// <summary>Un mese della scheda cliente.</summary>
public sealed record MeseScheda(int Anno, int Mese, decimal Laboratorio, decimal Altro, int Campioni, int CampioniLegionella);

/// <summary>Campioni di un controllo nel periodo di confronto e in quello attuale.</summary>
public sealed record ControlloScheda(int? IdControllo, string Controllo, int CampioniPrecedenti, int CampioniUltimi, decimal ValorePrecedenti, decimal ValoreUltimi);

public sealed record SchedaClienteDaSeguire(
    InfoCliente Info,
    AndamentoCliente? Andamento,
    AndamentoLegionella? Legionella,
    IReadOnlyList<MeseScheda> Mesi,
    IReadOnlyList<ControlloScheda> Controlli,
    IReadOnlyList<AzioneCliente> Azioni,
    IReadOnlyList<(AssegnazioneCliente Assegnazione, string Commerciale)> Assegnazioni,
    AttributiCliente? Attributi,
    ImpostazioniAndamento Impostazioni,
    int UltimoMese,
    RisultatoAndamento Risultato);

/// <summary>
/// «Clienti da seguire»: prepara i dati per il motore (fatturato AZW già importato, copia dei campioni ArcLab),
/// e gestisce registro azioni, assegnazioni, attributi dei clienti e soglie. Scrive solo nel database di LabAI.
/// </summary>
public sealed class ServizioClientiDaSeguire
{
    private readonly ContestoLaboratorioAI _db;
    private readonly MotoreAndamentoClienti _motore = new();

    public ServizioClientiDaSeguire(ContestoLaboratorioAI db) => _db = db;

    // ── Impostazioni ──────────────────────────────────────────────────

    public async Task<ImpostazioniAndamento> ImpostazioniAsync(CancellationToken ct = default)
    {
        var i = await _db.ImpostazioniAndamento.OrderBy(x => x.IdImpostazioniAndamento).FirstOrDefaultAsync(ct);
        if (i is not null) return i;
        i = new ImpostazioniAndamento { DataUltimaModifica = DateTime.UtcNow, UltimaModificaDa = "sistema" };
        _db.ImpostazioniAndamento.Add(i);
        await _db.SaveChangesAsync(ct);
        return i;
    }

    public static SoglieAndamento Soglie(ImpostazioniAndamento i) => new()
    {
        MinimoFatturato = i.MinimoFatturato,
        SogliaRosso = i.SogliaRosso / 100m,
        SogliaGiallo = i.SogliaGiallo / 100m,
        ValoreAnalisiPerse = i.ValoreAnalisiPerse,
        CampioniTrimestreMinimo = i.CampioniTrimestreMinimo,
        LegionellaTolleranzaGiorni = i.LegionellaTolleranzaGiorni,
        LegionellaPreavvisoGiorni = i.LegionellaPreavvisoGiorni,
        LegionellaSogliaCalo = i.LegionellaSogliaCalo / 100m,
        SoloConsulenzaMinimo = i.SoloConsulenzaMinimo,
        SoloConsulenzaLabMassimo = i.SoloConsulenzaLabMassimo
    };

    public async Task SalvaImpostazioniAsync(ImpostazioniAndamento valori, string? utente, CancellationToken ct = default)
    {
        var i = await ImpostazioniAsync(ct);
        i.MinimoFatturato = valori.MinimoFatturato;
        i.SogliaRosso = valori.SogliaRosso;
        i.SogliaGiallo = valori.SogliaGiallo;
        i.ValoreAnalisiPerse = valori.ValoreAnalisiPerse;
        i.CampioniTrimestreMinimo = valori.CampioniTrimestreMinimo;
        i.LegionellaTolleranzaGiorni = valori.LegionellaTolleranzaGiorni;
        i.LegionellaPreavvisoGiorni = valori.LegionellaPreavvisoGiorni;
        i.LegionellaSogliaCalo = valori.LegionellaSogliaCalo;
        i.SoloConsulenzaMinimo = valori.SoloConsulenzaMinimo;
        i.SoloConsulenzaLabMassimo = valori.SoloConsulenzaLabMassimo;
        i.TestoControlloLegionella = string.IsNullOrWhiteSpace(valori.TestoControlloLegionella) ? "Legionella" : valori.TestoControlloLegionella.Trim();
        i.VociLegionella = string.IsNullOrWhiteSpace(valori.VociLegionella) ? "LEV;LEC;LEG" : valori.VociLegionella.Trim().ToUpperInvariant();
        i.IdBaseCalcoloLaboratorio = valori.IdBaseCalcoloLaboratorio;
        i.DataUltimaModifica = DateTime.UtcNow;
        i.UltimaModificaDa = utente;
        await _db.SaveChangesAsync(ct);
    }

    // ── Calcolo ───────────────────────────────────────────────────────

    private sealed record DatiGrezzi(List<FatturatoMensile> Fatturato, List<CampioniMensili> Campioni, ImpostazioniAndamento Impostazioni,
        Dictionary<string, ClienteTolto> Uscite, HashSet<string> Rientrati, Dictionary<string, string> Destinazioni);

    /// <summary>Uscite dalla lista valide: per cliente la più recente non annullata.</summary>
    private async Task<Dictionary<string, ClienteTolto>> UsciteAsync(CancellationToken ct) =>
        (await _db.ClientiTolti.AsNoTracking().Where(t => !t.Annullata).ToListAsync(ct))
        .GroupBy(t => t.CodiceCliente, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.DataRegistrazione).ThenByDescending(t => t.IdClienteTolto).First(), StringComparer.Ordinal);

    private static UscitaCliente Uscita(ClienteTolto t) => new(t.CodiceCliente, t.Motivo, t.CodiceClienteSuccessore, t.DataUscita);

    private async Task<DatiGrezzi> DatiAsync(DateTime oggi, string? soloCliente, CancellationToken ct)
    {
        var imp = await ImpostazioniAsync(ct);
        var basi = await _db.BasiCalcolo.AsNoTracking().ToListAsync(ct);
        var baseLab = basi.FirstOrDefault(b => b.IdBaseCalcolo == imp.IdBaseCalcoloLaboratorio)
                      ?? basi.FirstOrDefault(b => b.Nome == ServizioProvvigioniLab.BaseAreaAnalisi)
                      ?? basi.FirstOrDefault(b => b.Nome == ServizioProvvigioniLab.BaseLaboratorio);
        var vociLeg = imp.VociLegionella.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);

        // Dal gennaio di tre anni prima: copre il confronto con due anni prima (anche sui 12 mesi) e il «da inizio anno».
        var dal = new DateTime(oggi.Year - 3, 1, 1);

        // Clienti tolti dalla lista (14/10/2026): per la scheda di un cliente servono anche i clienti che ha acquisito.
        var uscite = await UsciteAsync(ct);
        List<string>? codiciSolo = null;
        if (soloCliente is not null)
        {
            var possibili = UsciteClienti.Destinazioni(uscite.Values.Select(Uscita), new HashSet<string>());
            codiciSolo = possibili.Where(x => x.Value == soloCliente).Select(x => x.Key).Append(soloCliente).Distinct().ToList();
        }

        // Filtri sul cliente aggiunti solo se servono: una condizione «lista nulla OPPURE contiene» rende la query
        // sull'intero archivio molto lenta (timeout del 14/10/2026).
        var fattureBase = _db.AzwFatture.AsNoTracking().Where(f => f.DataFattura >= dal);
        var noteBase = _db.AzwRigheNoteCredito.AsNoTracking().Where(n => n.Data >= dal);
        var campioniBase = _db.CampioniMensiliArcLab.AsNoTracking().Where(c => c.Anno >= dal.Year);
        if (codiciSolo is not null)
        {
            fattureBase = fattureBase.Where(f => codiciSolo.Contains(f.CodiceCliente));
            noteBase = noteBase.Where(n => codiciSolo.Contains(n.CodiceCliente));
            campioniBase = campioniBase.Where(c => codiciSolo.Contains(c.CodiceCliente));
        }
        var fatture = from r in _db.AzwRigheFattura.AsNoTracking()
                      join f in fattureBase on r.ChiaveFattura equals f.Chiave
                      group r by new { f.CodiceCliente, f.DataFattura.Year, f.DataFattura.Month, r.Centro, r.Voce } into g
                      select new { g.Key.CodiceCliente, Anno = g.Key.Year, Mese = g.Key.Month, g.Key.Centro, g.Key.Voce, Netto = g.Sum(x => x.Netto) };
        var note = from n in noteBase
                   group n by new { n.CodiceCliente, n.Data.Year, n.Data.Month, n.Centro, n.Voce } into g
                   select new { g.Key.CodiceCliente, Anno = g.Key.Year, Mese = g.Key.Month, g.Key.Centro, g.Key.Voce, Netto = -g.Sum(x => Math.Abs(x.Netto)) };
        var righe = (await fatture.ToListAsync(ct)).Concat(await note.ToListAsync(ct));

        var fatturato = righe
            .Select(r =>
            {
                var lab = baseLab is not null && baseLab.Include(r.Centro, r.Voce);
                var voce = (r.Voce ?? "").Trim().ToUpperInvariant();
                return (r.CodiceCliente, r.Anno, r.Mese,
                    Lab: lab ? r.Netto : 0m, Leg: lab && vociLeg.Contains(voce) ? r.Netto : 0m, Altro: lab ? 0m : r.Netto);
            })
            .GroupBy(x => (x.CodiceCliente, x.Anno, x.Mese))
            .Select(g => new FatturatoMensile(g.Key.CodiceCliente, g.Key.Anno, g.Key.Mese,
                g.Sum(x => x.Lab), g.Sum(x => x.Leg), g.Sum(x => x.Altro)))
            .ToList();

        var testoLeg = imp.TestoControlloLegionella;
        var campioni = (await campioniBase.ToListAsync(ct))
            .Select(c => new CampioniMensili(c.CodiceCliente, c.Anno, c.Mese, c.IdControllo, c.Controllo, c.Campioni, c.ValoreListino,
                c.UltimoCampione, c.Controllo is not null && c.Controllo.Contains(testoLeg, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var rientrati = UsciteClienti.Rientrati(uscite.Values.Select(Uscita), fatturato, campioni);
        var destinazioni = UsciteClienti.Destinazioni(uscite.Values.Select(Uscita), rientrati);
        return new DatiGrezzi(UsciteClienti.Unisci(fatturato, destinazioni, soloCliente), UsciteClienti.Unisci(campioni, destinazioni, soloCliente),
            imp, uscite, rientrati, destinazioni);
    }

    /// <summary>
    /// Calcolo per le liste. I clienti tolti dalla lista (e non rientrati) non compaiono; quelli acquisiti da un altro cliente
    /// hanno già lo storico sommato al cliente che li ha assorbiti.
    /// </summary>
    public async Task<RiepilogoClientiDaSeguire> CalcolaAsync(DateTime oggi, FinestraConfronto? finestra = null, CancellationToken ct = default)
    {
        var d = await DatiAsync(oggi, null, ct);
        var risultato = _motore.Calcola(d.Fatturato, d.Campioni, Soglie(d.Impostazioni), oggi, finestra);
        var info = await InfoAsync(null, d, ct);
        bool Tolto(string c) => d.Uscite.ContainsKey(c) && !d.Rientrati.Contains(c);
        risultato = risultato with
        {
            Clienti = risultato.Clienti.Where(c => !Tolto(c.Cliente)).ToList(),
            Legionella = risultato.Legionella.Where(l => !Tolto(l.Cliente)).ToList()
        };
        var ultima = await _db.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita)
            .OrderByDescending(i => i.DataOra).Select(i => (DateTime?)i.DataOra).FirstOrDefaultAsync(ct);
        return new RiepilogoClientiDaSeguire(risultato, info, d.Impostazioni, ultima, d.Campioni.Count);
    }

    /// <summary>Anagrafica e dati LabAI dei clienti (di uno solo se indicato).</summary>
    private async Task<Dictionary<string, InfoCliente>> InfoAsync(string? soloCliente, DatiGrezzi d, CancellationToken ct)
    {
        var anagrafiche = await _db.AzwClienti.AsNoTracking()
            .Where(c => soloCliente == null || c.CodiceCliente == soloCliente).ToListAsync(ct);
        var responsabili = (await _db.ResponsabiliCliente.AsNoTracking().ToListAsync(ct))
            .GroupBy(r => r.Sigla.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First().Nome);
        var venditori = (await _db.AzwFatture.AsNoTracking()
                .Where(f => f.CodiceVenditore != null && f.CodiceVenditore != "" && (soloCliente == null || f.CodiceCliente == soloCliente))
                .GroupBy(f => f.CodiceCliente).Select(g => new { g.Key, Vet = g.Max(x => x.CodiceVenditore) }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Vet!, StringComparer.Ordinal);
        var commerciali = await _db.Commerciali.AsNoTracking().ToDictionaryAsync(c => c.IdCommerciale, c => c.Nome, ct);
        var oggi = DateTime.Today;
        var codici = (await _db.CodiciVenditore.AsNoTracking().ToListAsync(ct))
            .Where(c => c.ValidoDal.Date <= oggi && (c.ValidoAl == null || c.ValidoAl.Value.Date >= oggi))
            .GroupBy(c => c.CodiceVenditore.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => commerciali.TryGetValue(g.First().IdCommerciale, out var n) ? n : null);
        var assegnazioni = await _db.AssegnazioniClienti.AsNoTracking()
            .Where(a => (a.Stato == StatoAssegnazioneCliente.Approvata || a.Stato == StatoAssegnazioneCliente.Proposta)
                        && (soloCliente == null || a.CodiceCliente == soloCliente)).ToListAsync(ct);
        var attributi = (await _db.AttributiClienti.AsNoTracking()
                .Where(a => soloCliente == null || a.CodiceCliente == soloCliente).ToListAsync(ct))
            .ToDictionary(a => a.CodiceCliente, StringComparer.Ordinal);
        var ultimeAzioni = (await _db.AzioniClienti.AsNoTracking()
                .Where(a => !a.Annullata && (soloCliente == null || a.CodiceCliente == soloCliente)).ToListAsync(ct))
            .GroupBy(a => a.CodiceCliente, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.DataAzione).ThenByDescending(a => a.IdAzioneCliente).First(), StringComparer.Ordinal);

        // Nomi dei clienti coinvolti nelle uscite (successori e acquisiti), anche quando si calcola la scheda di uno solo.
        var codiciUscite = d.Uscite.Values.Select(u => u.CodiceClienteSuccessore).Concat(d.Destinazioni.Keys)
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        var nomiUscite = (await _db.AzwClienti.AsNoTracking().Where(c => codiciUscite.Contains(c.CodiceCliente))
                .Select(c => new { c.CodiceCliente, c.RagioneSociale }).ToListAsync(ct))
            .GroupBy(c => c.CodiceCliente, StringComparer.Ordinal).ToDictionary(g => g.Key, g => (g.First().RagioneSociale ?? g.Key).Trim(), StringComparer.Ordinal);
        string NomeDi(string c) => nomiUscite.TryGetValue(c, out var n) ? n : perCodiceNome(c);
        string perCodiceNome(string c) => anagrafiche.FirstOrDefault(a => a.CodiceCliente == c)?.RagioneSociale?.Trim() ?? $"Cliente {c}";

        var tutti = anagrafiche.Select(a => a.CodiceCliente)
            .Concat(assegnazioni.Select(a => a.CodiceCliente)).Concat(ultimeAzioni.Keys).Concat(attributi.Keys)
            .Concat(d.Uscite.Keys.Where(k => soloCliente == null || k == soloCliente))
            .Distinct(StringComparer.Ordinal);
        var perCodice = anagrafiche.GroupBy(a => a.CodiceCliente, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var info = new Dictionary<string, InfoCliente>(StringComparer.Ordinal);
        foreach (var cod in tutti)
        {
            perCodice.TryGetValue(cod, out var a);
            venditori.TryGetValue(cod, out var vet);
            var zona = a?.Zona?.Trim();
            var approvata = assegnazioni.FirstOrDefault(x => x.CodiceCliente == cod && x.Stato == StatoAssegnazioneCliente.Approvata);
            ultimeAzioni.TryGetValue(cod, out var azione);
            info[cod] = new InfoCliente
            {
                Codice = cod,
                RagioneSociale = a?.RagioneSociale?.Trim() ?? $"Cliente {cod}",
                Citta = a?.Citta, Provincia = a?.Provincia, Indirizzo = a?.Indirizzo,
                Telefono = a?.Telefono, Email = a?.Email, Pec = a?.Pec,
                Zona = zona,
                Responsabile = zona is { Length: > 0 } && TrovaResponsabile(zona) is { } nome ? nome : null,
                CodiceVenditore = vet,
                CommercialeAzw = vet is not null && codici.TryGetValue(vet.Trim().ToUpperInvariant(), out var nc) ? nc : null,
                Assegnato = approvata is not null && commerciali.TryGetValue(approvata.IdCommerciale, out var na) ? na : null,
                PropostaInAttesa = assegnazioni.Any(x => x.CodiceCliente == cod && x.Stato == StatoAssegnazioneCliente.Proposta),
                Intermediario = attributi.TryGetValue(cod, out var at) && at.Intermediario,
                UltimaAzione = azione,
                ProssimoContatto = azione?.ProssimoContatto,
                Uscita = d.Uscite.TryGetValue(cod, out var u)
                    ? new UscitaInfo(u.IdClienteTolto, u.Motivo, u.CodiceClienteSuccessore,
                        u.CodiceClienteSuccessore is string sc ? NomeDi(sc) : null, u.DataUscita, u.NomeUtente, u.Note)
                    : null,
                Rientrato = d.Rientrati.Contains(cod),
                Assorbiti = d.Destinazioni.Where(x => x.Value == cod).Select(x => (x.Key, NomeDi(x.Key))).OrderBy(x => x.Item2).ToList()
            };
        }
        return info;

        string? TrovaResponsabile(string sigla) => responsabili.TryGetValue(sigla.ToUpperInvariant(), out var n) ? n : null;
    }

    public async Task<SchedaClienteDaSeguire?> SchedaAsync(string codice, DateTime oggi, FinestraConfronto? finestra = null, CancellationToken ct = default)
    {
        var d = await DatiAsync(oggi, codice, ct);
        var info = (await InfoAsync(codice, d, ct)).GetValueOrDefault(codice);
        if (info is null && d.Fatturato.Count == 0 && d.Campioni.Count == 0) return null;
        info ??= new InfoCliente { Codice = codice, RagioneSociale = $"Cliente {codice}" };

        var risultato = _motore.Calcola(d.Fatturato, d.Campioni, Soglie(d.Impostazioni), oggi, finestra);
        var ultimo = risultato.UltimoMese;
        var fine = new DateTime(ultimo / 100, ultimo % 100, 1);
        var mesi = Enumerable.Range(0, 36).Select(k => fine.AddMonths(k - 35)).Select(m => new MeseScheda(m.Year, m.Month,
            d.Fatturato.Where(f => f.Anno == m.Year && f.Mese == m.Month).Sum(f => f.Laboratorio),
            d.Fatturato.Where(f => f.Anno == m.Year && f.Mese == m.Month).Sum(f => f.Altro),
            d.Campioni.Where(c => c.Anno == m.Year && c.Mese == m.Month).Sum(c => c.Campioni),
            d.Campioni.Where(c => c.Anno == m.Year && c.Mese == m.Month && c.Legionella).Sum(c => c.Campioni))).ToList();

        // Controlli nel periodo attuale e in quello di confronto scelti.
        bool InAttuale(CampioniMensili x) => x.Anno * 100 + x.Mese >= risultato.InizioPeriodo && x.Anno * 100 + x.Mese <= ultimo;
        bool InConfronto(CampioniMensili x) => x.Anno * 100 + x.Mese >= risultato.InizioConfronto && x.Anno * 100 + x.Mese <= risultato.FineConfronto;
        var controlli = d.Campioni.Where(c => InAttuale(c) || InConfronto(c))
            .GroupBy(c => c.IdControllo)
            .Select(g => new ControlloScheda(g.Key, g.Select(x => x.Controllo).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "(senza nome)",
                g.Where(InConfronto).Sum(x => x.Campioni),
                g.Where(InAttuale).Sum(x => x.Campioni),
                g.Where(InConfronto).Sum(x => x.ValoreListino),
                g.Where(InAttuale).Sum(x => x.ValoreListino)))
            .OrderByDescending(c => c.ValorePrecedenti + c.ValoreUltimi).ToList();

        var azioni = await _db.AzioniClienti.AsNoTracking().Where(a => a.CodiceCliente == codice)
            .OrderByDescending(a => a.DataAzione).ThenByDescending(a => a.IdAzioneCliente).ToListAsync(ct);
        var commerciali = await _db.Commerciali.AsNoTracking().ToDictionaryAsync(c => c.IdCommerciale, c => c.Nome, ct);
        var assegnazioni = (await _db.AssegnazioniClienti.AsNoTracking().Where(a => a.CodiceCliente == codice)
                .OrderByDescending(a => a.DataProposta).ToListAsync(ct))
            .Select(a => (a, commerciali.TryGetValue(a.IdCommerciale, out var n) ? n : $"Commerciale {a.IdCommerciale}")).ToList();
        var attributi = await _db.AttributiClienti.AsNoTracking().FirstOrDefaultAsync(a => a.CodiceCliente == codice, ct);

        return new SchedaClienteDaSeguire(info,
            risultato.Clienti.FirstOrDefault(c => c.Cliente == codice),
            risultato.Legionella.FirstOrDefault(c => c.Cliente == codice),
            mesi, controlli, azioni, assegnazioni, attributi, d.Impostazioni, ultimo, risultato);
    }

    // ── Registro azioni ───────────────────────────────────────────────

    public async Task<AzioneCliente> RegistraAzioneAsync(string codice, DateTime data, TipoAzioneCliente tipo, EsitoAzioneCliente esito,
        string? note, DateTime? prossimoContatto, int? idUtente, string? nomeUtente, CancellationToken ct = default)
    {
        var a = new AzioneCliente
        {
            CodiceCliente = codice, DataAzione = data.Date, Tipo = tipo, Esito = esito,
            Note = Tronca(note, 2000), ProssimoContatto = prossimoContatto?.Date,
            IdUtente = idUtente, NomeUtente = Tronca(nomeUtente, 100), DataCreazione = DateTime.UtcNow
        };
        _db.AzioniClienti.Add(a);
        await _db.SaveChangesAsync(ct);
        return a;
    }

    /// <summary>Annulla un'azione inserita per errore (nessuna cancellazione fisica).</summary>
    public async Task<AzioneCliente?> AnnullaAzioneAsync(int id, string? utente, CancellationToken ct = default)
    {
        var a = await _db.AzioniClienti.FirstOrDefaultAsync(x => x.IdAzioneCliente == id, ct);
        if (a is null || a.Annullata) return a;
        a.Annullata = true;
        a.AnnullataDa = Tronca(utente, 100);
        a.DataAnnullamento = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return a;
    }

    /// <summary>Promemoria: ultima azione valida di ogni cliente con un prossimo contatto entro la data indicata.</summary>
    public async Task<List<AzioneCliente>> PromemoriaAsync(DateTime entro, CancellationToken ct = default)
    {
        var azioni = await _db.AzioniClienti.AsNoTracking().Where(a => !a.Annullata).ToListAsync(ct);
        return azioni.GroupBy(a => a.CodiceCliente, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(a => a.DataAzione).ThenByDescending(a => a.IdAzioneCliente).First())
            .Where(a => a.ProssimoContatto is { } p && p.Date <= entro.Date)
            .OrderBy(a => a.ProssimoContatto).ToList();
    }

    // ── Assegnazioni ──────────────────────────────────────────────────

    /// <summary>Propone l'assegnazione di un cliente a un commerciale. Restituisce un messaggio d'errore o null.</summary>
    public async Task<string?> ProponiAssegnazioneAsync(string codice, int idCommerciale, string? motivo, decimal? fatturatoLab,
        string? utente, CancellationToken ct = default)
    {
        if (!await _db.Commerciali.AnyAsync(c => c.IdCommerciale == idCommerciale && c.Attivo, ct)) return "Commerciale non trovato.";
        var aperta = await _db.AssegnazioniClienti.AnyAsync(a => a.CodiceCliente == codice
            && (a.Stato == StatoAssegnazioneCliente.Proposta || a.Stato == StatoAssegnazioneCliente.Approvata), ct);
        if (aperta) return "Il cliente ha già un'assegnazione proposta o in corso: va prima decisa o chiusa.";
        _db.AssegnazioniClienti.Add(new AssegnazioneCliente
        {
            CodiceCliente = codice, IdCommerciale = idCommerciale, Stato = StatoAssegnazioneCliente.Proposta,
            Motivo = Tronca(motivo, 1000), FatturatoLabAllaProposta = fatturatoLab,
            DataProposta = DateTime.UtcNow, PropostaDa = Tronca(utente, 100)
        });
        await _db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<AssegnazioneCliente?> DecidiAssegnazioneAsync(int id, bool approva, string? nota, string? utente, CancellationToken ct = default)
    {
        var a = await _db.AssegnazioniClienti.FirstOrDefaultAsync(x => x.IdAssegnazioneCliente == id, ct);
        if (a is null || a.Stato != StatoAssegnazioneCliente.Proposta) return null;
        a.Stato = approva ? StatoAssegnazioneCliente.Approvata : StatoAssegnazioneCliente.Rifiutata;
        a.DataDecisione = DateTime.UtcNow;
        a.DecisaDa = Tronca(utente, 100);
        a.NotaDecisione = Tronca(nota, 1000);
        if (approva) a.DataInizio = DateTime.Today;
        await _db.SaveChangesAsync(ct);
        return a;
    }

    public async Task<AssegnazioneCliente?> ChiudiAssegnazioneAsync(int id, string? nota, string? utente, CancellationToken ct = default)
    {
        var a = await _db.AssegnazioniClienti.FirstOrDefaultAsync(x => x.IdAssegnazioneCliente == id, ct);
        if (a is null || a.Stato != StatoAssegnazioneCliente.Approvata) return null;
        a.Stato = StatoAssegnazioneCliente.Chiusa;
        a.DataFine = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(nota)) a.NotaDecisione = Tronca($"{a.NotaDecisione} Chiusura: {nota}".Trim(), 1000);
        a.DecisaDa = Tronca(utente, 100);
        await _db.SaveChangesAsync(ct);
        return a;
    }

    public async Task<List<(AssegnazioneCliente Assegnazione, string Commerciale, string Cliente)>> AssegnazioniAsync(CancellationToken ct = default)
    {
        var elenco = await _db.AssegnazioniClienti.AsNoTracking().OrderBy(a => a.Stato).ThenByDescending(a => a.DataProposta).ToListAsync(ct);
        var commerciali = await _db.Commerciali.AsNoTracking().ToDictionaryAsync(c => c.IdCommerciale, c => c.Nome, ct);
        var codici = elenco.Select(a => a.CodiceCliente).Distinct().ToList();
        var nomi = (await _db.AzwClienti.AsNoTracking().Where(c => codici.Contains(c.CodiceCliente))
                .Select(c => new { c.CodiceCliente, c.RagioneSociale }).ToListAsync(ct))
            .GroupBy(c => c.CodiceCliente).ToDictionary(g => g.Key, g => g.First().RagioneSociale ?? g.Key);
        return elenco.Select(a => (a, commerciali.TryGetValue(a.IdCommerciale, out var n) ? n : $"Commerciale {a.IdCommerciale}",
            nomi.TryGetValue(a.CodiceCliente, out var c) ? c : a.CodiceCliente)).ToList();
    }

    // ── Clienti tolti dalla lista (14/10/2026) ────────────────────────

    /// <summary>Toglie un cliente dalla lista. Restituisce un messaggio d'errore o null.</summary>
    public async Task<string?> TogliAsync(string codice, MotivoUscitaCliente motivo, string? successore, string? note,
        int? idUtente, string? nomeUtente, CancellationToken ct = default)
    {
        codice = codice.Trim();
        successore = string.IsNullOrWhiteSpace(successore) ? null : successore.Trim();
        if (!Enum.IsDefined(motivo)) return "Motivo non valido.";
        if (await _db.ClientiTolti.AnyAsync(t => t.CodiceCliente == codice && !t.Annullata, ct))
            return "Il cliente è già tolto dalla lista: per cambiare motivo rimettilo in lista e toglilo di nuovo.";
        if (motivo == MotivoUscitaCliente.AcquisitoDaCliente)
        {
            if (successore is null) return "Indica il cliente che l'ha acquisito.";
            if (successore == codice) return "Il cliente che acquisisce deve essere un altro.";
            if (!await _db.AzwClienti.AnyAsync(c => c.CodiceCliente == successore, ct)) return $"Cliente {successore} non trovato fra i clienti AZW.";
        }
        else successore = null;
        if (motivo == MotivoUscitaCliente.Altro && string.IsNullOrWhiteSpace(note)) return "Con «Altro» scrivi il motivo nella nota.";
        _db.ClientiTolti.Add(new ClienteTolto
        {
            CodiceCliente = codice, Motivo = motivo, CodiceClienteSuccessore = successore, Note = Tronca(note, 1000),
            DataUscita = DateTime.Today, IdUtente = idUtente, NomeUtente = Tronca(nomeUtente, 100), DataRegistrazione = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Rimette in lista un cliente tolto (annulla l'uscita, nessuna cancellazione). Restituisce quante righe ha annullato.</summary>
    public async Task<int> RimettiAsync(string codice, string? utente, CancellationToken ct = default)
    {
        var righe = await _db.ClientiTolti.Where(t => t.CodiceCliente == codice && !t.Annullata).ToListAsync(ct);
        foreach (var t in righe)
        {
            t.Annullata = true;
            t.DataAnnullamento = DateTime.UtcNow;
            t.AnnullataDa = Tronca(utente, 100);
        }
        await _db.SaveChangesAsync(ct);
        return righe.Count;
    }

    /// <summary>Clienti tolti dalla lista (validi), con il nome e se sono rientrati.</summary>
    public async Task<List<(InfoCliente Info, ClienteTolto Uscita)>> ToltiAsync(IReadOnlyDictionary<string, InfoCliente> info, CancellationToken ct = default)
    {
        var uscite = await UsciteAsync(ct);
        return uscite.Values.OrderByDescending(u => u.DataUscita).ThenBy(u => u.CodiceCliente)
            .Select(u => (info.TryGetValue(u.CodiceCliente, out var i) ? i : new InfoCliente { Codice = u.CodiceCliente, RagioneSociale = $"Cliente {u.CodiceCliente}" }, u))
            .ToList();
    }

    /// <summary>
    /// Ricerca di un cliente AZW per nome, codice o città (per indicare chi ha acquisito). Prima chi ha il codice uguale, poi chi
    /// ha il nome che comincia con il testo, poi chi lo contiene nel nome, infine chi è solo nella città (14/10/2026: con «riccione»
    /// i tanti clienti di Riccione nascondevano Riccione srl). Con più parole, nel nome devono esserci tutte.
    /// </summary>
    public async Task<List<(string Codice, string Nome, string? Citta)>> CercaClientiAsync(string testo, CancellationToken ct = default)
    {
        testo = (testo ?? "").Trim();
        if (testo.Length < 2) return new();
        var parole = testo.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var prima = parole[0];
        var righe = await _db.AzwClienti.AsNoTracking()
            .Where(c => c.CodiceCliente.Contains(prima) || (c.RagioneSociale != null && c.RagioneSociale.Contains(prima)) || (c.Citta != null && c.Citta.Contains(prima)))
            .Select(c => new { c.CodiceCliente, c.RagioneSociale, c.Citta }).Take(2000).ToListAsync(ct);
        static bool Ha(string? s, string p) => s is not null && s.Contains(p, StringComparison.OrdinalIgnoreCase);
        int Rango(string codice, string nome, string? citta)
        {
            if (string.Equals(codice.Trim(), testo, StringComparison.OrdinalIgnoreCase)) return 0;
            if (parole.All(p => Ha(nome, p)))
                return nome.StartsWith(testo, StringComparison.OrdinalIgnoreCase) ? 1 : nome.StartsWith(prima, StringComparison.OrdinalIgnoreCase) ? 2 : 3;
            if (parole.All(p => Ha(nome, p) || Ha(citta, p) || Ha(codice, p))) return 4;
            return 9;
        }
        return righe.Select(r => (Codice: r.CodiceCliente.Trim(), Nome: (r.RagioneSociale ?? r.CodiceCliente).Trim(), r.Citta))
            .Select(r => (r, Rango: Rango(r.Codice, r.Nome, r.Citta)))
            .Where(x => x.Rango < 9)
            .OrderBy(x => x.Rango).ThenBy(x => x.r.Nome, StringComparer.OrdinalIgnoreCase)
            .Take(40).Select(x => x.r).ToList();
    }

    // ── Attributi del cliente ─────────────────────────────────────────

    public async Task SalvaAttributiAsync(string codice, bool intermediario, string? note, string? utente, CancellationToken ct = default)
    {
        var a = await _db.AttributiClienti.FirstOrDefaultAsync(x => x.CodiceCliente == codice, ct);
        if (a is null)
        {
            a = new AttributiCliente { CodiceCliente = codice };
            _db.AttributiClienti.Add(a);
        }
        a.Intermediario = intermediario;
        a.Note = Tronca(note, 1000);
        a.DataUltimaModifica = DateTime.UtcNow;
        a.UltimaModificaDa = Tronca(utente, 100);
        await _db.SaveChangesAsync(ct);
    }

    private static string? Tronca(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }
}
