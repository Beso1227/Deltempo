const fs = require('fs');
const path = require('path');
const vm = require('vm');

const ROOT = path.join(__dirname, 'docs');
const pages = [
  'index.html',
  'download/index.html',
  'changelog/index.html',
  'docs/index.html',
  'faq/index.html',
];

let errors = 0;
const fail = (m) => { errors++; console.log('  FAIL ' + m); };

for (const rel of pages) {
  const file = path.join(ROOT, rel);
  const html = fs.readFileSync(file, 'utf8');
  console.log('\n== ' + rel);

  // 1. JSON-LD blocks must parse
  const ld = [...html.matchAll(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/g)];
  ld.forEach((m, i) => {
    try { JSON.parse(m[1]); } catch (e) { fail('JSON-LD #' + i + ': ' + e.message); }
  });
  console.log('  JSON-LD blocks: ' + ld.length + ' parsed');

  // 2. Inline scripts must be syntactically valid (skip JSON-LD + templates)
  const scripts = [...html.matchAll(/<script(\b[^>]*)>([\s\S]*?)<\/script>/g)];
  let checked = 0;
  for (const s of scripts) {
    const attrs = s[1] || '';
    const body = s[2];
    if (/application\/ld\+json/.test(attrs)) continue;
    if (/\bsrc\s*=/.test(attrs)) continue;
    if (!body.trim()) continue;
    checked++;
    try {
      new vm.Script(body, { filename: rel + ' (inline)' });
    } catch (e) {
      fail('inline JS syntax: ' + e.message);
    }
  }
  console.log('  inline scripts checked: ' + checked);

  // 3. The early-reveal script must be present and target a real class
  if (!/querySelectorAll\('\.reveal-on-scroll'\)/.test(html)) fail('early-reveal script missing');
  if (/class="[^"]*reveal-on-scroll/.test(html) && !/classList\.add\('revealed'\)/.test(html)) {
    fail('has reveal-on-scroll but no reveal script');
  }

  // 4. Every getElementById target must exist
  const ids = [...html.matchAll(/getElementById\('([^']+)'\)/g)].map((m) => m[1]);
  const present = new Set([...html.matchAll(/\bid="([^"]+)"/g)].map((m) => m[1]));
  for (const id of new Set(ids)) if (!present.has(id)) fail('missing element id: #' + id);
  console.log('  getElementById targets resolved: ' + new Set(ids).size);

  // 5. No leftover winget refs / stale poster
  if (/winget/i.test(html)) fail('contains winget reference');
}

// 6. CSS integrity
const css = fs.readFileSync(path.join(ROOT, 'style.css'), 'utf8');
console.log('\n== style.css');
const open = (css.match(/\{/g) || []).length;
const close = (css.match(/\}/g) || []).length;
if (open !== close) fail('brace mismatch: ' + open + ' open vs ' + close + ' close');
console.log('  braces: ' + open + '/' + close);
if (css.charCodeAt(0) === 0xfeff) fail('unexpected BOM');
console.log('  BOM: none');
if (!/--text-low: #7C8CA1;/.test(css)) fail('--text-low token not updated');
if (/--text-low: #475569;\s*\n\s*--accent/.test(css)) console.log('  (light theme keeps #475569 - expected)');

// 7. Poster file exists and is a real WebP
const poster = path.join(ROOT, 'deltempo-poster.webp');
if (!fs.existsSync(poster)) fail('deltempo-poster.webp missing');
else {
  const b = fs.readFileSync(poster);
  const sig = b.slice(0, 4).toString('latin1') === 'RIFF' && b.slice(8, 12).toString('latin1') === 'WEBP';
  if (!sig) fail('poster is not a valid WebP');
  console.log('\n== deltempo-poster.webp: ' + b.length + ' bytes, valid WebP=' + sig);
}

console.log('\n' + (errors === 0 ? 'ALL CHECKS PASSED' : errors + ' FAILURE(S)'));
process.exit(errors ? 1 : 0);
