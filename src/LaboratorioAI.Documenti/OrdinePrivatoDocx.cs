using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using static LaboratorioAI.Documenti.OrdineAccettazioneDocx;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Ordine di Accettazione per i clienti privati (Mod. Com. 05B, 06/11/2026), con lo stesso aspetto degli Ordini di
/// Accettazione ma i testi delle Condizioni per i privati (Mod. Com. 05A): prezzi IVA inclusa con imponibile e IVA,
/// prelievo del Cliente, sottoscrizione, approvazione specifica (artt. 1341-1342 c.c.) e consenso facoltativo.
///
/// Tre origini, alternative al banco:
///  • Ordine in bianco (da compilare a mano), con l'Allegato 1 dei controlli di «Spettabile cliente»;
///  • richiesta compilata nella maschera «Richieste analisi privati»;
///  • lotto ArcLab del cliente MAF2 («Spettabile cliente») dagli Ordini di Accettazione: il nome del privato è in
///    Campione.LuogoPrelievo e si scrive dopo «Spettabile cliente».
/// </summary>
public static class OrdinePrivatoDocx
{
    public const string PercorsoModello = "docx/Carta_intestata_Marea.docx";

    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private const string Linea = "______________________________";

    // ── Modello neutro del documento ─────────────────────────────────

    /// <summary>Riga di prova dell'allegato tecnico.</summary>
    public record Prova(string Denominazione, string? Metodo, bool Accreditato, string? Laboratorio, string? FraseLaboratorio);

    /// <summary>Controllo con le sue prove.</summary>
    public record ControlloProve(string Titolo, IReadOnlyList<Prova> Prove);

    public sealed class CampioneOrdine
    {
        public string Titolo { get; init; } = "CAMPIONE";
        public List<(string Etichetta, string Valore)> Dati { get; init; } = new();
        public List<ControlloProve> Controlli { get; init; } = new();
        /// <summary>Al posto delle prove (Ordine in bianco).</summary>
        public string? NotaProve { get; init; }
    }

    public sealed class DatiOrdine
    {
        /// <summary>Es. «Richiesta n. PRIV-2026/003» o «Lotto n. 12345».</summary>
        public string Numero { get; init; } = "";
        public string Emissione { get; init; } = "";
        public List<string> Destinatario { get; init; } = new();
        /// <summary>Righe del riepilogo: riferimento, voce, prezzo IVA inclusa (null = da scrivere a mano).</summary>
        public List<(string Rif, string Voce, decimal? PrezzoIvaInclusa)> Riepilogo { get; init; } = new();
        public int RigheVuoteRiepilogo { get; init; }
        public decimal? Totale { get; init; }
        public decimal? Imponibile { get; init; }
        public decimal AliquotaIva { get; init; } = 22m;
        /// <summary>true = alla consegna del Campione, false = alla consegna del RdP, null = caselle vuote.</summary>
        public bool? PagamentoAllaConsegnaCampione { get; init; }
        /// <summary>true = ritiro in Laboratorio, false = e-mail, null = caselle vuote.</summary>
        public bool? RitiroInLaboratorio { get; init; }
        public bool EmailERitiro { get; init; }
        public string? Email { get; init; }
        public List<CampioneOrdine> Campioni { get; init; } = new();
        public bool? Consenso { get; init; }
        public string? PerMarea { get; init; }
        public IReadOnlyList<ControlloAllegato>? Allegato1 { get; init; }
    }

    /// <summary>Controllo delle commesse di «Spettabile cliente» per l'Allegato 1 dell'Ordine in bianco.</summary>
    public record ControlloAllegato(int IdControllo, string Controllo, decimal PrezzoIvaInclusa, int TempiGiorni,
        IReadOnlyList<MetodoRichiestaPrivato> Metodi);

    // ── Origini ──────────────────────────────────────────────────────

    public static string NomeFile(RichiestaAnalisiPrivato? r) =>
        r is null ? "Ordine_accettazione_privati_in_bianco.docx"
                  : $"Ordine_accettazione_privati_{r.Anno}_{r.Numero:000}.docx";

    /// <summary>Dalla maschera «Richieste analisi privati»; <c>r = null</c> = Ordine in bianco con l'Allegato 1.</summary>
    public static DatiOrdine DaRichiesta(RichiestaAnalisiPrivato? r, TimeZoneInfo fuso, string? perMarea,
        IReadOnlyList<ControlloAllegato>? allegato1 = null)
    {
        if (r is null)
            return new DatiOrdine
            {
                Numero = "Richiesta n. ________", Emissione = "__/__/____",
                Destinatario = { "Gentile Cliente  " + Linea, "Indirizzo: " + Linea + Linea, "Codice fiscale: " + Linea + "    Telefono: " + Linea },
                RigheVuoteRiepilogo = 4,
                Campioni =
                {
                    new CampioneOrdine
                    {
                        Titolo = "CAMPIONE",
                        Dati =
                        {
                            ("Descrizione", Linea), ("Punto di prelievo", Linea), ("Data e ora del prelievo", Linea),
                            ("Contenitore", "{C} fornito dal Laboratorio     {C} del Cliente"), ("Data accettazione", Linea),
                            ("T° di ricevimento", Linea), ("Prelievo eseguito da", "Cliente"),
                            ("Comunicazione anticipata dei fuori limite", "{C} Non richiesta     {C} Richiesta"),
                            ("Giudizio di conformità e pareri", "{C} Non richiesti     {C} Richiesti rispetto a " + Linea),
                            ("Tempi di consegna", "________ giorni lavorativi"),
                        },
                        NotaProve = allegato1 is { Count: > 0 }
                            ? "Prove: quelle dei controlli spuntati nell'Allegato 1, riportati nel Riepilogo con il prezzo IVA inclusa."
                            : "Prove: quelle dei controlli indicati nel Riepilogo."
                    }
                },
                Allegato1 = allegato1
            };

        var numero = RegoleRichiestePrivati.NumeroFormattato(r.Anno, r.Numero);
        var oggi = Locale(r.DataCreazione, fuso).ToString("dd/MM/yyyy", It);
        var dest = new List<string> { "Gentile Cliente  " + r.NomeCognome };
        if (!string.IsNullOrWhiteSpace(r.Indirizzo)) dest.Add(r.Indirizzo!.Trim());
        var cf = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.CodiceFiscale)) cf.Add("C.F. " + r.CodiceFiscale!.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(r.Telefono)) cf.Add("Tel. " + r.Telefono!.Trim());
        if (cf.Count > 0) dest.Add(string.Join("    ", cf));

        var righe = r.Righe.OrderBy(x => x.Progressivo).ToList();
        var riepilogo = righe.Select(x => ($"{numero}/1", $"{x.Descrizione} (ID controllo {x.IdControlloArcLab})", (decimal?)x.PrezzoIvaInclusa)).ToList();
        if (r.GiudizioRichiesto) riepilogo.Add(($"{numero}/1", "Giudizio di conformità (supplemento)", r.SupplementoGiudizio));

        string? data = null;
        if (r.DataOraPrelievo is DateTime dp)
        {
            var l = Locale(dp, fuso);
            data = l.ToString(l.TimeOfDay == TimeSpan.Zero ? "dd/MM/yyyy" : "dd/MM/yyyy 'ore' HH:mm", It);
        }
        var contenitore = string.Join(" – ", new[] { r.Contenitore?.Trim(),
            r.ContenitoreDelLaboratorio switch { true => "fornito dal Laboratorio", false => "del Cliente", _ => null } }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        return new DatiOrdine
        {
            Numero = "Richiesta n. " + numero, Emissione = oggi, Destinatario = dest, Riepilogo = riepilogo,
            Totale = r.TotaleIvaInclusa, Imponibile = r.TotaleImponibile, AliquotaIva = r.AliquotaIva,
            PagamentoAllaConsegnaCampione = r.Pagamento == PagamentoPrivato.AllaConsegnaCampione,
            RitiroInLaboratorio = r.RitiroInLaboratorio, Email = r.Email, Consenso = r.ConsensoComunicazioni, PerMarea = perMarea,
            Campioni =
            {
                new CampioneOrdine
                {
                    Titolo = $"{numero}/1 — {(string.IsNullOrWhiteSpace(r.DescrizioneCampione) ? "CAMPIONE" : r.DescrizioneCampione!.ToUpperInvariant())}",
                    Dati =
                    {
                        ("Riferimento", $"{numero}/1"), ("Descrizione", T(r.DescrizioneCampione)), ("Punto di prelievo", T(r.PuntoPrelievo)),
                        ("Data e ora del prelievo", data ?? "—"), ("Contenitore", T(contenitore)), ("Data accettazione", oggi),
                        ("T° di ricevimento", r.TemperaturaRicevimento is decimal tr ? tr.ToString("0.0", It) + " °C" : "—"),
                        ("Prelievo eseguito da", "Cliente"),
                        ("Comunicazione anticipata dei fuori limite", r.ComunicazioneFuoriLimite ? "Richiesta dal Cliente" : "Non richiesta dal Cliente"),
                        ("Giudizio di conformità e pareri", r.GiudizioRichiesto
                            ? "Richiesti dal Cliente" + (string.IsNullOrWhiteSpace(r.NormaGiudizio) ? "" : " – rispetto a " + r.NormaGiudizio!.Trim())
                            : "Non richiesti"),
                        ("Tempi di consegna", $"{r.TempiConsegnaGiorni} giorni lavorativi"),
                    },
                    Controlli = righe.Select(x => new ControlloProve(
                        $"Analisi – {x.Descrizione} (ID controllo {x.IdControlloArcLab})",
                        ModuloRichiestaPrivatiDocx.Metodi(x).Select(m => new Prova(m.Denominazione, m.Metodo, m.Accreditato,
                            m.IdLabEsterno is null ? null : (m.LabEsterno ?? "laboratorio esterno"), m.LabEsternoFrase)).ToList())).ToList()
                }
            }
        };
    }

    /// <summary>
    /// Da un lotto ArcLab del cliente «Spettabile cliente» (MAF2) negli Ordini di Accettazione. Il privato è indicato in
    /// Campione.LuogoPrelievo e si scrive dopo il cliente; pagamento e consenso restano da spuntare a mano.
    /// </summary>
    public static DatiOrdine DaLotto(OrdineAccettazione o, string? perMarea)
    {
        var aliquota = o.AliquotaIva;
        var privati = o.Campioni.Select(c => c.LuogoPrelievo?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var dest = new List<string>
        {
            "Gentile Cliente  " + (o.ClienteRagioneSociale ?? "Spettabile cliente") + (privati.Count > 0 ? " – " + string.Join(", ", privati) : "")
        };
        var ricezione = o.DataRicezione is DateTime dr
            ? dr.ToString("dd/MM/yyyy", It) + (o.OraRicezione is TimeSpan ora ? " ore " + ora.ToString(@"hh\:mm", It) : "")
            : "—";

        var riepilogo = o.Campioni.Select(c => (c.Riferimento,
            string.IsNullOrWhiteSpace(c.Controllo)
                ? (c.Denominazione ?? c.Matrice ?? "Campione").Trim()
                : c.Controllo!.Trim() + (c.IdControllo is int ic ? $" (ID controllo {ic})" : ""),
            c.Prezzo is decimal p ? (decimal?)RegoleRichiestePrivati.IvaInclusa(p, aliquota) : null)).ToList();
        var totale = riepilogo.Sum(x => x.Item3 ?? 0m);
        var imponibile = o.Campioni.Sum(c => c.Prezzo ?? 0m);

        var firma = o.Firma;
        bool? ritiro = firma?.Consegna switch
        {
            ConsegnaRapportoPrivato.AMano => true,
            ConsegnaRapportoPrivato.Email => false,
            _ => null
        };

        return new DatiOrdine
        {
            Numero = "Lotto n. " + o.CodLotto, Emissione = DateTime.Today.ToString("dd/MM/yyyy", It),
            Destinatario = dest, Riepilogo = riepilogo,
            Totale = o.Campioni.Any(c => c.Prezzo is not null) ? totale : null,
            Imponibile = o.Campioni.Any(c => c.Prezzo is not null) ? imponibile : null,
            AliquotaIva = aliquota,
            RitiroInLaboratorio = ritiro, EmailERitiro = firma?.Consegna == ConsegnaRapportoPrivato.Entrambi,
            Email = firma?.EmailRapporto, PerMarea = perMarea ?? firma?.PerMarea,
            Campioni = o.Campioni.Select(c => new CampioneOrdine
            {
                Titolo = $"{c.Riferimento} — {(c.Denominazione ?? c.Matrice ?? "CAMPIONE").Trim().ToUpperInvariant()}",
                Dati =
                {
                    ("Riferimento", c.Riferimento), ("Descrizione", T(c.Denominazione)), ("Matrice", T(c.Matrice)),
                    ("Punto di prelievo", T(c.PuntoPrelievo)),
                    ("Data del prelievo", o.DataPrelievo is DateTime dp ? dp.ToString("dd/MM/yyyy", It) : "—"),
                    ("Contenitore", T(c.Imballaggio)), ("Data accettazione", ricezione),
                    ("T° di ricevimento", T(c.TemperaturaRicevimento)), ("Prelievo eseguito da", "Cliente"),
                    ("Comunicazione anticipata dei fuori limite", c.ComunicazioneAnticipataTesto ?? "Non richiesta dal Cliente"),
                    ("Giudizio di conformità e pareri", o.GiudizioConformitaTesto ?? "Non richiesti"),
                },
                Controlli =
                {
                    new ControlloProve(
                        "Analisi" + (string.IsNullOrWhiteSpace(c.Controllo) ? "" : $" – {c.Controllo!.Trim()}" + (c.IdControllo is int ic ? $" (ID controllo {ic})" : "")),
                        c.AnalisiRaggruppate().Select(x => new Prova(CampioneAccettazione.EtichettaRaggruppata(x.Analisi, x.Numero),
                            x.Analisi.Metodo, x.Analisi.Accreditato,
                            string.IsNullOrWhiteSpace(x.Analisi.LaboratorioEsterno) ? null : x.Analisi.LaboratorioEsterno!.Trim(),
                            x.Analisi.FraseLabEsterno)).ToList())
                }
            }).ToList()
        };
    }

    // ── Documento ────────────────────────────────────────────────────

    /// <summary>Word sul modello di carta intestata. <paramref name="perPdf"/>: caselle con glifi presenti nel font del PDF.</summary>
    public static byte[] GeneraWord(DatiOrdine d, string percorsoModello, bool perPdf = false)
    {
        string Vuota = perPdf ? "□" : "☐", Piena = perPdf ? "■" : "☒";
        string C(bool? x) => x == true ? Piena : Vuota;
        string Caselle(string s) => s.Replace("{C}", Vuota);

        using var ms = new MemoryStream();
        using (var fs = File.OpenRead(percorsoModello)) fs.CopyTo(ms);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var sectPr = body.Elements<SectionProperties>().LastOrDefault();
            var pm = sectPr?.Elements<PageMargin>().FirstOrDefault();
            if (pm is not null) { pm.Left = MargineLatoTwips; pm.Right = MargineLatoTwips; }
            void Aggiungi(OpenXmlElement e) { if (sectPr is not null) body.InsertBefore(e, sectPr); else body.AppendChild(e); }

            // ── Titolo e destinatario ──
            Aggiungi(Para("ORDINE DI ACCETTAZIONE", bold: true, size: 28, align: JustificationValues.Center, spazioPrima: 120, spazioDopo: 60));
            Aggiungi(Para($"{d.Numero}        Emesso il {d.Emissione}", size: 18, align: JustificationValues.Center, spazioDopo: 20));
            Aggiungi(Para($"{RegoleRichiestePrivati.ModuloOrdine} – Clienti privati", size: 15, italic: true, align: JustificationValues.Center, spazioDopo: 160));
            for (var i = 0; i < d.Destinatario.Count; i++)
                Aggiungi(Para(d.Destinatario[i], bold: i == 0, size: 20, spazioDopo: i == d.Destinatario.Count - 1 ? 120 : 0));
            Aggiungi(Para(d.Campioni.Count > 1 ? "OGGETTO: ACCETTAZIONE CAMPIONI" : "OGGETTO: ACCETTAZIONE CAMPIONE", bold: true, spazioDopo: 120));

            // ── Riepilogo (prezzi IVA inclusa) ──
            Aggiungi(Titolo("RIEPILOGO"));
            var t = Tabella(2000, 5900, 1738);
            t.Append(RigaTab(true, "F3F3F3", ("Riferimento", 2000, false), ("Controllo", 5900, false), ("Prezzo IVA incl.", 1738, true)));
            foreach (var (rif, voce, prezzo) in d.Riepilogo)
                t.Append(RigaTab(false, null, (rif, 2000, false), (voce, 5900, false), (prezzo is decimal p ? $"€ {Fmt(p)}" : "—", 1738, true)));
            for (var i = 0; i < d.RigheVuoteRiepilogo; i++)
                t.Append(RigaTab(false, null, (" ", 2000, false), (" ", 5900, false), (" ", 1738, true)));
            Aggiungi(t);

            var te = Tabella(false, 7200, 2000);
            if (d.Totale is decimal tot && d.Imponibile is decimal imp)
            {
                te.Append(RigaImporto("TOTALE (IVA compresa)", tot, true));
                te.Append(RigaImporto("di cui imponibile", imp, false));
                te.Append(RigaImporto($"di cui IVA {Fmt(d.AliquotaIva)}%", tot - imp, false));
            }
            else
            {
                te.Append(RigaTesto("TOTALE (IVA compresa)", "€ __________", true));
                te.Append(RigaTesto("di cui imponibile", "€ __________", false));
                te.Append(RigaTesto($"di cui IVA {Fmt(d.AliquotaIva)}%", "€ __________", false));
            }
            Aggiungi(te);
            Aggiungi(Para($"Pagamento:   {C(d.PagamentoAllaConsegnaCampione == true)} alla consegna del Campione     " +
                          $"{C(d.PagamentoAllaConsegnaCampione == false)} alla consegna del Rapporto di Prova",
                          size: 18, spazioPrima: 80, spazioDopo: 40));

            // ── Consegna del Rapporto di Prova ──
            Aggiungi(Titolo("CONSEGNA DEL RAPPORTO DI PROVA"));
            Aggiungi(Para($"Modalità:   {C(d.RitiroInLaboratorio == false || d.EmailERitiro)} per e-mail     " +
                          $"{C(d.RitiroInLaboratorio == true || d.EmailERitiro)} ritiro presso il Laboratorio", size: 18, spazioDopo: 40));
            Aggiungi(Para("E-mail per l'invio del Rapporto di Prova: " +
                          (string.IsNullOrWhiteSpace(d.Email) ? Linea + Linea : d.Email!.Trim()), size: 18, spazioDopo: 120));

            // ── Riquadro condizioni (Condizioni per i privati) ──
            Aggiungi(Riquadro(TestiOrdinePrivato.TitoloRiquadro, new[]
            {
                ((string?)TestiOrdinePrivato.TitoloVerifica, TestiOrdinePrivato.Verifica),
                (TestiOrdinePrivato.TitoloPrelievo, TestiOrdinePrivato.Prelievo),
                (TestiOrdinePrivato.TitoloRapporto, TestiOrdinePrivato.Rapporto),
                (TestiOrdinePrivato.TitoloCondizioni, TestiOrdinePrivato.TestoCondizioni),
            }));
            Aggiungi(Para("", spazioDopo: 60));

            // ── Allegato tecnico ──
            Aggiungi(Para("ALLEGATO TECNICO", bold: true, size: 24, spazioPrima: 240, spazioDopo: 120, keepNext: true));
            Aggiungi(DoppiaRiga());

            var labs = Laboratori(d.Campioni.SelectMany(c => c.Controlli).SelectMany(c => c.Prove));
            foreach (var c in d.Campioni)
            {
                Aggiungi(Titolo(c.Titolo));
                var dt = Tabella(false, 4080, 5558);
                foreach (var (et, val) in c.Dati) DatoRiga(dt, et, Caselle(val));
                Aggiungi(dt);
                if (c.NotaProve is not null)
                    Aggiungi(Para(c.NotaProve, size: 18, italic: true, spazioPrima: 100, spazioDopo: 40));
                foreach (var ctrl in c.Controlli)
                {
                    Aggiungi(Para(ctrl.Titolo, bold: true, size: 20, spazioPrima: 120, spazioDopo: 40, keepNext: true));
                    Aggiungi(TabellaProve(ctrl.Prove, labs));
                }
            }
            NoteLaboratori(labs, Aggiungi);

            // ── Firme ──
            Aggiungi(Para("SOTTOSCRIZIONE", bold: true, size: 20, spazioPrima: 240, spazioDopo: 40, keepNext: true));
            Aggiungi(Para(TestiOrdinePrivato.Firma, size: 16, align: JustificationValues.Both, spazioDopo: 80, keepNext: true));
            Aggiungi(TabellaFirme(d.PerMarea));
            Aggiungi(Para("", spazioDopo: 120));
            Aggiungi(Riquadro(TestiOrdinePrivato.TitoloApprovazione, new[] { ((string?)null, TestiOrdinePrivato.Approvazione) },
                firma: "Luogo e data " + Linea + "        Firma del Cliente " + Linea, unito: true));
            Aggiungi(Para("", spazioDopo: 120));
            var consenso = $"{TestiOrdinePrivato.DomandaConsenso}     {C(d.Consenso == true)} SÌ     {C(d.Consenso == false)} NO";
            Aggiungi(Riquadro(TestiOrdinePrivato.TitoloConsenso, new[] { ((string?)null, TestiOrdinePrivato.Consenso), ((string?)null, consenso) },
                firma: "Luogo e data " + Linea + "        Firma del Cliente " + Linea, unito: true));

            if (d.Allegato1 is { Count: > 0 })
                foreach (var e in Allegato1(d.Allegato1, Vuota)) Aggiungi(e);

            doc.MainDocumentPart.Document.Save();

            foreach (var footerPart in doc.MainDocumentPart.FooterParts)
            {
                var para = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
                para.Append(RunPiede(PrefissoPiede(d)));
                var fPag = new SimpleField { Instruction = "PAGE" }; fPag.Append(RunPiede("1"));
                var fTot = new SimpleField { Instruction = "NUMPAGES" }; fTot.Append(RunPiede("1"));
                para.Append(fPag); para.Append(RunPiede(" di ")); para.Append(fTot);
                footerPart.Footer.Append(para);
                footerPart.Footer.Save();
            }
        }
        return ms.ToArray();
    }

    /// <summary>PDF dello stesso documento (conversione del Word, come deleghe e contratti di consulenza).</summary>
    public static byte[] GeneraPdf(DatiOrdine d, string percorsoModello, string? percorsoWwwRoot) =>
        ConvertitoreWordPdf.Converti(GeneraWord(d, percorsoModello, perPdf: true), percorsoWwwRoot, PrefissoPiede(d));

    private static string PrefissoPiede(DatiOrdine d) => $"{RegoleRichiestePrivati.ModuloOrdine} – {d.Numero} – Pagina ";

    // ── Prove e laboratori terzi ─────────────────────────────────────

    private static List<(string Nome, string? Frase)> Laboratori(IEnumerable<Prova> prove)
    {
        var labs = new List<(string Nome, string? Frase)>();
        foreach (var p in prove)
            if (p.Laboratorio is string l && labs.All(x => !x.Nome.Equals(l, StringComparison.OrdinalIgnoreCase)))
                labs.Add((l, p.FraseLaboratorio));
        return labs;
    }

    private static Table TabellaProve(IReadOnlyList<Prova> prove, List<(string Nome, string? Frase)> labs)
    {
        var ta = Tabella(4650, 4140, 848);
        ta.Append(RigaTab(true, "F7F7F7", ("Denominazione", 4650, false), ("Metodo", 4140, false), ("Accr.", 848, false)));
        foreach (var p in prove)
        {
            var accr = p.Laboratorio is string l
                ? Simbolo(labs.Count, labs.FindIndex(x => x.Nome.Equals(l, StringComparison.OrdinalIgnoreCase)) + 1)
                : (p.Accreditato ? "Sì" : "—");
            ta.Append(RigaTab(false, null, (p.Denominazione, 4650, false), (p.Metodo ?? "—", 4140, false), (accr, 848, false)));
        }
        if (prove.Count == 0) ta.Append(RigaTab(false, null, ("Nessuna prova associata.", 4650, false), ("", 4140, false), ("", 848, false)));
        return ta;
    }

    private static void NoteLaboratori(List<(string Nome, string? Frase)> labs, Action<OpenXmlElement> aggiungi)
    {
        for (var i = 0; i < labs.Count; i++)
            aggiungi(Para($"{Simbolo(labs.Count, i + 1)} {Privati(TestiOfferta.FraseSubappalto(labs[i].Frase, labs[i].Nome))}", size: 16, spazioPrima: i == 0 ? 80 : 0));
        if (labs.Count > 0)
            aggiungi(Para(Privati(TestoVerificaAccredia(labs.Count)), bold: true, size: 17, shadingFill: "FFF3D6", spazioPrima: 120, spazioDopo: 120));
    }

    // ── Allegato 1 (Ordine in bianco) ────────────────────────────────

    /// <summary>
    /// I controlli delle commesse in vigore di «Spettabile cliente» (MAF2), con la casella da spuntare, il prezzo IVA
    /// inclusa, i tempi e le prove nello stesso formato dell'allegato tecnico. Il totale si scrive a mano nel Riepilogo.
    /// </summary>
    private static IEnumerable<OpenXmlElement> Allegato1(IReadOnlyList<ControlloAllegato> controlli, string casella)
    {
        yield return OrdineAccettazioneDocx.Para("ALLEGATO 1", bold: true, size: 24, spazioDopo: 40, pageBreakBefore: true);
        yield return Para("CONTROLLI DISPONIBILI PER I CLIENTI PRIVATI", bold: true, size: 20, spazioDopo: 40);
        yield return Para("Spuntare i controlli richiesti e riportarli nel Riepilogo con il prezzo; il totale si scrive a mano. " +
                          "Prezzi IVA inclusa; tempi di consegna indicativi in giorni lavorativi dalla consegna del Campione (Art. 6.1). " +
                          "Le prove indicate con (§) sono eseguite da un laboratorio terzo (Art. 4.2).", size: 16, italic: true, spazioDopo: 120);
        yield return DoppiaRiga();

        var prove = controlli.ToDictionary(c => c, c => (IReadOnlyList<Prova>)c.Metodi.Select(m => new Prova(m.Denominazione, m.Metodo, m.Accreditato,
            m.IdLabEsterno is null ? null : (m.LabEsterno ?? "laboratorio esterno"), m.LabEsternoFrase)).ToList());
        var labs = Laboratori(prove.Values.SelectMany(x => x));
        foreach (var c in controlli)
        {
            yield return Para($"{casella}  {c.Controllo} (ID controllo {c.IdControllo})", bold: true, size: 19, spazioPrima: 160, spazioDopo: 0, keepNext: true);
            yield return Para($"€ {Fmt(c.PrezzoIvaInclusa)} IVA inclusa – {c.TempiGiorni} giorni lavorativi", size: 17, spazioDopo: 40, keepNext: true);
            yield return TabellaProve(prove[c], labs);
        }
        var note = new List<OpenXmlElement>();
        NoteLaboratori(labs, note.Add);
        foreach (var n in note) yield return n;
    }

    // ── Helper ───────────────────────────────────────────────────────

    private static string T(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();

    /// <summary>Nei testi comuni (subappalto) il rimando alle Condizioni Generali diventa quello alle Condizioni per i privati.</summary>
    private static string Privati(string testo) =>
        testo.Replace("Condizioni Generali di Fornitura", $"Condizioni di Fornitura per i clienti privati ({RegoleRichiestePrivati.CondizioniPrivati})");

    private static DateTime Locale(DateTime utc, TimeZoneInfo fuso) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), fuso);

    private static TableRow RigaTesto(string label, string valore, bool grassetto)
    {
        var tr = new TableRow();
        tr.Append(new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "7200" }),
            Para(label, bold: grassetto, align: JustificationValues.Right, cella: true)));
        tr.Append(new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2000" }),
            Para(valore, bold: grassetto, align: JustificationValues.Right, cella: true)));
        return tr;
    }

    private static Paragraph Para(string testo, bool bold = false, int size = 20, bool italic = false,
        JustificationValues? align = null, string? shadingFill = null, int spazioPrima = 0, int spazioDopo = 0,
        bool cella = false, bool keepNext = false)
    {
        var p = OrdineAccettazioneDocx.Para(testo, bold, size, italic, align, shadingFill, spazioPrima, spazioDopo, cella);
        if (keepNext) p.ParagraphProperties!.PrependChild(new KeepNext());
        return p;
    }

    private const int Larghezza = 9638;

    /// <summary>Riquadro grigio a cella unica, come quello delle condizioni negli Ordini di Accettazione.</summary>
    private static Table Riquadro(string titolo, IEnumerable<(string? Titolo, string Corpo)> blocchi, string? firma = null, bool unito = false)
    {
        var t = Tabella(true, Larghezza);
        var cell = new TableCell(new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = Larghezza.ToString() },
            new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "F7F7F7" }));
        cell.Append(Para(titolo, bold: true, size: 20, spazioPrima: 60, spazioDopo: 40, cella: true, keepNext: true));
        foreach (var (tit, corpo) in blocchi)
        {
            if (tit is not null) cell.Append(Para(tit, bold: true, size: 17, spazioPrima: 120, spazioDopo: 20, cella: true, keepNext: true));
            cell.Append(Para(corpo, size: 16, align: JustificationValues.Both, cella: true, spazioDopo: 60));
        }
        if (firma is not null) cell.Append(Para(firma, size: 17, cella: true, spazioPrima: 200, spazioDopo: 80));
        t.Append(unito ? new TableRow(new TableRowProperties(new CantSplit()), cell) : new TableRow(cell));
        return t;
    }

    private static Table TabellaFirme(string? perMarea)
    {
        var t = Tabella(true, 4819, 4819);
        var tr = new TableRow(new TableRowProperties(new CantSplit()));
        TableCell Cella(string titolo, params (string Etichetta, string? Valore)[] righe)
        {
            var c = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "4819" }));
            c.Append(Para(titolo, bold: true, size: 18, spazioPrima: 60, spazioDopo: 160, cella: true));
            foreach (var (et, val) in righe)
                c.Append(Para($"{et}: {(string.IsNullOrWhiteSpace(val) ? "______________________" : val)}", size: 18, spazioDopo: 220, cella: true));
            return c;
        }
        tr.Append(Cella("Il Cliente", ("Nome e cognome", null), ("Firma", null), ("Data e ora", null)));
        tr.Append(Cella("Per MARE.A s.r.l.", ("Nome e cognome", perMarea), ("Firma", null), ("Data", null)));
        t.Append(tr);
        return t;
    }
}
