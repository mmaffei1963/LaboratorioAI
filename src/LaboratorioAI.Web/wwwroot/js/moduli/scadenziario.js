// Scadenziario tarature — interni (ArcLab), esterni (clienti), etichette.
import { mostraRinnovo } from './rinnovo-termometri.js';
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const dataIt = d => d ? new Date(d).toLocaleDateString('it-IT') : '—';
const anno = d => d ? new Date(d).getFullYear() : new Date().getFullYear();

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}

// ══ Interni ══
let interniVoci = [];
async function caricaInterni() {
    const g = +$('giorni').value || 30;
    const gruppi = await api('/api/scadenziario/interni?giorni=' + g);
    interniVoci = []; let html = '', tot = 0;
    gruppi.forEach(gr => {
        html += `<h3 style="margin-top:0.6rem">${esc(gr.gruppo)} <span class="sottotesto">(${gr.voci.length})</span></h3>`;
        html += `<div class="tabella-scorrevole"><table class="tabella"><thead><tr>
            <th>Strumento</th><th>Attività</th><th>Ultima taratura</th><th>Scadenza</th><th class="numerico">Giorni</th></tr></thead><tbody>`;
        gr.voci.forEach(v => {
            interniVoci.push(v);
            const gg = v.giorniAllaScadenza;
            const col = gg === null ? '#b26a00' : gg < 0 ? '#b3261e' : gg <= 15 ? '#b26a00' : '';
            html += `<tr>
                <td>${esc(v.strumento)}</td><td>${esc(v.attivita)}</td>
                <td>${dataIt(v.ultimaTaratura)}</td><td>${dataIt(v.scadenza)}</td>
                <td class="numerico" style="${col ? `color:${col};font-weight:600` : ''}">${gg === null ? 'mai' : gg}</td></tr>`;
        });
        html += `</tbody></table></div>`; tot += gr.voci.length;
    });
    $('gruppi-interni').innerHTML = html || '<p class="sottotesto">Nessuno strumento in scadenza nel periodo.</p>';
    $('conteggio-interni').textContent = tot + ' strumenti';
}

// ══ Esterni ══
let esterni = [];
async function caricaEsterni() {
    esterni = await api('/api/scadenziario/esterni');
    aggiornaRiferimenti();
    renderEsterni();
}
function giorniAScadenza(dataScadenza) {
    if (!dataScadenza) return null;
    const oggi = new Date(); oggi.setHours(0, 0, 0, 0);
    const s = new Date(dataScadenza); s.setHours(0, 0, 0, 0);
    return Math.round((s - oggi) / 86400000);
}

// 03/11/2026: stato delle tarature (in uso, rinnovata, dismessa), filtri per cliente, riferimento e preavviso.
// Le «in uso» scadute da oltre 12 mesi (conta la data di scadenza) sono di norma clienti persi: nascoste salvo spunta.
function scadutaDaOltre12Mesi(t) {
    if (!t.dataScadenza) return false;
    const soglia = new Date(); soglia.setHours(0, 0, 0, 0); soglia.setMonth(soglia.getMonth() - 12);
    return new Date(t.dataScadenza) < soglia;
}
const norm = s => (s ?? '').toString().normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();

function aggiornaRiferimenti() {
    const sel = $('filtro-rif'); if (!sel) return;
    const att = sel.value;
    const voci = [...new Set(esterni.map(t => (t.riferimento ?? '').trim()).filter(x => x))].sort((a, b) => a.localeCompare(b, 'it'));
    sel.innerHTML = '<option value="">tutti</option>' + voci.map(v => `<option value="${esc(v)}">${esc(v)}</option>`).join('');
    if (voci.includes(att)) sel.value = att;
}

const TIPI = { 0: '', 1: 'a sonda', 2: 'a gabbia', 3: 'pirometro', 4: 'datalogger', 5: 'altro' };

function testoStato(t) {
    if (t.stato === 'rinnovata')
        return `<span style="color:#1a7f37">rinnovata${t.dataRinnovo ? ' il ' + dataIt(t.dataRinnovo) : ''}${t.numeroRapportoRinnovo ? ' (n° ' + t.numeroRapportoRinnovo + ')' : ''}</span>`;
    if (t.stato === 'dismessa') return '<span style="color:#888">dismessa</span>';
    const gg = giorniAScadenza(t.dataScadenza);
    if (gg == null) return 'in uso';
    if (gg < 0) return `<strong style="color:#b3261e">in uso · scaduta da ${-gg} gg</strong>`;
    if (gg <= 30) return `<strong style="color:#b26a00">in uso · scade tra ${gg} gg</strong>`;
    return 'in uso';
}

function renderEsterni() {
    const soloDaFatt = $('filtro-fatturare')?.checked;
    const cliente = norm($('filtro-cliente')?.value);
    const rif = $('filtro-rif')?.value ?? '';
    const stato = $('filtro-stato')?.value ?? 'inuso';
    const preavviso = ($('filtro-preavviso')?.value ?? '').trim();
    const vecchie = $('filtro-vecchie')?.checked;
    const tipo = $('filtro-tipo')?.value ?? '';
    const servizio = $('filtro-servizio')?.value ?? '';
    const puoGestire = !!$('btn-nuovo-esterno');
    let nascosteVecchie = 0;
    const lista = esterni.filter(t => {
        if (soloDaFatt && t.fatturato) return false;
        if (cliente && !norm(t.cliente).includes(cliente)) return false;
        if (rif && (t.riferimento ?? '').trim() !== rif) return false;
        if (stato && t.stato !== stato) return false;
        if (tipo !== '' && String(t.tipoTermometro ?? 0) !== tipo) return false;
        if (servizio !== '' && String(t.servizio ?? 1) !== servizio) return false;
        if (preavviso !== '') {
            if (t.stato !== 'inuso') return false;          // le scadenze contano solo per le tarature in uso
            const gg = giorniAScadenza(t.dataScadenza);
            if (gg == null) return false;                   // senza data: esclusa
            if (gg > Number(preavviso)) return false;       // entro N giorni (comprese le scadute)
        }
        if (t.stato === 'inuso' && !vecchie && scadutaDaOltre12Mesi(t)) { nascosteVecchie++; return false; }
        return true;
    });
    const daFatturare = esterni.filter(t => !t.fatturato).length;
    $('conteggio-esterni').textContent = `${lista.length} mostrati · ${daFatturare} da fatturare`
        + (nascosteVecchie ? ` · ${nascosteVecchie} scadute da oltre 12 mesi nascoste` : '');
    $('tabella-esterni').tBodies[0].innerHTML = lista.map(t => {
        const i = esterni.indexOf(t);
        const fatt = t.fatturato ? '<span style="color:#1a7f37;font-weight:600">Sì</span>' : '';
        const azioniStato = !puoGestire ? '' : t.stato === 'inuso'
            ? `<button class="pulsante pulsante-tenue" data-stato="rinnovata" data-id="${t.idTermometroEsterno}" title="Rinnovata: non compare più tra le scadenze">Rinnovata</button>
               <button class="pulsante pulsante-tenue" data-stato="dismessa" data-id="${t.idTermometroEsterno}" title="Il cliente non la rinnova più">Dismessa</button>`
            : `<button class="pulsante pulsante-tenue" data-stato="inuso" data-id="${t.idTermometroEsterno}" title="Torna tra le scadenze">Rimetti in uso</button>`;
        return `<tr${!t.fatturato ? ' style="background:#fdf4f4"' : ''}>
            <td class="numerico">${t.numeroRapporto ?? '—'}</td><td>${TIPI[t.tipoTermometro] || '<span class="sottotesto">—</span>'}</td>
            <td>${esc(t.tipologia)}</td><td>${t.servizio === 2 ? '<strong>fornitura + taratura</strong>' : 'solo taratura'}</td>
            <td>${dataIt(t.dataTaratura)}</td><td>${dataIt(t.dataScadenza)}</td>
            <td>${esc(t.cliente)}</td><td>${esc(t.riferimento)}</td><td>${testoStato(t)}</td><td>${dataIt(t.dataConsegna)}</td>
            <td>${fatt}</td>
            <td>${puoGestire ? `<button class="pulsante pulsante-tenue" data-edit="${i}">Modifica</button> ${azioniStato} <button class="pulsante pulsante-tenue" data-del="${t.idTermometroEsterno}">Elimina</button>` : ''}</td>
        </tr>`;
    }).join('') || '<tr><td colspan="12">Nessun termometro con questi filtri.</td></tr>';
    document.querySelectorAll('#tabella-esterni [data-edit]').forEach(b => b.addEventListener('click', () => apriEsterno(esterni[+b.dataset.edit])));
    document.querySelectorAll('#tabella-esterni [data-stato]').forEach(b => b.addEventListener('click', async () => {
        try {
            await api(`/api/scadenziario/esterni/${b.dataset.id}/stato`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ stato: b.dataset.stato })
            });
            caricaEsterni();
        } catch (e) { alert(e.message); }
    }));
    document.querySelectorAll('#tabella-esterni [data-del]').forEach(b => b.addEventListener('click', async () => {
        if (!confirm('Eliminare il termometro?')) return;
        await api(`/api/scadenziario/esterni/${b.dataset.del}/elimina`, { method: 'POST' }); caricaEsterni();
    }));
}

function apriEsterno(t) {
    t = t || {};
    $('esterno-titolo').textContent = t.idTermometroEsterno ? 'Modifica termometro esterno' : 'Nuovo termometro esterno';
    $('e-id').value = t.idTermometroEsterno || '';
    $('e-numero').value = t.numeroRapporto ?? '';
    $('e-tipologia').value = t.tipologia ?? '';
    $('e-tipo').value = String(t.tipoTermometro ?? 0);
    $('e-servizio').value = String(t.servizio ?? 1);
    $('e-taratura').value = t.dataTaratura ? t.dataTaratura.substring(0, 10) : '';
    $('e-scadenza').value = t.dataScadenza ? t.dataScadenza.substring(0, 10) : '';
    $('e-cliente').value = t.cliente ?? '';
    $('e-rif').value = t.riferimento ?? '';
    $('e-consegna').value = t.dataConsegna ? t.dataConsegna.substring(0, 10) : '';
    $('e-operatore').value = t.operatoreConsegna ?? '';
    $('e-fattura').value = t.rifFattura ?? '';
    $('e-note').value = t.note ?? '';
    $('e-fatturare').checked = !!t.fatturato;
    if ($('e-escludi-scadenze')) $('e-escludi-scadenze').checked = !!t.escludiScadenze;
    $('pannello-rinnovo-esterno')?.classList.add('nascosto');
    $('pannello-esterno').classList.remove('nascosto');
    $('pannello-esterno').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

async function salvaEsterno() {
    const corpo = {
        idTermometroEsterno: +$('e-id').value || null,
        numeroRapporto: $('e-numero').value ? +$('e-numero').value : null,
        tipologia: $('e-tipologia').value || null,
        tipoTermometro: Number($('e-tipo').value) || 0,
        servizio: Number($('e-servizio').value) || 1,
        dataTaratura: $('e-taratura').value || null,
        dataScadenza: $('e-scadenza').value || null,
        cliente: $('e-cliente').value || null,
        riferimento: $('e-rif').value || null,
        dataConsegna: $('e-consegna').value || null,
        operatoreConsegna: $('e-operatore').value || null,
        rifFattura: $('e-fattura').value || null,
        note: $('e-note').value || null,
        fatturato: $('e-fatturare').checked,
        escludiScadenze: $('e-escludi-scadenze')?.checked || false
    };
    const e = await api('/api/scadenziario/esterni', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });
    caricaEsterni();
    // 03/11/2026: termometro nuovo: quali tarature dello stesso cliente e tipologia rinnova?
    if (e?.candidatiRinnovo?.length) {
        $('e-id').value = e.idTermometroEsterno;
        mostraRinnovo($('pannello-rinnovo-esterno'), e.candidatiRinnovo, `/api/scadenziario/esterni/${e.idTermometroEsterno}/rinnova`,
            () => { $('pannello-esterno').classList.add('nascosto'); caricaEsterni(); });
        return;
    }
    $('pannello-esterno').classList.add('nascosto');
}

async function importa() {
    const f = $('file-import').files[0];
    if (!f) { alert('Seleziona il file Excel.'); return; }
    const fd = new FormData(); fd.append('file', f);
    try {
        const r = await api('/api/scadenziario/esterni/importa', { method: 'POST', body: fd });
        alert(`Import: ${r.aggiunte} aggiunti, ${r.aggiornate} aggiornati con dati più recenti (su ${r.lette} letti).`);
        caricaEsterni();
    } catch (e) { alert(e.message); }
}

// ══ Avvio ══
$('btn-interni')?.addEventListener('click', caricaInterni);
$('btn-importa')?.addEventListener('click', importa);
$('btn-svuota-importati')?.addEventListener('click', async () => {
    if (!confirm('Svuota l\'archivio importato (le tarature fatte in app restano)?')) return;
    const r = await api('/api/scadenziario/esterni/svuota-importati', { method: 'POST' });
    alert(`${r.rimossi} righe rimosse.`);
    caricaEsterni();
});
$('btn-nuovo-esterno')?.addEventListener('click', () => apriEsterno(null));
$('btn-esterno-salva')?.addEventListener('click', salvaEsterno);
$('btn-esterno-annulla')?.addEventListener('click', () => $('pannello-esterno').classList.add('nascosto'));
$('filtro-fatturare')?.addEventListener('change', renderEsterni);
['filtro-rif', 'filtro-stato', 'filtro-vecchie', 'filtro-tipo', 'filtro-servizio'].forEach(id => $(id)?.addEventListener('change', renderEsterni));
['filtro-cliente', 'filtro-preavviso'].forEach(id => $(id)?.addEventListener('input', renderEsterni));
async function caricaClienti() {
    const lc = $('lista-clienti'); if (!lc) return;
    try {
        const clienti = await api('/api/scadenziario/clienti');
        lc.innerHTML = clienti.map(c => `<option value="${esc(c.nome)}">`).join('');
    } catch { }
}
caricaInterni();
caricaEsterni();
caricaClienti();
