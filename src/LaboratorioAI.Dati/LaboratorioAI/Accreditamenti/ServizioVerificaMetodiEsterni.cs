using System.Text.RegularExpressions;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.Accreditamenti;

/// <inheritdoc cref="IServizioVerificaMetodiEsterni" />
public class ServizioVerificaMetodiEsterni : IServizioVerificaMetodiEsterni
{
    private readonly ContestoLaboratorioAI _db;
    private readonly IRepositoryMetodiLabEsterno _metodiArcLab;

    public ServizioVerificaMetodiEsterni(ContestoLaboratorioAI db, IRepositoryMetodiLabEsterno metodiArcLab)
    {
        _db = db;
        _metodiArcLab = metodiArcLab;
    }

    public async Task<IReadOnlyList<MetodoDaVerificare>> ElencoAsync(int idAccreditamento, CancellationToken ct = default)
    {
        var lab = await _db.Set<AccreditamentoLabEsterno>().FirstOrDefaultAsync(a => a.IdAccreditamento == idAccreditamento, ct)
            ?? throw new InvalidOperationException($"Laboratorio {idAccreditamento} non trovato.");

        var metodiArcLab = await _metodiArcLab.MetodiAffidatiAsync(lab.IdLabEsternoArcLab, ct);

        // Revisione attualmente confermata per questo laboratorio: senza una revisione confermata
        // non c'è un elenco prove ufficiale da usare come riferimento, né testo da cui suggerire.
        RevisioneAccreditamentoEsterno? revisioneConfermata = null;
        if (lab.UltimaRevisioneConfermata is not null)
        {
            revisioneConfermata = await _db.Set<RevisioneAccreditamentoEsterno>()
                .FirstOrDefaultAsync(r => r.IdAccreditamento == idAccreditamento
                                        && r.NumeroRevisione == lab.UltimaRevisioneConfermata, ct);
        }

        var testoNormalizzato = Normalizza(revisioneConfermata?.TestoEstratto);

        var verifiche = await _db.Set<VerificaMetodoAccreditamento>()
            .Where(v => v.IdAccreditamento == idAccreditamento)
            .ToDictionaryAsync(v => v.IdLabEstMetodoArcLab, ct);

        var risultato = new List<MetodoDaVerificare>();

        foreach (var m in metodiArcLab)
        {
            verifiche.TryGetValue(m.IdLabEstMetodo, out var verifica);

            // La classificazione vale solo se fatta sulla revisione attualmente confermata:
            // se il PDF è cambiato da allora, la riga torna "da verificare".
            var aggiornata = verifica is not null
                && revisioneConfermata is not null
                && verifica.IdRevisioneVerificata == revisioneConfermata.IdRevisione;

            var (suggerimento, punteggio) = aggiornata
                ? (null, (double?)null)
                : Suggerisci(m.RiferimentoNormativo, m.NomeParametro, testoNormalizzato);

            risultato.Add(new MetodoDaVerificare(
                m.IdLabEstMetodo, m.IdMetodo, m.CodMetodo, m.RiferimentoNormativo, m.NomeParametro,
                m.AccreditatoArcLab,
                aggiornata ? verifica!.Stato : StatoVerificaMetodo.DaVerificare,
                aggiornata ? verifica!.Note : null,
                suggerimento, punteggio));
        }

        return risultato
            .OrderBy(m => m.Stato == StatoVerificaMetodo.DaVerificare ? 0 : 1)
            .ThenByDescending(m => m.PunteggioSuggerimento ?? 0)
            .ThenBy(m => m.NomeParametro)
            .ToList();
    }

    public async Task SalvaVerificaAsync(
        int idAccreditamento, int idLabEstMetodo, StatoVerificaMetodo stato,
        int idUtente, string? note, CancellationToken ct = default)
    {
        var lab = await _db.Set<AccreditamentoLabEsterno>().FirstOrDefaultAsync(a => a.IdAccreditamento == idAccreditamento, ct)
            ?? throw new InvalidOperationException($"Laboratorio {idAccreditamento} non trovato.");

        if (lab.UltimaRevisioneConfermata is null)
            throw new InvalidOperationException(
                "Nessuna revisione dell'elenco prove confermata per questo laboratorio: confermare prima la revisione ACCREDIA.");

        var revisioneConfermata = await _db.Set<RevisioneAccreditamentoEsterno>()
            .FirstOrDefaultAsync(r => r.IdAccreditamento == idAccreditamento
                                    && r.NumeroRevisione == lab.UltimaRevisioneConfermata, ct)
            ?? throw new InvalidOperationException("Revisione confermata non trovata.");

        var verifica = await _db.Set<VerificaMetodoAccreditamento>()
            .FirstOrDefaultAsync(v => v.IdAccreditamento == idAccreditamento && v.IdLabEstMetodoArcLab == idLabEstMetodo, ct);

        if (verifica is null)
        {
            verifica = new VerificaMetodoAccreditamento
            {
                IdAccreditamento = idAccreditamento,
                IdLabEstMetodoArcLab = idLabEstMetodo,
                DataCreazione = DateTime.UtcNow
            };
            _db.Set<VerificaMetodoAccreditamento>().Add(verifica);
        }

        verifica.Stato = stato;
        verifica.Note = note;
        verifica.IdRevisioneVerificata = revisioneConfermata.IdRevisione;
        verifica.DataVerifica = DateTime.UtcNow;
        verifica.IdUtenteVerifica = idUtente;
        verifica.DataUltimaModifica = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Righe ripulite (spazi collassati, righe vuote scartate), mantenendo il testo originale —
    /// mai una fonte di verità: serve solo a mostrare la riga più simile come confronto.
    /// </summary>
    private static string[]? Normalizza(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;

        var righe = testo
            .Split('\n')
            .Select(r => Regex.Replace(r.Trim(), @"\s+", " "))
            .Where(r => r.Length > 0)
            .ToArray();

        return righe.Length > 0 ? righe : null;
    }

    /// <summary>
    /// Suggerimento per l'operatore: fra le righe del PDF, quella con più "parole" (lettere/cifre,
    /// almeno 2 caratteri) del riferimento normativo in comune. Non decide nulla — mostra la riga
    /// intera dell'elenco ACCREDIA per un confronto diretto, perché la formattazione dei riferimenti
    /// nel PDF varia da laboratorio a laboratorio e un confronto esatto non è affidabile.
    /// </summary>
    private static (string? Estratto, double? Punteggio) Suggerisci(string? riferimentoNormativo, string? nomeParametro, string[]? righePdf)
    {
        if (string.IsNullOrWhiteSpace(riferimentoNormativo) || righePdf is null)
            return (null, null);

        var token = Token(riferimentoNormativo);
        if (token.Count == 0)
            return (null, null);

        // Nome del parametro (Metodo.DettagliMetodo, come nel Rapporto di Prova): pesa meno del riferimento
        // normativo ma aiuta a scegliere la riga giusta quando lo stesso metodo compare per più parametri.
        var tokenParametro = Token(nomeParametro).Where(t => t.Length >= 3).ToList();

        string? migliorRiga = null;
        var migliorPunteggio = 0.0;

        foreach (var riga in righePdf)
        {
            var rigaMaiuscola = riga.ToUpperInvariant();
            var trovati = token.Count(t => rigaMaiuscola.Contains(t, StringComparison.Ordinal));
            var punteggio = (double)trovati / token.Count;
            if (tokenParametro.Count > 0 && trovati > 0)
            {
                var trovatiPar = tokenParametro.Count(t => rigaMaiuscola.Contains(t, StringComparison.Ordinal));
                punteggio = 0.75 * punteggio + 0.25 * trovatiPar / tokenParametro.Count;
            }

            if (punteggio > migliorPunteggio)
            {
                migliorPunteggio = punteggio;
                migliorRiga = riga;
            }
        }

        return migliorPunteggio > 0 ? (migliorRiga, migliorPunteggio) : (null, 0);
    }

    private static List<string> Token(string? testo) =>
        string.IsNullOrWhiteSpace(testo) ? new List<string>()
        : Regex.Matches(testo.ToUpperInvariant(), "[A-Z0-9]+")
            .Select(m => m.Value)
            .Where(t => t.Length >= 2)
            .Distinct()
            .ToList();
}
