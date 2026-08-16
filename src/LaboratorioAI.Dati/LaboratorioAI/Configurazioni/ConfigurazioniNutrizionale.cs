using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Catalogo nutrizionale: schema "nutri". Convenzioni: nomi tabella italiani al
// plurale, chiavi Id<Entità> INT IDENTITY, date UTC di default, nessuna
// cancellazione fisica (Attivo/DataCancellazione).

public class ConfigurazioneFonte : IEntityTypeConfiguration<Fonte>
{
    public void Configure(EntityTypeBuilder<Fonte> e)
    {
        e.ToTable("Fonti", "nutri");
        e.HasKey(x => x.IdFonte);

        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.Descrizione).HasMaxLength(500).IsRequired();
        e.Property(x => x.Riferimento).HasMaxLength(500);
        e.Property(x => x.Url).HasMaxLength(500);

        e.HasIndex(x => x.Tipo);
    }
}

public class ConfigurazioneMateriaPrima : IEntityTypeConfiguration<MateriaPrima>
{
    public void Configure(EntityTypeBuilder<MateriaPrima> e)
    {
        e.ToTable("MateriePrime", "nutri");
        e.HasKey(x => x.IdMateriaPrima);

        e.Property(x => x.Denominazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.Marca).HasMaxLength(255);
        e.Property(x => x.Fornitore).HasMaxLength(255);
        e.Property(x => x.Categoria).HasMaxLength(150);
        e.Property(x => x.CodiceInterno).HasMaxLength(100);
        e.Property(x => x.Note).HasMaxLength(1000);

        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.Denominazione);
        e.HasIndex(x => x.Tipo);
        e.HasIndex(x => x.IdClienteArcLab);
    }
}

public class ConfigurazioneValoreNutrizionale : IEntityTypeConfiguration<ValoreNutrizionale>
{
    public void Configure(EntityTypeBuilder<ValoreNutrizionale> e)
    {
        e.ToTable("ValoriNutrizionali", "nutri");
        e.HasKey(x => x.IdValoreNutrizionale);

        foreach (var p in new[] { nameof(ValoreNutrizionale.Grassi), nameof(ValoreNutrizionale.GrassiSaturi),
            nameof(ValoreNutrizionale.Carboidrati), nameof(ValoreNutrizionale.Zuccheri),
            nameof(ValoreNutrizionale.Proteine), nameof(ValoreNutrizionale.Sale),
            nameof(ValoreNutrizionale.Fibre), nameof(ValoreNutrizionale.Acqua) })
            e.Property(p).HasPrecision(9, 4);

        e.Property(x => x.EnergiaKj).HasPrecision(10, 3);
        e.Property(x => x.EnergiaKcal).HasPrecision(10, 3);
        foreach (var p in new[] { nameof(ValoreNutrizionale.Sodio), nameof(ValoreNutrizionale.GrassiTrans),
            nameof(ValoreNutrizionale.GrassiMonoinsaturi), nameof(ValoreNutrizionale.GrassiPolinsaturi),
            nameof(ValoreNutrizionale.Colesterolo), nameof(ValoreNutrizionale.VitaminaD),
            nameof(ValoreNutrizionale.Calcio), nameof(ValoreNutrizionale.Ferro), nameof(ValoreNutrizionale.Potassio) })
            e.Property(p).HasPrecision(10, 4);
        e.Property(x => x.RiferimentoDocumento).HasMaxLength(500);
        e.Property(x => x.Note).HasMaxLength(1000);

        e.Property(x => x.DataAcquisizione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.MateriaPrima).WithMany(m => m.Valori)
         .HasForeignKey(x => x.IdMateriaPrima).OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Fonte).WithMany(f => f.Valori)
         .HasForeignKey(x => x.IdFonte).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => new { x.IdMateriaPrima, x.Corrente });
    }
}

public class ConfigurazioneRicetta : IEntityTypeConfiguration<Ricetta>
{
    public void Configure(EntityTypeBuilder<Ricetta> e)
    {
        e.ToTable("Ricette", "nutri");
        e.HasKey(x => x.IdRicetta);

        e.Property(x => x.Denominazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.Committente).HasMaxLength(255);
        e.Property(x => x.Lotto).HasMaxLength(100);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.RedattoDa).HasMaxLength(255);

        e.Property(x => x.UnitaConsumoG).HasPrecision(9, 3);
        e.Property(x => x.UnitaConsumoPezzi).HasPrecision(9, 3);
        e.Property(x => x.CaloPesoPercentuale).HasPrecision(6, 3);
        e.Property(x => x.PesoCottoKg).HasPrecision(10, 4);

        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => x.Denominazione);
        e.HasIndex(x => x.IdClienteArcLab);
    }
}

public class ConfigurazioneRigaRicetta : IEntityTypeConfiguration<RigaRicetta>
{
    public void Configure(EntityTypeBuilder<RigaRicetta> e)
    {
        e.ToTable("RigheRicetta", "nutri");
        e.HasKey(x => x.IdRigaRicetta);

        e.Property(x => x.QuantitaKg).HasPrecision(10, 4);

        e.HasOne(x => x.Ricetta).WithMany(r => r.Righe)
         .HasForeignKey(x => x.IdRicetta).OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.MateriaPrima).WithMany()
         .HasForeignKey(x => x.IdMateriaPrima).OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => x.IdRicetta);
    }
}

public class ConfigurazioneVerificaNutrizionale : IEntityTypeConfiguration<VerificaNutrizionale>
{
    public void Configure(EntityTypeBuilder<VerificaNutrizionale> e)
    {
        e.ToTable("VerificheNutrizionali", "nutri");
        e.HasKey(x => x.IdVerifica);

        e.Property(x => x.Firma).HasMaxLength(64).IsRequired();
        e.Property(x => x.Denominazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.Esito).HasMaxLength(120);
        e.Property(x => x.Testo).IsRequired();
        e.Property(x => x.ModelloAi).HasMaxLength(50).IsRequired();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20).IsRequired();
        e.Property(x => x.CostoStimato).HasPrecision(10, 4);

        e.Property(x => x.DataVerifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.Ricetta).WithMany()
         .HasForeignKey(x => x.IdRicetta).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => new { x.IdRicetta, x.Firma });
    }
}

public class ConfigurazioneRapportoNutrizionale : IEntityTypeConfiguration<RapportoNutrizionale>
{
    public void Configure(EntityTypeBuilder<RapportoNutrizionale> e)
    {
        e.ToTable("RapportiNutrizionali", "nutri");
        e.HasKey(x => x.IdRapporto);

        e.Property(x => x.RedattoDa).HasMaxLength(150);
        e.Property(x => x.Committente).HasMaxLength(255);
        e.Property(x => x.Denominazione).HasMaxLength(255).IsRequired();
        e.Property(x => x.Lotto).HasMaxLength(100);
        e.Property(x => x.Risultati).IsRequired();
        e.Property(x => x.RicettaUsata).IsRequired();
        e.Property(x => x.VersioneCalcolo).HasMaxLength(20).IsRequired();
        e.Property(x => x.VersioneApplicazione).HasMaxLength(20).IsRequired();
        e.Property(x => x.NomeFileGenerato).HasMaxLength(300);
        e.Property(x => x.PercorsoDocumento).HasMaxLength(500);

        e.Property(x => x.DataRapporto).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne(x => x.Ricetta).WithMany()
         .HasForeignKey(x => x.IdRicetta).OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Utente).WithMany()
         .HasForeignKey(x => x.IdUtente).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => x.DataRapporto).IsDescending();
        e.HasIndex(x => x.IdRicetta);
    }
}
