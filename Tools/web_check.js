// Serves Builds/Web, opens it in headless Chrome and saves screenshots (desktop and phone sizes) to the given folder.
//   node Tools/web_check.js <outDir>
const { spawn } = require('child_process'), path = require('path'), fs = require('fs');
const out = path.resolve(process.argv[2] || 'web-shots'); fs.mkdirSync(out, { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  const srv = spawn('python3', ['-m', 'http.server', '8765', '--bind', '127.0.0.1'], { cwd: path.join(__dirname, '..', 'Builds', 'Web'), stdio: 'ignore' });
  const prof = fs.mkdtempSync('/tmp/sl-chrome-');
  const chrome = spawn('/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', ['--headless=new', '--remote-debugging-port=9333', '--user-data-dir=' + prof, '--enable-unsafe-swiftshader', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--window-size=1440,900', 'about:blank'], { stdio: 'ignore' });
  try {
    let tabs; for (let i = 0; i < 40; i++) { await sleep(500); try { tabs = await (await fetch('http://127.0.0.1:9333/json')).json(); break; } catch {} }
    const ws = new WebSocket(tabs.find(t => t.type === 'page').webSocketDebuggerUrl); await new Promise(r => ws.onopen = r);
    let id = 0; const wait = {}; const logs = [];
    ws.onmessage = e => { const m = JSON.parse(e.data); if (m.id && wait[m.id]) wait[m.id](m.result || m.error); if (m.method === 'Runtime.consoleAPICalled') logs.push(m.params.args.map(a => a.value || a.description || '').join(' ').slice(0, 300)); if (m.method === 'Runtime.exceptionThrown') logs.push('EXCEPTION ' + JSON.stringify(m.params.exceptionDetails).slice(0, 400)); };
    const send = (method, params = {}) => new Promise(r => { wait[++id] = r; ws.send(JSON.stringify({ id, method, params })); });
    const shot = async name => { const r = await send('Page.captureScreenshot', { format: 'png' }); fs.writeFileSync(path.join(out, name + '.png'), Buffer.from(r.data, 'base64')); };
    const click = async (x, y) => { for (const type of ['mousePressed', 'mouseReleased']) await send('Input.dispatchMouseEvent', { type, x, y, button: 'left', clickCount: 1 }); };
    await send('Runtime.enable'); await send('Page.enable');
    for (const [name, w, h, go] of [['desktop', 1440, 900, [720, 856]], ['phone', 430, 900, [215, 866]]]) {
      await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
      await send('Page.navigate', { url: 'http://127.0.0.1:8765/index.html?' + name });
      await sleep(name === "desktop" ? 45000 : 30000); await shot(name + '-1-picker');
      await click(go[0], go[1]); await sleep(15000); await shot(name + '-2-suite');
    }
    const bad = logs.filter(l => /error|exception|could not|abort/i.test(l)); fs.writeFileSync(path.join(out, 'console.log'), logs.join('\n')); console.log(bad.slice(0, 12).join('\n') || 'no errors in the browser console');
  } finally { chrome.kill(); srv.kill(); }
  process.exit(0);
})();
