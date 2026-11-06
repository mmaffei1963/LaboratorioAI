namespace LaboratorioAI.Dati.MonitorTemperature.Models
{
    public class StatoAttualeDashboard
    {
        public string CodiceApparecchiatura { get; set; } = "";

        public string Apparecchiatura { get; set; } = "";

        public DateTime DataOra { get; set; }

        public decimal? TemperaturaCorretta { get; set; }

        public decimal? LimiteMin { get; set; }

        public decimal? LimiteMax { get; set; }

        public string Stato { get; set; } = "";
    }
}
