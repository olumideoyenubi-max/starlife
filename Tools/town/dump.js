// Injected into a copy of world.html (inside its module, before the test hook) by export.js.
// window.__export() walks the finished town and returns everything Unity needs:
//   - the static scene merged per material and per 48 m cell, already in Unity's left-handed space (z flipped),
//   - every material and canvas texture,
//   - the gameplay data (places, seats, colliders, platforms, walking graph, lamps, shop and activity tables).
// Gameplay positions stay in the town's own coordinates (x, z, ry); Unity converts them when it places things.
window.__export = async () => {
  const skip = new Set([sky, beacon, ring, player.ch.group, ...people.map(p => p.ch.group)]);
  const buckets = new Map(), mats = new Map(), texs = new Map(), fountains = [];
  const texId = t => {
    if (!t) return null;
    if (!texs.has(t.uuid)) {
      const img = t.image; let url = null;
      if (img && img.toDataURL) url = img.toDataURL('image/png');
      else if (img && img.width) { const c = document.createElement('canvas'); c.width = img.width; c.height = img.height; c.getContext('2d').drawImage(img, 0, 0); url = c.toDataURL('image/png'); }
      texs.set(t.uuid, { id: 't' + texs.size, url, repeat: t.wrapS === THREE.RepeatWrapping, w: img && img.width, h: img && img.height });
    }
    return texs.get(t.uuid).id;
  };
  const hex = c => c ? '#' + c.getHexString() : null;
  const matId = (m, kind) => {
    const key = m.uuid + kind;
    if (!mats.has(key)) {
      const e = {
        id: 'm' + mats.size, name: m.name || m.type, type: m.type, kind,
        color: hex(m.color), map: texId(m.map), emissive: hex(m.emissive), emissiveMap: texId(m.emissiveMap), emissiveIntensity: m.emissiveIntensity ?? 1,
        roughness: m.roughness ?? 1, metalness: m.metalness ?? 0, opacity: m.opacity, transparent: !!m.transparent, alphaTest: m.alphaTest || 0,
        side: m.side, vertexColors: !!m.vertexColors, depthWrite: m.depthWrite !== false, offset: m.polygonOffset ? m.polygonOffsetFactor : 0,
        basic: m.type === 'MeshBasicMaterial' || m.type === 'LineBasicMaterial', blending: m.blending,
        night: nightMats.includes(m) ? m.userData.base : 0, dayGlow: dayGlowMats.includes(m) ? m.userData.base : 0, env: m.userData.env ?? 1,
      };
      mats.set(key, e);
    }
    return mats.get(key).id;
  };
  const CELLS = 48;
  const bucket = (mid, x, z) => {
    const key = mid + ':' + Math.round(x / CELLS) + ':' + Math.round(z / CELLS);
    if (!buckets.has(key)) buckets.set(key, { mat: mid, p: [], n: [], u: [], c: [], i: [], hasC: false });
    return buckets.get(key);
  };
  const v3 = new THREE.Vector3(), n3 = new THREE.Vector3(), nm = new THREE.Matrix3(), box = new THREE.Box3(), ctr = new THREE.Vector3();
  function addGeometry(geo, matrix, mat, color, start = 0, count = Infinity) {
    const mid = matId(mat, 'mesh');
    const pos = geo.attributes.position; if (!pos) return;
    if (!geo.attributes.normal) geo.computeVertexNormals();
    const nor = geo.attributes.normal, uv = geo.attributes.uv, col = mat.vertexColors ? geo.attributes.color : null;
    const idx = geo.index ? geo.index.array : null;
    const total = idx ? idx.length : pos.count, s = start, e = Math.min(total, start + count);
    if (e <= s) return;
    box.setFromBufferAttribute(pos).applyMatrix4(matrix); box.getCenter(ctr);
    const b = bucket(mid, ctr.x, ctr.z);
    nm.getNormalMatrix(matrix);
    const flip = matrix.determinant() < 0;
    const uvm = mat.map ? (mat.map.matrixAutoUpdate && mat.map.updateMatrix(), mat.map.matrix) : null;
    const base = b.p.length / 3, used = new Map();
    const vert = k => {
      if (used.has(k)) return used.get(k);
      v3.fromBufferAttribute(pos, k).applyMatrix4(matrix); b.p.push(v3.x, v3.y, -v3.z);
      n3.fromBufferAttribute(nor, k).applyMatrix3(nm).normalize(); b.n.push(n3.x, n3.y, -n3.z);
      if (uv) { let u = uv.getX(k), w = uv.getY(k); if (uvm) { const t = new THREE.Vector3(u, w, 1).applyMatrix3(uvm); u = t.x; w = t.y; } b.u.push(u, w); } else b.u.push(0, 0);
      if (col || color) { b.hasC = true; if (col) b.c.push(col.getX(k), col.getY(k), col.getZ(k)); else b.c.push(color.r, color.g, color.b); } else b.c.push(1, 1, 1);
      const id = b.p.length / 3 - 1; used.set(k, id); return id;
    };
    for (let t = s; t + 2 < e; t += 3) {
      const a = vert(idx ? idx[t] : t), c1 = vert(idx ? idx[t + 1] : t + 1), c2 = vert(idx ? idx[t + 2] : t + 2);
      // z is flipped, which mirrors the scene: reverse the winding (twice mirrored keeps it)
      if (flip) b.i.push(a, c1, c2); else b.i.push(a, c2, c1);
    }
  }
  // lines become thin square tubes
  function addLines(obj) {
    const pos = obj.geometry.attributes.position, pairs = [];
    const pts = [...Array(pos.count)].map((_, k) => new THREE.Vector3().fromBufferAttribute(pos, k).applyMatrix4(obj.matrixWorld));
    if (obj.isLineSegments) for (let k = 0; k + 1 < pts.length; k += 2) pairs.push([pts[k], pts[k + 1]]);
    else for (let k = 0; k + 1 < pts.length; k++) pairs.push([pts[k], pts[k + 1]]);
    const g = new THREE.BufferGeometry(), P = [];
    for (const [a, c] of pairs) {
      const d = c.clone().sub(a), len = d.length(); if (len < 1e-4) continue;
      const cyl = new THREE.CylinderGeometry(0.012, 0.012, len, 4, 1, true); cyl.translate(0, len / 2, 0);
      cyl.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), d.normalize())); cyl.translate(a.x, a.y, a.z);
      P.push(cyl);
    }
    if (!P.length) return;
    const merged = mergeGeometries(P.map(x => x.toNonIndexed()));
    const m = new THREE.MeshStandardMaterial({ color: obj.material.color, roughness: 0.6 }); m.uuid = obj.material.uuid;
    addGeometry(merged, new THREE.Matrix4(), m, null);
  }
  scene.updateMatrixWorld(true);
  let tris = 0;
  scene.traverseVisible(o => { o.userData.__skipped = false; });
  (function walk(o, skipped) {
    if (skip.has(o)) skipped = true;
    if (!skipped) {
      if (o.isPoints) fountains.push(new THREE.Vector3().setFromMatrixPosition(o.matrixWorld).toArray());
      else if (o.isSkinnedMesh || o.isSprite || o.isLight) { /* people, name tags, lights */ }
      else if (o.isLine) addLines(o);
      else if (o.isInstancedMesh) {
        const m4 = new THREE.Matrix4(), c = new THREE.Color();
        for (let k = 0; k < o.count; k++) { o.getMatrixAt(k, m4); m4.premultiply(o.matrixWorld); if (o.instanceColor) o.getColorAt(k, c); addGeometry(o.geometry, m4, o.material, o.instanceColor ? c.clone() : null); }
      } else if (o.isMesh && o.visible !== false) {
        if (Array.isArray(o.material)) for (const g of o.geometry.groups) addGeometry(o.geometry, o.matrixWorld, o.material[g.materialIndex], null, g.start, g.count);
        else addGeometry(o.geometry, o.matrixWorld, o.material, null);
      }
    }
    // mannequins are made like people (skinned); their groups are skipped whole
    for (const ch of o.children) walk(ch, skipped || (ch.isGroup && ch.children.some(x => x.isSkinnedMesh)));
  })(scene, false);

  // binary: per bucket, float32 positions, normals, uvs, colours, uint32 indices
  const meshes = [], chunks = []; let off = 0;
  for (const b of buckets.values()) {
    const P = new Float32Array(b.p), Nn = new Float32Array(b.n), Uu = new Float32Array(b.u), Cc = b.hasC ? new Float32Array(b.c) : null, I = new Uint32Array(b.i);
    const e = { mat: b.mat, verts: P.length / 3, tris: I.length / 3, off, colors: !!Cc };
    for (const a of [P, Nn, Uu, Cc, I]) if (a) { chunks.push(new Uint8Array(a.buffer)); off += a.byteLength; }
    meshes.push(e); tris += e.tris;
  }
  const bin = new Uint8Array(off); let o2 = 0; for (const c of chunks) { bin.set(c, o2); o2 += c.length; }
  let s = ''; for (let k = 0; k < bin.length; k += 0x8000) s += String.fromCharCode.apply(null, bin.subarray(k, k + 0x8000));
  const data = {
    CELL, HALF, SW, CURB, N, EDGE, AVE, MALL, LAND: Object.keys(LAND), PARKS,
    places: places.map(p => ({ id: p.id, name: p.name, sub: p.sub || '', x: p.x, z: p.z, ry: p.ry, color: p.color, open: p.open || null, node: p.node, mall: !!p.mall, street: !!p.street, gate: !!p.gate, hangout: !!p.hangout })),
    seats: seats.map(s => ({ x: s.x, z: s.z, ry: s.ry, y: s.y, place: s.place || '' })),
    colliders: colliders.map(c => [c.x0, c.z0, c.x1, c.z1, c.y1 ?? 40]), platforms: platforms.map(p => [p.x0, p.z0, p.x1, p.z1, p.y]),
    nodes: nodes.map(n => [n.x, n.z]), edges: edges.map(e => e.map(([v]) => v)),
    lamps: lampHeads.map(v => [v.x, v.y, v.z]), mannequins: mannequinSpots.map(m => [m[0], m[1], m[2], m[3], m[4] ?? 0.5]), mannequinLooks: MANNEQUIN_LOOKS,
    parked: (window.__parked || []).map(c => [c[0], c[1], c[2], c[3]]),
    fountains, home: (() => { const h = places.find(p => p.id === 'home'); return [h.x, h.z, h.ry]; })(),
    ACTS, BLURB, LINES, COFFEE, BRANDS, HAUNTS, STARLETS, SKY, SLOT, DECAY, NEEDS,
    meshes, materials: [...mats.values()], textures: [...texs.values()].map(t => ({ id: t.id, repeat: t.repeat, w: t.w, h: t.h })),
  };
  window.__out = { json: JSON.stringify(data), bin: btoa(s), tex: Object.fromEntries([...texs.values()].map(t => [t.id, t.url])) };
  return { meshes: meshes.length, materials: mats.size, textures: texs.size, tris, bytes: bin.length };
};
