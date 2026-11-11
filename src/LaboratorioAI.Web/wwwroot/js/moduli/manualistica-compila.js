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
let domandeCorrenti = [];

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

  await caricaArchivio();
  await mostraPasso(passi.length ? passi[0].codice : null);
  if (puoCompilare) await riempiDocumenti();
}

// ── Generazione ──────────────────────────────────────────────────────

async function riempiDocumenti() {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/documenti`);
  if (!r.ok) return;
  const documenti = await r.json();
  if (!documenti.length) return;

  $('#riquadro-genera').hidden = false;
  $('#f-documento').innerHTML = documenti
    .map((d) => `<option value="${esc(d.codice)}">${esc(d.nome)} — ${esc(d.revisione)}</option>`).join('');
}

async function genera() {
  const codice = $('#f-documento').value;
  if (!codice) return;

  const btn = $('#btn-genera');
  btn.disabled = true;
  btn.textContent = 'Genero…';
  try {
    // Prima si guarda cosa verrebbe fuori: se manca qualcosa è meglio
    // leggerlo qui che accorgersene nel documento firmato.
    const r = await fetch(`/api/manualistica/compilazioni/${ID}/genera/${encodeURIComponent(codice)}/controllo`);
    if (!r.ok) { avviso(await r.text()); return; }
    const esito = await r.json();

    if (!esito.ok) {
      $('#esito-genera').innerHTML = `<p class="esito-errore">${esc(esito.errore)}</p>`;
      return;
    }

    $('#esito-genera').innerHTML = esito.avvisi.length
      ? `<p class="sottotitolo">Generato <strong>${esc(esito.nomeFile)}</strong>, ma ${esito.avvisi.length} ${plurale(esito.avvisi.length, 'cosa va riletta', 'cose vanno rilette')} prima della firma:</p>
         <ul class="elenco-approvatori">${esito.avvisi.map((a) => `<li><span>${esc(a)}</span></li>`).join('')}</ul>`
      : `<p class="sottotitolo">Generato <strong>${esc(esito.nomeFile)}</strong>, senza avvisi.</p>`;

    window.location.href = `/api/manualistica/compilazioni/${ID}/genera/${encodeURIComponent(codice)}`;

    // L'archivio si aggiorna dopo: il documento ci finisce quando viene
    // scaricato davvero, non quando si guardano gli avvisi.
    setTimeout(caricaArchivio, 1500);
  } finally {
    btn.disabled = false;
    btn.textContent = 'Genera e scarica';
  }
}

async function generaPdf() {
  const codice = $('#f-documento').value;
  if (!codice) return;

  const btn = $('#btn-genera-pdf');
  btn.disabled = true;
  btn.textContent = 'Converto…';
  try {
    window.location.href = `/api/manualistica/compilazioni/${ID}/genera/${encodeURIComponent(codice)}/pdf`;
    setTimeout(caricaArchivio, 2000);
  } finally {
    btn.disabled = false;
    btn.textContent = 'Genera in PDF';
  }
}

// ── Archivio e invio ─────────────────────────────────────────────────

let archiviati = [];

async function caricaArchivio() {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/documenti/archivio`);
  if (!r.ok) return;
  const d = await r.json();

  archiviati = d.documenti || [];
  $('#riquadro-archivio').hidden = false;
  $('#pannello-invio').hidden = !(d.puoCompilare && archiviati.length);
  $('#riga-riapri').hidden = !d.riapribile;
  disegnaInvii(d.invii || []);

  // Gli indirizzi che ArcLab conosce: il primo si propone, gli altri
  // restano a portata nella tendina. Si può comunque scrivere il proprio.
  const indirizzi = d.indirizzi || [];
  $('#lista-indirizzi').innerHTML = indirizzi.map((i) => `<option value="${esc(i)}"></option>`).join('');

  const campoA = $('#f-destinatari');
  if (indirizzi.length && !campoA.value) campoA.value = indirizzi[0];

  $('#nota-indirizzi').textContent = indirizzi.length
    ? (indirizzi.length === 1 ? 'proposto dall\'anagrafica' : `${indirizzi.length} indirizzi noti: scegli dalla tendina`)
    : 'ArcLab non ha e-mail per questo cliente: scrivila a mano';

  $('#stato-archivio').textContent = !d.configurato
    ? 'La cartella di archivio non è configurata: i documenti si scaricano, ma non restano sul server.'
    : archiviati.length
      ? `${archiviati.length} ${plurale(archiviati.length, 'documento prodotto', 'documenti prodotti')} per questo cliente.`
      : 'Nessun documento ancora prodotto.';

  disegnaArchivio();
}

function disegnaArchivio() {
  const corpo = $('#tabella-archivio tbody');

  if (!archiviati.length) {
    corpo.innerHTML = '<tr><td colspan="6">Niente da mostrare.</td></tr>';
    return;
  }

  corpo.innerHTML = archiviati.map((d) => `<tr>
      <td>${d.fileCiSta ? `<input type="checkbox" data-doc="${d.id}" />` : ''}</td>
      <td>${esc(d.nomeFile)}</td>
      <td>${esc(d.formato)}</td>
      <td>${esc(dataOra(d.generatoIlUtc))}</td>
      <td class="nota-riga">${esc(d.generatoDa || '')}</td>
      <td>${d.fileCiSta
        ? `<a class="pulsante pulsante-secondario pulsante-piccolo" href="/api/manualistica/compilazioni/${ID}/documenti/archivio/${d.id}">Scarica</a>`
        : '<span class="nota-riga">file non trovato</span>'}</td>
    </tr>`).join('');
}

const ETICHETTE_INVIO = {
  Programmato: 'programmato',
  InAttesaFirma: 'in attesa di firma',
  Inviato: 'inviato',
  Annullato: 'annullato',
  Errore: 'non riuscito'
};

function disegnaInvii(invii) {
  if (!invii.length) { $('#storico-invii').innerHTML = ''; return; }

  $('#storico-invii').innerHTML = `
    <h3 style="margin-top:1.5rem">Invii</h3>
    <div class="tabella-scorrevole">
      <table class="tabella">
        <thead><tr><th>A</th><th>Formati</th><th>Quando</th><th>Esito</th><th>Da</th></tr></thead>
        <tbody>
          ${invii.map((i) => `<tr>
              <td>${esc(i.destinatari)}</td>
              <td>${esc(i.formati || '')}</td>
              <td>${esc(dataOra(i.inviatoIlUtc))}</td>
              <td>${esc(ETICHETTE_INVIO[i.stato] || i.stato)}${i.esito ? `<div class="nota-riga">${esc(i.esito)}</div>` : ''}</td>
              <td class="nota-riga">${esc(i.creatoDa || '')}</td>
            </tr>`).join('')}
        </tbody>
      </table>
    </div>`;
}

async function riapri() {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/riapri`, { method: 'POST' });
  if (!r.ok) { avviso(await r.text()); return; }
  window.location.reload();
}

// Le date sono scritte in UTC nel database: a schermo vanno nell'ora di qui.
function dataOra(utc) {
  if (!utc) return '';
  const d = new Date(utc.endsWith('Z') ? utc : utc + 'Z');
  return d.toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' });
}

async function invia() {
  const scelti = [...$('#tabella-archivio').querySelectorAll('[data-doc]:checked')].map((c) => Number(c.dataset.doc));
  if (!scelti.length) { avviso('Scegli quali documenti mandare.'); return; }

  const destinatari = $('#f-destinatari').value.trim();
  if (!destinatari) { avviso('Manca l\'indirizzo a cui mandarli.'); return; }

  const btn = $('#btn-invia');
  btn.disabled = true;
  btn.textContent = 'Invio…';
  try {
    const r = await fetch(`/api/manualistica/compilazioni/${ID}/documenti/invia`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ destinatari, documenti: scelti, messaggio: $('#f-messaggio').value.trim() })
    });

    if (!r.ok) { $('#esito-invio').innerHTML = `<p class="esito-errore">${esc(await r.text())}</p>`; return; }

    const esito = await r.json();
    $('#esito-invio').innerHTML =
      `<p class="sottotitolo">Inviati ${esito.quanti} ${plurale(esito.quanti, 'documento', 'documenti')} a ${esc(esito.destinatari.join(', '))}.</p>`;

    // La check list adesso è consegnata: si ricarica, così i campi
    // diventano di sola lettura e compare «Riapri».
    setTimeout(() => window.location.reload(), 1200);
  } finally {
    btn.disabled = false;
    btn.textContent = 'Invia';
  }
}

async function mostraPasso(codice) {
  if (!codice) return;
  passoCorrente = codice;

  $$('[data-passo]').forEach((b) => b.classList.toggle('pulsante-secondario', b.dataset.passo !== codice));

  // Le schede legate a un passo. Si mostra quella del passo corrente e
  // si nascondono le altre.
  //
  // Se una sezione non c'è nella pagina si tira dritto: capita quando il
  // file JavaScript è già quello nuovo e la schermata no, perché la
  // prima sta in wwwroot e la seconda entra solo riavviando. Senza
  // questo controllo la pagina resterebbe bianca invece di mostrare le
  // domande.
  const schede = [
    ['CLIENTE', '#sezione-sedi', caricaSedi],
    ['AUTORIZZATI', '#sezione-autorizzati', caricaPersone],
    ['SISTEMI', '#sezione-archivi', caricaArchivi],
    ['FIRME', '#sezione-firme', caricaFirme]
  ];

  for (const [passo, dove, carica] of schede) {
    const sezione = $(dove);
    if (!sezione) continue;

    sezione.hidden = passo !== codice;
    if (passo === codice) await carica();
  }
  $('#nome-passo').textContent = (passi.find((p) => p.codice === codice) || {}).nome || '';

  const r = await fetch(`/api/manualistica/compilazioni/${ID}/passo/${encodeURIComponent(codice)}`);
  if (!r.ok) { avviso('Non riesco a leggere le domande di questo passo.'); return; }
  const domande = await r.json();

  disegna(domande);
}

function disegna(domande) {
  domandeCorrenti = domande;
  conta();

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

// Richiamato a ogni risposta salvata: il numero in cima deve dire
// quante ne restano davvero, non quante ce n'erano all'apertura.
function conta() {
  const totale = domandeCorrenti.length;
  const fatte = domandeCorrenti.filter((d) => risposte[d.codice]).length;

  $('#stato-passo').textContent = totale
    ? `${totale} ${plurale(totale, 'domanda', 'domande')} · ${fatte} con una risposta.`
    : 'In questo passo non ci sono domande da compilare qui: i dati vengono dalle schede (sedi, autorizzati, responsabili, archivi).';
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
  conta();
  if (esito) {
    esito.textContent = 'salvato';
    setTimeout(() => { if (esito.textContent === 'salvato') esito.textContent = ''; }, 1500);
  }
}

// ── Sedi del trattamento ─────────────────────────────────────────────

let sedi = [];
let proposte = [];

async function caricaSedi() {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/sedi`);
  if (!r.ok) { avviso('Non riesco a leggere le sedi.'); return; }
  const d = await r.json();
  sedi = d.sedi || [];
  proposte = d.proposte || [];
  disegnaSedi();
}

function disegnaSedi() {
  const corpo = $('#tabella-sedi tbody');
  const spento = puoCompilare ? '' : 'disabled';

  if (!sedi.length) {
    corpo.innerHTML = '<tr><td colspan="6">Nessuna sede: prendile da ArcLab oppure aggiungine una a mano.</td></tr>';
  } else {
    corpo.innerHTML = sedi.map((s) => `<tr data-sede="${s.id}">
        <td><input type="text" data-campo="nome" value="${esc(s.nome)}" maxlength="255" ${spento} /></td>
        <td><input type="text" data-campo="indirizzo" value="${esc(s.indirizzo)}" maxlength="300" ${spento} /></td>
        <td><input type="text" data-campo="comune" list="lista-comuni" value="${esc(s.comune)}" maxlength="100" ${spento} /></td>
        <td><input type="text" data-campo="cap" value="${esc(s.cap)}" maxlength="10" size="6" ${spento} /></td>
        <td><input type="text" data-campo="provincia" value="${esc(s.provincia)}" maxlength="2" size="3" ${spento} /></td>
        <td class="nota-riga">
          <span data-esito-sede="${s.id}"></span>
          ${puoCompilare ? `<button class="pulsante pulsante-secondario pulsante-piccolo" data-togli="${s.id}">Togli</button>` : ''}
          ${s.daArcLab ? '<div class="nota-riga">da ArcLab</div>' : ''}
        </td>
      </tr>`).join('');
  }

  if (!puoCompilare) return;

  $$('#tabella-sedi [data-campo]').forEach((el) => {
    el.addEventListener('change', () => salvaSede(el.closest('tr')));
    el.addEventListener('blur', () => salvaSede(el.closest('tr')));
    if (el.dataset.campo === 'comune') el.addEventListener('input', () => cercaComuni(el));
  });

  $$('#tabella-sedi [data-togli]').forEach((b) => b.addEventListener('click', () => togliSede(b.dataset.togli)));
}

function datiRiga(riga) {
  const v = (campo) => (riga.querySelector(`[data-campo="${campo}"]`) || {}).value || '';
  return {
    id: Number(riga.dataset.sede),
    nome: v('nome').trim(),
    indirizzo: v('indirizzo').trim(),
    comune: v('comune').trim(),
    cap: v('cap').trim(),
    provincia: v('provincia').trim().toUpperCase()
  };
}

// Confronta con quello che c'è già: senza questo ogni «blur» manderebbe
// una richiesta anche quando non si è cambiato niente.
function cambiata(d) {
  const vecchia = sedi.find((s) => s.id === d.id);
  if (!vecchia) return true;
  return ['nome', 'indirizzo', 'comune', 'cap', 'provincia']
    .some((c) => (vecchia[c] || '') !== d[c]);
}

async function salvaSede(riga) {
  if (!riga || riga.dataset.salvando) return;
  const d = datiRiga(riga);
  if (!cambiata(d)) return;
  if (!d.indirizzo && !d.comune) return;

  // «change» e «blur» arrivano uno dopo l'altro sullo stesso campo:
  // senza questo la riga si salverebbe due volte, e una riga nuova
  // finirebbe nell'elenco in doppia copia.
  riga.dataset.salvando = '1';

  const esito = $(`[data-esito-sede="${d.id}"]`);
  if (esito) esito.textContent = 'salvo…';

  const r = await fetch(`/api/manualistica/compilazioni/${ID}/sedi`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(d)
  });

  if (!r.ok) {
    riga.dataset.salvando = '';
    if (esito) esito.textContent = '';
    avviso(await r.text());
    return;
  }

  const risposta = await r.json();

  // Riga nuova: si ridisegna l'elenco, cosi' prende il suo «Togli» e il
  // suo identificativo invece di restare una riga a se'.
  if (!d.id) { await caricaSedi(); return; }

  const posto = sedi.findIndex((s) => s.id === d.id);
  if (posto >= 0) sedi[posto] = { ...sedi[posto], ...d, id: risposta.id };
  riga.dataset.salvando = '';

  if (esito) {
    esito.textContent = 'salvata';
    setTimeout(() => { if (esito.textContent === 'salvata') esito.textContent = ''; }, 1500);
  }
}

async function togliSede(id) {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/sedi/${id}/togli`, { method: 'POST' });
  if (!r.ok) { avviso(await r.text()); return; }
  await caricaSedi();
}

function sedeNuova() {
  const corpo = $('#tabella-sedi tbody');
  if (!sedi.length) corpo.innerHTML = '';

  const riga = document.createElement('tr');
  riga.dataset.sede = '0';
  riga.innerHTML = `
      <td><input type="text" data-campo="nome" maxlength="255" /></td>
      <td><input type="text" data-campo="indirizzo" maxlength="300" /></td>
      <td><input type="text" data-campo="comune" list="lista-comuni" maxlength="100" /></td>
      <td><input type="text" data-campo="cap" maxlength="10" size="6" /></td>
      <td><input type="text" data-campo="provincia" maxlength="2" size="3" /></td>
      <td class="nota-riga"><span data-esito-sede="0"></span></td>`;
  corpo.appendChild(riga);

  riga.querySelectorAll('[data-campo]').forEach((el) => {
    el.addEventListener('change', () => salvaSede(riga));
    el.addEventListener('blur', () => salvaSede(riga));
    if (el.dataset.campo === 'comune') el.addEventListener('input', () => cercaComuni(el));
  });

  riga.querySelector('[data-campo="indirizzo"]').focus();
}

// ── I comuni ─────────────────────────────────────────────────────────
// Si scrive il comune e CAP e provincia si compilano da sé, dall'elenco
// ISTAT: nello stesso pacchetto un luogo deve essere scritto sempre
// allo stesso modo.

let comuniTrovati = [];
let attesaComuni = null;

function cercaComuni(el) {
  clearTimeout(attesaComuni);
  const q = el.value.trim();
  if (q.length < 2) return;

  attesaComuni = setTimeout(async () => {
    const r = await fetch(`/api/comuni?q=${encodeURIComponent(q)}`);
    if (!r.ok) return;
    comuniTrovati = await r.json();

    $('#lista-comuni').innerHTML = comuniTrovati
      .map((c) => `<option value="${esc(c.nome)}">${esc(c.nome)} (${esc(c.siglaProvincia)})</option>`).join('');

    const esatto = comuniTrovati.filter((c) => c.nome.toLowerCase() === q.toLowerCase());
    if (esatto.length !== 1) return;

    // Un comune solo con quel nome: provincia sempre, CAP solo se unico
    // (Rimini ne ha 4, Milano 42: lì lo sceglie chi compila).
    const riga = el.closest('tr');
    const prov = riga.querySelector('[data-campo="provincia"]');
    const cap = riga.querySelector('[data-campo="cap"]');
    if (prov && !prov.value.trim()) prov.value = esatto[0].siglaProvincia;
    if (cap && !cap.value.trim() && esatto[0].cap.length === 1) cap.value = esatto[0].cap[0];
  }, 250);
}

// ── Gli insediamenti proposti da ArcLab ──────────────────────────────

function mostraProposte() {
  $('#pannello-proposte').hidden = false;

  const libere = proposte.filter((p) => !p.giaPresente);
  $('#stato-proposte').textContent = !proposte.length
    ? 'ArcLab non ha insediamenti per questo cliente, oppure la check list è stata aperta per un cliente nuovo.'
    : libere.length
      ? `${libere.length} da aggiungere, ${proposte.length - libere.length} già fra le sedi.`
      : 'Ci sono già tutti fra le sedi.';

  $('#elenco-proposte').innerHTML = proposte.map((p) => `
      <label class="scelta">
        <input type="checkbox" value="${esc(p.codice)}" ${p.giaPresente ? 'disabled' : ''} />
        <strong>${esc(p.nome)}</strong>
        <span class="nota-riga">${esc([p.indirizzo, [p.cap, p.comune].filter(Boolean).join(' '), p.provincia ? '(' + p.provincia + ')' : ''].filter(Boolean).join(' '))}</span>
      </label>`).join('');
}

async function importaScelte() {
  const codici = [...$('#elenco-proposte').querySelectorAll('input:checked')].map((c) => c.value);
  if (!codici.length) { avviso('Non hai scelto nessun insediamento.'); return; }

  const r = await fetch(`/api/manualistica/compilazioni/${ID}/sedi/importa`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ codici })
  });
  if (!r.ok) { avviso(await r.text()); return; }

  $('#pannello-proposte').hidden = true;
  await caricaSedi();
}

// ── Autorizzati al trattamento ───────────────────────────────────────

let persone = [];
let qualifiche = [];
let funzioni = [];

async function caricaPersone() {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/autorizzati`);
  if (!r.ok) { avviso('Non riesco a leggere gli autorizzati.'); return; }
  const d = await r.json();
  persone = d.autorizzati || [];
  qualifiche = d.qualifiche || [];
  funzioni = d.funzioni || [];
  disegnaPersone();
}

function disegnaPersone() {
  const corpo = $('#tabella-autorizzati tbody');

  if (!persone.length) {
    corpo.innerHTML = '<tr><td colspan="6">Nessun autorizzato: comincia dal Legale Rappresentante.</td></tr>';
  } else {
    corpo.innerHTML = persone.map((p) => rigaPersona(p)).join('');
  }

  if (!puoCompilare) return;
  corpo.querySelectorAll('tr[data-persona]').forEach(collegaPersona);
}

function rigaPersona(p) {
  const nominativo = p.richiedeNominativo;
  return `<tr data-persona="${p.id}" data-nominativo="${nominativo ? 1 : 0}">
      <td>${tendinaQualifiche(p.idQualifica)}</td>
      <td><input type="text" data-p="nome" value="${esc(p.nome)}" maxlength="100" ${nominativo ? '' : 'disabled'} /></td>
      <td><input type="text" data-p="cognome" value="${esc(p.cognome)}" maxlength="100" ${nominativo ? '' : 'disabled'} /></td>
      <td><input type="number" data-p="quantita" value="${p.quantita}" min="1" max="99" size="3" ${nominativo ? 'disabled' : ''} /></td>
      <td>${caselleFunzioni(p.funzioni || [])}</td>
      <td class="nota-riga">
        <span data-esito-persona="${p.id}"></span>
        ${puoCompilare ? `<button class="pulsante pulsante-secondario pulsante-piccolo" data-togli-persona="${p.id}">Togli</button>` : ''}
      </td>
    </tr>`;
}

function tendinaQualifiche(scelta) {
  return `<select data-p="qualifica">
      ${qualifiche.map((q) => `<option value="${esc(q.codice)}" data-nominativo="${q.nominativo ? 1 : 0}" ${String(scelta) === q.codice ? 'selected' : ''}>${esc(q.nome)}</option>`).join('')}
    </select>`;
}

function caselleFunzioni(scelte) {
  return `<div class="scelte" data-p="funzioni">
      ${funzioni.map((f) => `<label class="scelta">
          <input type="checkbox" value="${esc(f.codice)}" ${scelte.includes(f.codice) ? 'checked' : ''} />
          ${esc(f.nome)}</label>`).join('')}
    </div>`;
}

function collegaPersona(riga) {
  riga.querySelectorAll('[data-p]').forEach((el) => {
    el.addEventListener('change', () => {
      if (el.dataset.p === 'qualifica') aggiornaNominativo(riga);
      salvaPersona(riga);
    });
    if (el.tagName === 'INPUT' && el.type === 'text') {
      el.addEventListener('blur', () => salvaPersona(riga));
    }
  });

  const togli = riga.querySelector('[data-togli-persona]');
  if (togli) togli.addEventListener('click', () => togliPersona(togli.dataset.togliPersona));
}

// Cambiando qualifica cambia cosa si chiede: il nome per chi è una
// persona, il numero per chi è un gruppo.
function aggiornaNominativo(riga) {
  const sel = riga.querySelector('[data-p="qualifica"]');
  const nominativo = sel.selectedOptions[0].dataset.nominativo === '1';
  riga.dataset.nominativo = nominativo ? 1 : 0;

  riga.querySelector('[data-p="nome"]').disabled = !nominativo;
  riga.querySelector('[data-p="cognome"]').disabled = !nominativo;
  riga.querySelector('[data-p="quantita"]').disabled = nominativo;
  if (nominativo) riga.querySelector('[data-p="quantita"]').value = 1;
}

function datiPersona(riga) {
  const el = (c) => riga.querySelector(`[data-p="${c}"]`);
  return {
    id: Number(riga.dataset.persona),
    idQualifica: Number(el('qualifica').value),
    nome: el('nome').value.trim(),
    cognome: el('cognome').value.trim(),
    quantita: Number(el('quantita').value || 1),
    funzioni: [...el('funzioni').querySelectorAll('input:checked')].map((c) => c.value)
  };
}

async function salvaPersona(riga) {
  if (!riga || riga.dataset.salvando) return;
  const d = datiPersona(riga);
  if (!d.idQualifica) return;

  riga.dataset.salvando = '1';
  const esito = $(`[data-esito-persona="${d.id}"]`);
  if (esito) esito.textContent = 'salvo…';

  const r = await fetch(`/api/manualistica/compilazioni/${ID}/autorizzati`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(d)
  });

  if (!r.ok) {
    riga.dataset.salvando = '';
    if (esito) esito.textContent = '';
    avviso(await r.text());
    return;
  }

  // Si ridisegna sempre: la qualifica cambia l'ordine delle righe e
  // quali campi si possono scrivere.
  await caricaPersone();
}

async function togliPersona(id) {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/autorizzati/${id}/togli`, { method: 'POST' });
  if (!r.ok) { avviso(await r.text()); return; }
  await caricaPersone();
}

function personaNuova() {
  if (!qualifiche.length) { avviso('Non ci sono qualifiche configurate.'); return; }

  const corpo = $('#tabella-autorizzati tbody');
  if (!persone.length) corpo.innerHTML = '';

  const prima = qualifiche[0];
  const vuota = {
    id: 0, idQualifica: Number(prima.codice), qualifica: prima.nome,
    richiedeNominativo: prima.nominativo, nome: '', cognome: '', quantita: 1, funzioni: []
  };

  corpo.insertAdjacentHTML('beforeend', rigaPersona(vuota));
  const riga = corpo.lastElementChild;
  collegaPersona(riga);
  riga.querySelector('[data-p="nome"]').focus();
}

// ── Archivi e gestionali ─────────────────────────────────────────────

let archivi = [];
let gestionali = [];
let caselleEmail = [];
let funzioniAziendali = [];
let tendine = {};

async function caricaArchivi() {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/archivi`);
  if (!r.ok) { avviso('Non riesco a leggere archivi e gestionali.'); return; }
  const d = await r.json();
  archivi = d.archivi || [];
  gestionali = d.gestionali || [];
  caselleEmail = d.caselle || [];
  funzioniAziendali = d.funzioni || [];
  tendine = d.tendine || {};
  disegnaGestionali();
  disegnaArchivi();
  disegnaCaselle();
}

// ── Le caselle di posta ──────────────────────────────────────────────

function disegnaCaselle() {
  const corpo = $('#tabella-caselle tbody');

  if (!caselleEmail.length) {
    corpo.innerHTML = '<tr><td colspan="3">Nessuna casella.</td></tr>';
  } else {
    corpo.innerHTML = caselleEmail.map((c) => `<tr data-casella="${c.id}">
        <td><input type="text" data-c="indirizzo" value="${esc(c.indirizzo)}" maxlength="255" /></td>
        <td>
          <div class="scelte" data-c="funzioni">
            ${funzioniAziendali.map((f) => `<label class="scelta">
                <input type="checkbox" value="${esc(f.codice)}" ${c.funzioni.includes(f.codice) ? 'checked' : ''} />
                ${esc(f.nome)}</label>`).join('')}
          </div>
        </td>
        <td class="nota-riga">
          <span data-esito-c="${c.id}"></span>
          ${puoCompilare ? `<button class="pulsante pulsante-secondario pulsante-piccolo" data-togli-c="${c.id}">Togli</button>` : ''}
        </td>
      </tr>`).join('');
  }

  if (!puoCompilare) { corpo.querySelectorAll('input').forEach((i) => { i.disabled = true; }); return; }

  corpo.querySelectorAll('tr[data-casella]').forEach((riga) => {
    riga.querySelectorAll('[data-c]').forEach((el) => {
      el.addEventListener('change', () => salvaCasella(riga));
      if (el.tagName === 'INPUT' && el.type === 'text') el.addEventListener('blur', () => salvaCasella(riga));
    });
    const togli = riga.querySelector('[data-togli-c]');
    if (togli) togli.addEventListener('click', () => togliCasella(togli.dataset.togliC));
  });
}

async function salvaCasella(riga) {
  if (!riga || riga.dataset.salvando) return;

  const indirizzo = riga.querySelector('[data-c="indirizzo"]').value.trim();
  if (!indirizzo) return;

  const d = {
    id: Number(riga.dataset.casella),
    indirizzo,
    funzioni: [...riga.querySelector('[data-c="funzioni"]').querySelectorAll('input:checked')].map((x) => x.value)
  };

  const vecchia = caselleEmail.find((c) => c.id === d.id);
  if (vecchia && vecchia.indirizzo === d.indirizzo
      && vecchia.funzioni.join() === d.funzioni.join()) return;

  riga.dataset.salvando = '1';
  const esito = $(`[data-esito-c="${d.id}"]`);
  if (esito) esito.textContent = 'salvo…';

  const r = await fetch(`/api/manualistica/compilazioni/${ID}/caselle`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(d)
  });

  if (!r.ok) { riga.dataset.salvando = ''; if (esito) esito.textContent = ''; avviso(await r.text()); return; }
  await caricaArchivi();
}

async function togliCasella(id) {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/caselle/${id}/togli`, { method: 'POST' });
  if (!r.ok) { avviso(await r.text()); return; }
  await caricaArchivi();
}

function casellaNuova() {
  caselleEmail.push({ id: 0, indirizzo: '', funzioni: [] });
  disegnaCaselle();
  const ultima = $('#tabella-caselle tbody').lastElementChild;
  if (ultima) ultima.querySelector('[data-c="indirizzo"]').focus();
}

// Una tendina che accetta anche quello che non c'è nell'elenco: le voci
// vengono dai modelli, ma un cliente può avere un prodotto che nessun
// modello prevede.
function tendinaLibera(campo, chiave, valore, larghezza = '') {
  const voci = tendine[chiave] || [];
  const id = `l-${chiave}-${Math.random().toString(36).slice(2, 8)}`;
  return `<input type="text" data-a="${campo}" list="${id}" value="${esc(valore)}" maxlength="400" style="${larghezza}" />
    <datalist id="${id}">${voci.map((v) => `<option value="${esc(v)}"></option>`).join('')}</datalist>`;
}

// Chi accede a un archivio sono le funzioni aziendali, tutte e sette,
// non le tre che Vaccarini ha messo nella tendina del Registro: il
// valore si scrive nel documento come nome, quindi non si perde niente
// e la spunta è la stessa degli autorizzati e delle caselle (11/11/2026).
function caselleAccessi(scelte) {
  return `<div class="scelte" data-a="accessi">
      ${funzioniAziendali.map((f) => `<label class="scelta">
          <input type="checkbox" value="${esc(f.nome)}" ${scelte.includes(f.nome) ? 'checked' : ''} />
          ${esc(f.nome)}</label>`).join('')}
    </div>`;
}

function caselle(campo, chiave, scelte) {
  const voci = tendine[chiave] || [];
  return `<div class="scelte" data-a="${campo}">
      ${voci.map((v) => `<label class="scelta">
          <input type="checkbox" value="${esc(v)}" ${scelte.includes(v) ? 'checked' : ''} />
          ${esc(v)}</label>`).join('')}
    </div>`;
}

// ── I gestionali ─────────────────────────────────────────────────────

function disegnaGestionali() {
  const corpo = $('#tabella-gestionali tbody');
  const spento = puoCompilare ? '' : 'disabled';

  if (!gestionali.length) {
    corpo.innerHTML = '<tr><td colspan="7">Nessun gestionale.</td></tr>';
  } else {
    corpo.innerHTML = gestionali.map((g) => `<tr data-gestionale="${g.id}">
        <td>${tendinaLibera('prodotto', 'prodotto', g.prodotto)}</td>
        <td>${tendinaLibera('misureSicurezza', 'misureGestionale', g.misureSicurezza)}</td>
        <td>${tendinaLibera('manutenzione', 'manutenzione', g.manutenzione)}</td>
        <td>${tendinaLibera('backup', 'backupGestionale', g.backup)}</td>
        <td>${tendinaLibera('frequenza', 'frequenzaGestionale', g.frequenza)}</td>
        <td>${tendinaLibera('luogoConservazione', 'conservazioneGestionale', g.luogoConservazione)}</td>
        <td class="nota-riga">
          <span data-esito-g="${g.id}"></span>
          ${puoCompilare ? `<button class="pulsante pulsante-secondario pulsante-piccolo" data-togli-g="${g.id}">Togli</button>` : ''}
        </td>
      </tr>`).join('');
  }

  if (!puoCompilare) { corpo.querySelectorAll('input').forEach((i) => { i.disabled = true; }); return; }

  corpo.querySelectorAll('tr[data-gestionale]').forEach((riga) => {
    riga.querySelectorAll('[data-a]').forEach((el) => {
      el.addEventListener('change', () => salvaGestionale(riga));
      el.addEventListener('blur', () => salvaGestionale(riga));
    });
    const togli = riga.querySelector('[data-togli-g]');
    if (togli) togli.addEventListener('click', () => togliGestionale(togli.dataset.togliG));
  });
}

function datiGestionale(riga) {
  const v = (c) => (riga.querySelector(`[data-a="${c}"]`) || {}).value || '';
  return {
    id: Number(riga.dataset.gestionale),
    prodotto: v('prodotto').trim(),
    misureSicurezza: v('misureSicurezza').trim(),
    manutenzione: v('manutenzione').trim(),
    backup: v('backup').trim(),
    frequenza: v('frequenza').trim(),
    luogoConservazione: v('luogoConservazione').trim()
  };
}

async function salvaGestionale(riga) {
  if (!riga || riga.dataset.salvando) return;
  const d = datiGestionale(riga);
  if (!d.prodotto) return;

  const vecchio = gestionali.find((g) => g.id === d.id);
  if (vecchio && Object.keys(d).every((k) => (vecchio[k] || '') === d[k])) return;

  riga.dataset.salvando = '1';
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/gestionali`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(d)
  });
  if (!r.ok) { riga.dataset.salvando = ''; avviso(await r.text()); return; }
  await caricaArchivi();
}

async function togliGestionale(id) {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/gestionali/${id}/togli`, { method: 'POST' });
  if (!r.ok) { avviso(await r.text()); return; }
  await caricaArchivi();
}

function gestionaleNuovo() {
  gestionali.push({ id: 0, prodotto: '', misureSicurezza: '', manutenzione: '', backup: '', frequenza: '', luogoConservazione: '' });
  disegnaGestionali();
  const ultimo = $('#tabella-gestionali tbody').lastElementChild;
  if (ultimo) ultimo.querySelector('[data-a="prodotto"]').focus();
}

// ── Gli archivi ──────────────────────────────────────────────────────

function disegnaArchivi() {
  const contenitore = $('#elenco-archivi');

  if (!archivi.length) {
    contenitore.innerHTML = '<p class="sottotitolo">Nessun archivio. Comincia dall\'armadio dell\'amministrazione e dal computer del Front Office.</p>';
    return;
  }

  contenitore.innerHTML = archivi.map((a, i) => schedaArchivio(a, i)).join('');

  if (!puoCompilare) { contenitore.querySelectorAll('input').forEach((x) => { x.disabled = true; }); return; }

  contenitore.querySelectorAll('[data-archivio]').forEach((box) => {
    box.querySelectorAll('[data-a]').forEach((el) => {
      el.addEventListener('change', () => salvaArchivio(box));
      if (el.tagName === 'INPUT' && el.type === 'text') el.addEventListener('blur', () => salvaArchivio(box));
    });
    const togli = box.querySelector('[data-togli-a]');
    if (togli) togli.addEventListener('click', () => togliArchivio(togli.dataset.togliA));
  });
}

function schedaArchivio(a, i) {
  const info = a.tipo === 'Informatico';
  const suffisso = info ? 'Informatico' : 'Cartaceo';
  const numero = archivi.filter((x, k) => x.tipo === a.tipo && k <= i).length;

  const soloComputer = !info ? '' : `
      <div class="griglia-filtri">
        <label>Sistema operativo ${tendinaLibera('sistemaOperativo', 'sistemaOperativo', a.sistemaOperativo)}</label>
        <label>Antivirus ${tendinaLibera('antivirus', 'antivirus', a.antivirus)}</label>
        <label>Backup del PC ${tendinaLibera('backup', 'backup', a.backup)}</label>
        <label>Frequenza del backup ${tendinaLibera('frequenzaBackup', 'frequenzaBackup', a.frequenzaBackup)}</label>
        <label>Dove si conserva il backup ${tendinaLibera('luogoConservazione', 'luogoConservazione', a.luogoConservazione)}</label>
      </div>
      <p class="nota-riga" style="margin-top:.5rem">Gestionali installati su questo computer</p>
      <div class="scelte" data-a="gestionali">
        ${gestionali.filter((g) => g.id).map((g) => `<label class="scelta">
            <input type="checkbox" value="${g.id}" ${a.gestionali.includes(g.id) ? 'checked' : ''} />
            ${esc(g.prodotto)}</label>`).join('') || '<span class="nota-riga">nessun gestionale inserito</span>'}
      </div>`;

  return `<div class="riquadro" data-archivio="${a.id}" data-tipo="${a.tipo}" style="margin-bottom:1rem">
      <div class="barra-strumenti">
        <h4 style="margin:0">Archivio ${esc(suffisso.toLowerCase())} ${numero}</h4>
        ${puoCompilare ? `<button class="pulsante pulsante-secondario pulsante-piccolo" data-togli-a="${a.id}">Togli</button>` : ''}
      </div>

      <div class="griglia-filtri">
        <label>Dove si trova ${tendinaLibera('luogo', 'luogo' + suffisso, a.luogo)}</label>
      </div>

      <p class="nota-riga" style="margin-top:.5rem">Chi vi accede</p>
      ${caselleAccessi(a.accessi)}

      <p class="nota-riga" style="margin-top:.5rem">Categorie di interessati</p>
      ${caselle('interessati', 'interessati' + suffisso, a.interessati)}

      <p class="nota-riga" style="margin-top:.5rem">Categorie di dati</p>
      ${caselle('categorieDati', 'dati' + suffisso, a.categorieDati)}

      ${soloComputer}
      <div class="nota-riga" data-esito-a="${a.id}"></div>
    </div>`;
}

function datiArchivio(box) {
  const el = (c) => box.querySelector(`[data-a="${c}"]`);
  const testo = (c) => (el(c) || {}).value || '';
  const spunte = (c) => {
    const g = el(c);
    return g ? [...g.querySelectorAll('input:checked')].map((x) => x.value) : [];
  };

  return {
    id: Number(box.dataset.archivio),
    tipo: box.dataset.tipo,
    luogo: testo('luogo').trim(),
    sistemaOperativo: testo('sistemaOperativo').trim(),
    antivirus: testo('antivirus').trim(),
    backup: testo('backup').trim(),
    frequenzaBackup: testo('frequenzaBackup').trim(),
    luogoConservazione: testo('luogoConservazione').trim(),
    accessi: spunte('accessi'),
    interessati: spunte('interessati'),
    categorieDati: spunte('categorieDati'),
    gestionali: spunte('gestionali').map(Number)
  };
}

async function salvaArchivio(box) {
  if (!box || box.dataset.salvando) return;
  const d = datiArchivio(box);

  box.dataset.salvando = '1';
  const esito = box.querySelector(`[data-esito-a="${d.id}"]`);
  if (esito) esito.textContent = 'salvo…';

  const r = await fetch(`/api/manualistica/compilazioni/${ID}/archivi`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(d)
  });

  if (!r.ok) { box.dataset.salvando = ''; if (esito) esito.textContent = ''; avviso(await r.text()); return; }

  if (!d.id) { await caricaArchivi(); return; }

  const posto = archivi.findIndex((x) => x.id === d.id);
  if (posto >= 0) archivi[posto] = { ...archivi[posto], ...d };
  box.dataset.salvando = '';

  if (esito) {
    esito.textContent = 'salvato';
    setTimeout(() => { if (esito.textContent === 'salvato') esito.textContent = ''; }, 1500);
  }
}

async function togliArchivio(id) {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/archivi/${id}/togli`, { method: 'POST' });
  if (!r.ok) { avviso(await r.text()); return; }
  await caricaArchivi();
}

function archivioNuovo(tipo) {
  archivi.push({
    id: 0, tipo, luogo: '', sistemaOperativo: '', antivirus: '', backup: '',
    frequenzaBackup: '', luogoConservazione: '',
    accessi: [], interessati: [], categorieDati: [], gestionali: []
  });
  disegnaArchivi();
  const ultimo = $('#elenco-archivi').lastElementChild;
  if (ultimo) ultimo.querySelector('[data-a="luogo"]').focus();
}

// ── Le firme ─────────────────────────────────────────────────────────

const RUOLI_FIRMA = [
  { codice: 'LegaleRappresentante', nome: 'Legale rappresentante del cliente' },
  { codice: 'Consulente', nome: 'Consulente MARE.A' }
];

let firme = [];

async function caricaFirme() {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/firme`);
  if (!r.ok) { avviso('Non riesco a leggere le firme.'); return; }
  const d = await r.json();
  firme = d.firme || [];
  disegnaFirme();
}

function disegnaFirme() {
  $('#riquadri-firme').innerHTML = RUOLI_FIRMA.map((ruolo) => {
    const presa = firme.find((f) => f.ruolo === ruolo.codice);
    // La marca temporale nell'indirizzo evita che il browser mostri la
    // firma di prima dopo averne caricata una nuova.
    const immagine = presa
      ? `<img src="/api/manualistica/compilazioni/${ID}/firme/${ruolo.codice}/immagine?t=${Date.now()}"
             alt="firma" style="max-width:220px;max-height:90px;display:block;margin:.5rem 0" />`
      : '<p class="nota-riga">Nessuna firma caricata.</p>';

    return `<div>
        <strong>${esc(ruolo.nome)}</strong>
        ${immagine}
        ${presa ? `<div class="nota-riga">${esc(presa.nomeFirmatario || '')}</div>` : ''}
        ${puoCompilare ? `
          <input type="file" accept="image/png" data-firma="${ruolo.codice}" />
          ${presa ? `<button class="pulsante pulsante-secondario pulsante-piccolo" data-togli-firma="${ruolo.codice}">Togli</button>` : ''}` : ''}
      </div>`;
  }).join('');

  if (!puoCompilare) return;

  $$('[data-firma]').forEach((el) => el.addEventListener('change', () => caricaFile(el)));
  $$('[data-togli-firma]').forEach((b) =>
    b.addEventListener('click', () => togliFirma(b.dataset.togliFirma)));
}

async function caricaFile(el) {
  const file = el.files && el.files[0];
  if (!file) return;

  const corpo = new FormData();
  corpo.append('file', file);

  const r = await fetch(`/api/manualistica/compilazioni/${ID}/firme/${el.dataset.firma}`, {
    method: 'POST', body: corpo
  });

  el.value = '';
  if (!r.ok) { avviso(await r.text()); return; }
  await caricaFirme();
}

async function togliFirma(ruolo) {
  const r = await fetch(`/api/manualistica/compilazioni/${ID}/firme/${ruolo}/togli`, { method: 'POST' });
  if (!r.ok) { avviso(await r.text()); return; }
  await caricaFirme();
}

function avviso(messaggio) {
  $('#avviso-corpo').innerHTML = `<p>${esc(messaggio || 'Operazione non riuscita.')}</p>`;
  $('#dlg-avviso').showModal();
}

if ($('#btn-genera')) $('#btn-genera').addEventListener('click', genera);
if ($('#btn-genera-pdf')) $('#btn-genera-pdf').addEventListener('click', generaPdf);
if ($('#btn-invia')) $('#btn-invia').addEventListener('click', invia);
if ($('#btn-riapri')) $('#btn-riapri').addEventListener('click', riapri);

$('#btn-sede-nuova').addEventListener('click', sedeNuova);
$('#btn-persona-nuova').addEventListener('click', personaNuova);
$('#btn-gestionale-nuovo').addEventListener('click', gestionaleNuovo);
$('#btn-archivio-cartaceo').addEventListener('click', () => archivioNuovo('Cartaceo'));
$('#btn-archivio-informatico').addEventListener('click', () => archivioNuovo('Informatico'));
$('#btn-casella-nuova').addEventListener('click', casellaNuova);
$('#btn-proposte').addEventListener('click', mostraProposte);
$('#btn-importa').addEventListener('click', importaScelte);
$('#btn-chiudi-proposte').addEventListener('click', () => { $('#pannello-proposte').hidden = true; });

await apri();
