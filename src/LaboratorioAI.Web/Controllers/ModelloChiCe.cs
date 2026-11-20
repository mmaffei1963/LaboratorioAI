using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Presenze;

namespace LaboratorioAI.Web.Controllers;

/// <summary>Pagina «Chi c'è adesso» (20/11/2026): stato di oggi di ogni lavoratore attivo, filtrabile per sede.</summary>
public sealed record ModelloChiCe(IReadOnlyList<RigaDisponibilita> Righe, IReadOnlyList<SedeAziendale> Sedi, int? IdSede, DateTime Aggiornato);
