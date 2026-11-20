using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Presenze;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Presenze;

// Presenze (19/11/2026): lettura e scrittura dello schema «presenze». Definizione: claude/definizione-presenze.md
// Il calcolo è nel motore puro (LaboratorioAI.Core.Presenze); qui si leggono i dati, si chiama il motore e si registrano
// le modifiche. Nessuna cancellazione fisica; ogni correzione ha il motivo e finisce nel registro (solo in aggiunta).

public sealed record ProspettoLavoratore(LavoratorePresenze Calcolo, IReadOnlyList<CalcoloGiorno> Giorni, TotaliPeriodo Totali);

public sealed record RigaTimbratura(int IdTimbratura, string Ora, string Tipo, string? Luogo, string Origine,
    bool Annullata, string? MotivoAnnullamento);

public sealed record RigaAssenza(int IdAssenza, string Causale, decimal Ore, string? Note, bool Annullata, string? MotivoAnnullamento);

public sealed record VoceRegistro(DateTime QuandoUtc, string Autore, string Lavoratore, string Azione, DateOnly? Giorno,
    string? Prima, string? Dopo, string? Motivo);

public sealed record DatiGiorno(IReadOnlyList<RigaTimbratura> Timbrature, IReadOnlyList<RigaAssenza> Assenze,
    IReadOnlyList<VoceRegistro> Storico);

public sealed record RigaProspetto(Lavoratore Lavoratore, ProspettoLavoratore Prospetto);

public sealed record RigaCalendario(int IdGiornoCalendario, DateOnly Giorno, string Tipo, string? Descrizione, decimal? Ore,
    int? IdSede, string? Sede);

/// <summary>Dati del calendario delle ferie di un anno: lavoratori, ferie e permessi per lavoratore e giorno, calendario per sede.</summary>
public sealed record DatiFerie(IReadOnlyList<Lavoratore> Lavoratori,
    IReadOnlyDictionary<int, IReadOnlyDictionary<DateOnly, IReadOnlyList<AssenzaGiorno>>> Assenze,
    IReadOnlyDictionary<int, IReadOnlyDictionary<DateOnly, VoceCalendario>> CalendarioPerSede);

/// <summary>Chi è in sede adesso (entrata senza uscita) oppure l'ultima timbratura di oggi.</summary>
public sealed record PresenzaOggi(Lavoratore Lavoratore, bool InSede, string? UltimaOra, string? UltimoTipo);

/// <summary>Dati del modulo «Lavoratore» (testi come arrivano dal form).</summary>
public sealed class ModuloLavoratore
{
    public int? IdLavoratore { get; set; }
    public int IdUtente { get; set; }
    public int IdSede { get; set; }
    public string? Cognome { get; set; }
    public string? Colore { get; set; }
    public int Ordine { get; set; }
    public string? AttivoDal { get; set; }
    public string? OreLunedi { get; set; }
    public string? OreMartedi { get; set; }
    public string? OreMercoledi { get; set; }
    public string? OreGiovedi { get; set; }
    public string? OreVenerdi { get; set; }
    public string? OreSabato { get; set; }
    public string? OreDomenica { get; set; }
    public bool Attivo { get; set; } = true;
}

public sealed class ServizioPresenze
{
    private static readonly Regex ColoreEsadecimale = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private const string AzionePrecarico = "calendario.precarica";

    private readonly ContestoLaboratorioAI _ctx;

    public ServizioPresenze(ContestoLaboratorioAI ctx) { _ctx = ctx; }

    // ------------------------------------------------------------------ lavoratori

    public Task<Lavoratore?> LavoratoreDaUtenteAsync(int idUtente, CancellationToken ct) =>
        _ctx.Lavoratori.AsNoTracking().Include(l => l.Sede)
            .FirstOrDefaultAsync(l => l.IdUtente == idUtente && l.Attivo, ct);

    public Task<Lavoratore?> LavoratoreAsync(int idLavoratore, CancellationToken ct) =>
        _ctx.Lavoratori.AsNoTracking().Include(l => l.Sede).FirstOrDefaultAsync(l => l.IdLavoratore == idLavoratore, ct);

    public Task<List<Lavoratore>> ElencoLavoratoriAsync(bool soloAttivi, CancellationToken ct) =>
        _ctx.Lavoratori.AsNoTracking().Include(l => l.Sede).Include(l => l.Utente)
            .Where(l => !soloAttivi || l.Attivo)
            .OrderBy(l => l.Ordine).ThenBy(l => l.Cognome).ToListAsync(ct);

    public Task<List<SedeAziendale>> SediAsync(CancellationToken ct) =>
        _ctx.SediAziendali.AsNoTracking().Where(s => s.Attiva).OrderBy(s => s.Nome).ToListAsync(ct);

    /// <summary>Utenti attivi non ancora collegati a un lavoratore (più quello del lavoratore che si sta modificando).</summary>
    public Task<List<Utente>> UtentiLiberiAsync(int? idLavoratoreCorrente, CancellationToken ct)
    {
        var collegati = _ctx.Lavoratori.Where(l => l.IdLavoratore != idLavoratoreCorrente).Select(l => l.IdUtente);
        return _ctx.Utenti.AsNoTracking().Where(u => u.Attivo && !collegati.Contains(u.IdUtente))
            .OrderBy(u => u.NomeCompleto).ToListAsync(ct);
    }

    /// <summary>Crea o modifica un lavoratore. Restituisce un errore da mostrare, oppure null se è andato a buon fine.</summary>
    public async Task<string?> SalvaLavoratoreAsync(ModuloLavoratore m, int? idAutore, CancellationToken ct)
    {
        var cognome = RegolePresenze.NormalizzaTesto(m.Cognome, 100);
        if (cognome is null) return "Indica il cognome, come deve comparire nel prospetto.";
        var colore = (m.Colore ?? "").Trim();
        if (!ColoreEsadecimale.IsMatch(colore)) return "Il colore deve essere nel formato #RRGGBB (per esempio #FFC000).";

        var ore = new decimal[7];
        var testi = new[] { m.OreLunedi, m.OreMartedi, m.OreMercoledi, m.OreGiovedi, m.OreVenerdi, m.OreSabato, m.OreDomenica };
        var nomi = new[] { "lunedì", "martedì", "mercoledì", "giovedì", "venerdì", "sabato", "domenica" };
        for (var i = 0; i < 7; i++)
        {
            double? valore;
            try { valore = ParsingPresenze.ParseOre(testi[i]); }
            catch (Exception e) when (e is FormatException or OverflowException) { valore = -1; }
            valore ??= 0;
            if (valore < 0 || valore > 24) return $"Ore da contratto non valide per {nomi[i]} (da 0 a 24, per esempio 8 oppure 7:30).";
            ore[i] = Math.Round((decimal)valore.Value, 2);
        }

        DateOnly? attivoDal = null;
        if (!string.IsNullOrWhiteSpace(m.AttivoDal))
        {
            if (!DateOnly.TryParseExact(m.AttivoDal.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return "Data «attivo dal» non valida.";
            attivoDal = d;
        }

        if (!await _ctx.SediAziendali.AnyAsync(s => s.IdSede == m.IdSede && s.Attiva, ct)) return "Scegli la sede.";
        if (!await _ctx.Utenti.AnyAsync(u => u.IdUtente == m.IdUtente, ct)) return "Scegli l'utente di LaboratorioAI della persona.";
        if (await _ctx.Lavoratori.AnyAsync(l => l.IdUtente == m.IdUtente && l.IdLavoratore != m.IdLavoratore, ct))
            return "Questo utente è già collegato a un altro lavoratore.";
        if (await _ctx.Lavoratori.AnyAsync(l => l.Cognome == cognome && l.IdLavoratore != m.IdLavoratore, ct))
            return "Esiste già un lavoratore con questo cognome nel prospetto: aggiungi un'iniziale (per esempio «ROSSI M.»).";

        var adesso = DateTime.UtcNow;
        Lavoratore l;
        string? prima = null;
        if (m.IdLavoratore is int id)
        {
            var esistente = await _ctx.Lavoratori.FirstOrDefaultAsync(x => x.IdLavoratore == id, ct);
            if (esistente is null) return "Lavoratore non trovato.";
            l = esistente;
            prima = Descrivi(l);
        }
        else
        {
            l = new Lavoratore { DataCreazione = adesso };
            _ctx.Lavoratori.Add(l);
        }
        l.IdUtente = m.IdUtente; l.IdSede = m.IdSede; l.Cognome = cognome; l.Colore = colore.ToUpperInvariant();
        l.Ordine = m.Ordine; l.AttivoDal = attivoDal; l.Attivo = m.Attivo;
        l.OreLunedi = ore[0]; l.OreMartedi = ore[1]; l.OreMercoledi = ore[2]; l.OreGiovedi = ore[3];
        l.OreVenerdi = ore[4]; l.OreSabato = ore[5]; l.OreDomenica = ore[6];
        l.DataUltimaModifica = adesso;
        await _ctx.SaveChangesAsync(ct);

        _ctx.RegistroModifichePresenze.Add(new ModificaPresenze
        {
            QuandoUtc = adesso, IdUtenteAutore = idAutore, IdLavoratore = l.IdLavoratore,
            Azione = prima is null ? "lavoratore.crea" : "lavoratore.modifica", Prima = prima, Dopo = Descrivi(l),
        });
        await _ctx.SaveChangesAsync(ct);
        return null;
    }

    private static string Descrivi(Lavoratore l) => JsonSerializer.Serialize(new
    {
        l.Cognome, l.IdUtente, l.IdSede, l.Colore, l.Ordine, AttivoDal = l.AttivoDal?.ToString("yyyy-MM-dd"), l.Attivo,
        Ore = new[] { l.OreLunedi, l.OreMartedi, l.OreMercoledi, l.OreGiovedi, l.OreVenerdi, l.OreSabato, l.OreDomenica },
    });

    // ------------------------------------------------------------------ calendario

    public async Task<DateOnly> InizioConteggiAsync(CancellationToken ct)
    {
        var impostazione = await _ctx.ImpostazioniPresenze.AsNoTracking().FirstOrDefaultAsync(ct);
        return impostazione?.InizioConteggi ?? new DateOnly(2027, 1, 1);
    }

    /// <summary>
    /// Precarica nel calendario aziendale le festività nazionali dell'anno (per tutte le sedi) e i patroni delle sedi.
    /// Ripetibile: aggiunge solo quello che manca e non tocca quello che c'è (le modifiche di chi gestisce restano).
    /// </summary>
    public async Task AssicuraCalendarioAsync(int anno, CancellationToken ct)
    {
        var dal = new DateOnly(anno, 1, 1);
        var al = new DateOnly(anno, 12, 31);
        // Il precarico si fa una volta sola per anno (lo ricorda una riga del registro): così chi toglie un giorno dal calendario
        // non se lo ritrova alla visita successiva.
        if (await _ctx.RegistroModifichePresenze.AsNoTracking().AnyAsync(m => m.Azione == AzionePrecarico && m.Giorno == dal, ct))
            return;
        var sedi = await _ctx.SediAziendali.AsNoTracking().ToListAsync(ct);
        var esistenti = await _ctx.CalendarioAziendale.AsNoTracking()
            .Where(c => c.Giorno >= dal && c.Giorno <= al)
            .Select(c => new { c.IdSede, c.Giorno }).ToListAsync(ct);
        var presenti = new HashSet<(int? IdSede, DateOnly Giorno)>(esistenti.Select(e => (e.IdSede, e.Giorno)));

        var nuovi = new List<GiornoCalendarioAziendale>();
        foreach (var f in MotoreCalcoloPresenze.FestivitaNazionali(anno))
            if (!presenti.Contains((null, f.Giorno)))
                nuovi.Add(new GiornoCalendarioAziendale
                {
                    IdSede = null, Giorno = f.Giorno, Tipo = VoceCalendario.Festivita, Descrizione = f.Descrizione,
                });
        foreach (var sede in sedi)
        {
            var patrono = RegolePresenze.Patrono(sede.Nome, anno);
            if (patrono is not null && !presenti.Contains((sede.IdSede, patrono.Giorno)))
                nuovi.Add(new GiornoCalendarioAziendale
                {
                    IdSede = sede.IdSede, Giorno = patrono.Giorno, Tipo = VoceCalendario.Festivita, Descrizione = patrono.Descrizione,
                });
        }
        _ctx.CalendarioAziendale.AddRange(nuovi);
        _ctx.RegistroModifichePresenze.Add(new ModificaPresenze
        {
            QuandoUtc = DateTime.UtcNow, Azione = AzionePrecarico, Giorno = dal,
            Dopo = JsonSerializer.Serialize(new { anno, inserite = nuovi.Count }),
        });
        try { await _ctx.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Una richiesta parallela ha inserito le stesse righe: va bene così, al prossimo giro sono già lì.
            _ctx.ChangeTracker.Clear();
        }
    }

    private async Task<Dictionary<DateOnly, VoceCalendario>> CalendarioPerAsync(Lavoratore l, DateOnly dal, DateOnly al, CancellationToken ct)
    {
        for (var anno = dal.Year; anno <= al.Year; anno++) await AssicuraCalendarioAsync(anno, ct);
        var idSede = l.IdSede;
        var righe = await _ctx.CalendarioAziendale.AsNoTracking()
            .Where(c => c.Giorno >= dal && c.Giorno <= al && (c.IdSede == null || c.IdSede == idSede)).ToListAsync(ct);
        var calendario = new Dictionary<DateOnly, VoceCalendario>();
        // Prima le righe per tutte le sedi, poi quelle della sede del lavoratore: la sede ha la precedenza.
        foreach (var r in righe.OrderBy(r => r.IdSede.HasValue ? 1 : 0))
            calendario[r.Giorno] = new VoceCalendario(r.Tipo, r.Descrizione, r.Ore is decimal o ? (double)o : null);
        return calendario;
    }

    private static LavoratorePresenze PerIlMotore(Lavoratore l) =>
        new(l.Cognome, l.Colore, l.OreContratto(), l.AttivoDal);

    // ------------------------------------------------------------------ calcolo

    public async Task<ProspettoLavoratore> CalcolaAsync(Lavoratore l, DateOnly dal, DateOnly al, CancellationToken ct)
    {
        var calendario = await CalendarioPerAsync(l, dal, al, ct);
        var idl = l.IdLavoratore;
        var timbrature = await _ctx.Timbrature.AsNoTracking()
            .Where(t => t.IdLavoratore == idl && !t.Annullata && t.Giorno >= dal && t.Giorno <= al)
            .OrderBy(t => t.DataOraUtc).ThenBy(t => t.IdTimbratura).ToListAsync(ct);
        var assenze = await _ctx.Assenze.AsNoTracking()
            .Where(a => a.IdLavoratore == idl && !a.Annullata && a.Giorno >= dal && a.Giorno <= al)
            .OrderBy(a => a.IdAssenza).ToListAsync(ct);

        var perGiorno = timbrature.ToLookup(t => t.Giorno, t => new TimbraturaGiorno(FusoPresenze.OraLocale(t.DataOraUtc), t.Tipo, t.Luogo));
        var assenzePerGiorno = assenze.ToLookup(a => a.Giorno, a => new AssenzaGiorno(a.Causale, (double)a.Ore, a.Note));
        var inizio = await InizioConteggiAsync(ct);
        var oggi = FusoPresenze.GiornoLocale(DateTime.UtcNow);
        var calcolo = PerIlMotore(l);
        var giorni = MotoreCalcoloPresenze.CalcolaPeriodo(calcolo, dal, al, perGiorno, assenzePerGiorno, calendario, oggi, inizio);
        return new ProspettoLavoratore(calcolo, giorni, MotoreCalcoloPresenze.CalcolaTotali(giorni));
    }

    // ------------------------------------------------------------------ timbrature

    /// <summary>Timbratura dell'interessato: istante di adesso al minuto, giorno di Europe/Rome.</summary>
    public async Task<Timbratura> TimbraAsync(Lavoratore l, string tipo, string? luogo, int? idUtente, CancellationToken ct)
    {
        var adesso = FusoPresenze.AlMinuto(DateTime.UtcNow);
        var t = new Timbratura
        {
            IdLavoratore = l.IdLavoratore, DataOraUtc = adesso, Giorno = FusoPresenze.GiornoLocale(adesso), Tipo = tipo,
            Luogo = RegolePresenze.NormalizzaTesto(luogo, RegolePresenze.MaxLuogo), Origine = OrigineTimbratura.App,
            IdUtenteCreazione = idUtente, DataCreazione = DateTime.UtcNow,
        };
        _ctx.Timbrature.Add(t);
        await _ctx.SaveChangesAsync(ct);
        return t;
    }

    /// <summary>Tipo della prossima timbratura di oggi: uscita se l'ultima è un'entrata, altrimenti entrata.</summary>
    public async Task<string> ProssimoTipoAsync(Lavoratore l, DateOnly giorno, CancellationToken ct)
    {
        var idl = l.IdLavoratore;
        var ultima = await _ctx.Timbrature.AsNoTracking()
            .Where(t => t.IdLavoratore == idl && t.Giorno == giorno && !t.Annullata)
            .OrderByDescending(t => t.DataOraUtc).ThenByDescending(t => t.IdTimbratura).FirstOrDefaultAsync(ct);
        return ultima is { Tipo: "E" } ? "U" : "E";
    }

    /// <summary>Aggiunge una timbratura a mano (origine «Manuale»), con motivo. Errore da mostrare, oppure null.</summary>
    public async Task<string?> AggiungiTimbraturaAsync(Lavoratore l, DateOnly giorno, string? ora, string? tipo,
        string? luogo, string? motivo, int? idAutore, CancellationToken ct)
    {
        var errore = RegolePresenze.ValidaMotivo(motivo) ?? RegolePresenze.ValidaOra(ora, out var oraValida0)
            ?? RegolePresenze.ValidaTipo(tipo);
        if (errore is not null) return errore;
        RegolePresenze.ValidaOra(ora, out var oraValida);
        var utc = FusoPresenze.ToUtc(giorno, oraValida);
        if (utc is null) return "Quell'orario non esiste (cambio dell'ora legale).";

        var adesso = DateTime.UtcNow;
        var t = NuovaManuale(l, giorno, utc.Value, tipo!, luogo, idAutore, adesso);
        _ctx.Timbrature.Add(t);
        _ctx.RegistroModifichePresenze.Add(Registro(adesso, idAutore, l, "timbratura.aggiungi", giorno, null,
            DescriviTimbratura(giorno, oraValida, t.Tipo, t.Luogo), motivo));
        await _ctx.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Modifica una timbratura: la vecchia si annulla e se ne crea una nuova, tutto nello stesso salvataggio.</summary>
    public async Task<string?> ModificaTimbraturaAsync(Lavoratore l, int idTimbratura, string? ora, string? tipo,
        string? luogo, string? motivo, int? idAutore, CancellationToken ct)
    {
        var errore = RegolePresenze.ValidaMotivo(motivo) ?? RegolePresenze.ValidaOra(ora, out _) ?? RegolePresenze.ValidaTipo(tipo);
        if (errore is not null) return errore;
        RegolePresenze.ValidaOra(ora, out var oraValida);
        var idl = l.IdLavoratore;
        var vecchia = await _ctx.Timbrature.FirstOrDefaultAsync(t => t.IdTimbratura == idTimbratura && t.IdLavoratore == idl && !t.Annullata, ct);
        if (vecchia is null) return "Timbratura non trovata.";
        var utc = FusoPresenze.ToUtc(vecchia.Giorno, oraValida);
        if (utc is null) return "Quell'orario non esiste (cambio dell'ora legale).";

        var adesso = DateTime.UtcNow;
        var prima = DescriviTimbratura(vecchia.Giorno, FusoPresenze.OraLocale(vecchia.DataOraUtc), vecchia.Tipo, vecchia.Luogo);
        vecchia.Annullata = true; vecchia.DataAnnullamento = adesso; vecchia.MotivoAnnullamento = "Sostituita: " + motivo!.Trim();
        var nuova = NuovaManuale(l, vecchia.Giorno, utc.Value, tipo!, luogo, idAutore, adesso);
        _ctx.Timbrature.Add(nuova);
        _ctx.RegistroModifichePresenze.Add(Registro(adesso, idAutore, l, "timbratura.modifica", vecchia.Giorno, prima,
            DescriviTimbratura(vecchia.Giorno, oraValida, nuova.Tipo, nuova.Luogo), motivo));
        await _ctx.SaveChangesAsync(ct);
        return null;
    }

    public async Task<(string? Errore, DateOnly? Giorno)> AnnullaTimbraturaAsync(Lavoratore l, int idTimbratura, string? motivo,
        int? idAutore, CancellationToken ct)
    {
        var errore = RegolePresenze.ValidaMotivo(motivo);
        if (errore is not null) return (errore, null);
        var idl = l.IdLavoratore;
        var t = await _ctx.Timbrature.FirstOrDefaultAsync(x => x.IdTimbratura == idTimbratura && x.IdLavoratore == idl && !x.Annullata, ct);
        if (t is null) return ("Timbratura non trovata.", null);
        var adesso = DateTime.UtcNow;
        var prima = DescriviTimbratura(t.Giorno, FusoPresenze.OraLocale(t.DataOraUtc), t.Tipo, t.Luogo);
        t.Annullata = true; t.DataAnnullamento = adesso; t.MotivoAnnullamento = motivo!.Trim();
        _ctx.RegistroModifichePresenze.Add(Registro(adesso, idAutore, l, "timbratura.annulla", t.Giorno, prima, null, motivo));
        await _ctx.SaveChangesAsync(ct);
        return (null, t.Giorno);
    }

    private static Timbratura NuovaManuale(Lavoratore l, DateOnly giorno, DateTime utc, string tipo, string? luogo, int? idAutore, DateTime adesso) =>
        new()
        {
            IdLavoratore = l.IdLavoratore, DataOraUtc = utc, Giorno = giorno, Tipo = tipo,
            Luogo = RegolePresenze.NormalizzaTesto(luogo, RegolePresenze.MaxLuogo), Origine = OrigineTimbratura.Manuale,
            IdUtenteCreazione = idAutore, DataCreazione = adesso,
        };

    private static string DescriviTimbratura(DateOnly giorno, string ora, string tipo, string? luogo) =>
        JsonSerializer.Serialize(new { data = giorno.ToString("yyyy-MM-dd"), ora, tipo, luogo });

    private static ModificaPresenze Registro(DateTime adesso, int? idAutore, Lavoratore l, string azione, DateOnly? giorno,
        string? prima, string? dopo, string? motivo) =>
        new()
        {
            QuandoUtc = adesso, IdUtenteAutore = idAutore, IdLavoratore = l.IdLavoratore, Azione = azione, Giorno = giorno,
            Prima = prima, Dopo = dopo, Motivo = RegolePresenze.NormalizzaTesto(motivo, 500),
        };

    // ------------------------------------------------------------------ assenze

    /// <summary>Inserisce le assenze (un'assenza per giorno). Errore da mostrare, oppure i giorni inseriti.</summary>
    public async Task<(string? Errore, IReadOnlyList<DateOnly> Giorni, string? Causale)> AggiungiAssenzeAsync(Lavoratore l, string? causale,
        string? note, DateOnly dal, DateOnly al, bool intera, string? ore, int? idAutore, CancellationToken ct)
    {
        var calendario = await CalendarioPerAsync(l, dal, al, ct);
        var esito = RegolePresenze.PianificaAssenze(PerIlMotore(l), causale, note, dal, al, intera, ore, calendario);
        if (!esito.Valido) return (esito.Errore, Array.Empty<DateOnly>(), null);

        var adesso = DateTime.UtcNow;
        foreach (var g in esito.Giorni)
        {
            var a = new Assenza
            {
                IdLavoratore = l.IdLavoratore, Giorno = g.Giorno, Causale = causale!, Ore = Math.Round((decimal)g.Ore, 2),
                Note = esito.Note, IdUtenteCreazione = idAutore, DataCreazione = adesso,
            };
            _ctx.Assenze.Add(a);
            _ctx.RegistroModifichePresenze.Add(Registro(adesso, idAutore, l, "assenza.aggiungi", g.Giorno, null,
                JsonSerializer.Serialize(new { data = g.Giorno.ToString("yyyy-MM-dd"), causale, ore = g.Ore, note = esito.Note }), null));
        }
        await _ctx.SaveChangesAsync(ct);
        return (null, esito.Giorni.Select(g => g.Giorno).ToList(), causale);
    }

    public async Task<(string? Errore, DateOnly? Giorno)> AnnullaAssenzaAsync(Lavoratore l, int idAssenza, string? motivo,
        int? idAutore, CancellationToken ct)
    {
        var errore = RegolePresenze.ValidaMotivo(motivo);
        if (errore is not null) return (errore, null);
        var idl = l.IdLavoratore;
        var a = await _ctx.Assenze.FirstOrDefaultAsync(x => x.IdAssenza == idAssenza && x.IdLavoratore == idl && !x.Annullata, ct);
        if (a is null) return ("Assenza non trovata.", null);
        var adesso = DateTime.UtcNow;
        var prima = JsonSerializer.Serialize(new { data = a.Giorno.ToString("yyyy-MM-dd"), causale = a.Causale, ore = a.Ore, note = a.Note });
        a.Annullata = true; a.DataAnnullamento = adesso; a.MotivoAnnullamento = motivo!.Trim();
        _ctx.RegistroModifichePresenze.Add(Registro(adesso, idAutore, l, "assenza.annulla", a.Giorno, prima, null, motivo));
        await _ctx.SaveChangesAsync(ct);
        return (null, a.Giorno);
    }

    // ------------------------------------------------------------------ giorno e registro

    public async Task<DatiGiorno> GiornoAsync(Lavoratore l, DateOnly giorno, CancellationToken ct)
    {
        var idl = l.IdLavoratore;
        var timbrature = (await _ctx.Timbrature.AsNoTracking().Where(t => t.IdLavoratore == idl && t.Giorno == giorno)
                .OrderBy(t => t.DataOraUtc).ThenBy(t => t.IdTimbratura).ToListAsync(ct))
            .Select(t => new RigaTimbratura(t.IdTimbratura, FusoPresenze.OraLocale(t.DataOraUtc), t.Tipo, t.Luogo, t.Origine,
                t.Annullata, t.MotivoAnnullamento)).ToList();
        var assenze = (await _ctx.Assenze.AsNoTracking().Where(a => a.IdLavoratore == idl && a.Giorno == giorno)
                .OrderBy(a => a.IdAssenza).ToListAsync(ct))
            .Select(a => new RigaAssenza(a.IdAssenza, a.Causale, a.Ore, a.Note, a.Annullata, a.MotivoAnnullamento)).ToList();
        var storico = await RegistroAsync(idl, giorno, 100, ct);
        return new DatiGiorno(timbrature, assenze, storico);
    }

    /// <summary>Voci del registro, dalla più recente. Filtri facoltativi: lavoratore e giorno.</summary>
    public async Task<List<VoceRegistro>> RegistroAsync(int? idLavoratore, DateOnly? giorno, int massimo, CancellationToken ct)
    {
        var righe = await (
            from m in _ctx.RegistroModifichePresenze.AsNoTracking()
            join ut in _ctx.Utenti.AsNoTracking() on m.IdUtenteAutore equals (int?)ut.IdUtente into autori
            from autore in autori.DefaultIfEmpty()
            join lav in _ctx.Lavoratori.AsNoTracking() on m.IdLavoratore equals (int?)lav.IdLavoratore into lavoratori
            from lavoratore in lavoratori.DefaultIfEmpty()
            where m.Azione != AzionePrecarico && (idLavoratore == null || m.IdLavoratore == idLavoratore) && (giorno == null || m.Giorno == giorno)
            orderby m.IdModifica descending
            select new { m.QuandoUtc, Autore = autore != null ? autore.NomeCompleto : "", Lavoratore = lavoratore != null ? lavoratore.Cognome : "",
                m.Azione, m.Giorno, m.Prima, m.Dopo, m.Motivo }
        ).Take(massimo).ToListAsync(ct);
        return righe.Select(r => new VoceRegistro(r.QuandoUtc, r.Autore, r.Lavoratore, r.Azione, r.Giorno, r.Prima, r.Dopo, r.Motivo)).ToList();
    }

    // ------------------------------------------------------------------ prospetto mensile e riepilogo

    /// <summary>
    /// Lavoratori da mostrare in un mese: gli attivi (assunti entro la fine del mese) e chi ora non è più attivo ma ha dati in quel mese.
    /// Filtro facoltativo per sede.
    /// </summary>
    public async Task<List<Lavoratore>> LavoratoriDelMeseAsync(int anno, int mese, int? idSede, CancellationToken ct)
    {
        var dal = new DateOnly(anno, mese, 1);
        var al = dal.AddMonths(1).AddDays(-1);
        var conTimbrature = _ctx.Timbrature.AsNoTracking().Where(t => !t.Annullata && t.Giorno >= dal && t.Giorno <= al).Select(t => t.IdLavoratore);
        var conAssenze = _ctx.Assenze.AsNoTracking().Where(a => !a.Annullata && a.Giorno >= dal && a.Giorno <= al).Select(a => a.IdLavoratore);
        var conDati = await conTimbrature.Union(conAssenze).ToListAsync(ct);
        var q = _ctx.Lavoratori.AsNoTracking().Include(l => l.Sede)
            .Where(l => (l.Attivo && (l.AttivoDal == null || l.AttivoDal <= al)) || conDati.Contains(l.IdLavoratore));
        if (idSede is int s) q = q.Where(l => l.IdSede == s);
        return await q.OrderBy(l => l.Ordine).ThenBy(l => l.Cognome).ToListAsync(ct);
    }

    /// <summary>Il calcolo del mese per ogni lavoratore (per il prospetto, il riepilogo e l'esportazione in Excel).</summary>
    public async Task<List<RigaProspetto>> ProspettoMensileAsync(int anno, int mese, int? idSede, CancellationToken ct)
    {
        var dal = new DateOnly(anno, mese, 1);
        var al = dal.AddMonths(1).AddDays(-1);
        var risultato = new List<RigaProspetto>();
        foreach (var l in await LavoratoriDelMeseAsync(anno, mese, idSede, ct))
            risultato.Add(new RigaProspetto(l, await CalcolaAsync(l, dal, al, ct)));
        return risultato;
    }

    /// <summary>Chi è in sede adesso e, per gli altri, l'ultima timbratura di oggi (solo lavoratori attivi).</summary>
    public async Task<List<PresenzaOggi>> PresentiOggiAsync(CancellationToken ct)
    {
        var oggi = FusoPresenze.GiornoLocale(DateTime.UtcNow);
        var risultato = new List<PresenzaOggi>();
        foreach (var l in await _ctx.Lavoratori.AsNoTracking().Include(x => x.Sede).Where(x => x.Attivo)
                     .OrderBy(x => x.Ordine).ThenBy(x => x.Cognome).ToListAsync(ct))
        {
            var g = (await CalcolaAsync(l, oggi, oggi, ct)).Giorni[0];
            var ultima = g.Timbrature.Count > 0 ? g.Timbrature[^1] : null;
            risultato.Add(new PresenzaOggi(l, g.InCorso, ultima?.Ora, ultima?.Tipo));
        }
        return risultato;
    }

    // ------------------------------------------------------------------ chi c'e' adesso

    /// <summary>Stato di oggi di ogni lavoratore attivo (per «Chi c'è adesso»): stesso calcolo del prospetto, nell'ordine dell'elenco.</summary>
    public async Task<List<RigaDisponibilita>> DisponibilitaOggiAsync(CancellationToken ct)
    {
        var oggi = FusoPresenze.GiornoLocale(DateTime.UtcNow);
        var risultato = new List<RigaDisponibilita>();
        foreach (var l in await _ctx.Lavoratori.AsNoTracking().Include(x => x.Sede)
                     .Where(x => x.Attivo && (x.AttivoDal == null || x.AttivoDal <= oggi))
                     .OrderBy(x => x.Ordine).ThenBy(x => x.Cognome).ToListAsync(ct))
        {
            var g = (await CalcolaAsync(l, oggi, oggi, ct)).Giorni[0];
            risultato.Add(new RigaDisponibilita(l.IdLavoratore, l.Cognome, l.Colore, l.IdSede, l.Sede?.Nome ?? "", DisponibilitaPresenze.Valuta(g)));
        }
        return risultato;
    }

    // ------------------------------------------------------------------ calendario aziendale (modifica)

    /// <summary>Le voci del calendario dell'anno (festività e chiusure, per tutte le sedi o per una sede).</summary>
    public async Task<List<RigaCalendario>> CalendarioAnnoAsync(int anno, CancellationToken ct)
    {
        await AssicuraCalendarioAsync(anno, ct);
        var dal = new DateOnly(anno, 1, 1);
        var al = new DateOnly(anno, 12, 31);
        var righe = await _ctx.CalendarioAziendale.AsNoTracking().Include(c => c.Sede)
            .Where(c => c.Giorno >= dal && c.Giorno <= al)
            .OrderBy(c => c.Giorno).ThenBy(c => c.IdSede).ToListAsync(ct);
        return righe.Select(c => new RigaCalendario(c.IdGiornoCalendario, c.Giorno, c.Tipo, c.Descrizione, c.Ore,
            c.IdSede, c.Sede?.Nome)).ToList();
    }

    private static string DescriviCalendario(GiornoCalendarioAziendale c) =>
        JsonSerializer.Serialize(new { data = c.Giorno.ToString("yyyy-MM-dd"), sede = c.IdSede, tipo = c.Tipo, descrizione = c.Descrizione, ore = c.Ore });

    private static ModificaPresenze RegistroCalendario(DateTime adesso, int? idAutore, string azione, DateOnly giorno,
        string? prima, string? dopo, string? motivo) =>
        new()
        {
            QuandoUtc = adesso, IdUtenteAutore = idAutore, IdLavoratore = null, Azione = azione, Giorno = giorno,
            Prima = prima, Dopo = dopo, Motivo = RegolePresenze.NormalizzaTesto(motivo, 500),
        };

    /// <summary>
    /// Aggiunge festività o chiusure. Se per quella sede (o per tutte) il giorno c'è già, la voce viene sostituita.
    /// Ogni giorno finisce nel registro con il valore prima e dopo.
    /// </summary>
    public async Task<(string? Errore, int Giorni)> AggiungiCalendarioAsync(string? tipo, int? idSede, DateOnly dal, DateOnly al,
        string? descrizione, string? ore, string? motivo, int? idAutore, CancellationToken ct)
    {
        var esito = RegoleCalendarioPresenze.Pianifica(tipo, dal, al, ore);
        if (!esito.Valido) return (esito.Errore, 0);
        if (idSede is int s && !await _ctx.SediAziendali.AnyAsync(x => x.IdSede == s, ct)) return ("Sede non trovata.", 0);

        var testo = RegoleCalendarioPresenze.Descrizione(tipo!, descrizione);
        var oreDecimali = esito.Ore is double o ? Math.Round((decimal)o, 2) : (decimal?)null;
        var adesso = DateTime.UtcNow;
        var primo = esito.Giorni[0];
        var ultimo = esito.Giorni[^1];
        var esistenti = await _ctx.CalendarioAziendale
            .Where(c => c.IdSede == idSede && c.Giorno >= primo && c.Giorno <= ultimo).ToListAsync(ct);
        foreach (var giorno in esito.Giorni)
        {
            var c = esistenti.FirstOrDefault(x => x.Giorno == giorno);
            string? prima = null;
            if (c is null)
            {
                c = new GiornoCalendarioAziendale { IdSede = idSede, Giorno = giorno };
                _ctx.CalendarioAziendale.Add(c);
            }
            else prima = DescriviCalendario(c);
            c.Tipo = tipo!; c.Descrizione = testo; c.Ore = oreDecimali;
            _ctx.RegistroModifichePresenze.Add(RegistroCalendario(adesso, idAutore,
                prima is null ? "calendario.aggiungi" : "calendario.modifica", giorno, prima, DescriviCalendario(c), motivo));
        }
        await _ctx.SaveChangesAsync(ct);
        return (null, esito.Giorni.Count);
    }

    /// <summary>Toglie una voce dal calendario (richiede il motivo). La voce resta nel registro, prima e dopo.</summary>
    public async Task<(string? Errore, int? Anno)> EliminaCalendarioAsync(int idGiornoCalendario, string? motivo, int? idAutore, CancellationToken ct)
    {
        var errore = RegolePresenze.ValidaMotivo(motivo);
        if (errore is not null) return (errore, null);
        var c = await _ctx.CalendarioAziendale.FirstOrDefaultAsync(x => x.IdGiornoCalendario == idGiornoCalendario, ct);
        if (c is null) return ("Voce del calendario non trovata.", null);
        _ctx.RegistroModifichePresenze.Add(RegistroCalendario(DateTime.UtcNow, idAutore, "calendario.elimina", c.Giorno,
            DescriviCalendario(c), null, motivo));
        _ctx.CalendarioAziendale.Remove(c);
        await _ctx.SaveChangesAsync(ct);
        return (null, c.Giorno.Year);
    }

    // ------------------------------------------------------------------ calendario ferie (visibile a tutti)

    /// <summary>
    /// Ferie e permessi di tutti i lavoratori attivi in un anno (filtro facoltativo per sede). Le altre causali non vengono lette:
    /// malattia, L. 104 e simili non si condividono con i colleghi.
    /// </summary>
    public async Task<DatiFerie> FerieAnnoAsync(int anno, int? idSede, CancellationToken ct)
    {
        var dal = new DateOnly(anno, 1, 1);
        var al = new DateOnly(anno, 12, 31);
        var q = _ctx.Lavoratori.AsNoTracking().Include(l => l.Sede)
            .Where(l => l.Attivo && (l.AttivoDal == null || l.AttivoDal <= al));
        if (idSede is int s) q = q.Where(l => l.IdSede == s);
        var lavoratori = await q.OrderBy(l => l.Ordine).ThenBy(l => l.Cognome).ToListAsync(ct);
        var ids = lavoratori.Select(l => l.IdLavoratore).ToList();

        var righe = await _ctx.Assenze.AsNoTracking()
            .Where(a => !a.Annullata && a.Giorno >= dal && a.Giorno <= al
                        && (a.Causale == CausaliPresenze.Ferie || a.Causale == CausaliPresenze.Permesso)
                        && ids.Contains(a.IdLavoratore))
            .OrderBy(a => a.IdAssenza).ToListAsync(ct);
        var assenze = new Dictionary<int, IReadOnlyDictionary<DateOnly, IReadOnlyList<AssenzaGiorno>>>();
        foreach (var gruppo in righe.GroupBy(a => a.IdLavoratore))
            assenze[gruppo.Key] = gruppo.GroupBy(a => a.Giorno).ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AssenzaGiorno>)g.Select(a => new AssenzaGiorno(a.Causale, (double)a.Ore, null)).ToList());

        var calendario = await CalendarioAnnoAsync(anno, ct);
        var perSede = new Dictionary<int, IReadOnlyDictionary<DateOnly, VoceCalendario>>();
        foreach (var idDellaSede in lavoratori.Select(l => l.IdSede).Distinct())
        {
            var voci = new Dictionary<DateOnly, VoceCalendario>();
            // Prima le voci per tutte le sedi, poi quelle della sede: la sede ha la precedenza.
            foreach (var r in calendario.Where(r => r.IdSede == null || r.IdSede == idDellaSede).OrderBy(r => r.IdSede.HasValue ? 1 : 0))
                voci[r.Giorno] = new VoceCalendario(r.Tipo, r.Descrizione, r.Ore is decimal o ? (double)o : null);
            perSede[idDellaSede] = voci;
        }
        return new DatiFerie(lavoratori, assenze, perSede);
    }
}
