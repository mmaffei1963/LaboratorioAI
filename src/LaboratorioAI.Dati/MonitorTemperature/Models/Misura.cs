using System.ComponentModel.DataAnnotations;

namespace LaboratorioAI.Dati.MonitorTemperature.Models
{
    /// <summary>Misura originale (Econorma). Sola lettura dal punto di vista di LaboratorioAI.</summary>
    public class Misura
    {
        [Key]
        public long IdMisura { get; set; }

        public int IdSonda { get; set; }

        public DateTime DataOra { get; set; }

        public decimal? TemperaturaGrezza { get; set; }

        // Offset utilizzato nel momento in cui la misura è stata acquisita.
        // È fondamentale conservarlo nella misura perché l'offset della sonda
        // può cambiare in futuro dopo una nuova taratura.
        public decimal? OffsetApplicato { get; set; }

        public decimal? Batteria { get; set; }

        public int? Segnale { get; set; }

        public bool StatoAllarme { get; set; }

        public DateTime DataImportazione { get; set; }
    }
}
