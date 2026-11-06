// Amministrazione → Consulenze: deleghe operative (generazione, firma, registro), dati consulenti, documenti tipo.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const dataIt = s => s ? s.split('-').reverse().join('/') : '';
const oggi = () => { const d = new Date(); d.setMinutes(d.getMinutes() - d.getTimezoneOffset()); return d.toISOString().slice(0, 10); };

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}
const postJson = (url, corpo) => api(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });
function msg(id, t, err = false) { const e = $(id); e.textContent = t || ''; e.style.color = err ? '#b00' : ''; }

// ── Ricerca cliente (riusabile) ──
function comboCliente(input, box, onScegli) {
    let timer = null, risultati = [];
    input.addEventListener('input', () => {
        clearTimeout(timer);
        onScegli(null);
        const q = input.value.trim();
        if (q.length < 2) { box.classList.add('nascosto'); return; }
        timer = setTimeout(async () => {
            try { risultati = await api('/api/consulenze-admin/clienti?filtro=' + encodeURIComponent(q)); } catch { risultati = []; }
            if (!risultati.length) { box.classList.add('nascosto'); return; }
            box.innerHTML = risultati.map((c, i) => `<div class="voce-suggerimento" data-i="${i}">${esc(c.nome)} <span class="sottotesto">(${c.id})</span></div>`).join('');
            box.querySelectorAll('.voce-suggerimento').forEach(el => el.addEventListener('mousedown', ev => {
                ev.preventDefault();
                const c = risultati[+el.dataset.i];
                input.value = c.nome;
                box.classList.add('nascosto');
                onScegli(c);
            }));
            box.classList.remove('nascosto');
        }, 250);
    });
    input.addEventListener('blur', () => setTimeout(() => box.classList.add('nascosto'), 150));
}

// ── Sedi operative (insediamenti) con "tutte le sedi" ──
async function insediamentiDi(idCliente) {
    try { return await api(`/api/consulenze-admin/clienti/${idCliente}/insediamenti`); } catch { return []; }
}
function disegnaSedi(contId, tutteId, insediamenti, selezionati = []) {
    const c = $(contId);
    c.innerHTML = insediamenti.length
        ? insediamenti.map(i => `<label class="casella" style="font-weight:normal"><input type="checkbox" value="${i.idInsediamento}" ${selezionati.includes(i.idInsediamento) || insediamenti.length === 1 ? 'checked' : ''} /> ${esc(i.nome)}${i.indirizzo ? ' — ' + esc(i.indirizzo) : ''}</label>`).join('')
        : 'Nessun insediamento in ArcLab.';
    const agg = () => c.querySelectorAll('input').forEach(x => x.disabled = $(tutteId).checked);
    $(tutteId).onchange = agg;
    agg();
}
function leggiSedi(contId, tutteId) {
    return {
        tuttiInsediamenti: $(tutteId).checked,
        idInsediamenti: [...$(contId).querySelectorAll('input:checked')].map(x => +x.value)
    };
}

// ═══════════════ Consulenti ═══════════════
let consulenti = [];

async function caricaConsulenti() {
    try {
        consulenti = await api('/api/consulenze-admin/consulenti');
        const opz = '<option value="">— scegli —</option>' + consulenti.map(c => `<option value="${c.idOrganico}">${esc(c.nome)}</option>`).join('');
        $('gd-consulente').innerHTML = opz;
        $('dl-consulente').innerHTML = opz;
        const tb = $('tabella-cons').querySelector('tbody');
        tb.innerHTML = '';
        for (const c of consulenti) {
            const tr = document.createElement('tr');
            tr.innerHTML = `<td>${esc(c.nome)}</td>
                <td><input type="text" class="c-qual" maxlength="150" value="${esc(c.qualifica || '')}" placeholder="es. Tecnologo alimentare" style="width:100%" /></td>
                <td><input type="text" class="c-albo" maxlength="200" value="${esc(c.iscrizioneAlbo || '')}" placeholder="es. Ordine dei Tecnologi Alimentari ER n. …" style="width:100%" /></td>
                <td><button type="button" class="pulsante pulsante-tenue">Salva</button></td>`;
            tr.querySelector('button').addEventListener('click', async () => {
                try {
                    await postJson('/api/consulenze-admin/consulenti', {
                        idOrganico: c.idOrganico,
                        qualifica: tr.querySelector('.c-qual').value.trim(),
                        iscrizioneAlbo: tr.querySelector('.c-albo').value.trim() || null
                    });
                    msg('cons-msg', `Salvato: ${c.nome}.`);
                } catch (e) { msg('cons-msg', e.message, true); }
            });
            tb.appendChild(tr);
        }
    } catch (e) { msg('cons-msg', e.message, true); }
}

// ═══════════════ Deleghe: registro ═══════════════
let deleghe = [];

async function caricaDeleghe() {
    const p = new URLSearchParams();
    if ($('dl-cerca').value.trim()) p.set('testo', $('dl-cerca').value.trim());
    if ($('dl-tutte').checked) p.set('ancheNonAttive', 'true');
    try {
        deleghe = await api('/api/consulenze-admin/deleghe?' + p.toString());
        const tb = $('tabella-dl').querySelector('tbody');
        tb.innerHTML = deleghe.length ? '' : '<tr><td colspan="9" class="sottotesto">Nessuna delega.</td></tr>';
        for (const d of deleghe) {
            const tr = document.createElement('tr');
            let stato;
            if (!d.attivo) stato = '<span class="etichetta" style="background:#f3f3f3;color:#666">disattivata</span>';
            else if (d.stato === 'DaFirmare') stato = '<span class="etichetta" style="background:#fff3cd;color:#7a5b00">da firmare</span>';
            else if (d.inEssere) stato = '<span class="etichetta" style="background:#e7f5e7;color:#256029">in essere</span>';
            else stato = '<span class="etichetta" style="background:#f3f3f3;color:#666">non in essere oggi</span>';
            const doc = [
                d.haWord ? `<a href="/api/deleghe/${d.idDelegaCliente}/word">Word</a>` : '',
                d.haPdf ? `<a href="/api/deleghe/${d.idDelegaCliente}/pdf" target="_blank">PDF</a>` : '',
                d.haFirmata ? `<a href="/api/deleghe/${d.idDelegaCliente}/firmata" target="_blank">firmata</a>` : ''
            ].filter(x => x).join(' · ');
            tr.innerHTML = `<td>${esc(d.ragioneSociale)} <span class="sottotesto">(${d.idClienteArcLab})</span></td>
                <td>${esc(d.consulente || '—')}</td>
                <td>${esc(d.sedi)}</td>
                <td>${esc(d.estremi)}${d.idRapportino ? `<div class="sottotesto">dal rapportino n. ${d.idRapportino}</div>` : ''}${d.note ? `<div class="sottotesto">${esc(d.note)}</div>` : ''}</td>
                <td>${dataIt(d.dataSottoscrizione) || '—'}</td><td>${dataIt(d.dataFine) || '—'}</td>
                <td>${stato}</td><td style="white-space:nowrap">${doc || '—'}</td>
                <td style="white-space:nowrap">
                    ${d.generata && d.attivo ? `<button type="button" class="pulsante pulsante-tenue b-firma">${d.stato === 'DaFirmare' ? 'Registra firma' : 'Allega firmata'}</button>` : ''}
                    <button type="button" class="pulsante pulsante-tenue b-mod">Modifica</button></td>`;
            tr.querySelector('.b-firma')?.addEventListener('click', () => apriFirma(d));
            tr.querySelector('.b-mod').addEventListener('click', () => modifica(d));
            tb.appendChild(tr);
        }
        const daFirmare = deleghe.filter(d => d.attivo && d.stato === 'DaFirmare').length;
        $('dl-conta').textContent = `${deleghe.length} deleghe` + (daFirmare ? `, ${daFirmare} da firmare` : '');
    } catch (e) { msg('dl-msg', e.message, true); }
}

// ── Firma ──
function apriFirma(d) {
    $('dett-firma').classList.remove('nascosto');
    $('firma-id').value = d.idDelegaCliente;
    $('firma-titolo').textContent = `Registra firma — ${d.estremi} (${d.ragioneSociale}, ${d.consulente || ''})`;
    $('firma-data').value = d.dataSottoscrizione || oggi();
    $('firma-file').value = '';
    $('dett-firma').scrollIntoView({ behavior: 'smooth', block: 'center' });
}

async function salvaFirma() {
    const fd = new FormData();
    fd.append('dataSottoscrizione', $('firma-data').value);
    const f = $('firma-file').files[0];
    if (f) fd.append('file', f);
    try {
        const r = await fetch(`/api/deleghe/${$('firma-id').value}/firma`, { method: 'POST', body: fd });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.messaggio || 'Errore');
        msg('dl-msg', j.messaggio);
        $('dett-firma').classList.add('nascosto');
        await caricaDeleghe();
    } catch (e) { msg('dl-msg', e.message, true); }
}

// ── Modifica (fine validità, note, attiva; per quelle a mano anche gli altri dati) ──
let clienteManuale = null;
async function modifica(d) {
    const det = $('dl-id').closest('details');
    det.open = true;
    $('dl-id').value = d.idDelegaCliente;
    clienteManuale = { id: d.idClienteArcLab, nome: d.ragioneSociale };
    $('dl-cliente').value = d.ragioneSociale;
    $('dl-consulente').value = d.idOrganicoArcLab ?? '';
    $('dl-estremi').value = d.estremi;
    $('dl-data').value = d.dataSottoscrizione || '';
    $('dl-fine').value = d.dataFine || '';
    $('dl-note').value = d.note || '';
    $('dl-attivo').checked = d.attivo;
    $('dm-tutte').checked = d.tuttiInsediamenti || !(d.idInsediamenti || []).length;
    disegnaSedi('dm-sedi', 'dm-tutte', await insediamentiDi(d.idClienteArcLab), d.idInsediamenti || []);
    $('dm-tutte').disabled = d.generata;
    if (d.generata) $('dm-sedi').querySelectorAll('input').forEach(x => x.disabled = true);
    // Per le deleghe generate si cambiano solo fine, note e "attiva".
    for (const id of ['dl-cliente', 'dl-consulente', 'dl-estremi', 'dl-data']) $(id).disabled = d.generata;
    msg('dlm-msg', d.generata ? 'Delega generata: si possono cambiare solo fine validità, note e stato attivo.' : '');
    $('dl-estremi').scrollIntoView({ behavior: 'smooth', block: 'center' });
}

function svuotaManuale() {
    $('dl-id').value = '';
    clienteManuale = null;
    for (const id of ['dl-cliente', 'dl-estremi', 'dl-data', 'dl-fine', 'dl-note']) { $(id).value = ''; $(id).disabled = false; }
    $('dl-consulente').value = ''; $('dl-consulente').disabled = false;
    $('dl-attivo').checked = true;
    $('dm-tutte').checked = true; $('dm-tutte').disabled = false;
    $('dm-sedi').textContent = '— scegli prima il cliente —';
    msg('dlm-msg', '');
}

async function salvaManuale() {
    if (!clienteManuale) { msg('dlm-msg', 'Scegli il cliente dai suggerimenti.', true); return; }
    try {
        await postJson('/api/consulenze-admin/deleghe', {
            idDelegaCliente: $('dl-id').value ? +$('dl-id').value : null,
            idCliente: clienteManuale.id,
            idOrganico: $('dl-consulente').value ? +$('dl-consulente').value : null,
            estremi: $('dl-estremi').value.trim(),
            dataSottoscrizione: $('dl-data').value || null,
            dataFine: $('dl-fine').value || null,
            note: $('dl-note').value.trim() || null,
            attivo: $('dl-attivo').checked,
            ...leggiSedi('dm-sedi', 'dm-tutte')
        });
        msg('dlm-msg', 'Salvato.');
        svuotaManuale();
        await caricaDeleghe();
    } catch (e) { msg('dlm-msg', e.message, true); }
}

// ═══════════════ Deleghe: generazione ═══════════════
let clienteGen = null;

async function scegliClienteGen(c) {
    clienteGen = c;
    if (!c) return;
    $('gd-sedi').textContent = 'caricamento…';
    disegnaSedi('gd-sedi', 'gd-tutte', await insediamentiDi(c.id));
    await proposta();
}

async function proposta() {
    if (!clienteGen) return;
    const p = new URLSearchParams({ idCliente: clienteGen.id });
    if ($('gd-consulente').value) p.set('idOrganico', $('gd-consulente').value);
    try {
        const r = await api('/api/deleghe/proposta?' + p.toString());
        if (!$('gd-lr-nome').value) $('gd-lr-nome').value = r.legaleRappNome || '';
        if (!$('gd-lr-cognome').value) $('gd-lr-cognome').value = r.legaleRappCognome || '';
        const avvisi = [];
        if (!r.contrattualizzato) avvisi.push('Il cliente non ha commesse di consulenza attive: la delega non si può generare.');
        if ($('gd-consulente').value && !r.qualifica) avvisi.push('Manca la qualifica del consulente (sezione Consulenti qui sotto).');
        if (r.esistente?.stato === 'InEssere') avvisi.push(`Esiste già una delega in essere: ${r.esistente.estremi}.`);
        if (r.esistente?.stato === 'DaFirmare') avvisi.push(`Esiste già una delega da firmare: ${r.esistente.estremi}.`);
        msg('gd-msg', avvisi.join(' '), !r.contrattualizzato);
    } catch (e) { msg('gd-msg', e.message, true); }
}

async function genera() {
    if (!clienteGen) { msg('gd-msg', 'Scegli il cliente dai suggerimenti.', true); return; }
    try {
        const r = await postJson('/api/deleghe/genera', {
            idCliente: clienteGen.id,
            idInsediamento: 0,
            ...leggiSedi('gd-sedi', 'gd-tutte'),
            idOrganico: +($('gd-consulente').value || 0),
            legaleRappNome: $('gd-lr-nome').value.trim(),
            legaleRappCognome: $('gd-lr-cognome').value.trim(),
            idRapportino: null
        });
        $('gd-msg').innerHTML = `Generata: <strong>${esc(r.estremi)}</strong> — <a href="/api/deleghe/${r.idDelegaCliente}/word">Word</a> · <a href="/api/deleghe/${r.idDelegaCliente}/pdf" target="_blank">PDF</a>. Falla firmare e poi registra la firma nel registro qui sopra.`;
        $('gd-msg').style.color = '';
        await caricaDeleghe();
    } catch (e) { msg('gd-msg', e.message, true); }
}

// ═══════════════ Documenti tipo (modifica nella riga) ═══════════════
let documenti = [];

async function caricaDocumenti() {
    try {
        documenti = await api('/api/consulenze-admin/documenti-tipo');
        const tb = $('tabella-dt').querySelector('tbody');
        tb.innerHTML = '';
        for (const d of documenti) tb.appendChild(rigaDocumento(d));
        $('dt-conta').textContent = `${documenti.filter(d => d.attivo).length} attivi su ${documenti.length}`;
    } catch (e) { msg('dt-msg', e.message, true); }
}

function rigaDocumento(d) {
    const tr = document.createElement('tr');
    tr.innerHTML = `
        <td><input type="text" class="dt-nome" maxlength="200" value="${esc(d.nome)}" style="width:100%" /></td>
        <td><input type="text" class="dt-area" maxlength="50" value="${esc(d.area || '')}" style="width:8rem" /></td>
        <td style="text-align:center"><input type="checkbox" class="dt-lab" ${d.richiamaLaboratorio ? 'checked' : ''} /></td>
        <td style="text-align:center"><input type="checkbox" class="dt-piano" ${d.pianoAnalitico ? 'checked' : ''} /></td>
        <td class="numerico"><input type="number" class="dt-ordine" value="${d.ordine ?? 0}" style="width:5rem" /></td>
        <td style="text-align:center"><input type="checkbox" class="dt-attivo" ${d.attivo !== false ? 'checked' : ''} /></td>
        <td><button type="button" class="pulsante pulsante-tenue dt-salva">Salva</button></td>`;
    if (d.attivo === false) tr.style.opacity = '0.6';
    tr.querySelector('.dt-salva').addEventListener('click', async () => {
        const corpo = {
            idDocumentoTipo: d.idDocumentoTipo || null,
            nome: tr.querySelector('.dt-nome').value.trim(),
            area: tr.querySelector('.dt-area').value.trim() || null,
            richiamaLaboratorio: tr.querySelector('.dt-lab').checked,
            pianoAnalitico: tr.querySelector('.dt-piano').checked,
            ordine: +(tr.querySelector('.dt-ordine').value || 0),
            attivo: tr.querySelector('.dt-attivo').checked
        };
        try {
            await postJson('/api/consulenze-admin/documenti-tipo', corpo);
            msg('dt-msg', `Salvato: ${corpo.nome}.`);
            await caricaDocumenti();
        } catch (e) { msg('dt-msg', e.message, true); }
    });
    return tr;
}

// ═══════════════ Eventi ═══════════════
comboCliente($('gd-cliente'), $('gd-cliente-sugg'), scegliClienteGen);
comboCliente($('dl-cliente'), $('dl-cliente-sugg'), async c => {
    clienteManuale = c;
    if (c) disegnaSedi('dm-sedi', 'dm-tutte', await insediamentiDi(c.id));
});
$('gd-consulente').addEventListener('change', proposta);
$('btn-gd-genera').addEventListener('click', genera);
$('btn-dl-carica').addEventListener('click', caricaDeleghe);
$('dl-cerca').addEventListener('keydown', e => { if (e.key === 'Enter') caricaDeleghe(); });
$('dl-tutte').addEventListener('change', caricaDeleghe);
$('btn-firma-salva').addEventListener('click', salvaFirma);
$('btn-firma-chiudi').addEventListener('click', () => $('dett-firma').classList.add('nascosto'));
$('btn-dl-salva').addEventListener('click', salvaManuale);
$('btn-dl-nuova').addEventListener('click', svuotaManuale);
$('btn-dt-nuovo').addEventListener('click', () => {
    const tr = rigaDocumento({ nome: '', area: '', richiamaLaboratorio: false, pianoAnalitico: false, ordine: (documenti.length + 1) * 10, attivo: true });
    $('tabella-dt').querySelector('tbody').prepend(tr);
    tr.querySelector('.dt-nome').focus();
});

caricaConsulenti();
caricaDeleghe();
caricaDocumenti();
