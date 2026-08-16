namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Versione dell'applicazione in esecuzione.
///
/// Viene registrata su ogni analisi archiviata e su ogni voce di audit:
/// permette di sapere con quale versione del software è stato prodotto
/// un determinato risultato.
/// </summary>
public interface IServizioVersione
{
    /// <summary>Versione semantica, es. <c>1.2.0</c>.</summary>
    string Versione { get; }

    /// <summary>Versione completa con eventuale suffisso di build.</summary>
    string VersioneEstesa { get; }

    /// <summary>Nome dell'ambiente: Produzione, Collaudo, Sviluppo.</summary>
    string Ambiente { get; }
}
