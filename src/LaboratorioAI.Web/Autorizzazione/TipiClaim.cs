namespace LaboratorioAI.Web.Autorizzazione;

/// <summary>Tipi di claim applicativi aggiunti all'identità dopo l'accesso.</summary>
public static class TipiClaim
{
    /// <summary>Un claim per ogni permesso posseduto.</summary>
    public const string Permesso = "laboratorioai:permesso";

    /// <summary>Identificativo dell'utente nella tabella applicativa.</summary>
    public const string IdUtente = "laboratorioai:id_utente";
}
