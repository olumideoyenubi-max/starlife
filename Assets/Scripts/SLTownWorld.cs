// Starlife Town's streets and buildings, sky and lights. The town itself is the web game's (Tools/town/world.html):
// Tools/town/export.js runs it and saves its finished geometry, materials, painted textures and gameplay data to
// Resources/Town, and this class puts that together in Unity. Gameplay positions keep the web town's coordinates
// (x east, z south, ry a heading where 0 faces +z); W() and Yaw() turn them into Unity's.
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SLTownWorld {
    public sealed class Place { public string id, name, sub, color; public float x, z, ry; public int[] open; public int node; public bool mall, street, gate, hangout; }
    public sealed class Seat { public float x, z, ry, y; public string place; }

    public readonly Transform root; public readonly Light sun; readonly Material sky;
    public readonly List<Place> places = new List<Place>(); public readonly List<Seat> seats = new List<Seat>();
    public readonly List<float[]> colliders = new List<float[]>(), platforms = new List<float[]>();
    public readonly List<Vector2> nodes = new List<Vector2>(); public readonly List<List<int>> edges = new List<List<int>>();
    readonly List<Vector3> lampHeads = new List<Vector3>(); readonly Light[] lamps = new Light[6];
    public readonly Dictionary<string, object> data;
    public float Cell, Half, Edge; public int N; public Vector3 Home; public float NightNow, Exposure = 1.35f;
    public bool Ok { get; }

    public const int Layer = 9;     // the town and everyone in it; the suite's stage lights and camera leave this layer alone
    public static void SetLayer(Transform t) { t.gameObject.layer = Layer; foreach (Transform c in t) SetLayer(c); }
    public static Vector3 W(float x, float y, float z) => new Vector3(x, y, -z);
    public static Quaternion Yaw(float ry) => Quaternion.Euler(0, 180f - ry * Mathf.Rad2Deg, 0);

    public SLTownWorld() {
        var json = Resources.Load<TextAsset>("Town/towndata"); var geo = Resources.Load<TextAsset>("Town/towngeo");
        root = new GameObject("Starlife Town").transform; root.gameObject.SetActive(false);
        if (json == null || geo == null) { Debug.LogWarning("Starlife: the town is not in this build (run node Tools/town/export.js)."); return; }
        data = SLJson.O(SLJson.Parse(json.text));
        Cell = SLJson.F(data["CELL"]); Half = SLJson.F(data["HALF"]); Edge = SLJson.F(data["EDGE"]); N = (int)SLJson.F(data["N"]);
        foreach (var o in SLJson.A(data["places"])) {
            var p = new Place { id = SLJson.S(SLJson.Get(o, "id")), name = SLJson.S(SLJson.Get(o, "name")), sub = SLJson.S(SLJson.Get(o, "sub"), ""), color = SLJson.S(SLJson.Get(o, "color"), "#ffffff"),
                x = SLJson.F(SLJson.Get(o, "x")), z = SLJson.F(SLJson.Get(o, "z")), ry = SLJson.F(SLJson.Get(o, "ry")), node = (int)SLJson.F(SLJson.Get(o, "node")),
                mall = SLJson.B(SLJson.Get(o, "mall")), street = SLJson.B(SLJson.Get(o, "street")), gate = SLJson.B(SLJson.Get(o, "gate")), hangout = SLJson.B(SLJson.Get(o, "hangout")) };
            var op = SLJson.Get(o, "open") as List<object>; if (op != null) p.open = new[] { (int)SLJson.F(op[0]), (int)SLJson.F(op[1]) };
            places.Add(p);
        }
        foreach (var o in SLJson.A(data["seats"])) seats.Add(new Seat { x = SLJson.F(SLJson.Get(o, "x")), z = SLJson.F(SLJson.Get(o, "z")), ry = SLJson.F(SLJson.Get(o, "ry")), y = SLJson.F(SLJson.Get(o, "y")), place = SLJson.S(SLJson.Get(o, "place"), "") });
        foreach (var o in SLJson.A(data["colliders"])) colliders.Add(Floats(o));
        foreach (var o in SLJson.A(data["platforms"])) platforms.Add(Floats(o));
        foreach (var o in SLJson.A(data["nodes"])) { var f = Floats(o); nodes.Add(new Vector2(f[0], f[1])); }
        foreach (var o in SLJson.A(data["edges"])) { var l = new List<int>(); foreach (var v in SLJson.A(o)) l.Add((int)SLJson.F(v)); edges.Add(l); }
        foreach (var o in SLJson.A(data["lamps"])) { var f = Floats(o); lampHeads.Add(W(f[0], f[1], f[2])); }
        { var h = Floats(data["home"]); Home = new Vector3(h[0], h[1], h[2]); }

        BuildGeometry(geo.bytes);
        // sun, sky and street lamps
        sun = new GameObject("Sun").AddComponent<Light>(); sun.transform.SetParent(root, false);
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.8f; sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.3f;
        for (int i = 0; i < lamps.Length; i++) { var l = new GameObject("Lamp " + i).AddComponent<Light>(); l.transform.SetParent(root, false); l.type = LightType.Point; l.range = 18; l.color = new Color(1f, 0.85f, 0.63f); l.intensity = 0; l.shadows = LightShadows.None; lamps[i] = l; }
        sky = new Material(Resources.Load<Shader>("SLTownSky"));
        foreach (var o in SLJson.A(data["fountains"])) Fountain(Floats(o));
        SetLayer(root);
        sun.cullingMask = 1 << Layer; foreach (var l in lamps) l.cullingMask = 1 << Layer;
        Ok = true;
    }
    static float[] Floats(object o) { var l = SLJson.A(o); var f = new float[l.Count]; for (int i = 0; i < f.Length; i++) f[i] = SLJson.F(l[i]); return f; }
    static Color Hex(string h, Color def) => h != null && ColorUtility.TryParseHtmlString(h, out var c) ? c : def;

    // ---------------------------------------------------------------- geometry and materials
    readonly List<Material> allMats = new List<Material>();
    void BuildGeometry(byte[] bin) {
        var texById = new Dictionary<string, Texture2D>();
        foreach (var t in SLJson.A(data["textures"])) {
            string id = SLJson.S(SLJson.Get(t, "id")); var tex = Resources.Load<Texture2D>("Town/Tex/" + id);
            if (tex == null) continue;
            tex.wrapMode = SLJson.B(SLJson.Get(t, "repeat")) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; tex.anisoLevel = 8; tex.filterMode = FilterMode.Trilinear;
            texById[id] = tex;
        }
        Shader lit = Resources.Load<Shader>("SLTown"), clear = Resources.Load<Shader>("SLTownClear");
        var mats = new Dictionary<string, (Material m, bool clear)>();
        foreach (var e in SLJson.A(data["materials"])) {
            string id = SLJson.S(SLJson.Get(e, "id"));
            bool see = SLJson.B(SLJson.Get(e, "transparent")) && SLJson.F(SLJson.Get(e, "opacity"), 1) < 0.99f;
            var m = new Material(see ? clear : lit) { name = "Town " + id + " " + SLJson.S(SLJson.Get(e, "name"), "") };
            var col = Hex(SLJson.S(SLJson.Get(e, "color")), Color.white); col.a = SLJson.F(SLJson.Get(e, "opacity"), 1); m.SetColor("_Color", col);
            string map = SLJson.S(SLJson.Get(e, "map")); if (map != null && texById.TryGetValue(map, out var mt)) m.SetTexture("_MainTex", mt);
            m.SetFloat("_Glossiness", Mathf.Clamp01(1 - SLJson.F(SLJson.Get(e, "roughness"), 1)) * 0.85f);
            m.SetFloat("_Metallic", SLJson.F(SLJson.Get(e, "metalness")));
            m.SetFloat("_VC", SLJson.B(SLJson.Get(e, "vertexColors")) ? 1 : 0);
            int side = (int)SLJson.F(SLJson.Get(e, "side")); m.SetFloat("_Cull", side == 2 ? 0 : side == 1 ? 1 : 2);
            float off = SLJson.F(SLJson.Get(e, "offset")); m.SetFloat("_OffF", off); m.SetFloat("_OffU", off);
            if (!see) {
                m.SetFloat("_Cutoff", SLJson.F(SLJson.Get(e, "alphaTest")));
                var em = Hex(SLJson.S(SLJson.Get(e, "emissive")), Color.black); m.SetColor("_EmissionColor", em);
                string emap = SLJson.S(SLJson.Get(e, "emissiveMap")); if (emap != null && texById.TryGetValue(emap, out var et)) m.SetTexture("_EmissionMap", et);
                float night = SLJson.F(SLJson.Get(e, "night")), day = SLJson.F(SLJson.Get(e, "dayGlow")), k = SLJson.F(SLJson.Get(e, "emissiveIntensity"), 1);
                // lit windows and lamps: dim by day, full at night; signs and shop interiors: half by day
                if (night > 0) { m.SetFloat("_EmA", night * 0.08f); m.SetFloat("_EmB", night * 0.92f); }
                else if (day > 0) { m.SetFloat("_EmA", day * 0.45f); m.SetFloat("_EmB", day * 0.55f); }
                else { m.SetFloat("_EmA", k); m.SetFloat("_EmB", 0); }
                if (SLJson.B(SLJson.Get(e, "basic"))) { m.SetColor("_EmissionColor", col); m.SetFloat("_Glossiness", 0); }
            } else {
                m.SetFloat("_ZWrite", SLJson.B(SLJson.Get(e, "depthWrite")) && col.a > 0.6f ? 1 : 0);
                if (SLJson.B(SLJson.Get(e, "basic"))) m.SetFloat("_Glossiness", 0);
                m.renderQueue = 3000 + (off != 0 ? -10 : 0);
            }
            mats[id] = (m, see); allMats.Add(m);
        }
        var geo = new GameObject("Streets").transform; geo.SetParent(root, false);
        int n = 0;
        foreach (var e in SLJson.A(data["meshes"])) {
            int verts = (int)SLJson.F(SLJson.Get(e, "verts")), tris = (int)SLJson.F(SLJson.Get(e, "tris")), off = (int)SLJson.F(SLJson.Get(e, "off"));
            bool colors = SLJson.B(SLJson.Get(e, "colors")); string mid = SLJson.S(SLJson.Get(e, "mat"));
            if (verts == 0 || tris == 0 || !mats.TryGetValue(mid, out var mm)) continue;
            var P = new Vector3[verts]; var Nn = new Vector3[verts]; var U = new Vector2[verts]; Color[] C = colors ? new Color[verts] : null; var I = new int[tris * 3];
            var fl = new float[verts * 3];
            Buffer.BlockCopy(bin, off, fl, 0, verts * 12); for (int i = 0; i < verts; i++) P[i] = new Vector3(fl[i * 3], fl[i * 3 + 1], fl[i * 3 + 2]); off += verts * 12;
            Buffer.BlockCopy(bin, off, fl, 0, verts * 12); for (int i = 0; i < verts; i++) Nn[i] = new Vector3(fl[i * 3], fl[i * 3 + 1], fl[i * 3 + 2]); off += verts * 12;
            var f2 = new float[verts * 2]; Buffer.BlockCopy(bin, off, f2, 0, verts * 8); for (int i = 0; i < verts; i++) U[i] = new Vector2(f2[i * 2], f2[i * 2 + 1]); off += verts * 8;
            if (colors) { Buffer.BlockCopy(bin, off, fl, 0, verts * 12); for (int i = 0; i < verts; i++) C[i] = new Color(fl[i * 3], fl[i * 3 + 1], fl[i * 3 + 2]); off += verts * 12; }
            Buffer.BlockCopy(bin, off, I, 0, tris * 12);
            var mesh = new Mesh { name = "Town " + mid, indexFormat = verts > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.vertices = P; mesh.normals = Nn; mesh.uv = U; if (C != null) mesh.colors = C; mesh.triangles = I; mesh.RecalculateBounds();
            if (mm.m.GetTexture("_MainTex") != null) mesh.RecalculateTangents();
            var go = new GameObject("Town " + (n++) + " " + mid); go.transform.SetParent(geo, false); go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mm.m;
            r.shadowCastingMode = mm.clear ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = !mm.clear;
        }
    }

    // ---------------------------------------------------------------- the fountain in Central Park
    void Fountain(float[] at) {
        var go = new GameObject("Fountain spray"); go.transform.SetParent(root, false); go.transform.position = W(at[0], at[1] + 2.5f, at[2]);
        var ps = go.AddComponent<ParticleSystem>(); var r = go.GetComponent<ParticleSystemRenderer>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.duration = 1; main.loop = true; main.startLifetime = 1.3f; main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); main.gravityModifier = 0.6f; main.maxParticles = 1500; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.9f, 0.95f, 1f, 0.7f);
        var em = ps.emission; em.rateOverTime = 900;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 28; sh.radius = 0.08f; sh.rotation = new Vector3(-90, 0, 0);
        var mat = new Material(Shader.Find("Sprites/Default")); r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    // ---------------------------------------------------------------- the town's rules for moving about
    public float HeightAt(float x, float z) { float y = 0; foreach (var p in platforms) if (x >= p[0] && x <= p[2] && z >= p[1] && z <= p[3] && p[4] > y) y = p[4]; return y; }
    public Vector2 Collide(Vector2 p, float r = 0.3f) {
        foreach (var c in colliders) {
            if (p.x < c[0] - r || p.x > c[2] + r || p.y < c[1] - r || p.y > c[3] + r) continue;
            float dl = p.x - (c[0] - r), dr = (c[2] + r) - p.x, dt = p.y - (c[1] - r), db = (c[3] + r) - p.y, m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(dt, db));
            if (m == dl) p.x = c[0] - r; else if (m == dr) p.x = c[2] + r; else if (m == dt) p.y = c[1] - r; else p.y = c[3] + r;
        }
        float L = Edge + 30; p.x = Mathf.Clamp(p.x, -L, L); p.y = Mathf.Clamp(p.y, -L, L); return p;
    }
    /// <summary>How far the camera can sit from t (town x, y, z) along dir before a building is in the way.</summary>
    public float CamBlock(Vector3 t, Vector3 dir, float maxD) {
        float best = maxD;
        foreach (var c in colliders) {
            if (c[4] < 2.5f) continue;
            float tmin = 0, tmax = best; bool miss = false;
            for (int a = 0; a < 2; a++) {
                float o = a == 0 ? t.x : t.z, d = a == 0 ? dir.x : dir.z, lo = (a == 0 ? c[0] : c[1]) - 0.3f, hi = (a == 0 ? c[2] : c[3]) + 0.3f;
                if (Mathf.Abs(d) < 1e-6f) { if (o < lo || o > hi) { miss = true; break; } continue; }
                float t1 = (lo - o) / d, t2 = (hi - o) / d; if (t1 > t2) { var s = t1; t1 = t2; t2 = s; }
                tmin = Mathf.Max(tmin, t1); tmax = Mathf.Min(tmax, t2); if (tmin > tmax) { miss = true; break; }
            }
            if (!miss && tmin > 0.2f && tmin < best) best = tmin;
        }
        return Mathf.Max(1.2f, best - 0.25f);
    }
    public int NearestNode(float x, float z) { int best = 0; float bd = float.MaxValue; for (int i = 0; i < nodes.Count; i++) { float d = (nodes[i].x - x) * (nodes[i].x - x) + (nodes[i].y - z) * (nodes[i].y - z); if (d < bd) { bd = d; best = i; } } return best; }
    public List<Vector2> Route(int from, int to) {
        int n = nodes.Count; var dist = new float[n]; var prev = new int[n]; var done = new bool[n];
        for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
        dist[from] = 0;
        while (true) {
            int u = -1; float ud = float.MaxValue; for (int i = 0; i < n; i++) if (!done[i] && dist[i] < ud) { ud = dist[i]; u = i; }
            if (u < 0 || u == to) break; done[u] = true;
            foreach (int v in edges[u]) { float w = (nodes[u] - nodes[v]).magnitude; if (dist[u] + w < dist[v]) { dist[v] = dist[u] + w; prev[v] = u; } }
        }
        var path = new List<Vector2>(); for (int u = to; u >= 0; u = prev[u]) path.Insert(0, nodes[u]);
        return path;
    }

    // ---------------------------------------------------------------- time of day
    static readonly (float h, string top, string hor, string sun, float si, float hi, string fog)[] Sky = {
        (0, "#0b1030", "#1a2350", "#8fa6ff", 0.3f, 0.3f, "#141a3a"), (5, "#1a2050", "#4a4370", "#ffb08a", 0.3f, 0.3f, "#3a3a5a"),
        (6.5f, "#5c86c9", "#f3b38c", "#ffb07a", 1.0f, 0.42f, "#d8b49c"), (9, "#3d7fd6", "#bcd7f2", "#fff1dc", 2.0f, 0.5f, "#c3d8ee"),
        (16, "#3d7fd6", "#c7dcf1", "#fff1dc", 1.9f, 0.5f, "#c8dbee"), (18.5f, "#4a63a8", "#f6a873", "#ff9a5a", 1.2f, 0.45f, "#e2a988"),
        (20, "#1d2458", "#6a4f78", "#ff7f6a", 0.3f, 0.26f, "#4a3f62"), (21.5f, "#0b1030", "#1a2350", "#8fa6ff", 0.3f, 0.3f, "#141a3a"),
        (24, "#0b1030", "#1a2350", "#8fa6ff", 0.3f, 0.3f, "#141a3a") };
    float envHour = -1;
    /// <summary>Sets sky, sun, ambient light, fog and the glow of windows and signs for the hour (0..24). Returns how dark it is (0..1).</summary>
    public float SetHour(float h, Vector3 focus) {
        int i = 0; while (i < Sky.Length - 2 && Sky[i + 1].h <= h) i++;
        var a = Sky[i]; var b = Sky[i + 1]; float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((h - a.h) / (b.h - a.h)));
        Color Mix(string x, string y) => Color.Lerp(Hex(x, Color.black), Hex(y, Color.black), t);
        Color top = Mix(a.top, b.top), hor = Mix(a.hor, b.hor), sunC = Mix(a.sun, b.sun);
        bool day = h >= 5.5f && h <= 20.5f;
        float ang = day ? (h - 5.5f) / 15f * Mathf.PI : ((h + 24 - 20.5f) % 24) / 9f * Mathf.PI;
        var dir = new Vector3(Mathf.Cos(ang) * 0.8f, Mathf.Max(0.12f, Mathf.Sin(ang)), 0.45f).normalized;
        var dirU = new Vector3(dir.x, dir.y, -dir.z);
        sky.SetColor("_Top", top); sky.SetColor("_Horizon", hor); sky.SetColor("_Ground", new Color(0.43f, 0.42f, 0.40f));
        sky.SetVector("_SunDir", day ? dirU : Vector3.down); sky.SetColor("_SunCol", day ? sunC : Color.black);
        float si = Mathf.Lerp(a.si, b.si, t), hi = Mathf.Lerp(a.hi, b.hi, t);
        float night = Mathf.Clamp01(1 - (si - 0.3f) / 0.8f); NightNow = night; Exposure = Mathf.Lerp(1.02f, 1.5f, night);
        sky.SetFloat("_Night", night);
        sun.color = sunC; sun.intensity = si * 0.78f; sun.transform.rotation = Quaternion.LookRotation(-dirU);
        sun.shadowStrength = Mathf.Lerp(0.85f, 0.5f, night);
        Shader.SetGlobalVector("_HairLight", dirU);
        var hemi = Color.Lerp(top, Color.white, 0.55f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = hemi * hi * 1.15f; RenderSettings.ambientEquatorColor = Color.Lerp(hemi, hor, 0.5f) * hi * 0.95f; RenderSettings.ambientGroundColor = new Color(0.54f, 0.48f, 0.41f) * hi * 0.8f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = Mix(a.fog, b.fog);
        RenderSettings.fogStartDistance = 280 * 0.35f; RenderSettings.fogEndDistance = 280;
        RenderSettings.skybox = sky; RenderSettings.sun = sun;
        Shader.SetGlobalFloat("_TownNight", night);
        if (Mathf.Abs(h - envHour) > 0.25f) { envHour = h; DynamicGI.UpdateEnvironment(); }
        // street lamps: the nearest few become real lights after dark
        lampHeads.Sort((p, q) => (p - focus).sqrMagnitude.CompareTo((q - focus).sqrMagnitude));
        for (int k = 0; k < lamps.Length; k++) { if (k < lampHeads.Count) lamps[k].transform.position = lampHeads[k]; lamps[k].intensity = night * 2.4f; lamps[k].enabled = night > 0.02f; }
        return night;
    }
}

