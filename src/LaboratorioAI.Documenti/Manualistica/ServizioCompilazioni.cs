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

    /// <summary>Il contesto del database e la lettura di ArcLab.</summary>
    public ServizioCompilazioni(ContestoLaboratorioAI db, IRepositoryAnagrafiche anagrafiche)
    {
        _db = db;
        _anagrafiche = anagrafiche;
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
    public Task<IReadOnlyList<Core.Modelli.VoceAnagrafica>> ClientiAsync(CancellationToken ct = default) =>
        _anagrafiche.ClientiAsync(ct);

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
        if (c is null || !c.Attivo) return false;

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
