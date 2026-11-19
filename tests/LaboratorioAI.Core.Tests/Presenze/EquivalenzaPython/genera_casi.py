"""Genera i casi di equivalenza tra l'app «Presenze» di Vaccarini (Python) e il motore .NET.

Serve SOLO per rigenerare il file CasiEquivalenzaPython.cs (già incluso nel pacchetto):
non fa parte della compilazione né dei test.

Uso (da PowerShell, con Python 3.11+ e le librerie dell'app installate: flask, openpyxl):
    python genera_casi.py "C:\\...\\Presenze_prova_Windows\\Presenze\\app" "<percorso>\\CasiEquivalenzaPython.cs"

Per ogni caso si chiamano le funzioni ORIGINALI dell'app (calcoli.calcola_giorno,
excel_master.contenuto, app.calcola_periodo, app.totali) e si salva l'esito atteso.
I casi «periodo» usano il vero database SQLite dell'app, con la data di oggi fissata.
"""
import json
import os
import random
import sys
import tempfile
from datetime import date, datetime, time, timedelta

SEME = 20261119
N_GIORNI = 260
N_PERIODI = 12

PROFILI_ORE = [
    [8, 8, 8, 8, 8, 0, 0],
    [8, 8, 8, 8, 4, 0, 0],
    [4, 4, 4, 4, 4, 0, 0],
    [7.5, 7.5, 7.5, 7.5, 6, 0, 0],
    [6.5, 6.5, 6.5, 6.5, 6.5, 0, 0],
    [8, 8, 8, 8, 8, 4, 0],
    [0, 0, 0, 0, 0, 0, 0],
]
CAUSALI = ["FER", "PER", "104", "SEG", "MAL", "MAT", "DS", "ALT"]
ORE_ASSENZA = [0.25, 0.5, 1, 1.5, 2, 2.5, 4, 6.5, 7.5, 8]
LUOGHI = ["Cliente X", "Cantiere Rossi", "Pesaro", "Sede Cattolica", "Ditta Bianchi & C."]
NOTE = ["visita medica", "pratica", "Ponte", "esame (urgente)"]


def hhmm(minuti):
    minuti = max(0, min(23 * 60 + 59, int(minuti)))
    return f"{minuti // 60:02d}:{minuti % 60:02d}"


def timbrature_giorno(rng, strettamente_crescenti=False):
    """Lista [(tipo, minuti)] con giornate pulite, aperte o sporche."""
    r = rng.random()
    if r < 0.15:
        return []
    if r < 0.55:
        base = rng.choice([7 * 60 + 30, 8 * 60, 8 * 60 + 30, 9 * 60])
        e1 = base + rng.randint(-20, 20)
        if rng.random() < 0.5:
            return [("E", e1), ("U", e1 + rng.choice([4, 6, 8, 9]) * 60 + rng.randint(-25, 25))]
        u1 = e1 + 4 * 60 + rng.randint(-20, 20)
        e2 = u1 + rng.choice([30, 45, 60, 90]) + rng.randint(-10, 10)
        u2 = e2 + rng.choice([3, 4, 5]) * 60 + rng.randint(-20, 20)
        return [("E", e1), ("U", u1), ("E", e2), ("U", u2)]
    if r < 0.63:
        e1 = rng.choice([8 * 60, 8 * 60 + 15, 9 * 60])
        if rng.random() < 0.5:
            return [("E", e1)]
        return [("E", e1), ("U", e1 + 4 * 60), ("E", e1 + 5 * 60)]
    n = rng.randint(1, 5)
    if strettamente_crescenti:
        minuti = sorted(rng.sample(range(6 * 60, 20 * 60), n))
    else:
        minuti = [rng.randint(6 * 60, 20 * 60) for _ in range(n)]
    return [(rng.choice("EU"), m) for m in minuti]


def con_luoghi(rng, lista, mescola):
    out = [{"ora": hhmm(m), "tipo": t, "luogo": (rng.choice(LUOGHI) if rng.random() < 0.2 else None)}
           for t, m in lista]
    if mescola and rng.random() < 0.3:
        rng.shuffle(out)
    return out


def assenze_giorno(rng):
    r = rng.random()
    n = 0 if r < 0.65 else (1 if r < 0.95 else 2)
    return [{"causale": rng.choice(CAUSALI), "ore": float(rng.choice(ORE_ASSENZA)),
             "note": (rng.choice(NOTE) if rng.random() < 0.3 else None)} for _ in range(n)]


def voce_calendario(rng):
    r = rng.random()
    if r < 0.70:
        return None
    if r < 0.78:
        return {"tipo": "FE", "descrizione": rng.choice(["Epifania", "Patrono", ""]), "ore": None}
    return {"tipo": "CH", "descrizione": rng.choice(["Ponte", "Chiusura estiva", ""]),
            "ore": (None if rng.random() < 0.6 else float(rng.choice([2, 4, 6, 8])))}


def data_casuale(rng, C, dal=date(2026, 1, 1), al=date(2027, 12, 31)):
    r = rng.random()
    if r < 0.25:
        anno = rng.choice([2026, 2027])
        return rng.choice([d for d, _ in C.festivita_nazionali(anno)])
    d = dal + timedelta(days=rng.randint(0, (al - dal).days))
    if r < 0.40:
        d += timedelta(days=(5 - d.weekday()) % 7 + rng.choice([0, 1]))
    return d


def atteso(g):
    return {
        "contratto": g["contratto"], "lavorate": g["lavorate"],
        "coppie": [list(c) for c in g["coppie"]],
        "oreAssenze": g["ore_assenze"], "chiusura": g["chiusura"],
        "festivo": g["festivo"], "festivoDescrizione": g["festivo_desc"] or "",
        "chiusuraDescrizione": g["chiusura_desc"] or "",
        "weekend": g["weekend"], "straordinario": g["straordinario"], "nonLavorate": g["non_lavorate"],
        "anomalie": list(g["anomalie"]), "inCorso": g["in_corso"], "futuro": g["futuro"],
        "primaInizio": g["prima_inizio"],
    }


def cella(c):
    if c is None:
        return None
    v = c["v"]
    return {"ore": float(v) if isinstance(v, (int, float)) else None,
            "testo": v if isinstance(v, str) else None,
            "sfondo": c["fill"], "rosso": bool(c["rosso"]), "grassetto": bool(c["b"]),
            "corsivo": bool(c["i"]), "nota": c["nota"]}


def caso_giorno(rng, n, C, X, palette):
    ore = rng.choice(PROFILI_ORE)
    giorno = data_casuale(rng, C)
    r = rng.random()
    oggi = giorno if r < 0.2 else (giorno - timedelta(days=rng.randint(1, 30)) if r < 0.4
                                   else giorno + timedelta(days=rng.randint(1, 60)))
    inizio = None if rng.random() < 0.5 else giorno + timedelta(days=rng.randint(-20, 20))
    attivo_dal = None if rng.random() < 0.8 else giorno + timedelta(days=rng.randint(-20, 20))
    colore = rng.choice(palette)
    timb = con_luoghi(rng, timbrature_giorno(rng), mescola=True)
    ass = assenze_giorno(rng)
    cal = voce_calendario(rng)
    utente = {**{f"ore_{i}": float(v) for i, v in enumerate(ore)},
              "attivo_dal": attivo_dal.isoformat() if attivo_dal else None, "colore": colore}
    g = C.calcola_giorno(utente, giorno, timb, ass, cal, oggi, inizio)
    g["timbrature"] = timb
    return {
        "id": f"G{n:03d}",
        "utente": {"colore": colore, "ore": [float(v) for v in ore],
                   "attivoDal": attivo_dal.isoformat() if attivo_dal else None},
        "giorno": giorno.isoformat(), "oggi": oggi.isoformat(),
        "inizio": inizio.isoformat() if inizio else None,
        "timbrature": timb, "assenze": ass, "cal": cal,
        "atteso": atteso(g), "cella": cella(X.contenuto(utente, g)),
    }


def caso_periodo(rng, n, C, X, A, D, palette):
    anno, mese = rng.choice([2026, 2027]), rng.randint(1, 12)
    dal = date(anno, mese, 1)
    al = (dal + timedelta(days=32)).replace(day=1) - timedelta(days=1)
    giorni = [dal + timedelta(days=i) for i in range((al - dal).days + 1)]
    ore = rng.choice(PROFILI_ORE[:6])
    colore = rng.choice(palette)
    attivo_dal = None if rng.random() < 0.75 else rng.choice(giorni)
    inizio = None if rng.random() < 0.4 else rng.choice(giorni)
    oggi = rng.choice(giorni) + timedelta(days=rng.randint(-3, 3))

    calendario = {d: {"tipo": "FE", "descrizione": desc, "ore": None}
                  for d, desc in C.festivita_nazionali(anno) if d.month == mese}
    if rng.random() < 0.5:
        inizio_ch = rng.choice(giorni[:-2])
        for k in range(rng.randint(1, 3)):
            d = inizio_ch + timedelta(days=k)
            if d.month == mese and d.weekday() < 5 and d not in calendario:
                calendario[d] = {"tipo": "CH", "descrizione": "Ponte",
                                 "ore": None if rng.random() < 0.5 else float(rng.choice([4, 8]))}

    timb, ass = [], []
    for d in giorni:
        if d in calendario and calendario[d]["tipo"] == "FE" and rng.random() < 0.9:
            continue
        lavorativo = d.weekday() < 5
        if lavorativo and rng.random() < 0.12:
            ass.extend({"data": d.isoformat(), **a} for a in assenze_giorno(rng))
            if rng.random() < 0.6:
                continue
        if not lavorativo and rng.random() < 0.85:
            continue
        for t in con_luoghi(rng, timbrature_giorno(rng, strettamente_crescenti=True), mescola=False):
            timb.append({"data": d.isoformat(), **t})

    imp = None if inizio is None else inizio.isoformat()
    riga_utente = {"colore": colore, "ore": [float(v) for v in ore],
                   "attivoDal": attivo_dal.isoformat() if attivo_dal else None}

    with tempfile.TemporaryDirectory() as tmp:
        flask_app = A.crea_app(tmp)
        with flask_app.app_context():
            db = D.get_db()
            db.execute("""INSERT INTO utenti (username, nome, password_hash, colore, ordine, attivo_dal,
                          ore_0, ore_1, ore_2, ore_3, ore_4, ore_5, ore_6) VALUES ('u','ROSSI','x',?,10,?,?,?,?,?,?,?,?)""",
                       (colore, riga_utente["attivoDal"], *[float(v) for v in ore]))
            for t in timb:
                db.execute("INSERT INTO timbrature (utente_id, data, ora, tipo, luogo, origine, creata_il) "
                           "VALUES (1,?,?,?,?,'app','2026-01-01 00:00:00')", (t["data"], t["ora"], t["tipo"], t["luogo"]))
            for a in ass:
                db.execute("INSERT INTO assenze (utente_id, data, causale, ore, note, creata_il) "
                           "VALUES (1,?,?,?,?,'2026-01-01 00:00:00')", (a["data"], a["causale"], a["ore"], a["note"]))
            for d, v in calendario.items():
                db.execute("INSERT INTO calendario (data, tipo, descrizione, ore) VALUES (?,?,?,?)",
                           (d.isoformat(), v["tipo"], v["descrizione"], v["ore"]))
            if imp is None:
                db.execute("DELETE FROM impostazioni WHERE chiave='inizio_conteggi'")
            else:
                D.imposta(db, "inizio_conteggi", imp)
            db.commit()
            A.adesso = lambda: datetime.combine(oggi, time(10, 0), tzinfo=D.TZ)
            u = db.execute("SELECT * FROM utenti WHERE id=1").fetchone()
            risultato = A.calcola_periodo(db, u, dal, al)
            tot = A.totali(risultato)
            giorni_att = []
            for g in risultato:
                giorni_att.append({"giorno": g["data"].isoformat(), "atteso": atteso(g),
                                   "cella": cella(X.contenuto(u, g))})
            # ripristina l'orologio reale per i casi successivi
            A.adesso = D.adesso

    return {
        "id": f"P{n:02d}", "utente": riga_utente, "dal": dal.isoformat(), "al": al.isoformat(),
        "oggi": oggi.isoformat(), "inizio": imp, "timbrature": timb, "assenze": ass,
        "calendario": [{"giorno": d.isoformat(), **v} for d, v in sorted(calendario.items())],
        "giorni": giorni_att,
        "totali": {"contratto": tot["contratto"], "lavorate": tot["lavorate"],
                   "straordinario": tot["straordinario"], "nonLavorate": tot["non_lavorate"],
                   "chiusura": tot["chiusura"], "causali": tot["causali"], "giorniConAnomalie": tot["anomalie"]},
    }


def main(cartella_app, uscita):
    sys.path.insert(0, cartella_app)
    os.environ["PRESENZE_HTTPS"] = "0"
    import app as A
    import calcoli as C
    import db as D
    import excel_master as X

    rng = random.Random(SEME)
    palette = list(A.PALETTE)
    giorni = [caso_giorno(rng, n, C, X, palette) for n in range(N_GIORNI)]
    periodi = [caso_periodo(rng, n, C, X, A, D, palette) for n in range(N_PERIODI)]
    testo = json.dumps({"giorni": giorni, "periodi": periodi}, ensure_ascii=False, separators=(",", ":"))
    if '""""' in testo:
        sys.exit("Il JSON contiene quattro virgolette consecutive: cambiare il delimitatore.")
    with open(uscita, "w", encoding="utf-8", newline="\n") as f:
        f.write("// <auto-generated>\n"
                "// Generato da EquivalenzaPython/genera_casi.py con le funzioni ORIGINALI dell'app Presenze di Vaccarini.\n"
                "// Non modificare a mano: rigenerare con lo script.\n"
                "// </auto-generated>\n"
                "namespace LaboratorioAI.Core.Tests.Presenze;\n\n"
                "internal static class CasiEquivalenzaPython\n{\n"
                '    public const string Json = """"\n')
        f.write(testo)
        f.write('\n""""' + ";\n}\n")
    print(f"Scritti {len(giorni)} casi giorno e {len(periodi)} casi periodo in {uscita} ({len(testo) // 1024} KB)")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
