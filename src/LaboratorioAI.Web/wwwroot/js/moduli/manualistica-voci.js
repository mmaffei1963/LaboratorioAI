// Manualistica: voci della check list lette dai modelli approvati (05/11/2026).
// Si guarda l'anteprima, si leggono le segnalazioni, si scrive; poi si
// assegna a ogni domanda il passo in cui comparirà.
// Nessuna libreria esterna.

const $ = (s) => document.querySelector(s);
const $$ = (s) => [...document.querySelectorAll(s)];

let tipi = [];
let tipo = new URLSearchParams(location.search).get('tipo') || '';
let letto = false;
let passi = [];
let voci = [];
const selezione = new Set();

const esc = (v) => String(v == null ? '' : v)
  .replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const plurale = (n, uno, molti) => n === 1 ? uno : molti;
const nomePasso = (c) => (passi.find((p) => p.codice === c) || {}).nome || c;

// ── Estrazione dai modelli ───────────────────────────────────────────

async function tendinaTipi() {
  const r = await fetch('/api/manualistica/modelli' + (tipo ? '?tipo=' + encodeURIComponent(tipo) : ''));
  if (!r.ok) return;
  const d = await r.json();
  tipi = d.tipi || [];
  tipo = d.tipo || '';
  const sel = $('#f-tipo');
  sel.innerHTML = tipi.map((t) => `<option value="${esc(t.codice)}">${esc(t.nome)}</option>`).join('');
  if (tipo) sel.value = tipo;
  sel.disabled = tipi.length < 2;
}

async function anteprima() {
  await chiama('/api/manualistica/voci/anteprima?tipo=' + encodeURIComponent(tipo), 'GET', '#btn-anteprima', 'Leggo…');
}

async function scrivi() {
  await chiama('/api/manualistica/voci/estrai?tipo=' + encodeURIComponent(tipo), 'POST', '#btn-scrivi', 'Scrivo…');
  await caricaVoci();
}

async function chiama(url, metodo, bottone, attesa) {
  const btn = $(bottone);
  const testo = btn.textContent;
  btn.disabled = true;
  btn.textContent = attesa;
  try {
    const r = await fetch(url, { method: metodo });
    if (!r.ok) { avviso(await r.text()); return; }
    mostraEsito(await r.json());
  } finally {
    btn.disabled = false;
    btn.textContent = testo;
  }
}

function mostraEsito(d) {
  letto = true;
  $('#btn-scrivi').disabled = false;

  const legate = d.opzioniLegateAlModello
    ? `, di cui ${d.opzioniLegateAlModello} legate a un solo documento`
    : '';

  $('#riepilogo').textContent = d.scritto
    ? `Scritte le voci di ${d.nome}.`
    : `${d.modelliLetti} ${plurale(d.modelliLetti, 'modello approvato letto', 'modelli approvati letti')}: `
      + `${d.vociTrovate} ${plurale(d.vociTrovate, 'domanda', 'domande')} `
      + `e ${d.opzioniTrovate} ${plurale(d.opzioniTrovate, 'voce', 'voci')}${legate}. Niente è stato ancora scritto.`;

  $('#esito').innerHTML = d.scritto
    ? `<ul class="elenco-approvatori">
         <li><span>Domande nuove</span><strong>${d.vociCreate}</strong></li>
         <li><span>Domande già presenti, riallineate</span><strong>${d.vociAggiornate}</strong></li>
         <li><span>Voci di tendina nuove</span><strong>${d.opzioniCreate}</strong></li>
         <li><span>Voci riattivate</span><strong>${d.opzioniRiattivate}</strong></li>
         <li><span>Voci sparite dai modelli, spente</span><strong>${d.opzioniDisattivate}</strong></li>
       </ul>`
    : '';

  const s = d.segnalazioni || [];
  $('#riquadro-segnalazioni').hidden = s.length === 0;
  $('#tabella-segnalazioni tbody').innerHTML = s.map((x) => `<tr>
      <td><strong>${esc(x.codice)}</strong></td>
      <td>${esc(x.problema)}</td>
      <td class="nota-riga">${esc(x.dettaglio)}</td>
    </tr>`).join('');
}

// ── Le domande e i loro passi ────────────────────────────────────────

async function caricaVoci() {
  const r = await fetch('/api/manualistica/voci?tipo=' + encodeURIComponent(tipo));
  if (!r.ok) { $('#riquadro-voci').hidden = true; return; }
  const d = await r.json();
  passi = d.passi || [];
  voci = d.voci || [];

  const vive = new Set(voci.map((v) => v.idVoceChecklist));
  [...selezione].forEach((id) => { if (!vive.has(id)) selezione.delete(id); });

  $('#riquadro-voci').hidden = voci.length === 0;
  riempiTendinePassi();
  disegnaVoci();
}

function riempiTendinePassi() {
  const opzioni = passi.map((p) => `<option value="${esc(p.codice)}">${esc(p.nome)}</option>`).join('');

  const filtro = $('#f-passo');
  const sceltoFiltro = filtro.value;
  filtro.innerHTML = '<option value="">tutte le domande</option>'
    + '<option value="-">senza passo</option><option value="spente">spente</option>' + opzioni;
  filtro.value = sceltoFiltro;

  const massa = $('#f-passo-massa');
  massa.innerHTML = '<option value="">assegna il passo…</option>' + opzioni
    + '<option value="-">togli il passo</option>';
}

function filtra() {
  const p = $('#f-passo').value;
  const t = $('#f-testo').value.trim().toLowerCase();
  return voci.filter((v) => {
    // Le spente stanno in fondo a tutto: si vedono solo chiedendole.
    if (p !== 'spente' && !v.attivo) return false;
    if (p === 'spente') return !t || (v.codice + ' ' + v.etichetta).toLowerCase().includes(t);
    if (p === '-' && v.gruppo) return false;
    if (p && p !== '-' && v.gruppo !== p) return false;
    if (t && !(v.codice + ' ' + v.etichetta).toLowerCase().includes(t)) return false;
    return true;
  });
}

function disegnaVoci() {
  const righe = filtra();
  const accese = voci.filter((v) => v.attivo);
  const senzaPasso = accese.filter((v) => !v.gruppo).length;
  const spente = voci.length - accese.length;
  const coda = spente ? ` ${spente} spente, che la check list non chiede.` : '';

  $('#riepilogo-voci').textContent = (senzaPasso
    ? `${accese.length} domande · ${senzaPasso} ancora senza passo, e finché è così non compaiono nella check list.`
    : `${accese.length} domande, tutte assegnate a un passo.`) + coda;

  const corpo = $('#tabella-voci tbody');
  if (!righe.length) {
    corpo.innerHTML = '<tr><td colspan="5">Nessuna domanda con questi filtri.</td></tr>';
    aggiornaBarra();
    return;
  }

  corpo.innerHTML = righe.map((v) => {
    const proposta = !v.gruppo && v.passoProposto
      ? `<div class="nota-riga">proposto: ${esc(nomePasso(v.passoProposto))}</div>`
      : '';
    const attesa = v.attivo && v.fonteAttesa
      ? `<div class="nota-riga">da lasciare accesa: arriverà da ${esc(v.fonteAttesa)}</div>`
      : '';

    const automatica = v.attivo && v.spegnimentoProposto
      ? `<div class="nota-riga">da spegnere: ${esc(v.spegnimentoProposto)}</div>${attesa}`
      : attesa;
    const legate = v.opzioniLegateAlModello
      ? `<div class="nota-riga">${v.opzioniLegateAlModello} legate a un documento</div>`
      : '';
    return `<tr>
      <td class="col-spunta"><input type="checkbox" data-spunta="${v.idVoceChecklist}"
            ${selezione.has(v.idVoceChecklist) ? 'checked' : ''} /></td>
      <td><strong>${esc(v.etichetta)}</strong><div class="nota-riga">${esc(v.codice)}</div>${automatica}</td>
      <td>
        <select data-passo="${v.idVoceChecklist}">
          <option value="">— nessuno —</option>
          ${passi.map((p) => `<option value="${esc(p.codice)}" ${v.gruppo === p.codice ? 'selected' : ''}>${esc(p.nome)}</option>`).join('')}
        </select>
        ${proposta}
      </td>
      <td>${v.opzioni || '<span class="nota-riga">risposta scritta</span>'}${legate}</td>
      <td class="azioni-riga">
        ${v.opzioni ? `<button class="pulsante pulsante-secondario pulsante-piccolo" data-vedi="${v.idVoceChecklist}">Vedi le voci</button>` : ''}
        <button class="pulsante pulsante-secondario pulsante-piccolo"
                data-accendi="${v.idVoceChecklist}" data-stato="${v.attivo ? 'on' : 'off'}">${v.attivo ? 'Spegni' : 'Riaccendi'}</button>
      </td>
    </tr>`;
  }).join('');

  $$('[data-spunta]').forEach((c) => c.addEventListener('change', () => {
    const id = Number(c.dataset.spunta);
    c.checked ? selezione.add(id) : selezione.delete(id);
    aggiornaBarra();
  }));

  $$('[data-passo]').forEach((s) => s.addEventListener('change', () =>
    assegna([Number(s.dataset.passo)], s.value || null)));

  $$('[data-vedi]').forEach((b) => b.addEventListener('click', () => vediOpzioni(Number(b.dataset.vedi))));

  $$('[data-accendi]').forEach((b) => b.addEventListener('click', () =>
    accendi([Number(b.dataset.accendi)], b.dataset.stato === 'off')));

  aggiornaBarra();
}

function aggiornaBarra() {
  const n = selezione.size;
  $('#barra-selezione').hidden = n === 0;
  $('#conteggio-selezione').textContent = `${n} ${plurale(n, 'domanda selezionata', 'domande selezionate')}.`;
}

async function assegna(idVoci, passo) {
  const r = await fetch('/api/manualistica/voci/passo', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ idVoci, passo })
  });
  if (!r.ok) { avviso(await r.text()); return; }
  await caricaVoci();
}

async function accendi(idVoci, attivo) {
  const r = await fetch('/api/manualistica/voci/attivo', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ idVoci, attivo })
  });
  if (!r.ok) { avviso(await r.text()); return; }
  await caricaVoci();
}

async function vediOpzioni(idVoce) {
  const v = voci.find((x) => x.idVoceChecklist === idVoce);
  const r = await fetch(`/api/manualistica/voci/${idVoce}/opzioni`);
  if (!r.ok) { avviso('Non riesco a leggere le voci.'); return; }
  const opzioni = await r.json();

  $('#opzioni-titolo').textContent = v ? v.etichetta : 'Voci della tendina';

  const righe = opzioni.map((o) => {
    const dove = o.documento ? `<span class="nota-riga">solo in: ${esc(o.documento)}</span>` : '';
    const mano = o.daCompilazione ? '<span class="nota-riga">scritta a mano</span>' : '';
    const spenta = o.attivo ? '' : '<span class="nota-riga">spenta</span>';
    return `<li><span>${esc(o.valore)}</span><span>${dove} ${mano} ${spenta}</span></li>`;
  }).join('');

  $('#opzioni-corpo').innerHTML = opzioni.length
    ? `<p class="sottotitolo">${opzioni.length} ${plurale(opzioni.length, 'voce', 'voci')} · ${esc(v ? v.codice : '')}</p>
       <ul class="elenco-approvatori">${righe}</ul>`
    : '<p>Nessuna voce.</p>';

  $('#dlg-opzioni').showModal();
}

function avviso(messaggio) {
  $('#avviso-corpo').innerHTML = `<p>${esc(messaggio || 'Operazione non riuscita.')}</p>`;
  $('#dlg-avviso').showModal();
}

// ── Eventi ───────────────────────────────────────────────────────────

$('#f-tipo').addEventListener('change', async (e) => {
  tipo = e.target.value;
  letto = false;
  selezione.clear();
  $('#btn-scrivi').disabled = true;
  $('#riepilogo').textContent = 'Premi «Leggi i modelli» per vedere cosa contengono.';
  $('#esito').innerHTML = '';
  $('#riquadro-segnalazioni').hidden = true;
  const u = new URL(location.href);
  u.searchParams.set('tipo', tipo);
  history.replaceState(null, '', u);
  await caricaVoci();
});

$('#btn-anteprima').addEventListener('click', anteprima);

$('#btn-scrivi').addEventListener('click', () => {
  if (!letto) { avviso('Leggi prima i modelli, così vedi cosa stai per scrivere.'); return; }
  $('#conferma-corpo').innerHTML =
    '<p>Le domande e le voci di tendina vengono allineate ai modelli approvati. '
    + 'Le voci scritte a mano dai consulenti non si toccano, e quelle sparite dai modelli si spengono senza cancellarsi.</p>';
  const dlg = $('#dlg-conferma');
  dlg.returnValue = '';
  dlg.showModal();
  dlg.addEventListener('close', function once() {
    dlg.removeEventListener('close', once);
    if (dlg.returnValue === 'si') scrivi();
  });
});

$('#btn-ricarica').addEventListener('click', caricaVoci);
$('#f-passo').addEventListener('change', disegnaVoci);
$('#f-testo').addEventListener('input', disegnaVoci);
$('#btn-deseleziona').addEventListener('click', () => { selezione.clear(); disegnaVoci(); });

$('#spunta-tutte').addEventListener('change', (e) => {
  $$('[data-spunta]').forEach((c) => {
    const id = Number(c.dataset.spunta);
    e.target.checked ? selezione.add(id) : selezione.delete(id);
  });
  disegnaVoci();
});

$('#btn-massa-spegni').addEventListener('click', () => {
  if (!selezione.size) { avviso('Seleziona almeno una domanda.'); return; }
  accendi([...selezione], false);
});

$('#btn-massa-accendi').addEventListener('click', () => {
  if (!selezione.size) { avviso('Seleziona almeno una domanda.'); return; }
  accendi([...selezione], true);
});

$('#btn-massa-passo').addEventListener('click', () => {
  const scelto = $('#f-passo-massa').value;
  if (!scelto) { avviso('Scegli prima un passo.'); return; }
  if (!selezione.size) { avviso('Seleziona almeno una domanda.'); return; }
  assegna([...selezione], scelto === '-' ? null : scelto);
});

// Spegne in blocco le domande che sembrano arrivare da sole. Il motivo di
// ciascuna si legge sotto il suo nome prima di premere, e ognuna si riaccende
// da sola con il filtro «spente».
$('#btn-proponi-spente').addEventListener('click', async () => {
  const da = filtra().filter((v) => v.attivo && v.spegnimentoProposto);
  if (!da.length) { avviso('Fra le domande mostrate non ce n\'è nessuna che sembri arrivare da sola.'); return; }
  await accendi(da.map((v) => v.idVoceChecklist), false);
});

// La proposta si vede nella tendina prima di salvare: qui si accetta in blocco
// per le domande che un passo non ce l'hanno ancora.
$('#btn-proponi').addEventListener('click', async () => {
  const da = filtra().filter((v) => !v.gruppo && v.passoProposto);
  if (!da.length) { avviso('Non c\'è niente da proporre fra le domande mostrate.'); return; }

  const perPasso = new Map();
  da.forEach((v) => perPasso.set(v.passoProposto, [...(perPasso.get(v.passoProposto) || []), v.idVoceChecklist]));

  for (const [passo, id] of perPasso) await assegna(id, passo);
});

await tendinaTipi();
await caricaVoci();
