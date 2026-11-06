using LaboratorioAI.Core.CicloAnalitico;
using LaboratorioAI.Core.Entita;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Avviso campioni in ritardo (22/10/2026).</summary>
public class MotoreRitardiCampioniTest
{
    private static ProvaInAttesa P(int id, string matrice, DateTime ric, int? ritardo, bool verificata, int? lab = null, string par = "Parametro") =>
        new() { IdCampione = id, NumeroCampione = $"100/{id}", Matrice = matrice, DataRicezione = ric, RitardoAnalisi = ritardo,
                Verificata = verificata, IdLabEsterno = lab, LabEsterno = lab is null ? null : "Esterno", NomeParametro = par };

    private static readonly ImpostazioniRitardi Imp = new() { SogliaSubappalto = 20 };   // alimenti 7, legionella 14

    [Fact]
    public void InizioConRitardoProgrammato_10Ottobre_Piu5_E15Ottobre()
    {
        Assert.Equal(new DateTime(2026, 10, 15), MotoreRitardiCampioni.DataInizio(new DateTime(2026, 10, 10), 5));
        // alimenti, soglia 7: il 22/10 sono 7 giorni dall'inizio → non ancora in ritardo; il 23/10 sì
        var prove = new[] { P(1, "Formaggio", new(2026, 10, 10), 5, false) };
        Assert.Empty(MotoreRitardiCampioni.Calcola(prove, Imp, new(2026, 10, 22)));
        var r = MotoreRitardiCampioni.Calcola(prove, Imp, new(2026, 10, 23)).Single();
        Assert.Equal(StatoRitardo.ParametriMancanti, r.Stato);
        Assert.Equal(1, r.GiorniRitardo);
    }

    [Fact]
    public void StatoESogliaPerTipo()
    {
        var prove = new[]
        {
            P(1, "Acqua sanitaria", new(2026, 9, 1), null, true),                    // legionella, tutto verificato → rapporto da emettere
            P(2, "Formaggio", new(2026, 9, 1), null, true), P(2, "Formaggio", new(2026, 9, 1), null, false, 7),   // solo esterna mancante
            P(3, "Formaggio", new(2026, 10, 20), null, false),                        // recente: non in ritardo
        };
        var r = MotoreRitardiCampioni.Calcola(prove, Imp, new(2026, 10, 22));
        Assert.Equal(2, r.Count);
        Assert.Equal(StatoRitardo.RapportoDaEmettere, r.Single(x => x.IdCampione == 1).Stato);
        Assert.Equal(14, r.Single(x => x.IdCampione == 1).Soglia);
        Assert.Equal(StatoRitardo.Subappalto, r.Single(x => x.IdCampione == 2).Stato);
        Assert.Equal(20, r.Single(x => x.IdCampione == 2).Soglia);
    }

    [Fact]
    public void CampioniNascostiEsclusi()
    {
        var prove = new[] { P(1, "Formaggio", new(2026, 1, 1), null, false) };
        Assert.Empty(MotoreRitardiCampioni.Calcola(prove, Imp, new(2026, 10, 22), id => id == 1));
    }

    [Fact]
    public void SogliaImpostataVinceSullaPredefinita()
    {
        var imp = new ImpostazioniRitardi { Soglie = { [FamigliaNormativa.Alimenti.ToString()] = 30 } };
        Assert.Equal(30, imp.Soglia(FamigliaNormativa.Alimenti));
        Assert.Equal(14, imp.Soglia(FamigliaNormativa.Legionella));
    }
}
