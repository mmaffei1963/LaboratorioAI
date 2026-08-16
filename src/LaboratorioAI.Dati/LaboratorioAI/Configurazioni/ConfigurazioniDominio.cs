using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

public class ConfigurazioneAnalisiAi : IEntityTypeConfiguration<AnalisiAi>
{
    public void Configure(EntityTypeBuilder<AnalisiAi> e)
    {
        e.ToTable("Analisi", "app");
        e.HasKey(x => x.IdAnalisi);

        e.Property(x => x.Modalita).HasConversion<int>();
        e.Property(x => x.Giudizio).HasConversion<int>();

        e.Property(x => x.DataAnalisi).HasDefaultValueSql("SYSUTCDATETIME()");

        e.Property(x => x.RagioneSociale).HasMaxLength(255);
        e.Property(x => x.DenominazioneCampione).HasMaxLength(255);
        e.Property(x => x.Matrice).HasMaxLength(100);

        e.Property(x => x.Testo).IsRequired();
        e.Property(x => x.ModelloAi).HasMaxLength(50).IsRequired();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20).IsRequired();

        e.Property(x => x.CostoStimato).HasPrecision(10, 4);

        e.HasOne(x => x.Utente).WithMany(u => u.Analisi)
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        // Ricerca dell'ultima analisi di un campione
        e.HasIndex(x => new { x.IdCampione, x.DataAnalisi })
         .HasDatabaseName("IX_Analisi_Campione_Data")
         .IsDescending(false, true);

        // Statistiche e cruscotti
        e.HasIndex(x => x.DataAnalisi).IsDescending();
        e.HasIndex(x => x.Giudizio);
    }
}

public class ConfigurazioneRegistroDvr : IEntityTypeConfiguration<VoceRegistroDvr>
{
    public void Configure(EntityTypeBuilder<VoceRegistroDvr> e)
    {
        e.ToTable("RegistroDvr", "app");
        e.HasKey(x => x.IdVoce);

        e.Property(x => x.RagioneSociale).HasMaxLength(255).IsRequired();
        e.Property(x => x.NomeInsediamento).HasMaxLength(255);
        e.Property(x => x.Regione).HasMaxLength(50).IsRequired();
        e.Property(x => x.NomeFile).HasMaxLength(300).IsRequired();
        e.Property(x => x.PercorsoArchivio).HasMaxLength(500);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.Esito).HasConversion<int>();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20).IsRequired();

        e.Property(x => x.DataGenerazione).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.DataGenerazione).IsDescending();
        e.HasIndex(x => x.IdCliente);
        e.HasIndex(x => x.IdLotto);
    }
}

public class ConfigurazioneConfigurazione : IEntityTypeConfiguration<Configurazione>
{
    public void Configure(EntityTypeBuilder<Configurazione> e)
    {
        e.ToTable("Configurazioni", "app");
        e.HasKey(x => x.IdConfigurazione);

        e.Property(x => x.Chiave).HasMaxLength(100).IsRequired();
        e.Property(x => x.Valore).HasMaxLength(2000);
        e.Property(x => x.Descrizione).HasMaxLength(500);
        e.Property(x => x.Categoria).HasMaxLength(50);
        e.Property(x => x.DataModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.Chiave).IsUnique();
    }
}

public class ConfigurazioneDvrPiscina : IEntityTypeConfiguration<DvrPiscina>
{
    public void Configure(EntityTypeBuilder<DvrPiscina> e)
    {
        e.ToTable("DvrPiscine", "app");
        e.HasKey(x => x.IdDvrPiscina);

        e.Property(x => x.Stato).HasConversion<int>();

        e.Property(x => x.RagioneSociale).HasMaxLength(255).IsRequired();
        e.Property(x => x.NomeInsediamento).HasMaxLength(255);
        e.Property(x => x.Comune).HasMaxLength(150);
        e.Property(x => x.Provincia).HasMaxLength(100);
        e.Property(x => x.Regione).HasMaxLength(50);

        e.Property(x => x.DatiScheda).IsRequired();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20).IsRequired();
        e.Property(x => x.NomeFileGenerato).HasMaxLength(300);
        e.Property(x => x.PercorsoArchivio).HasMaxLength(500);

        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.IdInsediamentoArcLab);
        e.HasIndex(x => x.DataUltimaModifica).IsDescending();
        e.HasIndex(x => x.Stato);
    }
}
