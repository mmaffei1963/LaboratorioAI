// Manualistica: compilazione della check list di un cliente (10/11/2026).
// Sei passi, le domande di ciascuno, e il salvataggio a ogni risposta:
// in sopralluogo non si preme «salva», e quello che si è scritto non
// deve dipendere dal ricordarsene. Nessuna libreria esterna.

const $ = (s) => document.querySelector(s);
const $$ = (s) => [...document.querySelectorAll(s)];

const ID = window.ID_COMPILAZIONE;

let passi = [];
let risposte = {};
let passoCorrente = null;
let puoCompilare = false;

const esc = (v) => String(v == null ? '' : v)
  .replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const plurale = (n, uno, molti) => n === 1 ? uno : molti;

async function apri() {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}`);
  if (!r.ok) { avviso('Non riesco ad aprire la check list.'); return; }
  const d = await r.json();

  passi = d.passi || [];
  risposte = d.risposte || {};
  puoCompilare = d.puoCompilare;

  $('#titolo').textContent = d.ragioneSociale;
  $('#sottotitolo').textContent =
    `Documenti del ${d.data} · ${d.revisione}`
    + (puoCompilare ? ' · le risposte si salvano da sole' : ' · sola lettura');

  $('#passi').innerHTML = passi.map((p) => `
    <button class="pulsante pulsante-secondario pulsante-piccolo" data-passo="${esc(p.codice)}">${esc(p.nome)}</button>`).join('');

  $$('[data-passo]').forEach((b) => b.addEventListener('click', () => mostraPasso(b.dataset.passo)));

  await mostraPasso(passi.length ? passi[0].codice : null);
}

async function mostraPasso(codice) {
  if (!codice) return;
  passoCorrente = codice;

  $$('[data-passo]').forEach((b) => b.classList.toggle('pulsante-secondario', b.dataset.passo !== codice));
  $('#nome-passo').textContent = (passi.find((p) => p.codice === codice) || {}).nome || '';

  const r = await fetch(`/api/manualistica/compilazioni/${ID}/passo/${encodeURIComponent(codice)}`);
  if (!r.ok) { avviso('Non riesco a leggere le domande di questo passo.'); return; }
  const domande = await r.json();

  disegna(domande);
}

function disegna(domande) {
  const fatte = domande.filter((d) => risposte[d.codice]).length;
  $('#stato-passo').textContent = domande.length
    ? `${domande.length} ${plurale(domande.length, 'domanda', 'domande')} · ${fatte} con una risposta.`
    : 'In questo passo non ci sono domande da compilare qui: i dati vengono dalle schede (sedi, autorizzati, responsabili, archivi).';

  $('#domande').innerHTML = domande.map((d) => `
    <div class="riga-domanda">
      <label for="c-${esc(d.codice)}">${esc(d.etichetta)}${d.obbligatoria ? ' *' : ''}
        <div class="nota-riga">${esc(d.codice)}</div>
      </label>
      ${campo(d)}
      <span class="nota-riga" data-esito="${esc(d.codice)}"></span>
    </div>`).join('');

  $$('[data-risposta]').forEach((el) => {
    el.addEventListener('change', () => salva(el.dataset.risposta, valoreDi(el)));
    if (el.tagName === 'INPUT' && el.type === 'text') {
      el.addEventListener('blur', () => salva(el.dataset.risposta, valoreDi(el)));
    }
  });
}

function campo(d) {
  const v = risposte[d.codice] ?? '';
  const id = `c-${esc(d.codice)}`;
  const spento = puoCompilare ? '' : 'disabled';

  if (d.tipo === 'SiNo') {
    return `<select id="${id}" data-risposta="${esc(d.codice)}" ${spento}>
        <option value="">—</option>
        <option value="SI" ${v === 'SI' ? 'selected' : ''}>sì</option>
        <option value="NO" ${v === 'NO' ? 'selected' : ''}>no</option>
      </select>`;
  }

  if (d.tipo === 'Tendina') {
    return `<select id="${id}" data-risposta="${esc(d.codice)}" ${spento}>
        <option value="">—</option>
        ${d.opzioni.map((o) => `<option value="${esc(o.valore)}" ${v === o.valore ? 'selected' : ''}>${esc(o.testo)}</option>`).join('')}
      </select>`;
  }

  if (d.tipo === 'SceltaMultipla') {
    const scelte = v ? v.split(';') : [];
    return `<div class="scelte" data-risposta="${esc(d.codice)}" data-multipla="1">
        ${d.opzioni.map((o) => `<label class="scelta">
            <input type="checkbox" value="${esc(o.valore)}" ${scelte.includes(o.valore) ? 'checked' : ''} ${spento} />
            ${esc(o.testo)}</label>`).join('')}
      </div>`;
  }

  if (d.tipo === 'Numero') {
    return `<input type="number" id="${id}" data-risposta="${esc(d.codice)}" value="${esc(v)}" ${spento} />`;
  }

  if (d.tipo === 'Data') {
    return `<input type="date" id="${id}" data-risposta="${esc(d.codice)}" value="${esc(v)}" ${spento} />`;
  }

  return `<input type="text" id="${id}" data-risposta="${esc(d.codice)}" value="${esc(v)}" maxlength="400" ${spento} />`;
}

function valoreDi(el) {
  if (el.dataset.multipla) {
    return [...el.querySelectorAll('input:checked')].map((c) => c.value).join(';');
  }
  return el.value;
}

async function salva(codice, valore) {
  const esito = $(`[data-esito="${CSS.escape(codice)}"]`);
  if (esito) esito.textContent = 'salvo…';

  const r = await fetch(`/api/manualistica/compilazioni/${ID}/valore`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ codice, valore, scrittoAMano: false })
  });

  if (!r.ok) {
    if (esito) esito.textContent = '';
    avviso(await r.text());
    return;
  }

  risposte[codice] = valore;
  if (esito) {
    esito.textContent = 'salvato';
    setTimeout(() => { if (esito.textContent === 'salvato') esito.textContent = ''; }, 1500);
  }
}

function avviso(messaggio) {
  $('#avviso-corpo').innerHTML = `<p>${esc(messaggio || 'Operazione non riuscita.')}</p>`;
  $('#dlg-avviso').showModal();
}

await apri();
