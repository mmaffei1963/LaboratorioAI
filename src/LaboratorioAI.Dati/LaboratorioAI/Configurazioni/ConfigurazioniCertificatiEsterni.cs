using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Certificati esterni (schema "certificati").

public class ConfigurazioneEstrazioneCertificato : IEntityTypeConfiguration<EstrazioneCertificatoArchiviata>
{
    public void Configure(EntityTypeBuilder<EstrazioneCertificatoArchiviata> e)
    {
        e.ToTable("EstrazioniEsterni", "certificati");
        e.HasKey(x => x.IdEstrazione);
        e.Property(x => x.Hash).HasMaxLength(64).IsRequired();
        e.Property(x => x.NomeFileOriginale).HasMaxLength(300);
        e.Property(x => x.ModelloAi).HasMaxLength(100);
        e.Property(x => x.EstrazioneJson).IsRequired();
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.Hash);
    }
}
