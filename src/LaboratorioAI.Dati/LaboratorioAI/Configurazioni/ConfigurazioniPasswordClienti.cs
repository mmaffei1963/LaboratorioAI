using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Password del portale clienti (17/11/2026). Le entità si usano con ctx.Set<T>().

public class ConfigurazionePasswordCliente : IEntityTypeConfiguration<PasswordCliente>
{
    public void Configure(EntityTypeBuilder<PasswordCliente> e)
    {
        e.ToTable("PasswordClienti", "portale");
        e.HasKey(x => x.IdPasswordCliente);
        e.Property(x => x.ImprontaPassword).HasMaxLength(200).IsRequired();
        e.Property(x => x.Origine).HasMaxLength(20).IsRequired();
        e.Property(x => x.MotivoAnnullamento).HasMaxLength(50);
        e.Property(x => x.DataImpostazioneUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.IdClienteArcLab, x.Annullata });
    }
}

public class ConfigurazioneCodiceRecuperoPassword : IEntityTypeConfiguration<CodiceRecuperoPassword>
{
    public void Configure(EntityTypeBuilder<CodiceRecuperoPassword> e)
    {
        e.ToTable("CodiciRecuperoPassword", "portale");
        e.HasKey(x => x.IdCodiceRecuperoPassword);
        e.Property(x => x.Sale).HasMaxLength(64).IsRequired();
        e.Property(x => x.ImprontaCodice).HasMaxLength(128).IsRequired();
        e.Property(x => x.IndirizzoIp).HasMaxLength(45);
        e.Property(x => x.DataCreazioneUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.IdClienteArcLab, x.DataCreazioneUtc });
        e.HasIndex(x => new { x.IndirizzoIp, x.DataCreazioneUtc });
    }
}

public class ConfigurazioneEventoPasswordCliente : IEntityTypeConfiguration<EventoPasswordCliente>
{
    public void Configure(EntityTypeBuilder<EventoPasswordCliente> e)
    {
        e.ToTable("EventiPasswordClienti", "portale");
        e.HasKey(x => x.IdEventoPasswordCliente);
        e.Property(x => x.CodiceContabilita).HasMaxLength(50);
        e.Property(x => x.Evento).HasMaxLength(50).IsRequired();
        e.Property(x => x.IndirizzoIp).HasMaxLength(45);
        e.Property(x => x.UserAgent).HasMaxLength(500);
        e.Property(x => x.Dettaglio).HasMaxLength(500);
        e.Property(x => x.DataUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.IdClienteArcLab, x.DataUtc });
    }
}
