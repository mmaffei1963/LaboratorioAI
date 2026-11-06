using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Preventivi/contratti di consulenza (schema "cons"). Nessuna scrittura su ArcLab: gli id ArcLab sono solo riferimenti.

public class ConfigurazioneAttivitaConsulenza : IEntityTypeConfiguration<AttivitaConsulenza>
{
    public void Configure(EntityTypeBuilder<AttivitaConsulenza> e)
    {
        e.ToTable("AttivitaConsulenza", "cons");
        e.HasKey(x => x.IdAttivitaConsulenza);
        e.Property(x => x.Descrizione).HasMaxLength(255).IsRequired();
        e.Property(x => x.ModalitaPrezzo).HasConversion<int>();
        e.Property(x => x.UnitaMisura).HasMaxLength(30);
        e.Property(x => x.PrezzoProposto).HasPrecision(12, 2);
        e.Property(x => x.PrezzoSuccessiviProposto).HasPrecision(12, 2);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.IdTipoIncarico).IsUnique();
        e.HasMany(x => x.Contenuti).WithOne().HasForeignKey(c => c.IdAttivitaConsulenza).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneContenutoAttivita : IEntityTypeConfiguration<ContenutoAttivita>
{
    public void Configure(EntityTypeBuilder<ContenutoAttivita> e)
    {
        e.ToTable("ContenutiAttivita", "cons");
        e.HasKey(x => x.IdContenutoAttivita);
        e.Property(x => x.Chiave).HasMaxLength(30);
        e.Property(x => x.Testo).HasMaxLength(500).IsRequired();
    }
}

public class ConfigurazionePacchettoConsulenza : IEntityTypeConfiguration<PacchettoConsulenza>
{
    public void Configure(EntityTypeBuilder<PacchettoConsulenza> e)
    {
        e.ToTable("PacchettiConsulenza", "cons");
        e.HasKey(x => x.IdPacchettoConsulenza);
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(500);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.UltimaModificaDa).HasMaxLength(100);
        e.HasMany(x => x.Righe).WithOne().HasForeignKey(r => r.IdPacchettoConsulenza).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Analisi).WithOne().HasForeignKey(r => r.IdPacchettoConsulenza).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneAnalisiPacchettoConsulenza : IEntityTypeConfiguration<AnalisiPacchettoConsulenza>
{
    public void Configure(EntityTypeBuilder<AnalisiPacchettoConsulenza> e)
    {
        e.ToTable("AnalisiPacchettiConsulenza", "cons");
        e.HasKey(x => x.IdAnalisiPacchetto);
        e.Property(x => x.IdControlliOrigine).HasMaxLength(500);
        e.Property(x => x.Descrizione).HasMaxLength(300).IsRequired();
        e.Property(x => x.Modalita).HasConversion<int>();
        e.Property(x => x.QuantitaAnnua).HasPrecision(10, 2);
        e.Property(x => x.Prezzo).HasPrecision(12, 2);
        e.Property(x => x.Note).HasMaxLength(500);
    }
}

public class ConfigurazioneRigaPacchettoConsulenza : IEntityTypeConfiguration<RigaPacchettoConsulenza>
{
    public void Configure(EntityTypeBuilder<RigaPacchettoConsulenza> e)
    {
        e.ToTable("RighePacchettoConsulenza", "cons");
        e.HasKey(x => x.IdRigaPacchetto);
        e.Property(x => x.DescrizioneLibera).HasMaxLength(500);
        e.Property(x => x.Contenuti).HasMaxLength(1000);
        e.Property(x => x.ModalitaPrezzo).HasConversion<int?>();
        e.Property(x => x.Quantita).HasPrecision(10, 2);
        e.Property(x => x.Prezzo).HasPrecision(12, 2);
        e.Property(x => x.PrezzoSuccessivi).HasPrecision(12, 2);
        e.HasOne<AttivitaConsulenza>().WithMany().HasForeignKey(x => x.IdAttivitaConsulenza).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazionePreventivoConsulenza : IEntityTypeConfiguration<PreventivoConsulenza>
{
    public void Configure(EntityTypeBuilder<PreventivoConsulenza> e)
    {
        e.ToTable("PreventiviConsulenza", "cons");
        e.HasKey(x => x.IdPreventivoConsulenza);
        e.Ignore(x => x.NumeroCompleto);
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.CodiceClienteAzw).HasMaxLength(20);
        e.Property(x => x.RagioneSociale).HasMaxLength(200);
        e.Property(x => x.PartitaIva).HasMaxLength(20);
        e.Property(x => x.CodiceFiscale).HasMaxLength(20);
        e.Property(x => x.Indirizzo).HasMaxLength(255);
        e.Property(x => x.Cap).HasMaxLength(10);
        e.Property(x => x.Citta).HasMaxLength(100);
        e.Property(x => x.Provincia).HasMaxLength(4);
        e.Property(x => x.Email).HasMaxLength(250);
        e.Property(x => x.Pec).HasMaxLength(250);
        e.Property(x => x.CodiceSdi).HasMaxLength(10);
        e.Property(x => x.Telefono).HasMaxLength(50);
        e.Property(x => x.SedeNome).HasMaxLength(255);
        e.Property(x => x.SedeIndirizzo).HasMaxLength(500);
        e.Property(x => x.ReferenteNome).HasMaxLength(100);
        e.Property(x => x.ReferenteMansione).HasMaxLength(100);
        e.Property(x => x.ReferenteTelefono).HasMaxLength(30);
        e.Property(x => x.ReferenteEmail).HasMaxLength(250);
        e.Property(x => x.SettoreAttivita).HasMaxLength(100);
        e.Property(x => x.Oggetto).HasMaxLength(500);
        e.Property(x => x.CondizioniPagamento).HasMaxLength(500);
        e.Property(x => x.RevisioneCondizioni).HasMaxLength(100);
        e.Property(x => x.ConsulenteNome).HasMaxLength(100);
        e.Property(x => x.CommercialeNome).HasMaxLength(100);
        e.Property(x => x.ResponsabileCliente).HasMaxLength(100);
        e.Property(x => x.ZonaCliente).HasMaxLength(20);
        e.Property(x => x.TotalePrimoAnno).HasPrecision(12, 2);
        e.Property(x => x.TotaleAnniSuccessivi).HasPrecision(12, 2);
        e.Property(x => x.Note).HasMaxLength(2000);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.Anno, x.Numero }).IsUnique().HasFilter("[Numero] IS NOT NULL");
        e.HasMany(x => x.Righe).WithOne().HasForeignKey(r => r.IdPreventivoConsulenza).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Blocchi).WithOne().HasForeignKey(b => b.IdPreventivoConsulenza).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Analisi).WithOne().HasForeignKey(r => r.IdPreventivoConsulenza).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>29/10/2026: blocchi di quotazione dei preventivi di consulenza.</summary>
public class ConfigurazioneBloccoPreventivoConsulenza : IEntityTypeConfiguration<BloccoPreventivoConsulenza>
{
    public void Configure(EntityTypeBuilder<BloccoPreventivoConsulenza> e)
    {
        e.ToTable("BlocchiPreventivoConsulenza", "cons");
        e.HasKey(x => x.IdBloccoPreventivoConsulenza);
        e.Property(x => x.Titolo).HasMaxLength(200);
        e.Property(x => x.Descrizione).HasMaxLength(4000);
        e.HasIndex(x => new { x.IdPreventivoConsulenza, x.Numero }).IsUnique();
    }
}

public class ConfigurazioneAnalisiPreventivoConsulenza : IEntityTypeConfiguration<AnalisiPreventivoConsulenza>
{
    public void Configure(EntityTypeBuilder<AnalisiPreventivoConsulenza> e)
    {
        e.ToTable("AnalisiPreventiviConsulenza", "cons");
        e.HasKey(x => x.IdAnalisiPreventivoConsulenza);
        e.Property(x => x.IdControlliOrigine).HasMaxLength(500);
        e.Property(x => x.Descrizione).HasMaxLength(300).IsRequired();
        e.Property(x => x.Modalita).HasConversion<int>();
        e.Property(x => x.OriginePrezzo).HasConversion<int>();
        e.Property(x => x.QuantitaAnnua).HasPrecision(10, 2);
        e.Property(x => x.PrezzoBase).HasPrecision(12, 2);
        e.Property(x => x.Prezzo).HasPrecision(12, 2);
        e.Property(x => x.Note).HasMaxLength(500);
        e.HasMany(x => x.Metodi).WithOne().HasForeignKey(m => m.IdAnalisiPreventivoConsulenza).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConfigurazioneMetodoAnalisiConsulenza : IEntityTypeConfiguration<MetodoAnalisiConsulenza>
{
    public void Configure(EntityTypeBuilder<MetodoAnalisiConsulenza> e)
    {
        e.ToTable("MetodiAnalisiConsulenza", "cons");
        e.HasKey(x => x.IdMetodoAnalisiConsulenza);
        e.Property(x => x.Descrizione).HasMaxLength(300);
        e.Property(x => x.Denominazione).HasMaxLength(300);
        e.Property(x => x.RiferimentoNormativo).HasMaxLength(500);
        e.Property(x => x.UnitaMisura).HasMaxLength(50);
        e.Property(x => x.LabEsterno).HasMaxLength(200);
        e.Property(x => x.LabEsternoFrase).HasMaxLength(1000);
    }
}

public class ConfigurazioneRigaPreventivoConsulenza : IEntityTypeConfiguration<RigaPreventivoConsulenza>
{
    public void Configure(EntityTypeBuilder<RigaPreventivoConsulenza> e)
    {
        e.ToTable("RighePreventivoConsulenza", "cons");
        e.HasKey(x => x.IdRigaPreventivoConsulenza);
        e.Property(x => x.Blocco).HasDefaultValue(1);   // 29/10/2026
        e.Ignore(x => x.Personalizzata);
        e.Property(x => x.DescrizioneAttivita).HasMaxLength(255).IsRequired();
        e.Property(x => x.DescrizioneLibera).HasMaxLength(1000);
        e.Property(x => x.Contenuti).HasMaxLength(4000);
        e.Property(x => x.ModalitaPrezzo).HasConversion<int>();
        e.Property(x => x.UnitaMisura).HasMaxLength(30);
        e.Property(x => x.Quantita).HasPrecision(10, 2);
        e.Property(x => x.Prezzo).HasPrecision(12, 2);
        e.Property(x => x.PrezzoSuccessivi).HasPrecision(12, 2);
        e.HasOne<AttivitaConsulenza>().WithMany().HasForeignKey(x => x.IdAttivitaConsulenza).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneAzwCliente : IEntityTypeConfiguration<AzwCliente>
{
    public void Configure(EntityTypeBuilder<AzwCliente> e)
    {
        e.ToTable("AzwClienti", "provv");
        e.HasKey(x => x.IdAzwCliente);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.RagioneSociale).HasMaxLength(200);
        e.Property(x => x.PartitaIva).HasMaxLength(20);
        e.Property(x => x.CodiceFiscale).HasMaxLength(20);
        e.Property(x => x.Indirizzo).HasMaxLength(255);
        e.Property(x => x.Cap).HasMaxLength(10);
        e.Property(x => x.Citta).HasMaxLength(100);
        e.Property(x => x.Provincia).HasMaxLength(4);
        e.Property(x => x.Email).HasMaxLength(250);
        e.Property(x => x.Pec).HasMaxLength(250);
        e.Property(x => x.CodiceSdi).HasMaxLength(10);
        e.Property(x => x.Telefono).HasMaxLength(50);
        e.Property(x => x.CodiceVenditore).HasMaxLength(20);
        e.Property(x => x.Zona).HasMaxLength(20);
        e.HasIndex(x => x.CodiceCliente);
    }
}

public class ConfigurazioneResponsabileCliente : IEntityTypeConfiguration<ResponsabileCliente>
{
    public void Configure(EntityTypeBuilder<ResponsabileCliente> e)
    {
        e.ToTable("ResponsabiliCliente", "cons");
        e.HasKey(x => x.IdResponsabileCliente);
        e.Property(x => x.Sigla).HasMaxLength(20).IsRequired();
        e.Property(x => x.Nome).HasMaxLength(100);
        e.Property(x => x.Email).HasMaxLength(250);
        e.HasIndex(x => x.Sigla).IsUnique();
    }
}
