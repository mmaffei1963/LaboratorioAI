// Cruscotto Direzione (13/10/2026): andamento del fatturato per territorio e confronto sede di Pesaro.
import { montaCommentoAi } from './commento-ai-direzione.js';
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const euro = v => v == null ? '—' : Math.round(Number(v)).toLocaleString('it-IT') + ' €';
const pct = v => v == null ? '—' : (Number(v) >= 0 ? '+' : '') + (Number(v) * 100).toLocaleString('it-IT', { maximumFractionDigits: 1 }) + '%';
const dataOra = s => s ? new Date(s).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) : '—';
const FILTRI = ['dal', 'al', 'livello', 'regione', 'provincia', 'centro', 'voce', 'commessa', 'zona', 'commerciale'];
let elenchi = null;

function parametri() {
    const p = new URLSearchParams();
    for (const f of FILTRI) { const v = $('f-' + f).value; if (v) p.set(f, v); }
    return p;
}

function riempi(id, voci, valore, testo) {
    const sel = $(id), attuale = sel.value;
    sel.length = 1;
    for (const v of voci) { const o = document.createElement('option'); o.value = valore(v); o.textContent = testo(v); sel.appendChild(o); }
    sel.value = attuale;
}

function riempiProvince() {
    const reg = $('f-regione').value;
    const prov = (elenchi?.regioni || []).filter(r => !reg || r.nome === reg).flatMap(r => r.province).sort();
    riempi('f-provincia', prov, p => p, p => p);
}

async function carica() {
    $('tabella-zone').tBodies[0].innerHTML = '<tr><td class="sottotesto">Calcolo…</td></tr>';
    try {
        const r = await fetch('/api/direzione/territorio?' + parametri().toString());
        const j = await r.json();
        if (!r.ok) throw new Error(j.messaggio || 'Errore');
        if (!elenchi) {
            elenchi = j.filtri;
            riempi('f-centro', elenchi.centri, c => c.codice, c => `${c.codice} — ${c.descrizione || ''}`);
            riempi('f-voce', elenchi.voci, c => c.codice, c => `${c.codice} — ${c.descrizione || ''}`);
            riempi('f-commessa', elenchi.commesse, c => c.codice, c => `${c.descrizione || c.codice} (${c.codice})`);
            riempi('f-zona', elenchi.zone, c => c.codice, c => c.descrizione ? `${c.descrizione} (${c.codice})` : c.codice);
            riempi('f-commerciale', elenchi.commerciali, c => c.idCommerciale, c => c.nome);
            riempi('f-regione', elenchi.regioni, r => r.nome, r => r.nome);
            riempiProvince();
            $('f-dal').value = j.anni[0]; $('f-al').value = j.anni[j.anni.length - 1];
        }
        disegnaAggiornamento(j.aggiornamento);
        disegnaPesaro(j.pesaro, j.infoPesaro);
        disegnaZone(j);
    } catch (e) {
        $('tabella-zone').tBodies[0].innerHTML = `<tr><td style="color:#b00">${esc(e.message)}</td></tr>`;
    }
}

function disegnaAggiornamento(a) {
    $('aggiornamento').innerHTML = a
        ? `Dati AZW del file aggiornato il <strong>${esc(dataOra(a.file))}</strong>, importati il ${esc(dataOra(a.importazione))}.`
          + (a.vecchio ? ' <span style="color:#b00;font-weight:600">Attenzione: il file AZW non si aggiorna da più di un giorno lavorativo.</span>' : '')
        : '<span style="color:#b00">Nessuna importazione AZW riuscita.</span>';
}

function disegnaPesaro(gruppi, info) {
    const tab = $('tabella-pesaro');
    const anni = gruppi[0]?.anni.map(a => a.anno) || [];
    tab.tHead.innerHTML = '<tr><th>Zona</th>' + anni.map(a => `<th class="numerico"${a === info.annoApertura ? ' style="border-left:2px solid #b3261e"' : ''}>${a}</th>`).join('')
        + `<th class="numerico">Crescita annua prima<br><span class="sottotesto">${info.annoInizio}–${info.annoApertura - 1}</span></th>`
        + `<th class="numerico">Crescita annua dopo<br><span class="sottotesto">${info.annoApertura - 1}–${info.ultimoAnno}</span></th></tr>`;
    tab.tBodies[0].innerHTML = gruppi.map(g => `<tr><td style="white-space:nowrap">${esc(g.nome)}</td>`
        + g.anni.map(a => `<td class="numerico"${a.anno === info.annoApertura ? ' style="border-left:2px solid #b3261e"' : ''}>${euro(a.fatturato)}<div class="sottotesto">${a.clientiAttivi} clienti</div></td>`).join('')
        + `<td class="numerico">${pct(g.crescitaPrima)}</td><td class="numerico" style="font-weight:600">${pct(g.crescitaDopo)}</td></tr>`).join('');
    const pu = gruppi[0], rn = gruppi[2];
    if (pu?.crescitaDopo != null && rn?.crescitaDopo != null) {
        const diffDopo = pu.crescitaDopo - rn.crescitaDopo;
        const diffPrima = pu.crescitaPrima != null && rn.crescitaPrima != null ? pu.crescitaPrima - rn.crescitaPrima : null;
        $('pesaro-testo').innerHTML = `Dall'apertura della sede (${info.annoApertura}) Pesaro e Urbino cresce del <strong>${pct(pu.crescitaDopo)}</strong> l'anno contro il `
            + `<strong>${pct(rn.crescitaDopo)}</strong> di Rimini: <strong>${pct(diffDopo).replace('%', ' punti')}</strong> di differenza`
            + (diffPrima != null ? `, prima dell'apertura erano ${pct(diffPrima).replace('%', ' punti')}.` : '.');
    } else $('pesaro-testo').textContent = '';
}

function disegnaZone(j) {
    const nomi = { Regione: 'regione', Provincia: 'provincia', Comune: 'comune' };
    $('titolo-elenco').textContent = 'Fatturato per ' + nomi[j.livello];
    const anni = j.anni, ultimo = anni[anni.length - 1];
    const tab = $('tabella-zone');
    tab.tHead.innerHTML = `<tr><th>${esc(j.livello)}</th>${j.livello !== 'Regione' ? '<th>Regione</th>' : ''}`
        + anni.map(a => `<th class="numerico">${a}</th>`).join('')
        + `<th class="numerico">Var. ${ultimo} su ${ultimo - 1}</th><th class="numerico">Clienti ${ultimo}<br><span class="sottotesto">attivi · nuovi · persi</span></th></tr>`;
    const riga = (etichetta, regione, valori, stile = '') => {
        const u = valori[valori.length - 1], p = valori[valori.length - 2];
        const varz = p && p.fatturato > 0 ? (u.fatturato - p.fatturato) / p.fatturato : null;
        return `<tr${stile}><td style="white-space:nowrap">${esc(etichetta)}</td>${j.livello !== 'Regione' ? `<td class="sottotesto">${esc(regione || '')}</td>` : ''}`
            + valori.map(v => `<td class="numerico">${euro(v.fatturato)}</td>`).join('')
            + `<td class="numerico" style="color:${varz == null ? 'inherit' : varz >= 0 ? '#1a7f37' : '#b3261e'}">${pct(varz)}</td>`
            + `<td class="numerico">${u.clientiAttivi} · <span style="color:#1a7f37">${u.clientiNuovi}</span> · <span style="color:#b3261e">${u.clientiPersi}</span>`
            + (u.clientiAcquisiti ? ` <span class="sottotesto" title="acquisiti da un nostro cliente: non persi">(+${u.clientiAcquisiti} acq.)</span>` : '') + `</td></tr>`;
    };
    tab.tBodies[0].innerHTML = (j.zone.map(z => riga(z.etichetta, z.regione, z.anni)).join('') || '<tr><td class="sottotesto">Nessun fatturato con questi filtri.</td></tr>')
        + riga('Totale', '', j.totale, ' style="font-weight:600;background:#f6f8fb"');
    $('conteggio').textContent = `${j.zone.length} righe`;
}

$('btn-cerca')?.addEventListener('click', carica);
$('f-regione')?.addEventListener('change', () => { $('f-provincia').value = ''; riempiProvince(); });
$('btn-azzera')?.addEventListener('click', () => { for (const f of FILTRI) if (!['dal', 'al', 'livello'].includes(f)) $('f-' + f).value = ''; riempiProvince(); carica(); });
$('btn-excel')?.addEventListener('click', () => { location.href = '/api/direzione/territorio/excel?' + parametri().toString(); });
$('btn-importa')?.addEventListener('click', async (e) => {
    const b = e.currentTarget; b.disabled = true; const t = b.textContent; b.textContent = 'Importazione in corso…';
    try {
        const r = await fetch('/api/direzione/importa-azw', { method: 'POST' });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.messaggio || 'Errore');
        await carica();
        $('aggiornamento').insertAdjacentHTML('afterbegin', `<span style="color:#1a7f37">${esc(j.messaggio)}</span> `);
    } catch (err) { $('aggiornamento').innerHTML = `<span style="color:#b00">${esc(err.message)}</span>`; }
    finally { b.disabled = false; b.textContent = t; }
});
carica();

// Commento AI (13/10/2026) sui filtri attuali.
const contenitoreAi = document.getElementById('commento-ai');
if (contenitoreAi) montaCommentoAi({
    pagina: 'territorio', contenitore: contenitoreAi,
    filtri: () => {
        const o = {};
        for (const f of FILTRI) {
            const v = $('f-' + f).value;
            if (!v) continue;
            o[f] = ['dal', 'al', 'commerciale'].includes(f) ? Number(v) : v;
        }
        return o;
    }
});
