using System.Globalization;
using System.Text;
using System.Xml.Linq;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Genera il tracciato XML di una proposta di controllo, per l'import supervisionato in ArcLab
/// (Controllo + rigametodo). Formato PROVVISORIO, da allineare alla routine Palomar.
/// L'IdControllo lo assegna l'import; il proponente definisce solo nome e metodi.
/// </summary>
public interface IGeneratoreTracciatoControllo
{
    byte[] GeneraXml(PropostaControllo proposta);
    string NomeFile(PropostaControllo proposta);
}

/// <inheritdoc />
public class GeneratoreTracciatoControllo : IGeneratoreTracciatoControllo
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string NomeFile(PropostaControllo p) =>
        $"PropostaControllo_{p.DataCreazione:yyyyMMdd}_{p.CodiceLocale[..8]}.xml";

    public byte[] GeneraXml(PropostaControllo p)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("PropostaControllo",
                new XAttribute("versioneTracciato", "0.1-provvisorio"),
                new XAttribute("codiceLocale", p.CodiceLocale),
                Elem("Origine", p.Origine),
                // Se valorizzato è la MODIFICA di un controllo ArcLab esistente, non un nuovo controllo.
                Elem("IdControlloEsistente", p.IdControlloEsistenteArcLab?.ToString()),
                Elem("Controllo", p.Controllo),
                Elem("Note", p.Note),
                new XElement("RigheMetodo",
                    p.Righe.OrderBy(r => r.Progressivo).Select(r =>
                        new XElement("RigaMetodo",
                            new XAttribute("progressivo", r.Progressivo),
                            new XElement("Metodo", new XAttribute("id", r.IdMetodoArcLab), r.Metodo ?? ""),
                            Elem("DenominazioneRiga", r.DenominazioneRiga),
                            Elem("RifBiblio", r.RifBiblio),
                            Rif("Unita", r.IdUnitaArcLab, r.UnitaMisura),
                            Rif("LaboratorioEsterno", r.IdLabEsternoArcLab, r.LabEsterno),
                            Elem("CostoMetodoEuro", r.CostoMetodoEuro?.ToString(Inv)),
                            Elem("ScontoRiga", r.ScontoRiga.ToString(Inv)),
                            Elem("CifreSignificative", r.CifreSignificative?.ToString()),
                            Elem("MaxDecimali", r.MaxDecimali?.ToString()))))));

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
