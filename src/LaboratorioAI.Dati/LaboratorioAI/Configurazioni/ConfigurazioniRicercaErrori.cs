using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

/// <summary>Denominazioni dei metodi ArcLab segnalate da «Ricerca errori» e giudicate corrette (schema app).</summary>
public class ConfigurazioneDenominazioneMetodoVerificata : IEntityTypeConfiguration<DenominazioneMetodoVerificata>
{
    public void Configure(EntityTypeBuilder<DenominazioneMetodoVerificata> e)
    {
        e.ToTable("DenominazioniMetodoVerificate", "app");
        e.HasKey(x => x.IdDenominazioneVerificata);
        e.Property(x => x.Tipo).HasMaxLength(30).IsRequired();
        e.Property(x => x.DenominazioneVerificata).HasMaxLength(500);
        e.Property(x => x.ParametroVerificato).HasMaxLength(300);
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.NomeUtente).HasMaxLength(200);
        e.Property(x => x.Attivo).HasDefaultValue(true);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.IdMetodoArcLab, x.Tipo, x.Attivo });
    }
}
