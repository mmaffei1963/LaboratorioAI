using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Verifica del primo accesso da un browser nuovo (18/11/2026). Le entità si usano con ctx.Set<T>().

public class ConfigurazioneDispositivoFidatoPortale : IEntityTypeConfiguration<DispositivoFidatoPortale>
{
    public void Configure(EntityTypeBuilder<DispositivoFidatoPortale> e)
    {
        e.ToTable("DispositiviFidati", "portale");
        e.HasKey(x => x.IdDispositivoFidatoPortale);
        e.Property(x => x.ImprontaToken).HasMaxLength(128).IsRequired();
        e.Property(x => x.IndirizzoIp).HasMaxLength(45);
        e.Property(x => x.UserAgent).HasMaxLength(500);
        e.Property(x => x.DataCreazioneUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.ImprontaToken).IsUnique();
        e.HasIndex(x => new { x.IdClienteArcLab, x.Revocato });
    }
}

public class ConfigurazioneVerificaAccessoPortale : IEntityTypeConfiguration<VerificaAccessoPortale>
{
    public void Configure(EntityTypeBuilder<VerificaAccessoPortale> e)
    {
        e.ToTable("VerificheAccesso", "portale");
        e.HasKey(x => x.IdVerificaAccessoPortale);
        e.Property(x => x.CodiceContabilita).HasMaxLength(50).IsRequired();
        e.Property(x => x.RagioneSociale).HasMaxLength(255).IsRequired();
        e.Property(x => x.ImprontaToken).HasMaxLength(128).IsRequired();
        e.Property(x => x.Sale).HasMaxLength(64).IsRequired();
        e.Property(x => x.ImprontaCodice).HasMaxLength(128).IsRequired();
        e.Property(x => x.IndirizzoIp).HasMaxLength(45);
        e.Property(x => x.DataCreazioneUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.ImprontaToken).IsUnique();
        e.HasIndex(x => new { x.IdClienteArcLab, x.DataCreazioneUtc });
    }
}
