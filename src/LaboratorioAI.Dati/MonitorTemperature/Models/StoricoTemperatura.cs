namespace LaboratorioAI.Dati.MonitorTemperature.Models
{
    public class StoricoTemperatura
    {
        public long IdMisura { get; set; }

        public DateTime DataOra { get; set; }

        public int IdSonda { get; set; }

        public string CodiceSonda { get; set; } = "";

        public string DescrizioneSonda { get; set; } = "";

        public int IdApparecchiatura { get; set; }

        public string CodiceApparecchiatura { get; set; } = "";

        public string Apparecchiatura { get; set; } = "";

        public decimal? TemperaturaGrezza { get; set; }

        public decimal? OffsetTemperatura { get; set; }

        public decimal? TemperaturaCorretta { get; set; }

        public decimal? LimiteMin { get; set; }

        public decimal? LimiteMax { get; set; }

        public int AllarmeCalcolato { get; set; }
    }
}
