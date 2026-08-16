namespace LaboratorioAI.Core.Entita;

/// <summary>Un ingrediente della ricetta: materia prima + quantità in kg.</summary>
public class RigaRicetta
{
    public int IdRigaRicetta { get; set; }

    public int IdRicetta { get; set; }
    public Ricetta? Ricetta { get; set; }

    public int IdMateriaPrima { get; set; }
    public MateriaPrima? MateriaPrima { get; set; }

    public decimal QuantitaKg { get; set; }
    public int Ordine { get; set; }
}
