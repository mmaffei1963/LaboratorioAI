namespace LaboratorioAI.Core.Enumerazioni;

/// <summary>Natura della materia prima nel catalogo nutrizionale.</summary>
public enum TipoMateriaPrima
{
    /// <summary>Valore specifico di marca/fornitore (scheda tecnica).</summary>
    Specifica = 1,

    /// <summary>Alimento generico di riferimento (CREA e simili).</summary>
    Generica = 2
}

/// <summary>Tipo di fonte da cui provengono i valori nutrizionali.</summary>
public enum TipoFonte
{
    SchedaTecnica = 1,
    DatabaseCrea = 2,
    DatabaseEuroFir = 3,
    Referto = 4,
    Bibliografia = 5,
    Calcolo = 6,
    DatabaseCnf = 7
}
