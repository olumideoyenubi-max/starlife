<script>
/* =====================================================================
   Starlife: a single-file dress-up, makeup and room-decorating game.
   Everything (doll, clothes, makeup, furniture) is drawn as layered SVG from state.
   ===================================================================== */

// ---------- colour utilities ----------
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
function hexToRgb(h) {
  h = h.replace('#', '');
  if (h.length === 3) h = h.split('').map(x => x + x).join('');
  const n = parseInt(h, 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}
const rgbToHex = (r, g, b) => '#' + [r, g, b].map(v => Math.round(clamp(v, 0, 255)).toString(16).padStart(2, '0')).join('');
function shade(hex, amt) { // amt <0 darkens, >0 lightens
  const [r, g, b] = hexToRgb(hex), t = amt < 0 ? 0 : 255, p = Math.abs(amt);
  return rgbToHex(r + (t - r) * p, g + (t - g) * p, b + (t - b) * p);
}
function mix(a, b, p) {
  const A = hexToRgb(a), B = hexToRgb(b);
  return rgbToHex(A[0] + (B[0] - A[0]) * p, A[1] + (B[1] - A[1]) * p, A[2] + (B[2] - A[2]) * p);
}
const lum = hex => { const [r, g, b] = hexToRgb(hex); return (0.299 * r + 0.587 * g + 0.114 * b) / 255; };
const EDGE = c => shade(c, lum(c) < 0.22 ? 0.35 : -0.32);
const st = (c, w = 1.2) => `stroke="${EDGE(c)}" stroke-width="${w}" stroke-linejoin="round"`;
function rng(seed) { return () => (seed = (seed * 16807) % 2147483647) / 2147483647; }
const esc = s => String(s).replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));

// ---------- base geometry (doll is drawn facing forward, viewBox 400 x 820) ----------
const MIRROR = 'translate(400,0) scale(-1,1)';
const sym = s => s + `<g transform="${MIRROR}">${s}</g>`;
let UID = 0, PFX = '';
const nid = () => PFX + (++UID);

let TORSO = 'M186,232 C184,244 168,246 150,250 C138,253 133,266 134,290 C135,315 146,345 154,365 C159,382 160,394 156,410 C150,428 144,448 142,470 L258,470 C256,448 250,428 244,410 C240,394 241,382 246,365 C254,345 265,315 266,290 C267,266 262,253 250,250 C232,246 216,244 214,232 Z';
const LEG = 'M143,465 C141,520 150,590 158,640 C160,660 156,690 160,715 C163,740 168,755 170,772 L191,772 C192,750 195,730 196,710 C197,690 196,665 194,645 C193,610 200,560 200,470 Z';
const FOOT = 'M170,766 C167,776 163,786 164,793 C166,799 175,800 182,799 C190,800 196,797 195,790 C194,782 193,774 192,766 Z';
let NECK = 'M187,184 C188,208 186,228 182,244 Q200,252 218,244 C214,228 212,208 213,184 Z';
let ARM_SHAPE = 'M151,254 C137,254 127,268 125,296 C122,330 117,362 114,394 C111,424 109,450 110,474 L124,475 C125,452 128,428 131,402 C134,374 140,344 145,320 C149,302 156,280 151,254 Z';
let HAND = 'M110,471 C106,484 103,497 105,508 C106,515 110,519 113,516 C114,520 118,521 120,517 C123,517 125,513 124,507 C126,500 127,492 125,484 L124,472 Z';
let LOWER = 'M154,395 C150,425 144,445 143,465 C141,520 150,590 158,640 C160,660 156,690 160,715 C163,740 168,755 170,775 L191,775 C192,750 195,730 196,710 C197,690 196,665 194,645 C193,610 200,560 200,488 C200,560 207,610 206,645 C204,665 203,690 204,710 C205,730 208,750 209,775 L230,775 C232,755 237,740 240,715 C244,690 240,660 242,640 C250,590 259,520 257,465 C256,445 250,425 246,395 Z';
let ARM = 'M147,262 C131,300 125,350 122,400 C120,435 118,460 116,478';
let FACE = 'M142,118 C142,64 168,48 200,48 C232,48 258,64 258,118 C258,150 251,174 236,191 C224,204 211,210 200,210 C189,210 176,204 164,191 C149,174 142,150 142,118 Z';
// masculine body: broader shoulders, straighter waist and hips, thicker neck, squarer jaw
const BODY_F = { TORSO, NECK, ARM_SHAPE, HAND, LOWER, ARM, FACE };
const BODY_M = {
  TORSO: 'M186,230 C184,242 166,244 146,247 C131,250 125,264 126,290 C127,318 136,348 144,370 C148,386 149,398 148,412 C147,432 145,452 145,470 L255,470 C255,452 253,432 252,412 C251,398 252,386 256,370 C264,348 273,318 274,290 C275,264 269,250 254,247 C234,244 216,242 214,230 Z',
  NECK: 'M184,180 C185,208 183,228 178,244 Q200,254 222,244 C217,228 215,208 216,180 Z',
  ARM_SHAPE: 'M146,252 C130,252 119,268 117,296 C114,330 110,362 107,394 C104,424 103,450 104,474 L121,475 C122,452 125,428 128,402 C131,374 137,344 142,320 C146,302 152,280 146,252 Z',
  HAND: 'M104,471 C99,485 96,498 98,509 C99,516 104,520 107,517 C108,521 113,522 115,518 C118,518 121,514 120,507 C122,500 123,492 121,484 L120,472 Z',
  LOWER: 'M150,395 C148,425 145,447 143,465 C141,520 150,590 158,640 C160,660 156,690 160,715 C163,740 168,755 170,775 L191,775 C192,750 195,730 196,710 C197,690 196,665 194,645 C193,610 200,560 200,488 C200,560 207,610 206,645 C204,665 203,690 204,710 C205,730 208,750 209,775 L230,775 C232,755 237,740 240,715 C244,690 240,660 242,640 C250,590 259,520 257,465 C255,447 252,425 250,395 Z',
  ARM: 'M142,262 C125,300 119,350 116,400 C114,435 112,460 110,478',
  FACE: 'M142,114 C142,62 168,46 200,46 C232,46 258,62 258,114 C258,150 256,172 247,188 C237,204 217,212 200,212 C183,212 163,204 153,188 C144,172 142,150 142,114 Z',
};
let BODY = 'f';
function setBody(g) { BODY = g === 'm' ? 'm' : 'f'; ({ TORSO, NECK, ARM_SHAPE, HAND, LOWER, ARM, FACE } = BODY === 'm' ? BODY_M : BODY_F); }
const TORSO_T = 'translate(200,350) scale(1.07,1.02) translate(-200,-350)';
const LOWER_T = 'translate(200,580) scale(1.07,1.004) translate(-200,-580)';

const EYE_SHAPES = {
  almond:   { label: 'Almond',   ox: 160, oy: 136, ix: 190, iy: 137, top: 124, bot: 146, r: 6.8 },
  round:    { label: 'Round',    ox: 161, oy: 136, ix: 189, iy: 137, top: 119, bot: 151, r: 7.4 },
  upturned: { label: 'Upturned', ox: 159, oy: 131, ix: 190, iy: 138, top: 122, bot: 146, r: 6.8 },
  doe:      { label: 'Doe',      ox: 160, oy: 139, ix: 190, iy: 136, top: 122, bot: 150, r: 7.2 },
};
const BROWS = {
  natural:  { label: 'Natural',  d: 'M160,119 Q175,110 192,115', w: 4 },
  arched:   { label: 'Arched',   d: 'M159,121 Q169,103 192,115', w: 4 },
  straight: { label: 'Straight', d: 'M160,117 Q176,114 192,116', w: 4.5 },
  bold:     { label: 'Bold',     d: 'M159,119 Q175,108 193,115', w: 7 },
  thin:     { label: 'Thin',     d: 'M161,118 Q175,110 191,115', w: 2.2 },
  feathered:{ label: 'Feathered',d: 'M160,119 Q176,111 192,116', w: 5.5, soft: true },
};

function baseDefs() {
  return `
  <clipPath id="torsoClip"><path d="${TORSO}" transform="${TORSO_T}"/></clipPath>
  <clipPath id="lowerClip"><path d="${LOWER}" transform="${LOWER_T}"/></clipPath>
  <filter id="soft" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="2"/></filter>
  <filter id="softer" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="4"/></filter>
  <filter id="blur6" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="6"/></filter>`;
}

// A garment fitted to the body: fill is clipped to the torso/lower silhouette and the
// silhouette edge is re-stroked where the garment covers it.
function fit(shape, c, which = 't', extra = '', fill = c) {
  const id = 'f' + nid();
  const [clip, d, t] = which === 't' ? ['torsoClip', TORSO, TORSO_T] : ['lowerClip', LOWER, LOWER_T];
  return `<clipPath id="${id}"><path d="${shape}"/></clipPath>
    <g clip-path="url(#${clip})"><path d="${shape}" fill="${fill}" ${st(c, 2.4)}/>${extra}</g>
    <g clip-path="url(#${id})"><path d="${d}" transform="${t}" fill="none" ${st(c, 2.4)}/></g>`;
}
function sleeve(c, len = 100, w = 30, fill = c) {
  const a = `pathLength="100" stroke-dasharray="${len} 300" fill="none" stroke-linecap="round"`;
  return sym(`<path d="${ARM}" ${a} stroke="${EDGE(c)}" stroke-width="${w + 2.4}"/><path d="${ARM}" ${a} stroke="${fill}" stroke-width="${w}"/>`);
}
// clip a mirrored panel's details to the panel itself
function panel(d, c, details = '', fill = c) {
  const id = 'p' + nid();
  return sym(`<clipPath id="${id}"><path d="${d}"/></clipPath><path d="${d}" fill="${fill}" ${st(c, 1.3)}/><g clip-path="url(#${id})">${details}</g>`);
}
function curvePts(p0, p1, p2, t0, t1, n) {
  const pts = [];
  for (let i = 0; i < n; i++) {
    const t = t0 + (t1 - t0) * (n === 1 ? 0 : i / (n - 1)), u = 1 - t;
    pts.push([u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0], u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1], t]);
  }
  return pts;
}

// ---------- body ----------
function skinDefs(sk) {
  const d1 = shade(sk, -0.17), d2 = shade(sk, -0.07), l = shade(sk, 0.1);
  return `<linearGradient id="skinCyl" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="${d1}"/><stop offset=".4" stop-color="${l}"/><stop offset=".65" stop-color="${sk}"/><stop offset="1" stop-color="${d1}"/></linearGradient>
  <linearGradient id="skinTorso" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="${d1}"/><stop offset=".18" stop-color="${sk}"/><stop offset=".45" stop-color="${l}"/><stop offset=".82" stop-color="${sk}"/><stop offset="1" stop-color="${d1}"/></linearGradient>
  <radialGradient id="skinFace" cx=".5" cy=".44" r=".62"><stop offset="0" stop-color="${l}"/><stop offset=".72" stop-color="${sk}"/><stop offset="1" stop-color="${d2}"/></radialGradient>
  <filter id="hairTex" x="-5%" y="-5%" width="110%" height="110%" color-interpolation-filters="sRGB">
    <feTurbulence type="fractalNoise" baseFrequency="0.22 0.012" numOctaves="3" seed="4" result="n"/>
    <feColorMatrix in="n" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0.75 0 0 0 -0.36" result="dk"/>
    <feComposite in="dk" in2="SourceGraphic" operator="in" result="dkIn"/>
    <feColorMatrix in="n" type="matrix" values="0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  0 -0.8 0 0 0.3" result="lt"/>
    <feComposite in="lt" in2="SourceGraphic" operator="in" result="ltIn"/>
    <feMerge result="tex"><feMergeNode in="SourceGraphic"/><feMergeNode in="dkIn"/><feMergeNode in="ltIn"/></feMerge>
    <feGaussianBlur in="SourceAlpha" stdDeviation="9" result="b"/>
    <feDiffuseLighting in="b" surfaceScale="9" diffuseConstant="1.15" lighting-color="#fff" result="l"><feDistantLight azimuth="225" elevation="48"/></feDiffuseLighting>
    <feComponentTransfer in="l" result="l2"><feFuncR type="linear" slope="1.25" intercept=".02"/><feFuncG type="linear" slope="1.25" intercept=".02"/><feFuncB type="linear" slope="1.25" intercept=".02"/></feComponentTransfer>
    <feBlend in="tex" in2="l2" mode="multiply" result="lit"/>
    <feComposite in="lit" in2="SourceAlpha" operator="in"/>
  </filter>
  <filter id="volSkin" x="-8%" y="-8%" width="116%" height="116%" color-interpolation-filters="sRGB">
    <feGaussianBlur in="SourceAlpha" stdDeviation="9" result="b"/>
    <feDiffuseLighting in="b" surfaceScale="3.2" diffuseConstant="1.05" lighting-color="#fff" result="l"><feDistantLight azimuth="225" elevation="58"/></feDiffuseLighting>
    <feComponentTransfer in="l" result="l2"><feFuncR type="linear" slope="1.12" intercept=".04"/><feFuncG type="linear" slope="1.12" intercept=".04"/><feFuncB type="linear" slope="1.12" intercept=".04"/></feComponentTransfer>
    <feBlend in="SourceGraphic" in2="l2" mode="multiply" result="lit"/>
    <feComposite in="lit" in2="SourceAlpha" operator="in"/>
  </filter>
  <filter id="vol" x="-8%" y="-8%" width="116%" height="116%" color-interpolation-filters="sRGB">
    <feGaussianBlur in="SourceAlpha" stdDeviation="6" result="b"/>
    <feDiffuseLighting in="b" surfaceScale="7" diffuseConstant="1.1" lighting-color="#fff" result="l"><feDistantLight azimuth="225" elevation="52"/></feDiffuseLighting>
    <feComponentTransfer in="l" result="l2"><feFuncR type="linear" slope="1.2" intercept=".04"/><feFuncG type="linear" slope="1.2" intercept=".04"/><feFuncB type="linear" slope="1.2" intercept=".04"/></feComponentTransfer>
    <feBlend in="SourceGraphic" in2="l2" mode="multiply" result="lit"/>
    <feSpecularLighting in="b" surfaceScale="7" specularConstant=".35" specularExponent="18" lighting-color="#fff" result="sp"><feDistantLight azimuth="225" elevation="52"/></feSpecularLighting>
    <feComposite in="sp" in2="SourceAlpha" operator="in" result="spIn"/>
    <feComposite in="lit" in2="spIn" operator="arithmetic" k2="1" k3=".5"/>
    <feComposite in2="SourceAlpha" operator="in"/>
  </filter>`;
}
// The body is shaded with soft gradients and fine anatomy lines so it reads as a person, not a paper doll.
function bodyBase(skin) {
  const e = shade(skin, -0.26);
  const S = `stroke="${e}" stroke-width="1" stroke-opacity=".75"`;
  const line = (d, o = .4, w = 1) => `<path d="${d}" fill="none" stroke="${e}" stroke-width="${w}" stroke-linecap="round" opacity="${o}"/>`;
  let s = sym(`<path d="${LEG}" fill="url(#skinCyl)" ${S}/><path d="${FOOT}" fill="url(#skinCyl)" ${S}/>` +
    line('M167,632 Q178,641 190,633', .4) + line('M170,646 Q178,650 186,646', .25) +
    line('M175,758 q-2,5 1,9', .35) + line('M171,792 l0,5 M177,793 l0,5 M183,793 l0,5 M189,792 l0,4', .3, .8));
  s += `<path d="${TORSO}" fill="url(#skinTorso)" ${S}/>`;
  s += sym(line('M166,256 Q180,262 194,258', .5, 1.1) + (BODY === 'm' ? line('M146,306 Q170,322 196,312', .35) + line('M182,350 Q190,356 196,352 M180,384 Q190,388 196,386', .18, 1.4) : line('M150,326 Q170,346 194,334', .35)) + line('M156,436 Q174,460 194,474', .3) + line('M160,372 Q164,392 160,410', .18, 2));
  s += line('M200,300 L200,326', .18, 2) + `<ellipse cx="200" cy="410" rx="1.6" ry="2.8" fill="${e}" opacity=".6"/>`;
  s += `<path d="${NECK}" fill="url(#skinCyl)" ${S}/>` +
    `<path d="M187,186 Q200,226 213,186 Z" fill="${shade(skin, -0.32)}" opacity=".35" filter="url(#soft)"/>` +
    sym(line('M191,214 Q190,232 184,242', .22));
  return s;
}
function arms(skin) {
  const e = shade(skin, -0.26);
  return sym(`<path d="${ARM_SHAPE}" fill="url(#skinCyl)" stroke="${e}" stroke-width="1" stroke-opacity=".75"/>` +
    `<path d="M${BODY === 'm' ? 110 : 116},396 q6,3 12,0" fill="none" stroke="${e}" stroke-width="1" opacity=".35"/>`);
}
function hands(skin) {
  const e = shade(skin, -0.26);
  return sym(`<path d="${HAND}" fill="url(#skinCyl)" stroke="${e}" stroke-width="1" stroke-opacity=".8"/>` +
    `<g transform="translate(${BODY === 'm' ? -5 : 0},0)"><path d="M124,482 C130,487 131,495 127,500" fill="none" stroke="${e}" stroke-width="1" opacity=".6"/>` +
    `<path d="M113,505 L113,515 M118,506 L119,517" fill="none" stroke="${e}" stroke-width=".8" opacity=".45"/></g>`);
}

function underwear(c) {
  const lace = shade(c, -0.14);
  if (BODY === 'm') return fit('M100,428 Q200,438 300,428 L300,500 Q250,502 206,494 L194,494 Q150,502 100,500 Z', c, 'l',
    `<rect x="100" y="428" width="200" height="14" fill="${shade(c, -0.25)}"/><path d="M200,450 Q196,480 200,505" fill="none" stroke="${lace}" stroke-width="1.2"/>`);
  return sym(`<path d="M166,300 L160,246" stroke="${EDGE(c)}" stroke-width="2" stroke-linecap="round"/>`) +
    fit('M100,302 Q140,290 172,296 Q190,300 200,312 Q210,300 228,296 Q260,290 300,302 L300,336 Q200,330 100,336 Z', c, 't',
      `<path d="M150,306 Q175,322 198,314 M202,314 Q225,322 250,306" fill="none" stroke="${lace}" stroke-width="1" opacity=".7"/><circle cx="200" cy="318" r="2.2" fill="${lace}"/>`) +
    fit('M100,436 Q200,446 300,436 L300,472 Q236,478 205,500 L195,500 Q164,478 100,472 Z', c, 'l',
      `<path d="M120,442 Q200,452 280,442" fill="none" stroke="${lace}" stroke-width="1.2" stroke-dasharray="2 2"/><circle cx="200" cy="452" r="2" fill="${lace}"/>`);
}

// ---------- hair ----------
const HAIR_CAP = 'M137,124 C132,60 166,36 200,36 C234,36 268,60 263,124 C256,96 236,76 200,74 C164,76 144,96 137,124 Z';
const SLEEK_CAP = 'M137,124 C132,58 166,36 200,36 C234,36 268,58 263,124 C258,92 234,70 200,68 C166,70 142,92 137,124 Z';
const strands = (d) => `<path d="M176,44 Q156,62 146,104" fill="none" stroke="${d}" stroke-width="1.4" opacity=".55"/><path d="M226,44 Q246,62 254,104" fill="none" stroke="${d}" stroke-width="1.4" opacity=".55"/>`;
const shine = (l) => `<path d="M170,48 Q200,38 230,48" fill="none" stroke="${l}" stroke-width="4" stroke-linecap="round" opacity=".45"/>`;
const H = (d, c, dk) => `<path d="${d}" fill="${c}" stroke="${dk}" stroke-width=".9" stroke-opacity=".55" stroke-linejoin="round"/>`;

const SIDEBURN = (c, d) => sym(H('M139,96 L140,142 L147,142 L148,98 Z', c, d));
const HAIR = {
  crop: {
    label: 'Textured Crop',
    back: (c, d) => `<ellipse cx="200" cy="96" rx="64" ry="64" fill="${c}"/>`,
    front: (c, d, l) => H('M137,118 C133,56 168,36 202,36 C238,38 267,58 263,118 C259,98 251,86 242,80 L236,92 L224,76 L212,90 L198,72 L186,88 L172,74 L164,90 C152,96 142,106 137,118 Z', c, d) + SIDEBURN(c, d) + strands(d) + shine(l),
  },
  buzz: {
    label: 'Buzz Cut',
    back: (c, d) => `<ellipse cx="200" cy="100" rx="62" ry="60" fill="${c}" opacity=".9"/>`,
    front: (c, d) => `<g opacity=".88">${H('M139,118 C136,60 168,44 200,44 C232,44 264,60 261,118 C256,92 236,74 200,72 C164,74 144,92 139,118 Z', c, d)}</g>` + SIDEBURN(c, d),
  },
  quiff: {
    label: 'Quiff',
    back: (c, d) => `<ellipse cx="200" cy="96" rx="64" ry="64" fill="${c}"/>`,
    front: (c, d, l) => H('M137,120 C130,58 160,22 208,18 C250,18 272,50 263,120 C259,94 248,80 234,74 C222,60 198,52 174,62 C156,72 143,92 137,120 Z', c, d) + SIDEBURN(c, d) +
      `<path d="M170,58 Q200,26 246,40 M180,64 Q210,40 240,52" fill="none" stroke="${d}" stroke-width="1.4" opacity=".55"/>` + `<path d="M178,40 Q210,24 240,34" fill="none" stroke="${l}" stroke-width="5" stroke-linecap="round" opacity=".4"/>`,
  },
  locs: {
    label: 'Locs',
    back: (c, d) => { let s = `<ellipse cx="200" cy="96" rx="68" ry="66" fill="${c}"/>`; for (let i = 0; i < 13; i++) { const x = 128 + i * 12, len = 300 + (i % 3) * 30; s += `<rect x="${x}" y="70" width="12" height="${len}" rx="6" fill="${i % 2 ? c : shade(c, 0.12)}" stroke="${d}" stroke-width=".8"/>`; } return s; },
    front: (c, d, l) => { let s = H(SLEEK_CAP, c, d); for (let i = 0; i < 4; i++) s += sym(`<rect x="${132 + i * 10}" y="${96 + i * 6}" width="11" height="${220 - i * 20}" rx="5.5" fill="${i % 2 ? c : shade(c, 0.12)}" stroke="${d}" stroke-width=".8"/>`); return s + shine(l); },
  },
  long: {
    label: 'Long Sleek',
    back: (c, d) => H('M132,105 C126,40 172,26 200,26 C228,26 274,40 268,105 C280,240 300,420 292,540 Q200,566 108,540 C100,420 120,240 132,105 Z', c, d),
    front: (c, d, l) => H(HAIR_CAP, c, d) + strands(d) +
      sym(H('M139,112 C124,190 128,290 140,370 C146,420 146,460 138,500 C160,480 166,430 160,380 C152,300 148,190 157,118 Z', c, d)) +
      H('M138,128 C140,92 170,70 214,74 C236,76 252,88 260,110 C236,92 205,92 182,104 C163,113 150,124 138,128 Z', c, d) + shine(l),
  },
  waves: {
    label: 'Mermaid Waves',
    back: (c, d) => H('M132,100 C118,160 142,200 122,250 C104,300 134,340 116,390 C104,430 126,470 112,510 C108,525 118,540 128,548 Q200,566 272,548 C282,540 292,525 288,510 C274,470 296,430 284,390 C266,340 296,300 278,250 C258,200 282,160 268,100 C268,44 232,26 200,26 C168,26 132,44 132,100 Z', c, d),
    front: (c, d, l) => sym(H('M200,38 C168,38 138,58 135,124 C146,96 170,80 198,78 Z', c, d) +
      H('M138,112 C122,170 148,220 130,270 C116,320 142,360 126,410 C120,440 132,470 124,500 C150,480 158,440 148,400 C138,360 162,320 150,270 C140,220 160,170 152,130 C150,122 152,116 154,112 Z', c, d)) + shine(l),
  },
  bob: {
    label: 'Chic Bob',
    back: (c, d) => H('M134,110 C128,45 175,32 200,32 C232,32 272,45 266,110 C270,160 272,195 266,216 Q200,226 134,216 C128,195 130,160 134,110 Z', c, d),
    front: (c, d, l) => H(HAIR_CAP, c, d) +
      H('M140,108 C142,62 172,42 200,42 C228,42 258,62 260,108 C240,100 220,103 200,105 C180,103 160,100 140,108 Z', c, d) +
      sym(H('M141,100 C134,150 135,190 147,215 L133,216 C126,182 126,140 135,100 Z', c, d)) +
      `<path d="M160,104 L162,90 M180,104 L181,88 M220,104 L219,88 M240,104 L238,90" stroke="${d}" stroke-width="1.2" opacity=".5"/>` + shine(l),
  },
  pixie: {
    label: 'Pixie',
    back: (c, d) => `<ellipse cx="200" cy="100" rx="66" ry="70" fill="${c}" stroke="${d}" stroke-width="1.2"/>`,
    front: (c, d, l) => H('M136,128 C128,52 172,30 206,32 C246,34 272,62 264,128 C262,104 254,90 244,84 L236,98 L224,82 L210,98 L198,80 L186,100 L172,84 L164,102 L156,92 C146,104 140,116 136,128 Z', c, d) + strands(d) + shine(l),
  },
  ponytail: {
    label: 'High Pony',
    back: (c, d) => H('M238,58 C300,46 314,130 298,230 C288,300 306,370 286,440 C262,380 262,300 268,222 C274,142 266,98 234,80 Z', c, d) +
      `<path d="M262,96 C286,150 276,240 286,310" fill="none" stroke="${d}" stroke-width="1.3" opacity=".5"/>`,
    front: (c, d, l) => H(SLEEK_CAP, c, d) + strands(d) + shine(l) +
      `<ellipse cx="248" cy="62" rx="8" ry="12" transform="rotate(35 248 62)" fill="${shade(c, -0.45)}"/>`,
  },
  bun: {
    label: 'Ballet Bun',
    back: (c, d) => `<circle cx="200" cy="28" r="25" fill="${c}" stroke="${d}" stroke-width="1.2"/><path d="M182,24 Q200,8 218,24 M184,34 Q200,20 216,34" fill="none" stroke="${d}" stroke-width="1.3" opacity=".6"/>`,
    front: (c, d, l) => H(SLEEK_CAP, c, d) + strands(d) + shine(l) +
      `<path d="M178,48 Q200,40 222,48" fill="none" stroke="${shade(c, -0.45)}" stroke-width="5" stroke-linecap="round"/>`,
  },
  curly: {
    label: 'Cloud Curls',
    back: (c, d) => {
      let s = `<ellipse cx="200" cy="112" rx="96" ry="98" fill="${c}"/>`;
      for (let a = 0; a < 360; a += 18) {
        const r = a * Math.PI / 180, y = 112 + Math.sin(r) * 96;
        if (y > 190) continue;
        s += `<circle cx="${(200 + Math.cos(r) * 92).toFixed(1)}" cy="${y.toFixed(1)}" r="24" fill="${c}" stroke="${d}" stroke-width="1.1"/>`;
      }
      return s + `<ellipse cx="200" cy="112" rx="88" ry="90" fill="${c}"/>`;
    },
    front: (c, d, l) => {
      let s = H(HAIR_CAP, c, d);
      for (let a = 180; a <= 360; a += 15) {
        const r = a * Math.PI / 180;
        const x = 200 + Math.cos(r) * 62, y = 120 + Math.sin(r) * 62;
        s += `<circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="11" fill="${c}" stroke="${d}" stroke-width="1"/><path d="M${(x - 4).toFixed(1)},${y.toFixed(1)} a4,4 0 1 1 8,0" fill="none" stroke="${d}" stroke-width="1" opacity=".6"/>`;
      }
      return s + `<circle cx="186" cy="44" r="6" fill="${l}" opacity=".35"/>`;
    },
  },
  braids: {
    label: 'Twin Braids',
    back: (c, d) => `<ellipse cx="200" cy="100" rx="66" ry="72" fill="${c}" stroke="${d}" stroke-width="1.2"/>`,
    front: (c, d, l) => {
      let b = '';
      for (let i = 0; i < 18; i++) {
        const y = 150 + i * 19, x = 144 + Math.min(i, 6) * 1.6;
        b += `<ellipse cx="${x}" cy="${y}" rx="${12 - i * 0.2}" ry="12" transform="rotate(${i % 2 ? 22 : -22} ${x} ${y})" fill="${c}" stroke="${d}" stroke-width="1.1"/>`;
      }
      b += `<rect x="146" y="486" width="14" height="6" rx="3" fill="${shade(c, -0.5)}"/><path d="M147,492 L142,512 L153,506 L164,512 L159,492 Z" fill="${c}" stroke="${d}" stroke-width="1"/>`;
      return sym(H('M200,38 C168,38 138,58 135,124 C146,96 170,80 198,78 Z', c, d) + b) + shine(l);
    },
  },
};
function hairBack(s) { const c = s.hairColor; return HAIR[s.hair].back(c, shade(c, -0.32), shade(c, 0.32)); }
function hairFront(s) { const c = s.hairColor; return HAIR[s.hair].front(c, shade(c, -0.32), shade(c, 0.32)); }

// ---------- face & makeup ----------
const LIP_UP = 'M183,183 Q191,176 200,179.5 Q209,176 217,183 Q200,186.5 183,183 Z';
const LIP_LO = 'M183,183 Q200,186.5 217,183 Q211,194 200,195 Q189,194 183,183 Z';
const LIP_ALL = 'M183,183 Q191,176 200,179.5 Q209,176 217,183 Q211,194 200,195 Q189,194 183,183 Z';

function eyePath(E) { return `M${E.ox},${E.oy} Q175,${E.top} ${E.ix},${E.iy} Q175,${E.bot} ${E.ox},${E.oy} Z`; }

function faceDefs(s) {
  const m = s.makeup, E = EYE_SHAPES[s.eyeShape];
  const irisCol = m.contacts !== 'none' ? m.contacts : s.eyeColor;
  const lc = m.lips.color === 'none' ? '#c97a7a' : m.lips.color;
  const bc = m.blush.color === 'none' ? '#f19a9a' : m.blush.color;
  return `<clipPath id="faceClip"><path d="${FACE}"/></clipPath>
  <clipPath id="eyeClip"><path d="${eyePath(E)}"/></clipPath>
  <radialGradient id="irisG"><stop offset="0" stop-color="${shade(irisCol, 0.35)}"/><stop offset=".55" stop-color="${irisCol}"/><stop offset="1" stop-color="${shade(irisCol, -0.45)}"/></radialGradient>
  <radialGradient id="blushG"><stop offset="0" stop-color="${bc}" stop-opacity="1"/><stop offset="1" stop-color="${bc}" stop-opacity="0"/></radialGradient>
  <radialGradient id="hlG"><stop offset="0" stop-color="${m.highlight.color}" stop-opacity="1"/><stop offset="1" stop-color="${m.highlight.color}" stop-opacity="0"/></radialGradient>
  <linearGradient id="lipMetal" x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stop-color="${shade(lc, 0.45)}"/><stop offset=".35" stop-color="${lc}"/>
    <stop offset=".6" stop-color="${shade(lc, -0.3)}"/><stop offset=".85" stop-color="${shade(lc, 0.35)}"/><stop offset="1" stop-color="${lc}"/>
  </linearGradient>`;
}

function eyeshadow(m, E) {
  const sh = m.shadow;
  if (sh.style === 'none') return '';
  const { ox, oy, ix, iy, top, bot } = E, c = sh.color;
  const lid = `M${ox - 2},${oy + 1} Q175,${top - 16} ${ix + 1},${iy} Q175,${top + 4} ${ox - 2},${oy + 1} Z`;
  let s = '';
  if (sh.style === 'soft') s = `<path d="${lid}" fill="${c}"/>`;
  if (sh.style === 'smoky') {
    s = `<path d="M${ox - 7},${oy - 1} Q175,${top - 24} ${ix + 1},${iy - 1} Q175,${top + 4} ${ox - 7},${oy - 1} Z" fill="${c}"/>` +
        `<path d="M${ox - 2},${oy + 1} Q175,${top - 8} ${ix},${iy} Q175,${top + 4} ${ox - 2},${oy + 1} Z" fill="${shade(c, -0.35)}"/>` +
        `<path d="M${ox - 4},${oy} Q175,${bot + 8} ${ix},${iy} Q175,${bot} ${ox - 4},${oy} Z" fill="${c}"/>`;
  }
  if (sh.style === 'winged') {
    s = `<path d="${lid}" fill="${c}"/><path d="M${ox + 8},${oy - 5} L${ox - 15},${oy - 15} Q${ox - 4},${oy - 1} ${ox + 9},${oy + 3} Z" fill="${shade(c, -0.15)}"/>`;
  }
  if (sh.style === 'halo') {
    s = `<path d="${lid}" fill="${shade(c, -0.2)}"/><ellipse cx="176" cy="${top + 1}" rx="7" ry="5" fill="${mix(c, '#ffffff', 0.65)}"/>`;
  }
  return `<g opacity="${sh.opacity}" filter="url(#soft)">${s}</g>`;
}

function eyeliner(m, E) {
  const L = m.liner;
  if (L.style === 'none') return '';
  const { ox, oy, ix, iy, top, bot } = E, c = L.color;
  const lidD = `M${ox},${oy} Q175,${top} ${ix},${iy}`;
  const line = w => `<path d="${lidD}" fill="none" stroke="${c}" stroke-width="${w}" stroke-linecap="round"/>`;
  const lower = (t0, t1, w) => `<polyline points="${curvePts([ox, oy], [175, bot], [ix, iy], t0, t1, 8).map(p => p[0].toFixed(1) + ',' + p[1].toFixed(1)).join(' ')}" fill="none" stroke="${c}" stroke-width="${w}" stroke-linecap="round"/>`;
  switch (L.style) {
    case 'tight': return line(1.8);
    case 'classic': return line(2.8);
    case 'winged': return line(2.8) + `<path d="M${ox + 5},${oy - 1.5} L${ox - 11},${oy - 9} L${ox + 1},${oy + 1} Z" fill="${c}"/>`;
    case 'cat': return line(3.4) + `<path d="M${ox + 9},${oy - 2.5} L${ox - 17},${oy - 14} L${ox + 1},${oy + 1.5} Z" fill="${c}"/>` + lower(0, 0.45, 1.6);
    case 'smudged': return `<g filter="url(#soft)" opacity=".9">${line(3.4)}${lower(0, 1, 2.6)}</g>` + line(1.6);
  }
  return '';
}

function lashes(m, E) {
  const lvl = { none: [3, 3.5, 1], natural: [6, 5.5, 1.1], volume: [9, 7.5, 1.6], dramatic: [13, 10.5, 2] }[m.mascara];
  const { ox, oy, ix, iy, top, bot } = E;
  let s = '';
  curvePts([ox, oy], [175, top], [ix, iy], 0.02, 0.7, lvl[0]).forEach(([x, y, t]) => {
    let dx = -(1 - t) * 1.2 - 0.15, dy = -1; const n = Math.hypot(dx, dy); dx /= n; dy /= n;
    const len = lvl[1] * (1 - t * 0.45);
    s += `<path d="M${x.toFixed(1)},${y.toFixed(1)} q${(dx * len * 0.4).toFixed(1)},${(dy * len * 0.7).toFixed(1)} ${(dx * len).toFixed(1)},${(dy * len).toFixed(1)}" fill="none" stroke="#1a1214" stroke-width="${lvl[2]}" stroke-linecap="round"/>`;
  });
  if (m.mascara === 'dramatic' || m.mascara === 'volume') {
    curvePts([ox, oy], [175, bot], [ix, iy], 0.08, 0.6, m.mascara === 'dramatic' ? 6 : 4).forEach(([x, y, t]) => {
      const len = (m.mascara === 'dramatic' ? 4.5 : 3) * (1 - t * 0.5);
      s += `<path d="M${x.toFixed(1)},${y.toFixed(1)} l${(-len * 0.5).toFixed(1)},${len.toFixed(1)}" stroke="#1a1214" stroke-width="1" stroke-linecap="round"/>`;
    });
  }
  return s;
}

function eye(s) {
  const m = s.makeup, E = EYE_SHAPES[s.eyeShape];
  const iris = m.contacts !== 'none' ? m.contacts : s.eyeColor;
  const peakTop = 0.25 * E.oy + 0.5 * E.top + 0.25 * E.iy, peakBot = 0.25 * E.oy + 0.5 * E.bot + 0.25 * E.iy;
  const cy = (peakTop + peakBot) / 2 + 0.5, r = E.r;
  const skinLine = shade(s.skin, -0.28);
  return eyeshadow(m, E) +
    `<path d="${eyePath(E)}" fill="#fbf8f6"/>` +
    `<g clip-path="url(#eyeClip)">
      <rect x="150" y="110" width="50" height="50" fill="#000" opacity=".04"/>
      <circle cx="176" cy="${cy}" r="${r}" fill="url(#irisG)" stroke="${shade(iris, -0.5)}" stroke-width="1.1"/>
      ${Array.from({ length: 12 }, (_, i) => { const a = i * Math.PI / 6; return `<path d="M${(176 + Math.cos(a) * r * 0.45).toFixed(1)},${(cy + Math.sin(a) * r * 0.45).toFixed(1)} L${(176 + Math.cos(a) * r * 0.9).toFixed(1)},${(cy + Math.sin(a) * r * 0.9).toFixed(1)}" stroke="${shade(iris, 0.35)}" stroke-width=".6" opacity=".55"/>`; }).join('')}
      <circle cx="176" cy="${cy}" r="${r * 0.62}" fill="none" stroke="${shade(iris, 0.3)}" stroke-width="1.2" opacity=".55"/>
      <circle cx="176" cy="${cy}" r="${r * 0.42}" fill="#16100f"/>
      <circle cx="179" cy="${cy - 3}" r="2" fill="#fff"/><circle cx="173.5" cy="${cy + 2.4}" r=".9" fill="#fff" opacity=".8"/>
      <path d="M${E.ox},${E.oy} Q175,${E.top} ${E.ix},${E.iy}" fill="none" stroke="#000" stroke-width="4" opacity=".12"/>
    </g>` +
    `<ellipse cx="${E.ix - 1.5}" cy="${E.iy}" rx="2.2" ry="1.6" fill="#e8a0a0" opacity=".8"/>` +
    `<path d="M${E.ox},${E.oy} Q175,${E.bot} ${E.ix},${E.iy}" fill="none" stroke="${skinLine}" stroke-width=".9" opacity=".7"/>` +
    `<path d="M${E.ox + 3},${E.oy - 3} Q175,${E.top - 7} ${E.ix - 2},${E.iy - 4}" fill="none" stroke="${skinLine}" stroke-width=".9" opacity=".45"/>` +
    `<path d="M${E.ox},${E.oy} Q175,${E.top} ${E.ix},${E.iy}" fill="none" stroke="#2a1c1c" stroke-width="1.5" stroke-linecap="round"/>` +
    eyeliner(m, E) + lashes(m, E);
}

function head(s) {
  const sk = s.skin, e = shade(sk, -0.22), m = s.makeup;
  let h = sym(`<ellipse cx="141" cy="136" rx="8" ry="14" fill="${sk}" stroke="${e}" stroke-width="1.2"/><path d="M141,128 Q137,136 141,143" fill="none" stroke="${e}" stroke-width=".9"/>`);
  h += `<path d="${FACE}" fill="url(#skinFace)" stroke="${e}" stroke-width="1" stroke-opacity=".8"/>`;
  // natural shading: temples, cheek hollows, eye sockets
  h += `<g clip-path="url(#faceClip)" filter="url(#softer)" fill="${shade(sk, -0.3)}"><g opacity=".22">` +
    sym(`<ellipse cx="143" cy="140" rx="9" ry="44"/><ellipse cx="176" cy="126" rx="17" ry="6" opacity=".6"/>`) + `</g></g>`;

  // contour, blush, highlighter: all clipped to the face
  let face = '';
  if (m.contour.opacity > 0) {
    const cc = shade(sk, -0.38);
    face += `<g opacity="${m.contour.opacity}" filter="url(#softer)" fill="${cc}">` +
      sym(`<path d="M140,146 Q158,156 172,170 Q156,174 141,164 Z"/><path d="M144,160 Q160,196 200,210 Q164,204 146,178 Z"/><ellipse cx="145" cy="92" rx="8" ry="18"/>`) +
      `</g><g opacity="${m.contour.opacity * 0.8}" filter="url(#soft)">` + sym(`<path d="M195,132 L193.5,160" stroke="${cc}" stroke-width="3" stroke-linecap="round"/>`) + `</g>`;
  }
  if (m.blush.color !== 'none' && m.blush.opacity > 0) {
    face += `<g opacity="${m.blush.opacity}">` + sym(m.blush.place === 'lifted'
      ? `<ellipse cx="160" cy="156" rx="21" ry="9" transform="rotate(-28 160 156)" fill="url(#blushG)"/>`
      : m.blush.place === 'draped'
      ? `<ellipse cx="156" cy="140" rx="17" ry="28" transform="rotate(-18 156 140)" fill="url(#blushG)"/>`
      : `<ellipse cx="166" cy="163" rx="18" ry="12" fill="url(#blushG)"/>`) +
      (m.blush.place === 'apple' ? `<ellipse cx="200" cy="164" rx="6" ry="3" fill="url(#blushG)" opacity=".5"/>` : '') + `</g>`;
  }
  if (m.highlight.opacity > 0) {
    face += `<g opacity="${m.highlight.opacity}">` +
      sym(`<ellipse cx="158" cy="148" rx="13" ry="5" transform="rotate(-25 158 148)" fill="url(#hlG)"/><ellipse cx="180" cy="123" rx="8" ry="2.5" fill="url(#hlG)"/>`) +
      `<ellipse cx="200" cy="150" rx="2.6" ry="11" fill="url(#hlG)"/><ellipse cx="200" cy="176.5" rx="4" ry="1.8" fill="url(#hlG)"/><ellipse cx="200" cy="202" rx="6" ry="2" fill="url(#hlG)"/></g>`;
  }
  h += `<g clip-path="url(#faceClip)">${face}</g>`;

  h += sym(eye(s));

  // brows
  const B = BROWS[s.brow], bcol = s.browColor === 'match' ? shade(s.hairColor, lum(s.hairColor) > 0.6 ? -0.45 : -0.12) : s.browColor;
  const bp = B.d.match(/-?\d+(\.\d+)?/g).map(Number);
  const hairs = curvePts([bp[0], bp[1]], [bp[2], bp[3]], [bp[4], bp[5]], 0, 1, 22).map(([x, y, t]) => {
    const len = B.w * 1.1 + 1.5, ang = -0.35 - t * 0.9; // outer hairs sweep outwards, inner hairs stand up
    const dx = Math.cos(Math.PI / 2 + ang) * len, dy = -Math.sin(Math.PI / 2 + ang) * len;
    return `<path d="M${(x - dx / 2).toFixed(1)},${(y - dy / 2).toFixed(1)} l${dx.toFixed(1)},${dy.toFixed(1)}" stroke="${shade(bcol, -0.15)}" stroke-width=".85" stroke-linecap="round"/>`;
  }).join('');
  const brow = `<path d="${B.d}" fill="none" stroke="${bcol}" stroke-width="${B.w}" stroke-linecap="round" opacity=".55"/>` + hairs;
  h += sym(B.soft ? `<g filter="url(#soft)" opacity=".85">${brow}</g><path d="${B.d}" fill="none" stroke="${bcol}" stroke-width="2.4" stroke-linecap="round"/>` : brow);

  // nose
  const nz = shade(sk, -0.3);
  h += `<g filter="url(#soft)" opacity=".45">` + sym(`<path d="M194,128 C192,144 190,156 191,165" fill="none" stroke="${nz}" stroke-width="2.4"/>`) + `</g>` +
       `<ellipse cx="200" cy="164" rx="3.4" ry="2.4" fill="#fff" opacity=".2"/>` +
       sym(`<path d="M190,168 C188,164 191,160 195,164" fill="none" stroke="${nz}" stroke-width="1.2" stroke-linecap="round" opacity=".75"/><ellipse cx="195.5" cy="169.5" rx="2" ry="1.1" fill="${shade(sk, -0.45)}" opacity=".55"/>`) +
       `<path d="M193,171 Q200,174 207,171" fill="none" stroke="${nz}" stroke-width="1" stroke-linecap="round" opacity=".55"/>` +
       `<path d="M198,174 L198.5,179 M202,174 L201.5,179" stroke="${nz}" stroke-width=".7" opacity=".3"/>`;

  // lips
  const base = mix(sk, '#c45f6c', 0.42);
  h += `<path d="${LIP_UP}" fill="${shade(base, -0.06)}"/><path d="${LIP_LO}" fill="${base}"/>`;
  const L = m.lips;
  if (L.color !== 'none' && L.opacity > 0) {
    const fill = L.finish === 'metallic' ? 'url(#lipMetal)' : L.color;
    h += `<g opacity="${L.opacity}"><path d="${LIP_UP}" fill="${L.finish === 'metallic' ? fill : shade(L.color, -0.08)}"/><path d="${LIP_LO}" fill="${fill}"/></g>`;
    if (L.finish === 'gloss') {
      h += `<ellipse cx="199" cy="189.5" rx="6.5" ry="2" fill="#fff" opacity=".7"/><ellipse cx="192" cy="180.6" rx="2.6" ry=".9" fill="#fff" opacity=".6"/><ellipse cx="206" cy="187.5" rx="2" ry=".9" fill="#fff" opacity=".5"/>`;
    } else if (L.finish === 'metallic') {
      h += `<g fill="#fff">${[[193, 188], [204, 190], [198, 182], [209, 184], [189, 184]].map(([x, y]) => `<circle cx="${x}" cy="${y}" r=".8" opacity=".85"/>`).join('')}</g>`;
    }
  }
  h += `<path d="M183,183 Q200,186.5 217,183" fill="none" stroke="${shade(base, -0.4)}" stroke-width="1.1" stroke-linecap="round"/>` +
    sym(`<circle cx="183.5" cy="183.2" r="1.2" fill="${shade(base, -0.45)}" opacity=".6"/>`) +
    (L.finish === 'gloss' && L.color !== 'none' ? '' : `<ellipse cx="200" cy="190" rx="5.5" ry="1.6" fill="#fff" opacity=".18"/>`) +
    `<path d="M193,199 Q200,202 207,199" fill="none" stroke="${shade(sk, -0.25)}" stroke-width="1.2" opacity=".35" filter="url(#soft)"/>`;
  if (L.liner !== 'none') h += `<path d="${LIP_ALL}" fill="none" stroke="${L.liner}" stroke-width="1.4" stroke-linejoin="round"/>`;
  h += beard(s);
  return h;
}
const BEARDS = { none: 'Clean-shaven', stubble: 'Stubble', short: 'Short beard', moustache: 'Moustache', goatee: 'Goatee' };
function beard(s) {
  const b = s.beard || 'none'; if (b === 'none') return '';
  const c = shade(s.hairColor, lum(s.hairColor) > 0.6 ? -0.35 : -0.05), d = shade(c, -0.3);
  const tache = `<path d="M182,178 Q191,171 200,175 Q209,171 218,178 Q209,177 200,180 Q191,177 182,178 Z" fill="${c}" stroke="${d}" stroke-width=".6"/>`;
  const chin = `<path d="M186,194 Q200,214 214,194 Q207,201 200,201 Q193,201 186,194 Z" fill="${c}" stroke="${d}" stroke-width=".6"/>`;
  if (b === 'stubble') return `<g clip-path="url(#faceClip)" opacity=".42" filter="url(#soft)"><path d="M146,150 C150,186 172,210 200,213 C228,210 250,186 254,150 C248,172 236,188 222,194 Q200,206 178,194 C164,188 152,172 146,150 Z" fill="${c}"/>${tache}</g>`;
  if (b === 'moustache') return tache;
  if (b === 'goatee') return tache + chin;
  return `<g filter="url(#hairTex)"><path d="M144,138 C146,176 166,206 200,215 C234,206 254,176 256,138 C252,164 242,182 228,190 Q214,197 210,194 Q200,186 190,194 Q186,197 172,190 C158,182 148,164 144,138 Z" fill="${c}" stroke="${d}" stroke-width=".7"/></g>` + tache;
}

// ---------- wardrobe catalogue ----------
// slot: where it is worn; cat: wardrobe tab. draw(c) = main layer, sleeves(c) = arm layer.
const ITEMS = [
  // ----- tops -----
  { id: 'tee', name: 'Classic Tee', slot: 'top', cat: 'top', color: '#ffffff',
    draw: c => fit('M100,236 L178,236 Q200,262 222,236 L300,236 L300,412 Q200,420 100,412 Z', c),
    sleeves: c => sleeve(c, 22, 33) },
  { id: 'cami', name: 'Silk Cami', slot: 'top', cat: 'top', color: '#f4b8c8',
    draw: c => sym(`<path d="M163,292 L157,244" stroke="${EDGE(c)}" stroke-width="3" stroke-linecap="round"/>`) +
      fit('M100,292 Q150,280 200,296 Q250,280 300,292 L300,376 Q200,384 100,376 Z', c, 't',
        `<path d="M120,300 Q160,312 200,302 Q240,312 280,300" fill="none" stroke="#fff" stroke-width="3" opacity=".25"/>`) +
      `<path d="M193,294 l7,4 l7,-4 l-2,8 l-5,-3 l-5,3 Z" fill="${shade(c, -0.15)}"/>` },
  { id: 'blouse', name: 'Poet Blouse', slot: 'top', cat: 'top', color: '#fdf6ec',
    draw: c => fit('M100,238 L184,238 L200,290 L216,238 L300,238 L300,416 Q200,424 100,416 Z', c, 't',
      [304, 330, 356, 382, 406].map(y => `<circle cx="200" cy="${y}" r="2.4" fill="${shade(c, -0.25)}"/>`).join('')) +
      sym(`<path d="M184,238 L170,262 L196,276 Z" fill="${c}" ${st(c)}/>`),
    sleeves: c => sleeve(c, 96, 36) },
  { id: 'turtle', name: 'Cashmere Turtleneck', slot: 'top', cat: 'top', color: '#c8b6e2', over: true,
    draw: c => `<path d="M184,192 L216,192 L218,250 L182,250 Z" fill="${c}" ${st(c)}/>` +
      [189, 195, 201, 207, 213].map(x => `<path d="M${x},196 L${x},246" stroke="${EDGE(c)}" stroke-width=".8" opacity=".5"/>`).join('') +
      fit('M100,236 L300,236 L300,442 Q200,448 100,442 Z', c, 't',
        Array.from({ length: 18 }, (_, i) => `<path d="M${130 + i * 8},430 L${130 + i * 8},446" stroke="${EDGE(c)}" stroke-width=".8" opacity=".5"/>`).join('')),
    sleeves: c => sleeve(c, 100, 35) },
  { id: 'corset', name: 'Lace-up Corset', slot: 'top', cat: 'top', color: '#2b2230',
    draw: c => fit('M100,290 Q130,276 160,284 Q182,290 200,302 Q218,290 240,284 Q270,276 300,290 L300,406 Q200,396 100,406 Z', c, 't',
      [160, 180, 220, 240].map(x => `<path d="M${x},${x < 200 ? 290 : 290} L${x},404" stroke="${shade(c, 0.25)}" stroke-width="1" opacity=".6"/>`).join('') +
      [316, 334, 352, 370, 388].map(y => `<path d="M194,${y} L206,${y + 14} M206,${y} L194,${y + 14}" stroke="${mix(c, '#f6d3dd', 0.7)}" stroke-width="1.4"/>`).join('')) },
  { id: 'breton', name: 'Breton Stripe', slot: 'top', cat: 'top', color: '#2b3a67',
    draw: c => fit('M100,236 L178,236 Q200,258 222,236 L300,236 L300,412 Q200,420 100,412 Z', c, 't',
      Array.from({ length: 10 }, (_, i) => `<rect x="100" y="${258 + i * 15}" width="200" height="6" fill="${c}"/>`).join(''), '#fbfaf7'),
    sleeves: c => sleeve(c, 68, 31, '#fbfaf7') + sym(`<path d="${ARM}" pathLength="100" stroke-dasharray="0 12 2 6 2 6 2 6 2 6 2 6 2 300" fill="none" stroke="${c}" stroke-width="31"/>`) },

  // ----- bottoms -----
  { id: 'jeans', name: 'Skinny Jeans', slot: 'bottom', cat: 'bottom', color: '#4f6d9a',
    draw: c => fit('M100,394 L300,394 L300,782 L100,782 Z', c, 'l',
      `<path d="M150,406 L250,406" stroke="${EDGE(c)}" stroke-width="1.4"/>` +
      `<path d="M200,406 Q199,440 192,454" fill="none" stroke="${EDGE(c)}" stroke-width="1.2"/><circle cx="200" cy="400" r="2.6" fill="#d6b25e"/>` +
      sym(`<path d="M156,412 Q170,424 178,406" fill="none" stroke="${shade(c, 0.35)}" stroke-width="1.2" stroke-dasharray="2 2"/><ellipse cx="176" cy="600" rx="10" ry="40" fill="#fff" opacity=".12"/><path d="M162,762 L196,762" stroke="${EDGE(c)}" stroke-width="1"/>`)) },
  { id: 'shorts', name: 'Denim Cut-offs', slot: 'bottom', cat: 'bottom', color: '#7ea0cf',
    draw: c => fit('M100,394 L300,394 L300,515 L100,515 Z', c, 'l',
      `<path d="M150,406 L250,406" stroke="${EDGE(c)}" stroke-width="1.4"/><circle cx="200" cy="400" r="2.6" fill="#d6b25e"/>` +
      sym(`<path d="M156,412 Q170,424 178,406" fill="none" stroke="${shade(c, 0.35)}" stroke-width="1.2" stroke-dasharray="2 2"/>` +
        Array.from({ length: 8 }, (_, i) => `<path d="M${148 + i * 7},515 l1,-6" stroke="#fff" stroke-width="1" opacity=".7"/>`).join(''))) },
  { id: 'mini', name: 'A-line Mini', slot: 'bottom', cat: 'bottom', color: '#e57b97',
    draw: c => `<path d="M152,394 L248,394 L266,522 Q200,532 134,522 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M152,394 L248,394 L249,406 L151,406 Z" fill="${shade(c, -0.12)}"/>` +
      [176, 200, 224].map(x => `<circle cx="${x}" cy="420" r="2.4" fill="${shade(c, 0.4)}"/>`).join('') },
  { id: 'pleated', name: 'Pleated Midi', slot: 'bottom', cat: 'bottom', color: '#c9b37e',
    draw: c => `<path d="M151,394 L249,394 L288,648 Q200,660 112,648 Z" fill="${c}" ${st(c)}/>` +
      Array.from({ length: 9 }, (_, i) => `<path d="M${156 + i * 11},404 L${124 + i * 19},${651 + Math.sin(i) * 3}" stroke="${EDGE(c)}" stroke-width="1" opacity=".55"/>`).join('') +
      `<rect x="151" y="392" width="98" height="13" rx="2" fill="${shade(c, -0.15)}"/>` },
  { id: 'wide', name: 'Wide-leg Trousers', slot: 'bottom', cat: 'bottom', color: '#ece3d4',
    draw: c => `<path d="M150,394 L250,394 L256,470 L272,782 L205,782 L200,505 L195,782 L128,782 L144,470 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M170,420 L162,782 M230,420 L238,782" stroke="${EDGE(c)}" stroke-width=".9" opacity=".5"/>` +
      `<rect x="150" y="394" width="100" height="12" fill="${shade(c, -0.6)}"/><rect x="193" y="393" width="14" height="14" rx="2" fill="none" stroke="#d6b25e" stroke-width="2"/>` },
  { id: 'pencil', name: 'Pencil Skirt', slot: 'bottom', cat: 'bottom', color: '#1f1b22',
    draw: c => `<path d="M151,394 Q249,394 249,394 Q264,470 250,612 L150,612 Q136,470 151,394 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M228,612 L228,574" stroke="${EDGE(c)}" stroke-width="1.3"/><path d="M152,404 L248,404" stroke="${EDGE(c)}" stroke-width="1"/>` },

  // ----- dresses -----
  { id: 'slip', name: 'Satin Slip', slot: 'dress', cat: 'dress', color: '#e8c3a0',
    draw: c => sym(`<path d="M163,294 L157,244" stroke="${EDGE(c)}" stroke-width="2.4" stroke-linecap="round"/>`) +
      `<path d="M150,394 Q200,402 250,394 L270,652 Q200,664 130,652 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M176,410 Q168,520 160,650 M222,410 Q232,520 238,650" stroke="#fff" stroke-width="5" opacity=".25" fill="none"/>` +
      fit('M100,292 Q150,282 200,302 Q250,282 300,292 L300,402 L100,402 Z', c, 't', `<path d="M130,300 Q160,330 170,400" stroke="#fff" stroke-width="5" opacity=".25" fill="none"/>`) },
  { id: 'gown', name: 'Princess Gown', slot: 'dress', cat: 'dress', color: '#a9c6ef', vb: '30 170 340 650',
    draw: c => `<path d="M150,392 Q200,404 250,392 Q322,560 354,794 Q200,818 46,794 Q78,560 150,392 Z" fill="${c}" ${st(c)}/>` +
      Array.from({ length: 9 }, (_, i) => `<path d="M${160 + i * 10},408 Q${120 + i * 20},600 ${70 + i * 32},${796 + (i % 2) * 6}" fill="none" stroke="${shade(c, 0.4)}" stroke-width="2" opacity=".55"/>`).join('') +
      (() => { const r = rng(7); let s = ''; for (let i = 0; i < 26; i++) s += `<circle cx="${(90 + r() * 220).toFixed(0)}" cy="${(450 + r() * 330).toFixed(0)}" r="${(0.8 + r() * 1.4).toFixed(1)}" fill="#fff" opacity=".9"/>`; return s; })() +
      fit('M100,286 Q140,268 170,280 Q190,288 200,300 Q210,288 230,280 Q260,268 300,286 L300,404 L100,404 Z', c, 't',
        `<path d="M200,302 L200,402" stroke="${EDGE(c)}" stroke-width="1" opacity=".5"/>`) +
      `<path d="M150,394 Q200,408 250,394" fill="none" stroke="${shade(c, -0.25)}" stroke-width="4"/>` },
  { id: 'wrap', name: 'Wrap Dress', slot: 'dress', cat: 'dress', color: '#3f7f6b',
    draw: c => `<path d="M150,396 L250,396 L272,604 Q200,618 128,604 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M246,400 Q220,500 170,610" fill="none" stroke="${EDGE(c)}" stroke-width="1.4"/>` +
      fit('M100,238 L176,238 L200,322 L224,238 L300,238 L300,404 L100,404 Z', c, 't', `<path d="M224,238 L178,404" stroke="${EDGE(c)}" stroke-width="1.3"/>`) +
      `<rect x="150" y="392" width="100" height="10" fill="${shade(c, -0.15)}"/><path d="M152,398 Q140,430 146,462 M156,400 Q156,430 164,454" fill="none" stroke="${shade(c, -0.15)}" stroke-width="5" stroke-linecap="round"/>`,
    sleeves: c => sleeve(c, 100, 32) },
  { id: 'lbd', name: 'Little Black Dress', slot: 'dress', cat: 'dress', color: '#18151a',
    draw: c => fit('M100,262 Q200,278 300,262 L300,470 L100,470 Z', c) +
      `<path d="M143,462 L257,462 L254,562 Q200,570 146,562 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M148,470 L252,470" stroke="${c}" stroke-width="6"/>` },
  { id: 'sundress', name: 'Floral Sundress', slot: 'dress', cat: 'dress', color: '#f6c453',
    draw: c => {
      const pid = 'fl' + nid();
      const pat = `<pattern id="${pid}" width="28" height="28" patternUnits="userSpaceOnUse"><rect width="28" height="28" fill="${c}"/>` +
        `<g fill="#fff">${[[7, 7], [21, 21]].map(([x, y]) => [0, 72, 144, 216, 288].map(a => `<circle cx="${(x + Math.cos(a * Math.PI / 180) * 3).toFixed(1)}" cy="${(y + Math.sin(a * Math.PI / 180) * 3).toFixed(1)}" r="2"/>`).join('')).join('')}</g>` +
        `<circle cx="7" cy="7" r="1.5" fill="#e0794f"/><circle cx="21" cy="21" r="1.5" fill="#e0794f"/></pattern>`;
      return pat + sym(`<path d="M164,298 L158,244" stroke="${EDGE(c)}" stroke-width="3.2" stroke-linecap="round"/>`) +
        `<path d="M150,394 Q200,402 250,394 L282,604 Q200,618 118,604 Z" fill="url(#${pid})" ${st(c)}/>` +
        fit('M100,298 Q200,290 300,298 L300,402 L100,402 Z', c, 't', '', `url(#${pid})`) +
        `<path d="M150,398 Q200,404 250,398" fill="none" stroke="${shade(c, -0.2)}" stroke-width="3"/>`;
    } },
  { id: 'sequin', name: 'Disco Mini', slot: 'dress', cat: 'dress', color: '#c0c5d6',
    draw: c => {
      const pid = 'sq' + nid();
      const pat = `<pattern id="${pid}" width="10" height="9" patternUnits="userSpaceOnUse"><rect width="10" height="9" fill="${shade(c, -0.15)}"/>` +
        `<circle cx="2.5" cy="2.5" r="2.6" fill="${shade(c, 0.35)}"/><circle cx="7.5" cy="7" r="2.6" fill="${c}"/><circle cx="2" cy="2" r=".8" fill="#fff"/></pattern>`;
      return pat + fit('M100,250 L200,280 L300,250 L300,470 L100,470 Z', c, 't', '', `url(#${pid})`) +
        `<path d="M143,462 L257,462 L268,542 Q200,552 132,542 Z" fill="url(#${pid})" ${st(c)}/>`;
    } },

  // ----- outerwear -----
  { id: 'blazer', name: 'Tailored Blazer', slot: 'outer', cat: 'outer', color: '#d9c2a6',
    draw: c => panel('M133,246 C150,240 168,240 182,242 L190,322 L184,454 L137,454 C133,400 127,300 133,246 Z', c,
      `<path d="M146,412 L174,412" stroke="${EDGE(c)}" stroke-width="1.4"/>`) +
      sym(`<path d="M182,242 L165,251 L177,292 L190,322 Z" fill="${shade(c, -0.1)}" ${st(c)}/>`) +
      `<circle cx="186" cy="372" r="3.2" fill="${shade(c, -0.45)}"/>`,
    sleeves: c => sleeve(c, 100, 35) },
  { id: 'biker', name: 'Leather Biker', slot: 'outer', cat: 'outer', color: '#2a2326',
    draw: c => panel('M131,246 C150,240 170,240 186,242 L196,300 L190,412 L139,412 C133,370 127,300 131,246 Z', c,
      `<path d="M140,250 Q150,290 170,300" stroke="#fff" stroke-width="4" opacity=".09" fill="none"/><rect x="130" y="398" width="70" height="14" fill="${shade(c, 0.12)}"/>`) +
      sym(`<path d="M186,242 L160,254 L174,290 L196,300 Z" fill="${shade(c, 0.1)}" ${st(c)}/>`) +
      `<path d="M190,300 L186,412" stroke="#cfd3da" stroke-width="2" stroke-dasharray="2 2"/><rect x="232" y="397" width="10" height="16" rx="2" fill="none" stroke="#cfd3da" stroke-width="2"/>`,
    sleeves: c => sleeve(c, 100, 33) + sym(`<path d="${ARM}" pathLength="100" stroke-dasharray="0 88 6 300" fill="none" stroke="#cfd3da" stroke-width="2"/>`) },
  { id: 'trench', name: 'Classic Trench', slot: 'outer', cat: 'outer', color: '#c8a979', vb: '70 220 260 430',
    draw: c => panel('M131,246 C150,240 170,240 186,242 L194,330 L192,628 L120,628 Q122,520 138,452 C130,380 125,300 131,246 Z', c,
      `<circle cx="178" cy="300" r="2.8" fill="${shade(c, -0.4)}"/><circle cx="178" cy="340" r="2.8" fill="${shade(c, -0.4)}"/><path d="M134,600 L190,600" stroke="${EDGE(c)}" stroke-width="1" opacity=".6"/>`) +
      sym(`<path d="M186,242 L158,248 L170,300 L194,330 Z" fill="${shade(c, -0.08)}" ${st(c)}/>`) +
      `<path d="M132,392 L268,392 L268,406 L132,406 Z" fill="${shade(c, -0.1)}" ${st(c)}/><rect x="186" y="389" width="20" height="20" rx="2" fill="none" stroke="${shade(c, -0.5)}" stroke-width="2.2"/>`,
    sleeves: c => sleeve(c, 100, 35) + sym(`<path d="${ARM}" pathLength="100" stroke-dasharray="0 84 4 300" fill="none" stroke="${shade(c, -0.15)}" stroke-width="35"/>`) },
  { id: 'cardigan', name: 'Cosy Cardigan', slot: 'outer', cat: 'outer', color: '#f2d7e0',
    draw: c => panel('M136,246 C150,241 170,241 184,242 L188,300 L190,472 L139,472 C135,400 129,300 136,246 Z', c,
      Array.from({ length: 7 }, (_, i) => `<path d="M${140 + i * 8},250 L${141 + i * 8},470" stroke="${EDGE(c)}" stroke-width=".7" opacity=".35"/>`).join('') +
      `<rect x="130" y="456" width="70" height="16" fill="${shade(c, -0.07)}"/>`) +
      [300, 340, 380, 420].map(y => `<circle cx="184" cy="${y}" r="3" fill="${shade(c, -0.35)}"/>`).join(''),
    sleeves: c => sleeve(c, 100, 34) + sym(`<path d="${ARM}" pathLength="100" stroke-dasharray="0 90 10 300" fill="none" stroke="${shade(c, -0.08)}" stroke-width="34"/>`) },
  { id: 'denimjk', name: 'Denim Jacket', slot: 'outer', cat: 'outer', color: '#6f8fb8',
    draw: c => panel('M132,246 C150,240 170,240 186,242 L192,300 L190,422 L138,422 C133,380 127,300 132,246 Z', c,
      `<path d="M150,296 L180,296 L180,318 L150,318 Z" fill="${shade(c, -0.08)}" ${st(c)}/><path d="M134,282 L190,282" stroke="${shade(c, 0.35)}" stroke-dasharray="2 2"/><rect x="132" y="406" width="64" height="16" fill="${shade(c, -0.1)}"/>`) +
      sym(`<path d="M186,242 L162,252 L176,284 L192,300 Z" fill="${shade(c, -0.12)}" ${st(c)}/>`) +
      [330, 362, 394].map(y => `<circle cx="188" cy="${y}" r="2.6" fill="#c9a14a"/>`).join(''),
    sleeves: c => sleeve(c, 100, 34) },
  { id: 'puffer', name: 'Cloud Puffer', slot: 'outer', cat: 'outer', color: '#f5f1ea',
    draw: c => panel('M128,246 C150,236 172,236 194,240 L198,300 L198,438 L132,438 C126,380 120,300 128,246 Z', c,
      [282, 322, 362, 402].map(y => `<path d="M120,${y} Q160,${y + 6} 200,${y}" fill="none" stroke="${EDGE(c)}" stroke-width="1.2" opacity=".7"/>`).join('')) +
      `<path d="M176,232 Q200,250 224,232 L228,250 Q200,264 172,250 Z" fill="${shade(c, -0.05)}" ${st(c)}/>`,
    sleeves: c => sleeve(c, 100, 42) + sym(`<path d="${ARM}" pathLength="100" stroke-dasharray="0 30 1 25 1 25 1 300" fill="none" stroke="${EDGE(c)}" stroke-width="42" opacity=".5"/>`) },

  // ----- shoes -----
  { id: 'sneakers', name: 'Court Sneakers', slot: 'shoes', cat: 'shoes', vb: '148 744 104 64', color: '#e57b97',
    draw: c => sym(`<path d="M163,768 L196,768 L198,786 Q199,796 189,796 L168,796 Q158,795 160,786 Z" fill="#fbfbfb" ${st('#ffffff')}/>` +
      `<path d="M164,786 Q180,776 196,782" fill="none" stroke="${c}" stroke-width="3.4" stroke-linecap="round"/>` +
      `<path d="M158,792 L199,792 L198,799 Q190,801 168,801 Q158,800 158,792 Z" fill="${shade(c, 0.55)}" ${st(c, 1)}/>` +
      `<path d="M172,772 L188,772 M172,776 L188,776" stroke="#c9c2c6" stroke-width="1.2"/>`) },
  { id: 'pumps', name: 'Pointed Pumps', slot: 'shoes', cat: 'shoes', vb: '148 744 104 64', color: '#c2233f',
    draw: c => sym(`<path d="M165,770 L195,770 Q199,788 186,800 Q180,804 174,800 Q161,788 165,770 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M168,772 Q180,780 192,772" fill="none" stroke="${shade(c, 0.3)}" stroke-width="1.3"/><path d="M171,786 Q174,794 178,797" fill="none" stroke="#fff" stroke-width="1.6" opacity=".35"/>`) },
  { id: 'ankleboots', name: 'Chelsea Boots', slot: 'shoes', cat: 'shoes', vb: '146 694 108 114', color: '#5b3b2a', over: true,
    draw: c => sym(`<path d="M159,706 L198,706 L197,770 Q201,790 192,799 L168,799 Q157,797 161,784 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M166,722 Q180,736 195,722 L195,746 Q180,760 166,746 Z" fill="${shade(c, -0.25)}"/><rect x="164" y="797" width="34" height="5" rx="2" fill="${shade(c, -0.5)}"/>`) },
  { id: 'kneeboots', name: 'Knee-high Boots', slot: 'shoes', cat: 'shoes', color: '#1e1a1d', over: true,
    draw: c => sym(`<path d="M156,626 L199,626 L197,770 Q201,790 192,800 L168,800 Q157,797 161,784 L162,700 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M168,640 Q172,700 172,780" fill="none" stroke="#fff" stroke-width="3" opacity=".14"/><path d="M156,634 L199,634" stroke="${EDGE(c)}" stroke-width="1"/>`) },
  { id: 'flats', name: 'Ballet Flats', slot: 'shoes', cat: 'shoes', vb: '148 744 104 64', color: '#f2c6d3',
    draw: c => sym(`<path d="M165,772 L195,772 Q199,789 189,797 L171,797 Q161,789 165,772 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M174,776 L180,779 L186,776 L186,782 L180,779 L174,782 Z" fill="${shade(c, -0.25)}"/>`) },
  { id: 'sandals', name: 'Strappy Sandals', slot: 'shoes', cat: 'shoes', vb: '148 734 104 74', color: '#d6b25e',
    draw: c => sym(`<path d="M167,756 L193,756" stroke="${c}" stroke-width="3" stroke-linecap="round"/><path d="M180,756 L180,776" stroke="${c}" stroke-width="2.2"/>` +
      `<path d="M166,776 Q180,770 194,776" fill="none" stroke="${c}" stroke-width="3" stroke-linecap="round"/><path d="M167,784 Q180,780 193,784" fill="none" stroke="${c}" stroke-width="2.6" stroke-linecap="round"/>` +
      `<path d="M163,790 L197,790 L197,795 L163,795 Z" fill="${shade(c, -0.25)}"/>`) },

  // ----- accessories: bags -----
  { id: 'tote', name: 'Canvas Tote', slot: 'bag', cat: 'acc', group: 'Bags', color: '#efe4cf', vb: '230 440 120 140',
    draw: c => `<path d="M266,500 Q285,462 304,500" fill="none" stroke="${shade(c, -0.4)}" stroke-width="3.5"/>` +
      `<path d="M256,496 L314,496 L320,566 L250,566 Z" fill="${c}" ${st(c)}/><path d="M262,520 L308,520" stroke="${shade(c, -0.25)}" stroke-width="1.2" stroke-dasharray="3 3"/>` +
      `<text x="285" y="546" text-anchor="middle" font-family="Georgia,serif" font-size="11" font-style="italic" fill="${shade(c, -0.5)}">Starlife</text>` },
  { id: 'clutch', name: 'Pearl Clutch', slot: 'bag', cat: 'acc', group: 'Bags', color: '#f3e8ee', vb: '75 455 90 75',
    draw: c => `<path d="M90,480 L148,480 L150,512 L88,512 Z" fill="${c}" ${st(c)}/><path d="M90,480 L119,498 L148,480" fill="none" ${st(c)}/>` +
      `<circle cx="119" cy="497" r="3.2" fill="#fff" stroke="#d6b25e" stroke-width="1.2"/>` },
  { id: 'crossbody', name: 'Quilted Crossbody', slot: 'bag', cat: 'acc', group: 'Bags', color: '#1f1b22', vb: '140 240 170 260',
    draw: c => `<path d="M160,250 L266,446" stroke="#d6b25e" stroke-width="2.4" stroke-dasharray="4 2"/>` +
      `<rect x="236" y="436" width="64" height="46" rx="8" fill="${c}" ${st(c)}/>` +
      `<path d="M244,444 L292,474 M292,444 L244,474 M260,436 L300,462 M236,458 L268,482" stroke="${shade(c, 0.25)}" stroke-width="1" opacity=".5"/>` +
      `<rect x="262" y="446" width="12" height="8" rx="2" fill="#d6b25e"/>` },
  { id: 'heartbag', name: 'Heart Mini Bag', slot: 'bag', cat: 'acc', group: 'Bags', color: '#ff7aa2', vb: '245 470 80 110',
    draw: c => `<path d="M285,494 L285,520" stroke="#d6b25e" stroke-width="1.6" stroke-dasharray="2 1.5"/>` +
      `<path d="M285,568 C250,546 258,512 276,518 Q283,520 285,528 Q287,520 294,518 C312,512 320,546 285,568 Z" fill="${c}" ${st(c)}/>` +
      `<path d="M270,530 Q272,524 278,523" fill="none" stroke="#fff" stroke-width="2" opacity=".5"/>` },
  // necklaces
  { id: 'pearls', name: 'Pearl Strand', slot: 'necklace', cat: 'acc', group: 'Jewellery', color: '#f7f2ea', vb: '160 215 80 70',
    draw: c => curvePts([178, 236], [200, 290], [222, 236], 0, 1, 15).map(([x, y]) => `<circle cx="${x.toFixed(1)}" cy="${y.toFixed(1)}" r="3.1" fill="${c}" stroke="${shade(c, -0.25)}" stroke-width=".7"/><circle cx="${(x - 1).toFixed(1)}" cy="${(y - 1).toFixed(1)}" r=".9" fill="#fff"/>`).join('') },
  { id: 'pendant', name: 'Gem Pendant', slot: 'necklace', cat: 'acc', group: 'Jewellery', color: '#3aa58f', vb: '160 215 80 70',
    draw: c => `<path d="M180,236 Q200,288 220,236" fill="none" stroke="#d6b25e" stroke-width="1.3"/><path d="M200,256 L207,264 L200,276 L193,264 Z" fill="${c}" stroke="#d6b25e" stroke-width="1.4"/><path d="M198,262 L200,259 L202,262" stroke="#fff" stroke-width=".8" fill="none"/>` },
  { id: 'choker', name: 'Velvet Choker', slot: 'necklace', cat: 'acc', group: 'Jewellery', color: '#231d26', vb: '170 200 60 40',
    draw: c => `<path d="M184.5,213 Q200,219 215.5,213 L215.5,221 Q200,227 184.5,221 Z" fill="${c}" ${st(c, .8)}/><circle cx="200" cy="226" r="3" fill="#e6e8ee" stroke="#a9adb8" stroke-width=".8"/>` },
  // earrings
  { id: 'hoops', name: 'Gold Hoops', slot: 'earrings', cat: 'acc', group: 'Jewellery', color: '#d6b25e', vb: '115 120 170 70',
    draw: c => sym(`<circle cx="139" cy="160" r="11" fill="none" stroke="${c}" stroke-width="2.6"/><path d="M131,154 a11,11 0 0 1 6,-5" fill="none" stroke="#fff" stroke-width="1" opacity=".6"/>`) },
  { id: 'studs', name: 'Diamond Studs', slot: 'earrings', cat: 'acc', group: 'Jewellery', color: '#e8f1ff', vb: '115 120 170 70',
    draw: c => sym(`<path d="M141,144 L145,148 L141,152 L137,148 Z" fill="${c}" stroke="#9fb3cc" stroke-width=".8"/><circle cx="140" cy="147" r=".9" fill="#fff"/>`) },
  { id: 'drops', name: 'Crystal Drops', slot: 'earrings', cat: 'acc', group: 'Jewellery', color: '#b18be0', vb: '115 120 170 90',
    draw: c => sym(`<circle cx="141" cy="148" r="2" fill="#d6b25e"/><path d="M141,150 L141,162" stroke="#d6b25e" stroke-width="1.2"/><path d="M141,162 Q133,174 141,180 Q149,174 141,162 Z" fill="${c}" ${st(c, .8)}/><circle cx="139" cy="173" r="1.2" fill="#fff" opacity=".8"/>`) },
  // glasses
  { id: 'roundspecs', name: 'Round Specs', slot: 'glasses', cat: 'acc', group: 'Eyewear', color: '#b07a3b', vb: '128 100 144 70',
    draw: c => sym(`<circle cx="175" cy="135" r="17" fill="#fff" fill-opacity=".1" stroke="${c}" stroke-width="2.6"/><path d="M158,131 L141,127" stroke="${c}" stroke-width="2.4"/><path d="M165,125 a12,12 0 0 1 8,-4" fill="none" stroke="#fff" stroke-width="1.4" opacity=".6"/>`) +
      `<path d="M192,132 Q200,126 208,132" fill="none" stroke="${c}" stroke-width="2.4"/>` },
  { id: 'cateye', name: 'Cat-eye Frames', slot: 'glasses', cat: 'acc', group: 'Eyewear', color: '#1e1a1d', vb: '128 100 144 70',
    draw: c => sym(`<path d="M154,122 Q176,118 194,128 Q194,148 176,150 Q158,148 156,134 Q152,128 150,120 Z" fill="#fff" fill-opacity=".1" stroke="${c}" stroke-width="3" stroke-linejoin="round"/><path d="M152,124 L141,126" stroke="${c}" stroke-width="2.4"/>`) +
      `<path d="M194,130 Q200,126 206,130" fill="none" stroke="${c}" stroke-width="2.4"/>` },
  { id: 'sunnies', name: 'Oversized Sunnies', slot: 'glasses', cat: 'acc', group: 'Eyewear', color: '#3a2b33', vb: '128 100 144 70',
    draw: c => sym(`<path d="M154,124 L194,124 Q196,150 178,152 Q156,152 154,124 Z" fill="${c}" fill-opacity=".88" stroke="${shade(c, -0.4)}" stroke-width="2.4" stroke-linejoin="round"/><path d="M160,130 L170,128" stroke="#fff" stroke-width="2" opacity=".35" stroke-linecap="round"/><path d="M154,126 L141,126" stroke="${shade(c, -0.4)}" stroke-width="2.4"/>`) +
      `<path d="M194,128 L206,128" stroke="${shade(c, -0.4)}" stroke-width="2.6"/>` },
  { id: 'heartsunnies', name: 'Heart Sunnies', slot: 'glasses', cat: 'acc', group: 'Eyewear', color: '#ff5d8f', vb: '128 100 144 70',
    draw: c => sym(`<path d="M175,152 C154,140 154,122 166,122 Q173,122 175,129 Q177,122 184,122 C196,122 196,140 175,152 Z" fill="${c}" fill-opacity=".75" stroke="${shade(c, -0.35)}" stroke-width="2.2"/><path d="M157,128 L141,126" stroke="${shade(c, -0.35)}" stroke-width="2.2"/>`) +
      `<path d="M193,130 Q200,126 207,130" fill="none" stroke="${shade(c, -0.35)}" stroke-width="2.2"/>` },
  // hats
  { id: 'beret', name: 'Parisian Beret', slot: 'hat', cat: 'acc', group: 'Hats', color: '#b3263a', vb: '120 0 180 90',
    draw: c => `<ellipse cx="214" cy="46" rx="74" ry="24" transform="rotate(-9 214 46)" fill="${c}" ${st(c)}/><path d="M150,58 Q210,72 280,42" fill="none" stroke="${shade(c, -0.2)}" stroke-width="3"/><path d="M216,22 l2,-8" stroke="${shade(c, -0.3)}" stroke-width="3" stroke-linecap="round"/>` },
  { id: 'sunhat', name: 'Wide Sun Hat', slot: 'hat', cat: 'acc', group: 'Hats', color: '#e9d3a5', vb: '70 0 260 100',
    draw: c => `<ellipse cx="200" cy="66" rx="122" ry="26" fill="${c}" ${st(c)}/><path d="M146,66 C146,8 254,8 254,66 Z" fill="${c}" ${st(c)}/><path d="M147,52 Q200,62 253,52 L254,64 Q200,74 146,64 Z" fill="#1f1b22"/>` +
      Array.from({ length: 6 }, (_, i) => `<path d="M${100 + i * 40},70 Q${105 + i * 40},76 ${110 + i * 40},70" fill="none" stroke="${EDGE(c)}" stroke-width=".8" opacity=".6"/>`).join('') },
  { id: 'beanie', name: 'Knit Beanie', slot: 'hat', cat: 'acc', group: 'Hats', color: '#9fc7b8', vb: '120 0 160 110',
    draw: c => `<circle cx="200" cy="18" r="14" fill="${shade(c, 0.4)}" ${st(c)}/><path d="M134,92 C130,24 270,24 266,92 Z" fill="${c}" ${st(c)}/><rect x="130" y="80" width="140" height="22" rx="9" fill="${shade(c, -0.08)}" ${st(c)}/>` +
      Array.from({ length: 13 }, (_, i) => `<path d="M${140 + i * 10},84 L${140 + i * 10},98" stroke="${EDGE(c)}" stroke-width=".9" opacity=".5"/>`).join('') },
  { id: 'tiara', name: 'Crystal Tiara', slot: 'hat', cat: 'acc', group: 'Hats', color: '#e6c763', vb: '140 20 120 60',
    draw: c => `<path d="M156,68 L168,48 L180,62 L200,34 L220,62 L232,48 L244,68 Q200,58 156,68 Z" fill="${c}" stroke="${shade(c, -0.35)}" stroke-width="1.2" stroke-linejoin="round"/>` +
      `<circle cx="200" cy="50" r="4" fill="#ff8fb1" stroke="#fff" stroke-width="1"/><circle cx="168" cy="56" r="2.4" fill="#9fd8ff"/><circle cx="232" cy="56" r="2.4" fill="#9fd8ff"/>` },
];
// menswear (every piece fits either body)
ITEMS.push(
  { id: 'shirt', name: 'Oxford Shirt', slot: 'top', cat: 'top', color: '#dfe8f5',
    draw: c => fit('M100,236 L184,236 L200,262 L216,236 L300,236 L300,420 Q200,428 100,420 Z', c, 't',
      `<path d="M200,262 L200,420" stroke="${EDGE(c)}" stroke-width="1.2"/>` + [290, 320, 350, 380, 410].map(y => `<circle cx="204" cy="${y}" r="2" fill="${shade(c, -0.3)}"/>`).join('')) +
      sym(`<path d="M184,234 L172,256 L200,264 Z" fill="${shade(c, 0.1)}" ${st(c)}/>`),
    sleeves: c => sleeve(c, 98, 34) },
  { id: 'polo', name: 'Polo Shirt', slot: 'top', cat: 'top', color: '#2f5d8c',
    draw: c => fit('M100,236 L182,236 L200,250 L218,236 L300,236 L300,416 Q200,424 100,416 Z', c, 't',
      `<path d="M200,250 L200,292" stroke="${EDGE(c)}" stroke-width="1.2"/><circle cx="204" cy="266" r="2" fill="#fff"/><circle cx="204" cy="282" r="2" fill="#fff"/>`) +
      sym(`<path d="M182,234 L170,252 L198,254 Z" fill="${shade(c, 0.12)}" ${st(c)}/>`),
    sleeves: c => sleeve(c, 26, 35) },
  { id: 'hoodie', name: 'Cosy Hoodie', slot: 'top', cat: 'top', color: '#8a8f99', over: true,
    draw: c => `<path d="M166,240 Q200,226 234,240 Q228,264 200,272 Q172,264 166,240 Z" fill="${shade(c, -0.12)}" ${st(c)}/>` +
      fit('M100,238 L300,238 L300,452 Q200,458 100,452 Z', c, 't',
        `<path d="M150,372 L250,372 L240,426 L160,426 Z" fill="${shade(c, -0.06)}" stroke="${EDGE(c)}" stroke-width="1.2"/><path d="M192,268 L190,320 M208,268 L210,320" stroke="#f3f3f3" stroke-width="2.4" stroke-linecap="round"/><rect x="100" y="438" width="200" height="16" fill="${shade(c, -0.1)}"/>`),
    sleeves: c => sleeve(c, 100, 38) + sym(`<path d="${ARM}" pathLength="100" stroke-dasharray="0 90 10 300" fill="none" stroke="${shade(c, -0.1)}" stroke-width="38"/>`) },
  { id: 'chinos', name: 'Chinos', slot: 'bottom', cat: 'bottom', color: '#c8b48c',
    draw: c => fit('M100,394 L300,394 L300,782 L100,782 Z', c, 'l',
      `<rect x="100" y="394" width="200" height="14" fill="${shade(c, -0.12)}"/><path d="M200,410 Q199,440 193,452" fill="none" stroke="${EDGE(c)}" stroke-width="1.2"/>` +
      sym(`<path d="M178,420 L176,770" stroke="${EDGE(c)}" stroke-width=".9" opacity=".45"/><path d="M152,412 Q166,420 172,408" fill="none" stroke="${EDGE(c)}" stroke-width="1"/>`)) },
  { id: 'suitpants', name: 'Suit Trousers', slot: 'bottom', cat: 'bottom', color: '#1f2a44',
    draw: c => fit('M100,394 L300,394 L300,782 L100,782 Z', c, 'l',
      `<rect x="100" y="394" width="200" height="12" fill="${shade(c, -0.2)}"/>` + sym(`<path d="M178,416 L176,778" stroke="${shade(c, 0.25)}" stroke-width="1.2" opacity=".6"/>`)) },
  { id: 'suit', name: 'Suit Jacket', slot: 'outer', cat: 'outer', color: '#1f2a44',
    draw: c => panel('M128,246 C150,240 170,240 184,242 L194,330 L192,470 L134,470 C130,400 122,300 128,246 Z', c,
      `<path d="M144,420 L176,420" stroke="${shade(c, 0.3)}" stroke-width="1.4"/><path d="M150,300 L176,300" stroke="${shade(c, 0.3)}" stroke-width="1.2"/>`) +
      sym(`<path d="M184,242 L162,250 L172,280 L160,292 L194,330 Z" fill="${shade(c, -0.15)}" ${st(c)}/>`) +
      `<circle cx="190" cy="390" r="3.2" fill="${shade(c, 0.4)}"/><circle cx="190" cy="430" r="3.2" fill="${shade(c, 0.4)}"/><path d="M226,296 l8,-8 l6,8 z" fill="#fff"/>`,
    sleeves: c => sleeve(c, 100, 36) },
  { id: 'tux', name: 'Tuxedo Jacket', slot: 'outer', cat: 'outer', color: '#141218',
    draw: c => panel('M128,246 C150,240 170,240 184,242 L194,340 L190,462 L134,462 C130,400 122,300 128,246 Z', c, '') +
      sym(`<path d="M184,242 L164,250 L178,300 L194,340 Z" fill="#2c2833" ${st(c)}/><path d="M168,254 L182,300" stroke="#fff" stroke-width="1.5" opacity=".18"/>`) +
      `<circle cx="190" cy="396" r="3" fill="#2c2833"/>`,
    sleeves: c => sleeve(c, 100, 36) },
  { id: 'tie', name: 'Silk Tie', slot: 'necklace', cat: 'acc', group: 'Jewellery', color: '#8e1b2e', vb: '170 220 60 180',
    draw: c => `<path d="M194,238 L206,238 L208,252 L203,258 L211,372 L200,388 L189,372 L197,258 L192,252 Z" fill="${c}" ${st(c, .8)}/><path d="M198,270 L205,280 M196,300 L206,312 M195,330 L207,342" stroke="${shade(c, 0.3)}" stroke-width="1.4" opacity=".6"/>` },
  { id: 'bowtie', name: 'Bow Tie', slot: 'necklace', cat: 'acc', group: 'Jewellery', color: '#141218', vb: '170 220 60 36',
    draw: c => `<path d="M184,230 L198,237 L184,246 Z M216,230 L202,237 L216,246 Z" fill="${c}" ${st(c, .8)}/><rect x="196" y="233" width="8" height="8" rx="2" fill="${shade(c, 0.15)}"/>` },
  { id: 'oxfords', name: 'Oxford Shoes', slot: 'shoes', cat: 'shoes', color: '#3b2418', vb: '148 744 104 64',
    draw: c => sym(`<path d="M163,770 L196,770 Q201,786 195,797 L166,797 Q158,788 163,770 Z" fill="${c}" ${st(c)}/><path d="M172,776 L188,776 M172,781 L188,781" stroke="${shade(c, 0.35)}" stroke-width="1"/><path d="M166,786 Q170,794 176,795" fill="none" stroke="#fff" stroke-width="1.4" opacity=".25"/><rect x="164" y="795" width="32" height="4" rx="2" fill="${shade(c, -0.5)}"/>`) },
  { id: 'loafers', name: 'Loafers', slot: 'shoes', cat: 'shoes', color: '#7a4a2a', vb: '148 744 104 64',
    draw: c => sym(`<path d="M164,772 L196,772 Q200,788 194,797 L167,797 Q159,788 164,772 Z" fill="${c}" ${st(c)}/><path d="M170,778 Q180,784 190,778" fill="none" stroke="${shade(c, -0.35)}" stroke-width="2"/><rect x="176" y="777" width="8" height="3" fill="#d6b25e"/>`) },
  { id: 'cap', name: 'Baseball Cap', slot: 'hat', cat: 'acc', group: 'Hats', color: '#c2233f', vb: '120 0 170 100',
    draw: c => `<path d="M138,74 C136,26 264,26 262,74 Z" fill="${c}" ${st(c)}/><path d="M200,30 L200,74 M168,36 L176,74 M232,36 L224,74" stroke="${shade(c, -0.2)}" stroke-width="1"/><circle cx="200" cy="30" r="5" fill="${shade(c, -0.15)}"/>` +
      `<path d="M140,72 Q200,90 262,72 Q272,78 262,84 Q200,102 140,84 Q130,78 140,72 Z" fill="${shade(c, -0.1)}" ${st(c)}/>` },
);
const ITEM = Object.fromEntries(ITEMS.map(i => [i.id, i]));
const SLOT_ORDER = ['shoes', 'bottom', 'top', 'dress', 'outer', 'bag', 'necklace', 'earrings', 'glasses', 'hat'];
const VB = { top: '95 175 210 290', bottom: '95 380 210 420', dress: '80 175 240 500', outer: '90 220 220 400', shoes: '140 616 120 196' };

// wardrobe thumbnail: the garment on its own, on a hanger where it hangs
const HANGER = `<path d="M200,222 L200,212 q0,-9 8,-9 q7,0 7,7" fill="none" stroke="#b9a7b0" stroke-width="2.6" stroke-linecap="round"/><path d="M146,250 L200,222 L254,250" fill="none" stroke="#b9a7b0" stroke-width="3.2" stroke-linecap="round" stroke-linejoin="round"/>`;
function itemThumb(it, color) {
  let s = `<defs>${baseDefs()}</defs>`;
  if (['top', 'dress', 'outer'].includes(it.slot)) s += HANGER;
  s += it.draw(color) + (it.sleeves ? it.sleeves(color) : '');
  const vb = it.vb || VB[it.slot] || '90 200 220 400';
  return `<svg viewBox="${vb}" preserveAspectRatio="xMidYMid meet">${s}</svg>`;
}
const colOf = id => state.colors[id] || ITEM[id].color;

// ---------- starlets (fictional celebrities) ----------
const STARLETS = [
  { id: 'aria', name: 'Aria Bloom', role: 'Pop star', skin: '#f6cfb3', hair: 'long', hairColor: '#e3c07a', eyeShape: 'almond', eyeColor: '#4a78b5', brow: 'natural', preset: 'Soft Glam', underwear: '#f4c7d3', bg: 'blush' },
  { id: 'zuri', name: 'Zuri Nova', role: 'Supermodel', skin: '#5a3420', hair: 'curly', hairColor: '#1c1412', eyeShape: 'upturned', eyeColor: '#3b2414', brow: 'bold', preset: 'Editorial', underwear: '#e9c46a', bg: 'runway' },
  { id: 'mina', name: 'Mina Seo', role: 'Idol', skin: '#fde3d3', hair: 'bob', hairColor: '#1c1412', eyeShape: 'doe', eyeColor: '#3b2414', brow: 'straight', preset: 'Natural', underwear: '#ffffff', bg: 'garden' },
  { id: 'luz', name: 'Luz Ortega', role: 'Actress', skin: '#c28552', hair: 'waves', hairColor: '#6b3a1f', eyeShape: 'almond', eyeColor: '#8a6b35', brow: 'arched', preset: 'Night Out', underwear: '#1f1b22', bg: 'night' },
  { id: 'noor', name: 'Noor Sahni', role: 'Film star', skin: '#a5683d', hair: 'braids', hairColor: '#1c1412', eyeShape: 'doe', eyeColor: '#b07a2a', brow: 'feathered', preset: 'Soft Glam', underwear: '#c8b6e2', bg: 'boutique' },
  { id: 'ivy', name: 'Ivy Laurent', role: 'Designer', skin: '#fde3d3', hair: 'pixie', hairColor: '#f2a7c3', eyeShape: 'round', eyeColor: '#4f7a45', brow: 'thin', preset: 'Natural', underwear: '#a9c6ef', bg: 'boutique' },
  { id: 'kai', name: 'Kai Mensah', role: 'Musician', gender: 'm', beard: 'short', skin: '#5a3420', hair: 'locs', hairColor: '#1c1412', eyeShape: 'almond', eyeColor: '#3b2414', brow: 'bold', preset: 'Bare Face', underwear: '#1f1b22' },
  { id: 'leo', name: 'Leo Castillo', role: 'Actor', gender: 'm', beard: 'stubble', skin: '#c28552', hair: 'quiff', hairColor: '#3b2416', eyeShape: 'almond', eyeColor: '#6b4a2b', brow: 'straight', preset: 'Bare Face', underwear: '#2b3a67' },
  { id: 'jin', name: 'Jin Takeda', role: 'Dancer', gender: 'm', beard: 'none', skin: '#f6cfb3', hair: 'crop', hairColor: '#1c1412', eyeShape: 'upturned', eyeColor: '#3b2414', brow: 'straight', preset: 'Bare Face', underwear: '#ffffff' },
  { id: 'theo', name: 'Theo Brooks', role: 'Athlete', gender: 'm', beard: 'goatee', skin: '#a5683d', hair: 'buzz', hairColor: '#1c1412', eyeShape: 'round', eyeColor: '#4a2e1a', brow: 'bold', preset: 'Bare Face', underwear: '#3f7f6b' },
  { id: 'omar', name: 'Omar Haddad', role: 'Chef', gender: 'm', beard: 'short', skin: '#d9a273', hair: 'crop', hairColor: '#2a1a12', eyeShape: 'doe', eyeColor: '#6b4a2b', brow: 'natural', preset: 'Bare Face', underwear: '#c8b48c' },
];
const starletName = () => (STARLETS.find(x => x.id === state.starlet) || {}).name || 'Your doll';

// ---------- state ----------
const DEFAULT = {
  starlet: 'aria', underwear: '#f4c7d3', gender: 'f', beard: 'none',
  skin: '#f6cfb3', eyeShape: 'almond', eyeColor: '#4a78b5', brow: 'natural', browColor: 'match',
  hair: 'long', hairColor: '#e3c07a', tucked: true,
  makeup: {
    shadow: { style: 'soft', color: '#c9a2a8', opacity: 0.4 },
    liner: { style: 'tight', color: '#2a1c1c' },
    mascara: 'natural', contacts: 'none',
    lips: { color: '#c96b78', finish: 'gloss', opacity: 0.6, liner: 'none' },
    blush: { color: '#f19a9a', place: 'apple', opacity: 0.38 },
    highlight: { color: '#fff4dc', opacity: 0.35 },
    contour: { opacity: 0.15 },
  },
  outfit: { top: null, bottom: null, dress: null, outer: null, shoes: null, bag: null, necklace: null, earrings: null, glasses: null, hat: null },
  colors: {},
  focus: null,
};
const clone = o => JSON.parse(JSON.stringify(o));
const LS_CUR = 'starlife.current.v2', LS_BOOK = 'starlife.lookbook.v1';
const lsGet = (k, d) => { try { const v = localStorage.getItem(k); return v ? JSON.parse(v) : d; } catch { return d; } };
const lsSet = (k, v) => { try { localStorage.setItem(k, JSON.stringify(v)); return true; } catch { return false; } };

function merge(base, over) { // deep merge so old saves pick up new fields
  if (!over || typeof over !== 'object') return base;
  for (const k of Object.keys(base)) {
    if (base[k] && typeof base[k] === 'object' && !Array.isArray(base[k]) && k !== 'colors') base[k] = merge(base[k], over[k]);
    else if (over[k] !== undefined) base[k] = over[k];
  }
  return base;
}
function sanitize(s) {
  if (!HAIR[s.hair]) s.hair = DEFAULT.hair;
  if (!EYE_SHAPES[s.eyeShape]) s.eyeShape = DEFAULT.eyeShape;
  if (!BROWS[s.brow]) s.brow = DEFAULT.brow;
  if (!s.room || !Array.isArray(s.room.items)) s.room = clone(DEFAULT.room);
  s.room.items = s.room.items.filter(i => FURN[i.k]);
  if (!FLOORS[s.room.floor]) s.room.floor = 'oak';
  if (!/^#[0-9a-f]{6}$/i.test(s.underwear || '')) s.underwear = DEFAULT.underwear;
  if (s.gender !== 'm') s.gender = 'f';
  if (!BEARDS[s.beard]) s.beard = 'none';
  for (const k of Object.keys(s.outfit)) if (s.outfit[k] && !ITEM[s.outfit[k]]) s.outfit[k] = null;
  return s;
}
let state = clone(DEFAULT); // replaced with the saved state once the suite defaults exist (see below)
let lookbook = lsGet(LS_BOOK, []);

const getP = p => p.split('.').reduce((o, k) => o?.[k], state);
function setP(p, v) {
  const ks = p.split('.'), last = ks.pop();
  ks.reduce((o, k) => o[k], state)[last] = v;
}

// ---------- palettes ----------
const PAL = {
  skin: ['#fde3d3', '#f6cfb3', '#eab78f', '#d9a273', '#c28552', '#a5683d', '#7f4b2a', '#5a3420'],
  eyes: ['#3b2414', '#6b4a2b', '#8a6b35', '#4f7a45', '#4a78b5', '#7d8a96', '#b07a2a'],
  hair: ['#1c1412', '#3b2416', '#6b3a1f', '#8f3b1b', '#b5541c', '#c9944f', '#e3c07a', '#f1e6c8', '#c9c7cf', '#f2a7c3', '#b9a3e3', '#3f9c9a'],
  shadow: ['#c9a2a8', '#b98b6e', '#d9a05b', '#e8b4c4', '#9a5b8b', '#5b3a52', '#3a2a3f', '#4c7a8e', '#6c8f5e', '#d4a02a', '#c0c5d6'],
  liner: ['#1a1214', '#4a2e1f', '#1f2c56', '#1f5f4f', '#5b2a5e', '#c9a14a', '#ffffff'],
  lips: ['#e7a1a0', '#c96b78', '#d9475e', '#b3132c', '#8e1b2e', '#7a3b52', '#e2725b', '#c77a5e', '#ff6fa3', '#a05ac4', '#40303a'],
  lipLiner: ['#8e4a52', '#6b2c34', '#b05a52', '#4a2a2a'],
  blush: ['#f19a9a', '#f7a8b8', '#f2a17a', '#d9787e', '#b25a72', '#e88c6a'],
  hl: [{ v: '#fff4dc', l: 'Champagne' }, { v: '#ffe2e2', l: 'Rose Quartz' }, { v: '#f4f2ff', l: 'Pearl' }, { v: '#f2c98c', l: 'Bronze' }],
  cloth: ['#ffffff', '#f5f1ea', '#1f1b22', '#f4b8c8', '#e57b97', '#c2233f', '#8e1b2e', '#f6c453', '#e9a36b', '#3f7f6b', '#9fc7b8', '#a9c6ef', '#4f6d9a', '#2b3a67', '#c8b6e2', '#b18be0', '#c8a979', '#5b3b2a', '#d6b25e', '#c0c5d6'],
};
const CONTACTS = [{ v: 'none', l: 'Natural' }, { v: '#8fc6f0', l: 'Ice Blue', dot: '#8fc6f0' }, { v: '#3fae6a', l: 'Emerald', dot: '#3fae6a' }, { v: '#9b7ae0', l: 'Violet', dot: '#9b7ae0' }, { v: '#d49a3a', l: 'Honey', dot: '#d49a3a' }, { v: '#9aa4b1', l: 'Storm Grey', dot: '#9aa4b1' }, { v: '#d0587a', l: 'Rosé', dot: '#d0587a' }];

const PRESETS = {
  'Bare Face': { shadow: { style: 'none' }, liner: { style: 'none' }, mascara: 'none', lips: { color: 'none', liner: 'none' }, blush: { color: 'none' }, highlight: { opacity: 0 }, contour: { opacity: 0 } },
  'Natural': { shadow: { style: 'soft', color: '#b98b6e', opacity: 0.35 }, liner: { style: 'tight', color: '#4a2e1f' }, mascara: 'natural', lips: { color: '#e7a1a0', finish: 'matte', opacity: 0.5, liner: 'none' }, blush: { color: '#f2a17a', place: 'apple', opacity: 0.32 }, highlight: { color: '#fff4dc', opacity: 0.3 }, contour: { opacity: 0.12 } },
  'Soft Glam': { shadow: { style: 'winged', color: '#e8b4c4', opacity: 0.6 }, liner: { style: 'winged', color: '#1a1214' }, mascara: 'volume', lips: { color: '#c96b78', finish: 'gloss', opacity: 0.75, liner: '#8e4a52' }, blush: { color: '#f7a8b8', place: 'lifted', opacity: 0.45 }, highlight: { color: '#ffe2e2', opacity: 0.55 }, contour: { opacity: 0.3 } },
  'Night Out': { shadow: { style: 'smoky', color: '#5b3a52', opacity: 0.75 }, liner: { style: 'cat', color: '#1a1214' }, mascara: 'dramatic', lips: { color: '#8e1b2e', finish: 'matte', opacity: 0.92, liner: '#6b2c34' }, blush: { color: '#b25a72', place: 'draped', opacity: 0.35 }, highlight: { color: '#f4f2ff', opacity: 0.5 }, contour: { opacity: 0.42 } },
  'Editorial': { shadow: { style: 'halo', color: '#d4a02a', opacity: 0.75 }, liner: { style: 'smudged', color: '#1f2c56' }, mascara: 'volume', lips: { color: '#a05ac4', finish: 'metallic', opacity: 0.9, liner: 'none' }, blush: { color: '#e88c6a', place: 'draped', opacity: 0.3 }, highlight: { color: '#f2c98c', opacity: 0.7 }, contour: { opacity: 0.35 } },
};

// ---------- UI builders ----------
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
function chips(id, path, opts) {
  $('#' + id).innerHTML = opts.map(o => `<button class="chip" data-bind="${path}" data-val="${o.v}">${o.dot ? `<i style="background:${o.dot}"></i>` : ''}${o.l}</button>`).join('');
}
function swatches(id, path, colors, { custom = true, none = false, match = false } = {}) {
  $('#' + id).innerHTML =
    (none ? `<button class="sw none" data-bind="${path}" data-val="none" title="None" aria-label="None"></button>` : '') +
    (match ? `<button class="chip" data-bind="${path}" data-val="match">Match hair</button>` : '') +
    colors.map(c => `<button class="sw" style="--c:${c}" data-bind="${path}" data-val="${c}" title="${c}" aria-label="Colour ${c}"></button>`).join('') +
    (custom ? `<label class="sw custom" title="Custom colour" data-custom="${path}"><input type="color" data-color="${path}" aria-label="Custom colour"></label>` : '');
}
function slider(id, path, min = 0, max = 1, step = 0.01) {
  $('#' + id).innerHTML = `<input type="range" min="${min}" max="${max}" step="${step}" data-range="${path}" aria-label="${path}"><output></output>`;
}

// ---------- id scoping: every rendered SVG gets its own id namespace ----------
function scope(svg, p) {
  const ids = new Set([...svg.matchAll(/id="([^"]+)"/g)].map(m => m[1]));
  return svg.replace(/(id="|url\(#)([^")]+)/g, (m, a, b) => ids.has(b) ? a + p + b : m);
}

// ---------- full doll render ----------
function renderDoll(s, opts = {}) {
  setBody(s.gender);
  try { return renderDollBody(s, opts); } finally { setBody('f'); }
}
function renderDollBody(s, { standalone = false, width, height, vb = '0 0 400 820', inner = false } = {}) {
  const o = s.outfit;
  const I = k => (o[k] && ITEM[o[k]]) || null;
  const col = id => s.colors[id] || ITEM[id].color;
  const part = (k, p = 'draw') => { const it = I(k); return it && it[p] ? `<g data-slot="${k}" filter="url(#vol)">${it[p](col(it.id), s)}</g>` : ''; };
  const HEAD = 'translate(200,206) scale(0.62) translate(-200,-206)';
  let out = `<defs>${baseDefs()}${skinDefs(s.skin)}${faceDefs(s)}</defs>`;
  out += `<ellipse cx="200" cy="802" rx="78" ry="9" fill="#000" opacity=".22" filter="url(#blur6)"/>`;
  out += `<g transform="${HEAD}"><g filter="url(#hairTex)">${hairBack(s)}</g></g>`;
  out += `<g filter="url(#volSkin)">${bodyBase(s.skin)}</g>`;
  out += underwear(s.underwear);
  if (I('shoes') && !I('shoes').over) out += part('shoes');
  if (s.tucked) out += part('top') + part('bottom'); else out += part('bottom') + part('top');
  out += part('dress');
  if (I('shoes') && I('shoes').over) out += part('shoes');
  out += `<g filter="url(#volSkin)">${arms(s.skin)}</g>`;
  out += part('top', 'sleeves') + part('dress', 'sleeves');
  out += part('outer') + part('outer', 'sleeves');
  out += part('bag');
  out += hands(s.skin);
  out += part('necklace');
  out += `<g transform="${HEAD}">` + head(s) + `<g filter="url(#hairTex)">${hairFront(s)}</g>` + part('earrings') + part('glasses') + part('hat') + `</g>`;
  if (inner) return out;
  const size = standalone ? ` width="${width || 400}" height="${height || 820}"` : '';
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${vb}"${size} preserveAspectRatio="xMidYMax meet">${out}</svg>`;
}

// =====================================================================
// The suite: a room with furniture the starlet stands in
// =====================================================================
const ROOM_W = 1000, ROOM_H = 620, WALL_Y = 430, FLOOR_Y = 444, DOLL_H = 520;
const WALLS = ['#f8d3df', '#86aba1', '#cbbcef', '#f3ead9', '#a9c6ef', '#f6c9a8', '#2e2a4f', '#ffffff'];
const FLOORS = { oak: { l: 'Light oak', c: ['#efe6da', '#ddcdb9'] }, walnut: { l: 'Walnut', c: ['#a8774f', '#7f5639'] }, white: { l: 'Whitewash', c: ['#f8f7f4', '#e6e1da'] }, rose: { l: 'Rose tile', c: ['#f2d6dc', '#e2b8c2'] } };
const depth = y => 0.74 + 0.26 * clamp((y - FLOOR_Y) / (ROOM_H - FLOOR_Y), 0, 1);

const wood = '#7a5c45', cream = '#f3ede2', piping = '#c48a99';
const FURN = {
  window: { name: 'City window', layer: 'wall', draw: () => {
    const id = nid(), r = rng(5);
    let city = '';
    for (let x = -82; x < 82; x += 14 + r() * 10) {
      const h = 50 + r() * 110, w = 14 + r() * 16, c = ['#9aa3b8', '#b8b2c6', '#c9b7a8', '#8e9bb0', '#d6cfc4'][Math.floor(r() * 5)];
      city += `<rect x="${x.toFixed(0)}" y="${(70 - h).toFixed(0)}" width="${w.toFixed(0)}" height="${h.toFixed(0)}" fill="${c}"/>`;
      for (let wy = 74 - h; wy < 60; wy += 9) city += `<rect x="${(x + 3).toFixed(0)}" y="${wy.toFixed(0)}" width="${(w - 6).toFixed(0)}" height="3" fill="#fff" opacity=".45"/>`;
    }
    let trees = '';
    for (let x = -90; x < 95; x += 12) trees += `<circle cx="${x}" cy="${78 + r() * 14}" r="${12 + r() * 9}" fill="${['#4f7a45', '#3f6b3a', '#5f8f4f'][Math.floor(r() * 3)]}"/>`;
    return `<clipPath id="${id}"><rect x="-82" y="-114" width="164" height="228"/></clipPath>
      <rect x="-104" y="-128" width="208" height="264" rx="3" fill="#000" opacity=".08" filter="url(#fShadow)"/>
      <rect x="-96" y="-128" width="192" height="256" rx="3" fill="#fbfbfa" stroke="#e2dfda" stroke-width="2"/>
      <g clip-path="url(#${id})"><rect x="-82" y="-114" width="164" height="228" fill="url(#skyG)"/>${city}${trees}
        <path d="M-82,-60 L-20,-114 L10,-114 L-82,-20 Z" fill="#fff" opacity=".18"/></g>
      <rect x="-82" y="-10" width="164" height="10" fill="#fbfbfa"/><rect x="-82" y="-114" width="164" height="228" fill="none" stroke="#d8d4ce" stroke-width="3"/>
      <rect x="-108" y="128" width="216" height="10" rx="2" fill="#fff" stroke="#e2dfda"/>`;
  } },
  art: { name: 'Abstract print', layer: 'wall', draw: () =>
    `<rect x="-70" y="-88" width="140" height="176" fill="#000" opacity=".08" filter="url(#fShadow)"/>
     <rect x="-66" y="-86" width="132" height="172" fill="#ece3cf" stroke="#cdbf9e" stroke-width="3"/>
     <rect x="-52" y="-72" width="104" height="144" fill="#fffdf8"/>
     <circle cx="-12" cy="-22" r="30" fill="#f2b5a7"/><circle cx="18" cy="12" r="24" fill="#a9c6ef" opacity=".85"/>
     <path d="M-40,52 Q-10,20 40,46" fill="none" stroke="#2b2138" stroke-width="3"/><rect x="-30" y="30" width="16" height="16" fill="#f6c453"/>` },
  map: { name: 'Vintage map', layer: 'wall', draw: () =>
    `<rect x="-96" y="-116" width="192" height="232" fill="#000" opacity=".08" filter="url(#fShadow)"/>
     <rect x="-92" y="-114" width="184" height="228" fill="#e9e1cc" stroke="#cdbf9e" stroke-width="5"/>
     <rect x="-74" y="-96" width="148" height="192" fill="#a9d3df"/>
     <path d="M-60,-70 C-30,-90 10,-60 0,-30 C-10,0 30,10 20,50 C10,80 -40,70 -50,40 C-60,10 -80,-40 -60,-70 Z" fill="#efe7cf"/>
     <path d="M30,-80 C50,-70 60,-40 44,-30 C30,-24 20,-50 30,-80 Z" fill="#efe7cf"/>
     <path d="M-50,-40 L10,20 M-30,30 L20,-20" stroke="#c9a179" stroke-width="1" stroke-dasharray="3 3"/>` },
  mirror: { name: 'Gold mirror', layer: 'wall', draw: () =>
    `<ellipse cx="0" cy="0" rx="62" ry="92" fill="#000" opacity=".08" filter="url(#fShadow)"/>
     <ellipse cx="0" cy="0" rx="58" ry="88" fill="#e2c46a" stroke="#b8963a" stroke-width="2"/>
     <ellipse cx="0" cy="0" rx="48" ry="78" fill="#dfe9ee"/><path d="M-30,-40 L10,-70 L22,-60 L-24,-20 Z" fill="#fff" opacity=".55"/>` },
  sofa: { name: 'Cream sofa', layer: 'floor', w: 380, draw: () => {
    const cush = [-1, 0, 1].map(i => `<rect x="${i * 112 - 54}" y="-158" width="108" height="86" rx="20" fill="${cream}" stroke="${piping}" stroke-width="1.6"/>`).join('');
    const seat = [-1, 0, 1].map(i => `<rect x="${i * 112 - 54}" y="-86" width="108" height="30" rx="10" fill="#f7f2e8" stroke="${piping}" stroke-width="1.6"/>`).join('');
    return `<rect x="-170" y="-12" width="10" height="12" fill="${wood}"/><rect x="160" y="-12" width="10" height="12" fill="${wood}"/>
      <rect x="-188" y="-72" width="376" height="62" rx="12" fill="${cream}" stroke="${piping}" stroke-width="1.6"/>${cush}${seat}
      <rect x="-206" y="-122" width="48" height="112" rx="22" fill="${cream}" stroke="${piping}" stroke-width="1.6"/>
      <rect x="158" y="-122" width="48" height="112" rx="22" fill="${cream}" stroke="${piping}" stroke-width="1.6"/>
      <path d="M-150,-120 Q-110,-110 -80,-126 M40,-130 Q70,-118 100,-128" stroke="#e4dccd" stroke-width="2" fill="none"/>`;
  } },
  armchair: { name: 'Armchair', layer: 'floor', w: 170, draw: () =>
    `<rect x="-70" y="-12" width="8" height="12" fill="${wood}"/><rect x="62" y="-12" width="8" height="12" fill="${wood}"/>
     <rect x="-62" y="-150" width="124" height="90" rx="22" fill="${cream}" stroke="${piping}" stroke-width="1.6"/>
     <rect x="-80" y="-74" width="160" height="64" rx="12" fill="${cream}" stroke="${piping}" stroke-width="1.6"/>
     <rect x="-58" y="-86" width="116" height="28" rx="10" fill="#f7f2e8" stroke="${piping}" stroke-width="1.6"/>
     <rect x="-92" y="-116" width="40" height="106" rx="18" fill="${cream}" stroke="${piping}" stroke-width="1.6"/>
     <rect x="52" y="-116" width="40" height="106" rx="18" fill="${cream}" stroke="${piping}" stroke-width="1.6"/>` },
  rocker: { name: 'Pink rocker', layer: 'floor', w: 230, draw: () =>
    `<path d="M-120,-4 Q0,16 118,-10" fill="none" stroke="${wood}" stroke-width="7" stroke-linecap="round"/>
     <path d="M-76,-2 L-62,-74 M72,-4 L58,-70 M-40,2 L-30,-74 M40,0 L30,-70" stroke="${wood}" stroke-width="6" stroke-linecap="round"/>
     <path d="M-92,-246 L-58,-254 L-28,-96 L-66,-86 Z" fill="#e2589a" stroke="#b83c78" stroke-width="1.5"/>
     <path d="M-74,-74 L84,-60 L88,-88 L-60,-104 Z" fill="#ec6aa9" stroke="#b83c78" stroke-width="1.5"/>
     <path d="M-80,-128 L64,-120 M64,-120 L60,-86 M-46,-126 L-40,-96" stroke="${wood}" stroke-width="7" stroke-linecap="round"/>` },
  sidetable: { name: 'Lamp table', layer: 'floor', w: 150, draw: () =>
    `<ellipse cx="0" cy="-2" rx="44" ry="8" fill="#e9e6e2" stroke="#cfcac4"/>
     <path d="M-10,-6 C-14,-40 -24,-70 -8,-100 L8,-100 C24,-70 14,-40 10,-6 Z" fill="#f4f2ef" stroke="#cfcac4"/>
     <ellipse cx="0" cy="-104" rx="70" ry="13" fill="#f7f5f2" stroke="#cfcac4"/><rect x="-70" y="-104" width="140" height="8" fill="#eeeae5"/>
     <path d="M-14,-112 Q0,-150 14,-112 Z" fill="#fff" stroke="#d9d4ce"/><rect x="-2" y="-176" width="4" height="40" fill="#c9a24a"/>
     <path d="M-44,-170 L44,-170 L26,-236 L-26,-236 Z" fill="#f06aa0" stroke="#c94f84"/>
     <ellipse cx="0" cy="-170" rx="44" ry="6" fill="#ffd6e6" opacity=".9"/>` },
  plant: { name: 'Monstera', layer: 'floor', w: 170, draw: () => {
    const r = rng(9); let leaves = '';
    for (let i = 0; i < 11; i++) {
      const a = -170 + i * 16 + r() * 8, rad = a * Math.PI / 180, d = 60 + r() * 50;
      const x = Math.cos(rad) * d, y = -150 + Math.sin(rad) * d * 0.9;
      leaves += `<path d="M0,-140 Q${(x * 0.5).toFixed(0)},${(-150 + (y + 150) * 0.3).toFixed(0)} ${x.toFixed(0)},${y.toFixed(0)}" stroke="#2c5e33" stroke-width="2" fill="none"/>` +
        `<ellipse cx="${x.toFixed(0)}" cy="${y.toFixed(0)}" rx="${(26 + r() * 10).toFixed(0)}" ry="${(18 + r() * 6).toFixed(0)}" transform="rotate(${(a + 90).toFixed(0)} ${x.toFixed(0)} ${y.toFixed(0)})" fill="${i % 2 ? '#2f7a3e' : '#3d8f4a'}" stroke="#235a2c" stroke-width="1"/>` +
        `<path d="M${x.toFixed(0)},${y.toFixed(0)} l${(Math.cos(rad) * 18).toFixed(0)},${(Math.sin(rad) * 14).toFixed(0)}" stroke="#235a2c" stroke-width="1.2"/>`;
    }
    return `<path d="M-30,0 L-20,-100 M30,0 L20,-100 M0,0 L0,-100" stroke="#222" stroke-width="3"/><path d="M-28,-60 L28,-60" stroke="#222" stroke-width="2"/>
      <path d="M-44,-134 Q-44,-96 0,-94 Q44,-96 44,-134 Z" fill="#fbfbfa" stroke="#d9d6d1"/>${leaves}`;
  } },
  coffeetable: { name: 'Glass table', layer: 'floor', w: 320, draw: () =>
    `<rect x="-150" y="-92" width="6" height="92" fill="#b9c2c6"/><rect x="144" y="-92" width="6" height="92" fill="#b9c2c6"/>
     <rect x="-122" y="-110" width="4" height="70" fill="#c9d1d4" opacity=".7"/><rect x="118" y="-110" width="4" height="70" fill="#c9d1d4" opacity=".7"/>
     <path d="M-130,-112 L130,-112 L160,-92 L-160,-92 Z" fill="#cfe6ea" fill-opacity=".45" stroke="#9fbfc6" stroke-width="1.5"/>
     <rect x="-160" y="-92" width="320" height="6" fill="#bcd9de" opacity=".8"/>` },
  rug: { name: 'Pink rug', layer: 'rug', w: 440, draw: () => {
    const id = nid();
    return `<pattern id="${id}" width="40" height="22" patternUnits="userSpaceOnUse"><rect width="40" height="22" fill="#e45f95"/>
        <path d="M20,3 C28,6 28,16 20,19 C12,16 12,6 20,3 Z" fill="#c9437a"/><circle cx="0" cy="11" r="4" fill="#f08ab3"/><circle cx="40" cy="11" r="4" fill="#f08ab3"/></pattern>
      <path d="M-180,-44 L180,-44 L230,28 L-230,28 Z" fill="url(#${id})" stroke="#c9437a" stroke-width="2"/>`;
  } },
  floorlamp: { name: 'Arc lamp', layer: 'floor', w: 120, draw: () =>
    `<ellipse cx="0" cy="-3" rx="26" ry="6" fill="#2b2b2b"/><path d="M0,-6 L0,-250 Q0,-300 60,-300" fill="none" stroke="#2b2b2b" stroke-width="4"/>
     <path d="M40,-300 Q60,-330 84,-300 Z" fill="#f6c453" stroke="#c9a24a"/><ellipse cx="62" cy="-298" rx="20" ry="4" fill="#fff6c9"/>` },
  gift: { name: 'Gift box', layer: 'floor', w: 110, draw: () =>
    `<rect x="-46" y="-70" width="92" height="70" rx="3" fill="#1f1b22"/><rect x="-50" y="-82" width="100" height="16" rx="3" fill="#2d2830"/>
     <rect x="-6" y="-82" width="12" height="82" fill="#a37ef5"/><path d="M0,-84 C-30,-120 -44,-84 0,-84 C30,-120 44,-84 0,-84 Z" fill="#b896f8" stroke="#8a62e8"/>
     <rect x="16" y="-48" width="18" height="22" rx="2" fill="#e8c96a" transform="rotate(18 25 -37)"/>` },
  boxes: { name: 'Moving box', layer: 'floor', w: 200, draw: () =>
    `<path d="M-90,-120 L60,-120 L96,-96 L-54,-96 Z" fill="#c8915e"/><rect x="-90" y="-120" width="150" height="120" fill="#c48a55"/>
     <path d="M60,-120 L96,-96 L96,18 L60,0 Z" fill="#a8723f"/><rect x="-28" y="-120" width="26" height="40" fill="#d9b183" opacity=".9"/>
     <text x="-15" y="-46" text-anchor="middle" font-family="Georgia,serif" font-size="14" font-weight="700" fill="#8f5f33" opacity=".75">MOVING CO.</text>
     <circle cx="38" cy="-90" r="8" fill="#5ee0c4"/>` },
};

// furniture for everyday life
Object.assign(FURN, {
  bed: { name: 'Cosy bed', layer: 'floor', w: 320, draw: () =>
    `<rect x="-150" y="-34" width="300" height="34" rx="4" fill="#7a5c45"/>
     <rect x="-150" y="-206" width="300" height="116" rx="20" fill="#e7c6d3" stroke="#c99cb0" stroke-width="2"/>
     ${[-100, -50, 0, 50, 100].map(x => `<circle cx="${x}" cy="-160" r="4" fill="#c99cb0"/>`).join('')}
     <rect x="-158" y="-114" width="316" height="88" rx="12" fill="#fffaf7" stroke="#e4d9d2"/>
     <ellipse cx="-72" cy="-118" rx="58" ry="18" fill="#fff" stroke="#e4d9d2"/><ellipse cx="72" cy="-118" rx="58" ry="18" fill="#fff" stroke="#e4d9d2"/>
     <path d="M-158,-92 h316 v54 q0,12 -12,12 h-292 q-12,0 -12,-12 z" fill="#b9a3e3" stroke="#9a84cc"/>
     <path d="M-158,-80 h316" stroke="#fff" stroke-width="5" opacity=".5"/><path d="M-120,-60 q60,10 120,0 t120,0" fill="none" stroke="#9a84cc" opacity=".6"/>` },
  kitchen: { name: 'Kitchenette', layer: 'floor', w: 330, draw: () =>
    `<rect x="-166" y="-112" width="230" height="112" fill="#f7f4ef" stroke="#d8d2c8"/>
     ${[-110, -54, 2].map(x => `<rect x="${x}" y="-96" width="50" height="86" fill="none" stroke="#d8d2c8"/><rect x="${x + 21}" y="-60" width="8" height="3" rx="1.5" fill="#a9a29a"/>`).join('')}
     <rect x="-172" y="-124" width="242" height="14" rx="3" fill="#cfc4b6"/>
     <rect x="-140" y="-150" width="40" height="26" rx="5" fill="#c94f5c"/><rect x="-146" y="-152" width="52" height="5" rx="2" fill="#a83d49"/>
     <path d="M-30,-124 v-24 q0,-10 12,-10 h6" fill="none" stroke="#b9c2c6" stroke-width="4"/>
     <rect x="20" y="-148" width="26" height="24" rx="3" fill="#9fc7b8"/><path d="M26,-148 q7,-14 14,0" fill="#5a9a5f"/>
     <rect x="72" y="-246" width="88" height="246" rx="9" fill="#e9eef2" stroke="#c9d1d8" stroke-width="2"/>
     <path d="M72,-152 h88" stroke="#c9d1d8" stroke-width="2"/><rect x="146" y="-226" width="5" height="50" rx="2.5" fill="#a9b2ba"/><rect x="146" y="-138" width="5" height="60" rx="2.5" fill="#a9b2ba"/>
     <circle cx="100" cy="-200" r="7" fill="#ff8fb3"/><circle cx="116" cy="-210" r="5" fill="#ffd166"/>` },
  shower: { name: 'Glass shower', layer: 'floor', w: 150, draw: () => {
    let tiles = ''; for (let y = -262; y < -8; y += 22) for (let x = -62; x < 62; x += 22) tiles += `<rect x="${x}" y="${y}" width="21" height="21" fill="#f2f8fa" stroke="#dcebf0"/>`;
    return `<g>${tiles}</g><path d="M36,-262 v-8 h-22 v26" stroke="#b9c2c6" stroke-width="4" fill="none"/><ellipse cx="14" cy="-240" rx="13" ry="5" fill="#b9c2c6"/>
      ${[0, 1, 2, 3, 4].map(i => `<path d="M${6 + i * 4},-232 l${-3 + i * 1.5},40" stroke="#9fd0e6" stroke-width="1.4" opacity=".6"/>`).join('')}
      <rect x="-68" y="-272" width="136" height="272" rx="4" fill="#d7ecf4" fill-opacity=".45" stroke="#9fc5d3" stroke-width="3"/>
      <path d="M-60,-150 L-10,-262 L10,-262 L-60,-100 Z" fill="#fff" opacity=".35"/><rect x="-74" y="-10" width="148" height="10" rx="3" fill="#fff" stroke="#c9d1d8"/>`;
  } },
  tv: { name: 'TV & console', layer: 'floor', w: 240, draw: () =>
    `<rect x="-114" y="-58" width="228" height="58" rx="5" fill="#8a6a50"/><path d="M-114,-30 h228 M-38,-58 v58 M38,-58 v58" stroke="#6f533e" stroke-width="2"/>
     <rect x="-8" y="-76" width="16" height="18" fill="#2b2b30"/><rect x="-40" y="-60" width="80" height="4" fill="#2b2b30"/>
     <rect x="-104" y="-196" width="208" height="122" rx="6" fill="#14121a"/><rect x="-98" y="-190" width="196" height="110" rx="3" fill="#202a4a"/>
     <path d="M-94,-186 L-20,-186 L-84,-84 L-94,-84 Z" fill="#fff" opacity=".07"/><rect x="60" y="-52" width="34" height="16" rx="4" fill="#e9e6e2"/>` },
  bookshelf: { name: 'Bookshelf', layer: 'floor', w: 180, draw: () => {
    const r = rng(14); let b = '';
    for (let sh = 0; sh < 5; sh++) { const base = -24 - sh * 56; for (let x = -72; x < 70;) { const w = 8 + r() * 9, h = 34 + r() * 16; b += `<rect x="${x.toFixed(0)}" y="${(base - h).toFixed(0)}" width="${w.toFixed(0)}" height="${h.toFixed(0)}" fill="${['#c94f5c', '#e9b44c', '#4f7a9c', '#5e9c76', '#f2e3c6', '#9b5de5', '#2b3a67'][Math.floor(r() * 7)]}"/>`; x += w + 1; } b += `<rect x="-80" y="${base}" width="160" height="6" fill="#5f3826"/>`; }
    return `<rect x="-84" y="-310" width="168" height="310" fill="#6b4a35"/><rect x="-76" y="-302" width="152" height="296" fill="#4a2f22"/>${b}`;
  } },
  desk: { name: 'Work desk', layer: 'floor', w: 230, draw: () =>
    `<rect x="-104" y="-90" width="8" height="90" fill="#9a8a78"/><rect x="96" y="-90" width="8" height="90" fill="#9a8a78"/>
     <rect x="40" y="-86" width="56" height="70" fill="#efe6d9" stroke="#d8cdbd"/><path d="M40,-52 h56" stroke="#d8cdbd"/><rect x="62" y="-72" width="12" height="3" fill="#a99"/><rect x="62" y="-38" width="12" height="3" fill="#a99"/>
     <rect x="-112" y="-100" width="224" height="12" rx="3" fill="#e8dccb"/>
     <path d="M-46,-100 L46,-100 L52,-94 L-52,-94 Z" fill="#c0c5ce"/><rect x="-40" y="-154" width="80" height="54" rx="4" fill="#c0c5ce"/><rect x="-36" y="-150" width="72" height="46" rx="2" fill="#2b3a67"/>
     <path d="M-30,-140 h40 M-30,-130 h28 M-30,-120 h34" stroke="#9fc7ff" stroke-width="2" opacity=".7"/>
     <path d="M70,-100 v-40 l20,-16" fill="none" stroke="#333" stroke-width="3"/><path d="M80,-162 l26,-6 l-6,24 z" fill="#ffd166"/>` },
  treadmill: { name: 'Treadmill', layer: 'floor', w: 230, draw: () =>
    `<path d="M-112,-4 L98,-4 L110,-26 L-92,-26 Z" fill="#2b2b30"/><path d="M-90,-22 L100,-22" stroke="#55555d" stroke-width="5"/>
     <path d="M84,-26 L98,-150" stroke="#3a3a42" stroke-width="8" stroke-linecap="round"/><rect x="70" y="-176" width="56" height="28" rx="6" fill="#3a3a42"/><rect x="78" y="-170" width="30" height="14" rx="3" fill="#5ee0c4"/>` },
  stereo: { name: 'Record player', layer: 'floor', w: 170, draw: () =>
    `<rect x="-80" y="-110" width="40" height="110" rx="5" fill="#2b2b30"/><circle cx="-60" cy="-76" r="12" fill="#444"/><circle cx="-60" cy="-34" r="16" fill="#444"/>
     <rect x="40" y="-110" width="40" height="110" rx="5" fill="#2b2b30"/><circle cx="60" cy="-76" r="12" fill="#444"/><circle cx="60" cy="-34" r="16" fill="#444"/>
     <rect x="-36" y="-62" width="72" height="62" rx="4" fill="#a0714f"/><rect x="-36" y="-74" width="72" height="12" rx="3" fill="#8a5f42"/>
     <ellipse cx="-4" cy="-76" rx="24" ry="5" fill="#141218"/><circle cx="-4" cy="-76" r="3" fill="#ff4f8b"/><path d="M22,-80 l-14,6" stroke="#ccc" stroke-width="2"/>` },
});

function roomSVG(s, { vb = `0 0 ${ROOM_W} ${ROOM_H}`, standalone = false, scale = 1, aspect = 'xMidYMax slice', guests = [] } = {}) {
  const r = s.room, wall = r.wall, fl = (FLOORS[r.floor] || FLOORS.oak).c;
  let o = `<defs>${baseDefs()}
    <linearGradient id="wallG" x1="0" x2="1"><stop offset="0" stop-color="${shade(wall, -0.14)}"/><stop offset=".28" stop-color="${wall}"/><stop offset=".78" stop-color="${wall}"/><stop offset="1" stop-color="${shade(wall, -0.08)}"/></linearGradient>
    <linearGradient id="floorG" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${fl[1]}"/><stop offset="1" stop-color="${fl[0]}"/></linearGradient>
    <linearGradient id="skyG" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8fc6ef"/><stop offset="1" stop-color="#e8f4fb"/></linearGradient>
    <filter id="fShadow" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="8"/></filter>
    <clipPath id="floorClip"><path d="M0,${FLOOR_Y} L950,${FLOOR_Y} L1000,484 L1000,${ROOM_H} L0,${ROOM_H} Z"/></clipPath></defs>`;
  o += `<rect width="${ROOM_W}" height="${WALL_Y}" fill="url(#wallG)"/><rect width="${ROOM_W}" height="40" fill="#000" opacity=".05" filter="url(#blur6)"/>`;
  o += `<path d="M950,0 L1000,0 L1000,470 L950,${WALL_Y} Z" fill="${shade(wall, -0.2)}"/>`;
  const place = (it, inner) => `<g data-furn="${it.id}" transform="translate(${it.x.toFixed(1)},${it.y.toFixed(1)}) scale(${(FURN[it.k].layer === 'wall' ? 1 : depth(it.y)).toFixed(3)})">${inner}</g>`;
  for (const it of r.items.filter(i => FURN[i.k]?.layer === 'wall')) o += place(it, FURN[it.k].draw());
  o += `<path d="M0,${WALL_Y} L950,${WALL_Y} L950,${FLOOR_Y} L0,${FLOOR_Y} Z" fill="#fbfbfa"/><path d="M950,${WALL_Y} L1000,470 L1000,484 L950,${FLOOR_Y} Z" fill="#ecebe8"/>`;
  // wood floor with planks narrowing into the distance
  let planks = '';
  const rr = rng(21);
  for (let k = 1; k <= 12; k++) {
    const y = FLOOR_Y + (ROOM_H - FLOOR_Y) * Math.pow(k / 12, 1.45), prev = FLOOR_Y + (ROOM_H - FLOOR_Y) * Math.pow((k - 1) / 12, 1.45);
    planks += `<path d="M0,${y.toFixed(1)} L1000,${y.toFixed(1)}" stroke="#000" stroke-opacity=".09" stroke-width="1"/>`;
    for (let x = rr() * 200; x < 1000; x += 160 + rr() * 140) planks += `<path d="M${x.toFixed(0)},${prev.toFixed(1)} L${x.toFixed(0)},${y.toFixed(1)}" stroke="#000" stroke-opacity=".08"/>`;
    planks += `<path d="M${(rr() * 600).toFixed(0)},${((prev + y) / 2).toFixed(1)} l${(120 + rr() * 200).toFixed(0)},0" stroke="#fff" stroke-opacity=".25" stroke-width="1.5"/>`;
  }
  o += `<path d="M0,${FLOOR_Y} L950,${FLOOR_Y} L1000,484 L1000,${ROOM_H} L0,${ROOM_H} Z" fill="url(#floorG)"/><g clip-path="url(#floorClip)">${planks}</g>`;
  for (const it of r.items.filter(i => FURN[i.k]?.layer === 'rug')) o += place(it, FURN[it.k].draw());
  // floor furniture and the doll, back to front so nearer things overlap farther ones
  const floor = r.items.filter(i => FURN[i.k]?.layer === 'floor').map(i => ({ y: i.y, svg: place(i, `<ellipse cx="0" cy="0" rx="${(FURN[i.k].w || 150) * 0.46}" ry="10" fill="#000" opacity=".13" filter="url(#fShadow)"/>` + FURN[i.k].draw()) }));
  const d = r.doll, sc = depth(d.y), dw = DOLL_H * 400 / 820;
  floor.push({ y: d.y, svg: `<g data-doll="1" transform="translate(${d.x.toFixed(1)},${d.y.toFixed(1)}) scale(${sc.toFixed(3)})"><svg x="${(-dw / 2).toFixed(1)}" y="${(-DOLL_H * 802 / 820).toFixed(1)}" width="${dw.toFixed(1)}" height="${DOLL_H}" viewBox="0 0 400 820" overflow="visible">${scope(renderDoll(s, { inner: true }), 'me-')}</svg></g>` });
  guests.forEach((g, i) => floor.push({ y: g.y, svg: dollAt(g.s, g.x, g.y, `gu${i}-`) }));
  floor.sort((a, b) => a.y - b.y).forEach(f => { o += f.svg; });
  const tint = timeTint();
  if (tint) o += `<rect width="${ROOM_W}" height="${ROOM_H}" fill="${tint[0]}" opacity="${tint[1].toFixed(2)}" style="mix-blend-mode:multiply" pointer-events="none"/>`;
  const size = standalone ? ` width="${ROOM_W * scale}" height="${ROOM_H * scale}"` : '';
  return `<svg xmlns="http://www.w3.org/2000/svg" class="suite-room" viewBox="${vb}"${size} preserveAspectRatio="${aspect}">${o}</svg>`;
}
const furnThumb = k => {
  const f = FURN[k];
  const vb = f.layer === 'wall' ? '-120 -140 240 290' : f.layer === 'rug' ? '-240 -60 480 100' : `${-(f.w || 200) / 2 - 20} -320 ${(f.w || 200) + 40} 330`;
  return `<svg viewBox="${vb}" preserveAspectRatio="xMidYMax meet"><defs><filter id="fShadow"><feGaussianBlur stdDeviation="8"/></filter><linearGradient id="skyG" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8fc6ef"/><stop offset="1" stop-color="#e8f4fb"/></linearGradient></defs>${f.draw()}</svg>`;
};

// ---------- default suite ----------
DEFAULT.room = {
  wall: '#f8d3df', floor: 'oak', doll: { x: 540, y: 604 },
  items: [
    { id: 'w1', k: 'window', x: 330, y: 205 }, { id: 'w2', k: 'map', x: 780, y: 190 },
    { id: 'f1', k: 'rug', x: 560, y: 572 }, { id: 'f2', k: 'rocker', x: 175, y: 598 },
    { id: 'f3', k: 'sidetable', x: 360, y: 588 }, { id: 'f4', k: 'plant', x: 850, y: 590 },
  ],
};
delete DEFAULT.bg;
state = sanitize(merge(clone(DEFAULT), lsGet(LS_CUR, null)));

// =====================================================================
// UI
// =====================================================================
let view = 'suite', activeTab = 'avatar', activeCat = 'top', hairSig = '', drawer = null, decorMode = false, zoom = 'face';
const HOST = { name: 'Stella' };

function buildUI() {
  chips('genderRow', 'gender', [{ v: 'f', l: 'Feminine' }, { v: 'm', l: 'Masculine' }]);
  chips('beardRow', 'beard', Object.entries(BEARDS).map(([v, l]) => ({ v, l })));
  swatches('skinRow', 'skin', PAL.skin);
  chips('eyeShapeRow', 'eyeShape', Object.entries(EYE_SHAPES).map(([v, e]) => ({ v, l: e.label })));
  swatches('eyeColorRow', 'eyeColor', PAL.eyes);
  chips('browRow', 'brow', Object.entries(BROWS).map(([v, b]) => ({ v, l: b.label })));
  swatches('browColorRow', 'browColor', ['#1c1412', '#3b2416', '#6b3a1f', '#a77b4f', '#d8c3a0'], { match: true });
  swatches('hairColorRow', 'hairColor', PAL.hair);
  swatches('underwearRow', 'underwear', ['#f4c7d3', '#ffffff', '#1f1b22', '#e9c46a', '#c8b6e2', '#a9c6ef', '#e8c3a0', '#d9475e']);
  $('#presetRow').innerHTML = Object.entries(PRESETS).map(([name, p]) =>
    `<button class="preset" data-preset="${name}"><b>${name}</b><span>${[p.shadow.color, p.lips.color, p.blush.color].filter(c => c && c !== 'none').map(c => `<i style="background:${c}"></i>`).join('') || '<i style="background:#f1e5ea"></i>'}</span></button>`).join('');
  chips('shadowStyleRow', 'makeup.shadow.style', [{ v: 'none', l: 'None' }, { v: 'soft', l: 'Soft Wash' }, { v: 'smoky', l: 'Smoky' }, { v: 'winged', l: 'Winged' }, { v: 'halo', l: 'Halo' }]);
  swatches('shadowColorRow', 'makeup.shadow.color', PAL.shadow);
  slider('shadowOpacity', 'makeup.shadow.opacity');
  chips('linerStyleRow', 'makeup.liner.style', [{ v: 'none', l: 'None' }, { v: 'tight', l: 'Tightline' }, { v: 'classic', l: 'Classic' }, { v: 'winged', l: 'Winged' }, { v: 'cat', l: 'Cat-eye' }, { v: 'smudged', l: 'Smudged' }]);
  swatches('linerColorRow', 'makeup.liner.color', PAL.liner);
  chips('mascaraRow', 'makeup.mascara', [{ v: 'none', l: 'None' }, { v: 'natural', l: 'Natural' }, { v: 'volume', l: 'Volume' }, { v: 'dramatic', l: 'Dramatic' }]);
  chips('contactsRow', 'makeup.contacts', CONTACTS);
  swatches('lipColorRow', 'makeup.lips.color', PAL.lips, { none: true });
  chips('lipFinishRow', 'makeup.lips.finish', [{ v: 'matte', l: 'Matte' }, { v: 'gloss', l: 'Gloss' }, { v: 'metallic', l: 'Metallic' }]);
  slider('lipOpacity', 'makeup.lips.opacity');
  swatches('lipLinerRow', 'makeup.lips.liner', PAL.lipLiner, { none: true });
  swatches('blushColorRow', 'makeup.blush.color', PAL.blush, { none: true });
  chips('blushPlaceRow', 'makeup.blush.place', [{ v: 'apple', l: 'Apples' }, { v: 'lifted', l: 'Lifted' }, { v: 'draped', l: 'Draped' }]);
  slider('blushOpacity', 'makeup.blush.opacity', 0, 0.8);
  chips('hlColorRow', 'makeup.highlight.color', PAL.hl.map(h => ({ ...h, dot: h.v })));
  slider('hlOpacity', 'makeup.highlight.opacity');
  slider('contourOpacity', 'makeup.contour.opacity', 0, 0.7);
  // suite decorating
  swatches('wallRow', 'room.wall', WALLS);
  chips('floorRow', 'room.floor', Object.entries(FLOORS).map(([v, f]) => ({ v, l: f.l, dot: f.c[1] })));
  $('#furnGrid').innerHTML = Object.entries(FURN).map(([k, f]) => `<button class="furn-card" data-addfurn="${k}" title="Add ${f.name}">${scope(furnThumb(k), 'F' + k + '-')}<span>${f.name}</span></button>`).join('');
}

function portrait(s, lips = '#c96b78') {
  setBody(s.gender);
  try { return portraitBody(s, lips); } finally { setBody('f'); }
}
function portraitBody(s, lips) {
  const sk = s.skin, e = shade(sk, -0.22);
  return hairBack(s) +
    `<path d="M186,188 L186,246 L214,246 L214,188 Z" fill="${sk}" stroke="${e}"/>` +
    sym(`<ellipse cx="141" cy="136" rx="8" ry="14" fill="${sk}" stroke="${e}"/>`) +
    `<path d="${FACE}" fill="${sk}" stroke="${e}"/>` +
    sym(`<ellipse cx="175" cy="136" rx="5" ry="3.4" fill="#2a1c1c"/>`) +
    `<path d="M190,184 Q200,190 210,184" fill="none" stroke="${s.gender === 'm' ? shade(sk, -0.35) : lips}" stroke-width="3" stroke-linecap="round"/>` +
    (s.beard && s.beard !== 'none' ? `<path d="M146,140 C148,180 168,208 200,214 C232,208 252,180 254,140 C248,170 236,186 220,192 Q200,200 180,192 C164,186 152,170 146,140 Z" fill="${shade(s.hairColor, -0.05)}" opacity="${s.beard === 'stubble' ? .4 : .95}"/>` : '') +
    hairFront(s);
}
function renderHairCards() {
  const sig = state.skin + state.hairColor + state.hair;
  if (sig === hairSig) return;
  hairSig = sig;
  $('#hairGrid').innerHTML = Object.entries(HAIR).map(([k, h]) =>
    `<button class="hair-card" data-bind="hair" data-val="${k}"><svg viewBox="96 -6 208 240">${portrait({ ...state, hair: k }, shade(state.skin, -0.35))}</svg><span>${h.label}</span></button>`).join('');
}
function renderGrid() {
  const list = ITEMS.filter(i => i.cat === activeCat && owns(i.id));
  const locked = ITEMS.filter(i => i.cat === activeCat && !owns(i.id)).length;
  let html = '', grp = null;
  for (const it of list) {
    if (it.group && it.group !== grp) { grp = it.group; html += `<div class="group-label">${grp}</div>`; }
    const c = state.colors[it.id] || it.color;
    html += `<button class="item${state.outfit[it.slot] === it.id ? ' on' : ''}" draggable="false" data-item="${it.id}" title="${it.name}">${scope(itemThumb(it, c), 'T' + it.id + '-')}<span>${it.name}</span></button>`;
  }
  if (locked) html += `<button class="item more" data-goshop><span class="more-n">+${locked}</span><span>More in the Shop</span></button>`;
  $('#itemGrid').innerHTML = html;
  renderRecolor();
}

function renderRecolor() {
  const box = $('#recolor');
  const f = state.focus && ITEM[state.focus];
  const worn = f && state.outfit[f.slot] === f.id;
  if (!worn) { box.innerHTML = ''; return; }
  const cur = state.colors[f.id] || f.color;
  box.innerHTML = `<h5>Recolour · <span>${f.name}</span></h5><div class="row">` +
    PAL.cloth.map(c => `<button class="sw${c === cur ? ' on' : ''}" style="--c:${c}" data-recolor="${c}" aria-label="Colour ${c}"></button>`).join('') +
    `<label class="sw custom" title="Custom colour"><input type="color" data-recolor-input value="${cur}" aria-label="Custom colour"></label>` +
    `<button class="chip" data-recolor="reset">Original</button></div>`;
}

function renderLayers() {
  const o = state.outfit;
  const order = state.tucked ? ['shoes', 'top', 'bottom', 'dress', 'outer'] : ['shoes', 'bottom', 'top', 'dress', 'outer'];
  if (o.shoes && ITEM[o.shoes].over) { order.splice(order.indexOf('shoes'), 1); order.splice(order.indexOf('dress') + 1, 0, 'shoes'); }
  const all = [...order, 'bag', 'necklace', 'earrings', 'glasses', 'hat'].filter(k => o[k]);
  $('#layerList').innerHTML = all.length ? all.map((k, i) => {
    const it = ITEM[o[k]];
    return `<span class="layer-chip"><span class="n">${i + 1}</span><i style="--c:${state.colors[it.id] || it.color}"></i>${it.name}<button data-remove="${k}" aria-label="Remove ${it.name}">×</button></span>`;
  }).join('') : `<span class="empty">Just her underwear. Drag something on from the wardrobe.</span>`;
  const t = $('#tuckToggle');
  t.checked = state.tucked;
  t.disabled = !(o.top && o.bottom);
  t.parentElement.style.opacity = t.disabled ? .45 : 1;
}

function refreshUI() {
  $$('[data-bind]').forEach(b => {
    const on = String(getP(b.dataset.bind)) === b.dataset.val;
    b.classList.toggle('on', on);
    b.setAttribute('aria-pressed', on);
  });
  $$('input[data-range]').forEach(r => {
    const v = getP(r.dataset.range);
    if (document.activeElement !== r) r.value = v;
    r.nextElementSibling.textContent = Math.round(v * 100) + '%';
  });
  $$('input[data-color]').forEach(inp => {
    const v = getP(inp.dataset.color);
    const lab = inp.parentElement;
    const known = $$(`[data-bind="${inp.dataset.color}"]`).some(b => b.dataset.val === String(v));
    if (/^#[0-9a-f]{6}$/i.test(v)) inp.value = v;
    lab.classList.toggle('on', !known);
    lab.style.background = !known && /^#/.test(v) ? v : '';
  });
}



function renderSuite(pop = false) {
  const st = $('#suiteStage');
  const a = st.clientWidth ? st.clientWidth / Math.max(1, st.clientHeight) : ROOM_W / ROOM_H;
  let vw = ROOM_W, vx = 0;
  if (a < ROOM_W / ROOM_H) { vw = ROOM_H * a; vx = clamp(state.room.doll.x - vw / 2, 0, ROOM_W - vw); }
  st.querySelector('svg.suite-room')?.remove();
  st.insertAdjacentHTML('afterbegin', scope(roomSVG(state, { vb: `${vx.toFixed(1)} 0 ${vw.toFixed(1)} ${ROOM_H}` }), 'S'));
  st.classList.toggle('decorating', decorMode);
  const x = STARLETS.find(v => v.id === state.starlet);
  $('#stageName').textContent = x ? `${x.name} · ${x.role}` : 'My starlet';
  if (pop) { const d = st.querySelector('[data-doll] > svg'); d?.animate([{ transform: 'scale(1)' }, { transform: 'scale(1.02)' }, { transform: 'scale(1)' }], { duration: 380, easing: 'ease-out' }); }
}
function renderParlor() {
  $('#parlorDoll').style.setProperty('--mirror', state.room.wall);
  $('#parlorDoll').innerHTML = scope(renderDoll(state, { vb: zoom === 'face' ? '140 84 120 160' : '0 70 400 750' }), 'B');
  $$('[data-zoom]').forEach(b => b.classList.toggle('on', b.dataset.zoom === zoom));
  renderHairCards();
}
function renderDecor() {
  $('#placedList').innerHTML = state.room.items.map(i => `<span>${FURN[i.k].name}<button data-unplace="${i.id}" aria-label="Remove ${FURN[i.k].name}">×</button></span>`).join('') || '<span class="empty">Empty room</span>';
}

// ---------- undo history ----------
let undoStack = [], lastSnap = JSON.stringify(state), lastPush = 0, restoring = false;
function record() {
  const cur = JSON.stringify(state);
  if (cur === lastSnap) return;
  if (!restoring && Date.now() - lastPush > 600) { undoStack.push(lastSnap); if (undoStack.length > 60) undoStack.shift(); lastPush = Date.now(); }
  lastSnap = cur;
}
function undo() {
  if (!undoStack.length) return;
  restoring = true;
  state = sanitize(merge(clone(DEFAULT), JSON.parse(undoStack.pop())));
  hairSig = '';
  update(true);
  restoring = false;
  lastPush = 0;
  toast('Undone');
}

let saveTimer;
function update(pop = false) {
  record();
  UID = 0;
  renderSuite(pop);
  if (view === 'parlor') renderParlor();
  if (drawer === 'wardrobe') renderGrid();
  if (drawer === 'decor') renderDecor();
  renderLayers();
  refreshUI();
  $('#tbUndo').disabled = !undoStack.length;
  renderTopbar();
  renderLifeHud();
  if (view === 'shop') renderShop();
  if (view === 'contests') renderContests();
  if (view === 'events' && evMode === 'detail') renderEvents();
  if (preparing) renderPrepBar();
  clearTimeout(saveTimer);
  saveTimer = setTimeout(() => lsSet(LS_CUR, state), 250);
}

function setView(v) {
  view = v;
  $$('[data-view]').forEach(b => b.classList.toggle('on', b.dataset.view === v));
  $$('[data-viewpane]').forEach(p => p.classList.toggle('on', p.dataset.viewpane === v));
  if (v === 'parlor') { hairSig = ''; renderParlor(); }
  if (v === 'suite') renderSuite();
  if (v === 'album') renderLookbook();
  if (v === 'home') renderHome();
  if (v === 'shop') renderShop();
  if (v === 'contests') renderContests();
  if (v === 'events') { if (evMode === 'play' && !evSel) evMode = 'list'; renderEvents(); if (evMode === 'play') renderPlay(); }
  if (v === 'chat') { renderChat(); Promise.all([ensureRoom(), ensureSample()]).then(() => { if (view === 'chat') renderChat(); }); }
  window.scrollTo({ top: 0 });
  refreshUI();
}
function openDrawer(name) {
  drawer = drawer === name ? null : name;
  $('#drawerWardrobe').hidden = drawer !== 'wardrobe';
  $('#drawerDecor').hidden = drawer !== 'decor';
  $('#drawerLife').hidden = drawer !== 'life';
  decorMode = drawer === 'decor';
  $('#decorBanner').hidden = !decorMode;
  $$('[data-side]').forEach(b => b.classList.toggle('on', b.dataset.side === drawer));
  if (drawer === 'wardrobe') { renderGrid(); $('#guide').hidden = true; }
  if (drawer === 'decor') { renderDecor(); $('#guide').hidden = true; }
  if (drawer === 'life') { renderLifePanel(); $('#guide').hidden = true; }
  hideActMenu();
  $('#layersPop').hidden = true;
  renderSuite();
  refreshUI();
}

// ---------- wardrobe actions ----------
function equip(id, force = false) {
  const it = ITEM[id]; if (!it) return;
  if (!owns(id)) { toast(`Buy the ${it.name} in the Shop first`); return; }
  const o = state.outfit;
  if (o[it.slot] === id && !force) {
    o[it.slot] = null;
    toast(`Took off ${it.name}`);
  } else {
    o[it.slot] = id;
    if (it.slot === 'dress') { o.top = null; o.bottom = null; }
    if (it.slot === 'top' || it.slot === 'bottom') o.dress = null;
    if (it.slot === 'top') state.tucked = !it.over;
    state.focus = id;
  }
  update(true);
}

// ---------- toast ----------
let toastT;
function toast(msg) {
  const t = $('#toast'); t.textContent = msg; t.classList.add('on');
  clearTimeout(toastT); toastT = setTimeout(() => t.classList.remove('on'), 1800);
}

// ---------- randomize ----------
const pick = a => a[Math.floor(Math.random() * a.length)];
function randomize() {
  const byslot = slot => ITEMS.filter(i => i.slot === slot && owns(i.id)).map(i => i.id);
  state.skin = pick(PAL.skin); state.eyeColor = pick(PAL.eyes); state.eyeShape = pick(Object.keys(EYE_SHAPES));
  state.brow = pick(Object.keys(BROWS)); state.browColor = 'match';
  state.hair = pick(Object.keys(HAIR)); state.hairColor = pick(PAL.hair);
  applyPreset(pick(Object.keys(PRESETS).slice(1)), false);
  state.makeup.contacts = Math.random() < 0.25 ? pick(CONTACTS.slice(1)).v : 'none';
  const o = state.outfit;
  if (Math.random() < 0.4) { o.dress = pick(byslot('dress')); o.top = o.bottom = null; }
  else { o.dress = null; o.top = pick(byslot('top')); o.bottom = pick(byslot('bottom')); state.tucked = !ITEM[o.top].over; }
  o.outer = Math.random() < 0.45 ? pick(byslot('outer')) : null;
  o.shoes = pick(byslot('shoes'));
  for (const k of ['bag', 'necklace', 'earrings', 'glasses', 'hat']) o[k] = Math.random() < (k === 'hat' || k === 'glasses' ? 0.25 : 0.5) ? pick(byslot(k)) : null;
  state.colors = {};
  state.starlet = null;
  state.underwear = pick(['#f4c7d3', '#ffffff', '#1f1b22', '#c8b6e2']);
  state.focus = o.top || o.dress;
  update(true);
  toast('A brand new look ✦');
}
function applyPreset(name, doUpdate = true) {
  const p = PRESETS[name], m = state.makeup;
  for (const k of Object.keys(p)) {
    if (typeof p[k] === 'object') Object.assign(m[k], p[k]); else m[k] = p[k];
  }
  if (doUpdate) { update(); toast(`${name} applied`); }
}



// ---------- snapshots ----------
async function snapshot(s, { scale = 1.6, caption = '', type = 'image/png', quality = 0.9, svg: sceneSvg = null } = {}) {
  UID = 0;
  const svg = sceneSvg || roomSVG(s, { standalone: true, scale: 1 });
  const img = new Image();
  const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml;charset=utf-8' }));
  try {
    await new Promise((res, rej) => { img.onload = res; img.onerror = rej; img.src = url; });
    const W = ROOM_W * scale, Hr = ROOM_H * scale, foot = caption ? 56 * scale : 0;
    const cv = document.createElement('canvas');
    cv.width = W; cv.height = Hr + foot;
    const ctx = cv.getContext('2d');
    ctx.drawImage(img, 0, 0, W, Hr);
    if (caption) {
      ctx.fillStyle = '#ffffff'; ctx.fillRect(0, Hr, W, foot);
      ctx.fillStyle = '#2b2138'; ctx.textAlign = 'center';
      ctx.font = `italic 600 ${20 * scale}px Fraunces, Georgia, serif`;
      ctx.fillText(caption, W / 2, Hr + 28 * scale);
      ctx.fillStyle = '#ff4f8b'; ctx.font = `700 ${8 * scale}px "DM Sans", sans-serif`;
      ctx.fillText('★  S T A R L I F E', W / 2, Hr + 45 * scale);
    }
    return cv.toDataURL(type, quality);
  } finally { URL.revokeObjectURL(url); }
}
async function download(s, name) {
  try {
    if (document.fonts?.ready) await document.fonts.ready;
    const data = await snapshot(s, { scale: 1.6, caption: name || 'My Look' });
    const filename = (name || 'my-look').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') + '.png';
    // When hosted as a Claude artifact, saving goes through the viewer's downloads capability.
    const dl = window.claude?.use ? await window.claude.use('downloads').catch(() => null) : null;
    if (dl) {
      const blob = await (await fetch(data)).blob();
      try { await dl.save({ filename, data: blob }); toast('Snapshot saved ✦'); }
      catch (err) { if (err?.code !== 'declined') toast('Downloads are not available here'); }
      return;
    }
    const a = document.createElement('a');
    a.href = data; a.download = filename;
    document.body.appendChild(a); a.click(); a.remove();
    toast('Snapshot downloaded ✦');
  } catch (e) { console.error(e); toast('Could not create the image in this browser'); }
}

// ---------- album ----------
function renderLookbook() {
  $('#lbCount').textContent = lookbook.length ? `${lookbook.length} photo${lookbook.length > 1 ? 's' : ''}` : '';
  $('#lookbook').innerHTML = lookbook.length ? lookbook.map(l => `
    <article class="look" data-look="${l.id}">
      <img src="${l.thumb}" alt="${esc(l.name)}" loading="lazy">
      <div class="meta"><b title="${esc(l.name)}">${esc(l.name)}</b><small>${new Date(l.date).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}</small>
        <div class="acts"><button data-act="wear">Wear</button><button data-act="dl">PNG</button><button data-act="del" aria-label="Delete">✕</button></div>
      </div>
    </article>`).join('')
    : `<div class="lb-empty" style="grid-column:1/-1"><b>Your album is empty</b>Dress your starlet, then press the camera or <em>Save</em> to take a photo.</div>`;
}
function openSave() {
  UID = 0;
  $('#savePreview').innerHTML = scope(roomSVG(state, {}), 'M');
  $('#lookName').value = '';
  $('#saveModal').classList.add('on');
  setTimeout(() => $('#lookName').focus(), 50);
}
const closeSave = () => $('#saveModal').classList.remove('on');
async function confirmSave() {
  const name = $('#lookName').value.trim() || `Look No. ${lookbook.length + 1}`;
  try {
    const thumb = await snapshot(state, { scale: 0.42, type: 'image/jpeg', quality: 0.85 });
    lookbook.unshift({ id: Date.now().toString(36), name, date: Date.now(), state: clone(state), thumb });
    reward(5, 10);
    if (!lsSet(LS_BOOK, lookbook)) toast('Saved for this session (browser storage is full or blocked)');
    else toast(`“${name}” added to My Album ✦`);
    renderLookbook();
    closeSave();
  } catch (e) { console.error(e); toast('Could not save that look'); }
}

// ---------- starlets & picker ----------
function starletState(id, base = state) {
  const x = STARLETS.find(v => v.id === id);
  const s = clone(DEFAULT);
  s.room = clone(base.room);
  Object.assign(s, { starlet: x.id, gender: x.gender || 'f', beard: x.beard || 'none', skin: x.skin, hair: x.hair, hairColor: x.hairColor, eyeShape: x.eyeShape, eyeColor: x.eyeColor, brow: x.brow, underwear: x.underwear });
  const p = PRESETS[x.preset];
  for (const k of Object.keys(p)) { if (typeof p[k] === 'object') Object.assign(s.makeup[k], p[k]); else s.makeup[k] = p[k]; }
  return s;
}
function applyStarlet(id) {
  state = starletState(id);
  hairSig = '';
  update(true);
}
let pickSel = 'aria';
function openPicker() {
  pickSel = state.starlet || 'aria';
  $('#carTrack').innerHTML = STARLETS.map(x => {
    const lips = PRESETS[x.preset].lips.color;
    return `<button class="car-item" data-pick="${x.id}"><svg viewBox="104 30 192 192">${portrait(x, lips === 'none' ? '#c96b78' : lips)}</svg><span>${x.name.split(' ')[0]}</span></button>`;
  }).join('');
  $('#picker').hidden = false;
  document.body.style.overflow = 'hidden';
  renderPicker();
}
function renderPicker() {
  const s = starletState(pickSel);
  UID = 0;
  $('#pickerRoom').innerHTML = scope(roomSVG(s, { aspect: 'xMidYMid meet' }), 'P');
  $$('[data-pick]').forEach(b => b.classList.toggle('on', b.dataset.pick === pickSel));
  $(`[data-pick="${pickSel}"]`)?.scrollIntoView({ inline: 'center', block: 'nearest', behavior: 'smooth' });
  const x = STARLETS.find(v => v.id === pickSel);
  $('#pickerNote').innerHTML = `<b>${x.name}</b>, ${x.role.toLowerCase()}. You can restyle ${x.gender === 'm' ? 'him' : 'her'} any time, and there are <b>millions</b> of ways to dress up!`;
}
function closePicker(go) {
  if (go) { applyStarlet(pickSel); toast(`Welcome home, ${STARLETS.find(v => v.id === pickSel).name}!`); }
  $('#picker').hidden = true;
  document.body.style.overflow = '';
  setView('suite');
  if (go && !lsGet('starlife.guide.v1', false)) openGuide();
}

// ---------- guide ----------
const GUIDE = [
  () => `Welcome to your new suite! I'm ${HOST.name}, your stylist. ${starletName()} has just moved in and needs an outfit.`,
  () => `Open the <b>Wardrobe</b> and drag clothes onto her. Changed your mind? Drag a piece off her to take it off.`,
  () => `Head to the <b>Beauty Parlor</b> to change her hair, eyes and makeup.`,
  () => `Click <b>My Suite</b> to decorate. Drag the furniture and your starlet around, and pick a wall colour.`,
  () => `Win coins and fame in <b>Contests</b>, then spend your coins in the <b>Shop</b> on new clothes for her wardrobe.`,
  () => `Keep an eye on her needs in the bar above the room. <b>Click furniture</b> to eat, sleep, shower and have fun, click her to call a friend, and find a job in the <b>Life</b> panel.`,
  () => `Check <b>Events</b> for invitations: book club, coffee dates, balls, the Grand Gala and more. Your friends text you in <b>Chat</b>.`,
  () => `Love the look? Use the <b>Camera</b> to save a photo to My Album. Make yourself at home, see you soon!`,
];
let guideStep = 0;
function guideHost() {
  const h = clone(DEFAULT);
  Object.assign(h, { skin: '#d9a273', hair: 'waves', hairColor: '#c9944f', eyeShape: 'almond', eyeColor: '#6b4a2b', brow: 'arched' });
  const p = PRESETS['Soft Glam'];
  for (const k of Object.keys(p)) { if (typeof p[k] === 'object') Object.assign(h.makeup[k], p[k]); else h.makeup[k] = p[k]; }
  Object.assign(h.outfit, { top: 'cami', necklace: 'pendant', earrings: 'hoops' });
  h.colors.cami = '#f3a6c3';
  return h;
}
function openGuide(step = 0) {
  guideStep = step;
  if (drawer) openDrawer(drawer);
  UID = 0;
  $('#guideFace').innerHTML = scope(renderDoll(guideHost(), { vb: '128 84 144 196' }), 'H');
  $('#guide').hidden = false;
  renderGuide();
}
function renderGuide() {
  $('#guideText').innerHTML = GUIDE[guideStep]();
  $('#guideCount').textContent = `${guideStep + 1}/${GUIDE.length}`;
  $('#guideBack').disabled = guideStep === 0;
  $('#guideNext').textContent = guideStep === GUIDE.length - 1 ? 'Got it' : 'Next ›';
}
function closeGuide() { $('#guide').hidden = true; lsSet('starlife.guide.v1', true); }

// =====================================================================
// Profile: coins, fame and levels. Kept apart from the look, so Undo
// and saved looks never touch your wallet.
// =====================================================================
const LS_PROFILE = 'starlife.profile.v1';
const today = () => new Date().toISOString().slice(0, 10);
const dayNo = () => Math.floor(Date.now() / 864e5);
let profile = Object.assign({ coins: 150, fame: 0, owned: [], lastBonus: '', contests: {} }, lsGet(LS_PROFILE, {}));
const PRICES = {
  breton: 30, corset: 50, wide: 40, pencil: 35, slip: 55, gown: 120, wrap: 60, lbd: 65, sundress: 50, sequin: 90,
  biker: 70, trench: 80, cardigan: 40, puffer: 70, pumps: 40, ankleboots: 50, kneeboots: 60, sandals: 30,
  crossbody: 50, heartbag: 40, pearls: 45, drops: 35, cateye: 30, heartsunnies: 30, beret: 35, sunhat: 40, tiara: 100,
  suitpants: 40, suit: 90, tux: 120, tie: 25, bowtie: 30, oxfords: 45,
};
const TAGS = {
  tee: ['casual', 'summer'], cami: ['night', 'summer'], blouse: ['office', 'chic'], turtle: ['winter', 'chic'], corset: ['night', 'glam', 'edgy'], breton: ['casual', 'chic'],
  jeans: ['casual', 'edgy'], shorts: ['summer', 'casual'], mini: ['night', 'summer'], pleated: ['chic', 'office'], wide: ['office', 'chic'], pencil: ['office', 'glam'],
  slip: ['night', 'glam'], gown: ['redcarpet', 'glam'], wrap: ['office', 'chic'], lbd: ['night', 'redcarpet', 'chic'], sundress: ['summer', 'casual'], sequin: ['night', 'glam', 'redcarpet'],
  blazer: ['office', 'chic'], biker: ['night', 'edgy'], trench: ['winter', 'chic'], cardigan: ['winter', 'casual'], denimjk: ['casual', 'edgy'], puffer: ['winter', 'casual'],
  sneakers: ['casual'], pumps: ['office', 'redcarpet', 'night'], ankleboots: ['winter', 'edgy'], kneeboots: ['winter', 'night', 'edgy'], flats: ['chic', 'office'], sandals: ['summer'],
  tote: ['casual', 'summer'], clutch: ['redcarpet', 'night'], crossbody: ['casual', 'edgy'], heartbag: ['glam', 'summer'],
  pearls: ['chic', 'redcarpet'], pendant: ['casual', 'chic'], choker: ['edgy', 'night'], hoops: ['casual', 'glam'], studs: ['office', 'chic'], drops: ['redcarpet', 'glam'],
  roundspecs: ['office'], cateye: ['chic', 'office'], sunnies: ['summer'], heartsunnies: ['summer', 'glam'],
  beret: ['chic', 'winter'], sunhat: ['summer'], beanie: ['winter', 'casual'], tiara: ['redcarpet', 'glam'],
  shirt: ['office', 'chic'], polo: ['casual', 'summer'], hoodie: ['casual', 'winter', 'edgy'], chinos: ['casual', 'office'], suitpants: ['office', 'redcarpet'],
  suit: ['office', 'chic', 'redcarpet'], tux: ['redcarpet', 'glam'], tie: ['office'], bowtie: ['redcarpet', 'glam'], oxfords: ['office', 'redcarpet'], loafers: ['chic', 'summer'], cap: ['casual', 'summer'],
};
const TAG_LABEL = { casual: 'Casual', summer: 'Summer', night: 'Night out', office: 'Office', chic: 'Chic', winter: 'Winter', glam: 'Glam', edgy: 'Edgy', redcarpet: 'Red carpet' };
const owns = id => !PRICES[id] || profile.owned.includes(id);
const FAME_PER_LEVEL = 60;
const level = () => 1 + Math.floor(profile.fame / FAME_PER_LEVEL);
const COIN = `<svg class="coin" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10" fill="#f5c542" stroke="#c9961c" stroke-width="1.6"/><circle cx="12" cy="12" r="6.5" fill="none" stroke="#e0a92a" stroke-width="1.2"/><path d="M12 7.6l1.3 2.7 3 .4-2.2 2.1.5 3-2.6-1.4-2.6 1.4.5-3-2.2-2.1 3-.4z" fill="#fff6d0"/></svg>`;
const FAME = `<svg class="coin" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10" fill="#8b6cf0" stroke="#6243cf" stroke-width="1.6"/><path d="M12 5.5l1.9 3.9 4.3.6-3.1 3 .7 4.3L12 15.3l-3.8 2 .7-4.3-3.1-3 4.3-.6z" fill="#fff"/></svg>`;

function saveProfile() { lsSet(LS_PROFILE, profile); renderTopbar(); }
function reward(coins, fame) {
  const before = level();
  profile.coins += coins; profile.fame += fame;
  saveProfile();
  $$('.wallet .pill').forEach(p => { p.classList.remove('bump'); void p.offsetWidth; p.classList.add('bump'); });
  if (level() > before) setTimeout(() => toast(`Level up! ${starletName()} is now level ${level()} ✦`), 900);
}
function renderTopbar() {
  const x = STARLETS.find(v => v.id === state.starlet);
  const lips = x ? PRESETS[x.preset].lips.color : state.makeup.lips.color;
  $('#meFace').innerHTML = `<svg viewBox="104 30 192 192">${portrait(state, lips === 'none' ? '#c96b78' : lips)}</svg>`;
  $('#meName').textContent = starletName();
  $('#meLevel').textContent = level();
  $('#meXp').style.width = (profile.fame % FAME_PER_LEVEL) / FAME_PER_LEVEL * 100 + '%';
  $('#meXpText').textContent = `${profile.fame % FAME_PER_LEVEL} / ${FAME_PER_LEVEL} fame to level ${level() + 1}`;
  $('#coinVal').textContent = profile.coins;
  $('#fameVal').textContent = profile.fame;
}

// city skyline painted behind the page, like a view over the rooftops
function skylineBg() {
  const r = rng(77);
  let b = '';
  const side = (x0, x1) => {
    for (let x = x0; x < x1;) {
      const w = 40 + r() * 70, h = 120 + r() * 380, c = ['#d7e6f6', '#c4d8ee', '#b3cbe6', '#e6eef8', '#a9c0dd'][Math.floor(r() * 5)];
      b += `<rect x="${x.toFixed(0)}" y="${(900 - h).toFixed(0)}" width="${w.toFixed(0)}" height="${h.toFixed(0)}" fill="${c}"/>`;
      for (let y = 910 - h; y < 880; y += 18) b += `<rect x="${(x + 8).toFixed(0)}" y="${y.toFixed(0)}" width="${(w - 16).toFixed(0)}" height="6" fill="#fff" opacity=".55"/>`;
      if (r() > 0.7) b += `<path d="M${(x + w / 2).toFixed(0)},${(900 - h - 60).toFixed(0)} l6,60 h-12 z" fill="${c}"/>`;
      x += w + 4 + r() * 10;
    }
  };
  side(0, 420); side(1180, 1600);
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1600 900" preserveAspectRatio="xMidYMax slice"><path d="M0,620 L160,520 L300,600 L420,540 L560,640 L0,700 Z M1100,640 L1260,540 L1400,600 L1600,520 L1600,700 Z" fill="#e3edf7"/>${b}<rect y="860" width="1600" height="40" fill="#cfe0f0"/></svg>`;
  document.body.style.backgroundImage = `url("data:image/svg+xml,${encodeURIComponent(svg)}"), linear-gradient(180deg, #7ec2f0 0%, #bfe3fb 45%, #eef7fd 100%)`;
}

// =====================================================================
// What's New (home)
// =====================================================================
const SIGNATURE = {
  aria: { top: 'cami', bottom: 'pleated', shoes: 'flats', necklace: 'pearls' },
  zuri: { dress: 'sequin', shoes: 'pumps', earrings: 'drops' },
  mina: { top: 'breton', bottom: 'jeans', shoes: 'sneakers', glasses: 'roundspecs' },
  luz: { dress: 'lbd', outer: 'biker', shoes: 'ankleboots', earrings: 'hoops' },
  noor: { dress: 'sundress', shoes: 'sandals', earrings: 'studs' },
  ivy: { top: 'turtle', bottom: 'wide', glasses: 'cateye', necklace: 'pendant' },
  kai: { top: 'tee', bottom: 'jeans', outer: 'biker', shoes: 'sneakers' },
  leo: { top: 'shirt', bottom: 'suitpants', outer: 'suit', shoes: 'oxfords', necklace: 'tie' },
  jin: { top: 'hoodie', bottom: 'chinos', shoes: 'sneakers', hat: 'cap' },
  theo: { top: 'polo', bottom: 'shorts', shoes: 'sneakers' },
  omar: { top: 'turtle', bottom: 'chinos', outer: 'trench', shoes: 'loafers' },
};
const STARLET_FAME = { aria: 104, zuri: 86, mina: 48, luz: 75, noor: 33, ivy: 22, kai: 91, leo: 70, jin: 57, theo: 40, omar: 29 };
function signatureState(id) { const s = starletState(id); Object.assign(s.outfit, SIGNATURE[id]); s.tucked = true; return s; }
let heroCache = '';
function heroSVG() {
  if (heroCache) return heroCache;
  const order = ['luz', 'kai', 'aria', 'leo', 'zuri', 'jin', 'noor', 'theo'];
  const H = 700, W = H * 400 / 820;
  let dolls = '';
  order.forEach((id, i) => {
    const cx = 82 + i * 148, y = 26 + (i % 2) * 14;
    dolls += `<svg x="${cx - W / 2}" y="${y}" width="${W}" height="${H}" viewBox="0 0 400 820">${scope(renderDoll(signatureState(id), { inner: true }), 'h' + i + '-')}</svg>`;
  });
  heroCache = scope(`<svg viewBox="0 0 1200 420" preserveAspectRatio="xMidYMid slice"><defs>
    <linearGradient id="hs" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#9fd3f3"/><stop offset="1" stop-color="#eaf6fb"/></linearGradient>
    <linearGradient id="hw" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#e9577f"/><stop offset="1" stop-color="#f28aa6"/></linearGradient></defs>
    <rect width="1200" height="420" fill="url(#hs)"/>
    <path d="M0,250 L120,170 L240,230 L360,150 L520,240 L640,200 L760,250 L760,420 L0,420 Z" fill="#9fc5b0" opacity=".7"/>
    <path d="M0,290 L200,240 L420,300 L620,260 L760,300 L760,420 L0,420 Z" fill="#6f9f82" opacity=".75"/>
    <rect x="0" y="0" width="1200" height="34" fill="#4a4048"/><rect x="0" y="34" width="1200" height="8" fill="#2f282e" opacity=".5"/>
    ${[60, 260, 460, 660].map(x => `<rect x="${x}" y="34" width="16" height="386" fill="#544a52"/>`).join('')}
    <path d="M760,0 L1200,0 L1200,420 L760,420 Z" fill="url(#hw)"/>
    ${Array.from({ length: 9 }, (_, i) => `<path d="M${780 + i * 50},420 L${880 + i * 50},${160 + i * 10} L${900 + i * 50},${160 + i * 10} L${800 + i * 50},420 Z" fill="#2f3d4a" opacity=".55"/>`).join('')}
    <circle cx="1030" cy="90" r="40" fill="#ffd1dc" opacity=".55"/><circle cx="1120" cy="150" r="22" fill="#ffd1dc" opacity=".5"/>
    <rect x="0" y="330" width="760" height="10" fill="#3b6b6a"/>${Array.from({ length: 38 }, (_, i) => `<rect x="${i * 20 + 4}" y="270" width="5" height="70" fill="#3b6b6a"/>`).join('')}
    <rect x="0" y="268" width="760" height="6" fill="#3b6b6a"/>
    ${dolls}
    <rect y="380" width="1200" height="40" fill="url(#hs)" opacity="0"/></svg>`, 'HR');
  return heroCache;
}
function renderHome() {
  if (!$('#hero svg')) $('#hero').insertAdjacentHTML('afterbegin', heroSVG());
  $('#heroName').textContent = starletName();
  const claim = profile.lastBonus !== today();
  $('#bonusTile').innerHTML = `<h4>Daily bonus</h4>
    <div class="tile-big">${COIN}<b>50</b><span>coins</span></div>
    <p>${claim ? 'Your daily coins are ready. Spend them in the Shop.' : 'Collected for today. Come back tomorrow for another 50.'}</p>
    <button class="btn primary small" id="claimBonus"${claim ? '' : ' disabled'}>${claim ? 'Claim 50 coins' : 'Claimed ✓'}</button>`;
  const th = featuredTheme();
  $('#contestTile').innerHTML = `<h4>Today's contest</h4><div class="tile-theme" style="--a:${th.bg[0]};--b:${th.bg[1]}"><b>${th.name}</b></div>
    <p>${th.desc}</p><button class="btn small" data-view="contests">Enter the contest ›</button>`;
  const fresh = ITEMS.filter(i => PRICES[i.id] && !owns(i.id));
  const picks = [0, 1, 2].map(k => fresh[(dayNo() * 3 + k * 7) % Math.max(1, fresh.length)]).filter(Boolean);
  const uniq = [...new Set(picks)];
  $('#shopTile').innerHTML = `<h4>New in the Shop</h4><div class="tile-items">${uniq.length ? uniq.map(it => `<span>${scope(itemThumb(it, it.color), 'NS' + it.id + '-')}<small>${COIN}${PRICES[it.id]}</small></span>`).join('') : '<p>You own everything. Amazing!</p>'}</div>
    <button class="btn small" data-view="shop">Visit the Shop ›</button>`;
  $('#people').innerHTML = STARLETS.map(x => {
    const lips = PRESETS[x.preset].lips.color, cur = x.id === state.starlet;
    return `<article class="person${cur ? ' cur' : ''}">
      <div class="person-face"><svg viewBox="104 30 192 192">${portrait(x, lips === 'none' ? '#c96b78' : lips)}</svg><span class="crown">${STARLET_FAME[x.id]}</span></div>
      <b>${x.name}</b><small>${x.role}</small>
      <button class="btn small ${cur ? '' : 'primary'}" data-style="${x.id}">${cur ? 'Styling now' : 'Style her'}</button></article>`;
  }).join('');
  const last = lookbook[0];
  $('#promoAlbum').innerHTML = last ? `<img src="${last.thumb}" alt="">` : `<span class="promo-empty">Your photos will appear here</span>`;
}

// =====================================================================
// Shop with fitting room
// =====================================================================
let shopCat = 'all', tryOn = [];
function wearOn(s, id) {
  const it = ITEM[id], o = s.outfit;
  o[it.slot] = id;
  if (it.slot === 'dress') { o.top = null; o.bottom = null; }
  if (it.slot === 'top' || it.slot === 'bottom') o.dress = null;
  if (it.slot === 'top') s.tucked = !it.over;
}
function renderShop() {
  const cats = [['all', 'All'], ['top', 'Tops'], ['bottom', 'Bottoms'], ['dress', 'Dresses'], ['outer', 'Jackets'], ['shoes', 'Shoes'], ['acc', 'Accessories']];
  $('#shopTabs').innerHTML = cats.map(([k, l]) => `<button data-shopcat="${k}" class="${k === shopCat ? 'on' : ''}">${l}</button>`).join('');
  const list = ITEMS.filter(i => PRICES[i.id] && (shopCat === 'all' || i.cat === shopCat))
    .sort((a, b) => owns(a.id) - owns(b.id) || PRICES[a.id] - PRICES[b.id]);
  $('#shopGrid').innerHTML = list.map(it => {
    const own = owns(it.id), trying = tryOn.includes(it.id);
    return `<article class="shop-card${own ? ' owned' : ''}${trying ? ' trying' : ''}">
      <div class="shop-thumb">${scope(itemThumb(it, it.color), 'SH' + it.id + '-')}</div>
      <b>${it.name}</b>
      <div class="shop-tags">${(TAGS[it.id] || []).map(t => `<span>${TAG_LABEL[t]}</span>`).join('')}</div>
      <div class="shop-row">${own ? '<span class="owned-tag">In your wardrobe ✓</span>' : `<span class="price">${COIN}${PRICES[it.id]}</span>`}</div>
      <div class="shop-row">
        <button class="btn small" data-tryon="${it.id}">${trying ? 'Take off' : 'Try on'}</button>
        ${own ? `<button class="btn small primary" data-wearnow="${it.id}">Wear</button>` : `<button class="btn small primary" data-buy="${it.id}"${profile.coins < PRICES[it.id] ? ' aria-disabled="true"' : ''}>Buy</button>`}
      </div></article>`;
  }).join('');
  const s = clone(state); tryOn.forEach(id => wearOn(s, id));
  $('#fitDoll').innerHTML = scope(renderDoll(s), 'FR');
  $('#fitNote').textContent = tryOn.length ? `Trying on: ${tryOn.map(id => ITEM[id].name).join(', ')}` : 'Press Try on to see a piece on her before you buy.';
}
function buy(id) {
  const it = ITEM[id], price = PRICES[id];
  if (owns(id)) return;
  if (profile.coins < price) { toast(`You need ${price - profile.coins} more coins. Win some in Contests or claim your daily bonus.`); return; }
  profile.coins -= price; profile.owned.push(id);
  saveProfile();
  reward(0, 3);
  toast(`Bought ${it.name}! It's in your wardrobe ✦`);
  renderShop();
}

// =====================================================================
// Contests: themed style challenges scored by three judges
// =====================================================================
const THEMES = [
  { id: 'carpet', name: 'Red Carpet Premiere', short: 'the red carpet', desc: 'Cameras are flashing. Go glamorous from head to toe.', tags: ['redcarpet', 'glam'], bg: ['#3a1020', '#b3263a'],
    tip: 'Bold lips and full lashes', makeup: m => (m.lips.color !== 'none' && m.lips.opacity >= 0.7 ? 8 : 0) + (['volume', 'dramatic'].includes(m.mascara) ? 4 : 0) + (m.highlight.opacity >= 0.4 ? 3 : 0) },
  { id: 'beach', name: 'Beach Day', short: 'the beach', desc: 'Sun, sand and a sea breeze. Keep it light and breezy.', tags: ['summer', 'casual'], bg: ['#2fa9c9', '#f2d48f'],
    tip: 'Barely-there eyes and glossy lips', makeup: m => (m.shadow.style === 'none' || m.shadow.opacity < 0.45 ? 6 : 0) + (m.lips.finish === 'gloss' ? 5 : 0) + (m.blush.color !== 'none' ? 4 : 0) },
  { id: 'office', name: 'Office Chic', short: 'the office', desc: 'First day at a fashion magazine. Polished, smart and a little bit chic.', tags: ['office', 'chic'], bg: ['#8a7356', '#e9e1d4'],
    tip: 'Neat liner and a matte lip', makeup: m => (['tight', 'classic'].includes(m.liner.style) ? 6 : 0) + (m.lips.finish === 'matte' ? 5 : 0) + (m.shadow.style !== 'smoky' ? 4 : 0) },
  { id: 'snow', name: 'Snow Day Stroll', short: 'a snowy afternoon', desc: "It's freezing outside. Layer up and stay cosy.", tags: ['winter', 'casual'], bg: ['#7da7cf', '#f3f8ff'],
    tip: 'Rosy cheeks', makeup: m => (m.blush.color !== 'none' && m.blush.opacity >= 0.3 ? 9 : 0) + (m.lips.finish !== 'metallic' ? 3 : 0) + 3 },
  { id: 'rock', name: 'Rock Concert', short: 'the concert', desc: 'Front row at a sold-out show. Edgy, dark and ready to dance.', tags: ['edgy', 'night'], bg: ['#1b1424', '#6b2a6e'],
    tip: 'Smoky eyes and strong liner', makeup: m => (m.shadow.style === 'smoky' ? 7 : 0) + (['winged', 'cat', 'smudged'].includes(m.liner.style) ? 5 : 0) + (m.mascara === 'dramatic' ? 3 : 0) },
];
const JUDGES = [
  { name: 'Coco Marlowe', role: 'Magazine editor', skin: '#eab78f', hair: 'bob', hairColor: '#1c1412', lips: '#b3132c' },
  { name: 'Dex Rivera', role: 'Photographer', skin: '#a5683d', hair: 'pixie', hairColor: '#2a1a12', lips: '#a8705a' },
  { name: 'June Park', role: 'Designer', skin: '#fde3d3', hair: 'ponytail', hairColor: '#6b3a1f', lips: '#ff6fa3' },
];
const featuredTheme = () => THEMES[dayNo() % THEMES.length];
let themeSel = featuredTheme().id, lastResult = null;
function scoreLook(s, th) {
  const o = s.outfit, worn = Object.values(o).filter(Boolean).map(id => ITEM[id]);
  const base = !!(o.dress || (o.top && o.bottom));
  let score = base ? 20 : (o.top || o.bottom) ? 8 : 0;
  if (o.shoes) score += 10;
  const acc = ['bag', 'necklace', 'earrings', 'glasses', 'hat'].filter(k => o[k]).length;
  score += Math.min(acc, 3) * 4;
  const match = [], off = [];
  for (const it of worn) ((TAGS[it.id] || []).some(t => th.tags.includes(t)) ? match : off).push(it);
  score += Math.min(match.length * 9, 45) - off.length * 4;
  const mk = clamp(th.makeup(s.makeup), 0, 15);
  score = clamp(Math.round(score + mk), 0, 100);
  return { score, base, match, off, acc, mk, shoes: !!o.shoes };
}
function judgeSay(r, th, i) {
  const c = [];
  if (!r.base) c.push(`She can't go to ${th.short} in her underwear!`);
  if (r.match[0]) c.push(`Love the ${r.match[0].name.toLowerCase()}. Perfect for ${th.short}.`);
  if (r.match[1]) c.push(`The ${r.match[1].name.toLowerCase()} really sells the theme.`);
  if (r.off[0]) c.push(`The ${r.off[0].name.toLowerCase()} feels off-theme to me.`);
  if (!r.shoes) c.push('Shoes would finish this look.');
  if (!r.acc) c.push('Add an accessory or two for some sparkle.');
  c.push(r.mk >= 10 ? 'The makeup is spot on.' : `${th.tip} would suit ${th.short} better.`);
  if (r.score >= 85) c.push('Iconic. Front cover material!');
  if (r.score < 40) c.push('Keep going, this needs more of the theme.');
  return c[(i * 3 + r.score) % c.length];
}
function renderContests() {
  const th = THEMES.find(t => t.id === themeSel);
  $('#themePicker').innerHTML = THEMES.map(t => `<button data-theme="${t.id}" class="${t.id === themeSel ? 'on' : ''}">${t.name}${t.id === featuredTheme().id ? ' <em>Today</em>' : ''}</button>`).join('');
  $('#contestBanner').style.setProperty('--a', th.bg[0]); $('#contestBanner').style.setProperty('--b', th.bg[1]);
  $('#contestTitle').textContent = th.name;
  $('#contestDesc').textContent = th.desc;
  $('#contestTags').innerHTML = th.tags.map(t => `<span>${TAG_LABEL[t]}</span>`).join('') + `<span>Makeup: ${th.tip.toLowerCase()}</span>`;
  $('#contestDoll').style.setProperty('--a', th.bg[0]); $('#contestDoll').style.setProperty('--b', th.bg[1]);
  $('#contestDoll').innerHTML = scope(renderDoll(state), 'CT');
  const r = scoreLook(state, th);
  const rec = profile.contests[th.id];
  const line = (ok, text) => `<li class="${ok ? 'ok' : ''}">${ok ? '✓' : '○'} ${text}</li>`;
  $('#contestBrief').innerHTML = `<h3>Judging checklist</h3><ul>
    ${line(r.base, 'A full outfit (a dress, or a top with a bottom)')}
    ${line(r.shoes, 'Shoes')}
    ${line(r.match.length >= 3, `Pieces that fit the theme: ${r.match.length} (aim for 3 or more)`)}
    ${line(!r.off.length, r.off.length ? `Off-theme pieces: ${r.off.map(i => i.name).join(', ')}` : 'Nothing off-theme')}
    ${line(r.acc >= 2, `Accessories: ${r.acc} (2 or 3 is best)`)}
    ${line(r.mk >= 10, `Makeup: ${th.tip.toLowerCase()}`)}</ul>
    <p class="best">${rec && rec.date === today() ? `Today's best: <b>${rec.best}</b> points` : 'Prize: up to 50 coins and 25 fame'}</p>`;
  if (lastResult && lastResult.theme === th.id) showResult(lastResult, false);
  else $('#contestResult').innerHTML = '';
}
function enterContest() {
  const th = THEMES.find(t => t.id === themeSel), r = scoreLook(state, th);
  const rec = profile.contests[th.id], fresh = !rec || rec.date !== today();
  const prev = fresh ? 0 : rec.best;
  const gain = Math.max(0, r.score - prev);
  const coins = Math.round(gain / 2), fame = Math.round(gain / 4);
  profile.contests[th.id] = { date: today(), best: Math.max(prev, r.score) };
  bumpNeed('fun', 8);
  lastResult = { theme: th.id, r, coins, fame, beat: !fresh && r.score > prev, repeat: !fresh && r.score <= prev };
  if (coins || fame) reward(coins, fame); else saveProfile();
  renderContests();
  showResult(lastResult, true);
}
function showResult(res, animate) {
  const th = THEMES.find(t => t.id === res.theme), r = res.r;
  const medal = r.score >= 85 ? ['Gold', '#f5c542'] : r.score >= 65 ? ['Silver', '#c7ccd6'] : r.score >= 45 ? ['Bronze', '#d79b63'] : ['Keep styling', '#e7defa'];
  $('#contestResult').innerHTML = `<div class="result">
    <div class="score-ring" style="--p:${r.score};--m:${medal[1]}"><b id="scoreNum">${animate ? 0 : r.score}</b><small>points</small></div>
    <div class="result-copy"><h3>${medal[0]}${r.score >= 45 ? ' medal' : ''}</h3>
      <p>${res.repeat ? "You've already won today's prize for this contest. Beat your best score to earn more." : res.coins || res.fame ? `You won ${COIN}<b>${res.coins}</b> coins and ${FAME}<b>${res.fame}</b> fame${res.beat ? ' for beating your best' : ''}!` : 'No prize this time.'}</p></div></div>
    <div class="judges">${JUDGES.map((j, i) => {
      const stars = clamp(Math.round(r.score / 20 + (i - 1) * 0.4), 1, 5);
      return `<article class="judge"><div class="judge-face"><svg viewBox="104 30 192 192">${portrait(j, j.lips)}</svg></div>
        <div><b>${j.name}</b><small>${j.role}</small><div class="stars" aria-label="${stars} out of 5 stars">${'★'.repeat(stars)}<span>${'★'.repeat(5 - stars)}</span></div><p>“${judgeSay(r, th, i)}”</p></div></article>`;
    }).join('')}</div>`;
  if (animate) {
    const el = $('#scoreNum'), t0 = performance.now();
    const tick = t => { const k = Math.min(1, (t - t0) / 900); el.textContent = Math.round(r.score * (1 - Math.pow(1 - k, 3))); if (k < 1) requestAnimationFrame(tick); };
    requestAnimationFrame(tick);
  }
}

// =====================================================================
// Social life: events at venues around Starlife
// =====================================================================
// a doll placed in a scene; each one gets its own id namespace so several can share an SVG
function dollAt(s, x, y, pfx, attrs = '') {
  const sc = depth(y), dw = DOLL_H * 400 / 820;
  return `<g ${attrs} transform="translate(${x.toFixed(1)},${y.toFixed(1)}) scale(${sc.toFixed(3)})"><svg x="${(-dw / 2).toFixed(1)}" y="${(-DOLL_H * 802 / 820).toFixed(1)}" width="${dw.toFixed(1)}" height="${DOLL_H}" viewBox="0 0 400 820" overflow="visible">${scope(renderDoll(s, { inner: true }), pfx)}</svg></g>`;
}
function floorBoards(c1, c2, y0 = FLOOR_Y) {
  const rr = rng(31); let p = '';
  for (let k = 1; k <= 12; k++) {
    const y = y0 + (ROOM_H - y0) * Math.pow(k / 12, 1.45), prev = y0 + (ROOM_H - y0) * Math.pow((k - 1) / 12, 1.45);
    p += `<path d="M0,${y.toFixed(1)} H1000" stroke="#000" stroke-opacity=".1"/>`;
    for (let x = rr() * 200; x < 1000; x += 160 + rr() * 140) p += `<path d="M${x.toFixed(0)},${prev.toFixed(1)} V${y.toFixed(1)}" stroke="#000" stroke-opacity=".08"/>`;
  }
  return `<rect y="${y0}" width="1000" height="${ROOM_H - y0}" fill="url(#evFloor)"/><linearGradient id="evFloor" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${c2}"/><stop offset="1" stop-color="${c1}"/></linearGradient>${p}`;
}
function checker(a, b, y0 = FLOOR_Y) {
  let t = `<rect y="${y0}" width="1000" height="${ROOM_H - y0}" fill="${a}"/>`;
  for (let r = 0; r < 6; r++) {
    const y1 = y0 + (ROOM_H - y0) * Math.pow(r / 6, 1.3), y2 = y0 + (ROOM_H - y0) * Math.pow((r + 1) / 6, 1.3);
    for (let c = -8; c < 9; c++) {
      if ((r + c) % 2) continue;
      const k1 = 0.55 + r * 0.075, k2 = 0.55 + (r + 1) * 0.075, w = 80;
      t += `<path d="M${500 + c * w * k1},${y1.toFixed(1)} L${500 + (c + 1) * w * k1},${y1.toFixed(1)} L${500 + (c + 1) * w * k2},${y2.toFixed(1)} L${500 + c * w * k2},${y2.toFixed(1)} Z" fill="${b}"/>`;
    }
  }
  return t;
}
const sky = (a, b, c) => `<linearGradient id="evSky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${a}"/><stop offset=".6" stop-color="${b}"/><stop offset="1" stop-color="${c}"/></linearGradient><rect width="1000" height="${FLOOR_Y}" fill="url(#evSky)"/>`;
const skyline = (col, base = 444, seed = 3) => { const r = rng(seed); let s = ''; for (let x = 0; x < 1000;) { const w = 30 + r() * 60, h = 60 + r() * 180; s += `<rect x="${x.toFixed(0)}" y="${(base - h).toFixed(0)}" width="${w.toFixed(0)}" height="${h.toFixed(0)}" fill="${col}"/>`; for (let y = base - h + 8; y < base - 10; y += 16) if (r() > .45) s += `<rect x="${(x + 6).toFixed(0)}" y="${y.toFixed(0)}" width="5" height="6" fill="#ffe7a3" opacity=".8"/>`; x += w + 2; } return s; };
const fairy = (y, sag, col = '#ffe39a') => { let s = `<path d="M0,${y} Q250,${y + sag} 500,${y} Q750,${y + sag} 1000,${y}" fill="none" stroke="#333" stroke-width="1.2" opacity=".5"/>`; for (let i = 0; i <= 40; i++) { const x = i * 25, t = (x % 500) / 500, yy = y + sag * 2 * t * (1 - t) * 1; s += `<circle cx="${x}" cy="${(yy + 4).toFixed(1)}" r="4" fill="${col}"/><circle cx="${x}" cy="${(yy + 4).toFixed(1)}" r="9" fill="${col}" opacity=".25"/>`; } return s; };
const chandelier = (x, y, s = 1) => `<g transform="translate(${x},${y}) scale(${s})"><path d="M0,-${y / s} V0" stroke="#c9a24a" stroke-width="2"/><path d="M-70,10 Q0,50 70,10 M-46,0 Q0,30 46,0" fill="none" stroke="#c9a24a" stroke-width="4"/>${[-70, -46, -22, 0, 22, 46, 70].map((cx, i) => `<path d="M${cx},${i % 2 ? 0 : 10} v18" stroke="#c9a24a" stroke-width="2"/><circle cx="${cx}" cy="${(i % 2 ? 0 : 10) + 22}" r="5" fill="#fff7d6"/><circle cx="${cx}" cy="${(i % 2 ? 0 : 10) + 22}" r="14" fill="#fff3b0" opacity=".3"/>`).join('')}</g>`;

// props that only appear at venues (anchor bottom-centre, like furniture)
const PROPS = {
  bistro: { w: 150, draw: () => `<path d="M-60,0 L-52,-60 M-30,0 L-38,-60" stroke="#2b2b2b" stroke-width="3"/><path d="M-66,-62 h44 M-58,-62 v-46 h28 v46" fill="none" stroke="#2b2b2b" stroke-width="3"/>
      <rect x="-3" y="-80" width="6" height="80" fill="#2b2b2b"/><ellipse cx="0" cy="-2" rx="22" ry="5" fill="#2b2b2b"/><ellipse cx="0" cy="-82" rx="46" ry="10" fill="#f2efe9" stroke="#bdb6aa"/>
      <path d="M-14,-92 h14 v-12 h-14 z" fill="#fff" stroke="#c9c0b2"/><path d="M10,-90 q6,-14 12,0 z" fill="#d38b5d"/><path d="M60,0 L52,-60 M30,0 L38,-60" stroke="#2b2b2b" stroke-width="3"/>` },
  bar: { w: 380, draw: () => `<rect x="-190" y="-120" width="380" height="120" fill="#3b2a4a"/><rect x="-200" y="-132" width="400" height="14" rx="4" fill="#e9d8f5"/>
      ${[-150, -70, 10, 90, 160].map(x => `<path d="M${x},-132 l-6,-24 h18 l-6,24 z" fill="${['#ff9ec4', '#9ee6d5', '#ffd166', '#c8a6ff', '#ff9ec4'][(x + 150) / 80 | 0] || '#ffd166'}" opacity=".9"/><circle cx="${x + 4}" cy="-158" r="3" fill="#ff4f8b"/>`).join('')}
      <rect x="-190" y="-60" width="380" height="4" fill="#ffd166" opacity=".7"/>` },
  dj: { w: 300, draw: () => `<rect x="-150" y="-110" width="300" height="110" rx="6" fill="#1d1730"/><rect x="-150" y="-110" width="300" height="10" fill="#ff4f8b"/>
      <circle cx="-70" cy="-112" r="26" fill="#2c2440" stroke="#555"/><circle cx="70" cy="-112" r="26" fill="#2c2440" stroke="#555"/>
      <text x="0" y="-50" text-anchor="middle" font-family="DM Sans,sans-serif" font-weight="700" font-size="22" fill="#5ee0c4">DJ STELLA</text>` },
  speaker: { w: 90, draw: () => `<rect x="-40" y="-190" width="80" height="190" rx="6" fill="#141018"/><circle cx="0" cy="-140" r="22" fill="#2a2333" stroke="#444"/><circle cx="0" cy="-60" r="30" fill="#2a2333" stroke="#444"/>` },
  longtable: { w: 760, draw: () => `<rect x="-380" y="-96" width="760" height="96" fill="#fbf8f2"/><path d="M-380,-96 h760 l14,14 h-788 z" fill="#fff"/>
      ${Array.from({ length: 9 }, (_, i) => `<ellipse cx="${-320 + i * 80}" cy="-100" rx="22" ry="5" fill="#fff" stroke="#d9cfbd"/>`).join('')}
      ${Array.from({ length: 5 }, (_, i) => `<rect x="${-284 + i * 140}" y="-150" width="6" height="50" fill="#f3e6c4"/><path d="M${-281 + i * 140},-162 q5,8 0,12 q-5,-4 0,-12 z" fill="#ffb347"/><circle cx="${-281 + i * 140}" cy="-156" r="12" fill="#ffd27a" opacity=".3"/>`).join('')}
      <path d="M-380,-60 h760" stroke="#e8dfcc" stroke-width="2"/>` },
  bush: { w: 140, draw: () => `<ellipse cx="0" cy="-40" rx="70" ry="44" fill="#3f8a4a"/><ellipse cx="-30" cy="-60" rx="40" ry="32" fill="#4f9c57"/><ellipse cx="30" cy="-56" rx="42" ry="30" fill="#4a9552"/>
      ${[[-40, -70], [-10, -82], [26, -74], [48, -46], [-56, -40], [8, -46], [-24, -30], [36, -24]].map(([x, y], i) => `<circle cx="${x}" cy="${y}" r="7" fill="${['#ffb3cd', '#fff', '#ffd166', '#ff8fb3'][i % 4]}"/>`).join('')}` },
  urn: { w: 110, draw: () => `<path d="M-30,0 L-24,-20 Q-50,-60 -30,-90 L30,-90 Q50,-60 24,-20 L30,0 Z" fill="#efe8d8" stroke="#c9bfa8"/><ellipse cx="0" cy="-120" rx="52" ry="40" fill="#3d7a46"/><ellipse cx="-20" cy="-140" rx="30" ry="24" fill="#4f9c57"/>` },
  rope: { w: 260, draw: () => `<rect x="-130" y="-90" width="8" height="90" fill="#c9a24a"/><rect x="122" y="-90" width="8" height="90" fill="#c9a24a"/><circle cx="-126" cy="-94" r="9" fill="#e2c46a"/><circle cx="126" cy="-94" r="9" fill="#e2c46a"/><path d="M-120,-84 Q0,-30 120,-84" fill="none" stroke="#a5162f" stroke-width="8" stroke-linecap="round"/>` },
  photog: { w: 120, draw: () => `<path d="M-30,0 L-26,-90 Q-30,-150 0,-150 Q30,-150 26,-90 L30,0 Z" fill="#20202a"/><circle cx="0" cy="-170" r="22" fill="#2c2c36"/>
      <rect x="10" y="-170" width="40" height="28" rx="4" fill="#3a3a44"/><circle cx="34" cy="-156" r="9" fill="#111" stroke="#666"/><path d="M50,-182 l14,-14 M56,-170 h20 M50,-160 l14,10" stroke="#fff6c9" stroke-width="3"/><circle cx="58" cy="-176" r="16" fill="#fff" opacity=".55"/>` },
  crowd: { w: 1000, draw: () => { const r = rng(12); let s = ''; for (let row = 0; row < 3; row++) for (let i = 0; i < 26; i++) { const x = -500 + i * 40 + r() * 10, y = -row * 26; s += `<circle cx="${x.toFixed(0)}" cy="${(y - 36).toFixed(0)}" r="13" fill="${['#2a2338', '#3a3049', '#241e30'][row]}"/><rect x="${(x - 16).toFixed(0)}" y="${(y - 24).toFixed(0)}" width="32" height="40" rx="12" fill="${['#2a2338', '#3a3049', '#241e30'][row]}"/>`; } return s; } },
};
const propDraw = k => (PROPS[k] || FURN[k]).draw();
const propW = k => (PROPS[k] || FURN[k]).w || 150;

// each venue: a backdrop, plus props that get depth-sorted with the people
const VENUES = {
  bookshop: { bg: () => {
      let s = `<linearGradient id="vw" x1="0" x2="1"><stop offset="0" stop-color="#6b4230"/><stop offset=".5" stop-color="#8a5a43"/><stop offset="1" stop-color="#6b4230"/></linearGradient><rect width="1000" height="${FLOOR_Y}" fill="url(#vw)"/>`;
      const r = rng(8);
      for (let u = 0; u < 4; u++) {
        const x0 = 22 + u * 245;
        s += `<rect x="${x0}" y="36" width="224" height="408" fill="#4a2a1d"/>`;
        for (let sh = 0; sh < 5; sh++) {
          const base = 112 + sh * 72;
          for (let x = x0 + 10; x < x0 + 210;) { const w = 9 + r() * 9, h = 40 + r() * 22; s += `<rect x="${x.toFixed(0)}" y="${(base - h).toFixed(0)}" width="${w.toFixed(0)}" height="${h.toFixed(0)}" fill="${['#c94f5c', '#e9b44c', '#4f7a9c', '#5e9c76', '#f2e3c6', '#9b5de5', '#2b3a67'][Math.floor(r() * 7)]}"/>`; x += w + 1; }
          s += `<rect x="${x0}" y="${base}" width="224" height="8" fill="#5f3826"/>`;
        }
      }
      return s + `<rect y="${WALL_Y}" width="1000" height="14" fill="#4a2a1d"/>` + floorBoards('#9b6b4a', '#7a5038');
    }, props: [['rug', 500, 575], ['armchair', 160, 602], ['floorlamp', 70, 600], ['plant', 930, 596]] },
  cafe: { bg: () => {
      let s = `<rect width="1000" height="${FLOOR_Y}" fill="#f4e7d4"/><rect y="290" width="1000" height="154" fill="#86a89a"/>${Array.from({ length: 34 }, (_, i) => `<path d="M${i * 30},290 V444" stroke="#6f9384"/>`).join('')}
        <rect x="80" y="60" width="270" height="180" rx="6" fill="#2f3a35" stroke="#a0714f" stroke-width="10"/>
        <text x="215" y="102" text-anchor="middle" font-family="Fraunces,Georgia,serif" font-style="italic" font-size="28" fill="#fff">Menu</text>
        ${['Latte ........ 4', 'Matcha ...... 5', 'Hot cocoa ... 4', 'Croissant .... 3'].map((t, i) => `<text x="110" y="${138 + i * 26}" font-family="DM Sans,sans-serif" font-size="17" fill="#f4efe6">${t}</text>`).join('')}
        <rect x="600" y="50" width="320" height="220" fill="#fff" stroke="#e2dfda" stroke-width="8"/><rect x="608" y="58" width="304" height="204" fill="url(#skyG)"/>${skyline('#c7c1d6', 262, 9)}
        <rect x="604" y="150" width="312" height="6" fill="#fff"/><rect x="756" y="54" width="6" height="212" fill="#fff"/>
        ${[200, 500, 800].map(x => `<path d="M${x},0 V70" stroke="#333"/><path d="M${x - 30},100 Q${x},50 ${x + 30},100 Z" fill="#2f3a35"/><ellipse cx="${x}" cy="100" rx="30" ry="5" fill="#ffe39a"/><ellipse cx="${x}" cy="130" rx="60" ry="40" fill="#fff3c4" opacity=".18"/>`).join('')}`;
      s += `<rect x="40" y="330" width="380" height="114" fill="#a0714f"/><rect x="30" y="318" width="400" height="16" rx="4" fill="#ece5da"/><rect x="300" y="262" width="80" height="56" rx="6" fill="#c9ccd1"/><rect x="320" y="282" width="40" height="8" fill="#555"/><path d="M90,318 v-20 h22 v20" fill="#fff" stroke="#ccc"/><path d="M130,318 v-26 h22 v26" fill="#fff" stroke="#ccc"/>`;
      return s + checker('#f7f4ee', '#2f3a35');
    }, props: [['bistro', 220, 598], ['bistro', 760, 590], ['plant', 940, 600]] },
  rooftop: { bg: () => sky('#8f6bd9', '#ff9a8b', '#ffd59a') + `<circle cx="760" cy="300" r="70" fill="#ffe2a8" opacity=".9"/>` + skyline('#4b3a6b', 420, 5) + skyline('#3a2c55', 444, 6) + fairy(60, 70) + fairy(140, 50, '#fff') +
      `<rect y="372" width="1000" height="72" fill="#cfe8f2" opacity=".35"/>${Array.from({ length: 11 }, (_, i) => `<rect x="${i * 100}" y="368" width="6" height="76" fill="#8a7fa0"/>`).join('')}<rect y="364" width="1000" height="8" fill="#8a7fa0"/>` + floorBoards('#b98f6a', '#8e6a4c'),
    props: [['bar', 210, 560], ['sofa', 800, 600], ['plant', 960, 598]] },
  club: { bg: () => `<rect width="1000" height="${FLOOR_Y}" fill="#140d24"/>
      ${[[150, '#ff4f8b'], [380, '#5ee0c4'], [620, '#ffd166'], [850, '#9b6bf2']].map(([x, c]) => `<path d="M${x},0 L${x - 160},${FLOOR_Y} L${x + 60},${FLOOR_Y} Z" fill="${c}" opacity=".16"/>`).join('')}
      <text x="500" y="150" text-anchor="middle" font-family="Fraunces,Georgia,serif" font-style="italic" font-weight="700" font-size="74" fill="none" stroke="#ff4f8b" stroke-width="4">Starlife</text>
      <text x="500" y="150" text-anchor="middle" font-family="Fraunces,Georgia,serif" font-style="italic" font-weight="700" font-size="74" fill="none" stroke="#ff4f8b" stroke-width="14" opacity=".25"/>
      <path d="M500,0 V40" stroke="#888"/><circle cx="500" cy="70" r="34" fill="#c0c5d6"/>${Array.from({ length: 6 }, (_, i) => `<path d="M466,${46 + i * 10} H534 M${472 + i * 11},38 V102" stroke="#fff" stroke-width=".8" opacity=".6"/>`).join('')}
      ${(() => { let t = ''; const cols = ['#ff4f8b', '#5ee0c4', '#ffd166', '#9b6bf2']; for (let r = 0; r < 5; r++) for (let c = 0; c < 12; c++) { const y1 = FLOOR_Y + r * 36, x1 = c * 84 - r * 4; t += `<rect x="${x1}" y="${y1}" width="82" height="34" fill="${cols[(r + c) % 4]}" opacity="${((r * 7 + c * 3) % 5) / 10 + 0.2}"/>`; } return `<rect y="${FLOOR_Y}" width="1000" height="176" fill="#1d1430"/>` + t; })()}`,
    props: [['dj', 500, 470], ['speaker', 120, 520], ['speaker', 880, 520]] },
  ballroom: { bg: () => `<linearGradient id="vw" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#f6ecd2"/><stop offset="1" stop-color="#e6d3a6"/></linearGradient><rect width="1000" height="${FLOOR_Y}" fill="url(#vw)"/>
      ${[120, 380, 620, 880].map(x => `<path d="M${x - 70},400 V170 A70,70 0 0 1 ${x + 70},170 V400 Z" fill="#26305a"/><path d="M${x - 70},400 V170 A70,70 0 0 1 ${x + 70},170 V400 Z" fill="none" stroke="#c9a24a" stroke-width="6"/><circle cx="${x - 30}" cy="200" r="2" fill="#fff"/><circle cx="${x + 20}" cy="250" r="1.5" fill="#fff"/><path d="M${x},100 V400" stroke="#c9a24a" stroke-width="3"/>`).join('')}
      ${[0, 250, 500, 750, 1000].map(x => `<rect x="${x - 18}" y="40" width="36" height="404" fill="#fbf4e2" stroke="#d9c38a"/>`).join('')}
      ${chandelier(250, 70)}${chandelier(750, 70)}${chandelier(500, 110, 1.3)}` + checker('#f7f1e3', '#d6c7a3'),
    props: [['urn', 60, 590], ['urn', 940, 590]] },
  banquet: { bg: () => `<rect width="1000" height="${FLOOR_Y}" fill="#5a1726"/>${Array.from({ length: 25 }, (_, i) => `<rect x="${i * 40}" width="20" height="${FLOOR_Y}" fill="#7a2234" opacity=".7"/>`).join('')}
      <path d="M0,0 Q250,120 500,0 Q750,120 1000,0 V30 Q750,150 500,30 Q250,150 0,30 Z" fill="#8f2a3e"/>${chandelier(300, 80)}${chandelier(700, 80)}` + floorBoards('#5e3a2b', '#3d241a'),
    props: [['longtable', 500, 470], ['urn', 50, 600], ['urn', 950, 600]] },
  garden: { bg: () => sky('#7ec2f0', '#bfe3fb', '#e9f6ff') + `${[80, 230, 420, 610, 790, 940].map((x, i) => `<circle cx="${x}" cy="${260 - (i % 2) * 30}" r="${90 + (i % 3) * 20}" fill="${i % 2 ? '#5a9a5f' : '#4a8a50'}"/>`).join('')}
      <path d="M380,330 L500,250 L620,330 Z" fill="#fff"/><rect x="390" y="330" width="220" height="114" fill="#fffaf2"/><path d="M390,330 Q445,360 500,330 Q555,360 610,330" fill="none" stroke="#e7dccb" stroke-width="3"/>
      <rect y="360" width="1000" height="84" fill="#3f7a46"/>${Array.from({ length: 50 }, (_, i) => `<circle cx="${i * 20 + 10}" cy="362" r="14" fill="#4a8a50"/>`).join('')}` + fairy(40, 90) +
      `<linearGradient id="evFloor" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#7cbf6e"/><stop offset="1" stop-color="#5fa356"/></linearGradient><rect y="${FLOOR_Y}" width="1000" height="${ROOM_H - FLOOR_Y}" fill="url(#evFloor)"/>${Array.from({ length: 10 }, (_, i) => `<rect x="${i * 100}" y="${FLOOR_Y}" width="50" height="${ROOM_H - FLOOR_Y}" fill="#fff" opacity=".06"/>`).join('')}`,
    props: [['bush', 90, 580], ['bush', 910, 586], ['sidetable', 300, 560]] },
  gala: { bg: () => `<linearGradient id="vw" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#e7e2da"/><stop offset="1" stop-color="#cfc6b8"/></linearGradient><rect width="1000" height="${FLOOR_Y}" fill="url(#vw)"/>
      <path d="M330,444 L380,200 L620,200 L670,444 Z" fill="#f2ede4"/>${Array.from({ length: 12 }, (_, i) => `<path d="M${330 + i * 4.2},${444 - i * 20.3} H${670 - i * 4.2}" stroke="#d6cdbf" stroke-width="2"/>`).join('')}<path d="M440,444 L470,200 L530,200 L560,444 Z" fill="#a5162f"/>
      <rect x="60" y="80" width="180" height="230" fill="#c9a24a"/><rect x="74" y="94" width="152" height="202" fill="#2b3a67"/><circle cx="150" cy="170" r="44" fill="#ffd166"/><path d="M90,296 Q150,200 210,296 Z" fill="#ff4f8b"/>
      <rect x="760" y="80" width="180" height="230" fill="#c9a24a"/><rect x="774" y="94" width="152" height="202" fill="#3f7a6b"/><path d="M790,280 L850,120 L910,280 Z" fill="#f2e3c6"/><circle cx="850" cy="200" r="20" fill="#c94f5c"/>
      ${[290, 710].map(x => `<rect x="${x - 22}" y="20" width="44" height="424" fill="#f7f2e9" stroke="#d6cdbf"/>`).join('')}
      <text x="500" y="160" text-anchor="middle" font-family="Fraunces,Georgia,serif" font-style="italic" font-size="34" fill="#5a4a3a">The Grand Gala</text>` + checker('#f4efe6', '#d8cfbf'),
    props: [['rope', 230, 560], ['rope', 770, 560], ['urn', 50, 600], ['urn', 950, 600]] },
  awards: { bg: () => {
      let s = `<rect width="1000" height="${FLOOR_Y}" fill="#1f1b2e"/>`;
      for (let r = 0; r < 6; r++) for (let c = 0; c < 6; c++) s += `<g transform="translate(${c * 180 + (r % 2) * 90 - 20},${50 + r * 66})"><circle r="16" fill="#ff4f8b"/><path d="M0,-9 l2.6,5.4 5.9.8-4.3,4.1 1,5.9L0,9.4l-5.2,2.8 1-5.9-4.3-4.1 5.9-.8z" fill="#fff"/><text x="24" y="6" font-family="DM Sans,sans-serif" font-weight="700" font-size="15" fill="#fff" opacity=".85">STARLIFE AWARDS</text></g>`;
      return s + `<rect y="${FLOOR_Y}" width="1000" height="${ROOM_H - FLOOR_Y}" fill="#2a2433"/><path d="M380,${FLOOR_Y} L620,${FLOOR_Y} L760,620 L240,620 Z" fill="#b3132c"/><path d="M380,${FLOOR_Y} L620,${FLOOR_Y} L630,460 L370,460 Z" fill="#8e0f24"/>`;
    }, props: [['photog', 90, 610], ['photog', 900, 612], ['rope', 230, 560], ['rope', 770, 560]] },
  runway: { bg: () => `<rect width="1000" height="${FLOOR_Y}" fill="#18141f"/>${[200, 500, 800].map(x => `<path d="M${x},0 L${x - 120},${FLOOR_Y} L${x + 120},${FLOOR_Y} Z" fill="#fff" opacity=".06"/>`).join('')}
      <rect x="350" y="120" width="300" height="324" fill="#26212f"/><text x="500" y="200" text-anchor="middle" font-family="Fraunces,Georgia,serif" font-style="italic" font-weight="700" font-size="48" fill="#fff">SLFW</text><text x="500" y="232" text-anchor="middle" font-family="DM Sans,sans-serif" font-size="15" letter-spacing="4" fill="#ff8fb3">STARLIFE FASHION WEEK</text>
      <rect y="${FLOOR_Y}" width="1000" height="${ROOM_H - FLOOR_Y}" fill="#221c2b"/><path d="M420,${FLOOR_Y} L580,${FLOOR_Y} L700,620 L300,620 Z" fill="#f2eef6"/>`,
    props: [['crowd', 170, 470], ['crowd', 830, 470]] },
};

const LOOKS = {
  casual: [{ top: 'tee', bottom: 'jeans', shoes: 'sneakers' }, { top: 'breton', bottom: 'shorts', shoes: 'flats' }, { dress: 'sundress', shoes: 'sandals' }, { top: 'turtle', bottom: 'pleated', shoes: 'flats' }],
  night: [{ dress: 'sequin', shoes: 'pumps', earrings: 'drops' }, { top: 'corset', bottom: 'jeans', shoes: 'ankleboots', outer: 'biker' }, { dress: 'slip', shoes: 'pumps' }, { top: 'cami', bottom: 'mini', shoes: 'kneeboots', necklace: 'choker' }],
  formal: [{ dress: 'gown', hat: 'tiara' }, { dress: 'lbd', shoes: 'pumps', necklace: 'pearls' }, { dress: 'slip', shoes: 'pumps', earrings: 'drops' }, { dress: 'sequin', shoes: 'pumps', bag: 'clutch' }],
  chic: [{ top: 'blouse', bottom: 'wide', shoes: 'pumps' }, { dress: 'wrap', shoes: 'flats' }, { top: 'turtle', bottom: 'pencil', outer: 'trench', shoes: 'ankleboots' }, { top: 'breton', bottom: 'pleated', shoes: 'flats', hat: 'beret' }],
};
const mk = {
  natural: m => (m.shadow.style === 'none' || m.shadow.opacity < 0.5 ? 7 : 0) + (m.lips.finish !== 'metallic' ? 4 : 0) + (m.blush.color !== 'none' ? 4 : 0),
  glam: m => (m.lips.color !== 'none' && m.lips.opacity >= 0.7 ? 7 : 0) + (['volume', 'dramatic'].includes(m.mascara) ? 4 : 0) + (m.highlight.opacity >= 0.4 ? 4 : 0),
  night: m => (['smoky', 'winged', 'halo'].includes(m.shadow.style) ? 7 : 0) + (['winged', 'cat', 'smudged'].includes(m.liner.style) ? 5 : 0) + (m.mascara !== 'none' ? 3 : 0),
  editorial: m => (m.lips.finish === 'metallic' || m.shadow.style === 'halo' ? 8 : 0) + (m.highlight.opacity >= 0.5 ? 4 : 0) + (m.contour.opacity >= 0.3 ? 3 : 0),
};
const ch = (l, fame, say) => ({ l, fame, say });
const EVENTS = [
  { id: 'bookclub', name: 'Book Club Reading', venue: 'bookshop', when: 'Mon · 6pm', vibe: 'casual', tags: ['chic', 'casual'], tip: 'Soft, natural makeup', makeup: mk.natural, title: 'Bookworm Chic', host: 'Noor',
    desc: 'Tea, cushions and this month\'s mystery novel at The Turning Page bookshop.', guests: ['noor', 'mina', 'ivy'],
    moments: [
      { text: 'Noor taps her cup. "So… who guessed the twist before the last chapter?"', choices: [ch('"I did, the gardener!"', 8, 'Everyone gasps. You were right!'), ch('Read your favourite quote aloud', 6, 'Mina writes it in her notebook.'), ch('Admit you skipped to the end', 4, 'Ivy laughs: "Me too, honestly."')] },
      { text: 'A cat jumps onto the shelf and books start to wobble.', choices: [ch('Catch the falling books', 7, 'Saved! The shopkeeper gives you a bookmark.'), ch('Scoop up the cat for cuddles', 6, 'It purrs and naps on your lap.'), ch('Snap a photo for later', 4, 'A very cute picture.')] },
      { text: 'Time to pick next month\'s book.', choices: [ch('Suggest a fashion biography', 6, 'Ivy approves loudly.'), ch('Suggest a fairy tale retelling', 7, 'The vote is unanimous.'), ch('Let Noor choose', 5, 'Noor beams and picks a classic.')] }] },
  { id: 'coffee', name: 'Coffee Date', venue: 'cafe', when: 'Tue · 10am', vibe: 'casual', tags: ['casual', 'summer'], tip: 'Fresh face and rosy cheeks', makeup: mk.natural, title: 'Café Darling', host: 'Mina',
    desc: 'Catch up with Mina over lattes and croissants at Bean & Bloom.', guests: ['mina', 'omar', 'aria'],
    moments: [
      { text: 'The barista asks for your order.', choices: [ch('Matcha latte with oat milk', 6, 'Very green, very good.'), ch('Hot cocoa with extra marshmallows', 7, 'Mina orders the same.'), ch('Surprise me!', 5, 'You get a lavender honey latte.')] },
      { text: 'Mina shows you dance practice videos on her phone.', choices: [ch('Learn the chorus move', 8, 'You nail it on the second try.'), ch('Hype her up', 6, '"You\'re the best!" she squeals.'), ch('Suggest matching outfits for her show', 7, 'She is SO in.')] },
      { text: 'Last croissant on the plate…', choices: [ch('Split it in half', 7, 'Friendship goals.'), ch('Let Mina have it', 6, 'She saves you the flaky top.'), ch('Order two more', 5, 'The best kind of problem.')] }] },
  { id: 'rooftop', name: 'Rooftop Lounge', venue: 'rooftop', when: 'Wed · 7pm', vibe: 'chic', tags: ['night', 'chic'], tip: 'A winged eye', makeup: mk.night, title: 'Sunset Icon', host: 'Luz',
    desc: 'Sunset mocktails and city views at the Skyline Terrace.', guests: ['luz', 'leo', 'zuri', 'ivy'],
    moments: [
      { text: 'The bartender slides over the mocktail menu.', choices: [ch('Pink lemonade fizz', 6, 'It sparkles in the sunset.'), ch('Mango passion spritz', 6, 'Tropical and delicious.'), ch('Ask for the secret special', 8, 'It changes colour when you stir it!')] },
      { text: 'Luz says the sunset would make a perfect movie scene.', choices: [ch('Act out a dramatic scene together', 8, 'Strangers applaud.'), ch('Take a golden-hour photo', 7, 'The lighting is unreal.'), ch('Just enjoy the view', 5, 'Peaceful and pretty.')] },
      { text: 'The string lights flicker on as the music starts.', choices: [ch('Start a slow dance', 7, 'Zuri joins in.'), ch('Request your favourite song', 6, 'The DJ plays it next.'), ch('Make a wish on the first star', 6, 'Shh, it\'s a secret.')] }] },
  { id: 'club', name: 'Club Night', venue: 'club', when: 'Fri · 9pm', vibe: 'night', tags: ['night', 'edgy', 'glam'], tip: 'Smoky eyes and bold liner', makeup: mk.night, title: 'Queen of the Dance Floor', host: 'Zuri',
    desc: 'DJ Stella is spinning all night at Neon Garden. Bring your best moves.', guests: ['zuri', 'jin', 'kai', 'luz'],
    moments: [
      { text: 'The beat drops and the floor lights up.', choices: [ch('Hit the dance floor first', 8, 'Everyone follows your lead.'), ch('Start a dance circle', 7, 'Aria does a spin in the middle.'), ch('Wave to DJ Stella', 5, 'She gives you a shout-out!')] },
      { text: 'Aria wants a dance battle.', choices: [ch('Accept, show off your best move', 8, 'The crowd goes wild.'), ch('Team up instead', 7, 'A perfectly synced duo.'), ch('Be the judge', 5, 'You call it a tie.')] },
      { text: 'Last song of the night!', choices: [ch('Group photo under the disco ball', 7, 'Sparkles everywhere.'), ch('Dance until the lights come up', 8, 'Legendary.'), ch('Grab a lemonade and cool down', 5, 'Refreshing.')] }] },
  { id: 'house', name: 'House Party', venue: 'house', when: 'Sat · 8pm', vibe: 'casual', tags: ['casual', 'night'], tip: 'Fun, glowy makeup', makeup: mk.glam, title: 'Hostess with the Mostest', host: 'You',
    desc: 'You\'re hosting! Your friends come over to your suite. Decorate it first, it counts.', guests: ['aria', 'theo', 'mina', 'kai'],
    moments: [
      { text: 'Ding dong! Your friends arrive at the door.', choices: [ch('Give the grand tour', 7, '"Your suite is gorgeous!"'), ch('Put on a playlist', 6, 'Instant party mood.'), ch('Bring out snacks', 7, 'The popcorn is gone in minutes.')] },
      { text: 'Zuri suggests a game.', choices: [ch('Karaoke!', 8, 'Aria hits the high note.'), ch('Fashion show in the living room', 8, 'The rug becomes a runway.'), ch('Charades', 6, 'Mina wins with a perfect penguin.')] },
      { text: 'It\'s getting late.', choices: [ch('Movie night with blankets', 7, 'Everyone falls asleep halfway through.'), ch('Make friendship bracelets', 7, 'You trade them before saying goodbye.'), ch('Plan the next party', 6, 'Same time next week?')] }] },
  { id: 'garden', name: 'Garden Party', venue: 'garden', when: 'Sun · 2pm', vibe: 'chic', tags: ['summer', 'chic'], tip: 'Glossy lips and a soft flush', makeup: mk.natural, title: 'Garden Party Darling', host: 'Noor',
    desc: 'Lemonade, flower crowns and croquet on the lawn.', guests: ['noor', 'ivy', 'mina'],
    moments: [
      { text: 'There\'s a table of flowers for making crowns.', choices: [ch('Weave a daisy crown', 7, 'Pretty and perfect.'), ch('Make one for Noor', 8, 'She wears it all day.'), ch('Add roses to your bag', 5, 'Smells lovely.')] },
      { text: 'A croquet match begins.', choices: [ch('Play to win', 7, 'Victory on the last hit!'), ch('Cheer from the sidelines', 5, 'The loudest fan.'), ch('Invent new rules', 6, 'Nobody understands them, but it\'s fun.')] },
      { text: 'A breeze sends hats flying!', choices: [ch('Chase them down', 7, 'You return every one.'), ch('Laugh and let them fly', 5, 'One lands on a statue.'), ch('Lend Ivy your scarf', 6, 'She ties it like a pro.')] }] },
  { id: 'ball', name: 'Masquerade Ball', venue: 'ballroom', when: 'Sat · 9pm', vibe: 'formal', tags: ['glam', 'redcarpet'], tip: 'Full glam: bold lips and lashes', makeup: mk.glam, title: 'Belle of the Ball', host: 'Aria',
    desc: 'Masks, chandeliers and a string quartet in the Crystal Ballroom.', guests: ['aria', 'leo', 'luz', 'zuri'],
    moments: [
      { text: 'The herald announces your arrival.', choices: [ch('Sweep down the stairs gracefully', 8, 'Heads turn.'), ch('Curtsy to the room', 7, 'Very royal.'), ch('Wave like a pop star', 5, 'Aria waves back.')] },
      { text: 'A masked stranger asks you to waltz.', choices: [ch('Accept and twirl', 8, 'It\'s Luz! She\'s a great dancer.'), ch('Teach them a new step', 7, 'The whole room copies you.'), ch('Politely decline and chat', 5, 'You make a new friend.')] },
      { text: 'At midnight everyone unmasks.', choices: [ch('Reveal with a dramatic flourish', 8, 'Gasps all round.'), ch('Swap masks with Zuri', 6, 'Twinning.'), ch('Keep yours on, mysterious', 6, 'Everyone wonders who you are.')] }] },
  { id: 'banquet', name: 'Charity Banquet', venue: 'banquet', when: 'Thu · 7pm', vibe: 'formal', tags: ['chic', 'redcarpet'], tip: 'Polished, elegant makeup', makeup: mk.glam, title: 'Gracious Guest of Honour', host: 'Ivy',
    desc: 'A candlelit dinner raising money for the animal shelter.', guests: ['ivy', 'omar', 'noor', 'luz'],
    moments: [
      { text: 'You\'re seated next to the guest of honour.', choices: [ch('Ask about the shelter', 8, 'She tells you about a puppy named Biscuit.'), ch('Compliment her brooch', 6, 'It was her grandmother\'s.'), ch('Pass the bread rolls', 5, 'Always appreciated.')] },
      { text: 'The auction begins: a painting of kittens.', choices: [ch('Bid generously', 8, 'You win, all for a good cause!'), ch('Cheer on Noor\'s bid', 6, 'She wins it!'), ch('Donate your bracelet', 7, 'A very kind gesture.')] },
      { text: 'You\'re asked to say a few words.', choices: [ch('Give a heartfelt toast', 8, 'Standing ovation.'), ch('Thank the volunteers', 7, 'They wave from the back.'), ch('Keep it short and sweet', 6, 'Perfect timing.')] }] },
  { id: 'gala', name: 'The Grand Gala', venue: 'gala', when: 'First Monday · 8pm', vibe: 'formal', tags: ['glam', 'redcarpet'], tip: 'Editorial: metallic or halo eyes', makeup: mk.editorial, title: 'Gala Showstopper', host: 'Ivy',
    desc: 'Fashion\'s biggest night at the museum. This year\'s theme: "Stars & Dreams". Go dramatic.', guests: ['ivy', 'zuri', 'luz', 'aria'],
    moments: [
      { text: 'You reach the bottom of the famous museum staircase.', choices: [ch('Strike a pose on every step', 8, 'The photographers love it.'), ch('Twirl to show off the outfit', 8, 'Pure drama.'), ch('Walk up hand in hand with Zuri', 7, 'Iconic duo.')] },
      { text: 'A reporter asks: "Who are you wearing?"', choices: [ch('"A Starlife original!"', 7, 'Ivy blushes with pride.'), ch('"Something I designed myself"', 8, 'The reporter is impressed.'), ch('Explain how it fits the theme', 7, 'Very thoughtful.')] },
      { text: 'Inside, there\'s a surprise performance.', choices: [ch('Dance in front of the stage', 7, 'Aria spots you and waves.'), ch('Find the dessert table', 5, 'Tiny cakes everywhere!'), ch('Explore the art', 6, 'You find a painting that matches your look.')] }] },
  { id: 'awards', name: 'Starlife Awards', venue: 'awards', when: 'Sun · 7pm', vibe: 'formal', tags: ['redcarpet', 'glam'], tip: 'Red carpet glam', makeup: mk.glam, title: 'Best Dressed on the Carpet', host: 'Luz',
    desc: 'The red carpet for Starlife\'s award show. Luz is nominated for Best Actress!', guests: ['luz', 'leo', 'zuri', 'aria'],
    moments: [
      { text: 'Flash! Flash! The photographers call your name.', choices: [ch('Over-the-shoulder pose', 8, 'Front-page shot.'), ch('Blow a kiss to the cameras', 7, 'Very glamorous.'), ch('Pose with Luz', 7, 'Best-friend energy.')] },
      { text: 'An interviewer asks for your prediction.', choices: [ch('"Luz is taking it home!"', 8, 'Luz squeezes your hand.'), ch('"Everyone\'s a winner tonight"', 6, 'So diplomatic.'), ch('"Wait and see…"', 5, 'Ooh, mysterious.')] },
      { text: '"And the award goes to… Luz Ortega!"', choices: [ch('Cheer the loudest', 8, 'She points at you from the stage.'), ch('Happy tears', 7, 'Waterproof mascara saves the day.'), ch('Start a standing ovation', 8, 'The whole room joins.')] }] },
  { id: 'fashionweek', name: 'Fashion Week Show', venue: 'runway', when: 'Fri · 5pm', vibe: 'chic', tags: ['chic', 'edgy'], tip: 'Editorial makeup', makeup: mk.editorial, title: 'Front Row Favourite', host: 'Zuri',
    desc: 'Front-row seats at Starlife Fashion Week. Zuri walks the finale.', guests: ['zuri', 'jin', 'ivy'],
    moments: [
      { text: 'You find your name card in the front row.', choices: [ch('Snap a selfie with it', 6, 'Proof!'), ch('Chat with the designer', 8, 'Ivy introduces you.'), ch('Sketch outfits while you wait', 7, 'Your sketchbook is filling up.')] },
      { text: 'Zuri struts out for the finale.', choices: [ch('Cheer her name', 7, 'She winks mid-walk.'), ch('Film the whole walk', 6, 'Already trending.'), ch('Take notes on the looks', 6, 'You spot next season\'s trend.')] },
      { text: 'Backstage after the show.', choices: [ch('Congratulate the models', 7, 'Everyone hugs.'), ch('Ask to try on a look', 8, 'It fits perfectly!'), ch('Plan a collab with Ivy', 8, 'Watch this space.')] }] },
];
const STARLET_BY = Object.fromEntries(STARLETS.map(x => [x.id, x]));
function guestState(id, ev, i) {
  const s = starletState(id), look = LOOKS[ev.vibe][(i + ev.id.length) % LOOKS[ev.vibe].length];
  Object.assign(s.outfit, look);
  if (look.dress === 'gown') s.colors.gown = ['#e8b4c4', '#b18be0', '#d6b25e', '#a9c6ef'][i % 4];
  s.tucked = true;
  return s;
}
const SPOTS = [[205, 584], [760, 590], [890, 566], [330, 560]];
function eventGuests(ev) { return ev.guests.filter(g => g !== state.starlet).slice(0, 3); }
function eventSVG(s, ev, { standalone = false, scale = 1, aspect = 'xMidYMax slice' } = {}) {
  const guests = eventGuests(ev);
  if (ev.venue === 'house') {
    const room = clone(s.room);
    return roomSVG({ ...s, room }, { standalone, scale, aspect, guests: guests.map((g, i) => ({ s: guestState(g, ev, i), x: [180, 820, 700, 380][i], y: [590, 596, 566, 570][i] })) });
  }
  const V = VENUES[ev.venue];
  const items = V.props.map(([k, x, y]) => ({ y, svg: `<g transform="translate(${x},${y}) scale(${k === 'crowd' ? 1 : depth(y).toFixed(3)})">${k === 'rug' || k === 'crowd' ? '' : `<ellipse rx="${propW(k) * 0.46}" ry="10" fill="#000" opacity=".15" filter="url(#fShadow)"/>`}${propDraw(k)}</g>`, rug: k === 'rug' }));
  guests.forEach((g, i) => items.push({ y: SPOTS[i][1], svg: dollAt(guestState(g, ev, i), SPOTS[i][0], SPOTS[i][1], `g${i}-`) }));
  items.push({ y: 602, svg: dollAt(s, 480, 602, 'me-') });
  let o = `<defs>${baseDefs()}<filter id="fShadow" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="8"/></filter><linearGradient id="skyG" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8fc6ef"/><stop offset="1" stop-color="#e8f4fb"/></linearGradient></defs>` + V.bg();
  items.filter(i => i.rug).forEach(i => { o += i.svg; });
  items.filter(i => !i.rug).sort((a, b) => a.y - b.y).forEach(i => { o += i.svg; });
  const size = standalone ? ` width="${ROOM_W * scale}" height="${ROOM_H * scale}"` : '';
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${ROOM_W} ${ROOM_H}"${size} preserveAspectRatio="${aspect}">${o}</svg>`;
}

// ---------- events UI ----------
let evSel = null, evMode = 'list', evStep = 0, evPicked = [], preparing = null;
const evById = id => EVENTS.find(e => e.id === id);
function decorBonus() { return Math.min(state.room.items.length, 8) * 2; }
function renderEvents() {
  $('#evList').hidden = evMode !== 'list'; $('#evDetail').hidden = evMode !== 'detail'; $('#evPlay').hidden = evMode !== 'play';
  if (evMode === 'list') {
    $('#evGrid').innerHTML = EVENTS.map(ev => {
      const rec = profile.events?.[ev.id];
      return `<article class="invite" data-event="${ev.id}">
        <div class="invite-art" data-evart="${ev.id}"></div>
        <div class="invite-body"><small>${ev.when}</small><b>${ev.name}</b><p>${ev.desc}</p>
        <div class="shop-tags">${ev.tags.map(t => `<span>${TAG_LABEL[t]}</span>`).join('')}</div>
        <div class="invite-foot">${rec && rec.date === today() ? `<span class="owned-tag">Went today · ${rec.title}</span>` : `<span class="muted">Hosted by ${ev.host}</span>`}<button class="btn small primary" data-event="${ev.id}">RSVP</button></div></div></article>`;
    }).join('');
    // venue thumbnails are drawn lazily so the list stays quick
    requestAnimationFrame(() => $$('[data-evart]').forEach(el => { if (!el.firstChild) el.innerHTML = scope(venueThumb(evById(el.dataset.evart)), 'V' + el.dataset.evart + '-'); }));
  }
  if (evMode === 'detail') {
    const ev = evById(evSel), r = scoreLook(state, ev);
    UID = 0;
    $('#evScene').innerHTML = scope(eventSVG(state, ev), 'E');
    $('#evTitle').textContent = ev.name;
    $('#evWhen').textContent = `${ev.when} · hosted by ${ev.host}`;
    $('#evDesc').textContent = ev.desc;
    const line = (ok, t) => `<li class="${ok ? 'ok' : ''}">${ok ? '✓' : '○'} ${t}</li>`;
    $('#evBrief').innerHTML = `<h3>Dress code</h3><div class="shop-tags">${ev.tags.map(t => `<span>${TAG_LABEL[t]}</span>`).join('')}<span>${ev.tip}</span></div><ul>
      ${line(r.base, 'A full outfit')}${line(r.shoes, 'Shoes')}${line(r.match.length >= 3, `Pieces that match the dress code: ${r.match.length}`)}
      ${line(!r.off.length, r.off.length ? `Not quite right: ${r.off.map(i => i.name).join(', ')}` : 'Nothing off-code')}${line(r.mk >= 10, ev.tip)}
      ${ev.venue === 'house' ? line(state.room.items.length >= 6, `Suite decorated: ${state.room.items.length} pieces (6+ impresses guests)`) : ''}</ul>
      <p class="muted">Going with ${eventGuests(ev).map(g => STARLET_BY[g].name.split(' ')[0]).join(', ') || 'new friends'}.</p>`;
  }
}
function venueThumb(ev) {
  if (ev.venue === 'house') return roomSVG(state, { aspect: 'xMidYMid slice' }).replace(/<g data-doll[\s\S]*?<\/svg><\/g>/, '');
  const V = VENUES[ev.venue];
  return `<svg viewBox="0 0 1000 620" preserveAspectRatio="xMidYMid slice"><defs>${baseDefs()}<filter id="fShadow"><feGaussianBlur stdDeviation="8"/></filter><linearGradient id="skyG" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8fc6ef"/><stop offset="1" stop-color="#e8f4fb"/></linearGradient></defs>${V.bg()}${V.props.map(([k, x, y]) => `<g transform="translate(${x},${y}) scale(${k === 'crowd' ? 1 : depth(y).toFixed(3)})">${propDraw(k)}</g>`).join('')}</svg>`;
}
function openEvent(id) { evSel = id; evMode = 'detail'; renderEvents(); window.scrollTo({ top: 0 }); }
function startEvent() { evMode = 'play'; evStep = 0; evPicked = []; preparing = null; renderPrepBar(); renderEvents(); renderPlay(); }
function renderPlay() {
  const ev = evById(evSel);
  UID = 0;
  $('#evPlayScene').innerHTML = scope(eventSVG(state, ev), 'EP');
  const box = $('#evDialog');
  if (evStep === 0) {
    box.innerHTML = `<small>${ev.name}</small><p>You arrive at the ${ev.venue === 'house' ? 'door of your suite' : ev.name.toLowerCase()} looking amazing. ${ev.venue === 'house' ? 'Your guests are on their way!' : `${ev.host} spots you and waves.`}</p><div class="choices"><button class="btn primary" data-evnext>Let's go ›</button></div>`;
    return;
  }
  const m = ev.moments[evStep - 1];
  if (!m) { finishEvent(); return; }
  const picked = evPicked[evStep - 1];
  box.innerHTML = `<small>Moment ${evStep} of ${ev.moments.length}</small><p>${m.text}</p>` + (picked
    ? `<p class="reaction">${picked.say}</p><div class="choices"><button class="btn primary" data-evnext>${evStep === ev.moments.length ? 'See how it went ›' : 'Continue ›'}</button></div>`
    : `<div class="choices">${m.choices.map((c, i) => `<button class="btn" data-evchoice="${i}">${c.l}</button>`).join('')}</div>`);
}
function finishEvent() {
  const ev = evById(evSel), r = scoreLook(state, ev);
  const fun = evPicked.reduce((a, c) => a + c.fame, 0) + (ev.venue === 'house' ? decorBonus() : 0);
  const dress = r.score, stars = clamp(Math.round(dress / 20), 1, 5);
  const title = dress >= 80 ? ev.title : dress >= 55 ? 'Looking great' : 'Fashionably late';
  profile.events = profile.events || {};
  const rec = profile.events[ev.id], first = !rec || rec.date !== today();
  let coins = 0, fame = 0;
  if (first) { coins = Math.round(dress / 3 + fun / 2); fame = Math.round(dress / 6 + fun); }
  profile.events[ev.id] = { date: today(), title };
  if (coins || fame) reward(coins, fame); else saveProfile();
  if (first) lifeAfterEvent(eventGuests(ev));
  // a friend who was there sends a message afterwards
  const friend = eventGuests(ev)[0];
  if (friend && first) npcMessage(friend, pickLine(friend, 'after', ev));
  $('#evDialog').innerHTML = `<small>${ev.name} · the morning after</small>
    <div class="ev-result"><div class="score-ring" style="--p:${dress};--m:${dress >= 80 ? '#f5c542' : dress >= 55 ? '#c7ccd6' : '#e7defa'}"><b>${dress}</b><small>style</small></div>
    <div><h3>${title}</h3><div class="stars">${'★'.repeat(stars)}<span>${'★'.repeat(5 - stars)}</span></div>
    <p>${first ? `You earned ${COIN}<b>${coins}</b> coins and ${FAME}<b>${fame}</b> fame${ev.venue === 'house' ? ` (your decorating added ${decorBonus()})` : ''}.` : 'You already went today, so no prize this time, but what a night!'}</p></div></div>
    <div class="choices"><button class="btn primary" data-evphoto>Snap a photo for my album</button><button class="btn" data-evback>Back to invitations</button></div>`;
}
function renderPrepBar() {
  const bar = $('#prepBar');
  if (!preparing) { bar.hidden = true; return; }
  const ev = evById(preparing), r = scoreLook(state, ev);
  bar.hidden = false;
  bar.innerHTML = `<span>Getting ready for <b>${ev.name}</b> · dress code: ${ev.tags.map(t => TAG_LABEL[t]).join(', ')} · style ${r.score}/100</span><button class="btn small primary" data-evgo>Go to the event ›</button><button class="btn small" data-evcancel>Cancel</button>`;
}

// =====================================================================
// Chat: private messages with the starlets, and a live Lounge
// =====================================================================
const PERSONA = {
  aria: 'a bubbly pop star who loves pink, glitter, karaoke and big choruses',
  zuri: 'a confident, warm supermodel who gives runway and posing tips',
  mina: 'a cheerful idol who loves dance practice, cute cafés and snacks',
  luz: 'a dramatic actress who adores classic films and red lipstick',
  noor: 'a gentle film star who loves books, tea and garden parties',
  ivy: 'a witty fashion designer with strong opinions about colour and fabric',
  kai: 'a laid-back musician who writes songs about city life and loves vinyl records',
  leo: 'a charming actor who rehearses lines everywhere and adores classic tailoring',
  jin: 'an energetic dancer who is always practising choreography and loves street style',
  theo: 'a friendly athlete who is into training, smoothies and cheering his friends on',
  omar: 'a warm chef who talks about recipes, markets and hosting dinner parties',
  stella: 'the player\'s upbeat personal stylist who knows everything happening in Starlife',
};
const CONTACTS_LIST = () => [{ id: 'stella', name: 'Stella', role: 'Your stylist' }, ...STARLETS.filter(x => x.id !== state.starlet).map(x => ({ id: x.id, name: x.name, role: x.role }))];
const LINES = {
  hello: ['Hiii! 💕 How\'s your day going?', 'Hey you! I was just thinking about you.', 'Hello, gorgeous! What are we planning?'],
  outfit: ['Ooh, try something from the Shop, the new pieces are so good.', 'Accessories! Two is perfect, three if you\'re brave.', 'Match the dress code and the judges melt.'],
  event: ['I\'ll be there! Save me a spot.', 'Wait, what are you wearing?? Tell me everything.', 'That\'s going to be the best night ever.'],
  party: ['A party?! I\'m bringing snacks.', 'Count me in! Should I bring my playlist?', 'Yesss. Your suite is the cutest place for it.'],
  thanks: ['Any time! 💖', 'That\'s what friends are for.', 'Aww, stop it, you!'],
  default: ['Haha, I love that.', 'Tell me more!', 'You always make me smile.', 'Same here!', 'Wait, really? That\'s amazing.'],
  greet: { stella: 'Hi! I\'m Stella. Message me any time for styling tips or what\'s on this week.', aria: 'Heyyy it\'s Aria! Karaoke at your place sometime? 🎤', zuri: 'Zuri here. Shoulders back, chin up, you\'re a star. ✨', mina: 'Hi hi! Mina here! Coffee this week? ☕', luz: 'Darling! Luz here. Promise me you\'ll come to the Awards. 🎬', noor: 'Hello! It\'s Noor. Have you read anything good lately? 📚', ivy: 'Ivy here. I saw your last look… let\'s talk. 👀', kai: 'Yo, Kai here. New track dropping soon, you\'re hearing it first. 🎧', leo: 'Leo here. Rehearsals are wild this week, coffee to escape? 🎭', jin: 'Jin! Dance battle at the club Friday, you in? 💃', theo: 'Theo here. Morning run tomorrow? I\'ll go easy on you. 🏃', omar: 'Omar here! Come by for dinner, I\'m testing a new recipe. 🍲' },
};
function pickLine(id, kind, ev) {
  if (kind === 'after') return [`That ${ev.name.toLowerCase()} was SO fun. Your outfit was everything! 💕`, `Best ${ev.name.toLowerCase()} ever. Let's do it again soon!`, `Still thinking about last night. You looked amazing at the ${ev.name.toLowerCase()}!`][id.length % 3];
  const arr = LINES[kind] || LINES.default;
  return arr[Math.floor(Math.random() * arr.length)];
}
function scriptedReply(id, text) {
  const t = text.toLowerCase();
  const ev = EVENTS.find(e => t.includes(e.name.toLowerCase().split(' ')[0].toLowerCase()) || t.includes(e.id));
  if (ev) return `${ev.name}? ${pickLine(id, 'event')} The dress code is ${ev.tags.map(x => TAG_LABEL[x].toLowerCase()).join(' and ')}.`;
  if (/party|come over|hang/.test(t)) return pickLine(id, 'party');
  if (/wear|outfit|dress|shoes|look|style|makeup/.test(t)) return pickLine(id, 'outfit');
  if (/thank|love you|ily|bestie/.test(t)) return pickLine(id, 'thanks');
  if (/\b(hi|hey|hello|hiya|yo)\b/.test(t)) return pickLine(id, 'hello');
  return pickLine(id, 'default');
}
function chats() { profile.chats = profile.chats || {}; profile.unread = profile.unread || {}; return profile.chats; }
function ensureGreetings() {
  const c = chats();
  for (const x of CONTACTS_LIST()) if (!c[x.id]) { c[x.id] = [{ me: false, text: LINES.greet[x.id], t: Date.now() }]; profile.unread[x.id] = 1; }
  saveProfile();
}
function npcMessage(id, text) {
  const c = chats(); (c[id] = c[id] || []).push({ me: false, text, t: Date.now() });
  if (!(view === 'chat' && chatSel === id)) profile.unread[id] = (profile.unread[id] || 0) + 1;
  c[id] = c[id].slice(-60);
  saveProfile(); renderChatBadge();
  if (view === 'chat') renderChat();
}
function renderChatBadge() {
  const n = Object.values(profile.unread || {}).reduce((a, b) => a + b, 0);
  const b = $('#chatBadge'); b.textContent = n; b.hidden = !n;
}
let chatSel = 'stella', lounge = [], room = null, roomTried = false, peersN = 0, sampleFn, sampleTried = false, typing = false;
const faceOf = id => {
  const x = id === 'stella' ? { skin: '#d9a273', hair: 'waves', hairColor: '#c9944f' } : STARLET_BY[id];
  return x ? `<svg viewBox="104 30 192 192">${portrait(x, '#d3647f')}</svg>` : `<svg viewBox="0 0 40 40"><circle cx="20" cy="20" r="20" fill="#ffd166"/><text x="20" y="26" text-anchor="middle" font-size="16" fill="#231942">★</text></svg>`;
};
function renderChat() {
  ensureGreetings();
  const c = chats();
  const list = CONTACTS_LIST();
  $('#chatList').innerHTML = `<button class="chat-item${chatSel === 'lounge' ? ' on' : ''}" data-chat="lounge"><span class="chat-face lounge">★</span><span class="chat-meta"><b>The Lounge</b><small>${room ? `${peersN} online now` : 'Live chat with other players'}</small></span></button>` +
    list.map(x => { const last = (c[x.id] || []).slice(-1)[0]; const u = profile.unread[x.id]; return `<button class="chat-item${chatSel === x.id ? ' on' : ''}" data-chat="${x.id}"><span class="chat-face">${faceOf(x.id)}</span><span class="chat-meta"><b>${x.name}</b><small>${last ? esc(last.text).slice(0, 42) : x.role}</small></span>${u ? `<i class="dot">${u}</i>` : ''}</button>`; }).join('');
  if (chatSel === 'lounge') return renderLounge();
  const who = list.find(x => x.id === chatSel) || list[0];
  chatSel = who.id;
  profile.unread[who.id] = 0; saveProfile(); renderChatBadge();
  $('#chatHead').innerHTML = `<span class="chat-face">${faceOf(who.id)}</span><div><b>${who.name}</b><small>${who.role}${sampleFn ? ' · replies written by Claude' : ''}</small></div>`;
  $('#chatLog').innerHTML = (c[who.id] || []).map(m => `<div class="msg${m.me ? ' me' : ''}"><p>${esc(m.text)}</p></div>`).join('') + (typing ? `<div class="msg"><p class="typing"><i></i><i></i><i></i></p></div>` : '');
  $('#chatLog').scrollTop = 1e9;
  $('#chatInput').disabled = false; $('#chatSend').disabled = false;
  $('#chatInput').placeholder = `Message ${who.name.split(' ')[0]}…`;
}
async function ensureSample() {
  if (sampleTried) return sampleFn; sampleTried = true;
  try { sampleFn = window.claude?.use ? await window.claude.use('sample') : null; } catch { sampleFn = null; }
  return sampleFn;
}
async function sendDM(text) {
  const id = chatSel, c = chats(), who = CONTACTS_LIST().find(x => x.id === id);
  (c[id] = c[id] || []).push({ me: true, text, t: Date.now() });
  befriend(id, 2); bumpNeed('social', 3);
  typing = true; renderChat();
  let reply = null;
  const sample = await ensureSample();
  if (sample) {
    const convo = c[id].slice(-10).map(m => `${m.me ? starletName() : who.name}: ${m.text}`).join('\n');
    const prompt = `You are ${who.name}, ${PERSONA[id]}, in Starlife, a cheerful fashion dress-up game for all ages.
You are texting your friend ${starletName()}. Things happening in Starlife this week: ${EVENTS.map(e => `${e.name} (${e.when})`).join(', ')}. There is also a Shop, style Contests and a Beauty Parlor.
Reply as ${who.name} only, in one or two short, friendly text messages (under 45 words total). Stay kind and suitable for kids. No links, no personal questions about real life, and never say you are an AI.

Chat so far:
${convo}
${who.name}:`;
    try { reply = (await sample(prompt, { modelTier: 'quick', cache: false })).text.trim().replace(/^"|"$/g, '').slice(0, 400); }
    catch (e) { if (e?.code === 'not_granted') sampleFn = null; reply = null; }
  } else await new Promise(r => setTimeout(r, 700 + Math.random() * 700));
  typing = false;
  npcMessage(id, reply || scriptedReply(id, text));
}

// ---------- the Lounge: live chat with whoever has Starlife open right now ----------
async function ensureRoom() {
  if (roomTried) return room; roomTried = true;
  try { room = window.claude?.use ? await window.claude.use('room') : null; } catch { room = null; }
  if (!room) return null;
  room.presence({ name: starletName(), starlet: state.starlet || '' }).catch(() => {});
  room.on('chat', m => {
    const d = m.data || {};
    if (typeof d.text !== 'string' || !d.text.trim()) return;
    lounge.push({ me: m.isMe && m.sameTab, name: String(d.name || 'A starlet').slice(0, 40), starlet: STARLET_BY[d.starlet] ? d.starlet : '', text: d.text.slice(0, 280), guest: m.guest });
    lounge = lounge.slice(-100);
    if (view === 'chat' && chatSel === 'lounge') renderLounge();
  });
  room.onPeers?.(() => { peersN = room.peers().filter(p => p.kind === 'viewer').length; if (view === 'chat') renderChat(); });
  peersN = room.peers?.().filter(p => p.kind === 'viewer').length || 1;
  return room;
}
function renderLounge() {
  $('#chatHead').innerHTML = `<span class="chat-face lounge">★</span><div><b>The Lounge</b><small>${room ? `Live · ${peersN} online · messages aren't saved` : 'Live chat'}</small></div>`;
  if (!room) {
    $('#chatLog').innerHTML = `<div class="chat-empty"><b>The Lounge opens when Starlife is played online.</b><p>When this game is shared as a hosted page, everyone who has it open can chat here together in real time. For now, message your starlet friends on the left.</p></div>`;
    $('#chatInput').disabled = true; $('#chatSend').disabled = true;
    return;
  }
  $('#chatInput').disabled = false; $('#chatSend').disabled = false;
  $('#chatInput').placeholder = 'Say hi to the Lounge…';
  $('#chatLog').innerHTML = lounge.length ? lounge.map(m => `<div class="msg${m.me ? ' me' : ''}">${m.me ? '' : `<span class="msg-who">${m.starlet ? `<span class="chat-face tiny">${faceOf(m.starlet)}</span>` : ''}${esc(m.name)}${m.guest ? ' · guest' : ''}</span>`}<p>${esc(m.text)}</p></div>`).join('')
    : `<div class="chat-empty"><b>It's quiet in here.</b><p>Say hello! Everyone in the Lounge right now will see it.</p></div>`;
  $('#chatLog').scrollTop = 1e9;
}
async function sendLounge(text) {
  if (!room) return;
  room.presence({ name: starletName(), starlet: state.starlet || '' }).catch(() => {});
  try { await room.emit('chat', { text, name: starletName(), starlet: state.starlet || '' }); }
  catch (e) { toast(e?.code === 'not_permitted' ? 'You need contributor access to this page to talk in the Lounge.' : 'Message not sent. Check your connection.'); }
}

// =====================================================================
// Life: needs, time of day, things to do, career, skills and friends
// =====================================================================

const NEEDS = [['hunger', 'Hunger', '#f2a03d'], ['energy', 'Energy', '#5b8def'], ['hygiene', 'Hygiene', '#3fbfb0'], ['fun', 'Fun', '#ff4f8b'], ['social', 'Social', '#9b6bf2']];
const DECAY = { hunger: 0.075, energy: 0.055, hygiene: 0.045, fun: 0.065, social: 0.05 }; // per game minute
const NEED_WORDS = { hunger: 'hungry', energy: 'exhausted', hygiene: 'in need of a shower', fun: 'bored', social: 'lonely' };
const SKILLS = [['cooking', 'Cooking'], ['fitness', 'Fitness'], ['charisma', 'Charisma'], ['creativity', 'Creativity']];
const CAREERS = [['Fashion Assistant', 40], ['Junior Stylist', 65], ['Stylist', 95], ['Senior Stylist', 130], ['Creative Director', 180]];
const DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const LS_LIFE = 'starlife.life.v1';
let life = Object.assign({ min: 8 * 60, speed: 1, sims: {}, furnished: false, giftDay: -1 }, lsGet(LS_LIFE, {}));
const simKey = () => state.starlet || 'custom';
function sim() {
  const k = simKey();
  if (!life.sims[k]) life.sims[k] = { needs: { hunger: 75, energy: 80, hygiene: 85, fun: 65, social: 55 }, skills: { cooking: 0, fitness: 0, charisma: 0, creativity: 0 }, career: { job: -1, days: 0, lastDay: -1 }, rel: {} };
  return life.sims[k];
}
const hourOf = (m = life.min) => (m % 1440) / 60;
const dayNum = () => Math.floor(life.min / 1440);
function clockLabel() {
  const m = life.min % 1440, h = Math.floor(m / 60), mm = Math.floor(m % 60), h12 = ((h + 11) % 12) + 1;
  return `${DAYS[dayNum() % 7]} · ${h12}:${String(mm).padStart(2, '0')} ${h < 12 ? 'AM' : 'PM'}`;
}
function mood() {
  const n = sim().needs, v = NEEDS.reduce((a, [k]) => a + n[k], 0) / NEEDS.length;
  return v >= 70 ? ['Happy', '#2fbf71', v] : v >= 45 ? ['Fine', '#e3b11f', v] : v >= 25 ? ['Uncomfortable', '#f08a3c', v] : ['Miserable', '#e5484d', v];
}
function bumpNeed(k, amt) { const n = sim().needs; n[k] = clamp(n[k] + amt, 0, 100); }
function addSkill(k, amt) {
  const sk = sim().skills, before = Math.floor(sk[k]);
  sk[k] = clamp(sk[k] + amt, 0, 10);
  if (Math.floor(sk[k]) > before) toast(`${starletName()} reached ${SKILLS.find(x => x[0] === k)[1]} level ${Math.floor(sk[k])}!`);
}
function befriend(id, amt) { if (!STARLET_BY[id]) return; const r = sim().rel; r[id] = clamp((r[id] || 0) + amt, 0, 100); }
const relLabel = v => v >= 85 ? 'Best friend' : v >= 60 ? 'Good friend' : v >= 35 ? 'Friend' : v >= 15 ? 'Acquaintance' : 'Stranger';
function timeTint(h = hourOf()) {
  if (h >= 21 || h < 5) return ['#141a46', 0.42];
  if (h >= 19) return ['#ff7a45', 0.12 + (h - 19) * 0.08];
  if (h < 7) return ['#ffb37a', 0.14];
  return null;
}
const tintKey = () => { const t = timeTint(); return t ? t[0] + t[1].toFixed(2) : 'day'; };

// ---------- things to do ----------
const A = (label, dur, fx, extra = {}) => ({ label, dur, fx, ...extra });
const READ = [A('Read a book', 60, { fun: 0.5 }, { skill: ['creativity', 0.02], icon: 'Reading' })];
const ACTIONS = {
  bed: [A('Sleep', 480, { energy: 0.22 }, { fast: 40, icon: 'Sleeping' }), A('Take a nap', 90, { energy: 0.2 }, { fast: 12, icon: 'Napping' })],
  kitchen: [A('Cook a meal', 60, { hunger: 1.5 }, { cost: 8, skill: ['cooking', 0.025], icon: 'Cooking' }), A('Grab a snack', 15, { hunger: 1.7 }, { cost: 3, icon: 'Snacking' })],
  shower: [A('Take a shower', 20, { hygiene: 4.6, energy: 0.1 }, { icon: 'Showering' })],
  tv: [A('Watch TV', 60, { fun: 0.9, energy: -0.02 }, { icon: 'Watching TV' }), A('Play video games', 60, { fun: 1.1, social: 0.1 }, { icon: 'Gaming' })],
  sofa: [A('Watch a movie', 90, { fun: 0.7 }, { icon: 'Movie time' }), A('Nap on the sofa', 60, { energy: 0.18 }, { fast: 10, icon: 'Napping' })],
  armchair: READ, rocker: [A('Relax', 30, { fun: 0.5, energy: 0.05 }, { icon: 'Relaxing' }), ...READ],
  bookshelf: [...READ, A('Study fashion history', 60, { fun: 0.2 }, { skill: ['charisma', 0.015], icon: 'Studying' })],
  desk: [A('Freelance styling job', 120, { fun: -0.12, energy: -0.05 }, { earn: 0.35, skill: ['creativity', 0.015], icon: 'Working' }), A('Browse Starlife', 30, { fun: 0.7, social: 0.4 }, { icon: 'Browsing' })],
  treadmill: [A('Work out', 60, { energy: -0.4, hygiene: -0.45, fun: 0.25 }, { skill: ['fitness', 0.03], icon: 'Working out' })],
  stereo: [A('Dance', 30, { fun: 1.3, energy: -0.25 }, { skill: ['fitness', 0.01], icon: 'Dancing' }), A('Listen to records', 30, { fun: 0.8 }, { icon: 'Listening' })],
  mirror: [A('Practise a speech', 30, { fun: 0.2 }, { skill: ['charisma', 0.03], icon: 'Practising' })],
  plant: [A('Water the plant', 10, { fun: 0.5 }, { icon: 'Watering' })],
  window: [A('Gaze outside', 10, { fun: 0.6 }, { icon: 'Daydreaming' })],
  art: [A('Admire the art', 10, { fun: 0.5 }, { skill: ['creativity', 0.01], icon: 'Admiring' })],
  map: [A('Plan a trip', 15, { fun: 0.6 }, { icon: 'Planning' })],
  gift: [A('Open the gift', 5, { fun: 2 }, { gift: true, icon: 'Unwrapping' })],
  boxes: [A('Unpack a box', 20, { fun: 0.3 }, { icon: 'Unpacking' })],
};
const SELF = [A('Call a friend', 30, { social: 1.6, fun: 0.3 }, { skill: ['charisma', 0.015], call: true, icon: 'On the phone' }), A('Text friends', 0, {}, { chat: true })];

let act = null, walking = false, working = false, menuTarget = null, warned = {}, lastTint = '', lastSave = 0;
function openActMenu(e, list, target) {
  const st = $('#suiteStage'), r = st.getBoundingClientRect(), m = $('#actMenu');
  menuTarget = { list, target };
  m.innerHTML = `<b>${target ? FURN[target.k].name : starletName()}</b>` + list.map((a, i) => `<button data-actdo="${i}">${a.label}${a.cost ? ` <small>${a.cost} coins</small>` : ''}${a.earn ? ' <small>earns coins</small>' : ''}</button>`).join('');
  m.hidden = false;
  const x = clamp(e.clientX - r.left, 8, r.width - 210), y = clamp(e.clientY - r.top, 8, r.height - m.offsetHeight - 8);
  m.style.left = x + 'px'; m.style.top = y + 'px';
}
const hideActMenu = () => { $('#actMenu').hidden = true; menuTarget = null; };
const pickFriend = () => { const ids = STARLETS.map(x => x.id).filter(id => id !== state.starlet); return ids[Math.floor(Math.random() * ids.length)]; };
function startAction(a, target) {
  hideActMenu();
  if (working) { toast(`${starletName()} is at work`); return; }
  if (a.chat) { setView('chat'); return; }
  if (a.gift && life.giftDay === dayNum()) { toast('Already opened today. Another gift arrives tomorrow!'); return; }
  if (a.cost && profile.coins < a.cost) { toast(`You need ${a.cost} coins for that`); return; }
  if (a.cost) { profile.coins -= a.cost; saveProfile(); }
  const go = () => { act = { a, left: a.dur, earned: 0, friend: a.call ? pickFriend() : null }; if (act.friend) toast(`Calling ${STARLET_BY[act.friend].name}…`); renderLifeHud(); };
  if (target) {
    const w = (FURN[target.k].w || 150) * depth(target.y) * 0.5, side = state.room.doll.x < target.x ? -1 : 1;
    walkTo(target.x + side * (w * 0.7 + 30), clamp(target.y + 10, FLOOR_Y + 40, ROOM_H - 6), go);
  } else go();
}
function walkTo(x, y, done) {
  const d = state.room.doll, g = $('#suiteStage [data-doll]');
  x = clamp(x, 70, 930);
  const x0 = d.x, y0 = d.y, t0 = performance.now(), dur = Math.min(1500, 250 + Math.hypot(x - x0, y - y0) * 2.4);
  walking = true;
  const step = t => {
    const k = Math.min(1, (t - t0) / dur), e = k < 0.5 ? 2 * k * k : 1 - Math.pow(-2 * k + 2, 2) / 2;
    d.x = x0 + (x - x0) * e; d.y = y0 + (y - y0) * e;
    const bob = Math.abs(Math.sin(k * Math.PI * 7)) * 4 * (1 - k * 0.6);
    if (g) g.setAttribute('transform', `translate(${d.x.toFixed(1)},${(d.y - bob).toFixed(1)}) scale(${depth(d.y).toFixed(3)})`);
    placeBubble();
    if (k < 1) requestAnimationFrame(step); else { walking = false; update(); done && done(); }
  };
  requestAnimationFrame(step);
}
function finishAction() {
  const a = act.a;
  if (a.earn) { const pay = Math.round(act.earned); reward(pay, 2); toast(`Freelance job done: +${pay} coins`); }
  else if (a.gift) { const c = 20 + Math.floor(Math.random() * 31); life.giftDay = dayNum(); reward(c, 3); toast(`The gift had ${c} coins inside!`); }
  else toast(`${starletName()} finished: ${a.label.toLowerCase()}`);
  act = null;
  renderLifeHud();
}
function advance(mins) {
  const n = sim().needs, day0 = dayNum();
  life.min += mins;
  const boost = act ? act.a.fx : {};
  for (const [k] of NEEDS) n[k] = clamp(n[k] - DECAY[k] * mins * (boost[k] > 0 ? 0 : 1), 0, 100);
  if (act) {
    const m = Math.min(mins, act.left);
    for (const [k, v] of Object.entries(act.a.fx)) n[k] = clamp(n[k] + v * m, 0, 100);
    if (act.a.skill) addSkill(act.a.skill[0], act.a.skill[1] * m);
    if (act.a.earn) act.earned += act.a.earn * m * (1 + Math.max(0, sim().career.job) * 0.25);
    if (act.friend) befriend(act.friend, 0.25 * m);
    act.left -= m;
    if (act.left <= 0) finishAction();
  }
  if (dayNum() !== day0) toast(`A new day: ${DAYS[dayNum() % 7]}`);
  for (const [k] of NEEDS) {
    if (n[k] < 20 && !warned[k]) { warned[k] = true; toast(`${starletName()} is ${NEED_WORDS[k]}`); }
    if (n[k] > 35) warned[k] = false;
  }
  if (tintKey() !== lastTint) { lastTint = tintKey(); if (view === 'suite') renderSuite(); }
  if (Date.now() - lastSave > 3000) { lastSave = Date.now(); lsSet(LS_LIFE, life); }
  renderLifeHud();
}
let lastTick = performance.now();
setInterval(() => {
  const now = performance.now(), dt = Math.min(1, (now - lastTick) / 1000); lastTick = now;
  if (view !== 'suite' || document.hidden || !$('#picker').hidden || $('#saveModal').classList.contains('on') || working || walking || decorMode) return;
  let rate = [0, 1, 4, 12][life.speed] ?? 1;
  if (act && act.a.fast && life.speed > 0) rate = Math.max(rate, act.a.fast);
  if (rate) advance(dt * rate);
}, 250);

// ---------- career ----------
function canWork() { const c = sim().career, h = hourOf(); return c.job >= 0 && dayNum() % 7 < 5 && h >= 6 && h < 11 && c.lastDay !== dayNum(); }
function goToWork() {
  const c = sim().career;
  if (!canWork()) { toast(c.job < 0 ? 'Find a job first, in the Life panel.' : 'Work starts between 6 and 11 AM on weekdays.'); return; }
  act = null; working = true; hideActMenu();
  const ov = $('#workOverlay'), title = CAREERS[c.job][0];
  ov.hidden = false;
  ov.innerHTML = `<div><small>At work</small><b>${title}</b><p id="workClock">${clockLabel()}</p><div class="work-bar"><i id="workBar"></i></div></div>`;
  const start = life.min, end = dayNum() * 1440 + 17 * 60, t0 = performance.now(), dur = 3200;
  const tick = t => {
    const k = Math.min(1, (t - t0) / dur), target = start + (end - start) * k;
    const n = sim().needs, mins = target - life.min;
    life.min = target;
    for (const [kk] of NEEDS) n[kk] = clamp(n[kk] - DECAY[kk] * mins * 0.8, 0, 100);
    $('#workClock').textContent = clockLabel(); $('#workBar').style.width = (k * 100) + '%';
    if (k < 1) requestAnimationFrame(tick); else endWork();
  };
  requestAnimationFrame(tick);
}
function endWork() {
  const c = sim().career, look = scoreLook(state, THEMES.find(t => t.id === 'office')).score, perf = Math.round(mood()[2] * 0.6 + look * 0.4);
  const pay = Math.round(CAREERS[c.job][1] * (perf >= 40 ? 1 : 0.6));
  bumpNeed('social', 15); bumpNeed('fun', -8);
  c.lastDay = dayNum();
  let promo = '';
  if (perf >= 60) { c.days++; if (c.days >= 3 && c.job < CAREERS.length - 1) { c.job++; c.days = 0; promo = `Promoted to ${CAREERS[c.job][0]}!`; } }
  reward(pay, promo ? 15 : 4);
  working = false;
  $('#workOverlay').innerHTML = `<div><small>Back home · ${clockLabel()}</small><b>${promo || 'A day at work'}</b><p>Performance ${perf}/100 (mood and an office-ready outfit both help). Earned ${pay} coins.</p><button class="btn primary" data-workdone>Continue</button></div>`;
  lsSet(LS_LIFE, life); update();
}

// ---------- HUD + Life panel ----------
function renderLifeHud() {
  const n = sim().needs, [ml, mc] = mood();
  $('#hudClock').textContent = clockLabel();
  $$('[data-speed]').forEach(b => b.classList.toggle('on', +b.dataset.speed === life.speed));
  $('#hudMood').innerHTML = `<i style="background:${mc}"></i>${ml}`;
  $('#hudNeeds').innerHTML = NEEDS.map(([k, l, c]) => `<span class="need" title="${l}: ${Math.round(n[k])}%"><small>${l}</small><span class="bar"><i style="width:${n[k]}%;background:${n[k] < 25 ? '#e5484d' : c}"></i></span></span>`).join('');
  $('#hudAct').innerHTML = act ? `<span class="doing">${act.a.icon || act.a.label}<span class="bar"><i style="width:${100 - act.left / act.a.dur * 100}%"></i></span><button data-actcancel aria-label="Stop">×</button></span>` : '';
  const wb = $('#hudWork'); wb.hidden = !canWork();
  const star = $('#moodStar'); if (star) star.style.color = mc;
  placeBubble();
  if (drawer === 'life') renderLifePanel();
}
function placeBubble() {
  const st = $('#suiteStage'), d = st?.querySelector('[data-doll] > svg'), star = $('#moodStar');
  if (!d || !star) return;
  const r = d.getBoundingClientRect(), sr = st.getBoundingClientRect();
  star.style.left = (r.left - sr.left + r.width / 2) + 'px';
  star.style.top = (r.top - sr.top + r.height * 0.1 - 30) + 'px';
  star.hidden = view !== 'suite' || decorMode || working;
  star.dataset.doing = act ? (act.a.icon || act.a.label) : '';
}
function renderLifePanel() {
  const s = sim(), c = s.career, [ml, mc, mv] = mood();
  $('#lifeBody').innerHTML = `<div class="life-mood"><span style="color:${mc}">★</span><div><b>${ml}</b><small>${starletName()} · mood ${Math.round(mv)}/100</small></div></div>
    <h4>Needs</h4>${NEEDS.map(([k, l, col]) => `<div class="lrow"><span>${l}</span><span class="bar big"><i style="width:${s.needs[k]}%;background:${s.needs[k] < 25 ? '#e5484d' : col}"></i></span></div>`).join('')}
    <h4>Skills</h4>${SKILLS.map(([k, l]) => `<div class="lrow"><span>${l}</span><span class="pips">${Array.from({ length: 10 }, (_, i) => `<i class="${i < Math.floor(s.skills[k]) ? 'on' : ''}"></i>`).join('')}</span></div>`).join('')}
    <h4>Career</h4>${c.job < 0 ? `<p class="muted">No job yet. The fashion house is hiring a Fashion Assistant (${CAREERS[0][1]} coins a day, weekdays 9 to 5).</p><button class="btn small primary" data-takejob>Take the job</button>`
      : `<div class="job"><b>${CAREERS[c.job][0]}</b><small>${CAREERS[c.job][1]} coins a day · ${c.job < CAREERS.length - 1 ? `${c.days}/3 great days to promotion` : 'Top of the career!'}</small></div>
         <button class="btn small primary" data-gowork${canWork() ? '' : ' disabled'}>Go to work</button> <span class="muted">${canWork() ? 'Shift runs until 5 PM.' : c.lastDay === dayNum() ? 'Done for today.' : 'Leave between 6 and 11 AM on weekdays.'}</span>`}
    <h4>Friends</h4>${STARLETS.filter(x => x.id !== state.starlet).map(x => { const v = s.rel[x.id] || 0; return `<div class="lrow friend"><span class="chat-face tiny">${faceOf(x.id)}</span><span>${x.name.split(' ')[0]}</span><span class="bar"><i style="width:${v}%;background:#ff4f8b"></i></span><small>${relLabel(v)}</small></div>`; }).join('')}`;
}
function lifeAfterEvent(guests) { bumpNeed('fun', 30); bumpNeed('social', 40); bumpNeed('energy', -15); life.min += 180; guests.forEach(g => befriend(g, 8)); addSkill('charisma', 0.3); lsSet(LS_LIFE, life); }

// starter furniture for living: add once to existing suites
function furnishForLife() {
  if (life.furnished) return;
  const have = new Set(state.room.items.map(i => i.k));
  const add = [['kitchen', 130, 478], ['tv', 600, 488], ['shower', 935, 488], ['bed', 260, 612], ['desk', 790, 600]];
  const isDefault = state.room.items.length === 6 && ['w1', 'w2', 'f1', 'f2', 'f3', 'f4'].every(id => state.room.items.some(i => i.id === id));
  if (isDefault) state.room.items = state.room.items.filter(i => !['rocker', 'sidetable'].includes(i.k));
  add.forEach(([k, x, y], i) => { if (!have.has(k)) state.room.items.push({ id: 'L' + i, k, x, y }); });
  if (isDefault) { const pl = state.room.items.find(i => i.k === 'plant'); if (pl) { pl.x = 690; pl.y = 600; } state.room.doll = { x: 470, y: 604 }; }
  life.furnished = true; lsSet(LS_LIFE, life);
}

// ---------- events ----------
document.addEventListener('click', e => {
  if (suppressClick) return;
  const t = e.target;
  const sp = t.closest('[data-speed]'); if (sp) { life.speed = +sp.dataset.speed; renderLifeHud(); return; }
  const ad = t.closest('[data-actdo]'); if (ad && menuTarget) { startAction(menuTarget.list[+ad.dataset.actdo], menuTarget.target); return; }
  if (t.closest('[data-actcancel]')) { act = null; renderLifeHud(); return; }
  if (t.closest('[data-takejob]')) { sim().career.job = 0; reward(0, 5); toast(`Hired as ${CAREERS[0][0]}! Head to work on a weekday morning.`); renderLifePanel(); renderLifeHud(); return; }
  if (t.closest('[data-gowork]')) { goToWork(); return; }
  if (t.closest('[data-workdone]')) { $('#workOverlay').hidden = true; return; }
  if (!t.closest('#actMenu')) hideActMenu();
  if (view === 'suite' && !decorMode && !working && !walking) {
    const fe = t.closest('#suiteStage [data-furn]');
    if (fe) { const it = state.room.items.find(i => i.id === fe.dataset.furn); if (it && ACTIONS[it.k]) { openActMenu(e, ACTIONS[it.k], it); return; } }
    if (t.closest('#suiteStage [data-doll]')) { openActMenu(e, SELF, null); return; }
  }
  const bind = t.closest('[data-bind]');
  if (bind) { setP(bind.dataset.bind, bind.dataset.val); update(); return; }
  const v = t.closest('[data-view]'); if (v) { setView(v.dataset.view); return; }
  const side = t.closest('[data-side]');
  if (side) {
    const k = side.dataset.side;
    if (k === 'wardrobe' || k === 'decor' || k === 'life') openDrawer(k);
    else if (k === 'parlor') setView('parlor');
    else if (k === 'camera') openSave();
    else if (k === 'full') {
      const st = $('#suiteStage');
      if (document.fullscreenElement) document.exitFullscreen?.();
      else if (st.requestFullscreen) st.requestFullscreen().catch(() => toast('Full screen is not available here'));
      else toast('Full screen is not available here');
    }
    return;
  }
  if (t.closest('[data-close-drawer]') || t.closest('#decorDone')) { openDrawer(null); return; }
  const tab = t.closest('[data-tab]');
  if (tab) {
    activeTab = tab.dataset.tab;
    $$('[data-tab]').forEach(b => b.classList.toggle('on', b === tab));
    $$('[data-pane]').forEach(p => p.classList.toggle('on', p.dataset.pane === activeTab));
    hairSig = ''; renderHairCards(); refreshUI();
    return;
  }
  const sub = t.closest('[data-subtab]');
  if (sub) {
    $$('[data-subtab]').forEach(b => b.classList.toggle('on', b === sub));
    $$('[data-subpane]').forEach(p => p.classList.toggle('on', p.dataset.subpane === sub.dataset.subtab));
    return;
  }
  const wc = t.closest('[data-wcat]');
  if (wc) { activeCat = wc.dataset.wcat; $$('[data-wcat]').forEach(b => b.classList.toggle('on', b === wc)); renderGrid(); return; }
  const item = t.closest('[data-item]'); if (item) { equip(item.dataset.item); return; }
  const rc = t.closest('[data-recolor]');
  if (rc) { if (rc.dataset.recolor === 'reset') delete state.colors[state.focus]; else state.colors[state.focus] = rc.dataset.recolor; update(); return; }
  const rm = t.closest('[data-remove]');
  if (rm) { const it = ITEM[state.outfit[rm.dataset.remove]]; state.outfit[rm.dataset.remove] = null; update(); toast(`Took off ${it.name}`); return; }
  const pr = t.closest('[data-preset]'); if (pr) { applyPreset(pr.dataset.preset); return; }
  const zm = t.closest('[data-zoom]'); if (zm) { zoom = zm.dataset.zoom; renderParlor(); return; }
  const add = t.closest('[data-addfurn]');
  if (add) {
    const k = add.dataset.addfurn, f = FURN[k];
    const y = f.layer === 'wall' ? 200 : f.layer === 'rug' ? 560 : 580;
    state.room.items.push({ id: 'i' + Date.now().toString(36), k, x: 300 + Math.random() * 400, y });
    update(); toast(`Added ${f.name}. Drag it into place.`); return;
  }
  const un = t.closest('[data-unplace]');
  if (un) { state.room.items = state.room.items.filter(i => i.id !== un.dataset.unplace); update(); return; }
  const sty = t.closest('[data-style]');
  if (sty) { if (sty.dataset.style !== state.starlet) { applyStarlet(sty.dataset.style); toast(`Now styling ${starletName()}`); } setView('suite'); return; }
  if (t.closest('#claimBonus')) { if (profile.lastBonus !== today()) { profile.lastBonus = today(); reward(50, 5); toast('+50 coins! Treat her to something in the Shop.'); renderHome(); } return; }
  const sc = t.closest('[data-shopcat]'); if (sc) { shopCat = sc.dataset.shopcat; renderShop(); return; }
  const tr = t.closest('[data-tryon]');
  if (tr) { const id = tr.dataset.tryon; tryOn = tryOn.includes(id) ? tryOn.filter(x => x !== id) : [...tryOn.filter(x => ITEM[x].slot !== ITEM[id].slot), id]; renderShop(); return; }
  const by = t.closest('[data-buy]'); if (by) { buy(by.dataset.buy); return; }
  const wn = t.closest('[data-wearnow]'); if (wn) { equip(wn.dataset.wearnow, true); toast(`${starletName()} is wearing ${ITEM[wn.dataset.wearnow].name}`); renderShop(); return; }
  const thb = t.closest('[data-theme]'); if (thb) { themeSel = thb.dataset.theme; renderContests(); return; }
  const shopLink = t.closest('[data-goshop]'); if (shopLink) { setView('shop'); return; }
  const chatBtn = t.closest('[data-chat]'); if (chatBtn) { chatSel = chatBtn.dataset.chat; renderChat(); $('#chatInput').focus(); return; }
  if (t.closest('[data-evback]')) { evMode = 'list'; renderEvents(); return; }
  if (t.closest('[data-evprep]')) { preparing = evSel; setView('suite'); if (drawer !== 'wardrobe') openDrawer('wardrobe'); renderPrepBar(); toast(`Dress for ${evById(evSel).name}, then press Go`); return; }
  if (t.closest('[data-evcancel]')) { preparing = null; renderPrepBar(); return; }
  if (t.closest('[data-evgo]')) { if (preparing) evSel = preparing; setView('events'); startEvent(); return; }
  if (t.closest('[data-evnext]')) { evStep++; renderPlay(); return; }
  const evc = t.closest('[data-evchoice]');
  if (evc) { const m = evById(evSel).moments[evStep - 1]; evPicked[evStep - 1] = m.choices[+evc.dataset.evchoice]; renderPlay(); return; }
  if (t.closest('[data-evphoto]')) {
    const ev = evById(evSel), btn = t.closest('[data-evphoto]'); btn.disabled = true;
    snapshot(state, { scale: 0.42, type: 'image/jpeg', quality: 0.85, svg: eventSVG(state, ev, { standalone: true }) }).then(thumb => {
      lookbook.unshift({ id: Date.now().toString(36), name: `${ev.name}`, date: Date.now(), state: clone(state), thumb, event: ev.id });
      lsSet(LS_BOOK, lookbook); toast('Photo saved to My Album ✦'); btn.textContent = 'Saved ✓';
    }).catch(() => { toast('Could not take the photo'); btn.disabled = false; });
    return;
  }
  const evb = t.closest('[data-event]'); if (evb) { openEvent(evb.dataset.event); return; }
  const pk = t.closest('[data-pick]'); if (pk) { pickSel = pk.dataset.pick; renderPicker(); return; }
  const car = t.closest('[data-car]');
  if (car) { const i = STARLETS.findIndex(x => x.id === pickSel); pickSel = STARLETS[(i + Number(car.dataset.car) + STARLETS.length) % STARLETS.length].id; renderPicker(); return; }
  const act = t.closest('[data-act]');
  if (act) {
    const id = act.closest('[data-look]').dataset.look, look = lookbook.find(l => l.id === id);
    if (!look) return;
    if (act.dataset.act === 'wear') { state = sanitize(merge(clone(DEFAULT), clone(look.state))); hairSig = ''; setView('suite'); update(true); toast(`Wearing “${look.name}”`); }
    if (act.dataset.act === 'dl') download(sanitize(merge(clone(DEFAULT), clone(look.state))), look.name);
    if (act.dataset.act === 'del') {
      if (!act.dataset.armed) { act.dataset.armed = '1'; act.textContent = 'Sure?'; setTimeout(() => { if (act.isConnected) { delete act.dataset.armed; act.textContent = '✕'; } }, 2500); return; }
      lookbook = lookbook.filter(l => l.id !== id); lsSet(LS_BOOK, lookbook); renderLookbook(); toast(`Deleted “${look.name}”`);
    }
  }
});
document.addEventListener('input', e => {
  const t = e.target;
  if (t.dataset.range) { setP(t.dataset.range, parseFloat(t.value)); update(); }
  else if (t.dataset.color) { setP(t.dataset.color, t.value); update(); }
  else if (t.hasAttribute('data-recolor-input')) { state.colors[state.focus] = t.value; UID = 0; renderSuite(); renderLayers(); }
});
document.addEventListener('change', e => { if (e.target.hasAttribute('data-recolor-input')) update(); });
$('#tuckToggle').addEventListener('change', e => { state.tucked = e.target.checked; update(); });

$('#tbUndress').addEventListener('click', () => { for (const k of Object.keys(state.outfit)) state.outfit[k] = null; update(true); toast(`${starletName()} is back in her underwear`); });
$('#tbRandom').addEventListener('click', randomize);
$('#tbLayers').addEventListener('click', () => { const p = $('#layersPop'); p.hidden = !p.hidden; $('#tbLayers').classList.toggle('on', !p.hidden); });
$('#tbUndo').addEventListener('click', undo);
$('#tbSave').addEventListener('click', openSave);
$('#btnSave').addEventListener('click', openSave);
$('#meBtn').addEventListener('click', openPicker);
$('#chatForm').addEventListener('submit', e => {
  e.preventDefault();
  const inp = $('#chatInput'), text = inp.value.trim().slice(0, 280);
  if (!text || typing) return;
  inp.value = '';
  if (chatSel === 'lounge') sendLounge(text); else sendDM(text);
});
$('#fitClear').addEventListener('click', () => { tryOn = []; renderShop(); });
$('#enterContest').addEventListener('click', enterContest);
$('#contestDress').addEventListener('click', () => { setView('suite'); if (drawer !== 'wardrobe') openDrawer('wardrobe'); });
$('#btnHelp').addEventListener('click', () => { setView('suite'); openGuide(0); });
$('#pickerGo').addEventListener('click', () => closePicker(true));
$('#guideNext').addEventListener('click', () => { if (guideStep < GUIDE.length - 1) { guideStep++; renderGuide(); } else closeGuide(); });
$('#guideBack').addEventListener('click', () => { if (guideStep > 0) { guideStep--; renderGuide(); } });
$('#guideSkip').addEventListener('click', closeGuide);
$('#cancelSave').addEventListener('click', closeSave);
$('#confirmSave').addEventListener('click', confirmSave);
$('#downloadNow').addEventListener('click', () => download(state, $('#lookName').value.trim() || 'My Look'));
$('#lookName').addEventListener('keydown', e => { if (e.key === 'Enter') confirmSave(); });
$('#saveModal').addEventListener('click', e => { if (e.target.id === 'saveModal') closeSave(); });
document.addEventListener('keydown', e => {
  if (e.key !== 'Escape') return;
  if ($('#saveModal').classList.contains('on')) closeSave();
  else if (!$('#picker').hidden && state.starlet) closePicker(false);
  else if (drawer) openDrawer(null);
});
let resizeT;
window.addEventListener('resize', () => { clearTimeout(resizeT); resizeT = setTimeout(() => renderSuite(), 120); });
document.addEventListener('fullscreenchange', () => setTimeout(() => renderSuite(), 50));

// ---------- drag & drop (pointer based) ----------
// Wardrobe card -> onto the doll to wear it; worn piece -> off the doll to remove it;
// in decorating mode, furniture and the starlet herself move around the room.
let drag = null, suppressClick = false;
const stageEl = $('#suiteStage');
const svgPt = (x, y) => { const svg = stageEl.querySelector('svg.suite-room'); const p = svg.createSVGPoint(); p.x = x; p.y = y; return p.matrixTransform(svg.getScreenCTM().inverse()); };
function overDoll(x, y) {
  const d = stageEl.querySelector('[data-doll] > svg'); if (!d) return false;
  const r = d.getBoundingClientRect();
  return x >= r.left + r.width * 0.15 && x <= r.right - r.width * 0.15 && y >= r.top && y <= r.bottom;
}
document.addEventListener('pointerdown', e => {
  if (e.button !== 0) return;
  if (decorMode) {
    const g = e.target.closest('#suiteStage [data-furn], #suiteStage [data-doll]');
    if (!g) return;
    const ref = g.hasAttribute('data-doll') ? state.room.doll : state.room.items.find(i => i.id === g.dataset.furn);
    if (!ref) return;
    const p = svgPt(e.clientX, e.clientY);
    const layer = g.hasAttribute('data-doll') ? 'doll' : FURN[ref.k].layer;
    drag = { kind: 'move', g, ref, layer, px: p.x, py: p.y, ox: ref.x, oy: ref.y, moved: false };
    e.preventDefault();
    return;
  }
  const card = e.target.closest('[data-item]');
  const piece = !card && e.target.closest('#suiteStage [data-slot]');
  if (!card && !piece) return;
  const id = card ? card.dataset.item : state.outfit[piece.dataset.slot];
  if (!id) return;
  drag = { kind: 'cloth', id, slot: piece ? piece.dataset.slot : null, x: e.clientX, y: e.clientY, started: false, ghost: null };
});
document.addEventListener('pointermove', e => {
  if (!drag) return;
  if (drag.kind === 'move') {
    const p = svgPt(e.clientX, e.clientY), r = drag.ref;
    const lim = { wall: [70, 360], rug: [FLOOR_Y + 40, ROOM_H - 20], floor: [FLOOR_Y + 24, ROOM_H - 4], doll: [FLOOR_Y + 34, ROOM_H - 6] }[drag.layer];
    r.x = clamp(drag.ox + p.x - drag.px, 40, 960);
    r.y = clamp(drag.oy + p.y - drag.py, lim[0], lim[1]);
    drag.moved = true;
    const sc = drag.layer === 'wall' ? 1 : depth(r.y);
    drag.g.setAttribute('transform', `translate(${r.x.toFixed(1)},${r.y.toFixed(1)}) scale(${sc.toFixed(3)})`);
    return;
  }
  if (!drag.started) {
    if (Math.hypot(e.clientX - drag.x, e.clientY - drag.y) < 6) return;
    drag.started = true;
    const g = document.createElement('div');
    g.className = 'drag-ghost';
    g.innerHTML = scope(itemThumb(ITEM[drag.id], colOf(drag.id)), 'G');
    document.body.appendChild(g);
    drag.ghost = g;
    document.body.classList.add('dragging');
    if (drag.slot) { state.outfit[drag.slot] = null; UID = 0; renderSuite(); }
  }
  e.preventDefault();
  drag.ghost.style.left = e.clientX + 'px';
  drag.ghost.style.top = e.clientY + 'px';
  const over = overDoll(e.clientX, e.clientY);
  stageEl.classList.toggle('drag-over', over && !drag.slot);
  drag.ghost.classList.toggle('over', over);
});
function endDrag(e, cancelled) {
  if (!drag) return;
  const d = drag; drag = null;
  if (d.kind === 'move') { if (d.moved) { suppressClick = true; setTimeout(() => { suppressClick = false; }, 0); update(); } return; }
  if (!d.started) return;
  d.ghost.remove();
  stageEl.classList.remove('drag-over');
  document.body.classList.remove('dragging');
  suppressClick = true; setTimeout(() => { suppressClick = false; }, 0);
  const it = ITEM[d.id], over = !cancelled && overDoll(e.clientX, e.clientY);
  if (d.slot) {
    if (over || cancelled) { state.outfit[d.slot] = d.id; update(); }
    else { update(true); toast(`Took off ${it.name}`); }
  } else if (over) {
    equip(d.id, true);
    toast(`${starletName()} is wearing ${it.name}`);
  }
}
document.addEventListener('pointerup', e => endDrag(e, false));
document.addEventListener('pointercancel', e => endDrag(e, true));
document.addEventListener('dragstart', e => { if (e.target.closest?.('[data-item], #suiteStage')) e.preventDefault(); });

// ---------- boot ----------
buildUI();
furnishForLife();
lastTint = tintKey();
update();
$('#promoShop').innerHTML = `<svg viewBox="0 0 300 130"><rect width="300" height="130" fill="#5cc8c0"/><circle cx="250" cy="20" r="60" fill="#7fd8d0"/>` +
  ['pumps', 'heartbag', 'heartsunnies'].map((id, i) => scope(itemThumb(ITEM[id], ITEM[id].color), 'PS' + id + '-').replace('<svg ', `<svg x="${18 + i * 92}" y="22" width="80" height="86" `)).join('') + `</svg>`;
$('#promoContest').innerHTML = `<svg viewBox="0 0 300 130"><defs><linearGradient id="pcg" x1="0" x2="1"><stop offset="0" stop-color="#f06aa8"/><stop offset="1" stop-color="#8f63f0"/></linearGradient></defs><rect width="300" height="130" fill="url(#pcg)"/>` +
  `<path d="M128,30 h44 v16 a22,22 0 0 1 -44,0 z" fill="#ffd84a" stroke="#c9961c" stroke-width="2"/><rect x="144" y="68" width="12" height="18" fill="#ffd84a"/><rect x="132" y="86" width="36" height="10" rx="2" fill="#c9961c"/>` +
  `<text x="40" y="58" font-size="30" fill="#fff" opacity=".8">★</text><text x="232" y="96" font-size="24" fill="#fff" opacity=".7">★</text><text x="70" y="108" font-size="18" fill="#fff" opacity=".6">★</text></svg>`;
lastSnap = JSON.stringify(state); undoStack = []; $('#tbUndo').disabled = true;
renderLookbook();
ensureGreetings(); renderChatBadge();
$('#promoEvents').innerHTML = `<svg viewBox="0 0 300 130"><rect width="300" height="130" fill="#140d24"/><path d="M80,0 L20,130 L120,130 Z" fill="#ff4f8b" opacity=".25"/><path d="M220,0 L170,130 L270,130 Z" fill="#5ee0c4" opacity=".25"/><circle cx="150" cy="44" r="26" fill="#c0c5d6"/><path d="M126,36 H174 M126,52 H174 M140,20 V68 M160,20 V68" stroke="#fff" stroke-width=".8" opacity=".7"/><path d="M150,0 V18" stroke="#888"/><text x="150" y="112" text-anchor="middle" font-family="Fraunces,Georgia,serif" font-style="italic" font-weight="700" font-size="26" fill="#ffd166">Tonight</text></svg>`;
// links like index.html#shop open that page directly (the 3D town uses these)
const hashView = location.hash.slice(1);
setView(['home', 'suite', 'parlor', 'shop', 'contests', 'events', 'chat', 'album'].includes(hashView) ? hashView : 'home');
if (!lsGet(LS_CUR, null) || !lsGet('starlife.guide.v1', false)) openPicker();
</script>
