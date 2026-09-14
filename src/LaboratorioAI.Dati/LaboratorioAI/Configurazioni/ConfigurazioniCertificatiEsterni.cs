using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Certificati esterni (schema "certificati"). Staging/cache: nessuna scrittura su ArcLab.

/// <summary>Cache delle estrazioni AI dei certificati esterni (per impronta SHA-256 del PDF).</summary>
public class ConfigurazioneEstrazioneCertificatoArchiviata : IEntityTypeConfiguration<EstrazioneCertificatoArchiviata>
{
    public void Configure(EntityTypeBuilder<EstrazioneCertificatoArchiviata> e)
    {
        e.ToTable("EstrazioniEsterni", "certificati");
        e.HasKey(x => x.IdEstrazione);

        e.Property(x => x.Hash).HasMaxLength(64).IsRequired();
        e.Property(x => x.EstrazioneJson).IsRequired();
        e.Property(x => x.ModelloAi).HasMaxLength(100);
        e.Property(x => x.NomeFileOriginale).HasMaxLength(300);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.Hash);
    }
}

public class ConfigurazioneCertificatoEsternoDaImportare : IEntityTypeConfiguration<CertificatoEsternoDaImportare>
{
    public void Configure(EntityTypeBuilder<CertificatoEsternoDaImportare> e)
    {
        e.ToTable("EsterniDaImportare", "certificati");
        e.HasKey(x => x.IdCertificatoEsterno);
        e.Property(x => x.CodiceLocale).HasMaxLength(40).IsRequired();
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.NumeroCertificato).HasMaxLength(50).IsRequired();
        e.Property(x => x.NomeFile).HasMaxLength(255);
        e.Property(x => x.Laboratorio).HasMaxLength(100);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.CodiceLocale).IsUnique();
        e.HasIndex(x => new { x.Stato, x.Attivo });
        e.HasIndex(x => x.IdCampioneArcLab);
    }
}
