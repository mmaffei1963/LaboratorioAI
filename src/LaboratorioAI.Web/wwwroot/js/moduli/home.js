// Pagina principale: cartelle con finestra, contatori e preferiti (stellina).

// ── Finestre delle cartelle: si aprono al clic e si chiudono con Esc, ✕ o clic fuori ──
for (const pulsante of document.querySelectorAll('[data-apri]')) {
    const finestra = document.getElementById(pulsante.dataset.apri);
    if (!finestra) continue;
    pulsante.addEventListener('click', () => finestra.showModal());
    // Clic sullo sfondo (fuori dal riquadro) = chiudi.
    finestra.addEventListener('click', e => {
        if (e.target === finestra) finestra.close();
    });
}

// ── Contatori («cose da fare») ──
// Chiesti dopo l'apertura: la home non li aspetta. Risposta: { "/Validazione": { numero, breve, testo }, ... }
// solo per le voci visibili e calcolate; un contatore mancante (fonte lenta o irraggiungibile) non compare.
let contatori = {};

function mostraContatoriVoci(radice = document) {
    for (const etichetta of radice.querySelectorAll('[data-conta-voce]')) {
        const c = contatori[etichetta.dataset.contaVoce];
        if (!c || c.numero <= 0) { etichetta.hidden = true; continue; }
        etichetta.textContent = `${c.numero.toLocaleString('it-IT')} ${c.breve}`;
        etichetta.title = c.testo;
        etichetta.hidden = false;
    }
}

async function caricaContatori() {
    try {
        const r = await fetch('/api/home/contatori', { headers: { Accept: 'application/json' } });
        if (!r.ok) return;
        contatori = await r.json();
    } catch {
        return;
    }
    mostraContatoriVoci();

    // Bollino della cartella = somma dei contatori delle sue funzioni.
    for (const bollino of document.querySelectorAll('[data-conta-cartella]')) {
        const finestra = document.querySelector(`dialog[data-cartella="${bollino.dataset.contaCartella}"]`);
        if (!finestra) continue;
        const parti = [...finestra.querySelectorAll('[data-conta-voce]')]
            .map(e => contatori[e.dataset.contaVoce])
            .filter(c => c && c.numero > 0);
        const totale = parti.reduce((s, c) => s + c.numero, 0);
        if (totale <= 0) continue;
        bollino.textContent = totale > 999 ? '999+' : totale.toLocaleString('it-IT');
        const descrizione = parti.map(c => `${c.numero} ${c.breve}`).join(', ');
        bollino.title = descrizione;
        bollino.setAttribute('aria-label', `Cose da fare: ${descrizione}`);
        bollino.hidden = false;
    }
}

// ── Preferiti ──
// La stellina accanto a ogni funzione salva la scelta per l'utente (api/home/preferiti) e aggiorna la riga
// «Preferiti» in cima alla home, senza ricaricare la pagina.
const elencoPreferiti = document.getElementById('preferiti-elenco');
const avvisoVuoto = document.getElementById('preferiti-vuoto');

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

    const conta = document.createElement('em');
    conta.className = 'home-conta-voce';
    conta.dataset.contaVoce = url;
    conta.hidden = true;

    a.append(icona, testo, conta);
    return a;
}

function aggiornaVuoto() {
    if (avvisoVuoto && elencoPreferiti) avvisoVuoto.hidden = elencoPreferiti.children.length > 0;
}

function segnaStelle(url, attiva) {
    for (const b of document.querySelectorAll(`[data-stella="${CSS.escape(url)}"]`)) {
        b.setAttribute('aria-pressed', attiva ? 'true' : 'false');
        b.title = attiva ? 'Togli dai preferiti' : 'Aggiungi ai preferiti';
    }
}

for (const stella of document.querySelectorAll('[data-stella]')) {
    stella.addEventListener('click', async () => {
        const url = stella.dataset.stella;
        const attiva = stella.getAttribute('aria-pressed') !== 'true';
        segnaStelle(url, attiva); // subito a video; si annulla se il salvataggio non riesce
        stella.disabled = true;
        try {
            const r = await fetch('/api/home/preferiti', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                body: JSON.stringify({ url, preferito: attiva })
            });
            if (!r.ok) throw new Error(`Errore ${r.status}`);
            if (elencoPreferiti) {
                const esistente = elencoPreferiti.querySelector(`[data-url="${CSS.escape(url)}"]`);
                if (attiva && !esistente) {
                    const nuovo = creaPreferito(url);
                    if (nuovo) { elencoPreferiti.append(nuovo); mostraContatoriVoci(nuovo); }
                } else if (!attiva && esistente) {
                    esistente.remove();
                }
                aggiornaVuoto();
            }
        } catch {
            segnaStelle(url, !attiva);
            stella.title = 'Non è stato possibile salvare il preferito: riprova.';
        } finally {
            stella.disabled = false;
        }
    });
}

// ── Ricerca delle funzioni ──
// Tutto nel browser: l'indice si costruisce dalle funzioni già presenti nelle finestre (quindi solo quelle che
// l'utente vede). Ricerca senza accenti e maiuscole; ogni parola scritta deve comparire in nome, descrizione o cartella.
const casella = document.getElementById('home-cerca');
const boxRisultati = document.getElementById('home-risultati');
const elencoRisultati = document.getElementById('home-risultati-elenco');
const nessunRisultato = document.getElementById('home-risultati-vuoto');
const boxCartelle = document.getElementById('home-cartelle');

const semplifica = t => (t || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();

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
    if (trovati === null) {
        boxRisultati.hidden = true;
        boxCartelle.hidden = false;
        return;
    }
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
        const conta = document.createElement('em');
        conta.className = 'home-conta-voce';
        conta.dataset.contaVoce = v.url;
        conta.hidden = true;
        a.append(cartella, titolo, descr, conta);
        return a;
    }));
    nessunRisultato.hidden = trovati.length > 0;
    mostraContatoriVoci(elencoRisultati);
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
    // «/» porta alla casella di ricerca (se non si sta già scrivendo altrove).
    document.addEventListener('keydown', e => {
        if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
        const t = e.target;
        if (t instanceof HTMLInputElement || t instanceof HTMLTextAreaElement || t?.isContentEditable) return;
        if (document.querySelector('dialog[open]')) return;
        e.preventDefault();
        casella.focus();
    });
}

caricaContatori();
