namespace LaboratorioAI.Core.Modelli;

/// <summary>Dati di intestazione MARE.A (tabella ArcLab Marchi) per i documenti di accettazione.</summary>
public record IntestazioneMarea(string? Direttore, string? Citta, string? Mail);

/// <summary>Laboratorio esterno: nome e indirizzo separati (per richiesta e catena di custodia).</summary>
public record LabEsternoInfo(int Id, string? Nome, string? Indirizzo);

/// <summary>Indirizzo completo di cliente/insediamento (per la catena di custodia).</summary>
public record IndirizzoDestinatario(string? RagioneSociale, string? Insediamento, string? IndirizzoCompleto);
