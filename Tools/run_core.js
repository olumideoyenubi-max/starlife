// Runs the generated core under Node with a stub host; writes sample SVGs to Tools/test for comparing renderers.
const fs = require('fs'), path = require('path'), vm = require('vm');
const store = {};
const host = { get: k => (k in store ? store[k] : null), set: (k, v) => { store[k] = v; }, changed() {}, toast(m) { console.log('toast:', m); }, profile() {} };
const ctx = vm.createContext({ host, console });
vm.runInContext(fs.readFileSync(path.join(__dirname, '../Assets/Resources/starlife_core.txt'), 'utf8') + '\nthis.SL = SL;', ctx);
const SL = ctx.SL, out = (n, s) => fs.writeFileSync(path.join(__dirname, 'test', n + '.svg'), s.includes('xmlns') ? s : s.replace('<svg', '<svg xmlns="http://www.w3.org/2000/svg"'));
out('doll0', SL.doll());
SL.wearNow('blouse'); SL.wearNow('jeans'); SL.wearNow('blazer'); SL.wearNow('sneakers'); SL.wearNow('tote'); SL.wearNow('hoops'); SL.wearNow('beanie');
out('doll1', SL.doll()); out('face', SL.doll('140 84 120 160'));
out('roombg', SL.roomBg()); out('roomfull', SL.roomFull());
for (const k of ['window', 'sofa', 'rug', 'plant', 'boxes']) out('furn_' + k, SL.furn(k));
for (const id of ['tee', 'gown', 'sundress', 'sequin', 'trench', 'tote', 'pumps', 'breton']) out('thumb_' + id, SL.thumb(id));
out('portrait', SL.mePortrait()); out('hero', SL.hero());
const d = JSON.parse(SL.data()); console.log(d.items.length, 'items', d.furn.length, 'furn', JSON.parse(SL.profile()));
console.log(SL.enter('office').slice(0, 200)); console.log(SL.layers());
