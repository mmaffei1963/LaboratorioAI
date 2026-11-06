/* ═══════════════════════════════════════════════════════════
   Laboratorio AI — funzioni comuni a termometri, incubatori,
   bilance e mappature (09/10/2026):
   - numerazione delle letture e aggiunta a gruppi (10 / 5);
   - eliminazione dal registro (nessuna cancellazione fisica:
     la riga resta nel database con Attivo = false) e ripristino.
   ═══════════════════════════════════════════════════════════ */

import { inviaJson, avvisa } from './comune.js';

// ── Letture ───────────────────────────────────────────────

const normalizza = (v) => String(v ?? '').trim().toUpperCase();

/**
 * Numero progressivo di ogni riga dentro il suo gruppo: riparte a ogni cambio
 * di chiave (punto/posizione). Solo di supporto all'inserimento.
 */
export function numeriPerGruppo(chiavi) {
    let precedente = null, contatore = 0;
    return chiavi.map(c => {
        const k = normalizza(c);
        if (k !== precedente) { contatore = 1; precedente = k; } else { contatore++; }
        return contatore;
    });
}

/** Lettera successiva per i gruppi (A → B … Y → Z); se non è una lettera singola resta uguale. */
export function letteraSuccessiva(chiave) {
    const k = normalizza(chiave);
    if (/^[A-Y]$/.test(k)) return String.fromCharCode(k.charCodeAt(0) + 1);
    return String(chiave ?? '').trim() || 'A';
}

/**
 * Aggiunge righe a gruppi di <dimensione> (10 letture per termometri e incubatori):
 * - tabella vuota → <dimensione> righe con «A»;
 * - ultimo gruppo incompleto (es. 1 riga A) → lo completa con la stessa chiave (9 righe A);
 * - ultimo gruppo completo (10 A) → <dimensione> righe con la lettera successiva (10 B).
 * <leggi>(riga) restituisce la chiave del gruppo; <crea>(chiave) crea una riga nuova.
 * Restituisce il numero di righe aggiunte.
 */
export function aggiungiGruppo(elenco, leggi, crea, dimensione) {
    let chiave = 'A', quante = dimensione;
    if (elenco.length > 0) {
        const ultima = String(leggi(elenco[elenco.length - 1]) ?? '').trim() || 'A';
        let nelGruppo = 0;
        for (let i = elenco.length - 1; i >= 0 && normalizza(leggi(elenco[i]) || 'A') === normalizza(ultima); i--) nelGruppo++;
        const resto = nelGruppo % dimensione;
        if (resto !== 0) { chiave = ultima; quante = dimensione - resto; }
        else { chiave = letteraSuccessiva(ultima); }
    }
    for (let i = 0; i < quante; i++) elenco.push(crea(chiave));
    return quante;
}

/**
 * Numero di righe che porta l'elenco al prossimo multiplo di <dimensione>
 * (linearità bilance: 1 punto presente → 4; 5 presenti → 5).
 */
export function righeMancanti(lunghezza, dimensione) {
    const resto = lunghezza % dimensione;
    return resto === 0 ? dimensione : dimensione - resto;
}

// ── Registro: eliminazione e ripristino ───────────────────

/** True se l'utente può eliminare/ripristinare (attributo data-puo-eliminare impostato dalla vista). */
export function puoEliminare() {
    return document.getElementById('riquadro-registro')?.dataset.puoEliminare === 'true';
}

/** True se è spuntata «Mostra eliminate». */
export function mostraEliminate() {
    return document.getElementById('mostra-eliminate')?.checked === true;
}

/** URL dell'elenco, con le righe eliminate se richieste. */
export const urlElenco = (base) => mostraEliminate() ? base + '?eliminate=true' : base;

/** Testo del conteggio: «12 tarature» oppure «12 tarature · 2 eliminate». */
export function conteggioRegistro(righe, nome) {
    const eliminate = righe.filter(r => r.attivo === false).length;
    const attive = righe.length - eliminate;
    return eliminate > 0 ? `${attive} ${nome} · ${eliminate} eliminate` : `${attive} ${nome}`;
}

/** Classe da aggiungere alla riga del registro. */
export const classeRiga = (r) => r.attivo === false ? ' riga-eliminata' : '';

/** Contrassegno da mettere nella prima cella: «eliminata» e, per i responsabili, «Ripristina». */
export function marcaEliminata(r, id) {
    if (r.attivo !== false) return '';
    const bottone = puoEliminare()
        ? ` <button type="button" class="pulsante pulsante-tenue btn-ripristina" data-id="${id}">Ripristina</button>`
        : '';
    return ` <span class="etichetta-eliminata">eliminata</span>${bottone}`;
}

/** Collega i pulsanti «Ripristina» del registro. */
export function collegaRipristino(tabella, urlBase, ricarica) {
    tabella.querySelectorAll('.btn-ripristina').forEach(b => b.addEventListener('click', async (ev) => {
        ev.stopPropagation();
        try {
            const r = await inviaJson(`${urlBase}/${b.dataset.id}/ripristina`, {});
            avvisa(r?.messaggio ?? 'Ripristinata.', 'successo');
            await ricarica();
        } catch (e) { avvisa(e.message, 'errore'); }
    }));
}

/**
 * Chiede il motivo e toglie la riga dal registro. Restituisce true se eliminata.
 * <descrizione> compare nella domanda (es. «la taratura TT-2026-012»).
 */
export async function eliminaDalRegistro(urlBase, id, descrizione) {
    if (!id) { avvisa('Salva o apri prima una riga del registro.', 'info'); return false; }
    const motivo = prompt(`Eliminare dal registro ${descrizione}?\n\nNon viene cancellata: resta nel database e si può ripristinare con «Mostra eliminate».\nScrivi il motivo (obbligatorio):`);
    if (motivo === null) return false;
    if (!motivo.trim()) { avvisa('Eliminazione annullata: il motivo è obbligatorio.', 'info'); return false; }
    try {
        const r = await inviaJson(`${urlBase}/${id}/elimina`, { motivo: motivo.trim() });
        avvisa(r?.messaggio ?? 'Eliminata dal registro.', 'successo');
        return true;
    } catch (e) { avvisa(e.message, 'errore'); return false; }
}
