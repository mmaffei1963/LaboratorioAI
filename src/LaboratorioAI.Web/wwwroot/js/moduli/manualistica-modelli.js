// Manualistica: modelli documentali (01/11/2026; selezione multipla 02/11/2026;
// scelta del manuale 04/11/2026).
// Caricamento di uno o più modelli, controllo dei marcatori, invio in
// approvazione e approvazione, anche su più righe insieme.
// Nessuna libreria esterna.

const $ = (s) => document.querySelector(s);
const $$ = (s) => [...document.querySelectorAll(s)];

let stato = { modelli: [], puoCaricare: false, puoApprovare: false, io: '', tipi: [], tipo: null };

// Il manuale scelto sta nell'indirizzo: cosi' un aggiornamento della pagina
// non lo perde e il collegamento si puo' mandare a un collega.
const tipoNellIndirizzo = () => new URLSearchParams(location.search).get('tipo') || '';
const selezione = new Set();   // id delle revisioni spuntate

const testo = (v) => (v == null ? '' : String(v));
const esc = (v) => testo(v).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const rev = (n) => 'Rev. ' + String(n).padStart(2, '0');
const plurale = (n, uno, molti) => n === 1 ? uno : molti;

const ETICHETTE_STATO = {
  Bozza: 'in bozza',
  InApprovazione: 'in approvazione',
  Approvato: 'approvato',
  Obsoleto: 'obsoleto'
};

function quandoEsce(condizione) {
  if (!condizione) return 'sempre';
  return condizione
    .split(',')
    .map((c) => c.trim())
    .map((c) => (c === 'DA_CHECKLIST' ? 'se scelto in check list' : c.startsWith('!') ? 'se non ' + c.slice(1) : 'se ' + c))
    .join(' e ');
}

// ── Caricamento dell'elenco ──────────────────────────────────────────

async function carica() {
  const tipo = stato.tipo || tipoNellIndirizzo();
  const r = await fetch('/api/manualistica/modelli' + (tipo ? '?tipo=' + encodeURIComponent(tipo) : ''));
  if (!r.ok) { avviso('Non riesco a leggere l\'elenco dei modelli.'); return; }
  const d = await r.json();
  stato = {
    modelli: d.modelli || [], puoCaricare: d.puoCaricare, puoApprovare: d.puoApprovare, io: d.io,
    chiApprova: d.chiApprova || [], puoIndicareApprovatori: d.puoIndicareApprovatori,
    tipi: d.tipi || [], tipo: d.tipo || null, nomeTipo: d.nomeTipo || ''
  };
  ricordaTipo();
  // una revisione che non c'è più non resta selezionata
  const vive = new Set(stato.modelli.map((m) => m.idRevisione).filter(Boolean));
  [...selezione].forEach((id) => { if (!vive.has(id)) selezione.delete(id); });
  riempiTendinaTipi();
  riempiTendinaModelli();
  disegna();
}

// Il tipo che il programma ha effettivamente usato torna nell'indirizzo:
// se avevo chiesto un manuale che non esiste, l'indirizzo si corregge da solo.
function ricordaTipo() {
  if (!stato.tipo) return;
  const u = new URL(location.href);
  if (u.searchParams.get('tipo') === stato.tipo) return;
  u.searchParams.set('tipo', stato.tipo);
  history.replaceState(null, '', u);
}

function riempiTendinaTipi() {
  const sel = $('#f-tipo');
  if (!sel) return;
  sel.innerHTML = stato.tipi
    .map((t) => `<option value="${esc(t.codice)}">${esc(t.nome)}</option>`).join('');
  if (stato.tipo) sel.value = stato.tipo;
  // Con un manuale solo la scelta non c'e': la tendina resta, ma non invita a usarla.
  sel.disabled = stato.tipi.length < 2;
}

function riempiTendinaModelli() {
  const sel = $('#f-modello');
  if (!sel) return;
  const scelto = sel.value;
  sel.innerHTML = '<option value="">riconosci dal nome del file</option>'
    + stato.modelli.map((m) => `<option value="${esc(m.codice)}">${esc(m.nome)}</option>`).join('');
  sel.value = scelto;
}

function filtra() {
  const s = $('#f-stato').value;
  const t = $('#f-testo').value.trim().toLowerCase();
  return stato.modelli.filter((m) => {
    if (s === 'mancante' && m.stato) return false;
    if (s && s !== 'mancante' && m.stato !== s) return false;
    if (t && !(m.codice + ' ' + m.nome).toLowerCase().includes(t)) return false;
    return true;
  });
}

// Il nome del manuale come compare nelle frasi: «la documentazione privacy».
const nomeManuale = () => (stato.nomeTipo || 'questo manuale').toLowerCase();

function disegna() {
  const righe = filtra();
  const corpo = $('#tabella-modelli tbody');

  const approvati = stato.modelli.filter((m) => m.numeroRevisioneApprovata != null).length;
  const mancanti = stato.modelli.filter((m) => !m.stato).length;
  $('#riepilogo').textContent =
    `${stato.modelli.length} documenti · ${approvati} con una revisione approvata · ${mancanti} senza nessun modello caricato.`
    + (approvati < stato.modelli.length ? ' Finché un documento non ha una revisione approvata, il pacchetto non si può generare.' : '');

  const chi = stato.chiApprova || [];
  const avvisoChi = $('#chi-approva');
  if (chi.length) {
    avvisoChi.hidden = false;
    avvisoChi.className = 'nota-riga';
    avvisoChi.textContent = `Approva i modelli di ${nomeManuale()}: ${chi.join(', ')}.`;
  } else {
    avvisoChi.hidden = false;
    avvisoChi.className = 'esito-errore';
    avvisoChi.innerHTML = `Per ${esc(nomeManuale())} non è indicato nessun approvatore: i modelli restano in attesa.`
      + (stato.puoIndicareApprovatori ? ' <a href="/Manualistica/Approvatori">Indica chi approva</a>.' : '');
  }

  if (!righe.length) {
    corpo.innerHTML = '<tr><td colspan="8">Nessun documento con questi filtri.</td></tr>';
    aggiornaBarra();
    return;
  }

  corpo.innerHTML = righe.map((m) => {
    const esami = m.approvazioni || [];
    const miaDecisione = esami.some((a) => (a.approvatore || '').toLowerCase() === stato.io.toLowerCase());
    const suo = (m.caricataDa || '').toLowerCase() === stato.io.toLowerCase();

    const approvazione = !m.stato ? ''
      : m.stato === 'Approvato'
        ? esami.filter((a) => a.approvata).map((a) => esc(a.approvatore)).join('<br>')
        : m.stato === 'InApprovazione'
          ? '<span class="nota-riga">in attesa</span>'
          + (esami.length ? '<br><span class="nota-riga">' + esami.map((a) => esc(a.approvatore) + (a.approvata ? ' ✓' : ' ✗')).join(', ') + '</span>' : '')
          : '—';

    const azioni = [];
    if (m.idRevisione) azioni.push(`<button class="pulsante pulsante-secondario pulsante-piccolo" data-controllo="${m.idRevisione}">Controllo</button>`);
    if (m.idRevisione) azioni.push(`<a class="pulsante pulsante-secondario pulsante-piccolo" href="/api/manualistica/modelli/${m.idRevisione}/scarica">Scarica</a>`);
    if (stato.puoCaricare && m.stato === 'Bozza') azioni.push(`<button class="pulsante pulsante-piccolo" data-invia="${m.idRevisione}">In approvazione</button>`);
    if (stato.puoApprovare && m.stato === 'InApprovazione' && !miaDecisione) {
      azioni.push(`<button class="pulsante pulsante-piccolo" data-approva="${m.idRevisione}">Approva</button>`);
      azioni.push(`<button class="pulsante pulsante-secondario pulsante-piccolo" data-respingi="${m.idRevisione}">Respingi</button>`);
    }

    const spuntabile = m.idRevisione && (m.stato === 'Bozza' || m.stato === 'InApprovazione');

    return `<tr data-riga="${m.idRevisione || ''}">
      <td class="col-spunta">${spuntabile
        ? `<input type="checkbox" data-spunta="${m.idRevisione}" ${selezione.has(m.idRevisione) ? 'checked' : ''} />` : ''}</td>
      <td><strong>${esc(m.nome)}</strong><div class="nota-riga">${esc(m.codice)}</div></td>
      <td>${esc(quandoEsce(m.condizioneInclusione))}</td>
      <td>${m.numeroRevisione != null ? rev(m.numeroRevisione) : '—'}
          ${m.caricataIl ? '<div class="nota-riga">' + esc(m.caricataIl) + '<br>' + esc(m.caricataDa) + '</div>' : ''}</td>
      <td>${m.stato ? esc(ETICHETTE_STATO[m.stato] || m.stato) : '<em>nessun modello</em>'}
          ${m.stato === 'InApprovazione' && suo ? '<div class="nota-riga">caricata da te</div>' : ''}</td>
      <td>${approvazione}</td>
      <td data-esito="${m.idRevisione || ''}">${m.idRevisione ? '<span class="nota-riga">—</span>' : ''}</td>
      <td><div class="azioni-riga">${azioni.join('')}</div></td>
    </tr>`;
  }).join('');

  $$('[data-controllo]').forEach((b) => b.addEventListener('click', () => mostraControllo(Number(b.dataset.controllo))));
  $$('[data-invia]').forEach((b) => b.addEventListener('click', () => eseguiSu([Number(b.dataset.invia)], 'approvazione')));
  $$('[data-approva]').forEach((b) => b.addEventListener('click', () => decidiUno(Number(b.dataset.approva), true)));
  $$('[data-respingi]').forEach((b) => b.addEventListener('click', () => decidiUno(Number(b.dataset.respingi), false)));
  $$('[data-spunta]').forEach((c) => c.addEventListener('change', () => {
    const id = Number(c.dataset.spunta);
    c.checked ? selezione.add(id) : selezione.delete(id);
    aggiornaBarra();
  }));

  aggiornaBarra();
}

// ── Selezione ────────────────────────────────────────────────────────

const selezionati = () => stato.modelli.filter((m) => selezione.has(m.idRevisione));

function aggiornaBarra() {
  const scelti = selezionati();
  const barra = $('#barra-selezione');
  barra.hidden = scelti.length === 0;

  const bozze = scelti.filter((m) => m.stato === 'Bozza');
  const attesa = scelti.filter((m) => m.stato === 'InApprovazione'
    && !(m.approvazioni || []).some((a) => (a.approvatore || '').toLowerCase() === stato.io.toLowerCase()));

  $('#conteggio-selezione').textContent =
    `${scelti.length} ${plurale(scelti.length, 'documento selezionato', 'documenti selezionati')}.`;

  const bInvia = $('#btn-massa-approvazione');
  bInvia.hidden = !(stato.puoCaricare && bozze.length);
  bInvia.textContent = `Manda in approvazione (${bozze.length})`;

  const bApprova = $('#btn-massa-approva');
  bApprova.hidden = !(stato.puoApprovare && attesa.length);
  bApprova.textContent = `Approva (${attesa.length})`;

  const tutte = $$('[data-spunta]');
  const spuntate = tutte.filter((c) => c.checked).length;
  const tutti = $('#spunta-tutti');
  tutti.checked = tutte.length > 0 && spuntate === tutte.length;
  tutti.indeterminate = spuntate > 0 && spuntate < tutte.length;
}

// ── Controllo dei marcatori ──────────────────────────────────────────

function riquadroControllo(c) {
  const conta = Object.entries(c.tagPerPrefisso || {})
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([p, n]) => `${p}: ${n}`).join(' · ');

  const elenco = (titolo, voci) => !voci || !voci.length ? ''
    : `<h3>${titolo}</h3><ul>${voci.map((v) => `<li>${esc(v)}</li>`).join('')}</ul>`;

  return `<p>${c.ok
      ? '<strong>Nessun errore.</strong> Il modello si può mandare in approvazione.'
      : '<strong>Ci sono marcatori che il motore non sa interpretare.</strong> Finché restano, il modello non va in approvazione.'}</p>
    <p class="nota-riga">${c.numeroControlli} controlli contenuto marcati — ${esc(conta)}</p>
    ${elenco('Errori', c.errori)}
    ${elenco('Avvisi', c.avvisi)}`;
}

async function mostraControllo(idRevisione) {
  const r = await fetch(`/api/manualistica/modelli/${idRevisione}/controllo`);
  if (!r.ok) { avviso('Non riesco a controllare questa revisione.'); return; }
  const c = await r.json();
  $('#dlg-titolo').textContent = 'Controllo dei marcatori — ' + (c.nomeFile || '');
  $('#dlg-corpo').innerHTML = riquadroControllo(c);
  $('#dlg-controllo').showModal();
  segnaEsito(idRevisione, c);
}

function segnaEsito(idRevisione, c) {
  const cella = document.querySelector(`[data-esito="${idRevisione}"]`);
  if (!cella) return;
  cella.innerHTML = c.ok
    ? '<span class="esito-ok">a posto</span>'
    : `<span class="esito-errore">${c.errori.length} ${plurale(c.errori.length, 'errore', 'errori')}</span>`;
}

// ── Azioni, su una riga o su molte ───────────────────────────────────

async function decidiUno(idRevisione, approva) {
  const note = approva
    ? prompt('Note dell\'approvazione (facoltative):', '')
    : prompt('Perché la respingi? La revisione torna in bozza.', '');
  if (note === null) return;
  await eseguiSu([idRevisione], approva ? 'approva' : 'respingi', note);
}

/// Esegue l'azione una revisione per volta: ognuna resta registrata per
/// conto suo, e una che fallisce non ferma le altre.
async function eseguiSu(id, azione, note) {
  const falliti = [];
  const barra = $('#conteggio-selezione');
  const testoBarra = barra.textContent;

  for (let i = 0; i < id.length; i++) {
    if (id.length > 1) barra.textContent = `In corso… ${i + 1} di ${id.length}`;

    const r = azione === 'approvazione'
      ? await fetch(`/api/manualistica/modelli/${id[i]}/in-approvazione`, { method: 'POST' })
      : await fetch(`/api/manualistica/modelli/${id[i]}/decidi`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ approva: azione === 'approva', note: note || null })
        });

    if (!r.ok) {
      const m = stato.modelli.find((x) => x.idRevisione === id[i]);
      falliti.push(`${m ? m.nome : id[i]}: ${await r.text()}`);
    }
  }

  barra.textContent = testoBarra;
  selezione.clear();
  await carica();

  if (falliti.length) {
    avviso(`${falliti.length} ${plurale(falliti.length, 'documento non è stato elaborato', 'documenti non sono stati elaborati')}:`
      + `<ul>${falliti.map((f) => `<li>${esc(f)}</li>`).join('')}</ul>`);
  }
}

async function massaInApprovazione() {
  const bozze = selezionati().filter((m) => m.stato === 'Bozza');
  if (!bozze.length) return;
  await eseguiSu(bozze.map((m) => m.idRevisione), 'approvazione');
}

async function massaApprova() {
  const scelti = selezionati().filter((m) => m.stato === 'InApprovazione'
    && !(m.approvazioni || []).some((a) => (a.approvatore || '').toLowerCase() === stato.io.toLowerCase()));
  if (!scelti.length) return;

  const ok = await conferma(
    `Approvare ${scelti.length} ${plurale(scelti.length, 'documento', 'documenti')}?`,
    `<p>La tua approvazione vale per ${plurale(scelti.length, 'questo documento', 'tutti questi documenti')}:</p>
     <ul>${scelti.map((m) => `<li>${esc(m.nome)} — ${rev(m.numeroRevisione)}</li>`).join('')}</ul>
     <p class="nota-riga">Approvati, diventano le revisioni da cui si generano i documenti per i clienti.</p>`,
    'Approva tutti');
  if (!ok) return;

  await eseguiSu(scelti.map((m) => m.idRevisione), 'approva', null);
}

// ── Caricamento dei file ─────────────────────────────────────────────

async function invia() {
  const input = $('#f-file');
  if (!input.files.length) { avviso('Scegli almeno un file.'); return; }

  const codice = $('#f-modello').value;
  if (input.files.length > 1 && codice) {
    avviso('Il modello si indica solo quando carichi un file per volta: con più file lascia «riconosci dal nome».');
    return;
  }

  const dati = new FormData();
  for (const f of input.files) dati.append('file', f);
  if (codice) dati.append('codiceModello', codice);
  if (stato.tipo) dati.append('tipo', stato.tipo);   // si carica nel manuale che si sta guardando

  const btn = $('#btn-invia');
  btn.disabled = true;
  btn.textContent = 'Caricamento…';
  try {
    const r = await fetch('/api/manualistica/modelli/carica', { method: 'POST', body: dati });
    if (!r.ok) { avviso(await r.text()); return; }
    mostraEsiti(await r.json());
    await carica();
  } finally {
    btn.disabled = false;
    btn.textContent = 'Carica';
  }
}

function mostraEsiti(d) {
  const righe = d.esiti.map((e) => {
    const c = e.controllo;
    const dettaglio = !e.salvato
      ? `<span class="esito-errore">${esc(e.motivo)}</span>`
      : c && !c.ok
        ? `<span class="esito-errore">salvato in bozza, ma ${c.errori.length} ${plurale(c.errori.length, 'errore', 'errori')} nei marcatori</span>`
        : '<span class="esito-ok">salvato in bozza</span>';

    const problemi = c && (c.errori.length || c.avvisi.length)
      ? `<ul>${[...c.errori, ...c.avvisi].slice(0, 6).map((x) => `<li>${esc(x)}</li>`).join('')}</ul>` : '';

    return `<tr>
      <td>${esc(e.nomeFile)}</td>
      <td>${esc(e.codiceModello || '—')}</td>
      <td>${e.numeroRevisione != null ? rev(e.numeroRevisione) : '—'}</td>
      <td>${dettaglio}${problemi}</td>
    </tr>`;
  }).join('');

  $('#esiti-carica').innerHTML = `
    <p class="sottotitolo">${d.salvati} di ${d.totale} file salvati come nuova revisione in bozza.</p>
    <div class="tabella-scorrevole">
      <table class="tabella">
        <thead><tr><th>File</th><th>Modello</th><th>Revisione</th><th>Esito</th></tr></thead>
        <tbody>${righe}</tbody>
      </table>
    </div>`;
}

// ── Finestre ─────────────────────────────────────────────────────────

function avviso(messaggio) {
  $('#dlg-titolo').textContent = 'Attenzione';
  $('#dlg-corpo').innerHTML = /<\w/.test(messaggio) ? messaggio : `<p>${esc(messaggio || 'Operazione non riuscita.')}</p>`;
  $('#dlg-controllo').showModal();
}

function conferma(titolo, corpo, etichetta) {
  return new Promise((risolvi) => {
    const dlg = $('#dlg-conferma');
    $('#conferma-titolo').textContent = titolo;
    $('#conferma-corpo').innerHTML = corpo;
    $('#conferma-si').textContent = etichetta || 'Procedi';
    dlg.addEventListener('close', () => risolvi(dlg.returnValue === 'si'), { once: true });
    dlg.showModal();
  });
}

// ── Avvio ────────────────────────────────────────────────────────────

$('#btn-ricarica').addEventListener('click', carica);
$('#f-tipo').addEventListener('change', (e) => {
  stato.tipo = e.target.value;
  selezione.clear();            // la selezione non ha senso fra manuali diversi
  $('#pannello-carica') && ($('#pannello-carica').hidden = true);
  carica();
});
$('#f-stato').addEventListener('change', disegna);
$('#f-testo').addEventListener('input', disegna);
$('#btn-deseleziona').addEventListener('click', () => { selezione.clear(); disegna(); });
$('#btn-massa-approvazione').addEventListener('click', massaInApprovazione);
$('#btn-massa-approva').addEventListener('click', massaApprova);

$('#spunta-tutti').addEventListener('change', (e) => {
  $$('[data-spunta]').forEach((c) => {
    const id = Number(c.dataset.spunta);
    e.target.checked ? selezione.add(id) : selezione.delete(id);
  });
  disegna();
});

if ($('#btn-carica')) {
  $('#btn-carica').addEventListener('click', () => { $('#pannello-carica').hidden = false; $('#f-file').focus(); });
  $('#btn-annulla').addEventListener('click', () => { $('#pannello-carica').hidden = true; });
  $('#btn-invia').addEventListener('click', invia);
}

carica();
