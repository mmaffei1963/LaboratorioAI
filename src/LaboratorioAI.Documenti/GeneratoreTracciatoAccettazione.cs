using System.Text;
using System.Xml.Linq;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Genera il tracciato XML di un'accettazione, da consegnare a Palomar per l'import in ArcLab.
/// Formato PROVVISORIO: da allineare alla specifica Palomar (come il flusso Legionella).
/// </summary>
public interface IGeneratoreTracciatoAccettazione
{
    byte[] GeneraXml(AccettazioneLotto lotto);
    string NomeFile(AccettazioneLotto lotto);
}

/// <inheritdoc />
public class GeneratoreTracciatoAccettazione : IGeneratoreTracciatoAccettazione
{
    private const string FormatoData = "yyyy-MM-dd";

    public string NomeFile(AccettazioneLotto l) =>
        $"Accettazione_{l.DataConferimento:yyyyMMdd}_{l.CodiceLocale[..8]}.xml";

    public byte[] GeneraXml(AccettazioneLotto l)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Accettazione",
                new XAttribute("versioneTracciato", "0.1-provvisorio"),
                new XAttribute("codiceLocale", l.CodiceLocale),
                new XElement("Lotto",
                    Elem("CodLotto", l.CodLotto),
                    Elem("Origine", l.Origine),
                    Elem("DataOraRicezione", l.DataConferimento.ToString("yyyy-MM-ddTHH:mm")),
                    Rif("Cliente", l.IdClienteArcLab, l.Cliente),
                    Rif("Insediamento", l.IdInsediamentoArcLab, l.Insediamento),
                    Rif("Commessa", l.IdCommessaArcLab, l.Commessa),
                    Rif("Prelevatore", l.IdPrelevatore, l.Prelevatore),
                    Elem("Vettore", l.Vettore),
                    Elem("VerbalePrelievo", l.VerbalePrelievo),
                    Rif("Ricevitore", l.IdRicevitore, l.Ricevitore),
                    Elem("Note", l.Note)),
                new XElement("Campioni",
                    l.Campioni.OrderBy(c => c.Progressivo).Select(c =>
                        new XElement("Campione",
                            new XAttribute("progressivo", c.Progressivo),
                            Rif("Matrice", c.IdMatriceArcLab, c.Matrice),
                            Elem("Denominazione", c.Descrizione),
                            Elem("EtichettaCliente", c.EtichettaCliente),
                            Elem("SiglaCliente", c.SiglaCliente),
                            Elem("Identificativo", c.Identificativo),
                            Elem("Imballaggio", c.Imballaggio),
                            Elem("ProceduraPrelievo", c.ProceduraPrelievo),
                            Elem("CameraLegionella", c.CameraLegionella),
                            Elem("DataPrevistaConsegna", c.DataPrevistaConsegna?.ToString(FormatoData)),
                            Elem("RitardoAnalisiGiorni", c.RitardoAnalisiGiorni?.ToString()),
                            Elem("DataOraPrelievo", c.DataPrelievo?.ToString("yyyy-MM-ddTHH:mm")),
                            Elem("OraPrelievo", c.OraPrelievo),
                            Elem("PuntoPrelievo", c.PuntoPrelievo),
                            Elem("LuogoPrelievo", c.LuogoPrelievo),
                            Elem("TemperaturaPrelievo", c.Temperatura?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                            Elem("TempRicezione", c.TempRicezione),
                            Rif("Termometro", c.IdTermometroArcLab, c.Termometro),
                            Elem("CondizioniTrasporto", c.CondizioniTrasporto),
                            Elem("CertificatoNonRichiesto", c.CertificatoNonRichiesto ? "1" : null),
                            Rif("LaboratorioEsterno", c.IdLabEsternoArcLab, c.LabEsterno),
                            Elem("DataInvioLab", c.DataInvioLab?.ToString(FormatoData)),
                            Elem("ConsegnaPrevistaLabEstGiorni", c.ConsegnaPrevistaLabEstGiorni?.ToString()),
                            Elem("NoteLabEsterno", c.NoteLabEsterno),
                            Elem("Note", c.Note),
                            new XElement("Prove",
                                c.Prove.Select(p =>
                                    new XElement("Prova",
                                        Rif("Controllo", p.IdControlloArcLab, p.Controllo),
                                        Rif("Metodo", p.IdMetodoArcLab, p.Metodo),
                                        Elem("IdRigaMetodo", p.IdRigaMetodoArcLab?.ToString()),
                                        Elem("Parametro", p.Parametro),
                                        Elem("LaboratorioEsterno", p.LaboratorioEsterno),
                                        Elem("Note", p.Note)))))))));

        using var ms = new MemoryStream();
        using (var w = new StreamWriter(ms, new UTF8Encoding(false)))
            doc.Save(w);
        return ms.ToArray();
    }

    private static XElement? Elem(string nome, string? valore) =>
        string.IsNullOrWhiteSpace(valore) ? null : new XElement(nome, valore);

    private static XElement Rif(string nome, int? id, string? descr) =>
        new(nome, id.HasValue ? new XAttribute("id", id.Value) : null, descr ?? "");
}
