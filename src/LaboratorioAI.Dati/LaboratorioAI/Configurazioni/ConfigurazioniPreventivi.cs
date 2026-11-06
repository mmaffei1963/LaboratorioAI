using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LaboratorioAI.Dati.LaboratorioAI.Configurazioni;

// Modulo Preventivi (schema "preventivi"). Nessuna scrittura su ArcLab:
// i dati dei controlli/prezzi sono snapshot letti in sola lettura.

public class ConfigurazionePreventivo : IEntityTypeConfiguration<Preventivo>
{
    public void Configure(EntityTypeBuilder<Preventivo> e)
    {
        e.ToTable("Preventivi", "preventivi");
        e.HasKey(x => x.IdPreventivo);

        e.Property(x => x.Stato).HasConversion<int>();

        // Cliente (anagrafica mista)
        e.Property(x => x.ClienteRagioneSociale).HasMaxLength(255);
        e.Property(x => x.ClientePartitaIva).HasMaxLength(20);
        e.Property(x => x.ClienteIndirizzo).HasMaxLength(255);
        e.Property(x => x.ClienteCap).HasMaxLength(10);
        e.Property(x => x.ClienteCitta).HasMaxLength(120);
        e.Property(x => x.ClienteProvincia).HasMaxLength(5);
        e.Property(x => x.ClienteEmail).HasMaxLength(255);
        e.Property(x => x.ClienteTelefono).HasMaxLength(50);
        e.Property(x => x.ClienteNome).HasMaxLength(255);

        // Offerta
        e.Property(x => x.Oggetto).HasMaxLength(500);
        e.Property(x => x.DataOfferta).HasColumnType("date");
        // 1000 (non più 500): scelto per ospitare una condizione di pagamento completa di IBAN,
        // banca e intestatario (v. MetodoPagamento), copiata qui come testo libero e poi restando
        // comunque modificabile per la singola offerta.
        e.Property(x => x.CondizioniPagamento).HasMaxLength(1000);
        e.Property(x => x.RevisioneCondizioni).HasMaxLength(120);

        // Importi
        e.Property(x => x.TotaleImponibile).HasColumnType("money");
        e.Property(x => x.AliquotaIva).HasColumnType("decimal(5,2)");

        e.Property(x => x.Note).HasMaxLength(2000);
        e.Property(x => x.RiepilogoPromozionale).HasDefaultValue(false);
        e.Property(x => x.MostraInsediamenti).HasDefaultValue(false);

        e.Property(x => x.DataCreazione).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.DataUltimaModifica).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasIndex(x => new { x.Stato, x.Attivo });
        e.HasIndex(x => new { x.Anno, x.Numero });
        e.HasIndex(x => x.IdClienteArcLab);

        e.HasMany(x => x.Righe)
         .WithOne(r => r.Preventivo!)
         .HasForeignKey(r => r.IdPreventivo)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConfigurazioneRigaPreventivo : IEntityTypeConfiguration<RigaPreventivo>
{
    public void Configure(EntityTypeBuilder<RigaPreventivo> e)
    {
        e.ToTable("Righe", "preventivi");
        e.HasKey(x => x.IdRigaPreventivo);

        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.OriginePrezzo).HasConversion<int>();
        e.Property(x => x.Descrizione).HasMaxLength(500);
        e.Property(x => x.IdControlliOrigine).HasMaxLength(200);
        e.Property(x => x.RifLocale).HasMaxLength(20);
        e.Property(x => x.Insediamento).HasMaxLength(255);   // 05/11/2026
        // 4000 (12/10/2026): per le voci accessorie è il testo descrittivo stampato in offerta sotto la voce.
        e.Property(x => x.Note).HasMaxLength(4000);

        e.Property(x => x.PrezzoBase).HasColumnType("money");
        e.Property(x => x.PrezzoFinale).HasColumnType("money");
        e.Property(x => x.ScontoPercentuale).HasColumnType("decimal(5,2)");

        e.HasIndex(x => x.IdPreventivo);

        e.HasMany(x => x.Metodi)
         .WithOne(m => m.Riga!)
         .HasForeignKey(m => m.IdRigaPreventivo)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConfigurazioneRigaMetodoPreventivo : IEntityTypeConfiguration<RigaMetodoPreventivo>
{
    public void Configure(EntityTypeBuilder<RigaMetodoPreventivo> e)
    {
        e.ToTable("RigheMetodo", "preventivi");
        e.HasKey(x => x.IdRigaMetodoPreventivo);

        e.Property(x => x.Descrizione).HasMaxLength(255);
        e.Property(x => x.DenominazioneRiga).HasMaxLength(255);
        e.Property(x => x.RiferimentoNormativo).HasMaxLength(255);
        e.Property(x => x.UnitaMisura).HasMaxLength(50);
        e.Property(x => x.CostoMetodoEuro).HasColumnType("money");
        e.Property(x => x.LabEsterno).HasMaxLength(100);
        e.Property(x => x.LabEsternoFrase).HasMaxLength(500);

        e.HasIndex(x => x.IdRigaPreventivo);
    }
}

public class ConfigurazioneVoceListinoAccessorio : IEntityTypeConfiguration<VoceListinoAccessorio>
{
    public void Configure(EntityTypeBuilder<VoceListinoAccessorio> e)
    {
        e.ToTable("ListinoAccessori", "preventivi");
        e.HasKey(x => x.IdVoceAccessorio);

        e.Property(x => x.Codice).HasMaxLength(40).IsRequired();
        e.Property(x => x.Descrizione).HasMaxLength(255).IsRequired();
        e.Property(x => x.Prezzo).HasColumnType("money");
        e.Property(x => x.UnitaMisura).HasMaxLength(20);

        e.HasIndex(x => x.Codice).IsUnique();

        // Seed dal listino accessori delle Condizioni Generali di Fornitura (Mod. Com. 05 Rev. 18).
        e.HasData(
            new VoceListinoAccessorio { IdVoceAccessorio = 1, Codice = "GIUDIZIO_CONF", Descrizione = "Giudizio di conformità sul rapporto di prova", Prezzo = 10m, UnitaMisura = "cad", Attivo = true },
            new VoceListinoAccessorio { IdVoceAccessorio = 2, Codice = "RIEMISSIONE_RDP", Descrizione = "Riemissione rapporto di prova su richiesta del cliente", Prezzo = 20m, UnitaMisura = "cad", Attivo = true },
            new VoceListinoAccessorio { IdVoceAccessorio = 3, Codice = "CONSERVAZIONE", Descrizione = "Conservazione campione oltre 20 giorni per cause del cliente", Prezzo = 0.50m, UnitaMisura = "giorno", Attivo = true },
            new VoceListinoAccessorio { IdVoceAccessorio = 4, Codice = "CAMPIONAMENTO", Descrizione = "Prelievo eseguito da operatore MARE.A", Prezzo = 0m, UnitaMisura = "cad", Attivo = true }
        );
    }
}

public class ConfigurazioneMetodoPagamento : IEntityTypeConfiguration<MetodoPagamento>
{
    public void Configure(EntityTypeBuilder<MetodoPagamento> e)
    {
        e.ToTable("MetodiPagamento", "preventivi");
        e.HasKey(x => x.IdMetodoPagamento);

        // 1000 (non più 255): la descrizione può includere per esteso IBAN, banca e intestatario
        // (sempre i nostri dati, mai quelli del cliente — nessuna tabella dedicata necessaria).
        e.Property(x => x.Descrizione).HasMaxLength(1000).IsRequired();

        // Seed: l'unica condizione finora usata come testo libero nelle offerte.
        e.HasData(
            new MetodoPagamento { IdMetodoPagamento = 1, Descrizione = "Bonifico bancario vista fattura", Ordine = 1, Attivo = true }
        );
    }
}
