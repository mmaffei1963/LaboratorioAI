using System.Text;
using System.Text.Json;
using LaboratorioAI.Tracciabilita.Core.Importazione;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Importazione;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Importatore dal vecchio programma Access (sola lettura sul file Access).
//   prova  <file.mdb> [cartella]                          legge, controlla e scrive rapporto e file di abbinamento. Non scrive in nessun database.
//   importa <file.mdb> <idAzienda> <siglaSede> [cartella]  scrive nel database di PROVA (Tracciabilita_Test) usando le scelte dei due file CSV.
//   struttura <file.mdb>                                  sola lettura: elenca tabelle e colonne del file Access (nessun dato).
//   valori <file.mdb>                                     sola lettura: valori delle colonne di esito e profili di controllo.
//   controlli <idAzienda> [cartella]                      sola lettura: elenca i lotti con arrivi molto distanti nel tempo (codici riusati?).
//   ricette <idAzienda> [cartella]                        crea nel database di PROVA le ricette scelte nel foglio ricette_da_confermare.csv (cartella RicetteAccess).
//   azzera  <idAzienda>                                   cancella i dati importati di quell'azienda dal database di PROVA (non utenti, sedi, azienda).

var produzione = args.Contains("--produzione", StringComparer.OrdinalIgnoreCase);
args = args.Where(a => !string.Equals(a, "--produzione", StringComparison.OrdinalIgnoreCase)).ToArray();

if (args.Length < 2 || args[0] is not ("prova" or "importa" or "azzera" or "controlli" or "struttura" or "valori" or "ricette") || (args[0] == "importa" && args.Length < 4))
{
    Console.WriteLine("Uso:");
    Console.WriteLine("  struttura <file Access>   (sola lettura, nessun dato)");
    Console.WriteLine("  valori <file Access>   (sola lettura)");
    Console.WriteLine("  controlli <idAzienda> [cartella]   (sola lettura)");
    Console.WriteLine("  ricette <idAzienda> [cartella RicetteAccess] [--produzione]");
    Console.WriteLine("  azzera  <idAzienda>   (solo database di prova)");
    Console.WriteLine("  prova   <file Access> [cartella]");
    Console.WriteLine("  importa <file Access> <idAzienda> <siglaSede> [cartella] [--produzione]");
    Console.WriteLine("Con --produzione si scrive nel database indicato dalla variabile d'ambiente TRACCIATO_CONNESSIONE (vedi DEPLOY_Tracciato.md).");
    Console.WriteLine("La cartella predefinita è Documenti\\ImportazioneTracciato (fuori dal repository: contiene dati reali).");
    return 1;
}
var comando = args[0];
if (comando == "azzera") return await Azzera(args);
if (comando == "controlli") return await Controlli(args);
if (comando == "ricette") return await Ricette(args, produzione);
var file = Path.GetFullPath(args[1]);
if (!File.Exists(file))
{
    Console.WriteLine($"File non trovato: {file}");
    return 1;
}
if (!file.EndsWith(".mdb", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".accdb", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Il file deve essere un .mdb o .accdb.");
    return 1;
}

if (comando == "valori")
{
    try
    {
        var testoValori = ValoriAccess.Descrivi(file);
        var cartellaValori = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ImportazioneTracciato");
        Directory.CreateDirectory(cartellaValori);
        var fileValori = Path.Combine(cartellaValori, "valori_access.txt");
        File.WriteAllText(fileValori, testoValori, FileAbbinamenti.Utf8ConBom);
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine($"File scritto in: {fileValori}");
        Console.WriteLine(testoValori.Length > 200 ? "(il contenuto è nel file: aprilo e incollamelo)" : testoValori);
        return 0;
    }
    catch (Exception e)
    {
        Console.WriteLine("Non riesco a leggere il file Access: " + e.Message);
        return 2;
    }
}

if (comando == "struttura")
{
    try
    {
        var testoStruttura = StrutturaAccess.Descrivi(file);
        var cartellaStruttura = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ImportazioneTracciato");
        Directory.CreateDirectory(cartellaStruttura);
        var fileStruttura = Path.Combine(cartellaStruttura, "struttura_access.txt");
        File.WriteAllText(fileStruttura, testoStruttura, FileAbbinamenti.Utf8ConBom);
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(testoStruttura);
        Console.WriteLine($"File scritto in: {fileStruttura}");
        return 0;
    }
    catch (Exception e)
    {
        Console.WriteLine("Non riesco a leggere il file Access: " + e.Message);
        return 2;
    }
}

var posCartella = comando == "importa" ? 4 : 2;
var cartella = args.Length > posCartella
    ? Path.GetFullPath(args[posCartella])
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ImportazioneTracciato");
Directory.CreateDirectory(cartella);

Console.WriteLine($"Leggo (sola lettura): {file}");
DatiAccess dati;
try
{
    dati = new LettoreAccessOdbc(file).Leggi();
}
catch (Exception e)
{
    Console.WriteLine("Non riesco a leggere il file Access: " + e.Message);
    Console.WriteLine("Controlla che il file non sia aperto in Access e che sia una copia in una cartella normale.");
    return 2;
}

if (comando == "importa")
    return await Importa(dati, args, cartella, produzione);

var piano = Prova.Prepara(dati, DateTime.Today);
var rapporto = RapportoProva.Scrivi(piano, DateTime.Now);

File.WriteAllText(Path.Combine(cartella, "rapporto_prova.txt"), rapporto, FileAbbinamenti.Utf8ConBom);
File.WriteAllText(Path.Combine(cartella, "abbinamenti_fornitori.csv"), FileAbbinamenti.ScriviFornitori(piano.Fornitori), FileAbbinamenti.Utf8ConBom);
File.WriteAllText(Path.Combine(cartella, "abbinamenti_articoli.csv"), FileAbbinamenti.ScriviArticoli(piano.Articoli), FileAbbinamenti.Utf8ConBom);

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine();
Console.WriteLine(rapporto);
Console.WriteLine($"File scritti in: {cartella}");
return 0;


static async Task<int> Importa(DatiAccess dati, string[] args, string cartella, bool produzione)
{
    if (!int.TryParse(args[2], out var idAzienda)) { Console.WriteLine("idAzienda deve essere un numero (lo vedi nell'indirizzo della scheda azienda)."); return 1; }
    var sigla = args[3].Trim();

    var fileF = Path.Combine(cartella, "abbinamenti_fornitori.csv");
    var fileA = Path.Combine(cartella, "abbinamenti_articoli.csv");
    if (!File.Exists(fileF) || !File.Exists(fileA))
    {
        Console.WriteLine($"Mancano i file di abbinamento in {cartella}. Lancia prima «prova» e compila i file.");
        return 1;
    }

    var piano = PianificaImportazione.Costruisci(
        dati,
        FileAbbinamenti.LeggiFornitori(File.ReadAllText(fileF)),
        FileAbbinamenti.LeggiArticoli(File.ReadAllText(fileA)),
        DateTime.Today);

    if (piano.NonRisolte.Count > 0)
    {
        Console.WriteLine("Non posso importare: mancano delle scelte nei file di abbinamento.");
        foreach (var v in piano.NonRisolte.Take(40)) Console.WriteLine("  - " + v);
        Console.WriteLine("Compila la colonna SCELTA (fornitori) o NOME ARTICOLO (articoli) e rilancia.");
        return 3;
    }

    var connessione = LeggiConnessione(produzione);
    if (connessione is null) return 1;
    var nomeDb = new SqlConnectionStringBuilder(connessione).InitialCatalog;
    if (!VerificaDatabase(nomeDb, produzione, "importa")) return 1;

    Console.WriteLine();
    Console.WriteLine($"Database: {nomeDb}  ·  azienda {idAzienda}  ·  sede {sigla}");
    Console.WriteLine($"Righe da importare: {piano.Righe.Count}  ·  lotti distinti: {piano.LottiDistinti}");
    Console.WriteLine($"Righe escluse: {piano.Scartate.Count}  ·  fornitori nuovi da creare: {piano.FornitoriNuovi.Count}");
    foreach (var g in piano.Scartate.GroupBy(x => x.Motivo)) Console.WriteLine($"    {g.Key}: {g.Count()}");
    Console.WriteLine("Si può rilanciare senza duplicare. Scrivere nel database? Digita SI per continuare:");
    if (!string.Equals(Console.ReadLine()?.Trim(), "SI", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Annullato. Non è stato scritto nulla.");
        return 0;
    }

    var opzioni = new DbContextOptionsBuilder<ContestoTracciabilita>()
        .UseSqlServer(connessione, s => s.MigrationsHistoryTable("__EFMigrationsHistory", ContestoTracciabilita.Schema))
        .AddInterceptors(new ContestoSessioneInterceptor())
        .Options;
    await using var db = new ContestoTracciabilita(opzioni, new ContestoGestore());
    db.Database.SetCommandTimeout(TimeSpan.FromMinutes(15));

    try
    {
        var r = await ScrittoreTracciabilita.ScriviAsync(db, idAzienda, sigla, dati, piano, DateTime.UtcNow);
        var testo = $@"IMPORTAZIONE — {DateTime.Now:dd/MM/yyyy HH:mm}  (database {nomeDb}, azienda {idAzienda}, sede {sigla})
Fornitori creati: {r.FornitoriCreati} (già presenti: {r.FornitoriGiaPresenti})
Categorie create: {r.CategorieCreate} (già presenti: {r.CategorieGiaPresenti})
Articoli creati: {r.ArticoliCreati} (già presenti: {r.ArticoliGiaPresenti})
Legami articolo-fornitore creati: {r.ArticoliFornitoriCreati}  ·  materie del vecchio elenco senza articolo corrispondente: {r.MaterieSenzaArticolo}
Lotti creati: {r.LottiCreati} (già presenti: {r.LottiGiaPresenti}) — chiusi per storico: {r.LottiChiusiPerStorico}, in magazzino: {r.LottiInMagazzino}
Accettazioni create: {r.AccettazioniCreate} (già presenti o doppioni: {r.AccettazioniGiaPresenti})
Movimenti scritti: {r.MovimentiCreati}
Lotti chiusi alla data di arrivo perché l'impegno non era attendibile: {r.ChiusureDaDataNonAttendibile}
Articoli con categoria (matrice): {r.ArticoliConCategoria}
Profili di controllo creati: {r.ProfiliCreati} (già presenti: {r.ProfiliGiaPresenti}) — categorie collegate a un profilo: {r.CategorieConProfilo}
Controlli registrati sulle accettazioni: {r.ControlliCreati}
Lotti con data di scadenza: {r.ScadenzeImpostate}
Giudizi: respinti {r.GiudiziRespinti}, accettati con riserva {r.GiudiziConRiserva}
Righe escluse: {piano.Scartate.Count}

Da controllare:
" + (piano.Categorie.Segnalazioni.Count == 0 ? "  nessuna segnalazione\n" : string.Join("\n", piano.Categorie.Segnalazioni.Select(x => "  - " + x)) + "\n");
        File.WriteAllText(Path.Combine(cartella, "rapporto_importazione.txt"), testo, FileAbbinamenti.Utf8ConBom);
        File.WriteAllLines(Path.Combine(cartella, "righe_escluse.txt"), piano.Scartate.Select(x => $"riga {x.Numero}: {x.Motivo}"), FileAbbinamenti.Utf8ConBom);
        Console.WriteLine();
        Console.WriteLine(testo);
        Console.WriteLine($"Rapporto scritto in: {cartella}");
        return 0;
    }
    catch (Exception e)
    {
        Console.WriteLine("Importazione interrotta, nel database non è rimasto niente a metà: " + (e.InnerException?.Message ?? e.Message));
        return 4;
    }
}

// Database di prova: via libera. Produzione: serve --produzione e bisogna riscrivere il nome esatto del database.
static bool VerificaDatabase(string nomeDb, bool produzione, string comando)
{
    var èProva = nomeDb.EndsWith("_Test", StringComparison.OrdinalIgnoreCase);
    if (!produzione)
    {
        if (èProva) return true;
        Console.WriteLine($"Per sicurezza «{comando}» scrive solo in database di prova (nome che finisce per _Test). Trovato: «{nomeDb}».");
        Console.WriteLine("Per scrivere in produzione aggiungi --produzione.");
        return false;
    }
    if (èProva)
    {
        Console.WriteLine($"Con --produzione ci si aspetta un database di produzione, ma «{nomeDb}» è di prova. Tolgo il --produzione o correggi TRACCIATO_CONNESSIONE.");
        return false;
    }
    Console.WriteLine();
    Console.WriteLine($"ATTENZIONE: stai per scrivere nel database di PRODUZIONE «{nomeDb}».");
    Console.WriteLine($"Per confermare riscrivi il nome del database ({nomeDb}):");
    if (!string.Equals(Console.ReadLine()?.Trim(), nomeDb, StringComparison.Ordinal))
    {
        Console.WriteLine("Nome diverso. Annullato, non è stato scritto nulla.");
        return false;
    }
    return true;
}

static string? LeggiConnessione(bool produzione = false)
{
    if (produzione)
    {
        var daAmbiente = Environment.GetEnvironmentVariable("TRACCIATO_CONNESSIONE");
        if (string.IsNullOrWhiteSpace(daAmbiente))
        {
            Console.WriteLine("Con --produzione serve la variabile d'ambiente TRACCIATO_CONNESSIONE (stringa di connessione al database di produzione).");
            Console.WriteLine("Esempio in PowerShell (solo per questa finestra):");
            Console.WriteLine("  $env:TRACCIATO_CONNESSIONE = \"Server=192.168.23.1;Database=Tracciabilita;Trusted_Connection=True;TrustServerCertificate=True\"");
            return null;
        }
        return daAmbiente;
    }
    var percorso = Path.Combine("src", "LaboratorioAI.Tracciabilita.Web", "appsettings.Development.json");
    if (!File.Exists(percorso))
    {
        Console.WriteLine($"Non trovo {percorso}: lancia il comando dalla cartella principale del progetto (C:\\Sviluppo\\LaboratorioAI).");
        return null;
    }
    using var doc = JsonDocument.Parse(File.ReadAllText(percorso));
    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) && cs.TryGetProperty("Tracciabilita", out var v) && v.GetString() is { Length: > 0 } s)
        return s;
    Console.WriteLine("In appsettings.Development.json manca ConnectionStrings:Tracciabilita.");
    return null;
}


static async Task<int> Azzera(string[] args)
{
    if (!int.TryParse(args[1], out var idAzienda)) { Console.WriteLine("idAzienda deve essere un numero."); return 1; }
    var connessione = LeggiConnessione();
    if (connessione is null) return 1;
    var nomeDb = new SqlConnectionStringBuilder(connessione).InitialCatalog;
    if (!nomeDb.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Per sicurezza «azzera» funziona solo su database di prova (nome che finisce per _Test). Trovato: «{nomeDb}».");
        return 1;
    }

    Console.WriteLine($"Database: {nomeDb}  ·  azienda {idAzienda}");
    Console.WriteLine("Cancello i dati importati e quelli creati nei test: movimenti, produzioni, spedizioni, richiami, accettazioni, lotti, ricette, articoli, categorie, profili di controllo e fornitori.");
    Console.WriteLine("Restano l'azienda, le sedi, gli utenti e il registro attività. Digita AZZERA per continuare:");
    if (!string.Equals(Console.ReadLine()?.Trim(), "AZZERA", StringComparison.Ordinal))
    {
        Console.WriteLine("Annullato. Non è stato cancellato nulla.");
        return 0;
    }

    var opzioni = new DbContextOptionsBuilder<ContestoTracciabilita>()
        .UseSqlServer(connessione, s => s.MigrationsHistoryTable("__EFMigrationsHistory", ContestoTracciabilita.Schema))
        .AddInterceptors(new ContestoSessioneInterceptor())
        .Options;
    await using var db = new ContestoTracciabilita(opzioni, new ContestoGestore());
    db.AbilitaEliminazioneFisica();   // solo il gestore, solo qui, solo su database di prova
    db.Database.SetCommandTimeout(TimeSpan.FromMinutes(15));   // le cancellazioni grosse passano da un trigger e possono durare

    // Dai dati che dipendono dagli altri a quelli da cui dipendono.
    var tabelle = new[]
    {
        // Prima i dati creati usando il programma (richiami, produzioni, spedizioni, trasferimenti, ordini), che puntano a lotti e ricette.
        "Richiami", "ProduzioniConsumi", "SpedizioniRighe", "TrasferimentiRighe", "Allegati",
        "MovimentiLotto", "LottiInUso", "ValoriCampi", "ContatoriLotti",
        "Produzioni", "Spedizioni", "Trasferimenti",
        "RicetteRigheCategorie", "RicetteRighe", "RicetteVersioni", "Ricette",
        "AccettazioniControlli", "Accettazioni", "OrdiniRighe", "Ordini", "Lotti",
        "ArticoliFornitori", "ArticoliAllergeni", "Articoli", "CategorieMateriaPrima",
        "ProfiliControlloRighe", "ProfiliControllo", "Fornitori"
    };
    try
    {
        // A blocchi piccoli e senza una transazione unica: una cancellazione sola di migliaia di righe con i trigger
        // può finire la memoria di SQL Server Express. L'ordine (dai figli ai padri) rende sicuro rilanciare il comando.
        foreach (var t in tabelle)
        {
            Console.Write($"  {t}… ");
            var totale = 0;
            int n;
            do
            {
                n = await db.Database.ExecuteSqlRawAsync($"DELETE TOP (1000) FROM trac.[{t}] WHERE IdAzienda = {{0}}", idAzienda);
                totale += n;
            } while (n > 0);
            Console.WriteLine($"{totale} righe cancellate");
        }
        Console.WriteLine("Fatto. Ora puoi rilanciare «importa».");
        return 0;
    }
    catch (Exception e)
    {
        Console.WriteLine("Interrotto: " + (e.InnerException?.Message ?? e.Message) + "  Puoi rilanciare «azzera»: riprende da dove si è fermato.");
        return 4;
    }
}


static async Task<int> Controlli(string[] args)
{
    if (!int.TryParse(args[1], out var idAzienda)) { Console.WriteLine("idAzienda deve essere un numero."); return 1; }
    var connessione = LeggiConnessione();
    if (connessione is null) return 1;
    var cartella = args.Length > 2
        ? Path.GetFullPath(args[2])
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ImportazioneTracciato");
    Directory.CreateDirectory(cartella);

    var opzioni = new DbContextOptionsBuilder<ContestoTracciabilita>()
        .UseSqlServer(connessione, s => s.MigrationsHistoryTable("__EFMigrationsHistory", ContestoTracciabilita.Schema))
        .AddInterceptors(new ContestoSessioneInterceptor())
        .Options;
    await using var db = new ContestoTracciabilita(opzioni, new ContestoGestore());
    var voci = await ControlloCodiciRipetuti.TrovaAsync(db, idAzienda);
    var percorso = Path.Combine(cartella, "codici_lotto_ripetuti.csv");
    File.WriteAllText(percorso, ControlloCodiciRipetuti.ScriviCsv(voci), FileAbbinamenti.Utf8ConBom);
    Console.WriteLine($"Lotti con più arrivi a più di {ControlloCodiciRipetuti.GiorniSoglia} giorni di distanza: {voci.Count}");
    Console.WriteLine($"  di cui ancora con giacenza: {voci.Count(v => v.Giacenza > 0)}");
    Console.WriteLine($"File scritto (sola lettura sul database): {percorso}");
    return 0;
}


static async Task<int> Ricette(string[] args, bool produzione)
{
    if (!int.TryParse(args[1], out var idAzienda)) { Console.WriteLine("idAzienda deve essere un numero."); return 1; }
    var connessione = LeggiConnessione(produzione);
    if (connessione is null) return 1;
    var nomeDb = new SqlConnectionStringBuilder(connessione).InitialCatalog;
    if (!VerificaDatabase(nomeDb, produzione, "ricette")) return 1;
    var cartella = args.Length > 2
        ? Path.GetFullPath(args[2])
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ImportazioneTracciato", "RicetteAccess");

    ImportaRicette.Piano piano;
    try { piano = ImportaRicette.Prepara(cartella); }
    catch (Exception e) { Console.WriteLine("Non riesco a leggere i file delle ricette: " + e.Message); return 2; }

    Console.OutputEncoding = Encoding.UTF8;
    Console.WriteLine($"Database: {nomeDb}  ·  azienda {idAzienda}  ·  cartella {cartella}");
    Console.WriteLine($"Ricette da creare (con IMPORTA = SI): {piano.Ricette.Count}");
    foreach (var rc in piano.Ricette)
        Console.WriteLine($"  {rc.Numero,2}. {rc.Nome}  ({rc.Ingredienti.Count} ingredienti)");
    foreach (var a in piano.Avvisi) Console.WriteLine("  ATTENZIONE: " + a);
    Console.WriteLine("Ogni ricetta crea anche il suo prodotto finito. Si può rilanciare senza duplicare: le ricette già presenti ricevono solo gli ingredienti che mancano, e le categorie mancanti si creano.");
    Console.WriteLine("Scrivere nel database? Digita SI per continuare:");
    if (!string.Equals(Console.ReadLine()?.Trim(), "SI", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Annullato. Non è stato scritto nulla.");
        return 0;
    }

    var opzioni = new DbContextOptionsBuilder<ContestoTracciabilita>()
        .UseSqlServer(connessione, s => s.MigrationsHistoryTable("__EFMigrationsHistory", ContestoTracciabilita.Schema))
        .AddInterceptors(new ContestoSessioneInterceptor())
        .Options;
    await using var db = new ContestoTracciabilita(opzioni, new ContestoGestore());
    try
    {
        var r = await ImportaRicette.ScriviAsync(db, idAzienda, piano, DateOnly.FromDateTime(DateTime.Today));
        var testo = $@"RICETTE — {DateTime.Now:dd/MM/yyyy HH:mm}  (database {nomeDb}, azienda {idAzienda})
Ricette create: {r.RicetteCreate}  ·  ricette già presenti completate con ingredienti mancanti: {r.RicetteCompletate}  ·  già complete o nome ripetuto: {r.RicetteGiaPresenti}\nCategorie create perché mancavano in Tracciato: {r.CategorieCreate}
Prodotti finiti creati: {r.ArticoliCreati} (già presenti: {r.ArticoliRiusati})
Ingredienti creati: {r.IngredientiCreati}

Da controllare:
" + (r.Segnalazioni.Count == 0 && piano.Avvisi.Count == 0 ? "  nessuna segnalazione\n" : string.Join("\n", piano.Avvisi.Concat(r.Segnalazioni).Select(x => "  - " + x)) + "\n");
        File.WriteAllText(Path.Combine(cartella, "rapporto_ricette.txt"), testo, FileAbbinamenti.Utf8ConBom);
        Console.WriteLine();
        Console.WriteLine(testo);
        Console.WriteLine($"Rapporto scritto in: {cartella}");
        return 0;
    }
    catch (Exception e)
    {
        Console.WriteLine("Importazione interrotta, nel database non è rimasto niente a metà: " + (e.InnerException?.Message ?? e.Message));
        return 4;
    }
}
