using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Portale.Servizi;

/// <summary>
/// Documenti sul portale (19/11/2026): i documenti del cliente pubblicati su Palomar (tabella Entries di Marea, sola
/// lettura), con lo scarico del file e lo stato letto/non letto tenuto in LaboratorioAI (portale.LettureDocumenti).
/// Regole fisse (nel repository): niente documenti bloccati, niente certificati senza firma digitale (DataFirma).
/// </summary>
public class ServizioDocumentiPortale
{
    /// <summary>Riga per il portale.</summary>
    public record Riga(DocumentoMarea Documento, string Area, bool Certificato, bool Letto, string? Estensione);

    public record File(Stream Contenuto, string NomeFile, string TipoContenuto);

    private readonly IRepositoryDocumentiPortale _repo;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IConfiguration _config;
    private readonly ILogger<ServizioDocumentiPortale> _log;

    public ServizioDocumentiPortale(IRepositoryDocumentiPortale repo, ContestoLaboratorioAI ctx, IConfiguration config,
        ILogger<ServizioDocumentiPortale> log)
    {
        _repo = repo; _ctx = ctx; _config = config; _log = log;
    }

    public async Task<IReadOnlyList<Riga>> DocumentiAsync(int idClienteArcLab, CancellationToken ct)
    {
        var documenti = await _repo.DocumentiAsync(idClienteArcLab, ct);
        var letture = await _ctx.Set<LetturaDocumentoPortale>().AsNoTracking()
            .Where(l => l.IdClienteArcLab == idClienteArcLab)
            .ToDictionaryAsync(l => l.IdEntry, l => l.Letto, ct);
        return documenti.Select(d => new Riga(d, AreeDocumenti.Area(d.Tipo), AreeDocumenti.Certificato(d.Tipo),
            letture.TryGetValue(d.Id, out var letto) ? letto : d.DataUltimoDownload is not null,
            Path.GetExtension(d.NomeFile ?? "").TrimStart('.').ToLowerInvariant())).ToList();
    }

    /// <summary>
    /// File del documento, solo se è del cliente (null = operatore senza vincolo). Lo segna come letto per quel cliente.
    /// Restituisce un messaggio d'errore al posto del file.
    /// </summary>
    public async Task<(File? File, string? Errore)> ScaricaAsync(int idEntry, int? idClienteArcLab, CancellationToken ct)
    {
        var doc = await _repo.DocumentoAsync(idEntry, ct);
        // Inesistente o di un altro cliente: stessa risposta, per non rivelare quali documenti esistono.
        if (doc is null || doc.IdClienteArcLab is not int proprietario || (idClienteArcLab is not null && proprietario != idClienteArcLab))
            return (null, "Documento non trovato.");

        var percorso = PercorsoFile(doc);
        if (percorso is null)
        {
            _log.LogWarning("Documento {IdEntry}: percorso non ammesso ({Percorso} / {NomeFile})", idEntry, doc.Percorso, doc.NomeFile);
            return (null, "Documento non disponibile: contatta il laboratorio.");
        }
        if (!System.IO.File.Exists(percorso))
        {
            _log.LogWarning("Documento {IdEntry}: file non trovato {Percorso}", idEntry, percorso);
            return (null, "Il file del documento non è al momento disponibile: contatta il laboratorio.");
        }

        // Lo stato «letto» lo segna solo il cliente: l'operatore che controlla non cambia quello che vede il cliente.
        if (idClienteArcLab is not null) await SegnaAsync(idEntry, proprietario, true, "scarico", ct);

        var nome = NomeScarico(doc);
        var flusso = new FileStream(percorso, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return (new File(flusso, nome, TipoContenuto(nome)), null);
    }

    /// <summary>Riporta il documento a «non letto» per il cliente; false se il documento non è suo.</summary>
    public async Task<bool> SegnaNonLettoAsync(int idEntry, int idClienteArcLab, CancellationToken ct)
    {
        var doc = await _repo.DocumentoAsync(idEntry, ct);
        if (doc is null || doc.IdClienteArcLab != idClienteArcLab) return false;
        await SegnaAsync(idEntry, idClienteArcLab, false, "segnato non letto", ct);
        return true;
    }

    private async Task SegnaAsync(int idEntry, int idCliente, bool letto, string origine, CancellationToken ct)
    {
        var riga = await _ctx.Set<LetturaDocumentoPortale>().FirstOrDefaultAsync(l => l.IdClienteArcLab == idCliente && l.IdEntry == idEntry, ct);
        if (riga is null)
        {
            riga = new LetturaDocumentoPortale { IdEntry = idEntry, IdClienteArcLab = idCliente };
            _ctx.Add(riga);
        }
        riga.Letto = letto;
        riga.Origine = origine;
        riga.DataAggiornamentoUtc = DateTime.UtcNow;
        try { await _ctx.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) { _log.LogWarning(ex, "Stato lettura del documento {IdEntry} non salvato", idEntry); }
    }

    /// <summary>
    /// Percorso del file: Percorso (cartella, o già completo del nome) + NomeFile. Ammesso solo sotto le radici di
    /// Palomar (Portale:Documenti:RadiciAmmesse, predefinite D:\web\, C:\web\, D:\marea\) e senza «..». Con
    /// Portale:Documenti:SostituisciDa / SostituisciCon la radice si può tradurre (es. D:\web\ → \\SRVMAREA\web\ in
    /// sviluppo).
    /// </summary>
    private string? PercorsoFile(DocumentoMarea d)
    {
        var cartella = (d.Percorso ?? "").Trim();
        var nome = (d.NomeFile ?? "").Trim();
        if (cartella.Length == 0) return null;
        var completo = nome.Length == 0 || cartella.EndsWith(nome, StringComparison.OrdinalIgnoreCase)
            ? cartella
            : cartella.TrimEnd('\\', '/') + "\\" + nome;
        completo = completo.Replace('/', '\\');
        if (completo.Contains("..")) return null;

        var radici = _config.GetSection("Portale:Documenti:RadiciAmmesse").Get<string[]>() ?? [@"D:\web\", @"C:\web\", @"D:\marea\"];
        if (!radici.Any(r => completo.StartsWith(r, StringComparison.OrdinalIgnoreCase))) return null;

        var da = _config["Portale:Documenti:SostituisciDa"];
        var con = _config["Portale:Documenti:SostituisciCon"];
        if (!string.IsNullOrWhiteSpace(da) && con is not null && completo.StartsWith(da, StringComparison.OrdinalIgnoreCase))
            completo = con + completo[da.Length..];
        return completo;
    }

    private static string NomeScarico(DocumentoMarea d)
    {
        var nome = Path.GetFileName((d.NomeFile ?? "").Trim());
        if (nome.Length == 0) nome = Path.GetFileName((d.Percorso ?? "").Trim().Replace('/', '\\').Split('\\').LastOrDefault() ?? "");
        return nome.Length == 0 ? $"documento_{d.Id}" : nome;
    }

    private static string TipoContenuto(string nome) => Path.GetExtension(nome).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".p7m" => "application/pkcs7-mime",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}
