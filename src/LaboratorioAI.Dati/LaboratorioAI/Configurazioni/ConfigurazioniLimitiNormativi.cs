using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Limiti normativi (17/10/2026): schema «limiti». Nessuna cancellazione fisica (Restrict sulle chiavi esterne).

public class ConfigurazioneFonteNormativa : IEntityTypeConfiguration<FonteNormativa>
{
    public void Configure(EntityTypeBuilder<FonteNormativa> e)
    {
        e.ToTable("FontiNormative", "limiti");
        e.HasKey(x => x.IdFonteNormativa);
        e.Property(x => x.Titolo).HasMaxLength(300).IsRequired();
        e.Property(x => x.Ente).HasMaxLength(150);
        e.Property(x => x.Url).HasMaxLength(1000);
        e.Property(x => x.Note).HasMaxLength(2000);
        e.Property(x => x.EsitoUltimaVerifica).HasMaxLength(1000);
        e.Property(x => x.CreataDa).HasMaxLength(150);
        e.Property(x => x.UltimaModificaDa).HasMaxLength(150);
        e.HasIndex(x => x.Famiglia);
    }
}

public class ConfigurazioneVersioneFonte : IEntityTypeConfiguration<VersioneFonte>
{
    public void Configure(EntityTypeBuilder<VersioneFonte> e)
    {
        e.ToTable("VersioniFonti", "limiti");
        e.HasKey(x => x.IdVersioneFonte);
        e.Property(x => x.Origine).HasMaxLength(20).IsRequired();
        e.Property(x => x.NomeFile).HasMaxLength(260);
        e.Property(x => x.TipoContenuto).HasMaxLength(100);
        e.Property(x => x.TestoEstratto).IsRequired();
        e.Property(x => x.Impronta).HasMaxLength(64).IsRequired();
        e.Property(x => x.CaricataDa).HasMaxLength(150);
        e.Property(x => x.EsitoEstrazione).HasMaxLength(2000);
        e.HasOne(x => x.Fonte).WithMany().HasForeignKey(x => x.IdFonteNormativa).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdFonteNormativa, x.DataConsultazione });
    }
}

public class ConfigurazioneLimiteNormativo : IEntityTypeConfiguration<LimiteNormativo>
{
    public void Configure(EntityTypeBuilder<LimiteNormativo> e)
    {
        e.ToTable("LimitiNormativi", "limiti");
        e.HasKey(x => x.IdLimiteNormativo);
        e.Property(x => x.CodiceScheda).HasMaxLength(20).IsRequired();
        e.Property(x => x.Parametro).HasMaxLength(300).IsRequired();
        e.Property(x => x.MatriceCategoria).HasMaxLength(500);
        e.Property(x => x.Valore).HasPrecision(18, 6);
        e.Property(x => x.ValoreMax).HasPrecision(18, 6);
        e.Property(x => x.M).HasPrecision(18, 6);
        e.Property(x => x.MMaiuscola).HasPrecision(18, 6);
        e.Property(x => x.Unita).HasMaxLength(50);
        e.Property(x => x.Condizioni).HasMaxLength(1000);
        e.Property(x => x.Riferimento).HasMaxLength(500);
        e.Property(x => x.Citazione).HasMaxLength(4000);
        e.Property(x => x.TestoSegnalazione).HasMaxLength(500);
        e.Property(x => x.Note).HasMaxLength(2000);
        e.Property(x => x.PropostaDa).HasMaxLength(150).IsRequired();
        e.Property(x => x.ModificataDa).HasMaxLength(150);
        e.Property(x => x.ValidataDa).HasMaxLength(150);
        e.Property(x => x.NoteValidazione).HasMaxLength(1000);
        e.HasOne(x => x.VersioneFonte).WithMany().HasForeignKey(x => x.IdVersioneFonte).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<LimiteNormativo>().WithMany().HasForeignKey(x => x.IdLimitePrecedente).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Abbinamenti).WithOne().HasForeignKey(a => a.IdLimiteNormativo).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.CodiceScheda, x.Versione }).IsUnique();
        e.HasIndex(x => new { x.Famiglia, x.Stato });
    }
}

public class ConfigurazioneAbbinamentoLimite : IEntityTypeConfiguration<AbbinamentoLimite>
{
    public void Configure(EntityTypeBuilder<AbbinamentoLimite> e)
    {
        e.ToTable("AbbinamentiLimiti", "limiti");
        e.HasKey(x => x.IdAbbinamentoLimite);
        e.Property(x => x.NomeParametro).HasMaxLength(300);
        e.Property(x => x.NomeMatrice).HasMaxLength(300);
        e.Property(x => x.NomeControllo).HasMaxLength(300);
        e.Property(x => x.FattoreUnita).HasPrecision(18, 9);
        e.HasIndex(x => x.IdParametroArcLab);
    }
}

// Verifica delle schede validate (18/10/2026)
public class ConfigurazioneGiroVerificaLimiti : IEntityTypeConfiguration<GiroVerificaLimiti>
{
    public void Configure(EntityTypeBuilder<GiroVerificaLimiti> e)
    {
        e.ToTable("GiriVerifica", "limiti");
        e.HasKey(x => x.IdGiroVerificaLimiti);
        e.Property(x => x.RichiestoDa).HasMaxLength(150).IsRequired();
        e.Property(x => x.Modello).HasMaxLength(100);
        e.Property(x => x.Esito).HasMaxLength(2000);
        e.HasOne<FonteNormativa>().WithMany().HasForeignKey(x => x.IdFonteNormativa).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.Stato);
    }
}

public class ConfigurazioneRilievoVerificaLimite : IEntityTypeConfiguration<RilievoVerificaLimite>
{
    public void Configure(EntityTypeBuilder<RilievoVerificaLimite> e)
    {
        e.ToTable("RilieviVerifica", "limiti");
        e.HasKey(x => x.IdRilievoVerificaLimite);
        e.Property(x => x.Regola).HasMaxLength(50).IsRequired();
        e.Property(x => x.Motivo).HasMaxLength(2000).IsRequired();
        e.Property(x => x.Citazione).HasMaxLength(2000);
        e.Property(x => x.ChiusoDa).HasMaxLength(150);
        e.Property(x => x.NotaChiusura).HasMaxLength(1000);
        e.HasOne<GiroVerificaLimiti>().WithMany().HasForeignKey(x => x.IdGiroVerificaLimiti).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<LimiteNormativo>().WithMany().HasForeignKey(x => x.IdLimiteNormativo).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdLimiteNormativo, x.Chiuso });
    }
}

// Fase 1b (18/10/2026)
public class ConfigurazioneCategoriaNormativa : IEntityTypeConfiguration<CategoriaNormativa>
{
    public void Configure(EntityTypeBuilder<CategoriaNormativa> e)
    {
        e.ToTable("CategorieNormative", "limiti");
        e.HasKey(x => x.IdCategoriaNormativa);
        e.Property(x => x.Nome).HasMaxLength(500).IsRequired();
        e.Property(x => x.Chiave).HasMaxLength(500).IsRequired();
        e.HasIndex(x => new { x.Famiglia, x.Chiave }).IsUnique();
    }
}

public class ConfigurazioneAbbinamentoMatriceCategoria : IEntityTypeConfiguration<AbbinamentoMatriceCategoria>
{
    public void Configure(EntityTypeBuilder<AbbinamentoMatriceCategoria> e)
    {
        e.ToTable("MatriciCategorie", "limiti");
        e.HasKey(x => x.IdAbbinamentoMatriceCategoria);
        e.Property(x => x.NomeMatrice).HasMaxLength(300).IsRequired();
        e.Property(x => x.PropostoDa).HasMaxLength(150).IsRequired();
        e.Property(x => x.Nota).HasMaxLength(1000);
        e.Property(x => x.ConfermatoDa).HasMaxLength(150);
        e.HasOne<CategoriaNormativa>().WithMany().HasForeignKey(x => x.IdCategoriaNormativa).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdMatriceArcLab, x.Stato });
    }
}

public class ConfigurazioneTempoMassimoProva : IEntityTypeConfiguration<TempoMassimoProva>
{
    public void Configure(EntityTypeBuilder<TempoMassimoProva> e)
    {
        e.ToTable("TempiMassimiProva", "limiti");
        e.HasKey(x => x.IdTempoMassimoProva);
        e.Property(x => x.NomeParametro).HasMaxLength(300);
        e.Property(x => x.Nota).HasMaxLength(500);
        e.Property(x => x.ModificatoDa).HasMaxLength(150).IsRequired();
        e.HasIndex(x => new { x.Famiglia, x.IdParametroArcLab });
    }
}
