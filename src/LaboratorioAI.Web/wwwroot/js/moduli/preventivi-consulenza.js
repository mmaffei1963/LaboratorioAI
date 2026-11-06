// Preventivi di consulenza: filtro dell'elenco e ricerca del cliente ArcLab nel modulo.
const $ = id => document.getElementById(id);

// Filtro dell'elenco
const filtro = $('filtro-preventivi');
const tabella = $('tabella-preventivi');
if (filtro && tabella) {
    const righe = Array.from(tabella.tBodies[0].rows);
    filtro.addEventListener('input', () => {
        const t = filtro.value.trim().toLowerCase();
        for (const r of righe) r.style.display = t === '' || r.textContent.toLowerCase().includes(t) ? '' : 'none';
    });
}

// Ricerca cliente: suggerimenti da ArcLab, l'id scelto va nel campo nascosto e si carica con il pulsante.
const cerca = $('cerca-cliente');
const elenco = $('elenco-clienti-arclab');
const idScelto = $('id-cliente-scelto');
const pulsante = $('carica-cliente');
let trovati = [];
let timer = null;

if (cerca && elenco && idScelto) {
    const norm = x => (x ?? '').trim().toLowerCase();
    // Se il testo coincide con un nome dell'elenco, l'id va nel campo nascosto;
    // altrimenti il server cerca il cliente dal testo scritto.
    const abbina = () => {
        const t = norm(cerca.value);
        const uguali = trovati.filter(c => norm(c.nome) === t);
        idScelto.value = uguali.length === 1 ? uguali[0].id : '';
        return uguali.length === 1;
    };
    cerca.addEventListener('change', abbina);
    cerca.addEventListener('input', () => {
        clearTimeout(timer);
        const t = cerca.value.trim();
        if (abbina() || t.length < 2) return;
        timer = setTimeout(async () => {
            try {
                const r = await fetch('/PreventiviConsulenza/Clienti?filtro=' + encodeURIComponent(t));
                if (!r.ok) return;
                trovati = await r.json();
                // Nomi uguali (clienti doppi in ArcLab): si distinguono con il numero cliente.
                const conteggio = {};
                for (const c of trovati) conteggio[norm(c.nome)] = (conteggio[norm(c.nome)] ?? 0) + 1;
                for (const c of trovati) if (conteggio[norm(c.nome)] > 1) c.nome = `${c.nome} (n. ${c.id})`;
                elenco.innerHTML = '';
                for (const c of trovati) {
                    const o = document.createElement('option');
                    o.value = c.nome;
                    elenco.appendChild(o);
                }
                abbina();
            } catch { /* ricerca non disponibile: decide il server dal testo */ }
        }, 250);
    });
    // Invio nella casella di ricerca = carica il cliente (non il salvataggio generico).
    cerca.addEventListener('keydown', e => {
        if (e.key === 'Enter' && pulsante) { e.preventDefault(); abbina(); pulsante.click(); }
    });
}

// Campi del prezzo successivo visibili solo per i canoni annuali.
for (const sel of document.querySelectorAll('select[data-modalita]')) {
    const riga = sel.closest('[data-riga]');
    const aggiorna = () => {
        const annuale = sel.value === 'Annuale';
        for (const el of riga.querySelectorAll('.solo-annuale')) el.style.display = annuale ? '' : 'none';
    };
    sel.addEventListener('change', aggiorna);
    aggiorna();
}

// Ricerca dei controlli ArcLab per le analisi di pacchetti e preventivi: al clic l'elenco di tutti i controlli
// attivi (caricato una volta), che si restringe mentre si scrive (tutte le parole, anche l'ID). Scelto un
// controllo, si aggiunge subito.
const normalizza = x => (x ?? '').toString().toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
const escHtml = x => (x ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
let catalogo = null;
async function caricaCatalogo() {
    if (catalogo) return catalogo;
    const r = await fetch('/PreventiviConsulenza/Controlli');
    if (!r.ok) throw new Error('ArcLab non raggiungibile');
    catalogo = (await r.json()).map(c => ({ ...c, chiave: normalizza(`${c.id} ${c.controllo}`) }));
    return catalogo;
}

for (const campo of document.querySelectorAll('input[data-cerca-controllo]')) {
    const contenitore = campo.closest('label');
    const pannello = contenitore.querySelector('[data-elenco-controlli]');
    const idNascosto = contenitore.querySelector('[data-id-controllo]');
    const modulo = campo.closest('form');
    const aggiungi = modulo?.querySelector('button[name=azione][value=aggiungi-controllo]');
    let visibili = [], evidenziata = -1;
    const MAX = 400;

    const scegli = c => {
        campo.value = c.nome;
        idNascosto.value = c.id;
        pannello.classList.add('nascosto');
        if (aggiungi) aggiungi.click();
    };
    const evidenzia = i => {
        const voci = pannello.querySelectorAll('.voce-suggerimento');
        voci.forEach(v => v.classList.remove('evidenziata'));
        evidenziata = Math.max(-1, Math.min(i, voci.length - 1));
        if (evidenziata >= 0) voci[evidenziata].classList.add('evidenziata'), voci[evidenziata].scrollIntoView({ block: 'nearest' });
    };
    const mostra = async () => {
        let elenco;
        try { elenco = await caricaCatalogo(); }
        catch (e) { pannello.innerHTML = `<div class="voce-suggerimento">${escHtml(e.message)}: scrivi l'ID del controllo e premi «Aggiungi controllo».</div>`; pannello.classList.remove('nascosto'); return; }
        const parole = normalizza(campo.value.replace(/\(ID \d+\)\s*$/, '')).split(/\s+/).filter(Boolean);
        const trovati = parole.length ? elenco.filter(c => parole.every(w => c.chiave.includes(w))) : elenco;
        visibili = trovati.slice(0, MAX);
        const intest = `<div class="sottotesto" style="padding:4px 14px">${trovati.length} controlli attivi${parole.length ? ' trovati' : ''}` +
            (trovati.length > MAX ? ` — ne mostro ${MAX}, scrivi per restringere` : '') + '</div>';
        pannello.innerHTML = intest + (visibili.length
            ? visibili.map((c, i) => `<div class="voce-suggerimento" data-i="${i}"><strong>ID ${c.id}</strong> — ${escHtml(c.controllo)}</div>`).join('')
            : '<div class="voce-suggerimento">Nessun controllo contiene queste parole.</div>');
        pannello.querySelectorAll('.voce-suggerimento[data-i]').forEach(el =>
            el.addEventListener('mousedown', ev => { ev.preventDefault(); scegli(visibili[+el.dataset.i]); }));
        evidenziata = -1;
        pannello.classList.remove('nascosto');
    };

    let attesa = null;
    campo.addEventListener('focus', mostra);
    campo.addEventListener('click', mostra);
    campo.addEventListener('input', () => { idNascosto.value = ''; clearTimeout(attesa); attesa = setTimeout(mostra, 120); });
    campo.addEventListener('blur', () => setTimeout(() => pannello.classList.add('nascosto'), 150));
    campo.addEventListener('keydown', e => {
        if (pannello.classList.contains('nascosto')) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); evidenzia(evidenziata + 1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); evidenzia(evidenziata - 1); }
        else if (e.key === 'Escape') pannello.classList.add('nascosto');
        else if (e.key === 'Enter' && (evidenziata >= 0 || visibili.length === 1)) {
            e.preventDefault(); e.stopPropagation();
            scegli(visibili[Math.max(0, evidenziata)]);
        }
    });
}

// Invio in un campo di testo: non deve premere il primo pulsante del modulo (che può essere «Togli»).
// Nella ricerca del controllo aggiunge il controllo, altrimenti salva.
for (const modulo of document.querySelectorAll('form')) {
    const salva = modulo.querySelector('button[type=submit]:not([name]), button[name=azione][value=salva]');
    if (!salva) continue;
    modulo.addEventListener('keydown', e => {
        if (e.key !== 'Enter' || e.target.tagName !== 'INPUT' || e.target.id === 'cerca-cliente') return;
        if (['checkbox', 'radio', 'submit', 'button'].includes(e.target.type)) return;
        e.preventDefault();
        const aggiungi = e.target.hasAttribute('data-cerca-controllo')
            ? modulo.querySelector('button[name=azione][value=aggiungi-controllo]') : null;
        (aggiungi ?? salva).click();
    });
}
