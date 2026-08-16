using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Produce il foglio di calcolo delle analisi affidate all'esterno.</summary>
public interface IEsportatoreLaboratoriEsterni
{
    /// <summary>Genera la cartella di lavoro e la restituisce come flusso di byte.</summary>
    byte[] Genera(EsitoLaboratoriEsterni esito, CriteriLaboratoriEsterni criteri);

    /// <summary>Nome file suggerito, coerente con il periodo estratto.</summary>
    string NomeFile(CriteriLaboratoriEsterni criteri);
}

/// <inheritdoc />
public class EsportatoreLaboratoriEsterni : IEsportatoreLaboratoriEsterni
{
    public byte[] Genera(EsitoLaboratoriEsterni esito, CriteriLaboratoriEsterni criteri)
    {
        using var cartella = new XLWorkbook();

        var periodo = $"Periodo di analisi dal {criteri.DataDa:dd/MM/yyyy} "
                    + $"al {criteri.DataA:dd/MM/yyyy} — "
                    + $"estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";

        ScriviRiepilogo(cartella, esito.Riepilogo, periodo);
        ScriviProve(cartella, esito.Prove, periodo);

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile(CriteriLaboratoriEsterni criteri) =>
        $"LaboratoriEsterni_{criteri.DataDa:yyyyMMdd}-{criteri.DataA:yyyyMMdd}.xlsx";

    // ── Foglio 1: riepilogo per metodo ─────────────────────────────

    private static void ScriviRiepilogo(
        XLWorkbook cartella, IReadOnlyList<RiepilogoMetodoEsterno> righe, string periodo)
    {
        var foglio = cartella.Worksheets.Add("Riepilogo metodi");
        const int colonne = 6;

        var riga = StileFoglio.Titolo(foglio, "Analisi affidate a laboratori esterni", periodo, colonne);

        foglio.Cell(riga, 1).Value = "Laboratorio";
        foglio.Cell(riga, 2).Value = "Matrice";
        foglio.Cell(riga, 3).Value = "Metodo";
        foglio.Cell(riga, 4).Value = "Riferimento normativo";
        foglio.Cell(riga, 5).Value = "Prove";
        foglio.Cell(riga, 6).Value = "Di cui certificate";
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var prima = riga + 1;
        var corrente = prima;

        foreach (var r in righe)
        {
            foglio.Cell(corrente, 1).Value = r.LaboratorioEsterno;
            foglio.Cell(corrente, 2).Value = r.Matrice;
            foglio.Cell(corrente, 3).Value = r.DenominazioneRiga;
            foglio.Cell(corrente, 4).Value = r.RiferimentoNormativo;
            foglio.Cell(corrente, 5).Value = r.NumeroProve;
            foglio.Cell(corrente, 6).Value = r.NumeroCertificate;
            corrente++;
        }

        // Riga di totale, utile per la verifica a colpo d'occhio
        if (righe.Count > 0)
        {
            foglio.Cell(corrente, 4).Value = "Totale";
            foglio.Cell(corrente, 5).Value = righe.Sum(r => r.NumeroProve);
            foglio.Cell(corrente, 6).Value = righe.Sum(r => r.NumeroCertificate);
            foglio.Range(corrente, 1, corrente, colonne).Style.Font.Bold = true;
            foglio.Range(corrente, 1, corrente, colonne).Style.Fill.BackgroundColor = StileFoglio.Chiaro;
        }

        StileFoglio.Rifinisci(foglio, riga, corrente, colonne);
    }

    // ── Foglio 2: elenco analitico ─────────────────────────────────

    private static void ScriviProve(
        XLWorkbook cartella, IReadOnlyList<ProvaEsterna> righe, string periodo)
    {
        var foglio = cartella.Worksheets.Add("Elenco prove");
        const int colonne = 12;

        var riga = StileFoglio.Titolo(foglio, "Elenco delle prove", periodo, colonne);

        foglio.Cell(riga, 1).Value  = "N° campione";
        foglio.Cell(riga, 2).Value  = "Cliente";
        foglio.Cell(riga, 3).Value  = "Insediamento";
        foglio.Cell(riga, 4).Value  = "Ricezione";
        foglio.Cell(riga, 5).Value  = "Data analisi";
        foglio.Cell(riga, 6).Value  = "Denominazione";
        foglio.Cell(riga, 7).Value  = "Matrice";
        foglio.Cell(riga, 8).Value  = "Metodo";
        foglio.Cell(riga, 9).Value  = "Riferimento normativo";
        foglio.Cell(riga, 10).Value = "Laboratorio";
        foglio.Cell(riga, 11).Value = "Accreditato esterno";
        foglio.Cell(riga, 12).Value = "Stato";
        StileFoglio.Intestazione(foglio, riga, colonne);

        var corrente = riga + 1;

        foreach (var r in righe)
        {
            foglio.Cell(corrente, 1).Value  = r.NumeroCampione;
            foglio.Cell(corrente, 2).Value  = r.RagioneSociale;
            foglio.Cell(corrente, 3).Value  = r.NomeInsediamento;
            foglio.Cell(corrente, 4).Value  = r.DataRicezione;
            foglio.Cell(corrente, 5).Value  = r.DataAnalisi;
            foglio.Cell(corrente, 6).Value  = r.DenominazioneCampione;
            foglio.Cell(corrente, 7).Value  = r.Matrice;
            foglio.Cell(corrente, 8).Value  = r.DenominazioneRiga;
            foglio.Cell(corrente, 9).Value  = r.RiferimentoNormativo;
            foglio.Cell(corrente, 10).Value = r.LaboratorioEsterno;
            foglio.Cell(corrente, 11).Value = r.AccreditatoEsterno ? "Sì" : "No";
            foglio.Cell(corrente, 12).Value = r.Stato == StatoCampione.Certificato
                ? "Certificato" : "In corso";
            corrente++;
        }

        foglio.Column(4).Style.DateFormat.Format = "dd/MM/yyyy";
        foglio.Column(5).Style.DateFormat.Format = "dd/MM/yyyy";

        StileFoglio.Rifinisci(foglio, riga, corrente - 1, colonne);

        if (righe.Count > 0)
            foglio.Range(riga, 1, corrente - 1, colonne).SetAutoFilter();
    }
}
