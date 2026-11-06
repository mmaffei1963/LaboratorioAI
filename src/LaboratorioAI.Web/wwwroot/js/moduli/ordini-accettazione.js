// Ordini di Accettazione (rivista il 14/10/2026). Gli Ordini per il Portale li genera Palomar da ArcLab:
// qui restano l'anteprima di qualsiasi lotto, la stampa «da firmare» per i clienti privati, il registro
// «Consegnati ai privati» (ricerca per lotto, divisi fra invio per e-mail e ritiro a mano) e il registro di controllo.
import { disegnaRegistro } from './registro-documenti.js';

const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const data = s => s ? s.split('-').reverse().join('/') : '';
const dataOra = s => s ? new Date(s).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) : '';
const NUMERO_CONSEGNA = { Email: 1, AMano: 2, Entrambi: 3 };
const NOME_CONSEGNA = { Email: 'e-mail', AMano: 'a mano', Entrambi: 'e-mail e a mano' };

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}

function msg(testo, errore = false) {
    const el = $('msg');
    el.textContent = testo || '';
    el.style.color = errore ? '#b00' : '';
}

// ── Lotti ricevuti ──

function paramsPeriodo() {
    const p = new URLSearchParams();
    if ($('f-dal').value) p.set('dal', $('f-dal').value);
    if ($('f-al').value) p.set('al', $('f-al').value);
    const testo = $('f-cliente').value.trim();
    const cliente = clienteScelto();
    if (cliente) p.set('idCliente', cliente.id); else if (testo) p.set('cliente', testo);
    if (cliente && $('f-insediamento').value) p.set('idInsediamento', $('f-insediamento').value);
    return p;
}

// ── Tendine cliente / insediamento (anagrafica ArcLab) ──
// Nell'elenco i nomi doppi sono resi unici con il numero ArcLab del cliente.
let clienti = [];
const etichettaCliente = c => c.etichetta;
const clienteScelto = () => { const v = $('f-cliente').value.trim().toLowerCase(); return v ? clienti.find(c => c.etichetta.toLowerCase() === v) : null; };

async function caricaClienti() {
    try {
        const elenco = await api('/api/ordini-accettazione/clienti');
        const conta = {};
        for (const c of elenco) { const k = (c.nome || '').trim().toLowerCase(); conta[k] = (conta[k] || 0) + 1; }
        clienti = elenco.map(c => ({ id: c.id, etichetta: conta[(c.nome || '').trim().toLowerCase()] > 1 ? `${(c.nome || '').trim()} · n. ${c.id}` : (c.nome || '').trim() }));
        $('lista-clienti').innerHTML = clienti.map(c => `<option value="${esc(etichettaCliente(c))}">`).join('');
    } catch { /* senza elenco resta la ricerca per parte del nome */ }
}

let ultimoCliente = null;
async function aggiornaInsediamenti() {
    const c = clienteScelto();
    if ((c?.id ?? null) === ultimoCliente) return;
    ultimoCliente = c?.id ?? null;
    const sel = $('f-insediamento');
    if (!c) { sel.innerHTML = '<option value="">— scegli prima il cliente —</option>'; sel.disabled = true; return; }
    sel.disabled = true; sel.innerHTML = '<option value="">caricamento…</option>';
    try {
        const ins = await api('/api/ordini-accettazione/insediamenti?idCliente=' + c.id);
        sel.innerHTML = '<option value="">— tutti —</option>' + ins.map(i => `<option value="${i.id}">${esc(i.nome)}</option>`).join('');
        sel.disabled = false;
    } catch { sel.innerHTML = '<option value="">— errore —</option>'; }
}

let ultimiLotti = [];

async function caricaLotti() {
    const filtro = $('f-cliente').value.trim();
    if (!filtro && !$('f-dal').value) { msg('Indica la data «Dal», oppure un cliente o un insediamento.', true); return; }
    msg('Caricamento…');
    try {
        ultimiLotti = await api('/api/ordini-accettazione/lotti?' + paramsPeriodo().toString());
        render();
        const pronti = ultimiLotti.filter(l => l.pronto).length;
        const privati = ultimiLotti.filter(l => l.privato).length;
        msg(`${ultimiLotti.length} lotti trovati, ${pronti} pronti, ${privati} già stampati per un privato.`
            + (ultimiLotti.length >= 2000 ? ' Mostrati solo i 2000 più recenti: restringi la ricerca.' : ''));
    } catch (e) { msg(e.message, true); }
}

function etichettaStato(l) {
    if (!l.pronto) {
        const mancanti = l.numeroCampioni - l.campioniConParametri;
        return `<span class="etichetta" style="background:#f8d7da;color:#842029" title="Campioni senza parametri caricati">Senza parametri (${mancanti}/${l.numeroCampioni})</span>`;
    }
    if (l.privato) {
        const p = l.privato;
        return `<span class="etichetta" style="background:#e7f5e7;color:#256029" title="Ultima stampa ${esc(dataOra(p.dataStampa))}">Privato · ${esc(NOME_CONSEGNA[p.consegna] || '')}${p.firmatoIl ? ' · firmato' : ' · da firmare'}</span>`;
    }
    return '<span class="etichetta" style="background:#e8eef7;color:#1f3b63">Pronto</span>';
}

function render() {
    const mostraNonPronti = $('filtro-non-pronti')?.checked;
    const tb = $('tabella-lotti').querySelector('tbody');
    tb.innerHTML = '';
    for (const l of ultimiLotti.filter(l => mostraNonPronti || l.pronto)) {
        const tr = document.createElement('tr');
        const azioni = l.pronto
            ? `<a class="pulsante pulsante-tenue" href="/api/ordini-accettazione/${l.idLotto}/documento?formato=pdf" target="_blank" title="Anteprima dell'Ordine (versione Portale)">Anteprima</a>
               <a class="pulsante pulsante-tenue" href="/OrdiniAccettazione/Privato/${l.idLotto}" title="Ordine da firmare per un cliente privato">Stampa da firmare</a>`
            : `<span class="sottotesto">non generabile</span>`;
        if (!l.pronto) tr.style.opacity = '0.7';
        tr.innerHTML = `
            <td>${esc(l.codLotto)}</td>
            <td>${esc(data(l.dataRicezione))}</td>
            <td>${esc(l.cliente || '')}</td>
            <td>${esc(l.insediamento || '')}${l.insediamentoCitta ? ` <span class="sottotesto">${esc(l.insediamentoCitta)}</span>` : ''}</td>
            <td class="numerico">${l.campioniConParametri}/${l.numeroCampioni}</td>
            <td>${etichettaStato(l)}</td>
            <td style="white-space:nowrap">${azioni}</td>`;
        tb.appendChild(tr);
    }
}

// ── Consegnati ai privati ──

let righePrivati = [];

async function caricaPrivati() {
    const p = new URLSearchParams();
    const cerca = $('p-cerca').value.trim();
    if (cerca) p.set('cerca', cerca); else p.set('giorni', $('p-giorni').value);
    $('p-msg').textContent = 'Caricamento…'; $('p-msg').style.color = '';
    try {
        righePrivati = await api('/api/ordini-accettazione/privati?' + p.toString());
        const email = righePrivati.filter(r => r.perEmail), mano = righePrivati.filter(r => !r.perEmail);
        disegnaPrivati($('tabella-privati-email'), email);
        disegnaPrivati($('tabella-privati-mano'), mano);
        const daFirmare = righePrivati.filter(r => !r.firmatoIl).length;
        $('p-msg').textContent = righePrivati.length
            ? `${righePrivati.length} ordini: ${email.length} per e-mail, ${mano.length} a mano` + (daFirmare ? ` — ${daFirmare} senza data di firma.` : '.')
            : (cerca ? `Nessun ordine da firmare trovato per «${cerca}».` : 'Nessun ordine da firmare nel periodo.');
    } catch (e) { $('p-msg').textContent = e.message; $('p-msg').style.color = '#b00'; }
}

function disegnaPrivati(tabella, righe) {
    const tb = tabella.querySelector('tbody');
    tb.innerHTML = righe.length ? '' : '<tr><td colspan="8" class="sottotesto">Nessuno.</td></tr>';
    for (const r of righe) {
        const tr = document.createElement('tr');
        const email = r.email
            ? `<a href="mailto:${esc(r.email)}?subject=${encodeURIComponent('Rapporto di Prova – lotto ' + (r.codLotto || ''))}">${esc(r.email)}</a>
               <button type="button" class="pulsante pulsante-tenue btn-copia" title="Copia l'indirizzo">Copia</button>`
            : '<span class="sottotesto">—</span>';
        tr.innerHTML = `
            <td style="white-space:nowrap"><strong>${esc(r.codLotto || '')}</strong>${r.consegna === 'Entrambi' ? ' <span class="sottotesto">(anche a mano)</span>' : ''}</td>
            <td>${esc(data(r.dataRicezione))}</td>
            <td>${esc(r.cliente || '')}</td>
            <td class="numerico">${r.numeroCampioni}</td>
            <td style="white-space:nowrap">${email}</td>
            <td>${r.firmatoIl ? esc(data(r.firmatoIl)) : '<span style="color:#b00">non firmato</span>'}</td>
            <td>${esc(r.note || '')}</td>
            <td style="white-space:nowrap">
                <a class="pulsante pulsante-tenue" href="/api/ordini-accettazione/privati/${r.id}/pdf" target="_blank" title="Stampato ${esc(dataOra(r.dataStampa))} da ${esc(r.utente || '')}">PDF</a>
                <button type="button" class="pulsante pulsante-tenue btn-modifica">Modifica</button>
            </td>`;
        tr.querySelector('.btn-copia')?.addEventListener('click', async e => {
            try { await navigator.clipboard.writeText(r.email); e.target.textContent = 'Copiato'; } catch { e.target.textContent = 'Non copiato'; }
            setTimeout(() => e.target.textContent = 'Copia', 1500);
        });
        tr.querySelector('.btn-modifica').addEventListener('click', () => apriModifica(r));
        tb.appendChild(tr);
    }
}

let inModifica = null;

function apriModifica(r) {
    inModifica = r;
    $('dlg-titolo').textContent = `Lotto ${r.codLotto || ''} — ${r.cliente || ''}`;
    $('dlg-consegna').value = String(NUMERO_CONSEGNA[r.consegna] || 2);
    $('dlg-email').value = r.email || '';
    $('dlg-firmato').value = r.firmatoIl || '';
    $('dlg-note').value = r.note || '';
    $('dlg-msg').textContent = '';
    $('dlg-privato').showModal();
}

async function salvaModifica() {
    const consegna = Number($('dlg-consegna').value);
    const email = $('dlg-email').value.trim();
    if (consegna !== 2 && !email) { $('dlg-msg').textContent = 'Per l\'invio per e-mail serve l\'indirizzo.'; $('dlg-msg').style.color = '#b00'; return; }
    try {
        await api(`/api/ordini-accettazione/privati/${inModifica.id}`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: email || null, consegna, firmatoIl: $('dlg-firmato').value || null, note: $('dlg-note').value })
        });
        $('dlg-privato').close();
        await caricaPrivati();
    } catch (e) { $('dlg-msg').textContent = e.message; $('dlg-msg').style.color = '#b00'; }
}

// ── Registro di controllo ──

async function caricaRegistro() {
    const p = new URLSearchParams({ giorni: $('reg-giorni').value, soloProblemi: $('reg-solo-problemi').checked });
    $('reg-msg').textContent = 'Caricamento…';
    try {
        const righe = await api('/api/ordini-accettazione/registro?' + p.toString());
        disegnaRegistro($('tabella-registro'), righe);
        $('reg-msg').textContent = `${righe.length} righe.`;
        $('reg-msg').style.color = '';
    } catch (e) { $('reg-msg').textContent = e.message; $('reg-msg').style.color = '#b00'; }
}

$('btn-carica').addEventListener('click', caricaLotti);
$('f-cliente').addEventListener('input', aggiornaInsediamenti);
$('f-cliente').addEventListener('change', aggiornaInsediamenti);
$('f-cliente').addEventListener('keydown', e => { if (e.key === 'Enter') { aggiornaInsediamenti(); caricaLotti(); } });
$('f-insediamento').addEventListener('change', caricaLotti);
$('filtro-non-pronti')?.addEventListener('change', render);
$('btn-privati').addEventListener('click', caricaPrivati);
$('p-cerca').addEventListener('keydown', e => { if (e.key === 'Enter') caricaPrivati(); });
$('p-giorni').addEventListener('change', caricaPrivati);
$('dlg-annulla').addEventListener('click', () => $('dlg-privato').close());
$('dlg-salva').addEventListener('click', salvaModifica);
$('btn-registro').addEventListener('click', caricaRegistro);
$('reg-giorni').addEventListener('change', caricaRegistro);
$('reg-solo-problemi').addEventListener('change', caricaRegistro);

// Default: lotti degli ultimi 7 giorni; ricerca per lotto da ?lotto= (link diretti).
(function init() {
    const oggi = new Date();
    const dal = new Date(oggi); dal.setDate(dal.getDate() - 7);
    $('f-al').value = oggi.toISOString().slice(0, 10);
    $('f-dal').value = dal.toISOString().slice(0, 10);
    const lotto = new URLSearchParams(location.search).get('lotto');
    if (lotto) $('p-cerca').value = lotto;
    caricaPrivati();
    caricaClienti();
    caricaLotti();
    caricaRegistro();
})();
