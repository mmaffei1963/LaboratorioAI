using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class RigaProduzione
{
    public int IdProduzione { get; set; }
    public DateOnly Giorno { get; set; }
    public string Prodotto { get; set; } = "";
    public int IdLottoProdotto { get; set; }
    public string LottoProdotto { get; set; } = "";
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; }
    public int LottiUsati { get; set; }
    public bool Annullata { get; set; }
}

public class ModelloElencoProduzioni
{
    public string? Cerca { get; set; }
    public DateOnly? Dal { get; set; }
    public DateOnly? Al { get; set; }
    public int Pagina { get; set; } = 1;
    public int PerPagina { get; set; } = 50;
    public int Totale { get; set; }
    public List<RigaProduzione> Righe { get; set; } = new();
    public int Pagine => Math.Max(1, (Totale + PerPagina - 1) / PerPagina);
}

public class RigaConsumoProduzione
{
    public string Ingrediente { get; set; } = "";
    public int IdLotto { get; set; }
    public string Lotto { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string? Fornitore { get; set; }
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; }
    public OrigineConsumo Origine { get; set; }
}

public class ModelloSchedaProduzione
{
    public int IdProduzione { get; set; }
    public DateOnly Giorno { get; set; }
    public DateTime DataOra { get; set; }
    public string Prodotto { get; set; } = "";
    public int IdLottoProdotto { get; set; }
    public string LottoProdotto { get; set; } = "";
    public decimal? Quantita { get; set; }
    public string? UnitaMisura { get; set; }
    public string? Note { get; set; }
    public bool Annullata { get; set; }
    public List<RigaConsumoProduzione> Consumi { get; set; } = new();
    // Per tornare all'elenco con gli stessi filtri.
    public string? Cerca { get; set; }
    public DateOnly? Dal { get; set; }
    public DateOnly? Al { get; set; }
    public int Pagina { get; set; } = 1;
}
