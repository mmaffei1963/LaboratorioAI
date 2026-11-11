using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>Un archivio della check list, con le sue voci multiple.</summary>
public sealed record ArchivioSalvato(
    int Id, string Tipo, string? Luogo,
    string? SistemaOperativo, string? Antivirus, string? Backup,
    string? FrequenzaBackup, string? LuogoConservazione,
    IReadOnlyList<string> Accessi, IReadOnlyList<string> Interessati,
    IReadOnlyList<string> CategorieDati, IReadOnlyList<int> Gestionali);

/// <summary>Un gestionale del cliente.</summary>
public sealed record GestionaleSalvato(
    int Id, string Prodotto, string? MisureSicurezza, string? Manutenzione,
    string? Backup, string? Frequenza, string? LuogoConservazione);

/// <summary>Una casella di posta del cliente.</summary>
public sealed record CasellaSalvata(int Id, string Indirizzo, IReadOnlyList<string> Funzioni);

/// <summary>
/// Gli archivi, i gestionali e le caselle di posta di un cliente
/// (11/11/2026).
///
/// Nel modello Word ogni archivio ha un numero fisso di caselle: tre
/// persone che accedono, tre categorie di interessati, sei di dati, due
/// gestionali. Qui le righe sono libere, come deciso il 10/11/2026: un
/// albergo con quattro gestionali va descritto per intero, e sara' la
/// generazione del Registro ad avvisare quando il documento non ci sta.
/// Troncare qui perderebbe i dati in silenzio, che e' peggio del limite.
///
/// Le tendine non si ridigitano: le voci sono quelle estratte dai
/// modelli approvati, in <c>man.OpzioniVoce</c>.
/// </summary>
public class ServizioArchiviCompilazione
{
    private readonly ContestoLaboratorioAI _db;

    public ServizioArchiviCompilazione(ContestoLaboratorioAI db) => _db = db;

    // ── Il dizionario delle tendine ───────────────────────

    /// <summary>
    /// Da quale domanda dei modelli viene ciascuna tendina della scheda.
    ///
    /// I nomi non sono sempre quelli che ci si aspetterebbe: nel Registro
    /// <c>RIGA_1</c> e le sue sorelle sono le categorie di dati, mentre
    /// <c>LUOGO_DI_CONSERVAZIONE_2</c> e' quello del secondo gestionale.
    /// Verificato l'11/11/2026 leggendo gli elenchi del Registro, e
    /// segnalato a Vaccarini perche' li rinomini.
    /// </summary>
    private static readonly (string Campo, string Codice)[] Tendine =
    [
        ("luogoCartaceo",        "ARCHIVIO_CARTACEO_LUOGO_1"),
        ("accessiCartaceo",      "ARCHIVIO_CARTACEO_ACCESSIBILE_A_1"),
        ("interessatiCartaceo",  "ARCHIVIO_CARTACEO_CATEGORIE_DI_INTERESSATI_1"),
        ("datiCartaceo",         "ARCHIVIO_CARTACEO_RIGA_1"),

        ("luogoInformatico",     "ARCHIVIO_INFORMATICO_LUOGO_1"),
        ("accessiInformatico",   "ARCHIVIO_INFORMATICO_ACCESSIBILE_A_1"),
        ("interessatiInformatico","ARCHIVIO_INFORMATICO_CATEGORIE_DI_INTERESSATI_1"),
        ("datiInformatico",      "ARCHIVIO_INFORMATICO_RIGA_1"),
        ("sistemaOperativo",     "ARCHIVIO_INFORMATICO_SISTEMA_OPERATIVO_1"),
        ("antivirus",            "ARCHIVIO_INFORMATICO_ANTIVIRUS_1"),
        ("backup",               "ARCHIVIO_INFORMATICO_BACK_UP_1"),
        ("frequenzaBackup",      "ARCHIVIO_INFORMATICO_FREQUENZA_BACK_UP_1"),
        ("luogoConservazione",   "ARCHIVIO_INFORMATICO_LUOGO_DI_CONSERVAZIONE_1"),

        ("prodotto",             "ARCHIVIO_INFORMATICO_GESTIONALI_INSTALLATI_PRODOTTO_1"),
        ("misureGestionale",     "ARCHIVIO_INFORMATICO_MISURE_DI_SICUREZZA_GESTIONALI_1"),
        ("manutenzione",         "ARCHIVIO_INFORMATICO_MANUTENZIONE_GESTIONALI_1"),
        ("backupGestionale",     "ARCHIVIO_INFORMATICO_BACKUP_GESTIONALI_1"),
        ("frequenzaGestionale",  "ARCHIVIO_INFORMATICO_FREQUENZA_1"),
        ("conservazioneGestionale", "ARCHIVIO_INFORMATICO_LUOGO_DI_CONSERVAZIONE_2")
    ];

    /// <summary>Le voci di ogni tendina della scheda, dal dizionario.</summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> DizionarioAsync(
        string codiceTipo, CancellationToken ct = default)
    {
        var codici = Tendine.Select(t => t.Codice).Distinct(StringComparer.Ordinal).ToList();

        var voci = await _db.OpzioniVoce.AsNoTracking()
            .Where(o => o.Attivo
                        && codici.Contains(o.VoceChecklist!.Codice)
                        && o.VoceChecklist.TipoManuale!.Codice == codiceTipo)
            .OrderBy(o => o.Ordine)
            .Select(o => new { o.VoceChecklist!.Codice, o.Valore })
            .ToListAsync(ct);

        var perCodice = voci
            .GroupBy(v => v.Codice, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(v => v.Valore)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        return Tendine.ToDictionary(
            t => t.Campo,
            t => perCodice.TryGetValue(t.Codice, out var v) ? v : [],
            StringComparer.Ordinal);
    }

    // ── Archivi ─────────────────────────────────────

    /// <summary>Gli archivi della check list, nell'ordine del documento.</summary>
    public async Task<IReadOnlyList<ArchivioSalvato>> ArchiviAsync(
        int idCompilazione, CancellationToken ct = default)
    {
        var righe = await _db.ArchiviCompilazione.AsNoTracking()
            .Where(a => a.IdCompilazione == idCompilazione && a.Attivo)
            .Include(a => a.Accessi).Include(a => a.Interessati)
            .Include(a => a.CategorieDati).Include(a => a.Gestionali)
            .OrderBy(a => a.Tipo).ThenBy(a => a.Ordine)
            .ToListAsync(ct);

        return righe.Select(a => new ArchivioSalvato(
            a.IdArchivioCompilazione, a.Tipo.ToString(), a.Luogo,
            a.SistemaOperativo, a.Antivirus, a.Backup, a.FrequenzaBackup, a.LuogoConservazione,
            a.Accessi.OrderBy(x => x.Ordine).Select(x => x.Valore).ToList(),
            a.Interessati.OrderBy(x => x.Ordine).Select(x => x.Valore).ToList(),
            a.CategorieDati.OrderBy(x => x.Ordine).Select(x => x.Valore).ToList(),
            a.Gestionali.OrderBy(x => x.Ordine).Select(x => x.IdGestionaleCompilazione).ToList()
        )).ToList();
    }

    /// <summary>Aggiunge o corregge un archivio. Id a zero ne crea uno nuovo.</summary>
    public async Task<int?> SalvaArchivioAsync(
        int idCompilazione, int idArchivio, string tipo, string? luogo,
        string? sistemaOperativo, string? antivirus, string? backup,
        string? frequenzaBackup, string? luogoConservazione,
        IReadOnlyList<string> accessi, IReadOnlyList<string> interessati,
        IReadOnlyList<string> categorieDati, IReadOnlyList<int> gestionali,
        string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !ServizioCompilazioni.Modificabile(c)) return null;

        if (!Enum.TryParse<TipoArchivio>(tipo, out var tipoArchivio)) return null;

        ArchivioCompilazione? archivio;

        if (idArchivio > 0)
        {
            archivio = await _db.ArchiviCompilazione
                .Include(a => a.Accessi).Include(a => a.Interessati)
                .Include(a => a.CategorieDati).Include(a => a.Gestionali)
                .FirstOrDefaultAsync(a => a.IdArchivioCompilazione == idArchivio
                                          && a.IdCompilazione == idCompilazione, ct);
            if (archivio is null) return null;
        }
        else
        {
            // Il numero dell'archivio nel documento parte da uno per tipo:
            // «archivio cartaceo 1», «archivio informatico 1».
            var ultimo = await _db.ArchiviCompilazione
                .Where(a => a.IdCompilazione == idCompilazione && a.Tipo == tipoArchivio)
                .MaxAsync(a => (int?)a.Ordine, ct) ?? 0;

            archivio = new ArchivioCompilazione
            {
                IdCompilazione = idCompilazione,
                Tipo = tipoArchivio,
                Ordine = ultimo + 1,
                CreatoIlUtc = DateTime.UtcNow,
                CreatoDa = upn
            };
            _db.ArchiviCompilazione.Add(archivio);
        }

        archivio.Luogo = Pulito(luogo);
        archivio.Attivo = true;

        // I campi del solo informatico restano vuoti sui cartacei,
        // altrimenti un archivio di carta uscirebbe con un antivirus.
        var informatico = tipoArchivio == TipoArchivio.Informatico;
        archivio.SistemaOperativo = informatico ? Pulito(sistemaOperativo) : null;
        archivio.Antivirus = informatico ? Pulito(antivirus) : null;
        archivio.Backup = informatico ? Pulito(backup) : null;
        archivio.FrequenzaBackup = informatico ? Pulito(frequenzaBackup) : null;
        archivio.LuogoConservazione = informatico ? Pulito(luogoConservazione) : null;

        Allinea(archivio.Accessi, accessi, (v, o) => new ArchivioAccesso { Valore = v, Ordine = o },
            x => _db.ArchiviAccessi.Remove(x));
        Allinea(archivio.Interessati, interessati, (v, o) => new ArchivioInteressati { Valore = v, Ordine = o },
            x => _db.ArchiviInteressati.Remove(x));
        Allinea(archivio.CategorieDati, categorieDati, (v, o) => new ArchivioCategoriaDati { Valore = v, Ordine = o },
            x => _db.ArchiviCategorieDati.Remove(x));

        if (informatico) AllineaGestionali(archivio, gestionali);
        else foreach (var g in archivio.Gestionali.ToList()) _db.ArchiviGestionali.Remove(g);

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return archivio.IdArchivioCompilazione;
    }

    /// <summary>Toglie un archivio; la riga resta nel database.</summary>
    public async Task<bool> TogliArchivioAsync(
        int idCompilazione, int idArchivio, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !ServizioCompilazioni.Modificabile(c)) return false;

        var archivio = await _db.ArchiviCompilazione.FirstOrDefaultAsync(
            a => a.IdArchivioCompilazione == idArchivio && a.IdCompilazione == idCompilazione, ct);
        if (archivio is null) return false;

        archivio.Attivo = false;
        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Gestionali ────────────────────────────────

    /// <summary>I gestionali del cliente.</summary>
    public async Task<IReadOnlyList<GestionaleSalvato>> GestionaliAsync(
        int idCompilazione, CancellationToken ct = default) =>
        await _db.GestionaliCompilazione.AsNoTracking()
            .Where(g => g.IdCompilazione == idCompilazione && g.Attivo)
            .OrderBy(g => g.Ordine)
            .Select(g => new GestionaleSalvato(
                g.IdGestionaleCompilazione, g.Prodotto, g.MisureSicurezza,
                g.Manutenzione, g.Backup, g.Frequenza, g.LuogoConservazione))
            .ToListAsync(ct);

    /// <summary>Aggiunge o corregge un gestionale.</summary>
    public async Task<int?> SalvaGestionaleAsync(
        int idCompilazione, int idGestionale, string prodotto, string? misureSicurezza,
        string? manutenzione, string? backup, string? frequenza, string? luogoConservazione,
        string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !ServizioCompilazioni.Modificabile(c)) return null;
        if (string.IsNullOrWhiteSpace(prodotto)) return null;

        GestionaleCompilazione? gestionale;

        if (idGestionale > 0)
        {
            gestionale = await _db.GestionaliCompilazione.FirstOrDefaultAsync(
                g => g.IdGestionaleCompilazione == idGestionale && g.IdCompilazione == idCompilazione, ct);
            if (gestionale is null) return null;
        }
        else
        {
            var ultimo = await _db.GestionaliCompilazione
                .Where(g => g.IdCompilazione == idCompilazione)
                .MaxAsync(g => (int?)g.Ordine, ct) ?? 0;

            gestionale = new GestionaleCompilazione
            {
                IdCompilazione = idCompilazione,
                Ordine = ultimo + 1,
                CreatoIlUtc = DateTime.UtcNow,
                CreatoDa = upn
            };
            _db.GestionaliCompilazione.Add(gestionale);
        }

        gestionale.Prodotto = prodotto.Trim();
        gestionale.MisureSicurezza = Pulito(misureSicurezza);
        gestionale.Manutenzione = Pulito(manutenzione);
        gestionale.Backup = Pulito(backup);
        gestionale.Frequenza = Pulito(frequenza);
        gestionale.LuogoConservazione = Pulito(luogoConservazione);
        gestionale.Attivo = true;

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return gestionale.IdGestionaleCompilazione;
    }

    /// <summary>Toglie un gestionale e lo stacca dagli archivi.</summary>
    public async Task<bool> TogliGestionaleAsync(
        int idCompilazione, int idGestionale, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !ServizioCompilazioni.Modificabile(c)) return false;

        var gestionale = await _db.GestionaliCompilazione
            .Include(g => g.Installazioni)
            .FirstOrDefaultAsync(g => g.IdGestionaleCompilazione == idGestionale
                                      && g.IdCompilazione == idCompilazione, ct);
        if (gestionale is null) return false;

        gestionale.Attivo = false;
        foreach (var i in gestionale.Installazioni.ToList()) _db.ArchiviGestionali.Remove(i);

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Caselle di posta ─────────────────────────────

    /// <summary>
    /// Le caselle di posta del cliente, con le funzioni aziendali che vi
    /// accedono. Nel Registro ogni sezione di funzione elenca le proprie:
    /// una casella senza funzioni compare dappertutto.
    /// </summary>
    public async Task<IReadOnlyList<CasellaSalvata>> CaselleAsync(
        int idCompilazione, CancellationToken ct = default)
    {
        var righe = await _db.CaselleEmailCompilazione.AsNoTracking()
            .Where(c => c.IdCompilazione == idCompilazione && c.Attivo)
            .Include(c => c.Funzioni).ThenInclude(f => f.Funzione)
            .OrderBy(c => c.Ordine)
            .ToListAsync(ct);

        return righe.Select(c => new CasellaSalvata(
            c.IdCasellaEmailCompilazione, c.Indirizzo,
            c.Funzioni.Where(f => f.Funzione != null)
                .OrderBy(f => f.Funzione!.Ordine)
                .Select(f => f.Funzione!.Codice).ToList())).ToList();
    }

    /// <summary>Aggiunge o corregge una casella. Id a zero ne crea una nuova.</summary>
    public async Task<int?> SalvaCasellaAsync(
        int idCompilazione, int idCasella, string indirizzo,
        IReadOnlyList<string> funzioni, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !ServizioCompilazioni.Modificabile(c)) return null;
        if (string.IsNullOrWhiteSpace(indirizzo)) return null;

        CasellaEmailCompilazione? casella;

        if (idCasella > 0)
        {
            casella = await _db.CaselleEmailCompilazione
                .Include(x => x.Funzioni)
                .FirstOrDefaultAsync(x => x.IdCasellaEmailCompilazione == idCasella
                                          && x.IdCompilazione == idCompilazione, ct);
            if (casella is null) return null;
        }
        else
        {
            var ultimo = await _db.CaselleEmailCompilazione
                .Where(x => x.IdCompilazione == idCompilazione)
                .MaxAsync(x => (int?)x.Ordine, ct) ?? 0;

            casella = new CasellaEmailCompilazione
            {
                IdCompilazione = idCompilazione,
                Ordine = ultimo + 1,
                CreatoIlUtc = DateTime.UtcNow,
                CreatoDa = upn
            };
            _db.CaselleEmailCompilazione.Add(casella);
        }

        casella.Indirizzo = indirizzo.Trim();
        casella.Attivo = true;

        var volute = await _db.FunzioniAziendali
            .Where(f => f.Attivo && funzioni.Contains(f.Codice))
            .Select(f => f.IdFunzioneAziendale)
            .ToListAsync(ct);

        foreach (var vecchia in casella.Funzioni.Where(f => !volute.Contains(f.IdFunzioneAziendale)).ToList())
            _db.CaselleEmailFunzioni.Remove(vecchia);

        foreach (var id in volute.Where(id => casella.Funzioni.All(f => f.IdFunzioneAziendale != id)))
            casella.Funzioni.Add(new CasellaEmailFunzione { IdFunzioneAziendale = id });

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return casella.IdCasellaEmailCompilazione;
    }

    /// <summary>Toglie una casella; la riga resta nel database.</summary>
    public async Task<bool> TogliCasellaAsync(
        int idCompilazione, int idCasella, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !ServizioCompilazioni.Modificabile(c)) return false;

        var casella = await _db.CaselleEmailCompilazione.FirstOrDefaultAsync(
            x => x.IdCasellaEmailCompilazione == idCasella && x.IdCompilazione == idCompilazione, ct);
        if (casella is null) return false;

        casella.Attivo = false;
        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Aiutanti ───────────────────────────────────

    /// <summary>
    /// Riscrive una lista di valori semplici: si toglie quello che non
    /// c'e' piu' e si aggiunge il resto, mantenendo l'ordine scelto.
    /// </summary>
    private static void Allinea<T>(
        List<T> esistenti, IReadOnlyList<string> voluti,
        Func<string, int, T> crea, Action<T> togli) where T : class
    {
        foreach (var vecchio in esistenti.ToList()) togli(vecchio);
        esistenti.Clear();

        var ordine = 0;
        foreach (var v in voluti.Where(v => !string.IsNullOrWhiteSpace(v)))
            esistenti.Add(crea(v.Trim(), ++ordine));
    }

    /// <summary>I gestionali installati su questo archivio.</summary>
    private void AllineaGestionali(ArchivioCompilazione archivio, IReadOnlyList<int> voluti)
    {
        foreach (var vecchio in archivio.Gestionali
                     .Where(g => !voluti.Contains(g.IdGestionaleCompilazione)).ToList())
        {
            _db.ArchiviGestionali.Remove(vecchio);
            archivio.Gestionali.Remove(vecchio);
        }

        var ordine = 0;
        foreach (var id in voluti)
        {
            ordine++;
            var esistente = archivio.Gestionali.FirstOrDefault(g => g.IdGestionaleCompilazione == id);
            if (esistente is null)
                archivio.Gestionali.Add(new ArchivioGestionale { IdGestionaleCompilazione = id, Ordine = ordine });
            else esistente.Ordine = ordine;
        }
    }

    private static string? Pulito(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
