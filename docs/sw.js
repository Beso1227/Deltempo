// Deltempo Documentation & Landing Page PWA Service Worker
const CACHE_NAME = 'deltempo-cache-v3.9.47';

const PRECACHE_ASSETS = [
  './',
  'index.html',
  // The stylesheet is NOT precached, and that is deliberate: every page
  // carries the styles inline (see scripts/build_site.js), so no page ever
  // requests the file. Precaching it made every visitor download 115 KB of
  // CSS that nothing would read - a page that inlines its styles is already
  // complete in the HTML the browser was given.
  'fonts/plus-jakarta-sans-latin.woff2',
  'fonts/plus-jakarta-sans-latin-ext.woff2',
  'fonts/jetbrains-mono-latin.woff2',
  'fonts/jetbrains-mono-latin-ext.woff2',
  'vendor/lenis.min.js',
  'motion.js?v=3.9.47',
  'manifest.json?v=3.9.47',
  'favicon.ico',
  'favicon-32.png',
  'favicon-48.png',
  'favicon-64.png',
  'app_icon-64.png',
  'app_icon-192.png',
  'apple-touch-icon.png',
  'docs/',
  'download/',
  'changelog/',
  'faq/',
  'compare/',
  'alternatives/',
  'privacy.html'
];

// Install Event: Precaching App Shell
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(PRECACHE_ASSETS).catch((err) => {
        console.warn('Pre-cache item failed, continuing:', err);
      });
    }).then(() => self.skipWaiting())
  );
});

// Activate Event: Cleanup Stale Caches
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((cacheNames) => {
      return Promise.all(
        cacheNames.map((name) => {
          if (name !== CACHE_NAME) {
            return caches.delete(name);
          }
        })
      );
    }).then(() => self.clients.claim())
  );
});

// Fetch Event: Stale-While-Revalidate for Static Assets, Network-First for Navigations
self.addEventListener('fetch', (event) => {
  // Only handle GET requests
  if (event.request.method !== 'GET') return;

  const url = new URL(event.request.url);

  // Same-origin only: every asset the site uses is self-hosted.
  if (url.origin !== self.location.origin) {
    return;
  }

  // Navigation requests: Network-First with Cache Fallback
  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request)
        .then((response) => {
          if (response && response.status === 200) {
            const responseClone = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(event.request, responseClone));
          }
          return response;
        })
        .catch(() => {
          return caches.match(event.request).then((cached) => {
            return cached || caches.match('index.html');
          });
        })
    );
    return;
  }

  // Static Assets: Stale-While-Revalidate
  event.respondWith(
    caches.match(event.request).then((cachedResponse) => {
      const fetchPromise = fetch(event.request).then((networkResponse) => {
        if (networkResponse && networkResponse.status === 200) {
          const responseClone = networkResponse.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(event.request, responseClone));
        }
        return networkResponse;
      }).catch(() => cachedResponse);

      return cachedResponse || fetchPromise;
    })
  );
});
