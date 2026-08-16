using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <inheritdoc cref="IServizioValidazioneBilancia" />
public class ServizioValidazioneBilancia : IServizioValidazioneBilancia
{
    private readonly IServizioBilancia _calcolo;

    public ServizioValidazioneBilancia(IServizioBilancia calcolo) => _calcolo = calcolo;

    public EsitoValidazioneCalcoli Esegui(string versioneApplicazione)
    {
        double toll = SetRiferimentoBilancia.Tolleranza;
        var dettagli = new List<ConfrontoParametro>();
        int ok = 0, ko = 0;

        void Verifica(string punto, string nome, double c, double atteso)
        {
            bool esito = Math.Abs(c - atteso) <= toll;
            dettagli.Add(new ConfrontoParametro { Caso = "EURAMET cg-18", Punto = punto, Parametro = nome, Calcolato = c, Atteso = atteso, Ok = esito });
            if (esito) ok++; else ko++;
        }

        var r = _calcolo.Calcola(SetRiferimentoBilancia.Caso());

        Verifica("—", "Eccentricità scostamento max [g]", r.EccentricitaScostamentoMax, SetRiferimentoBilancia.EccentricitaScostamentoMax);
        Verifica("—", "Ripetibilità scarto tipo [g]", r.RipetibilitaScartoTipo, SetRiferimentoBilancia.RipetibilitaScartoTipo);
        Verifica("—", "Ripetibilità composta [g]", r.RipetibilitaComposta, SetRiferimentoBilancia.RipetibilitaComposta);

        var attesi = SetRiferimentoBilancia.Attesi();
        for (int i = 0; i < attesi.Count && i < r.Punti.Count; i++)
        {
            var p = r.Punti[i]; var a = attesi[i];
            string carico = p.CaricoNominale.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("it-IT")) + " g";
            Verifica(carico, "u(E) [g]", p.IncertezzaTipoErrore, a.IncertezzaTipoErrore);
            Verifica(carico, "U(E) [g]", p.IncertezzaEstesa, a.IncertezzaEstesa);
            Verifica(carico, "U d'uso [g]", p.IncertezzaEstesaUso, a.IncertezzaEstesaUso);
        }

        var casi = new List<RiepilogoCasoValidazione>
        {
            new() { Nome = "EURAMET cg-18", Descrizione = "Esempio della guida (pag. 64)", Ok = ok, Ko = ko }
        };

        return new EsitoValidazioneCalcoli
        {
            DataEsecuzione = DateTime.UtcNow,
            VersioneSet = SetRiferimentoBilancia.Versione,
            VersioneApplicazione = versioneApplicazione,
            Tolleranza = toll,
            Validato = ko == 0,
            NumeroOk = ok,
            NumeroKo = ko,
            Casi = casi,
            Dettagli = dettagli
        };
    }
}
