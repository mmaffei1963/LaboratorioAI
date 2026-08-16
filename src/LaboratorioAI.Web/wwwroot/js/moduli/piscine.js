/* ═══════════════════════════════════════════════════════════
   DVR Piscine — questionario, salvataggio e generazione
   ═══════════════════════════════════════════════════════════ */
import { chiamaApi, inviaJson, testo, avvisa, occupa } from '../comune.js';

let base = null;   // scheda precaricata (anagrafica, punti, analisi storiche)
let idCorrente = null;

const $ = id => document.getElementById(id);

// ── Ricerca insediamenti ───────────────────────────────────
let timer = null;
function cercaConRitardo() {
    clearTimeout(timer);
    const q = $('ricerca-insediamento').value.trim();
    const elenco = $('elenco-insediamenti');
    if (q.length < 3) {
        elenco.innerHTML = '<p class="vuoto">Digita almeno tre caratteri per cercare.</p>';
        return;
    }
    timer = setTimeout(() => cerca(q), 300);
}

async function cerca(q) {
    const elenco = $('elenco-insediamenti');
    elenco.innerHTML = '<p class="attesa">Ricerca…</p>';
    try {
        const righe = await chiamaApi(`/api/piscine/cerca?q=${encodeURIComponent(q)}`);
        if (righe.length === 0) {
            elenco.innerHTML = '<p class="vuoto">Nessun insediamento trovato.</p>';
            return;
        }
        elenco.innerHTML = righe.map(r => `
            <article class="voce-elenco" data-id="${r.idInsediamento}">
                <div class="voce-testata"><strong>${testo(r.ragioneSociale)}</strong></div>
                <div class="voce-pie">
                    <span>${testo(r.comune) || ''} ${r.provincia ? `(${testo(r.provincia)})` : ''}</span>
                    <span>${testo(r.partitaIva) || ''}</span>
                </div>
            </article>`).join('');
        elenco.querySelectorAll('.voce-elenco').forEach(n =>
            n.addEventListener('click', () => precarica(Number(n.dataset.id))));
    } catch (e) {
        elenco.innerHTML = `<p class="errore">${testo(e.message)}</p>`;
    }
}

// ── Precarica e nuova scheda ───────────────────────────────
async function precarica(idInsediamento) {
    try {
        base = await chiamaApi(`/api/piscine/precarica/${idInsediamento}`);
        idCorrente = null;
        popolaForm(base.scheda);
        renderRapporti(base.analisiStoriche || []);
        $('titolo-scheda').textContent = 'Nuova scheda — ' + (base.scheda.anagrafica.ragioneSociale || '');
        mostraScheda();
        $('btn-genera').disabled = true;
        $('btn-genera-regolamento').disabled = true;
        $('btn-genera-manuale').disabled = true;
        avvisa(`Precaricati ${base.analisiStoriche?.length || 0} rapporti di prova (ultimi 5 anni).`, 'info');
    } catch (e) {
        avvisa(e.message, 'errore');
    }
}

function mostraScheda() {
    $('pannello-scheda').classList.remove('nascosto');
    $('segnaposto-scheda').classList.add('nascosto');
}

// ── Riempimento del form da una scheda ─────────────────────
function popolaForm(s) {
    const a = s.anagrafica || {};
    $('p-ragione').value = a.ragioneSociale || '';
    $('p-piva').value = a.partitaIva || '';
    $('p-indirizzo').value = a.indirizzo || '';
    $('p-comune').value = a.comune || '';
    $('p-provincia').value = a.provincia || '';
    $('p-regione').value = a.regione || '';
    // Etichetta bottone in base alla regione: Marche = «Registro», altrove «Manuale».
    $('btn-genera-manuale').textContent =
        (a.regione || '').toLowerCase().includes('march') ? 'Genera Registro' : 'Genera Manuale';
    $('p-codfisc').value = a.codiceFiscale || '';
    $('p-sedelegale').value = a.sedeLegale || '';

    $('p-ateco').value = s.codiceAteco || '';
    $('p-attivita').value = s.attivitaEsercitata || '';
    $('p-periodo').value = s.periodoEsercizio || '';
    $('p-anno').value = s.annoCostruzione ?? '';
    $('p-gestore').value = s.gestore || '';
    sel('p-categoria', s.categoria);
    $('p-gruppo').value = s.gruppo || '';
    $('p-fonte').value = s.fonteApprovvigionamento || '';
    $('p-autsan').value = s.autorizzazioneSanitaria || '';
    $('p-autsan-data').value = isoAData(s.dataAutorizzazioneSanitaria);
    $('p-autscarico').value = s.autorizzazioneScarico || '';
    $('p-tipologia-str').value = s.tipologiaStrutturale || '';
    $('p-tipo-util').value = s.tipoUtilizzazione || '';
    $('p-contatore').value = s.contatoreImmissione || '';
    $('p-registrazione').value = s.registrazioneIngressi || '';
    $('p-carvasca-sup').value = s.caratteristicheVascaSuperficie || '';
    $('p-carvasca-prof').value = s.caratteristicheVascaProfondita || '';
    $('p-lati-bordo').value = s.latiBordoLiberi || '';
    $('p-vigilanza').value = s.vigilanzaBagnanti || '';
    $('p-assistente-scelta').value = s.assistenteBagnantiScelta || '';
    $('p-istruttori').value = s.istruttoriNuoto || '';
    $('p-ph-pericolo').value = s.phPericolo || '';
    $('p-ph-prev').value = s.phAzionePreventiva || '';
    $('p-ph-mon').value = s.phMonitoraggio || '';
    $('p-cll-pericolo').value = s.cloroLiberoPericolo || '';
    $('p-cll-prev').value = s.cloroLiberoAzionePreventiva || '';
    $('p-cll-mon').value = s.cloroLiberoMonitoraggio || '';
    $('p-clc-pericolo').value = s.cloroCombinatoPericolo || '';
    $('p-pompe-corr').value = s.pompeAzioneCorrettiva || '';
    $('p-filtri-corr').value = s.filtriAzioneCorrettiva || '';
    $('p-disinf-prod').value = s.disinfettanteProdotto || '';
    $('p-disinf-dos').value = s.disinfettanteDosaggio || '';
    $('p-reintegro-prod').value = s.reintegroCloroProdotto || '';
    $('p-reintegro-mod').value = s.reintegroCloroModalita || '';
    $('p-flocc-prod').value = s.flocculanteProdotto || '';
    $('p-flocc-mod').value = s.flocculanteModalita || '';
    $('p-corrph-prod').value = s.correttorePhProdotto || '';
    $('p-corrph-mod').value = s.correttorePhModalita || '';
    $('p-antialghe-prod').value = s.antialgheProdotto || '';
    $('p-antialghe-mod').value = s.antialgheModalita || '';
    $('p-servizi-caratt').value = s.serviziCaratterizzazione || '';
    $('p-servizi-distinz').value = s.serviziDistinzione || '';
    $('p-servizi-docce').value = s.serviziNumeroDocce || '';
    $('p-servizi-lavabo').value = s.serviziNumeroLavabo || '';
    $('p-passaggio').value = s.passaggioAccesso || '';
    $('p-lavap-num').value = s.lavapiedNumero || '';
    $('p-lavap-ril').value = s.lavapiedRilascio || '';
    $('p-lavap-sol').value = s.lavapiedSoluzione || '';
    $('p-antimic-freq').value = s.antimicFrequenza || '';
    $('p-antimic-proc').value = s.antimicProcedura || '';
    $('p-docce-caratt').value = s.docceCaratterizzazione || '';
    $('p-docce-comp').value = s.docceComposizione || '';
    $('p-docce-linee').value = s.docceLinee || '';
    $('p-docce-misc').value = s.docceMiscelazione || '';
    $('p-pronto-socc').value = s.prontoSoccorso || '';
    $('p-access-mezzi').value = s.accessibilitaMezzi || '';
    $('p-ps-azprev').value = s.psAzionePreventiva || '';
    $('p-ps-luogo').value = s.psLuogoConservazione || '';
    $('p-ps-ubic').value = s.psUbicazione || '';
    $('p-addetto-pulizie').value = s.addettoPulizie || '';
    $('p-mar-gruppo').value = s.marcheGruppoStruttura || '';
    $('p-mar-carsup').value = s.marcheCarVascaSup || '';
    $('p-mar-carprof').value = s.marcheCarVascaProf || '';
    $('p-mar-postiletto').value = s.marchePostiLetto || '';
    $('p-mar-imm-n').value = s.marcheImmissioneN || '';
    $('p-mar-imm-tipo').value = s.marcheImmissioneTipo || '';
    $('p-mar-ripresa-sup').value = s.marcheRipresaSup || '';
    $('p-mar-ripresa-imm-n').value = s.marcheRipresaImmN || '';
    $('p-mar-ripresa-imm-tipo').value = s.marcheRipresaImmTipo || '';
    $('p-mar-filtri-modello').value = s.marcheFiltriModello || '';
    $('p-mar-filtri-altezza').value = s.marcheFiltriAltezza || '';
    $('p-mar-filtri-portata').value = s.marcheFiltriPortata || '';
    $('p-mar-pompe-modello').value = s.marchePompeModello || '';
    $('p-mar-pompe-hp').value = s.marchePompeHp || '';
    $('p-mar-pompe-riserva').value = s.marchePompeRiserva || '';
    $('p-mar-curva').value = s.marcheCurva || '';
    $('p-mar-neutr-prod').value = s.marcheNeutralizzanteProd || '';
    $('p-mar-neutr-mod').value = s.marcheNeutralizzanteMod || '';
    $('p-mar-sistema-auto').value = s.marcheSistemaAuto || '';
    $('p-ditta-ord-rag').value = s.dittaOrdRag || '';
    $('p-ditta-ord-sede').value = s.dittaOrdSede || '';
    $('p-ditta-ord-comune').value = s.dittaOrdComune || '';
    $('p-ditta-ord-piva').value = s.dittaOrdPiva || '';
    $('p-ditta-str-rag').value = s.dittaStrRag || '';
    $('p-ditta-str-sede').value = s.dittaStrSede || '';
    $('p-ditta-str-comune').value = s.dittaStrComune || '';
    $('p-ditta-str-piva').value = s.dittaStrPiva || '';
    $('p-mar-gest-nascita').value = s.marcheGestoreNascita || '';
    $('p-mar-gest-luogo').value = s.marcheGestoreLuogo || '';
    $('p-mar-gest-residenza').value = s.marcheGestoreResidenza || '';
    $('p-mar-gest-comune').value = s.marcheGestoreComune || '';
    $('p-mar-gest-cf').value = s.marcheGestoreCf || '';
    $('p-mar-bagnanti').value = s.marcheBagnantiMax || '';
    $('p-mar-manut-interno').value = s.marcheManutInterno || '';
    $('p-orario-dalle').value = s.orarioDalle || '';
    $('p-orario-alle').value = s.orarioAlle || '';
    $('p-servizi').value = s.ubicazioneServizi || '';

    $('p-data').value = isoAData(s.dataValutazione);
    $('p-revisione').value = s.revisione || '';
    $('p-valutatore').value = s.valutatore || '';

    sel('p-tipo-struttura', s.tipoStruttura);
    $('p-numero-vasche').value = s.numeroVasche ?? '';
    sel('p-collocazione', s.collocazione);
    sel('p-tipo-utenza', s.tipoUtenza);
    sel('p-ricircolo', s.sistemaRicircolo);
    sel('p-griglie', s.griglieConformi);
    sel('p-trattamento', s.trattamentoAcqua);
    $('p-idromassaggio').value = s.presenzaIdromassaggio ? 'true' : 'false';
    sel('p-bagnino', s.presenzaBagnino);
    $('p-dae').value = s.presenzaDae ? 'true' : 'false';
    $('p-datore').value = s.datoreLavoro || '';
    $('p-responsabile').value = s.responsabilePiscina || '';
    $('p-addetto').value = s.addettoImpianti || '';
    $('p-resp-bagnanti').value = s.responsabileSicurezzaBagnanti || '';
    $('p-assistente').value = s.assistenteBagnanti || '';
    $('p-rischio').value = s.rischioResiduo || '';

    const m = s.misure || {};
    $('m-griglie').checked = !!m.griglie;
    $('m-cartelli').checked = !!m.cartelli;
    $('m-salvagente').checked = !!m.salvagente;
    $('m-pronto').checked = !!m.prontoSoccorso;
    $('m-dae').checked = !!m.dae;
    $('m-regolamento').checked = !!m.regolamento;
    $('m-registro').checked = !!m.registro;
    $('m-manutenzione').checked = !!m.manutenzione;
    $('m-formazione').checked = !!m.formazione;

    document.querySelector('#elenco-vasche').innerHTML = '';
    (s.vasche || []).forEach(aggiungiVasca);

    document.querySelector('#tab-parametri tbody').innerHTML = '';
    (s.parametriControllo || []).forEach(p => aggiungiRiga('#tab-parametri tbody', COL_PARAM, p));

    document.querySelector('#tab-punti tbody').innerHTML = '';
    (s.puntiPrelievo || []).forEach(p => aggiungiRiga('#tab-punti tbody', COL_PUNTI, p));

    document.querySelector('#tab-manutenzione tbody').innerHTML = '';
    (s.manutenzione || []).forEach(mm => aggiungiRiga('#tab-manutenzione tbody', COL_MANUT,
        { ...mm, prossimaScadenza: isoAData(mm.prossimaScadenza) }));
}

function sel(id, valore) { $(id).value = (valore === null || valore === undefined) ? '' : String(valore); }

// ── Rapporti di prova (consultazione, sola lettura) ────────
function dataBreve(iso) {
    if (!iso) return '';
    const d = iso.substring(0, 10).split('-');
    return `${d[2]}/${d[1]}/${d[0]}`;
}

function renderRapporti(righe) {
    const c = $('tab-rapporti');
    if (!righe.length) {
        c.innerHTML = '<p class="vuoto">Nessun rapporto di prova negli ultimi 5 anni.</p>';
        return;
    }
    c.innerHTML = `<table class="tabella-compatta"><thead><tr>
            <th>Campione</th><th>Prelievo</th><th>Matrice</th><th>Punto</th>
            <th>Luogo</th><th>N° certificato</th><th>Data cert.</th>
        </tr></thead><tbody>${righe.map(r => `<tr>
            <td>${testo(r.numeroCampione)}</td>
            <td>${dataBreve(r.dataPrelievo || r.dataRicezione)}</td>
            <td>${testo(r.matrice) || ''}</td>
            <td>${testo(r.puntoPrelievo) || ''}</td>
            <td>${testo(r.luogoPrelievo) || ''}</td>
            <td>${testo(r.numeroCertificato) || ''}</td>
            <td>${dataBreve(r.dataCertificato)}</td>
        </tr>`).join('')}</tbody></table>`;
}

// ── Vasche (righe dinamiche) ───────────────────────────────
function aggiungiVasca(v = {}) {
    const card = document.createElement('div');
    card.className = 'vasca-card';
    const f = (k, etichetta, ph = '') =>
        `<label>${etichetta}<input type="text" data-k="${k}" value="${testo(v[k]) || ''}" placeholder="${ph}" /></label>`;
    const tipi = ['Principale', 'Nuoto', 'Bambini', 'Idromassaggio', 'Altro'];
    if (v.tipo && !tipi.includes(v.tipo)) tipi.push(v.tipo);
    const tipoSel = `<label>Tipo<select data-k="tipo"><option value="">—</option>${
        tipi.map(o => `<option value="${o}"${v.tipo === o ? ' selected' : ''}>${o}</option>`).join('')
    }</select></label>`;
    // Select con le stesse opzioni dei menu a tendina del manuale Word.
    const sf = (k, etichetta, opzioni) => {
        const opts = opzioni.slice();
        if (v[k] && !opts.includes(v[k])) opts.push(v[k]);
        return `<label>${etichetta}<select data-k="${k}"><option value="">—</option>${
            opts.map(o => `<option value="${o.replace(/"/g, '&quot;')}"${v[k] === o ? ' selected' : ''}>${o}</option>`).join('')
        }</select></label>`;
    };
    const OPT_FORMA = ['Come da planimetria in allegato', 'Quadrata', 'Rettangolare', 'Circolare', 'Irregolare'];
    const OPT_VARIAZ = ['Fondo regolare', 'Variazioni segnalate a bordo vasca'];
    const OPT_ATTREZ = ['Nessuna', 'Scivolo <200cm', 'Scivolo >200cm', 'Trampolino', 'Idromassaggio',
        'Vasca bimbi ≤ 60cm integrata in vasca principale non contigua con la stessa'];
    const OPT_DELIM = ['Nessuna', 'Recinzione in metallo', 'Recinzione in vetro temprato', 'Recinzione in legno', 'Divisione mediante fioriere'];
    const OPT_FILTRO = ['Sabbia', 'Sabbia quarzifera', 'Vetro'];
    const OPT_LAVMOD = ['Automatica', 'Manuale'];
    const OPT_LAVFREQ = ['Giornaliera', 'Ogni 6 ore', 'Ogni 12 ore'];
    const OPT_RISC = ['Nessuno', 'Scambiatore di calore', 'Caldaia dedicata', 'Centrale termica principale',
        'Impianto solare termico dedicato', 'Impianto solare termico principale', 'Pompa di calore'];
    const OPT_TEMP = ['Ambiente', '27°C', '28°C', '29°C', '30°C', '31°C', '32°C', '33°C'];
    const OPT_SINO = ['Sì', 'No'];
    card.innerHTML = `
        <div class="vasca-testata">
            <strong>Vasca ${testo(v.denominazione) || ''}</strong>
            <button type="button" class="pulsante minuto rimuovi">Rimuovi</button>
        </div>
        <div class="vasca-gruppo"><span class="vasca-tit">Dati</span><div class="griglia-campi">
            ${f('denominazione', 'Denominazione')}${tipoSel}${f('dimensioni', 'Dimensioni')}
            ${f('profondita', 'Profondità (cm)', 'es. 139')}${f('volume', 'Volume (m³)')}${f('temperatura', 'Temperatura (°C)')}
            ${f('capienza', 'Capienza')}
        </div></div>
        <div class="vasca-gruppo"><span class="vasca-tit">Struttura</span><div class="griglia-campi">
            ${sf('forma', 'Forma', OPT_FORMA)}${f('superficie', 'Superficie (m²)')}
            ${sf('variazioneBatimetrica', 'Variazione batimetrica', OPT_VARIAZ)}
            ${sf('attrezzatureAccessorie', 'Attrezzature accessorie', OPT_ATTREZ)}
            ${f('struttura', 'Struttura')}${f('rivestimento', 'Rivestimento')}
        </div></div>
        <div class="vasca-gruppo"><span class="vasca-tit">Impianto tecnico</span><div class="griglia-campi">
            ${sf('delimitazioneArea', 'Delimitazione area', OPT_DELIM)}
            ${f('pompeNumero', 'N° pompe', 'es. 2 + 1 di riserva')}${f('pompePotenza', 'Potenza (kW)')}${f('pompePortata', 'Portata (m³/h)')}
            ${f('filtriNumero', 'N° filtri')}${f('filtriDiametro', 'Diametro netto (mm)')}${sf('filtriTipologia', 'Tipologia filtri', OPT_FILTRO)}
            ${sf('contrLavaggiModalita', 'Modalità contro-lavaggi', OPT_LAVMOD)}${sf('contrLavaggiFrequenza', 'Frequenza contro-lavaggi', OPT_LAVFREQ)}
            ${f('prefiltriNumero', 'N° prefiltri')}
            ${sf('riscaldamento', 'Riscaldamento', OPT_RISC)}${sf('temperaturaEsercizio', '°T di esercizio', OPT_TEMP)}
            ${f('ricambioAcqua', 'Ricambio ex-novo', 'Ogni 12 mesi')}${sf('contatoreDedicato', 'Contatore dedicato', OPT_SINO)}${sf('ispezioni', 'Ispezioni acqua immissione', OPT_SINO)}
        </div></div>
        <div class="vasca-gruppo"><span class="vasca-tit">Circolazione</span><div class="griglia-campi">
            ${f('bocchetteParete', 'Bocchette a parete (n°)')}${f('bocchetteFondo', 'Bocchette a fondo (n°)')}
            ${f('vascaCompenso', 'Vasca di compenso (m³)', 'es. ~ 5')}${sf('pompeRiserva', 'Pompe di riserva', OPT_SINO)}
        </div></div>
        <div class="vasca-gruppo"><span class="vasca-tit">Trattamento</span><div class="griglia-campi">
            ${f('sistemaDosaggio', 'Sistema di dosaggio')}${f('principioAttivo', 'Principio attivo')}
            ${f('sistemaAutomaticoAnalisi', 'Sistema automatico di analisi')}
        </div></div>`;
    card.querySelector('.rimuovi').addEventListener('click', () => card.remove());
    document.querySelector('#elenco-vasche').appendChild(card);
}

function raccogliVasche() {
    return [...document.querySelectorAll('#elenco-vasche .vasca-card')].map(card => {
        const o = {};
        card.querySelectorAll('input, select').forEach(i => o[i.dataset.k] = i.value.trim() || null);
        return o;
    }).filter(o => Object.values(o).some(x => x));   // tiene la vasca se ha almeno un campo
}

// ── Tabelle generiche: parametri, punti, manutenzione ──────
const COL_PARAM = [{ k: 'parametro' }, { k: 'unitaMisura' }, { k: 'limite' }, { k: 'frequenza' }, { k: 'metodo' }];
const COL_PUNTI = [{ k: 'punto' }, { k: 'ubicazione' }, { k: 'frequenza' }, { k: 'esitoStorico' }];
const COL_MANUT = [{ k: 'attivita' }, { k: 'frequenza' }, { k: 'responsabile' }, { k: 'prossimaScadenza', ph: 'gg/mm/aaaa' }];

function aggiungiRiga(tbodySel, colonne, v = {}) {
    const tr = document.createElement('tr');
    tr.innerHTML = colonne.map(c =>
        `<td><input type="text" data-k="${c.k}" value="${testo(v[c.k]) || ''}" placeholder="${c.ph || ''}" /></td>`).join('')
        + '<td><button type="button" class="minuto rimuovi">×</button></td>';
    tr.querySelector('.rimuovi').addEventListener('click', () => tr.remove());
    document.querySelector(tbodySel).appendChild(tr);
}

function raccogliTab(tbodySel, chiave) {
    return [...document.querySelectorAll(tbodySel + ' tr')].map(tr => {
        const o = {};
        tr.querySelectorAll('input').forEach(i => o[i.dataset.k] = i.value.trim() || null);
        return o;
    }).filter(o => o[chiave]);
}

function raccogliManutenzione() {
    return raccogliTab('#tab-manutenzione tbody', 'attivita').map(o => ({
        attivita: o.attivita,
        frequenza: o.frequenza,
        responsabile: o.responsabile,
        prossimaScadenza: dataAIso(o.prossimaScadenza)
    }));
}

async function caricaStandardParametri() {
    try {
        const righe = await chiamaApi('/api/piscine/standard/parametri');
        document.querySelector('#tab-parametri tbody').innerHTML = '';
        righe.forEach(p => aggiungiRiga('#tab-parametri tbody', COL_PARAM, p));
        avvisa(`Caricati ${righe.length} parametri standard (Tabella A).`, 'ok');
    } catch (e) {
        avvisa(e.message, 'errore');
    }
}

// ── Costruzione della scheda dal form ──────────────────────
function numOrNull(id) { const v = $(id).value; return v === '' ? null : Number(v); }
function valOrNull(id) { const v = $(id).value.trim(); return v === '' ? null : v; }
function chk(id) { return $(id).checked; }

function componiScheda() {
    return {
        anagrafica: base.scheda.anagrafica,
        tipoStruttura: numOrNull('p-tipo-struttura'),
        numeroVasche: numOrNull('p-numero-vasche'),
        collocazione: numOrNull('p-collocazione'),
        tipoUtenza: numOrNull('p-tipo-utenza'),
        sistemaRicircolo: numOrNull('p-ricircolo'),
        griglieConformi: numOrNull('p-griglie'),
        trattamentoAcqua: numOrNull('p-trattamento'),
        presenzaIdromassaggio: $('p-idromassaggio').value === 'true',
        presenzaBagnino: numOrNull('p-bagnino'),
        presenzaDae: $('p-dae').value === 'true',
        datoreLavoro: valOrNull('p-datore'),
        responsabilePiscina: valOrNull('p-responsabile'),
        addettoImpianti: valOrNull('p-addetto'),
        responsabileSicurezzaBagnanti: valOrNull('p-resp-bagnanti'),
        assistenteBagnanti: valOrNull('p-assistente'),
        gestore: valOrNull('p-gestore'),
        codiceAteco: valOrNull('p-ateco'),
        attivitaEsercitata: valOrNull('p-attivita'),
        periodoEsercizio: valOrNull('p-periodo'),
        annoCostruzione: numOrNull('p-anno'),
        categoria: numOrNull('p-categoria'),
        gruppo: valOrNull('p-gruppo'),
        fonteApprovvigionamento: valOrNull('p-fonte'),
        autorizzazioneSanitaria: valOrNull('p-autsan'),
        dataAutorizzazioneSanitaria: dataAIso($('p-autsan-data').value),
        autorizzazioneScarico: valOrNull('p-autscarico'),
        tipologiaStrutturale: valOrNull('p-tipologia-str'),
        tipoUtilizzazione: valOrNull('p-tipo-util'),
        contatoreImmissione: valOrNull('p-contatore'),
        registrazioneIngressi: valOrNull('p-registrazione'),
        caratteristicheVascaSuperficie: valOrNull('p-carvasca-sup'),
        caratteristicheVascaProfondita: valOrNull('p-carvasca-prof'),
        latiBordoLiberi: valOrNull('p-lati-bordo'),
        vigilanzaBagnanti: valOrNull('p-vigilanza'),
        assistenteBagnantiScelta: valOrNull('p-assistente-scelta'),
        istruttoriNuoto: valOrNull('p-istruttori'),
        phPericolo: valOrNull('p-ph-pericolo'),
        phAzionePreventiva: valOrNull('p-ph-prev'),
        phMonitoraggio: valOrNull('p-ph-mon'),
        cloroLiberoPericolo: valOrNull('p-cll-pericolo'),
        cloroLiberoAzionePreventiva: valOrNull('p-cll-prev'),
        cloroLiberoMonitoraggio: valOrNull('p-cll-mon'),
        cloroCombinatoPericolo: valOrNull('p-clc-pericolo'),
        pompeAzioneCorrettiva: valOrNull('p-pompe-corr'),
        filtriAzioneCorrettiva: valOrNull('p-filtri-corr'),
        disinfettanteProdotto: valOrNull('p-disinf-prod'),
        disinfettanteDosaggio: valOrNull('p-disinf-dos'),
        reintegroCloroProdotto: valOrNull('p-reintegro-prod'),
        reintegroCloroModalita: valOrNull('p-reintegro-mod'),
        flocculanteProdotto: valOrNull('p-flocc-prod'),
        flocculanteModalita: valOrNull('p-flocc-mod'),
        correttorePhProdotto: valOrNull('p-corrph-prod'),
        correttorePhModalita: valOrNull('p-corrph-mod'),
        antialgheProdotto: valOrNull('p-antialghe-prod'),
        antialgheModalita: valOrNull('p-antialghe-mod'),
        serviziCaratterizzazione: valOrNull('p-servizi-caratt'),
        serviziDistinzione: valOrNull('p-servizi-distinz'),
        serviziNumeroDocce: valOrNull('p-servizi-docce'),
        serviziNumeroLavabo: valOrNull('p-servizi-lavabo'),
        passaggioAccesso: valOrNull('p-passaggio'),
        lavapiedNumero: valOrNull('p-lavap-num'),
        lavapiedRilascio: valOrNull('p-lavap-ril'),
        lavapiedSoluzione: valOrNull('p-lavap-sol'),
        antimicFrequenza: valOrNull('p-antimic-freq'),
        antimicProcedura: valOrNull('p-antimic-proc'),
        docceCaratterizzazione: valOrNull('p-docce-caratt'),
        docceComposizione: valOrNull('p-docce-comp'),
        docceLinee: valOrNull('p-docce-linee'),
        docceMiscelazione: valOrNull('p-docce-misc'),
        prontoSoccorso: valOrNull('p-pronto-socc'),
        accessibilitaMezzi: valOrNull('p-access-mezzi'),
        psAzionePreventiva: valOrNull('p-ps-azprev'),
        psLuogoConservazione: valOrNull('p-ps-luogo'),
        psUbicazione: valOrNull('p-ps-ubic'),
        addettoPulizie: valOrNull('p-addetto-pulizie'),
        marcheGruppoStruttura: valOrNull('p-mar-gruppo'),
        marcheCarVascaSup: valOrNull('p-mar-carsup'),
        marcheCarVascaProf: valOrNull('p-mar-carprof'),
        marchePostiLetto: valOrNull('p-mar-postiletto'),
        marcheImmissioneN: valOrNull('p-mar-imm-n'),
        marcheImmissioneTipo: valOrNull('p-mar-imm-tipo'),
        marcheRipresaSup: valOrNull('p-mar-ripresa-sup'),
        marcheRipresaImmN: valOrNull('p-mar-ripresa-imm-n'),
        marcheRipresaImmTipo: valOrNull('p-mar-ripresa-imm-tipo'),
        marcheFiltriModello: valOrNull('p-mar-filtri-modello'),
        marcheFiltriAltezza: valOrNull('p-mar-filtri-altezza'),
        marcheFiltriPortata: valOrNull('p-mar-filtri-portata'),
        marchePompeModello: valOrNull('p-mar-pompe-modello'),
        marchePompeHp: valOrNull('p-mar-pompe-hp'),
        marchePompeRiserva: valOrNull('p-mar-pompe-riserva'),
        marcheCurva: valOrNull('p-mar-curva'),
        marcheNeutralizzanteProd: valOrNull('p-mar-neutr-prod'),
        marcheNeutralizzanteMod: valOrNull('p-mar-neutr-mod'),
        marcheSistemaAuto: valOrNull('p-mar-sistema-auto'),
        dittaOrdRag: valOrNull('p-ditta-ord-rag'),
        dittaOrdSede: valOrNull('p-ditta-ord-sede'),
        dittaOrdComune: valOrNull('p-ditta-ord-comune'),
        dittaOrdPiva: valOrNull('p-ditta-ord-piva'),
        dittaStrRag: valOrNull('p-ditta-str-rag'),
        dittaStrSede: valOrNull('p-ditta-str-sede'),
        dittaStrComune: valOrNull('p-ditta-str-comune'),
        dittaStrPiva: valOrNull('p-ditta-str-piva'),
        marcheGestoreNascita: valOrNull('p-mar-gest-nascita'),
        marcheGestoreLuogo: valOrNull('p-mar-gest-luogo'),
        marcheGestoreResidenza: valOrNull('p-mar-gest-residenza'),
        marcheGestoreComune: valOrNull('p-mar-gest-comune'),
        marcheGestoreCf: valOrNull('p-mar-gest-cf'),
        marcheBagnantiMax: valOrNull('p-mar-bagnanti'),
        marcheManutInterno: valOrNull('p-mar-manut-interno'),
        orarioDalle: valOrNull('p-orario-dalle'),
        orarioAlle: valOrNull('p-orario-alle'),
        ubicazioneServizi: valOrNull('p-servizi'),
        rischioResiduo: valOrNull('p-rischio'),
        vasche: raccogliVasche(),
        puntiPrelievo: raccogliTab('#tab-punti tbody', 'punto'),
        misure: {
            griglie: chk('m-griglie'), cartelli: chk('m-cartelli'), salvagente: chk('m-salvagente'),
            prontoSoccorso: chk('m-pronto'), dae: chk('m-dae'), regolamento: chk('m-regolamento'),
            registro: chk('m-registro'), manutenzione: chk('m-manutenzione'), formazione: chk('m-formazione')
        },
        parametriControllo: raccogliTab('#tab-parametri tbody', 'parametro'),
        manutenzione: raccogliManutenzione(),
        dataValutazione: dataAIso($('p-data').value) || base.scheda.dataValutazione,
        revisione: valOrNull('p-revisione'),
        valutatore: valOrNull('p-valutatore')
    };
}

// ── Proposta rischio residuo ───────────────────────────────
function proponiRischio() {
    const griglie = numOrNull('p-griglie');   // 0 Conformi · 1 NonConformi · 2 DaVerificare
    const bagnino = numOrNull('p-bagnino');   // 0 Continuativo · 1 FasceOrarie · 2 Assente
    const idro = $('p-idromassaggio').value === 'true';
    const dae = $('p-dae').value === 'true' || chk('m-dae');   // select "DAE presente" oppure casella misure

    let punteggio = 0;
    const fattori = [];

    if (griglie === 1) { punteggio += 3; fattori.push('griglie/bocchettoni non conformi (rischio intrappolamento)'); }
    else if (griglie === 2) { punteggio += 1; fattori.push('conformità griglie da verificare'); }

    if (bagnino === 2) { punteggio += 3; fattori.push('assenza di assistente ai bagnanti (rischio annegamento)'); }
    else if (bagnino === 1) { punteggio += 1; fattori.push('sorveglianza solo a fasce orarie'); }

    if (idro) { punteggio += 2; fattori.push('presenza di idromassaggio (rischio legionella accentuato)'); }
    if (!dae) { punteggio += 1; fattori.push('DAE non presente'); }

    const misureCritiche = [
        ['m-salvagente', 'salvagente e asta di salvataggio'],
        ['m-pronto', 'cassetta di primo soccorso'],
        ['m-cartelli', 'cartellonistica di sicurezza'],
        ['m-regolamento', 'regolamento di balneazione esposto'],
        ['m-manutenzione', 'piano di manutenzione periodica']
    ];
    const mancanti = misureCritiche.filter(([id]) => !$(id).checked).map(([, t]) => t);
    punteggio += mancanti.length;

    const livello = punteggio >= 4 ? 'ALTO' : punteggio >= 2 ? 'MEDIO' : 'BASSO';

    let msg = `Rischio residuo stimato: ${livello}.`;
    if (fattori.length) msg += `\nFattori considerati: ${fattori.join('; ')}.`;
    if (mancanti.length) msg += `\nMisure da integrare: ${mancanti.join('; ')}.`;
    if (!fattori.length && !mancanti.length) msg += '\nCondizioni conformi e misure di prevenzione presenti.';
    msg += '\n(Valore proposto automaticamente dalle risposte; verificare e correggere secondo giudizio professionale.)';

    $('p-rischio').value = msg;
    avvisa(`Proposto rischio residuo: ${livello}.`, 'info');
}

// ── Salvataggio ────────────────────────────────────────────
async function salva(stato, pulsante) {
    if (!base) { avvisa('Nessuna scheda da salvare.', 'info'); return; }
    const libera = occupa(pulsante, 'Salvataggio…');
    try {
        const esito = await inviaJson('/api/piscine/salva',
            { id: idCorrente, stato, scheda: componiScheda() });
        idCorrente = esito.idDvrPiscina;
        $('btn-genera').disabled = false;
        $('btn-genera-regolamento').disabled = false;
        $('btn-genera-manuale').disabled = false;
        avvisa(stato === 2 ? 'Salvato come definitivo.' : 'Bozza salvata.', 'ok');
    } catch (e) {
        avvisa(e.message, 'errore');
    } finally {
        libera();
    }
}

// ── Generazione Word ───────────────────────────────────────
async function genera(endpoint, pulsante, fallback) {
    if (!idCorrente) { avvisa('Salva la scheda prima di generare.', 'info'); return; }
    const libera = occupa(pulsante, 'Generazione…');
    try {
        const resp = await fetch(`/api/piscine/${endpoint}/${idCorrente}`, { method: 'POST' });
        if (!resp.ok) { avvisa('Generazione non riuscita.', 'errore'); return; }
        const blob = await resp.blob();
        const disp = resp.headers.get('Content-Disposition') || '';
        const nome = /filename="?([^"]+)"?/.exec(disp)?.[1] || `${fallback}_${idCorrente}.docx`;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = decodeURIComponent(nome); a.click();
        URL.revokeObjectURL(url);
        const mancanti = resp.headers.get('X-Piscine-TagNonTrovati');
        const archivio = resp.headers.get('X-Piscine-Archivio');
        let nota = mancanti ? `Documento generato. Tag non trovati: ${mancanti}` : 'Documento generato.';
        if (archivio) nota += ` — salvato anche in: ${decodeURIComponent(archivio)}`;
        avvisa(nota, mancanti ? 'info' : 'ok');
    } catch (e) {
        avvisa(e.message, 'errore');
    } finally {
        libera();
    }
}

// ── Modulo di raccolta dati (Word vuoto, stampabile) ───────
async function scaricaModuloRaccolta() {
    const regione = $('regione-modulo')?.value || '';
    const url = `/api/piscine/scheda-raccolta${regione ? `?regione=${encodeURIComponent(regione)}` : ''}`;
    try {
        const resp = await fetch(url);
        if (!resp.ok) { avvisa('Modulo non disponibile.', 'errore'); return; }
        const blob = await resp.blob();
        const disp = resp.headers.get('Content-Disposition') || '';
        const nome = /filename="?([^"]+)"?/.exec(disp)?.[1] || 'Scheda_Raccolta_Dati_Piscine.docx';
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = decodeURIComponent(nome);
        a.click();
        URL.revokeObjectURL(a.href);
        avvisa('Modulo di raccolta dati scaricato.', 'ok');
    } catch (e) {
        avvisa(e.message, 'errore');
    }
}

// ── Registro ───────────────────────────────────────────────
async function apriRegistro() {
    const finestra = $('finestra');
    const corpo = $('corpo-finestra');
    corpo.innerHTML = '<p class="attesa">Caricamento…</p>';
    finestra.classList.remove('nascosto');
    try {
        const righe = await chiamaApi('/api/piscine/elenco');
        corpo.innerHTML = righe.length === 0
            ? '<p class="vuoto">Nessun DVR piscina salvato.</p>'
            : `<table class="tabella-compatta"><thead><tr>
                 <th>Cliente</th><th>Comune</th><th>Regione</th><th>Stato</th><th>Modificato</th><th></th>
               </tr></thead><tbody>${righe.map(r => `
                 <tr><td>${testo(r.ragioneSociale)}</td><td>${testo(r.comune) || ''}</td>
                     <td>${testo(r.regione) || ''}</td><td>${testo(r.stato)}</td>
                     <td>${testo(r.dataUltimaModifica)}</td>
                     <td><button type="button" class="minuto apri" data-id="${r.idDvrPiscina}">Apri</button></td>
                 </tr>`).join('')}</tbody></table>`;
    } catch (e) {
        corpo.innerHTML = `<p class="errore">${testo(e.message)}</p>`;
    }
}

async function caricaSalvato(id) {
    try {
        const dati = await chiamaApi(`/api/piscine/${id}`);
        base = { scheda: dati.scheda, analisiStoriche: [] };
        idCorrente = dati.id;
        popolaForm(dati.scheda);

        // Le analisi storiche non sono salvate nella scheda: si rileggono da
        // ArcLab all'apertura, così i rapporti degli ultimi 5 anni ricompaiono.
        const idIns = dati.scheda.anagrafica && dati.scheda.anagrafica.idInsediamento;
        if (idIns) {
            try {
                const pre = await chiamaApi(`/api/piscine/precarica/${idIns}`);
                base.analisiStoriche = pre.analisiStoriche || [];
            } catch { /* non bloccante */ }
        }
        renderRapporti(base.analisiStoriche);
        $('titolo-scheda').textContent = `Scheda #${dati.id} — ${dati.scheda.anagrafica.ragioneSociale || ''} (${dati.stato})`;
        mostraScheda();
        $('btn-genera').disabled = false;
        $('btn-genera-regolamento').disabled = false;
        $('btn-genera-manuale').disabled = false;
        $('finestra').classList.add('nascosto');
    } catch (e) {
        avvisa(e.message, 'errore');
    }
}

// ── Date gg/mm/aaaa ↔ ISO ─────────────────────────────────
function isoAData(iso) {
    if (!iso) return '';
    const [a, m, g] = iso.substring(0, 10).split('-');
    return `${g}/${m}/${a}`;
}
function dataAIso(gmma) {
    const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec((gmma || '').trim());
    return m ? `${m[3]}-${m[2]}-${m[1]}` : null;
}

// ── Avvio ──────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    $('ricerca-insediamento').addEventListener('input', cercaConRitardo);
    $('btn-aggiungi-vasca').addEventListener('click', () => aggiungiVasca());
    $('btn-aggiungi-parametro').addEventListener('click', () => aggiungiRiga('#tab-parametri tbody', COL_PARAM));
    $('btn-aggiungi-punto').addEventListener('click', () => aggiungiRiga('#tab-punti tbody', COL_PUNTI));
    $('btn-aggiungi-manutenzione').addEventListener('click', () => aggiungiRiga('#tab-manutenzione tbody', COL_MANUT));
    $('btn-standard-parametri').addEventListener('click', caricaStandardParametri);
    $('btn-proponi-rischio').addEventListener('click', proponiRischio);
    $('btn-salva-bozza').addEventListener('click', e => salva(1, e.currentTarget));
    $('btn-salva-definitivo').addEventListener('click', e => salva(2, e.currentTarget));
    $('btn-genera').addEventListener('click', e => genera('genera', e.currentTarget, 'DVR_Piscine'));
    $('btn-genera-regolamento').addEventListener('click', e => genera('genera-regolamento', e.currentTarget, 'Regolamento_Piscine'));
    $('btn-genera-manuale').addEventListener('click', e => genera('genera-manuale', e.currentTarget, 'Manuale_Piscine'));

    const modulo = $('btn-modulo-raccolta');
    if (modulo) modulo.addEventListener('click', scaricaModuloRaccolta);

    const reg = $('btn-registro');
    if (reg) reg.addEventListener('click', apriRegistro);
    $('chiudi-finestra').addEventListener('click', () => $('finestra').classList.add('nascosto'));

    // Delega eventi: il pulsante "Apri" del registro è ricreato a ogni apertura.
    $('corpo-finestra').addEventListener('click', e => {
        const b = e.target.closest('.apri');
        if (b) caricaSalvato(Number(b.dataset.id));
    });
});
