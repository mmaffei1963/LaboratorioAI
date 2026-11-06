// Amministrazione → Utenti e ruoli (11/10/2026): persone con i loro ruoli, registrazione in anticipo per e-mail,
// disattivazione, e tabella dei permessi per ruolo. Le regole (chi può dare il ruolo di Amministratore, almeno un
// Amministratore attivo…) le applica il server; qui si guida l'utente.

import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

const $ = (id) => document.getElementById(id);
const AMM = 'amministratore';

let dati = { utenti: [], ruoli: [], sonoAmministratore: false, idUtenteCorrente: null };
let inModifica = null;   // null = nessuno; 0 = nuova persona; altrimenti IdUtente

const dataOra = (v) => v ? new Date(v.endsWith('Z') ? v : v + 'Z').toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) : '';
const nomeRuolo = (codice) => dati.ruoli.find(r => r.codice === codice)?.nome ?? codice;

// ══ Schede ══
function mostraScheda(nome) {
    $('pannello-persone').classList.toggle('nascosto', nome !== 'persone');
    $('pannello-ruoli').classList.toggle('nascosto', nome !== 'ruoli');
    $('sch-persone').classList.toggle('pulsante-tenue', nome !== 'persone');
    $('sch-ruoli').classList.toggle('pulsante-tenue', nome !== 'ruoli');
    if (nome === 'ruoli') caricaRuoli();
}
document.querySelectorAll('[data-scheda]').forEach(b => b.addEventListener('click', () => mostraScheda(b.dataset.scheda)));

// ══ Persone ══
async function caricaPersone() {
    try {
        dati = await chiamaApi('/api/utenti');
        disegnaPersone();
    } catch (e) { avvisa(e.message, 'errore'); }
}

function disegnaPersone() {
    const filtro = $('filtro-persone').value.trim().toLowerCase();
    const disattivati = $('mostra-disattivati').checked;
    const righe = dati.utenti.filter(u => (disattivati || u.attivo) && (!filtro ||
        `${u.nomeCompleto} ${u.email} ${u.note ?? ''} ${u.ruoli.map(nomeRuolo).join(' ')}`.toLowerCase().includes(filtro)));
    $('tabella-persone').querySelector('tbody').innerHTML = righe.length === 0
        ? '<tr><td colspan="5" class="vuoto">Nessuna persona.</td></tr>'
        : righe.map(u => `<tr class="riga-cliccabile${u.attivo ? '' : ' riga-inattiva'}" data-id="${u.idUtente}">
            <td>${testo(u.nomeCompleto)}${u.note ? `<div class="sottotesto">${testo(u.note)}</div>` : ''}</td>
            <td>${testo(u.email)}</td>
            <td>${u.ruoli.map(c => `<span class="chip-ruolo">${testo(nomeRuolo(c))}</span>`).join('')}</td>
            <td>${u.preregistrato ? '<em>non ancora entrato</em>' : testo(dataOra(u.dataUltimoAccesso))}</td>
            <td>${u.attivo ? 'attivo' : '<span style="color:#b3261e">disattivato</span>'}</td></tr>`).join('');
    $('tabella-persone').querySelectorAll('tr[data-id]').forEach(t => t.addEventListener('click', () => apriPersona(Number(t.dataset.id))));
}
$('filtro-persone').addEventListener('input', disegnaPersone);
$('mostra-disattivati').addEventListener('change', disegnaPersone);

function apriPersona(id) {
    const u = id ? dati.utenti.find(x => x.idUtente === id) : null;
    inModifica = id || 0;
    $('editor-titolo').textContent = u ? u.nomeCompleto : 'Nuova persona';
    $('p-email').value = u?.email ?? '';
    $('p-email').disabled = !!u;
    $('p-nome').value = u?.nomeCompleto ?? '';
    $('p-note').value = u?.note ?? '';
    const assegnati = new Set(u ? u.ruoli : ['consultazione']);
    $('p-ruoli').innerHTML = dati.ruoli.filter(r => r.attivo || assegnati.has(r.codice)).map(r => {
        const bloccato = r.codice === AMM && !dati.sonoAmministratore;
        return `<label class="casella" title="${testo(r.descrizione)}">
            <input type="checkbox" value="${testo(r.codice)}" ${assegnati.has(r.codice) ? 'checked' : ''} ${bloccato ? 'disabled' : ''} />
            <span><strong>${testo(r.nome)}</strong><br><span class="sottotesto">${testo(r.descrizione)}</span></span></label>`;
    }).join('');
    $('p-nota-amm').textContent = dati.sonoAmministratore ? '' : 'Il ruolo di Amministratore lo assegna o lo toglie solo un Amministratore.';
    $('btn-attiva-persona').classList.toggle('nascosto', !u);
    $('btn-attiva-persona').textContent = u?.attivo ? 'Disattiva' : 'Riattiva';
    $('btn-attiva-persona').style.color = u?.attivo ? 'var(--rosso)' : '';
    $('editor-persona').classList.remove('nascosto');
    $('editor-persona').scrollIntoView({ behavior: 'smooth', block: 'start' });
}

function chiudiPersona() { inModifica = null; $('editor-persona').classList.add('nascosto'); }
$('btn-chiudi-persona').addEventListener('click', chiudiPersona);
$('btn-nuova-persona').addEventListener('click', () => apriPersona(0));

async function salvaPersona(pulsante, attivo) {
    const ruoli = [...$('p-ruoli').querySelectorAll('input:checked')].map(c => c.value);
    const corpo = { email: $('p-email').value.trim(), nomeCompleto: $('p-nome').value.trim(), note: $('p-note').value.trim(), ruoli };
    if (attivo !== undefined) corpo.attivo = attivo;
    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const r = inModifica ? await inviaJson(`/api/utenti/${inModifica}`, corpo) : await inviaJson('/api/utenti', corpo);
        avvisa(r.messaggio, 'successo');
        await caricaPersone();
        if (r.idUtente) apriPersona(r.idUtente); else if (inModifica) apriPersona(inModifica);
    } catch (e) { avvisa(e.message, 'errore'); }
    finally { libera(); }
}
$('btn-salva-persona').addEventListener('click', (e) => salvaPersona(e.currentTarget));
$('btn-attiva-persona').addEventListener('click', (e) => {
    const u = dati.utenti.find(x => x.idUtente === inModifica);
    if (!u) return;
    if (u.attivo && !confirm(`Disattivare ${u.nomeCompleto}? Non potrà più entrare in LaboratorioAI; resta nello storico e si può riattivare.`)) return;
    salvaPersona(e.currentTarget, !u.attivo);
});

// ══ Ruoli e permessi ══
let matrice = null;       // { aree, ruoli, modificabile }
const modifiche = new Map();   // codice ruolo → Set di permessi

async function caricaRuoli() {
    try {
        matrice = await chiamaApi('/api/utenti/ruoli');
        modifiche.clear();
        disegnaRuoli();
    } catch (e) { avvisa(e.message, 'errore'); }
}

function permessiRuolo(r) { return modifiche.get(r.codice) ?? new Set(r.permessi); }

function disegnaRuoli() {
    const { aree, ruoli, modificabile } = matrice;
    $('nota-ruoli').textContent = modificabile
        ? 'Spunta o togli i permessi, poi «Salva le modifiche». L\'Amministratore ha sempre tutto.'
        : 'Consultazione: solo un Amministratore cambia i permessi dei ruoli.';
    const t = $('tabella-ruoli');
    t.querySelector('thead').innerHTML = `<tr><th>Permesso</th>${ruoli.map(r =>
        `<th class="ruolo" title="${testo(r.descrizione)}">${testo(r.nome)}</th>`).join('')}</tr>`;
    t.querySelector('tbody').innerHTML = aree.map(a =>
        `<tr class="area"><td colspan="${ruoli.length + 1}">${testo(a.area)}</td></tr>` +
        a.permessi.map(p => `<tr><td title="${testo(p.codice)}">${testo(p.descrizione)}</td>${ruoli.map(r => {
            const amm = r.codice === AMM;
            const on = amm || permessiRuolo(r).has(p.codice);
            const cambiata = modifiche.has(r.codice) && (new Set(r.permessi).has(p.codice) !== on);
            return `<td class="cella-permesso${cambiata ? ' modificata' : ''}"><input type="checkbox" data-ruolo="${testo(r.codice)}" data-permesso="${testo(p.codice)}"
                ${on ? 'checked' : ''} ${amm || !modificabile ? 'disabled' : ''} /></td>`;
        }).join('')}</tr>`).join('')).join('');
    t.querySelectorAll('input[data-ruolo]:not([disabled])').forEach(c => c.addEventListener('change', () => {
        const r = ruoli.find(x => x.codice === c.dataset.ruolo);
        const s = new Set(permessiRuolo(r));
        c.checked ? s.add(c.dataset.permesso) : s.delete(c.dataset.permesso);
        const uguale = s.size === r.permessi.length && r.permessi.every(x => s.has(x));
        uguale ? modifiche.delete(r.codice) : modifiche.set(r.codice, s);
        c.closest('td').classList.toggle('modificata', new Set(r.permessi).has(c.dataset.permesso) !== c.checked);
        $('btn-salva-ruoli').disabled = modifiche.size === 0;
    }));
    $('btn-salva-ruoli').disabled = modifiche.size === 0;
}

$('btn-salva-ruoli').addEventListener('click', async (e) => {
    const nomi = [...modifiche.keys()].map(c => matrice.ruoli.find(r => r.codice === c)?.nome ?? c);
    if (!confirm(`Salvare i permessi di: ${nomi.join(', ')}?\nValgono subito per tutte le persone con questi ruoli.`)) return;
    const libera = occupa(e.currentTarget, 'Salvataggio…');
    try {
        for (const [codice, s] of modifiche) {
            const r = await inviaJson(`/api/utenti/ruoli/${encodeURIComponent(codice)}/permessi`, { permessi: [...s] });
            avvisa(r.messaggio, 'successo');
        }
        await Promise.all([caricaRuoli(), caricaPersone()]);
    } catch (e2) { avvisa(e2.message, 'errore'); }
    finally { libera(); }
});

window.addEventListener('beforeunload', (e) => { if (modifiche.size > 0) { e.preventDefault(); e.returnValue = ''; } });

caricaPersone();
