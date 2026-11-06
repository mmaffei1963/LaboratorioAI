using LaboratorioAI.Core.Andamento;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Cambi di gestione (12/10/2026): casi presi dalle prove in SSMS del 12/10/2026.</summary>
public class MotoreSubentriTest
{
    private static readonly DateTime Oggi = new(2026, 10, 12);
    private readonly MotoreSubentri _motore = new();

    private static InsediamentoSubentri Ins(int id, int cliente, string codice, string rag, string? piva, string? nome, string? via, string? citta) =>
        new(id, cliente, codice, rag, piva, nome, via, citta);

    /// <summary>Campioni in un mese: Legionella se leg = true.</summary>
    private static AttivitaInsediamentoMese Mese(int id, int anno, int mese, int n, bool leg = true) =>
        new(id, anno, mese, n, leg ? n : 0, n * 33.5m, new DateTime(anno, mese, 20), new DateTime(anno, mese, 10));

    private RisultatoSubentro Unico(IEnumerable<InsediamentoSubentri> ins, IEnumerable<AttivitaInsediamentoMese> camp,
        IEnumerable<DecisioneSubentro>? dec = null) =>
        Assert.Single(_motore.Calcola(ins, camp, dec ?? [], Oggi).Persi);

    [Fact]
    public void NuovoGestoreAttivo_StessoNome_EsitoA()
    {
        var r = Unico(
            [Ins(1, 10, "0312", "HOTEL BRIG di Semprini", "02189470400", "HOTEL BRIG", "via Carducci, 139", "CATTOLICA"),
             Ins(2, 20, "7227", "DF SMART HOTELS", "04838430405", "Hotel Brig", "Via Carducci 139", "Cattolica")],
            [Mese(1, 2025, 6, 6), Mese(2, 2026, 6, 6)]);
        Assert.Equal(EsitoSubentro.SubentratoAttivo, r.Esito);
        Assert.Equal(AffidabilitaSubentro.Alta, r.Successore!.Affidabilita);
        Assert.True(r.NonPerso);
    }

    [Fact]
    public void NuovoGestoreSenzaCampioni_CodicePiuAlto_EsitoC()
    {
        var r = Unico(
            [Ins(1, 10, "2052", "DARSENA S.A.S.", "00847310406", "HOTEL DARSENA", "Via Galli, 5", "RICCIONE"),
             Ins(2, 5, "7063", "FH S.R.L. HOTEL DARSENA", "04722030402", "HOTEL DARSENA", "Via Galli 5", "RICCIONE")],
            [Mese(1, 2025, 3, 6)]);
        Assert.Equal(EsitoSubentro.SubentratoNonAttivo, r.Esito);
    }

    [Fact]
    public void GestorePrecedente_NonContaComeSubentro_EsitoD()
    {
        // Il vecchio gestore (codice più basso, nessun campione) non è un subentro anche se il suo Id ArcLab è più alto.
        var r = Unico(
            [Ins(1, 10, "3404", "FAMIGLIA PEDRELLI HOTELS", "04056930409", "HOTEL BREMEN", "VIALE BRINDISI, 3", "RIVAZZURRA DI RIMINI"),
             Ins(2, 99, "1821", "HOTEL BREMEN di Bernabè", "00632790408", "HOTEL BREMEN di Bernabè Maurizio", "Viale Brindisi 3", "Rivazzurra di Rimini")],
            [Mese(1, 2025, 6, 6)]);
        Assert.Equal(EsitoSubentro.NessunSubentro, r.Esito);
    }

    [Fact]
    public void SoloStessoIndirizzo_DaVerificare_NonContaFinoAllaConferma()
    {
        InsediamentoSubentri[] ins =
            [Ins(1, 10, "3116", "RESIDENCE PROMENADE S.A.S.", "03152570408", "RESIDENCE PROMENADE", "Via Facchini, 1", "CATTOLICA"),
             Ins(2, 20, "3487", "BAGNI 98-102 di Maltoni", "03198410403", "BAGNI 98-99-100-101-102", "Via Facchini 1", "CATTOLICA")];
        AttivitaInsediamentoMese[] camp = [Mese(1, 2025, 8, 6), Mese(2, 2023, 6, 1), Mese(2, 2026, 5, 2)];
        var r = Unico(ins, camp);
        Assert.Equal(EsitoSubentro.SubentratoAttivo, r.Esito);
        Assert.Equal(AffidabilitaSubentro.Bassa, r.Successore!.Affidabilita);
        Assert.True(r.DaVerificare);
        Assert.False(r.NonPerso);

        // Scartato: non viene più proposto.
        var scartato = Unico(ins, camp, [new DecisioneSubentro(1, 2, false)]);
        Assert.Equal(EsitoSubentro.NessunSubentro, scartato.Esito);
    }

    [Fact]
    public void StessoClienteAttivoAltrove_EsitoE()
    {
        var r = Unico(
            [Ins(1, 10, "0342", "ECOSYS S.R.L.", "02672590409", "ECOSYS", "Via E. Romagna, 206", "CATTOLICA"),
             Ins(2, 10, "0342", "ECOSYS S.R.L.", "02672590409", "HOTEL CLIENTE DI ECOSYS", "Via Roma 1", "RIMINI")],
            [Mese(1, 2025, 7, 79), Mese(2, 2026, 7, 215, leg: false)]);
        Assert.Equal(EsitoSubentro.ClienteAttivoAltrove, r.Esito);
        Assert.Equal(215, r.CampioniClienteAltrove);
        Assert.True(r.NonPerso);
    }

    [Fact]
    public void StessaPartitaIva_EsitoB_EPartitaIvaFittiziaIgnorata()
    {
        var r = Unico(
            [Ins(1, 10, "", "BAGNI GIUSEPPE SAS", "03207000401", "BAGNI GIUSEPPE SAS", "Viale Monte Rosa, 11", "RICCIONE"),
             Ins(2, 20, "3438", "BAGNI GIUSEPPE SAS", "03207000401", "BAGNI GIUSEPPE SAS", "Viale Monte Rosa 11", "RICCIONE")],
            [Mese(1, 2025, 5, 1), Mese(2, 2026, 5, 1)]);
        Assert.Equal(EsitoSubentro.AnagraficaDoppia, r.Esito);

        Assert.Null(MotoreSubentri.PartitaIvaValida("99999999999"));
        Assert.Equal("04722030402", MotoreSubentri.PartitaIvaValida("IT 04722030402"));
    }

    [Fact]
    public void ConfermaManuale_NessunSubentro_ERegistrata()
    {
        var r = Unico(
            [Ins(1, 10, "6876", "CANDIOTTI MATTEO HOTEL LUCCIOLA", "04773280401", "HOTEL LUCCIOLA", "Via Dante, 113", "CATTOLICA")],
            [Mese(1, 2025, 5, 10)], [new DecisioneSubentro(1, null, true)]);
        Assert.Equal(EsitoSubentro.NessunSubentro, r.Esito);
        Assert.True(r.Confermato);
    }

    [Fact]
    public void FinestraSeiMesi_ConfrontaGliStessiMesiDellAnnoPrima()
    {
        // Oggi 12/10/2026: 6 mesi = apr–set 2026 contro apr–set 2025.
        InsediamentoSubentri[] ins = [Ins(1, 10, "1160", "HOTEL HAWAY", "00745560409", "HOTEL HAWAY", "Via Sardegna 21", "MISANO")];
        var sei = new FinestraConfronto { Mesi = 6 };
        var r = _motore.Calcola(ins, [Mese(1, 2025, 5, 6)], [], Oggi, sei);
        Assert.Equal("apr–set 2026", r.Attuale);
        Assert.Equal("apr–set 2025", r.Confronto);
        Assert.Single(r.Persi);

        // Legionella solo fuori dai mesi di confronto (ottobre 2025): non è tra le perse dei 6 mesi.
        Assert.Empty(_motore.Calcola(ins, [Mese(1, 2025, 10, 6)], [], Oggi, sei).Persi);

        // Due anni prima: apr–set 2024.
        var due = _motore.Calcola(ins, [Mese(1, 2024, 6, 6)], [], Oggi, new FinestraConfronto { Mesi = 6, AnniIndietro = 2 });
        Assert.Equal("apr–set 2024", due.Confronto);
        Assert.Single(due.Persi);
    }

    [Fact]
    public void AreeDellaConsulenzaDallaVoce()
    {
        Assert.Equal(AmbitoSubentri.Sicurezza, AmbitiSubentri.AreaDaVoce("626", 5));
        Assert.Equal(AmbitoSubentri.IgieneAlimentare, AmbitiSubentri.AreaDaVoce("HAC", 5));
        Assert.Equal(AmbitoSubentri.Ambiente, AmbitiSubentri.AreaDaVoce("RIF", 5));
        Assert.Equal(AmbitoSubentri.Formazione, AmbitiSubentri.AreaDaVoce("626", 4));
        Assert.Null(AmbitiSubentri.AreaDaVoce("", 5));
    }

    [Fact]
    public void ChiaviIgnoranoPunteggiaturaEPrefissi()
    {
        Assert.Equal(MotoreSubentri.ChiaveVia("V.le Carducci, 6"), MotoreSubentri.ChiaveVia("VIALE CARDUCCI 6"));
        Assert.Equal(MotoreSubentri.ChiaveNome("Hotel Canasta"), MotoreSubentri.ChiaveNome("CANASTA"));
        Assert.Equal(MotoreSubentri.ChiaveCitta("Riccione "), MotoreSubentri.ChiaveCitta("RICCIONE"));
    }
}
