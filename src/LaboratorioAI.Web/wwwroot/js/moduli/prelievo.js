// Prelievo vocale — cattura offline dei dati del prelievo (lotto → campioni), riepilogo da digitare in ArcLab.
// Tutto lato client: stato in localStorage, nessuna chiamata al server.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}
const CHIAVE = 'prelievo-giro-v1';
const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
// Il riconoscimento vocale del browser funziona solo in contesto sicuro (HTTPS o localhost).
// Su http://IP non parte: in quel caso ci si affida al microfono della tastiera del telefono.
const MIC_DISPONIBILE = !!SR && window.isSecureContext;

// ── Stato ──
let giro = carica();

function carica() {
    try { const v = JSON.parse(localStorage.getItem(CHIAVE)); if (v && Array.isArray(v.lotti)) return v; } catch { }
    return { lotti: [] };
}
function salva() {
    localStorage.setItem(CHIAVE, JSON.stringify(giro));
    const t = new Date().toLocaleTimeString('it-IT');
    $('stato-salvataggio').textContent = `Salvato in locale alle ${t} · ${giro.lotti.length} lotto/i`;
}
function nuovoCampione() {
    return { denominazione: '', matrice: '', imballaggio: '', temperatura: '', dataOra: '', punto: '', puntoSconosciuto: false, shelfLife: false, ritardo: '', luogo: '', noteCampione: '' };
}
function nuovoLotto() { return { cliente: '', localita: '', controllo: '', note: '', campioni: [nuovoCampione()] }; }

// ── Rendering ──
function render() {
    const cont = $('lista-lotti');
    cont.innerHTML = '';
    giro.lotti.forEach((lotto, li) => cont.appendChild(costruisciLotto(lotto, li)));
    $('conteggio-giro').textContent = `${giro.lotti.length} lotto/i · ${giro.lotti.reduce((s, l) => s + l.campioni.length, 0)} campione/i`;
    salva();
}

function costruisciLotto(lotto, li) {
    const el = $('tpl-lotto').content.firstElementChild.cloneNode(true);
    el.querySelector('.titolo-lotto').textContent = `Lotto ${li + 1}` + (lotto.cliente ? ` — ${lotto.cliente}` : '');
    el.querySelectorAll('[data-f]').forEach(inp => legaCampo(inp, lotto, () => {
        el.querySelector('.titolo-lotto').textContent = `Lotto ${li + 1}` + (lotto.cliente ? ` — ${lotto.cliente}` : '');
    }));
    el.querySelector('.lotto-rm').addEventListener('click', () => { if (confirm('Rimuovere il lotto?')) { giro.lotti.splice(li, 1); render(); } });
    el.querySelector('.lotto-dup').addEventListener('click', () => { giro.lotti.splice(li + 1, 0, JSON.parse(JSON.stringify(lotto))); render(); });
    el.querySelector('.camp-add').addEventListener('click', () => { lotto.campioni.push(nuovoCampione()); render(); });

    const lc = el.querySelector('.lista-campioni');
    lotto.campioni.forEach((camp, ci) => lc.appendChild(costruisciCampione(camp, lotto, li, ci)));
    return el;
}

function costruisciCampione(camp, lotto, li, ci) {
    const el = $('tpl-campione').content.firstElementChild.cloneNode(true);
    const titolo = () => el.querySelector('.titolo-campione').textContent =
        `Campione ${ci + 1}` + (camp.denominazione ? ` — ${camp.denominazione.slice(0, 40)}` : '');
    titolo();

    const rigaRitardo = el.querySelector('.riga-ritardo');
    const aggiornaRitardo = () => rigaRitardo.style.display = camp.shelfLife ? '' : 'none';
    aggiornaRitardo();

    el.querySelectorAll('[data-f]').forEach(inp => legaCampo(inp, camp, () => {
        titolo(); aggiornaRitardo(); mostraMancanti(el, camp);
    }));
    mostraMancanti(el, camp);

    el.querySelector('.camp-rm').addEventListener('click', () => {
        if (lotto.campioni.length === 1) { alert('Il lotto deve avere almeno un campione.'); return; }
        if (confirm('Rimuovere il campione?')) { lotto.campioni.splice(ci, 1); render(); }
    });
    el.querySelector('.camp-dup').addEventListener('click', () => { lotto.campioni.splice(ci + 1, 0, JSON.parse(JSON.stringify(camp))); render(); });
    return el;
}

// Collega un input al modello; supporta text/number/checkbox + pulsante microfono.
function legaCampo(inp, obj, extra) {
    const f = inp.dataset.f;
    if (inp.type === 'checkbox') {
        inp.checked = !!obj[f];
        inp.addEventListener('change', () => { obj[f] = inp.checked; salva(); extra && extra(); });
    } else {
        inp.value = obj[f] ?? '';
        inp.addEventListener('input', () => { obj[f] = inp.value; salva(); extra && extra(); });
    }
    const mic = inp.parentElement.querySelector?.('.mic');
    if (mic) {
        if (!MIC_DISPONIBILE) mic.remove();          // su HTTP si usa il microfono della tastiera
        else mic.addEventListener('click', () => dettatura(inp));
    }
}

function mancantiCampione(camp) {
    const m = [];
    if (!camp.denominazione.trim()) m.push('Denominazione');
    if (!camp.matrice.trim()) m.push('Matrice');
    if (camp.temperatura === '' || camp.temperatura == null) m.push('Temperatura');
    if (camp.shelfLife && (camp.ritardo === '' || camp.ritardo == null)) m.push('Ritardo analisi');
    return m;
}
function mostraMancanti(el, camp) {
    const m = mancantiCampione(camp);
    const avvisoPunto = (!camp.punto.trim() && !camp.puntoSconosciuto) ? ' · Punto di prelievo non indicato (spunta "sconosciuto" se n.a.)' : '';
    const p = el.querySelector('.mancanti');
    p.textContent = (m.length ? 'Mancano: ' + m.join(', ') : (avvisoPunto ? '' : 'Completo')) + avvisoPunto;
    p.style.color = m.length ? '#b00' : '#7a5b00';
    // evidenzia i campi obbligatori vuoti
    el.querySelectorAll('[data-f]').forEach(inp => inp.classList.remove('campo-mancante'));
    m.forEach(nome => {
        const map = { 'Denominazione': 'denominazione', 'Matrice': 'matrice', 'Temperatura': 'temperatura', 'Ritardo analisi': 'ritardo' };
        const inp = el.querySelector(`[data-f="${map[nome]}"]`);
        if (inp) inp.classList.add('campo-mancante');
    });
}

// ── Dettatura vocale (dove supportata; altrimenti si usa il microfono della tastiera) ──
function dettatura(input) {
    if (!SR) return;
    const rec = new SR();
    rec.lang = 'it-IT'; rec.interimResults = false; rec.maxAlternatives = 1;
    rec.onresult = e => {
        const t = e.results[0][0].transcript;
        input.value = (input.value ? input.value + ' ' : '') + t;
        input.dispatchEvent(new Event('input', { bubbles: true }));
    };
    rec.onerror = () => { };
    try { rec.start(); } catch { }
}

// ── Riepilogo ──
function riepilogo() {
    const righe = [];
    righe.push('RIEPILOGO PRELIEVI — ' + new Date().toLocaleString('it-IT'));
    giro.lotti.forEach((l, li) => {
        righe.push('');
        righe.push(`LOTTO ${li + 1}`);
        righe.push(`  Cliente: ${l.cliente || '—'}`);
        righe.push(`  Località/Insediamento: ${l.localita || '—'}`);
        righe.push(`  Controllo: ${l.controllo || '—'}`);
        if (l.note?.trim()) righe.push(`  Note lotto: ${l.note}`);
        l.campioni.forEach((c, ci) => {
            const m = mancantiCampione(c);
            righe.push(`  Campione ${ci + 1}${m.length ? '  [INCOMPLETO: ' + m.join(', ') + ']' : ''}`);
            righe.push(`    Denominazione: ${c.denominazione || '—'}`);
            righe.push(`    Matrice: ${c.matrice || '—'}   Imballaggio: ${c.imballaggio || '—'}`);
            righe.push(`    Temperatura prelievo: ${c.temperatura === '' ? '—' : c.temperatura + ' °C'}`);
            if (c.dataOra) righe.push(`    Data/ora prelievo: ${c.dataOra.replace('T', ' ')}`);
            righe.push(`    Punto di prelievo: ${c.puntoSconosciuto ? 'sconosciuto/n.a.' : (c.punto || '—')}`);
            if (c.shelfLife) righe.push(`    Shelf life: sì — ritardo analisi ${c.ritardo || '—'} gg`);
            if (c.luogo?.trim()) righe.push(`    Luogo di prelievo: ${c.luogo}`);
            if (c.noteCampione?.trim()) righe.push(`    Note: ${c.noteCampione}`);
        });
    });
    return righe.join('\n');
}

// ── Azioni ──
$('btn-nuovo-lotto').addEventListener('click', () => { giro.lotti.push(nuovoLotto()); render(); window.scrollTo(0, document.body.scrollHeight); });
$('btn-riassunto').addEventListener('click', () => { $('riassunto').textContent = riepilogo(); });
$('btn-copia').addEventListener('click', async () => {
    const t = $('riassunto').textContent || riepilogo();
    try { await navigator.clipboard.writeText(t); $('stato-salvataggio').textContent = 'Riepilogo copiato negli appunti.'; }
    catch { $('riassunto').textContent = t; alert('Copia non riuscita: seleziona il testo manualmente.'); }
});
$('btn-stampa').addEventListener('click', () => {
    const t = $('riassunto').textContent || riepilogo();
    const w = window.open('', '_blank');
    w.document.write('<pre style="font-family:monospace;white-space:pre-wrap">' + t.replace(/[&<>]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c])) + '</pre>');
    w.document.close(); w.print();
});
$('btn-svuota').addEventListener('click', () => {
    if (!confirm('Eliminare TUTTI i prelievi salvati? L\'operazione non è annullabile.')) return;
    giro = { lotti: [] }; localStorage.removeItem(CHIAVE); $('riassunto').textContent = ''; render();
});

// ── Dettato libero (AI) ──
const CHIAVE_DETTATO = 'prelievo-dettato-v1';
const dettatoEl = $('dettato');
if (dettatoEl) {
    dettatoEl.value = localStorage.getItem(CHIAVE_DETTATO) || '';
    dettatoEl.addEventListener('input', () => localStorage.setItem(CHIAVE_DETTATO, dettatoEl.value));
}

function normalizzaCampione(c) {
    const b = nuovoCampione();
    for (const k of Object.keys(b)) if (c[k] != null) b[k] = c[k];
    b.temperatura = (b.temperatura ?? '').toString();
    b.ritardo = (b.ritardo ?? '').toString();
    return b;
}
function normalizzaLotto(l) {
    return {
        cliente: l.cliente || '', localita: l.localita || '', controllo: (l.controllo ?? '').toString(), note: l.note || '',
        campioni: Array.isArray(l.campioni) && l.campioni.length ? l.campioni.map(normalizzaCampione) : [nuovoCampione()]
    };
}

async function strutturaAI() {
    const testo = (dettatoEl?.value || '').trim();
    if (!testo) { $('domande-ai').innerHTML = '<p class="sottotesto" style="color:#b00">Detta o scrivi qualcosa prima di strutturare.</p>'; return; }
    if (!navigator.onLine) { $('domande-ai').innerHTML = '<p class="sottotesto" style="color:#b00">Sei offline: la strutturazione con AI richiede rete. I dati dettati restano salvati.</p>'; return; }
    $('domande-ai').innerHTML = '<p class="sottotesto">Elaborazione con AI…</p>';
    try {
        const r = await api('/api/prelievo/struttura', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ testo }) });
        const nuovi = Array.isArray(r.lotti) ? r.lotti.map(normalizzaLotto) : [];
        if (nuovi.length) {
            // se il giro è "vuoto" (un solo lotto senza dati) lo sostituisco, altrimenti aggiungo.
            const vuoto = giro.lotti.length === 1 && !giro.lotti[0].cliente && giro.lotti[0].campioni.every(c => !c.denominazione);
            giro.lotti = vuoto ? nuovi : giro.lotti.concat(nuovi);
            render();
        }
        const domande = Array.isArray(r.domande) ? r.domande : [];
        $('domande-ai').innerHTML = domande.length
            ? '<p style="font-weight:600;margin:0.5rem 0 0.2rem">L\'AI chiede:</p><ul>' + domande.map(d => `<li>${esc(d)}</li>`).join('') + '</ul>'
            : '<p class="sottotesto" style="color:#256029">Struttura completata: nessun dato obbligatorio mancante.</p>';
    } catch (e) {
        $('domande-ai').innerHTML = `<p class="sottotesto" style="color:#b00">${esc(e.message)}</p>`;
    }
}

$('btn-struttura')?.addEventListener('click', strutturaAI);
$('btn-svuota-dettato')?.addEventListener('click', () => { if (dettatoEl) { dettatoEl.value = ''; localStorage.removeItem(CHIAVE_DETTATO); } $('domande-ai').innerHTML = ''; });

// Service worker (installabile + offline) — solo in contesto sicuro (HTTPS/localhost).
if ('serviceWorker' in navigator && window.isSecureContext) {
    navigator.serviceWorker.register('/sw.js').catch(() => { });
}

// ── Avvio ──
if (!MIC_DISPONIBILE) {
    document.body.classList.add('senza-voce');
    $('stato-salvataggio').textContent = 'Suggerimento: tocca un campo e usa il microfono della tastiera del telefono per dettare (funziona anche offline).';
}
if (giro.lotti.length === 0) giro.lotti.push(nuovoLotto());
render();
