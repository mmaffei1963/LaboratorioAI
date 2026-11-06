using System.Reflection;
using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LaboratorioAI.Tracciabilita.Dati;

/// <summary>
/// Contesto del database «Tracciabilita» (schema <c>trac</c>).
///
/// È separato dal database di Laboratorio AI: qui stanno solo i dati dei clienti del servizio.
/// Non legge né scrive mai ArcLab o AZW.
///
/// Difese contro la fuga di dati tra aziende (a più livelli):
/// <list type="number">
/// <item>filtro globale su <c>IdAzienda</c> per ogni entità dell'azienda;</item>
/// <item>controllo in <see cref="SaveChanges()"/>: non si scrive per un'altra azienda;</item>
/// <item>sicurezza a livello di riga di SQL Server (vedi <see cref="VincoliSicurezzaSql"/>).</item>
/// </list>
/// Regole del progetto applicate qui: nessuna cancellazione fisica; i registri
/// (<see cref="IAppendOnly"/>) non si modificano.
/// </summary>
public partial class ContestoTracciabilita : DbContext
{
    public const string Schema = "trac";

    private readonly IContestoAzienda _contesto;

    public ContestoTracciabilita(DbContextOptions<ContestoTracciabilita> opzioni, IContestoAzienda contesto)
        : base(opzioni)
    {
        _contesto = contesto;
    }

    /// <summary>Azienda per cui si sta lavorando (vuota = nessun dato visibile).</summary>
    public int? IdAziendaCorrente => _contesto.IdAzienda;

    /// <summary>Personale MARE.A: vede tutte le aziende.</summary>
    public bool Gestore => _contesto.Gestore;

    public int? IdUtenteCorrente => _contesto.IdUtente;

    /// <summary>Vera solo durante una chiusura del servizio: permette l'eliminazione fisica.</summary>
    public bool ConsentiEliminazioneFisica { get; private set; }

    /// <summary>Abilita l'eliminazione fisica. Solo il gestore, solo per la chiusura di un servizio.</summary>
    public void AbilitaEliminazioneFisica()
    {
        if (!Gestore)
            throw new InvalidOperationException("Solo il gestore del servizio può eliminare fisicamente i dati.");
        ConsentiEliminazioneFisica = true;
    }

    // ── Struttura ──────────────────────────────────────────────────
    public DbSet<Azienda> Aziende => Set<Azienda>();
    public DbSet<ImpostazioniAzienda> ImpostazioniAziende => Set<ImpostazioniAzienda>();
    public DbSet<Insediamento> Insediamenti => Set<Insediamento>();
    public DbSet<Utente> Utenti => Set<Utente>();
    public DbSet<UtenteInsediamento> UtentiInsediamenti => Set<UtenteInsediamento>();
    public DbSet<VoceAudit> Audit => Set<VoceAudit>();
    public DbSet<Esportazione> Esportazioni => Set<Esportazione>();

    // ── Anagrafiche ────────────────────────────────────────────────
    public DbSet<Fornitore> Fornitori => Set<Fornitore>();
    public DbSet<Cliente> Clienti => Set<Cliente>();
    public DbSet<ProfiloControllo> ProfiliControllo => Set<ProfiloControllo>();
    public DbSet<ProfiloControlloRiga> ProfiliControlloRighe => Set<ProfiloControlloRiga>();
    public DbSet<CategoriaMateriaPrima> CategorieMateriaPrima => Set<CategoriaMateriaPrima>();
    public DbSet<Articolo> Articoli => Set<Articolo>();
    public DbSet<Allergene> Allergeni => Set<Allergene>();
    public DbSet<ArticoloAllergene> ArticoliAllergeni => Set<ArticoloAllergene>();
    public DbSet<ArticoloFornitore> ArticoliFornitori => Set<ArticoloFornitore>();
    public DbSet<Ricetta> Ricette => Set<Ricetta>();
    public DbSet<RicettaVersione> RicetteVersioni => Set<RicettaVersione>();
    public DbSet<RicettaRiga> RicetteRighe => Set<RicettaRiga>();
    public DbSet<RicettaRigaCategoria> RicetteRigheCategorie => Set<RicettaRigaCategoria>();
    public DbSet<CampoAggiuntivo> CampiAggiuntivi => Set<CampoAggiuntivo>();
    public DbSet<ValoreCampo> ValoriCampi => Set<ValoreCampo>();

    // ── Lotti e movimenti ──────────────────────────────────────────
    public DbSet<Ordine> Ordini => Set<Ordine>();
    public DbSet<OrdineRiga> OrdiniRighe => Set<OrdineRiga>();
    public DbSet<Accettazione> Accettazioni => Set<Accettazione>();
    public DbSet<AccettazioneControllo> AccettazioniControlli => Set<AccettazioneControllo>();
    public DbSet<Allegato> Allegati => Set<Allegato>();
    public DbSet<Lotto> Lotti => Set<Lotto>();
    public DbSet<MovimentoLotto> MovimentiLotto => Set<MovimentoLotto>();
    public DbSet<LottoInUso> LottiInUso => Set<LottoInUso>();
    public DbSet<ContatoreLotto> ContatoriLotti => Set<ContatoreLotto>();
    public DbSet<Produzione> Produzioni => Set<Produzione>();
    public DbSet<ProduzioneConsumo> ProduzioniConsumi => Set<ProduzioneConsumo>();
    public DbSet<Spedizione> Spedizioni => Set<Spedizione>();
    public DbSet<SpedizioneRiga> SpedizioniRighe => Set<SpedizioneRiga>();
    public DbSet<Trasferimento> Trasferimenti => Set<Trasferimento>();
    public DbSet<TrasferimentoRiga> TrasferimentiRighe => Set<TrasferimentoRiga>();
    public DbSet<Richiamo> Richiami => Set<Richiamo>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // I filtri per azienda sono su tutte le entità, quindi anche su quelle collegate:
        // l'avviso di EF Core sul «possibile errore» non serve.
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    // ── Filtri per azienda ─────────────────────────────────────────

    private static readonly MethodInfo _filtroObbligatorio =
        typeof(ContestoTracciabilita).GetMethod(nameof(ImpostaFiltroObbligatorio), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo _filtroOpzionale =
        typeof(ContestoTracciabilita).GetMethod(nameof(ImpostaFiltroOpzionale), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private void ApplicaFiltriAzienda(ModelBuilder mb)
    {
        foreach (var tipo in mb.Model.GetEntityTypes().Select(t => t.ClrType).Where(t => t.BaseType == typeof(object)).ToList())
        {
            if (typeof(IDellAzienda).IsAssignableFrom(tipo))
                _filtroObbligatorio.MakeGenericMethod(tipo).Invoke(this, new object[] { mb });
            else if (typeof(IDellAziendaOpzionale).IsAssignableFrom(tipo))
                _filtroOpzionale.MakeGenericMethod(tipo).Invoke(this, new object[] { mb });
        }
    }

    // Se manca l'azienda e non si è gestore, il filtro non lascia passare nulla (comportamento sicuro).
    private void ImpostaFiltroObbligatorio<T>(ModelBuilder mb) where T : class, IDellAzienda =>
        mb.Entity<T>().HasQueryFilter(e => Gestore || (IdAziendaCorrente != null && e.IdAzienda == IdAziendaCorrente));

    private void ImpostaFiltroOpzionale<T>(ModelBuilder mb) where T : class, IDellAziendaOpzionale =>
        mb.Entity<T>().HasQueryFilter(e => Gestore || (IdAziendaCorrente != null && e.IdAzienda == IdAziendaCorrente));

    // ── Salvataggio con le regole del progetto ─────────────────────

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ControllaModifiche();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ControllaModifiche();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static readonly string[] _proprietaDataCreazione =
        { "DataCreazione", "DataCaricamento", "DataRichiesta" };

    /// <summary>
    /// Applica le regole prima di salvare: imposta l'azienda e le date di creazione,
    /// impedisce di scrivere per un'altra azienda, di cancellare fisicamente e di modificare i registri.
    /// Visibile ai test.
    /// </summary>
    internal void ControllaModifiche()
    {
        ChangeTracker.DetectChanges();
        var adesso = DateTime.UtcNow;

        foreach (var voce in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
        {
            var nome = voce.Entity.GetType().Name;

            if (voce.State == EntityState.Deleted && !ConsentiEliminazioneFisica)
                throw new InvalidOperationException(
                    $"Eliminazione fisica non consentita ({nome}): usare «Attivo = false», l'annullamento o uno storno.");

            if (voce.Entity is IAppendOnly && voce.State is EntityState.Modified)
                throw new InvalidOperationException(
                    $"{nome} è un registro in sola aggiunta: non si modifica. Registrare uno storno.");

            if (voce.State == EntityState.Added)
            {
                foreach (var p in _proprietaDataCreazione)
                {
                    var prop = voce.Properties.FirstOrDefault(x => x.Metadata.Name == p && x.Metadata.ClrType == typeof(DateTime));
                    if (prop is { CurrentValue: DateTime d } && d == default)
                        prop.CurrentValue = adesso;
                }
                if (voce.Entity is VoceAudit audit && audit.DataOra == default)
                    audit.DataOra = adesso;
            }

            ControllaAzienda(voce, nome);
        }
    }

    private void ControllaAzienda(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry voce, string nome)
    {
        if (voce.Entity is Azienda)
        {
            // Le aziende le crea e le gestisce solo il personale MARE.A.
            if (!Gestore)
                throw new InvalidOperationException("Solo il gestore del servizio può creare o modificare le aziende.");
            return;
        }

        int? idRiga;
        if (voce.Entity is IDellAzienda obbligatoria)
        {
            if (voce.State == EntityState.Added && obbligatoria.IdAzienda == 0)
            {
                if (IdAziendaCorrente is null)
                    throw new InvalidOperationException($"Impossibile salvare {nome}: nessuna azienda selezionata.");
                obbligatoria.IdAzienda = IdAziendaCorrente.Value;
            }
            idRiga = obbligatoria.IdAzienda;
        }
        else if (voce.Entity is IDellAziendaOpzionale opzionale)
        {
            if (voce.State == EntityState.Added && opzionale.IdAzienda is null && !Gestore)
                opzionale.IdAzienda = IdAziendaCorrente;
            idRiga = opzionale.IdAzienda;
        }
        else
        {
            return;   // entità condivise (es. gli allergeni): nessuna azienda
        }

        if (Gestore) return;

        if (idRiga != IdAziendaCorrente)
            throw new InvalidOperationException($"Non è consentito salvare {nome} per un'altra azienda.");

        // L'azienda di una riga esistente non si cambia mai.
        if (voce.State == EntityState.Modified)
        {
            var proprieta = voce.Property("IdAzienda");
            if (proprieta.IsModified && !Equals(proprieta.OriginalValue, proprieta.CurrentValue))
                throw new InvalidOperationException($"L'azienda di {nome} non si può cambiare.");
        }
    }
}
