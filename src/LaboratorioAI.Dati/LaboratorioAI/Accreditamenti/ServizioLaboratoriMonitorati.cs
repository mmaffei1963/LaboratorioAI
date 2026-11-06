using System.Text.RegularExpressions;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.Accreditamenti;

/// <summary>Dati di un laboratorio monitorato inviati dalla maschera (nuovo se <see cref="IdAccreditamento"/> è nullo).</summary>
public sealed record DatiLaboratorioMonitorato(
    int? IdAccreditamento, int IdLabEsternoArcLab, string? NumeroAccreditamento, string? Sede, string? Note, bool Attivo);

/// <summary>
/// Gestione dei laboratori esterni monitorati su ACCREDIA (08/10/2026, proposta 5 del documento sui subappalti):
/// aggiungere un laboratorio, correggere numero/sede/note, disattivarlo o riattivarlo. Scrive solo sul database di
/// LabAI (<c>labesterni.Accreditamenti</c>); il laboratorio si sceglie fra quelli di ArcLab (sola lettura).
/// Regole:
/// - numero = 5 cifre (numerazione ACCREDIA 2025, «253» diventa «00253»); sede = una lettera (A, B, …);
/// - nessuna cancellazione: «disattivo» = non più controllato, lo storico delle revisioni resta;
/// - se cambiano numero o sede, è un altro elenco prove: la revisione confermata si azzera e va riconfermata
///   (le revisioni già trovate restano nello storico).
/// I laboratori iniziali restano caricati all'avvio da PopolamentoAccreditamentiEsterni, che aggiunge solo quelli
/// mancanti e non tocca quelli esistenti: le modifiche fatte qui non vengono sovrascritte.
/// </summary>
public sealed class ServizioLaboratoriMonitorati
{
    private readonly ContestoLaboratorioAI _db;
    private readonly IRepositoryMetodiLabEsterno _arcLab;

    public ServizioLaboratoriMonitorati(ContestoLaboratorioAI db, IRepositoryMetodiLabEsterno arcLab)
    {
        _db = db;
        _arcLab = arcLab;
    }

    /// <summary>Tutti i laboratori monitorati, anche disattivati (attivi prima).</summary>
    public async Task<IReadOnlyList<AccreditamentoLabEsterno>> TuttiAsync(CancellationToken ct = default) =>
        await _db.Set<AccreditamentoLabEsterno>().AsNoTracking()
            .OrderByDescending(a => a.Attivo).ThenBy(a => a.NomeLaboratorio).ToListAsync(ct);

    /// <summary>Salva un laboratorio. Restituisce (laboratorio salvato, null) oppure (null, messaggio d'errore).</summary>
    public async Task<(AccreditamentoLabEsterno? Laboratorio, string? Errore)> SalvaAsync(
        DatiLaboratorioMonitorato d, int? idUtente, CancellationToken ct = default)
    {
        var cifre = Regex.Replace(d.NumeroAccreditamento ?? "", @"\D", "");
        if (cifre.Length is 0 or > 5) return (null, "Numero di accreditamento non valido: servono fino a 5 cifre (es. 00253).");
        var numero = cifre.PadLeft(5, '0');
        var sede = (d.Sede ?? "").Trim().ToUpperInvariant();
        if (!Regex.IsMatch(sede, "^[A-Z]$")) return (null, "Sede non valida: una lettera (es. A).");
        var note = string.IsNullOrWhiteSpace(d.Note) ? null : d.Note.Trim();
        if (note?.Length > 500) return (null, "Note troppo lunghe (massimo 500 caratteri).");

        var set = _db.Set<AccreditamentoLabEsterno>();
        AccreditamentoLabEsterno? lab = null;
        if (d.IdAccreditamento is int id)
        {
            lab = await set.FirstOrDefaultAsync(a => a.IdAccreditamento == id, ct);
            if (lab is null) return (null, "Laboratorio non trovato.");
        }

        var idArcLab = lab?.IdLabEsternoArcLab ?? d.IdLabEsternoArcLab;
        var idCorrente = lab?.IdAccreditamento ?? 0;
        if (await set.AnyAsync(a => a.IdLabEsternoArcLab == idArcLab && a.IdAccreditamento != idCorrente, ct))
            return (null, "Questo laboratorio di ArcLab è già nell'elenco (eventualmente disattivato): modificalo invece di aggiungerlo.");
        if (await set.AnyAsync(a => a.NumeroAccreditamento == numero && a.Sede == sede
                                    && a.IdAccreditamento != idCorrente, ct))
            return (null, $"Il numero {numero}/{sede} è già usato da un altro laboratorio dell'elenco.");

        var adesso = DateTime.UtcNow;
        if (lab is null)
        {
            var arcLab = (await _arcLab.LaboratoriAsync(new[] { idArcLab }, ct)).FirstOrDefault();
            if (arcLab is null) return (null, "Laboratorio non trovato in ArcLab.");
            lab = new AccreditamentoLabEsterno
            {
                IdLabEsternoArcLab = idArcLab,
                NomeLaboratorio = Tronca(arcLab.Nome, 200) ?? $"Laboratorio {idArcLab}",
                DataCreazione = adesso
            };
            set.Add(lab);
        }
        else if (lab.NumeroAccreditamento != numero || lab.Sede != sede)
        {
            // Altro numero/sede = altro elenco prove: la revisione confermata non vale più.
            lab.UltimaRevisioneConfermata = null;
        }

        lab.NumeroAccreditamento = numero;
        lab.Sede = sede;
        lab.Note = note;
        if (lab.Attivo != d.Attivo || lab.IdAccreditamento == 0)
        {
            lab.Attivo = d.Attivo;
            lab.DataCancellazione = d.Attivo ? null : adesso;
        }
        lab.DataUltimaModifica = adesso;
        lab.IdUtente = idUtente;
        await _db.SaveChangesAsync(ct);
        return (lab, null);
    }

    private static string? Tronca(string? s, int n) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length <= n ? s.Trim() : s.Trim()[..n];
}
