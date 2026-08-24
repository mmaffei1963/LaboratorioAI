// Scadenziario tarature — interni (ArcLab), esterni (clienti), etichette.
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
    renderEsterni();
}
function renderEsterni() {
    const soloDaFatt = $('filtro-fatturare')?.checked;
    const puoGestire = !!$('btn-nuovo-esterno');
    const lista = esterni.filter(t => !soloDaFatt || !t.fatturato);
    const daFatturare = esterni.filter(t => !t.fatturato).length;
    $('conteggio-esterni').textContent = `${lista.length} mostrati · ${daFatturare} da fatturare`;
    $('tabella-esterni').tBodies[0].innerHTML = lista.map(t => {
        const i = esterni.indexOf(t);
        const fatt = t.fatturato ? '<span style="color:#1a7f37;font-weight:600">Sì</span>' : '';
        return `<tr${!t.fatturato ? ' style="background:#fdf4f4"' : ''}>
            <td class="numerico">${t.numeroRapporto ?? '—'}</td><td>${esc(t.tipologia)}</td>
            <td>${dataIt(t.dataTaratura)}</td><td>${dataIt(t.dataScadenza)}</td>
            <td>${esc(t.cliente)}</td><td>${esc(t.riferimento)}</td><td>${dataIt(t.dataConsegna)}</td>
            <td>${fatt}</td>
            <td>${puoGestire ? `<button class="pulsante pulsante-tenue" data-edit="${i}">Modifica</button> <button class="pulsante pulsante-tenue" data-del="${t.idTermometroEsterno}">Elimina</button>` : ''}</td>
        </tr>`;
    }).join('') || '<tr><td colspan="9">Nessun termometro. Importa lo storico da Excel.</td></tr>';
    document.querySelectorAll('#tabella-esterni [data-edit]').forEach(b => b.addEventListener('click', () => apriEsterno(esterni[+b.dataset.edit])));
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
    $('e-taratura').value = t.dataTaratura ? t.dataTaratura.substring(0, 10) : '';
    $('e-scadenza').value = t.dataScadenza ? t.dataScadenza.substring(0, 10) : '';
    $('e-cliente').value = t.cliente ?? '';
    $('e-rif').value = t.riferimento ?? '';
    $('e-consegna').value = t.dataConsegna ? t.dataConsegna.substring(0, 10) : '';
    $('e-operatore').value = t.operatoreConsegna ?? '';
    $('e-fattura').value = t.rifFattura ?? '';
    $('e-note').value = t.note ?? '';
    $('e-fatturare').checked = !!t.fatturato;
    $('pannello-esterno').classList.remove('nascosto');
    $('pannello-esterno').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

async function salvaEsterno() {
    const corpo = {
        idTermometroEsterno: +$('e-id').value || null,
        numeroRapporto: $('e-numero').value ? +$('e-numero').value : null,
        tipologia: $('e-tipologia').value || null,
        dataTaratura: $('e-taratura').value || null,
        dataScadenza: $('e-scadenza').value || null,
        cliente: $('e-cliente').value || null,
        riferimento: $('e-rif').value || null,
        dataConsegna: $('e-consegna').value || null,
        operatoreConsegna: $('e-operatore').value || null,
        rifFattura: $('e-fattura').value || null,
        note: $('e-note').value || null,
        fatturato: $('e-fatturare').checked
    };
    await api('/api/scadenziario/esterni', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });
    $('pannello-esterno').classList.add('nascosto');
    caricaEsterni();
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
