using System.Security.Cryptography;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>Un documento gia' prodotto e archiviato.</summary>
public sealed record DocumentoArchiviato(
    int Id, string CodiceModello, string NomeFile, string Formato,
    DateTime GeneratoIlUtc, string? GeneratoDa, bool FileCiSta);

/// <summary>
/// L'archivio dei documenti prodotti (11/11/2026).
///
/// Serve a rispondere, anni dopo, a tre domande: che cosa è stato
/// consegnato a questo cliente, con quale revisione del modello, e chi
/// lo ha prodotto. Il file resta sul disco; qui si tengono percorso,
/// impronta e il legame con la revisione usata.
///
/// Niente si cancella: un documento sbagliato si rigenera e resta anche
/// quello vecchio, perché è quello che il cliente ha in mano.
/// </summary>
public class ServizioArchivioManualistica
{
    private readonly ContestoLaboratorioAI _db;
    private readonly OpzioniManualistica _opzioni;

    public ServizioArchivioManualistica(
        ContestoLaboratorioAI db, IOptions<OpzioniManualistica> opzioni)
    {
        _db = db;
        _opzioni = opzioni.Value;
    }

    /// <summary>Vero se è stata configurata una cartella di archivio.</summary>
    public bool Configurato => !string.IsNullOrWhiteSpace(_opzioni.CartellaArchivio);

    /// <summary>I documenti prodotti per una check list, dal piu' recente.</summary>
    public async Task<IReadOnlyList<DocumentoArchiviato>> ElencoAsync(
        int idCompilazione, CancellationToken ct = default)
    {
        var righe = await _db.ManualiGenerati.AsNoTracking()
            .Where(m => m.IdCompilazione == idCompilazione)
            .OrderByDescending(m => m.GeneratoIlUtc)
            .ToListAsync(ct);

        return righe.Select(m => new DocumentoArchiviato(
            m.IdManualeGenerato, m.CodiceModello, m.NomeFile, m.Formato.ToString(),
            m.GeneratoIlUtc, m.GeneratoDa,
            !string.IsNullOrWhiteSpace(m.PercorsoArchivio) && File.Exists(m.PercorsoArchivio))).ToList();
    }

    /// <summary>
    /// Archivia un documento appena prodotto.
    /// </summary>
    /// <param name="compilazione">La check list da cui viene.</param>
    /// <param name="codiceModello">Il modello usato.</param>
    /// <param name="idRevisioneModello">La revisione approvata usata.</param>
    /// <param name="nomeFile">Nome del file prodotto.</param>
    /// <param name="contenuto">Il file.</param>
    /// <param name="formato">Word o PDF.</param>
    /// <param name="upn">Chi lo ha prodotto.</param>
    /// <returns>Null se l'archivio non è configurato: il documento si scarica lo stesso.</returns>
    public async Task<int?> ArchiviaAsync(
        Compilazione compilazione, string codiceModello, int? idRevisioneModello,
        string nomeFile, byte[] contenuto, FormatoManualeGenerato formato,
        string? upn, CancellationToken ct = default)
    {
        if (!Configurato) return null;

        var cartella = Path.Combine(_opzioni.CartellaArchivio, CartellaDi(compilazione));
        Directory.CreateDirectory(cartella);

        // Il nome porta l'ora: rigenerando lo stesso documento il
        // precedente non si sovrascrive, perché potrebbe essere quello
        // che il cliente ha già in mano.
        var marca = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var nome = $"{marca} {Pulisci(nomeFile)}";
        var percorso = Path.Combine(cartella, nome);

        await File.WriteAllBytesAsync(percorso, contenuto, ct);

        var documento = new ManualeGenerato
        {
            IdCompilazione = compilazione.IdCompilazione,
            CodiceModello = codiceModello,
            NomeFile = nomeFile,
            Formato = formato,
            PercorsoArchivio = percorso,
            ImprontaSha256 = Convert.ToHexString(SHA256.HashData(contenuto)),
            GeneratoIlUtc = DateTime.UtcNow,
            GeneratoDa = upn
        };

        if (idRevisioneModello is not null)
            documento.Revisioni.Add(new ManualeGeneratoRevisione
            {
                IdRevisioneModello = idRevisioneModello.Value
            });

        _db.ManualiGenerati.Add(documento);
        await _db.SaveChangesAsync(ct);

        return documento.IdManualeGenerato;
    }

    /// <summary>Rilegge un documento archiviato.</summary>
    public async Task<(string NomeFile, byte[] Contenuto)?> LeggiAsync(
        int idCompilazione, int idDocumento, CancellationToken ct = default)
    {
        var documento = await _db.ManualiGenerati.AsNoTracking().FirstOrDefaultAsync(
            m => m.IdManualeGenerato == idDocumento && m.IdCompilazione == idCompilazione, ct);

        if (documento?.PercorsoArchivio is null || !File.Exists(documento.PercorsoArchivio))
            return null;

        return (documento.NomeFile, await File.ReadAllBytesAsync(documento.PercorsoArchivio, ct));
    }

    /// <summary>«12_PARCO LE NAVI»: l'identificativo davanti perché due clienti possono chiamarsi uguale.</summary>
    private static string CartellaDi(Compilazione c) =>
        Pulisci($"{c.IdCompilazione}_{c.RagioneSociale}");

    private static string Pulisci(string nome)
    {
        foreach (var vietato in Path.GetInvalidFileNameChars()) nome = nome.Replace(vietato, ' ');
        return nome.Trim();
    }
}
