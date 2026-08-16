using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

public class ConfigurazioneUtente : IEntityTypeConfiguration<Utente>
{
    public void Configure(EntityTypeBuilder<Utente> e)
    {
        e.ToTable("Utenti", "sec");
        e.HasKey(x => x.IdUtente);

        e.Property(x => x.Email).HasMaxLength(256).IsRequired();
        e.Property(x => x.NomeCompleto).HasMaxLength(200).IsRequired();
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");

        // Un account Entra corrisponde a un solo utente applicativo
        e.HasIndex(x => x.ObjectIdEntra).IsUnique();
        e.HasIndex(x => x.Email).IsUnique();
    }
}

public class ConfigurazioneRuolo : IEntityTypeConfiguration<Ruolo>
{
    public void Configure(EntityTypeBuilder<Ruolo> e)
    {
        e.ToTable("Ruoli", "sec");
        e.HasKey(x => x.IdRuolo);

        e.Property(x => x.Codice).HasMaxLength(50).IsRequired();
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(500);

        e.HasIndex(x => x.Codice).IsUnique();
    }
}

public class ConfigurazionePermesso : IEntityTypeConfiguration<Permesso>
{
    public void Configure(EntityTypeBuilder<Permesso> e)
    {
        e.ToTable("Permessi", "sec");
        e.HasKey(x => x.IdPermesso);

        e.Property(x => x.Codice).HasMaxLength(100).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(300).IsRequired();
        e.Property(x => x.Area).HasConversion<int>();

        e.HasIndex(x => x.Codice).IsUnique();
    }
}

public class ConfigurazioneUtenteRuolo : IEntityTypeConfiguration<UtenteRuolo>
{
    public void Configure(EntityTypeBuilder<UtenteRuolo> e)
    {
        e.ToTable("UtentiRuoli", "sec");
        e.HasKey(x => new { x.IdUtente, x.IdRuolo });

        e.Property(x => x.DataAssegnazione).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.Utente).WithMany(u => u.Ruoli)
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Ruolo).WithMany(r => r.Utenti)
         .HasForeignKey(x => x.IdRuolo).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneRuoloPermesso : IEntityTypeConfiguration<RuoloPermesso>
{
    public void Configure(EntityTypeBuilder<RuoloPermesso> e)
    {
        e.ToTable("RuoliPermessi", "sec");
        e.HasKey(x => new { x.IdRuolo, x.IdPermesso });

        e.HasOne(x => x.Ruolo).WithMany(r => r.Permessi)
         .HasForeignKey(x => x.IdRuolo).OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Permesso).WithMany(p => p.Ruoli)
         .HasForeignKey(x => x.IdPermesso).OnDelete(DeleteBehavior.Cascade);
    }
}
