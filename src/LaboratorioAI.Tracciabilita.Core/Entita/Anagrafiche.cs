using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;

namespace LaboratorioAI.Tracciabilita.Core.Entita;

public class Fornitore : IDellAzienda
{
    public int IdFornitore { get; set; }
    public int IdAzienda { get; set; }
    public string RagioneSociale { get; set; } = null!;
    public string? PartitaIva { get; set; }
    public string? Indirizzo { get; set; }
    public string? Cap { get; set; }
    public string? Citta { get; set; }
    public string? Provincia { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Note { get; set; }

    /// <summary>Codice usato dal vecchio programma (per l'importazione).</summary>
    public string? CodiceEsterno { get; set; }

    public bool Attivo { get; set; } = true;
}

/// <summary>Destinatario delle spedizioni: altro produttore, punto vendita, distributore.</summary>
public class Cliente : IDellAzienda
{
    public int IdCliente { get; set; }
    public int IdAzienda { get; set; }
    public string RagioneSociale { get; set; } = null!;
    public string? PartitaIva { get; set; }
    public string? Indirizzo { get; set; }
    public string? Cap { get; set; }
    public string? Citta { get; set; }
    public string? Provincia { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Note { get; set; }
    public string? CodiceEsterno { get; set; }
    public bool Attivo { get; set; } = true;
}

/// <summary>Insieme dei controlli da fare all'arrivo di una materia prima.</summary>
public class ProfiloControllo : IDellAzienda
{
    public int IdProfiloControllo { get; set; }
    public int IdAzienda { get; set; }
    public string Nome { get; set; } = null!;
    public bool Attivo { get; set; } = true;
    public List<ProfiloControlloRiga> Righe { get; set; } = new();
}

public class ProfiloControlloRiga : IDellAzienda
{
    public int IdProfiloControlloRiga { get; set; }
    public int IdAzienda { get; set; }
    public int IdProfiloControllo { get; set; }
    public TipoControllo Controllo { get; set; }
    public bool Obbligatorio { get; set; } = true;
    public string? Istruzione { get; set; }
    /// <summary>Falso = il controllo è stato tolto dal profilo (le righe non si cancellano).</summary>
    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Categoria di materia prima (la «matrice» del vecchio programma): è l'ingrediente
/// di una ricetta. Il fornitore e il lotto si risolvono al momento della produzione.
/// Include anche imballi e gas.
/// </summary>
public class CategoriaMateriaPrima : IDellAzienda
{
    public int IdCategoria { get; set; }
    public int IdAzienda { get; set; }
    public string Nome { get; set; } = null!;
    public int? IdProfiloControllo { get; set; }
    public string? Note { get; set; }
    public bool Attivo { get; set; } = true;
}

public class Articolo : IDellAzienda
{
    public int IdArticolo { get; set; }
    public int IdAzienda { get; set; }
    public string? Codice { get; set; }
    public string Descrizione { get; set; } = null!;
    public TipoArticolo Tipo { get; set; } = TipoArticolo.MateriaPrima;
    public int? IdCategoria { get; set; }
    public string UnitaMisura { get; set; } = "kg";
    public bool Bio { get; set; }
    public string? Note { get; set; }
    public bool Attivo { get; set; } = true;
}

/// <summary>I 14 allergeni del Reg. UE 1169/2011 (Allegato II). Elenco unico per tutte le aziende.</summary>
public class Allergene
{
    public int IdAllergene { get; set; }
    public string Codice { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public int Ordine { get; set; }
}

public class ArticoloAllergene : IDellAzienda
{
    public int IdArticoloAllergene { get; set; }
    public int IdAzienda { get; set; }
    public int IdArticolo { get; set; }
    public int IdAllergene { get; set; }

    /// <summary>Presente solo come possibile contaminazione («può contenere»).</summary>
    public bool SoloTracce { get; set; }
}

public class ArticoloFornitore : IDellAzienda
{
    public int IdArticoloFornitore { get; set; }
    public int IdAzienda { get; set; }
    public int IdArticolo { get; set; }
    public int IdFornitore { get; set; }
    public string? CodiceDelFornitore { get; set; }
    public bool Preferito { get; set; }

    /// <summary>Falso = il fornitore non fornisce più questo articolo (non si cancella).</summary>
    public bool Attivo { get; set; } = true;
}

/// <summary>Ricetta (distinta base) di un prodotto finito o semilavorato.</summary>
public class Ricetta : IDellAzienda
{
    public int IdRicetta { get; set; }
    public int IdAzienda { get; set; }
    public int IdArticolo { get; set; }
    public string Nome { get; set; } = null!;
    public bool Attiva { get; set; } = true;
    public List<RicettaVersione> Versioni { get; set; } = new();
}

public class RicettaVersione : IDellAzienda
{
    public int IdRicettaVersione { get; set; }
    public int IdAzienda { get; set; }
    public int IdRicetta { get; set; }
    public int Numero { get; set; } = 1;
    public DateOnly ValidaDal { get; set; }
    public DateOnly? ValidaAl { get; set; }
    public decimal? QuantitaProdottaTeorica { get; set; }
    public string? Note { get; set; }
    public List<RicettaRiga> Righe { get; set; } = new();
}

/// <summary>Ingrediente di una ricetta: una categoria di materia prima oppure un semilavorato.</summary>
public class RicettaRiga : IDellAzienda
{
    public int IdRicettaRiga { get; set; }
    public int IdAzienda { get; set; }
    public int IdRicettaVersione { get; set; }
    public int? IdCategoria { get; set; }
    public int? IdArticoloSemilavorato { get; set; }
    public decimal? QuantitaTeorica { get; set; }
    public string? UnitaMisura { get; set; }
    public int Ordine { get; set; }
    public string? Note { get; set; }

    /// <summary>Nome mostrato nella ricetta (es. «Farina»), quando l'ingrediente copre più categorie.</summary>
    public string? Etichetta { get; set; }

    /// <summary>Quanti lotti recenti proporre per ogni fornitore (es. 3 per la farina, 2 per gli altri).</summary>
    public int LottiPerFornitore { get; set; } = 2;

    /// <summary>Falso = ingrediente tolto dalla ricetta (non si cancella: resta la storia).</summary>
    public bool Attiva { get; set; } = true;
}

/// <summary>
/// Categorie in più di un ingrediente di ricetta. La categoria principale resta in <see cref="RicettaRiga.IdCategoria"/>;
/// qui stanno le altre (es. la «Farina» copre farina 00 in silos, farina 0 in sacchi, farina 0 in silos…).
/// </summary>
public class RicettaRigaCategoria : IDellAzienda
{
    public int IdRicettaRigaCategoria { get; set; }
    public int IdAzienda { get; set; }
    public int IdRicettaRiga { get; set; }
    public int IdCategoria { get; set; }
}

/// <summary>Campo in più per un profilo di settore (es. nell'ittico: zona FAO).</summary>
public class CampoAggiuntivo : IDellAzienda
{
    public int IdCampo { get; set; }
    public int IdAzienda { get; set; }

    /// <summary>A quale entità si applica: «Articolo» o «Lotto».</summary>
    public string Entita { get; set; } = null!;

    public string Nome { get; set; } = null!;
    public TipoCampo Tipo { get; set; } = TipoCampo.Testo;
    public bool Obbligatorio { get; set; }

    /// <summary>Valori ammessi, separati da punto e virgola (solo per i campi a elenco).</summary>
    public string? Opzioni { get; set; }

    public bool Attivo { get; set; } = true;
}

public class ValoreCampo : IDellAzienda
{
    public int IdValoreCampo { get; set; }
    public int IdAzienda { get; set; }
    public int IdCampo { get; set; }
    public int IdEntita { get; set; }
    public string? Valore { get; set; }
}
