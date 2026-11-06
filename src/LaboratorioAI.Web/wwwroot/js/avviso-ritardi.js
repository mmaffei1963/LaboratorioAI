// 22/10/2026: avviso campioni in ritardo. Nei giorni impostati (Ciclo analitico → Campioni in ritardo), alla prima pagina
// aperta nella sessione del browser, mostra quanti campioni sono in ritardo e il link all'elenco.
(async () => {
    const box = document.getElementById('avviso-ritardi');
    if (!box || location.pathname.toLowerCase().startsWith('/ritardicampioni')) return;
    const chiave = 'labai.avvisoRitardi.' + new Date().toISOString().slice(0, 10);
    try { if (sessionStorage.getItem(chiave) === '1') return; } catch { /* senza memoria di sessione si riprova */ }
    let d;
    try {
        const r = await fetch('/api/ritardi-campioni/avviso');
        if (!r.ok) return;
        d = await r.json();
    } catch { return; }
    if (!d.giornoAvviso || d.errore || !d.totale) return;
    try { sessionStorage.setItem(chiave, '1'); } catch { }
    const parti = [];
    if (d.parametri) parti.push(`${d.parametri} con parametri mancanti`);
    if (d.rapporto) parti.push(`${d.rapporto} con il rapporto di prova da emettere`);
    if (d.subappalto) parti.push(`${d.subappalto} in attesa del subappalto`);
    box.textContent = `${d.totale} campion${d.totale === 1 ? 'e' : 'i'} in ritardo: ${parti.join(', ')}. `;
    const a = document.createElement('a');
    a.href = '/RitardiCampioni'; a.textContent = 'Vedi l\'elenco';
    box.append(a);
    const x = document.createElement('button');
    x.type = 'button'; x.className = 'avviso-azw-chiudi'; x.textContent = '×'; x.title = 'Chiudi';
    x.addEventListener('click', () => { box.hidden = true; });
    box.append(x);
    box.classList.add('errore');
    box.hidden = false;
})();
