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

public class ConfigurazioneSiglaLaboratorioEsterno : IEntityTypeConfiguration<SiglaLaboratorioEsterno>
{
    public void Configure(EntityTypeBuilder<SiglaLaboratorioEsterno> e)
    {
        e.ToTable("SigleLaboratoriEsterni", "certificati");
        e.HasKey(x => x.IdSiglaLaboratorio);
        e.Property(x => x.Sigla).HasMaxLength(20).IsRequired();
        e.Property(x => x.ParoleChiave).HasMaxLength(500);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.IdLabEsternoArcLab).IsUnique();
    }
}

/// <summary>Registro di controllo dei documenti per Palomar (Ordini di Accettazione e certificati esterni).</summary>
public class ConfigurazioneRegistroDocumento : IEntityTypeConfiguration<RegistroDocumento>
{
    public void Configure(EntityTypeBuilder<RegistroDocumento> e)
    {
        e.ToTable("RegistroDocumenti", "log");
        e.HasKey(x => x.IdRegistroDocumento);
        e.Property(x => x.Ambito).HasMaxLength(40).IsRequired();
        e.Property(x => x.IdEsecuzione).HasMaxLength(40).IsRequired();
        e.Property(x => x.Origine).HasMaxLength(30);
        e.Property(x => x.Riferimento).HasMaxLength(300);
        e.Property(x => x.Esito).HasConversion<int>();
        e.Property(x => x.Messaggio).HasMaxLength(2000);
        e.Property(x => x.FileOrigine).HasMaxLength(500);
        e.Property(x => x.FileArchivio).HasMaxLength(500);
        e.Property(x => x.FilePalomar).HasMaxLength(500);
        e.Property(x => x.DataOra).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.Ambito, x.DataOra });
        e.HasIndex(x => x.IdEsecuzione);
        e.Property(x => x.Tipo).HasConversion<int?>();
        e.HasIndex(x => x.IdRegistroCollegato);
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
