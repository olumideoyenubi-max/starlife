// The 3D side of Starlife: the six starlet avatars (Resources/Starlets/<id>, built by SLBuild.Import3D), a real
// room with 3D furniture, and one camera that draws them into textures for the interface.
//
// The room keeps the drawn game's coordinates (1000 x 620, floor from y 444 at the back wall to 620 at the front),
// so furniture and the starlet stand exactly where the rules and the drag handling expect them: the camera is set
// up so that a room point (x, y) lands on the same spot of the picture as in the drawn room.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class SLStage3D {
    // picture geometry: 306 px per metre at the front edge, the horizon 57 px above the picture, camera 6 m back
    public const float RoomW = 1000, RoomH = 620, F = 1836f, CamH = 677f / 306f, D0 = 6f, HorizonY = -57f;
    const float WallD = F * CamH / (444f - HorizonY), WallZ = WallD - D0, Front = 180f, Fov = 14f;
    static readonly Vector3 Home = new Vector3(0, -500, 0);   // far from the interface camera
    public static readonly string[] Lineup6 = { "luz", "aria", "zuri", "mina", "noor", "ivy" };

    readonly Transform root, room, furnRoot, actors; readonly Camera cam; readonly Shader shader;
    readonly Dictionary<string, GameObject> furn = new Dictionary<string, GameObject>();
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    readonly Dictionary<string, RenderTexture> shots = new Dictionary<string, RenderTexture>();
    readonly Dictionary<string, Texture2D> floors = new Dictionary<string, Texture2D>(), art = new Dictionary<string, Texture2D>();
    Material wallMat, sideMat, floorMat; string wallHex, floorKind; RenderTexture live;
    public bool Ok { get; }

    public SLStage3D() {
        shader = Resources.Load<Shader>("StarletTint");
        root = new GameObject("Starlife 3D Stage").transform; root.position = Home;
        room = Child(root, "Room"); furnRoot = Child(room, "Furniture"); actors = Child(root, "Actors");
        cam = new GameObject("Stage Camera").AddComponent<Camera>(); cam.transform.SetParent(root, false);
        cam.enabled = false; cam.allowHDR = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.nearClipPlane = 1f; cam.farClipPlane = 40f;
        cam.cullingMask = ~(1 << SLTownWorld.Layer);      // Starlife Town lives on its own layer, with its own sun
        Shader.SetGlobalVector("_HairLight", -(Quaternion.Euler(40, 24, 0) * Vector3.forward));     // where the key light comes from, for hair highlights
        Lamp("Key", new Vector3(40, 24, 0), new Color(1f, 0.97f, 0.93f), 0.80f, true);
        Lamp("Fill", new Vector3(18, -55, 0), new Color(0.93f, 0.95f, 1f), 0.26f, false);
        Lamp("Rim", new Vector3(28, 160, 0), new Color(0.95f, 0.93f, 1f), 0.5f, false);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;      // light from above cooler and stronger than what bounces off the floor
        RenderSettings.ambientSkyColor = new Color(0.60f, 0.58f, 0.64f); RenderSettings.ambientEquatorColor = new Color(0.46f, 0.43f, 0.45f); RenderSettings.ambientGroundColor = new Color(0.33f, 0.29f, 0.28f);
        QualitySettings.shadowDistance = 24; QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        BuildShell(); LoadIndex();
        Ok = shader != null && Has("aria") && Prefab("Starlets/aria") != null;
        if (!Ok) Debug.LogWarning("Starlife: the 3D starlets are not in this build (run SLBuild.Import3D).");
    }
    static Transform Child(Transform p, string name) { var t = new GameObject(name).transform; t.SetParent(p, false); return t; }
    void Lamp(string name, Vector3 euler, Color c, float power, bool shadows) {
        var l = new GameObject(name).AddComponent<Light>(); l.transform.SetParent(root, false); l.transform.localEulerAngles = euler;
        l.type = LightType.Directional; l.cullingMask = ~(1 << SLTownWorld.Layer); l.color = c; l.intensity = power; l.shadows = shadows ? LightShadows.Soft : LightShadows.None; l.shadowStrength = 0.42f; l.shadowBias = 0.03f;
    }

    // ---------------------------------------------------------------- room coordinates
    /// <summary>A point of the drawn room's floor (y from 444 to 620) in stage space.</summary>
    public static Vector3 Floor(float x, float y) { float d = F * CamH / (y - HorizonY); return new Vector3((x - 500) * d / F, 0, d - D0); }
    /// <summary>A point of the drawn room's back wall in stage space.</summary>
    public static Vector3 Wall(float x, float y) => new Vector3((x - 500) * WallD / F, CamH - (y - HorizonY) * WallD / F, WallZ);

    // ---------------------------------------------------------------- materials and parts
    Material Mat(Color c, float gloss = 0.2f, Texture tex = null, float cutoff = 0) {
        string key = ColorUtility.ToHtmlStringRGBA(c) + gloss + (tex != null ? tex.name : "") + cutoff;
        if (mats.TryGetValue(key, out var m)) return m;
        m = new Material(shader); m.SetColor("_Tint", c); m.SetFloat("_Glossiness", gloss); m.SetFloat("_Cull", 0); m.SetFloat("_Cutoff", cutoff);
        if (tex != null) m.SetTexture("_MainTex", tex);
        return mats[key] = m;
    }
    static Color H(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    GameObject Prim(PrimitiveType type, Transform p, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null) {
        var go = GameObject.CreatePrimitive(type); var col = go.GetComponent<Collider>(); if (col != null) UnityEngine.Object.Destroy(col);
        go.transform.SetParent(p, false); go.transform.localPosition = pos; go.transform.localScale = scale; if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        go.GetComponent<Renderer>().sharedMaterial = m; return go;
    }
    GameObject Box(Transform p, Vector3 c, Vector3 size, Color col, float gloss = 0.2f, Vector3? euler = null) => Prim(PrimitiveType.Cube, p, c, size, Mat(col, gloss), euler);
    GameObject Cyl(Transform p, Vector3 c, float radius, float height, Color col, float gloss = 0.2f, Vector3? euler = null) => Prim(PrimitiveType.Cylinder, p, c, new Vector3(radius * 2, height / 2, radius * 2), Mat(col, gloss), euler);
    GameObject Ball(Transform p, Vector3 c, Vector3 size, Color col, float gloss = 0.2f, Vector3? euler = null) => Prim(PrimitiveType.Sphere, p, c, size, Mat(col, gloss), euler);
    GameObject Rod(Transform p, Vector3 a, Vector3 b, float radius, Color col, float gloss = 0.2f) {
        var go = Cyl(p, (a + b) / 2, radius, (b - a).magnitude, col, gloss); go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized); return go;
    }
    /// <summary>A turned shape (pot, lampshade, table leg): pairs of radius and height, bottom to top.</summary>
    GameObject Lathe(Transform p, Vector3 pos, Color col, float gloss, params float[] ry) {
        const int seg = 28; int rings = ry.Length / 2;
        var v = new Vector3[rings * (seg + 1)]; var tri = new List<int>();
        for (int i = 0; i < rings; i++) for (int s = 0; s <= seg; s++) { float a = s * Mathf.PI * 2 / seg; v[i * (seg + 1) + s] = new Vector3(Mathf.Cos(a) * ry[i * 2], ry[i * 2 + 1], Mathf.Sin(a) * ry[i * 2]); }
        for (int i = 0; i < rings - 1; i++) for (int s = 0; s < seg; s++) { int a = i * (seg + 1) + s, b = a + seg + 1; tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
        var mesh = new Mesh { vertices = v, triangles = tri.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("Lathe"); go.transform.SetParent(p, false); go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = Mat(col, gloss); return go;
    }

    // ---------------------------------------------------------------- the room
    void BuildShell() {
        floorMat = new Material(shader); floorMat.SetFloat("_Glossiness", 0.28f); floorMat.SetFloat("_Cull", 0);
        wallMat = new Material(shader); wallMat.SetFloat("_Glossiness", 0.05f); wallMat.SetFloat("_Cull", 0);
        sideMat = new Material(shader); sideMat.SetFloat("_Glossiness", 0.05f); sideMat.SetFloat("_Cull", 0);
        Prim(PrimitiveType.Cube, room, new Vector3(0, -0.05f, WallZ / 2 - 2), new Vector3(10, 0.1f, WallZ + 4), floorMat);
        Prim(PrimitiveType.Cube, room, new Vector3(0, 1.75f, WallZ + 0.05f), new Vector3(10, 3.5f, 0.1f), wallMat);
        Box(room, new Vector3(0, 0.06f, WallZ - 0.011f), new Vector3(10, 0.12f, 0.022f), Color.white, 0.35f);          // skirting board, with a rounded cap
        Cyl(room, new Vector3(0, 0.12f, WallZ - 0.011f), 0.011f, 10, Color.white, 0.35f, new Vector3(0, 0, 90));
        foreach (var m in new[] { wallMat, sideMat }) { m.SetTexture("_BumpMap", Resources.Load<Texture2D>("Room/wall_n"));   /* the plaster's relief only: its grey colour would dull the paint */ m.SetFloat("_Sheen", 0.15f); }
        wallMat.SetTextureScale("_MainTex", new Vector2(10 / 2.4f, 3.5f / 2.4f)); sideMat.SetTextureScale("_MainTex", new Vector2((WallZ + 4) / 2.4f, 3.5f / 2.4f));
        float sx = (968 - 500) * WallD / F;   // the corner on the right, where the drawn room has its darker side wall
        Prim(PrimitiveType.Cube, room, new Vector3(sx + 0.05f, 1.75f, WallZ / 2 - 2), new Vector3(0.1f, 3.5f, WallZ + 4), sideMat).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Box(room, new Vector3(sx - 0.011f, 0.06f, WallZ / 2 - 2), new Vector3(0.022f, 0.12f, WallZ + 4), Color.white, 0.35f);
    }
    Texture2D FloorTex(string kind) {
        if (floors.TryGetValue(kind, out var t)) return t;
        Color b = H(kind == "walnut" ? "#7f5639" : kind == "white" ? "#e6e1da" : kind == "rose" ? "#e2b8c2" : "#ddcdb9");
        const int n = 256; t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
        var rnd = new System.Random(7); var px = new Color[n * n];
        if (kind == "rose") {     // square tiles with pale grout
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { bool g = x % 64 < 2 || y % 64 < 2; float k = ((x / 64 + y / 64) % 2 == 0 ? 1.03f : 0.97f); px[y * n + x] = g ? Color.Lerp(b, Color.white, 0.55f) : b * k; }
        } else {                  // boards running across the room, with staggered joints and a little variation
            for (int row = 0; row < 8; row++) {
                int joint = rnd.Next(n); float tone = 0.94f + (float)rnd.NextDouble() * 0.12f, tone2 = 0.94f + (float)rnd.NextDouble() * 0.12f;
                for (int y = row * 32; y < row * 32 + 32; y++) for (int x = 0; x < n; x++) {
                    bool seam = y % 32 == 0 || Mathf.Abs(x - joint) < 1; float grain = 1 + Mathf.Sin((x * 0.11f + row * 3) + Mathf.Sin(y * 0.9f) * 0.6f) * 0.012f;
                    px[y * n + x] = seam ? b * 0.82f : b * ((x < joint ? tone : tone2) * grain);
                }
            }
        }
        for (int i = 0; i < px.Length; i++) px[i].a = 1;
        t.SetPixels(px); t.Apply(true); return floors[kind] = t;
    }
    Texture2D Art(string kind, Func<string, string> svg, int w, int h) {
        if (art.TryGetValue(kind, out var t)) return t;
        t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        t.LoadRawTextureData(SLArt.RenderNow(svg(kind), w, h)); t.Apply(false); t.name = "art-" + kind; return art[kind] = t;
    }

    /// <summary>Make the room match the game's room: colours, and one 3D piece per placed item.</summary>
    public void SetRoom(DRoom r, Dictionary<string, DFurn> kinds, Func<string, string> furnSvg) {
        if (r.wall != wallHex) { wallHex = r.wall; var c = H(r.wall); wallMat.SetColor("_Tint", c); sideMat.SetColor("_Tint", c * 0.82f); }
        if (r.floor != floorKind) {      // real floor materials (Poly Haven), with their normal maps; the old drawn pattern is the fallback
            floorKind = r.floor; string fk = r.floor == "walnut" ? "walnut" : r.floor == "white" ? "white" : r.floor == "rose" ? "rose" : "oak";
            var ft = Resources.Load<Texture2D>("Room/floor_" + fk); float tile = fk == "rose" ? 1.6f : 2.2f;
            floorMat.SetTexture("_MainTex", ft != null ? ft : FloorTex(r.floor)); floorMat.SetTexture("_BumpMap", Resources.Load<Texture2D>("Room/floor_" + fk + "_n"));
            floorMat.SetColor("_Tint", ft == null ? Color.white : fk == "rose" ? new Color(1.25f, 0.92f, 0.98f) : fk == "white" ? new Color(1.5f, 1.45f, 1.38f) : fk == "oak" ? new Color(1.25f, 1.15f, 1.0f) : new Color(1.05f, 0.95f, 0.9f));
            floorMat.SetFloat("_Glossiness", fk == "rose" ? 0.6f : 0.42f);
            floorMat.SetTextureScale("_MainTex", ft != null ? new Vector2(10 / tile, (WallZ + 4) / tile) : new Vector2(10 / 1.6f, (WallZ + 4) / 1.6f));
        }
        var seen = new HashSet<string>();
        foreach (var it in r.items) {
            if (!kinds.TryGetValue(it.k, out var f)) continue;
            string key = it.id + ":" + it.k; seen.Add(key);
            if (!furn.TryGetValue(key, out var go)) {
                go = new GameObject(it.k); go.transform.SetParent(furnRoot, false); furn[key] = go;
                if (f.layer == "wall") {      // framed pictures, the mirror and the window hang flat on the wall, drawn by the game's own art
                    float k = WallD / F; var tex = Art(it.k, furnSvg, Mathf.RoundToInt(f.box[2] * 2), Mathf.RoundToInt(f.box[3] * 2));
                    Vector3 mid = new Vector3((f.box[0] + f.box[2] / 2) * k, -(f.box[1] + f.box[3] / 2) * k, 0); float qw = f.box[2] * k, qh = f.box[3] * k;
                    Prim(PrimitiveType.Quad, go.transform, mid + new Vector3(0, 0, -0.012f), new Vector3(qw, qh, 1), Mat(Color.white, it.k == "mirror" ? 0.85f : 0.1f, tex, 0.5f));
                    // real depth round the drawn piece: where its picture is, a frame that stands out from the wall; a window also gets a sill and curtains
                    var px = tex.GetPixels32(); int tw = tex.width, th = tex.height, x0 = tw, x1 = 0, y0 = th, y1 = 0;
                    for (int y = 0; y < th; y += 2) for (int x = 0; x < tw; x += 2) if (px[y * tw + x].a > 128) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
                    if (x1 > x0 && it.k != "mirror") {
                        float l = mid.x - qw / 2 + qw * x0 / tw, rr = mid.x - qw / 2 + qw * x1 / tw, bt = mid.y - qh / 2 + qh * y0 / th, tp = mid.y - qh / 2 + qh * y1 / th, t2 = 0.035f, dp = 0.06f;
                        Color fc = it.k == "window" ? Color.white : it.k == "map" ? H("#5a4634") : H("#e9d8b4");
                        Box(go.transform, new Vector3((l + rr) / 2, tp, -dp / 2), new Vector3(rr - l + t2, t2, dp), fc, 0.35f); Box(go.transform, new Vector3((l + rr) / 2, bt, -dp / 2), new Vector3(rr - l + t2, t2, dp), fc, 0.35f);
                        Box(go.transform, new Vector3(l, (tp + bt) / 2, -dp / 2), new Vector3(t2, tp - bt, dp), fc, 0.35f); Box(go.transform, new Vector3(rr, (tp + bt) / 2, -dp / 2), new Vector3(t2, tp - bt, dp), fc, 0.35f);
                        if (it.k == "window") {
                            Box(go.transform, new Vector3((l + rr) / 2, bt - 0.02f, -0.08f), new Vector3(rr - l + 0.16f, 0.035f, 0.16f), Color.white, 0.4f);                       // sill
                            Box(go.transform, new Vector3((l + rr) / 2, (tp + bt) / 2, -0.03f), new Vector3(0.022f, tp - bt, 0.03f), Color.white, 0.35f);                          // glazing bars
                            Box(go.transform, new Vector3((l + rr) / 2, (tp + bt) / 2, -0.03f), new Vector3(rr - l, 0.022f, 0.03f), Color.white, 0.35f);
                            float top = tp + 0.14f, len = top - (bt - 0.28f); Color cloth = H("#fbeef3");
                            Rod(go.transform, new Vector3(l - 0.3f, top, -0.1f), new Vector3(rr + 0.3f, top, -0.1f), 0.012f, H("#d9b25a"), 0.7f);                                     // curtain rod
                            foreach (int sd in new[] { -1, 1 }) {
                                Ball(go.transform, new Vector3(sd < 0 ? l - 0.3f : rr + 0.3f, top, -0.1f), Vector3.one * 0.045f, H("#d9b25a"), 0.7f);
                                for (int i = 0; i < 7; i++) {      // a drawn-back curtain: folds of cloth, gathered a little towards the hem
                                    float cx = (sd < 0 ? l - 0.24f : rr + 0.24f) + sd * -0.05f * i * 0.6f, wob = (i % 2) * 0.02f;
                                    Rod(go.transform, new Vector3(cx, top - 0.01f, -0.09f - wob), new Vector3(cx + sd * 0.012f * (3 - i), top - len, -0.085f - wob), 0.03f, i % 2 == 0 ? cloth : cloth * 0.93f, 0.12f);
                                }
                            }
                        }
                    }
                } else if (Prefab("Furniture/" + it.k) != null) {    // a detailed model, where one has been brought in
                    var model = UnityEngine.Object.Instantiate(Prefab("Furniture/" + it.k), go.transform, false); model.transform.localRotation = Quaternion.Euler(0, Front + (it.k == "rocker" ? 28 : 0), 0);
                    if (it.k == "sidetable") {        // the table carries a lamp, and the lamp is lit
                        Lathe(go.transform, new Vector3(0, 0.62f, 0), H("#fbfaf8"), 0.5f, 0, 0, 0.06f, 0, 0.075f, 0.045f, 0.045f, 0.09f, 0.011f, 0.12f);
                        Cyl(go.transform, new Vector3(0, 0.80f, 0), 0.007f, 0.16f, H("#d9b25a"), 0.7f);
                        Lathe(go.transform, new Vector3(0, 0.83f, 0), H("#f06aa6"), 0.15f, 0.14f, 0, 0.07f, 0.19f);
                        Glow(go.transform, new Vector3(0, 0.9f, -0.05f));
                    }
                    if (it.k == "sofa" || it.k == "armchair") foreach (var rend in model.GetComponentsInChildren<Renderer>()) {   // the game's sofa is cream and its armchair lilac: dye the leather
                        var m = rend.material; m.SetFloat("_Recolor", 1); m.SetColor("_Tint", H(it.k == "sofa" ? "#f3ead9" : "#c8b6e2")); m.SetFloat("_Gain", 2.6f); m.SetFloat("_Lift", 0.18f);
                    }
                } else { BuildFurniture(go.transform, it.k, (f.box[2] - 60) / 306f); if (it.k == "floorlamp") Glow(go.transform, new Vector3(-0.17f, 0.95f, -0.05f)); }
            }
            go.transform.localPosition = f.layer == "wall" ? Wall(it.x, it.y) : Floor(it.x, it.y) + (f.layer == "rug" ? Vector3.zero : new Vector3(0, 0.001f, 0));
        }
        foreach (var key in new List<string>(furn.Keys)) if (!seen.Contains(key)) { UnityEngine.Object.Destroy(furn[key]); furn.Remove(key); }
    }

    /// <summary>The warm light of a lamp.</summary>
    void Glow(Transform p, Vector3 at) {
        var l = new GameObject("Lamp light").AddComponent<Light>(); l.transform.SetParent(p, false); l.transform.localPosition = at;
        l.type = LightType.Point; l.color = new Color(1f, 0.82f, 0.6f); l.intensity = 1.1f; l.range = 2.4f; l.shadows = LightShadows.None;
    }
    // Furniture is modelled here from simple solids; w is the piece's width in metres, the origin its footprint centre.
    void BuildFurniture(Transform t, string kind, float w) {
        Color cream = H("#f3ead9"), wood = H("#7a5a3c"), pink = H("#f06aa6"), gold = H("#d9b25a"), white = H("#fbfaf8"), dark = H("#262229");
        switch (kind) {
            case "sofa": case "armchair": {
                Color c = kind == "sofa" ? cream : H("#c8b6e2"), c2 = Color.Lerp(c, Color.white, 0.35f); float d = 0.5f;
                Box(t, new Vector3(0, 0.2f, 0), new Vector3(w, 0.2f, d), c);
                Box(t, new Vector3(0, 0.46f, d / 2 - 0.07f), new Vector3(w, 0.52f, 0.14f), c, 0.2f, new Vector3(-8, 0, 0));
                foreach (int s in new[] { -1, 1 }) Box(t, new Vector3(s * (w / 2 - 0.06f), 0.3f, -0.02f), new Vector3(0.12f, 0.4f, d - 0.04f), c);
                int n = kind == "sofa" ? 2 : 1; float cw = (w - 0.26f) / n;
                for (int i = 0; i < n; i++) {
                    float x = -((n - 1) * cw) / 2 + i * cw;
                    Box(t, new Vector3(x, 0.335f, -0.05f), new Vector3(cw - 0.015f, 0.08f, d - 0.16f), c2);
                    Box(t, new Vector3(x, 0.53f, d / 2 - 0.15f), new Vector3(cw - 0.015f, 0.3f, 0.07f), c2, 0.2f, new Vector3(-8, 0, 0));
                }
                if (kind == "sofa") Ball(t, new Vector3(-w / 2 + 0.25f, 0.47f, 0.02f), new Vector3(0.24f, 0.24f, 0.1f), pink, 0.2f, new Vector3(-20, 25, 10));
                foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) Cyl(t, new Vector3(sx * (w / 2 - 0.07f), 0.05f, sz * (d / 2 - 0.07f)), 0.022f, 0.1f, wood);
                break;
            }
            case "rocker": {
                Box(t, new Vector3(0, 0.36f, 0), new Vector3(w * 0.72f, 0.05f, 0.5f), pink, 0.25f, new Vector3(-12, 0, 0));
                Box(t, new Vector3(0, 0.74f, 0.27f), new Vector3(w * 0.72f, 0.72f, 0.05f), pink, 0.25f, new Vector3(-16, 0, 0));
                foreach (int s in new[] { -1, 1 }) {
                    float x = s * w * 0.4f;
                    Rod(t, new Vector3(x, 0.06f, -0.2f), new Vector3(x, 0.52f, -0.24f), 0.018f, wood); Rod(t, new Vector3(x, 0.06f, 0.2f), new Vector3(x, 0.5f, 0.24f), 0.018f, wood);
                    Rod(t, new Vector3(x, 0.52f, -0.26f), new Vector3(x, 0.52f, 0.28f), 0.02f, wood);
                    for (int i = 0; i < 8; i++) {   // the curved runner
                        float a0 = -0.42f + i * 0.105f, a1 = a0 + 0.105f, R = 1.5f;
                        Rod(t, new Vector3(x, R - Mathf.Cos(a0) * R + 0.02f, Mathf.Sin(a0) * R), new Vector3(x, R - Mathf.Cos(a1) * R + 0.02f, Mathf.Sin(a1) * R), 0.02f, wood);
                    }
                }
                break;
            }
            case "sidetable": {
                Lathe(t, Vector3.zero, white, 0.45f, 0, 0, 0.16f, 0, 0.16f, 0.015f, 0.03f, 0.04f, 0.022f, 0.46f, 0.05f, 0.48f, 0.23f, 0.485f, 0.23f, 0.51f, 0, 0.51f);
                Lathe(t, new Vector3(0, 0.51f, 0), white, 0.5f, 0, 0, 0.07f, 0, 0.085f, 0.05f, 0.05f, 0.1f, 0.012f, 0.13f);
                Cyl(t, new Vector3(0, 0.71f, 0), 0.008f, 0.18f, gold, 0.7f);
                Lathe(t, new Vector3(0, 0.74f, 0), pink, 0.15f, 0.15f, 0, 0.075f, 0.2f);
                break;
            }
            case "plant": {
                float top = 0.5f;
                for (int i = 0; i < 3; i++) { float a = i * 2.094f + 0.5f; Rod(t, new Vector3(Mathf.Cos(a) * 0.16f, 0, Mathf.Sin(a) * 0.16f), new Vector3(Mathf.Cos(a) * 0.09f, top, Mathf.Sin(a) * 0.09f), 0.009f, dark); }
                Lathe(t, new Vector3(0, top - 0.02f, 0), white, 0.5f, 0, 0, 0.09f, 0, 0.13f, 0.1f, 0.14f, 0.2f, 0.125f, 0.2f, 0.1f, 0.19f, 0, 0.19f);
                var green = H("#2f8a4a"); var rnd = new System.Random(3);
                for (int i = 0; i < 11; i++) {
                    float a = i * 2.4f, reach = 0.12f + (float)rnd.NextDouble() * 0.16f, rise = 0.2f + (float)rnd.NextDouble() * 0.36f, s = 0.15f + (float)rnd.NextDouble() * 0.08f;
                    var basePt = new Vector3(0, top + 0.17f, 0); var tip = basePt + new Vector3(Mathf.Cos(a) * reach, rise, Mathf.Sin(a) * reach);
                    Rod(t, basePt, tip, 0.005f, H("#3d7a45"));
                    Ball(t, tip + new Vector3(Mathf.Cos(a) * s * 0.4f, 0.01f, Mathf.Sin(a) * s * 0.4f), new Vector3(s, 0.012f, s * 0.86f), i % 3 == 0 ? H("#3a9a55") : green, 0.35f, new Vector3(-28 + (float)rnd.NextDouble() * 24, -a * Mathf.Rad2Deg, 18));
                }
                break;
            }
            case "coffeetable": {
                Box(t, new Vector3(0, 0.33f, 0), new Vector3(w, 0.025f, 0.5f), H("#cfe6f1"), 0.9f);
                Box(t, new Vector3(0, 0.12f, 0), new Vector3(w * 0.8f, 0.015f, 0.36f), gold, 0.7f);
                foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) Cyl(t, new Vector3(sx * (w / 2 - 0.05f), 0.16f, sz * 0.2f), 0.012f, 0.32f, gold, 0.7f);
                Lathe(t, new Vector3(w * 0.2f, 0.343f, 0.02f), H("#f4b8c8"), 0.5f, 0, 0, 0.03f, 0, 0.045f, 0.05f, 0.03f, 0.1f, 0.022f, 0.13f);
                Box(t, new Vector3(-w * 0.2f, 0.36f, -0.03f), new Vector3(0.2f, 0.03f, 0.14f), H("#a9c6ef"), 0.2f, new Vector3(0, 14, 0));
                break;
            }
            case "rug": {
                float rw = 500 / 306f * 0.92f, rd = 0.82f; var weave = new Material(shader); weave.SetColor("_Tint", H("#e9609a")); weave.SetFloat("_Cull", 0); weave.SetFloat("_Glossiness", 0.04f); weave.SetFloat("_Sheen", 0.5f);
                weave.SetTexture("_BumpMap", Resources.Load<Texture2D>("Room/rug_n")); weave.SetTextureScale("_MainTex", new Vector2(3, 1.6f));
                Prim(PrimitiveType.Cube, t, new Vector3(0, 0.007f, -rd * 0.25f), new Vector3(rw, 0.014f, rd), weave);
                Box(t, new Vector3(0, 0.0075f, -rd * 0.25f), new Vector3(rw - 0.16f, 0.0155f, rd - 0.16f), H("#f7a9c8"), 0.04f);           // a paler field inside the border
                Box(t, new Vector3(0, 0.008f, -rd * 0.25f), new Vector3(rw - 0.42f, 0.0165f, rd - 0.42f), H("#e9609a"), 0.04f);
                for (int i = 0; i < 46; i++) foreach (int sd in new[] { -1, 1 })                                                          // fringe at both ends
                    Box(t, new Vector3(sd * (rw / 2 + 0.03f), 0.004f, -rd * 0.25f - rd / 2 + rd * (i + 0.5f) / 46), new Vector3(0.06f, 0.004f, 0.006f), H("#fbeef3"), 0.05f, new Vector3(0, ((i * 7) % 5 - 2) * 4f, 0));
                break;
            }
            case "floorlamp": {
                Lathe(t, Vector3.zero, dark, 0.5f, 0, 0, 0.14f, 0, 0.14f, 0.02f, 0.02f, 0.04f);
                Rod(t, new Vector3(0, 0.03f, 0), new Vector3(0, 0.82f, 0), 0.011f, gold, 0.7f);
                Vector3 prev = new Vector3(0, 0.82f, 0);
                for (int i = 1; i <= 8; i++) { float a = i * 0.31f; var p = new Vector3(-0.17f + 0.17f * Mathf.Cos(a), 0.82f + 0.2f * Mathf.Sin(a), 0); Rod(t, prev, p, 0.011f, gold, 0.7f); prev = p; }
                Lathe(t, prev + new Vector3(0, -0.16f, 0), white, 0.3f, 0.12f, 0, 0.11f, 0.08f, 0.06f, 0.15f, 0.012f, 0.17f);
                break;
            }
            case "gift": {
                float s = Mathf.Min(w, 0.32f);
                Box(t, new Vector3(0, s * 0.42f, 0), new Vector3(s, s * 0.84f, s), H("#ff8fb3"), 0.3f);
                Box(t, new Vector3(0, s * 0.86f, 0), new Vector3(s * 1.06f, s * 0.14f, s * 1.06f), H("#ff7aa2"), 0.3f);
                Box(t, new Vector3(0, s * 0.47f, 0), new Vector3(s * 0.14f, s * 0.95f, s * 1.08f), white, 0.4f); Box(t, new Vector3(0, s * 0.47f, 0), new Vector3(s * 1.08f, s * 0.95f, s * 0.14f), white, 0.4f);
                foreach (int k in new[] { -1, 1 }) Ball(t, new Vector3(k * s * 0.16f, s * 1.0f, 0), new Vector3(s * 0.3f, s * 0.16f, s * 0.16f), white, 0.4f, new Vector3(0, 0, k * 25));
                break;
            }
            case "boxes": {
                Color card = H("#c9a06a"), tape = H("#e9d7b4");
                Box(t, new Vector3(-0.08f, 0.17f, 0), new Vector3(0.44f, 0.34f, 0.38f), card); Box(t, new Vector3(-0.08f, 0.342f, 0), new Vector3(0.07f, 0.004f, 0.38f), tape);
                Box(t, new Vector3(0.04f, 0.47f, 0.02f), new Vector3(0.32f, 0.26f, 0.3f), Color.Lerp(card, Color.white, 0.12f), 0.2f, new Vector3(0, 14, 0));
                Box(t, new Vector3(0.24f, 0.11f, -0.12f), new Vector3(0.26f, 0.22f, 0.26f), card * 0.94f, 0.2f, new Vector3(0, -18, 0));
                break;
            }
            default: Box(t, new Vector3(0, 0.2f, 0), new Vector3(w, 0.4f, 0.4f), cream); break;
        }
    }

    // ---------------------------------------------------------------- starlets, assembled from pieces
    // Every avatar is cut into pieces in one shared space (same neck joint), so a starlet is put together per look:
    // her own head, her hair or another, and either her own outfit or the wardrobe's pieces over a shared body.
    const string Base = "olivia";    // the body (and swimwear, used as underwear) worn under wardrobe pieces
    const string ManBase = "manbase"; // the men's shared body (make_men.py), in athletic shorts; all menswear is made on it
    static readonly HashSet<string> MenIds = new HashSet<string> { "kai", "leo", "jin", "theo", "omar", "manbase", "p_sam", "p_femi", "p_ken" };
    // MakeHuman's hair, beards and brows are pale strand textures meant to be tinted: each generated person's own colour
    static readonly Dictionary<string, string> GenHairHex = new Dictionary<string, string> {
        { "kai", "#1c1412" }, { "leo", "#3b2416" }, { "jin", "#14100e" }, { "theo", "#1c1412" }, { "omar", "#2a1a12" }, { "manbase", "#2a1a12" },
        { "p_ada", "#1c1412" }, { "p_grace", "#b9b3ad" }, { "p_mei", "#14100e" }, { "p_priya", "#1c1412" }, { "p_sam", "#6b3a1f" }, { "p_femi", "#3a3532" },
        { "p_ken", "#14100e" }, { "p_rosa", "#d8d4cf" } };
    static Color? GenHair(string id) => id != null && (GenHairHex.TryGetValue(id, out var h) || id.StartsWith("m_") && GenHairHex.TryGetValue("manbase", out h)) ? H(h) : (Color?)null;
    public static bool IsMale(string id) => id != null && (MenIds.Contains(id) || id.StartsWith("m_"));
    /// <summary>Avatars that are only there to lend clothes (and the town's passers-by): not offered as faces or outfits.</summary>
    public static bool IsHelper(string id) => id == "basics" || id == "goth" || id == ManBase || id.StartsWith("m_") || id.StartsWith("p_") || id.StartsWith("f_");
    static readonly Dictionary<string, Color> ToneOf = new Dictionary<string, Color> {   // skin tones of the one-piece avatars
        { "aria", new Color(0.93f, 0.76f, 0.66f) }, { "mina", new Color(0.90f, 0.73f, 0.63f) }, { "luz", new Color(0.70f, 0.50f, 0.39f) } };
    static readonly Dictionary<string, Color> HairDye = new Dictionary<string, Color> {  // hairstyles whose own texture is only a pale strand pattern
        { "bantu", new Color(0.09f, 0.07f, 0.06f) }, { "cornrows", new Color(0.10f, 0.08f, 0.07f) } };
    readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    readonly Dictionary<string, E3> entries = new Dictionary<string, E3>();
    readonly List<Transform> slots = new List<Transform>(); readonly List<string> slotKeys = new List<string>();
    public E3[] Starlets = new E3[0], Hair = new E3[0];

    void LoadIndex() {
        var txt = Resources.Load<TextAsset>("starlife3d"); if (txt == null) return;
        var ix = JsonUtility.FromJson<Index3>(txt.text); Starlets = ix.starlets ?? new E3[0]; Hair = ix.hair ?? new E3[0];
        foreach (var e in Starlets) entries["Starlets/" + e.id] = e;
        foreach (var e in Hair) entries["Hair/" + e.id] = e;
    }
    public bool Has(string id) => entries.ContainsKey("Starlets/" + id);
    GameObject Prefab(string path) { if (!prefabs.TryGetValue(path, out var g)) prefabs[path] = g = Resources.Load<GameObject>(path); return g; }
    P3 Info(string path, string part) { if (entries.TryGetValue(path, out var e)) foreach (var p in e.parts) if (p.mesh == part) return p; return null; }
    bool HasPart(string id, string part) => Info("Starlets/" + id, part) != null;
    Color Tone(string id) {
        var p = Info("Starlets/" + id, "body");
        if (p != null && p.tone != null && p.tone.Length >= 3) return new Color(p.tone[0], p.tone[1], p.tone[2]);
        return ToneOf.TryGetValue(id, out var c) ? c : new Color(0.9f, 0.74f, 0.64f);
    }
    /// <summary>Adds one piece of an avatar or hairstyle to an actor. dye: recolour it by brightness; tint: multiply it; inset: shrink it under clothes.</summary>
    void Add(Transform to, string path, string part, Color? dye = null, Color? tint = null, float inset = 0, bool flat = false, float shrink = 0, float zpush = 0, bool keepPrint = false) {
        var pf = Prefab(path); var src = pf != null ? pf.transform.Find(part) : null; if (src == null) return;
        var go = UnityEngine.Object.Instantiate(src.gameObject, to, false); go.name = path + "/" + part;
        if (!dye.HasValue && !tint.HasValue && inset == 0 && shrink == 0 && zpush == 0) return;
        var m = go.GetComponent<Renderer>().material; var info = Info(path, part);
        if (dye.HasValue) { m.SetFloat("_Recolor", 1); m.SetColor("_Tint", dye.Value); m.SetFloat("_Lift", 0); m.SetFloat("_Gain", Mathf.Min(1f / Mathf.Max(info != null && info.lum > 0 ? info.lum : 0.5f, 0.05f), 4f)); m.SetFloat("_SatMask", 0); if (flat) { m.SetFloat("_Lift", 1); m.SetFloat("_Gain", 0); m.SetFloat("_SSS", 0.32f); m.SetFloat("_Sheen", 0.22f); }   // flat dye is only used for skin fillers
            if (keepPrint && info != null && info.tone != null && info.tone.Length >= 3) { m.SetColor("_Mean", new Color(info.tone[0], info.tone[1], info.tone[2])); m.SetFloat("_Keep", 1); } }
        else if (tint.HasValue) m.SetColor("_Tint", m.GetColor("_Tint") * tint.Value);
        m.SetFloat("_Inset", inset); m.SetFloat("_Shrink", shrink); m.SetFloat("_ZPush", zpush);
    }
    void Compose(Transform t, Look3 L) {
        for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(t.GetChild(i).gameObject);   // at once: several looks can be drawn in one frame
        string me = "Starlets/" + (HasPart(L.id, "head") ? L.id : "aria"), id = HasPart(L.id, "head") ? L.id : "aria";
        Color? skinTint = L.skinTint == Color.white ? (Color?)null : L.skinTint; Color tone = Tone(id) * L.skinTint;
        Add(t, me, "head", null, skinTint); Add(t, me, "eyes"); Add(t, me, "teeth"); Add(t, me, "extra");
        Add(t, me, "brows", L.hairColor ?? GenHair(id)); Add(t, me, "lashes", new Color(0.08f, 0.06f, 0.05f)); Add(t, me, "beard", L.hairColor ?? GenHair(id));      // the generated men's (and passers-by's) brows, lashes and beards
        foreach (Transform c in t) if (c.name == me + "/head") {      // her face: marked for the makeup studio, and wearing whatever has been painted
            var hm = c.GetComponent<Renderer>().material; hm.SetFloat("_IsHead", 1);
            if (L.makeup != null) { hm.SetTexture("_MakeupTex", L.makeup); hm.SetFloat("_Makeup", 1); }
        }
        if (string.IsNullOrEmpty(L.hair)) Add(t, me, "hair", L.hairColor ?? GenHair(id));
        else if (L.hair.StartsWith("s:")) Add(t, "Starlets/" + L.hair.Substring(2), "hair", L.hairColor);
        else if (entries.TryGetValue("Hair/" + L.hair.Substring(2), out var he)) {
            Color? dye = L.hairColor ?? (HairDye.TryGetValue(he.id, out var hd) ? hd : (Color?)null);
            foreach (var p in he.parts) Add(t, "Hair/" + he.id, p.mesh, p.material != null && p.material.StartsWith("Bead") ? (Color?)null : dye);   // beads and bands keep their gold
        }

        bool male = IsMale(id);
        var plan = SLWardrobe3D.Plan(L.worn, HasPart, male); bool clothed = plan.Exists(g => SLWardrobe3D.IsClothing(g.slot));
        Color? Dye(SLWardrobe3D.Piece g) { if (g.part == "fishnets") return new Color(0.07f, 0.06f, 0.07f);   /* tights are always black */ var info = Info("Starlets/" + g.donor, g.part); return g.keep || (info != null && info.material != null && info.material.StartsWith("Bead")) ? (Color?)null : g.color; }   // metal keeps its own colour
        if (!clothed && L.signature) {    // nothing from the wardrobe: a signature outfit, exactly as an avatar came (her own, or the one chosen for her)
            string from = L.body != null && HasPart(L.body, "top") || L.body != null && HasPart(L.body, "suit") ? L.body : id, f = "Starlets/" + from;
            foreach (var part in new[] { "body", "feet" }) { if (from == id) Add(t, f, part, null, skinTint); else Add(t, f, part, tone); }
            foreach (var part in new[] { "top", "bottom", "shoes", "suit" }) Add(t, f, part);
            foreach (var g in plan) Add(t, "Starlets/" + g.donor, g.part, Dye(g));      // accessories over her own outfit
            return;
        }
        if (male && HasPart(ManBase, "body")) {     // a man on the men's shared body: every piece of menswear was made on it, so all of it fits
            string mb = "Starlets/" + ManBase; bool topM = plan.Exists(g => g.slot == "top" || g.slot == "dress" || g.slot == "outer"), botM = plan.Exists(g => g.slot == "bottom" || g.slot == "dress");
            Add(t, mb, "body", tone, null, 0.003f, true, 0, 24000);
            if (!botM) Add(t, mb, "bottom", L.underwear);
            foreach (var g in plan) Add(t, "Starlets/" + g.donor, g.part, Dye(g), null, 0, false, 0, 0, SLWardrobe3D.IsClothing(g.slot) && g.slot != "shoes");      // made on one body, already layered
            return;
        }
        // Otherwise she is built on a shared body. The avatars' bodies have no skin where their own clothes were, so the
        // chest and hips are filled with skin-coloured pieces drawn in a little, and her underwear goes over those.
        string b = "Starlets/" + Base, u = "Starlets/basics"; bool shoes = false;
        bool topOn = plan.Exists(g => g.slot == "top" || g.slot == "dress" || g.slot == "outer"), bottomOn = plan.Exists(g => g.slot == "bottom" || g.slot == "dress");
        Color under = L.underwear ?? new Color(0.96f, 0.78f, 0.83f);
        // Skin always steps back a little in depth (zpush), so cloth lying close to it covers it from every side, front or back.
        const float Skin = 24000, Hidden = 60000;
        Add(t, b, "body", tone, null, 0.004f, true, 0, Skin);       // one even skin colour on the body and on the pieces that fill it in, so their joins cannot be seen
        Add(t, u, "torso", tone, null, 0.008f, true, 0, topOn ? Hidden : Skin + 14000);     // well behind the body itself, so it only fills what the body lacks                               // chest, back and waist, from several avatars' own skin
        Add(t, u, "hips", tone, null, 0.007f, true, 0, bottomOn ? Hidden : Skin);
        // her underwear is always on; under clothes it steps well back, so it only shows where nothing covers it
        string bra = L.bra == "bandeau" ? "bra" : L.bra == "tank" ? "tank" : null;
        // the refitted garments (f_*) have real necklines, halters and plunges: no underwear top under them
        bool realTop = plan.Exists(g => g.donor.StartsWith("f_") && (g.slot == "top" || g.slot == "dress" || g.slot == "outer"));
        if (realTop) { }
        else if (bra != null && !topOn) Add(t, u, bra, under); else Add(t, b, "top", under, null, 0, false, 0, topOn ? Hidden : 0);
        Add(t, u, L.pants == "shorts" && !bottomOn ? "shorts" : "briefs", under, null, bottomOn ? 0.006f : 0, false, 0, bottomOn ? Hidden : 0);
        foreach (var g in plan) {
            // a top or jacket hangs over a bottom, so where the two lie together the bottom gives way
            Add(t, "Starlets/" + g.donor, g.part, Dye(g), null, 0, false, 0, g.part == "fishnets" ? 34000 : g.slot == "bottom" && topOn && g.donor != "goth" ? 20000 : 0, SLWardrobe3D.IsClothing(g.slot) && g.slot != "shoes" && !(g.donor == "lina" && g.part == "bottom"));   // that skirt's two-colour print dyes badly: one colour   // tights sit under the skirt   // shoes and frames take the dye whole; clothes keep their trims
            if (g.part == "shoes") { shoes = true; Add(t, "Starlets/" + g.donor, "feet", tone, null, 0, true, 0, Skin); }
        }
        if (!shoes) Add(t, b, "feet", tone, null, 0, true);
    }
    /// <summary>set: 0 = no backdrop, 1 = the suite, 2 = the salon.</summary>
    void Show(int set, params (Look3 look, Vector3 pos, float yaw)[] who) {
        room.gameObject.SetActive(set == 1); if (salon != null) salon.gameObject.SetActive(set == 2); if (gym != null) gym.gameObject.SetActive(set == 3);
        while (slots.Count < who.Length) { var t = Child(actors, "Actor " + slots.Count); slots.Add(t); slotKeys.Add(null); }
        for (int i = 0; i < slots.Count; i++) {
            bool on = i < who.Length; slots[i].gameObject.SetActive(on); if (!on) continue;
            string key = who[i].look.Key;
            if (slotKeys[i] != key) {
                slotKeys[i] = key; Compose(slots[i], who[i].look);
                // tell every piece where it sits in her space and what build she has; the shader reshapes body and clothes alike
                var lk0 = who[i].look; var sh = lk0.shape; var block = new MaterialPropertyBlock(); var inv = slots[i].worldToLocalMatrix;
                foreach (var r in slots[i].GetComponentsInChildren<Renderer>()) {
                    var m = inv * r.localToWorldMatrix; r.GetPropertyBlock(block);
                    block.SetMatrix("_ObjToActor", m); block.SetMatrix("_ActorToObj", m.inverse); block.SetFloat("_Shaped", 1);
                    block.SetVector("_Shape", new Vector4(sh[0], sh[1], sh[2], sh[3])); block.SetVector("_Shape2", new Vector4(sh[4], sh[5], sh[6], sh[7])); r.SetPropertyBlock(block);
                }
            }
            slots[i].localPosition = who[i].pos; slots[i].localRotation = Quaternion.Euler(0, Front + who[i].yaw, 0);
            var lk = who[i].look; slots[i].localScale = new Vector3(lk.build * lk.height, lk.height, lk.build * lk.height);
        }
    }

    /// <summary>A starlet built on her own, for Starlife Town: a new transform under parent holding her pieces, shaped and
    /// sized like the suite's figures. SLRig.Attach gives her a skeleton.</summary>
    public Transform Figure(Look3 look, Transform parent, string name) {
        var t = Child(parent, name); Compose(t, look);
        var sh = look.shape; var block = new MaterialPropertyBlock(); var inv = t.worldToLocalMatrix;
        foreach (var r in t.GetComponentsInChildren<Renderer>()) {
            var m = inv * r.localToWorldMatrix; r.GetPropertyBlock(block);
            block.SetMatrix("_ObjToActor", m); block.SetMatrix("_ActorToObj", m.inverse); block.SetFloat("_Shaped", 1);
            block.SetVector("_Shape", new Vector4(sh[0], sh[1], sh[2], sh[3])); block.SetVector("_Shape2", new Vector4(sh[4], sh[5], sh[6], sh[7])); r.SetPropertyBlock(block);
        }
        t.localScale = new Vector3(look.build * look.height, look.height, look.build * look.height);
        return t;
    }

    // ---------------------------------------------------------------- cameras and pictures
    // pictures are drawn larger than they are shown (as far as 2048 allows) and smoothed down, for clean edges and fine detail
    static RenderTexture NewRT(int w, int h) {
        float ss = Mathf.Min(1.6f, 2048f / Mathf.Max(w, h, 1)); if (ss < 1) ss = 1;
        return new RenderTexture(Mathf.Clamp(Mathf.RoundToInt(w * ss), 16, 2048), Mathf.Clamp(Mathf.RoundToInt(h * ss), 16, 2048), 24, RenderTextureFormat.ARGB32) { antiAliasing = 8, filterMode = FilterMode.Trilinear, useMipMap = true, autoGenerateMips = true };
    }
    void RoomCamera(RenderTexture rt) {
        cam.transform.localPosition = new Vector3(0, CamH, -D0); cam.transform.localRotation = Quaternion.identity; cam.nearClipPlane = 1f; cam.farClipPlane = 40f;
        float n = cam.nearClipPlane;   // looking level, with the picture shifted below the horizon, as the drawn room is
        cam.projectionMatrix = Matrix4x4.Frustum(-500f / F * n, 500f / F * n, (HorizonY - RoomH) / F * n, HorizonY / F * n, n, cam.farClipPlane);
        cam.backgroundColor = wallHex != null ? H(wallHex) : Color.white; cam.targetTexture = rt; cam.Render();
    }
    /// <summary>Looks at the box (centre, size) of stage space, fitted inside the picture, or covering it.</summary>
    void BoxCamera(RenderTexture rt, Vector2 centre, Vector2 size, bool cover, Color? bg = null, Shader repl = null) {
        float aspect = (float)rt.width / rt.height, box = size.x / size.y;
        float vh = (aspect > box) != cover ? size.y : size.x / aspect, dist = vh / (2 * Mathf.Tan(Fov * Mathf.Deg2Rad / 2));
        cam.ResetProjectionMatrix(); cam.fieldOfView = Fov; cam.aspect = aspect; cam.nearClipPlane = Mathf.Max(0.3f, dist - 4); cam.farClipPlane = dist + 8;
        cam.transform.localPosition = new Vector3(centre.x, centre.y, -dist); cam.transform.localRotation = Quaternion.identity;
        cam.backgroundColor = bg ?? new Color(0.62f, 0.52f, 0.50f, 0); cam.targetTexture = rt; if (repl != null) cam.RenderWithShader(repl, ""); else cam.Render();
    }
    // stills are kept and reused; the oldest are dropped once there are many
    readonly Queue<string> shotOrder = new Queue<string>();
    RenderTexture Keep(string key, RenderTexture rt) {
        shots[key] = rt; shotOrder.Enqueue(key);
        while (shotOrder.Count > 140) { string old = shotOrder.Dequeue(); if (shots.TryGetValue(old, out var o)) { shots.Remove(old); o.Release(); UnityEngine.Object.Destroy(o); } }
        return rt;
    }

    /// <summary>The room with one starlet in it, drawn again on every call (My Suite).</summary>
    public RenderTexture Live(int w, int h, DRoom r, Dictionary<string, DFurn> kinds, Func<string, string> furnSvg, Look3 look, float yaw) {
        w = Mathf.Clamp(w, 16, 2048); h = Mathf.Clamp(h, 16, 2048);
        if (live == null || liveSize.x != w || liveSize.y != h) { liveSize = new Vector2Int(w, h); if (live != null) { live.Release(); UnityEngine.Object.Destroy(live); } live = NewRT(w, h); }
        SetRoom(r, kinds, furnSvg); Show(1, (look, Floor(r.doll.x, r.doll.y), yaw)); RoomCamera(live); return live;
    }
    // ---------------------------------------------------------------- the makeup studio
    readonly Dictionary<string, Texture2D> makeups = new Dictionary<string, Texture2D>(); RenderTexture liveFace; Vector2Int liveFaceSize; Shader uvShader;
    public static string MakeupFile(string key) => Path.Combine(Application.persistentDataPath, "makeup-" + key + ".png");
    /// <summary>The makeup layer of one starlet's face (a transparent picture laid over her skin texture). Null if she has none and create is false.</summary>
    public Texture2D Makeup(string key, bool create) {
        if (makeups.TryGetValue(key, out var t) && t != null) return t;
        string f = MakeupFile(key);
        if (key.StartsWith("shots-")) { if (!create) return null; }                 // the screenshot tour always starts with a clean face
        else if (File.Exists(f)) { t = new Texture2D(2, 2, TextureFormat.RGBA32, false); t.LoadImage(File.ReadAllBytes(f)); }
        if (t == null) { if (!create) return null; t = new Texture2D(1024, 1024, TextureFormat.RGBA32, false); t.SetPixels32(new Color32[1024 * 1024]); t.Apply(false); }
        t.wrapMode = TextureWrapMode.Clamp; return makeups[key] = t;
    }
    static readonly Vector2 FaceAt = new Vector2(0, 1.555f), FaceSize = new Vector2(0.44f, 0.56f);
    /// <summary>Her face in the mirror, drawn again on every call.</summary>
    public RenderTexture FaceLive(int w, int h, Look3 look) {
        w = Mathf.Clamp(w, 16, 2048); h = Mathf.Clamp(h, 16, 2048);
        if (liveFace == null || liveFaceSize.x != w || liveFaceSize.y != h) { liveFaceSize = new Vector2Int(w, h); if (liveFace != null) { liveFace.Release(); UnityEngine.Object.Destroy(liveFace); } liveFace = NewRT(w, h); }
        Show(0, (look, Vector3.zero, 0)); BoxCamera(liveFace, FaceAt, FaceSize, false, H("#f8d3df")); return liveFace;
    }
    /// <summary>The same view as FaceLive, but each pixel says where on her skin texture it is (r, g) and whether it is her head (b).</summary>
    public Color[] FaceUV(int w, int h, Look3 look) {
        if (uvShader == null) uvShader = Resources.Load<Shader>("StarlifeUV");
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBFloat) { antiAliasing = 1, filterMode = FilterMode.Point };
        Show(0, (look, Vector3.zero, 0)); BoxCamera(rt, FaceAt, FaceSize, false, new Color(0, 0, 0, 0), uvShader);
        var keep = RenderTexture.active; RenderTexture.active = rt; var t = new Texture2D(w, h, TextureFormat.RGBAFloat, false); t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply(false);
        RenderTexture.active = keep; var px = t.GetPixels(); UnityEngine.Object.Destroy(t); cam.targetTexture = null; rt.Release(); UnityEngine.Object.Destroy(rt); return px;
    }

    // ---------------------------------------------------------------- the hair salon
    Transform salon; RenderTexture liveSalon; Vector2Int liveSize, liveSalonSize, liveGymSize;   // the sizes asked for (the textures themselves are larger)
    void BuildSalon() {
        salon = Child(root, "Salon"); var t = salon;
        Color lilac = H("#ead9f7"), pink = H("#e8478c"), gold = H("#d9b25a"), white = H("#fbfaf8"), chrome = H("#c9ccd6"), blush = H("#f6c9d8");
        const int n = 128; var tile = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };   // checkerboard floor
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) tile.SetPixel(x, y, x % 64 < 1 || y % 64 < 1 ? H("#d9c7e6") : ((x / 64 + y / 64) % 2 == 0 ? H("#fbf3fa") : H("#f3c3d6")));
        tile.Apply(true);
        var floor = new Material(shader); floor.SetTexture("_MainTex", tile); floor.SetTextureScale("_MainTex", new Vector2(8, 8)); floor.SetFloat("_Glossiness", 0.45f); floor.SetFloat("_Cull", 0);
        Prim(PrimitiveType.Cube, t, new Vector3(0, -0.05f, 0), new Vector3(9, 0.1f, 9), floor);
        Box(t, new Vector3(0, 1.8f, 1.55f), new Vector3(9, 3.6f, 0.1f), lilac, 0.05f);
        Box(t, new Vector3(0, 0.04f, 1.49f), new Vector3(9, 0.08f, 0.03f), white, 0.3f);
        Box(t, new Vector3(0, 2.25f, 1.49f), new Vector3(9, 0.05f, 0.02f), gold, 0.6f);
        foreach (int side in new[] { -1, 1 }) {   // a lit mirror and a counter with bottles on each side of where she stands
            float x = side * 0.74f;
            Cyl(t, new Vector3(x, 1.52f, 1.485f), 0.36f, 0.02f, gold, 0.7f, new Vector3(90, 0, 0));
            Cyl(t, new Vector3(x, 1.52f, 1.47f), 0.32f, 0.02f, H("#d5ecf6"), 0.95f, new Vector3(90, 0, 0));
            for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6; Ball(t, new Vector3(x + Mathf.Cos(a) * 0.41f, 1.52f + Mathf.Sin(a) * 0.41f, 1.46f), Vector3.one * 0.055f, H("#fff6cf"), 0.9f); }
            Box(t, new Vector3(x, 0.42f, 1.30f), new Vector3(0.80f, 0.84f, 0.36f), blush, 0.3f);
            Box(t, new Vector3(x, 0.86f, 1.28f), new Vector3(0.88f, 0.045f, 0.44f), white, 0.6f);
            foreach (int k in new[] { -1, 1 }) Ball(t, new Vector3(x + k * 0.2f, 0.52f, 1.115f), Vector3.one * 0.035f, gold, 0.7f);
            string[] cols = { "#7c4ee0", "#3fcf8e", "#f5b82e", "#e8478c", "#4fb6e8" };
            for (int i = 0; i < 5; i++) Lathe(t, new Vector3(x - 0.30f + i * 0.15f, 0.883f, 1.30f - (i % 2) * 0.07f), H(cols[(i + (side > 0 ? 2 : 0)) % 5]), 0.6f, 0, 0, 0.032f, 0, 0.036f, 0.07f + (i % 3) * 0.02f, 0.014f, 0.10f + (i % 3) * 0.02f, 0.014f, 0.13f + (i % 3) * 0.02f, 0, 0.13f + (i % 3) * 0.02f);
        }
        // the styling chair, turned a little towards her
        var chair = Child(t, "Chair"); chair.localPosition = new Vector3(1.32f, 0, 0.45f); chair.localRotation = Quaternion.Euler(0, 205, 0);
        Lathe(chair, Vector3.zero, chrome, 0.8f, 0, 0, 0.29f, 0, 0.29f, 0.02f, 0.05f, 0.05f, 0.045f, 0.42f, 0.09f, 0.44f, 0, 0.44f);
        Box(chair, new Vector3(0, 0.50f, 0), new Vector3(0.52f, 0.11f, 0.5f), pink, 0.35f);
        Box(chair, new Vector3(0, 0.84f, 0.235f), new Vector3(0.5f, 0.62f, 0.09f), pink, 0.35f, new Vector3(-9, 0, 0));
        Box(chair, new Vector3(0, 1.20f, 0.275f), new Vector3(0.3f, 0.14f, 0.1f), pink, 0.35f, new Vector3(-9, 0, 0));
        foreach (int k in new[] { -1, 1 }) { Box(chair, new Vector3(k * 0.28f, 0.67f, 0.02f), new Vector3(0.05f, 0.05f, 0.42f), chrome, 0.8f); Rod(chair, new Vector3(k * 0.28f, 0.52f, -0.16f), new Vector3(k * 0.28f, 0.67f, -0.16f), 0.018f, chrome, 0.8f); }
        Rod(chair, new Vector3(-0.2f, 0.2f, -0.36f), new Vector3(0.2f, 0.2f, -0.36f), 0.02f, chrome, 0.8f);
        foreach (int k in new[] { -1, 1 }) Rod(chair, new Vector3(k * 0.2f, 0.2f, -0.36f), new Vector3(k * 0.12f, 0.45f, -0.2f), 0.014f, chrome, 0.8f);
        // a trolley with a dryer, on the other side
        var cart = Child(t, "Trolley"); cart.localPosition = new Vector3(-1.32f, 0, 0.5f);
        foreach (float y in new[] { 0.12f, 0.45f, 0.78f }) Box(cart, new Vector3(0, y, 0), new Vector3(0.44f, 0.025f, 0.34f), white, 0.5f);
        foreach (int kx in new[] { -1, 1 }) foreach (int kz in new[] { -1, 1 }) { Rod(cart, new Vector3(kx * 0.2f, 0.04f, kz * 0.15f), new Vector3(kx * 0.2f, 0.86f, kz * 0.15f), 0.012f, gold, 0.7f); Ball(cart, new Vector3(kx * 0.2f, 0.03f, kz * 0.15f), Vector3.one * 0.06f, H("#262229"), 0.4f); }
        Rod(cart, new Vector3(-0.1f, 0.83f, 0), new Vector3(0.1f, 0.83f, 0.02f), 0.045f, pink, 0.5f); Rod(cart, new Vector3(-0.03f, 0.80f, 0.01f), new Vector3(-0.07f, 0.795f, 0.14f), 0.022f, H("#262229"), 0.4f);
        foreach (int i in new[] { 0, 1, 2 }) Lathe(cart, new Vector3(-0.13f + i * 0.13f, 0.465f, 0), H(i == 1 ? "#f5b82e" : "#7c4ee0"), 0.6f, 0, 0, 0.04f, 0, 0.04f, 0.11f, 0.02f, 0.14f, 0, 0.14f);
        Box(cart, new Vector3(0, 0.16f, 0), new Vector3(0.3f, 0.06f, 0.24f), blush, 0.1f);
        if (Prefab("Furniture/plant") != null) { var pl = UnityEngine.Object.Instantiate(Prefab("Furniture/plant"), t, false); pl.transform.localPosition = new Vector3(2.05f, 0, 1.05f); pl.transform.localRotation = Quaternion.Euler(0, Front, 0); pl.transform.localScale *= 1.5f; }
    }
    /// <summary>The starlet in the hair salon, from the knees up, drawn again on every call.</summary>
    public RenderTexture Salon(int w, int h, Look3 look, float yaw) {
        w = Mathf.Clamp(w, 16, 2048); h = Mathf.Clamp(h, 16, 2048);
        if (liveSalon == null || liveSalonSize.x != w || liveSalonSize.y != h) { liveSalonSize = new Vector2Int(w, h); if (liveSalon != null) { liveSalon.Release(); UnityEngine.Object.Destroy(liveSalon); } liveSalon = NewRT(w, h); }
        if (salon == null) BuildSalon();
        Show(2, (look, Vector3.zero, yaw)); BoxCamera(liveSalon, new Vector2(0, 1.24f), new Vector2(1.6f, 1.5f), true, H("#ead9f7")); return liveSalon;
    }

    // ---------------------------------------------------------------- the gym
    Transform gym; RenderTexture liveGym;
    void BuildGym() {
        gym = Child(root, "Gym"); var t = gym;
        Color mint = H("#d7efe6"), dark = H("#2b2f38"), steel = H("#b9bec9"), red = H("#e8473f"), yellow = H("#f5b82e"), purple = H("#7c4ee0"), white = H("#fbfaf8");
        const int n = 128; var mat = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };   // rubber floor tiles
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) mat.SetPixel(x, y, x % 64 < 2 || y % 64 < 2 ? H("#3c4250") : (((x * 7 + y * 13) % 11 == 0) ? H("#566070") : H("#4a5160")));
        mat.Apply(true);
        var floor = new Material(shader); floor.SetTexture("_MainTex", mat); floor.SetTextureScale("_MainTex", new Vector2(9, 9)); floor.SetFloat("_Glossiness", 0.2f); floor.SetFloat("_Cull", 0);
        Prim(PrimitiveType.Cube, t, new Vector3(0, -0.05f, 0), new Vector3(10, 0.1f, 10), floor);
        Box(t, new Vector3(0, 1.8f, 1.9f), new Vector3(10, 3.6f, 0.1f), mint, 0.05f);
        Box(t, new Vector3(0, 1.25f, 1.84f), new Vector3(3.4f, 1.9f, 0.02f), H("#cfe6f1"), 0.95f); Box(t, new Vector3(0, 1.25f, 1.85f), new Vector3(3.5f, 2.0f, 0.02f), steel, 0.6f);
        Box(t, new Vector3(0, 2.5f, 1.84f), new Vector3(10, 0.12f, 0.02f), yellow, 0.3f); Box(t, new Vector3(0, 0.06f, 1.84f), new Vector3(10, 0.12f, 0.03f), dark, 0.3f);
        // treadmill
        var tm = Child(t, "Treadmill"); tm.localPosition = new Vector3(-1.0f, 0, 0.95f); tm.localRotation = Quaternion.Euler(0, 160, 0);
        Box(tm, new Vector3(0, 0.09f, 0), new Vector3(0.62f, 0.14f, 1.5f), dark, 0.4f); Box(tm, new Vector3(0, 0.165f, 0), new Vector3(0.46f, 0.012f, 1.36f), H("#15171c"), 0.15f);
        foreach (int k in new[] { -1, 1 }) { Rod(tm, new Vector3(k * 0.29f, 0.12f, 0.6f), new Vector3(k * 0.29f, 1.12f, 0.72f), 0.025f, steel, 0.7f); Rod(tm, new Vector3(k * 0.29f, 0.9f, 0.69f), new Vector3(k * 0.29f, 0.9f, 0.2f), 0.02f, steel, 0.7f); }
        Box(tm, new Vector3(0, 1.16f, 0.72f), new Vector3(0.66f, 0.26f, 0.08f), dark, 0.5f, new Vector3(-25, 0, 0)); Box(tm, new Vector3(0, 1.17f, 0.69f), new Vector3(0.4f, 0.15f, 0.02f), H("#4fb6e8"), 0.9f, new Vector3(-25, 0, 0));
        // rack of dumbbells
        var rack = Child(t, "Rack"); rack.localPosition = new Vector3(0.95f, 0, 1.4f);
        foreach (int k in new[] { -1, 1 }) Rod(rack, new Vector3(k * 0.55f, 0, 0.1f), new Vector3(k * 0.55f, 0.9f, -0.1f), 0.022f, dark, 0.5f);
        string[] cols = { "#e8478c", "#4fb6e8", "#f5b82e", "#3fcf8e", "#7c4ee0" };
        for (int row = 0; row < 2; row++) {
            float y = 0.38f + row * 0.4f, z = 0.03f - row * 0.08f; Box(rack, new Vector3(0, y, z), new Vector3(1.18f, 0.03f, 0.26f), steel, 0.6f);
            for (int i = 0; i < 5; i++) { float x = -0.44f + i * 0.22f, r = 0.05f + (4 - i) * 0.006f + row * 0.012f; var c = H(cols[(i + row) % 5]);
                Rod(rack, new Vector3(x, y + r + 0.015f, z - 0.09f), new Vector3(x, y + r + 0.015f, z + 0.09f), 0.014f, steel, 0.8f);
                foreach (int k in new[] { -1, 1 }) Cyl(rack, new Vector3(x, y + r + 0.015f, z + k * 0.085f), r, 0.05f, c, 0.4f, new Vector3(90, 0, 0)); }
        }
        // bench, mat, ball, kettlebells, a water bottle and a towel
        var bench = Child(t, "Bench"); bench.localPosition = new Vector3(1.0f, 0, 0.45f); bench.localRotation = Quaternion.Euler(0, 20, 0);
        Box(bench, new Vector3(0, 0.44f, 0), new Vector3(0.34f, 0.08f, 1.15f), red, 0.35f);
        foreach (int kz in new[] { -1, 1 }) { Rod(bench, new Vector3(0, 0.4f, kz * 0.42f), new Vector3(0, 0.03f, kz * 0.42f), 0.03f, dark, 0.5f); Rod(bench, new Vector3(-0.2f, 0.03f, kz * 0.42f), new Vector3(0.2f, 0.03f, kz * 0.42f), 0.025f, dark, 0.5f); }
        Box(t, new Vector3(-0.35f, 0.008f, -0.55f), new Vector3(0.68f, 0.016f, 1.75f), purple, 0.2f, new Vector3(0, 12, 0));
        Ball(t, new Vector3(-1.9f, 0.31f, 1.3f), Vector3.one * 0.62f, H("#3fcf8e"), 0.6f);
        for (int i = 0; i < 2; i++) { var kp = new Vector3(0.55f + i * 0.26f, 0, 1.45f); Ball(t, kp + new Vector3(0, 0.09f, 0), Vector3.one * (0.17f + i * 0.03f), dark, 0.5f);
            Lathe(t, kp + new Vector3(0, 0.16f, 0), i == 0 ? yellow : red, 0.5f, 0.05f, 0, 0.065f, 0.04f, 0.05f, 0.07f, 0.0f, 0.07f); }
        Lathe(t, new Vector3(0.62f, 0, -0.2f), H("#4fb6e8"), 0.7f, 0, 0, 0.035f, 0, 0.035f, 0.17f, 0.018f, 0.2f, 0.018f, 0.23f, 0, 0.23f);
        Box(t, new Vector3(1.2f, 0.5f, 0.62f), new Vector3(0.3f, 0.05f, 0.2f), white, 0.05f, new Vector3(0, 20, 0));
    }
    /// <summary>The starlet in the gym, head to toe, drawn again on every call. lift raises her off the floor (a jump).</summary>
    public RenderTexture Gym(int w, int h, Look3 look, float yaw, float lift) {
        w = Mathf.Clamp(w, 16, 2048); h = Mathf.Clamp(h, 16, 2048);
        if (liveGym == null || liveGymSize.x != w || liveGymSize.y != h) { liveGymSize = new Vector2Int(w, h); if (liveGym != null) { liveGym.Release(); UnityEngine.Object.Destroy(liveGym); } liveGym = NewRT(w, h); }
        if (gym == null) BuildGym();
        Show(3, (look, new Vector3(0, lift, 0), yaw)); BoxCamera(liveGym, new Vector2(0, 0.98f), new Vector2(2.3f, 2.12f), true, H("#d7efe6")); return liveGym;
    }

    /// <summary>A still of a room with a starlet in it (picker, photos).</summary>
    public RenderTexture RoomShot(string key, int w, int h, DRoom r, Dictionary<string, DFurn> kinds, Func<string, string> furnSvg, Look3 look) {
        key = "room:" + key + ":" + look.Key + ":" + w + "x" + h;
        if (shots.TryGetValue(key, out var rt) && rt != null) return rt;
        rt = NewRT(w, h); SetRoom(r, kinds, furnSvg); Show(1, (look, Floor(r.doll.x, r.doll.y), 0)); RoomCamera(rt);
        return Keep(key, rt);
    }
    /// <summary>A starlet on her own, on a transparent background: "full" (head to toe), "face" (the mirror) or "portrait" (round pictures).</summary>
    public RenderTexture Shot(Look3 look, string shot, int w, int h, float yaw = 0) {
        string key = look.Key + ":" + shot + ":" + w + "x" + h + ":" + yaw;
        if (shots.TryGetValue(key, out var rt) && rt != null) return rt;
        rt = NewRT(w, h); Show(0, (look, Vector3.zero, yaw));
        if (shot == "full") BoxCamera(rt, new Vector2(0, 0.885f), new Vector2(0.9f, 1.88f), false);
        else if (shot == "face") BoxCamera(rt, new Vector2(0, 1.555f), new Vector2(0.44f, 0.56f), false);
        else BoxCamera(rt, new Vector2(0, 1.575f), new Vector2(0.44f, 0.44f), true);
        return Keep(key, rt);
    }
    /// <summary>Starlets side by side, from the hips up (the home banner).</summary>
    public RenderTexture Lineup(Look3[] looks, int w, int h) {
        string key = "lineup:" + string.Join(",", Array.ConvertAll(looks, l => l.Key)) + w + "x" + h; var ids = looks;
        if (shots.TryGetValue(key, out var rt) && rt != null) return rt;
        rt = NewRT(w, h); var who = new (Look3, Vector3, float)[ids.Length]; const float gap = 0.66f;
        for (int i = 0; i < ids.Length; i++) who[i] = (looks[i], new Vector3((i - (ids.Length - 1) / 2f) * gap, 0, i % 2 * 0.25f), (i - (ids.Length - 1) / 2f) * 4f);
        Show(0, who); float width = ids.Length * gap + 0.25f, vh = width * h / w;
        BoxCamera(rt, new Vector2(0, 1.86f - vh / 2), new Vector2(width, vh), true);
        return Keep(key, rt);
    }
    /// <summary>Copies a picture out of the graphics card, to save it as a file.</summary>
    public static Texture2D Read(RenderTexture rt) {
        var keep = RenderTexture.active; RenderTexture.active = rt;
        var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
        RenderTexture.active = keep; return t;
    }
}

[Serializable] public class P3 { public string mesh, material; public float[] tone; public float lum; }
[Serializable] public class E3 { public string id, label; public P3[] parts; }
[Serializable] public class Index3 { public E3[] starlets, hair, furniture; }

/// <summary>How a starlet should look in 3D.</summary>
public sealed class Look3 {
    public string id = "aria";          // whose head and face
    public string body;                 // whose signature outfit she arrives in (null: her own)
    public string hair;                 // null: her own; "h:<id>": a hairstyle; "s:<id>": another starlet's hair
    public Color? hairColor, underwear; // null: as it comes
    public Color skinTint = Color.white;
    public float height = 1, build = 1; // overall size
    public readonly float[] shape = new float[8];   // weight, muscle, shoulders, hips, bust, leg length, head size, bum: about -1..1 (0 = as she came; hips, bust and bum go to 1.6)
    public string bra = "halter", pants = "briefs";   // underwear styles
    public Texture2D makeup; public int makeupVer;   // the makeup painted on her face, and how many times it has been changed
    public bool signature = true;       // with nothing on from the wardrobe: her signature outfit (true) or her underwear (false)
    public readonly List<(string slot, string item, Color color)> worn = new List<(string, string, Color)>();
    public string Key {
        get {
            var sb = new System.Text.StringBuilder(id).Append('|').Append(body).Append('|').Append(hair).Append('|').Append(hairColor).Append('|').Append(underwear).Append('|').Append(skinTint)
                .Append('|').Append(height).Append('|').Append(build).Append('|').Append(string.Join(",", shape)).Append('|').Append(makeup != null ? "m" + makeupVer : "").Append('|').Append(bra).Append(pants).Append(signature);
            foreach (var w in worn) sb.Append('|').Append(w.slot).Append('=').Append(w.item).Append(ColorUtility.ToHtmlStringRGB(w.color));
            return sb.ToString();
        }
    }
}
