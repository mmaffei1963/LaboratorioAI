namespace LaboratorioAI.Dati.ArcLab;

/// <summary>
/// Costanti dipendenti dalla struttura dati di ArcLab.
///
/// Raccolte qui perché sono conoscenza sul gestionale, non sul dominio:
/// se ArcLab cambia, si interviene in un punto solo.
/// </summary>
internal static class CostantiArcLab
{
    /// <summary>
    /// Riga di metodo tecnica presente su ogni campione, da escludere
    /// dai conteggi e dagli elenchi delle prove.
    /// </summary>
    public const int IdRigaMetodoDaEscludere = 18963;

    /// <summary>Giorni oltre i quali un campione privo di prove è considerato spedito in originale.</summary>
    public const int GiorniPerSpeditoOriginale = 10;

    /// <summary>Tipo commessa corrispondente alle attività di laboratorio.</summary>
    public const int TipoCommessaLaboratorio = 3;
}
