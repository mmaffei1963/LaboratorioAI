// 21/10/2026: pulsante «⟳ AZW» nella barra in alto (tutti gli utenti). Aggiorna i dati contabili da AZW
// (sola lettura; al massimo ogni 15 minuti, una importazione alla volta) e mostra l'esito in un avviso.
(() => {
    const b = document.getElementById('pulsante-azw');
    const avviso = document.getElementById('avviso-azw');
    if (!b || !avviso) return;
    const testoBase = b.textContent;
    const fmt = d => new Date(d).toLocaleString('it-IT', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });

    let timerAvviso = null;
    function mostra(testo, tipo, ricarica) {
        clearTimeout(timerAvviso);
        avviso.className = 'avviso-azw ' + (tipo || '');
        avviso.textContent = testo;
        if (ricarica) {
            const a = document.createElement('a');
            a.href = '#'; a.textContent = 'Ricarica la pagina';
            a.addEventListener('click', e => { e.preventDefault(); location.reload(); });
            avviso.append(' ', a);
        }
        const x = document.createElement('button');
        x.type = 'button'; x.className = 'avviso-azw-chiudi'; x.textContent = '×'; x.title = 'Chiudi';
        x.addEventListener('click', () => { avviso.hidden = true; });
        avviso.append(x);
        avviso.hidden = false;
        if (tipo !== 'errore' && !ricarica) timerAvviso = setTimeout(() => { avviso.hidden = true; }, 8000);
    }

    async function aggiornaStato() {
        try {
            const r = await fetch('/api/azw/stato');
            if (!r.ok) return null;
            const s = await r.json();
            b.title = (s.ultimoAggiornamento ? `Dati AZW aggiornati il ${fmt(s.ultimoAggiornamento)}. ` : 'Dati AZW mai aggiornati. ')
                + `Clic per aggiornare (al massimo ogni ${s.minutiIntervallo} minuti).`;
            return s;
        } catch { return null; /* il suggerimento non è essenziale */ }
    }

    // 21/10/2026: promemoria all'apertura. Una volta per sessione del browser (prima pagina aperta),
    // solo se l'ultimo aggiornamento è più vecchio dell'intervallo minimo (altrimenti non servirebbe).
    const CHIAVE = 'labai.promemoriaAzw';
    function giaMostrato() { try { return sessionStorage.getItem(CHIAVE) === '1'; } catch { return false; } }
    function segnaMostrato() { try { sessionStorage.setItem(CHIAVE, '1'); } catch { /* senza memoria di sessione si mostra a ogni pagina: pazienza */ } }

    function promemoria(s) {
        if (!s || giaMostrato()) return;
        const eta = s.ultimoAggiornamento ? (Date.now() - new Date(s.ultimoAggiornamento).getTime()) / 60000 : Infinity;
        if (eta < s.minutiIntervallo) return;
        segnaMostrato();
        const quando = s.ultimoAggiornamento ? `I dati AZW sono aggiornati al ${fmt(s.ultimoAggiornamento)}.` : 'I dati AZW non sono mai stati aggiornati.';
        mostra(quando + ' Se ti servono calcoli contabili (provvigioni, spettanze, consuntivi, cruscotto) aggiornali prima.', 'info');
        clearTimeout(timerAvviso);
        const az = document.createElement('button');
        az.type = 'button'; az.className = 'pulsante-azw-avviso'; az.textContent = 'Aggiorna ora';
        az.addEventListener('click', () => b.click());
        avviso.insertBefore(az, avviso.lastChild);
        timerAvviso = setTimeout(() => { avviso.hidden = true; }, 20000);
    }

    b.addEventListener('click', async () => {
        if (b.disabled) return;
        b.disabled = true; b.textContent = '⟳ AZW…';
        mostra('Aggiornamento da AZW in corso: può richiedere qualche minuto, puoi continuare a lavorare in questa pagina.', 'info');
        clearTimeout(timerAvviso);
        try {
            const r = await fetch('/api/azw/aggiorna', { method: 'POST' });
            const esito = r.ok ? await r.json() : { riuscita: false, messaggio: 'Aggiornamento non riuscito (errore ' + r.status + ').' };
            // 22/10/2026: importazione riuscita ma con un avviso (es. contabilità analitica non aggiornata) → in evidenza.
            const conAvviso = esito.riuscita && /Attenzione:/.test(esito.messaggio || '');
            mostra(esito.messaggio, esito.riuscita && !conAvviso ? 'ok' : 'errore', !!esito.importato);
        } catch {
            mostra('Aggiornamento non riuscito: server non raggiungibile.', 'errore');
        } finally {
            b.disabled = false; b.textContent = testoBase;
            aggiornaStato();
        }
    });

    aggiornaStato().then(promemoria);
})();
