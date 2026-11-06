namespace LaboratorioAI.Tracciabilita.Core.Sicurezza;

/// <summary>
/// Entità che appartiene a un'azienda cliente. Il contesto dati le filtra
/// automaticamente: chi lavora per un'azienda non vede mai righe di un'altra.
/// </summary>
public interface IDellAzienda
{
    int IdAzienda { get; set; }
}

/// <summary>Come <see cref="IDellAzienda"/>, ma l'azienda può mancare (utenti gestore, voci di audit del gestore).</summary>
public interface IDellAziendaOpzionale
{
    int? IdAzienda { get; set; }
}

/// <summary>
/// Registro che si scrive e basta: niente modifiche, niente cancellazioni.
/// Una correzione è sempre una riga nuova (storno).
/// </summary>
public interface IAppendOnly
{
}

/// <summary>Chi sta lavorando e per quale azienda. Fornito dal sito (sessione) o dai servizi interni.</summary>
public interface IContestoAzienda
{
    /// <summary>Azienda per cui si lavora. Assente = nessun dato visibile (comportamento sicuro).</summary>
    int? IdAzienda { get; }

    int? IdUtente { get; }

    /// <summary>Insediamento su cui si sta lavorando, se scelto.</summary>
    int? IdInsediamento { get; }

    /// <summary>Personale MARE.A che gestisce il servizio: vede tutte le aziende.</summary>
    bool Gestore { get; }
}

/// <summary>Contesto del gestore del servizio, per operazioni interne (avvio, migrazioni, chiusure).</summary>
public sealed class ContestoGestore : IContestoAzienda
{
    public int? IdAzienda => null;
    public int? IdUtente => null;
    public int? IdInsediamento => null;
    public bool Gestore => true;
}

/// <summary>Contesto di un'azienda precisa, con o senza utente (usato dai test e dai servizi in background).</summary>
public sealed class ContestoDiAzienda : IContestoAzienda
{
    public ContestoDiAzienda(int idAzienda, int? idUtente = null, int? idInsediamento = null)
    {
        IdAzienda = idAzienda;
        IdUtente = idUtente;
        IdInsediamento = idInsediamento;
    }

    public int? IdAzienda { get; }
    public int? IdUtente { get; }
    public int? IdInsediamento { get; }
    public bool Gestore => false;
}

/// <summary>Nessuna azienda e nessun gestore: non si vede niente (stato predefinito prima dell'accesso).</summary>
public sealed class ContestoAnonimo : IContestoAzienda
{
    public int? IdAzienda => null;
    public int? IdUtente => null;
    public int? IdInsediamento => null;
    public bool Gestore => false;
}
