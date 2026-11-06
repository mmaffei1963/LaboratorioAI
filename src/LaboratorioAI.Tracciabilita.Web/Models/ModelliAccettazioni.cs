using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public static class NomiControlli
{
    public static string Nome(TipoControllo t) => t switch
    {
        TipoControllo.Etichettatura => "Etichettatura",
        TipoControllo.Confezione => "Integrità della confezione",
        TipoControllo.Temperatura => "Temperatura",
        TipoControllo.Organolettico => "Verifica organolettica o visiva",
        TipoControllo.PrelievoAnalisi => "Prelievo campione / analisi",
        TipoControllo.Certificati => "Certificato n.",
        TipoControllo.Pallet => "Pallet e automezzo",
        _ => t.ToString()
    };

    /// <summary>Come i riquadri della scheda di accettazione del vecchio programma.</summary>
    public static string Gruppo(TipoControllo t) => t switch
    {
        TipoControllo.Pallet => "Controllo automezzo",
        TipoControllo.Etichettatura or TipoControllo.Confezione => "Etichettatura e confezionamento",
        TipoControllo.Temperatura => "Controllo temperatura",
        _ => "Campionatura"
    };
}

public class ControlloInput
{
    public TipoControllo Controllo { get; set; }
    public string Nome { get; set; } = "";
    public bool Obbligatorio { get; set; }
    public string? Istruzione { get; set; }
    public bool? Conforme { get; set; }
    public string? Valore { get; set; }
}

public class ModelloNuovaAccettazione
{
    // Solo per la correzione di un'accettazione già registrata
    public int IdAccettazione { get; set; }
    public bool CodiceBloccato { get; set; }
    public string? MotivoCodiceBloccato { get; set; }
    public bool Respinta { get; set; }

    // Da dove nasce
    public int? IdOrdineRiga { get; set; }
    public string? NumeroOrdine { get; set; }

    // Che cosa arriva
    public int? IdFornitore { get; set; }
    public int? IdArticolo { get; set; }
    public int? IdCategoria { get; set; }
    public string Fornitore { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string? Matrice { get; set; }
    public bool Bio { get; set; }

    // Documento e lotto
    public DateOnly DataArrivo { get; set; }
    public string? TipoDocumento { get; set; } = "Bolla";
    public string? NumeroDocumento { get; set; }
    public DateOnly? DataDocumento { get; set; }
    public string? Vettore { get; set; }
    public string? CodiceLotto { get; set; }
    public DateOnly? DataProduzione { get; set; }
    public DateOnly? DataScadenza { get; set; }
    public bool ETmc { get; set; }
    public string? EnteBio { get; set; }
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; } = "kg";

    // Controlli e giudizio
    public List<ControlloInput> Controlli { get; set; } = new();
    public GiudizioAccettazione Giudizio { get; set; } = GiudizioAccettazione.Accettato;
    public string? NoteReclami { get; set; }

    // Per il primo passo (accettazione senza ordine)
    public bool Pronto { get; set; }
    public List<OpzioneAnagrafica> Fornitori { get; set; } = new();
    public List<OpzioneAnagrafica> Articoli { get; set; } = new();
    public List<OpzioneAnagrafica> Categorie { get; set; } = new();
}

public class RigaAccettazione
{
    public int IdAccettazione { get; set; }
    public DateOnly DataArrivo { get; set; }
    public string Fornitore { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string? Lotto { get; set; }
    public int? IdLotto { get; set; }
    public decimal Quantita { get; set; }
    public string UnitaMisura { get; set; } = "";
    public GiudizioAccettazione Giudizio { get; set; }
    public string? NumeroOrdine { get; set; }
}

public class ModelloElencoAccettazioni
{
    public string? Cerca { get; set; }
    public int Totale { get; set; }
    public List<RigaAccettazione> Righe { get; set; } = new();
}

public class VoceEtichetta
{
    public string Gruppo { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Esito { get; set; } = "";
}

public class ModelloEtichetta
{
    public int IdAccettazione { get; set; }
    /// <summary>Indirizzo della scheda del lotto, scritto nel codice QR (vuoto se non c'è un lotto).</summary>
    public string? UrlLotto { get; set; }
    public bool Vuota { get; set; }
    public string Azienda { get; set; } = "";
    public string Sede { get; set; } = "";
    public string? NumeroOrdine { get; set; }
    public DateTime? Accettata { get; set; }
    public string Fornitore { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string? Matrice { get; set; }
    public string? TipoDocumento { get; set; }
    public DateOnly? DataDocumento { get; set; }
    public string? NumeroDocumento { get; set; }
    public string? Lotto { get; set; }
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; }
    public DateOnly? DataProduzione { get; set; }
    public DateOnly? DataScadenza { get; set; }
    public bool ETmc { get; set; }
    public string? EnteBio { get; set; }
    public GiudizioAccettazione? Giudizio { get; set; }
    public string? Note { get; set; }
    public List<VoceEtichetta> Voci { get; set; } = new();
}

public class RigaMagazzino
{
    public int IdLotto { get; set; }
    public string Codice { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string? Fornitore { get; set; }
    public string? Matrice { get; set; }
    public int? IdCategoria { get; set; }
    public decimal Giacenza { get; set; }
    public string UnitaMisura { get; set; } = "";
    public DateOnly? DataArrivo { get; set; }
    public DateOnly? DataScadenza { get; set; }
    public DateTime? InUsoDal { get; set; }
    public bool InUsoAperto { get; set; }
}

public class RimanenzaArticolo
{
    public string? Fornitore { get; set; }
    public string Articolo { get; set; } = "";
    public decimal Quantita { get; set; }
    public string UnitaMisura { get; set; } = "";
}

public class ModelloMagazzino
{
    /// <summary>da-impegnare, in-uso, tutti.</summary>
    public string Vista { get; set; } = "da-impegnare";
    public string? Cerca { get; set; }
    public List<RigaMagazzino> Righe { get; set; } = new();
    public List<RimanenzaArticolo> Rimanenze { get; set; } = new();
}
