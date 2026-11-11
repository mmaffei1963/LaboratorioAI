namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Storico della zona AZW di un cliente (ANACF.ZONA = sigla del responsabile del cliente), 11/11/2026.
/// AZW conserva solo la zona attuale: a ogni importazione LaboratorioAI registra le variazioni, così un documento
/// prende la zona valida alla sua data. Nessuna cancellazione: una riga si chiude impostando <see cref="DataAl"/>.
/// </summary>
public class StoricoZonaCliente
{
    public int IdStoricoZonaCliente { get; set; }
    /// <summary>ANACF.CODCF del cliente.</summary>
    public string CodiceCliente { get; set; } = null!;
    /// <summary>Sigla della zona (maiuscola); null se in AZW il cliente non ha zona.</summary>
    public string? Zona { get; set; }
    /// <summary>Giorno della rilevazione da cui vale la zona (data di calendario).</summary>
    public DateTime DataDal { get; set; }
    /// <summary>Giorno in cui è stata rilevata la zona successiva (escluso); null = zona attuale.</summary>
    public DateTime? DataAl { get; set; }
    public DateTime DataCreazione { get; set; }
}
