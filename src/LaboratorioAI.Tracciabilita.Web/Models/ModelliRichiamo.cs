using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Web.Models;

public class RigaLottoRapporto
{
    public int IdLotto { get; set; }
    public int Livello { get; set; }
    public string Codice { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string Tipo { get; set; } = "";
    public decimal Giacenza { get; set; }
    public string UnitaMisura { get; set; } = "kg";
    public string Stato { get; set; } = "";
}

public class ConsegnaRapporto
{
    public string Data { get; set; } = "";
    public string? Documento { get; set; }
    public string Lotto { get; set; } = "";
    public decimal? Quantita { get; set; }
}

public class ClienteRapporto
{
    public int IdCliente { get; set; }
    public string Nome { get; set; } = "";
    public string? Indirizzo { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public List<ConsegnaRapporto> Consegne { get; set; } = new();
}

/// <summary>Rapporto di richiamo o simulazione: viene salvato così com'è nel registro e non cambia più.</summary>
public class RapportoRichiamo
{
    public int IdLottoOrigine { get; set; }
    public string LottoOrigine { get; set; } = "";
    public string ArticoloOrigine { get; set; } = "";
    public string? Motivo { get; set; }
    public bool Reale { get; set; }
    public DateTime DataOra { get; set; }
    public int Produzioni { get; set; }
    public bool Troncato { get; set; }
    public List<RigaLottoRapporto> Lotti { get; set; } = new();
    public List<ClienteRapporto> Clienti { get; set; } = new();
    public decimal GiacenzaResidua => Lotti.Sum(l => l.Giacenza);
}

public class RigaLottoRicerca
{
    public int IdLotto { get; set; }
    public string Codice { get; set; } = "";
    public string Articolo { get; set; } = "";
    public string? Fornitore { get; set; }
    public TipoLotto Tipo { get; set; }
}

public class RigaRegistroRichiamo
{
    public int IdRichiamo { get; set; }
    public DateTime DataAvvio { get; set; }
    public bool Reale { get; set; }
    public string Lotto { get; set; } = "";
    public string? Motivo { get; set; }
    public int? Secondi { get; set; }
    public string? Esito { get; set; }
}

public class ModelloIndiceRichiamo
{
    public string? Cerca { get; set; }
    public List<RigaLottoRicerca> Trovati { get; set; } = new();
    public List<RigaRegistroRichiamo> Registro { get; set; } = new();
}

public class ModelloPreparaRichiamo
{
    public RapportoRichiamo Rapporto { get; set; } = new();
    public long AvvioTicks { get; set; }
}
