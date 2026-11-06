namespace LaboratorioAI.Core.Provvigioni;

/// <summary>Nomi leggibili delle voci e dei tipi di accordo (le percentuali stanno negli accordi, non qui).</summary>
public static class EtichetteProvvigioni
{
    public static string Voce(VoceProvvigione v) => v switch
    {
        VoceProvvigione.Nuovo => "Cliente nuovo",
        VoceProvvigione.Mantenimento => "Mantenimento",
        VoceProvvigione.Sviluppo => "Sviluppo (obiettivo)",
        VoceProvvigione.ExtraLaboratorio => "Extra laboratorio procurato (primi 12 mesi)",
        VoceProvvigione.ConvenzioneIndicataAnno1 => "Convenzione indicata, 1° anno",
        VoceProvvigione.ConvenzioneIndicataAnni2a5 => "Convenzione indicata, anni successivi",
        VoceProvvigione.ConvenzioneSegnalata => "Convenzione segnalata da MARE.A",
        VoceProvvigione.AltriServizi => "Altri servizi",
        VoceProvvigione.ContrattoOccasionale => "Contratti occasionali (spot), 1° anno",
        VoceProvvigione.ContrattoAnalisi => "Contratti di analisi, 1° anno",
        _ => v.ToString()
    };

    public static string Tipo(TipoAccordo t) => t switch
    {
        TipoAccordo.Laboratorio => "Laboratorio (nuovo / mantenimento / sviluppo)",
        TipoAccordo.Convenzioni => "Convenzioni pluriennali",
        _ => t.ToString()
    };

    public static string Stato(StatoAccordo s) => s switch
    {
        StatoAccordo.Bozza => "Bozza",
        StatoAccordo.Attivo => "Attivo",
        StatoAccordo.Annullato => "Annullato",
        _ => s.ToString()
    };

    public static string TipoSegnalatore(LaboratorioAI.Core.Entita.TipoSegnalatore t) => t switch
    {
        LaboratorioAI.Core.Entita.TipoSegnalatore.Associazione => "Associazione di categoria",
        LaboratorioAI.Core.Entita.TipoSegnalatore.StudioProfessionale => "Studio professionale",
        LaboratorioAI.Core.Entita.TipoSegnalatore.Persona => "Persona",
        LaboratorioAI.Core.Entita.TipoSegnalatore.Consulente => "Consulente MARE.A",
        LaboratorioAI.Core.Entita.TipoSegnalatore.Azienda => "MARE.A",
        LaboratorioAI.Core.Entita.TipoSegnalatore.Altro => "Altro",
        _ => t.ToString()
    };

    public static string Ambito(LaboratorioAI.Core.Entita.AmbitoCompenso a) => a switch
    {
        LaboratorioAI.Core.Entita.AmbitoCompenso.Commessa => "Sulla commessa",
        LaboratorioAI.Core.Entita.AmbitoCompenso.Area => "Su tutta l'area",
        _ => a.ToString()
    };
}
