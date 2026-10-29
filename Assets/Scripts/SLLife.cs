// Starlife's life outside the suite: careers (star careers and everyday jobs, which decide the wardrobe), the gym,
// underwear, the avatar studio, and dragging clothes onto the starlet. Part of SLApp.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

public partial class SLApp {
    // ================================================================ settings kept with the save, per starlet
    string P(string key, string def = "") => core.Pref(key + "." + Av, def);
    void SetP(string key, string value) { core.SetPref(key + "." + Av, value); dirty = true; }
    static float Num(string s, float def = 1) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
    static string Today => DateTime.Now.ToString("yyyyMMdd");
    static string Json(IEnumerable<string> ids) => "[" + string.Join(",", new List<string>(ids).ConvertAll(s => "\"" + s + "\"")) + "]";

    /// <summary>How far each build control goes: hips, bust and bum can go well past "as she came" for a curvy figure.</summary>
    static float ShapeMax(int i) => i == 3 || i == 4 || i == 7 ? 1.6f : 1f;
    /// <summary>A starlet as she has been set up in the avatar studio and the salon, in her signature outfit.</summary>
    Look3 SigLook(string sid) {
        string face = core.Pref("face." + sid, sid == "custom" ? "lina" : sid), body = core.Pref("body." + sid, sid == "custom" ? "jenny" : sid);
        if (stage3d != null && !stage3d.Has(face)) face = "aria";
        var L = new Look3 { id = face, body = body == face ? null : body, height = Num(core.Pref("height." + sid, "1")) };
        var sv = core.Pref("shape." + sid, "").Split(','); for (int i = 0; i < L.shape.Length && i < sv.Length; i++) L.shape[i] = Mathf.Clamp(Num(sv[i], 0), -1, ShapeMax(i));
        string h = Hair3D(sid); L.hair = h == "" ? null : h;
        WearMakeup(L, sid);
        return L;
    }

    // ================================================================ dragging clothes
    /// <summary>Makes a wardrobe card draggable: drop it where drop() says yes to wear it. A plain click still works.</summary>
    void DragToWear(VisualElement card, DItem it, Func<Vector2, bool> drop, Action wear) {
        Vector2 start = Vector2.zero; bool down = false, dragging = false; VisualElement ghost = null;
        card.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; down = true; dragging = false; start = e.position; card.CapturePointer(e.pointerId); });
        card.RegisterCallback<PointerMoveEvent>(e => {
            if (!down || !card.HasPointerCapture(e.pointerId)) return;
            if (!dragging && ((Vector2)e.position - start).magnitude > 9) {
                dragging = true; ghost = Thumb(it, true); ghost.pickingMode = PickingMode.Ignore;
                ghost.style.position = Position.Absolute; ghost.style.width = 96; ghost.style.height = 100; ghost.style.opacity = 0.92f; overlayHost.Add(ghost);
            }
            if (dragging) { Vector2 p = overlayHost.WorldToLocal(e.position); ghost.style.left = p.x - 48; ghost.style.top = p.y - 50; ghost.style.scale = new Scale(Vector3.one * (drop(e.position) ? 1.25f : 1f)); }
        });
        void End(Vector2 pos, int pointer, bool cancel) {
            if (!down) return; down = false; if (card.HasPointerCapture(pointer)) card.ReleasePointer(pointer);
            bool was = dragging; dragging = false; if (ghost != null) { ghost.RemoveFromHierarchy(); ghost = null; }
            if (cancel) return;
            if (!was || drop(pos)) wear();
        }
        card.RegisterCallback<PointerUpEvent>(e => End(e.position, e.pointerId, false));
        card.RegisterCallback<PointerCancelEvent>(e => End(e.position, e.pointerId, true));
    }
    /// <summary>With the wardrobe open, a piece can be pulled off the starlet: press on it, drag it away from her, let go.</summary>
    void DragOff(VisualElement doll) {
        bool down = false, dragging = false; string slot = null; VisualElement ghost = null; Vector2 start = Vector2.zero;
        doll.RegisterCallback<PointerDownEvent>(e => {
            if (drawer != "wardrobe" || e.button != 0) return;
            slot = SlotAt(e.localPosition.y / Mathf.Max(1, doll.resolvedStyle.height)); if (slot == null) return;
            down = true; dragging = false; start = e.position; doll.CapturePointer(e.pointerId); e.StopPropagation();
        });
        doll.RegisterCallback<PointerMoveEvent>(e => {
            if (!down || !doll.HasPointerCapture(e.pointerId)) return;
            if (!dragging && ((Vector2)e.position - start).magnitude > 9 && itemById.TryGetValue(Worn(slot), out var it)) {
                dragging = true; ghost = Thumb(it, true); ghost.pickingMode = PickingMode.Ignore;
                ghost.style.position = Position.Absolute; ghost.style.width = 96; ghost.style.height = 100; overlayHost.Add(ghost);
            }
            if (dragging) { Vector2 p = overlayHost.WorldToLocal(e.position); ghost.style.left = p.x - 48; ghost.style.top = p.y - 50; ghost.style.opacity = ((Vector2)e.position - start).magnitude > 70 ? 0.55f : 0.95f; }
        });
        void End(Vector2 pos, int pointer, bool cancel) {
            if (!down) return; down = false; if (doll.HasPointerCapture(pointer)) doll.ReleasePointer(pointer);
            bool was = dragging; dragging = false; if (ghost != null) { ghost.RemoveFromHierarchy(); ghost = null; }
            if (!cancel && was && (pos - start).magnitude > 70) core.Call("remove", slot);     // pulled clear of her: it comes off
        }
        doll.RegisterCallback<PointerUpEvent>(e => End(e.position, e.pointerId, false));
        doll.RegisterCallback<PointerCancelEvent>(e => End(e.position, e.pointerId, true));
    }
    /// <summary>What she is wearing at a height on her figure (0 = top of the picture, 1 = the floor): the outermost piece there.</summary>
    string SlotAt(float f) {
        float head = mode3d ? 0.21f : 0.29f, waist = mode3d ? 0.47f : 0.53f;
        string[] order = f < head * 0.55f ? new[] { "hat", "glasses", "earrings" } : f < head ? new[] { "glasses", "earrings", "hat", "necklace" }
            : f < waist ? new[] { "outer", "top", "dress", "necklace", "bag" } : f < 0.9f ? new[] { "bottom", "dress", "bag", "outer" } : new[] { "shoes", "bottom" };
        foreach (var s in order) if (Worn(s) != "") return s;
        return null;
    }

    /// <summary>In My Suite a piece is dropped anywhere on the room outside the wardrobe panel.</summary>
    bool OnStarlet(Vector2 pos) => stageWrap.worldBound.Contains(pos) && !drawerWardrobe.worldBound.Contains(pos);

    // ================================================================ underwear and the signature outfit (wardrobe > Basics)
    void RenderBasics() {
        wardGrid.Clear(); recolorBox.Clear(); recolorBox.style.display = DisplayStyle.None;
        bool dressed = core.Get<DLayers>("layers").list.Length > 0, sig = P("sig", "1") == "1";
        VisualElement Card(string label, bool on, Action click) { var c = El("item" + (on ? " on" : ""), L(label)); c.style.height = 46; c.style.justifyContent = Justify.Center; c.RegisterCallback<ClickEvent>(_ => click()); return c; }
        wardGrid.Add(L("OUTFIT", "group-label"));
        wardGrid.Add(Card("Signature outfit", !dressed && sig, () => { core.Call("undress"); SetP("sig", "1"); }));
        wardGrid.Add(Card("Just underwear", !dressed && !sig, () => { core.Call("undress"); SetP("sig", "0"); }));
        wardGrid.Add(L("UNDERWEAR TOP", "group-label")); string bra = P("bra", "halter");
        foreach (var (v, label) in new[] { ("halter", "Halter"), ("bandeau", "Bandeau"), ("tank", "Crop tank") }) { string val = v; wardGrid.Add(Card(label, bra == v, () => SetP("bra", val))); }
        wardGrid.Add(L("UNDERWEAR BOTTOM", "group-label")); string pants = P("pants", "briefs");
        foreach (var (v, label) in new[] { ("briefs", "Briefs"), ("shorts", "Boy shorts") }) { string val = v; wardGrid.Add(Card(label, pants == v, () => SetP("pants", val))); }
        wardGrid.Add(L("COLOUR", "group-label")); var row = El("row"); string cur = core.Call("get", "underwear");
        foreach (var c in new[] { "#f4c7d3", "#ffffff", "#1f1b22", "#e9c46a", "#c8b6e2", "#a9c6ef", "#e8c3a0", "#d9475e", "#3fcf8e", "#7c4ee0" }) { string v = c; row.Add(Swatch(v, cur == v, () => core.Call("set", "underwear", v))); }
        wardGrid.Add(row);
        if (!On3) wardGrid.Add(L("Underwear styles show in 3D; the drawn game shows the colour.", "muted"));
    }

    // ================================================================ careers
    public sealed class Job { public string id, name, tier, blurb; public int pay, fame, budget; public string[] tags, uniform = new string[0]; }
    static readonly Job[] Jobs = {
        new Job { id = "popstar", name = "Pop star", tier = "star", pay = 70, fame = 8, tags = new[] { "Glam", "Night out" }, blurb = "Sold-out arenas and a number one single." },
        new Job { id = "actress", name = "Film star", tier = "star", pay = 80, fame = 9, tags = new[] { "Red carpet", "Glam" }, blurb = "Premieres, press tours and awards season." },
        new Job { id = "model", name = "Supermodel", tier = "star", pay = 75, fame = 8, tags = new[] { "Chic", "Edgy" }, blurb = "Runways in four cities and every magazine cover." },
        new Job { id = "designer", name = "Fashion designer", tier = "star", pay = 65, fame = 6, tags = new[] { "Chic", "Office" }, blurb = "Her own label, shown twice a year." },
        new Job { id = "athlete", name = "Pro athlete", tier = "star", pay = 70, fame = 7, tags = new[] { "Sporty" }, blurb = "Medals, sponsors and early mornings." },
        new Job { id = "mogul", name = "Business mogul", tier = "star", pay = 90, fame = 5, tags = new[] { "Office", "Chic" }, blurb = "A boardroom with her name on the building." },
        new Job { id = "influencer", name = "Influencer", tier = "star", pay = 55, fame = 7, tags = new[] { "Casual", "Summer" }, blurb = "Ten million followers watch what she wears." },
        new Job { id = "rockstar", name = "Rock star", tier = "star", pay = 72, fame = 8, tags = new[] { "Goth", "Edgy" }, blurb = "Black eyeliner, loud guitars and a world tour." },
        new Job { id = "barista", name = "Barista", tier = "everyday", pay = 16, budget = 35, tags = new[] { "Workwear", "Casual" }, uniform = new[] { "polo", "workpants" }, blurb = "Flat whites from six in the morning." },
        new Job { id = "nurse", name = "Nurse", tier = "everyday", pay = 24, budget = 40, tags = new[] { "Workwear" }, uniform = new[] { "scrubtop", "scrubpants" }, blurb = "Long shifts on the ward, in scrubs." },
        new Job { id = "chef", name = "Chef", tier = "everyday", pay = 22, budget = 40, tags = new[] { "Workwear" }, uniform = new[] { "chefcoat", "workpants", "workboots" }, blurb = "A hot kitchen and a white jacket." },
        new Job { id = "assistant", name = "Office assistant", tier = "everyday", pay = 26, budget = 50, tags = new[] { "Office" }, uniform = new[] { "workpants" }, blurb = "Diaries, phones and the coffee run." },
        new Job { id = "coach", name = "Fitness coach", tier = "everyday", pay = 24, budget = 45, tags = new[] { "Sporty" }, uniform = new[] { "gymtank", "leggings", "trainers" }, blurb = "Classes at the gym, morning to night." },
        new Job { id = "retail", name = "Shop assistant", tier = "everyday", pay = 18, budget = 40, tags = new[] { "Casual", "Chic" }, uniform = new[] { "polo" }, blurb = "Folding jumpers on the high street." },
    };
    static readonly Dictionary<string, string> FirstJob = new Dictionary<string, string> {
        { "aria", "popstar" }, { "zuri", "model" }, { "mina", "popstar" }, { "luz", "actress" }, { "noor", "actress" }, { "ivy", "designer" }, { "charlotte", "influencer" }, { "jenny", "athlete" },
        { "lina", "athlete" }, { "lucy", "athlete" }, { "mary", "popstar" }, { "olivia", "athlete" }, { "rosie", "influencer" }, { "songbird", "popstar" }, { "eve", "actress" }, { "cute", "designer" },
        { "pigtail", "mogul" }, { "custom", "influencer" } };
    Job CurJob { get { string id = P("job", FirstJob.TryGetValue(Av, out var f) ? f : "popstar"); return Array.Find(Jobs, j => j.id == id) ?? Jobs[0]; } }
    /// <summary>A star can wear anything; on an everyday wage only modest pieces and the uniform are in the wardrobe.</summary>
    bool Allowed(DItem it) { var j = CurJob; return j.budget == 0 || it.price <= j.budget || Array.IndexOf(j.uniform, it.id) >= 0; }
    int ShiftsLeft => Mathf.Max(0, 3 - (int)Num(core.Pref("shifts." + Today, "0"), 0));

    void TakeJob(Job job) {
        SetP("job", job.id); workResult = "";
        if (job.uniform.Length > 0) core.Call("grant", Json(job.uniform));
        var d = JsonUtility.FromJson<DLook3>(core.Call("look3d")); var off = new List<string>();
        if (d.worn != null) foreach (var w in d.worn) if (itemById.TryGetValue(w.id, out var it) && !Allowed(it)) off.Add(w.id);
        if (off.Count > 0) core.Call("takeOff", Json(off));
        ShowToast(job.tier == "star" ? "Now a " + job.name.ToLowerInvariant() + ": the whole wardrobe is hers" : "Now a " + job.name.ToLowerInvariant() + ": the pricey pieces go into storage");
    }
    (List<DLine> lines, int score, int match) DressCheck(string[] tags, int need) {
        var d = JsonUtility.FromJson<DLook3>(core.Call("look3d")); bool top = false, bottom = false, dress = false, shoes = false; int match = 0;
        if (d.worn != null) foreach (var w in d.worn) {
            if (w.slot == "top") top = true; else if (w.slot == "bottom") bottom = true; else if (w.slot == "dress") dress = true; else if (w.slot == "shoes") shoes = true;
            if (itemById.TryGetValue(w.id, out var it) && Array.Exists(it.tags, t => Array.IndexOf(tags, t) >= 0)) match++;
        }
        bool full = dress || (top && bottom);
        var lines = new List<DLine> { new DLine { ok = full, text = "A full outfit (a dress, or a top with a bottom)" }, new DLine { ok = shoes, text = "Shoes" },
            new DLine { ok = match >= need, text = "Pieces that are " + string.Join(" or ", tags).ToLowerInvariant() + ": " + match + " (wear " + need + " or more)" } };
        return (lines, (full ? 40 : 0) + (shoes ? 15 : 0) + Mathf.Min(match, 3) * 15, match);
    }
    void Checklist(VisualElement s, List<DLine> lines) {
        foreach (var l in lines) { var tick = Icon(l.ok ? "check" : "ring", l.ok ? "#1d9a63" : "#c9bfd6", 18); tick.style.marginRight = 8; var row = El("row", tick, L(l.text, "check" + (l.ok ? " ok" : ""))); row.style.flexWrap = Wrap.NoWrap; s.Add(row); }
    }

    VisualElement careerBody, careerBox; SvgImage careerDoll; string workResult = "";
    void BuildCareer(VisualElement pane) {
        careerDoll = new SvgImage(); careerBox = El("contest-doll", careerDoll); careerBox.style.backgroundColor = C("#dfeaf8"); careerBody = Scroll();
        pane.Add(El("contest-body", careerBox, careerBody));
    }
    void RefreshCareer() {
        if (On3) Doll3D(careerDoll, "career", "full"); else careerDoll.Set("doll:" + dollKey + ":career", () => core.Call("doll"));
        var s = careerBody; s.Clear(); var job = CurJob; var (lines, score, _) = DressCheck(job.tags, 2);
        s.Add(L(profile.name + ", " + job.name.ToLowerInvariant(), "h2"));
        s.Add(L(job.blurb + (job.tier == "star" ? " A star career: nothing in the shop is out of reach." : " An everyday job: only pieces up to " + job.budget + " coins, and the uniform, are in her wardrobe and the shop."), "muted"));
        s.Add(L("Dress code for work", "h3")); Checklist(s, lines);
        s.Add(L("Pay: up to " + job.pay + " coins" + (job.fame > 0 ? " and " + job.fame + " fame" : "") + " a shift. Shifts left today: " + ShiftsLeft + " of 3 (a workout at the gym gives one back).", "muted"));
        var go = B("Go to work", "btn primary big", Work); go.SetEnabled(ShiftsLeft > 0);
        var acts = El("row", go, B("Change her outfit", "btn", () => { SetView("suite"); OpenDrawer("wardrobe"); })); acts.style.marginTop = 12; s.Add(acts);
        if (workResult != "") { var r = L(workResult, "h3"); s.Add(r); }
        foreach (var (tier, title) in new[] { ("star", "STAR CAREERS"), ("everyday", "EVERYDAY JOBS") }) {
            s.Add(L(title, "sec")); var grid = El("grid");
            foreach (var j in Jobs) {
                if (j.tier != tier) continue; var jj = j; bool cur = j.id == job.id;
                var take = B(cur ? "Her job now" : "Take this job", "btn small" + (cur ? "" : " primary"), () => TakeJob(jj)); take.SetEnabled(!cur);
                string wardrobe = j.budget == 0 ? "Whole wardrobe" : "Wardrobe up to " + j.budget + " coins" + (j.uniform.Length > 0 ? ", uniform provided" : "");
                var card = El(null, L(j.name.ToUpperInvariant(), "tile-h"), L(j.pay + " coins a shift", "look-name"), L(j.blurb, "muted"), L("Wears: " + string.Join(", ", j.tags).ToLowerInvariant() + ". " + wardrobe + ".", "muted"), take);
                card.style.width = 268; card.style.marginRight = 12; card.style.marginBottom = 12; card.style.paddingLeft = card.style.paddingRight = 14; card.style.paddingTop = card.style.paddingBottom = 12;
                card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 14;
                card.style.borderLeftWidth = card.style.borderRightWidth = card.style.borderTopWidth = card.style.borderBottomWidth = cur ? 2 : 1;
                card.style.borderLeftColor = card.style.borderRightColor = card.style.borderTopColor = card.style.borderBottomColor = C(cur ? "#9b6bf2" : "#ece6f7"); card.style.backgroundColor = C(cur ? "#f6f0ff" : "#fbf9fe");
                foreach (var kid in card.Children()) if (kid is Label lab) lab.style.whiteSpace = WhiteSpace.Normal;
                take.style.marginTop = 8; take.style.alignSelf = Align.FlexStart; grid.Add(card);
            }
            s.Add(grid);
        }
    }
    void Work() {
        if (ShiftsLeft <= 0) return; var job = CurJob; var (_, score, _) = DressCheck(job.tags, 2);
        int pay = Mathf.RoundToInt(job.pay * (0.3f + 0.7f * score / 100f)), fame = Mathf.RoundToInt(job.fame * score / 100f);
        core.SetPref("shifts." + Today, ((int)Num(core.Pref("shifts." + Today, "0"), 0) + 1).ToString());
        core.Call("earn", (double)pay, (double)fame);
        workResult = (score >= 85 ? "A great shift, dressed for the part. " : score >= 55 ? "A decent shift. " : "A hard shift: she was not dressed for it. ") + "She earned " + pay + " coins" + (fame > 0 ? " and " + fame + " fame." : ".");
        dirty = true;
    }

    // ================================================================ gym (3D)
    Image gymImg; VisualElement gymBody; int gymW, gymH; float gymYaw, gymFrom, gymJump = -1; bool gymTurn; string gymResult = "";
    void BuildGym(VisualElement pane) {
        gymImg = new Image { scaleMode = ScaleMode.StretchToFill }; gymImg.style.flexGrow = 1; gymImg.pickingMode = PickingMode.Ignore;
        var view = El(null, gymImg); view.style.flexGrow = 1; view.style.flexBasis = 0; view.style.marginRight = 20; view.style.overflow = Overflow.Hidden; view.style.backgroundColor = C("#d7efe6");
        view.style.borderTopLeftRadius = view.style.borderTopRightRadius = view.style.borderBottomLeftRadius = view.style.borderBottomRightRadius = 20;
        view.RegisterCallback<GeometryChangedEvent>(e => { float k = view.scaledPixelsPerPoint; gymW = Mathf.RoundToInt(e.newRect.width * k); gymH = Mathf.RoundToInt(e.newRect.height * k); });
        view.RegisterCallback<PointerDownEvent>(e => { gymTurn = true; gymFrom = e.position.x; view.CapturePointer(e.pointerId); });
        view.RegisterCallback<PointerMoveEvent>(e => { if (!gymTurn || !view.HasPointerCapture(e.pointerId)) return; gymYaw -= (e.position.x - gymFrom) * 0.7f; gymFrom = e.position.x; });
        view.RegisterCallback<PointerUpEvent>(e => { if (view.HasPointerCapture(e.pointerId)) view.ReleasePointer(e.pointerId); gymTurn = false; });
        gymBody = Scroll();
        pane.Add(El("parlor", view, El("panel", L("Gym", "h2"), L("Kit her out in sports gear, then train. A workout earns fame and gives back a work shift.", "muted"), gymBody)));
    }
    void RefreshGym() {
        var b = gymBody; b.Clear();
        if (!On3) { b.Add(L("The gym works in 3D.", "h3")); b.Add(B("Switch to 3D", "btn primary", () => Set3D(true, true))); gymImg.image = null; return; }
        var (lines, _, match) = DressCheck(new[] { "Sporty" }, 2); lines.RemoveAt(0); int done = (int)Num(core.Pref("gym." + Today, "0"), 0);
        b.Add(L("Ready to train?", "h3")); Checklist(b, lines);
        b.Add(L("Workouts left today: " + Mathf.Max(0, 3 - done) + " of 3.", "muted"));
        var row = El("row"); row.style.marginTop = 10;
        foreach (var (name, fame) in new[] { ("Cardio", 4), ("Weights", 5), ("Yoga", 3) }) {
            string nm = name; int f = fame; var w = B(name, "btn primary", () => Workout(nm, f)); w.SetEnabled(match >= 2 && lines[0].ok && done < 3); w.style.marginRight = 8; row.Add(w);
        }
        b.Add(row);
        if (gymResult != "") b.Add(L(gymResult, "h3"));
        b.Add(L("Sports gear", "h3")); var grid = El("grid");
        foreach (var it in data.items) {
            if (it.cat != "sport") continue; string id = it.id; bool own = Owns(id), on = Worn(it.slot) == id;
            var act = own ? B(on ? "Take off" : "Wear", "btn small" + (on ? "" : " primary"), () => core.Call("equip", id)) : B("Buy · " + it.price, "btn small", () => core.Call("buy", id));
            if (!own) act.SetEnabled(profile.coins >= it.price && Allowed(it));
            var card = El("item" + (on ? " on" : ""), Thumb(it, true), L(it.name), act); grid.Add(card);
        }
        b.Add(grid);
    }
    void Workout(string name, int fame) {
        int done = (int)Num(core.Pref("gym." + Today, "0"), 0); if (done >= 3) return;
        core.SetPref("gym." + Today, (done + 1).ToString());
        int used = (int)Num(core.Pref("shifts." + Today, "0"), 0); bool back = used > 0; if (back) core.SetPref("shifts." + Today, (used - 1).ToString());
        core.Call("earn", 0d, (double)fame); gymJump = Time.time;
        // training shapes her: cardio slims, weights build muscle, yoga a little of both
        string body = name == "Cardio" ? BodyChange(Av, -0.06f, 0.01f) : name == "Weights" ? BodyChange(Av, -0.02f, 0.05f) : BodyChange(Av, -0.03f, 0.02f);
        gymResult = name + " done: +" + fame + " fame" + (back ? ", and she has the energy for another shift at work." : ".") + body;
        dirty = true;
    }
    /// <summary>Food and exercise change her build a little at a time (weight -1..1, muscle 0..1, as in the avatar studio).
    /// Returns a few words about it, or "" when nothing showed.</summary>
    string BodyChange(string sid, float dWeight, float dMuscle) {
        var sv = core.Pref("shape." + sid, "").Split(','); var sh = new float[8];
        for (int i = 0; i < sh.Length && i < sv.Length; i++) sh[i] = Num(sv[i], 0);
        float w0 = sh[0], m0 = sh[1];
        sh[0] = Mathf.Clamp(sh[0] + dWeight, -1, 1); sh[1] = Mathf.Clamp(sh[1] + dMuscle, 0, 1);
        if (sh[0] == w0 && sh[1] == m0) return "";
        core.SetPref("shape." + sid, string.Join(",", Array.ConvertAll(sh, v => v.ToString("0.###", CultureInfo.InvariantCulture)))); dirty = true;
        bodyDirty = true;
        if (dWeight < 0 && sh[1] > m0) return " She looks leaner and more toned.";
        if (dWeight < 0) return " She looks a little slimmer.";
        if (dWeight > 0) return " She has filled out a little.";
        return " She looks more toned.";
    }
    bool bodyDirty;
    void TickGym() {
        if (!On3 || view != "gym" || gymW < 16 || picker.ClassListContains("on")) return;
        float lift = 0; if (gymJump >= 0) { float t = Time.time - gymJump; if (t > 2.4f) gymJump = -1; else lift = Mathf.Abs(Mathf.Sin(t * 8f)) * 0.11f; }
        float sway = shotsDir == null && !gymTurn ? Mathf.Sin(Time.time * 0.6f) * 6f : 0;
        gymImg.image = stage3d.Gym(gymW, gymH, look3 ?? MyLook(null), gymYaw + sway, lift);
    }

    // ================================================================ avatar studio (Beauty Parlor > Avatar, 3D)
    void AvatarStudio(VisualElement b) {
        string me = Av; var mine = SigLook(me);
        b.Add(L("Avatar studio", "h3")); b.Add(L("Make her your own: a face, the outfit she arrives in, her height and her build. Hair is done in the Hair Salon.", "muted"));
        if (me == "custom") {
            var name = new TextField { value = profile.name }; name.AddToClassList("input"); name.style.flexGrow = 1; name.style.marginRight = 8;
            var row = El("row", name, B("Save name", "btn small primary", () => { string v = name.value.Trim(); if (v == "") return; core.SetPref("name.custom", v); core.Call("rename", v); foreach (var x in data.starlets) if (x.id == "custom") x.name = v; dirty = true; }));
            row.style.flexWrap = Wrap.NoWrap; row.style.marginTop = 8; b.Add(Field("Name", row));
        }
        b.Add(L("Face", "h3")); var faces = El("grid");
        foreach (var e in stage3d.Starlets) {
            if (!Array.Exists(e.parts, q => q.mesh == "head")) continue; string id = e.id;
            var look = SigLook(me); look.id = id; look.body = look.body ?? mine.id; if (look.body == id) look.body = null;
            var card = El("hair-card" + (mine.id == id ? " on" : ""), new SvgImage().Set3D("3d:studio-face:" + look.Key, (w, h) => stage3d.Shot(look, "portrait", w, h)));
            card.RegisterCallback<ClickEvent>(_ => { string keep = core.Pref("body." + me, me == "custom" ? "jenny" : me); core.SetPref("body." + me, keep); core.SetPref("face." + me, id); dirty = true; RefreshParlorPanel(); });
            faces.Add(card);
        }
        b.Add(faces);
        b.Add(L("Arrives wearing", "h3")); var fits = El("grid"); string curBody = mine.body ?? mine.id;
        foreach (var e in stage3d.Starlets) {
            if (!Array.Exists(e.parts, q => q.mesh == "top" || q.mesh == "suit")) continue; string id = e.id;
            if (id == "basics") continue;
            var look = SigLook(me); look.body = id == look.id ? null : id; look.hair = mine.hair;
            var card = El("hair-card" + (curBody == id ? " on" : ""), new SvgImage().Set3D("3d:studio-fit:" + look.Key, (w, h) => stage3d.Shot(look, "full", w, h)));
            card.RegisterCallback<ClickEvent>(_ => { core.SetPref("body." + me, id); core.Call("undress"); SetP("sig", "1"); RefreshParlorPanel(); });
            fits.Add(card);
        }
        b.Add(fits);
        // her build: sliders, and a few ready-made builds to start from
        b.Add(L("Build", "h3")); b.Add(L("Height, weight, muscle and proportions. Her clothes change shape with her. Press Full length under the mirror to see all of her.", "muted"));
        string[] names = { "Weight", "Muscle", "Shoulders", "Hips", "Bust", "Leg length", "Head size", "Bum" };
        float[] cur = SigLook(me).shape; float height = Num(core.Pref("height." + me, "1"));
        void Save() { core.SetPref("shape." + me, string.Join(",", Array.ConvertAll(cur, v => v.ToString("0.###", CultureInfo.InvariantCulture)))); core.SetPref("height." + me, height.ToString("0.###", CultureInfo.InvariantCulture)); dirty = true; }
        var presets = El("row");
        foreach (var (label, h, v) in new[] { ("As she came", 1f, new float[] { 0, 0, 0, 0, 0, 0, 0 }), ("Petite", 0.93f, new float[] { -0.3f, 0, -0.2f, -0.1f, -0.2f, -0.3f, 0.3f }), ("Slim", 1f, new float[] { -0.7f, 0, -0.2f, -0.4f, -0.3f, 0.2f, 0 }),
                ("Tall model", 1.08f, new float[] { -0.5f, 0.1f, 0.1f, -0.2f, -0.1f, 0.8f, -0.3f }), ("Athletic", 1.02f, new float[] { -0.1f, 0.55f, 0.4f, 0, 0, 0.2f, 0 }), ("Curvy", 1f, new float[] { 0.3f, 0, 0, 1.1f, 1.1f, 0, 0, 1.1f }), ("Hourglass", 1.01f, new float[] { -0.15f, 0.1f, 0.1f, 1.4f, 1.4f, 0.1f, 0, 1.4f }),
                ("Bodybuilder", 1.03f, new float[] { 0.2f, 1f, 1f, 0.2f, 0.2f, 0, -0.2f }), ("Plus size", 1f, new float[] { 1f, 0, 0.3f, 0.9f, 0.9f, -0.1f, 0, 0.9f }) }) {
            float hh = h; var vv = v; presets.Add(B(label, "chip", () => { height = hh; Array.Clear(cur, 0, cur.Length); Array.Copy(vv, cur, Mathf.Min(vv.Length, cur.Length)); Save(); RefreshParlorPanel(); }));
        }
        b.Add(presets);
        VisualElement Bar(string label, float min, float max, float value, Action<float> set) {
            var sl = new Slider(min, max) { value = value }; sl.AddToClassList("slider"); sl.RegisterValueChangedCallback(e => { set(e.newValue); Save(); });
            var row = El("row", sl); row.style.flexWrap = Wrap.NoWrap; return Field(label, row);
        }
        b.Add(Bar("Height", 0.88f, 1.12f, height, v => height = v));
        for (int k = 0; k < names.Length; k++) { int kk = k; b.Add(Bar(names[k], k == 1 ? 0 : -1, ShapeMax(k), cur[k], v => cur[kk] = v)); }
    }
}
