namespace LaboratorioAI.Dati.MonitorTemperature.Models
{
    public class DifferenzaTemperatura
    {
        public long IdMisura { get; set; }

        public DateTime DataOra { get; set; }

        public int IdSonda { get; set; }

        public string CodiceSonda { get; set; } = "";

        public string Apparecchiatura { get; set; } = "";

        public string CodiceApparecchiatura { get; set; } = "";

        public string TipoApparecchiatura { get; set; } = "";

        public bool Congelatore { get; set; }

        public decimal TemperaturaOriginale { get; set; }

        public decimal TemperaturaControllo { get; set; }

        public decimal Differenza { get; set; }

        public decimal? LimiteMin { get; set; }

        public decimal? LimiteMax { get; set; }

        public decimal FuoriLimiteOriginale { get; set; }

        public decimal FuoriLimiteControllo { get; set; }
    }
}
