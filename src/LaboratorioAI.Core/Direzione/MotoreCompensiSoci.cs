namespace LaboratorioAI.Core.Direzione;

/// <summary>Regole dell'anno usate dal calcolo (copiate da <c>AnnoCompensi</c>).</summary>
public sealed record ParametriCompensi(
    int Anno,
    decimal BaseTempoPieno, decimal OreTempoPieno, decimal CompensoAmministratore, decimal CompensoResponsabileArea,
    decimal AssicurazioneAuto, decimal Premio, decimal PercentualeRspp, decimal PercentualeIndennitaLaboratorio,
    decimal? BaseLaboratorioManuale);

/// <summary>Dati di un socio per l'anno e le cifre lette da AZW.</summary>
public sealed record DatiSocioCompensi(
    int IdSocio, string Nome,
    decimal OreSettimanali, int MesiAmministratore, bool ResponsabileArea, decimal PercentualeAutoPropria,
    bool QuotaRspp, bool IndennitaLaboratorio, decimal? Premio,
    /// <summary>Fatturato RSPP del socio (voce RSP, sua commessa) da AZW.</summary>
    decimal FatturatoRspp,
    /// <summary>Rimborsi spese dell'anno, già con le maggiorazioni.</summary>
    decimal Rimborsi,
    /// <summary>Recupero dall'anno prima (differenza dell'anno prima, o scritto a mano per il primo anno).</summary>
    decimal Recupero,
    /// <summary>Compensi percepiti da AZW (positivi).</summary>
    decimal PercepitoAzw,
    decimal RettificaPercepito,
    /// <summary>22/10/2026: rimborsi delle trasferte con mezzo privato (AZW, Viaggi).</summary>
    decimal RimborsiTrasferte = 0m);

/// <summary>Premio dell'area laboratorio; se <paramref name="IdSocio"/> è indicato va nella colonna di quel socio.</summary>
public sealed record PremioLaboratorio(string Nome, decimal Importo, int? IdSocio);

/// <summary>Una voce AZW che entra nella base del laboratorio.</summary>
public sealed record VoceBaseLaboratorio(string Voce, string? Descrizione, decimal ImportoAzw, decimal Percentuale)
{
    public decimal Quota => Math.Round(ImportoAzw * Percentuale / 100m, 2);
}

/// <summary>Il conto di un socio, colonna per colonna come nel foglio Excel «Compensi soci».</summary>
public sealed record ContoSocio(
    int IdSocio, string Nome,
    decimal Base, decimal Amministratore, decimal ResponsabileArea, decimal Fisso,
    decimal Rspp, decimal IndennitaLaboratorio, decimal FissoEAccessori,
    decimal Rimborsi, decimal Assicurazione, decimal Recupero, decimal Premio,
    decimal Totale, decimal PercepitoAzw, decimal RettificaPercepito, decimal Percepito, decimal Differenza,
    decimal RimborsiTrasferte = 0m);

public sealed record RisultatoCompensi(
    int Anno,
    IReadOnlyList<ContoSocio> Soci,
    IReadOnlyList<VoceBaseLaboratorio> VociLaboratorio,
    decimal BaseLaboratorioCalcolata,
    decimal BaseLaboratorioUsata,
    bool BaseLaboratorioManuale,
    decimal IndennitaLaboratorioLorda,
    decimal TotalePremiLaboratorio,
    IReadOnlyList<PremioLaboratorio> Premi)
{
    public ContoSocio Totali => new(0, "Totale",
        Soci.Sum(s => s.Base), Soci.Sum(s => s.Amministratore), Soci.Sum(s => s.ResponsabileArea), Soci.Sum(s => s.Fisso),
        Soci.Sum(s => s.Rspp), Soci.Sum(s => s.IndennitaLaboratorio), Soci.Sum(s => s.FissoEAccessori),
        Soci.Sum(s => s.Rimborsi), Soci.Sum(s => s.Assicurazione), Soci.Sum(s => s.Recupero), Soci.Sum(s => s.Premio),
        Soci.Sum(s => s.Totale), Soci.Sum(s => s.PercepitoAzw), Soci.Sum(s => s.RettificaPercepito), Soci.Sum(s => s.Percepito),
        Soci.Sum(s => s.Differenza), Soci.Sum(s => s.RimborsiTrasferte));
}

/// <summary>
/// Compensi soci (21/10/2026): stessi conti del foglio Excel «Compensi_Soci» (foglio «2026x2025»).
/// <list type="bullet">
/// <item>Base = base a tempo pieno × ore settimanali / ore del tempo pieno.</item>
/// <item>Amministratore = compenso annuo / 12 × mesi. Responsabile d'area = compenso, se lo è.</item>
/// <item>RSPP = % × fatturato RSPP del socio (AZW, voce RSP, sua commessa).</item>
/// <item>Indennità laboratorio = % × base laboratorio − tutti i premi dell'area laboratorio; un premio collegato a un socio
/// va nella colonna di quel socio.</item>
/// <item>Totale = fisso + accessori + rimborsi trasferte (AZW) + altri rimborsi + assicurazione × % auto propria + recupero + premio.
/// Differenza = totale − percepito: diventa il recupero dell'anno dopo.</item>
/// </list>
/// Definizione: claude/definizione-compensi-soci.md
/// </summary>
public static class MotoreCompensiSoci
{
    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static RisultatoCompensi Calcola(ParametriCompensi p, IReadOnlyList<DatiSocioCompensi> soci,
        IReadOnlyList<VoceBaseLaboratorio> vociLaboratorio, IReadOnlyList<PremioLaboratorio> premi)
    {
        var baseCalcolata = vociLaboratorio.Sum(v => v.Quota);
        var baseUsata = p.BaseLaboratorioManuale ?? baseCalcolata;
        var indennitaLorda = R(baseUsata * p.PercentualeIndennitaLaboratorio / 100m);
        var totalePremi = premi.Sum(x => x.Importo);

        var conti = new List<ContoSocio>();
        foreach (var s in soci)
        {
            var baseSocio = p.OreTempoPieno > 0 ? R(p.BaseTempoPieno * s.OreSettimanali / p.OreTempoPieno) : 0m;
            var amm = R(p.CompensoAmministratore / 12m * s.MesiAmministratore);
            var resp = s.ResponsabileArea ? p.CompensoResponsabileArea : 0m;
            var fisso = baseSocio + amm + resp;
            var rspp = s.QuotaRspp ? R(s.FatturatoRspp * p.PercentualeRspp / 100m) : 0m;
            var lab = (s.IndennitaLaboratorio ? indennitaLorda - totalePremi : 0m)
                      + premi.Where(x => x.IdSocio == s.IdSocio).Sum(x => x.Importo);
            var fissoAcc = fisso + rspp + lab;
            var assic = R(p.AssicurazioneAuto * s.PercentualeAutoPropria / 100m);
            var premio = s.Premio ?? p.Premio;
            var totale = fissoAcc + s.RimborsiTrasferte + s.Rimborsi + assic + s.Recupero + premio;
            var percepito = s.PercepitoAzw + s.RettificaPercepito;
            conti.Add(new ContoSocio(s.IdSocio, s.Nome, baseSocio, amm, resp, fisso, rspp, lab, fissoAcc,
                s.Rimborsi, assic, s.Recupero, premio, totale, s.PercepitoAzw, s.RettificaPercepito, percepito, totale - percepito,
                s.RimborsiTrasferte));
        }
        return new RisultatoCompensi(p.Anno, conti, vociLaboratorio, baseCalcolata, baseUsata, p.BaseLaboratorioManuale.HasValue,
            indennitaLorda, totalePremi, premi);
    }

    /// <summary>Importo di un rimborso con la maggiorazione della sua categoria (30 = +30%).</summary>
    public static decimal ImportoRimborso(decimal importo, decimal maggiorazione) => R(importo * (1m + maggiorazione / 100m));

    /// <summary>Percentuali di partenza delle voci per la base del laboratorio (decise il 21/10/2026).</summary>
    public static readonly IReadOnlyList<(string Voce, decimal Percentuale)> PercentualiPredefinite =
    [
        ("ANA", 100m), ("LEV", 100m), ("LEC", 100m), ("LEG", 100m), ("AMB", 100m),
        ("ANE", 50m), ("AND", 50m), ("TAR", 50m),
    ];
}
