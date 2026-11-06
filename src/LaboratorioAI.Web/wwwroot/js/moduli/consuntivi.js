// Consuntivo per area (20/10/2026): dettaglio delle registrazioni, scelta utile/ricavi nell'andamento, stampa.
const tabella = document.getElementById('cons-voci');
const dlg = document.getElementById('cons-dettaglio');
const titolo = document.getElementById('cons-dettaglio-titolo');
const corpo = document.getElementById('cons-dettaglio-corpo');

document.getElementById('cons-chiudi')?.addEventListener('click', () => dlg.close());
document.getElementById('cons-stampa')?.addEventListener('click', () => window.print());

tabella?.addEventListener('click', async (e) => {
    const cella = e.target.closest('td.cliccabile');
    if (!cella) return;
    const p = new URLSearchParams({ dal: tabella.dataset.dal, al: tabella.dataset.al, voce: cella.dataset.voce ?? '' });
    if (cella.dataset.area) p.set('area', cella.dataset.area);
    titolo.textContent = cella.dataset.nome ?? '';
    corpo.innerHTML = '<p>Caricamento…</p>';
    dlg.showModal();
    try {
        const r = await fetch('/Consuntivi/Dettaglio?' + p.toString(), { headers: { 'X-Requested-With': 'fetch' } });
        corpo.innerHTML = r.ok ? await r.text() : `<p>Dettaglio non disponibile (errore ${r.status}).</p>`;
    } catch {
        corpo.innerHTML = '<p>Dettaglio non disponibile: connessione non riuscita.</p>';
    }
});

for (const radio of document.querySelectorAll('input[name="cons-misura"]')) {
    radio.addEventListener('change', () => {
        for (const blocco of document.querySelectorAll('.cons-mesi')) blocco.hidden = blocco.dataset.misura !== radio.value;
    });
}

// ── Grafici (20/10/2026): SVG disegnati qui, senza librerie esterne ──
const BLU = '#2563EB', GRIGIO = '#9CA3AF', VERDE = '#16A34A', ROSSO = '#DC2626', ASSE = '#6B7280', GRIGLIA = '#E5E7EB';
const NS = 'http://www.w3.org/2000/svg';
const eur = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR', maximumFractionDigits: 0 });

function el(tag, attr = {}, testo) {
    const e = document.createElementNS(NS, tag);
    for (const [k, v] of Object.entries(attr)) e.setAttribute(k, v);
    if (testo !== undefined) e.textContent = testo;
    return e;
}
function breve(v) {
    const a = Math.abs(v);
    const s = a >= 1e6 ? (a / 1e6).toLocaleString('it-IT', { maximumFractionDigits: 1 }) + ' M'
        : a >= 1e3 ? Math.round(a / 1e3).toLocaleString('it-IT') + ' k' : Math.round(a).toLocaleString('it-IT');
    return (v < 0 ? '−' : '') + s;
}
// Tacche "tonde" tra min e max (comprende sempre lo zero).
function scala(min, max, n = 5) {
    min = Math.min(0, min); max = Math.max(0, max);
    if (min === max) max = 1;
    const passoGrezzo = (max - min) / n;
    const mag = Math.pow(10, Math.floor(Math.log10(passoGrezzo)));
    const passo = [1, 2, 2.5, 5, 10].map(x => x * mag).find(x => x >= passoGrezzo);
    const da = Math.floor(min / passo) * passo, a = Math.ceil(max / passo) * passo;
    const tacche = [];
    for (let v = da; v <= a + passo / 2; v += passo) tacche.push(Math.round(v));
    return { da, a, tacche };
}
function svg(w, h) { return el('svg', { viewBox: `0 0 ${w} ${h}`, role: 'img' }); }

// Barre verticali raggruppate: categorie × serie [{nome, colore, valori, colori?}]
function barre(contenitore, categorie, serie) {
    if (!contenitore) return;
    const W = 560, H = 280, sx = 56, dx = 10, su = 12, giu = 46;
    const tutti = serie.flatMap(s => s.valori);
    const { da, a, tacche } = scala(Math.min(...tutti), Math.max(...tutti));
    const y = v => su + (a - v) / (a - da) * (H - su - giu);
    const s = svg(W, H);
    for (const t of tacche) {
        s.append(el('line', { x1: sx, x2: W - dx, y1: y(t), y2: y(t), stroke: t === 0 ? ASSE : GRIGLIA, 'stroke-width': t === 0 ? 1.2 : 1 }));
        s.append(el('text', { x: sx - 6, y: y(t) + 4, 'text-anchor': 'end', 'font-size': 11, fill: ASSE }, breve(t)));
    }
    const larg = (W - sx - dx) / categorie.length, gruppo = larg * 0.72, barra = gruppo / serie.length;
    categorie.forEach((c, i) => {
        const x0 = sx + i * larg + (larg - gruppo) / 2;
        serie.forEach((se, j) => {
            const v = se.valori[i] ?? 0, yv = y(v), y0 = y(0);
            const r = el('rect', { x: x0 + j * barra + 1, y: Math.min(yv, y0), width: Math.max(barra - 2, 1), height: Math.max(Math.abs(y0 - yv), 0.5),
                fill: se.colori ? se.colori[i] : se.colore, rx: 2 });
            r.append(el('title', {}, `${c} · ${se.nome}: ${eur.format(v)}`));
            s.append(r);
        });
        s.append(el('text', { x: x0 + gruppo / 2, y: H - giu + 16, 'text-anchor': 'middle', 'font-size': 11, fill: '#111827' }, c.length > 16 ? c.slice(0, 15) + '…' : c));
    });
    contenitore.replaceChildren(s);
}

// Linee: etichette × serie [{nome, colore, valori, tratteggio?}]
function linee(contenitore, etichette, serie) {
    if (!contenitore) return;
    const W = 560, H = 280, sx = 56, dx = 14, su = 12, giu = 34;
    const tutti = serie.flatMap(s => s.valori);
    if (tutti.length === 0) return;
    const { da, a, tacche } = scala(Math.min(...tutti), Math.max(...tutti));
    const y = v => su + (a - v) / (a - da) * (H - su - giu);
    const n = Math.max(etichette.length, 2);
    const x = i => sx + i * (W - sx - dx) / (n - 1);
    const s = svg(W, H);
    for (const t of tacche) {
        s.append(el('line', { x1: sx, x2: W - dx, y1: y(t), y2: y(t), stroke: t === 0 ? ASSE : GRIGLIA }));
        s.append(el('text', { x: sx - 6, y: y(t) + 4, 'text-anchor': 'end', 'font-size': 11, fill: ASSE }, breve(t)));
    }
    etichette.forEach((e, i) => s.append(el('text', { x: x(i), y: H - giu + 16, 'text-anchor': 'middle', 'font-size': 11, fill: '#111827' }, e)));
    for (const se of serie) {
        const punti = se.valori.map((v, i) => `${x(i)},${y(v)}`).join(' ');
        s.append(el('polyline', { points: punti, fill: 'none', stroke: se.colore, 'stroke-width': 2.5, 'stroke-dasharray': se.tratteggio ? '6 4' : 'none' }));
        se.valori.forEach((v, i) => {
            const p = el('circle', { cx: x(i), cy: y(v), r: 3.5, fill: se.colore });
            p.append(el('title', {}, `${etichette[i]} · ${se.nome}: ${eur.format(v)}`));
            s.append(p);
        });
        const ult = se.valori.length - 1;
        if (ult >= 0) s.append(el('text', { x: x(ult) - 4, y: y(se.valori[ult]) - 8, 'text-anchor': 'end', 'font-size': 11, 'font-weight': 600, fill: se.colore }, breve(se.valori[ult])));
    }
    contenitore.replaceChildren(s);
}

// Barre orizzontali: etichette × serie [{nome, colore, valori}]
function barreOrizzontali(contenitore, etichette, serie) {
    if (!contenitore || etichette.length === 0) return;
    const W = 560, sx = 170, dx = 52, riga = 26, su = 6;
    const H = su + etichette.length * riga + 22;
    const tutti = serie.flatMap(s => s.valori);
    const { da, a, tacche } = scala(Math.min(...tutti), Math.max(...tutti), 4);
    const x = v => sx + (v - da) / (a - da) * (W - sx - dx);
    const s = svg(W, H);
    for (const t of tacche) {
        s.append(el('line', { x1: x(t), x2: x(t), y1: su, y2: H - 20, stroke: t === 0 ? ASSE : GRIGLIA }));
        s.append(el('text', { x: x(t), y: H - 6, 'text-anchor': 'middle', 'font-size': 11, fill: ASSE }, breve(t)));
    }
    const alt = riga * 0.75 / serie.length;
    etichette.forEach((e, i) => {
        const y0 = su + i * riga + riga * 0.125;
        s.append(el('text', { x: sx - 6, y: y0 + riga * 0.45, 'text-anchor': 'end', 'font-size': 11, fill: '#111827' }, e.length > 26 ? e.slice(0, 25) + '…' : e));
        serie.forEach((se, j) => {
            const v = se.valori[i] ?? 0;
            const r = el('rect', { x: Math.min(x(0), x(v)), y: y0 + j * alt, width: Math.max(Math.abs(x(v) - x(0)), 0.5), height: alt - 1, fill: se.colore, rx: 2 });
            r.append(el('title', {}, `${e} · ${se.nome}: ${eur.format(v)}`));
            s.append(r);
            if (j === 0) s.append(el('text', { x: x(v) + 4, y: y0 + alt - 2, 'font-size': 10.5, fill: ASSE }, breve(v)));
        });
    });
    contenitore.replaceChildren(s);
}

const datiGrafici = document.getElementById('cons-dati-grafici');
if (datiGrafici) {
    const d = JSON.parse(datiGrafici.textContent);
    barre(document.getElementById('graf-utile'), d.areeOperative, [
        { nome: d.periodo, valori: d.utile, colori: d.utile.map(v => v < 0 ? ROSSO : BLU) },
        { nome: 'anno prima ' + d.precedente, colore: GRIGIO, valori: d.utilePrec },
    ]);
    linee(document.getElementById('graf-progressivo'), d.mesi, [
        { nome: 'anno prima ' + d.precedente, colore: GRIGIO, valori: d.progressivoPrec, tratteggio: true },
        { nome: d.periodo, colore: BLU, valori: d.progressivo },
    ]);
    barre(document.getElementById('graf-ricavi-costi'), d.aree, [
        { nome: 'ricavi', colore: VERDE, valori: d.ricavi },
        { nome: 'costi', colore: ROSSO, valori: d.costi },
    ]);
    barreOrizzontali(document.getElementById('graf-voci'), d.vociCosto, [
        { nome: d.periodo, colore: BLU, valori: d.costoVoci },
        { nome: 'anno prima ' + d.precedente, colore: GRIGIO, valori: d.costoVociPrec },
    ]);
}
