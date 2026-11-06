using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Esporta in Excel un certificato emesso: intestazione + tabella dei valori delle prove.</summary>
public interface IEsportatoreCertificato
{
    byte[] Genera(CertificatoCompleto completo);
    string NomeFile(CertificatoCompleto completo);
}

/// <inheritdoc />
public class EsportatoreCertificato : IEsportatoreCertificato
{
    private static readonly string[] Intestazioni =
    {
        "Parametro", "Valore", "Valore (num.)", "Unità di misura", "Intervallo di riferimento",
        "Riferimento normativo", "Verifica", "Eseguito da", "Accreditamento"
    };

    public byte[] Genera(CertificatoCompleto d)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Certificato");
        int colonne = Intestazioni.Length;

        var riga = StileFoglio.Titolo(foglio,
            $"Certificato {d.Certificato.NumeroCertificato}",
            $"Estratto il {DateTime.Now:dd/MM/yyyy HH:mm}", colonne);

        // ── Intestazione (coppie etichetta/valore) ──
        void Info(string etichetta, string? valore)
        {
            foglio.Cell(riga, 1).Value = etichetta;
            foglio.Cell(riga, 1).Style.Font.Bold = true;
            foglio.Cell(riga, 2).Value = valore ?? "";
            riga++;
        }
        Info("Data certificato", d.Certificato.DataCertificato?.ToString("dd/MM/yyyy"));
        Info("Tempo di risposta (gg)", d.Certificato.GiorniRisposta?.ToString());
        Info("Cliente", d.Campione.RagioneSociale);
        Info("Insediamento", d.Campione.NomeInsediamento);
        Info("Campione", d.Campione.NumeroCampione);
        Info("Denominazione", d.Campione.DenominazioneCampione);
        Info("Matrice", d.Campione.Matrice);
        Info("Controllo", d.Campione.Controllo);
        riga++;

        // ── Tabella prove ──
        for (int i = 0; i < colonne; i++) foglio.Cell(riga, i + 1).Value = Intestazioni[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var corrente = riga + 1;
        foreach (var p in d.Prove)
        {
            foglio.Cell(corrente, 1).Value = p.DenominazioneRiga ?? p.Parametro;
            foglio.Cell(corrente, 2).Value = p.ValoreFormattato;
            if (p.ValoreCalcolato is decimal v) foglio.Cell(corrente, 3).Value = v;
            foglio.Cell(corrente, 4).Value = p.UnitaMisura;
            foglio.Cell(corrente, 5).Value = p.IntervalloFormattato;
            foglio.Cell(corrente, 6).Value = p.RiferimentoNormativo;
            foglio.Cell(corrente, 7).Value = p.VerificaValore == true ? "Verificato"
                : p.VerificaValore == false ? "Non verificato" : "—";
            foglio.Cell(corrente, 8).Value = string.IsNullOrWhiteSpace(p.LaboratorioEsterno)
                ? "MARE.A"
                : p.LaboratorioEsterno + (string.IsNullOrWhiteSpace(p.NumeroLaboratorioEsterno) ? "" : $" (n° {p.NumeroLaboratorioEsterno})");
            // Prova in subappalto: conta solo l'accreditamento del laboratorio esterno, mai quello di MARE.A (Art. 8.2).
            foglio.Cell(corrente, 9).Value = !string.IsNullOrWhiteSpace(p.LaboratorioEsterno)
                ? (p.AccreditatoEsterno ? "Lab esterno accreditato" : "")
                : (p.Accreditato ? "MARE.A accreditato" : "");
            corrente++;
        }

        foglio.Column(3).Style.NumberFormat.Format = "#,##0.####";
        StileFoglio.Rifinisci(foglio, riga, corrente - 1, colonne);
        if (d.Prove.Count > 0) foglio.Range(riga, 1, corrente - 1, colonne).SetAutoFilter();

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile(CertificatoCompleto d)
    {
        var nome = new string((d.Certificato.NumeroCertificato ?? "Certificato")
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"Certificato_{nome}.xlsx";
    }
}
