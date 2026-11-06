// Service worker per la pagina "Prelievo vocale": rende l'app installabile e funzionante offline.
// Strategia: network-first con cache di ripiego (così vedi sempre la versione aggiornata quando c'è
// rete, ma la pagina resta usabile offline). Attivo solo su HTTPS/localhost.
const CACHE = 'labai-prelievo-v4';
const BASE = [
  '/Prelievo',
  '/css/sito.css',
  '/js/moduli/prelievo.js',
  '/img/icon-192.png',
  '/img/icon-512.png',
  '/manifest.webmanifest'
];

self.addEventListener('install', e => {
  e.waitUntil(caches.open(CACHE).then(c => c.addAll(BASE)).catch(() => { }).then(() => self.skipWaiting()));
});

self.addEventListener('activate', e => {
  e.waitUntil(
    caches.keys().then(ks => Promise.all(ks.filter(k => k !== CACHE).map(k => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', e => {
  const req = e.request;
  if (req.method !== 'GET') return;
  e.respondWith(
    fetch(req)
      .then(res => { const cp = res.clone(); caches.open(CACHE).then(c => c.put(req, cp)); return res; })
      .catch(() => caches.match(req).then(m => m || caches.match('/Prelievo')))
  );
});
