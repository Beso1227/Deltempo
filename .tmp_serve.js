// Temporary static server that mimics GitHub Pages (gzip + mime types).
// Used only for local Lighthouse A/B measurements. Delete before commit.
const http = require('http');
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const ROOT = path.join(__dirname, 'docs');
const PORT = process.env.PORT ? Number(process.env.PORT) : 8123;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json',
  '.xml': 'application/xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.gif': 'image/gif',
  '.ico': 'image/x-icon',
  '.svg': 'image/svg+xml',
  '.mp4': 'video/mp4',
  '.vtt': 'text/vtt; charset=utf-8',
  '.txt': 'text/plain; charset=utf-8',
  '.woff2': 'font/woff2',
};
const COMPRESSIBLE = new Set(['.html', '.css', '.js', '.json', '.xml', '.svg', '.txt', '.vtt']);

http
  .createServer((req, res) => {
    let p = decodeURIComponent(req.url.split('?')[0]);
    if (p.endsWith('/')) p += 'index.html';
    const file = path.join(ROOT, p);
    if (!file.startsWith(ROOT) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
      res.writeHead(404, { 'content-type': 'text/plain' });
      return res.end('not found');
    }
    const ext = path.extname(file).toLowerCase();
    const type = MIME[ext] || 'application/octet-stream'; // GH Pages: extensionless -> octet-stream
    const body = fs.readFileSync(file);
    const headers = {
      'content-type': type,
      'cache-control': 'max-age=600', // GH Pages always sends this
      'x-content-type-options': 'nosniff',
    };
    const acceptsGz = /\bgzip\b/.test(req.headers['accept-encoding'] || '');
    if (acceptsGz && COMPRESSIBLE.has(ext)) {
      const gz = zlib.gzipSync(body, { level: 6 });
      headers['content-encoding'] = 'gzip';
      headers['content-length'] = gz.length;
      res.writeHead(200, headers);
      return res.end(gz);
    }
    headers['content-length'] = body.length;
    res.writeHead(200, headers);
    res.end(body);
  })
  .listen(PORT, () => console.log('serving ' + ROOT + ' on http://localhost:' + PORT + '/'));
