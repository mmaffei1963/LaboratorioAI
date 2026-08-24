using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Accettazione campioni fuori sede (schema "accettazione"). Staging: nessuna scrittura su ArcLab.

public class ConfigurazioneAccettazioneLotto : IEntityTypeConfiguration<AccettazioneLotto>
{
    public void Configure(EntityTypeBuilder<AccettazioneLotto> e)
    {
        e.ToTable("Lotti", "accettazione");
        e.HasKey(x => x.IdAccettazioneLotto);
        e.Property(x => x.CodiceLocale).HasMaxLength(40).IsRequired();
        e.Property(x => x.CodLotto).HasMaxLength(30);
        e.HasIndex(x => x.CodLotto);
        e.Property(x => x.Stato).HasConversion<int>();
        e.Property(x => x.Cliente).HasMaxLength(255);
        e.Property(x => x.Insediamento).HasMaxLength(255);
        e.Property(x => x.Commessa).HasMaxLength(255);
        e.Property(x => x.Prelevatore).HasMaxLength(255);
        e.Property(x => x.Vettore).HasMaxLength(255);
        e.Property(x => x.VerbalePrelievo).HasMaxLength(100);
        e.Property(x => x.Ricevitore).HasMaxLength(255);
        e.Property(x => x.Note).HasMaxLength(2000);
        e.Property(x => x.Origine).HasMaxLength(50);
        e.Property(x => x.NomeFileTracciato).HasMaxLength(255);
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => x.CodiceLocale).IsUnique();
        e.HasIndex(x => new { x.Stato, x.Attivo });
    }
}

public class ConfigurazioneAccettazioneCampione : IEntityTypeConfiguration<AccettazioneCampione>
{
    public void Configure(EntityTypeBuilder<AccettazioneCampione> e)
    {
        e.ToTable("Campioni", "accettazione");
        e.HasKey(x => x.IdAccettazioneCampione);
        e.Property(x => x.Matrice).HasMaxLength(255);
        e.Property(x => x.Descrizione).HasMaxLength(500);
        e.Property(x => x.EtichettaCliente).HasMaxLength(255);
        e.Property(x => x.SiglaCliente).HasMaxLength(100);
        e.Property(x => x.Identificativo).HasMaxLength(100);
        e.Property(x => x.Imballaggio).HasMaxLength(255);
        e.Property(x => x.ProceduraPrelievo).HasMaxLength(100);
        e.Property(x => x.CameraLegionella).HasMaxLength(50);
        e.Property(x => x.OraPrelievo).HasMaxLength(10);
        e.Property(x => x.PuntoPrelievo).HasMaxLength(255);
        e.Property(x => x.LuogoPrelievo).HasMaxLength(255);
        e.Property(x => x.TempRicezione).HasMaxLength(20);
        e.Property(x => x.Termometro).HasMaxLength(100);
        e.Property(x => x.CondizioniTrasporto).HasMaxLength(50);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.LabEsterno).HasMaxLength(255);
        e.Property(x => x.NoteLabEsterno).HasMaxLength(1000);
        e.HasOne(x => x.Lotto).WithMany(l => l.Campioni)
         .HasForeignKey(x => x.IdAccettazioneLotto).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.IdAccettazioneLotto);
    }
}

public class ConfigurazioneContatoreAccettazione : IEntityTypeConfiguration<ContatoreAccettazione>
{
    public void Configure(EntityTypeBuilder<ContatoreAccettazione> e)
    {
        e.ToTable("Contatori", "accettazione");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        // riga unica seed: il primo CodLotto assegnato sarà il progressivo 1
        e.HasData(new ContatoreAccettazione { Id = 1, ProssimoValore = 0 });
    }
}

public class ConfigurazioneAccettazioneProva : IEntityTypeConfiguration<AccettazioneProva>
{
    public void Configure(EntityTypeBuilder<AccettazioneProva> e)
    {
        e.ToTable("Prove", "accettazione");
        e.HasKey(x => x.IdAccettazioneProva);
        e.Property(x => x.Controllo).HasMaxLength(255);
        e.Property(x => x.Metodo).HasMaxLength(500);
        e.Property(x => x.Parametro).HasMaxLength(255);
        e.Property(x => x.LaboratorioEsterno).HasMaxLength(255);
        e.Property(x => x.Note).HasMaxLength(500);
        e.HasOne(x => x.Campione).WithMany(c => c.Prove)
         .HasForeignKey(x => x.IdAccettazioneCampione).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.IdAccettazioneCampione);
    }
}
