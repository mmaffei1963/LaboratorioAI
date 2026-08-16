namespace LaboratorioAI.Core.Modelli;

/// <summary>
/// Valori standard per l'autocontrollo delle piscine, in vigore:
/// limiti da Accordo Stato-Regioni 16/1/2003 (Allegato 1, Tabella A),
/// frequenze da UNI 10637, metodi da Rapporto ISTISAN 07/11.
/// Fonte unica e centrale: si aggiorna qui e vale per tutti i manuali.
///
/// I limiti riportano il valore in vasca e, fra parentesi, quello di
/// immissione quando diverso (il modello Word ha una sola colonna limite).
/// </summary>
public static class StandardPiscine
{
    private const string M = "ISTISAN 07/11 (metodo ISS/Min. Salute) — da confermare";

    /// <summary>Parametri di controllo standard dell'acqua di piscina.</summary>
    public static IReadOnlyList<ParametroControllo> ParametriTabellaA() =>
    [
        // ── Requisiti fisici ─────────────────────────────────────────
        new() { Parametro = "Temperatura (vasche coperte)", UnitaMisura = "°C",
                Limite = "24–30 (imm. 24–32)", Frequenza = "2 volte/giorno (in situ)", Metodo = "Termometria in situ" },
        new() { Parametro = "Temperatura (vasche bambini coperte)", UnitaMisura = "°C",
                Limite = "26–32 (imm. 26–35)", Frequenza = "2 volte/giorno (in situ)", Metodo = "Termometria in situ" },
        new() { Parametro = "Temperatura (vasche scoperte)", UnitaMisura = "°C",
                Limite = "18–30", Frequenza = "2 volte/giorno (in situ)", Metodo = "Termometria in situ" },
        new() { Parametro = "pH", UnitaMisura = "—",
                Limite = "6,5–7,5", Frequenza = "1 volta/giorno (in situ)", Metodo = "Potenziometria in situ" },
        new() { Parametro = "Torbidità", UnitaMisura = "mg/l SiO₂",
                Limite = "≤ 4 (imm. ≤ 2)", Frequenza = "campo settimanale; lab. bimestrale", Metodo = M },
        new() { Parametro = "Solidi sospesi", UnitaMisura = "mg/l",
                Limite = "≤ 4 (imm. ≤ 2)", Frequenza = "lab. quadrimestrale", Metodo = M },
        new() { Parametro = "Colore", UnitaMisura = "mg/l Pt/Co",
                Limite = "≤ 5 oltre approvvig.", Frequenza = "lab. bimestrale", Metodo = M },

        // ── Requisiti chimici ────────────────────────────────────────
        new() { Parametro = "Cloro attivo libero", UnitaMisura = "mg/l Cl₂",
                Limite = "0,7–1,5 (imm. 0,6–1,8)", Frequenza = "~1 h prima apertura, poi ogni ~3 h (in situ)", Metodo = "Colorimetria DPD in situ" },
        new() { Parametro = "Cloro attivo combinato", UnitaMisura = "mg/l Cl₂",
                Limite = "≤ 0,4 (imm. ≤ 0,2)", Frequenza = "con 1ª e ultima analisi cloro libero", Metodo = "Colorimetria DPD in situ" },
        new() { Parametro = "Acido isocianurico", UnitaMisura = "mg/l",
                Limite = "≤ 75", Frequenza = "campo 2 volte/settimana; lab. bimestrale", Metodo = M },
        new() { Parametro = "Sostanze organiche (al permanganato)", UnitaMisura = "mg/l O₂",
                Limite = "≤ 2 oltre immissione", Frequenza = "lab. bimestrale", Metodo = M },
        new() { Parametro = "Nitrati", UnitaMisura = "mg/l NO₃",
                Limite = "≤ 20 oltre approvvig.", Frequenza = "lab. bimestrale", Metodo = M },
        new() { Parametro = "Flocculanti", UnitaMisura = "mg/l (Al o Fe)",
                Limite = "≤ 0,2", Frequenza = "campo settimanale; lab. bimestrale", Metodo = M },

        // ── Requisiti microbiologici (lab. mensile, bimestrale dopo 6 mesi conformi) ──
        new() { Parametro = "Conta batterica a 22 °C", UnitaMisura = "UFC/ml",
                Limite = "≤ 200 (imm. ≤ 100)", Frequenza = "mensile (poi bimestrale)", Metodo = M },
        new() { Parametro = "Conta batterica a 36 °C", UnitaMisura = "UFC/ml",
                Limite = "≤ 100 (imm. ≤ 10)", Frequenza = "mensile (poi bimestrale)", Metodo = M },
        new() { Parametro = "Escherichia coli", UnitaMisura = "UFC/100 ml",
                Limite = "0", Frequenza = "mensile (poi bimestrale)", Metodo = M },
        new() { Parametro = "Enterococchi", UnitaMisura = "UFC/100 ml",
                Limite = "0", Frequenza = "mensile (poi bimestrale)", Metodo = M },
        new() { Parametro = "Staphylococcus aureus", UnitaMisura = "UFC/100 ml",
                Limite = "≤ 1 (imm. 0)", Frequenza = "mensile (poi bimestrale)", Metodo = M },
        new() { Parametro = "Pseudomonas aeruginosa", UnitaMisura = "UFC/100 ml",
                Limite = "≤ 1 (imm. 0)", Frequenza = "mensile (poi bimestrale)", Metodo = M },
    ];
}
