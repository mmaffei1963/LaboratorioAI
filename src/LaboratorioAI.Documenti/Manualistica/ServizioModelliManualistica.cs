using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Manualistica;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>Le colonne della revisione che servono all'elenco, senza il file.</summary>
internal sealed record TestaRevisione(
    int IdRevisioneModello, int IdModelloDocumento, int NumeroRevisione,
    StatoRevisioneModello Stato, DateTime CreatoIlUtc, string? CreatoDa);

/// <summary>Un modello con lo stato della sua revisione più recente.</summary>
public sealed record ModelloConStato(
    int IdModelloDocumento,
    string Codice,
    string Nome,
    string? CondizioneInclusione,
    int Ordine,
    int? IdRevisione,
    int? NumeroRevisione,
    StatoRevisioneModello? Stato,
    DateTime? CaricataIlUtc,
    string? CaricataDa,
    IReadOnlyList<ApprovazioneSintesi> Approvazioni,
    int? IdRevisioneApprovata,
    int? NumeroRevisioneApprovata);

/// <summary>Un tipo di manuale con chi ne approva i modelli.</summary>
/// <summary>Un tipo di manuale, per la scelta in testa alla pagina.</summary>
public sealed record TipoManualeVoce(int IdTipoManuale, string Codice, string Nome);

public sealed record TipoConApprovatori(
    int IdTipoManuale, string Codice, string Nome, IReadOnlyList<ApprovatoreSintesi> Approvatori);

/// <summary>Una persona abilitata ad approvare i modelli di un tipo.</summary>
public sealed record ApprovatoreSintesi(int IdUtente, string Email, string NomeCompleto);

/// <summary>Chi ha esaminato una revisione e com'è andata.</summary>
public sealed record ApprovazioneSintesi(string Approvatore, bool Approvata, DateTime DataUtc, string? Note);

/// <summary>Esito di un'azione su una revisione, nelle operazioni in blocco.</summary>
public sealed record EsitoAzioneModello(int IdRevisione, string? Documento, bool Ok, string? Errore);

/// <summary>Esito del caricamento di un file.</summary>
public sealed record EsitoCaricamentoModello(
    string NomeFile,
    bool Salvato,
    string? CodiceModello,
    int? IdRevisione,
    int? NumeroRevisione,
    string? Motivo,
    EsitoControlloModello? Controllo);

/// <summary>
/// Gestione dei modelli documentali della manualistica (01/11/2026).
///
/// Un modello si carica come revisione in Bozza, si controlla, si manda
/// in approvazione e diventa utilizzabile quando chi ha il permesso di
/// approvare lo approva (decisione del 02/11/2026: una sola firma, anche
/// di chi lo ha caricato — nella pratica i modelli li prepara il
/// consulente e li approva il responsabile, quindi autore e approvatore
/// restano persone diverse senza bisogno di imporlo).
/// Niente si cancella: la revisione superata passa a Obsoleto.
///
/// Il controllo dei marcatori non si salva: si ricalcola quando serve,
/// così resta allineato a quello che il motore sa fare oggi.
/// </summary>
public sealed class ServizioModelliManualistica
{
    /// <summary>Quante approvazioni positive servono perché una revisione sia utilizzabile (02/11/2026: una).</summary>
    public const int ApprovazioniNecessarie = 1;

    private readonly ContestoLaboratorioAI _db;

    public ServizioModelliManualistica(ContestoLaboratorioAI db) => _db = db;

    // ── Elenco ─────────────────────────────────────────────────────

    /// <summary>
    /// I tipi di manuale attivi, nell'ordine in cui si mostrano. Oggi
    /// c'è solo la privacy; piscine, legionellosi, HACCP e sicurezza si
    /// aggiungono come dati, e la pagina li trova da sola.
    /// </summary>
    public async Task<IReadOnlyList<TipoManualeVoce>> TipiAsync(CancellationToken ct = default) =>
        await _db.TipiManuale.AsNoTracking()
            .Where(t => t.Attivo)
            .OrderBy(t => t.Ordine).ThenBy(t => t.Nome)
            .Select(t => new TipoManualeVoce(t.IdTipoManuale, t.Codice, t.Nome))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ModelloConStato>> ElencoAsync(string codiceTipo, CancellationToken ct = default)
    {
        var modelli = await _db.ModelliDocumento
            .AsNoTracking()
            .Where(m => m.Attivo && m.TipoManuale!.Codice == codiceTipo)
            .OrderBy(m => m.Ordine)
            .ToListAsync(ct);

        var idModelli = modelli.Select(m => m.IdModelloDocumento).ToList();

        // Solo le colonne che servono all'elenco: ContenutoDocx pesa centinaia di KB
        // per modello e caricarlo a ogni apertura della pagina significa tirare
        // giu' qualche megabyte di file che qui non si guardano nemmeno.
        var revisioni = await _db.RevisioniModello
            .AsNoTracking()
            .Where(r => r.Attivo && idModelli.Contains(r.IdModelloDocumento))
            .Select(r => new TestaRevisione(
                r.IdRevisioneModello, r.IdModelloDocumento, r.NumeroRevisione,
                r.Stato, r.CreatoIlUtc, r.CreatoDa))
            .ToListAsync(ct);

        var idRevisioni = revisioni.Select(r => r.IdRevisioneModello).ToList();

        var approvazioni = await _db.ApprovazioniRevisione
            .AsNoTracking()
            .Where(a => idRevisioni.Contains(a.IdRevisioneModello))
            .OrderBy(a => a.DataUtc)
            .ToListAsync(ct);

        return modelli.Select(m =>
        {
            var sue = revisioni.Where(r => r.IdModelloDocumento == m.IdModelloDocumento).ToList();
            var ultima = sue.OrderByDescending(r => r.NumeroRevisione).FirstOrDefault();
            var approvata = sue.Where(r => r.Stato == StatoRevisioneModello.Approvato)
                               .OrderByDescending(r => r.NumeroRevisione).FirstOrDefault();

            var esami = ultima is null
                ? Array.Empty<ApprovazioneSintesi>()
                : approvazioni.Where(a => a.IdRevisioneModello == ultima.IdRevisioneModello)
                    .Select(a => new ApprovazioneSintesi(
                        a.Approvatore, a.Esito == EsitoApprovazioneModello.Approvata, a.DataUtc, a.Note))
                    .ToArray();

            return new ModelloConStato(
                m.IdModelloDocumento, m.Codice, m.Nome, m.CondizioneInclusione, m.Ordine,
                ultima?.IdRevisioneModello, ultima?.NumeroRevisione, ultima?.Stato,
                ultima?.CreatoIlUtc, ultima?.CreatoDa,
                esami,
                approvata?.IdRevisioneModello, approvata?.NumeroRevisione);
        }).ToList();
    }

    // ── Caricamento ────────────────────────────────────────────────

    /// <summary>
    /// Carica un file come nuova revisione in Bozza. Se
    /// <paramref name="codiceModello"/> è null il modello si riconosce
    /// dal nome del file; se non si riconosce il file non viene salvato.
    /// </summary>
    public async Task<EsitoCaricamentoModello> CaricaAsync(
        string codiceTipo, string nomeFile, byte[] contenuto, string? codiceModello, string upn,
        CancellationToken ct = default)
    {
        if (contenuto.Length == 0)
            return new EsitoCaricamentoModello(nomeFile, false, null, null, null, "Il file è vuoto.", null);

        var codice = codiceModello ?? RiconoscimentoModelli.DaNomeFile(nomeFile);
        if (codice is null)
            return new EsitoCaricamentoModello(nomeFile, false, null, null, null,
                "Non si capisce a quale modello corrisponde: sceglierlo dall'elenco.", null);

        var modello = await _db.ModelliDocumento
            .FirstOrDefaultAsync(m => m.Attivo && m.Codice == codice && m.TipoManuale!.Codice == codiceTipo, ct);
        if (modello is null)
            return new EsitoCaricamentoModello(nomeFile, false, codice, null, null,
                $"Il modello «{codice}» non esiste per questo tipo di manuale.", null);

        var controllo = await VerificaContenutoAsync(nomeFile, contenuto, modello.IdTipoManuale, modello.RichiedeMarcatori, ct);

        var impronta = Convert.ToHexString(SHA256.HashData(contenuto)).ToLowerInvariant();

        var precedente = await _db.RevisioniModello
            .Where(r => r.IdModelloDocumento == modello.IdModelloDocumento)
            .OrderByDescending(r => r.NumeroRevisione)
            .FirstOrDefaultAsync(ct);

        if (precedente is not null && precedente.ImprontaSha256 == impronta)
            return new EsitoCaricamentoModello(nomeFile, false, codice, precedente.IdRevisioneModello,
                precedente.NumeroRevisione,
                $"Identico alla revisione {precedente.NumeroRevisione} già caricata: non ne serve una nuova.", controllo);

        var revisione = new RevisioneModello
        {
            IdModelloDocumento = modello.IdModelloDocumento,
            NumeroRevisione = (precedente?.NumeroRevisione ?? 0) + 1,
            Stato = StatoRevisioneModello.Bozza,
            ContenutoDocx = contenuto,
            ImprontaSha256 = impronta,
            NomeFileOriginale = nomeFile,
            CreatoIlUtc = DateTime.UtcNow,
            CreatoDa = upn
        };

        _db.RevisioniModello.Add(revisione);
        await _db.SaveChangesAsync(ct);

        return new EsitoCaricamentoModello(nomeFile, true, codice, revisione.IdRevisioneModello,
            revisione.NumeroRevisione, null, controllo);
    }

    // ── Controllo dei marcatori ────────────────────────────────────

    public async Task<EsitoControlloModello?> VerificaAsync(int idRevisione, CancellationToken ct = default)
    {
        var revisione = await _db.RevisioniModello.AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdRevisioneModello == idRevisione, ct);
        if (revisione is null) return null;

        var modello = await _db.ModelliDocumento.AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdModelloDocumento == revisione.IdModelloDocumento, ct);
        if (modello is null) return null;

        return await VerificaContenutoAsync(
            revisione.NomeFileOriginale ?? modello.Codice, revisione.ContenutoDocx,
            modello.IdTipoManuale, modello.RichiedeMarcatori, ct);
    }

    private async Task<EsitoControlloModello> VerificaContenutoAsync(
        string nomeFile, byte[] contenuto, int idTipoManuale, bool richiedeMarcatori, CancellationToken ct)
    {
        var funzioni = await _db.FunzioniAziendali.AsNoTracking()
            .Where(f => f.Attivo && f.IdTipoManuale == idTipoManuale)
            .Select(f => f.Codice)
            .ToListAsync(ct);

        var campiNoti = await CampiDellaChecklistAsync(idTipoManuale, ct);
        return ControlloMarcatori.Verifica(nomeFile, contenuto, funzioni, campiNoti, richiedeMarcatori);
    }

    /// <summary>
    /// I codici delle voci della check list: sono i campi che il
    /// programma sa compilare. Un campo del modello che non c'è resta
    /// vuoto nel documento, quindi si segnala.
    ///
    /// Finché la check list non è stata definita restituisce null e non
    /// si segnala niente: sarebbero nuovi tutti e 148 i campi, e un
    /// avviso per ciascuno non direbbe nulla a nessuno.
    /// </summary>
    private async Task<IReadOnlySet<string>?> CampiDellaChecklistAsync(int idTipoManuale, CancellationToken ct)
    {
        var codici = await _db.VociChecklist.AsNoTracking()
            .Where(v => v.Attivo && v.IdTipoManuale == idTipoManuale)
            .Select(v => v.Codice)
            .ToListAsync(ct);

        return codici.Count == 0 ? null : codici.ToHashSet(StringComparer.Ordinal);
    }

    // ── Approvazione ───────────────────────────────────────────────

    public async Task<(bool Ok, string? Errore)> InviaInApprovazioneAsync(int idRevisione, CancellationToken ct = default)
    {
        var revisione = await _db.RevisioniModello
            .FirstOrDefaultAsync(r => r.IdRevisioneModello == idRevisione, ct);
        if (revisione is null) return (false, "Revisione non trovata.");
        if (revisione.Stato != StatoRevisioneModello.Bozza)
            return (false, "Si manda in approvazione solo una revisione in bozza.");

        var controllo = await VerificaAsync(idRevisione, ct);
        if (controllo is null) return (false, "Revisione non trovata.");
        if (!controllo.Ok)
            return (false, "Il modello ha marcatori che il motore non sa interpretare: " + controllo.Errori[0]);

        revisione.Stato = StatoRevisioneModello.InApprovazione;
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    /// <summary>
    /// Registra l'esame di un approvatore. Raggiunte le approvazioni
    /// necessarie la revisione diventa utilizzabile e la precedente passa
    /// a Obsoleto. Una revisione respinta torna in bozza: chi l'ha già
    /// vista dovrà rivederla corretta. Resta registrato chi ha caricato e
    /// chi ha approvato, anche quando sono la stessa persona.
    /// </summary>
    public async Task<(bool Ok, string? Errore)> DecidiAsync(
        int idRevisione, string upn, bool approva, string? note, CancellationToken ct = default)
    {
        var revisione = await _db.RevisioniModello
            .FirstOrDefaultAsync(r => r.IdRevisioneModello == idRevisione, ct);
        if (revisione is null) return (false, "Revisione non trovata.");
        if (revisione.Stato != StatoRevisioneModello.InApprovazione)
            return (false, "La revisione non è in approvazione.");

        var modello = await _db.ModelliDocumento.AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdModelloDocumento == revisione.IdModelloDocumento, ct);
        if (modello is null) return (false, "Modello non trovato.");

        if (!await PuoApprovareAsync(upn, modello.IdTipoManuale, ct))
            return (false, "Per questo tipo di manuale i modelli li approva un'altra persona.");

        var gia = await _db.ApprovazioniRevisione
            .AnyAsync(a => a.IdRevisioneModello == idRevisione && a.Approvatore == upn, ct);
        if (gia) return (false, "Questa revisione l'hai già esaminata.");

        _db.ApprovazioniRevisione.Add(new ApprovazioneRevisione
        {
            IdRevisioneModello = idRevisione,
            Approvatore = upn,
            Esito = approva ? EsitoApprovazioneModello.Approvata : EsitoApprovazioneModello.Respinta,
            DataUtc = DateTime.UtcNow,
            Note = note
        });
        await _db.SaveChangesAsync(ct);

        if (!approva)
        {
            revisione.Stato = StatoRevisioneModello.Bozza;
            await _db.SaveChangesAsync(ct);
            return (true, null);
        }

        var positive = await _db.ApprovazioniRevisione
            .Where(a => a.IdRevisioneModello == idRevisione && a.Esito == EsitoApprovazioneModello.Approvata)
            .Select(a => a.Approvatore)
            .Distinct()
            .CountAsync(ct);

        if (positive < ApprovazioniNecessarie) return (true, null);

        var precedenti = await _db.RevisioniModello
            .Where(r => r.IdModelloDocumento == revisione.IdModelloDocumento
                     && r.IdRevisioneModello != idRevisione
                     && r.Stato == StatoRevisioneModello.Approvato)
            .ToListAsync(ct);
        foreach (var p in precedenti) p.Stato = StatoRevisioneModello.Obsoleto;

        revisione.Stato = StatoRevisioneModello.Approvato;
        revisione.DataEfficacia ??= DateTime.UtcNow.Date;
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    // ── Azioni in blocco ───────────────────────────────────────────
    // Ogni revisione è indipendente: quelle che non si possono trattare
    // non fermano le altre, e il riepilogo dice riga per riga com'è andata.

    public async Task<IReadOnlyList<EsitoAzioneModello>> InviaInApprovazioneMoltiAsync(
        IEnumerable<int> idRevisioni, CancellationToken ct = default)
    {
        var esiti = new List<EsitoAzioneModello>();
        foreach (var id in idRevisioni.Distinct())
        {
            var (ok, errore) = await InviaInApprovazioneAsync(id, ct);
            esiti.Add(new EsitoAzioneModello(id, await NomeDocumentoAsync(id, ct), ok, errore));
        }
        return esiti;
    }

    public async Task<IReadOnlyList<EsitoAzioneModello>> DecidiMoltiAsync(
        IEnumerable<int> idRevisioni, string upn, bool approva, string? note, CancellationToken ct = default)
    {
        var esiti = new List<EsitoAzioneModello>();
        foreach (var id in idRevisioni.Distinct())
        {
            var (ok, errore) = await DecidiAsync(id, upn, approva, note, ct);
            esiti.Add(new EsitoAzioneModello(id, await NomeDocumentoAsync(id, ct), ok, errore));
        }
        return esiti;
    }

    private async Task<string?> NomeDocumentoAsync(int idRevisione, CancellationToken ct) =>
        await _db.RevisioniModello.AsNoTracking()
            .Where(r => r.IdRevisioneModello == idRevisione)
            .Select(r => r.ModelloDocumento!.Nome)
            .FirstOrDefaultAsync(ct);

    // ── Chi approva ────────────────────────────────────────────────

    /// <summary>
    /// Se questa persona può approvare i modelli di un tipo di manuale.
    /// Finché per un tipo non è indicato nessuno, i suoi modelli non si
    /// approvano: meglio una pagina che lo dice che una firma messa da
    /// chi capita.
    /// </summary>
    public async Task<bool> PuoApprovareAsync(string upn, int idTipoManuale, CancellationToken ct = default) =>
        await _db.ApprovatoriTipoManuale.AsNoTracking()
            .AnyAsync(a => a.Attivo
                        && a.IdTipoManuale == idTipoManuale
                        && a.Utente!.Email == upn, ct);

    public async Task<bool> PuoApprovareAsync(string upn, string codiceTipo, CancellationToken ct = default) =>
        await _db.ApprovatoriTipoManuale.AsNoTracking()
            .AnyAsync(a => a.Attivo
                        && a.TipoManuale!.Codice == codiceTipo
                        && a.Utente!.Email == upn, ct);

    /// <summary>I tipi di manuale con chi ne approva i modelli.</summary>
    public async Task<IReadOnlyList<TipoConApprovatori>> ApprovatoriAsync(CancellationToken ct = default)
    {
        var tipi = await _db.TipiManuale.AsNoTracking()
            .Where(t => t.Attivo).OrderBy(t => t.Ordine)
            .Select(t => new { t.IdTipoManuale, t.Codice, t.Nome })
            .ToListAsync(ct);

        var righe = await _db.ApprovatoriTipoManuale.AsNoTracking()
            .Where(a => a.Attivo)
            .Select(a => new { a.IdTipoManuale, a.IdUtente, a.Utente!.Email, a.Utente.NomeCompleto })
            .ToListAsync(ct);

        return tipi.Select(t => new TipoConApprovatori(
            t.IdTipoManuale, t.Codice, t.Nome,
            righe.Where(r => r.IdTipoManuale == t.IdTipoManuale)
                 .OrderBy(r => r.NomeCompleto)
                 .Select(r => new ApprovatoreSintesi(r.IdUtente, r.Email, r.NomeCompleto))
                 .ToList())).ToList();
    }

    /// <summary>
    /// Aggiunge o toglie una persona dagli approvatori di un tipo.
    /// Togliere non cancella la riga: la disattiva, così resta la storia
    /// di chi approvava quando un documento è stato prodotto.
    /// </summary>
    public async Task<(bool Ok, string? Errore)> ImpostaApprovatoreAsync(
        int idTipoManuale, int idUtente, bool abilitato, string upn, CancellationToken ct = default)
    {
        var tipo = await _db.TipiManuale.FirstOrDefaultAsync(t => t.IdTipoManuale == idTipoManuale, ct);
        if (tipo is null) return (false, "Tipo di manuale non trovato.");

        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.IdUtente == idUtente, ct);
        if (utente is null) return (false, "Persona non trovata.");
        if (abilitato && !utente.Attivo) return (false, "La persona non è attiva in Laboratorio AI.");

        var riga = await _db.ApprovatoriTipoManuale
            .FirstOrDefaultAsync(a => a.IdTipoManuale == idTipoManuale && a.IdUtente == idUtente, ct);

        if (riga is null)
        {
            if (!abilitato) return (true, null);
            _db.ApprovatoriTipoManuale.Add(new ApprovatoreTipoManuale
            {
                IdTipoManuale = idTipoManuale,
                IdUtente = idUtente,
                Attivo = true,
                CreatoIlUtc = DateTime.UtcNow,
                CreatoDa = upn
            });
        }
        else riga.Attivo = abilitato;

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    /// <summary>Le persone attive, per la tendina di scelta.</summary>
    public async Task<IReadOnlyList<ApprovatoreSintesi>> UtentiAttiviAsync(CancellationToken ct = default) =>
        await _db.Utenti.AsNoTracking()
            .Where(u => u.Attivo)
            .OrderBy(u => u.NomeCompleto)
            .Select(u => new ApprovatoreSintesi(u.IdUtente, u.Email, u.NomeCompleto))
            .ToListAsync(ct);

    // ── Scarico ────────────────────────────────────────────────────

    public async Task<(byte[] Contenuto, string NomeFile)?> ScaricaAsync(int idRevisione, CancellationToken ct = default)
    {
        var revisione = await _db.RevisioniModello.AsNoTracking()
            .Include(r => r.ModelloDocumento)
            .FirstOrDefaultAsync(r => r.IdRevisioneModello == idRevisione, ct);
        if (revisione is null) return null;

        var nome = revisione.NomeFileOriginale
                   ?? $"{revisione.ModelloDocumento?.Codice}_rev{revisione.NumeroRevisione}.docx";
        return (revisione.ContenutoDocx, nome);
    }
}
