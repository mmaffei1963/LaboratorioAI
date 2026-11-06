namespace LaboratorioAI.Dati.MonitorTemperature.Models
{
    /// <summary>Proiezione di sola lettura da ArcLab (LabAnalisiDati). Mai scritta.</summary>
    public class SondaArcLab
    {
        public int IdApparecchioSonda { get; set; }
        public int IdStrumentoSonda { get; set; }

        public string NomeSonda { get; set; } = "";
        public string? SerialeSonda { get; set; }

        public int IdApparecchioAssociato { get; set; }
        public int IdStrumentoAssociato { get; set; }

        public string NomeApparecchiatura { get; set; } = "";
        public string? SerialeApparecchiatura { get; set; }

        public int? IdPianoInt { get; set; }
        public string? NomePiano { get; set; }

        public decimal? ValImpostato { get; set; }
        public decimal? LimInfInt { get; set; }
        public decimal? LimSupInt { get; set; }
        public decimal? IncMaxAmmessa { get; set; }

        public string? Associato1 { get; set; }
    }
}
