using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Accreditamento ACCREDIA dei laboratori esterni (schema "labesterni"). Nessuna scrittura su ArcLab.

public class ConfigurazioneAccreditamentoLabEsterno : IEntityTypeConfiguration<AccreditamentoLabEsterno>
{
    public void Configure(EntityTypeBuilder<AccreditamentoLabEsterno> e)
    {
        e.ToTable("Accreditamenti", "labesterni");
        e.HasKey(x => x.IdAccreditamento);

        e.Property(x => x.NomeLaboratorio).HasMaxLength(200).IsRequired();
        e.Property(x => x.NumeroAccreditamento).HasMaxLength(20).IsRequired();
        e.Property(x => x.Sede).HasMaxLength(10).IsRequired();
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.IdLabEsternoArcLab).IsUnique();
        e.HasIndex(x => new { x.NumeroAccreditamento, x.Sede }).IsUnique();
        e.HasIndex(x => x.Attivo);
    }
}

public class ConfigurazioneRevisioneAccreditamentoEsterno : IEntityTypeConfiguration<RevisioneAccreditamentoEsterno>
{
    public void Configure(EntityTypeBuilder<RevisioneAccreditamentoEsterno> e)
    {
        e.ToTable("Revisioni", "labesterni");
        e.HasKey(x => x.IdRevisione);

        e.Property(x => x.PercorsoFile).HasMaxLength(500);
        e.Property(x => x.HashFileSha256).HasMaxLength(64);
        e.Property(x => x.NoteConferma).HasMaxLength(500);
        e.Property(x => x.DataRilevazione).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne<AccreditamentoLabEsterno>()
            .WithMany()
            .HasForeignKey(x => x.IdAccreditamento)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.IdAccreditamento, x.NumeroRevisione }).IsUnique();
        e.HasIndex(x => x.Confermata);
    }
}

public class ConfigurazioneVerificaMetodoAccreditamento : IEntityTypeConfiguration<VerificaMetodoAccreditamento>
{
    public void Configure(EntityTypeBuilder<VerificaMetodoAccreditamento> e)
    {
        e.ToTable("VerificheMetodo", "labesterni");
        e.HasKey(x => x.IdVerifica);

        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne<AccreditamentoLabEsterno>()
            .WithMany()
            .HasForeignKey(x => x.IdAccreditamento)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.IdAccreditamento, x.IdLabEstMetodoArcLab }).IsUnique();
    }
}

public class ConfigurazioneProvaSospesaLabEsterno : IEntityTypeConfiguration<ProvaSospesaLabEsterno>
{
    public void Configure(EntityTypeBuilder<ProvaSospesaLabEsterno> e)
    {
        e.ToTable("ProveSospese", "labesterni");
        e.HasKey(x => x.IdProvaSospesa);

        e.Property(x => x.ChiaveProva).HasMaxLength(64).IsRequired();
        e.Property(x => x.Matrice).HasMaxLength(1000);
        e.Property(x => x.Prova).HasMaxLength(1000);
        e.Property(x => x.Norma).HasMaxLength(1000);
        e.Property(x => x.Tecnica).HasMaxLength(300);
        e.Property(x => x.DataRilevazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaLettura).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne<AccreditamentoLabEsterno>()
            .WithMany()
            .HasForeignKey(x => x.IdAccreditamento)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.IdAccreditamento, x.ChiaveProva }).IsUnique();
        e.HasIndex(x => x.DataFineSospensione);
    }
}
