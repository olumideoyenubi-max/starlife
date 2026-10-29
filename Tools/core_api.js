// ---------------------------------------------------------------------
// Host bridge for the Unity port. The page-rendering functions are replaced
// with calls to the host, and SL exposes what the native UI needs.
// ---------------------------------------------------------------------
update = function (pop) { record(); UID = 0; lsSet(LS_CUR, state); host.changed(pop ? 1 : 0); };
toast = function (msg) { host.toast(String(msg)); };
renderTopbar = renderShop = renderContests = renderLookbook = renderHome = renderSuite = renderParlor = function () {};
renderGrid = renderDecor = renderLayers = refreshUI = renderEvents = renderPlay = renderPrepBar = renderChat = renderChatBadge = function () {};
showResult = function () {};
saveProfile = function () { lsSet(LS_PROFILE, profile); host.profile(); };
reward = function (coins, fame) {
  const before = level();
  profile.coins += coins; profile.fame += fame;
  saveProfile();
  if (level() > before) host.toast(`Level up! ${starletName()} is now level ${level()} ✦`);
};

// More starlets than the artifact has: each has a 3D avatar (Assets/Starlets/<id>) and a drawn look for the 2D game.
[
  { id: 'charlotte', name: 'Charlotte Rose', role: 'Ballerina', skin: '#f6cfb3', hair: 'waves', hairColor: '#f2a7c3', eyeShape: 'doe', eyeColor: '#4a78b5', brow: 'thin', preset: 'Soft Glam', underwear: '#f4c7d3', bg: 'blush', fame: 64 },
  { id: 'jenny', name: 'Jenny Vale', role: 'Surfer', skin: '#f6cfb3', hair: 'bob', hairColor: '#b9b3ad', eyeShape: 'almond', eyeColor: '#4f7a45', brow: 'natural', preset: 'Natural', underwear: '#e9c46a', bg: 'garden', fame: 41 },
  { id: 'lina', name: 'Lina Reyes', role: 'Cheer captain', skin: '#c28552', hair: 'bun', hairColor: '#e8623a', eyeShape: 'upturned', eyeColor: '#3b2414', brow: 'arched', preset: 'Soft Glam', underwear: '#d9475e', bg: 'runway', fame: 57 },
  { id: 'lucy', name: 'Lucy Okoro', role: 'Sprinter', skin: '#5a3420', hair: 'long', hairColor: '#f4f1ec', eyeShape: 'almond', eyeColor: '#3b2414', brow: 'bold', preset: 'Natural', underwear: '#a9c6ef', bg: 'runway', fame: 92 },
  { id: 'mary', name: 'Mary Adeyemi', role: 'DJ', skin: '#5a3420', hair: 'pixie', hairColor: '#8fd14f', eyeShape: 'round', eyeColor: '#3b2414', brow: 'straight', preset: 'Editorial', underwear: '#1f1b22', bg: 'night', fame: 70 },
  { id: 'olivia', name: 'Olivia Mensah', role: 'Swimmer', skin: '#5a3420', hair: 'bun', hairColor: '#4fb6e8', eyeShape: 'doe', eyeColor: '#3b2414', brow: 'natural', preset: 'Natural', underwear: '#a9c6ef', bg: 'garden', fame: 38 },
  { id: 'rosie', name: 'Rosie Park', role: 'Vlogger', skin: '#f6cfb3', hair: 'bun', hairColor: '#f2a7c3', eyeShape: 'round', eyeColor: '#4a78b5', brow: 'feathered', preset: 'Soft Glam', underwear: '#f4c7d3', bg: 'boutique', fame: 51 },
  { id: 'songbird', name: 'Melody Song', role: 'Singer', skin: '#fde3d3', hair: 'long', hairColor: '#f09ad0', eyeShape: 'almond', eyeColor: '#8a6b35', brow: 'arched', preset: 'Night Out', underwear: '#ffffff', bg: 'night', fame: 88 },
  { id: 'eve', name: 'Eve Seraphine', role: 'Fantasy queen', skin: '#fde3d3', hair: 'waves', hairColor: '#e9e6f2', eyeShape: 'upturned', eyeColor: '#4a78b5', brow: 'arched', preset: 'Editorial', underwear: '#e9c46a', bg: 'boutique', fame: 29 },
  { id: 'cute', name: 'Coco Banks', role: 'Stylist', skin: '#fde3d3', hair: 'bob', hairColor: '#1c1412', eyeShape: 'doe', eyeColor: '#3b2414', brow: 'straight', preset: 'Night Out', underwear: '#1f1b22', bg: 'boutique', fame: 45 },
  // the men (generated with MPFB by Tools/blender/make_men.py; in the drawn game they borrow the women's drawing)
  { id: 'kai', name: 'Kai Mensah', role: 'Musician', skin: '#5a3420', hair: 'pixie', hairColor: '#1c1412', eyeShape: 'almond', eyeColor: '#3b2414', brow: 'bold', preset: 'Natural', underwear: '#1f1b22', bg: 'night', fame: 66, gender: 'm' },
  { id: 'leo', name: 'Leo Castillo', role: 'Actor', skin: '#c28552', hair: 'pixie', hairColor: '#3b2416', eyeShape: 'almond', eyeColor: '#3b2414', brow: 'straight', preset: 'Natural', underwear: '#2b3a67', bg: 'runway', fame: 74, gender: 'm' },
  { id: 'jin', name: 'Jin Takeda', role: 'Dancer', skin: '#f6cfb3', hair: 'pixie', hairColor: '#1c1412', eyeShape: 'almond', eyeColor: '#3b2414', brow: 'straight', preset: 'Natural', underwear: '#ffffff', bg: 'night', fame: 58, gender: 'm' },
  { id: 'theo', name: 'Theo Brooks', role: 'Athlete', skin: '#a5683d', hair: 'pixie', hairColor: '#1c1412', eyeShape: 'round', eyeColor: '#3b2414', brow: 'bold', preset: 'Natural', underwear: '#3f7f6b', bg: 'garden', fame: 81, gender: 'm' },
  { id: 'omar', name: 'Omar Haddad', role: 'Chef', skin: '#d9a273', hair: 'pixie', hairColor: '#2a1a12', eyeShape: 'almond', eyeColor: '#3b2414', brow: 'bold', preset: 'Natural', underwear: '#c8b48c', bg: 'boutique', fame: 47, gender: 'm' },
  { id: 'pigtail', name: 'Ruby Tanaka', role: 'Executive', skin: '#fde3d3', hair: 'ponytail', hairColor: '#8f3b3b', eyeShape: 'round', eyeColor: '#8a6b35', brow: 'natural', preset: 'Natural', underwear: '#c8b6e2', bg: 'runway', fame: 36 },
].forEach(function (x) { if (STARLETS.some(function (y) { return y.id === x.id; })) return; STARLETS.push(x); STARLET_FAME[x.id] = x.fame; if (typeof STARLET_BY !== 'undefined') STARLET_BY[x.id] = x; });

// A starlet of the player's own: her face, outfit, height and name are chosen in the Beauty Parlor (3D).
(function (x) { STARLETS.push(x); STARLET_FAME[x.id] = 0; if (typeof STARLET_BY !== 'undefined') STARLET_BY[x.id] = x; })(
  { id: 'custom', name: 'My Starlet', role: 'Rising star', skin: '#c28552', hair: 'bun', hairColor: '#e8623a', eyeShape: 'almond', eyeColor: '#3b2414', brow: 'natural', preset: 'Natural', underwear: '#f4c7d3', bg: 'blush' });

// More wardrobe: sports gear and workwear. In the drawn game each borrows the drawing of an existing piece; in 3D
// they have pieces of their own (SLWardrobe3D.cs). [id, name, slot, tab, drawn like, colour, price, tags]
[
  ['sportsbra', 'Sports Bra', 'top', 'sport', 'cami', '#e8478c', 25, ['sport', 'summer']],
  ['gymtank', 'Gym Tank', 'top', 'sport', 'tee', '#4fb6e8', 20, ['sport', 'casual']],
  ['leggings', 'Power Leggings', 'bottom', 'sport', 'jeans', '#2b2138', 30, ['sport', 'casual']],
  ['gymshorts', 'Running Shorts', 'bottom', 'sport', 'shorts', '#f5b82e', 25, ['sport', 'summer']],
  ['tennisskirt', 'Tennis Skirt', 'bottom', 'sport', 'mini', '#ffffff', 35, ['sport', 'summer', 'chic']],
  ['tracktop', 'Track Jacket', 'outer', 'sport', 'denimjk', '#7c4ee0', 45, ['sport', 'casual']],
  ['trainers', 'Gym Trainers', 'shoes', 'sport', 'sneakers', '#ffffff', 30, ['sport', 'casual']],
  ['runners', 'Speed Runners', 'shoes', 'sport', 'sneakers', '#3fcf8e', 40, ['sport']],
  ['rainbowhoodie', 'Rainbow Hoodie', 'outer', 'sport', 'cardigan', '#b8a6f0', 40, ['sport', 'casual']],
  ['joggers', 'Joggers', 'bottom', 'sport', 'wide', '#2b2f38', 30, ['sport', 'casual']],
  ['hightops', 'High-tops', 'shoes', 'sport', 'sneakers', '#2b2f38', 35, ['sport', 'casual', 'edgy']],
  ['staffpolo', 'Staff Polo', 'top', 'work', 'tee', '#2f6f9f', 15, ['work', 'casual']],
  ['scrubtop', 'Scrub Top', 'top', 'work', 'tee', '#6fc3c0', 15, ['work']],
  ['scrubpants', 'Scrub Trousers', 'bottom', 'work', 'wide', '#6fc3c0', 15, ['work']],
  ['workpants', 'Work Trousers', 'bottom', 'work', 'wide', '#3a3f4a', 15, ['work', 'office']],
  ['chefcoat', 'Chef Jacket', 'outer', 'work', 'blazer', '#ffffff', 20, ['work']],
  ['workboots', 'Work Boots', 'shoes', 'work', 'ankleboots', '#5b3b2a', 20, ['work', 'winter']],
  // menswear (Starlife Town's Hartwell & Sons and the male starlets); in 3D the men wear their own avatars' pieces
  ['shirt', 'Poplin Shirt', 'top', 'men', 'blouse', '#dfe8f5', 30, ['office', 'chic']],
  ['chinos', 'Chinos', 'bottom', 'men', 'wide', '#c8b48c', 35, ['casual', 'office']],
  ['suitpants', 'Suit Trousers', 'bottom', 'men', 'wide', '#1f2a44', 45, ['office', 'chic']],
  ['suit', 'Suit Jacket', 'outer', 'men', 'blazer', '#1f2a44', 90, ['office', 'chic', 'glam']],
  ['tux', 'Tuxedo Jacket', 'outer', 'men', 'blazer', '#141218', 140, ['glam', 'night', 'redcarpet']],
  ['tie', 'Silk Tie', 'necklace', 'men', 'pendant', '#8c1c2c', 20, ['office', 'chic']],
  ['bowtie', 'Bow Tie', 'necklace', 'men', 'pendant', '#141218', 20, ['glam', 'night', 'redcarpet']],
  ['oxfords', 'Oxford Shoes', 'shoes', 'men', 'flats', '#3b2418', 60, ['office', 'chic', 'glam']],
  ['loafers', 'Loafers', 'shoes', 'men', 'flats', '#7a4a2a', 50, ['chic', 'casual']],
  ['cap', 'Baseball Cap', 'hat', 'men', 'beanie', '#2f5d8c', 20, ['casual', 'sport']],
  // Ankara & Prints: the fabric keeps its print in 3D (Tools/blender/make_fashion.py)
  ['ankaramaxi', 'Ankara Maxi Dress', 'dress', 'prints', 'gown', '#e0782f', 160, ['glam', 'summer', 'chic']],
  ['ankaracorset', 'Ankara Corset', 'top', 'prints', 'corset', '#c8344a', 90, ['night', 'chic', 'edgy']],
  ['leopardcami', 'Leopard Silk Cami', 'top', 'prints', 'cami', '#c9a24a', 70, ['night', 'chic']],
  ['silkcarre', 'Silk Carré Blouse', 'top', 'prints', 'blouse', '#f1e6d2', 95, ['office', 'chic']],
  ['discotop', 'Disco Top', 'top', 'prints', 'cami', '#b9bcc2', 75, ['night', 'glam']],
  ['quilted', 'Quilted Mini Dress', 'dress', 'prints', 'lbd', '#2b3a67', 120, ['winter', 'chic']],
  // New Season
  ['haltermaxi', 'Halter Maxi Dress', 'dress', 'season', 'gown', '#a3122e', 140, ['glam', 'redcarpet', 'night']],
  ['shiftdress', 'Shift Dress', 'dress', 'season', 'lbd', '#f2c6d2', 85, ['office', 'chic']],
  ['keyholedress', 'Keyhole Dress', 'dress', 'season', 'lbd', '#1f6b4f', 95, ['night', 'chic']],
  ['sixties', 'Sixties Dress', 'dress', 'season', 'sundress', '#f6c453', 80, ['summer', 'casual']],
  ['midihalter', 'Halter Midi Dress', 'dress', 'season', 'slip', '#d8c39a', 110, ['glam', 'summer']],
  ['laceup', 'Lace-up Blouse', 'top', 'season', 'blouse', '#ffffff', 60, ['casual', 'chic']],
  ['peasant', 'Peasant Blouse', 'top', 'season', 'blouse', '#f4e9d8', 55, ['summer', 'casual']],
  ['offshoulder', 'Off-shoulder Top', 'top', 'season', 'breton', '#1f1b22', 50, ['night', 'casual']],
  ['tubetop', 'Tube Top', 'top', 'season', 'cami', '#f07aa8', 35, ['summer', 'night']],
  ['highneck', 'High-neck Crop', 'top', 'season', 'turtle', '#f2efe8', 40, ['casual', 'edgy']],
  ['croptop', 'Sleeveless Crop', 'top', 'season', 'tee', '#a98ad6', 30, ['summer', 'casual']],
  ['vneck', 'V-neck Top', 'top', 'season', 'tee', '#24407a', 35, ['casual', 'office']],
  ['buttertop', 'Butter Off-shoulder', 'top', 'season', 'breton', '#f3df9a', 65, ['summer', 'chic']],
  ['sagehalter', 'Sage Halter', 'top', 'season', 'cami', '#a7b89a', 55, ['summer', 'chic']],
  ['cobalttank', 'Cobalt Tank', 'top', 'season', 'tee', '#2b4fb8', 30, ['casual', 'summer']],
  ['bootcut', 'Bootcut Jeans', 'bottom', 'season', 'jeans', '#4f6d9a', 60, ['casual']],
  ['leather', 'Leather Trousers', 'bottom', 'season', 'jeans', '#151418', 120, ['night', 'edgy', 'chic']],
  ['harem', 'Harem Pants', 'bottom', 'season', 'wide', '#c8a979', 55, ['summer', 'casual']],
  ['trousers', 'Tailored Trousers', 'bottom', 'season', 'wide', '#3a3f4a', 70, ['office', 'chic']],
  ['womensuit', 'Tailored Suit', 'dress', 'season', 'lbd', '#1f2a44', 220, ['office', 'chic', 'glam']],
  ['casualsuit', 'Soft Suit', 'dress', 'season', 'lbd', '#c8b6e2', 150, ['office', 'casual']],
  // the goth collection (its accessories are made for 3D by Tools/blender/make_goth.py)
  ['gothgown', 'Fallen Angel Gown', 'dress', 'goth', 'gown', '#18151a', 110, ['goth', 'glam', 'night']],
  ['gothcoat', 'Midnight Trench', 'outer', 'goth', 'trench', '#18151a', 85, ['goth', 'winter', 'edgy']],
  ['harness', 'Chequered Crop Top', 'top', 'goth', 'corset', '#2a1c22', 45, ['goth', 'edgy', 'night']],
  ['meshtop', 'Fishnet Top', 'top', 'goth', 'cami', '#18151a', 35, ['goth', 'edgy', 'night']],
  ['kilt', 'Studded Kilt and Fishnets', 'bottom', 'goth', 'mini', '#7a1222', 60, ['goth', 'edgy']],
  ['platforms', 'Buckle Platform Boots', 'shoes', 'goth', 'kneeboots', '#18151a', 70, ['goth', 'edgy', 'winter']],
  ['creepers', 'Creeper Boots', 'shoes', 'goth', 'ankleboots', '#18151a', 45, ['goth', 'edgy']],
  ['spikecollar', 'Spiked Collar and Cuffs', 'necklace', 'goth', 'choker', '#18151a', 40, ['goth', 'edgy']],
  ['crosschain', 'Cross Pendant', 'necklace', 'goth', 'pendant', '#c9ccd6', 35, ['goth', 'chic']],
  ['horns', 'Devil Horns', 'hat', 'goth', 'tiara', '#7a1222', 30, ['goth', 'night']],
  ['catears', 'Cat-ear Band', 'hat', 'goth', 'tiara', '#18151a', 30, ['goth', 'casual']],
  ['crossear', 'Cross Earrings', 'earrings', 'goth', 'drops', '#c9ccd6', 25, ['goth', 'edgy']],
  ['coffinbag', 'Coffin Bag', 'bag', 'goth', 'crossbody', '#18151a', 55, ['goth', 'edgy']],
].forEach(function (e) {
  if (ITEM[e[0]]) return;     // the artifact has it already (its own drawing wins)
  var it = Object.assign({}, ITEM[e[4]], { id: e[0], name: e[1], slot: e[2], cat: e[3], color: e[5], group: '' });
  ITEMS.push(it); ITEM[it.id] = it; PRICES[it.id] = e[6]; TAGS[it.id] = e[7];
});
TAG_LABEL.sport = 'Sporty'; TAG_LABEL.men = 'Menswear'; TAG_LABEL.prints = 'Ankara & Prints'; TAG_LABEL.season = 'New Season';
// the artifact's menswear goes under the Menswear tab of the wardrobe and the shop
['shirt', 'polo', 'hoodie', 'chinos', 'suitpants', 'suit', 'tux', 'tie', 'bowtie', 'oxfords', 'loafers', 'cap'].forEach(function (id) { if (ITEM[id]) ITEM[id].cat = 'men'; }); TAG_LABEL.work = 'Workwear'; TAG_LABEL.goth = 'Goth';
['corset', 'biker', 'choker', 'kneeboots', 'ankleboots', 'lbd', 'crossbody'].forEach(function (id) { if (TAGS[id]) TAGS[id].push('goth'); });   // older pieces that suit the look

const SL = {
  // ---- work, gym, wardrobe grants ----
  earn(coins, fame) { reward(Number(coins) || 0, Number(fame) || 0); },
  grant(idsJson) { JSON.parse(idsJson).forEach(function (id) { if (PRICES[id] && !profile.owned.includes(id)) profile.owned.push(id); }); saveProfile(); },
  takeOff(idsJson) { const ids = JSON.parse(idsJson); let any = false; for (const k of Object.keys(state.outfit)) if (ids.includes(state.outfit[k])) { state.outfit[k] = null; any = true; } if (any) update(true); },
  // Starlife Town's boutiques: a piece bought in store goes into the wardrobe and on, in the brand's colour
  wearColor(id, color) { if (!ITEM[id]) return; if (!profile.owned.includes(id)) { profile.owned.push(id); saveProfile(); } wearOn(state, id); if (color) state.colors[id] = color; update(true); },
  rename(name) { const x = STARLETS.find(v => v.id === 'custom'); if (x && name) x.name = String(name); },
  // ---- art ----
  doll(vb) { UID = 0; return renderDoll(state, { vb: vb || '0 0 400 820' }); },
  dollOf(json, vb) { UID = 0; return renderDoll(JSON.parse(json), { vb: vb || '0 0 400 820' }); },
  dollSig() { const s = clone(state); delete s.room; delete s.focus; delete s.starlet; return JSON.stringify(s); },
  roomBg(json) {
    UID = 0;
    const s = json ? JSON.parse(json) : clone(state);
    s.room = Object.assign({}, s.room, { items: [] });
    const svg = roomSVG(s, {}), i = svg.indexOf('<g data-doll');
    return svg.slice(0, i) + '</svg>';
  },
  roomFull(json) { UID = 0; return roomSVG(json ? JSON.parse(json) : state, { standalone: true }); },
  furnBox(k) {
    const f = FURN[k];
    if (f.layer === 'wall') return [-130, -150, 260, 310];
    if (f.layer === 'rug') return [-250, -60, 500, 110];
    const w = (f.w || 200);
    return [-(w / 2) - 30, -345, w + 60, 365];
  },
  furn(k) {
    UID = 0;
    const f = FURN[k], b = SL.furnBox(k);
    const shadow = f.layer === 'floor' ? `<ellipse cx="0" cy="0" rx="${(f.w || 150) * 0.46}" ry="10" fill="#000" opacity=".13" filter="url(#fShadow)"/>` : '';
    return `<svg viewBox="${b.join(' ')}"><defs><filter id="fShadow" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="8"/></filter><linearGradient id="skyG" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8fc6ef"/><stop offset="1" stop-color="#e8f4fb"/></linearGradient></defs>${shadow}${f.draw()}</svg>`;
  },
  furnThumb(k) { UID = 0; return furnThumb(k); },
  thumb(id, color) { UID = 0; return itemThumb(ITEM[id], color || colOf(id)); },
  portrait(json, lips) {
    const s = json ? JSON.parse(json) : state;
    return `<svg viewBox="104 30 192 192">${portrait(s, lips || '#c96b78')}</svg>`;
  },
  mePortrait() {
    const x = STARLETS.find(v => v.id === state.starlet);
    const lips = x ? PRESETS[x.preset].lips.color : state.makeup.lips.color;
    return SL.portrait(null, lips === 'none' ? '#c96b78' : lips);
  },
  hairCard(k) { return `<svg viewBox="96 -6 208 240">${portrait(Object.assign({}, state, { hair: k }), shade(state.skin, -0.35))}</svg>`; },
  hero() { UID = 0; return typeof heroSVG === 'function' ? heroSVG() : ''; },
  guideFace() { UID = 0; return renderDoll(guideHost(), { vb: '128 84 144 196' }); },
  // ---- data ----
  data() {
    return JSON.stringify({
      items: ITEMS.map(i => ({ id: i.id, name: i.name, slot: i.slot, cat: i.cat, group: i.group || '', color: i.color, price: PRICES[i.id] || 0, tags: (TAGS[i.id] || []).map(t => TAG_LABEL[t]) })),
      starlets: STARLETS.map(x => ({ id: x.id, name: x.name, role: x.role, fame: STARLET_FAME[x.id] || 0, face: JSON.stringify(x), lips: PRESETS[x.preset].lips.color === 'none' ? '#c96b78' : PRESETS[x.preset].lips.color })),
      pal: PAL, contacts: CONTACTS, presets: Object.entries(PRESETS).map(([name, p]) => ({ name, dots: [p.shadow.color, p.lips.color, p.blush.color].filter(c => c && c !== 'none') })),
      eyeShapes: Object.entries(EYE_SHAPES).map(([v, e]) => ({ v, l: e.label })), brows: Object.entries(BROWS).map(([v, e]) => ({ v, l: e.label })),
      hair: Object.entries(HAIR).map(([v, e]) => ({ v, l: e.label })),
      walls: WALLS, floors: Object.entries(FLOORS).map(([v, f]) => ({ v, l: f.l, dot: f.c[1] })),
      furn: Object.entries(FURN).map(([k, f]) => ({ k, name: f.name, layer: f.layer, box: SL.furnBox(k) })),
      themes: THEMES.map(t => ({ id: t.id, name: t.name, desc: t.desc, tip: t.tip, bg: t.bg, tags: t.tags.map(x => TAG_LABEL[x]) })),
      judges: JUDGES.map(j => ({ name: j.name, role: j.role, face: JSON.stringify(j), lips: j.lips })),
      guide: GUIDE.length, famePerLevel: FAME_PER_LEVEL,
    });
  },
  state() { return JSON.stringify(state); },
  profile() {
    return JSON.stringify({ coins: profile.coins, fame: profile.fame, level: level(), xp: profile.fame % FAME_PER_LEVEL, owned: ITEMS.filter(i => owns(i.id)).map(i => i.id),
      bonus: profile.lastBonus !== today(), name: starletName(), featured: featuredTheme().id, canUndo: undoStack.length > 0, hasSave: !!lsGet(LS_CUR, null), guideSeen: !!lsGet('starlife.guide.v1', false) });
  },
  get(p) { const v = getP(p); return v === undefined || v === null ? '' : String(v); },
  set(p, v) { const cur = getP(p); setP(p, typeof cur === 'number' ? Number(v) : typeof cur === 'boolean' ? v === 'true' : v); update(); },
  setQuiet(p, v) { const cur = getP(p); setP(p, typeof cur === 'number' ? Number(v) : v); },
  commit() { update(); },
  layers() {
    const o = state.outfit;
    const order = state.tucked ? ['shoes', 'top', 'bottom', 'dress', 'outer'] : ['shoes', 'bottom', 'top', 'dress', 'outer'];
    if (o.shoes && ITEM[o.shoes].over) { order.splice(order.indexOf('shoes'), 1); order.splice(order.indexOf('dress') + 1, 0, 'shoes'); }
    return JSON.stringify([...order, 'bag', 'necklace', 'earrings', 'glasses', 'hat'].filter(k => o[k]).map(k => ({ slot: k, name: ITEM[o[k]].name, color: colOf(o[k]) })));
  },
  remove(slot) { state.outfit[slot] = null; update(true); },
  undress() { for (const k of Object.keys(state.outfit)) state.outfit[k] = null; update(true); toast(`${starletName()} is back in her underwear`); },
  recolor(c) {
    const f = state.focus && ITEM[state.focus]; if (!f) return;
    if (c === 'reset') delete state.colors[f.id]; else state.colors[f.id] = c;
    update();
  },
  focus() { const f = state.focus && ITEM[state.focus]; return f && state.outfit[f.slot] === f.id ? JSON.stringify({ id: f.id, name: f.name, color: colOf(f.id) }) : ''; },
  // ---- 3D: what the 3D starlet should show (optionally with shop pieces tried on) ----
  look3d(idsJson) {
    const s = clone(state); if (idsJson) JSON.parse(idsJson).forEach(id => wearOn(s, id));
    const x = STARLETS.find(v => v.id === s.starlet) || {};
    const worn = Object.keys(s.outfit).filter(k => s.outfit[k]).map(k => ({ slot: k, id: s.outfit[k], color: s.colors[s.outfit[k]] || ITEM[s.outfit[k]].color }));
    return JSON.stringify({ starlet: s.starlet, hairColor: s.hairColor, skin: s.skin, underwear: s.underwear, hairColor0: x.hairColor || '', skin0: x.skin || '', worn });
  },
  // ---- room ----
  addFurn(k) {
    const f = FURN[k]; if (!f) return;
    const id = 'u' + Date.now().toString(36) + Math.floor(Math.random() * 1e3);
    state.room.items.push({ id, k, x: 300 + Math.random() * 400, y: f.layer === 'wall' ? 200 : f.layer === 'rug' ? 560 : 590 });
    update();
  },
  moveFurn(id, x, y) {
    const it = state.room.items.find(i => i.id === id); if (!it) return;
    const f = FURN[it.k];
    it.x = clamp(x, 20, ROOM_W - 20);
    it.y = f.layer === 'wall' ? clamp(y, 90, WALL_Y - 110) : clamp(y, FLOOR_Y + 14, ROOM_H - 4);
  },
  moveDoll(x, y) { state.room.doll.x = clamp(x, 60, ROOM_W - 60); state.room.doll.y = clamp(y, FLOOR_Y + 40, ROOM_H - 4); },
  unplace(id) { state.room.items = state.room.items.filter(i => i.id !== id); update(); },
  depth(y) { return depth(y); },
  random() { randomize(); }, undo() { undo(); }, preset(n) { applyPreset(n); }, buy(id) { buy(id); }, equip(id) { equip(id); },
  // ---- shop ----
  bonus() { if (profile.lastBonus !== today()) { profile.lastBonus = today(); reward(50, 5); toast('+50 coins! Treat her to something in the Shop.'); } },
  tryOnDoll(idsJson) { UID = 0; const s = clone(state); JSON.parse(idsJson).forEach(id => wearOn(s, id)); return renderDoll(s); },
  wearNow(id) { equip(id, true); },
  // ---- contests ----
  brief(themeId) {
    const th = THEMES.find(t => t.id === themeId), r = scoreLook(state, th), rec = profile.contests[th.id];
    return JSON.stringify({ lines: [
      [r.base, 'A full outfit (a dress, or a top with a bottom)'], [r.shoes, 'Shoes'],
      [r.match.length >= 3, `Pieces that fit the theme: ${r.match.length} (aim for 3 or more)`],
      [!r.off.length, r.off.length ? `Off-theme pieces: ${r.off.map(i => i.name).join(', ')}` : 'Nothing off-theme'],
      [r.acc >= 2, `Accessories: ${r.acc} (2 or 3 is best)`], [r.mk >= 10, `Makeup: ${th.tip.toLowerCase()}`]].map(([ok, text]) => ({ ok, text })),
      best: rec && rec.date === today() ? rec.best : -1 });
  },
  enter(themeId) {
    themeSel = themeId; enterContest();
    const res = lastResult, th = THEMES.find(t => t.id === themeId), r = res.r;
    const medal = r.score >= 85 ? ['Gold medal', '#f5c542'] : r.score >= 65 ? ['Silver medal', '#c7ccd6'] : r.score >= 45 ? ['Bronze medal', '#d79b63'] : ['Keep styling', '#e7defa'];
    return JSON.stringify({ score: r.score, medal: medal[0], medalColor: medal[1], coins: res.coins, fame: res.fame, beat: res.beat, repeat: res.repeat,
      judges: JUDGES.map((j, i) => ({ stars: clamp(Math.round(r.score / 20 + (i - 1) * 0.4), 1, 5), say: judgeSay(r, th, i) })) });
  },
  // ---- starlets, album, guide ----
  starletJson(id) { return JSON.stringify(starletState(id)); },
  pickStarlet(id) { applyStarlet(id); toast(`Welcome home, ${STARLETS.find(v => v.id === id).name}!`); },
  guideText(i) { return GUIDE[i](); },
  guideDone() { lsSet('starlife.guide.v1', true); },
  albumAdd(name, thumb) {
    name = (name || '').trim() || `Look No. ${lookbook.length + 1}`;
    lookbook.unshift({ id: Date.now().toString(36), name, date: Date.now(), state: clone(state), thumb });
    reward(5, 10); lsSet(LS_BOOK, lookbook);
    toast(`“${name}” added to My Album ✦`);
    return name;
  },
  album() { return JSON.stringify(lookbook.map(l => ({ id: l.id, name: l.name, date: new Date(l.date).toDateString().slice(4), thumb: l.thumb || '' }))); },
  albumWear(id) { const l = lookbook.find(x => x.id === id); if (!l) return; state = sanitize(merge(clone(DEFAULT), clone(l.state))); update(true); toast(`Wearing “${l.name}”`); },
  albumDel(id) { lookbook = lookbook.filter(x => x.id !== id); lsSet(LS_BOOK, lookbook); },
  albumState(id) { const l = lookbook.find(x => x.id === id); return l ? JSON.stringify(l.state) : ''; },
};
// one entry point for the host: always returns a string; arrays come back as {"list":[...]}
function slCall(n, a, b, c) {
  let r = SL[n](a, b, c);
  if (r === undefined || r === null) return '';
  if (typeof r !== 'string') r = JSON.stringify(r);
  return r[0] === '[' ? '{"list":' + r + '}' : r;
}
lastSnap = JSON.stringify(state); undoStack = [];

// ---------------------------------------------------------------------
// Realism pass for the Unity port: avatars are the shaded doll head rather than the flat
// cartoon portrait, eyes are life-sized, and the face gets modelled light and shadow.
// ---------------------------------------------------------------------
const _eye = eye, _head = head;
eye = function (s) { return `<g transform="translate(176,137) scale(.8) translate(-176,-137)">${_eye(s)}</g>`; };
head = function (s) {
  const sk = s.skin, d = shade(sk, -0.45), dd = shade(sk, -0.62), l = shade(sk, 0.3), warm = mix(sk, '#d9534f', 0.55);
  // soften the drawn nose outlines so the nose is carried by light and shadow instead
  const base = _head(s)
    .replace(/(d="M190,168 C188,164 191,160 195,164"[^>]*?opacity=")\.75/g, '$1.34')
    .replace(/(d="M193,171 Q200,174 207,171"[^>]*?opacity=")\.55/, '$1.26');
  const soft = 'filter="url(#soft)"', softer = 'filter="url(#softer)"';
  const side = sym(
    `<ellipse cx="146" cy="120" rx="7" ry="30" fill="${d}" opacity=".2" ${softer}/>` +                         // temple
    `<ellipse cx="176" cy="127.5" rx="15" ry="4.6" fill="${d}" opacity=".24" ${soft}/>` +                      // eye socket
    `<ellipse cx="176" cy="131.6" rx="9" ry="1.8" fill="${l}" opacity=".3" ${soft}/>` +                        // lid highlight
    `<path d="M164,143 Q176,149 188,143 Q176,153 164,143 Z" fill="${d}" opacity=".2" ${soft}/>` +              // under-eye
    `<ellipse cx="160" cy="150" rx="12" ry="4.6" transform="rotate(-25 160 150)" fill="url(#rlHi)" opacity=".34"/>` + // cheekbone
    `<ellipse cx="163" cy="161" rx="17" ry="12" fill="url(#rlWarm)" opacity=".17"/>` +                        // blood under the cheek
    `<path d="M188,159 Q186,167 190,171" fill="none" stroke="${d}" stroke-width="2" opacity=".24" ${soft}/>` + // side of the nostril
    `<ellipse cx="194.2" cy="169.6" rx="3" ry="1.6" fill="${dd}" opacity=".38" ${soft}/>` +                    // nostril
    `<path d="M186,171 Q180,178 180.5,188" fill="none" stroke="${d}" stroke-width="1.6" opacity=".13" ${soft}/>` + // smile line
    `<ellipse cx="182" cy="183.4" rx="2.6" ry="2" fill="${dd}" opacity=".3" ${soft}/>`);                       // mouth corner
  const creases = [191, 195.5, 200, 204.5, 209].map(x => `<path d="M${x},185.6 L${x + (x - 200) * 0.06},192" stroke="${dd}" stroke-width=".5" opacity=".16"/>`).join('');
  return base + `<radialGradient id="rlHi"><stop offset="0" stop-color="${l}" stop-opacity="1"/><stop offset="1" stop-color="${l}" stop-opacity="0"/></radialGradient>
    <radialGradient id="rlWarm"><stop offset="0" stop-color="${warm}" stop-opacity="1"/><stop offset="1" stop-color="${warm}" stop-opacity="0"/></radialGradient>
    <g clip-path="url(#faceClip)">
      <path d="M142,118 C142,64 168,48 200,48 C232,48 258,64 258,118" fill="none" stroke="${d}" stroke-width="14" opacity=".26" ${softer}/>
      <ellipse cx="196" cy="92" rx="36" ry="20" fill="url(#rlHi)" opacity=".45"/>
      <path d="M140,146 Q146,196 200,216 Q254,196 260,146 Q252,186 200,201 Q148,186 140,146 Z" fill="${d}" opacity=".26" ${softer}/>
      <path d="M236,96 Q262,130 250,176 Q246,140 236,96 Z" fill="${d}" opacity=".2" ${softer}/>
      ${side}
      <path d="M204,130 C207,146 209,156 208,166 L213,169 C213,154 211,142 208,130 Z" fill="${d}" opacity=".22" ${soft}/>
      <path d="M198.5,126 L198.5,159" stroke="${l}" stroke-width="2.6" stroke-linecap="round" opacity=".4" ${soft}/>
      <ellipse cx="200" cy="165" rx="8" ry="6" fill="url(#rlWarm)" opacity=".15"/>
      <ellipse cx="199.4" cy="162.6" rx="3.6" ry="2.8" fill="url(#rlHi)" opacity=".55"/>
      <ellipse cx="200" cy="177" rx="3" ry="2.4" fill="${d}" opacity=".16" ${soft}/>
      <path d="M184,183.2 Q200,187 216,183.2" fill="none" stroke="${dd}" stroke-width="1.5" opacity=".34" ${soft}/>
      ${creases}
      <ellipse cx="200" cy="200.5" rx="11" ry="2.6" fill="${d}" opacity=".24" ${soft}/>
      <ellipse cx="200" cy="204" rx="9" ry="4.5" fill="url(#rlHi)" opacity=".3"/>
      <ellipse cx="200" cy="205" rx="12" ry="6" fill="url(#rlWarm)" opacity=".08"/>
    </g>`;
};
const FACE_VB = '135 86 130 130';
function faceState(x, lips) {
  const s = x.id && STARLETS.some(v => v.id === x.id) ? starletState(x.id) : clone(DEFAULT);
  for (const k of ['skin', 'hair', 'hairColor', 'eyeShape', 'eyeColor', 'brow']) if (x[k]) s[k] = x[k];
  if (x.makeup) s.makeup = clone(x.makeup); else if (lips) { s.makeup.lips.color = lips; s.makeup.lips.opacity = 0.8; }
  if (x.outfit) { s.outfit = clone(x.outfit); s.colors = clone(x.colors || {}); }
  return s;
}
SL.portrait = function (json, lips) { UID = 0; return renderDoll(json ? faceState(JSON.parse(json), lips) : state, { vb: FACE_VB }); };
SL.mePortrait = function () { UID = 0; return renderDoll(state, { vb: FACE_VB }); };
SL.hairCard = function (k) { UID = 0; return renderDoll(Object.assign({}, state, { hair: k }), { vb: '118 70 164 190' }); };
SL.icon = function (n) { return n === 'fame' ? FAME : COIN; };
