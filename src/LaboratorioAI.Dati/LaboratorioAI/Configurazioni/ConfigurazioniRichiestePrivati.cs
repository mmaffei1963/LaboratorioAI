using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Richieste di analisi dei clienti privati (05/11/2026), schema "preventivi" accanto alle offerte.
// I dati ArcLab (matrice, contenitore, controlli, prezzi, metodi) sono copie lette in sola lettura.

public class ConfigurazioneRichiestaAnalisiPrivato : IEntityTypeConfiguration<RichiestaAnalisiPrivato>
{
    public void Configure(EntityTypeBuilder<RichiestaAnalisiPrivato> e)
    {
        e.ToTable("RichiesteAnalisiPrivati", "preventivi");
        e.HasKey(x => x.IdRichiestaAnalisiPrivato);
        e.HasIndex(x => new { x.Anno, x.Numero }).IsUnique();

        e.Property(x => x.NomeCognome).HasMaxLength(200).IsRequired();
        e.Property(x => x.CodiceFiscale).HasMaxLength(20);
        e.Property(x => x.Indirizzo).HasMaxLength(300);
        e.Property(x => x.Telefono).HasMaxLength(50);
        e.Property(x => x.Email).HasMaxLength(255);
        e.Property(x => x.Pagamento).HasConversion<int>();

        e.Property(x => x.DescrizioneCampione).HasMaxLength(300);
        e.Property(x => x.PuntoPrelievo).HasMaxLength(300);
        e.Property(x => x.Contenitore).HasMaxLength(300);
        e.Property(x => x.TemperaturaRicevimento).HasColumnType("decimal(5,1)");

        e.Property(x => x.NormaGiudizio).HasMaxLength(500);
        e.Property(x => x.SupplementoGiudizio).HasColumnType("decimal(10,2)");
        e.Property(x => x.AliquotaIva).HasColumnType("decimal(5,2)");
        e.Property(x => x.TotaleImponibile).HasColumnType("decimal(10,2)");
        e.Property(x => x.TotaleIvaInclusa).HasColumnType("decimal(10,2)");
        e.Property(x => x.Note).HasMaxLength(2000);

        e.HasMany(x => x.Righe).WithOne(r => r.Richiesta!)
            .HasForeignKey(r => r.IdRichiestaAnalisiPrivato).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ConfigurazioneRigaRichiestaAnalisiPrivato : IEntityTypeConfiguration<RigaRichiestaAnalisiPrivato>
{
    public void Configure(EntityTypeBuilder<RigaRichiestaAnalisiPrivato> e)
    {
        e.ToTable("RigheRichiestaAnalisiPrivati", "preventivi");
        e.HasKey(x => x.IdRigaRichiestaAnalisiPrivato);
        e.Property(x => x.Descrizione).HasMaxLength(500).IsRequired();
        e.Property(x => x.PrezzoImponibile).HasColumnType("decimal(10,2)");
        e.Property(x => x.PrezzoIvaInclusa).HasColumnType("decimal(10,2)");
    }
}
