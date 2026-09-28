const { spawn, execFileSync } = require('child_process');
const path = require('path');
const ROOT = __dirname;
const server = spawn('node', [path.join(ROOT, '.tmp_serve.js')], {
  env: { ...process.env, PORT: '8123' },
  stdio: ['ignore', 'pipe', 'pipe'],
});
server.stdout.on('data', (d) => process.stdout.write('[serve] ' + d));
server.stderr.on('data', (d) => process.stderr.write('[serve-err] ' + d));
const chrome = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
(async () => {
  await wait(1500);
  try {
    const out = execFileSync(
      chrome,
      [
        '--headless=new',
        '--no-sandbox',
        '--disable-gpu',
        '--virtual-time-budget=9000',
        '--dump-dom',
        'http://localhost:8123/__fontprobe.html',
      ],
      { maxBuffer: 64 * 1024 * 1024, timeout: 60000, encoding: 'utf8' }
    );
    const m = out.match(/<pre id="out">([\s\S]*?)<\/pre>/);
    console.log('=== PROBE RESULT ===');
    console.log(m ? m[1] : out.slice(0, 3000));
  } catch (e) {
    console.error('chrome failed:', e.message);
    if (e.stdout) console.log(String(e.stdout).slice(0, 2000));
  }
  server.kill();
  process.exit(0);
})();
