using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

/// <summary>Gli archivi del cliente e quello che contengono (10/11/2026).</summary>
public class ConfigurazioneArchivioCompilazione : IEntityTypeConfiguration<ArchivioCompilazione>
{
    public void Configure(EntityTypeBuilder<ArchivioCompilazione> e)
    {
        e.ToTable("ArchiviCompilazione", "man");
        e.HasKey(x => x.IdArchivioCompilazione);

        e.Property(x => x.Tipo).HasConversion<int>().IsRequired();
        e.Property(x => x.Luogo).HasMaxLength(400);
        e.Property(x => x.SistemaOperativo).HasMaxLength(200);
        e.Property(x => x.Antivirus).HasMaxLength(200);
        e.Property(x => x.Backup).HasMaxLength(400);
        e.Property(x => x.FrequenzaBackup).HasMaxLength(200);
        e.Property(x => x.LuogoConservazione).HasMaxLength(400);
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.Compilazione).WithMany()
            .HasForeignKey(x => x.IdCompilazione).OnDelete(DeleteBehavior.Restrict);

        e.HasMany(x => x.Accessi).WithOne(a => a.Archivio!)
            .HasForeignKey(a => a.IdArchivioCompilazione).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Interessati).WithOne(a => a.Archivio!)
            .HasForeignKey(a => a.IdArchivioCompilazione).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.CategorieDati).WithOne(a => a.Archivio!)
            .HasForeignKey(a => a.IdArchivioCompilazione).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Gestionali).WithOne(a => a.Archivio!)
            .HasForeignKey(a => a.IdArchivioCompilazione).OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.IdCompilazione, x.Tipo, x.Ordine });
    }
}

public class ConfigurazioneArchivioAccesso : IEntityTypeConfiguration<ArchivioAccesso>
{
    public void Configure(EntityTypeBuilder<ArchivioAccesso> e)
    {
        e.ToTable("ArchiviAccessi", "man");
        e.HasKey(x => x.IdArchivioAccesso);
        e.Property(x => x.Valore).HasMaxLength(400).IsRequired();
        e.HasIndex(x => new { x.IdArchivioCompilazione, x.Ordine });
    }
}

public class ConfigurazioneArchivioInteressati : IEntityTypeConfiguration<ArchivioInteressati>
{
    public void Configure(EntityTypeBuilder<ArchivioInteressati> e)
    {
        e.ToTable("ArchiviInteressati", "man");
        e.HasKey(x => x.IdArchivioInteressati);
        e.Property(x => x.Valore).HasMaxLength(400).IsRequired();
        e.HasIndex(x => new { x.IdArchivioCompilazione, x.Ordine });
    }
}

public class ConfigurazioneArchivioCategoriaDati : IEntityTypeConfiguration<ArchivioCategoriaDati>
{
    public void Configure(EntityTypeBuilder<ArchivioCategoriaDati> e)
    {
        e.ToTable("ArchiviCategorieDati", "man");
        e.HasKey(x => x.IdArchivioCategoriaDati);
        e.Property(x => x.Valore).HasMaxLength(400).IsRequired();
        e.HasIndex(x => new { x.IdArchivioCompilazione, x.Ordine });
    }
}

/// <summary>I gestionali del cliente, scritti una volta e installati dove serve.</summary>
public class ConfigurazioneGestionaleCompilazione : IEntityTypeConfiguration<GestionaleCompilazione>
{
    public void Configure(EntityTypeBuilder<GestionaleCompilazione> e)
    {
        e.ToTable("GestionaliCompilazione", "man");
        e.HasKey(x => x.IdGestionaleCompilazione);

        e.Property(x => x.Prodotto).HasMaxLength(200).IsRequired();
        e.Property(x => x.MisureSicurezza).HasMaxLength(400);
        e.Property(x => x.Manutenzione).HasMaxLength(400);
        e.Property(x => x.Backup).HasMaxLength(400);
        e.Property(x => x.Frequenza).HasMaxLength(200);
        e.Property(x => x.LuogoConservazione).HasMaxLength(400);
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.Compilazione).WithMany()
            .HasForeignKey(x => x.IdCompilazione).OnDelete(DeleteBehavior.Restrict);

        // Lo stesso prodotto due volte nella stessa compilazione sarebbe
        // la duplicazione che questa tabella serve a togliere.
        e.HasIndex(x => new { x.IdCompilazione, x.Prodotto }).IsUnique();
    }
}

public class ConfigurazioneArchivioGestionale : IEntityTypeConfiguration<ArchivioGestionale>
{
    public void Configure(EntityTypeBuilder<ArchivioGestionale> e)
    {
        e.ToTable("ArchiviGestionali", "man");
        e.HasKey(x => x.IdArchivioGestionale);

        e.HasOne(x => x.Gestionale).WithMany(g => g.Installazioni)
            .HasForeignKey(x => x.IdGestionaleCompilazione).OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.IdArchivioCompilazione, x.IdGestionaleCompilazione }).IsUnique();
        e.HasIndex(x => x.IdGestionaleCompilazione);
    }
}

/// <summary>Le caselle di posta del cliente e chi vi accede.</summary>
public class ConfigurazioneCasellaEmailCompilazione : IEntityTypeConfiguration<CasellaEmailCompilazione>
{
    public void Configure(EntityTypeBuilder<CasellaEmailCompilazione> e)
    {
        e.ToTable("CaselleEmailCompilazione", "man");
        e.HasKey(x => x.IdCasellaEmailCompilazione);

        e.Property(x => x.Indirizzo).HasMaxLength(320).IsRequired();
        e.Property(x => x.CreatoDa).HasMaxLength(200);
        e.Property(x => x.CreatoIlUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.Compilazione).WithMany()
            .HasForeignKey(x => x.IdCompilazione).OnDelete(DeleteBehavior.Restrict);

        e.HasMany(x => x.Funzioni).WithOne(f => f.Casella!)
            .HasForeignKey(f => f.IdCasellaEmailCompilazione).OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.IdCompilazione, x.Ordine });
    }
}

public class ConfigurazioneCasellaEmailFunzione : IEntityTypeConfiguration<CasellaEmailFunzione>
{
    public void Configure(EntityTypeBuilder<CasellaEmailFunzione> e)
    {
        e.ToTable("CaselleEmailFunzioni", "man");
        e.HasKey(x => x.IdCasellaEmailFunzione);

        e.HasOne(x => x.Funzione).WithMany()
            .HasForeignKey(x => x.IdFunzioneAziendale).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.IdCasellaEmailCompilazione, x.IdFunzioneAziendale }).IsUnique();
    }
}
