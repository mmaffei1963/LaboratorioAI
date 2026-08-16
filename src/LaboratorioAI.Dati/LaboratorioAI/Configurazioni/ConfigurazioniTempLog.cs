using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Monitoraggio TempLog Econorma (schema "templog").

public class ConfigurazioneMappaturaCanaleTempLog : IEntityTypeConfiguration<MappaturaCanaleTempLog>
{
    public void Configure(EntityTypeBuilder<MappaturaCanaleTempLog> e)
    {
        e.ToTable("MappatureCanali", "templog");
        e.HasKey(x => x.IdMappatura);
        e.Property(x => x.Canale).HasMaxLength(20).IsRequired();
        e.Property(x => x.Sonda).HasMaxLength(255);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.Canale, x.Attivo });
    }
}

public class ConfigurazioneCaricamentoTempLog : IEntityTypeConfiguration<CaricamentoTempLog>
{
    public void Configure(EntityTypeBuilder<CaricamentoTempLog> e)
    {
        e.ToTable("Caricamenti", "templog");
        e.HasKey(x => x.IdCaricamento);
        e.Property(x => x.NomeFile).HasMaxLength(255);
        e.Property(x => x.CartellaOrigine).HasMaxLength(500);
        e.Property(x => x.EsitoValidazione).HasMaxLength(20);
        e.Property(x => x.Validatore).HasMaxLength(255);
        e.Property(x => x.NoteValidazione).HasMaxLength(1000);
        e.Property(x => x.VersioneApplicazione).HasMaxLength(50);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.DataCreazione);
    }
}

public class ConfigurazioneSincronizzazioneSondeTempLog : IEntityTypeConfiguration<SincronizzazioneSondeTempLog>
{
    public void Configure(EntityTypeBuilder<SincronizzazioneSondeTempLog> e)
    {
        e.ToTable("Sincronizzazioni", "templog");
        e.HasKey(x => x.IdSincronizzazione);
        e.Property(x => x.DataSincronizzazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.DataSincronizzazione);
    }
}
