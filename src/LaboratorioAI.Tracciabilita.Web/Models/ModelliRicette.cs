using System.ComponentModel.DataAnnotations;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Motori;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class RigaRicettaElenco
{
    public int IdRicetta { get; set; }
    public string Nome { get; set; } = "";
    public int Ingredienti { get; set; }
    public bool Attiva { get; set; }
}

public class ModelloElencoRicette
{
    public List<RigaRicettaElenco> Ricette { get; set; } = new();
    public bool MostraTutte { get; set; }
    public int NonInUso { get; set; }
    [Required(ErrorMessage = "Scrivi il nome del prodotto.")]
    [Display(Name = "Nome del prodotto")]
    public string? NuovoNome { get; set; }
}

public class RigaIngrediente
{
    public int IdRicettaRiga { get; set; }
    public string Nome { get; set; } = "";
    public List<string> Categorie { get; set; } = new();
    public int LottiPerFornitore { get; set; }
    public decimal? QuantitaTeorica { get; set; }
}

public class OpzioneCategoria
{
    public int IdCategoria { get; set; }
    public string Nome { get; set; } = "";
}

public class ModelloSchedaRicetta
{
    public int IdRicetta { get; set; }
    public string Nome { get; set; } = "";
    public bool Attiva { get; set; } = true;
    public List<RigaIngrediente> Ingredienti { get; set; } = new();
    public List<OpzioneCategoria> CategorieDisponibili { get; set; } = new();

    public int IdRicettaRiga { get; set; }
    public string? Etichetta { get; set; }
    public int LottiPerFornitore { get; set; } = 2;
    [Range(0.001, 100000, ErrorMessage = "I kg devono essere maggiori di zero.")]
    public decimal? QuantitaIngrediente { get; set; }
    /// <summary>Kg di prodotto finito a cui si riferiscono i kg degli ingredienti (es. 100).</summary>
    public decimal? QuantitaRiferimento { get; set; }
    public List<int> CategorieScelte { get; set; } = new();
}

public class IngredienteProposto
{
    public decimal? QuantitaTeorica { get; set; }
    public string Nome { get; set; } = "";
    public int LottiPerFornitore { get; set; }
    public IReadOnlyList<LottiDelFornitore> Fornitori { get; set; } = Array.Empty<LottiDelFornitore>();
    public List<(int IdFornitore, string Fornitore, DateOnly UltimoImpegno)> Scartati { get; set; } = new();
}

public class ModelloProposta
{
    public ModoQuantitaConsumi ModoConsumi { get; set; }
    public bool ConsumiObbligatori { get; set; }
    public decimal? QuantitaRiferimento { get; set; }
    public int IdRicetta { get; set; }
    public string Nome { get; set; } = "";
    public DateOnly Giorno { get; set; }
    public List<int> Esclusi { get; set; } = new();
    public List<IngredienteProposto> Ingredienti { get; set; } = new();
    /// <summary>Numero di lotto interno del giorno (es. 296) e se esiste già una produzione di questo prodotto in quel giorno.</summary>
    public string? LottoInterno { get; set; }
    public bool LottoInternoEsistente { get; set; }
    /// <summary>Tutti i fornitori comparsi nella proposta, per poterli escludere a mano.</summary>
    public List<(int IdFornitore, string Fornitore)> FornitoriPresenti { get; set; } = new();
}

public class ModelloImpostazioni
{
    public ModoQuantitaConsumi ModoConsumi { get; set; }
    public bool ConsumiObbligatori { get; set; }
}
