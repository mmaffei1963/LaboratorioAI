using LaboratorioAI.Tracciabilita.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LaboratorioAI.Tracciabilita.Dati;

// Struttura delle tabelle: chiavi, collegamenti, indici, vincoli.
// Regola del progetto: tabelle in italiano al plurale, chiavi Id<Entità> INT IDENTITY.
public partial class ContestoTracciabilita
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurazione)
    {
        configurazione.Properties<decimal>().HavePrecision(18, 3);
        configurazione.Properties<string>().HaveMaxLength(300);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema(Schema);

        ConfiguraChiavi(mb);
        ConfiguraCollegamenti(mb);
        ConfiguraIndiciEVincoli(mb);
        ConfiguraTesti(mb);
        ApplicaFiltriAzienda(mb);

        mb.Entity<Allergene>().HasData(ElencoAllergeni());

        // Mai cancellazioni a cascata: nessuna riga sparisce per effetto di un'altra.
        foreach (var fk in mb.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    // Le chiavi si chiamano Id<Entità>, tranne queste eccezioni.
    private static readonly Dictionary<Type, string> _chiaviParticolari = new()
    {
        [typeof(ImpostazioniAzienda)] = nameof(ImpostazioniAzienda.IdImpostazioni),
        [typeof(VoceAudit)] = nameof(VoceAudit.IdAudit),
        [typeof(MovimentoLotto)] = nameof(MovimentoLotto.IdMovimento),
        [typeof(CategoriaMateriaPrima)] = nameof(CategoriaMateriaPrima.IdCategoria),
        [typeof(CampoAggiuntivo)] = nameof(CampoAggiuntivo.IdCampo),
        [typeof(ContatoreLotto)] = nameof(ContatoreLotto.IdContatore),
    };

    private static void ConfiguraChiavi(ModelBuilder mb)
    {
        foreach (var tipo in mb.Model.GetEntityTypes().Select(t => t.ClrType).ToList())
        {
            var chiave = _chiaviParticolari.TryGetValue(tipo, out var nome) ? nome : "Id" + tipo.Name;
            mb.Entity(tipo).HasKey(chiave);
            mb.Entity(tipo).Property(chiave).ValueGeneratedOnAdd();
        }
    }

    private static void ConfiguraCollegamenti(ModelBuilder mb)
    {
        // Ogni riga con IdAzienda punta all'azienda.
        foreach (var tipo in mb.Model.GetEntityTypes().Select(t => t.ClrType).ToList())
        {
            if (tipo == typeof(Azienda)) continue;
            if (tipo.GetProperty("IdAzienda") is null) continue;
            mb.Entity(tipo).HasOne(typeof(Azienda)).WithMany().HasForeignKey("IdAzienda");
        }

        // Struttura
        mb.Entity<UtenteInsediamento>().HasOne<Utente>().WithMany().HasForeignKey(x => x.IdUtente);
        mb.Entity<UtenteInsediamento>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamento);
        mb.Entity<Esportazione>().HasOne<Utente>().WithMany().HasForeignKey(x => x.IdUtente);

        // Anagrafiche
        mb.Entity<ProfiloControllo>().HasMany(x => x.Righe).WithOne().HasForeignKey(x => x.IdProfiloControllo);
        // Le righe già presenti restano attive quando si aggiunge la colonna (migrazione ProfiloRigaAttiva).
        mb.Entity<ProfiloControlloRiga>().Property(x => x.Attivo).HasDefaultValue(true);
        mb.Entity<CategoriaMateriaPrima>().HasOne<ProfiloControllo>().WithMany().HasForeignKey(x => x.IdProfiloControllo);
        mb.Entity<Articolo>().HasOne<CategoriaMateriaPrima>().WithMany().HasForeignKey(x => x.IdCategoria);
        mb.Entity<ArticoloAllergene>().HasOne<Articolo>().WithMany().HasForeignKey(x => x.IdArticolo);
        mb.Entity<ArticoloAllergene>().HasOne<Allergene>().WithMany().HasForeignKey(x => x.IdAllergene);
        mb.Entity<ArticoloFornitore>().HasOne<Articolo>().WithMany().HasForeignKey(x => x.IdArticolo);
        mb.Entity<ArticoloFornitore>().HasOne<Fornitore>().WithMany().HasForeignKey(x => x.IdFornitore);
        mb.Entity<Ricetta>().HasOne<Articolo>().WithMany().HasForeignKey(x => x.IdArticolo);
        mb.Entity<Ricetta>().HasMany(x => x.Versioni).WithOne().HasForeignKey(x => x.IdRicetta);
        mb.Entity<RicettaVersione>().HasMany(x => x.Righe).WithOne().HasForeignKey(x => x.IdRicettaVersione);
        mb.Entity<RicettaRiga>().HasOne<CategoriaMateriaPrima>().WithMany().HasForeignKey(x => x.IdCategoria);
        mb.Entity<RicettaRiga>().HasOne<Articolo>().WithMany().HasForeignKey(x => x.IdArticoloSemilavorato);
        mb.Entity<RicettaRigaCategoria>().HasOne<RicettaRiga>().WithMany().HasForeignKey(x => x.IdRicettaRiga);
        mb.Entity<RicettaRigaCategoria>().HasOne<CategoriaMateriaPrima>().WithMany().HasForeignKey(x => x.IdCategoria).OnDelete(DeleteBehavior.NoAction);
        mb.Entity<RicettaRigaCategoria>().HasIndex(x => new { x.IdRicettaRiga, x.IdCategoria }).IsUnique();
        mb.Entity<ValoreCampo>().HasOne<CampoAggiuntivo>().WithMany().HasForeignKey(x => x.IdCampo);

        // Ordini e accettazioni
        mb.Entity<Ordine>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamento);
        mb.Entity<Ordine>().HasOne<Fornitore>().WithMany().HasForeignKey(x => x.IdFornitore);
        mb.Entity<Ordine>().HasMany(x => x.Righe).WithOne().HasForeignKey(x => x.IdOrdine);
        mb.Entity<OrdineRiga>().HasOne<Articolo>().WithMany().HasForeignKey(x => x.IdArticolo);
        mb.Entity<OrdineRiga>().HasOne<CategoriaMateriaPrima>().WithMany().HasForeignKey(x => x.IdCategoria).OnDelete(DeleteBehavior.NoAction);
        mb.Entity<Accettazione>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamento);
        mb.Entity<Accettazione>().HasOne<OrdineRiga>().WithMany().HasForeignKey(x => x.IdOrdineRiga);
        mb.Entity<Accettazione>().HasOne<Fornitore>().WithMany().HasForeignKey(x => x.IdFornitore);
        mb.Entity<Accettazione>().HasOne<Articolo>().WithMany().HasForeignKey(x => x.IdArticolo);
        // Accettazione -> Lotto con collegamento; il verso opposto (Lotto.IdAccettazione) è solo un riferimento,
        // senza vincolo, per evitare un giro vizioso che impedirebbe di salvare i due record insieme.
        mb.Entity<Accettazione>().HasOne<Lotto>().WithMany().HasForeignKey(x => x.IdLotto);
        mb.Entity<Accettazione>().HasMany(x => x.Controlli).WithOne().HasForeignKey(x => x.IdAccettazione);

        mb.Entity<Allegato>().HasOne<Accettazione>().WithMany().HasForeignKey(x => x.IdAccettazione);
        mb.Entity<Allegato>().HasOne<Lotto>().WithMany().HasForeignKey(x => x.IdLotto);
        mb.Entity<Allegato>().HasOne<Fornitore>().WithMany().HasForeignKey(x => x.IdFornitore);
        mb.Entity<Allegato>().HasOne<Spedizione>().WithMany().HasForeignKey(x => x.IdSpedizione);

        // Lotti e movimenti
        mb.Entity<Lotto>().HasOne<Articolo>().WithMany().HasForeignKey(x => x.IdArticolo);
        mb.Entity<Lotto>().HasOne<Fornitore>().WithMany().HasForeignKey(x => x.IdFornitore);
        mb.Entity<Lotto>().HasOne<CategoriaMateriaPrima>().WithMany().HasForeignKey(x => x.IdCategoria);

        mb.Entity<MovimentoLotto>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamento);
        mb.Entity<MovimentoLotto>().HasOne<Lotto>().WithMany().HasForeignKey(x => x.IdLotto);
        mb.Entity<MovimentoLotto>().HasOne<Accettazione>().WithMany().HasForeignKey(x => x.IdAccettazione);
        mb.Entity<MovimentoLotto>().HasOne<Produzione>().WithMany().HasForeignKey(x => x.IdProduzione);
        mb.Entity<MovimentoLotto>().HasOne<Spedizione>().WithMany().HasForeignKey(x => x.IdSpedizione);
        mb.Entity<MovimentoLotto>().HasOne<Trasferimento>().WithMany().HasForeignKey(x => x.IdTrasferimento);
        mb.Entity<MovimentoLotto>().HasOne<MovimentoLotto>().WithMany().HasForeignKey(x => x.IdMovimentoStornato);

        mb.Entity<LottoInUso>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamento);
        mb.Entity<LottoInUso>().HasOne<CategoriaMateriaPrima>().WithMany().HasForeignKey(x => x.IdCategoria);
        mb.Entity<LottoInUso>().HasOne<Lotto>().WithMany().HasForeignKey(x => x.IdLotto);
        mb.Entity<ContatoreLotto>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamento);

        // Produzioni
        mb.Entity<Produzione>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamento);
        mb.Entity<Produzione>().HasOne<RicettaVersione>().WithMany().HasForeignKey(x => x.IdRicettaVersione);
        mb.Entity<Produzione>().HasOne<Articolo>().WithMany().HasForeignKey(x => x.IdArticolo);
        mb.Entity<Produzione>().HasOne<Lotto>().WithMany().HasForeignKey(x => x.IdLottoProdotto);
        mb.Entity<Produzione>().HasMany(x => x.Consumi).WithOne().HasForeignKey(x => x.IdProduzione);
        mb.Entity<ProduzioneConsumo>().HasOne<CategoriaMateriaPrima>().WithMany().HasForeignKey(x => x.IdCategoria);
        mb.Entity<ProduzioneConsumo>().HasOne<Lotto>().WithMany().HasForeignKey(x => x.IdLottoIngresso);

        // Spedizioni, trasferimenti, richiami
        mb.Entity<Spedizione>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamento);
        mb.Entity<Spedizione>().HasOne<Cliente>().WithMany().HasForeignKey(x => x.IdCliente);
        mb.Entity<Spedizione>().HasMany(x => x.Righe).WithOne().HasForeignKey(x => x.IdSpedizione);
        mb.Entity<SpedizioneRiga>().HasOne<Lotto>().WithMany().HasForeignKey(x => x.IdLotto);

        mb.Entity<Trasferimento>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamentoOrigine);
        mb.Entity<Trasferimento>().HasOne<Insediamento>().WithMany().HasForeignKey(x => x.IdInsediamentoDestinazione);
        mb.Entity<Trasferimento>().HasMany(x => x.Righe).WithOne().HasForeignKey(x => x.IdTrasferimento);
        mb.Entity<TrasferimentoRiga>().HasOne<Lotto>().WithMany().HasForeignKey(x => x.IdLotto);

        mb.Entity<Richiamo>().HasOne<Lotto>().WithMany().HasForeignKey(x => x.IdLottoOrigine);
    }

    private static void ConfiguraIndiciEVincoli(ModelBuilder mb)
    {
        mb.Entity<ImpostazioniAzienda>().HasIndex(x => x.IdAzienda).IsUnique();
        mb.Entity<Insediamento>().HasIndex(x => new { x.IdAzienda, x.Sigla }).IsUnique();
        mb.Entity<Utente>().HasIndex(x => x.Email).IsUnique();
        mb.Entity<Allergene>().HasIndex(x => x.Codice).IsUnique();

        // Un lotto è identificato da azienda + articolo + fornitore + codice + (giorno, per i lotti interni).
        // HasFilter(null): senza, EF aggiunge un filtro sulle colonne vuote e i lotti di ingresso (senza giorno interno)
        // e quelli interni (senza fornitore) resterebbero fuori dal controllo. In SQL Server i vuoti contano come uguali.
        mb.Entity<Lotto>().HasIndex(x => new { x.IdAzienda, x.IdArticolo, x.IdFornitore, x.Codice, x.GiornoLottoInterno })
            .IsUnique().HasFilter(null);
        mb.Entity<Lotto>().HasIndex(x => new { x.IdAzienda, x.Codice });

        // Un movimento si può stornare una volta sola.
        mb.Entity<MovimentoLotto>().HasIndex(x => x.IdMovimentoStornato).IsUnique()
            .HasFilter("[IdMovimentoStornato] IS NOT NULL");
        mb.Entity<MovimentoLotto>().HasIndex(x => new { x.IdLotto, x.IdInsediamento });

        mb.Entity<LottoInUso>().HasIndex(x => new { x.IdInsediamento, x.IdCategoria, x.Fine });
        mb.Entity<ContatoreLotto>().HasIndex(x => new { x.IdInsediamento, x.Giorno }).IsUnique();
        mb.Entity<ProduzioneConsumo>().HasIndex(x => x.IdLottoIngresso);
        mb.Entity<Produzione>().HasIndex(x => x.IdLottoProdotto);
        mb.Entity<Produzione>().HasIndex(x => new { x.IdInsediamento, x.Giorno });
        mb.Entity<SpedizioneRiga>().HasIndex(x => x.IdLotto);
        mb.Entity<Spedizione>().HasIndex(x => new { x.IdInsediamento, x.DataSpedizione });
        mb.Entity<Ordine>().HasIndex(x => new { x.IdAzienda, x.Numero }).IsUnique();
        mb.Entity<VoceAudit>().HasIndex(x => new { x.IdAzienda, x.DataOra });

        // Una riga di ricetta è o una categoria o un semilavorato, mai tutte e due né nessuna.
        mb.Entity<RicettaRiga>().ToTable(t => t.HasCheckConstraint(
            "CK_RicetteRighe_Ingrediente",
            "([IdCategoria] IS NOT NULL AND [IdArticoloSemilavorato] IS NULL) OR ([IdCategoria] IS NULL AND [IdArticoloSemilavorato] IS NOT NULL)"));
        mb.Entity<Trasferimento>().ToTable(t => t.HasCheckConstraint(
            "CK_Trasferimenti_SediDiverse", "[IdInsediamentoOrigine] <> [IdInsediamentoDestinazione]"));
    }

    private static readonly string[] _campiTestoLungo =
        { "Note", "NoteReclami", "Nota", "Motivo", "MotivoBlocco", "Istruzione", "Opzioni", "Valore", "Esito", "Errore" };

    private static void ConfiguraTesti(ModelBuilder mb)
    {
        foreach (var entita in mb.Model.GetEntityTypes())
            foreach (var p in entita.GetProperties().Where(p => p.ClrType == typeof(string)))
            {
                if (_campiTestoLungo.Contains(p.Name)) p.SetMaxLength(2000);
                else if (p.Name is "Percorso" or "PercorsoFile") p.SetMaxLength(500);
                else if (p.Name is "ImprontaSha256") p.SetMaxLength(64);
            }

        mb.Entity<VoceAudit>().Property(x => x.Prima).HasColumnType("nvarchar(max)");
        mb.Entity<VoceAudit>().Property(x => x.Dopo).HasColumnType("nvarchar(max)");
        mb.Entity<Richiamo>().Property(x => x.RapportoJson).HasColumnType("nvarchar(max)");
    }

    /// <summary>I 14 allergeni dell'Allegato II del Reg. UE 1169/2011.</summary>
    internal static Allergene[] ElencoAllergeni() => new[]
    {
        Al(1,  "GLUTINE",       "Cereali contenenti glutine"),
        Al(2,  "CROSTACEI",     "Crostacei e prodotti a base di crostacei"),
        Al(3,  "UOVA",          "Uova e prodotti a base di uova"),
        Al(4,  "PESCE",         "Pesce e prodotti a base di pesce"),
        Al(5,  "ARACHIDI",      "Arachidi e prodotti a base di arachidi"),
        Al(6,  "SOIA",          "Soia e prodotti a base di soia"),
        Al(7,  "LATTE",         "Latte e prodotti a base di latte (incluso lattosio)"),
        Al(8,  "FRUTTA_GUSCIO", "Frutta a guscio"),
        Al(9,  "SEDANO",        "Sedano e prodotti a base di sedano"),
        Al(10, "SENAPE",        "Senape e prodotti a base di senape"),
        Al(11, "SESAMO",        "Semi di sesamo e prodotti a base di semi di sesamo"),
        Al(12, "SOLFITI",       "Anidride solforosa e solfiti (oltre 10 mg/kg o 10 mg/l)"),
        Al(13, "LUPINI",        "Lupini e prodotti a base di lupini"),
        Al(14, "MOLLUSCHI",     "Molluschi e prodotti a base di molluschi"),
    };

    private static Allergene Al(int id, string codice, string nome) =>
        new() { IdAllergene = id, Codice = codice, Nome = nome, Ordine = id };
}
