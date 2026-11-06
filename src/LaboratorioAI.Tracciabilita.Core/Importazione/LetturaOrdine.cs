using System.Globalization;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Core.Importazione;

/// <summary>Un controllo fatto in accettazione, tradotto dalle colonne di Ordine.</summary>
public sealed record ControlloPronto(TipoControllo Tipo, bool? Conforme, string? Valore);

/// <summary>Traduce i testi scritti a mano nella tabella Ordine in valori utilizzabili.</summary>
public static class LetturaOrdine
{
    private static readonly CultureInfo It = new("it-IT");

    private static readonly string[] FormatiMese = { "MM/yyyy", "M/yyyy", "MM/yy", "M/yy" };
    private static readonly string[] FormatiGiorno =
        { "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy", "ddMMyy", "ddMMyyyy", "dd-MM-yyyy", "dd.MM.yyyy" };

    /// <summary>
    /// Data di scadenza scritta come testo: «26/09/2022», «07/2022» (vale l'ultimo giorno del mese), «301223».
    /// Se non si capisce restituisce null: meglio nessuna data che una data inventata.
    /// </summary>
    public static DateOnly? Data(string? testo)
    {
        var t = testo?.Trim();
        if (string.IsNullOrEmpty(t)) return null;

        if (DateTime.TryParseExact(t, FormatiMese, It, DateTimeStyles.None, out var m) && Plausibile(m.Year))
            return new DateOnly(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month));
        if (DateTime.TryParseExact(t, FormatiGiorno, It, DateTimeStyles.None, out var g) && Plausibile(g.Year))
            return DateOnly.FromDateTime(g);
        return null;
    }

    private static bool Plausibile(int anno) => anno is >= 2000 and <= 2100;

    /// <summary>«Conforme - Accettato», «Non Conforme - Respinto NCG», «Non Conforme - NCL». Il vuoto vale «accettato».</summary>
    public static GiudizioAccettazione Giudizio(string? testo)
    {
        var s = (testo ?? "").ToLowerInvariant();
        if (s.Contains("respint")) return GiudizioAccettazione.Respinto;
        if (s.Contains("non conforme")) return GiudizioAccettazione.AccettatoConRiserva;
        return GiudizioAccettazione.Accettato;
    }

    /// <summary>Unità di misura normalizzata (Kg → kg, Litri → l, Pezzi → pz) oppure null se vuota o sconosciuta.</summary>
    public static string? Unita(string? testo)
    {
        var s = testo?.Trim().ToLowerInvariant().TrimEnd('.');
        return s switch
        {
            "kg" or "kilogrammi" or "chili" => "kg",
            "l" or "lt" or "litri" or "litro" => "l",
            "pz" or "pezzi" or "pezzo" or "n" or "nr" => "pz",
            "g" or "grammi" => "g",
            _ => null
        };
    }

    public static IReadOnlyList<ControlloPronto> Controlli(RigaMagazzino r)
    {
        var elenco = new List<ControlloPronto>();

        if (Pieno(r.Integrita) || Pieno(r.Sottovuoto))
        {
            var integrita = r.Integrita?.Trim().ToLowerInvariant();
            bool? conforme = integrita switch
            {
                null or "" => null,
                var x when x.StartsWith("non") => false,
                var x when x.StartsWith("integra") => true,
                _ => null
            };
            elenco.Add(new ControlloPronto(TipoControllo.Confezione, conforme, Vuoto(r.Sottovuoto)));
        }

        if (Pieno(r.ControlloTemperatura) || Pieno(r.AccettazioneTemperatura))
        {
            var acc = r.AccettazioneTemperatura?.Trim().ToLowerInvariant();
            bool? conforme = acc switch
            {
                null or "" => null,
                var x when x.StartsWith("non") => false,
                var x when x.StartsWith("conforme") => true,
                _ => null
            };
            elenco.Add(new ControlloPronto(TipoControllo.Temperatura, conforme, Vuoto(r.ControlloTemperatura)));
        }

        if (Pieno(r.PrelievoCampione) || Pieno(r.AnalisiLaboratorio))
        {
            var parti = new List<string>();
            if (Pieno(r.PrelievoCampione)) parti.Add("Prelievo: " + r.PrelievoCampione!.Trim());
            if (Pieno(r.AnalisiLaboratorio)) parti.Add("Analisi: " + r.AnalisiLaboratorio!.Trim());
            elenco.Add(new ControlloPronto(TipoControllo.PrelievoAnalisi, null, string.Join(" · ", parti)));
        }

        if (Pieno(r.VerificaOrganolettica))
            elenco.Add(new ControlloPronto(TipoControllo.Organolettico, null, r.VerificaOrganolettica!.Trim()));

        if (Pieno(r.CertificatoNumero))
            elenco.Add(new ControlloPronto(TipoControllo.Certificati, null, r.CertificatoNumero!.Trim()));

        return elenco;
    }

    /// <summary>
    /// Nei profili del vecchio programma i controlli non necessari sono scritti «Non richiesto», «Non necessario»,
    /// «Non applicabile», «Prelievo non richiesto», «x»: non entrano nel profilo.
    /// «Non deve contenere impurità» invece è un controllo vero.
    /// </summary>
    public static bool ControlloNonRichiesto(string? testo)
    {
        var s = (testo ?? "").Trim().ToLowerInvariant();
        if (s.Length == 0 || s == "x") return true;
        return s.Contains("non richiest") || s.Contains("non necessar") || s.Contains("non applicabil");
    }

    private static bool Pieno(string? t) => !string.IsNullOrWhiteSpace(t);
    private static string? Vuoto(string? t) => Pieno(t) ? t!.Trim() : null;
}
