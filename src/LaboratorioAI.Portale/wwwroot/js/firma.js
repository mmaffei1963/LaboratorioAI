// Pagina di approvazione delle Condizioni Generali (10/10/2026). Ogni passo viene inviato al server, che lo registra
// nel registro delle evidenze; al momento della conferma è il server a verificare che i passi siano stati fatti.
// Dal 15/10/2026 si firma con un codice OTP inviato per e-mail; tolte la firma grafica e la finestra di doppia conferma.

const $ = (id) => document.getElementById(id);
const pagina = $('pagina');
const codice = pagina.dataset.codice;
const conOfferta = pagina.dataset.offerta === 'true';
const base = `/Firma/${encodeURIComponent(codice)}`;

async function invia(percorso, corpo) {
    const r = await fetch(base + percorso, {
        method: 'POST', headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
        body: JSON.stringify(corpo)
    });
    const dati = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(dati.messaggio || `Errore ${r.status}`);
    return dati;
}
const passo = (tipo) => invia('/passo', { tipo }).catch(e => mostraErrore(e.message));

function mostraErrore(testo) {
    const el = $('errore-firma');
    el.textContent = testo || '';
    el.classList.toggle('nascosto', !testo);
}

// ── 1. Documenti ─────────────────────────────────────────────
const aperti = new Set();
document.querySelectorAll('.documento-firma').forEach(a => a.addEventListener('click', () => {
    // L'apertura la registra il server quando serve il PDF; qui si aggiorna solo la pagina.
    aperti.add(a.dataset.documento);
    a.classList.add('aperto');
    a.querySelector('.aperto-il').textContent = 'Aperto il ' + new Date().toLocaleString('it-IT');
    aggiorna();
}));
const documentiAperti = () => aperti.has('condizioni') && (!conOfferta || aperti.has('offerta'));

// ── 2-4. Caselle e dati ──────────────────────────────────────
const c1 = $('casella1'), c2 = $('casella2'), c3 = $('casella3');
[c1, c2, c3].forEach((c, i) => c.addEventListener('change', () => {
    passo(`casella${i + 1}.${c.checked ? 'spuntata' : 'tolta'}`);
    // Togliendo la casella 1 si toglie anche la 2 (la 2 si spunta solo dopo la 1).
    if (i === 0 && !c.checked && c2.checked) { c2.checked = false; passo('casella2.tolta'); }
    aggiorna();
}));

function qualifica() {
    const v = $('f-qualifica').value;
    return v === 'altro' ? $('f-qualifica-altro').value.trim() : v;
}
function datiCompleti() { return $('f-nome').value.trim() && $('f-cognome').value.trim() && qualifica(); }

/** Stesso testo di TestiApprovazione.Casella3 sul server: quello registrato è comunque il testo del server. */
function testoCasella3() {
    const nome = `${$('f-nome').value.trim() || '[nome]'} ${$('f-cognome').value.trim() || '[cognome]'}`;
    const rs = pagina.dataset.ragioneSociale || 'il Cliente';
    const piva = pagina.dataset.partitaIva ? ` (P. IVA ${pagina.dataset.partitaIva})` : '';
    return `Dichiaro di essere ${nome}, ${qualifica() || '[qualifica]'}, e di avere i poteri per impegnare ${rs}${piva}. ` +
        `Sono consapevole che dell'approvazione verrà conservata evidenza e che la ricevuta sarà inviata all'indirizzo ${pagina.dataset.email}.`;
}

['f-nome', 'f-cognome', 'f-qualifica-altro'].forEach(id => $(id).addEventListener('input', () => {
    // Se i dati cambiano dopo la spunta, la casella 3 va rispuntata: il testo dichiarato è cambiato.
    if (c3.checked) { c3.checked = false; passo('casella3.tolta'); }
    aggiorna();
}));
$('f-qualifica').addEventListener('change', () => {
    $('campo-qualifica-altro').classList.toggle('nascosto', $('f-qualifica').value !== 'altro');
    if (c3.checked) { c3.checked = false; passo('casella3.tolta'); }
    aggiorna();
});

function pronto() { return documentiAperti() && c1.checked && c2.checked && c3.checked && datiCompleti(); }

function aggiorna() {
    const doc = documentiAperti();
    c1.disabled = !doc;
    c2.disabled = !doc || !c1.checked;
    c3.disabled = !doc || !c2.checked || !datiCompleti();
    $('testo-casella3').textContent = testoCasella3();
    $('btn-codice').disabled = !pronto() || attesa > 0;
    $('nota-codice').classList.toggle('nascosto', pronto());
    $('btn-conferma').disabled = !pronto() || codiceInserito().length !== 6;
}

// ── 5. Codice OTP per e-mail (firma elettronica, 15/10/2026) ──
// Il codice è legato ai dati dichiarati: se cambiano dopo l'invio, il server ne chiede uno nuovo.
let attesa = 0, timer = null;
const codiceInserito = () => $('f-codice').value.replace(/\D/g, '');

function dati() { return { nome: $('f-nome').value.trim(), cognome: $('f-cognome').value.trim(), qualifica: qualifica() }; }

async function chiediCodice() {
    mostraErrore('');
    const b = $('btn-codice'); b.disabled = true; const t = b.textContent; b.textContent = 'Invio in corso…';
    try {
        const r = await invia('/codice', dati());
        $('area-codice').classList.remove('nascosto');
        b.textContent = 'Codice inviato a ' + r.a;
        $('f-codice').value = ''; $('f-codice').focus();
        attesa = 60; clearInterval(timer);
        timer = setInterval(() => { attesa--; if (attesa <= 0) { clearInterval(timer); b.textContent = 'Inviami un nuovo codice'; } aggiorna(); }, 1000);
    } catch (e) { b.textContent = t; mostraErrore(e.message); }
    aggiorna();
}
$('btn-codice').addEventListener('click', chiediCodice);
$('link-reinvia').addEventListener('click', (e) => { e.preventDefault(); if (attesa > 0) mostraErrore(`Attendi ancora ${attesa} secondi.`); else chiediCodice(); });
$('f-codice').addEventListener('input', aggiorna);
$('f-codice').addEventListener('keydown', (e) => { if (e.key === 'Enter' && !$('btn-conferma').disabled) conferma(); });

async function conferma() {
    mostraErrore('');
    const b = $('btn-conferma'); b.disabled = true; b.textContent = 'Registrazione…';
    try {
        await invia('/conferma', { ...dati(), codice: codiceInserito() });
        document.querySelectorAll('.blocco-firma:not(#esito-ok)').forEach(s => s.classList.add('nascosto'));
        $('esito-ok').classList.remove('nascosto');
        window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (e) {
        mostraErrore(e.message);
    } finally { b.textContent = 'Confermo e firmo con il codice'; aggiorna(); }
}
$('btn-conferma').addEventListener('click', conferma);

aggiorna();
