using System.Globalization;

using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>Una compilazione nell'elenco.</summary>
public sealed record CompilazioneSintesi(
    int IdCompilazione, string RagioneSociale, DateTime DataDocumenti, int Revisione,
    StatoCompilazione Stato, string? CreatoDa, DateTime CreatoIlUtc,
    DateTime? ModificatoIlUtc, int Risposte);

/// <summary>Esito della creazione: l'id se e' andata, il motivo se no.</summary>
public sealed record EsitoNuovaCompilazione(int? IdCompilazione, string? Errore);

/// <summary>Una compilazione aperta, con le risposte gia' date.</summary>
public sealed record CompilazioneAperta(
    int IdCompilazione, string CodiceTipo, string RagioneSociale,
    DateTime DataDocumenti, int Revisione, StatoCompilazione Stato,
    IReadOnlyDictionary<string, string?> Risposte);

/// <summary>Una voce di tendina nella schermata.</summary>
public sealed record OpzioneDomanda(string Valore, string Testo, bool Predefinita);

/// <summary>Una domanda di un passo.</summary>
public sealed record DomandaPasso(
    int IdVoceChecklist, string Codice, string Etichetta,
    TipoCampoChecklist Tipo, bool Obbligatoria, IReadOnlyList<OpzioneDomanda> Opzioni);

/// <summary>
/// Le compilazioni della check list: una per cliente e per revisione
/// (10/11/2026).
///
/// La compilazione porta con se' una **copia** dei dati del cliente,
/// non un rimando: i documenti che ne escono vanno firmati e restano
/// validi anni, e devono dire cosa il cliente era il giorno in cui sono
/// stati fatti, non cosa e' diventato dopo. Per questo la ragione
/// sociale e l'indirizzo si copiano qui alla creazione, e non si
/// rileggono piu' da ArcLab.
/// </summary>
public class ServizioCompilazioni
{
    private readonly ContestoLaboratorioAI _db;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly ServizioArchivioManualistica _archivio;

    /// <summary>Il contesto del database, la lettura di ArcLab e l'archivio.</summary>
    public ServizioCompilazioni(
        ContestoLaboratorioAI db, IRepositoryAnagrafiche anagrafiche,
        ServizioArchivioManualistica archivio)
    {
        _db = db;
        _anagrafiche = anagrafiche;
        _archivio = archivio;
    }

    /// <summary>Le check list di un tipo di manuale, dalla piu' toccata di recente.</summary>
    public async Task<IReadOnlyList<CompilazioneSintesi>> ElencoAsync(
        string codiceTipo, CancellationToken ct = default) =>
        await _db.Compilazioni.AsNoTracking()
            .Where(c => c.Attivo && c.TipoManuale!.Codice == codiceTipo)
            .OrderByDescending(c => c.ModificatoIlUtc ?? c.CreatoIlUtc)
            .Select(c => new CompilazioneSintesi(
                c.IdCompilazione, c.RagioneSociale, c.DataDocumenti, c.NumeroRevisioneDocumenti,
                c.Stato, c.CreatoDa, c.CreatoIlUtc, c.ModificatoIlUtc,
                c.Valori.Count))
            .ToListAsync(ct);

    /// <summary>I clienti di ArcLab, per sceglierne uno. Sola lettura, come sempre.</summary>
    public Task<IReadOnlyList<Core.Modelli.ClienteDistinto>> ClientiAsync(CancellationToken ct = default) =>
        _anagrafiche.ClientiDistintiAsync(ct);

    /// <summary>
    /// Apre una compilazione. Con <paramref name="idClienteArcLab"/> i
    /// dati del cliente si copiano da ArcLab; senza, il cliente e'
    /// nuovo e si scrive solo la ragione sociale — il resto lo completa
    /// il consulente nel primo passo.
    /// </summary>
    public async Task<EsitoNuovaCompilazione> CreaAsync(
        string codiceTipo, int? idClienteArcLab, string? ragioneSociale,
        DateTime dataDocumenti, int revisione, string? upn, CancellationToken ct = default)
    {
        var tipo = await _db.TipiManuale.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Codice == codiceTipo && t.Attivo, ct);
        if (tipo is null) return new EsitoNuovaCompilazione(null, "Tipo di manuale sconosciuto.");

        var compilazione = new Compilazione
        {
            IdTipoManuale = tipo.IdTipoManuale,
            DataDocumenti = dataDocumenti.Date,
            NumeroRevisioneDocumenti = revisione,
            Stato = StatoCompilazione.Bozza,
            CreatoIlUtc = DateTime.UtcNow,
            CreatoDa = upn,
            RagioneSociale = (ragioneSociale ?? "").Trim()
        };

        if (idClienteArcLab is { } idCliente)
        {
            var clienti = await _anagrafiche.ClientiAsync(ct);
            var cliente = clienti.FirstOrDefault(c => c.Id == idCliente);
            if (cliente is null) return new EsitoNuovaCompilazione(null, "Cliente non trovato in ArcLab.");

            compilazione.CodiceClienteArcLab = idCliente.ToString();
            compilazione.RagioneSociale = cliente.Nome;

            var dettaglio = await _anagrafiche.DettaglioClienteAsync(idCliente, ct);
            if (dettaglio is not null)
            {
                compilazione.PartitaIva = dettaglio.PartitaIva;
                compilazione.SedeLegaleIndirizzo = dettaglio.Indirizzo;
                compilazione.SedeLegaleCap = dettaglio.Cap;
                compilazione.SedeLegaleComune = dettaglio.Citta;
                compilazione.SedeLegaleProvincia = dettaglio.Provincia;
                compilazione.Email = dettaglio.Email;
            }
        }
        else
        {
            compilazione.NuovoCliente = true;
        }

        if (compilazione.RagioneSociale.Length == 0)
            return new EsitoNuovaCompilazione(null, "Serve la ragione sociale del cliente.");

        _db.Compilazioni.Add(compilazione);
        await _db.SaveChangesAsync(ct);

        return new EsitoNuovaCompilazione(compilazione.IdCompilazione, null);
    }

    // ── Compilare ──────────────────────────────────────────────────

    /// <summary>
    /// Una compilazione aperta: la testata e le risposte gia' date.
    /// </summary>
    public async Task<CompilazioneAperta?> ApriAsync(int idCompilazione, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.AsNoTracking()
            .Include(x => x.TipoManuale)
            .FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null) return null;

        var risposte = await _db.ValoriCompilazione.AsNoTracking()
            .Where(v => v.IdCompilazione == idCompilazione)
            .ToDictionaryAsync(v => v.CodiceVoce, v => v.Valore, StringComparer.Ordinal, ct);

        return new CompilazioneAperta(
            c.IdCompilazione, c.TipoManuale!.Codice, c.RagioneSociale,
            c.DataDocumenti, c.NumeroRevisioneDocumenti, c.Stato, risposte);
    }

    /// <summary>
    /// Le domande accese di un passo, con le loro voci.
    ///
    /// Solo le voci che valgono per tutti i documenti: quelle legate a
    /// un modello appartengono alla scheda di quel documento — la
    /// lettera al Responsabile, la scheda dell'archivio — e li' si
    /// compilano, con il contesto che dice quale modello e'.
    /// </summary>
    public async Task<IReadOnlyList<DomandaPasso>> DomandeAsync(
        string codiceTipo, string passo, CancellationToken ct = default)
    {
        var voci = await _db.VociChecklist.AsNoTracking()
            .Where(v => v.Attivo && v.Gruppo == passo && v.TipoManuale!.Codice == codiceTipo)
            .OrderBy(v => v.Ordine)
            .Select(v => new
            {
                v.IdVoceChecklist, v.Codice, v.Etichetta, v.TipoCampo, v.Obbligatoria,
                Opzioni = v.Opzioni
                    .Where(o => o.Attivo && o.IdModelloDocumento == null)
                    .OrderBy(o => o.Ordine)
                    .Select(o => new OpzioneDomanda(o.Valore, o.Etichetta ?? o.Valore, o.Predefinita))
                    .ToList()
            })
            .ToListAsync(ct);

        return voci.Select(v => new DomandaPasso(
            v.IdVoceChecklist, v.Codice, v.Etichetta, v.TipoCampo, v.Obbligatoria, v.Opzioni)).ToList();
    }

    /// <summary>
    /// Salva una risposta. Una riga per domanda: la seconda volta si
    /// sovrascrive, perche' la risposta e' una sola. Il valore vuoto
    /// toglie la riga — «non risposto» e «risposto con niente» sono la
    /// stessa cosa, e tenerne due sarebbe una distinzione senza seguito.
    /// </summary>
    public async Task<bool> SalvaValoreAsync(
        int idCompilazione, string codiceVoce, string? valore, bool scrittoAMano,
        string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !Modificabile(c)) return false;

        var esistente = await _db.ValoriCompilazione
            .FirstOrDefaultAsync(v => v.IdCompilazione == idCompilazione && v.CodiceVoce == codiceVoce, ct);

        if (string.IsNullOrWhiteSpace(valore))
        {
            if (esistente is not null) _db.ValoriCompilazione.Remove(esistente);
        }
        else if (esistente is null)
        {
            _db.ValoriCompilazione.Add(new ValoreCompilazione
            {
                IdCompilazione = idCompilazione,
                CodiceVoce = codiceVoce,
                Valore = valore.Trim(),
                ScrittoAMano = scrittoAMano
            });
        }
        else
        {
            esistente.Valore = valore.Trim();
            esistente.ScrittoAMano = scrittoAMano;
        }

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Sedi del trattamento ──────────────────────────────

    /// <summary>Una sede gia' nella check list.</summary>
    public sealed record SedeSalvata(
        int Id, string? Nome, string? Indirizzo, string? Cap, string? Comune, string? Provincia,
        bool DaArcLab);

    /// <summary>Un insediamento di ArcLab, da tenere o lasciare.</summary>
    public sealed record SedeProposta(
        string Codice, string Nome, string? Indirizzo, string? Cap, string? Comune, string? Provincia,
        bool GiaPresente);

    /// <summary>Le sedi della check list, nell'ordine in cui escono nei documenti.</summary>
    public async Task<IReadOnlyList<SedeSalvata>> SediAsync(int idCompilazione, CancellationToken ct = default) =>
        await _db.SediCompilazione
            .Where(x => x.IdCompilazione == idCompilazione && x.Attivo)
            .OrderBy(x => x.Ordine).ThenBy(x => x.IdSedeCompilazione)
            .Select(x => new SedeSalvata(
                x.IdSedeCompilazione, x.Nome, x.Indirizzo, x.Cap, x.Comune, x.Provincia,
                x.CodiceInsediamentoArcLab != null))
            .ToListAsync(ct);

    /// <summary>
    /// Gli insediamenti che ArcLab conosce per questo cliente, da cui il
    /// consulente sceglie le sedi di trattamento.
    ///
    /// Si rileggono ogni volta invece di copiarli alla creazione: qui non
    /// sono un dato del documento, sono una proposta, e se il cliente ha
    /// aperto una sede nel frattempo e' giusto vederla.
    /// </summary>
    public async Task<IReadOnlyList<SedeProposta>> ProposteSediAsync(
        int idCompilazione, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni
            .FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);

        // Il riferimento ad ArcLab e' registrato come testo (e' un
        // riferimento logico, non una chiave esterna: su ArcLab non si
        // scrive). Se non c'e', la check list e' di un cliente nuovo e
        // non c'e' niente da proporre.
        if (c is null || !int.TryParse(c.CodiceClienteArcLab, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var idCliente))
            return [];

        var gia = await _db.SediCompilazione
            .Where(x => x.IdCompilazione == idCompilazione && x.Attivo && x.CodiceInsediamentoArcLab != null)
            .Select(x => x.CodiceInsediamentoArcLab!)
            .ToListAsync(ct);

        var insediamenti = await _anagrafiche.SediInsediamentiAsync(idCliente, ct);

        return insediamenti
            .Select(i => new SedeProposta(
                i.Id.ToString(CultureInfo.InvariantCulture), i.Nome,
                i.Indirizzo, i.Cap, i.Comune, i.Provincia,
                gia.Contains(i.Id.ToString(CultureInfo.InvariantCulture))))
            .ToList();
    }

    /// <summary>
    /// Aggiunge o corregge una sede. <paramref name="idSede"/> a zero ne
    /// crea una nuova.
    /// </summary>
    /// <returns>L'identificativo della sede, o null se la check list non si tocca.</returns>
    public async Task<int?> SalvaSedeAsync(
        int idCompilazione, int idSede, string? nome, string? indirizzo,
        string? cap, string? comune, string? provincia, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !Modificabile(c)) return null;

        SedeCompilazione? sede;

        if (idSede > 0)
        {
            sede = await _db.SediCompilazione.FirstOrDefaultAsync(
                x => x.IdSedeCompilazione == idSede && x.IdCompilazione == idCompilazione, ct);
            if (sede is null) return null;
        }
        else
        {
            var ultimo = await _db.SediCompilazione
                .Where(x => x.IdCompilazione == idCompilazione)
                .MaxAsync(x => (int?)x.Ordine, ct) ?? 0;

            sede = new SedeCompilazione { IdCompilazione = idCompilazione, Ordine = ultimo + 1 };
            _db.SediCompilazione.Add(sede);
        }

        sede.Nome = Pulito(nome);
        sede.Indirizzo = Pulito(indirizzo);
        sede.Cap = Pulito(cap);
        sede.Comune = Pulito(comune);
        sede.Provincia = Pulito(provincia)?.ToUpperInvariant();
        sede.Attivo = true;

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return sede.IdSedeCompilazione;
    }

    /// <summary>
    /// Porta dentro gli insediamenti scelti fra quelli proposti. Quelli
    /// gia' presenti si saltano, cosi' premere due volte non li sdoppia.
    /// </summary>
    public async Task<int> ImportaSediAsync(
        int idCompilazione, IReadOnlyList<string> codici, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !Modificabile(c) || codici.Count == 0) return 0;

        var proposte = (await ProposteSediAsync(idCompilazione, ct))
            .Where(p => codici.Contains(p.Codice, StringComparer.Ordinal) && !p.GiaPresente)
            .ToList();

        if (proposte.Count == 0) return 0;

        var ordine = await _db.SediCompilazione
            .Where(x => x.IdCompilazione == idCompilazione)
            .MaxAsync(x => (int?)x.Ordine, ct) ?? 0;

        foreach (var p in proposte)
        {
            _db.SediCompilazione.Add(new SedeCompilazione
            {
                IdCompilazione = idCompilazione,
                CodiceInsediamentoArcLab = p.Codice,
                Nome = Pulito(p.Nome),
                Indirizzo = Pulito(p.Indirizzo),
                Cap = Pulito(p.Cap),
                Comune = Pulito(p.Comune),
                Provincia = Pulito(p.Provincia)?.ToUpperInvariant(),
                Ordine = ++ordine
            });
        }

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return proposte.Count;
    }

    /// <summary>Toglie una sede dalla check list; la riga resta nel database.</summary>
    public async Task<bool> TogliSedeAsync(
        int idCompilazione, int idSede, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !Modificabile(c)) return false;

        var sede = await _db.SediCompilazione.FirstOrDefaultAsync(
            x => x.IdSedeCompilazione == idSede && x.IdCompilazione == idCompilazione, ct);
        if (sede is null) return false;

        sede.Attivo = false;

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string? Pulito(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>
    /// Se la check list si puo' ancora toccare (11/11/2026).
    ///
    /// Si modifica finche' i documenti non sono stati consegnati:
    /// generarli non basta a chiuderla, perche' quasi sempre si genera,
    /// si rilegge e si corregge. Dopo la consegna si blocca, altrimenti
    /// le risposte non corrisponderebbero piu' a quello che il cliente
    /// ha in mano; per tornare indietro c'e' «Riapri», che resta
    /// scritto.
    ///
    /// Il controllo sta qui e non solo nella schermata: nascondere un
    /// pulsante non impedisce a nessuno di chiamare l'indirizzo a mano.
    /// </summary>
    public static bool Modificabile(Compilazione c) =>
        c.Attivo && c.Stato is StatoCompilazione.Bozza or StatoCompilazione.Generata;

    // ── Autorizzati al trattamento ──────────────────────

    /// <summary>Un autorizzato gia' inserito.</summary>
    /// <summary>
    /// <paramref name="Funzioni"/> sono i codici, che servono alla
    /// schermata per le caselle; <paramref name="NomiFunzioni"/> sono i
    /// nomi come vanno scritti nei documenti («Cameriera ai piani», non
    /// CAMERIERA_AI_PIANI).
    /// </summary>
    public sealed record AutorizzatoSalvato(
        int Id, int IdQualifica, string Qualifica, bool RichiedeNominativo,
        string? Nome, string? Cognome, int Quantita,
        IReadOnlyList<string> Funzioni, IReadOnlyList<string> NomiFunzioni);

    /// <summary>Le scelte possibili per la schermata.</summary>
    public sealed record SceltePersone(
        IReadOnlyList<VoceScelta> Qualifiche, IReadOnlyList<VoceScelta> Funzioni);

    /// <summary>Una voce di una tendina.</summary>
    public sealed record VoceScelta(string Codice, string Nome, bool Nominativo);

    /// <summary>Qualifiche e funzioni aziendali fra cui scegliere.</summary>
    public async Task<SceltePersone> SceltePersoneAsync(string codiceTipo, CancellationToken ct = default)
    {
        var qualifiche = await _db.Qualifiche
            .Where(q => q.Attivo)
            .OrderBy(q => q.Ordine)
            .Select(q => new VoceScelta(q.IdQualifica.ToString(), q.Nome, q.RichiedeNominativo))
            .ToListAsync(ct);

        var funzioni = await _db.FunzioniAziendali
            .Where(f => f.Attivo && f.TipoManuale!.Codice == codiceTipo)
            .OrderBy(f => f.Ordine)
            .Select(f => new VoceScelta(f.Codice, f.Nome, false))
            .ToListAsync(ct);

        return new SceltePersone(qualifiche, funzioni);
    }

    /// <summary>
    /// Gli autorizzati della check list, nell'ordine in cui escono nei
    /// documenti.
    /// </summary>
    public async Task<IReadOnlyList<AutorizzatoSalvato>> AutorizzatiAsync(
        int idCompilazione, CancellationToken ct = default)
    {
        var righe = await _db.AutorizzatiCompilazione
            .Where(x => x.IdCompilazione == idCompilazione && x.Attivo)
            .Include(x => x.Qualifica)
            .Include(x => x.Funzioni).ThenInclude(f => f.Funzione)
            .OrderBy(x => x.Ordine).ThenBy(x => x.IdAutorizzatoCompilazione)
            .ToListAsync(ct);

        return righe.Select(x => new AutorizzatoSalvato(
            x.IdAutorizzatoCompilazione,
            x.IdQualifica,
            x.Qualifica?.Nome ?? "",
            x.Qualifica?.RichiedeNominativo ?? false,
            x.Nome, x.Cognome, x.Quantita,
            x.Funzioni.Where(f => f.Funzione != null)
                .OrderBy(f => f.Funzione!.Ordine)
                .Select(f => f.Funzione!.Codice).ToList(),
            x.Funzioni.Where(f => f.Funzione != null)
                .OrderBy(f => f.Funzione!.Ordine)
                .Select(f => f.Funzione!.Nome).ToList())).ToList();
    }

    /// <summary>
    /// Aggiunge o corregge un autorizzato. <paramref name="idAutorizzato"/>
    /// a zero ne crea uno nuovo.
    /// </summary>
    public async Task<int?> SalvaAutorizzatoAsync(
        int idCompilazione, int idAutorizzato, int idQualifica, string? nome, string? cognome,
        int quantita, IReadOnlyList<string> funzioni, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !Modificabile(c)) return null;

        var qualifica = await _db.Qualifiche.FirstOrDefaultAsync(q => q.IdQualifica == idQualifica, ct);
        if (qualifica is null) return null;

        AutorizzatoCompilazione? persona;

        if (idAutorizzato > 0)
        {
            persona = await _db.AutorizzatiCompilazione
                .Include(x => x.Funzioni)
                .FirstOrDefaultAsync(x => x.IdAutorizzatoCompilazione == idAutorizzato
                                          && x.IdCompilazione == idCompilazione, ct);
            if (persona is null) return null;
        }
        else
        {
            var ultimo = await _db.AutorizzatiCompilazione
                .Where(x => x.IdCompilazione == idCompilazione)
                .MaxAsync(x => (int?)x.Ordine, ct) ?? 0;

            persona = new AutorizzatoCompilazione { IdCompilazione = idCompilazione, Ordine = ultimo + 1 };
            _db.AutorizzatiCompilazione.Add(persona);
        }

        persona.IdQualifica = idQualifica;
        persona.Attivo = true;

        // Chi richiede il nominativo e' una persona sola e ha un nome; gli
        // altri sono un gruppo («tre lavoratori») e il nome non si scrive.
        if (qualifica.RichiedeNominativo)
        {
            persona.Nome = Pulito(nome);
            persona.Cognome = Pulito(cognome);
            persona.Quantita = 1;
        }
        else
        {
            persona.Nome = null;
            persona.Cognome = null;
            persona.Quantita = Math.Max(1, quantita);
        }

        var volute = await _db.FunzioniAziendali
            .Where(f => f.Attivo && funzioni.Contains(f.Codice))
            .Select(f => f.IdFunzioneAziendale)
            .ToListAsync(ct);

        foreach (var vecchia in persona.Funzioni.Where(f => !volute.Contains(f.IdFunzioneAziendale)).ToList())
            _db.AutorizzatiFunzioni.Remove(vecchia);

        foreach (var id in volute.Where(id => persona.Funzioni.All(f => f.IdFunzioneAziendale != id)))
            persona.Funzioni.Add(new AutorizzatoFunzione { IdFunzioneAziendale = id });

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return persona.IdAutorizzatoCompilazione;
    }

    /// <summary>Toglie un autorizzato; la riga resta nel database.</summary>
    public async Task<bool> TogliAutorizzatoAsync(
        int idCompilazione, int idAutorizzato, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !Modificabile(c)) return false;

        var persona = await _db.AutorizzatiCompilazione.FirstOrDefaultAsync(
            x => x.IdAutorizzatoCompilazione == idAutorizzato && x.IdCompilazione == idCompilazione, ct);
        if (persona is null) return false;

        persona.Attivo = false;

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Gli indirizzi a cui si possono mandare i documenti, proposti
    /// nell'ordine in cui conviene provarli (11/11/2026).
    ///
    /// Prima quello scelto dal consulente per questa check list, poi
    /// quello del cliente copiato alla creazione, poi quelli degli
    /// insediamenti in ArcLab — dove arrivano le comunicazioni dei
    /// rapporti di prova, e spesso sono gli unici che il cliente legge.
    /// Restano una proposta: l'indirizzo si puo' sempre scrivere a mano.
    /// </summary>
    public async Task<IReadOnlyList<string>> IndirizziPropostiAsync(
        int idCompilazione, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null) return [];

        var indirizzi = new List<string?> { c.EmailCliente, c.Email };

        if (int.TryParse(c.CodiceClienteArcLab, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var idCliente))
        {
            // Gli stessi insediamenti proposti come sedi: gia' filtrati
            // su quelli in uso, cosi' non si propone l'indirizzo di una
            // sede chiusa.
            var insediamenti = await _anagrafiche.SediInsediamentiAsync(idCliente, ct);
            indirizzi.AddRange(insediamenti.Select(i => i.Email));
        }

        return indirizzi
            .Where(i => !string.IsNullOrWhiteSpace(i) && i!.Contains('@'))
            .Select(i => i!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── Firme ───────────────────────────────────────

    /// <summary>Una firma raccolta per questa check list.</summary>
    public sealed record FirmaRaccolta(
        string Ruolo, string? NomeFirmatario, DateTime AcquisitaIlUtc, string? AcquisitaDa);

    /// <summary>Le firme gia' raccolte, senza le immagini.</summary>
    public async Task<IReadOnlyList<FirmaRaccolta>> FirmeAsync(
        int idCompilazione, CancellationToken ct = default) =>
        await _db.FirmeCompilazione.AsNoTracking()
            .Where(f => f.IdCompilazione == idCompilazione && f.ImmaginePng != null)
            .OrderBy(f => f.Ruolo)
            .Select(f => new FirmaRaccolta(
                f.Ruolo.ToString(), f.NomeFirmatario, f.AcquisitaIlUtc, f.AcquisitaDa))
            .ToListAsync(ct);

    /// <summary>L'immagine di una firma, per rivederla.</summary>
    public async Task<byte[]?> ImmagineFirmaAsync(
        int idCompilazione, RuoloFirmaManualistica ruolo, CancellationToken ct = default) =>
        await _db.FirmeCompilazione.AsNoTracking()
            .Where(f => f.IdCompilazione == idCompilazione && f.Ruolo == ruolo)
            .Select(f => f.ImmaginePng)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Registra una firma. L'immagine resta qui dentro: il documento
    /// prodotto deve restare uguale a se' stesso anche se l'archivio
    /// cambia. Si conserva solo il PNG, nessun dato biometrico.
    /// </summary>
    public async Task<bool> SalvaFirmaAsync(
        int idCompilazione, RuoloFirmaManualistica ruolo, byte[] png,
        string? nomeFirmatario, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !Modificabile(c)) return false;
        if (png.Length == 0) return false;

        var firma = await _db.FirmeCompilazione.FirstOrDefaultAsync(
            f => f.IdCompilazione == idCompilazione && f.Ruolo == ruolo, ct);

        if (firma is null)
        {
            firma = new FirmaCompilazione { IdCompilazione = idCompilazione, Ruolo = ruolo };
            _db.FirmeCompilazione.Add(firma);
        }

        firma.ImmaginePng = png;
        firma.ImprontaSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png));
        firma.NomeFirmatario = Pulito(nomeFirmatario);
        firma.Modo = ModoFirmaManualistica.Autografa;
        firma.AcquisitaIlUtc = DateTime.UtcNow;
        firma.AcquisitaDa = upn;

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Toglie una firma: l'immagine si cancella davvero.</summary>
    /// <remarks>
    /// E' l'unica cosa che si cancella in tutta la manualistica, e di
    /// proposito: una firma caricata per sbaglio, o sulla check list
    /// sbagliata, non deve restare da nessuna parte.
    /// </remarks>
    public async Task<bool> TogliFirmaAsync(
        int idCompilazione, RuoloFirmaManualistica ruolo, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !Modificabile(c)) return false;

        var firma = await _db.FirmeCompilazione.FirstOrDefaultAsync(
            f => f.IdCompilazione == idCompilazione && f.Ruolo == ruolo, ct);
        if (firma is null) return false;

        _db.FirmeCompilazione.Remove(firma);

        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── Generazione ────────────────────────────────────────────────

    /// <summary>Un documento che si puo' generare per questa compilazione.</summary>
    public sealed record ModelloGenerabile(string Codice, string Nome, int Revisione);

    /// <summary>
    /// I modelli del tipo che hanno una revisione approvata. Senza
    /// approvazione non si genera: il contenuto non e' ancora quello
    /// ufficiale.
    /// </summary>
    public async Task<IReadOnlyList<ModelloGenerabile>> ModelliGenerabiliAsync(
        string codiceTipo, CancellationToken ct = default) =>
        await _db.ModelliDocumento.AsNoTracking()
            .Where(m => m.Attivo && m.TipoManuale!.Codice == codiceTipo
                     && m.Revisioni.Any(r => r.Attivo && r.Stato == StatoRevisioneModello.Approvato))
            .OrderBy(m => m.Ordine)
            .Select(m => new ModelloGenerabile(
                m.Codice, m.Nome,
                m.Revisioni.Where(r => r.Attivo && r.Stato == StatoRevisioneModello.Approvato)
                    .Max(r => r.NumeroRevisione)))
            .ToListAsync(ct);

    /// <summary>
    /// Genera un documento per una compilazione, dal modello approvato.
    ///
    /// Il modello non si tocca: si lavora su una copia in memoria. Il
    /// documento prodotto non viene salvato da nessuna parte — in questa
    /// versione si scarica e basta — perche' la cartella dei documenti
    /// consegnati e il registro di consegna sono il pezzo dopo.
    /// </summary>
    /// <param name="archivia">
    /// Vero solo quando il documento viene davvero consegnato. La
    /// schermata chiama questa stessa funzione anche per mostrare gli
    /// avvisi prima di scaricare: archiviare anche quella volta
    /// riempirebbe l'archivio di documenti che nessuno ha mai visto.
    /// </param>
    public async Task<EsitoGenerazione?> GeneraAsync(
        int idCompilazione, string codiceModello, string? upn = null,
        bool archivia = false, CancellationToken ct = default)
    {
        var compilazione = await _db.Compilazioni.AsNoTracking()
            .Include(c => c.TipoManuale)
            .FirstOrDefaultAsync(c => c.IdCompilazione == idCompilazione, ct);
        if (compilazione is null) return null;

        var modello = await _db.ModelliDocumento.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Attivo && m.Codice == codiceModello
                                   && m.IdTipoManuale == compilazione.IdTipoManuale, ct);
        if (modello is null)
            return new EsitoGenerazione(codiceModello, "", null, [],
                "Questo documento non esiste per questo tipo di manuale.");

        var revisione = await _db.RevisioniModello.AsNoTracking()
            .Where(r => r.Attivo && r.IdModelloDocumento == modello.IdModelloDocumento
                     && r.Stato == StatoRevisioneModello.Approvato)
            .OrderByDescending(r => r.NumeroRevisione)
            .Select(r => new { r.IdRevisioneModello, r.ContenutoDocx })
            .FirstOrDefaultAsync(ct);

        if (revisione is null)
            return new EsitoGenerazione(codiceModello, "", null, [],
                "Questo documento non ha una revisione approvata: non si puo' generare.");

        var risposte = await _db.ValoriCompilazione.AsNoTracking()
            .Where(v => v.IdCompilazione == idCompilazione)
            .ToDictionaryAsync(v => v.CodiceVoce, v => v.Valore, StringComparer.Ordinal, ct);

        var sedi = await _db.SediCompilazione
            .Where(x => x.IdCompilazione == idCompilazione && x.Attivo)
            .OrderBy(x => x.Ordine).ThenBy(x => x.IdSedeCompilazione)
            .Select(x => new SedeDocumento(x.Nome, x.Indirizzo, x.Cap, x.Comune, x.Provincia))
            .ToListAsync(ct);

        var persone = (await AutorizzatiAsync(idCompilazione, ct))
            .Select(a => new AutorizzatoDocumento(
                a.Nome, a.Cognome, a.Qualifica, a.RichiedeNominativo, a.Quantita, a.NomiFunzioni))
            .ToList();

        var contesto = new ContestoGenerazione(compilazione, risposte, sedi, persone);

        var firme = await _db.FirmeCompilazione.AsNoTracking()
            .Where(f => f.IdCompilazione == idCompilazione)
            .Select(f => new { f.Ruolo, f.ImmaginePng })
            .ToListAsync(ct);

        var esito = GeneratoreManualistica.Genera(
            modello.Codice,
            GeneratoreManualistica.NomeFile(modello.Nome, compilazione),
            revisione.ContenutoDocx,
            contesto,
            firme.FirstOrDefault(f => f.Ruolo == RuoloFirmaManualistica.LegaleRappresentante)?.ImmaginePng,
            firme.FirstOrDefault(f => f.Ruolo == RuoloFirmaManualistica.Consulente)?.ImmaginePng);

        // Si archivia appena prodotto, prima ancora di consegnarlo: un
        // documento scaricato e non registrato e' un documento che fra
        // tre anni non si sa piu' con quale revisione era stato fatto.
        if (archivia && esito.Contenuto is not null)
        {
            await _archivio.ArchiviaAsync(
                compilazione, modello.Codice, revisione.IdRevisioneModello,
                esito.NomeFile, esito.Contenuto, FormatoManualeGenerato.Docx, upn, ct);

            await AvanzaStatoAsync(idCompilazione, StatoCompilazione.Generata, upn, ct);
        }

        return esito;
    }

    /// <summary>
    /// Porta avanti lo stato della check list, mai indietro.
    ///
    /// Generare un documento la porta a «documenti generati», mandarlo
    /// a «consegnata». Una check list gia' consegnata non torna a
    /// «generata» solo perche' si rigenera un documento: per tornare
    /// indietro c'e' <see cref="RiapriAsync"/>, che chiede una persona.
    /// </summary>
    private async Task AvanzaStatoAsync(
        int idCompilazione, StatoCompilazione nuovo, string? upn, CancellationToken ct)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !c.Attivo || c.Stato >= nuovo) return;

        c.Stato = nuovo;
        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Riapre una check list consegnata, per correggerla.
    ///
    /// I documenti gia' prodotti restano nell'archivio: quello che il
    /// cliente ha ricevuto non si tocca. Rigenerando ne nascono di
    /// nuovi accanto ai vecchi.
    /// </summary>
    public async Task<bool> RiapriAsync(int idCompilazione, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null || !c.Attivo) return false;
        if (c.Stato is not (StatoCompilazione.Inviata or StatoCompilazione.InAttesaFirma)) return false;

        c.Stato = StatoCompilazione.Generata;
        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Registra un invio nella sua tabella, riuscito o fallito.
    ///
    /// Il log di audit dice chi ha fatto cosa; questa tabella risponde
    /// a un'altra domanda: a questo cliente la documentazione e' stata
    /// mandata, quando, a chi, e com'e' andata. Anche i tentativi
    /// falliti restano, altrimenti un invio mai arrivato sembrerebbe
    /// non essere mai stato tentato.
    /// </summary>
    public async Task<int?> RegistraInvioAsync(
        int idCompilazione, IReadOnlyList<string> destinatari, IReadOnlyList<string> formati,
        string? idMessaggio, string? errore, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null) return null;

        var riuscito = errore is null;

        var invio = new InvioDocumenti
        {
            IdCompilazione = idCompilazione,
            Tipo = TipoInvioManualistica.Cliente,
            Destinatari = string.Join("; ", destinatari),
            Formati = string.Join(", ", formati.Distinct(StringComparer.OrdinalIgnoreCase)),
            Stato = riuscito ? StatoInvioManualistica.Inviato : StatoInvioManualistica.Errore,
            InviatoIlUtc = riuscito ? DateTime.UtcNow : null,
            IdMessaggio = idMessaggio,
            Esito = errore,
            CreatoIlUtc = DateTime.UtcNow,
            CreatoDa = upn
        };

        _db.InviiDocumenti.Add(invio);
        await _db.SaveChangesAsync(ct);

        if (riuscito) await AvanzaStatoAsync(idCompilazione, StatoCompilazione.Inviata, upn, ct);

        return invio.IdInvioDocumenti;
    }

    /// <summary>Gli invii gia' fatti per questa check list.</summary>
    public sealed record InvioFatto(
        string Destinatari, string? Formati, string Stato, DateTime? InviatoIlUtc,
        string? CreatoDa, string? Esito);

    /// <summary>Lo storico degli invii, dal piu' recente.</summary>
    public async Task<IReadOnlyList<InvioFatto>> InviiAsync(
        int idCompilazione, CancellationToken ct = default) =>
        await _db.InviiDocumenti.AsNoTracking()
            .Where(i => i.IdCompilazione == idCompilazione)
            .OrderByDescending(i => i.CreatoIlUtc)
            .Select(i => new InvioFatto(
                i.Destinatari, i.Formati, i.Stato.ToString(), i.InviatoIlUtc, i.CreatoDa, i.Esito))
            .ToListAsync(ct);

    /// <summary>
    /// Genera un documento e lo consegna gia' in PDF.
    ///
    /// La conversione sta qui e non nella schermata perche' il
    /// convertitore e' interno a questo progetto, ed e' lo stesso usato
    /// per DVR e preventivi: un PDF della manualistica deve venire su
    /// come tutti gli altri.
    /// </summary>
    /// <param name="percorsoWwwRoot">Serve al convertitore per la carta intestata.</param>
    public async Task<EsitoGenerazione?> GeneraPdfAsync(
        int idCompilazione, string codiceModello, string? percorsoWwwRoot,
        string? upn = null, CancellationToken ct = default)
    {
        var esito = await GeneraAsync(idCompilazione, codiceModello, upn, archivia: false, ct);
        if (esito?.Contenuto is null) return esito;

        var pdf = ConvertitoreWordPdf.Converti(esito.Contenuto, percorsoWwwRoot);
        var nome = Path.ChangeExtension(esito.NomeFile, ".pdf");

        await ArchiviaPdfAsync(idCompilazione, codiceModello, nome, pdf, upn, ct);
        await AvanzaStatoAsync(idCompilazione, StatoCompilazione.Generata, upn, ct);

        return esito with { NomeFile = nome, Contenuto = pdf };
    }

    /// <summary>
    /// Archivia un PDF appena convertito. Nell'archivio resta quello che
    /// e' uscito davvero: se al cliente si manda il PDF, e' il PDF a
    /// dover essere ritrovabile, non il Word da cui viene.
    /// </summary>
    public async Task<int?> ArchiviaPdfAsync(
        int idCompilazione, string codiceModello, string nomeFile, byte[] pdf,
        string? upn, CancellationToken ct = default)
    {
        var compilazione = await _db.Compilazioni.AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdCompilazione == idCompilazione, ct);
        if (compilazione is null) return null;

        var idRevisione = await _db.RevisioniModello.AsNoTracking()
            .Where(r => r.Attivo && r.ModelloDocumento!.Codice == codiceModello
                        && r.Stato == StatoRevisioneModello.Approvato)
            .OrderByDescending(r => r.NumeroRevisione)
            .Select(r => (int?)r.IdRevisioneModello)
            .FirstOrDefaultAsync(ct);

        return await _archivio.ArchiviaAsync(
            compilazione, codiceModello, idRevisione, nomeFile, pdf,
            FormatoManualeGenerato.Pdf, upn, ct);
    }

    /// <summary>
    /// Annulla una compilazione: non si cancella, si spegne con il
    /// motivo. Un pacchetto gia' consegnato resta spiegabile anche
    /// quando la compilazione che l'ha prodotto e' stata abbandonata.
    /// </summary>
    public async Task<bool> AnnullaAsync(
        int idCompilazione, string motivo, string? upn, CancellationToken ct = default)
    {
        var c = await _db.Compilazioni.FirstOrDefaultAsync(x => x.IdCompilazione == idCompilazione, ct);
        if (c is null) return false;

        c.Attivo = false;
        c.Stato = StatoCompilazione.Annullata;
        c.MotivoAnnullamento = motivo;
        c.ModificatoIlUtc = DateTime.UtcNow;
        c.ModificatoDa = upn;

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
