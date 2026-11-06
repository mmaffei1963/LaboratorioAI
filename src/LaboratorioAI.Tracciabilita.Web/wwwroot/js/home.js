// Pagina principale: cartelle con finestra, ricerca delle funzioni e preferiti (stellina, salvati in questo browser).

// ── Finestre delle cartelle: si aprono al clic e si chiudono con Esc, ✕ o clic fuori ──
for (const pulsante of document.querySelectorAll('[data-apri]')) {
    const finestra = document.getElementById(pulsante.dataset.apri);
    if (!finestra) continue;
    pulsante.addEventListener('click', () => finestra.showModal());
    finestra.addEventListener('click', e => { if (e.target === finestra) finestra.close(); });
}

// ── Preferiti ──
const CHIAVE = 'tracciato.preferiti';
const elencoPreferiti = document.getElementById('preferiti-elenco');
const avvisoVuoto = document.getElementById('preferiti-vuoto');

function leggi() {
    try { const v = JSON.parse(localStorage.getItem(CHIAVE) || '[]'); return Array.isArray(v) ? v : []; } catch { return []; }
}
function salva(lista) {
    try { localStorage.setItem(CHIAVE, JSON.stringify(lista)); } catch { /* senza memoria locale i preferiti non si ricordano */ }
}

function creaPreferito(url) {
    const voce = document.querySelector(`.home-voce[href="${CSS.escape(url)}"]`);
    if (!voce) return null;
    const a = document.createElement('a');
    a.className = 'home-preferito';
    a.href = url;
    a.dataset.url = url;

    const icona = document.createElement('span');
    icona.className = 'home-preferito-icona';
    icona.setAttribute('aria-hidden', 'true');
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', '18'); svg.setAttribute('height', '18'); svg.setAttribute('viewBox', '0 0 24 24');
    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    path.setAttribute('d', voce.dataset.icona || '');
    for (const [k, v] of Object.entries({ fill: 'none', stroke: 'currentColor', 'stroke-width': '1.8', 'stroke-linecap': 'round', 'stroke-linejoin': 'round' }))
        path.setAttribute(k, v);
    svg.append(path);
    icona.append(svg);

    const testo = document.createElement('span');
    testo.className = 'home-preferito-testo';
    const titolo = document.createElement('strong');
    titolo.textContent = voce.dataset.titolo || '';
    const cartella = document.createElement('small');
    cartella.textContent = voce.dataset.cartellaNome || '';
    testo.append(titolo, cartella);

    a.append(icona, testo);
    return a;
}

function segnaStelle(url, attiva) {
    for (const b of document.querySelectorAll(`[data-stella="${CSS.escape(url)}"]`)) {
        b.setAttribute('aria-pressed', attiva ? 'true' : 'false');
        b.title = attiva ? 'Togli dai preferiti' : 'Aggiungi ai preferiti';
    }
}

function disegnaPreferiti() {
    if (!elencoPreferiti) return;
    const lista = leggi();
    elencoPreferiti.replaceChildren(...lista.map(creaPreferito).filter(Boolean));
    for (const url of lista) segnaStelle(url, true);
    if (avvisoVuoto) avvisoVuoto.hidden = elencoPreferiti.children.length > 0;
}

for (const stella of document.querySelectorAll('[data-stella]')) {
    stella.addEventListener('click', () => {
        const url = stella.dataset.stella;
        const attiva = stella.getAttribute('aria-pressed') !== 'true';
        let lista = leggi().filter(u => u !== url);
        if (attiva) lista.push(url);
        salva(lista);
        segnaStelle(url, attiva);
        disegnaPreferiti();
    });
}
disegnaPreferiti();

// ── Ricerca delle funzioni ──
// Tutto nel browser. Ricerca senza accenti e maiuscole; ogni parola scritta deve comparire in nome, descrizione o cartella.
const casella = document.getElementById('home-cerca');
const boxRisultati = document.getElementById('home-risultati');
const elencoRisultati = document.getElementById('home-risultati-elenco');
const nessunRisultato = document.getElementById('home-risultati-vuoto');
const boxCartelle = document.getElementById('home-cartelle');

const semplifica = t => (t || '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

const indice = [...document.querySelectorAll('.home-finestra .home-voce')].map(v => ({
    url: v.getAttribute('href'),
    titolo: v.dataset.titolo || '',
    cartella: v.dataset.cartellaNome || '',
    descrizione: v.querySelector('span')?.textContent || '',
    get chiave() { return semplifica(`${this.titolo} ${this.descrizione} ${this.cartella}`); }
}));

function cerca(testo) {
    const parole = semplifica(testo).split(/\s+/).filter(Boolean);
    if (parole.length === 0) return null;
    return indice
        .filter(v => parole.every(p => v.chiave.includes(p)))
        .map(v => {
            const t = semplifica(v.titolo);
            const punti = parole.reduce((s, p) => s + (t.startsWith(p) ? 3 : t.includes(p) ? 2 : 0), 0);
            return { v, punti };
        })
        .sort((a, b) => b.punti - a.punti || a.v.titolo.localeCompare(b.v.titolo, 'it'))
        .map(x => x.v);
}

function mostraRisultati() {
    const trovati = cerca(casella.value);
    if (trovati === null) { boxRisultati.hidden = true; boxCartelle.hidden = false; return; }
    boxCartelle.hidden = true;
    boxRisultati.hidden = false;
    elencoRisultati.replaceChildren(...trovati.map(v => {
        const a = document.createElement('a');
        a.className = 'home-voce';
        a.href = v.url;
        const cartella = document.createElement('small');
        cartella.className = 'home-voce-cartella';
        cartella.textContent = v.cartella;
        const titolo = document.createElement('strong');
        titolo.textContent = v.titolo;
        const descr = document.createElement('span');
        descr.textContent = v.descrizione;
        a.append(cartella, titolo, descr);
        return a;
    }));
    nessunRisultato.hidden = trovati.length > 0;
}

if (casella) {
    casella.addEventListener('input', mostraRisultati);
    casella.addEventListener('keydown', e => {
        if (e.key === 'Enter') {
            const primo = elencoRisultati.querySelector('a.home-voce');
            if (primo && !boxRisultati.hidden) { e.preventDefault(); window.location.href = primo.href; }
        } else if (e.key === 'Escape') {
            casella.value = '';
            mostraRisultati();
        }
    });
    document.addEventListener('keydown', e => {
        if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
        const t = e.target;
        if (t instanceof HTMLInputElement || t instanceof HTMLTextAreaElement || t?.isContentEditable) return;
        if (document.querySelector('dialog[open]')) return;
        e.preventDefault();
        casella.focus();
    });
}
