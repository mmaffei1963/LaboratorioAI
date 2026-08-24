using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Proposte di controllo (schema "controlli"). Staging: nessuna scrittura su ArcLab.

public class ConfigurazionePropostaControllo : IEntityTypeConfiguration<PropostaControllo>
{
    public void Configure(EntityTypeBuilder<PropostaControllo> e)
    {
        e.ToTable("Proposte", "controlli");
        e.HasKey(x => x.IdPropostaControllo);
        e.Property(x => x.CodiceLocale).HasMaxLength(40).IsRequired();
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.Controllo).HasMaxLength(255);
        e.Property(x => x.Note).HasMaxLength(2000);
        e.Property(x => x.Origine).HasMaxLength(50);
        e.Property(x => x.NomeFileTracciato).HasMaxLength(255);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.CodiceLocale).IsUnique();
        e.HasIndex(x => new { x.Stato, x.Attivo });
    }
}

public class ConfigurazionePropostaRigaMetodo : IEntityTypeConfiguration<PropostaRigaMetodo>
{
    public void Configure(EntityTypeBuilder<PropostaRigaMetodo> e)
    {
        e.ToTable("RigheMetodo", "controlli");
        e.HasKey(x => x.IdPropostaRigaMetodo);
        e.Property(x => x.Metodo).HasMaxLength(255);
        e.Property(x => x.DenominazioneRiga).HasMaxLength(255);
        e.Property(x => x.RifBiblio).HasMaxLength(255);
        e.Property(x => x.CostoMetodoEuro).HasColumnType("money");
        e.Property(x => x.UnitaMisura).HasMaxLength(50);
        e.Property(x => x.LabEsterno).HasMaxLength(100);
        e.HasOne(x => x.Proposta).WithMany(p => p.Righe)
         .HasForeignKey(x => x.IdPropostaControllo).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.IdPropostaControllo);
    }
}
