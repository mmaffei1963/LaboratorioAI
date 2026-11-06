using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Invio ai laboratori esterni dell'elenco dei campioni (06/11/2026). Schema "accettazione".

public class ConfigurazioneContattoLaboratorioEsterno : IEntityTypeConfiguration<ContattoLaboratorioEsterno>
{
    public void Configure(EntityTypeBuilder<ContattoLaboratorioEsterno> e)
    {
        e.ToTable("ContattiLaboratoriEsterni", "accettazione");
        e.HasKey(x => x.IdContattoLaboratorioEsterno);
        e.HasIndex(x => x.IdLabEsternoArcLab).IsUnique();
        e.Property(x => x.NomeLaboratorio).HasMaxLength(300).IsRequired();
        e.Property(x => x.EmailDestinatari).HasMaxLength(1000);
        e.Property(x => x.EmailCopia).HasMaxLength(1000);
        e.Property(x => x.Note).HasMaxLength(1000);
    }
}

public class ConfigurazioneInvioLaboratorioEsterno : IEntityTypeConfiguration<InvioLaboratorioEsterno>
{
    public void Configure(EntityTypeBuilder<InvioLaboratorioEsterno> e)
    {
        e.ToTable("InviiLaboratoriEsterni", "accettazione");
        e.HasKey(x => x.IdInvioLaboratorioEsterno);
        e.HasIndex(x => new { x.IdLabEsternoArcLab, x.DataInvio });
        e.Property(x => x.NomeLaboratorio).HasMaxLength(300).IsRequired();
        e.Property(x => x.Dal).HasColumnType("date");
        e.Property(x => x.Al).HasColumnType("date");
        e.Property(x => x.Destinatari).HasMaxLength(1000).IsRequired();
        e.Property(x => x.Copia).HasMaxLength(1000);
        e.Property(x => x.Oggetto).HasMaxLength(300).IsRequired();
        e.Property(x => x.Testo).HasMaxLength(4000);
        e.Property(x => x.NomeFile).HasMaxLength(200).IsRequired();
        e.Property(x => x.MessageId).HasMaxLength(300);
        e.Property(x => x.Errore).HasMaxLength(2000);
        e.Property(x => x.NomeUtente).HasMaxLength(100);
        e.HasMany(x => x.Righe).WithOne(r => r.Invio!)
            .HasForeignKey(r => r.IdInvioLaboratorioEsterno).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneRigaInvioLaboratorioEsterno : IEntityTypeConfiguration<RigaInvioLaboratorioEsterno>
{
    public void Configure(EntityTypeBuilder<RigaInvioLaboratorioEsterno> e)
    {
        e.ToTable("RigheInvioLaboratorioEsterno", "accettazione");
        e.HasKey(x => x.IdRigaInvioLaboratorioEsterno);
        e.HasIndex(x => x.IdProvaArcLab);
        e.Property(x => x.NumeroCampione).HasMaxLength(50).IsRequired();
    }
}
