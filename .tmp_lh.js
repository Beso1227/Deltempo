const fs = require('fs');
const r = JSON.parse(fs.readFileSync(process.env.TEMP + '\\lh_desktop.json', 'utf8'));
const m = r.audits;

console.log('=== NETWORK REQUESTS (chronological) ===');
const reqs = (m['network-requests'] && m['network-requests'].details.items) || [];
const base = Math.min(...reqs.map((q) => q.networkRequestTime || q.startTime || 0));
for (const q of reqs.sort((a, b) => (a.networkRequestTime || 0) - (b.networkRequestTime || 0))) {
  const start = ((q.networkRequestTime || 0) - base).toFixed(0);
  const end = ((q.networkEndTime || 0) - base).toFixed(0);
  console.log(
    '  ' + String(start).padStart(5) + '-' + String(end).padStart(5) + 'ms  ' +
    String(Math.round((q.transferSize || 0) / 1024)).padStart(4) + 'KB  ' +
    String(q.priority || '').padEnd(8) +
    String(q.resourceType || '').padEnd(10) +
    (q.renderBlocking ? '[' + q.renderBlocking + '] ' : '') +
    q.url.replace('https://beso1227.github.io/Deltempo', '~')
  );
}

for (const id of ['first-contentful-paint-element', 'largest-contentful-paint-element', 'lcp-discovery-insight', 'prioritize-lcp-image', 'lcp-lazy-loaded', 'critical-request-chains', 'bootup-time', 'dom-size', 'font-display', 'layout-shift-elements']) {
  const a = m[id];
  console.log('\n## ' + id + ' -> ' + (a ? 'score=' + a.score : 'ABSENT'));
  if (a && a.details) console.log('   ' + JSON.stringify(a.details.items).slice(0, 700));
}

console.log('\n=== PRECONNECT / NETWORK TREE ===');
const nd = m['network-dependency-tree-insight'];
if (nd && nd.details) {
  for (const it of nd.details.items) {
    if (it.type === 'list-section') {
      const v = it.value;
      console.log('\n-- ' + (it.title || v.type) + ' --');
      if (v.type === 'table' && v.items) {
        for (const row of v.items.slice(0, 12)) console.log('   ' + JSON.stringify(row));
      } else if (v.chains) {
        const walk = (n, d) => {
          console.log('   ' + '  '.repeat(d) + n.url.replace('https://beso1227.github.io/Deltempo', '~') + ' (' + (n.navStartToEndTime || '') + 'ms, ' + (n.transferSize || 0) + 'B)');
          Object.values(n.children || {}).forEach((c) => walk(c, d + 1));
        };
        Object.values(v.chains).forEach((c) => walk(c, 0));
      } else if (Array.isArray(v)) {
        v.forEach((x) => console.log('   ' + JSON.stringify(x)));
      } else {
        console.log('   ' + JSON.stringify(v).slice(0, 900));
      }
    }
  }
}

console.log('\n=== METRIC SAVINGS (insights) ===');
for (const [id, a] of Object.entries(m)) {
  if (a.details && a.details.overallSavingsMs) {
    console.log('  ' + id.padEnd(36) + ' score=' + a.score + '  ' + a.details.overallSavingsMs + 'ms  ' + ((a.details.overallSavingsBytes || 0) / 1024).toFixed(0) + 'KB');
  }
}
