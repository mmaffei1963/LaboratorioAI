using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Voci dei documenti di vendita AZW per la ricerca delle fatture per descrizione (10/11/2026), schema "dir".
public class ConfigurazioneVoceDocumentoVenditaAzw : IEntityTypeConfiguration<VoceDocumentoVenditaAzw>
{
    public void Configure(EntityTypeBuilder<VoceDocumentoVenditaAzw> e)
    {
        e.ToTable("VociDocumentiVenditaAzw", "dir");
        e.HasKey(x => x.IdVoceDocumentoVenditaAzw);
        e.HasIndex(x => x.DataDocumento);
        e.HasIndex(x => x.CodiceCliente);
        e.Property(x => x.TipoDocumento).HasMaxLength(5).IsRequired();
        e.Property(x => x.NumeroDocumento).HasMaxLength(20).IsRequired();
        e.Property(x => x.DataDocumento).HasColumnType("date");
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.RagioneSociale).HasMaxLength(200);
        e.Property(x => x.CodiceArticolo).HasMaxLength(50);
        e.Property(x => x.Descrizione).HasMaxLength(4000).IsRequired();
        e.Property(x => x.Quantita).HasColumnType("decimal(18,4)");
        e.Property(x => x.Prezzo).HasColumnType("decimal(18,4)");
        e.Property(x => x.Sconto).HasMaxLength(30);
        e.Property(x => x.Importo).HasColumnType("decimal(18,2)");
        e.Property(x => x.Centro).HasMaxLength(10);
        e.Property(x => x.Voce).HasMaxLength(10);
        e.Property(x => x.Commessa).HasMaxLength(20);
    }
}

// Storico delle zone AZW dei clienti (11/11/2026), schema "provv" come l'anagrafica AzwClienti.
public class ConfigurazioneStoricoZonaCliente : IEntityTypeConfiguration<StoricoZonaCliente>
{
    public void Configure(EntityTypeBuilder<StoricoZonaCliente> e)
    {
        e.ToTable("StoricoZoneClienti", "provv");
        e.HasKey(x => x.IdStoricoZonaCliente);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Zona).HasMaxLength(20);
        e.Property(x => x.DataDal).HasColumnType("date");
        e.Property(x => x.DataAl).HasColumnType("date");
        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.HasIndex(x => new { x.CodiceCliente, x.DataDal });
    }
}
