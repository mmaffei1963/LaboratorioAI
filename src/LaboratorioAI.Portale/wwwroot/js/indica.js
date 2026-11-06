// Pagina «Indica il Firmatario» (11/10/2026): invio dei dati della persona che approverà le Condizioni.
const $ = (id) => document.getElementById(id);
const pagina = $('pagina');
const modulo = $('modulo');

if (modulo) {
    $('f-qualifica').addEventListener('change', () =>
        $('campo-qualifica-altro').classList.toggle('nascosto', $('f-qualifica').value !== 'altro'));

    const errore = (t) => { $('errore').textContent = t || ''; $('errore').classList.toggle('nascosto', !t); };

    async function invia(confermaGenerica = false) {
        const q = $('f-qualifica').value === 'altro' ? $('f-qualifica-altro').value.trim() : $('f-qualifica').value;
        const email = $('f-email').value.trim();
        if (!$('f-nome').value.trim() || !$('f-cognome').value.trim() || !q) return errore('Indicate nome, cognome e ruolo.');
        if (!email) return errore("Indicate l'e-mail della persona.");
        if (email.toLowerCase() !== $('f-email2').value.trim().toLowerCase()) return errore('Le due e-mail non coincidono.');
        errore('');
        const b = $('btn-invia'); b.disabled = true; b.textContent = 'Invio…';
        try {
            const r = await fetch(`/Indica/${encodeURIComponent(pagina.dataset.codice)}`, {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                body: JSON.stringify({ nome: $('f-nome').value.trim(), cognome: $('f-cognome').value.trim(), qualifica: q, email, confermaGenerica })
            });
            const d = await r.json().catch(() => ({}));
            if (!r.ok) throw new Error(d.messaggio || `Errore ${r.status}`);
            if (d.richiedeConferma) {
                b.disabled = false; b.textContent = "Invia l'indicazione";
                if (confirm(d.messaggio)) return invia(true);
                return;
            }
            modulo.classList.add('nascosto');
            $('esito-testo').textContent = d.messaggio;
            $('esito').classList.remove('nascosto');
        } catch (e) {
            errore(e.message);
            b.disabled = false; b.textContent = "Invia l'indicazione";
        }
    }
    $('btn-invia').addEventListener('click', () => invia());
}
