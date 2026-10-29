// Starlife Town: GO OUT from the suite and walk around a 3D town as your starlet (the web game's world.html, ported).
// The streets come from SLTownWorld; this part is the life in them: you and the other starlets (the suite's own 3D
// avatars, skinned by SLRig), walking routes and daily routines, places to go and things to do there, chatting and
// friendships, the boutiques of Velvet Avenue (pieces bought there go into the suite's wardrobe), Café Doré, the phone
// with its map, and the town's clock and needs. Coins, fame and clothes are the suite's; the clock, needs, skills and
// friendships are kept with the save (SLCore.Pref, "town.*"). Part of SLApp.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static SLJson;

public partial class SLApp {
    // ================================================================ people
    sealed class TP {
        public string id, name, role; public Look3 look; public Transform go, fig; public SLRigMotion mo;
        public Vector3 pos; public float ry, speed, walkSpeed = 1.3f, timer, waveT; public float? faceTo;
        public List<Vector2> path = new List<Vector2>(); public string state = "idle", prevState;
        public float emoteFor;     // a reaction (a laugh, a dance) that ends by itself
        public SLTownWorld.Place at, dest, meetup; public SLTownWorld.Seat seat; public VisualElement tag; public bool visible = true, crowd, male;
        public string First => name.Split(' ')[0];
    }
    SLTownWorld town; Camera tcam; SLTownTone tone; bool inTown, townBusyLoading; TP player; readonly List<TP> people = new List<TP>();
    System.Random trng = new System.Random(20261026);
    float R() => (float)trng.NextDouble();
    T Pick<T>(IList<T> l) => l[trng.Next(l.Count)];

    // ================================================================ life: clock, needs, skills, friendships
    static readonly string[] NeedKeys = { "hunger", "energy", "hygiene", "fun", "social" }, NeedNames = { "Hunger", "Energy", "Hygiene", "Fun", "Social" };
    static readonly float[] Decay = { 0.075f, 0.055f, 0.045f, 0.065f, 0.05f };
    static readonly string[] SkillKeys = { "cooking", "fitness", "charisma", "creativity" };
    static readonly string[] Days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
    float lifeMin = 8 * 60; int tSpeed = 1; float[] needs = { 75, 80, 85, 65, 55 }; float[] skills = new float[4];
    readonly Dictionary<string, float> rel = new Dictionary<string, float>();
    string Me => state != null && !string.IsNullOrEmpty(state.starlet) ? state.starlet : "aria";
    static float Num0(string s, float d) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;
    static string Nums(IEnumerable<float> f) => string.Join(",", f.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));
    void LoadLife() {
        lifeMin = Num0(core.Pref("town.min"), 8 * 60); tSpeed = (int)Num0(core.Pref("town.speed"), 1);
        var n = core.Pref("town.needs." + Me).Split(','); for (int i = 0; i < 5; i++) needs[i] = i < n.Length ? Num0(n[i], new[] { 75f, 80, 85, 65, 55 }[i]) : new[] { 75f, 80, 85, 65, 55 }[i];
        var s = core.Pref("town.skills." + Me).Split(','); for (int i = 0; i < 4; i++) skills[i] = i < s.Length ? Num0(s[i], 0) : 0;
        rel.Clear(); foreach (var kv in core.Pref("town.rel." + Me).Split(';')) { var p = kv.Split('='); if (p.Length == 2) rel[p[0]] = Num0(p[1], 0); }
    }
    void SaveLife() {
        core.SetPref("town.min", lifeMin.ToString("0.#", CultureInfo.InvariantCulture)); core.SetPref("town.speed", tSpeed.ToString());
        core.SetPref("town.needs." + Me, Nums(needs)); core.SetPref("town.skills." + Me, Nums(skills));
        core.SetPref("town.rel." + Me, string.Join(";", rel.Select(kv => kv.Key + "=" + kv.Value.ToString("0.#", CultureInfo.InvariantCulture))));
        if (player != null) { var at = MapPos(); core.SetPref("town.pos", Nums(new[] { at.x, at.y, room != null ? room.p.ry : player.ry })); }
    }
    float Hour => (lifeMin % 1440) / 60f;
    int DayNum => Mathf.FloorToInt(lifeMin / 1440);
    string ClockLabel() { float m = lifeMin % 1440; int h = Mathf.FloorToInt(m / 60), mm = Mathf.FloorToInt(m % 60), h12 = (h + 11) % 12 + 1; return Days[DayNum % 7] + " · " + h12 + ":" + mm.ToString("00") + " " + (h < 12 ? "AM" : "PM"); }
    (string, string) Mood() { float v = needs.Average(); return v >= 70 ? ("Happy", "#2fbf71") : v >= 45 ? ("Fine", "#e3b11f") : v >= 25 ? ("Uncomfortable", "#f08a3c") : ("Miserable", "#e5484d"); }
    void Bump(string k, float a) { int i = Array.IndexOf(NeedKeys, k); if (i >= 0) needs[i] = Mathf.Clamp(needs[i] + a, 0, 100); }
    static string RelLabel(float v) => v >= 85 ? "Best friend" : v >= 60 ? "Good friend" : v >= 35 ? "Friend" : v >= 15 ? "Acquaintance" : "Stranger";
    float Rel(string id) => rel.TryGetValue(id, out var v) ? v : 0;
    void Befriend(string id, float a) => rel[id] = Mathf.Clamp(Rel(id) + a, 0, 100);
    void AddSkill(string k, float a) {
        int i = Array.IndexOf(SkillKeys, k); if (i < 0) return; int before = Mathf.FloorToInt(skills[i]);
        skills[i] = Mathf.Clamp(skills[i] + a, 0, 10);
        if (Mathf.FloorToInt(skills[i]) > before) ShowToast(char.ToUpper(k[0]) + k.Substring(1) + " reached level " + Mathf.FloorToInt(skills[i]));
    }
    static string HoursTxt(int a, int b) { string f(int h) { h %= 24; return ((h + 11) % 12 + 1) + " " + (h < 12 ? "AM" : "PM"); } return f(a) + "–" + f(b); }
    bool IsOpen(SLTownWorld.Place p, float h) { if (p.open == null) return true; int a = p.open[0], b = p.open[1]; return a < b ? h >= a && h < b : h >= a || h < b; }
    bool IsOpen(SLTownWorld.Place p) => IsOpen(p, Hour);
    SLTownWorld.Place PlaceBy(string id) => town.places.Find(p => p.id == id);
    float D(TP a, Vector3 b) => new Vector2(a.pos.x - b.x, a.pos.z - b.z).magnitude;
    float D(TP a, TP b) => D(a, b.pos);

    // ================================================================ going out and coming home
    VisualElement townUI; Camera mainCam; bool savedFog; float savedShadowDist; int savedCascades; UnityEngine.Rendering.AmbientMode savedAmbient; Color savedSky, savedEq, savedGround; Material savedSkybox;
    public void GoOut() {
        if (inTown || townBusyLoading) return;
        if (!On3) Set3D(true, false);
        if (!On3) { ShowToast("Starlife Town needs the 3D starlets"); return; }
        StartCoroutine(EnterTown());
    }
    IEnumerator EnterTown() {
        townBusyLoading = true;
        var loading = El("town-loading", L("STARLIFE TOWN", "town-load-t"), L("Painting the streets…", "town-load-s"));
        overlayHost.Add(loading); yield return null; yield return null;
        if (town == null) town = new SLTownWorld();
        if (!town.Ok) { loading.RemoveFromHierarchy(); townBusyLoading = false; ShowToast("Starlife Town is not in this build"); yield break; }
        ((Label)loading[1]).text = "Waking up the starlets…"; yield return null;
        LoadLife();
        foreach (var t in people) Destroy(t.go.gameObject); people.Clear();
        // you, where you left off (or at your front door)
        player = Spawn(Me, data.starlets.FirstOrDefault(s => s.id == Me), MyLook(null));
        var sp = core.Pref("town.pos").Split(',');
        if (sp.Length == 3 && Mathf.Abs(Num0(sp[0], 1e9f)) < town.Edge + 30) { player.pos = new Vector3(Num0(sp[0], 0), 0, Num0(sp[1], 0)); player.ry = Num0(sp[2], 0); }
        else { var h = town.Home; player.pos = new Vector3(h.x + Mathf.Sin(h.z) * 1.5f, 0, h.y + Mathf.Cos(h.z) * 1.5f); player.ry = h.z; }
        var c0 = town.Collide(new Vector2(player.pos.x, player.pos.z)); player.pos.x = c0.x; player.pos.z = c0.y;
        player.pos.y = town.HeightAt(player.pos.x, player.pos.z);
        cam.yaw = player.ry + Mathf.PI; cam.cur = 0;
        // everyone else, each starting at one of her favourite places
        int k = 0;
        foreach (var s in data.starlets) {
            if (s.id == Me || s.id == "custom" || !stage3d.Has(core.Pref("face." + s.id, s.id))) continue;
            var pp = Spawn(s.id, s, SigLook(s.id)); pp.walkSpeed = 1.25f + R() * 0.3f; pp.timer = 2 + R() * 20;
            var at = PlaceBy(Pick(Haunts(s.id))) ?? PlaceBy("home");
            pp.pos = new Vector3(at.x + (R() - 0.5f) * 6, 0, at.z + (R() - 0.5f) * 3); pp.at = at;
            people.Add(pp);
            if (++k % 3 == 0) { ((Label)loading[1]).text = "Waking up the starlets… " + k; yield return null; }
        }
        // passers-by: the town's own people (make_men.py), each going about their day
        foreach (var e in stage3d.Starlets) {
            if (!e.id.StartsWith("p_")) continue;
            var pp = Spawn(e.id, null, new Look3 { id = e.id, height = SLStage3D.IsMale(e.id) ? 1.06f : 1f }); pp.crowd = true; pp.name = char.ToUpperInvariant(e.id[2]) + e.id.Substring(3); pp.role = "Lives in Starlife Town"; pp.walkSpeed = 1.15f + R() * 0.4f; pp.timer = R() * 5;
            var n0 = town.nodes[trng.Next(town.nodes.Count)]; pp.pos = new Vector3(n0.x, 0, n0.y); people.Add(pp);
        }
        Mannequins();
        SetUpCars();
        SetUpEateries();
        ((Label)loading[1]).text = "Turning on the lights…"; yield return null;
        // the town camera and the town's light; the suite's interface steps aside
        mainCam = Camera.main; if (mainCam != null) mainCam.enabled = false;
        if (tcam == null) {
            tcam = new GameObject("Town Camera").AddComponent<Camera>(); tcam.fieldOfView = 55; tcam.nearClipPlane = 0.1f; tcam.farClipPlane = 480;
            tcam.clearFlags = CameraClearFlags.Skybox; tcam.cullingMask = 1 << SLTownWorld.Layer; tcam.allowMSAA = true; tcam.allowHDR = true; tone = tcam.gameObject.AddComponent<SLTownTone>();
        }
        tcam.gameObject.SetActive(true);
        savedFog = RenderSettings.fog; savedShadowDist = QualitySettings.shadowDistance; savedCascades = QualitySettings.shadowCascades; savedSkybox = RenderSettings.skybox;
        savedAmbient = RenderSettings.ambientMode; savedSky = RenderSettings.ambientSkyColor; savedEq = RenderSettings.ambientEquatorColor; savedGround = RenderSettings.ambientGroundColor;
        QualitySettings.shadowDistance = 80; QualitySettings.shadowCascades = 4;
        town.root.gameObject.SetActive(true);
        BuildTownUI();
        foreach (var e in root.Children().ToList()) if (e != overlayHost && e != townUI) e.style.display = DisplayStyle.None;
        root.AddToClassList("in-town"); root.style.backgroundImage = StyleKeyword.None;
        loading.RemoveFromHierarchy(); inTown = true; townBusyLoading = false;
        ShowToast("Welcome to town, " + player.First + "!");
    }
    void LeaveTown(string toView = "suite") {
        if (!inTown) return;
        if (shop != null) CloseBoutique(); CloseCard(); busy = null;
        SaveLife(); if (room != null) ExitRoom(true); core.Flush();
        inTown = false; town.root.gameObject.SetActive(false); tcam.gameObject.SetActive(false); if (mainCam != null) mainCam.enabled = true;
        foreach (var t in people) Destroy(t.go.gameObject); people.Clear(); if (player != null) { Destroy(player.go.gameObject); player = null; }
        foreach (var m in mannequins) Destroy(m.gameObject); mannequins.Clear();
        if (driving != null) GetOut();
        foreach (var c in cars) Destroy(c.go.gameObject); cars.Clear(); myCar = testCar = null;
        RenderSettings.fog = savedFog; RenderSettings.skybox = savedSkybox; QualitySettings.shadowDistance = savedShadowDist; QualitySettings.shadowCascades = savedCascades;
        RenderSettings.ambientMode = savedAmbient; RenderSettings.ambientSkyColor = savedSky; RenderSettings.ambientEquatorColor = savedEq; RenderSettings.ambientGroundColor = savedGround;
        Shader.SetGlobalVector("_HairLight", -(Quaternion.Euler(40, 24, 0) * Vector3.forward));
        townUI.RemoveFromHierarchy(); townUI = null;
        foreach (var e in root.Children()) e.style.display = StyleKeyword.Null;
        root.RemoveFromClassList("in-town"); root.style.backgroundImage = Grad("#a9cdf2", "#e6f2fc");
        dirty = true; SetView(toView);
    }

    // ---------------------------------------------------------------- figures
    TP Spawn(string id, DStarlet s, Look3 look) {
        var pp = new TP { id = id, name = s != null ? s.name : id, role = s != null ? s.role : "", look = look, male = SLStage3D.IsMale(look.id) };
        pp.go = new GameObject("Starlet " + id).transform; pp.go.SetParent(town.root, false);
        Dress(pp);
        return pp;
    }
    void Dress(TP pp) {
        if (pp.fig != null) Destroy(pp.fig.gameObject);
        var keep = pp.mo; pp.fig = stage3d.Figure(pp.look, pp.go, "Figure"); var bones = SLRig.Attach(pp.fig);
        pp.mo = new SLRigMotion(bones); if (keep != null) { pp.mo.pose = keep.pose; pp.mo.seatDrop = keep.seatDrop; }
        SLTownWorld.SetLayer(pp.go);
        foreach (var r in pp.fig.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        if (keep != null && keep.cup) CupOn(pp, true);
    }
    Material cupM, sleeveM, lidM;
    void CupOn(TP pp, bool on) {
        if (cupM == null) { var sh = Resources.Load<Shader>("SLTown"); cupM = new Material(sh); cupM.SetColor("_Color", new Color(0.96f, 0.94f, 0.9f)); sleeveM = new Material(sh); sleeveM.SetColor("_Color", new Color(0.16f, 0.1f, 0.07f)); lidM = new Material(sh); lidM.SetColor("_Color", new Color(0.79f, 0.64f, 0.29f)); lidM.SetFloat("_Metallic", 0.7f); lidM.SetFloat("_Glossiness", 0.6f); }
        pp.mo.Cup(on, cupM, sleeveM, lidM); if (pp.mo.CupHolder != null) SLTownWorld.SetLayer(pp.mo.CupHolder);
    }
    // what each starlet likes to do through the day (the web town's list for its six; the others get their own)
    List<string> Haunts(string id) {
        var h = Get(data_town("HAUNTS"), id) as List<object>; if (h != null) return h.Select(o => (string)o).ToList();
        var all = town.places.Where(p => p.id != "home" && p.id != "galleria").Select(p => p.id).ToList();
        var rnd = new System.Random(id.GetHashCode() & 0x7fffffff); var res = new List<string>();
        while (res.Count < 9 && all.Count > 0) { int i = rnd.Next(all.Count); res.Add(all[i]); all.RemoveAt(i); }
        return res;
    }
    object data_town(string k) => town.data.TryGetValue(k, out var v) ? v : null;
    // headless dress forms in the boutique windows, wearing this week's looks
    readonly List<Transform> mannequins = new List<Transform>();
    void Mannequins() {
        var looks = A(data_town("mannequinLooks"));
        foreach (var o in A(data_town("mannequins"))) {
            var f = A(o); int li = (int)F(f[3]); if (li >= looks.Count) continue;
            var outfit = O(A(looks[li])[0]); var colors = O(A(looks[li])[1]);
            var L = new Look3 { id = "olivia", skinTint = new Color(1.25f, 1.32f, 1.38f), signature = false, underwear = new Color(0.94f, 0.92f, 0.9f) };
            foreach (var kv in outfit) { string item = Swap(kv.Value as string); if (item == null || !itemById.ContainsKey(item)) continue; var col = colors != null && colors.TryGetValue(kv.Value as string, out var cv) ? (string)cv : itemById[item].color; L.worn.Add((itemById[item].slot, item, C(col))); }
            var go = new GameObject("Mannequin").transform; go.SetParent(town.root, false);
            var fig = stage3d.Figure(L, go, "Figure");
            foreach (Transform c in fig) { var part = c.name.Split('/').Last(); if (part == "head" || part == "hair" || part == "eyes" || part == "teeth" || part == "extra") c.gameObject.SetActive(false); }
            go.position = SLTownWorld.W(F(f[0]), F(f[4]), F(f[1])); go.rotation = SLTownWorld.Yaw(F(f[2])); go.localScale = Vector3.one * 0.98f;
            SLTownWorld.SetLayer(go); mannequins.Add(go);
        }
    }
    static readonly Dictionary<string, string> Swaps = new Dictionary<string, string>();     // the web town's items that the game does not have (none since menswear came in)
    static string Swap(string id) => id != null && Swaps.TryGetValue(id, out var s) ? s : id;

    // ================================================================ walking about
    void SendTo(TP pp, SLTownWorld.Place dest, Vector2? target = null) {
        pp.mo.emote = null; pp.mo.sitStyle = null;
        int from = town.NearestNode(pp.pos.x, pp.pos.z), to = dest != null ? dest.node : town.NearestNode(target.Value.x, target.Value.y);
        var path = town.Route(from, to);
        Vector2? end = target ?? (dest != null ? new Vector2(dest.x + (R() - 0.5f) * 2.5f, dest.z + (R() - 0.5f) * 1.2f) : (Vector2?)null);
        if (end.HasValue) path.Add(end.Value);
        pp.path = path; pp.dest = dest; pp.state = "walk"; pp.seat = null; pp.mo.pose = "stand";
    }
    SLTownWorld.Place ChooseDest(TP pp) {
        float h = Hour;
        if (h >= 2 && h < 7) return PlaceBy("home");
        var list = (pp.crowd ? town.places.Select(p => p.id).ToList() : Haunts(pp.id)).Select(PlaceBy).Where(p => p != null && IsOpen(p, h) && p != pp.at).ToList();
        bool night = h >= 20 || h < 2;
        string[] nightSpots = { "club", "bar", "house", "hall", "theatre", "skygarden", "lounge" }, daySpots = { "cafe", "park", "books", "boutique", "foodcourt", "avenue", "gelato", "patisserie" };
        var w = list.Select(p => (night && nightSpots.Contains(p.id) ? 3f : 1f) + (!night && daySpots.Contains(p.id) ? 1.5f : 0)).ToList();
        float r = R() * w.Sum();
        for (int i = 0; i < list.Count; i++) { r -= w[i]; if (r <= 0) return list[i]; }
        return list.Count > 0 ? list[0] : PlaceBy("park");
    }
    bool SeatTaken(SLTownWorld.Seat s, TP but = null) => people.Any(p => p != but && p.seat == s) || player.seat == s;
    void Arrive(TP pp) {
        pp.at = pp.dest;
        if (pp.meetup != null && pp.meetup == pp.dest) {
            pp.state = "idle"; pp.timer = 120; pp.faceTo = Mathf.Atan2(player.pos.x - pp.pos.x, player.pos.z - pp.pos.z);
            if (D(pp, player) < 40) ShowToast(pp.First + " made it to " + pp.dest.name + "!");
            return;
        }
        if (pp.dest != null && pp.dest.id == "home" && Hour >= 1 && Hour < 7) { pp.state = "inside"; pp.timer = 60 + R() * 120; return; }
        var free = town.seats.Where(s => s.place == (pp.dest != null ? pp.dest.id : null) && !SeatTaken(s) && new Vector2(s.x - pp.pos.x, s.z - pp.pos.z).magnitude < 22).ToList();
        if (free.Count > 0 && R() < 0.7f) { var s = Pick(free); pp.seat = s; pp.path = new List<Vector2> { SeatFront(s) }; pp.state = "toSeat"; return; }
        pp.state = "idle"; pp.timer = 15 + R() * 45;
        // something to do while she waits: a dance at a party, a call, folded arms, a think
        string at = pp.dest != null ? pp.dest.id : "";
        pp.mo.emote = at == "club" || at == "house" ? (R() < 0.75f ? "dance" : "cheer") : R() < 0.45f ? new[] { "phone", "arms", "think", "laugh", null }[trng.Next(5)] : null;
        if (pp.dest != null) pp.faceTo = pp.dest.ry + Mathf.PI + (R() - 0.5f) * 1.5f;
    }
    /// <summary>Where you stand to sit down on a seat: just in front of it.</summary>
    Vector2 SeatFront(SLTownWorld.Seat s) {      // in front if that is clear (a bench), else beside or behind it (a chair at a table)
        foreach (float a in new[] { 0f, Mathf.PI / 2, -Mathf.PI / 2, Mathf.PI }) {
            var q = new Vector2(s.x + Mathf.Sin(s.ry + a) * 0.6f, s.z + Mathf.Cos(s.ry + a) * 0.6f);
            if ((town.Collide(q, 0.25f) - q).sqrMagnitude < 0.0004f && !town.seats.Any(o => o != s && new Vector2(o.x - q.x, o.z - q.y).magnitude < 0.35f)) return q;
        }
        return new Vector2(s.x + Mathf.Sin(s.ry) * 0.45f, s.z + Mathf.Cos(s.ry) * 0.45f);
    }
    /// <summary>Walkers keep out of buildings, benches, lamp posts and cars (yours too), and give each other room.</summary>
    void KeepClear(TP pp, float dt) {
        var c = KeepOutOfCars(town.Collide(new Vector2(pp.pos.x, pp.pos.z), 0.25f), 0.3f, true); pp.pos.x = c.x; pp.pos.z = c.y;
        Spread(pp, dt);
    }
    void Spread(TP pp, float dt) {
        foreach (var o in people) {
            if (o == pp || !o.visible || o.state == "inside" || o.state == "sit" || o.state == "sitDown") continue;
            float dx = pp.pos.x - o.pos.x, dz = pp.pos.z - o.pos.z, d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < 0.6f && d > 0.001f) { float push = (0.6f - d) * Mathf.Min(1, dt * 6); pp.pos.x += dx / d * push; pp.pos.z += dz / d * push; }
        }
    }
    static float Turn(float a, float b, float k) { float d = ((b - a + Mathf.PI) % (Mathf.PI * 2) + Mathf.PI * 2) % (Mathf.PI * 2) - Mathf.PI; return a + d * Mathf.Min(1, k); }
    void MoveToward(TP pp, float x, float z, float sp, float dt) {
        float dx = x - pp.pos.x, dz = z - pp.pos.z, d = Mathf.Sqrt(dx * dx + dz * dz);
        pp.speed = Mathf.Lerp(pp.speed, sp, 1 - Mathf.Exp(-dt * 5));
        pp.ry = Turn(pp.ry, Mathf.Atan2(dx, dz), dt * 7);
        float step = Mathf.Min(d, pp.speed * dt);
        pp.pos.x += Mathf.Sin(pp.ry) * step; pp.pos.z += Mathf.Cos(pp.ry) * step;
    }
    void StepPerson(TP pp, float dt, float gm) {
        var mo = pp.mo;
        if (pp.state == "watch") { pp.speed = 0; Place(pp); return; }      // watching the fashion show
        if (pp.state == "inside") { pp.timer -= gm; if (pp.timer <= 0 && !(Hour >= 2 && Hour < 7)) SendTo(pp, ChooseDest(pp)); return; }
        if (pp.state == "talk") { pp.speed = 0; pp.ry = Turn(pp.ry, Mathf.Atan2(player.pos.x - pp.pos.x, player.pos.z - pp.pos.z), dt * 6); mo.pose = pp.waveT > 0 ? "wave" : "stand"; pp.waveT -= dt; }
        else if (pp.state == "follow") {
            float d = D(pp, player);
            if (player.seat != null) pp.speed = 0;
            else if (d > 1.8f) { float sp = d > 4 ? 3.6f : player.speed > 2 ? 3.8f : 1.5f; MoveToward(pp, player.pos.x - Mathf.Sin(player.ry) * 1.2f + Mathf.Cos(player.ry) * 0.8f, player.pos.z - Mathf.Cos(player.ry) * 1.2f - Mathf.Sin(player.ry) * 0.8f, sp, dt); KeepClear(pp, dt); }
            else { pp.speed = Mathf.Lerp(pp.speed, 0, dt * 8); pp.ry = Turn(pp.ry, Mathf.Atan2(player.pos.x - pp.pos.x, player.pos.z - pp.pos.z), dt * 3); }
            if (d > 40) pp.pos = new Vector3(player.pos.x - 1.5f, 0, player.pos.z - 1.5f);
            mo.pose = "stand";
        } else if (pp.state == "walk" || pp.state == "toSeat") {
            if (pp.path.Count == 0) {
                if (pp.state == "toSeat") {
                    if (SeatTaken(pp.seat, pp)) { pp.seat = null; pp.state = "idle"; pp.timer = 10; }
                    else { pp.state = "sitDown"; pp.timer = 0.7f; mo.emote = null; mo.pose = "sit"; mo.seatDrop = pp.seat.y - town.HeightAt(pp.seat.x, pp.seat.z) + 0.06f; mo.sitStyle = new[] { null, "chat", "phone", "lean", "eat" }[trng.Next(5)]; }
                }
                else Arrive(pp);
            } else {
                var w = pp.path[0]; float d = new Vector2(w.x - pp.pos.x, w.y - pp.pos.z).magnitude;
                if (d < 0.35f) pp.path.RemoveAt(0); else { MoveToward(pp, w.x, w.y, pp.walkSpeed, dt); KeepClear(pp, dt); }
                float pd = D(pp, player);     // step aside for the player
                if (pd < 0.75f && pd > 0.01f) { pp.pos.x -= (player.pos.x - pp.pos.x) / pd * dt * 1.2f; pp.pos.z -= (player.pos.z - pp.pos.z) / pd * dt * 1.2f; }
            }
        } else if (pp.state == "sitDown") {     // turning round and lowering into the seat
            pp.speed = 0; pp.timer -= dt; float k = 1 - Mathf.Exp(-dt * 7);
            pp.ry = Turn(pp.ry, pp.seat.ry, dt * 8); pp.pos.x = Mathf.Lerp(pp.pos.x, pp.seat.x, k); pp.pos.z = Mathf.Lerp(pp.pos.z, pp.seat.z, k);
            if (pp.timer <= 0) { pp.state = "sit"; pp.timer = 25 + R() * 50; pp.pos.x = pp.seat.x; pp.pos.z = pp.seat.z; pp.ry = pp.seat.ry; }
        } else if (pp.state == "sit") {
            pp.speed = 0; pp.timer -= gm;
            if (pp.timer <= 0) { mo.pose = "stand"; mo.sitStyle = null; pp.state = "standUp"; pp.timer = 0.6f; }
        } else if (pp.state == "standUp") {     // up, and a step out from the table before walking off
            pp.speed = 0; pp.timer -= dt; var fr = SeatFront(pp.seat); float k = 1 - Mathf.Exp(-dt * 6);
            pp.pos.x = Mathf.Lerp(pp.pos.x, fr.x, k); pp.pos.z = Mathf.Lerp(pp.pos.z, fr.y, k);
            if (pp.timer <= 0) { pp.seat = null; SendTo(pp, ChooseDest(pp)); }
        } else {
            pp.speed = Mathf.Lerp(pp.speed, 0, dt * 8); Spread(pp, dt);
            if (pp.faceTo.HasValue) pp.ry = Turn(pp.ry, pp.faceTo.Value, dt * 2);
            pp.timer -= gm;
            if (pp.timer <= 0) SendTo(pp, ChooseDest(pp));
        }
        float gy = (pp.state == "sit" || pp.state == "sitDown") && pp.seat != null ? town.HeightAt(pp.seat.x, pp.seat.z) : town.HeightAt(pp.pos.x, pp.pos.z);
        pp.pos.y = Mathf.Lerp(pp.pos.y, gy, 1 - Mathf.Exp(-dt * 12));
        Place(pp);
    }
    void Place(TP pp) { pp.go.position = SLTownWorld.W(pp.pos.x, pp.pos.y, pp.pos.z); pp.go.rotation = SLTownWorld.Yaw(pp.ry); }

    // ================================================================ you
    sealed class TCam { public float yaw = Mathf.PI, pitch = 0.28f, dist = 5.2f, lastDrag = -10, cur; public Vector3 target; public float? focusY; public float shift; }
    readonly TCam cam = new TCam();
    void UpdatePlayer(float dt) {
        float ix = 0, iz = 0;
        bool typing = false;
        if (!typing) {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) iz += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) iz -= 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ix -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ix += 1;
        }
        bool run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || autoRun;
        if (ix == 0 && iz == 0) {
            // a game controller's left stick (the shoulder buttons or a stick press run), or the touch joystick
            float px = Input.GetAxis("Horizontal"), pz = Input.GetAxis("Vertical");
            if (new Vector2(px, pz).magnitude > 0.2f) { ix = px; iz = pz; run |= PadRun(); }
            if (joy != null && joy.Value.magnitude > 0.12f) { ix = joy.Value.x; iz = joy.Value.y; run |= joy.Value.magnitude > 0.92f; }
        }
        if (autoWalk != null) { var a = autoWalk.Value; ix = a.x; iz = a.y; }
        float mag = Mathf.Min(1, new Vector2(ix, iz).magnitude);
        if (busy != null || modal != null) mag = 0;
        if (mag > 0.05f) HideHint();
        var mo = player.mo;
        if (stepT > 0) { stepT -= dt; float ks = 1 - Mathf.Exp(-dt * 9); player.pos.x = Mathf.Lerp(player.pos.x, stepTo.x, ks); player.pos.z = Mathf.Lerp(player.pos.z, stepTo.y, ks); player.speed = 0; mo.Animate(dt, 0); Place(player); return; }
        // walking off ends an emote, or gets her up off the ground
        if (mag > 0.1f && (mo.emote != null || groundSit)) { mo.emote = null; if (groundSit) { groundSit = false; mo.pose = "stand"; } }
        if (player.seat != null && mag > 0.1f) { StandUp(); mo.Animate(dt, 0); Place(player); return; }
        if (player.seat != null) {      // easing round and down into the seat
            player.speed = 0; mo.Animate(dt, 0); float ks = 1 - Mathf.Exp(-dt * 8);
            player.pos = new Vector3(Mathf.Lerp(player.pos.x, player.seat.x, ks), town.HeightAt(player.seat.x, player.seat.z), Mathf.Lerp(player.pos.z, player.seat.z, ks)); player.ry = Turn(player.ry, player.seat.ry, dt * 9);
            Place(player); return;
        }
        float target = mag > 0.05f ? (run ? 4.2f : 1.6f * (0.45f + mag * 0.55f)) : 0;
        player.speed = Mathf.Lerp(player.speed, target, 1 - Mathf.Exp(-dt * (target > player.speed ? 6 : 10)));
        // -ix: the town is mirrored into Unity (W flips z), so a larger heading turns left on screen
        if (mag > 0.05f && !autoFixed) { float fwd = cam.yaw + Mathf.PI, a = Mathf.Atan2(-ix, iz) + fwd; player.ry = Turn(player.ry, a, dt * 10); }
        if (player.speed > 0.01f) {
            player.pos.x += Mathf.Sin(player.ry) * player.speed * dt; player.pos.z += Mathf.Cos(player.ry) * player.speed * dt;
            if (player.speed > 3) { runMetres += player.speed * dt; if (runMetres >= 500) { runMetres = 0; ShowToast("Half a kilometre of running." + BodyChange(Me, -0.02f, 0.01f)); if (bodyDirty) { bodyDirty = false; player.look = MyLook(null); Dress(player); } } }
            var c = KeepOutOfCars(town.Collide(new Vector2(player.pos.x, player.pos.z)), 0.3f); player.pos.x = c.x; player.pos.z = c.y;
            foreach (var o in people) {      // and she walks round people, not through them
                if (!o.visible || o.state == "inside" || o.state == "follow") continue;
                float dx = player.pos.x - o.pos.x, dz = player.pos.z - o.pos.z, dd = Mathf.Sqrt(dx * dx + dz * dz);
                if (dd < 0.55f && dd > 0.001f) { player.pos.x += dx / dd * (0.55f - dd); player.pos.z += dz / dd * (0.55f - dd); }
            }
        }
        player.pos.y = Mathf.Lerp(player.pos.y, town.HeightAt(player.pos.x, player.pos.z), 1 - Mathf.Exp(-dt * 14));
        mo.talking = talking != null; mo.Animate(dt, player.speed); Place(player);
    }
    void SitOn(SLTownWorld.Seat s) {
        player.seat = s; player.mo.pose = "sit"; player.mo.seatDrop = s.y - town.HeightAt(s.x, s.z) + 0.06f; player.mo.emote = null; groundSit = false;
        ShowToast((s.place == "cafe" ? "You grab a table on the terrace." : "You sit down for a while.") + " Press G for things to do here, or move to stand up.");
    }
    // ---------------------------------------------------------------- emotes, and things to do in a seat
    void OpenEmotes() {
        if (driving != null || busy != null || player == null || showCam) return;
        if (player.seat != null) {
            Opt S(string label, string style, string note) => new Opt { label = label, note = note, accent = player.mo.sitStyle == style, run = () => { CloseCard(); player.mo.sitStyle = style; if (style == "chat" && people.Any(p => p.seat != null && D(p, player) < 3)) Bump("social", 4); else Bump("fun", 2); } };
            OpenCard("Make yourself comfortable", "In your seat", "What do you feel like doing?", new List<Opt> {
                S("Just relax", null, "settle in"), S("Chat with the table", "chat", "+social"), S("Scroll your phone", "phone", "+fun"), S("Eat and drink", "eat", "a bite, a sip"), S("Lean back", "lean", "+energy"),
                new Opt { label = "Stand up", run = () => { CloseCard(); StandUp(); } } });
            return;
        }
        var list = new[] { ("wave", "Wave", "say hi"), ("dance", "Dance", "+fun"), ("clap", "Clap", "applause!"), ("cheer", "Cheer", "arms up"), ("laugh", "Laugh", "+fun"), ("think", "Think", "hmm…"),
            ("pose", "Strike a pose", "+charisma"), ("phone", "Phone call", "+social"), ("arms", "Fold your arms", ""), ("nod", "Nod", "yes!"), ("ground", "Sit on the ground", "anywhere") };
        var opts = list.Select(e => new Opt { label = e.Item2, note = e.Item3, accent = player.mo.emote == e.Item1, run = () => DoEmote(e.Item1) }).ToList();
        if (player.mo.emote != null || groundSit) opts.Insert(0, new Opt { label = "Stop", run = () => { CloseCard(); player.mo.emote = null; if (groundSit) { groundSit = false; player.mo.pose = "stand"; } } });
        OpenCard("Emotes", "G or ☺ any time · walk to stop", "Express yourself! People nearby may join in.", opts);
    }
    void DoEmote(string id) {
        CloseCard(); var mo = player.mo; player.speed = 0;
        if (id == "ground") { mo.emote = null; mo.pose = "sit"; mo.seatDrop = 0.17f; groundSit = true; Bump("energy", 2); ShowToast("You sit down right here. Move to get up."); return; }
        if (groundSit) { groundSit = false; mo.pose = "stand"; }
        mo.emote = id;
        if (id == "dance" || id == "laugh") Bump("fun", id == "dance" ? 6 : 3); else if (id == "phone") Bump("social", 3); else if (id == "pose") AddSkill("charisma", 0.05f);
        // the people around join in
        string join = id == "dance" ? "dance" : id == "cheer" || id == "clap" ? "clap" : id == "laugh" ? "laugh" : id == "wave" ? "wave" : null;
        if (join == null) return; int n = 0;
        foreach (var pp in people) {
            if (!pp.visible || D(pp, player) > 6 || !(pp.state == "idle" || pp.state == "talk" || pp.state == "follow")) continue;
            pp.mo.emote = join; pp.emoteFor = 6 + R() * 4; pp.faceTo = Mathf.Atan2(player.pos.x - pp.pos.x, player.pos.z - pp.pos.z); if (!pp.crowd) Befriend(pp.id, 1); n++;
        }
        if (n > 0 && id == "dance") { Bump("social", 4); ShowToast(n == 1 ? "Someone nearby starts dancing with you!" : n + " people join your dance!"); }
    }
    void StandUp() {
        if (player.seat == null) return; var s = player.seat; player.seat = null; player.mo.pose = "stand"; player.mo.sitStyle = null;
        stepTo = SeatFront(s); stepT = 0.45f;      // up, and a step out from the table
    }
    void UpdateCamera(float dt) {
        var pp = player.pos;
        float fy = cam.focusY ?? (player.seat != null ? 1.05f : 1.42f) * player.look.height, sh = cam.shift;
        var want = new Vector3(pp.x + Mathf.Cos(cam.yaw) * sh, pp.y + fy, pp.z - Mathf.Sin(cam.yaw) * sh);
        cam.target = cam.cur == 0 ? want : Vector3.Lerp(cam.target, want, 1 - Mathf.Exp(-dt * 10));
        if (player.speed > 0.4f && Time.time - cam.lastDrag > 1.6f) cam.yaw = Turn(cam.yaw, player.ry + Mathf.PI, dt * 0.9f);
        var dir = new Vector3(Mathf.Sin(cam.yaw) * Mathf.Cos(cam.pitch), Mathf.Sin(cam.pitch), Mathf.Cos(cam.yaw) * Mathf.Cos(cam.pitch));
        float d = town.CamBlock(cam.target, dir, cam.dist);
        cam.cur = cam.cur == 0 ? d : Mathf.Lerp(cam.cur, d, 1 - Mathf.Exp(-dt * (d < cam.cur ? 25 : 4)));
        var p = cam.target + dir * cam.cur; p.y = Mathf.Max(p.y, town.HeightAt(p.x, p.z) + 0.3f);
        tcam.transform.position = SLTownWorld.W(p.x, p.y, p.z);
        tcam.transform.LookAt(SLTownWorld.W(cam.target.x, cam.target.y, cam.target.z));
    }

    // ================================================================ the main loop
    float coffeeT, phoneT, saveT, runMetres, stepT; Vector2? autoWalk; Vector2 stepTo; bool autoRun, autoFixed, groundSit;
    void TickTown() {
        if (!inTown) return;
        float raw = Mathf.Min(1, Time.deltaTime), dt = Mathf.Min(0.05f, raw), gm = 0;
        if (busy != null) TickBusy();
        else if (tSpeed > 0) { gm = dt * tSpeed * (player.seat != null ? 3 : 1); lifeMin += gm; }
        if (gm > 0) {
            for (int i = 0; i < 5; i++) needs[i] = Mathf.Clamp(needs[i] - Decay[i] * gm, 0, 100);
            if (player.seat != null) { Bump("energy", 0.12f * gm); Bump("fun", 0.12f * gm); if (people.Any(p => !p.crowd && p.seat != null && D(p, player) < 3)) Bump("social", 0.25f * gm); }
        }
        TownInput();
        if (driving != null) TickDriving(dt); else UpdatePlayer(dt);
        TickTraffic(dt); TickSignals();
        if (coffeeT > 0) { coffeeT -= gm; if (coffeeT <= 0) { CupOn(player, false); ShowToast("Coffee finished."); } }
        foreach (var pp in people) {
            StepPerson(pp, dt, busy != null ? dt * 30 : gm);
            float d = D(pp, player); bool vis = pp.state != "inside" && d < 280;
            if (pp.visible != vis) { pp.visible = vis; pp.go.gameObject.SetActive(vis); }
            if (pp.emoteFor > 0 && (pp.emoteFor -= dt) <= 0) pp.mo.emote = null;
            if (vis) { pp.mo.talking = pp.state == "talk" && pp.mo.emote == null; pp.mo.Animate(dt, pp.state == "sit" ? 0 : pp.speed); }
        }
        if (room != null) TickRoom(dt);
        if (showCam) { } else if (driving != null) UpdateCarCamera(dt); else { UpdateCamera(dt); tcam.fieldOfView = Mathf.Lerp(tcam.fieldOfView, 55, dt * 3); }
        town.SetHour(Hour, SLTownWorld.W(player.pos.x, player.pos.y, player.pos.z)); if (tone != null) tone.exposure = town.Exposure;
        if (room != null) RoomLight();
        UpdateTags();
        near = FindNear();
        if (near != null) { promptL.text = near.label; promptSub.text = near.sub ?? ""; promptSub.style.display = string.IsNullOrEmpty(near.sub) ? DisplayStyle.None : DisplayStyle.Flex; prompt.style.display = DisplayStyle.Flex; }
        else prompt.style.display = DisplayStyle.None;
        if (waypoint != null) {
            var w = WpPos(); float d = new Vector2(w.x - player.pos.x, w.y - player.pos.z).magnitude;
            float bearing = Mathf.Atan2(w.x - player.pos.x, w.y - player.pos.z) - (cam.yaw + Mathf.PI);
            compassArrow.style.rotate = new Rotate(new Angle(-bearing * Mathf.Rad2Deg));
            compassTxt.text = waypoint.name + " · " + Mathf.RoundToInt(d) + " m";
            if (d < 3) SetWaypoint(null);
        }
        hud.style.opacity = player.speed > 0.5f && near == null ? 0.6f : 1;
        UpdateHud(false);
        phoneT -= dt; if (phoneT <= 0 && phone.style.display == DisplayStyle.Flex) { phoneT = 0.5f; DrawMap(); }
        saveT -= dt; if (saveT <= 0) { saveT = 4; SaveLife(); }
        if (hintShown > 0 && Time.time > hintShown) { hint.style.opacity = 0; }
    }

    // ---------------------------------------------------------------- keyboard and mouse
    Vector2 dragFrom; bool dragging, dragMoved; float dragT;
    bool OverUI(Vector2 screen) {
        var pnl = root.panel; if (pnl == null) return false;
        var pick = pnl.Pick(RuntimePanelUtils.ScreenToPanel(pnl, new Vector2(screen.x, Screen.height - screen.y)));
        return pick != null && pick != townUI && pick != root && pick != overlayHost;
    }
    // ---- game controllers (legacy input: buttons by number, the right stick on axes defined in InputManager)
    static bool PadRun() => Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.JoystickButton5) || Input.GetKey(KeyCode.JoystickButton8) || Input.GetKey(KeyCode.JoystickButton9) || Input.GetKey(KeyCode.JoystickButton10) || Input.GetKey(KeyCode.JoystickButton11);
    static Vector2 PadLook() {
        bool mac = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
        float x = Input.GetAxis(mac ? "Joy Axis 3" : "Joy Axis 4"), y = Input.GetAxis(mac ? "Joy Axis 4" : "Joy Axis 5");
        return new Vector2(Mathf.Abs(x) > 0.15f ? x : 0, Mathf.Abs(y) > 0.15f ? y : 0);
    }
    bool padUsed;
    void TownInput() {
        bool padA = Input.GetKeyDown(KeyCode.JoystickButton0), padB = Input.GetKeyDown(KeyCode.JoystickButton1), padMenu = Input.GetKeyDown(KeyCode.JoystickButton3) || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton9) && Application.platform == RuntimePlatform.OSXPlayer;
        if (padA || padB || padMenu) padUsed = true;
        if (Input.GetKeyDown(KeyCode.E) || (Input.GetKeyDown(KeyCode.Return) || padA) && modal == null && !PhoneOpen && Time.frameCount - cardClosedAt > 2) Interact();
        if (Input.GetKeyDown(KeyCode.Escape) || padB) { if (shop != null) CloseBoutique(); else if (modal != null) CloseCard(); else if (PhoneOpen) TogglePhone(false); }
        if (Input.GetKeyDown(KeyCode.M) || padMenu) TogglePhone(!PhoneOpen);
        if ((Input.GetKeyDown(KeyCode.G) || driving == null && Input.GetKeyDown(KeyCode.JoystickButton2)) && modal == null && !PhoneOpen) OpenEmotes();
        var look = PadLook();
        if (look != Vector2.zero && modal == null) { cam.yaw -= look.x * 2.4f * Time.deltaTime; cam.pitch = Mathf.Clamp(cam.pitch + look.y * 1.6f * Time.deltaTime, -0.15f, 1.2f); cam.lastDrag = Time.time; HideHint(); padUsed = true; }
        TouchInput();
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0 && !OverUI(Input.mousePosition)) cam.dist = Mathf.Clamp(cam.dist * (1 - Mathf.Sign(wheel) * 0.12f), 2, 14);
        if (Input.touchCount > 0) return;      // fingers are handled by TouchInput
        if (Input.GetMouseButtonDown(0) && !OverUI(Input.mousePosition)) { dragging = true; dragMoved = false; dragFrom = Input.mousePosition; dragT = Time.time; HideHint(); }
        if (dragging && Input.GetMouseButton(0)) {
            Vector2 m = Input.mousePosition, d = m - dragFrom; dragFrom = m;
            if (d.sqrMagnitude > 0) {
                float k = 900f / Mathf.Max(600, Screen.height);       // the same feel at any window size
                cam.yaw -= d.x * 0.0055f * k; cam.pitch = Mathf.Clamp(cam.pitch - d.y * 0.004f * k, -0.15f, 1.2f); cam.lastDrag = Time.time;
                if (d.magnitude > 1) dragMoved = true;
            }
        }
        if (dragging && Input.GetMouseButtonUp(0)) { dragging = false; if (!dragMoved && Time.time - dragT < 0.3f) TapPick(Input.mousePosition); }
    }
    // ---- touch: drag on the left to walk (a joystick appears where the finger lands), drag on the right to look,
    // pinch to zoom, tap a starlet to talk to her
    Vector2? joy; int joyId = -1, lookId = -1; Vector2 joyOrigin, lookLast, lookStart; float lookT, pinch; VisualElement joyRing, joyKnob;
    bool PhoneOpen => phone != null && phone.style.display == DisplayStyle.Flex;
    void TouchInput() {
        if (Input.touchCount == 0) { if (joyId >= 0) EndJoy(); lookId = -1; pinch = 0; return; }
        Input.simulateMouseWithTouches = false;
        if (Input.touchCount >= 2 && joyId < 0) {
            var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position; float d = (a - b).magnitude;
            if (pinch > 0) cam.dist = Mathf.Clamp(cam.dist * pinch / Mathf.Max(d, 1), 2, 14); pinch = d; lookId = -1; return;
        }
        pinch = 0;
        float R = 60f * Mathf.Max(1, Screen.dpi / 160f);
        foreach (var t in Input.touches) {
            if (t.phase == TouchPhase.Began) {
                if (OverUI(t.position)) continue;
                HideHint();
                if (t.position.x < Screen.width * 0.45f && joyId < 0 && modal == null) { joyId = t.fingerId; joyOrigin = t.position; joy = Vector2.zero; ShowJoy(t.position, Vector2.zero, R); }
                else if (lookId < 0) { lookId = t.fingerId; lookLast = lookStart = t.position; lookT = Time.time; }
            } else if (t.fingerId == joyId) {
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { EndJoy(); continue; }
                var v = (t.position - joyOrigin) / R; if (v.magnitude > 1) v.Normalize(); joy = v; ShowJoy(joyOrigin, v, R);
            } else if (t.fingerId == lookId) {
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { lookId = -1; if ((t.position - lookStart).magnitude < 12 && Time.time - lookT < 0.3f) TapPick(t.position); continue; }
                var d = t.position - lookLast; lookLast = t.position; float k = 900f / Mathf.Max(600, Screen.height);
                cam.yaw -= d.x * 0.0055f * k; cam.pitch = Mathf.Clamp(cam.pitch - d.y * 0.004f * k, -0.15f, 1.2f); cam.lastDrag = Time.time;
            }
        }
    }
    void ShowJoy(Vector2 at, Vector2 v, float R) {
        var pnl = root.panel; if (pnl == null || townUI == null) return;
        if (joyRing == null) { joyRing = El("t-joy"); joyKnob = El("t-joyknob"); joyRing.Add(joyKnob); joyRing.pickingMode = PickingMode.Ignore; joyKnob.pickingMode = PickingMode.Ignore; }
        if (joyRing.parent != townUI) townUI.Add(joyRing);
        var p = RuntimePanelUtils.ScreenToPanel(pnl, new Vector2(at.x, Screen.height - at.y)); float size = 120;
        joyRing.style.display = DisplayStyle.Flex; joyRing.style.left = p.x - size / 2; joyRing.style.top = p.y - size / 2;
        joyKnob.style.translate = new Translate(v.x * 38, -v.y * 38);
    }
    void EndJoy() { joyId = -1; joy = null; if (joyRing != null) joyRing.style.display = DisplayStyle.None; }
    void TapPick(Vector2 screen) {
        if (modal != null || busy != null) return;
        var ray = tcam.ScreenPointToRay(screen); TP best = null; float bd = 1e9f;
        foreach (var pp in people) {
            if (!pp.visible) continue;
            var p = SLTownWorld.W(pp.pos.x, pp.pos.y + 1.1f, pp.pos.z);
            float d = Vector3.Cross(ray.direction, p - ray.origin).magnitude, far = (p - tcam.transform.position).magnitude;
            if (d < 0.45f && far < 30 && far < bd) { bd = far; best = pp; }
        }
        if (best == null) return;
        if (D(best, player) < 3.2f) TalkTo(best); else SetWaypoint(new Waypoint { name = best.First, pp = best });
    }

    // ================================================================ things to do
    sealed class Opt { public string label, note, link; public bool disabled, accent; public Action run; }
    sealed class Modal { public string title; public List<Opt> opts; public string relId; }
    sealed class Busy { public SLTownWorld.Place p; public Dictionary<string, object> a; public float t0, dur, start; public float[] before; public TP friend; public List<TP> group; }
    Modal modal; Busy busy; TP talking;
    sealed class Near { public string kind, label, sub; public TP pp; public SLTownWorld.Seat s; public SLTownWorld.Place p; public Worker w; }
    Near near;
    Near FindNear() {
        if (busy != null || modal != null) return null;
        if (player.seat != null) return new Near { kind = "stand", label = "Stand up" };
        if (driving != null) return Mathf.Abs(driving.v) < 1.5f ? new Near { kind = "getout", label = driving == testCar ? "Bring the car back" : "Get out" } : null;
        if (room != null) { var rn = RoomNear(); if (rn != null) return rn; }
        Near best = null; float bd = 1e9f;
        foreach (var pp in people) { if (!pp.visible || pp.state == "inside") continue; float d = D(pp, player); if (d < 2.3f && d < bd) { bd = d; best = new Near { kind = "person", pp = pp, label = "Talk to " + pp.First }; } }
        if (best != null) return best;
        if (myCar != null && new Vector2(myCar.x - player.pos.x, myCar.z - player.pos.z).magnitude < myCar.Len * 0.5f + 1.4f) return new Near { kind = "car", label = "Drive your " + ModelName(myCar.spec.id) };
        foreach (var s in town.seats) { float d = new Vector2(s.x - player.pos.x, s.z - player.pos.z).magnitude; if (d < 1.1f && d < bd && !SeatTaken(s)) { bd = d; best = new Near { kind = "seat", s = s, label = "Sit down" }; } }
        if (best != null) return best;
        foreach (var p in town.places) { float d = new Vector2(p.x - player.pos.x, p.z - player.pos.z).magnitude; if (d < 2.6f && d < bd) { bd = d; best = new Near { kind = "place", p = p, label = HasRoom(p) && IsOpen(p) ? "Go into " + p.name : p.name, sub = IsOpen(p) ? p.sub : "Closed now" }; } }
        return best;
    }
    void Interact() {
        if (modal != null || !inTown) return;
        if (driving != null) { if (near != null && near.kind == "getout") GetOut(); return; }
        if (player.seat != null) { StandUp(); return; }
        var n = near; if (n == null) return;
        if (n.kind == "person") TalkTo(n.pp); else if (n.kind == "seat") SitOn(n.s); else if (n.kind == "place") { if (HasRoom(n.p) && IsOpen(n.p)) EnterRoom(n.p); else OpenPlace(n.p); }
        else if (n.kind == "worker") TalkToWorker(n.w); else if (n.kind == "exit") ExitRoom();
        else if (n.kind == "car") GetIn(myCar); else if (n.kind == "getout") GetOut();
    }
    int OutfitScore() {
        var w = player.look.worn; bool Has(string slot, params string[] ids) => w.Any(x => x.slot == slot && ids.Contains(x.item));
        bool formal = Has("dress", "gown", "lbd", "slip", "sequin", "wrap") || Has("outer", "blazer");
        int s = 35 + (formal ? 30 : 0) + (Has("dress", "gown", "sequin") ? 15 : 0);
        s += Has("shoes", "pumps", "kneeboots") ? 8 : 0;
        s += w.Count(x => new[] { "pearls", "pendant", "tiara", "drops", "studs" }.Contains(x.item)) * 6;
        return Mathf.Clamp(s, 0, 100);
    }
    static Dictionary<string, object> Act(string label, float min, Dictionary<string, object> fx, Dictionary<string, object> extra = null) {
        var a = new Dictionary<string, object> { { "label", label }, { "min", (double)min }, { "fx", fx } };
        if (extra != null) foreach (var kv in extra) a[kv.Key] = kv.Value; return a;
    }
    static Dictionary<string, object> Fx(params (string k, float v)[] f) { var d = new Dictionary<string, object>(); foreach (var x in f) d[x.k] = (double)x.v; return d; }
    bool InHours(Dictionary<string, object> a, float h) { var hr = Get(a, "hours") as List<object>; return hr == null || (h >= F(hr[0]) && h < F(hr[1])); }
    List<TP> Pals(SLTownWorld.Place p) => people.Where(x => x.visible && !x.crowd && D(x, player) < 9 && (Rel(x.id) >= 15 || x.state == "follow" || x.meetup == p)).ToList();
    string Names(IEnumerable<TP> l) => string.Join(" & ", l.Select(x => x.First));
    void OpenPlace(SLTownWorld.Place p, string greet = null) {
        if (busy != null) return;
        if (Eateries.Any(e => e.id == p.id)) { OpenEatery(p, greet); return; }
        var brands = O(data_town("BRANDS"));
        if (brands.ContainsKey(p.id)) {
            if (!IsOpen(p)) { OpenCard(p.name, p.sub + " · " + HoursTxt(p.open[0], p.open[1]), "Closed right now. Opens at " + HoursTxt(p.open[0], p.open[1]).Split('–')[0] + ".", new List<Opt>()); return; }
            OpenBoutique(p); return;
        }
        if (p.id == "dore") { OpenCoffee(p, greet); return; }
        if (p.id == "motors") { OpenDealer(p); return; }
        float h = Hour; bool open = IsOpen(p, h); var friend = people.FirstOrDefault(x => x.state == "follow");
        var opts = new List<Opt>();
        foreach (var o in A(Get(data_town("ACTS"), p.id))) {
            var a = O(o); string link = S(Get(a, "link")), sit = S(Get(a, "sit"));
            if (link != null) { string view = link.Contains('#') ? link.Substring(link.IndexOf('#') + 1) : "suite"; opts.Add(new Opt { label = S(Get(a, "label")), note = S(Get(a, "note"), ""), run = () => LeaveTown(view == "events" || view == "shop" || view == "parlor" || view == "suite" ? view : "suite") }); continue; }
            if (sit != null) { opts.Add(new Opt { label = S(Get(a, "label")), run = () => SitAt(sit) }); continue; }
            float cost = F(Get(a, "cost")), min = F(Get(a, "min"));
            bool ok = open && InHours(a, h) && (cost == 0 || profile.coins >= cost);
            var hr = Get(a, "hours") as List<object>;
            string note = !open ? "closed now" : !InHours(a, h) ? HoursTxt((int)F(hr[0]), (int)F(hr[1])) : cost > 0 ? cost + " coins · " + min + " min" : min + " min";
            var act = a; opts.Add(new Opt { label = S(Get(a, "label")), note = note, disabled = !ok, run = () => DoActivity(p, act) });
        }
        if (p.id == "home") opts.AddRange(GarageOptions());
        if (p.id == "hall") opts.Insert(0, new Opt { label = "Fashion Week: show your own collection", note = h >= 17 && h < 23 ? "four looks · a runway · the critics" : "5 PM – 11 PM", accent = h >= 17 && h < 23, disabled = !(h >= 17 && h < 23), run = OpenFashionWeek });
        if (p.id == "galleria") foreach (var q in town.places.Where(q => q.mall)) { var qq = q; opts.Add(new Opt { label = q.name, note = q.sub, run = () => { CloseCard(); SetWaypoint(new Waypoint { name = qq.name, x = qq.x, z = qq.z }); } }); }
        if (open && (p.hangout || new[] { "cafe", "bar", "park", "house" }.Contains(p.id))) {
            var pals = Pals(p);
            if (pals.Count > 0) opts.Insert(0, new Opt { label = "Hang out with " + Names(pals), note = "60 min · +social +fun", accent = true, run = () => DoActivity(p, Act("Hang out together", 60, Fx(("social", 35), ("fun", 25)), new Dictionary<string, object> { { "busy", "Hanging out at " + p.name } }), pals) });
            opts.Add(new Opt { label = "Call your friends to meet here", note = "they’ll walk over", run = () => CallFriends(p) });
        }
        string say = !open ? "Closed right now. Opens at " + HoursTxt(p.open[0], p.open[1]).Split('–')[0] + "."
            : p.id == "hall" || p.id == "theatre" || p.id == "club" ? "Dress code tonight: " + (p.id == "club" ? "party sparkle" : "black tie and gowns") + ". Your look scores " + OutfitScore() + "/100."
            : friend != null ? friend.First + " is with you." : S(Get(data_town("BLURB"), p.id), p.sub);
        if (greet != null) say = greet + " " + say;
        OpenCard(p.name, p.sub + (p.open != null ? " · " + HoursTxt(p.open[0], p.open[1]) : ""), say, opts);
    }
    void SitAt(string place) {
        var s = town.seats.Where(x => x.place == place && !SeatTaken(x)).OrderBy(x => new Vector2(x.x - player.pos.x, x.z - player.pos.z).magnitude).FirstOrDefault();
        CloseCard(); if (s != null) SitOn(s); else ShowToast("Every seat is taken right now.");
    }
    void CallFriends(SLTownWorld.Place p) {
        CloseCard();
        var pals = people.Where(x => !x.crowd && x.state != "inside" && x.state != "follow" && Rel(x.id) >= 15).OrderByDescending(x => Rel(x.id)).Take(3).ToList();
        if (pals.Count == 0) { ShowToast("No friends to call yet. Chat with starlets around town until they’re your friends."); return; }
        foreach (var x in pals) { x.meetup = p; if (x.seat != null) { x.seat = null; x.mo.pose = "stand"; } SendTo(x, p); }
        ShowToast("You texted " + string.Join(", ", pals.Select(x => x.First)) + ". They’re on their way to " + p.name + "!");
    }
    void DoActivity(SLTownWorld.Place p, Dictionary<string, object> a, List<TP> group = null) {
        CloseCard();
        float cost = F(Get(a, "cost")); if (cost > 0) core.Call("earn", (-cost).ToString(CultureInfo.InvariantCulture), "0");
        busy = new Busy { p = p, a = a, t0 = Time.time, dur = 2.6f + Mathf.Min(2, F(Get(a, "min")) / 90), start = lifeMin, before = (float[])needs.Clone(), friend = people.FirstOrDefault(x => x.state == "follow"), group = group };
        busyT.text = "AT " + p.name.ToUpperInvariant(); busyH.text = S(Get(a, "busy"), S(Get(a, "label")));
        busyEl.style.display = DisplayStyle.Flex;
    }
    void TickBusy() {
        var b = busy; float k = Mathf.Clamp01((Time.time - b.t0) / b.dur), e = k * k * (3 - 2 * k);
        lifeMin = b.start + F(Get(b.a, "min")) * e;
        foreach (var kv in O(Get(b.a, "fx")) ?? new Dictionary<string, object>()) { int i = Array.IndexOf(NeedKeys, kv.Key); if (i >= 0) needs[i] = Mathf.Clamp(b.before[i] + F(kv.Value) * e, 0, 100); }
        busyBar.style.width = Length.Percent(k * 100); busyC.text = ClockLabel();
        if (k < 1) return;
        var a = b.a; string msg = S(Get(a, "busy"), S(Get(a, "label"))) + ": done.";
        var sk = Get(a, "skill") as List<object>; if (sk != null) AddSkill((string)sk[0], F(sk[1]));
        if (Bool(Get(a, "glam"))) { int s = OutfitScore(), coins = Mathf.RoundToInt(s / 5f), fame = Mathf.RoundToInt(s / 8f) + (Bool(Get(a, "fame")) ? 4 : 0); core.Call("earn", coins.ToString(), fame.ToString()); msg = "Your look scored " + s + "/100: +" + coins + " coins, +" + fame + " fame."; }
        // what she eats fills her out a little; exercise slims and tones her
        var fxd = O(Get(a, "fx")) ?? new Dictionary<string, object>(); float food = fxd.TryGetValue("hunger", out var hv) ? F(hv) : 0;
        bool exercise = sk != null && (string)sk[0] == "fitness";
        string bodyNote = exercise ? BodyChange(Me, -0.04f, 0.03f) : food > 0 ? BodyChange(Me, food * 0.0012f, 0) : "";
        if (b.group != null) { foreach (var x in b.group) { Befriend(x.id, 6); x.meetup = null; } msg = "Great time with " + Names(b.group) + "! Friendships grew."; }
        if (b.friend != null) { Befriend(b.friend.id, 8); Bump("social", 8); msg += " " + b.friend.First + " had a great time with you."; }
        foreach (var pp in people) if (!pp.crowd && pp != b.friend && pp.at == b.p && D(pp, player) < 25) Befriend(pp.id, 3);
        msg += bodyNote; if (bodyDirty) { bodyDirty = false; player.look = MyLook(null); Dress(player); }
        busyEl.style.display = DisplayStyle.None; busy = null; ShowToast(msg); SaveLife(); UpdateHud(true);
    }
    // ---------------------------------------------------------------- talking
    string Say(string k) { var l = A(Get(data_town("LINES"), k)); return l.Count > 0 ? (string)Pick(l) : ""; }
    void TalkTo(TP pp) {
        if (busy != null || pp == null) return;
        talking = pp; pp.prevState = pp.state; if (pp.state != "sit") pp.state = "talk"; pp.waveT = 1.4f;
        float h = Hour; bool first = !(Rel(pp.id) > 0);
        string where = pp.at != null ? " at " + pp.at.name : "";
        string line = first ? "Hi! I’m " + pp.First + ". Nice to finally meet you" + where + "!" : h < 11 ? Say("morning") : h >= 20 || h < 3 ? Say("night") : Say("hi");
        if (first) Befriend(pp.id, 2);
        TalkMenu(pp, line);
    }
    void TalkMenu(TP pp, string line) {
        bool following = pp.prevState == "follow"; float r = Rel(pp.id); string him = pp.male ? "him" : "her";
        var opts = new List<Opt> {
            new Opt { label = "Chat", note = "+social", run = () => { Bump("social", 8); Befriend(pp.id, 3); lifeMin += 10; TalkMenu(pp, Say("chat")); } },
            new Opt { label = "How’s your day going?", note = "+social", run = () => { Bump("social", 5); Befriend(pp.id, 2); lifeMin += 5; TalkMenu(pp, DayLine(pp)); } },
            new Opt { label = "Any recommendations?", note = "somewhere to go", run = () => Recommend(pp) },
            new Opt { label = "More…", note = "jokes, fashion, a dance, a selfie, a gift", run = () => TalkMore(pp) },
            following ? new Opt { label = "Say goodbye", run = () => { pp.prevState = "idle"; CloseCard(); ShowToast(pp.First + " heads off. See you soon!"); } }
                : new Opt { label = "Want to hang out?", note = r < 15 ? "get to know " + him + " first" : (pp.male ? "he" : "she") + "’ll come with you", accent = r >= 15, run = () => {
                    if (r >= 15) { foreach (var x in people) if (x.state == "follow") x.state = "idle"; pp.prevState = "follow"; pp.seat = null; pp.mo.pose = "stand"; CloseCard(); ShowToast(pp.First + " is coming with you. Go somewhere fun!"); }
                    else { Befriend(pp.id, 1); TalkMenu(pp, Say("no")); } } },
        };
        OpenCard(pp.name, pp.role, line, opts, pp.id);
    }
    void TalkMore(TP pp) {
        string him = pp.male ? "him" : "her"; int gift = 10;
        void React(string emote, float secs) { pp.mo.emote = emote; pp.emoteFor = secs; }
        OpenCard(pp.name, pp.role, Pick(new[] { "So, what’s on your mind?", "I’m all ears!", "Go on…" }), new List<Opt> {
            new Opt { label = "Tell a joke", note = "+fun", run = () => { var jk = Pick(Jokes); bool land = R() < 0.75f; Bump("fun", land ? 8 : 3); Befriend(pp.id, land ? 3 : 1); lifeMin += 5;
                React(land ? "laugh" : "think", 4); TalkMenu(pp, "You: “" + jk.Item1 + "” " + jk.Item2 + " " + (land ? Pick(new[] { "Haha! Okay, that was good.", "Stop it, my sides hurt!", "I’m stealing that one." }) : Pick(new[] { "…I don’t get it.", "Oof. That was a groaner.", "Was that the punchline?" }))); } },
            new Opt { label = "Compliment " + (pp.male ? "his" : "her") + " look", note = "+charisma", run = () => { Befriend(pp.id, 2); AddSkill("charisma", 0.15f); Bump("social", 3); React("pose", 3); TalkMenu(pp, Say("compliment")); } },
            new Opt { label = "Talk fashion", note = "+charisma", run = () => { int sc = OutfitScore(); AddSkill("charisma", 0.1f); Befriend(pp.id, 2); lifeMin += 10;
                TalkMenu(pp, sc >= 75 ? "Honestly? Your look today is a ten. Where did you get that?" : sc >= 50 ? "I like your style! A statement piece would really finish it." : "Comfy is a vibe! The boutiques on Velvet Avenue have new pieces in, though."); } },
            new Opt { label = "Ask about Starlife Town", note = "local tips", run = () => { Befriend(pp.id, 1); lifeMin += 5; TalkMenu(pp, Pick(TownLines)); } },
            new Opt { label = "Dance together", note = "+fun +social", run = () => { CloseCard(); Bump("fun", 12); Bump("social", 8); Befriend(pp.id, 4); player.mo.emote = "dance"; React("dance", 10); ShowToast("You and " + pp.First + " dance right there on the street!"); } },
            new Opt { label = "Take a selfie together", note = "+fun", run = () => { CloseCard(); Bump("fun", 6); Befriend(pp.id, 2); player.mo.emote = "pose"; React("pose", 5); ShowToast("Say cheese! You and " + pp.First + " strike a pose."); } },
            new Opt { label = "Give " + him + " a little gift", note = gift + " coins · +friendship", disabled = profile.coins < gift, run = () => { core.Call("earn", (-gift).ToString(), "0"); Befriend(pp.id, 6); React("cheer", 3); UpdateHud(true); TalkMenu(pp, Pick(new[] { "For me? You’re so sweet!", "Aww, you didn’t have to! Thank you!", "This made my day, honestly." })); } },
            new Opt { label = "Back", run = () => TalkMenu(pp, Pick(new[] { "So…", "Anyway!", "What else?" })) } }, pp.id);
    }
    string DayLine(TP pp) {
        float h = Hour; string where = pp.at != null ? pp.at.name : "town";
        var l = new List<string> { "Pretty good! I just came from " + where + ".", "Busy! But " + where + " always cheers me up.", "Can’t complain. The weather’s lovely for a walk." };
        if (h < 11) l.AddRange(new[] { "Still waking up, honestly. I need a coffee from Greenwave.", "Early start! I love the town before it gets busy." });
        else if (h < 17) l.AddRange(new[] { "Lunch at Jollof & Co. was amazing. Have you been?", "I’ve been shopping on Velvet Avenue all afternoon." });
        else l.AddRange(new[] { "Winding down. Maybe dinner at Trattoria Bella later.", "Big night! Someone said the Neon Owl is packed tonight." });
        return Pick(l);
    }
    void Recommend(TP pp) {
        float h = Hour; var open = town.places.Where(p => p.id != "home" && p.id != "galleria" && !p.street && IsOpen(p, h)).ToList();
        var liked = open.Where(p => pp.crowd || Haunts(pp.id).Contains(p.id)).ToList(); var p0 = Pick(liked.Count > 0 ? liked : open);
        string why = Eateries.Any(e => e.id == p0.id) ? Pick(new[] { "The food there is unreal.", "Get the special, trust me.", "Best table in town." }) : Pick(new[] { "It’s my favourite spot.", "You’ll love it.", "Everyone’s talking about it." });
        Befriend(pp.id, 1);
        OpenCard(pp.name, pp.role, "Try " + p0.name + "! " + p0.sub + ". " + why + (p0.open != null ? " Open till " + HoursTxt(p0.open[0], p0.open[1]).Split('–')[1] + "." : ""), new List<Opt> {
            new Opt { label = "Show me the way", accent = true, run = () => { CloseCard(); SetWaypoint(new Waypoint { name = p0.name, x = p0.x, z = p0.z }); } },
            new Opt { label = "Something else?", run = () => Recommend(pp) },
            new Opt { label = "Thanks!", run = () => TalkMenu(pp, Pick(new[] { "Any time!", "Have fun!", "Tell me how it goes!" })) } }, pp.id);
    }
    static readonly (string, string)[] Jokes = {
        ("Why did the scarecrow win an award?", "Because he was outstanding in his field."), ("What do you call a fake noodle?", "An impasta."),
        ("Why don’t skeletons fight each other?", "They don’t have the guts."), ("What did the coffee say to the latte?", "You’re a-latte fun."),
        ("Why did the jollof go to the party?", "It heard it was the main dish."), ("What do you call a sleeping dinosaur?", "A dino-snore."),
        ("Why did the stylist get promoted?", "She always cut to the chase."), ("How do you organise a space party?", "You planet.") };
    static readonly string[] TownLines = {
        "Restaurant Row by the theatre is new. Big Bun does the best fries, and Sakura’s omakase is worth saving up for.",
        "The Grand Hall runs Fashion Week every evening. Get your collection ready!", "Étoile Motors lets you test-drive for free. I took the Comète out yesterday!",
        "Velvet Avenue has no cars, so it’s the nicest street for a stroll.", "Café Doré is fancy, but their Rose Cappuccino is worth it.",
        "The Galleria has the cinema, the arcade and the Glam Salon, all under one roof.", "Stop at the red lights when you drive. People do get told off!",
        "Central Park’s fountain is gorgeous at sunset.", "The Neon Owl only opens at eight, and the dance floor gets packed." };

    // ================================================================ Velvet Avenue's boutiques
    sealed class Shop { public SLTownWorld.Place p; public Dictionary<string, object> brand; public List<Dictionary<string, object>> items; public int? trying; public float yaw, pitch, dist; }
    Shop shop;
    HashSet<string> Lux() => new HashSet<string>(core.Pref("town.lux").Split(',').Where(s => s != ""));
    void OpenBoutique(SLTownWorld.Place p) {
        var b = O(Get(data_town("BRANDS"), p.id));
        var items = A(Get(b, "items")).Select(O).Where(it => { string id = Swap(S(Get(it, "item"))); return id != null && itemById.ContainsKey(id); }).ToList();
        shop = new Shop { p = p, brand = b, items = items, yaw = cam.yaw, pitch = cam.pitch, dist = cam.dist };
        modal = new Modal { title = "boutique" };
        // turn to face the camera as if in front of a mirror, and frame the whole outfit beside the panel
        player.ry = p.ry; cam.yaw = player.ry; cam.pitch = 0.06f; cam.dist = 3.9f; cam.focusY = 0.95f; cam.shift = 0.85f; cam.lastDrag = Time.time + 9999;
        RenderBoutique(); btq.style.display = DisplayStyle.Flex; prompt.style.display = DisplayStyle.None;
    }
    string LuxKey(int i) => shop.p.id + ":" + i;
    void RenderBoutique() {
        var lux = Lux(); btqName.text = S(Get(shop.brand, "name")); btqLine.text = S(Get(shop.brand, "line")); btqCoins.text = profile.coins + " coins";
        btqName.style.color = C(S(Get(shop.brand, "accent"), "#e2c47a"));
        btqGrid.Clear();
        for (int i = 0; i < shop.items.Count; i++) {
            var it = shop.items[i]; int idx = i; string id = Swap(S(Get(it, "item"))), col = S(Get(it, "color")); int price = (int)F(Get(it, "price"));
            bool own = lux.Contains(LuxKey(i)), wearing = player.look.worn.Any(w => w.item == id && ColorUtility.ToHtmlStringRGB(w.color) == ColorUtility.ToHtmlStringRGB(C(col)));
            var thumb = new SvgImage().Set("thumb:" + id + ":" + col, () => core.Call("thumb", id, col)); thumb.AddToClassList("pc-img"); thumb.pickingMode = PickingMode.Ignore;
            var tb = B("", "pc-thumb", () => TryPiece(idx)); tb.Add(thumb);
            var row = El("pc-row");
            if (own) { if (wearing) row.Add(L("Wearing ✓", "pc-own")); else row.Add(B("Wear", "pc-btn", () => WearOwned(idx))); }
            else { row.Add(L(price + " coins", "pc-price")); var buy = B("Buy", "pc-btn buy", () => BuyPiece(idx)); buy.SetEnabled(profile.coins >= price); row.Add(buy); }
            btqGrid.Add(El("piece" + (shop.trying == i ? " on" : ""), tb, L(S(Get(it, "name")), "pc-name"), row));
        }
        btqNote.text = shop.trying.HasValue && !lux.Contains(LuxKey(shop.trying.Value)) ? "Trying on the " + S(Get(shop.items[shop.trying.Value], "name")) + ". Buy it to keep it, or it comes off when you leave." : "Tap a piece to try it on. Everything you buy goes into your wardrobe in the suite too.";
    }
    static void WearOn(Look3 L, string slot, string item, Color c) {
        L.worn.RemoveAll(w => w.slot == slot);
        if (slot == "dress") L.worn.RemoveAll(w => w.slot == "top" || w.slot == "bottom");
        if (slot == "top" || slot == "bottom") L.worn.RemoveAll(w => w.slot == "dress");
        L.worn.Add((slot, item, c));
    }
    void TryPiece(int i) {
        shop.trying = i; var it = shop.items[i]; string id = Swap(S(Get(it, "item")));
        var L = MyLook(null); WearOn(L, itemById[id].slot, id, C(S(Get(it, "color")))); player.look = L; Dress(player); RenderBoutique();
    }
    void BuyPiece(int i) {
        var it = shop.items[i]; int price = (int)F(Get(it, "price")); string id = Swap(S(Get(it, "item"))), col = S(Get(it, "color"));
        if (profile.coins < price) { ShowToast("Not enough coins yet. Work, go to events and win contests to earn more."); return; }
        int fame = Mathf.RoundToInt(price / 15f);
        core.Call("earn", (-price).ToString(), fame.ToString());
        var lux = Lux(); lux.Add(LuxKey(i)); core.SetPref("town.lux", string.Join(",", lux));
        core.Call("wearColor", id, col); profile = core.Get<DProfile>("profile");
        shop.trying = null; player.look = MyLook(null); Dress(player); RenderBoutique(); UpdateHud(true);
        ShowToast("You bought the " + S(Get(it, "name")) + "! +" + fame + " fame. It’s in your wardrobe now.");
    }
    void WearOwned(int i) {
        var it = shop.items[i]; core.Call("wearColor", Swap(S(Get(it, "item"))), S(Get(it, "color")));
        shop.trying = null; player.look = MyLook(null); Dress(player); RenderBoutique();
    }
    void CloseBoutique() {
        if (shop == null) return;
        var keep = MyLook(null); if (keep.Key != player.look.Key) { player.look = keep; Dress(player); }
        cam.yaw = shop.yaw; cam.pitch = shop.pitch; cam.dist = shop.dist; cam.focusY = null; cam.shift = 0; cam.lastDrag = Time.time;
        shop = null; modal = null; btq.style.display = DisplayStyle.None;
    }

    // ---------------------------------------------------------------- Café Doré
    void OpenCoffee(SLTownWorld.Place p, string greet = null) {
        float h = Hour; bool open = IsOpen(p, h); var friend = people.FirstOrDefault(x => x.state == "follow");
        var menu = A(data_town("COFFEE")).Select(A).ToList();
        var opts = menu.Select(c => {
            string name = (string)c[0]; float cost = F(c[1]); var fx = O(c[2]).ToDictionary(kv => kv.Key, kv => (object)(F(kv.Value) + (kv.Key == "energy" ? 6 : 0) * 1.0));
            return new Opt { label = name, note = cost + " coins · sit in", disabled = !open || profile.coins < cost, run = () => DoActivity(p, Act(name, 25, fx, new Dictionary<string, object> { { "cost", (double)cost }, { "busy", "Sipping a " + name } })) };
        }).ToList();
        opts.Add(new Opt { label = "Order a coffee to go", note = "pick one, carry it with you", disabled = !open, run = () => OpenCard("Café Doré", "To go · 1 coin for the cup", "What can we get you?", menu.Select(c => new Opt { label = (string)c[0], note = (F(c[1]) + 1) + " coins", disabled = profile.coins < F(c[1]) + 1, run = () => CoffeeToGo(c) }).ToList()) });
        opts.Add(new Opt { label = "Sit on the terrace", run = () => SitAt("dore") });
        var pals = Pals(p);
        if (open && pals.Count > 0) opts.Insert(0, new Opt { label = "Coffee date with " + Names(pals), note = "45 min · +social +energy", accent = true, run = () => DoActivity(p, Act("Coffee together", 45, Fx(("social", 35), ("energy", 15), ("fun", 10)), new Dictionary<string, object> { { "cost", 7.0 }, { "busy", "Coffee with " + Names(pals) } }), pals) });
        if (open) opts.Add(new Opt { label = "Call your friends to meet here", note = "they’ll walk over", run = () => CallFriends(p) });
        OpenCard(p.name, p.sub + " · " + HoursTxt(p.open[0], p.open[1]), (greet != null ? greet + " " : "") + (!open ? "Closed right now. Opens at 7 AM." : "Marble counters, a brass espresso machine and the smell of fresh beans. " + (friend != null ? friend.First + " is with you." : "What are you having?")), opts);
    }
    void CoffeeToGo(List<object> c) {
        float cost = F(c[1]) + 1; if (profile.coins < cost) { ShowToast("Not enough coins."); return; }
        core.Call("earn", (-cost).ToString(CultureInfo.InvariantCulture), "0");
        foreach (var kv in O(c[2])) Bump(kv.Key, F(kv.Value));
        float food = O(c[2]).TryGetValue("hunger", out var hv) ? F(hv) : 0; if (food > 0) BodyChange(Me, food * 0.0012f, 0); if (bodyDirty) { bodyDirty = false; player.look = MyLook(null); Dress(player); }
        CupOn(player, true); coffeeT = 90; CloseCard(); UpdateHud(true);
        ShowToast(c[0] + " to go. Sip it while you stroll the Avenue!");
    }

    // ================================================================ waypoints
    sealed class Waypoint { public string name; public float x, z; public TP pp; }
    Waypoint waypoint;
    Vector2 WpPos() => waypoint.pp != null ? new Vector2(waypoint.pp.pos.x, waypoint.pp.pos.z) : new Vector2(waypoint.x, waypoint.z);
    void SetWaypoint(Waypoint w) { waypoint = w; compass.style.display = w != null ? DisplayStyle.Flex : DisplayStyle.None; if (w != null) ShowToast("Route set to " + w.name); }

    // ================================================================ the interface
    VisualElement hud, needsBox, prompt, cardEl, cardOpts, cardRel, busyEl, busyBar, btq, btqGrid, phone, phonePanes, compass, compassArrow, hint, tagLayer, mapBox;
    Label clockL, moodL, coinsL, promptL, promptSub, cardTitle, cardSub, cardSay, busyT, busyH, busyC, btqName, btqLine, btqCoins, btqNote, compassTxt; VisualElement moodDot;
    Image mapImg; Texture2D mapTex; float hintShown; string phoneTab = "map";
    void BuildTownUI() {
        townUI = El("town"); townUI.pickingMode = PickingMode.Ignore; root.Insert(root.IndexOf(overlayHost), townUI);
        tagLayer = El("town-tags"); tagLayer.pickingMode = PickingMode.Ignore; townUI.Add(tagLayer);
        // clock, mood, coins and needs
        hud = El("town-hud glass");
        hud.Add(El("t-row", clockL = L("", "t-clock"), El("t-mood", moodDot = El("t-dot"), moodL = L("")), coinsL = L("", "t-coins")));
        hud.Add(needsBox = El("t-needs")); townUI.Add(hud);
        hud.RegisterCallback<PointerEnterEvent>(_ => hudHover = true); hud.RegisterCallback<PointerLeaveEvent>(_ => hudHover = false);
        // top right: back to the suite
        townUI.Add(El("town-tr", B("My suite", "t-btn glass", () => LeaveTown("suite"))));
        // waypoint compass
        compass = El("town-compass glass", compassArrow = L("↑", "t-arrow"), compassTxt = L("", "t-ctext"), B("×", "t-x", () => SetWaypoint(null))); compass.style.display = DisplayStyle.None; townUI.Add(compass);
        // the prompt to interact
        prompt = El("town-prompt glass", L(Application.isMobilePlatform ? "Tap" : Input.GetJoystickNames().Any(n => !string.IsNullOrEmpty(n)) ? "A" : "E", "t-kbd"), promptL = L("", "t-ptext"), promptSub = L("", "t-psub")); prompt.RegisterCallback<ClickEvent>(_ => Interact()); prompt.style.display = DisplayStyle.None; townUI.Add(prompt);
        hint = L(Input.touchSupported && Application.isMobilePlatform ? "Drag on the left to walk · drag on the right to look · tap a starlet to talk" : Input.GetJoystickNames().Any(n => !string.IsNullOrEmpty(n)) ? "Left stick to walk · right stick to look · A to interact · B to go back · Y for your phone" : "WASD or arrow keys to walk · Shift to run · drag to look · E to interact · M for your phone", "town-hint glass"); hint.pickingMode = PickingMode.Ignore; townUI.Add(hint);
        // a card for places and conversations
        cardEl = El("town-card glass", cardTitle = L("", "t-ctitle"), cardSub = L("", "t-csub"), cardRel = El("t-rel"), cardSay = L("", "t-csay"), cardOpts = El("t-opts"), B("×", "t-close", CloseCard));
        cardEl.style.display = DisplayStyle.None; townUI.Add(cardEl);
        // the activity overlay
        busyEl = El("town-busy", El("t-bcol", busyT = L("", "t-bt"), busyH = L("", "t-bh"), busyC = L("", "t-bc"), El("t-bar big", busyBar = El("t-fill")))); busyEl.style.display = DisplayStyle.None; townUI.Add(busyEl);
        // boutique
        btq = El("town-btq glass", btqName = L("", "t-bname"), btqLine = L("", "t-bline"), El("t-btop", btqCoins = L("", "")), btqGrid = El("t-bgrid"), btqNote = L("", "t-bnote"), B("×", "t-close", CloseBoutique));
        var sc = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden }; sc.AddToClassList("t-bscroll"); btq.Insert(btq.IndexOf(btqGrid), sc); btqGrid.RemoveFromHierarchy(); sc.Add(btqGrid);
        btq.style.display = DisplayStyle.None; townUI.Add(btq);
        // phone
        townUI.Add(El("town-phonebtn", B("☰", "t-pbtn glass", () => TogglePhone(phone.style.display != DisplayStyle.Flex))));
        townUI.Add(El("town-emotebtn", B("☺", "t-pbtn glass", () => { if (modal == null) OpenEmotes(); else CloseCard(); })));
        var tabs = El("t-tabs");
        phonePanes = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden }; phonePanes.AddToClassList("t-ppane");
        phone = El("town-phone glass", tabs, phonePanes); phone.style.display = DisplayStyle.None; townUI.Add(phone);
        foreach (var (k, l) in new[] { ("map", "Map"), ("places", "Places"), ("friends", "Friends"), ("settings", "Settings") }) { string kk = k; tabs.Add(B(l, k == phoneTab ? "on" : null, () => { phoneTab = kk; foreach (var b in tabs.Children()) b.EnableInClassList("on", ((Button)b).text == l); RenderPhone(); })); }
        hintShown = Time.time + 14; UpdateHud(true);
    }
    bool hudHover; string hudSig = "";
    void UpdateHud(bool force) {
        if (townUI == null) return;
        var (mw, mc) = Mood(); string sig = ClockLabel() + mw + profile.coins + string.Join(",", needs.Select(n => Mathf.RoundToInt(n))) + hudHover + (phone.style.display == DisplayStyle.Flex);
        if (sig == hudSig && !force) return; hudSig = sig;
        clockL.text = ClockLabel(); moodL.text = mw; moodDot.style.backgroundColor = C(mc); coinsL.text = profile.coins + " coins";
        bool open = hudHover || needs.Any(n => n < 30) || phone.style.display == DisplayStyle.Flex;
        needsBox.Clear(); needsBox.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        for (int i = 0; i < 5; i++) { var fill = El("t-fill" + (needs[i] < 30 ? " low" : "")); fill.style.width = Length.Percent(needs[i]); needsBox.Add(El("t-nrow", L(NeedNames[i], "t-nlab"), El("t-bar", fill))); }
    }
    void HideHint() { hintShown = Mathf.Min(hintShown, Time.time + 6); }
    void OpenCard(string title, string sub, string say, List<Opt> opts, string relId = null) {
        modal = new Modal { title = title, opts = opts, relId = relId };
        cardTitle.text = title; cardSub.text = sub; cardSay.text = say;
        cardRel.Clear(); cardRel.style.display = relId != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (relId != null) { float r = Rel(relId); var fill = El("t-fill"); fill.style.width = Length.Percent(r); cardRel.Add(L(RelLabel(r), "t-rlab")); cardRel.Add(El("t-bar grow", fill)); }
        cardOpts.Clear();
        foreach (var o in opts) {
            var oo = o; var b = B("", "t-opt" + (o.accent ? " accent" : ""), () => { if (!oo.disabled) oo.run?.Invoke(); });
            b.Add(L(o.label, "t-olab")); if (!string.IsNullOrEmpty(o.note)) b.Add(L(o.note, "t-onote")); b.SetEnabled(!o.disabled); cardOpts.Add(b);
        }
        cardEl.style.display = DisplayStyle.Flex; prompt.style.display = DisplayStyle.None;
        if (padUsed) { var first = cardOpts.Children().FirstOrDefault(c => c.enabledSelf); first?.schedule.Execute(() => first.Focus()); }
    }
    int cardClosedAt;
    void CloseCard() {
        cardClosedAt = Time.frameCount;
        if (talking != null) { talking.state = talking.prevState == "follow" || talking.prevState == "sit" ? talking.prevState : "idle"; talking.timer = 8; talking = null; }
        if (shop == null) modal = null; if (cardEl != null) cardEl.style.display = DisplayStyle.None;
    }
    // name tags over the starlets nearby
    void UpdateTags() {
        var pnl = root.panel;
        foreach (var pp in people) {
            if (pp.crowd) continue;
            if (pp.tag == null) { pp.tag = El("t-tag", L(pp.First, "t-tagname"), L(pp.role, "t-tagsub")); pp.tag.pickingMode = PickingMode.Ignore; foreach (var c in pp.tag.Children()) c.pickingMode = PickingMode.Ignore; tagLayer.Add(pp.tag); }
            float d = D(pp, player); var w = SLTownWorld.W(pp.pos.x, pp.pos.y + 2.12f * pp.look.height, pp.pos.z); var s = tcam.WorldToScreenPoint(w);
            bool show = pp.visible && d < 14 && d > 2.6f && busy == null && !showRunning && s.z > 0 && pnl != null;
            pp.tag.style.display = show ? DisplayStyle.Flex : DisplayStyle.None; if (!show) continue;
            var pos = RuntimePanelUtils.ScreenToPanel(pnl, new Vector2(s.x, Screen.height - s.y));
            pp.tag.style.left = pos.x - 60; pp.tag.style.top = pos.y - 34; pp.tag.style.opacity = Mathf.Clamp01((14 - d) / 4);
        }
    }

    // ---------------------------------------------------------------- the phone: map, places, friends, settings
    void TogglePhone(bool on) { phone.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; if (on) RenderPhone(); UpdateHud(true); }
    void RenderPhone() {
        phonePanes.Clear(); float h = Hour;
        if (phoneTab == "map") {
            mapBox = El("t-map"); mapImg = new Image { scaleMode = ScaleMode.StretchToFill }; mapImg.style.flexGrow = 1; mapBox.Add(mapImg); phonePanes.Add(mapBox);
            mapImg.RegisterCallback<ClickEvent>(e => {
                float sz = mapImg.resolvedStyle.width; if (sz <= 0) return; float x = e.localPosition.x / sz * 600, y = e.localPosition.y / sz * 600;
                SLTownWorld.Place best = null; float bd = 30; foreach (var p in town.places) { var m = ToMap(p.x, p.z); float d = (m - new Vector2(x, y)).magnitude; if (d < bd) { bd = d; best = p; } }
                if (best != null) { SetWaypoint(new Waypoint { name = best.name, x = best.x, z = best.z }); TogglePhone(false); }
            });
            phonePanes.Add(L("Tap a place on the map to set a route.", "t-note")); DrawMap();
        } else if (phoneTab == "places") {
            foreach (var p in town.places.Where(p => !p.mall)) { var pp = p; var b = B("", "t-place", () => { SetWaypoint(new Waypoint { name = pp.name, x = pp.x, z = pp.z }); TogglePhone(false); }); var dot = El("t-pdot"); dot.style.backgroundColor = C(p.color); b.Add(dot); b.Add(L(p.name, "t-pname")); b.Add(L(IsOpen(p, h) ? p.sub : "closed", "t-psub2")); phonePanes.Add(b); }
        } else if (phoneTab == "friends") {
            foreach (var pp in people.Where(q => !q.crowd)) {
                var x = pp; float r = Rel(pp.id);
                var nearP = town.places.Select(p => (p, d: new Vector2(p.x - pp.pos.x, p.z - pp.pos.z).magnitude)).Where(t => t.d < 30).OrderBy(t => t.d).Select(t => t.p).FirstOrDefault();
                string where = pp.state == "inside" ? "at home" : pp.state == "follow" ? "with you" : nearP != null ? "near " + nearP.name : "out and about";
                var b = B("", "t-place", () => { if (x.state == "inside") { ShowToast(x.First + " is at home asleep."); return; } SetWaypoint(new Waypoint { name = x.First, pp = x }); TogglePhone(false); });
                var fill = El("t-fill"); fill.style.width = Length.Percent(r);
                b.Add(El("t-pcol", L(pp.name, "t-pname"), L(where, "t-psub2"))); b.Add(El("t-pcol r", El("t-bar small", fill), L(RelLabel(r), "t-psub2"))); phonePanes.Add(b);
            }
        } else {
            phonePanes.Add(L("GAME SPEED", "t-lbl"));
            var seg = El("t-seg"); foreach (var (v, l) in new[] { (0, "Pause"), (1, "Normal"), (4, "Fast") }) { int vv = v; seg.Add(B(l, tSpeed == v ? "on" : null, () => { tSpeed = vv; SaveLife(); RenderPhone(); })); } phonePanes.Add(seg);
            phonePanes.Add(L("One real second is one minute in town at normal speed. Sitting down makes time pass three times as fast.", "t-note"));
            phonePanes.Add(L("SKILLS", "t-lbl"));
            for (int i = 0; i < 4; i++) { var fill = El("t-fill"); fill.style.width = Length.Percent(skills[i] * 10); phonePanes.Add(El("t-nrow", L(char.ToUpper(SkillKeys[i][0]) + SkillKeys[i].Substring(1), "t-nlab"), El("t-bar", fill))); }
            phonePanes.Add(L("HOME", "t-lbl")); phonePanes.Add(B("Back to my suite", "t-opt", () => LeaveTown("suite")));
        }
    }
    Vector2 ToMap(float x, float z) { float s = 600f / (2 * town.Edge + 16); return new Vector2(300 + x * s, 300 + z * s); }
    void DrawMap() {
        if (mapImg == null || phoneTab != "map") return;
        if (mapTex == null) { mapTex = new Texture2D(600, 600, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear }; }
        var px = new Color32[600 * 600]; var bg = new Color32(0x2a, 0x2c, 0x34, 255); for (int i = 0; i < px.Length; i++) px[i] = bg;
        void Box(Vector2 a, Vector2 b, Color32 c) { int x0 = Mathf.Clamp((int)a.x, 0, 599), x1 = Mathf.Clamp((int)b.x, 0, 599), y0 = Mathf.Clamp((int)a.y, 0, 599), y1 = Mathf.Clamp((int)b.y, 0, 599); for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) px[(599 - y) * 600 + x] = c; }
        void Disc(Vector2 m, float r, Color32 c, float inner = -1) { for (int y = (int)(m.y - r - 1); y <= m.y + r + 1; y++) for (int x = (int)(m.x - r - 1); x <= m.x + r + 1; x++) { if (x < 0 || y < 0 || x > 599 || y > 599) continue; float d = (new Vector2(x, y) - m).magnitude; if (d <= r && d >= inner) px[(599 - y) * 600 + x] = c; } }
        var lands = new HashSet<string>(A(data_town("LAND")).Select(o => (string)o)); var parks = O(data_town("PARKS"));
        for (int i = -town.N; i <= town.N; i++) for (int j = -town.N; j <= town.N; j++) {
            string key = i + "," + j; var col = parks.ContainsKey(key) ? new Color32(0x3f, 0x6b, 0x35, 255) : lands.Contains(key) ? new Color32(0x4a, 0x46, 0x52, 255) : new Color32(0x3c, 0x3d, 0x45, 255);
            Box(ToMap(i * town.Cell - town.Half, j * town.Cell - town.Half), ToMap(i * town.Cell + town.Half, j * town.Cell + town.Half), col);
        }
        var ave = O(data_town("AVE")); Box(ToMap(F(ave["x0"]), F(ave["z0"])), ToMap(F(ave["x1"]), F(ave["z1"])), new Color32(0x8a, 0x73, 0x40, 255));
        var mall = O(data_town("MALL")); Box(ToMap(F(mall["cx"]) - town.Half, -town.Half), ToMap(F(mall["cx"]) + town.Half, town.Half), new Color32(0x6a, 0x60, 0x70, 255));
        foreach (var p in town.places) Disc(ToMap(p.x, p.z), 6, (Color32)C(p.color));
        foreach (var pp in people) if (pp.visible && !pp.crowd) Disc(ToMap(pp.pos.x, pp.pos.z), 3.5f, new Color32(0xff, 0xd1, 0xe1, 255));
        if (waypoint != null) { var w = WpPos(); Disc(ToMap(w.x, w.y), 12, new Color32(0xff, 0x4f, 0x8b, 255), 9); }
        // you: an arrow pointing where you face
        var mp = MapPos(); var me = ToMap(mp.x, mp.y); float a = player.ry; var fwd = new Vector2(Mathf.Sin(a), Mathf.Cos(a)); var side = new Vector2(fwd.y, -fwd.x);
        Vector2 tip = me + fwd * 11, l = me - fwd * 8 + side * 7, r = me - fwd * 8 - side * 7;
        for (int y = (int)me.y - 13; y <= me.y + 13; y++) for (int x = (int)me.x - 13; x <= me.x + 13; x++) { if (x < 0 || y < 0 || x > 599 || y > 599) continue; var q = new Vector2(x, y); if (InTri(q, tip, l, me) || InTri(q, tip, me, r)) px[(599 - y) * 600 + x] = new Color32(255, 255, 255, 255); }
        mapTex.SetPixels32(px); mapTex.Apply(false); mapImg.image = mapTex;
        // place names, skipping any that would overlap
        foreach (var lab in mapBox.Children().Where(c => c is Label).ToList()) lab.RemoveFromHierarchy();
        float k = mapBox.resolvedStyle.width > 0 ? mapBox.resolvedStyle.width / 600f : 316f / 600f; var boxes = new List<Rect>();
        foreach (var p in town.places) {
            var m = ToMap(p.x, p.z); float w = (p.name.Length * 6.6f + 6) * 1.2f;
            foreach (float ly in new[] { m.y - 24, m.y + 10 }) { var rr = new Rect(m.x - w / 2, ly, w, 16); if (boxes.Any(o => o.Overlaps(rr))) continue; boxes.Add(rr); var lab = L(p.name, "t-maplab"); lab.pickingMode = PickingMode.Ignore; lab.style.left = rr.x * k; lab.style.top = rr.y * k; lab.style.width = rr.width * k; mapBox.Add(lab); break; }
        }
    }
    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c) { float s(Vector2 u, Vector2 v, Vector2 w) => (u.x - w.x) * (v.y - w.y) - (v.x - w.x) * (u.y - w.y); float d1 = s(p, a, b), d2 = s(p, b, c), d3 = s(p, c, a); bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0; return !(neg && pos); }
}
