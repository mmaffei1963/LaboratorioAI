using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Motori;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class RigaLotto
{
    public int IdLotto { get; set; }
    public string Codice { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string? Fornitore { get; set; }
    public TipoLotto Tipo { get; set; }
    public StatoLotto Stato { get; set; }
    public decimal Giacenza { get; set; }
    public string UnitaMisura { get; set; } = "kg";
    public DateOnly? DataArrivo { get; set; }
}

public class ModelloElencoLotti
{
    public string? Cerca { get; set; }
    public bool SoloInMagazzino { get; set; }
    public string FiltroTipo { get; set; } = "mp";
    public int Pagina { get; set; } = 1;
    public int PerPagina { get; set; } = 50;
    public int Totale { get; set; }
    public List<RigaLotto> Righe { get; set; } = new();
    public int Pagine => Math.Max(1, (Totale + PerPagina - 1) / PerPagina);
}

public class RigaAccettazioneLotto
{
    public DateOnly DataArrivo { get; set; }
    public string? TipoDocumento { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateOnly? DataDocumento { get; set; }
    public decimal Quantita { get; set; }
    public string UnitaMisura { get; set; } = "kg";
    public string? Note { get; set; }
}

public class RigaMovimentoLotto
{
    public DateTime Data { get; set; }
    public TipoMovimento Tipo { get; set; }
    public decimal Quantita { get; set; }
    public string UnitaMisura { get; set; } = "kg";
    public string? Nota { get; set; }
}

public class ModelloSchedaLotto
{
    // Ricerca da cui si arriva: serve per tornare all'elenco com'era.
    public string? Cerca { get; set; }
    public bool SoloInMagazzino { get; set; }
    public string FiltroTipo { get; set; } = "mp";
    public int Pagina { get; set; } = 1;
    public int IdLotto { get; set; }
    public string Codice { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string? Categoria { get; set; }
    public string? Fornitore { get; set; }
    public TipoLotto Tipo { get; set; }
    public StatoLotto Stato { get; set; }
    public DateOnly? GiornoLottoInterno { get; set; }
    public DateOnly? DataScadenza { get; set; }
    public bool ETmc { get; set; }
    public string? MotivoBlocco { get; set; }
    public decimal Giacenza { get; set; }
    public string UnitaMisura { get; set; } = "kg";
    public List<RigaAccettazioneLotto> Accettazioni { get; set; } = new();
    public List<RigaMovimentoLotto> Movimenti { get; set; } = new();
}

public class RigaLottoRaggiunto
{
    public int IdLotto { get; set; }
    public int Livello { get; set; }
    public string Codice { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string? Fornitore { get; set; }
}

public class RigaClienteRaggiunto
{
    public string Cliente { get; set; } = "";
    public DateOnly Data { get; set; }
    public string? NumeroDocumento { get; set; }
    public string LottoCodice { get; set; } = "";
    public decimal? Quantita { get; set; }
}

public class ModelloRintracciabilita
{
    // Ricerca da cui si arriva: serve per tornare all'elenco com'era.
    public string? Cerca { get; set; }
    public bool SoloInMagazzino { get; set; }
    public string FiltroTipo { get; set; } = "mp";
    public int Pagina { get; set; } = 1;
    public int IdLotto { get; set; }
    public string Codice { get; set; } = "";
    public string Articolo { get; set; } = "";
    public List<RigaLottoRaggiunto> ABMonte { get; set; } = new();
    public List<RigaLottoRaggiunto> AValle { get; set; } = new();
    public List<RigaClienteRaggiunto> Clienti { get; set; } = new();
    public int ProduzioniMonte { get; set; }
    public int ProduzioniValle { get; set; }
    public bool Troncato { get; set; }
}
