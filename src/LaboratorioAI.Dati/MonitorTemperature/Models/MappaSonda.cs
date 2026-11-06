using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LaboratorioAI.Dati.MonitorTemperature.Models
{
    [Table("MappaSonde", Schema = "dbo")]
    public class MappaSonda
    {
        [Key]
        public int IdMappa { get; set; }

        public int IdApparecchioSonda { get; set; }

        public string SerialeSonda { get; set; } = "";

        public string? CodiceEconorma { get; set; }

        public bool Attiva { get; set; }

        public DateTime DataCreazione { get; set; }
    }
}
