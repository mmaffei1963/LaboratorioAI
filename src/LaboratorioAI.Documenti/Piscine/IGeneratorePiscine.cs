// LaboratorioAI.Documenti/Piscine/IGeneratorePiscine.cs
//
// Contratto del generatore dei documenti piscine.

using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti.Piscine;

/// <summary>Produce il documento Word piscine a partire da una scheda compilata.</summary>
public interface IGeneratorePiscine
{
    /// <summary>
    /// Genera il documento valorizzando il modello configurato in
    /// <see cref="OpzioniPiscine"/> con i dati della scheda.
    /// </summary>
    /// <param name="scheda">Dati del questionario e anagrafica.</param>
    /// <returns>Esito con il .docx in memoria ed eventuali tag non trovati.</returns>
    RisultatoGenerazionePiscina Genera(SchedaPiscina scheda);

    /// <summary>Genera il Regolamento interno dal modello a token configurato.</summary>
    RisultatoGenerazionePiscina GeneraRegolamento(SchedaPiscina scheda);

    /// <summary>
    /// Genera il Manuale di autocontrollo (Emilia-Romagna) o il Registro tecnico
    /// funzionale (Marche), scegliendo il modello a token in base alla regione
    /// dell'insediamento.
    /// </summary>
    RisultatoGenerazionePiscina GeneraManuale(SchedaPiscina scheda);
}
