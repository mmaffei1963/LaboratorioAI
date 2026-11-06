// Cruscotto Direzione (13/10/2026): indicatori economici per anno, MAREA o area.
import { montaCommentoAi } from './commento-ai-direzione.js';
const $ = id => document.getElementById(id);
const esc = s => (s ?? '').toString().replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const euro = v => v == null ? '—' : Number(v).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';
const pct = v => v == null ? '—' : (Number(v) * 100).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + '%';
const dataOra = s => s ? new Date(s).toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' }) : '—';

// Righe come le pagine «Indicatori MAREA / area» di Power BI. [titolo, campo, percentuale, stile]
function righe(area) {
    return [
        ['Fatturato', 'fatturato', false, 'evidenza'],
        [area ? 'Totale spese fisse (con 1/5 Amministrazione)' : 'Totale spese fisse', 'speseFisse', false],
        ...(area ? [['  di cui quota Amministrazione (1/5)', 'quotaAmministrazione', false, 'tenue']] : []),
        ['Totale spese variabili', 'speseVariabili', false],
        ['Costo materie prime', 'materiePrime', false],
        ['Margine di contribuzione', 'margineContribuzione', false, 'evidenza'],
        ['Incidenza costi variabili su fatturato', 'incidenzaVariabili', true],
        ['Personale', 'personale', false],
        ['Incidenza personale su fatturato', 'incidenzaPersonale', true],
        ['Valore aggiunto', 'valoreAggiunto', false],
        ['EBITDA (MOL)', 'ebitda', false, 'evidenza'],
        ['Ammortamenti e accantonamenti', 'ammortamenti', false],
        ['EBIT (reddito operativo)', 'ebit', false],
        ['ROS', 'ros', true],
        ['Imposte (IRES + IRAP)', 'imposte', false],
        ['Utile ante imposte', 'utileAnteImposte', false, 'evidenza'],
        ['Marginalità lorda %', 'marginalitaPercento', true],
        ['Utile netto (di bilancio)', 'utileNetto', false],
        ['Fatturato di pareggio (BEP)', 'fatturatoPareggio', false],
        ['% fatturato di pareggio', 'pareggioPercento', true],
        ['Costi non classificati', 'costiNonClassificati', false, 'tenue'],
    ];
}

function parametri() {
    const p = new URLSearchParams();
    if ($('f-centro').value) p.set('centro', $('f-centro').value);
    if ($('f-dal').value) p.set('dal', $('f-dal').value);
    if ($('f-al').value) p.set('al', $('f-al').value);
    return p;
}

async function carica() {
    const tb = $('tabella-indicatori');
    tb.tBodies[0].innerHTML = '<tr><td class="sottotesto">Calcolo…</td></tr>';
    try {
        const r = await fetch('/api/direzione/indicatori?' + parametri().toString());
        const j = await r.json();
        if (!r.ok) throw new Error(j.messaggio || 'Errore');
        disegna(j);
    } catch (e) {
        tb.tBodies[0].innerHTML = `<tr><td style="color:#b00">${esc(e.message)}</td></tr>`;
    }
}

let centriCaricati = false;
function disegna(j) {
    if (!centriCaricati) {
        const sel = $('f-centro');
        for (const c of j.centri) {
            const o = document.createElement('option');
            o.value = c.codice; o.textContent = `${c.codice} — ${c.descrizione || ''}`;
            sel.appendChild(o);
        }
        if (j.centro) sel.value = j.centro;
        centriCaricati = true;
        if (!$('f-dal').value) $('f-dal').value = j.anni[0];
        if (!$('f-al').value) $('f-al').value = j.anni[j.anni.length - 1];
    }
    const a = j.aggiornamento;
    $('aggiornamento').innerHTML = a
        ? `Dati AZW del file aggiornato il <strong>${esc(dataOra(a.file))}</strong>, importati il ${esc(dataOra(a.importazione))}.`
          + (a.vecchio ? ' <span style="color:#b00;font-weight:600">Attenzione: il file AZW non si aggiorna da più di un giorno lavorativo (Copia.bat sul PC di Mirko).</span>' : '')
          + (a.messaggio ? ` <span style="color:#b26a00">${esc(a.messaggio)}</span>` : '')
        : '<span style="color:#b00">Nessuna importazione AZW riuscita: premi «Aggiorna da AZW» o attendi la notte.</span>';
    const avv = [];
    if (j.vociDaVerificare) avv.push(`${j.vociDaVerificare} voci nuove da verificare`);
    if (j.vociSenzaTipo?.length) avv.push(`voci di costo senza tipo (né fisse né variabili): ${j.vociSenzaTipo.join(', ')}`);
    $('avviso-voci').classList.toggle('nascosto', !avv.length);
    $('avviso-voci').innerHTML = avv.length ? 'Classificazione voci: ' + esc(avv.join('; ')) + '. <a href="/Direzione/Voci">Vai alla classificazione</a>.' : '';

    const tab = $('tabella-indicatori');
    tab.tHead.innerHTML = '<tr><th></th>' + j.risultati.map(x => `<th class="numerico">${x.anno}</th>`).join('') + '</tr>';
    tab.tBodies[0].innerHTML = righe(!!j.centro).map(([titolo, campo, isPct, stile]) => {
        const celle = j.risultati.map(x => {
            const v = x[campo];
            const neg = typeof v === 'number' && v < 0 && !isPct && campo !== 'costiNonClassificati';
            return `<td class="numerico"${neg ? ' style="color:#b3261e"' : ''}>${isPct ? pct(v) : euro(v)}</td>`;
        }).join('');
        const st = stile === 'evidenza' ? ' style="font-weight:600;background:#f6f8fb"' : stile === 'tenue' ? ' class="sottotesto"' : '';
        return `<tr${st}><td style="white-space:nowrap">${esc(titolo)}</td>${celle}</tr>`;
    }).join('');
    $('nota-area').textContent = j.centro
        ? 'Area: alle spese fisse è aggiunto 1/5 delle spese fisse dell\'Amministrazione (centro 03), come in Power BI.'
        : '';
}

$('btn-cerca')?.addEventListener('click', carica);
$('f-centro')?.addEventListener('change', carica);
$('btn-excel')?.addEventListener('click', () => { location.href = '/api/direzione/indicatori/excel?' + parametri().toString(); });
// «Aggiorna da AZW»: stessa importazione della notte, poi si ricalcola.
$('btn-importa')?.addEventListener('click', async (e) => {
    const b = e.currentTarget;
    b.disabled = true; const t = b.textContent; b.textContent = 'Importazione in corso…';
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
    pagina: 'indicatori', contenitore: contenitoreAi,
    filtri: () => ({ centro: $('f-centro').value || null, dal: Number($('f-dal').value) || null, al: Number($('f-al').value) || null })
});
