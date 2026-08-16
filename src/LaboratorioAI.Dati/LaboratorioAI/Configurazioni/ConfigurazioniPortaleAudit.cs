using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

public class ConfigurazioneSessionePortale : IEntityTypeConfiguration<SessionePortale>
{
    public void Configure(EntityTypeBuilder<SessionePortale> e)
    {
        e.ToTable("SessioniPortale", "portale");
        e.HasKey(x => x.IdSessione);

        e.Property(x => x.Token).HasMaxLength(128).IsRequired();
        e.Property(x => x.CodiceContabilita).HasMaxLength(50);
        e.Property(x => x.RagioneSociale).HasMaxLength(255);
        e.Property(x => x.IndirizzoIp).HasMaxLength(45);   // IPv6
        e.Property(x => x.UserAgent).HasMaxLength(500);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.Token).IsUnique();
        e.HasIndex(x => x.Scadenza);   // pulizia periodica delle sessioni scadute
    }
}

public class ConfigurazioneTentativoAccesso : IEntityTypeConfiguration<TentativoAccesso>
{
    public void Configure(EntityTypeBuilder<TentativoAccesso> e)
    {
        e.ToTable("TentativiAccesso", "portale");
        e.HasKey(x => x.IdTentativo);

        e.Property(x => x.CodiceContabilita).HasMaxLength(50).IsRequired();
        e.Property(x => x.IndirizzoIp).HasMaxLength(45);
        e.Property(x => x.Motivo).HasMaxLength(200);
        e.Property(x => x.DataOra).HasDefaultValueSql("SYSUTCDATETIME()");

        // Conteggio dei tentativi recenti per la protezione a forza bruta
        e.HasIndex(x => new { x.CodiceContabilita, x.DataOra });
        e.HasIndex(x => new { x.IndirizzoIp, x.DataOra });
    }
}

public class ConfigurazioneVoceAudit : IEntityTypeConfiguration<VoceAudit>
{
    public void Configure(EntityTypeBuilder<VoceAudit> e)
    {
        e.ToTable("Audit", "log");
        e.HasKey(x => x.IdVoce);

        e.Property(x => x.EmailUtente).HasMaxLength(256);
        e.Property(x => x.IndirizzoIp).HasMaxLength(45);
        e.Property(x => x.Area).HasConversion<int>();
        e.Property(x => x.Azione).HasMaxLength(100).IsRequired();
        e.Property(x => x.TipoEntita).HasMaxLength(100);
        e.Property(x => x.IdEntita).HasMaxLength(100);
        e.Property(x => x.Esito).HasConversion<int>();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20).IsRequired();
        e.Property(x => x.DataOra).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.DataOra).IsDescending();
        e.HasIndex(x => new { x.IdUtente, x.DataOra });
        e.HasIndex(x => new { x.Area, x.Azione });
    }
}
