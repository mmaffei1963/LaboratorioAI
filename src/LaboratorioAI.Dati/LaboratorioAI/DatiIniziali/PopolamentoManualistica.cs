using LaboratorioAI.Core.Entita;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.DatiIniziali;

/// <summary>
/// Popolamento iniziale del modulo Manualistica (01/11/2026).
///
/// Crea il tipo di manuale PRIVACY, le qualifiche degli autorizzati, le
/// funzioni aziendali e l'elenco dei documenti del pacchetto. Non carica
/// i modelli Word: quelli entrano come revisioni, dalla pagina dei
/// modelli, e vanno approvati da due persone prima dell'uso.
///
/// Eseguito all'avvio, idempotente: aggiunge solo ciò che manca e non
/// tocca le righe già presenti, che un operatore potrebbe aver corretto.
///
/// Le condizioni di inclusione seguono il motore della versione di prova
/// (vedi docs\Privacy\INVENTARIO_Motore_Manualistica.md):
/// null = il documento esce sempre; «DA_CHECKLIST» = esce nel numero di
/// copie indicato nella check list; più condizioni separate da virgola
/// valgono tutte insieme; «!» nega.
/// </summary>
public static class PopolamentoManualistica
{
    public const string CodicePrivacy = "PRIVACY";

    public static async Task EseguiAsync(ContestoLaboratorioAI ctx, CancellationToken ct = default)
    {
        await PopolaQualificheAsync(ctx, ct);

        var tipo = await ctx.TipiManuale.FirstOrDefaultAsync(t => t.Codice == CodicePrivacy, ct);
        if (tipo is null)
        {
            tipo = new TipoManuale { Codice = CodicePrivacy, Nome = "Documentazione privacy", Ordine = 1 };
            ctx.TipiManuale.Add(tipo);
            await ctx.SaveChangesAsync(ct);
        }

        await PopolaFunzioniAsync(ctx, tipo.IdTipoManuale, ct);
        await PopolaModelliAsync(ctx, tipo.IdTipoManuale, ct);
        await ctx.SaveChangesAsync(ct);
    }

    // ── Qualifiche ─────────────────────────────────────────────────
    // Il nominativo si chiede solo a chi lo richiede: per i Lavoratori
    // non si chiede e non si salva, si scrive a mano sul documento
    // stampato (decisione del 22/10/2026).

    private static async Task PopolaQualificheAsync(ContestoLaboratorioAI ctx, CancellationToken ct)
    {
        var esistenti = await ctx.Qualifiche.Select(q => q.Nome).ToListAsync(ct);

        var catalogo = new (string Nome, bool RichiedeNominativo, int Ordine)[]
        {
            ("Legale Rappresentante",  true,  1),
            ("Socio",                  true,  2),
            ("Coadiuvante familiare",  true,  3),
            ("Lavoratore",             false, 4)
        };

        foreach (var (nome, nominativo, ordine) in catalogo)
        {
            if (esistenti.Contains(nome)) continue;
            ctx.Qualifiche.Add(new QualificaAutorizzato
            {
                Nome = nome,
                RichiedeNominativo = nominativo,
                Ordine = ordine
            });
        }
    }

    // ── Funzioni aziendali ─────────────────────────────────────────
    // L'ordine è quello con cui escono le lettere di nomina.
    // Amministrazione e Front Office sono sempre presenti; le altre
    // compaiono solo se hanno almeno un autorizzato.

    private static async Task PopolaFunzioniAsync(ContestoLaboratorioAI ctx, int idTipo, CancellationToken ct)
    {
        var esistenti = await ctx.FunzioniAziendali
            .Where(f => f.IdTipoManuale == idTipo)
            .Select(f => f.Codice)
            .ToListAsync(ct);

        var catalogo = new (string Codice, string Nome, bool Facoltativa)[]
        {
            ("AMMINISTRAZIONE",    "Amministrazione",    false),
            ("FRONT_OFFICE",       "Front Office",       false),
            ("CAPO_CHEF",          "Capo chef",          true),
            ("GUARDIANO_NOTTURNO", "Guardiano notturno", true),
            ("CAMERIERA_AI_PIANI", "Cameriera ai piani", true),
            ("CAMERIERE_DI_SALA",  "Cameriere di sala",  true),
            ("CAPO_SALA",          "Capo sala",          true)
        };

        var ordine = 1;
        foreach (var (codice, nome, facoltativa) in catalogo)
        {
            if (!esistenti.Contains(codice))
            {
                ctx.FunzioniAziendali.Add(new FunzioneAziendale
                {
                    IdTipoManuale = idTipo,
                    Codice = codice,
                    Nome = nome,
                    Facoltativa = facoltativa,
                    Ordine = ordine
                });
            }
            ordine++;
        }
    }

    // ── Documenti del pacchetto ────────────────────────────────────

    private static async Task PopolaModelliAsync(ContestoLaboratorioAI ctx, int idTipo, CancellationToken ct)
    {
        var esistenti = await ctx.ModelliDocumento
            .Where(m => m.IdTipoManuale == idTipo)
            .Select(m => m.Codice)
            .ToListAsync(ct);

        // I modelli di servizio non hanno marcatori: vanno riconosciuti come tali,
        // altrimenti il controllo li segnala come file rovinati (04/11/2026).
        var diServizio = Catalogo()
            .Where(x => x.Condizione == NonNelPacchetto)
            .Select(x => x.Codice)
            .ToHashSet(StringComparer.Ordinal);

        var ordine = 1;
        foreach (var (codice, nome, condizione) in Catalogo())
        {
            if (!esistenti.Contains(codice))
            {
                ctx.ModelliDocumento.Add(new ModelloDocumento
                {
                    IdTipoManuale = idTipo,
                    Codice = codice,
                    Nome = nome,
                    CondizioneInclusione = condizione,
                    Ordine = ordine,
                    RichiedeMarcatori = !diServizio.Contains(codice)
                });
            }
            ordine++;
        }

        // Correzione delle righe gia' presenti: il campo e' nato dopo di loro.
        var daCorreggere = await ctx.ModelliDocumento
            .Where(m => m.IdTipoManuale == idTipo && m.RichiedeMarcatori && diServizio.Contains(m.Codice))
            .ToListAsync(ct);
        foreach (var m in daCorreggere) m.RichiedeMarcatori = false;
    }

    /// <summary>
    /// I documenti della documentazione privacy, nell'ordine in cui escono
    /// nel pacchetto, più i modelli di servizio.
    ///
    /// Condizione <c>NON_NEL_PACCHETTO</c>: modello controllato e versionato
    /// come gli altri, ma che non è un documento del pacchetto; lo usa il
    /// motore per conto suo (03/11/2026).
    /// </summary>
    /// <summary>Modello controllato che non e' un documento del pacchetto.</summary>
    private const string NonNelPacchetto = "NON_NEL_PACCHETTO";

    private static IEnumerable<(string Codice, string Nome, string? Condizione)> Catalogo() =>
    [
        // Sempre presenti
        ("REGISTRO",               "Registro delle attività di trattamento",       null),
        ("NOMINE",                 "Autorizzati al trattamento – Nomine",          null),
        ("FORMAZIONE",             "Autorizzati al trattamento – Formazione",      null),
        ("DISCIPLINARE",           "Procedure di gestione – Disciplinare",         null),
        ("INFORMATIVA_CLIENTI",    "Informativa clienti",                          null),
        ("INFORMATIVA_LAVORATORI", "Informativa lavoratori",                       null),
        ("MODULO_CONSENSI",        "Modulo consensi",                              null),
        ("INFORMATIVA_CV",         "Informativa curriculum vitae",                 null),
        ("RESP_BACKUP_CREDENZIALI","Responsabile backup e credenziali",             null),
        ("PROC_DATA_BREACH",       "Procedura violazioni (data breach)",           null),
        ("ISTRUZIONI",             "Istruzioni documentazione",                    null),

        // A condizione
        ("DPIA_VIDEO",             "DPIA videosorveglianza",                       "VIDEO_O_PISCINA"),
        ("CARTELLO_VIDEO",         "Cartello videosorveglianza",                   "VIDEO_PRESENTE"),
        ("CARTELLO_PISCINA",       "Cartello telecamera piscina",                  "PISCINA_PRESENTE"),
        ("DISP_CAMERIERE",         "Disposizioni interne cameriere ai piani",      "PIANI=INTERNE_NON_NOMINATE"),
        ("DISP_MANUTENTORE",       "Disposizioni interne manutentore",             "MANUTENTORI_INTERNI"),
        // Il motore lo include solo se è stata chiesta almeno una copia (pacchetto(), 04/11/2026).
        ("IMPEGNO_DITTA_PIANI",    "Impegno di riservatezza ditta servizi ai piani","PIANI=DITTA_ESTERNA,DA_CHECKLIST"),

        // Lettere di nomina a Responsabile esterno: una o più per fornitore scelto.
        // Con il contratto di fornitura la nomina di MARE.A è dentro il contratto,
        // quindi la lettera non si genera (decisione del 29/10/2026).
        ("RESP_CONSULENTE_MAREA",  "Responsabile del trattamento – Consulente MARE.A", "DA_CHECKLIST,!CONTRATTO_FORNITURA"),
        ("RESP_CONSULENTE",        "Responsabile del trattamento – Consulente",        "DA_CHECKLIST"),
        ("RESP_COMMERCIALISTA",    "Responsabile del trattamento – Commercialista",    "DA_CHECKLIST"),
        ("RESP_PAGHE",             "Responsabile del trattamento – Paghe",             "DA_CHECKLIST"),
        ("RESP_PAGHE_E_COMMERCIALISTA", "Responsabile del trattamento – Paghe e commercialista", "DA_CHECKLIST"),
        ("RESP_WEB_AGENCY",        "Responsabile del trattamento – Web agency",        "DA_CHECKLIST"),
        ("RESP_SOFTWARE_HOUSE",    "Responsabile del trattamento – Software house",    "DA_CHECKLIST"),
        ("RESP_TECNICO_COMPUTER",  "Responsabile del trattamento – Tecnico computer",  "DA_CHECKLIST"),
        ("RESP_CHIAVI_ELETTRONICHE","Responsabile del trattamento – Chiavi elettroniche","DA_CHECKLIST"),
        ("RESP_IMPIANTO_VIDEOSORVEGLIANZA", "Responsabile del trattamento – Impianto videosorveglianza", "DA_CHECKLIST"),
        ("RESP_STABILIMENTO_BALNEARE", "Responsabile del trattamento – Stabilimento balneare", "DA_CHECKLIST"),
        ("RESP_EXPERIENCE",        "Responsabile del trattamento – Experience",        "DA_CHECKLIST"),
        ("RESP_DITTA_ESTERNA_SERVIZI_AI_PIANI", "Responsabile del trattamento – Ditta esterna servizi ai piani", "DA_CHECKLIST"),

        // Contratto di fornitura servizi
        ("CONTRATTO",              "Contratto di fornitura servizi",               "CONTRATTO_FORNITURA"),

        // Modelli di servizio: controllati e versionati, ma non consegnati come documento.
        ("CARTA_RIEPILOGO",        "Carta intestata del riepilogo",                NonNelPacchetto)
    ];
}
