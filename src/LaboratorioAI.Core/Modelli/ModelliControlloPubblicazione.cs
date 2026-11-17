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

/// <summary>
/// Certificato spedito in ArcLab che non risulta sul portale nel periodo. 16/11/2026: con il file trovato sul portale in
/// qualsiasi data e di qualsiasi tipo (prima il certificato, poi l'Allegato A), per distinguere «manca del tutto»,
/// «c'è solo l'Allegato A» e «caricato in un'altra data» (per esempio data di spedizione aggiornata dopo mesi).
/// </summary>
public record SoloArcLabPubblicazione(string NCert, DateTime DataSped, int? IdTipoCertificato,
    string? TipoPortale, string? NomeFilePortale, DateTime? DataUploadPortale)
{
    public string SulPortale =>
        NomeFilePortale is null ? "non trovato"
        : TipoPortale is not null && TipoPortale.Contains("Allegato", StringComparison.OrdinalIgnoreCase)
            ? $"solo l'Allegato A (caricato il {DataUploadPortale:dd/MM/yyyy}): manca il certificato"
        : $"caricato il {DataUploadPortale:dd/MM/yyyy}, fuori periodo: spedizione in ArcLab diversa di {(DataSped.Date - DataUploadPortale!.Value.Date).Days} giorni";
}

/// <summary>File sul portale senza certificato spedito nel periodo (con l'eventuale certificato ArcLab senza DataSped).</summary>
public record SoloPortalePubblicazione(string? Tipo, string? NomeFile, string? Titolo, DateTime? DataUpload, DateTime? DataFirma,
    string? CertificatoArcLab, DateTime? DataSpedArcLab);

/// <summary>Certificato caricato più volte (versioni inglesi escluse dal conteggio dei doppioni).</summary>
public record DoppioniPubblicazione(string NCert, DateTime DataSped, int Caricamenti, int DiCuiInglese, DateTime? PrimoUpload, DateTime? UltimoUpload);

/// <summary>
/// 17/11/2026: insediamento con certificati spediti nel periodo (Controllo pubblicazione) o con campioni recenti (pagina
/// «Clienti e insediamenti», senza date) che sul portale non può riceverli (insediamento o cliente
/// mancante, disattivato o collegato al cliente sbagliato). Il certificato caricato non arriva al cliente.
/// </summary>
public record AnagraficaNonAllineata(int IdCliente, string? RagioneSociale, string? CodContabilita, int IdInsediamento,
    string? InsediamentoArcLab, int Campioni, DateTime UltimoRicevimento, int Certificati, DateTime? UltimaSpedizione, int? IdInsediamentoPortale, string? InsediamentoPortale,
    int? IdClientePortale, string? ClientePortale, string Problema, bool ClienteAttivoArcLab, bool InsediamentoInUsoArcLab)
{
    /// <summary>Disattivato (obsoleto) sul portale, come insediamento o come cliente.</summary>
    public bool DisattivatoSulPortale => Problema is "InsediamentoDisattivato" or "ClienteDisattivato";

    /// <summary>Stato in ArcLab (Clienti.Attivo, Insediamento.NonInUso).</summary>
    public string StatoArcLab => (ClienteAttivoArcLab, InsediamentoInUsoArcLab) switch
    {
        (true, true) => "attivo",
        (false, true) => "cliente non attivo",
        (true, false) => "insediamento non in uso",
        _ => "cliente non attivo, insediamento non in uso"
    };

    /// <summary>Descrizione del problema e di cosa fare sul portale.</summary>
    public string Descrizione => Problema switch
    {
        "MancaTutto" => "mancano cliente e insediamento sul portale",
        "MancaInsediamento" => "manca l'insediamento sul portale",
        "InsediamentoDisattivato" => "insediamento disattivato (obsoleto) sul portale",
        "ClienteDisattivato" => "cliente disattivato (obsoleto) sul portale",
        "AltroClienteArcLab" => "sul portale l'insediamento è collegato a un altro cliente ArcLab",
        "ClientePortaleNonCollegato" => "sul portale l'insediamento è sotto un cliente senza il codice ArcLab giusto",
        _ => Problema
    };
}

/// <summary>Esito completo del confronto.</summary>
public record EsitoControlloPubblicazione(
    IReadOnlyList<DateDiversePubblicazione> DateDiverse,
    IReadOnlyList<SoloArcLabPubblicazione> SoloArcLab,
    IReadOnlyList<SoloPortalePubblicazione> SoloPortale,
    IReadOnlyList<DoppioniPubblicazione> Doppioni,
    IReadOnlyList<AnagraficaNonAllineata>? Anagrafiche = null);
