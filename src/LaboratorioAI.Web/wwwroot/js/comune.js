/* ═══════════════════════════════════════════════════════════
   Laboratorio AI — funzioni comuni
   Modulo ES importato da tutti i moduli di area.
   ═══════════════════════════════════════════════════════════ */

/**
 * Chiamata a un endpoint applicativo.
 * Centralizza intestazioni, gestione della sessione scaduta e degli errori.
 */
export async function chiamaApi(url, opzioni = {}) {
    const risposta = await fetch(url, {
        ...opzioni,
        headers: {
            'Accept': 'application/json',
            ...(opzioni.body ? { 'Content-Type': 'application/json' } : {}),
            ...(opzioni.headers || {})
        }
    });

    // Sessione scaduta: il ricaricamento rilancia il flusso di accesso
    if (risposta.status === 401) {
        window.location.reload();
        throw new Error('Sessione scaduta');
    }

    if (risposta.status === 403) {
        throw new Error('Permesso non sufficiente per questa operazione.');
    }

    if (!risposta.ok) {
        let messaggio = `Errore ${risposta.status}`;
        try {
            const corpo = await risposta.json();
            if (corpo?.messaggio) messaggio = corpo.messaggio;
        } catch { /* corpo non JSON: si tiene il messaggio predefinito */ }
        throw new Error(messaggio);
    }

    return risposta.json();
}

/** Invio di un corpo JSON. */
export const inviaJson = (url, corpo) =>
    chiamaApi(url, { method: 'POST', body: JSON.stringify(corpo) });

/** Protegge dall'inserimento di HTML nei dati provenienti dal database. */
export function testo(valore) {
    if (valore === null || valore === undefined) return '';
    return String(valore)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

/** Etichetta grafica del giudizio di un'analisi. */
export function etichettaGiudizio(giudizio) {
    const mappa = {
        Conforme:       ['conforme',    'Conforme'],
        NonConforme:    ['non-conforme','Non conforme'],
        Borderline:     ['borderline',  'Borderline'],
        NonDeterminato: ['neutro',      'Da verificare']
    };
    const [classe, testo] = mappa[giudizio] ?? mappa.NonDeterminato;
    return `<span class="etichetta etichetta-${classe}">${testo}</span>`;
}

/** Etichetta grafica dello stato di un campione. */
export function etichettaStato(stato) {
    const mappa = {
        Certificato:      ['conforme',   '✓ Certificato'],
        InCorso:          ['attesa',     '⏳ In corso'],
        SpeditoOriginale: ['outsourcing','↗ Spedito originale']
    };
    const [classe, testo] = mappa[stato] ?? ['neutro', stato];
    return `<span class="etichetta etichetta-${classe}">${testo}</span>`;
}

/** Messaggio temporaneo in sovrimpressione. */
export function avvisa(messaggio, tipo = 'info') {
    const nodo = document.createElement('div');
    nodo.className = `notifica notifica-${tipo}`;
    nodo.textContent = messaggio;
    document.body.appendChild(nodo);

    setTimeout(() => nodo.classList.add('visibile'), 10);
    setTimeout(() => {
        nodo.classList.remove('visibile');
        setTimeout(() => nodo.remove(), 300);
    }, 4000);
}

/** Disabilita un pulsante mostrando un testo di attesa. */
export function occupa(pulsante, testoAttesa = 'Attendere…') {
    const originale = pulsante.textContent;
    pulsante.disabled = true;
    pulsante.dataset.testoOriginale = originale;
    pulsante.textContent = testoAttesa;
    return () => {
        pulsante.disabled = false;
        pulsante.textContent = pulsante.dataset.testoOriginale ?? originale;
    };
}

/** Popola un elenco a discesa. */
export function popolaSelect(select, voci, etichettaVuota = 'Tutti') {
    const valorePrecedente = select.value;
    select.innerHTML = `<option value="">${etichettaVuota}</option>`;
    for (const voce of voci) {
        const opzione = document.createElement('option');
        opzione.value = voce.id ?? voce.Id;
        opzione.textContent = voce.nome ?? voce.Nome;
        select.appendChild(opzione);
    }
    if (valorePrecedente) select.value = valorePrecedente;
}

/** Evidenzia la voce di navigazione dell'area corrente. */
function evidenziaNavigazione() {
    const percorso = window.location.pathname.toLowerCase();
    document.querySelectorAll('.navigazione .voce').forEach(voce => {
        const href = voce.getAttribute('href')?.toLowerCase();
        if (href && href !== '#' && percorso.startsWith(href)) {
            voce.classList.add('attiva');
        }
    });
}

document.addEventListener('DOMContentLoaded', evidenziaNavigazione);
