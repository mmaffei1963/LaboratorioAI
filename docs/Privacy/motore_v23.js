/* Motore di generazione – versione di prova nel browser.
   Stessa logica dei prototipi Python (gen_generico, gen_nomine, gen_tabella, gen_responsabili, organigramma),
   da riscrivere in C# con OpenXml in Laboratorio AI. Lavora sui modelli marcati con controlli contenuto (w:sdt + w:tag). */
(function () {
  'use strict';
  const W = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main';
  const W14 = 'http://schemas.microsoft.com/office/word/2010/wordml';
  const WP = 'http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing';
  const XMLNS = 'http://www.w3.org/2000/xmlns/';
  const NSDECL = 'xmlns:w="' + W + '" xmlns:wp="' + WP + '" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" ' +
    'xmlns:wpg="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup" xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape" ' +
    'xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"';

  const ORDINE = ['Amministrazione', 'Front Office', 'Capo chef', 'Guardiano notturno', 'Cameriera ai piani', 'Cameriere di sala', 'Capo sala'];
  const CON_NOMINATIVO = ['Legale Rappresentante', 'Socio', 'Coadiuvante familiare'];

  /* ---------- utilità DOM ---------- */
  const isW = (e, n) => e && e.nodeType === 1 && e.namespaceURI === W && e.localName === n;
  const kids = (e, n) => [...e.children].filter(c => isW(c, n));
  const kid = (e, n) => kids(e, n)[0] || null;
  const all = (e, n, ns) => [...e.getElementsByTagNameNS(ns || W, n)];
  const wa = (e, n) => e.getAttributeNS(W, n);
  const setWa = (e, n, v) => e.setAttributeNS(W, 'w:' + n, v);
  const mk = (doc, n) => doc.createElementNS(W, 'w:' + n);
  function tag(s) { const pr = kid(s, 'sdtPr'); const t = pr && kid(pr, 'tag'); return t ? wa(t, 'val') : ''; }
  function setTag(s, v) { const t = kid(kid(s, 'sdtPr'), 'tag'); if (t) setWa(t, 'val', v); }
  const content = s => kid(s, 'sdtContent');
  const testo = e => all(e, 't').map(t => t.textContent).join('');
  function sdts(root, pred) { return all(root, 'sdt').filter(s => pred(tag(s))); }
  function unwrap(s) {
    const c = content(s), p = s.parentNode;
    if (c) while (c.firstChild) p.insertBefore(c.firstChild, s);
    p.removeChild(s);
  }
  const remove = e => e.parentNode && e.parentNode.removeChild(e);
  // toglie una parte conservando l'eventuale interruzione di sezione (intestazioni e piè di pagina)
  function togliParte(e) {
    const sps = all(e, 'sectPr').filter(sp => isW(sp.parentNode, 'pPr'));
    if (sps.length && !isW(e, 'r') && !isW(e.parentNode, 'p')) {
      const doc = e.ownerDocument, ultimo = sps[sps.length - 1].cloneNode(true);
      for (const tipo of ['headerReference', 'footerReference'])
        for (const sp of sps.slice(0, -1).reverse()) for (const ref of kids(sp, tipo))
          if (!kids(ultimo, tipo).some(x => wa(x, 'type') === wa(ref, 'type'))) ultimo.insertBefore(ref.cloneNode(true), ultimo.firstChild);
      const p = mk(doc, 'p'), ppr = mk(doc, 'pPr'); ppr.appendChild(ultimo); p.appendChild(ppr);
      e.parentNode.insertBefore(p, e);
    }
    remove(e);
  }
  const norm = v => String(v == null ? '' : v).replace(/^[\s\-–]+/, '').replace(/[’']/g, "'").replace(/\s+/g, ' ').trim().toLowerCase();
  function listItems(s) {
    const pr = kid(s, 'sdtPr'); if (!pr) return [];
    return all(pr, 'listItem').map(li => wa(li, 'displayText') || wa(li, 'value') || '');
  }
  let idSeq = 1;
  function rinumera(el, tagBm) {
    // copie: id univoci per controlli, segnalibri, disegni; via gli id di paragrafo (Word li rigenera)
    for (const x of [el, ...el.getElementsByTagName('*')]) {
      if (x.hasAttributeNS(W14, 'paraId')) x.removeAttributeNS(W14, 'paraId');
      if (x.hasAttributeNS(W14, 'textId')) x.removeAttributeNS(W14, 'textId');
    }
    for (const i of all(el, 'id')) if (isW(i.parentNode, 'sdtPr')) setWa(i, 'val', String(100000000 + Math.floor(Math.random() * 1900000000)));
    for (const b of [...all(el, 'bookmarkStart'), ...all(el, 'bookmarkEnd')]) {
      setWa(b, 'id', String(Number(wa(b, 'id') || 0) + 1000 * tagBm));
      if (wa(b, 'name')) setWa(b, 'name', wa(b, 'name') + '_' + tagBm);
    }
    for (const d of all(el, 'docPr', WP)) d.setAttribute('id', String(5000 + (idSeq++)));
  }
  let bmSeq = 1;
  const clona = e => { const c = e.cloneNode(true); rinumera(c, 900 + (bmSeq++)); return c; };

  /* ---------- scrittura di un valore in un controllo ---------- */
  function setText(s, value, opts) {
    opts = opts || {};
    const doc = s.ownerDocument, pr = kid(s, 'sdtPr'), c = content(s);
    value = value == null ? '' : String(value);
    // voce della tendina se corrisponde (stessa forma del modello, es. «- Amministrazione»)
    const items = listItems(s);
    const hit = items.find(i => norm(i) && norm(i) === norm(value));
    if (hit && !opts.raw) value = hit;
    if (pr) {
      for (const x of kids(pr, 'showingPlcHdr')) pr.removeChild(x);
      const dd = kid(pr, 'dropDownList');
      if (dd && !hit) { const cb = mk(doc, 'comboBox'); while (dd.firstChild) cb.appendChild(dd.firstChild); pr.replaceChild(cb, dd); }
      const dt = kid(pr, 'date'); const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);
      if (dt && m) setWa(dt, 'fullDate', m[3] + '-' + m[2] + '-' + m[1] + 'T00:00:00Z');
    }
    if (!c) return;
    for (const rs of all(c, 'rStyle')) if (/segnaposto|Placeholder/i.test(wa(rs, 'val'))) remove(rs);
    for (const h of all(c, 'highlight')) remove(h);
    let ts = all(c, 't');
    if (!ts.length) {
      let r = all(c, 'r')[0];
      if (!r) { const p = all(c, 'p')[0] || c; r = mk(doc, 'r'); p.appendChild(r); }
      for (const ch of [...r.children]) if (!isW(ch, 'rPr')) r.removeChild(ch);
      const t = mk(doc, 't'); r.appendChild(t); ts = [t];
    }
    for (const b of all(c, 'br')) if (!b.hasAttributeNS(W, 'type')) remove(b);
    const righe = value.split('\n');
    ts[0].textContent = righe[0]; ts[0].setAttribute('xml:space', 'preserve');
    for (const t of ts.slice(1)) t.textContent = '';
    let dopo = ts[0];
    for (const r of righe.slice(1)) {
      const br = mk(doc, 'br'); dopo.parentNode.insertBefore(br, dopo.nextSibling);
      const nt = mk(doc, 't'); nt.textContent = r; nt.setAttribute('xml:space', 'preserve');
      br.parentNode.insertBefore(nt, br.nextSibling); dopo = nt;
    }
  }
  function fill(root, values) {
    for (const s of all(root, 'sdt')) {
      const t = tag(s);
      if (!t.startsWith('CAMPO:')) continue;
      const k = t.slice(6);
      if (!(k in values)) continue;
      let v = values[k];
      if (k === 'REVISIONE_NUMERO' && /^\s*Rev/i.test(testo(s))) v = 'Rev. ' + v;
      const nome = NOMI.test(k) && String(v || '').trim();
      setText(s, nome ? String(v).toUpperCase() : v); setTag(s, 'CAMPO:_' + k);
      if (nome) evidenziaNome(s);   // compilato: non va riscritto dai passi successivi
    }
  }
  // nome e cognome di LR, soci, coadiuvanti: maiuscolo, grassetto, centrati nella casella (decisione 27/10/2026)
  const NOMI = /^(AUTORIZZATO|LR|SOSTITUTO_LR|BACKUP_AUTORIZZATO|CREDENZIALI_AUTORIZZATO)_(NOME|COGNOME)$/;
  function evidenziaNome(s) {
    const doc = s.ownerDocument;
    for (const r of all(content(s), 'r')) {
      let rpr = kid(r, 'rPr'); if (!rpr) { rpr = mk(doc, 'rPr'); r.insertBefore(rpr, r.firstChild); }
      if (!kid(rpr, 'b')) { const b = mk(doc, 'b'); const ref = kid(rpr, 'rStyle') || kid(rpr, 'rFonts'); rpr.insertBefore(b, ref ? ref.nextSibling : rpr.firstChild); }
    }
    let p = s; while (p && !isW(p, 'p')) p = p.parentNode;
    if (!p) return;
    let intc = false; for (let a = p.parentNode; a; a = a.parentNode) if (isW(a, 'tc')) { intc = true; break; }
    if (!intc || testo(p).trim() !== testo(s).trim()) return;
    let ppr = kid(p, 'pPr'); if (!ppr) { ppr = mk(doc, 'pPr'); p.insertBefore(ppr, p.firstChild); }
    let jc = kid(ppr, 'jc');
    if (!jc) { jc = mk(doc, 'jc'); ppr.insertBefore(jc, kid(ppr, 'textDirection') || kid(ppr, 'textAlignment') || kid(ppr, 'textboxTightWrap') || kid(ppr, 'outlineLvl') || kid(ppr, 'divId') || kid(ppr, 'cnfStyle') || kid(ppr, 'rPr') || kid(ppr, 'sectPr') || kid(ppr, 'pPrChange') || null); }
    setWa(jc, 'val', 'center');
  }
  // sostituisce un testo dentro un paragrafo anche se è diviso in più run (tiene la formattazione del primo run)
  function sostituisci(p, da, a) {
    const ts = all(p, 't'); let full = '', pos = [];
    for (const t of ts) { pos.push(full.length); full += t.textContent; }
    const i = full.indexOf(da); if (i < 0) return false;
    const j = i + da.length; let messo = false;
    ts.forEach((t, n) => {
      const s0 = pos[n], s1 = s0 + t.textContent.length; if (s1 <= i || s0 >= j) return;
      const tx = t.textContent, a0 = Math.max(i, s0) - s0, a1 = Math.min(j, s1) - s0;
      t.textContent = tx.slice(0, a0) + (messo ? '' : a) + tx.slice(a1); messo = true; t.setAttribute('xml:space', 'preserve');
    });
    return true;
  }
  const paragrafi = (doc, re) => all(doc, 'p').filter(p => re.test(testo(p)));
  const vuoto = p => isW(p, 'p') && !testo(p).trim() && !all(p, 'drawing').length && !all(p, 'br').some(b => wa(b, 'type') === 'page') && !all(p, 'sectPr').length && !all(p, 'sdt').length && !all(p, 'object').length && !all(p, 'pict').length;
  function pageBreakBefore(p) {
    const doc = p.ownerDocument; let ppr = kid(p, 'pPr');
    if (!ppr) { ppr = mk(doc, 'pPr'); p.insertBefore(ppr, p.firstChild); }
    if (kid(ppr, 'pageBreakBefore')) return;
    const pb = mk(doc, 'pageBreakBefore');
    const prima = ['pStyle', 'keepNext', 'keepLines'].map(n => kid(ppr, n)).filter(Boolean).pop();
    ppr.insertBefore(pb, prima ? prima.nextSibling : ppr.firstChild);
  }
  // valori legati alla posizione (stessa etichetta più volte nello stesso blocco): «ETICHETTA#n»
  function fillPos(root, values) {
    const occ = {};
    for (const s of all(root, 'sdt')) {
      const t = tag(s);
      if (!t.startsWith('CAMPO:')) continue;
      const k = t.slice(6); occ[k] = (occ[k] || 0) + 1;
      const key = k + '#' + occ[k];
      if (key in values) { setText(s, values[key]); }
    }
  }
  /* modulo (tabella) di un blocco ripetuto: righe con etichetta e campi, per costruire la schermata */
  function moduloBlocco(s) {
    const occ = {}, campi = [];
    for (const x of all(s, 'sdt')) {
      const t = tag(x); if (!t.startsWith('CAMPO:')) continue;
      const k = t.slice(6); occ[k] = (occ[k] || 0) + 1;
      const pr = kid(x, 'sdtPr'), al = pr && kid(pr, 'alias');
      // etichetta: testo della prima cella della riga, se il campo è in una tabella
      let tr = x; while (tr && !isW(tr, 'tr')) tr = tr.parentNode;
      let riga = '', nriga = -1;
      if (tr) { nriga = all(s, 'tr').indexOf(tr); const c0 = [...tr.children].find(c => isW(c, 'tc') || isW(c, 'sdt')); if (c0 && !all(c0, 'sdt').includes(x) && c0 !== x) riga = testo(c0).trim(); }
      let rigaPrec = '';
      if (tr && !riga) { let t2 = tr.previousElementSibling; while (t2 && !rigaPrec) { if (isW(t2, 'tr')) { const c0 = [...t2.children].find(c => isW(c, 'tc')); if (c0) rigaPrec = testo(c0).trim(); } t2 = t2.previousElementSibling; } }
      campi.push({ key: k + '#' + occ[k], tag: k, nriga, rigaPrec, alias: al ? wa(al, 'val') : k, riga, items: listItems(x).filter(i => i.trim()), def: testo(x).replace(/\s+$/, '') });
    }
    return campi;
  }

  /* ---------- pulizia ---------- */
  function togliCommentiXml(doc) {
    for (const n of ['commentRangeStart', 'commentRangeEnd']) all(doc, n).forEach(remove);
    for (const cr of all(doc, 'commentReference')) { const r = cr.parentNode; remove(isW(r, 'r') ? r : cr); }
  }
  function sistemaVuoti(doc) {
    for (const tbl of all(doc, 'tbl')) if (!all(tbl, 'tr').length) remove(tbl);
    for (const tc of all(doc, 'tc')) if (!kids(tc, 'p').length && !kids(tc, 'tbl').length && !kids(tc, 'sdt').length) tc.appendChild(mk(doc, 'p'));
    for (const tc of all(doc, 'tc')) { const last = [...tc.children].pop(); if (isW(last, 'tbl')) tc.appendChild(mk(doc, 'p')); }
    // due tabelle consecutive si fonderebbero: un paragrafo vuoto in mezzo
    for (const tbl of all(doc, 'tbl')) { const n = tbl.nextElementSibling; if (isW(n, 'tbl')) tbl.parentNode.insertBefore(mk(doc, 'p'), n); }
  }

  /* ---------- contesto dalla check list ---------- */
  function contesto(cl) {
    const C = {};
    const V = cl.VIDEO, P = cl.PISCINA;
    C.VIDEO_PRESENTE = V !== 'NESSUNA'; C.PISCINA_PRESENTE = P !== 'NESSUNA';
    C.VIDEO_REGISTRAZIONE = V === 'VISIONE_REG' || V === 'SOLO_REG';
    C.PISCINA_REGISTRAZIONE = P === 'VISIONE_REG' || P === 'SOLO_REG';
    C.VIDEO_O_PISCINA = C.VIDEO_PRESENTE || C.PISCINA_PRESENTE;
    // visione in tempo reale per impianto: la piscina non ha «solo registrazione», ma la regola vale per entrambi
    C.VIDEO_VISIONE = C.VIDEO_PRESENTE && V !== 'SOLO_REG'; C.PISCINA_VISIONE = C.PISCINA_PRESENTE && P !== 'SOLO_REG';
    C.VISIONE_DIRETTA = C.VIDEO_VISIONE || C.PISCINA_VISIONE;   // almeno un impianto si vede in diretta (03/11/2026, corretto per le 12 combinazioni)
    C.QUALSIASI_REGISTRAZIONE = C.VIDEO_REGISTRAZIONE || C.PISCINA_REGISTRAZIONE;
    C.VIDEO_NON_REGISTRA = C.VIDEO_PRESENTE && !C.VIDEO_REGISTRAZIONE; C.PISCINA_NON_REGISTRA = C.PISCINA_PRESENTE && !C.PISCINA_REGISTRAZIONE;
    // impianti in modalità diverse: nella DPIA una frase per impianto (03/11/2026, MARCATURA_DPIA_Impianti_misti.md)
    const entrambi = C.VIDEO_PRESENTE && C.PISCINA_PRESENTE;
    C.IMPIANTI_MISTI_REGISTRAZIONE = entrambi && C.VIDEO_REGISTRAZIONE !== C.PISCINA_REGISTRAZIONE;
    C.IMPIANTI_MISTI_VISIONE = entrambi && C.VIDEO_VISIONE !== C.PISCINA_VISIONE;
    for (const f of ['FINALITA_STATISTICHE', 'GESTIONALE_CLIENTI', 'FINALITA_COMUNICAZIONE_PRESENZA', 'FINALITA_FOTO_VIDEO', 'FINALITA_MARKETING'])
      C[f] = !!(cl.finalita || {})[f];
    C.FUNZIONE_CAPO_CHEF = !!cl.CAPO_CHEF;
    C.GUARDIANO_PRESENTE = cl.GUARDIANO !== 'NESSUNO';
    C.MANUTENTORI_INTERNI = Number(cl.manutentori || 0) > 0;
    C.CRM_PRESENTE = !!cl.crm.presente; C.WIFI_PRESENTE = !!cl.wifi.presente;
    const tipi = cl.backup.tipi || [];
    C.BACKUP_SERVER = tipi.includes('SERVER'); C.BACKUP_DISPOSITIVI_ESTERNI = tipi.includes('DISPOSITIVI'); C.BACKUP_CLOUD = tipi.includes('CLOUD');
    C.NESSUN_BACKUP = !tipi.length;
    C.SOSTITUTO_LR_DATA_BREACH = cl.sostituto !== '' && cl.sostituto != null;
    C.SALA = cl.SALA; C.PIANI = cl.PIANI; C.GUARDIANO = cl.GUARDIANO;
    const k = cl.contratto || {};
    C.CONTRATTO_FORNITURA = !!k.richiesto; C.ASSISTENZA_ANNUALE = k.assistenza !== false;   // contratto di fornitura servizi (29/10/2026)
    return C;
  }
  function cond(c, C, avvisi) {
    if (c.includes('=')) { const [k, v] = c.split('='); return C[k] === v; }
    if (c in C) return !!C[c];
    avvisi.push('Condizione sconosciuta «' + c + '»: parte mantenuta.');
    return true;
  }
  const dataIt = iso => { const [y, m, d] = iso.split('-'); return d + '/' + m + '/' + y; };
  const elenco = l => l.length <= 1 ? (l[0] || '') : l.slice(0, -1).join(', ') + ' e ' + l[l.length - 1];

  function persone(cl) {   // un elemento per lettera, nell'ordine delle funzioni
    const out = [];
    const ps = cl.autorizzati.filter(p => p.funzioni.length)
      .slice().sort((a, b) => Math.min(...a.funzioni.map(f => ORDINE.indexOf(f))) - Math.min(...b.funzioni.map(f => ORDINE.indexOf(f))));
    for (const p of ps) {
      const fz = p.funzioni.slice().sort((a, b) => ORDINE.indexOf(a) - ORDINE.indexOf(b));
      const n = p.qualifica === 'Lavoratore' ? Math.max(1, Number(p.quantita || 1)) : 1;
      for (let i = 0; i < n; i++) out.push({ p, fz });
    }
    return out;
  }
  const nominativo = p => CON_NOMINATIVO.includes(p.qualifica);

  const trattino = v => { v = String(v || '').trim(); return !v || /^-\s/.test(v) ? v : '- ' + v; };
  function valori(cl, C) {
    const c = cl.cliente, s0 = cl.sedi[0] || {};
    const lr = cl.autorizzati.find(p => p.qualifica === 'Legale Rappresentante') || {};
    const capCom = (cap, com, pr) => [cap, com].filter(Boolean).join(' ') + (pr ? ' (' + pr + ')' : '');
    const d = cl.dataDocumenti; const [y, m, g] = d.split('-').map(Number);
    let ries = new Date(Date.UTC(y + 2, m - 1, g)); if (ries.getUTCMonth() !== m - 1) ries = new Date(Date.UTC(y + 2, m - 1, 28));
    const persona = idx => {
      if (idx === '' || idx == null) return null;
      if (idx === 'LR') return { p: lr, f: 'Amministrazione' };
      const [i, f] = String(idx).split('|'); const p = cl.autorizzati[Number(i)]; return p ? { p, f: f || p.funzioni[0] || '' } : null;
    };
    const incaricato = (pref, idx) => {
      const x = persona(idx); const o = {};
      if (!x) { o[pref + '_NOME'] = ''; o[pref + '_COGNOME'] = ''; o[pref + '_FUNZIONE'] = ' '; o[pref + '_QUALIFICA'] = ' '; return o; }
      o[pref + '_NOME'] = nominativo(x.p) ? (x.p.nome || '') : ''; o[pref + '_COGNOME'] = nominativo(x.p) ? (x.p.cognome || '') : '';
      o[pref + '_FUNZIONE'] = x.f; o[pref + '_QUALIFICA'] = x.p.qualifica; return o;
    };
    const v = Object.assign({
      RAGIONE_SOCIALE: c.RAGIONE_SOCIALE, SEDE_LEGALE_INDIRIZZO: c.INDIRIZZO, SEDE_LEGALE_CAP_COMUNE: capCom(c.CAP, c.COMUNE, c.PROV),
      PARTITA_IVA: c.PARTITA_IVA, CODICE_FISCALE: c.CODICE_FISCALE || c.PARTITA_IVA, EMAIL: c.EMAIL, PEC: c.PEC, TELEFONO: c.TELEFONO,
      CODICE_ATECO: c.CODICE_ATECO, ATTIVITA_ESERCITATA: c.ATTIVITA_ESERCITATA, SITI_WEB: (c.SITI_WEB || '').split(/[\n,;]+/).map(x => x.trim()).filter(Boolean).join(', '),
      SEDE_TRATTAMENTO_NOME: s0.nome || '', SEDE_TRATTAMENTO_INDIRIZZO: s0.indirizzo || '', SEDE_TRATTAMENTO_CAP_COMUNE: capCom(s0.cap, s0.comune, s0.prov),
      REVISIONE_NUMERO: cl.revisione, REVISIONE_DATA: dataIt(d), DATA_DOCUMENTI: dataIt(d),
      DATA_RIESAME_DPIA: dataIt(ries.toISOString().slice(0, 10)), DURATA_CONSERVAZIONE_IMMAGINI: cl.durataOre + ' ore',
      LR_NOME: lr.nome || '', LR_COGNOME: lr.cognome || '', FORMAZIONE_DURATA_ORE: String(cl.formazioneOre),
      TEMPO_RIPRISTINO_ORE: String(cl.backup.ripristinoOre),
      INL_NUMERO: cl.video.INL_NUMERO, INL_DATA: cl.video.INL_DATA ? dataIt(cl.video.INL_DATA) : '', DVR_LUOGO: trattino(cl.video.DVR_LUOGO), SCHERMO_LUOGO: trattino(cl.video.SCHERMO_LUOGO),
      CRM_NOME: cl.crm.CRM_NOME, CRM_FORNITORE: cl.crm.CRM_FORNITORE, CRS_NOME: cl.crm.CRS_NOME, CRS_FORNITORE: cl.crm.CRS_FORNITORE,
      CRO_NOME: cl.crm.CRO_NOME, CRO_FORNITORE: cl.crm.CRO_FORNITORE, WIFI_NOME: cl.wifi.nome,
      BUSTE_PAGA_RICEZIONE: cl.bustePaga.ricezione, BUSTE_PAGA_MAIL: cl.bustePaga.mail, BUSTE_PAGA_CONSERVAZIONE: cl.bustePaga.conservazione,
      BACKUP_SERVER_LUOGO: cl.backup.serverLuogo, BACKUP_DISPOSITIVO: cl.backup.dispositivo, BACKUP_DISPOSITIVO_CUSTODIA: cl.backup.custodia,
      BACKUP_CLOUD_FORNITORE: cl.backup.cloudFornitore, BACKUP_FREQUENZA: cl.backup.frequenza,
      INFORMAZIONI_FORNITE_DA: cl.informazioniFornite
    }, incaricato('BACKUP_AUTORIZZATO', cl.incaricoBackup), incaricato('CREDENZIALI_AUTORIZZATO', cl.incaricoCredenziali),
      incaricato('SOSTITUTO_LR', cl.sostituto));
    Object.assign(v, valoriContratto(cl));
    if (!C.CRM_PRESENTE) for (const k of ['CRM_NOME', 'CRM_FORNITORE', 'CRS_NOME', 'CRS_FORNITORE', 'CRO_NOME', 'CRO_FORNITORE']) delete v[k];
    for (const k in v) if (v[k] == null) v[k] = '';
    return v;
  }
  // contratto di fornitura servizi (modello MODELLO_PRIVACY_Contratto, 29/10/2026)
  const euro = n => (Number(n) || 0).toLocaleString('it-IT', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  function viaCivico(ind) { const m = /^(.*?)[,\s]+(\d+[A-Za-z]?(?:\/\w+)?)\s*$/.exec((ind || '').trim()); return m ? [m[1], m[2]] : [ind || '', '']; }
  function abiCab(iban) { const x = (iban || '').replace(/\s/g, '').toUpperCase(); return /^IT\d{2}[A-Z]\d{10}/.test(x) ? [x.slice(5, 10), x.slice(10, 15)] : ['', '']; }
  function valoriContratto(cl) {
    const k = cl.contratto || {}, c = cl.cliente, s0 = cl.sedi[0] || {};
    const i = k.intestazione === 'ALTRA' && !cl.nuovoCliente ? (k.intestatario || {}) : c;
    const [slv, sln] = viaCivico(i.INDIRIZZO), [sov, son] = viaCivico(s0.indirizzo);
    const lr = cl.autorizzati.find(p => p.qualifica === 'Legale Rappresentante') || {};
    const ref = cl.referente && !cl.referente.comeLR ? [cl.referente.nome, cl.referente.cognome] : [lr.nome, lr.cognome];
    const riba = k.pagamento === 'RIBA' ? (k.riba || {}) : {};
    const [abi, cab] = abiCab(riba.iban);
    const cons = cl.consulente || {};
    return {
      CONTRATTO_RAGIONE_SOCIALE: i.RAGIONE_SOCIALE || '', CONTRATTO_INDIRIZZO: i.INDIRIZZO || '',
      CONTRATTO_CAP_COMUNE: [i.CAP, i.COMUNE].filter(Boolean).join(' ') + (i.PROV ? ' (' + i.PROV + ')' : ''),
      CONTRATTO_SL_VIA: slv, CONTRATTO_SL_CIVICO: sln, CONTRATTO_SL_CITTA: i.COMUNE || '', CONTRATTO_SL_CAP: i.CAP || '', CONTRATTO_SL_PROVINCIA: i.PROV || '',
      CONTRATTO_SO_VIA: sov, CONTRATTO_SO_CIVICO: son, CONTRATTO_SO_CITTA: s0.comune || '', CONTRATTO_SO_CAP: s0.cap || '', CONTRATTO_SO_PROVINCIA: s0.prov || '',
      CONTRATTO_PIVA: i.PARTITA_IVA || '', CONTRATTO_CODFISC: i.CODICE_FISCALE || i.PARTITA_IVA || '', CONTRATTO_SDI: k.sdi || cl.codiceUnivoco || '',
      CONTRATTO_REFERENTE: ref.filter(Boolean).join(' '), CONTRATTO_TELEFONO: k.telefono || i.TELEFONO || c.TELEFONO || '',
      CONTRATTO_EMAIL: k.emailDocumentazione || i.EMAIL || '',
      CONTRATTO_RIBA_ISTITUTO: riba.istituto || '', CONTRATTO_RIBA_AGENZIA: riba.agenzia || '', CONTRATTO_RIBA_IBAN: (riba.iban || '').toUpperCase(), CONTRATTO_RIBA_ABI: abi, CONTRATTO_RIBA_CAB: cab,
      PREZZO_DOCUMENTAZIONE: euro(k.prezzoDocumentazione ?? 300), PREZZO_ASSISTENZA: euro(k.prezzoAssistenza ?? 200),
      CONSULENTE_NOME: cons.nome || '', CONSULENTE_CELLULARE: cons.cellulare || '', CONSULENTE_EMAIL: cons.email || '',
      DATA_DOCUMENTO: dataIt(cl.dataDocumenti), REV_CORRENTE: cl.revisione
    };
  }
  // funzioni con accesso alle immagini: passo 2, corrette dalle scelte del passo 3 «Accesso alle immagini nelle nomine» (03/11/2026)
  function conAccesso(base, acc, test) {
    const s = new Set(base || []);
    for (const [f, v] of Object.entries(acc || {})) {
      if (v === undefined || v === '') continue;
      if (v !== '—' && test(v)) s.add(f); else s.delete(f);
    }
    const prima = (base || []).filter(f => s.has(f));
    return [...prima, ...ORDINE.filter(f => s.has(f) && !prima.includes(f))];
  }
  function liste(cl, C) {
    const acc = cl.videoAccesso || {};
    const vis = conAccesso(cl.video.visione, acc, v => /visione/.test(v));
    const cons = C.QUALSIASI_REGISTRAZIONE ? conAccesso(cl.video.consultazione, acc, v => /e consultazione delle registrazioni|^solo consultazione/.test(v)) : [];
    return {
      FUNZIONI_VISIONE_DIRETTA: C.VISIONE_DIRETTA ? vis : [],
      FUNZIONI_CONSULTAZIONE_REGISTRAZIONI: cons,
      BACKUP_SERVER_FUNZIONI: cl.backup.serverFunzioni, BACKUP_CLOUD_FUNZIONI: cl.backup.cloudFunzioni,
      EMAIL_CASELLE: s => {
        const f = funzioneSezione(s);
        return cl.caselle.filter(e => e.indirizzo && (!f || e.funzioni.includes(f))).map(e => e.indirizzo);
      },
      MISURE_MINACCE_UMANE: cl.misure.MINACCE_UMANE, MISURE_ANTINCENDIO: cl.misure.ANTINCENDIO, MISURE_EVENTI_NATURALI: cl.misure.EVENTI_NATURALI
    };
  }

  // funzione aziendale della sezione del Registro in cui si trova un elemento (titolo «FRONT OFFICE Autorizzati al trattamento»)
  const TITOLI_FUNZ = { 'AMMINISTRAZIONE': 'Amministrazione', 'FRONT OFFICE': 'Front Office', 'CAPO CHEF': 'Capo chef', 'GUARDIANO NOTTURNO': 'Guardiano notturno', 'CAMERIERE AI PIANI': 'Cameriera ai piani', 'CAMERIERI DI SALA': 'Cameriere di sala', 'CAPO SALA': 'Capo sala' };
  function funzioneSezione(el) {
    const doc = el.ownerDocument; let cur = null;
    for (const p of all(doc, 'p')) {
      if (p.compareDocumentPosition(el) & 2) break;   // el viene prima di p
      if (p.contains(el)) break;
      const m = /^(AMMINISTRAZIONE|FRONT OFFICE|CAPO CHEF|GUARDIANO NOTTURNO|CAMERIERE AI PIANI|CAMERIERI DI SALA|CAPO SALA)\s*Autorizzati al trattamento$/i.exec(testo(p).trim());
      if (m) cur = TITOLI_FUNZ[m[1].toUpperCase()];
    }
    return cur;
  }
  /* ---------- testi alternativi ---------- */
  function testoAlt(t, s, cl, C, en) {
    const dur = Number(cl.durataOre);
    const items = listItems(s);
    const voce = re => items.find(i => re.test(i));
    if (t === 'CONSERVAZIONE_VIDEO' || t === 'CONSERVAZIONE_PISCINA') {
      const r = t.endsWith('VIDEO') ? C.VIDEO_REGISTRAZIONE : C.PISCINA_REGISTRAZIONE;
      if (items.length) return r ? (voce(/durata massima/) || '').replace(/\d+\s*h\b/, dur + 'h') : voce(/non sono conservati/);
      if (en) return r ? 'a maximum of ' + dur + ' hours, followed by automatic deletion. ' : 'the data collected for this purpose will not be stored.';
      return r ? 'massimo ' + dur + 'h, con successiva cancellazione automatica. ' : 'i dati raccolti per tale finalità non saranno conservati.';
    }
    if (t === 'REGISTRAZIONI_AUTORITA_VIDEO' || t === 'REGISTRAZIONI_AUTORITA_PISCINA') {
      const r = t.endsWith('VIDEO') ? C.VIDEO_REGISTRAZIONE : C.PISCINA_REGISTRAZIONE;
      return r ? (en ? 'The recordings may also be viewed by the competent public authorities. ' : 'Le registrazioni possono essere visionate anche dalle Autorità pubbliche competenti in materia. ') : ' ';
    }
    if (t === 'CATEGORIE_IMMAGINI_VIDEO') return C.VIDEO_O_PISCINA ? (en ? ', video surveillance images.' : ', immagini di videosorveglianza.') : '.';
    const vis = liste(cl, C).FUNZIONI_VISIONE_DIRETTA, cons = liste(cl, C).FUNZIONI_CONSULTAZIONE_REGISTRAZIONI;
    if (t === 'DPIA_RESPONSABILITA') return ['- TITOLARE DEL TRATTAMENTO', ...vis.map(f => '- ' + f.toUpperCase() + ' Autorizzati')].join('\n');
    if (t === 'FUNZIONI_VISIONE_ELENCO') return elenco(vis);
    if (t === 'FUNZIONI_CONSULTAZIONE_ELENCO') {
      const fz = cons.filter(f => ORDINE.includes(f)), altri = cons.filter(f => !ORDINE.includes(f));
      const parti = [];
      if (fz.length) parti.push((fz.length === 1 ? 'agli autorizzati della funzione ' : 'agli autorizzati delle funzioni ') + elenco(fz));
      for (const a of altri) parti.push(/^RLS\b/i.test(a) ? 'al Rappresentante dei lavoratori per la sicurezza (RLS)' : 'a ' + a);
      return elenco(parti);
    }
    if (t === 'DPIA_PERIODO_CONSERVAZIONE') {
      const reg = 'massimo ' + dur + ' ore, come da Circolare INL n.5 del 19 Febbraio 2018.', noReg = 'le immagini non sono conservate; è prevista la sola visione in tempo reale.';
      if (C.IMPIANTI_MISTI_REGISTRAZIONE)   // una riga per impianto, prima la videosorveglianza (03/11/2026)
        return 'Videosorveglianza: ' + (C.VIDEO_REGISTRAZIONE ? reg : noReg) + '\nTelecamera piscina: ' + (C.PISCINA_REGISTRAZIONE ? reg : noReg);
      return C.QUALSIASI_REGISTRAZIONE ? 'Massimo ' + dur + ' ore, come da Circolare INL n.5 del 19 Febbraio 2018.' : 'Le immagini non sono conservate: è prevista la sola visione in tempo reale.';
    }
    // DPIA, impianti in modalità diverse (03/11/2026): il campo resta vuoto quando la condizione è falsa
    const nomeImp = video => video ? 'videosorveglianza' : 'telecamera piscina';
    if (t === 'DPIA_CONSERVAZIONE_IMPIANTO') return C.IMPIANTI_MISTI_REGISTRAZIONE ? ' della ' + nomeImp(C.VIDEO_REGISTRAZIONE) : '';
    if (t === 'DPIA_IMPIANTO_SOLA_VISIONE') return C.IMPIANTI_MISTI_REGISTRAZIONE ? ' ' + nomeImp(!C.VIDEO_REGISTRAZIONE) : ' telecamera piscina';
    if (t === 'DPIA_VISIONE_IMPIANTO') return C.IMPIANTI_MISTI_VISIONE ? ' della ' + nomeImp(C.VIDEO_VISIONE) : '';
    if (t === 'BACKUP_INTRO') return C.NESSUN_BACKUP ? voce(/non effettua/) : voce(/Al fine di garantire/);
    if (/^CONSENSO_/.test(t)) {   // contratto: consensi dell'informativa (vuoto = li sceglie il cliente)
      const x = ((cl.contratto || {}).consensi || {})[t.slice(9)];
      return x === 'SI' ? voce(/\[X\] SI/) : x === 'NO' ? voce(/\[X\] NO/) : voce(/^\[\s+\] SI\s+\[\s+\] NO$/);
    }
    if (t === 'PAGAMENTO_BONIFICO' || t === 'PAGAMENTO_RIBA') return ((cl.contratto || {}).pagamento === t.slice(10)) ? voce(/X/) : voce(/^\[\s*\]$/);
    if (t === 'NESSUNA_VIDEOSORVEGLIANZA') return C.VIDEO_O_PISCINA ? ' ' : voce(/NON è dotata/);
    return undefined;
  }

  /* ---------- passi generici ---------- */
  function ripetiSedi(doc, cl) {
    const capCom = s => [s.cap, s.comune].filter(Boolean).join(' ') + (s.prov ? ' (' + s.prov + ')' : '');
    for (const s of sdts(doc, t => t === 'RIPETI:SEDI_TRATTAMENTO')) {
      const riga = isW(content(s).firstElementChild, 'tr');
      cl.sedi.forEach((sede, k) => {
        const cp = clona(s);
        fill(cp, { SEDE_TRATTAMENTO_INDIRIZZO: sede.indirizzo, SEDE_TRATTAMENTO_CAP_COMUNE: capCom(sede), SEDE_TRATTAMENTO_NOME: sede.nome || '' });
        if (riga && k) { const tc = all(cp, 'tc')[0]; if (tc && !all(tc, 'sdt').length) for (const t of all(tc, 't')) t.textContent = ''; }
        if (!riga && cl.sedi.length > 1 && sede.nome) {   // più sedi: anche il nome dell'attività (decisione 27/10/2026)
          const r0 = all(s, 'r')[0]; const rn = r0.cloneNode(true);
          for (const ch of [...rn.children]) if (!isW(ch, 'rPr')) rn.removeChild(ch);
          const t = mk(doc, 't'); t.textContent = sede.nome + ' – '; t.setAttribute('xml:space', 'preserve'); rn.appendChild(t);
          content(cp).insertBefore(rn, content(cp).firstChild);
        }
        if (!riga && k) {
          const r0 = all(s, 'r')[0]; const sep = r0.cloneNode(true);
          for (const ch of [...sep.children]) if (!isW(ch, 'rPr')) sep.removeChild(ch);
          const t = mk(doc, 't'); t.textContent = '; '; t.setAttribute('xml:space', 'preserve'); sep.appendChild(t);
          s.parentNode.insertBefore(sep, s);
        }
        s.parentNode.insertBefore(cp, s); unwrap(cp);
      });
      remove(s);
    }
  }
  function ripetiVisione(doc, cl, C) {
    const vis = liste(cl, C).FUNZIONI_VISIONE_DIRETTA;
    for (const s of sdts(doc, t => t === 'RIPETI:FUNZIONI_VISIONE_DIRETTA')) {
      for (const f of vis) { const cp = clona(s); fill(cp, { FUNZIONE_VISIONE: f }); s.parentNode.insertBefore(cp, s); unwrap(cp); }
      remove(s);
    }
  }
  function applicaListe(doc, L) {
    const gruppi = {};
    for (const s of sdts(doc, t => t.startsWith('LISTA:'))) {
      if (!s.isConnected) continue;
      const k = tag(s).slice(6); let vals = typeof L[k] === 'function' ? L[k](s) : L[k];
      if (!vals) continue;
      const voci = listItems(s).filter(i => i.trim());
      if (voci.length && voci.every(i => /^-\s/.test(i))) vals = vals.map(v => /^-\s/.test(v) ? v : '- ' + v);
      if (typeof L[k] === 'function' && !vals.length) { let tr = s; while (tr && !isW(tr, 'tr')) tr = tr.parentNode; if (tr) { remove(tr); continue; } }
      if (typeof L[k] === 'function') { s.__vals = vals; }
      const first = content(s).firstElementChild;
      if (isW(first, 'tbl')) {   // tabella con una riga per voce
        const tbl = first; const righe = kids(tbl, 'tr');
        const proto = righe[0]; const dopo = righe[righe.length - 1].nextSibling;
        const nuove = vals.map(v => { const r = clona(proto); for (const x of all(r, 'sdt')) if (tag(x).startsWith('CAMPO:')) setText(x, v); return r; });
        righe.forEach(remove);
        nuove.forEach(r => tbl.insertBefore(r, dopo && dopo.parentNode === tbl ? dopo : null));
        if (!all(tbl, 'tr').length) { const r = clona(proto); for (const x of all(r, 'sdt')) if (tag(x).startsWith('CAMPO:')) setText(x, ' '); tbl.appendChild(r); }
        unwrap(s); continue;
      }
      let unit = s;
      if (isW(first, 'r') || isW(first, 'sdt')) {
        const p = s.parentNode;
        if (isW(p, 'p') && testo(p).trim() === testo(s).trim()) unit = p;
        else { setText(s, vals.join(', ')); continue; }
      }
      const gk = typeof L[k] === 'function' ? k + '#' + (idSeq++) : k;
      unit.__vals = vals; (gruppi[gk] = gruppi[gk] || []).push(unit);
    }
    for (const k in gruppi) {
      // unità consecutive = un solo elenco
      const blocchi = []; let cur = null;
      for (const u of gruppi[k]) {
        let prev = u.previousElementSibling;
        while (prev && /^(bookmarkStart|bookmarkEnd|proofErr|permStart|permEnd)$/.test(prev.localName)) prev = prev.previousElementSibling;
        if (cur && prev === cur[cur.length - 1]) cur.push(u); else { cur = [u]; blocchi.push(cur); }
      }
      for (const b of blocchi) {
        const proto = b[0]; const kk = k.split('#')[0];
        for (const v of proto.__vals) {
          const cp = clona(proto);
          const sd = isW(cp, 'sdt') ? cp : all(cp, 'sdt').find(x => tag(x) === 'LISTA:' + kk);
          setText(sd, v);
          proto.parentNode.insertBefore(cp, proto);
        }
        b.forEach(remove);
      }
    }
  }
  function applicaCondizioni(root, C, avvisi, salta) {
    for (const s of sdts(root, t => t.startsWith('SE:'))) {
      if (!s.isConnected && !root.contains(s)) continue;
      if (!root.contains(s)) continue;
      const c = tag(s).slice(3);
      if (salta && salta(c)) continue;
      if (cond(c, C, avvisi)) unwrap(s); else togliParte(s);
    }
  }
  function applicaTesti(doc, cl, C) {
    const pEn = all(doc, 'p').find(p => testo(p).startsWith('INFORMATION ON THE PROCESSING'));
    for (const s of sdts(doc, t => t.startsWith('TESTO:'))) {
      if (!s.isConnected) continue;
      const en = !!pEn && !!(pEn.compareDocumentPosition(s) & 4);
      const v = testoAlt(tag(s).slice(6), s, cl, C, en);
      if (v !== undefined) setText(s, v);
    }
  }
  function applicaFirme(doc, firme, ctx) {
    for (const s of sdts(doc, t => t.startsWith('FIRMA:'))) {
      const chi = tag(s).slice(6) === 'CONSULENTE' ? 'consulente' : 'lr';
      const img = firme[chi];
      const c = content(s);
      if (!img) { if (chi === 'consulente') { for (const r of all(c, 'r')) for (const ch of [...r.children]) if (!isW(ch, 'rPr')) r.removeChild(ch); } continue; }
      const r = runFirma(doc, img, ctx);
      const r0 = all(c, 'r')[0];
      if (r0) { const rpr = kid(r0, 'rPr'); if (rpr) r.insertBefore(rpr.cloneNode(true), r.firstChild); all(c, 'r').forEach(remove); }
      const p = isW(c.firstElementChild, 'p') ? c.firstElementChild : c;
      p.appendChild(r);
    }
  }
  function runFirma(doc, img, ctx, maxCy) {
      const rId = ctx.aggiungiImmagine(img);
      let cx = 1440000, cy = Math.round(cx * img.h / img.w);
      if (maxCy && cy > maxCy) { cx = Math.round(cx * maxCy / cy); cy = maxCy; }   // firma più bassa dove lo spazio è contato
      const xml = '<w:r ' + NSDECL + '><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="' + cx + '" cy="' + cy + '"/>' +
        '<wp:docPr id="' + (7000 + idSeq++) + '" name="Firma"/><wp:cNvGraphicFramePr/><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">' +
        '<pic:pic><pic:nvPicPr><pic:cNvPr id="0" name="firma.png"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="' + rId + '"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>' +
        '<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="' + cx + '" cy="' + cy + '"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>';
      return doc.importNode(new DOMParser().parseFromString(xml, 'application/xml').documentElement, true);
  }
  // firma del Legale Rappresentante (la stessa del Registro) negli altri documenti che la richiedono (decisione 27/10/2026)
  function firmaLR(code, doc, cl, firme, ctx) {
    const img = firme.lr; if (!img) return;
    if (/^RESP_/.test(code) && code !== 'RESP_BACKUP_CREDENZIALI' || code === 'IMPEGNO_DITTA_PIANI') {
      // tabella delle firme: colonna «IL TITOLARE DEL TRATTAMENTO», casella con la sola linea
      for (const tbl of all(doc, 'tbl')) {
        const righe = kids(tbl, 'tr'); let col = -1;
        const celle = tr => { let x = 0; return kids(tr, 'tc').map(tc => { const pr = kid(tc, 'tcPr'), gs = pr && kid(pr, 'gridSpan'); const o = { tc, da: x }; x += gs ? Number(wa(gs, 'val')) : 1; return o; }); };
        for (const tr of righe) for (const c of celle(tr)) if (/^(PER )?IL TITOLARE DEL TRATTAMENTO$/i.test(testo(c.tc).trim())) col = c.da;
        if (col < 0) continue;
        for (const tr of righe) for (const c of celle(tr)) {
          if (c.da < col) continue;
          for (const p of kids(c.tc, 'p')) if (/^_{8,}$/.test(testo(p).trim())) { const lin = testo(p).trim(); sostituisci(p, lin, ''); p.appendChild(runFirma(doc, img, ctx)); }
        }
      }
    }
    const inPar = (re, lineaRe) => { for (const p of paragrafi(doc, re)) { const m = lineaRe.exec(testo(p)); if (m) { sostituisci(p, m[0], ' '); p.appendChild(runFirma(doc, img, ctx)); } } };
    if (code === 'DPIA_VIDEO') inPar(/^Il Titolare del trattamento \(firma\):/, /_{6,}/);
    if (code === 'PROC_DATA_BREACH') {
      // presa visione del Legale Rappresentante: la prima riga «Firma dell’autorizzato…» dopo la sua tabella
      const ps = all(doc, 'p'); let dopoLR = false;
      for (const p of ps) {
        const t = testo(p).trim();
        if (/^Legale Rappresentante$/.test(t)) dopoLR = true;
        if (/^Sostituto del Legale Rappresentante$/.test(t)) dopoLR = false;
        if (dopoLR && /^Firma dell.autorizzato per presa visione/i.test(t)) { const m = /_{6,}/.exec(testo(p)); if (m) { sostituisci(p, m[0], ' '); p.appendChild(runFirma(doc, img, ctx)); } break; }
      }
    }
    if (code === 'FORMAZIONE') {
      for (const p of paragrafi(doc, /^Firma del Titolare o del Legale Rappresentante/)) {
        // la linea del Titolare è la prima: la firma va lì, la linea del docente resta
        const ts = all(p, 't'); const t = ts.find(t => /_{6,}/.test(t.textContent));
        if (!t) continue;
        const m = /_{6,}/.exec(t.textContent); const prima = t.textContent.slice(0, m.index), dopo = t.textContent.slice(m.index + m[0].length);
        const r = t.parentNode; t.textContent = prima; t.setAttribute('xml:space', 'preserve');
        const rf = runFirma(doc, img, ctx); const rpr = kid(r, 'rPr'); if (rpr) rf.insertBefore(rpr.cloneNode(true), rf.firstChild);
        r.parentNode.insertBefore(rf, r.nextSibling);
        const r2 = r.cloneNode(true); for (const x of all(r2, 't').slice(1)) remove(x); const t2 = all(r2, 't')[0]; if (t2) { t2.textContent = dopo; t2.setAttribute('xml:space', 'preserve'); }
        rf.parentNode.insertBefore(r2, rf.nextSibling);
      }
    }
    if (code === 'NOMINE')   // nomina del Legale Rappresentante: firma per presa visione (decisione 27/10/2026)
      for (const L of sdts(doc, t => t === 'LETTERA:LR'))
        for (const p of all(L, 'p').filter(p => /^Firma dell.Autorizzato per presa visione/.test(testo(p)))) { const m = /_{6,}/.exec(testo(p)); if (m) { sostituisci(p, m[0], ' '); p.appendChild(runFirma(doc, img, ctx, 230000)); } }
    if (code === 'RESP_BACKUP_CREDENZIALI') {
      const ps = paragrafi(doc, /^Firma dell.Autorizzato per presa visione/);
      [cl.incaricoBackup, cl.incaricoCredenziali].forEach((inc, i) => {
        if (inc !== 'LR' || !ps[i]) return;
        const m = /_{6,}/.exec(testo(ps[i])); if (m) { sostituisci(ps[i], m[0], ' '); ps[i].appendChild(runFirma(doc, img, ctx)); }
      });
    }
  }
  function pulisciControlli(doc) {
    for (const s of sdts(doc, t => /^(SE|RIPETI|SEZIONE|SPECIALE|LISTA|LETTERA):/.test(t) || t === 'LETTERA')) if (s.isConnected) unwrap(s);
    for (const s of sdts(doc, t => t.startsWith('CAMPO:_'))) setTag(s, 'CAMPO:' + tag(s).slice(7));
  }

  /* ---------- documenti con struttura propria ---------- */
  function accessoVideo(funz, cl, C) {
    const sc = (cl.videoAccesso || {})[funz];
    if (sc !== undefined && sc !== '') return sc === '—' ? null : sc;
    const L = liste(cl, C);
    const v = L.FUNZIONI_VISIONE_DIRETTA.includes(funz), r = C.QUALSIASI_REGISTRAZIONE && L.FUNZIONI_CONSULTAZIONE_REGISTRAZIONI.includes(funz);
    if (v && r) return 'visione in tempo reale e consultazione delle registrazioni.';
    if (v) return C.QUALSIASI_REGISTRAZIONE ? 'solo visione in tempo reale; consultazione delle registrazioni non consentita.' : 'solo visione in tempo reale.';
    if (r) return 'solo consultazione delle registrazioni.';
    return null;
  }
  const chiave = n => n.toUpperCase().replace(/[^A-Z]+/g, '_').replace(/^_|_$/g, '');
  const LIVELLI = [null, { '16': '15', '14': '13' }, { '16': '14', '14': '13' }, { '16': '14', '14': '13', riga: '228' }];
  function compatta(lettera, lv) {
    const L = LIVELLI[lv]; if (!L) return;
    for (const sz of [...all(lettera, 'sz'), ...all(lettera, 'szCs')]) { const o = wa(sz, 'val'); if (L[o]) setWa(sz, 'val', L[o]); }
    if (L.riga) for (const p of all(lettera, 'p')) {
      let ppr = kid(p, 'pPr'); if (!ppr) continue;
      let sp = kid(ppr, 'spacing'); if (!sp) { sp = mk(lettera.ownerDocument, 'spacing'); ppr.appendChild(sp); }
      setWa(sp, 'line', L.riga); setWa(sp, 'lineRule', 'auto');
    }
  }
  function nomine(doc, cl, C, avvisi, log) {
    const body = kid(doc.documentElement, 'body');
    const modelli = {}, ordine = [];
    for (const s of sdts(body, t => t.startsWith('RIPETI:AUTORIZZATI_'))) { const k = tag(s).slice(19); modelli[k] = s.cloneNode(true); ordine.push(k); }
    const sezioni = {};
    for (const k in modelli) sezioni[k] = sdts(modelli[k], t => t === 'SEZIONE:AMBITO_' + k)[0];
    for (const s of kids(body, 'sdt')) remove(s);
    const sect = kid(body, 'sectPr');
    const lista = persone(cl);
    for (const { p, fz } of lista) {
      const cp = clona(modelli[chiave(fz[0])]);
      let ultima = sdts(cp, t => t === 'SEZIONE:AMBITO_' + chiave(fz[0]))[0];
      for (const f of fz.slice(1)) { const ex = sezioni[chiave(f)].cloneNode(true); ultima.parentNode.insertBefore(ex, ultima.nextSibling); ultima = ex; }
      rinumera(cp, 900 + (bmSeq++));
      body.insertBefore(cp, sect);
      for (const x of sdts(cp, t => t.startsWith('SEZIONE:AMBITO_'))) {
        const k = tag(x).slice(15); const f = fz.find(f => chiave(f) === k);
        for (const s of sdts(x, t => t === 'TESTO:VIDEO_ACCESSO')) setText(s, accessoVideo(f, cl, C) || '');
        applicaCondizioni(x, Object.assign({}, C, { VIDEO_ACCESSO_FUNZIONE: C.VIDEO_O_PISCINA && accessoVideo(f, cl, C) != null }), avvisi);
        unwrap(x);
      }
      const nom = nominativo(p);
      fill(cp, { AUTORIZZATO_NOME: nom ? p.nome || '' : '', AUTORIZZATO_COGNOME: nom ? p.cognome || '' : '', AUTORIZZATO_QUALIFICA: p.qualifica });
      const cf = sdts(cp, t => t === 'CAMPO:AUTORIZZATO_FUNZIONE');
      if (cf[0]) setText(cf[0], fz.join(', '));
      if (cf[1]) setText(cf[1], fz.join('\n'));
      cf.forEach(s => setTag(s, 'CAMPO:_AUTORIZZATO_FUNZIONE'));
      // una pagina tassativa: nel browser non si può impaginare, si compatta in base al numero di funzioni (margine di sicurezza)
      let lv = fz.length >= 3 ? 3 : fz.length === 2 ? 2 : 0;
      if (p.qualifica === 'Legale Rappresentante' && lv < 3) lv++;   // spazio per la firma del LR (27/10/2026)
      compatta(cp, lv);
      if (fz.length >= 4) avvisi.push('Nomine: la lettera con ' + fz.length + ' funzioni (' + fz.join(', ') + ') potrebbe superare una pagina: controllarla in Word.');
      if (lv) log.push('Nomine: lettera «' + fz.join(', ') + '» compattata al livello ' + lv + '.');
      const cc = content(cp); let ultimo = cc.lastElementChild;
      while (ultimo && vuoto(ultimo) && ultimo.previousElementSibling) { const prev = ultimo.previousElementSibling; remove(ultimo); ultimo = prev; }
      setTag(cp, p.qualifica === 'Legale Rappresentante' ? 'LETTERA:LR' : 'LETTERA:');
    }
    log.push('Nomine: ' + lista.length + ' lettere.');
  }
  function tabellaAutorizzati(doc, cl) {
    for (const s of sdts(doc, t => t === 'RIPETI:AUTORIZZATI')) {
      for (const { p, fz } of persone(cl)) {
        const cp = clona(s); const nom = nominativo(p);
        fill(cp, { AUTORIZZATO_NOME: nom ? p.nome || '' : '', AUTORIZZATO_COGNOME: nom ? p.cognome || '' : '', AUTORIZZATO_FUNZIONE: fz.join('\n'), AUTORIZZATO_QUALIFICA: p.qualifica });
        s.parentNode.insertBefore(cp, s); unwrap(cp);
      }
      remove(s);
    }
  }
  function lettereRipetute(doc, tagR, lettere, C, avvisi, nuovaPagina) {
    for (const mod of sdts(doc, t => t === tagR)) {
      lettere.forEach((L, n) => {
        const cp = clona(mod);
        fillPos(cp, L || {});
        applicaCondizioni(cp, C, avvisi, c => c === 'RIGA_COMPILATA');
        for (const s of sdts(cp, t => t === 'SE:RIGA_COMPILATA')) {
          const vuota = sdts(s, t => t.startsWith('CAMPO:RESP_TRATTAMENTO')).every(x => !testo(x).trim());
          if (vuota) remove(s); else unwrap(s);
        }
        if (nuovaPagina && n) {
          const p = all(cp, 'p')[0]; let ppr = kid(p, 'pPr');
          if (!ppr) { ppr = mk(doc, 'pPr'); p.insertBefore(ppr, p.firstChild); }
          if (!kid(ppr, 'pageBreakBefore')) { const pb = mk(doc, 'pageBreakBefore'); const ps = kid(ppr, 'pStyle'); ppr.insertBefore(pb, ps ? ps.nextSibling : ppr.firstChild); }
        }
        mod.parentNode.insertBefore(cp, mod); unwrap(cp);
      });
      remove(mod);
    }
  }
  // riga «BACK UP» dell'archivio informatico: se nella check list c'è un backup, riporta quello (03/11/2026);
  // resta la scelta del consulente solo se ha indicato un tipo diverso da «Nessuno»
  function backupArchivio(a, cl, log) {
    const tipi = cl.backup.tipi || [], k = 'ARCHIVIO_INFORMATICO_BACK_UP_1#1', v = String(a[k] || '');
    if (!tipi.length) return a;
    const kf = 'ARCHIVIO_INFORMATICO_FREQUENZA_BACK_UP_1#1', vf = String(a[kf] || '').trim(), fr = String(cl.backup.frequenza || '').trim();
    const extra = (!vf || /^Nessuna$/i.test(vf)) && fr ? { [kf]: fr.charAt(0).toUpperCase() + fr.slice(1) } : {};
    if (v.trim() && !/^Nessuno/i.test(v.trim())) return Object.assign({}, a, extra);
    const fornitore = String(cl.backup.cloudFornitore || '').split(/\s+-\s+/)[0].replace(/\.$/, '').trim();
    const voci = tipi.map(t => t === 'SERVER' ? 'Server' + (cl.backup.serverLuogo ? ' (' + String(cl.backup.serverLuogo).replace(/^-\s*/, '') + ')' : '')
      : t === 'DISPOSITIVI' ? (String(cl.backup.dispositivo || 'Dispositivo esterno').replace(/^-\s*/, ''))
      : 'Cloud' + (fornitore ? ' (' + fornitore + ')' : ''));
    log.push('Registro: riga BACK UP degli archivi informatici dal backup della check list (' + voci.join('; ') + ').');
    return Object.assign({}, a, { [k]: voci.join('; ') }, extra);
  }
  function registro(doc, cl, C, avvisi, log, ctx) {
    // archivi: una tabella per archivio, numerate
    for (const [tg, arr] of [['RIPETI:ARCHIVIO_CARTACEO', cl.archivi.cartacei], ['RIPETI:ARCHIVIO_INFORMATICO', cl.archivi.informatici]]) {
      for (const s of sdts(doc, t => t === tg)) {
        arr.forEach((a, k) => {
          const cp = clona(s);
          const num = tg.endsWith('CARTACEO') ? 'ARCHIVIO_CARTACEO_NUMERO' : 'ARCHIVIO_INFORMATICO_NUMERO';
          fillPos(cp, tg.endsWith('INFORMATICO') ? backupArchivio(a, cl, log) : a); fill(cp, { [num]: String(k + 1) });
          if (k) {
            const sep = mk(doc, 'p');
            if (tg.endsWith('INFORMATICO')) {   // una pagina per ogni archivio informatico (27/10/2026)
              const r = mk(doc, 'r'), br = mk(doc, 'br'); setWa(br, 'type', 'page'); r.appendChild(br); sep.appendChild(r);
            }
            s.parentNode.insertBefore(sep, s);
          }
          s.parentNode.insertBefore(cp, s); unwrap(cp);
        });
        if (!arr.length) avvisi.push('Registro: nessun archivio ' + (tg.endsWith('CARTACEO') ? 'cartaceo' : 'informatico') + ' indicato, tabella tolta.');
        remove(s);
      }
    }
    // tabella revisioni: dati nella riga della revisione corrente
    for (const s of sdts(doc, t => t === 'SPECIALE:TABELLA_REVISIONI')) {
      const tbl = all(s, 'tbl')[0]; const righe = kids(tbl, 'tr');
      const celle = tr => [...tr.children].filter(c => isW(c, 'tc') || isW(c, 'sdt'));
      const r1 = righe[1]; const n = parseInt(cl.revisione, 10) || 0; const nn = String(n).padStart(2, '0');
      const target = righe.find(r => testo(celle(r)[0]).trim() === 'Rev. ' + nn);
      if (n > 0 && target && target !== r1) {
        const c1 = celle(r1), ct = celle(target);
        for (let i = 1; i < c1.length && i < ct.length; i++) target.replaceChild(c1[i].cloneNode(true), ct[i]);
        // riga Rev. 00: resta il numero, data e firme da compilare dallo storico
        for (const x of all(r1, 'sdt')) {
          const t = tag(x);
          if (t === 'CAMPO:REVISIONE_NUMERO') { setText(x, 'Rev. 00'); setTag(x, 'CAMPO:_REVISIONE_NUMERO'); }
          else if (t === 'CAMPO:REVISIONE_DATA') { setText(x, ''); setTag(x, 'CAMPO:_REVISIONE_DATA'); }
          else if (t.startsWith('FIRMA:')) { for (const r of all(x, 'r')) for (const ch of [...r.children]) if (!isW(ch, 'rPr')) r.removeChild(ch); setTag(x, 'CAMPO:_FIRMA'); }
        }
        avvisi.push('Registro: tabella revisioni compilata sulla riga Rev. ' + nn + '; data e firme delle revisioni precedenti vanno riportate dallo storico.');
      } else if (n > 0 && !target) avvisi.push('Registro: nella tabella revisioni non c\'è la riga Rev. ' + nn + ': compilata la prima riga.');
    }
    // organigramma ricostruito al posto dello SmartArt
    for (const s of sdts(doc, t => t === 'SPECIALE:ORGANIGRAMMA')) {
      let e = s.nextElementSibling, run = null;
      for (let i = 0; i < 4 && e && !run; i++, e = e.nextElementSibling)
        for (const gd of all(e, 'graphicData', 'http://schemas.openxmlformats.org/drawingml/2006/main'))
          if (/diagram/.test(gd.getAttribute('uri'))) { run = gd; while (run && !isW(run, 'r')) run = run.parentNode; }
      if (!run) { avvisi.push('Registro: organigramma del modello non trovato.'); continue; }
      const nuovo = doc.importNode(new DOMParser().parseFromString(organigramma(cl, C), 'application/xml').documentElement, true);
      let pa = run; while (pa && !isW(pa, 'p')) pa = pa.parentNode;
      run.parentNode.replaceChild(nuovo, run);
      // le righe vuote sotto lo SmartArt (che era «flottante») farebbero scivolare la pagina: si tolgono
      let nx = pa && pa.nextElementSibling;
      while (nx && vuoto(nx)) { const n2 = nx.nextElementSibling; remove(nx); nx = n2; }
      log.push('Registro: organigramma ricostruito dalle funzioni presenti.');
    }
    ctx.aggiornaCampi = true;
  }

  /* ---------- rifiniture per documento (richieste del 27/10/2026) ---------- */
  const luogoFrase = l => { l = String(l || '').replace(/^-\s*/, '').trim(); const m = /^ufficio\s+(.*)$/i.exec(l); return m ? 'nell\'ufficio ' + m[1] : 'presso ' + l; };
  function rifiniture(code, doc, cl, C, avvisi, log) {
    const cloud = C.QUALSIASI_REGISTRAZIONE && cl.video.archiviazione === 'CLOUD';
    const dur = Number(cl.durataOre);
    if (code === 'REGISTRO') {
      // RLS: ora è una voce dell'elenco di chi consulta le registrazioni, la riga fissa del modello si toglie
      paragrafi(doc, /^\s*-\s*Rappresentante dei lavoratori per la sicurezza \(RLS\)/).forEach(remove);
      for (const p of paragrafi(doc, /dotato di sistema Digital Video Recorder \(DVR\)/)) {
        sostituisci(p, '24h', dur + 'h');
        if (cloud) sostituisci(p, 'è dotato di sistema Digital Video Recorder (DVR) che consente la conservazione delle immagini', 'consente la conservazione delle immagini sul cloud del fornitore');
      }
      if (cloud) for (const p of paragrafi(doc, /^Il DVR di registrazione è conservato/)) sostituisci(p, testo(p), 'Le registrazioni sono conservate in:');
      // CRS e CRO: tabella tolta se il nome non è indicato; senza entrambi si toglie anche l'introduzione
      if (C.CRM_PRESENTE) {
        const senza = k => !String(cl.crm[k] || '').trim();
        const tabDi = k => all(doc, 'tbl').find(t => sdts(t, x => x === 'CAMPO:' + k).length);
        if (senza('CRS_NOME') && senza('CRO_NOME')) {
          const t = tabDi('CRS_NOME'); let blocco = t; while (blocco && !(isW(blocco, 'sdt') && /^SE:CRM_PRESENTE$/.test(tag(blocco)))) blocco = blocco.parentNode;
          if (blocco) remove(blocco); else { [tabDi('CRS_NOME'), tabDi('CRO_NOME')].forEach(x => x && remove(x)); }
        } else {
          if (senza('CRS_NOME')) { const t = tabDi('CRS_NOME'); if (t) remove(t); }
          if (senza('CRO_NOME')) { const t = tabDi('CRO_NOME'); if (t) remove(t); }
        }
      }
    }
    if (code === 'DPIA_VIDEO' && (C.IMPIANTI_MISTI_REGISTRAZIONE || C.IMPIANTI_MISTI_VISIONE))
      avvisi.push('DPIA con impianti in modalità diverse: rileggere conservazione e ciclo di vita prima della firma.');
    if (code === 'DPIA_VIDEO' && C.QUALSIASI_REGISTRAZIONE) {
      const luogo = cl.video.DVR_LUOGO;
      if (!cloud) for (const p of paragrafi(doc, /nell'ufficio Front Office/)) sostituisci(p, "nell'ufficio Front Office", luogoFrase(luogo));
      else {
        const forn = String(luogo || '').replace(/^-\s*/, '').trim() || 'cloud del fornitore dell’impianto';
        const cambi = [
          ["su Digital Video Recorder (DVR) - custodito in armadietto ignifugo chiuso a chiave nell'ufficio Front Office, la cui chiave è custodita dal Legale Rappresentante;", 'sul ' + forn.replace(/^Cloud/, 'cloud') + ', con accesso tramite credenziali personali;'],
          ['- Digital Video Recorder (DVR)', '- Cloud del fornitore dell’impianto (conservazione delle registrazioni)'],
          ['- Il Digital Video Recorder (DVR) è accessibile sia fisicamente, sia a livello informatico, unicamente al Legale Rappresentante e ', '- Le registrazioni conservate sul cloud del fornitore sono accessibili, tramite credenziali personali, unicamente al Legale Rappresentante e '],
          ["Il DVR di registrazione è inserito all'interno di un armadietto ignifugo, in acciaio e chiuso a chiave.", 'Le registrazioni non sono conservate su supporti fisici presso la struttura: sono archiviate sul cloud del fornitore dell’impianto, nominato Responsabile del trattamento.'],
          ["Il Digital Video Recorder (DVR) è custodito in un contenitore chiuso a chiave nell'ufficio Front Office; la chiave è custodita dal Legale Rappresentante, a uso esclusivo.", 'Presso la struttura non è presente un DVR: le registrazioni sono conservate sul cloud del fornitore dell’impianto.']
        ];
        let n = 0;
        for (const [da, a] of cambi) for (const p of paragrafi(doc, new RegExp(da.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))) if (sostituisci(p, da, a)) n++;
        log.push('DPIA: testi del DVR adattati alla conservazione su cloud (' + n + ' punti).');
        avvisi.push('DPIA: con le registrazioni su cloud alcune frasi sono state riscritte dal programma: rileggerle prima della firma.');
      }
    }
    if (code === 'PROC_DATA_BREACH') paragrafi(doc, /^Attenzione al calcolo dei tempi/).forEach(pageBreakBefore);
    if (code === 'DISCIPLINARE') {
      // tabella firme: larga quanto lo spazio di scrittura e centrata
      const tbl = all(doc, 'tbl').filter(t => sdts(t, x => /AUTORIZZATO_QUALIFICA/.test(x)).length).pop();
      const sp = kid(kid(doc.documentElement, 'body'), 'sectPr');
      if (tbl && sp) {
        const pg = kid(sp, 'pgSz'), mg = kid(sp, 'pgMar');
        const larg = Number(wa(pg, 'w')) - Number(wa(mg, 'left')) - Number(wa(mg, 'right')) - Number(wa(mg, 'gutter') || 0);
        const tp = kid(tbl, 'tblPr'), tw = kid(tp, 'tblW'); const att = Number(wa(tw, 'w'));
        if (att > larg) {
          const f = larg / att;
          setWa(tw, 'w', String(larg)); setWa(tw, 'type', 'dxa');
          for (const g of all(tbl, 'gridCol')) setWa(g, 'w', String(Math.floor(Number(wa(g, 'w')) * f)));
          for (const c of all(tbl, 'tcW')) if (wa(c, 'type') === 'dxa') setWa(c, 'w', String(Math.floor(Number(wa(c, 'w')) * f)));
        }
        let jc = kid(tp, 'jc'); if (!jc) { jc = mk(doc, 'jc'); tp.insertBefore(jc, kid(tp, 'tblCellSpacing') || kid(tp, 'tblInd') || kid(tp, 'tblBorders') || kid(tp, 'shd') || kid(tp, 'tblLayout') || kid(tp, 'tblCellMar') || kid(tp, 'tblLook') || null); }
        setWa(jc, 'val', 'center'); const ind = kid(tp, 'tblInd'); if (ind) remove(ind);
      }
    }
  }
  function rifinitureFinali(code, doc, cl, avvisi, log) {
    if (code === 'CARTELLO_VIDEO' || code === 'CARTELLO_PISCINA') return righeCartello(doc, log);
    if (code === 'INFORMATIVA_LAVORATORI') return unaPagina(doc, log);
    if (code === 'PROC_DATA_BREACH') return comeTabellaLR(doc, log);
    if (code !== 'REGISTRO') return;
    // «…ed agli Autorizzati facenti parte delle funzioni:» senza funzioni sotto: solo il Legale Rappresentante (03/11/2026)
    for (const p of paragrafi(doc, /Legale Rappresentante ed agli Autorizzati facenti parte delle funzioni:\s*$/)) {
      let nx = p.nextElementSibling; while (nx && vuoto(nx)) nx = nx.nextElementSibling;
      if (nx && /^\s*-\s/.test(testo(nx))) continue;
      sostituisci(p, ' ed agli Autorizzati facenti parte delle funzioni:', '.');
      sostituisci(p, 'consentita al Legale', 'consentita esclusivamente al Legale');
      avvisi.push('Registro: nessuna funzione indicata per l\'accesso alle immagini; la frase cita solo il Legale Rappresentante. Controlla la check list (passo 2 o 3).');
    }
    impaginaRegistro(doc, log);
    // spazi vuoti sopra il titolo «SITO WEB AZIENDALE»
    for (const p of paragrafi(doc, /^SITO WEB AZIENDALE$/)) {
      if (testo(p) !== testo(p).trim() && !/^SITO WEB AZIENDALE$/.test(testo(p).trim())) continue;
      let prev = p.previousElementSibling;
      while (prev && vuoto(prev)) { const pp = prev.previousElementSibling; remove(prev); prev = pp; }
      // il titolo apre una pagina nuova anche se sopra non c'è più l'interruzione
      if (!(prev && all(prev, 'br').some(b => wa(b, 'type') === 'page'))) pageBreakBefore(p);
    }
  }

  /* ---------- impaginazione del Registro (03/11/2026) ----------
     Via le pagine bianche del modello (servivano a far cadere le copertine sul fronte con il testo originale, ma con
     testi di lunghezza diversa finivano nei posti sbagliati). Ogni copertina di macro sezione diventa una sezione che
     comincia su pagina dispari; anche il testo che la segue riparte da pagina dispari: il retro della copertina resta
     bianco. Word (e LibreOffice) aggiungono da soli la pagina bianca solo quando serve. */
  const COPERTINA = /^(ANALISI DELLE FUNZIONI AZIENDALI|TITOLARE DEL TRATTAMENTO|AUTORIZZATI AL TRATTAMENTO|VALUTAZIONE DEI RISCHI|MISURE DI SICUREZZA GENERALI E STRUTTURALI|SISTEMI DI ARCHIVIAZIONE DATI|MISURE ORGANIZZATIVE E GESTIONALI|Funzione aziendale:.*Autorizzati al trattamento)$/;
  function impaginaRegistro(doc, log) {
    const body = all(doc, 'body')[0];
    const isBr = e => isW(e, 'p') && all(e, 'br').some(b => wa(b, 'type') === 'page') && !testo(e).trim() && !all(e, 'drawing').length && !all(e, 'sectPr').length;
    const vuotoSeg = e => vuoto(e) || (isW(e, 'p') && !testo(e).trim() && !all(e, 'drawing').length && !all(e, 'br').some(b => wa(b, 'type') === 'page') && !all(e, 'sectPr').length && !all(e, 'pict').length);
    const segmenti = () => {
      const out = []; let cur = { el: [], prima: null };
      for (const e of [...body.children]) {
        if (isW(e, 'sectPr')) continue;
        if (isBr(e)) { cur.dopo = e; out.push(cur); cur = { el: [], prima: e }; } else cur.el.push(e);
      }
      out.push(cur); return out;
    };
    const salvaSegnalibri = (e, dest) => { if (!dest) return; const ppr = kid(dest, 'pPr'); for (const b of [...all(e, 'bookmarkStart'), ...all(e, 'bookmarkEnd')]) dest.insertBefore(b, ppr ? ppr.nextSibling : dest.firstChild); };
    // 1) pagine bianche: segmenti senza contenuto tra due interruzioni di pagina
    let tolte = 0;
    for (const sg of segmenti()) {
      const soloSezione = e => isW(e, 'p') && all(e, 'sectPr').length && !testo(e).trim() && !all(e, 'drawing').length;
      if (!sg.prima || !sg.dopo || !sg.el.every(e => vuotoSeg(e) || soloSezione(e))) continue;
      let dest = sg.dopo.nextElementSibling; while (dest && !isW(dest, 'p')) dest = dest.nextElementSibling;
      for (const e of sg.el) { if (soloSezione(e)) continue; salvaSegnalibri(e, dest && isW(dest, 'p') ? dest : null); remove(e); }   // le interruzioni di sezione restano
      salvaSegnalibri(sg.dopo, dest && isW(dest, 'p') ? dest : null); remove(sg.dopo); tolte++;
    }
    // 2) copertine su pagina dispari, retro bianco
    const finale = kid(body, 'sectPr');
    // le sezioni del modello avevano formati e margini diversi tra loro: con un formato diverso un'interruzione «continua»
    // diventa una pagina nuova (e una pagina bianca se segue un'interruzione di pagina). Tutte come la prima sezione
    // (quella della copertina e della maggior parte del testo, su cui il modello è impaginato).
    const primaSez = all(body, 'sectPr')[0];
    for (const sp of all(body, 'sectPr')) {
      if (sp === primaSez) continue;
      for (const n of ['pgSz', 'pgMar']) { const a = kid(sp, n), b = kid(primaSez, n); if (a && b) sp.replaceChild(b.cloneNode(true), a); }
    }
    const reggente = e => { for (let x = e; x; x = x.nextElementSibling) { const sp = isW(x, 'p') ? all(x, 'sectPr')[0] : null; if (sp) return sp; } return finale; };
    const nuovaSezione = (rif, tipo) => {
      const sp = rif.cloneNode(true);
      for (const x of [...sp.children]) if (x.localName === 'type') remove(x);
      if (tipo) { const t = mk(doc, 'type'); setWa(t, 'val', tipo); const ref = [...sp.children].find(x => !/^(headerReference|footerReference|footnotePr|endnotePr)$/.test(x.localName)); sp.insertBefore(t, ref || null); }
      const p = mk(doc, 'p'), ppr = mk(doc, 'pPr'); ppr.appendChild(sp); p.appendChild(ppr); return p;
    };
    const dispari = sp => {
      for (const x of [...sp.children]) if (/^(type|titlePg)$/.test(x.localName)) remove(x);
      const t = mk(doc, 'type'); setWa(t, 'val', 'oddPage');
      const ref = [...sp.children].find(x => !/^(headerReference|footerReference|footnotePr|endnotePr)$/.test(x.localName));
      sp.insertBefore(t, ref || null);
    };
    let n = 0, ultimaFine = null;
    for (const sg of segmenti()) {
      const tx = sg.el.map(testo).join(' ').replace(/\s+/g, ' ').trim();
      if (!COPERTINA.test(tx) || !sg.el.length) continue;
      const primo = sg.el[0], ultimo = sg.el[sg.el.length - 1];
      if (sg.prima) remove(sg.prima);
      if (sg.dopo) remove(sg.dopo);
      // fine della sezione precedente (se tra le due copertine c'è del testo)
      let prev = primo.previousElementSibling; while (prev && vuotoSeg(prev) && prev !== ultimaFine) prev = prev.previousElementSibling;
      if (prev && prev !== ultimaFine) {
        const rif = reggente(primo); const tipo = all(rif, 'type')[0]; const pb = nuovaSezione(rif, tipo ? wa(tipo, 'val') : null);
        body.insertBefore(pb, primo);
      }
      // fine della copertina: la sezione della copertina parte da pagina dispari
      const fine = nuovaSezione(reggente(ultimo), 'oddPage');
      body.insertBefore(fine, ultimo.nextSibling);
      // il testo dopo la copertina riparte da pagina dispari (retro bianco)
      const dopo = reggente(fine.nextElementSibling);
      if (dopo !== finale || fine.nextElementSibling) dispari(dopo);
      ultimaFine = fine; n++;
    }
    // 3) interruzioni di pagina rimaste: diventano «nuova pagina prima» del paragrafo seguente, così una pagina piena
    //    fino in fondo non lascia dietro una pagina bianca con la sola interruzione
    let conv = 0;
    for (const e of [...body.children]) {
      if (!isBr(e)) continue;
      const pv = e.previousElementSibling;   // una tabella «mobile» è ancorata al paragrafo che la segue: le si dà un paragrafo
      if (isW(pv, 'tbl') && all(pv, 'tblpPr').length) {   // proprio sulla stessa pagina, prima della nuova pagina
        const a = mk(doc, 'p'), ppr = mk(doc, 'pPr'), sp = mk(doc, 'spacing');
        setWa(sp, 'before', '0'); setWa(sp, 'after', '0'); setWa(sp, 'line', '20'); setWa(sp, 'lineRule', 'exact'); ppr.appendChild(sp); a.appendChild(ppr);
        a.__ancora = true; body.insertBefore(a, e);
      }
      let nx = e.nextElementSibling; while (nx && isW(nx, 'bookmarkStart')) nx = nx.nextElementSibling;
      let target = null;
      if (isW(nx, 'p') && !all(nx, 'sectPr').length) target = nx;
      else if (isW(nx, 'tbl')) target = all(nx, 'p')[0] || null;
      if (!target) continue;
      salvaSegnalibri(e, target); pageBreakBefore(target); remove(e); conv++;
    }
    //    righe vuote prima di una nuova pagina: tolte (finivano da sole su una pagina se il testo sopra riempiva la pagina)
    for (const pb of all(body, 'pageBreakBefore')) {
      let top = pb; while (top.parentNode !== body) top = top.parentNode;
      let pr = top.previousElementSibling;
      while (pr && vuoto(pr) && !pr.__ancora && !all(pr, 'bookmarkStart').length) {
        const x = pr.previousElementSibling;
        if (isW(x, 'tbl') && all(x, 'tblpPr').length) break;   // ancoraggio di una tabella «mobile»: resta
        remove(pr); pr = x;
      }
      if (pr && isW(pr, 'tbl') && isW(top, 'tbl')) {   // due tabelle attaccate si fonderebbero: paragrafo minimo tra le due
        const p = mk(doc, 'p'), ppr = mk(doc, 'pPr'), sp = mk(doc, 'spacing');
        setWa(sp, 'before', '0'); setWa(sp, 'after', '0'); setWa(sp, 'line', '20'); setWa(sp, 'lineRule', 'exact'); ppr.appendChild(sp); p.appendChild(ppr);
        body.insertBefore(p, top);
      }
    }
    // intestazione e piè di pagina «normali» scritti in ogni sezione (non solo ereditati: con le sezioni nuove alcuni
    // programmi li perdevano); la prima sezione (copertina del documento) resta com'è
    const sps = all(body, 'sectPr');
    const refs = [];
    for (const sp of sps) for (const r of [...kids(sp, 'headerReference'), ...kids(sp, 'footerReference')]) if (wa(r, 'type') === 'default' && !refs.some(x => x.localName === r.localName)) refs.push(r);
    for (const sp of sps.slice(1)) {
      // solo la prima sezione (copertina e pagine iniziali) ha la prima pagina diversa
      for (const x of [...sp.children]) if (x.localName === 'titlePg' || (/^(header|footer)Reference$/.test(x.localName) && wa(x, 'type') !== 'default')) remove(x);
      for (const r of refs) if (!kids(sp, r.localName).some(x => wa(x, 'type') === 'default')) sp.insertBefore(r.cloneNode(true), sp.firstChild);
    }
    log.push('Registro: ' + tolte + ' pagine bianche tolte; ' + n + ' copertine di sezione su pagina dispari con retro bianco.');
  }

  /* ---------- «Elenco destinatari»: ogni elenco numerato riparte da 1 (03/11/2026) ----------
     In Word gli elenchi che usano la stessa definizione continuano la numerazione: ogni tabella riceve una propria
     istanza (w:num) con «riparti da 1» (startOverride) su tutti i livelli. */
  function riavviaElenchi(doc, numDoc, log) {
    const nr = numDoc.documentElement;
    let max = Math.max(0, ...kids(nr, 'num').map(x => Number(wa(x, 'numId')) || 0));
    const astratto = id => { const x = kids(nr, 'num').find(e => wa(e, 'numId') === id); const a = x && kid(x, 'abstractNumId'); return a ? wa(a, 'val') : null; };
    let n = 0;
    for (const tbl of all(doc, 'tbl')) {
      if (!/ELENCO DESTINATARI/.test(testo(tbl))) continue;
      const mappa = {};
      for (const np of all(tbl, 'numPr')) {
        const id = kid(np, 'numId'); if (!id) continue; const v = wa(id, 'val'); if (v === '0') continue;
        if (!mappa[v]) {
          const abs = astratto(v); if (abs == null) continue;
          const num = numDoc.createElementNS(W, 'w:num'); setWa(num, 'numId', String(++max));
          const a = numDoc.createElementNS(W, 'w:abstractNumId'); setWa(a, 'val', abs); num.appendChild(a);
          for (let lv = 0; lv < 9; lv++) {
            const o = numDoc.createElementNS(W, 'w:lvlOverride'); setWa(o, 'ilvl', String(lv));
            const st = numDoc.createElementNS(W, 'w:startOverride'); setWa(st, 'val', '1'); o.appendChild(st); num.appendChild(o);
          }
          const ultimoNum = kids(nr, 'num').pop(); nr.insertBefore(num, ultimoNum ? ultimoNum.nextSibling : null); mappa[v] = String(max);
        }
        setWa(id, 'val', mappa[v]);
      }
      n++;
    }
    log.push('Registro: ' + n + ' elenchi destinatari numerati da 1.');
  }

  /* ---------- cartelli: dati del Titolare su una riga (03/11/2026) ---------- */
  function righeCartello(doc, log) {
    for (const tbl of all(doc, 'tbl')) {
      if (!/TITOLARE DEL TRATTAMENTO/.test(testo(tbl))) continue;
      const tr = all(tbl, 'tr')[0]; const tcs = kids(tr, 'tc'); if (tcs.length < 2) continue;
      const w = Number(wa(all(tcs[1], 'tcW')[0], 'w')) - 220;   // larghezza utile in twip
      for (const p of kids(tcs[1], 'p')) {
        const lung = testo(p).length; if (!lung) continue;
        let sz = Math.max(...all(p, 'sz').map(x => Number(wa(x, 'val'))).filter(Boolean), 20);
        // stima: un carattere Montserrat ≈ 0,6 × corpo (mezzi punti → twip: × 10 × 0,6)
        let nuovo = sz; while (nuovo > 14 && lung * nuovo * 10 * 0.6 > w) nuovo--;
        if (nuovo < sz) { for (const x of [...all(p, 'sz'), ...all(p, 'szCs')]) setWa(x, 'val', String(nuovo)); log.push('Cartello: riga «' + testo(p).slice(0, 30) + '…» ridotta a ' + nuovo / 2 + ' pt per stare su una riga.'); }
      }
    }
  }
  /* ---------- data breach: tabella del sostituto con la stessa formattazione della tabella del LR (03/11/2026) ---------- */
  function comeTabellaLR(doc, log) {
    const tbls = all(doc, 'tbl');
    const sost = tbls.find(t => /Sostituto del Legale Rappresentante/.test(testo(t)));
    const lr = sost && tbls.slice(0, tbls.indexOf(sost)).reverse().find(t => /AUTORIZZATO/.test(testo(t)) && /NOME/.test(testo(t)));
    if (!sost || !lr) return;
    const righeL = all(lr, 'tr'), righeS = all(sost, 'tr'); let n = 0;
    righeS.forEach((trS, i) => {
      const trL = righeL[i]; if (!trL) return;
      const cL = kids(trL, 'tc'), cS = kids(trS, 'tc');
      cS.forEach((tc, j) => {
        const refs = cL[j] ? all(cL[j], 'r').filter(r => testo(r).trim() && kid(r, 'rPr')) : []; if (!refs.length) return;
        const runs = all(tc, 'r').filter(r => testo(r).trim());
        for (const [k, r] of runs.entries()) {   // stesso ordine: 1° testo come il 1° della tabella del LR, ecc.
          const ref = refs[Math.min(k, refs.length - 1)];
          const old = kid(r, 'rPr'), nuovo = kid(ref, 'rPr').cloneNode(true);
          if (old) r.replaceChild(nuovo, old); else r.insertBefore(nuovo, r.firstChild); n++;
        }
      });
    });
    log.push('Data breach: ' + n + ' testi della tabella del sostituto con il carattere della tabella del Legale Rappresentante.');
  }
  /* ---------- informativa lavoratori in una pagina (03/11/2026): righe vuote ridotte ---------- */
  function unaPagina(doc, log) {
    let n = 0;
    for (const p of all(doc, 'p')) {
      if (!vuoto(p) || !isW(p.parentNode, 'body')) continue;
      let ppr = kid(p, 'pPr'); if (!ppr) { ppr = mk(doc, 'pPr'); p.insertBefore(ppr, p.firstChild); }
      let sp = kid(ppr, 'spacing');
      if (!sp) { sp = mk(doc, 'spacing'); const dopo = ['pStyle', 'keepNext', 'keepLines', 'pageBreakBefore', 'framePr', 'widowControl', 'numPr', 'suppressLineNumbers', 'pBdr', 'shd', 'tabs', 'suppressAutoHyphens', 'kinsoku', 'wordWrap', 'overflowPunct', 'topLinePunct', 'autoSpaceDE', 'autoSpaceDN', 'bidi', 'adjustRightInd', 'snapToGrid'].map(x => kid(ppr, x)).filter(Boolean).pop(); ppr.insertBefore(sp, dopo ? dopo.nextSibling : ppr.firstChild); }
      setWa(sp, 'before', '0'); setWa(sp, 'after', '0'); setWa(sp, 'line', '120'); setWa(sp, 'lineRule', 'exact'); n++;
    }
    for (const br of all(doc, 'br')) if (wa(br, 'type') === 'page') remove(br);
    log.push('Informativa lavoratori: ' + n + ' righe vuote ridotte a 6 pt per stare in una pagina.');
  }

  /* ---------- organigramma (porting di organigramma.py) ---------- */
  function organigramma(cl, C) {
    const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    const BLU = '002060', VERDE = '00B050', ROSSO = 'FF0000';
    const QUAL = { LR: 'Legale Rappresentante', CF: 'Coadiuvanti familiari', SOCI: 'Soci', LAV: 'Lavoratori' };
    const run = (t, col, sz, b, i) => '<w:r><w:rPr><w:rFonts w:ascii="Montserrat" w:hAnsi="Montserrat"/>' + (b ? '<w:b/>' : '') + (i ? '<w:i/>' : '') +
      '<w:color w:val="' + col + '"/><w:sz w:val="' + sz + '"/><w:szCs w:val="' + sz + '"/></w:rPr><w:t xml:space="preserve">' + esc(t) + '</w:t></w:r>';
    const para = r => '<w:p><w:pPr><w:spacing w:before="0" w:after="0" w:line="240" w:lineRule="auto"/><w:jc w:val="center"/></w:pPr>' + r + '</w:p>';
    const shapes = []; let n = 0;
    const box = (x, y, w, h, ps) => { n++; shapes.push('<wps:wsp><wps:cNvPr id="' + n + '" name="Riquadro ' + n + '"/><wps:cNvSpPr txBox="1"/><wps:spPr><a:xfrm><a:off x="' + x + '" y="' + y + '"/><a:ext cx="' + w + '" cy="' + h + '"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="FFFFFF"/></a:solidFill><a:ln w="19050"><a:solidFill><a:schemeClr val="accent1"/></a:solidFill></a:ln></wps:spPr><wps:txbx><w:txbxContent>' + ps.join('') + '</w:txbxContent></wps:txbx><wps:bodyPr rot="0" vert="horz" wrap="square" lIns="45720" tIns="27432" rIns="45720" bIns="27432" anchor="ctr" anchorCtr="0"><a:noAutofit/></wps:bodyPr></wps:wsp>'); };
    const line = (x1, y1, x2, y2) => { n++; shapes.push('<wps:wsp><wps:cNvPr id="' + n + '" name="Linea ' + n + '"/><wps:cNvCnPr/><wps:spPr><a:xfrm><a:off x="' + Math.min(x1, x2) + '" y="' + Math.min(y1, y2) + '"/><a:ext cx="' + Math.abs(x2 - x1) + '" cy="' + Math.abs(y2 - y1) + '"/></a:xfrm><a:prstGeom prst="line"><a:avLst/></a:prstGeom><a:ln w="12700"><a:solidFill><a:schemeClr val="accent1"/></a:solidFill></a:ln></wps:spPr><wps:bodyPr/></wps:wsp>'); };
    const qualDi = f => {
      const q = new Set();
      for (const p of cl.autorizzati) if (p.funzioni.includes(f)) q.add({ 'Legale Rappresentante': 'LR', 'Coadiuvante familiare': 'CF', 'Socio': 'SOCI', 'Lavoratore': 'LAV' }[p.qualifica]);
      return ['LR', 'CF', 'SOCI', 'LAV'].filter(x => q.has(x));
    };
    const riq = (nome, qs) => [para(run('AUTORIZZATI AL TRATTAMENTO', BLU, 14, true)), para(run(nome, VERDE, 14, true, true)), ...qs.map(q => para(run(QUAL[q], VERDE, 14, false, true)))];
    const Wd = 6400000, bw = 2880000, tw = 3285260, th = 1000000;
    box((Wd - tw) / 2 | 0, 0, tw, th, [para(run('TITOLARE DEL TRATTAMENTO', ROSSO, 28, true))]);
    const aq = qualDi('Amministrazione'), aw = 2689664, ah = 300000 + 160000 * (2 + aq.length), ax = (Wd - aw) / 2 | 0, ay = th + 400000;
    box(ax, ay, aw, ah, riq('AMMINISTRAZIONE', aq)); line(Wd / 2, th, Wd / 2, ay);
    const y2 = ay + ah + 450000, fq = qualDi('Front Office'), fh = 300000 + 160000 * (2 + fq.length);
    const fx = C.FUNZIONE_CAPO_CHEF ? 140000 : (Wd - bw) / 2 | 0;
    box(fx, y2, bw, fh, riq('FRONT OFFICE', fq));
    const ymid = ay + ah + 225000; line(Wd / 2, ay + ah, Wd / 2, ymid);
    if (C.FUNZIONE_CAPO_CHEF) {
      const cx = Wd - 140000 - bw, cq = qualDi('Capo chef'), ch = 300000 + 160000 * (2 + cq.length);
      box(cx, y2, bw, ch, riq('CAPO CHEF', cq)); line(fx + bw / 2, ymid, cx + bw / 2, ymid); line(cx + bw / 2, ymid, cx + bw / 2, y2);
    }
    line(fx + bw / 2, ymid, fx + bw / 2, y2);
    const figli = [];
    if (C.SALA === 'CAMERIERI_SALA') figli.push(riq('CAMERIERI DI SALA', qualDi('Cameriere di sala')));
    if (C.SALA === 'CAPO_SALA') figli.push(riq('CAPO SALA', qualDi('Capo sala')));
    if (C.PIANI === 'INTERNE_NOMINATE') figli.push(riq('CAMERIERE AI PIANI', qualDi('Cameriera ai piani')));
    if (C.GUARDIANO_PRESENTE) figli.push(riq('GUARDIANO NOTTURNO', qualDi('Guardiano notturno')));
    figli.push([para(run('Responsabili del trattamento', ROSSO, 14, false, true)), para(run('(Esterni)', ROSSO, 14, false, true))]);
    const spine = fx + 280000, kx = spine + 500000; let y = y2 + fh + 250000, last = 0;
    for (const p of figli) { const h = 300000 + 160000 * Math.max(2, p.length); box(kx, y, bw, h, p); const mid = y + h / 2 | 0; line(spine, mid, kx, mid); last = mid; y += h + 200000; }
    line(spine, y2 + fh, spine, last);
    const H = y;
    return '<w:r ' + NSDECL + '><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="' + Wd + '" cy="' + H + '"/><wp:docPr id="' + (8000 + idSeq++) + '" name="Organigramma"/><wp:cNvGraphicFramePr/>' +
      '<a:graphic><a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"><wpg:wgp><wpg:cNvGrpSpPr/><wpg:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="' + Wd + '" cy="' + H + '"/><a:chOff x="0" y="0"/><a:chExt cx="' + Wd + '" cy="' + H + '"/></a:xfrm></wpg:grpSpPr>' +
      shapes.join('') + '</wpg:wgp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>';
  }

  /* ---------- elenco dei documenti del pacchetto ---------- */
  const RESPONSABILI = [
    ['RESP_CONSULENTE_MAREA', 'Consulente MARE.A'], ['RESP_CONSULENTE', 'Consulente'], ['RESP_COMMERCIALISTA', 'Commercialista'],
    ['RESP_PAGHE', 'Paghe'], ['RESP_PAGHE_E_COMMERCIALISTA', 'Paghe e commercialista'], ['RESP_WEB_AGENCY', 'Web agency'],
    ['RESP_SOFTWARE_HOUSE', 'Software house'], ['RESP_TECNICO_COMPUTER', 'Tecnico computer'], ['RESP_CHIAVI_ELETTRONICHE', 'Chiavi elettroniche'],
    ['RESP_IMPIANTO_VIDEOSORVEGLIANZA', 'Impianto videosorveglianza'], ['RESP_STABILIMENTO_BALNEARE', 'Stabilimento balneare'],
    ['RESP_EXPERIENCE', 'Experience'], ['RESP_DITTA_ESTERNA_SERVIZI_AI_PIANI', 'Ditta esterna servizi ai piani (uso piano camere)']
  ];
  function pacchetto(cl) {
    const C = contesto(cl);
    const d = [];
    const add = (code, nome, perche) => d.push({ code, nome, perche });
    add('REGISTRO', 'Registro delle attività di trattamento', 'sempre');
    add('NOMINE', 'Autorizzati al trattamento – Nomine', 'sempre');
    add('FORMAZIONE', 'Autorizzati al trattamento – Formazione', 'sempre');
    add('DISCIPLINARE', 'Procedure di gestione – Disciplinare', 'sempre');
    add('INFORMATIVA_CLIENTI', 'Informativa clienti', 'sempre');
    add('INFORMATIVA_LAVORATORI', 'Informativa lavoratori', 'sempre');
    add('MODULO_CONSENSI', 'Modulo consensi', 'sempre');
    add('INFORMATIVA_CV', 'Informativa curriculum vitae', 'sempre');
    add('RESP_BACKUP_CREDENZIALI', 'Responsabile backup e credenziali', 'sempre');
    add('PROC_DATA_BREACH', 'Procedura violazioni (data breach)', 'sempre');
    add('ISTRUZIONI', 'Istruzioni documentazione', 'sempre');
    if (C.VIDEO_O_PISCINA) add('DPIA_VIDEO', 'DPIA videosorveglianza', 'telecamere presenti');
    if (C.VIDEO_PRESENTE) add('CARTELLO_VIDEO', 'Cartello videosorveglianza', 'videosorveglianza presente');
    if (C.PISCINA_PRESENTE) add('CARTELLO_PISCINA', 'Cartello telecamera piscina', 'telecamera piscina presente');
    if (C.PIANI === 'INTERNE_NON_NOMINATE') add('DISP_CAMERIERE', 'Disposizioni interne cameriere ai piani', 'cameriere interne non autorizzate');
    if (C.MANUTENTORI_INTERNI) add('DISP_MANUTENTORE', 'Disposizioni interne manutentore (' + cl.manutentori + (cl.manutentori == 1 ? ' copia)' : ' copie)'), 'manutentori interni');
    if (C.PIANI === 'DITTA_ESTERNA' && (cl.impegnoDittaPiani || 0) > 0) add('IMPEGNO_DITTA_PIANI', 'Impegno di riservatezza ditta servizi ai piani' + (cl.impegnoDittaPiani > 1 ? ' (' + cl.impegnoDittaPiani + ' lettere)' : ''), 'servizi ai piani a ditta esterna');
    for (const [code, nome] of RESPONSABILI) {
      // con il contratto di fornitura (preventivo) la nomina di MARE.A è nella sezione «Assunzione dell'incarico» del contratto: niente lettera separata (29/10/2026)
      if (code === 'RESP_CONSULENTE_MAREA' && cl.contratto && cl.contratto.richiesto) continue;
      const n = (cl.responsabili[code] || []).length;
      for (let i = 0; i < n; i++) d.push({ code, nome: 'Responsabile del trattamento – ' + nome + (n > 1 ? ' (lettera ' + (i + 1) + ' di ' + n + ')' : ''), perche: 'scelto in check list', lettera: i });
    }
    if (C.CONTRATTO_FORNITURA) d.push({ code: 'CONTRATTO', nome: 'Contratto di fornitura servizi', perche: 'richiesto in check list' });
    return d;
  }

  /* ---------- generazione ---------- */
  const bin = {};
  async function modello(code) {
    if (!bin[code]) { const s = atob(window.MODELLI[code]); const u = new Uint8Array(s.length); for (let i = 0; i < s.length; i++) u[i] = s.charCodeAt(i); bin[code] = u; }
    return JSZip.loadAsync(bin[code]);
  }
  const parse = x => new DOMParser().parseFromString(x, 'application/xml');
  const ser = d => { let s = new XMLSerializer().serializeToString(d); if (!s.startsWith('<?xml')) s = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\r\n' + s; return s; };

  async function generaDoc(code, cl, firme, avvisi, log, opz) {
    opz = opz || {};
    const zip = await modello(code);
    const C = contesto(cl), V = valori(cl, C), L = liste(cl, C);
    const relsDoc = parse(await zip.file('word/_rels/document.xml.rels').async('string'));
    let rid = 900;
    const ctx = {
      aggiornaCampi: false,
      aggiungiImmagine(img) {
        const nome = 'media/firma_' + (rid) + '.png';
        zip.file('word/' + nome, img.png);
        const r = relsDoc.createElementNS('http://schemas.openxmlformats.org/package/2006/relationships', 'Relationship');
        r.setAttribute('Id', 'rIdF' + rid); r.setAttribute('Type', 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/image'); r.setAttribute('Target', nome);
        relsDoc.documentElement.appendChild(r); return 'rIdF' + (rid++);
      }
    };
    const parti = Object.keys(zip.files).filter(n => /^word\/(document|header\d+|footer\d+)\.xml$/.test(n)).sort();
    const numDoc = code === 'REGISTRO' && zip.file('word/numbering.xml') ? parse(await zip.file('word/numbering.xml').async('string')) : null;
    for (const n of parti) {
      const doc = parse(await zip.file(n).async('string'));
      togliCommentiXml(doc);
      if (n === 'word/document.xml') {
        if (code === 'NOMINE') nomine(doc, cl, C, avvisi, log);
        if (code === 'FORMAZIONE' || code === 'DISCIPLINARE') tabellaAutorizzati(doc, cl);
        if (code === 'REGISTRO') registro(doc, cl, C, avvisi, log, ctx);
        if (code === 'DISP_MANUTENTORE') lettereRipetute(doc, 'RIPETI:MANUTENTORI_INTERNI', Array.from({ length: Number(cl.manutentori) }, () => ({})), C, avvisi, true);
        if (code === 'IMPEGNO_DITTA_PIANI') lettereRipetute(doc, 'RIPETI:LETTERE_RESPONSABILE', Array.from({ length: Number(cl.impegnoDittaPiani) }, () => ({})), C, avvisi, false);
        if (code.startsWith('RESP_') && code !== 'RESP_BACKUP_CREDENZIALI') lettereRipetute(doc, 'RIPETI:LETTERE_RESPONSABILE', [(cl.responsabili[code] || [{}])[opz.lettera || 0] || {}], C, avvisi, false);
        rifiniture(code, doc, cl, C, avvisi, log);
      }
      if (code === 'REGISTRO' && /header/.test(n))   // «HOTEL» evidenziato nell'intestazione del Registro (non marcato): nome dell'insediamento
        for (const r of all(doc, 'r')) { const t = kid(r, 't'); if (t && t.textContent.trim() === 'HOTEL' && all(r, 'highlight').length) { t.textContent = (cl.sedi[0] && cl.sedi[0].nome) || cl.cliente.RAGIONE_SOCIALE; all(r, 'highlight').forEach(remove); } }
      if (/header/.test(n)) all(doc, 'highlight').forEach(remove);   // segnaposto gialli delle intestazioni
      ripetiSedi(doc, cl);
      ripetiVisione(doc, cl, C);
      applicaListe(doc, L);
      applicaCondizioni(doc, C, avvisi);
      applicaTesti(doc, cl, C);
      fill(doc, V);
      if (code === 'REGISTRO' || code === 'CONTRATTO') applicaFirme(doc, firme, ctx);
      if (n === 'word/document.xml') firmaLR(code, doc, cl, firme, ctx);
      if (code === 'DPIA_VIDEO') all(doc, 'highlight').forEach(remove);
      for (const x of all(doc, 'lastRenderedPageBreak')) remove(x);
      if (n === 'word/document.xml') rifinitureFinali(code, doc, cl, avvisi, log);
      if (n === 'word/document.xml' && numDoc) riavviaElenchi(doc, numDoc, log);
      pulisciControlli(doc);
      sistemaVuoti(doc);
      zip.file(n, ser(doc));
    }
    zip.file('word/_rels/document.xml.rels', ser(relsDoc));
    if (numDoc) zip.file('word/numbering.xml', ser(numDoc));
    // commenti dei modelli: non vanno nei documenti consegnati
    const ct = parse(await zip.file('[Content_Types].xml').async('string'));
    for (const n of Object.keys(zip.files)) if (/^word\/comments[A-Za-z]*\.xml$/.test(n)) zip.remove(n);
    for (const o of [...ct.documentElement.children]) if (/comments/i.test(o.getAttribute('PartName') || '')) remove(o);
    if (![...ct.documentElement.children].some(e => (e.getAttribute('Extension') || '').toLowerCase() === 'png')) {
      const e = ct.createElementNS(ct.documentElement.namespaceURI, 'Default'); e.setAttribute('Extension', 'png'); e.setAttribute('ContentType', 'image/png');
      ct.documentElement.insertBefore(e, ct.documentElement.firstChild);
    }
    zip.file('[Content_Types].xml', ser(ct));
    const rels = parse(await zip.file('word/_rels/document.xml.rels').async('string'));
    for (const r of [...rels.documentElement.children]) if (/comments/i.test(r.getAttribute('Target') || '')) remove(r);
    zip.file('word/_rels/document.xml.rels', ser(rels));
    if (ctx.aggiornaCampi && zip.file('word/settings.xml')) {   // indice del Registro aggiornato all'apertura in Word
      const st = parse(await zip.file('word/settings.xml').async('string'));
      if (!all(st, 'updateFields').length) {
        const u = mk(st, 'updateFields'); setWa(u, 'val', 'true');
        const dopo = ['hdrShapeDefaults', 'footnotePr', 'endnotePr', 'compat', 'docVars', 'rsids', 'mathPr', 'attachedSchema', 'themeFontLang', 'clrSchemeMapping', 'doNotIncludeSubdocsInStats', 'doNotAutoCompressPictures', 'forceUpgrade', 'captions', 'readModeInkLockDown', 'smartTagType', 'schemaLibrary', 'shapeDefaults', 'doNotEmbedSmartTags', 'decimalSymbol', 'listSeparator'];
        const ref = [...st.documentElement.children].find(e => dopo.includes(e.localName));
        st.documentElement.insertBefore(u, ref || null);
      }
      zip.file('word/settings.xml', ser(st));
    }
    return zip.generateAsync({ type: 'uint8array', compression: 'DEFLATE' });
  }

  async function genera(cl, firme, onProgress) {
    const avvisi = [], log = [], files = [];
    const docs = pacchetto(cl);
    let i = 0;
    for (const d of docs) {
      i++; onProgress && onProgress(i, docs.length, d.nome);
      const data = await generaDoc(d.code, cl, firme || {}, avvisi, log, { lettera: d.lettera });
      files.push({ nome: String(i).padStart(2, '0') + '_' + d.nome.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[–()]/g, '').replace(/[^\wÀ-ÿ.]+/g, '_').replace(/_+$/, '') + '.docx', data, doc: d });
    }
    return { files, avvisi: [...new Set(avvisi)], log };
  }

  /* metadati dei modelli per costruire le schermate (tendine, voci predefinite) */
  async function metadati() {
    const out = { campi: {}, blocchi: {} };
    for (const code of Object.keys(window.MODELLI)) {
      const zip = await modello(code);
      const doc = parse(await zip.file('word/document.xml').async('string'));
      for (const s of all(doc, 'sdt')) {
        const t = tag(s); if (!t.startsWith('CAMPO:') || out.campi[t.slice(6)]) continue;
        out.campi[t.slice(6)] = { items: listItems(s).filter(i => i.trim()), def: testo(s).trim() };
      }
      for (const [k, tg] of [['ARCHIVIO_CARTACEO', 'RIPETI:ARCHIVIO_CARTACEO'], ['ARCHIVIO_INFORMATICO', 'RIPETI:ARCHIVIO_INFORMATICO']]) {
        const s = sdts(doc, t => t === tg)[0]; if (s) out.blocchi[k] = moduloBlocco(s).filter(c => c.tag.startsWith('ARCHIVIO_') && !/_NUMERO$/.test(c.tag));
      }
      if (code.startsWith('RESP_')) { const s = sdts(doc, t => t === 'RIPETI:LETTERE_RESPONSABILE')[0]; if (s) { const m = moduloBlocco(s).filter(c => c.tag.startsWith('RESP_')); if (m.length) out.blocchi[code] = m; } }
      for (const L of ['MISURE_MINACCE_UMANE', 'MISURE_ANTINCENDIO', 'MISURE_EVENTI_NATURALI']) {
        const s = sdts(doc, t => t === 'LISTA:' + L)[0];
        if (s) { const v = sdts(s, t => t.startsWith('CAMPO:'))[0]; out.campi['LISTA_' + L] = { items: listItems(v).filter(i => i.trim()) }; }
      }
    }
    return out;
  }

  window.Motore = { genera, pacchetto, metadati, contesto, RESPONSABILI, ORDINE, CON_NOMINATIVO };
})();
