// Proposta controlli — il proponente sceglie i metodi, il prezzo si somma da solo.
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const eur = n => (n == null ? '' : Number(n).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }));

async function api(url, opt) {
    const r = await fetch(url, opt);
    if (!r.ok) { let m = 'Errore'; try { m = (await r.json()).messaggio || m; } catch { } throw new Error(m); }
    return (r.headers.get('content-type') || '').includes('json') ? r.json() : r.text();
}

const chiave = m => `${m.id}|${(m.denominazioneRiga || '').trim().toLowerCase()}`;
function metodiPresenti() {
    return new Set([...$('tabella-metodi').tBodies[0].querySelectorAll('.metodo')].map(tr => tr.dataset.chiave));
}
function ricalcolaTotale() {
    let tot = 0;
    $('tabella-metodi').tBodies[0].querySelectorAll('.metodo').forEach(tr => {
        const c = parseFloat(tr.dataset.costo) || 0, s = parseFloat(tr.dataset.sconto) || 0;
        tot += c * (1 - s / 100);
    });
    $('totale-prezzo').textContent = eur(tot);
}
function aggiungiMetodo(m) {
    if (metodiPresenti().has(chiave(m))) return;
    // Un controllo può contenere metodi di un solo laboratorio esterno.
    if (m.idLabEsterno) {
        const labs = new Set([...$('tabella-metodi').tBodies[0].querySelectorAll('.metodo')]
            .map(tr => tr.dataset.idlab).filter(x => x));
        if (labs.size && !labs.has(String(m.idLabEsterno))) {
            alert('Un controllo può contenere metodi di un solo laboratorio esterno.\nQuesto metodo è affidato a un laboratorio diverso da quello già presente nel controllo.');
            return;
        }
    }
    const tr = $('tpl-metodo').content.firstElementChild.cloneNode(true);
    tr.dataset.chiave = chiave(m);
    tr.dataset.idmetodo = m.id;
    tr.dataset.costo = m.costo ?? 0;
    tr.dataset.sconto = m.sconto ?? 0;
    tr.dataset.idunita = m.idUnita ?? '';
    tr.dataset.idlab = m.idLabEsterno ?? '';
    tr.dataset.cifre = m.cifreSignificative ?? '';
    tr.dataset.maxdec = m.maxDecimali ?? '';
    tr.dataset.acronimo = m.acronimo ?? '';
    tr.querySelector('.m-descr').textContent = m.descrizione || '';
    tr.querySelector('.m-denom').textContent = m.denominazioneRiga || '';
    tr.querySelector('.m-biblio').textContent = m.rifBiblio || '';
    tr.querySelector('.m-unita').textContent = m.unitaMisura || '';
    tr.querySelector('.m-lab').textContent = m.labEsterno || '';
    tr.querySelector('.m-cifre').textContent = m.cifreSignificative ?? '';
    tr.querySelector('.m-maxdec').textContent = m.maxDecimali ?? '';
    tr.querySelector('.m-costo').textContent = eur(m.costo);
    tr.querySelector('.m-sconto').textContent = (m.sconto ?? 0);
    tr.querySelector('.m-rimuovi').addEventListener('click', () => { tr.remove(); ricalcolaTotale(); });
    $('tabella-metodi').tBodies[0].appendChild(tr);
    ricalcolaTotale();
}

// catalogo metodi in una tendina (datalist): filtra mentre scrivi, alla scelta si aggiunge
let mappaMetodi = {};
async function caricaCatalogo() {
    try {
        const cat = await api('/api/controlli/metodi');
        mappaMetodi = {};
        $('lista-metodi').innerHTML = cat.map(m => {
            const label = (m.descrizione || '') + (m.denominazioneRiga ? ' — ' + m.denominazioneRiga : '');
            mappaMetodi[label.trim().toLowerCase()] = m;
            return `<option value="${esc(label)}"></option>`;
        }).join('');
    } catch (e) { console.error('catalogo metodi', e); }
}
function scegliMetodo() {
    const m = mappaMetodi[$('m-input').value.trim().toLowerCase()];
    if (m) { aggiungiMetodo(m); $('m-input').value = ''; }
}

// ── Modifica di un controllo esistente (attivo) ──
let mappaControlli = {};
let nomeControlloDiPartenza = '';   // nome del controllo caricato: non riutilizzabile tale e quale
async function caricaControlliAttivi() {
    try {
        const attivi = await api('/api/controlli/attivi');
        mappaControlli = {};
        $('lista-controlli-attivi').innerHTML = attivi.map(c => {
            const label = `${c.idControllo} — ${c.controllo}`;
            mappaControlli[label.trim().toLowerCase()] = c;
            return `<option value="${esc(label)}"></option>`;
        }).join('');
    } catch (e) { console.error('controlli attivi', e); }
}
async function caricaEsistente() {
    const c = mappaControlli[$('c-esistente').value.trim().toLowerCase()];
    if (!c) return;
    const righe = await api('/api/controlli/righe-esistente?idControllo=' + c.idControllo);
    $('p-id-esistente').value = c.idControllo;
    $('p-nome').value = c.controllo;
    $('tabella-metodi').tBodies[0].innerHTML = '';
    righe.forEach(r => aggiungiMetodo({
        id: r.id, descrizione: r.descrizione, acronimo: r.acronimo, denominazioneRiga: r.denominazioneRiga, rifBiblio: r.rifBiblio,
        unitaMisura: r.unitaMisura, idUnita: r.idUnita, costo: r.costo,
        idLabEsterno: r.idLabEsterno, labEsterno: r.labEsterno,
        cifreSignificative: r.cifreSignificative, maxDecimali: r.maxDecimali
    }));
    nomeControlloDiPartenza = c.controllo || '';
    $('c-esistente-info').textContent = `Stai partendo dal controllo ${c.idControllo} — ${c.controllo}. Aggiungi o rimuovi metodi; se ne crei uno nuovo cambia il nome, poi salva/genera.`;
    $('form-titolo').textContent = 'Modifica controllo ' + c.controllo;
}

function raccogli() {
    return {
        idPropostaControllo: +$('p-id').value || null,
        codiceLocale: $('p-codice').value || null,
        controllo: $('p-nome').value || null,
        note: $('p-note').value || null,
        origine: 'consulente',
        righe: [...$('tabella-metodi').tBodies[0].querySelectorAll('.metodo')].map((tr, i) => ({
            progressivo: i + 1,
            idMetodoArcLab: +tr.dataset.idmetodo,
            metodo: tr.querySelector('.m-descr').textContent || null,
            denominazioneRiga: tr.querySelector('.m-denom').textContent || null,
            rifBiblio: tr.querySelector('.m-biblio').textContent || null,
            costoMetodoEuro: parseFloat(tr.dataset.costo) || null,
            scontoRiga: parseFloat(tr.dataset.sconto) || 0,
            idUnitaArcLab: tr.dataset.idunita ? +tr.dataset.idunita : null,
            unitaMisura: tr.querySelector('.m-unita').textContent || null,
            idLabEsternoArcLab: tr.dataset.idlab ? +tr.dataset.idlab : null,
            labEsterno: tr.querySelector('.m-lab').textContent || null,
            cifreSignificative: tr.dataset.cifre !== '' ? +tr.dataset.cifre : null,
            maxDecimali: tr.dataset.maxdec !== '' ? +tr.dataset.maxdec : null
        }))
    };
}

// parametri (descrizioni + sigla) attualmente nel controllo, per la proposta del nome
const parametriSelezionati = () => [...$('tabella-metodi').tBodies[0].querySelectorAll('.metodo')]
    .map(tr => {
        const d = tr.querySelector('.m-descr').textContent, a = (tr.dataset.acronimo || '').trim();
        return a ? `${d} [${a}]` : d;
    }).filter(Boolean);
const idMetodiSelezionati = () => [...$('tabella-metodi').tBodies[0].querySelectorAll('.metodo')]
    .map(tr => +tr.dataset.idmetodo);

// segnala se esiste già un controllo con lo stesso insieme di metodi
async function controllaDuplicati() {
    const ids = idMetodiSelezionati();
    if (!ids.length) return true;
    let dup;
    try { dup = await api('/api/controlli/duplicati', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ idMetodi: ids, escludiIdControllo: +$('p-id-esistente').value || null }) }); }
    catch { return true; }   // in caso di errore non blocco
    if (!dup.length) return true;
    const elenco = dup.map(d => `• ${d.controllo} (id ${d.idControllo})`).join('\n');
    return confirm(`Esiste già un controllo con gli stessi metodi:\n${elenco}\n\nVuoi salvare comunque la proposta?`);
}

async function proponiNome() {
    const parametri = parametriSelezionati();
    if (!parametri.length) { alert('Aggiungi prima qualche metodo.'); return; }
    const btn = $('btn-nome-ai'); btn.disabled = true; const t = btn.textContent; btn.textContent = '…';
    try {
        const r = await api('/api/controlli/proponi-nome', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ parametri }) });
        if (r.nome && confirm(`Nome proposto:\n\n"${r.nome}"\n\nVuoi usarlo?`)) $('p-nome').value = r.nome;
    } catch (e) { alert(e.message); }
    finally { btn.disabled = false; btn.textContent = t; }
}

async function salva() {
    const corpo = raccogli();
    if (!corpo.controllo) { alert('Indicare il nome del controllo.'); return null; }
    if (!corpo.righe.length) { alert('Aggiungi almeno un metodo.'); return null; }
    const senzaArrot = corpo.righe.filter(r => r.cifreSignificative == null || r.maxDecimali == null);
    if (senzaArrot.length) { alert('Cifre significative / MaxDecimali mancanti su:\n• ' + senzaArrot.map(r => r.denominazioneRiga || r.metodo).join('\n• ')); return null; }
    if (nomeControlloDiPartenza && corpo.controllo.trim().toLowerCase() === nomeControlloDiPartenza.trim().toLowerCase()) {
        alert(`Hai caricato il controllo "${nomeControlloDiPartenza}": per proporne uno nuovo devi cambiargli il nome.`);
        return null;
    }
    if (!await controllaDuplicati()) return null;
    try {
        const r = await api('/api/controlli/salva', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corpo) });
        $('p-id').value = r.idPropostaControllo; $('p-codice').value = r.codiceLocale;
        $('form-stato').textContent = 'Stato: ' + r.stato;
        caricaRegistro();
        return r;
    } catch (e) { alert(e.message); return null; }
}

async function approva() {
    const r = await salva();
    if (!r) return;
    try {
        await api(`/api/controlli/${r.idPropostaControllo}/approva`, { method: 'POST' });
    } catch (e) { alert(e.message); return; }
    $('pannello-form').classList.add('nascosto');
    caricaRegistro();
}

function nuova() {
    $('p-id').value = ''; $('p-codice').value = ''; $('p-id-esistente').value = '';
    $('c-esistente').value = ''; $('c-esistente-info').textContent = ''; nomeControlloDiPartenza = '';
    $('p-nome').value = ''; $('p-note').value = '';
    $('m-input').value = '';
    $('tabella-metodi').tBodies[0].innerHTML = ''; ricalcolaTotale();
    $('form-titolo').textContent = 'Nuova proposta'; $('form-stato').textContent = '';
    $('pannello-form').classList.remove('nascosto');
}

async function apri(id) {
    const p = await api('/api/controlli/' + id);
    $('p-id').value = p.idPropostaControllo; $('p-codice').value = p.codiceLocale;
    nomeControlloDiPartenza = '';
    $('p-id-esistente').value = p.idControlloEsistenteArcLab || '';
    $('c-esistente-info').textContent = p.idControlloEsistenteArcLab ? `Modifica del controllo ArcLab ${p.idControlloEsistenteArcLab}.` : '';
    $('p-nome').value = p.controllo || ''; $('p-note').value = p.note || '';
    $('tabella-metodi').tBodies[0].innerHTML = '';
    (p.righe || []).forEach(r => aggiungiMetodo({
        id: r.idMetodoArcLab, descrizione: r.metodo, denominazioneRiga: r.denominazioneRiga, rifBiblio: r.rifBiblio,
        unitaMisura: r.unitaMisura, idUnita: r.idUnitaArcLab, costo: r.costoMetodoEuro, sconto: r.scontoRiga,
        idLabEsterno: r.idLabEsternoArcLab, labEsterno: r.labEsterno,
        cifreSignificative: r.cifreSignificative, maxDecimali: r.maxDecimali
    }));
    $('form-titolo').textContent = 'Proposta ' + (p.controllo || '');
    $('form-stato').textContent = 'Stato: ' + p.stato;
    $('pannello-form').classList.remove('nascosto');
    $('pannello-form').scrollIntoView({ behavior: 'smooth', block: 'start' });
}

const badge = s => ({ Bozza: '#666', DaApprovare: '#b26a00', Approvato: '#1741b3', Trasmesso: '#1a7f37', Importato: '#1741b3' }[s] || '#666');

async function caricaRegistro() {
    const righe = await api('/api/controlli/elenco');
    $('conteggio').textContent = righe.length + ' proposte';
    $('tabella-registro').tBodies[0].innerHTML = righe.map(p => `<tr>
        <td style="color:${badge(p.stato)};font-weight:600">${esc(p.stato)}</td>
        <td>${esc(p.controllo)}</td><td class="numerico">${p.numeroMetodi}</td><td class="numerico">${eur(p.prezzo)}</td>
        <td>${esc(p.origine)}</td><td>${p.dataCreazione ? new Date(p.dataCreazione).toLocaleDateString('it-IT') : ''}</td>
        <td>${p.importato ? 'IdControllo ' + p.idControlloArcLab : ''}</td>
        <td>
            <button class="pulsante pulsante-tenue" data-apri="${p.idPropostaControllo}">Apri</button>
            ${p.stato === 'Bozza' || p.stato === 'DaApprovare' || p.stato === 'Approvato' ? `<button class="pulsante pulsante-tenue" data-del="${p.idPropostaControllo}">Elimina</button>` : ''}
        </td></tr>`).join('') || '<tr><td colspan="8">Nessuna proposta. Premi «Nuova proposta».</td></tr>';
    document.querySelectorAll('#tabella-registro [data-apri]').forEach(b => b.addEventListener('click', () => apri(+b.dataset.apri)));
    document.querySelectorAll('#tabella-registro [data-del]').forEach(b => b.addEventListener('click', async () => {
        if (!confirm('Eliminare la proposta?')) return;
        await api(`/api/controlli/${b.dataset.del}/elimina`, { method: 'POST' }); caricaRegistro();
    }));
}

$('btn-nuova')?.addEventListener('click', nuova);
$('btn-annulla')?.addEventListener('click', () => $('pannello-form').classList.add('nascosto'));
$('m-input')?.addEventListener('change', scegliMetodo);
$('c-esistente')?.addEventListener('change', caricaEsistente);
$('btn-nome-ai')?.addEventListener('click', proponiNome);
$('btn-salva')?.addEventListener('click', salva);
$('btn-approva')?.addEventListener('click', approva);
caricaCatalogo();
caricaControlliAttivi();
caricaRegistro();
