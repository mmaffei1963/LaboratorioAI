// Manualistica: chi approva i modelli di ogni tipo di manuale (03/11/2026).
// Nessuna libreria esterna.

const $ = (s) => document.querySelector(s);
const $$ = (s) => [...document.querySelectorAll(s)];

let tipi = [];
let utenti = [];

const esc = (v) => String(v == null ? '' : v)
  .replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

async function carica() {
  const r = await fetch('/api/manualistica/approvatori');
  if (!r.ok) { avviso('Non riesco a leggere l\'elenco.'); return; }
  const d = await r.json();
  tipi = d.tipi;
  utenti = d.utenti;
  disegna();
}

function disegna() {
  $('#elenco-tipi').innerHTML = tipi.map((t) => {
    const scelti = new Set(t.approvatori.map((a) => a.idUtente));
    const liberi = utenti.filter((u) => !scelti.has(u.idUtente));

    const righe = t.approvatori.length
      ? t.approvatori.map((a) => `<li>
            <span><strong>${esc(a.nomeCompleto)}</strong> <span class="nota-riga">${esc(a.email)}</span></span>
            <button class="pulsante pulsante-secondario pulsante-piccolo"
                    data-togli="${t.idTipoManuale}:${a.idUtente}">Togli</button>
          </li>`).join('')
      : '<li class="esito-errore">Nessuno indicato: i modelli di questo tipo non si possono approvare.</li>';

    return `<div class="blocco-tipo">
      <h2>${esc(t.nome)} <span class="nota-riga">${esc(t.codice)}</span></h2>
      <ul class="elenco-approvatori">${righe}</ul>
      <div class="azioni-riga">
        <select data-scelta="${t.idTipoManuale}">
          <option value="">aggiungi una persona…</option>
          ${liberi.map((u) => `<option value="${u.idUtente}">${esc(u.nomeCompleto)} — ${esc(u.email)}</option>`).join('')}
        </select>
        <button class="pulsante pulsante-piccolo" data-aggiungi="${t.idTipoManuale}">Aggiungi</button>
      </div>
    </div>`;
  }).join('');

  $$('[data-togli]').forEach((b) => b.addEventListener('click', () => {
    const [tipo, utente] = b.dataset.togli.split(':');
    imposta(Number(tipo), Number(utente), false);
  }));

  $$('[data-aggiungi]').forEach((b) => b.addEventListener('click', () => {
    const tipo = Number(b.dataset.aggiungi);
    const sel = document.querySelector(`[data-scelta="${tipo}"]`);
    if (!sel.value) { avviso('Scegli prima una persona dall\'elenco.'); return; }
    imposta(tipo, Number(sel.value), true);
  }));
}

async function imposta(idTipoManuale, idUtente, abilitato) {
  const r = await fetch('/api/manualistica/approvatori', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ idTipoManuale, idUtente, abilitato })
  });
  if (!r.ok) { avviso(await r.text()); return; }
  await carica();
}

function avviso(messaggio) {
  $('#avviso-corpo').innerHTML = `<p>${esc(messaggio || 'Operazione non riuscita.')}</p>`;
  $('#dlg-avviso').showModal();
}

carica();
