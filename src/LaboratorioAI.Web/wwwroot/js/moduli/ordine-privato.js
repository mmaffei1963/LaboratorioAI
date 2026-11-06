// Ordine di Accettazione da firmare per i clienti privati (14/10/2026): aperto dal pulsante «Stampa da firmare» di ArcLab.
const $ = id => document.getElementById(id);
const btn = $('btn-stampa');

function scelta() { return document.querySelector('input[name="consegna"]:checked')?.value || '2'; }  // 1 = e-mail, 2 = a mano, 3 = entrambi

function aggiornaEmail() {
    const aMano = scelta() === '2';
    $('email').required = !aMano;
    $('email').closest('label').style.opacity = aMano ? '0.6' : '';
}

function msg(t, errore = false) { $('msg').textContent = t || ''; $('msg').style.color = errore ? '#b00' : ''; }

btn?.addEventListener('click', async () => {
    const email = $('email').value.trim();
    const consegna = scelta();
    if (consegna !== '2' && !email) { msg('Scrivi l\'e-mail oppure scegli «ritiro a mano».', true); $('email').focus(); return; }
    if (email && !$('email').checkValidity()) { msg('L\'indirizzo e-mail non sembra valido.', true); $('email').focus(); return; }
    // La finestra si apre subito (dentro il clic) per non essere bloccata dal browser; il PDF arriva dopo.
    const finestra = window.open('about:blank', '_blank');
    btn.disabled = true; msg('Preparazione del PDF…');
    try {
        const r = await fetch(`/api/ordini-accettazione/${btn.dataset.lotto}/privato`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: email || null, consegna: Number(consegna), firmato: $('firmato').checked })
        });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.messaggio || 'Errore');
        if (finestra) finestra.location.href = j.url; else window.location.href = j.url;
        msg(`Registrato (stampa n. ${j.stampe}). Stampa il PDF dalla scheda che si è aperta e fallo firmare al cliente.`);
    } catch (e) {
        finestra?.close();
        msg(e.message, true);
    } finally { btn.disabled = false; }
});

document.querySelectorAll('input[name="consegna"]').forEach(r => r.addEventListener('change', aggiornaEmail));
if (btn) aggiornaEmail();
