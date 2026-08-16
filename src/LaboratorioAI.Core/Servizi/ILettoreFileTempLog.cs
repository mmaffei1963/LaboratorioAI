using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Legge il file notturno Econorma (.xls BIFF, foglio "ALL") e ne estrae le letture per canale.</summary>
public interface ILettoreFileTempLog
{
    /// <summary>Interpreta lo stream del file; <paramref name="nomeFile"/> è solo per l'etichetta.</summary>
    FileTempLog Leggi(Stream contenuto, string? nomeFile = null);
}
