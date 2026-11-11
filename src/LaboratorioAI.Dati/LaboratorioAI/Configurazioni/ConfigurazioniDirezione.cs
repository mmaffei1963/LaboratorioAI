using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Cruscotto Direzione (13/10/2026): schema «dir».

public class ConfigurazioneMovimentoAnaliticoMensile : IEntityTypeConfiguration<MovimentoAnaliticoMensile>
{
    public void Configure(EntityTypeBuilder<MovimentoAnaliticoMensile> e)
    {
        e.ToTable("MovimentiAnalitici", "dir");
        e.HasKey(x => x.IdMovimentoAnalitico);
        e.Property(x => x.Centro).HasMaxLength(10).IsRequired();
        e.Property(x => x.Voce).HasMaxLength(10).IsRequired();
        e.Property(x => x.Commessa).HasMaxLength(20).IsRequired();
        e.Property(x => x.Conto).HasMaxLength(20).IsRequired();
        e.Property(x => x.Importo).HasPrecision(18, 2);
        e.HasIndex(x => new { x.Anno, x.Centro });
        e.HasIndex(x => x.Voce);
        e.HasIndex(x => x.Commessa);
    }
}

public class ConfigurazioneRegistrazioneAnalitica : IEntityTypeConfiguration<RegistrazioneAnalitica>
{
    public void Configure(EntityTypeBuilder<RegistrazioneAnalitica> e)
    {
        e.ToTable("RegistrazioniAnalitiche", "dir");
        e.HasKey(x => x.IdRegistrazioneAnalitica);
        e.Property(x => x.DataRegistrazione).HasColumnType("date");
        e.Property(x => x.TipoDocumento).HasMaxLength(5);
        e.Property(x => x.NumeroDocumento).HasMaxLength(20);
        e.Property(x => x.Centro).HasMaxLength(10).IsRequired();
        e.Property(x => x.Voce).HasMaxLength(10).IsRequired();
        e.Property(x => x.Commessa).HasMaxLength(20).IsRequired();
        e.Property(x => x.Conto).HasMaxLength(20).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(200);
        e.Property(x => x.Importo).HasPrecision(18, 2);
        e.Property(x => x.ClienteFornitore).HasMaxLength(1);
        e.Property(x => x.CodiceControparte).HasMaxLength(20);
        e.Property(x => x.RagioneSocialeControparte).HasMaxLength(200);
        e.HasIndex(x => x.DataRegistrazione);
        e.HasIndex(x => new { x.Voce, x.Centro });
    }
}

public class ConfigurazioneClassificazioneVoce : IEntityTypeConfiguration<ClassificazioneVoce>
{
    public void Configure(EntityTypeBuilder<ClassificazioneVoce> e)
    {
        e.ToTable("ClassificazioniVoci", "dir");
        e.HasKey(x => x.IdClassificazioneVoce);
        e.Property(x => x.CodiceVoce).HasMaxLength(10).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(100);
        e.Property(x => x.TipoCosto).HasMaxLength(20);
        e.Property(x => x.Macrocategoria).HasMaxLength(30).IsRequired();
        e.HasIndex(x => x.CodiceVoce).IsUnique();
    }
}

public class ConfigurazioneCentroCostoAzw : IEntityTypeConfiguration<CentroCostoAzw>
{
    public void Configure(EntityTypeBuilder<CentroCostoAzw> e)
    {
        e.ToTable("CentriCosto", "dir");
        e.HasKey(x => x.IdCentroCosto);
        e.Property(x => x.Codice).HasMaxLength(10).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(100);
    }
}

public class ConfigurazioneCommessaAzw : IEntityTypeConfiguration<CommessaAzw>
{
    public void Configure(EntityTypeBuilder<CommessaAzw> e)
    {
        e.ToTable("CommesseAzw", "dir");
        e.HasKey(x => x.IdCommessaAzw);
        e.Property(x => x.Codice).HasMaxLength(20).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(100);
    }
}

public class ConfigurazioneFatturatoClienteMensile : IEntityTypeConfiguration<FatturatoClienteMensile>
{
    public void Configure(EntityTypeBuilder<FatturatoClienteMensile> e)
    {
        e.ToTable("FatturatoClienti", "dir");
        e.HasKey(x => x.IdFatturatoCliente);
        e.Property(x => x.CodiceCliente).HasMaxLength(20).IsRequired();
        e.Property(x => x.Centro).HasMaxLength(10).IsRequired();
        e.Property(x => x.Voce).HasMaxLength(10).IsRequired();
        e.Property(x => x.Commessa).HasMaxLength(20).IsRequired();
        e.Property(x => x.Importo).HasPrecision(18, 2);
        e.HasIndex(x => new { x.Anno, x.CodiceCliente });
    }
}

public class ConfigurazioneCommentoAiDirezione : IEntityTypeConfiguration<CommentoAiDirezione>
{
    public void Configure(EntityTypeBuilder<CommentoAiDirezione> e)
    {
        e.ToTable("CommentiAi", "dir");
        e.HasKey(x => x.IdCommentoAiDirezione);
        e.Property(x => x.Pagina).HasMaxLength(20).IsRequired();
        e.Property(x => x.Filtri).HasMaxLength(1000).IsRequired();
        e.Property(x => x.DescrizioneFiltri).HasMaxLength(500).IsRequired();
        e.Property(x => x.DatiInviati).IsRequired();
        e.Property(x => x.Testo).IsRequired();
        e.Property(x => x.Modello).HasMaxLength(100).IsRequired();
        e.Property(x => x.CostoStimato).HasPrecision(10, 4);
        e.Property(x => x.NomeUtente).HasMaxLength(200);
        e.HasIndex(x => new { x.Pagina, x.DataCreazione });
    }
}
