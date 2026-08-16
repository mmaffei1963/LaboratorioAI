using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Taratura bilance (schema "bilance"). Valori metrologici in double (float SQL).

public class ConfigurazionePesiera : IEntityTypeConfiguration<Pesiera>
{
    public void Configure(EntityTypeBuilder<Pesiera> e)
    {
        e.ToTable("Pesiere", "bilance");
        e.HasKey(x => x.IdPesiera);
        e.Property(x => x.Identificazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.Identificazione);
    }
}

public class ConfigurazioneMassaCampione : IEntityTypeConfiguration<MassaCampione>
{
    public void Configure(EntityTypeBuilder<MassaCampione> e)
    {
        e.ToTable("MasseCampione", "bilance");
        e.HasKey(x => x.IdMassa);
        e.Property(x => x.CertificatoLat).HasMaxLength(100);
        e.Property(x => x.ClasseOiml).HasMaxLength(10);
        e.HasOne(x => x.Pesiera).WithMany(p => p.Masse)
         .HasForeignKey(x => x.IdPesiera).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.IdPesiera);
    }
}

public class ConfigurazioneTaraturaBilancia : IEntityTypeConfiguration<TaraturaBilancia>
{
    public void Configure(EntityTypeBuilder<TaraturaBilancia> e)
    {
        e.ToTable("TaratureBilance", "bilance");
        e.HasKey(x => x.IdTaratura);
        e.Property(x => x.BilanciaIdentificazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.BilanciaModello).HasMaxLength(255);
        e.Property(x => x.Tipo).HasMaxLength(100);
        e.Property(x => x.Sda).HasMaxLength(50);
        e.Property(x => x.TermometroUtilizzato).HasMaxLength(100);
        e.Property(x => x.NumeroRapporto).HasMaxLength(50);
        e.Property(x => x.RiferimentoIstruzione).HasMaxLength(100);
        e.Property(x => x.Responsabile).HasMaxLength(255);
        e.Property(x => x.DatiProve).IsRequired();
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasOne(x => x.Pesiera).WithMany()
         .HasForeignKey(x => x.IdPesiera).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.DataTaratura).IsDescending();
        e.HasIndex(x => x.DataScadenza);
    }
}

public class ConfigurazioneEsitoValidazioneBilancia : IEntityTypeConfiguration<EsitoValidazioneBilancia>
{
    public void Configure(EntityTypeBuilder<EsitoValidazioneBilancia> e)
    {
        e.ToTable("ValidazioniCalcoloBilance", "bilance");
        e.HasKey(x => x.IdEsito);
        e.Property(x => x.VersioneSet).HasMaxLength(20).IsRequired();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);
        e.Property(x => x.Operatore).HasMaxLength(255);
        e.Property(x => x.Dettaglio).IsRequired();
        e.HasOne(x => x.Utente).WithMany().HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.DataEsecuzione).IsDescending();
    }
}

public class ConfigurazioneAnalisiBilancia : IEntityTypeConfiguration<AnalisiBilancia>
{
    public void Configure(EntityTypeBuilder<AnalisiBilancia> e)
    {
        e.ToTable("AnalisiBilance", "bilance");
        e.HasKey(x => x.IdAnalisi);
        e.Property(x => x.Firma).HasMaxLength(100).IsRequired();
        e.Property(x => x.Esito).HasMaxLength(255);
        e.Property(x => x.Testo).IsRequired();
        e.Property(x => x.ModelloAi).HasMaxLength(100);
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20);
        e.HasOne(x => x.Taratura).WithMany().HasForeignKey(x => x.IdTaratura).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Utente).WithMany().HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.IdTaratura, x.Firma });
    }
}
