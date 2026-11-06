using Microsoft.EntityFrameworkCore;
using LaboratorioAI.Dati.MonitorTemperature.Models;

namespace LaboratorioAI.Dati.MonitorTemperature
{
    /// <summary>
    /// Contesto EF Core sui dati Econorma di monitoraggio temperature, che dal 23/09/2026
    /// risiedono nel database "LaboratorioAI" (stesso server dell'app), schema "temperature"
    /// — prima erano in un database "MonitorTemperature" a parte, sempre schema "dbo".
    ///
    /// Sola lettura dal punto di vista applicativo: le uniche scritture ammesse
    /// sono sulla tabella MappaSonde (mappatura sonda Econorma &lt;-&gt; ArcLab),
    /// mai su Misure/Misure1/Misure3/StoricoSerialiSonde, che restano di
    /// competenza del programma di acquisizione Econorma.
    /// </summary>
    public class ContestoMonitorTemperature : DbContext
    {
        public ContestoMonitorTemperature(DbContextOptions<ContestoMonitorTemperature> options)
            : base(options)
        {
        }

        public DbSet<Misura> Misure { get; set; }

        public DbSet<MappaSonda> MappaSonde { get; set; }

        public DbSet<StatoAttualeDashboard> StatoAttualeDashboard { get; set; }

        public DbSet<StoricoTemperatura> StoricoTemperature { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("temperature");

            modelBuilder.Entity<MappaSonda>()
                .ToTable("MappaSonde", "temperature");

            modelBuilder.Entity<MappaSonda>()
                .HasKey(x => x.IdMappa);

            modelBuilder.Entity<MappaSonda>()
                .HasIndex(x => x.IdApparecchioSonda)
                .IsUnique();

            modelBuilder.Entity<StatoAttualeDashboard>()
                .HasNoKey()
                .ToView("vw_StatoAttualeDashboard", "temperature");

            modelBuilder.Entity<StoricoTemperatura>()
                .HasNoKey()
                .ToView("vw_StoricoTemperature", "temperature");
        }
    }
}
