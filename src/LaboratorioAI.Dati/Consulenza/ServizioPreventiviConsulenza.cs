using System.Reflection;
using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Consulenza;

/// <summary>
/// Preventivi/contratti di consulenza: attività (dalle descrizioni incarico di ArcLab, sola lettura), testi e
/// pacchetti iniziali (dai modelli Word 2026), numerazione della serie C, totali e dati per la commessa ArcLab.
/// </summary>
public sealed class ServizioPreventiviConsulenza
{
    public const string ChiaveRevisione = "PreventiviConsulenza.RevisioneCondizioni";
    public const string RevisioneDefault = "Mod. Com. 05-Cons Rev. 0";
    private const string RisorsaSeed = "LaboratorioAI.Dati.Consulenza.seed-consulenza.json";

    private readonly ContestoLaboratorioAI _db;
    private readonly IServizioPreventivi _preventivi;
    private readonly IRepositoryControlli _controlli;

    public ServizioPreventiviConsulenza(ContestoLaboratorioAI db, IServizioPreventivi preventivi, IRepositoryControlli controlli)
    {
        _db = db; _preventivi = preventivi; _controlli = controlli;
    }

    public async Task<string> RevisioneCondizioniAsync(CancellationToken ct = default)
    {
        var c = await _db.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == ChiaveRevisione, ct);
        return string.IsNullOrWhiteSpace(c?.Valore) ? RevisioneDefault : c!.Valore!.Trim();
    }

    // ── Attività da ArcLab ────────────────────────────────────────────

    /// <summary>
    /// Allinea le attività alle descrizioni incarico di ArcLab: crea quelle nuove (con modalità di prezzo proposta e
    /// testi iniziali dai modelli), aggiorna descrizione e tipo commessa, disattiva quelle sparite. Poi crea i pacchetti
    /// di settore se non ce ne sono. Restituisce (create, disattivate).
    /// </summary>
    public async Task<(int Create, int Disattivate)> SincronizzaAsync(IReadOnlyList<TipoIncaricoArcLab> tipi, CancellationToken ct = default)
    {
        var seed = LeggiSeed();
        var esistenti = await _db.AttivitaConsulenza.Include(a => a.Contenuti).ToListAsync(ct);
        var perTipo = esistenti.ToDictionary(a => a.IdTipoIncarico);
        int create = 0, disattivate = 0;
        foreach (var t in tipi)
        {
            if (!perTipo.TryGetValue(t.IdTipoIncarico, out var a))
            {
                var (modalita, unita) = ModalitaProposta(t);
                a = new AttivitaConsulenza
                {
                    IdTipoIncarico = t.IdTipoIncarico, IdTipoCommessa = t.IdTipoCommessa, Descrizione = Taglia(t.Descrizione, 255)!,
                    ModalitaPrezzo = modalita, UnitaMisura = unita, Ordine = t.IdTipoIncarico
                };
                _db.AttivitaConsulenza.Add(a);
                perTipo[t.IdTipoIncarico] = a;
                create++;
            }
            else
            {
                a.Descrizione = Taglia(t.Descrizione, 255)!;
                a.IdTipoCommessa = t.IdTipoCommessa;
            }
            var s = seed?.Attivita.FirstOrDefault(x => x.IdTipoIncarico == t.IdTipoIncarico);
            if (s is not null && string.IsNullOrWhiteSpace(a.TestoOfferta) && a.Contenuti.Count == 0)
            {
                a.TestoOfferta = s.Testo;
                a.TestoSuccessivi = string.IsNullOrWhiteSpace(s.TestoSuccessivi) ? null : s.TestoSuccessivi;
                if (Enum.TryParse<ModalitaPrezzoConsulenza>(s.Modalita, out var m)) a.ModalitaPrezzo = m;
                var o = 0;
                foreach (var c in s.Contenuti ?? new List<SeedContenuto>())
                    a.Contenuti.Add(new ContenutoAttivita { Chiave = c.Chiave, Testo = Taglia(c.Testo, 500)!, Predefinito = c.Predefinito, Ordine = ++o });
            }
        }
        var idArcLab = tipi.Select(t => t.IdTipoIncarico).ToHashSet();
        // 29/10/2026: l'attività personalizzata non è in ArcLab e non si disattiva.
        foreach (var a in esistenti.Where(a => a.Attivo && !a.Personalizzata && !idArcLab.Contains(a.IdTipoIncarico)))
        {
            a.Attivo = false;
            disattivate++;
        }
        await _db.SaveChangesAsync(ct);

        if (seed is not null && !await _db.PacchettiConsulenza.AnyAsync(ct))
        {
            foreach (var p in seed.Pacchetti.Where(p => p.Righe.Count > 0))
            {
                var pacchetto = new PacchettoConsulenza { Nome = Taglia(p.Nome, 100)!, Descrizione = $"Dal modello «{p.Modello}»" };
                var o = 0;
                foreach (var r in p.Righe)
                {
                    if (!perTipo.TryGetValue(r.IdTipoIncarico, out var a)) continue;
                    pacchetto.Righe.Add(new RigaPacchettoConsulenza
                    {
                        IdAttivitaConsulenza = a.IdAttivitaConsulenza, Ordine = ++o, DescrizioneLibera = r.Descrizione,
                        Contenuti = r.Contenuti is { Count: > 0 } ? string.Join(';', r.Contenuti) : null,
                        ModalitaPrezzo = Enum.TryParse<ModalitaPrezzoConsulenza>(r.Modalita, out var m) ? m : null,
                        Prezzo = r.Prezzo, PrezzoSuccessivi = r.PrezzoSuccessivi is > 0 ? r.PrezzoSuccessivi : null,
                        Informativa = r.Informativa
                    });
                }
                if (pacchetto.Righe.Count > 0) _db.PacchettiConsulenza.Add(pacchetto);
            }
            await _db.SaveChangesAsync(ct);
        }
        return (create, disattivate);
    }

    /// <summary>
    /// 29/10/2026: crea, se manca, l'attività «personalizzata» della consulenza (tipo commessa 5), solo in Laboratorio AI.
    /// </summary>
    public async Task AssicuraPersonalizzataAsync(CancellationToken ct = default)
    {
        const int tipoConsulenza = 5;
        if (await _db.AttivitaConsulenza.AnyAsync(a => a.Personalizzata && a.IdTipoCommessa == tipoConsulenza, ct)) return;
        _db.AttivitaConsulenza.Add(new AttivitaConsulenza
        {
            IdTipoIncarico = -tipoConsulenza, IdTipoCommessa = tipoConsulenza, Descrizione = "Attività personalizzata",
            ModalitaPrezzo = ModalitaPrezzoConsulenza.UnaTantum, Ordine = 99999, Personalizzata = true
        });
        await _db.SaveChangesAsync(ct);
    }

    private static (ModalitaPrezzoConsulenza, string?) ModalitaProposta(TipoIncaricoArcLab t)
    {
        var d = t.Descrizione.ToLowerInvariant();
        if (t.IdTipoCommessa == 4) return (ModalitaPrezzoConsulenza.PerUnita, "partecipante");
        if (d.Contains("prezzo orario")) return (ModalitaPrezzoConsulenza.Oraria, "ora");
        if (d.StartsWith("analisi laboratorio")) return (ModalitaPrezzoConsulenza.PerUnita, "campione");
        if (d.Contains("gestione") || d.Contains("assistenza") || d.Contains("assitenza") || d.StartsWith("incarico")
            || d.Contains("registri") || d.Contains("(mud)") || d.StartsWith("documento di valutazione dei rischi"))
            return (ModalitaPrezzoConsulenza.Annuale, null);
        return (ModalitaPrezzoConsulenza.UnaTantum, null);
    }

    // ── Righe ────────────────────────────────────────────────────────

    /// <summary>Nuova riga da un'attività: testo, contenuti (quelli indicati per chiave, altrimenti i predefiniti) e prezzi proposti.</summary>
    public static RigaPreventivoConsulenza NuovaRiga(AttivitaConsulenza a, IEnumerable<string>? chiavi = null,
        string? descrizioneLibera = null, ModalitaPrezzoConsulenza? modalita = null, decimal? quantita = null,
        decimal? prezzo = null, decimal? prezzoSuccessivi = null, bool informativa = false)
    {
        var scelte = chiavi?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var contenuti = a.Contenuti.Where(c => c.Attivo)
            .Where(c => scelte is { Count: > 0 } ? c.Chiave is not null && scelte.Contains(c.Chiave) : c.Predefinito)
            .OrderBy(c => c.Ordine).Select(c => c.Testo).ToList();
        var m = modalita ?? a.ModalitaPrezzo;
        return new RigaPreventivoConsulenza
        {
            IdAttivitaConsulenza = a.IdAttivitaConsulenza, IdTipoIncarico = a.IdTipoIncarico, IdTipoCommessa = a.IdTipoCommessa,
            DescrizioneAttivita = a.Descrizione, DescrizioneLibera = descrizioneLibera,
            TestoOfferta = a.TestoOfferta, TestoSuccessivi = m == ModalitaPrezzoConsulenza.Annuale ? a.TestoSuccessivi : null,
            Contenuti = contenuti.Count > 0 ? string.Join('\n', contenuti) : null,
            ModalitaPrezzo = m, UnitaMisura = a.UnitaMisura, Quantita = quantita ?? 1m,
            Prezzo = prezzo ?? a.PrezzoProposto ?? 0m,
            PrezzoSuccessivi = m == ModalitaPrezzoConsulenza.Annuale ? (prezzoSuccessivi ?? a.PrezzoSuccessiviProposto) : null,
            Informativa = informativa
        };
    }

    /// <summary>
    /// Aggiunge al preventivo le attività e le analisi di un pacchetto di settore. Le analisi si compongono come nei
    /// preventivi analisi (metodi e prezzo da ArcLab); se ArcLab non risponde le attività entrano comunque e
    /// l'errore torna nel terzo valore.
    /// </summary>
    public async Task<(int Attivita, int Analisi, string? Errore)> ApplicaPacchettoAsync(PreventivoConsulenza p, int idPacchetto, CancellationToken ct = default)
    {
        var pac = await _db.PacchettiConsulenza.AsNoTracking().Include(x => x.Righe).Include(x => x.Analisi)
            .FirstOrDefaultAsync(x => x.IdPacchettoConsulenza == idPacchetto, ct);
        if (pac is null) return (0, 0, null);
        var ids = pac.Righe.Select(r => r.IdAttivitaConsulenza).Distinct().ToList();
        var attivita = await _db.AttivitaConsulenza.AsNoTracking().Include(a => a.Contenuti)
            .Where(a => ids.Contains(a.IdAttivitaConsulenza)).ToDictionaryAsync(a => a.IdAttivitaConsulenza, ct);
        var prog = p.Righe.Select(r => r.Progressivo).DefaultIfEmpty(0).Max();
        var n = 0;
        foreach (var r in pac.Righe.OrderBy(r => r.Ordine))
        {
            if (!attivita.TryGetValue(r.IdAttivitaConsulenza, out var a)) continue;
            var riga = NuovaRiga(a, r.Contenuti?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                r.DescrizioneLibera, r.ModalitaPrezzo, r.Quantita, r.Prezzo, r.PrezzoSuccessivi, r.Informativa);
            riga.Progressivo = ++prog;
            p.Righe.Add(riga);
            n++;
        }
        p.IdPacchettoOrigine = idPacchetto;
        if (string.IsNullOrWhiteSpace(p.SettoreAttivita)) p.SettoreAttivita = pac.Nome;

        var na = 0;
        string? errore = null;
        foreach (var a in pac.Analisi.OrderBy(x => x.Ordine))
        {
            if (a.IdControlli().Count == 0) continue;
            try
            {
                var an = await ComponiAnalisiAsync(a.IdControlli(), a.IdControlliOrigine is null ? null : a.Descrizione,
                    p.IdClienteArcLab, a.Modalita, a.QuantitaAnnua, a.Prezzo, ct);
                an.Note = a.Note;
                AggiungiAnalisi(p, an);
                na++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errore = "Analisi del pacchetto non caricate (ArcLab non raggiungibile?): " + ex.Message;
                break;
            }
        }
        return (n, na, errore);
    }

    // ── Analisi di laboratorio ───────────────────────────────────────

    /// <summary>
    /// Compone un'analisi da uno o più controlli ArcLab con le stesse regole dei preventivi analisi: copia dei metodi
    /// e prezzo = prezzo di commessa del cliente se c'è, altrimenti somma dei costi dei metodi (listino). Con più
    /// controlli è un gruppo: metodi uniti, prezzo sommato. Un prezzo fissato nel pacchetto vale come prezzo manuale.
    /// </summary>
    public async Task<AnalisiPreventivoConsulenza> ComponiAnalisiAsync(IReadOnlyList<int> idControlli, string? descrizione,
        int? idClienteArcLab, ModalitaAnalisiConsulenza modalita, decimal? quantitaAnnua, decimal? prezzoFisso, CancellationToken ct = default)
    {
        var an = new AnalisiPreventivoConsulenza { Modalita = modalita, QuantitaAnnua = quantitaAnnua is > 0 ? quantitaAnnua : null };
        if (idControlli.Count == 0) { an.Descrizione = Taglia(descrizione, 300) ?? "Analisi"; return an; }
        decimal somma = 0m;
        var tutteCommessa = true;
        var nomi = new List<string>();
        var visti = new HashSet<int>();
        var prog = 0;
        foreach (var id in idControlli)
        {
            var r = await _preventivi.ComponiRigaControlloAsync(id, null, idClienteArcLab, 0m, ct);
            somma += r.PrezzoBase;
            if (r.OriginePrezzo != OriginePrezzoPreventivo.Commessa) tutteCommessa = false;
            nomi.Add((r.Descrizione ?? $"Controllo {id}").Trim());
            foreach (var m in r.Metodi.OrderBy(x => x.Progressivo))
            {
                if (m.IdMetodoArcLab is int im && !visti.Add(im)) continue;
                an.Metodi.Add(CopiaMetodo(m, ++prog));
            }
        }
        if (idControlli.Count == 1) an.IdControlloArcLab = idControlli[0];
        else an.IdControlliOrigine = string.Join('-', idControlli);
        an.Descrizione = Taglia(string.IsNullOrWhiteSpace(descrizione) ? string.Join(" + ", nomi) : descrizione, 300)!;
        an.PrezzoBase = Math.Round(somma, 2);
        if (prezzoFisso is decimal pf && pf > 0) { an.Prezzo = pf; an.OriginePrezzo = OriginePrezzoPreventivo.Manuale; }
        else { an.Prezzo = an.PrezzoBase; an.OriginePrezzo = tutteCommessa ? OriginePrezzoPreventivo.Commessa : OriginePrezzoPreventivo.Listino; }
        return an;
    }

    private static MetodoAnalisiConsulenza CopiaMetodo(RigaMetodoPreventivo m, int progressivo) => new()
    {
        Progressivo = progressivo, IdMetodoArcLab = m.IdMetodoArcLab,
        Descrizione = Taglia(m.Descrizione, 300), Denominazione = Taglia(m.Denominazione ?? m.DenominazioneRiga, 300),
        RiferimentoNormativo = Taglia(m.RiferimentoNormativo, 500), UnitaMisura = Taglia(m.UnitaMisura, 50),
        Accreditato = m.Accreditato, IdLabEsternoArcLab = m.IdLabEsternoArcLab,
        LabEsterno = Taglia(m.LabEsterno, 200), LabEsternoFrase = Taglia(m.LabEsternoFrase, 1000)
    };

    public static void AggiungiAnalisi(PreventivoConsulenza p, AnalisiPreventivoConsulenza an)
    {
        an.Progressivo = p.Analisi.Select(x => x.Progressivo).DefaultIfEmpty(0).Max() + 1;
        p.Analisi.Add(an);
    }

    /// <summary>
    /// Ricalcola il prezzo delle analisi non scritte a mano, ad esempio dopo aver scelto il cliente (il prezzo di
    /// commessa del cliente prevale sul listino). Restituisce quante analisi hanno cambiato prezzo.
    /// </summary>
    public async Task<int> AggiornaPrezziAnalisiAsync(PreventivoConsulenza p, CancellationToken ct = default)
    {
        var cambiate = 0;
        foreach (var an in p.Analisi.Where(a => a.OriginePrezzo != OriginePrezzoPreventivo.Manuale))
        {
            decimal somma = 0m;
            var tutteCommessa = true;
            foreach (var id in an.IdControlli())
            {
                var r = await _preventivi.ComponiRigaControlloAsync(id, an.Descrizione, p.IdClienteArcLab, 0m, ct);
                somma += r.PrezzoBase;
                if (r.OriginePrezzo != OriginePrezzoPreventivo.Commessa) tutteCommessa = false;
            }
            somma = Math.Round(somma, 2);
            if (somma != an.Prezzo) cambiate++;
            an.PrezzoBase = somma;
            an.Prezzo = somma;
            an.OriginePrezzo = tutteCommessa ? OriginePrezzoPreventivo.Commessa : OriginePrezzoPreventivo.Listino;
        }
        return cambiate;
    }

    /// <summary>
    /// Riunisce in una sola riga (gruppo con nome commerciale e prezzo unico) le analisi indicate: metodi uniti,
    /// Id dei controlli d'origine sempre tracciati, prezzo = somma dei prezzi delle righe riunite.
    /// </summary>
    public static AnalisiPreventivoConsulenza? Raggruppa(PreventivoConsulenza p, IReadOnlyCollection<int> progressivi, string? nome)
    {
        var sel = p.Analisi.Where(a => progressivi.Contains(a.Progressivo)).OrderBy(a => a.Progressivo).ToList();
        if (sel.Count < 2) return null;
        var ids = sel.SelectMany(a => a.IdControlli()).Distinct().ToList();
        var g = new AnalisiPreventivoConsulenza
        {
            Progressivo = sel[0].Progressivo,
            IdControlliOrigine = string.Join('-', ids),
            Descrizione = Taglia(string.IsNullOrWhiteSpace(nome) ? string.Join(" + ", sel.Select(a => a.Descrizione)) : nome, 300)!,
            Modalita = sel[0].Modalita, QuantitaAnnua = sel[0].QuantitaAnnua,
            PrezzoBase = sel.Sum(a => a.PrezzoBase), Prezzo = sel.Sum(a => a.Prezzo),
            OriginePrezzo = sel.Any(a => a.OriginePrezzo == OriginePrezzoPreventivo.Manuale) ? OriginePrezzoPreventivo.Manuale
                : sel.All(a => a.OriginePrezzo == OriginePrezzoPreventivo.Commessa) ? OriginePrezzoPreventivo.Commessa : OriginePrezzoPreventivo.Listino,
            Note = Taglia(string.Join("; ", sel.Select(a => a.Note).Where(x => !string.IsNullOrWhiteSpace(x))), 500)
        };
        var visti = new HashSet<int>();
        var prog = 0;
        foreach (var m in sel.SelectMany(a => a.Metodi.OrderBy(x => x.Progressivo)))
        {
            if (m.IdMetodoArcLab is int im && !visti.Add(im)) continue;
            g.Metodi.Add(new MetodoAnalisiConsulenza
            {
                Progressivo = ++prog, IdMetodoArcLab = m.IdMetodoArcLab, Descrizione = m.Descrizione, Denominazione = m.Denominazione,
                RiferimentoNormativo = m.RiferimentoNormativo, UnitaMisura = m.UnitaMisura, Accreditato = m.Accreditato,
                IdLabEsternoArcLab = m.IdLabEsternoArcLab, LabEsterno = m.LabEsterno, LabEsternoFrase = m.LabEsternoFrase
            });
        }
        foreach (var a in sel) p.Analisi.Remove(a);
        p.Analisi.Add(g);
        return g;
    }

    /// <summary>Importo annuo stimato delle analisi a pagamento con quantità indicata (fuori dai corrispettivi del contratto).</summary>
    public static decimal StimaAnnuaAnalisi(PreventivoConsulenza p) =>
        p.Analisi.Where(a => a.Modalita == ModalitaAnalisiConsulenza.APagamento && a.QuantitaAnnua is > 0)
            .Sum(a => Math.Round(a.Prezzo * a.QuantitaAnnua!.Value, 2));

    /// <summary>Valore interno (non stampato) delle analisi comprese nel canone, all'anno se la quantità è indicata.</summary>
    public static decimal ValoreAnalisiComprese(PreventivoConsulenza p) =>
        p.Analisi.Where(a => a.Modalita == ModalitaAnalisiConsulenza.CompresaNelCanone)
            .Sum(a => Math.Round(a.PrezzoBase * (a.QuantitaAnnua is > 0 ? a.QuantitaAnnua.Value : 1m), 2));

    /// <summary>
    /// Una commessa di laboratorio per controllo (IDControllo e PrezzoControllo della commessa ArcLab). Per i gruppi il
    /// prezzo è quello del gruppo, riportato sulla prima riga; per le analisi comprese nel canone il prezzo è 0.
    /// </summary>
    public static IReadOnlyList<CommessaAnalisiDaCompilare> CommesseAnalisiDaCompilare(PreventivoConsulenza p)
    {
        var inizio = p.DataInizio ?? p.DataAccettazione ?? p.DataOfferta;
        var res = new List<CommessaAnalisiDaCompilare>();
        foreach (var a in p.Analisi.OrderBy(x => x.Progressivo))
        {
            var ids = a.IdControlli();
            for (var i = 0; i < ids.Count; i++)
            {
                var compresa = a.Modalita == ModalitaAnalisiConsulenza.CompresaNelCanone;
                decimal? prezzo = compresa ? 0m : (ids.Count == 1 || i == 0 ? a.Prezzo : null);
                var nota = compresa ? "Compresa nel canone di consulenza" : ids.Count > 1 ? $"Gruppo «{a.Descrizione}» ({ids.Count} controlli): prezzo unico del gruppo" : null;
                res.Add(new CommessaAnalisiDaCompilare(a.Progressivo, ids[i], ids.Count == 1 ? a.Descrizione : a.Descrizione + $" – controllo {ids[i]}",
                    p.CodiceClienteAzw, p.IdInsediamentoArcLab, p.SedeNome, p.IdContattoArcLab, prezzo, a.QuantitaAnnua, inizio, p.Numero, nota));
            }
        }
        return res;
    }

    // ── Numerazione e totali ─────────────────────────────────────────

    /// <summary>Prossimo numero della serie C dell'anno.</summary>
    public async Task<int> ProssimoNumeroAsync(int anno, CancellationToken ct = default) =>
        (await _db.PreventiviConsulenza.Where(x => x.Anno == anno && x.Numero != null).MaxAsync(x => (int?)x.Numero, ct) ?? 0) + 1;

    /// <summary>Importo di una riga nel primo anno (o per la prestazione).</summary>
    public static decimal ImportoPrimoAnno(RigaPreventivoConsulenza r) => r.ModalitaPrezzo switch
    {
        ModalitaPrezzoConsulenza.Annuale or ModalitaPrezzoConsulenza.PacchettoOre => r.Prezzo,
        _ => Math.Round(r.Prezzo * r.Quantita, 2)
    };

    /// <summary>Importo annuo dal secondo anno (solo canoni).</summary>
    public static decimal ImportoSuccessivi(RigaPreventivoConsulenza r) =>
        r.ModalitaPrezzo == ModalitaPrezzoConsulenza.Annuale ? (r.PrezzoSuccessivi ?? r.Prezzo) : 0m;

    // ── Blocchi di quotazione (29/10/2026) ───────────────────────────

    /// <summary>Blocchi in ordine di stampa.</summary>
    public static IReadOnlyList<BloccoPreventivoConsulenza> BlocchiOrdinati(PreventivoConsulenza p) =>
        p.Blocchi.OrderBy(b => b.Numero).ToList();

    /// <summary>
    /// Blocco a cui appartiene la riga: quello col suo numero, altrimenti il primo (righe di un blocco tolto).
    /// Null se il preventivo non ha blocchi.
    /// </summary>
    public static BloccoPreventivoConsulenza? BloccoDi(PreventivoConsulenza p, RigaPreventivoConsulenza r)
    {
        var blocchi = BlocchiOrdinati(p);
        if (blocchi.Count == 0) return null;
        return blocchi.FirstOrDefault(b => b.Numero == r.Blocco) ?? blocchi[0];
    }

    /// <summary>Righe del blocco (null = tutte), in ordine.</summary>
    public static IReadOnlyList<RigaPreventivoConsulenza> RigheBlocco(PreventivoConsulenza p, BloccoPreventivoConsulenza? b) =>
        p.Righe.Where(r => b is null || BloccoDi(p, r) == b).OrderBy(r => r.Progressivo).ToList();

    /// <summary>Totale del primo anno e annuo dal secondo anno di un blocco (null = tutto il preventivo). Le righe «per conoscenza» non contano.</summary>
    public static (decimal PrimoAnno, decimal AnniSuccessivi, bool Canoni) TotaliBlocco(PreventivoConsulenza p, BloccoPreventivoConsulenza? b)
    {
        var righe = RigheBlocco(p, b).Where(r => !r.Informativa).ToList();
        return (righe.Sum(ImportoPrimoAnno), righe.Sum(ImportoSuccessivi), righe.Any(r => r.ModalitaPrezzo == ModalitaPrezzoConsulenza.Annuale));
    }

    /// <summary>
    /// Totali salvati nel preventivo: quelli del primo blocco (o di tutto, senza blocchi). I blocchi non si sommano:
    /// l'elenco mostra a parte quanti altri blocchi ci sono.
    /// </summary>
    public static void Ricalcola(PreventivoConsulenza p)
    {
        var (primo, successivi, _) = TotaliBlocco(p, BlocchiOrdinati(p).FirstOrDefault());
        p.TotalePrimoAnno = primo;
        p.TotaleAnniSuccessivi = successivi;
    }

    /// <summary>
    /// Aggiunge un blocco in fondo. Se il preventivo non aveva blocchi e ha già attività, crea il blocco 1 (con le
    /// attività esistenti) e il blocco 2. Restituisce il blocco nuovo.
    /// </summary>
    public static BloccoPreventivoConsulenza AggiungiBlocco(PreventivoConsulenza p)
    {
        if (p.Blocchi.Count == 0 && p.Righe.Count > 0)
        {
            p.Blocchi.Add(new BloccoPreventivoConsulenza { Numero = 1 });
            foreach (var r in p.Righe) r.Blocco = 1;
        }
        var b = new BloccoPreventivoConsulenza { Numero = p.Blocchi.Select(x => x.Numero).DefaultIfEmpty(0).Max() + 1 };
        p.Blocchi.Add(b);
        return b;
    }

    /// <summary>Toglie un blocco: le sue attività passano al blocco precedente (o al primo che resta).</summary>
    public static BloccoPreventivoConsulenza? TogliBlocco(PreventivoConsulenza p, int numero)
    {
        var b = p.Blocchi.FirstOrDefault(x => x.Numero == numero);
        if (b is null) return null;
        p.Blocchi.Remove(b);
        var dest = p.Blocchi.Where(x => x.Numero < numero).OrderByDescending(x => x.Numero).FirstOrDefault()
                   ?? p.Blocchi.OrderBy(x => x.Numero).FirstOrDefault();
        foreach (var r in p.Righe.Where(r => r.Blocco == numero)) r.Blocco = dest?.Numero ?? 1;
        return b;
    }

    /// <summary>Nome del blocco per schermate e note: il titolo, altrimenti «Blocco N».</summary>
    public static string NomeBlocco(BloccoPreventivoConsulenza b) =>
        string.IsNullOrWhiteSpace(b.Titolo) ? $"Blocco {b.Numero}" : b.Titolo!.Trim();

    // ── Dati per la commessa ArcLab ──────────────────────────────────

    /// <summary>
    /// Una commessa ArcLab per ogni attività del contratto (le righe «per conoscenza» escluse), con i valori da
    /// riportare nei campi della tabella <c>commessa</c>. Laboratorio AI non scrive in ArcLab.
    /// </summary>
    public static IReadOnlyList<CommessaDaCompilare> CommesseDaCompilare(PreventivoConsulenza p, int? mansioneCommerciale)
    {
        var inizio = p.DataInizio ?? p.DataAccettazione ?? p.DataOfferta;
        return p.Righe.Where(r => !r.Informativa).OrderBy(r => r.Progressivo).Select(r =>
        {
            var continuativa = r.ModalitaPrezzo == ModalitaPrezzoConsulenza.Annuale;
            return new CommessaDaCompilare(
                r.Progressivo, p.CodiceClienteAzw, p.IdInsediamentoArcLab, p.SedeNome, p.IdContattoArcLab, p.ReferenteNome,
                r.IdTipoCommessa, r.IdTipoIncarico, r.DescrizioneAttivita,
                continuativa ? (short)1 : (short)2,
                mansioneCommerciale?.ToString(), p.IdOrganicoConsulente, p.ConsulenteNome,
                ImportoPrimoAnno(r), continuativa ? ImportoSuccessivi(r) : null,
                inizio, continuativa && inizio is DateTime d ? d.AddYears(Math.Max(1, p.DurataAnni)).AddDays(-1) : null,
                p.Numero, p.NumeroRate, Taglia(string.IsNullOrWhiteSpace(p.Oggetto) ? r.DescrizioneAttivita : p.Oggetto, 100),
                // 29/10/2026: blocco di quotazione e attività personalizzate segnalate nella nota della commessa
                Taglia(string.Join(" ", new[]
                {
                    p.Blocchi.Count > 1 && BloccoDi(p, r) is { } bl ? $"Blocco «{NomeBlocco(bl)}»: aprire solo se il cliente l'ha accettato." : null,
                    r.Personalizzata ? "Attività personalizzata: scegliere in ArcLab la descrizione incarico." : null,
                    r.DescrizioneLibera
                }.Where(x => !string.IsNullOrWhiteSpace(x))), 250));
        }).ToList();
    }

    // ── Seed ─────────────────────────────────────────────────────────

    private static Seed? LeggiSeed()
    {
        using var s = typeof(ServizioPreventiviConsulenza).Assembly.GetManifestResourceStream(RisorsaSeed);
        if (s is null) return null;
        return JsonSerializer.Deserialize<Seed>(s, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static string? Taglia(string? v, int max)
    {
        v = v?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }

    private sealed class Seed
    {
        public List<SeedAttivita> Attivita { get; set; } = new();
        public List<SeedPacchetto> Pacchetti { get; set; } = new();
    }
    private sealed class SeedAttivita
    {
        public int IdTipoIncarico { get; set; }
        public string? Modalita { get; set; }
        public string? Testo { get; set; }
        public string? TestoSuccessivi { get; set; }
        public List<SeedContenuto>? Contenuti { get; set; }
    }
    private sealed class SeedContenuto
    {
        public string? Chiave { get; set; }
        public string Testo { get; set; } = "";
        public bool Predefinito { get; set; }
    }
    private sealed class SeedPacchetto
    {
        public string Nome { get; set; } = "";
        public string? Modello { get; set; }
        public List<SeedRiga> Righe { get; set; } = new();
    }
    private sealed class SeedRiga
    {
        public int IdTipoIncarico { get; set; }
        public string? Descrizione { get; set; }
        public string? Modalita { get; set; }
        public List<string>? Contenuti { get; set; }
        public decimal Prezzo { get; set; }
        public decimal? PrezzoSuccessivi { get; set; }
        public bool Informativa { get; set; }
    }
}

/// <summary>Valori da riportare nella commessa di laboratorio ArcLab per un controllo del contratto.</summary>
public sealed record CommessaAnalisiDaCompilare(
    int Riga, int IdControllo, string Descrizione, string? CodCliente, int? IdInsediamento, string? Sede, int? IdContatto,
    decimal? PrezzoControllo, decimal? QuantitaAnnua, DateTime? DataCommessa, int? NContratto, string? Note);

/// <summary>Valori da riportare nei campi della commessa ArcLab per una riga del contratto.</summary>
public sealed record CommessaDaCompilare(
    int Riga, string? CodCliente, int? IdInsediamento, string? Sede, int? IdContatto, string? Referente,
    int? IdTipoCommessa, int IdTipoIncarico, string DescrIncarico, short TipoStampa,
    string? CodCommessa, int? IdOrganicoResponsabile, string? Responsabile,
    decimal PrezzoPrimoAnno, decimal? PrezzoAnniSuccessivi,
    DateTime? DataCommessa, DateTime? DataScadenzaInc, int? NContratto, int NumeroRateAnno,
    string? OggettoPrev, string? NoteCommessa);
