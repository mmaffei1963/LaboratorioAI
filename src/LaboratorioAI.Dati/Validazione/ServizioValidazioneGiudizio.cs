using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Validazione;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Limiti;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Validazione;

public record SchedaGiudizio(StatoCampioneArcLab? ArcLab, IReadOnlyList<GiudizioArcLab> Proposti, GiudizioDaRiportare? Validazione,
    string? ColoreLimiti, string? MotivoBlocco);

public record EsitoValidazione(bool Riuscita, string Messaggio, bool ChiedeConferma = false);

/// <summary>
/// Validazione del campione da LaboratorioAI (18/10/2026): scrive solo in scambio.GiudiziDaRiportare (database LaboratorioAI).
/// Palomar importa in ArcLab (GiudCamp + Campione.AnalisiChiusa). Lo stato «Riportato» si ricava rileggendo ArcLab.
/// </summary>
public class ServizioValidazioneGiudizio
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryGiudiziArcLab _arclab;
    private readonly ServizioSegnalazioniLimiti _segnalazioni;

    public ServizioValidazioneGiudizio(ContestoLaboratorioAI ctx, IRepositoryGiudiziArcLab arclab, ServizioSegnalazioniLimiti segnalazioni)
    { _ctx = ctx; _arclab = arclab; _segnalazioni = segnalazioni; }

    private Task<GiudizioDaRiportare?> AttivaAsync(int idCampione, CancellationToken ct) =>
        _ctx.GiudiziDaRiportare.FirstOrDefaultAsync(g => g.IdCampione == idCampione && g.Stato != StatoGiudizioDaRiportare.Annullato, ct);

    private async Task<string?> ColoreAsync(int idCampione, CancellationToken ct)
    {
        var esiti = await _segnalazioni.CalcolaAsync(false, ct);
        return esiti.TryGetValue(idCampione, out var e) ? e.Colore.ToString().ToLowerInvariant() : null;
    }

    public async Task<SchedaGiudizio> SchedaAsync(int idCampione, CancellationToken ct)
    {
        var arclab = await _arclab.StatoAsync(idCampione, ct);
        var attiva = await AttivaAsync(idCampione, ct);
        if (arclab is not null && attiva is not null && Allinea(attiva, arclab)) await _ctx.SaveChangesAsync(ct);
        var proposti = arclab?.IdMatrice is int im ? await _arclab.GiudiziMatriceAsync(im, ct) : [];
        if (!proposti.Any(p => p.IdGiudizio == RegoleGiudizio.SenzaGiudizio) && await _arclab.GiudizioAsync(RegoleGiudizio.SenzaGiudizio, ct) is { } g212)
            proposti = [.. proposti, g212];
        string? colore = null;
        try { colore = await ColoreAsync(idCampione, ct); } catch (Exception ex) when (ex is not OperationCanceledException) { }
        var motivo = arclab is null ? "Campione non trovato in ArcLab." : attiva is null ? RegoleGiudizio.MotivoBlocco(arclab) : null;
        return new SchedaGiudizio(arclab, proposti, attiva, colore, motivo);
    }

    /// <summary>Validazioni non annullate dei campioni indicati, allineate con ArcLab (per le etichette dell'elenco).</summary>
    public async Task<List<GiudizioDaRiportare>> ElencoAsync(CancellationToken ct)
    {
        var righe = await _ctx.GiudiziDaRiportare
            .Where(g => g.Stato == StatoGiudizioDaRiportare.DaRiportare || g.Stato == StatoGiudizioDaRiportare.Conflitto
                        || (g.Stato == StatoGiudizioDaRiportare.Riportato && g.DataRiportato > DateTime.UtcNow.AddDays(-2)))
            .ToListAsync(ct);
        var daControllare = righe.Where(r => r.Stato != StatoGiudizioDaRiportare.Riportato).Select(r => r.IdCampione).ToList();
        if (daControllare.Count > 0)
        {
            var stati = (await _arclab.StatiAsync(daControllare, ct)).ToDictionary(s => s.IdCampione);
            var cambiate = false;
            foreach (var r in righe)
                if (stati.TryGetValue(r.IdCampione, out var s)) cambiate |= Allinea(r, s);
            if (cambiate) await _ctx.SaveChangesAsync(ct);
        }
        return righe;
    }

    /// <summary>Aggiorna lo stato della riga da quanto c'è in ArcLab. True se è cambiato.</summary>
    private static bool Allinea(GiudizioDaRiportare r, StatoCampioneArcLab s)
    {
        if (r.Stato is StatoGiudizioDaRiportare.Annullato or StatoGiudizioDaRiportare.Riportato) return false;
        var nuovo = s.Giudizi.Count == 0 ? StatoGiudizioDaRiportare.DaRiportare
            : s.Giudizi.Any(g => g.IdGiudizio != r.IdGiudizio) ? StatoGiudizioDaRiportare.Conflitto
            : s.AnalisiChiusa ? StatoGiudizioDaRiportare.Riportato
            : StatoGiudizioDaRiportare.DaRiportare;
        if (nuovo == r.Stato) return false;
        r.Stato = nuovo;
        if (nuovo == StatoGiudizioDaRiportare.Riportato) r.DataRiportato = DateTime.UtcNow;
        return true;
    }

    public async Task<EsitoValidazione> ValidaAsync(int idCampione, int? idGiudizio, bool confermaColore, OperatoreLimiti op, CancellationToken ct)
    {
        var arclab = await _arclab.StatoAsync(idCampione, ct);
        if (arclab is null) return new(false, "Campione non trovato in ArcLab.");
        if (await AttivaAsync(idCampione, ct) is { } gia)
            return new(false, $"Campione già validato da {gia.ValidatoDa} il {gia.DataValidazione.ToLocalTime():dd/MM/yyyy HH:mm}.");
        if (RegoleGiudizio.MotivoBlocco(arclab) is string blocco) return new(false, blocco);

        var proposti = arclab.IdMatrice is int im ? await _arclab.GiudiziMatriceAsync(im, ct) : [];
        var scelta = RegoleGiudizio.Scegli(arclab, idGiudizio, proposti);
        if (scelta.Errore is not null) return new(false, scelta.Errore);
        var giudizio = proposti.FirstOrDefault(p => p.IdGiudizio == scelta.IdGiudizio) ?? await _arclab.GiudizioAsync(scelta.IdGiudizio!.Value, ct);
        if (giudizio is null) return new(false, "Giudizio non trovato in ArcLab.");

        string? colore; var coloreNonDisponibile = false;
        try { colore = await ColoreAsync(idCampione, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { colore = null; coloreNonDisponibile = true; }
        if (!confermaColore && (RegoleGiudizio.ChiedeConferma(colore) || coloreNonDisponibile))
            return new(false, coloreNonDisponibile
                ? "I colori dei limiti non sono disponibili in questo momento: confermi comunque la validazione?"
                : $"Il campione è segnalato in {colore} nei limiti normativi: confermi la validazione con il giudizio «{giudizio.CodGiudizio}»?", true);

        _ctx.GiudiziDaRiportare.Add(new GiudizioDaRiportare
        {
            IdCampione = idCampione, NumeroCampione = arclab.NumeroCampione, IdGiudizio = giudizio.IdGiudizio,
            CodGiudizio = Taglia(giudizio.CodGiudizio, 255), GiudizioTesto = Taglia(giudizio.Giudizio, 2000), Automatico = scelta.Automatico,
            ColoreLimiti = colore, ConfermaColore = confermaColore, ValidatoDa = op.Nome, IdUtenteValidazione = op.IdUtente,
            DataValidazione = DateTime.UtcNow, Stato = StatoGiudizioDaRiportare.DaRiportare
        });
        try { await _ctx.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // indice univoco: qualcun altro l'ha validato nello stesso momento
            _ctx.ChangeTracker.Clear();
            return new(false, "Il campione risulta già validato (validazione contemporanea): ricarica la pagina.");
        }
        return new(true, scelta.Automatico
            ? $"Validato con giudizio 212 «{giudizio.CodGiudizio}» (automatico). In attesa che ArcLab lo riceva."
            : $"Validato con giudizio «{giudizio.CodGiudizio}». In attesa che ArcLab lo riceva.");
    }

    public async Task<EsitoValidazione> AnnullaAsync(int idGiudizioDaRiportare, string? nota, OperatoreLimiti op, CancellationToken ct)
    {
        var r = await _ctx.GiudiziDaRiportare.FirstOrDefaultAsync(g => g.IdGiudizioDaRiportare == idGiudizioDaRiportare, ct);
        if (r is null) return new(false, "Validazione non trovata.");
        // rilegge ArcLab: se l'ha già importata non si annulla più qui
        if (await _arclab.StatoAsync(r.IdCampione, ct) is { } s && Allinea(r, s)) await _ctx.SaveChangesAsync(ct);
        if (r.Stato != StatoGiudizioDaRiportare.DaRiportare)
            return new(false, "La validazione è già arrivata in ArcLab (o c'è un giudizio diverso): la correzione si fa in ArcLab.");
        r.Stato = StatoGiudizioDaRiportare.Annullato;
        r.AnnullatoDa = op.Nome;
        r.DataAnnullamento = DateTime.UtcNow;
        r.Nota = Taglia(nota, 1000);
        await _ctx.SaveChangesAsync(ct);
        return new(true, "Validazione annullata: il campione si può validare di nuovo.");
    }

    private static string? Taglia(string? s, int max) => s is null ? null : s.Length > max ? s[..max] : s;
}
