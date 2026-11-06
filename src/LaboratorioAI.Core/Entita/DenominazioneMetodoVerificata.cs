namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Segnalazione di «Ricerca errori → Denominazioni dei metodi» giudicata corretta da una persona (09/10/2026): il metodo
/// ArcLab ha una denominazione che non cita il parametro, o la condivide con altri parametri, ma va bene così (es.
/// «Conta dei microrganismi a 30 °C» per il parametro «Carica batterica totale»). La riga vale solo finché denominazione
/// e parametro restano quelli verificati: se in ArcLab cambiano, il metodo torna tra le segnalazioni.
/// Solo database di LabAI; ArcLab non si modifica. Nessuna cancellazione: «annulla» = Attivo false.
/// </summary>
public class DenominazioneMetodoVerificata
{
    public int IdDenominazioneVerificata { get; set; }

    /// <summary>Metodo.IdMetodo di ArcLab (sola lettura, nessuna vera FK).</summary>
    public int IdMetodoArcLab { get; set; }

    /// <summary>Tipo di segnalazione verificata: «ParametroAssente» o «DenominazioneCondivisa».</summary>
    public string Tipo { get; set; } = "";

    /// <summary>Metodo.DettagliMetodo al momento della verifica.</summary>
    public string? DenominazioneVerificata { get; set; }

    /// <summary>Parametro.NomeParametro al momento della verifica.</summary>
    public string? ParametroVerificato { get; set; }

    public string? Note { get; set; }
    public int? IdUtente { get; set; }
    public string? NomeUtente { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
    public DateTime? DataCancellazione { get; set; }
}
