using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Composizione e calcolo dei preventivi.
///
/// Legge da ArcLab in sola lettura (controlli, metodi, prezzi di commessa) e produce righe di
/// preventivo con snapshot dei dati, così l'offerta resta stabile nel tempo. Il prezzo base
/// segue la cascata: prezzo di commessa del cliente se esiste, altrimenti somma dei
/// CostoMetodoEuro dei metodi del controllo. Lo sconto è una percentuale sul prezzo base e,
/// se presente su una qualunque riga, accende la richiesta di approvazione.
/// </summary>
public interface IServizioPreventivi
{
    /// <summary>Compone una riga a partire da un controllo ArcLab esistente (snapshot metodi + prezzo base a cascata).</summary>
    /// <param name="idInsediamentoArcLab">05/11/2026: insediamento della riga; il prezzo di commessa si cerca prima lì,
    /// poi su tutte le commesse del cliente, infine il listino.</param>
    Task<RigaPreventivo> ComponiRigaControlloAsync(
        int idControllo, string? nomeControllo, int? idClienteArcLab,
        decimal scontoPercentuale, CancellationToken ct = default, int? idInsediamentoArcLab = null);

    /// <summary>Compone una riga "controllo libero" (subappalto) con prezzo concordato e metodi inseriti a mano.</summary>
    RigaPreventivo ComponiRigaLibera(
        string? nomeControllo, decimal prezzoConcordato, decimal scontoPercentuale,
        IEnumerable<RigaMetodoPreventivo> metodiLiberi);

    /// <summary>Compone una riga accessoria da listino (giudizio conformità, conservazione, ecc.).</summary>
    RigaPreventivo ComponiRigaAccessorio(VoceListinoAccessorio voce, int quantita, decimal scontoPercentuale);

    /// <summary>
    /// Verifica se un metodo è già presente tra i nostri (ricerca sul catalogo ArcLab):
    /// il commerciale la usa prima di inserire un metodo a campo libero.
    /// </summary>
    Task<IReadOnlyList<VoceMetodo>> CercaMetodiNostriAsync(string filtro, CancellationToken ct = default);

    /// <summary>Ricalcola i prezzi finali di riga, il totale imponibile e il flag di approvazione.</summary>
    void Ricalcola(Preventivo preventivo);
}

/// <inheritdoc cref="IServizioPreventivi" />
public class ServizioPreventivi : IServizioPreventivi
{
    private readonly IRepositoryControlli _controlli;
    private readonly IRepositoryCommesse _commesse;

    public ServizioPreventivi(IRepositoryControlli controlli, IRepositoryCommesse commesse)
    {
        _controlli = controlli;
        _commesse = commesse;
    }

    public async Task<RigaPreventivo> ComponiRigaControlloAsync(
        int idControllo, string? nomeControllo, int? idClienteArcLab,
        decimal scontoPercentuale, CancellationToken ct = default, int? idInsediamentoArcLab = null)
    {
        var metodi = await _controlli.RigheDiControlloAsync(idControllo, ct);
        var sommaCosto = metodi.Sum(m => m.Costo ?? 0m);

        decimal? prezzoCommessa = null;
        if (idClienteArcLab is int idc)
        {
            if (idInsediamentoArcLab is int idIns)
                prezzoCommessa = await _commesse.PrezzoControlloClienteAsync(idc, idControllo, ct, idIns);
            if (prezzoCommessa is not > 0)
                prezzoCommessa = await _commesse.PrezzoControlloClienteAsync(idc, idControllo, ct);
        }

        var (prezzoBase, origine) = prezzoCommessa is decimal pc && pc > 0
            ? (pc, OriginePrezzoPreventivo.Commessa)
            : (sommaCosto, OriginePrezzoPreventivo.Listino);

        var riga = new RigaPreventivo
        {
            Tipo = TipoRigaPreventivo.ControlloEsistente,
            IdControlloArcLab = idControllo,
            IdInsediamentoArcLab = idInsediamentoArcLab,
            Descrizione = nomeControllo ?? await _controlli.NomeControlloAsync(idControllo, ct),
            OriginePrezzo = origine,
            PrezzoBase = prezzoBase,
            ScontoPercentuale = scontoPercentuale,
            Quantita = 1
        };

        var progressivo = 1;
        foreach (var m in metodi)
        {
            riga.Metodi.Add(new RigaMetodoPreventivo
            {
                Progressivo = progressivo++,
                IdMetodoArcLab = m.Id,
                Libero = false,
                Descrizione = m.Descrizione,
                DenominazioneRiga = m.DenominazioneRiga,
                Denominazione = m.DettagliMetodo,
                RiferimentoNormativo = m.RifBiblio,
                UnitaMisura = m.UnitaMisura,
                CostoMetodoEuro = m.Costo,
                Accreditato = m.Accreditato,
                CifreSignificative = m.CifreSignificative,
                MaxDecimali = m.MaxDecimali,
                IdLabEsternoArcLab = m.IdLabEsterno,
                LabEsterno = m.LabEsterno,
                LabEsternoFrase = m.LabEsternoFrase
            });
        }

        riga.PrezzoFinale = PrezzoFinale(riga);
        return riga;
    }

    public RigaPreventivo ComponiRigaLibera(
        string? nomeControllo, decimal prezzoConcordato, decimal scontoPercentuale,
        IEnumerable<RigaMetodoPreventivo> metodiLiberi)
    {
        var riga = new RigaPreventivo
        {
            Tipo = TipoRigaPreventivo.ControlloLibero,
            Descrizione = nomeControllo,
            OriginePrezzo = OriginePrezzoPreventivo.Manuale,
            PrezzoBase = prezzoConcordato,
            ScontoPercentuale = scontoPercentuale,
            Quantita = 1
        };

        var progressivo = 1;
        foreach (var m in metodiLiberi)
        {
            m.Progressivo = progressivo++;
            m.Libero = m.IdMetodoArcLab is null;
            riga.Metodi.Add(m);
        }

        riga.PrezzoFinale = PrezzoFinale(riga);
        return riga;
    }

    public RigaPreventivo ComponiRigaAccessorio(VoceListinoAccessorio voce, int quantita, decimal scontoPercentuale)
    {
        var riga = new RigaPreventivo
        {
            Tipo = TipoRigaPreventivo.Accessorio,
            IdVoceAccessorio = voce.IdVoceAccessorio,
            Descrizione = voce.Descrizione,
            OriginePrezzo = OriginePrezzoPreventivo.Listino,
            PrezzoBase = voce.Prezzo,
            ScontoPercentuale = scontoPercentuale,
            Quantita = quantita < 1 ? 1 : quantita
        };
        riga.PrezzoFinale = PrezzoFinale(riga);
        return riga;
    }

    public Task<IReadOnlyList<VoceMetodo>> CercaMetodiNostriAsync(string filtro, CancellationToken ct = default) =>
        _controlli.MetodiCatalogoAsync(filtro, ct);

    public void Ricalcola(Preventivo preventivo)
    {
        foreach (var r in preventivo.Righe)
            r.PrezzoFinale = PrezzoFinale(r);

        preventivo.TotaleImponibile = preventivo.Righe.Sum(r => r.PrezzoFinale);
        preventivo.RichiedeApprovazione = preventivo.Righe.Any(r => r.ScontoPercentuale > 0m);
    }

    /// <summary>Prezzo finale di riga = base * quantità * (1 - sconto%/100), arrotondato al centesimo.</summary>
    private static decimal PrezzoFinale(RigaPreventivo r)
    {
        var quantita = r.Quantita < 1 ? 1 : r.Quantita;
        var lordo = r.PrezzoBase * quantita;
        var netto = lordo * (1m - r.ScontoPercentuale / 100m);
        return Math.Round(netto, 2, MidpointRounding.AwayFromZero);
    }
}
