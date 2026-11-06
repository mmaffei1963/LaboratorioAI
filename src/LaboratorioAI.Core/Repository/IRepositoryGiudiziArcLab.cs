namespace LaboratorioAI.Core.Repository;

/// <summary>Giudizio di ArcLab (TipoGiudizi).</summary>
public class GiudizioArcLab
{
    public int IdGiudizio { get; set; }
    public string CodGiudizio { get; set; } = "";
    public string Giudizio { get; set; } = "";
    public bool GiudizioConformita { get; set; }
}

/// <summary>Stato del campione in ArcLab per la validazione: chiusura, giudizi già inseriti, richiesta di giudizio del cliente.</summary>
public class StatoCampioneArcLab
{
    public int IdCampione { get; set; }
    public string? NumeroCampione { get; set; }
    public int? IdMatrice { get; set; }
    public bool AnalisiChiusa { get; set; }
    public bool GiudizioConf { get; set; }
    public int? IdCertificato { get; set; }
    public int Prove { get; set; }
    /// <summary>Prove con VerificaValore diverso da 1 (risultati non ancora verificati).</summary>
    public int ProveNonVerificate { get; set; }
    /// <summary>Giudizi presenti in GiudCamp (IdGiudizio non nullo).</summary>
    public List<GiudizioArcLab> Giudizi { get; set; } = [];
}

/// <summary>
/// Validazione da LaboratorioAI (18/10/2026): letture su ArcLab per giudizi e chiusura analisi.
/// SOLA LETTURA: la scrittura in GiudCamp/Campione la fa Palomar importando da scambio.GiudiziDaRiportare.
/// </summary>
public interface IRepositoryGiudiziArcLab
{
    Task<StatoCampioneArcLab?> StatoAsync(int idCampione, CancellationToken ct = default);
    Task<IReadOnlyList<StatoCampioneArcLab>> StatiAsync(IReadOnlyCollection<int> idCampioni, CancellationToken ct = default);
    /// <summary>Giudizi non sospesi abbinati alla matrice (GiudizioMatrice).</summary>
    Task<IReadOnlyList<GiudizioArcLab>> GiudiziMatriceAsync(int idMatrice, CancellationToken ct = default);
    Task<GiudizioArcLab?> GiudizioAsync(int idGiudizio, CancellationToken ct = default);
}
