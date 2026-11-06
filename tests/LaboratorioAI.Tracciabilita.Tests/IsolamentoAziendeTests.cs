using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using LaboratorioAI.Tracciabilita.Dati;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

/// <summary>
/// Prove delle regole di isolamento tra aziende e di integrità dei dati.
/// Non serve un database: il contesto si costruisce senza collegarsi a SQL Server.
/// Le protezioni dentro SQL Server (trigger, sicurezza a livello di riga) vanno invece provate
/// sul database di prova.
/// </summary>
public class IsolamentoAziendeTests
{
    private static ContestoTracciabilita Crea(IContestoAzienda contesto)
    {
        var opzioni = new DbContextOptionsBuilder<ContestoTracciabilita>()
            .UseSqlServer("Server=.;Database=NonUsato;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        return new ContestoTracciabilita(opzioni, contesto);
    }

    private static ContestoTracciabilita Azienda(int id) => Crea(new ContestoDiAzienda(id, 10));
    private static ContestoTracciabilita Gestore() => Crea(new ContestoGestore());

    // ── Modello ────────────────────────────────────────────────────

    [Fact]
    public void Ogni_tabella_dell_azienda_ha_il_filtro_per_azienda()
    {
        using var db = Azienda(1);
        var senzaFiltro = db.Model.GetEntityTypes()
            .Where(t => typeof(IDellAzienda).IsAssignableFrom(t.ClrType) || typeof(IDellAziendaOpzionale).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetQueryFilter() is null)
            .Select(t => t.ClrType.Name)
            .ToList();
        Assert.Empty(senzaFiltro);
    }

    [Fact]
    public void Gli_allergeni_sono_condivisi_e_senza_filtro()
    {
        using var db = Azienda(1);
        Assert.Null(db.Model.FindEntityType(typeof(Allergene))!.GetQueryFilter());
    }

    [Fact]
    public void Ogni_riga_ha_la_colonna_azienda_o_e_condivisa()
    {
        using var db = Azienda(1);
        var senza = db.Model.GetEntityTypes()
            .Where(t => t.ClrType != typeof(Allergene) && t.FindProperty("IdAzienda") is null)
            .Select(t => t.ClrType.Name).ToList();
        Assert.Empty(senza);
    }

    [Fact]
    public void Nessun_collegamento_cancella_a_cascata()
    {
        using var db = Azienda(1);
        var pericolosi = db.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior != DeleteBehavior.Restrict).ToList();
        Assert.Empty(pericolosi);
    }

    [Fact]
    public void Tabelle_nello_schema_trac_e_chiavi_Id_Entita()
    {
        using var db = Azienda(1);
        foreach (var t in db.Model.GetEntityTypes())
        {
            Assert.Equal("trac", t.GetSchema());
            var chiave = Assert.Single(t.FindPrimaryKey()!.Properties);
            Assert.StartsWith("Id", chiave.Name);
        }
        Assert.Equal("MovimentiLotto", db.Model.FindEntityType(typeof(MovimentoLotto))!.GetTableName());
        Assert.Equal("Audit", db.Model.FindEntityType(typeof(VoceAudit))!.GetTableName());
    }

    [Fact]
    public void L_unicita_dei_lotti_vale_per_tutti_i_lotti_senza_filtri()
    {
        using var db = Azienda(1);
        var indice = db.Model.FindEntityType(typeof(Lotto))!.GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Any(p => p.Name == "GiornoLottoInterno"));
        Assert.Null(indice.GetFilter());
    }

    [Fact]
    public void Allergeni_dell_allegato_II_sono_14()
    {
        using var db = Azienda(1);
        var modello = db.GetService<IDesignTimeModel>().Model;
        var dati = modello.FindEntityType(typeof(Allergene))!.GetSeedData().ToList();
        Assert.Equal(14, dati.Count);
        Assert.Equal(14, dati.Select(d => d["Codice"]).Distinct().Count());
    }

    [Fact]
    public void Il_filtro_confronta_l_azienda_e_lascia_passare_il_gestore()
    {
        using var db = Azienda(7);
        var filtro = db.Model.FindEntityType(typeof(Fornitore))!.GetQueryFilter()!.ToString();
        Assert.Contains("IdAzienda", filtro);
        Assert.Contains("Gestore", filtro);
        Assert.Contains("IdAziendaCorrente", filtro);
    }

    [Fact]
    public void Senza_azienda_e_senza_gestore_il_contesto_non_vede_nulla()
    {
        using var db = Crea(new ContestoAnonimo());
        Assert.False(db.Gestore);
        Assert.Null(db.IdAziendaCorrente);
        // Il filtro richiede «gestore» oppure «azienda indicata e uguale»: con entrambi assenti non passa nessuna riga.
        var filtro = db.Model.FindEntityType(typeof(Lotto))!.GetQueryFilter()!.ToString();
        Assert.Contains("IdAziendaCorrente != null", filtro);
    }

    // ── Regole di salvataggio ──────────────────────────────────────

    [Fact]
    public void Una_nuova_riga_prende_l_azienda_corrente_e_la_data_di_creazione()
    {
        using var db = Azienda(1);
        var f = new Fornitore { RagioneSociale = "Molino" };
        db.Fornitori.Add(f);
        var cli = new Cliente { RagioneSociale = "Bar" };
        db.Clienti.Add(cli);
        var ord = new Ordine { Numero = "1", IdInsediamento = 1, IdFornitore = 1 };
        db.Ordini.Add(ord);

        db.ControllaModifiche();

        Assert.Equal(1, f.IdAzienda);
        Assert.Equal(1, cli.IdAzienda);
        Assert.Equal(1, ord.IdAzienda);
        Assert.NotEqual(default, ord.DataCreazione);
    }

    [Fact]
    public void Non_si_puo_scrivere_per_un_altra_azienda()
    {
        using var db = Azienda(1);
        db.Fornitori.Add(new Fornitore { RagioneSociale = "Intruso", IdAzienda = 2 });
        var ex = Assert.Throws<InvalidOperationException>(() => db.ControllaModifiche());
        Assert.Contains("altra azienda", ex.Message);
    }

    [Fact]
    public void L_azienda_di_una_riga_esistente_non_si_cambia()
    {
        using var db = Azienda(1);
        var f = new Fornitore { IdFornitore = 5, IdAzienda = 1, RagioneSociale = "Molino" };
        db.Fornitori.Attach(f);
        f.IdAzienda = 2;
        Assert.Throws<InvalidOperationException>(() => db.ControllaModifiche());
    }

    [Fact]
    public void Una_azienda_non_puo_creare_altre_aziende()
    {
        using var db = Azienda(1);
        db.Aziende.Add(new Azienda { RagioneSociale = "Nuova" });
        Assert.Throws<InvalidOperationException>(() => db.ControllaModifiche());
    }

    [Fact]
    public void Il_gestore_crea_aziende_ma_non_scrive_righe_senza_dire_per_chi()
    {
        using var db = Gestore();
        db.Aziende.Add(new Azienda { RagioneSociale = "Piadine Srl" });
        db.ControllaModifiche();   // consentito

        using var db2 = Gestore();
        db2.Fornitori.Add(new Fornitore { RagioneSociale = "Senza azienda" });
        var ex = Assert.Throws<InvalidOperationException>(() => db2.ControllaModifiche());
        Assert.Contains("nessuna azienda", ex.Message);
    }

    [Fact]
    public void Il_gestore_puo_scrivere_per_un_azienda_indicata()
    {
        using var db = Gestore();
        db.Fornitori.Add(new Fornitore { RagioneSociale = "Molino", IdAzienda = 3 });
        db.ControllaModifiche();
    }

    [Fact]
    public void Nessuna_cancellazione_fisica_per_le_aziende()
    {
        using var db = Azienda(1);
        var f = new Fornitore { IdFornitore = 5, IdAzienda = 1, RagioneSociale = "Molino" };
        db.Fornitori.Attach(f);
        db.Fornitori.Remove(f);
        var ex = Assert.Throws<InvalidOperationException>(() => db.ControllaModifiche());
        Assert.Contains("Eliminazione fisica", ex.Message);
    }

    [Fact]
    public void Il_gestore_non_cancella_senza_abilitazione_esplicita()
    {
        using var db = Gestore();
        var f = new Fornitore { IdFornitore = 5, IdAzienda = 1, RagioneSociale = "Molino" };
        db.Fornitori.Attach(f);
        db.Fornitori.Remove(f);
        Assert.Throws<InvalidOperationException>(() => db.ControllaModifiche());

        db.AbilitaEliminazioneFisica();
        db.ControllaModifiche();   // ora consentito (chiusura del servizio)
    }

    [Fact]
    public void Solo_il_gestore_puo_abilitare_l_eliminazione_fisica()
    {
        using var db = Azienda(1);
        Assert.Throws<InvalidOperationException>(() => db.AbilitaEliminazioneFisica());
    }

    [Fact]
    public void I_movimenti_dei_lotti_non_si_modificano()
    {
        using var db = Azienda(1);
        var m = new MovimentoLotto { IdMovimento = 1, IdAzienda = 1, IdInsediamento = 1, IdLotto = 1, Tipo = TipoMovimento.Carico, Quantita = 10m };
        db.MovimentiLotto.Attach(m);
        m.Quantita = 99m;
        var ex = Assert.Throws<InvalidOperationException>(() => db.ControllaModifiche());
        Assert.Contains("sola aggiunta", ex.Message);
    }

    [Fact]
    public void I_movimenti_dei_lotti_non_si_cancellano_senza_abilitazione()
    {
        // Senza abilitazione esplicita l'app rifiuta la cancellazione; con l'abilitazione (solo gestore) resta il trigger di SQL Server.
        using var db = Azienda(1);
        var m = new MovimentoLotto { IdMovimento = 1, IdAzienda = 1, IdInsediamento = 1, IdLotto = 1, Tipo = TipoMovimento.Carico, Quantita = 10m };
        db.MovimentiLotto.Attach(m);
        db.MovimentiLotto.Remove(m);
        Assert.Throws<InvalidOperationException>(() => db.ControllaModifiche());
    }

    [Fact]
    public void Un_nuovo_movimento_si_aggiunge()
    {
        using var db = Azienda(1);
        var m = new MovimentoLotto { IdInsediamento = 1, IdLotto = 1, Tipo = TipoMovimento.Carico, Quantita = 10m };
        db.MovimentiLotto.Add(m);
        db.ControllaModifiche();
        Assert.Equal(1, m.IdAzienda);
        Assert.NotEqual(default, m.DataCreazione);
    }

    [Fact]
    public void L_audit_non_si_modifica_e_la_data_si_imposta_da_sola()
    {
        using var db = Azienda(1);
        var voce = new VoceAudit { Azione = "Creato", Entita = "Fornitore" };
        db.Audit.Add(voce);
        db.ControllaModifiche();
        Assert.Equal(1, voce.IdAzienda);
        Assert.NotEqual(default, voce.DataOra);

        using var db2 = Azienda(1);
        var esistente = new VoceAudit { IdAudit = 3, IdAzienda = 1, Azione = "x", Entita = "y", DataOra = DateTime.UtcNow };
        db2.Audit.Attach(esistente);
        esistente.Azione = "alterata";
        Assert.Throws<InvalidOperationException>(() => db2.ControllaModifiche());
    }

    // ── Protezioni SQL ─────────────────────────────────────────────

    [Fact]
    public void I_trigger_riguardano_tabelle_che_esistono_nel_modello()
    {
        using var db = Azienda(1);
        var tabelle = db.Model.GetEntityTypes().Select(t => t.GetTableName()).ToHashSet();
        var istruzioni = VincoliSicurezzaSql.Triggers().ToList();
        Assert.Equal(2, istruzioni.Count);
        Assert.Contains("MovimentiLotto", tabelle);
        Assert.Contains("Audit", tabelle);
        Assert.All(istruzioni, s =>
        {
            Assert.Contains("INSTEAD OF UPDATE, DELETE", s);
            Assert.Contains("THROW 51000", s);
        });
    }

    [Fact]
    public async Task I_vincoli_SQL_non_si_applicano_senza_il_contesto_del_gestore()
    {
        using var db = Azienda(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => VincoliSicurezzaSql.ApplicaAsync(db, attivaRls: false));
    }
}
