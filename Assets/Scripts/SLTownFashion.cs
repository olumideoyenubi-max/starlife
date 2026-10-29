// Fashion Week at the Grand Hall: design a four-look collection from the pieces you own (a theme, then a hero piece per
// look, completed from your wardrobe), cast starlets as your models, and show it on a runway built in front of the
// hall: each model walks it in your look, poses and turns, the town gathers along the sides, and you take the bow.
// Three critics review it; the score pays fame and coins. Part of SLApp.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class SLApp {
    static readonly (string name, string blurb, string[] tags, string colour)[] ShowThemes = {
        ("Evening Glamour", "Gowns, sparkle and red-carpet drama.", new[] { "glam", "redcarpet", "night" }, "#a3122e"),
        ("Ankara Summer", "Bold prints, warm colours, easy shapes.", new[] { "prints", "summer", "casual" }, "#e0782f"),
        ("City Chic", "Sharp tailoring and clean lines.", new[] { "office", "chic" }, "#24407a"),
        ("Street Edge", "Leather, crops and attitude.", new[] { "edgy", "casual", "night" }, "#151418") };
    sealed class Show { public int theme; public readonly List<List<(string slot, string item, Color color)>> looks = new List<List<(string, string, Color)>>(); public readonly List<string> heroes = new List<string>(); }
    Show show; bool showRunning;

    static readonly string[] ClothingSlots = { "dress", "top", "bottom", "outer", "shoes" };
    List<DItem> OwnedClothes() => data.items.Where(i => Owns(i.id) && ClothingSlots.Contains(i.slot)).ToList();
    bool ThemeFits(DItem it, int theme) => it.tags != null && it.tags.Intersect(ShowThemes[theme].tags).Any() || ShowThemes[theme].tags.Contains(it.cat);
    Color ItemColour(DItem it) { string c = core.Call("get", "colors." + it.id); return C(c != "" ? c : it.color); }

    // ---------------------------------------------------------------- designing
    void OpenFashionWeek() {
        var owned = OwnedClothes();
        if (owned.Count(i => i.slot == "dress" || i.slot == "top") < 4) { OpenCard("Fashion Week", "Grand Hall · evenings", "You need at least four dresses or tops of your own to show a collection. Shop on Velvet Avenue or in the suite, then come back.", new List<Opt>()); return; }
        show = new Show();
        var opts = new List<Opt>();
        for (int t = 0; t < ShowThemes.Length; t++) { int tt = t; int fits = owned.Count(i => ThemeFits(i, tt)); opts.Add(new Opt { label = ShowThemes[t].name, note = fits + " of your pieces fit", run = () => { show.theme = tt; PickHero(0); } }); }
        OpenCard("Fashion Week", "Grand Hall · your collection", "Tonight the runway is yours. Choose a theme for your collection: the critics judge how well every look carries it.", opts);
    }
    void PickHero(int k) {
        if (k >= 4) { CastAndGo(); return; }
        var owned = OwnedClothes().Where(i => (i.slot == "dress" || i.slot == "top" || i.slot == "outer") && !show.heroes.Contains(i.id))
            .OrderByDescending(i => ThemeFits(i, show.theme)).ThenByDescending(i => i.price).Take(10).ToList();
        var opts = owned.Select(it => { var ii = it; return new Opt { label = it.name, note = ThemeFits(it, show.theme) ? "★ on theme" : it.slot, accent = ThemeFits(it, show.theme), run = () => { show.heroes.Add(ii.id); show.looks.Add(CompleteLook(ii)); PickHero(k + 1); } }; }).ToList();
        OpenCard("Look " + (k + 1) + " of 4", ShowThemes[show.theme].name, "Choose the hero piece. The rest of the look is styled from your wardrobe to match.", opts);
    }
    List<(string, string, Color)> CompleteLook(DItem hero) {
        var look = new List<(string, string, Color)> { (hero.slot, hero.id, ItemColour(hero)) };
        var owned = OwnedClothes();
        DItem Best(string slot) => owned.Where(i => i.slot == slot && !show.looks.SelectMany(l => l).Any(w => w.Item2 == i.id && slot != "shoes")).OrderByDescending(i => ThemeFits(i, show.theme)).ThenByDescending(i => i.price).FirstOrDefault();
        if (hero.slot == "top" || hero.slot == "outer") { var b = Best("bottom"); if (b != null) look.Add(("bottom", b.id, ItemColour(b))); }
        if (hero.slot == "outer") { var t = Best("top"); if (t != null) look.Add(("top", t.id, ItemColour(t))); }
        var s = owned.Where(i => i.slot == "shoes").OrderByDescending(i => ThemeFits(i, show.theme)).ThenByDescending(i => i.price).FirstOrDefault();
        if (s != null) look.Add(("shoes", s.id, ItemColour(s)));
        return look;
    }
    void CastAndGo() {
        var models = people.Where(p => !p.crowd && !p.male).OrderByDescending(p => Rel(p.id)).ThenBy(p => p.id).Take(4).Select(p => p.id).ToList();
        while (models.Count < 4) models.Add(new[] { "aria", "zuri", "mina", "luz" }[models.Count]);
        string names = string.Join(", ", models.Select(m => data.starlets.FirstOrDefault(s => s.id == m)?.name.Split(' ')[0] ?? m));
        OpenCard("Your models", ShowThemes[show.theme].name, "Walking tonight: " + names + ". The front row is full and the photographers are ready.", new List<Opt> {
            new Opt { label = "Start the show", note = "about a minute", accent = true, run = () => { CloseCard(); StartCoroutine(RunShow(models)); } },
            new Opt { label = "Not tonight", run = () => { show = null; CloseCard(); } } });
    }

    // ---------------------------------------------------------------- the show
    IEnumerator RunShow(List<string> models) {
        showRunning = true; modal = new Modal { title = "show" }; prompt.style.display = DisplayStyle.None; hud.style.display = DisplayStyle.None;
        player.go.gameObject.SetActive(false);      // the designer waits backstage until the bow
        var hall = PlaceBy("hall"); var f = new Vector2(Mathf.Sin(hall.ry), Mathf.Cos(hall.ry)); var side = new Vector2(f.y, -f.x);
        Vector2 a = new Vector2(hall.x, hall.z) + f * 2.4f, b = a + f * 15f; const float H = 0.5f;
        float ry = Mathf.Atan2(f.x, f.y);
        // the runway: a lacquered catwalk with a glowing edge and spotlights over it
        var stage = new GameObject("Fashion Week").transform; stage.SetParent(town.root, false);
        var lit = Resources.Load<Shader>("SLTown");
        GameObject Block(Vector2 c, float len, float wid, float y0, float y1, Color col, float gloss, Color? glow = null) {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(g.GetComponent<Collider>()); g.transform.SetParent(stage, false);
            g.transform.position = SLTownWorld.W(c.x, (y0 + y1) / 2, c.y); g.transform.rotation = SLTownWorld.Yaw(ry); g.transform.localScale = new Vector3(wid, y1 - y0, len);
            var m = new Material(lit); m.SetColor("_Color", col); m.SetFloat("_Glossiness", gloss); if (glow.HasValue) { m.SetColor("_EmissionColor", glow.Value); m.SetFloat("_EmA", 1); }
            g.GetComponent<Renderer>().sharedMaterial = m; return g;
        }
        Block((a + b) / 2, 15, 2.4f, 0, H, new Color(0.96f, 0.95f, 0.97f), 0.9f);
        foreach (int s2 in new[] { -1, 1 }) Block((a + b) / 2 + side * (1.22f * s2), 15, 0.05f, H - 0.06f, H + 0.01f, Color.white, 0.5f, new Color(1f, 0.85f, 0.95f) * 1.4f);
        Block(a - f * 0.6f, 1.2f, 4.5f, 0, 3.6f, new Color(0.1f, 0.08f, 0.12f), 0.3f);     // the backdrop the models come out from
        var spots = new List<Light>();
        for (int i = 0; i < 4; i++) {
            var l = new GameObject("Runway spot").AddComponent<Light>(); l.transform.SetParent(stage, false); l.type = LightType.Spot; l.range = 14; l.spotAngle = 40; l.intensity = 3.2f; l.color = new Color(1f, 0.96f, 0.92f);
            var p = Vector2.Lerp(a, b, (i + 0.5f) / 4); l.transform.position = SLTownWorld.W(p.x, 6.5f, p.y); l.transform.rotation = Quaternion.LookRotation(Vector3.down + SLTownWorld.W(f.x, 0, f.y) * 0.15f); l.cullingMask = 1 << SLTownWorld.Layer; spots.Add(l);
        }
        SLTownWorld.SetLayer(stage);
        // the town gathers along both sides; traffic waits
        var crowd = people.Where(p => p.visible && p.state != "inside" && p.state != "talk").Take(18).ToList(); var saved = crowd.Select(p => (p, p.pos, p.state)).ToList();
        for (int i = 0; i < crowd.Count; i++) {
            var pp = crowd[i]; int s3 = i % 2 == 0 ? 1 : -1; var at = a + f * (3 + (i / 2) * 1.5f) + side * (s3 * (2.4f + (i % 4 < 2 ? 0 : 0.9f)));
            pp.pos = new Vector3(at.x, 0, at.y); pp.state = "watch"; pp.speed = 0; pp.ry = Mathf.Atan2(-side.x * s3, -side.y * s3); pp.path.Clear(); Place(pp);
        }
        trafficPaused = true; foreach (var c in cars) if (c.traffic && (new Vector2(c.x, c.z) - (a + b) / 2).magnitude < 20) c.v = 0;
        // the camera at the end of the runway, where the photographers are
        showCam = true; var camAt = b + f * 2.6f + side * 1.1f;      // a little to the side, clear of the street lamps
        tcam.transform.position = SLTownWorld.W(camAt.x, 2.1f, camAt.y); tcam.transform.LookAt(SLTownWorld.W(((a + b) / 2).x, 1.1f, ((a + b) / 2).y)); tcam.fieldOfView = 36;
        ShowToast(ShowThemes[show.theme].name + " — the lights go down…");
        for (float t = 0; t < 1.8f; t += Time.deltaTime) yield return null;
        // each look: out to the end, a pose, a turn, back
        var walkers = new List<TP>();
        for (int k = 0; k < 4; k++) {
            var look = SigLook(models[k]); look.signature = false; look.worn.Clear(); look.worn.AddRange(show.looks[k]);
            var m = Spawn("model" + k, data.starlets.FirstOrDefault(s => s.id == models[k]), look); m.crowd = true; walkers.Add(m);
            yield return StartCoroutine(Walk(m, a, b, H, ry, true));
        }
        // the finale: everyone walks out together, then you take your bow
        for (int k = 0; k < 4; k++) { walkers[k].go.gameObject.SetActive(true); }
        for (float t = 0; t < 4.2f; t += Time.deltaTime) {
            for (int k = 0; k < 4; k++) { float d = Mathf.Clamp(t * 1.25f - k * 1.6f, 0, 9 - k * 1.1f); var p = a + f * d; walkers[k].pos = new Vector3(p.x, H, p.y); walkers[k].ry = ry; Place(walkers[k]); walkers[k].mo.Animate(Time.deltaTime, d < 9 - k * 1.1f && d > 0 ? 1.25f : 0); }
            yield return null;
        }
        player.go.gameObject.SetActive(true); var savedPos = player.pos; float savedRy = player.ry;
        for (float t = 0; t < 4f; t += Time.deltaTime) {
            float d = Mathf.Min(t * 1.3f, 12.5f); var p = a + f * d; player.pos = new Vector3(p.x, H, p.y); player.ry = ry; Place(player);
            player.mo.pose = d >= 12.5f ? "wave" : "stand"; player.mo.Animate(Time.deltaTime, d < 12.5f ? 1.3f : 0);
            for (int k = 0; k < 4; k++) walkers[k].mo.Animate(Time.deltaTime, 0);
            yield return null;
        }
        player.mo.pose = "stand";
        // the reviews
        int score = ScoreShow(); int fame = Mathf.RoundToInt(score / 4f) + 5, coins = score * 2;
        core.Call("earn", coins.ToString(), fame.ToString()); profile = core.Get<DProfile>("profile");
        int shows = (int)Num0(core.Pref("town.shows"), 0) + 1; core.SetPref("town.shows", shows.ToString());
        int best = (int)Num0(core.Pref("town.showbest"), 0); if (score > best) core.SetPref("town.showbest", score.ToString());
        foreach (var w in walkers) Destroy(w.go.gameObject);
        Destroy(stage.gameObject); showCam = false; trafficPaused = false; tcam.fieldOfView = 55;
        foreach (var (pp, pos, st) in saved) { pp.pos = pos; pp.state = st == "talk" ? "idle" : st; Place(pp); }
        foreach (var pp in crowd) if (pp.state == "watch") { pp.state = "idle"; pp.timer = 4; }
        player.pos = savedPos; player.ry = savedRy; Place(player); cam.cur = 0;
        hud.style.display = DisplayStyle.Flex; showRunning = false; modal = null;
        OpenCard("The reviews are in", ShowThemes[show.theme].name + " · " + score + "/100", Review(score), new List<Opt> {
            new Opt { label = "Celebrate", note = "+" + coins + " coins · +" + fame + " fame", accent = true, run = () => { Bump("fun", 20); Bump("social", 15); CloseCard(); ShowToast("Show number " + shows + (score > best ? ", your best yet!" : ".")); } } });
        show = null;
    }
    IEnumerator Walk(TP m, Vector2 a, Vector2 b, float h, float ry, bool turn) {
        float len = (b - a).magnitude; var f = (b - a) / len; float d = 0;
        while (d < len) { d = Mathf.Min(len, d + 1.25f * Time.deltaTime); var p = a + f * d; m.pos = new Vector3(p.x, h, p.y); m.ry = ry; Place(m); m.mo.Animate(Time.deltaTime, 1.25f); yield return null; }
        for (float t = 0; t < 1.6f; t += Time.deltaTime) { m.mo.Animate(Time.deltaTime, 0); m.ry = ry + Mathf.Sin(t * 2) * 0.35f; Place(m); yield return null; }     // a pose for the cameras
        for (float t = 0; t < 0.6f; t += Time.deltaTime) { m.ry = Mathf.LerpAngle(ry * Mathf.Rad2Deg, ry * Mathf.Rad2Deg + 180, t / 0.6f) * Mathf.Deg2Rad; Place(m); m.mo.Animate(Time.deltaTime, 0.6f); yield return null; }
        while (d > 0) { d = Mathf.Max(0, d - 1.25f * Time.deltaTime); var p = a + f * d; m.pos = new Vector3(p.x, h, p.y); m.ry = ry + Mathf.PI; Place(m); m.mo.Animate(Time.deltaTime, 1.25f); yield return null; }
        m.go.gameObject.SetActive(false);
    }
    int ScoreShow() {
        float total = 0; var heroes = new HashSet<string>();
        foreach (var look in show.looks) {
            var items = look.Select(w => itemById.TryGetValue(w.Item2, out var it) ? it : null).Where(i => i != null).ToList();
            float onTheme = items.Count(i => ThemeFits(i, show.theme)) / (float)Mathf.Max(1, items.Count);
            bool whole = items.Any(i => i.slot == "dress") || items.Any(i => i.slot == "top" || i.slot == "outer") && items.Any(i => i.slot == "bottom");
            bool shoes = items.Any(i => i.slot == "shoes");
            float hues = look.Select(w => { Color.RGBToHSV(w.Item3, out float hh, out float ss, out _); return ss < 0.2f ? -1 : Mathf.Round(hh * 6); }).Distinct().Count();
            total += 45 * onTheme + (whole ? 20 : 0) + (shoes ? 10 : 0) + (hues <= 3 ? 15 : 7) + Mathf.Min(10, items.Sum(i => i.price) / 30f);
            heroes.Add(items.FirstOrDefault()?.cat ?? "");
        }
        return Mathf.Clamp(Mathf.RoundToInt(total / show.looks.Count) + (heroes.Count >= 3 ? 5 : 0), 0, 100);
    }
    static string Review(int s) {
        string a = s >= 85 ? "“A triumph. Every look said the same thing, beautifully.” — Vogue Starlife" : s >= 70 ? "“Confident and polished. A designer to watch.” — The Avenue Review" : s >= 50 ? "“Some lovely moments, but the story wandered.” — The Avenue Review" : "“The theme got lost somewhere between looks two and three.” — Vogue Starlife";
        string b = s >= 80 ? "“The front row stood up. I haven't seen that since the Met Ball.” — Fizz Weekly" : s >= 60 ? "“The finale had the whole street cheering.” — Fizz Weekly" : "“Bold choices. Not all of them landed.” — Fizz Weekly";
        string c = s >= 75 ? "“Sold out by morning, if I had to bet.” — Starlife Business" : "“Next season will be the one.” — Starlife Business";
        return a + "\n" + b + "\n" + c;
    }
}
