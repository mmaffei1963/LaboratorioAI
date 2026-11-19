using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Presenze (19/11/2026): schema «presenze». Definizione: claude/definizione-presenze.md

public class ConfigurazionePresenzeSede : IEntityTypeConfiguration<SedeAziendale>
{
    public void Configure(EntityTypeBuilder<SedeAziendale> e)
    {
        e.ToTable("Sedi", "presenze");
        e.HasKey(x => x.IdSede);
        e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.Nome).IsUnique();
        e.HasData(
            new SedeAziendale { IdSede = 1, Nome = "Cattolica", Attiva = true },
            new SedeAziendale { IdSede = 2, Nome = "Pesaro", Attiva = true });
    }
}

public class ConfigurazionePresenzeLavoratore : IEntityTypeConfiguration<Lavoratore>
{
    public void Configure(EntityTypeBuilder<Lavoratore> e)
    {
        e.ToTable("Lavoratori", "presenze", t =>
        {
            t.HasCheckConstraint("CK_Lavoratori_Ore",
                "[OreLunedi] BETWEEN 0 AND 24 AND [OreMartedi] BETWEEN 0 AND 24 AND [OreMercoledi] BETWEEN 0 AND 24 " +
                "AND [OreGiovedi] BETWEEN 0 AND 24 AND [OreVenerdi] BETWEEN 0 AND 24 " +
                "AND [OreSabato] BETWEEN 0 AND 24 AND [OreDomenica] BETWEEN 0 AND 24");
        });
        e.HasKey(x => x.IdLavoratore);
        e.HasOne(x => x.Utente).WithMany().HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.IdSede).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.IdUtente).IsUnique();
        e.HasIndex(x => x.Cognome).IsUnique();
        e.Property(x => x.Cognome).HasMaxLength(100).IsRequired();
        e.Property(x => x.Colore).HasMaxLength(7).IsRequired();
        e.Property(x => x.AttivoDal).HasColumnType("date");
        foreach (var p in new[] { nameof(Lavoratore.OreLunedi), nameof(Lavoratore.OreMartedi), nameof(Lavoratore.OreMercoledi),
                     nameof(Lavoratore.OreGiovedi), nameof(Lavoratore.OreVenerdi), nameof(Lavoratore.OreSabato),
                     nameof(Lavoratore.OreDomenica) })
            e.Property(p).HasPrecision(4, 2);
    }
}

public class ConfigurazionePresenzeTimbratura : IEntityTypeConfiguration<Timbratura>
{
    public void Configure(EntityTypeBuilder<Timbratura> e)
    {
        e.ToTable("Timbrature", "presenze", t =>
        {
            t.HasCheckConstraint("CK_Timbrature_Tipo", "[Tipo] IN ('E', 'U')");
            t.HasCheckConstraint("CK_Timbrature_Origine", "[Origine] IN ('App', 'Manuale')");
        });
        e.HasKey(x => x.IdTimbratura);
        e.HasOne(x => x.Lavoratore).WithMany().HasForeignKey(x => x.IdLavoratore).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.Giorno).HasColumnType("date");
        e.Property(x => x.Tipo).HasMaxLength(1).IsRequired().IsFixedLength();
        e.Property(x => x.Luogo).HasMaxLength(200);
        e.Property(x => x.Origine).HasMaxLength(10).IsRequired();
        e.Property(x => x.MotivoAnnullamento).HasMaxLength(500);
        e.HasIndex(x => new { x.IdLavoratore, x.Giorno });
        e.HasIndex(x => x.Giorno);
    }
}

public class ConfigurazionePresenzeAssenza : IEntityTypeConfiguration<Assenza>
{
    public void Configure(EntityTypeBuilder<Assenza> e)
    {
        e.ToTable("Assenze", "presenze", t =>
        {
            t.HasCheckConstraint("CK_Assenze_Causale", "[Causale] IN ('FER', 'PER', '104', 'SEG', 'MAL', 'MAT', 'DS', 'ALT')");
            t.HasCheckConstraint("CK_Assenze_Ore", "[Ore] >= 0 AND [Ore] <= 24");
        });
        e.HasKey(x => x.IdAssenza);
        e.HasOne(x => x.Lavoratore).WithMany().HasForeignKey(x => x.IdLavoratore).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.Giorno).HasColumnType("date");
        e.Property(x => x.Causale).HasMaxLength(3).IsRequired();
        e.Property(x => x.Ore).HasPrecision(4, 2);
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.MotivoAnnullamento).HasMaxLength(500);
        e.HasIndex(x => new { x.IdLavoratore, x.Giorno });
        e.HasIndex(x => x.Giorno);
    }
}

public class ConfigurazionePresenzeGiornoCalendario : IEntityTypeConfiguration<GiornoCalendarioAziendale>
{
    public void Configure(EntityTypeBuilder<GiornoCalendarioAziendale> e)
    {
        e.ToTable("CalendarioAziendale", "presenze", t =>
        {
            t.HasCheckConstraint("CK_CalendarioAziendale_Tipo", "[Tipo] IN ('FE', 'CH')");
            t.HasCheckConstraint("CK_CalendarioAziendale_Ore", "[Ore] IS NULL OR ([Ore] > 0 AND [Ore] <= 24)");
        });
        e.HasKey(x => x.IdGiornoCalendario);
        e.HasOne(x => x.Sede).WithMany().HasForeignKey(x => x.IdSede).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.Giorno).HasColumnType("date");
        e.Property(x => x.Tipo).HasMaxLength(2).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(200);
        e.Property(x => x.Ore).HasPrecision(4, 2);
        // Un solo giorno speciale per sede e data; con IdSede vuoto vale per tutte le sedi (anche quello è unico per data).
        // HasFilter(null) toglie il filtro «IS NOT NULL» che EF aggiungerebbe da solo e lascerebbe duplicare le righe «tutte le sedi».
        e.HasIndex(x => new { x.IdSede, x.Giorno }).IsUnique().HasFilter(null);
    }
}

public class ConfigurazionePresenzeImpostazione : IEntityTypeConfiguration<ImpostazionePresenze>
{
    public void Configure(EntityTypeBuilder<ImpostazionePresenze> e)
    {
        e.ToTable("ImpostazioniPresenze", "presenze");
        e.HasKey(x => x.IdImpostazione);
        e.Property(x => x.IdImpostazione).ValueGeneratedNever();
        e.Property(x => x.InizioConteggi).HasColumnType("date");
        e.HasData(new ImpostazionePresenze { IdImpostazione = 1, InizioConteggi = new DateOnly(2027, 1, 1) });
    }
}

public class ConfigurazionePresenzeModifica : IEntityTypeConfiguration<ModificaPresenze>
{
    public void Configure(EntityTypeBuilder<ModificaPresenze> e)
    {
        e.ToTable("RegistroModifiche", "presenze");
        e.HasKey(x => x.IdModifica);
        e.Property(x => x.Azione).HasMaxLength(50).IsRequired();
        e.Property(x => x.Giorno).HasColumnType("date");
        e.Property(x => x.Motivo).HasMaxLength(500);
        e.HasOne<Lavoratore>().WithMany().HasForeignKey(x => x.IdLavoratore).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Utente>().WithMany().HasForeignKey(x => x.IdUtenteAutore).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.IdLavoratore, x.Giorno });
        e.HasIndex(x => x.QuandoUtc);
    }
}
