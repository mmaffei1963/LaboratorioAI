namespace LaboratorioAI.Core.Modelli;

// Controllo pubblicazione certificati (16/11/2026): confronto (sola lettura) tra i certificati spediti in ArcLab
// (LabAnalisiDati.dbo.certificato.DataSped) e quelli caricati sul portale clienti (Marea.dbo.Entries, tipo «Certificato»,
// senza gli Allegati A). Aggancio: NCertificato contenuto in NomeFile o Titolo.

/// <summary>Certificato caricato sul portale con date diverse dalla spedizione in ArcLab.</summary>
public record DateDiversePubblicazione(string NCert, DateTime DataSped, DateTime? DataUpload, DateTime? DataFirma,
    int GiorniUpload, int? GiorniFirma, string? Tipo, string? NomeFile)
{
    /// <summary>Firma registrata dopo il caricamento sul portale.</summary>
    public bool FirmatoDopoPubblicazione => DataFirma is DateTime f && DataUpload is DateTime u && f > u;
}

/// <summary>Certificato spedito in ArcLab che non risulta sul portale.</summary>
public record SoloArcLabPubblicazione(string NCert, DateTime DataSped, int? IdTipoCertificato);

/// <summary>File sul portale senza certificato spedito nel periodo (con l'eventuale certificato ArcLab senza DataSped).</summary>
public record SoloPortalePubblicazione(string? Tipo, string? NomeFile, string? Titolo, DateTime? DataUpload, DateTime? DataFirma,
    string? CertificatoArcLab, DateTime? DataSpedArcLab);

/// <summary>Certificato caricato più volte (versioni inglesi escluse dal conteggio dei doppioni).</summary>
public record DoppioniPubblicazione(string NCert, DateTime DataSped, int Caricamenti, int DiCuiInglese, DateTime? PrimoUpload, DateTime? UltimoUpload);

/// <summary>Esito completo del confronto.</summary>
public record EsitoControlloPubblicazione(
    IReadOnlyList<DateDiversePubblicazione> DateDiverse,
    IReadOnlyList<SoloArcLabPubblicazione> SoloArcLab,
    IReadOnlyList<SoloPortalePubblicazione> SoloPortale,
    IReadOnlyList<DoppioniPubblicazione> Doppioni);
