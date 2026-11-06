// «Chiedi alla guida» (11/10/2026): invia la domanda, mostra la risposta e raccoglie 👍/👎.
// Vale per ogni riquadro .chiedi-guida della pagina (pannello laterale, elenco guide, guida a pagina intera).
for (const box of document.querySelectorAll('.chiedi-guida')) {
    const testo = box.querySelector('textarea');
    const invia = box.querySelector('.chiedi-invia');
    const risposta = box.querySelector('.chiedi-risposta');
    const voti = box.querySelector('.chiedi-voti');
    const grazie = box.querySelector('.chiedi-grazie');

    const chiedi = async () => {
        const domanda = testo.value.trim();
        if (domanda.length < 3) { testo.focus(); return; }
        invia.disabled = true;
        voti.hidden = true;
        risposta.hidden = false;
        risposta.classList.remove('errore');
        risposta.textContent = 'Sto cercando nelle guide…';
        try {
            const r = await fetch('/api/guide/chiedi', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ domanda, percorso: location.pathname })
            });
            const d = await r.json().catch(() => ({}));
            if (r.ok) {
                risposta.textContent = d.risposta;
                box.dataset.id = d.id;
                voti.hidden = false;
                grazie.textContent = '';
                voti.querySelectorAll('button').forEach(b => { b.disabled = false; b.classList.remove('scelto'); });
            } else {
                risposta.classList.add('errore');
                risposta.textContent = d.messaggio || 'In questo momento non riesco a rispondere: riprova tra poco.';
            }
        } catch {
            risposta.classList.add('errore');
            risposta.textContent = 'In questo momento non riesco a rispondere: riprova tra poco.';
        } finally {
            invia.disabled = false;
        }
    };

    invia.addEventListener('click', chiedi);
    testo.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); chiedi(); }
    });

    voti.querySelectorAll('button[data-voto]').forEach(b => b.addEventListener('click', async () => {
        if (!box.dataset.id) return;
        voti.querySelectorAll('button').forEach(x => { x.disabled = true; x.classList.toggle('scelto', x === b); });
        try {
            const r = await fetch('/api/guide/valuta', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: Number(box.dataset.id), voto: Number(b.dataset.voto) })
            });
            grazie.textContent = r.ok ? (b.dataset.voto === '1' ? 'Grazie!' : 'Grazie: la useremo per migliorare la guida.') : '';
        } catch { /* il voto non è essenziale */ }
    }));
}
