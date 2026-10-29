// Builds Starlife Town for Unity from the web game's own town (Tools/town/world.html, copied from the artifact):
// opens it in headless Chrome with dump.js injected, lets it build, and writes
//   Assets/Resources/Town/towndata.json   gameplay data, materials and the mesh table
//   Assets/Resources/Town/towngeo.bytes   merged geometry (see dump.js for the layout)
//   Assets/Resources/Town/Tex/*.png       the painted textures (facades, signs, posters, ground)
// Usage: node Tools/town/export.js        (needs Google Chrome and an internet connection for three.js and the fonts)
const { spawn } = require('child_process'), path = require('path'), fs = require('fs');
const here = __dirname, outDir = path.join(here, '..', '..', 'Assets', 'Resources', 'Town');
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  // a copy of the page with the dump code inside its module, so it can see the town's variables
  const build = path.join(here, '.build'); fs.mkdirSync(build, { recursive: true });
  const page = fs.readFileSync(path.join(here, 'world.html'), 'utf8');
  const at = page.indexOf('// test hook'); if (at < 0) throw new Error('world.html has no test hook line');
  fs.writeFileSync(path.join(build, 'world.html'), page.slice(0, at) + fs.readFileSync(path.join(here, 'dump.js'), 'utf8') + '\n' + page.slice(at));
  const srv = spawn('python3', ['-m', 'http.server', '8766', '--bind', '127.0.0.1'], { cwd: build, stdio: 'ignore' });
  const prof = fs.mkdtempSync('/tmp/sl-town-');
  const chrome = spawn('/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', ['--headless=new', '--remote-debugging-port=9334', '--user-data-dir=' + prof, '--enable-unsafe-swiftshader', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--window-size=1280,800', 'about:blank'], { stdio: 'ignore' });
  try {
    let tabs; for (let i = 0; i < 40; i++) { await sleep(500); try { tabs = await (await fetch('http://127.0.0.1:9334/json')).json(); break; } catch {} }
    const ws = new WebSocket(tabs.find(t => t.type === 'page').webSocketDebuggerUrl); await new Promise(r => ws.onopen = r);
    let id = 0; const wait = {}; const logs = [];
    ws.onmessage = e => { const m = JSON.parse(e.data); if (m.id && wait[m.id]) wait[m.id](m.result || m.error); if (m.method === 'Runtime.consoleAPICalled') logs.push(m.params.args.map(a => a.value || a.description || '').join(' ').slice(0, 400)); if (m.method === 'Runtime.exceptionThrown') logs.push('EXCEPTION ' + JSON.stringify(m.params.exceptionDetails).slice(0, 600)); };
    const send = (method, params = {}) => new Promise(r => { wait[++id] = r; ws.send(JSON.stringify({ id, method, params })); });
    const ev = async (expr, awaitPromise = false) => { const r = await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise }); if (r.exceptionDetails) throw new Error(JSON.stringify(r.exceptionDetails).slice(0, 800)); return r.result && r.result.value; };
    await send('Runtime.enable'); await send('Page.enable');
    await send('Page.navigate', { url: 'http://127.0.0.1:8766/world.html' });
    let ready = false;
    for (let i = 0; i < 240 && !ready; i++) { await sleep(1000); ready = await ev("!!(window.__town && window.__export && !document.querySelector('#loading'))").catch(() => false); }
    if (!ready) throw new Error('the town did not finish loading:\n' + logs.join('\n'));
    const info = await ev('window.__export()', true);
    console.log('exported', JSON.stringify(info));
    const len = await ev('window.__out.bin.length'); let b64 = '';
    for (let k = 0; k < len; k += 4e6) b64 += await ev(`window.__out.bin.slice(${k}, ${k + 4e6})`);
    const json = await ev('window.__out.json'), tex = JSON.parse(await ev('JSON.stringify(Object.keys(window.__out.tex))'));
    fs.mkdirSync(path.join(outDir, 'Tex'), { recursive: true });
    for (const f of fs.readdirSync(path.join(outDir, 'Tex'))) if (f.endsWith('.png')) fs.unlinkSync(path.join(outDir, 'Tex', f));
    for (const t of tex) { const url = await ev(`window.__out.tex[${JSON.stringify(t)}]`); if (url) fs.writeFileSync(path.join(outDir, 'Tex', t + '.png'), Buffer.from(url.split(',')[1], 'base64')); }
    fs.writeFileSync(path.join(outDir, 'towndata.json'), json);
    fs.writeFileSync(path.join(outDir, 'towngeo.bytes'), Buffer.from(b64, 'base64'));
    const bad = logs.filter(l => /error|exception/i.test(l)); if (bad.length) console.log(bad.slice(0, 10).join('\n'));
  } finally { chrome.kill(); srv.kill(); }
  process.exit(0);
})().catch(e => { console.error(e); process.exit(1); });
