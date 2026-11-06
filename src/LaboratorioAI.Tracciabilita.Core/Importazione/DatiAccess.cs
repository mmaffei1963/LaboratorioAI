using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Core.Importazione;

/// <summary>Una riga della tabella MAGAZZINO del vecchio programma (un arrivo di merce).</summary>
public sealed record RigaMagazzino(
    int Numero,                 // posizione nella tabella, per ritrovare la riga nelle segnalazioni
    string? TipoDocumento,
    DateTime? DataDocumento,
    string? NumeroDocumento,
    string? Fornitore,
    string? Articolo,
    decimal? Quantita,
    string? UnitaMisura,
    string? Lotto,
    DateTime? DataImpegno,
    string? Impegno,
    string? EnteBio,
    // Dalla tabella Ordine (tutto facoltativo: chi non li passa ottiene il comportamento di prima).
    int? IdMatrice = null,
    string? DataScadenza = null,
    string? DataProduzione = null,
    string? Giudizio = null,
    string? Note = null,
    string? Integrita = null,
    string? Sottovuoto = null,
    string? ControlloTemperatura = null,
    string? AccettazioneTemperatura = null,
    string? PrelievoCampione = null,
    string? AnalisiLaboratorio = null,
    string? VerificaOrganolettica = null,
    string? CertificatoNumero = null);

public sealed record FornitoreAccess(
    string Codice,
    string RagioneSociale,
    string? Indirizzo,
    string? Cap,
    string? Localita,
    string? Provincia,
    string? PartitaIva,
    string? Telefono);

/// <summary>«Questa materia si compra da questo fornitore» (tabella Mat-fornitori).</summary>
public sealed record MateriaFornitoreAccess(string Materia, string CodiceFornitore);

/// <summary>Una matrice (categoria di ingrediente) con il suo numero nel vecchio programma.</summary>
public sealed record MatriceAccess(int IdMatrice, string Nome);

public sealed record DatiAccess(
    IReadOnlyList<RigaMagazzino> Magazzino,
    IReadOnlyList<FornitoreAccess> Fornitori,
    IReadOnlyList<MateriaFornitoreAccess> MaterieFornitori,
    IReadOnlyList<string> Matrici,
    IReadOnlyList<MatriceAccess>? MatriciConId = null,
    IReadOnlyList<RigaControlliMatrice>? RigheControlli = null,
    IReadOnlyList<TestoControllo>? TestiControlli = null);

/// <summary>Legge il vecchio programma. Sola lettura: non scrive mai nel file Access.</summary>
public interface ILettoreAccess
{
    DatiAccess Leggi();
}
