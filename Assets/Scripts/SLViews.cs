// The Starlife views: suite (room, wardrobe, decorating, guide), beauty parlor, shop, contests and album.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

public partial class SLApp {
    // ================================================================ suite
    const float RoomW = 1000, RoomH = 620, DollH = 520, DollW = 520f * 400f / 820f;
    VisualElement stageWrap, stage, drawerWardrobe, drawerDecor, banner, layersPop, guide, wardGrid, wardTabs, recolorBox, decorBody, layersBody;
    Label stageName, guideText, guideCount; SvgImage roomBg, dollEl; Button undoBtn, guideBack, guideNext, btn3d;
    SLStage3D stage3d; Image room3d; bool mode3d, turning; float yaw3d, turnFrom; int liveW, liveH;
    readonly Dictionary<string, SvgImage> furnEls = new Dictionary<string, SvgImage>();
    readonly Dictionary<string, Button> sideBtns = new Dictionary<string, Button>();
    string drawer, wardrobeCat = "top"; float stageK = 1; int guideStep; Vector2 grab;
    static float Depth(float y) => 0.74f + 0.26f * Mathf.Clamp01((y - 444f) / (RoomH - 444f));

    void BuildSuite(VisualElement pane) {
        roomBg = new SvgImage("abs"); dollEl = new SvgImage("abs"); Draggable(dollEl, null);
        room3d = new Image { scaleMode = ScaleMode.StretchToFill }; room3d.AddToClassList("abs"); room3d.pickingMode = PickingMode.Ignore; room3d.style.display = DisplayStyle.None; Turnable(dollEl); DragOff(dollEl);
        stage = El("stage", roomBg, room3d, dollEl); stage.style.position = Position.Absolute;
        stageName = L("", "stage-name"); stageName.pickingMode = PickingMode.Ignore;
        var toolbar = El("toolbar", IB("shirt", "#ffffff", "Undress", null, () => { core.Call("undress"); SetP("sig", "0"); }), IB("shuffle", "#ffffff", "Shuffle", null, () => core.Call("random")), IB("layers", "#ffffff", "Layers", null, ToggleLayers), undoBtn = IB("undo", "#ffffff", "Undo", null, () => core.Call("undo")), btn3d = IB("cube", "#ffffff", "3D", null, () => Set3D(!mode3d, true)));
        layersPop = El("layers-pop", El("drawer-head", L("WHAT SHE'S WEARING", "tile-h"), B("×", "close", ToggleLayers)), layersBody = El("grid")); layersPop.style.display = DisplayStyle.None;

        wardTabs = El("tabs"); wardGrid = Scroll("grid"); recolorBox = El("recolor");
        drawerWardrobe = El("drawer", El("drawer-head", L("Wardrobe", "drawer-title"), B("×", "close", () => OpenDrawer(null))), wardTabs, wardGrid, recolorBox);
        decorBody = Scroll();
        drawerDecor = El("drawer", El("drawer-head", L("Decorate", "drawer-title"), B("×", "close", () => OpenDrawer(null))), decorBody);
        banner = El("banner", L("Drag the furniture and your starlet to rearrange the room"), B("Done", null, () => OpenDrawer(null)));

        var gf = new SvgImage().Set("guideface", () => core.Call("guideFace"));
        guide = El("guide", El("guide-face", gf), El("bubble", guideText = L("", "bubble-text"),
            El("row", B("Skip", "btn small", CloseGuide), El("grow"), guideCount = L("", "muted"), guideBack = B("‹ Back", "btn small", () => { if (guideStep > 0) { guideStep--; RenderGuide(); } }),
                guideNext = B("Next ›", "btn small primary", () => { if (guideStep < data.guide - 1) { guideStep++; RenderGuide(); } else CloseGuide(); }))));
        guideCount.style.marginRight = 8; guide.style.display = DisplayStyle.None;

        stageWrap = El("stage-wrap", stage, stageName, toolbar, layersPop, banner, drawerWardrobe, drawerDecor, guide);
        stageWrap.RegisterCallback<GeometryChangedEvent>(_ => LayoutRoom());
        var sidebar = El("sidebar");
        foreach (var (id, label) in new[] { ("wardrobe", "WARDROBE"), ("parlor", "BEAUTY PARLOR"), ("decor", "DECORATE"), ("camera", "CAMERA") }) {
            string k = id;
            sideBtns[id] = IB(k == "wardrobe" ? "hanger" : k == "parlor" ? "mirror" : k == "decor" ? "sofa" : "camera", "#9b6bf2", label, "side-btn", () => { if (k == "parlor") SetView("parlor"); else if (k == "camera") OpenSave(); else OpenDrawer(drawer == k ? null : k); });
            sidebar.Add(sideBtns[id]);
        }
        sidebar.Add(El("grow")); sidebar.Add(B("Save photo", "save-btn", OpenSave));
        pane.Add(El("suite", stageWrap, sidebar));
        OpenDrawer(null);
    }

    void OpenDrawer(string name) {
        drawer = name;
        drawerWardrobe.style.display = name == "wardrobe" ? DisplayStyle.Flex : DisplayStyle.None;
        drawerDecor.style.display = name == "decor" ? DisplayStyle.Flex : DisplayStyle.None;
        banner.style.display = name == "decor" ? DisplayStyle.Flex : DisplayStyle.None;
        if (name != null) { guide.style.display = DisplayStyle.None; layersPop.style.display = DisplayStyle.None; }
        foreach (var kv in sideBtns) kv.Value.EnableInClassList("on", kv.Key == name);
        if (state != null) RefreshSuite();
    }
    void ToggleLayers() { layersPop.style.display = layersPop.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None; RenderLayers(); }

    void RefreshSuite() {
        LayoutRoom();
        var x = Array.Find(data.starlets, s => s.id == state.starlet);
        stageName.text = x != null ? x.name + " · " + CurJob.name : "My starlet";
        undoBtn.SetEnabled(profile.canUndo);
        if (drawer == "wardrobe") RenderWardrobe();
        if (drawer == "decor") RenderDecor();
        if (layersPop.style.display != DisplayStyle.None) RenderLayers();
    }

    Vector2 RoomPos(Vector3 panelPos) => stage.WorldToLocal((Vector2)panelPos) / stageK;
    void Draggable(VisualElement el, string id) {
        el.RegisterCallback<PointerDownEvent>(e => {
            if (drawer != "decor") return;
            Vector2 cur = id == null ? new Vector2(state.room.doll.x, state.room.doll.y) : ItemPos(id);
            grab = cur - RoomPos(e.position); el.CapturePointer(e.pointerId); e.StopPropagation();
        });
        el.RegisterCallback<PointerMoveEvent>(e => {
            if (turning || !el.HasPointerCapture(e.pointerId)) return;
            Vector2 p = RoomPos(e.position) + grab;
            if (id == null) core.Call("moveDoll", (double)p.x, (double)p.y); else core.Call("moveFurn", id, (double)p.x, (double)p.y);
            state = core.Get<DState>("state"); LayoutRoom();
        });
        el.RegisterCallback<PointerUpEvent>(e => { if (el.HasPointerCapture(e.pointerId)) { el.ReleasePointer(e.pointerId); core.Call("commit"); } });
    }
    Vector2 ItemPos(string id) { var it = Array.Find(state.room.items, i => i.id == id); return it == null ? Vector2.zero : new Vector2(it.x, it.y); }

    void LayoutRoom() {
        if (state == null || stage.panel == null) return;
        var r = stageWrap.contentRect; if (r.width < 10 || r.height < 10) return;
        float k = stageK = Mathf.Max(r.width / RoomW, r.height / RoomH), ppp = stage.scaledPixelsPerPoint;
        float top = r.height - RoomH * k;
        if (narrowMode && drawer != null) { // a bottom sheet covers the lower part: zoom out so she stands above it
            float ha = r.height * 0.42f; k = stageK = Mathf.Max(ha / (DollH * Depth(state.room.doll.y) + 40), r.width / RoomW); top = ha - (state.room.doll.y + 12) * k;
        }
        stage.style.left = Mathf.Clamp(r.width / 2 - state.room.doll.x * k, r.width - RoomW * k, 0); stage.style.top = top;
        stage.style.width = RoomW * k; stage.style.height = RoomH * k;
        roomBg.style.left = 0; roomBg.style.top = 0; roomBg.style.width = RoomW * k; roomBg.style.height = RoomH * k;
        roomBg.Set("roombg:" + state.room.wall + state.room.floor, () => core.Call("roomBg"));
        stageWrap.style.backgroundColor = C(state.room.wall);

        var d = state.room.doll; float sc = Depth(d.y);
        dollEl.Fixed(Mathf.RoundToInt(DollW * k * ppp), Mathf.RoundToInt(DollH * k * ppp)).Set("doll:" + dollKey, () => core.Call("doll"));
        Place(dollEl, d.x - DollW / 2 * sc, d.y - DollH * 802f / 820f * sc, DollW * sc, DollH * sc);

        // in 3D the whole room is one picture from the 3D stage; the drawn pieces stay in place, invisible, to take the pointer
        bool on3 = On3;
        roomBg.style.opacity = dollEl.style.opacity = on3 ? 0 : 1; room3d.style.display = on3 ? DisplayStyle.Flex : DisplayStyle.None;
        if (on3) { room3d.style.left = 0; room3d.style.top = 0; room3d.style.width = RoomW * k; room3d.style.height = RoomH * k; liveW = Mathf.RoundToInt(RoomW * k * ppp); liveH = Mathf.RoundToInt(RoomH * k * ppp); }

        var order = new List<(int layer, float y, VisualElement el)> { (2, d.y, dollEl) };
        var seen = new HashSet<string>();
        foreach (var it in state.room.items) {
            if (!furnByK.TryGetValue(it.k, out var f)) continue;
            seen.Add(it.id);
            if (!furnEls.TryGetValue(it.id, out var el)) { el = new SvgImage("abs"); furnEls[it.id] = el; stage.Add(el); Draggable(el, it.id); }
            float fs = f.layer == "wall" ? 1 : Depth(it.y); string kind = it.k;
            el.Fixed(Mathf.RoundToInt(f.box[2] * k * ppp), Mathf.RoundToInt(f.box[3] * k * ppp)).Set("furn:" + kind, () => core.Call("furn", kind));
            Place(el, it.x + f.box[0] * fs, it.y + f.box[1] * fs, f.box[2] * fs, f.box[3] * fs); el.style.opacity = on3 ? 0 : 1;
            order.Add((f.layer == "wall" ? 0 : f.layer == "rug" ? 1 : 2, f.layer == "floor" ? it.y : 0, el));
        }
        foreach (var id in new List<string>(furnEls.Keys)) if (!seen.Contains(id)) { furnEls[id].RemoveFromHierarchy(); furnEls.Remove(id); }
        order.Sort((a, b) => a.layer != b.layer ? a.layer.CompareTo(b.layer) : a.y.CompareTo(b.y));
        roomBg.SendToBack(); foreach (var o in order) o.el.BringToFront();
    }
    // ---- 3D: the starlets are 3D avatars and the suite a 3D room; drag her sideways to turn her (outside Decorate)
    bool On3 => mode3d && stage3d != null && stage3d.Ok;
    string Av => state == null || string.IsNullOrEmpty(state.starlet) ? "aria" : state.starlet;
    string FurnSvg(string k) => core.Call("furn", k);
    void Turnable(VisualElement el) {
        el.RegisterCallback<PointerDownEvent>(e => { if (!mode3d || drawer == "decor" || drawer == "wardrobe") return; turning = true; turnFrom = e.position.x; el.CapturePointer(e.pointerId); e.StopPropagation(); });
        el.RegisterCallback<PointerMoveEvent>(e => { if (!turning || !el.HasPointerCapture(e.pointerId)) return; yaw3d -= (e.position.x - turnFrom) * 0.7f; turnFrom = e.position.x; });
        el.RegisterCallback<PointerUpEvent>(e => { if (turning && el.HasPointerCapture(e.pointerId)) el.ReleasePointer(e.pointerId); turning = false; });
    }
    void Set3D(bool on, bool remember) {
        if (on && stage3d == null) stage3d = new SLStage3D();
        if (on && !stage3d.Ok) { on = false; ShowToast("The 3D starlets are not in this build"); }
        mode3d = on; yaw3d = 0;
        if (remember) { PlayerPrefs.SetInt("sl3d", on ? 1 : 0); ShowToast(on ? "3D on: drag your starlet to turn her around" : "Back to the drawn game"); }
        btn3d.EnableInClassList("on", on);
        dirty = true;
    }
    void Tick3D() {
        if (!On3 || view != "suite" || state == null || liveW < 16 || picker.ClassListContains("on")) return;
        float sway = shotsDir == null && !turning ? Mathf.Sin(Time.time * 0.7f) * 4f : 0;
        room3d.image = stage3d.Live(liveW, liveH, state.room, furnByK, FurnSvg, look3 ?? MyLook(null), yaw3d + sway);
    }
    SvgImage Doll3D(SvgImage el, string where, string shot, Look3 look = null) { var L = look ?? look3 ?? MyLook(null); return el.Set3D("3d:" + where + ":" + L.Key + shot, (w, h) => stage3d.Shot(L, shot, w, h)); }

    // what the current starlet shows in 3D: her head, the chosen hair, and whatever she wears from the wardrobe
    [Serializable] class DWorn { public string slot, id, color; }
    [Serializable] class DLook3 { public string starlet, hairColor, skin, underwear, hairColor0, skin0; public DWorn[] worn; }
    Look3 look3; readonly Dictionary<string, string> hair3d = new Dictionary<string, string>();
    string Hair3D(string id) { if (!hair3d.TryGetValue(id, out var h)) hair3d[id] = h = shotsDir == null ? PlayerPrefs.GetString("sl3d.hair." + id, "") : ""; return h; }
    void SetHair3D(string id, string hair) { hair3d[id] = hair; if (shotsDir == null) PlayerPrefs.SetString("sl3d.hair." + id, hair); dirty = true; }
    Look3 MyLook(string tryIds) {
        var d = JsonUtility.FromJson<DLook3>(tryIds == null ? core.Call("look3d") : core.Call("look3d", tryIds));
        string sid = string.IsNullOrEmpty(d.starlet) ? "aria" : d.starlet; var L = SigLook(sid); hairColor0 = d.hairColor0 ?? "";
        L.bra = core.Pref("bra." + sid, "halter"); L.pants = core.Pref("pants." + sid, "briefs"); L.signature = core.Pref("sig." + sid, "1") == "1";
        if (!string.IsNullOrEmpty(d.hairColor) && d.hairColor != d.hairColor0) L.hairColor = C(d.hairColor);
        if (!string.IsNullOrEmpty(d.skin0) && d.skin != d.skin0) { Color a = C(d.skin), b = C(d.skin0); L.skinTint = new Color(Mathf.Clamp(a.r / b.r, 0.25f, 1.7f), Mathf.Clamp(a.g / b.g, 0.25f, 1.7f), Mathf.Clamp(a.b / b.b, 0.25f, 1.7f)); }
        if (!string.IsNullOrEmpty(d.underwear)) L.underwear = C(d.underwear);
        if (d.worn != null) foreach (var w in d.worn) L.worn.Add((w.slot, w.id, C(w.color)));
        return L;
    }

    void Place(VisualElement el, float x, float y, float w, float h) { el.style.left = x * stageK; el.style.top = y * stageK; el.style.width = w * stageK; el.style.height = h * stageK; }

    bool Owns(string id) => Array.IndexOf(profile.owned, id) >= 0;
    string Worn(string slot) => core.Call("get", "outfit." + slot);
    SvgImage Thumb(DItem it, bool ownColor) {
        string col = ownColor ? core.Call("get", "colors." + it.id) : ""; if (col == "") col = it.color;
        return new SvgImage().Set("thumb:" + it.id + ":" + col, () => core.Call("thumb", it.id, col));
    }

    void RenderWardrobe() {
        wardTabs.Clear();
        foreach (var (k, label) in new[] { ("top", "Tops"), ("bottom", "Bottoms"), ("dress", "Dresses"), ("outer", "Jackets"), ("shoes", "Shoes"), ("acc", "Accessories"), ("sport", "Sports"), ("work", "Workwear"), ("goth", "Goth"), ("men", "Menswear"), ("basics", "Underwear") }) {
            string cat = k; var b = B(label, k == wardrobeCat ? "on" : null, () => { wardrobeCat = cat; RenderWardrobe(); }); wardTabs.Add(b);
        }
        if (wardrobeCat == "basics") { RenderBasics(); return; }
        wardGrid.Clear(); string grp = null; int locked = 0, stored = 0;
        foreach (var it in data.items) {
            if (it.cat != wardrobeCat) continue;
            if (!Allowed(it)) { if (Owns(it.id)) stored++; continue; }     // out of reach on her present wage
            if (!Owns(it.id)) { locked++; continue; }
            if (!string.IsNullOrEmpty(it.group) && it.group != grp) { grp = it.group; wardGrid.Add(L(grp.ToUpperInvariant(), "group-label")); }
            string id = it.id;
            var card = El("item" + (Worn(it.slot) == it.id ? " on" : ""), Thumb(it, true), L(it.name));
            DragToWear(card, it, OnStarlet, () => core.Call("equip", id)); wardGrid.Add(card);      // drag it onto her, or just click
        }
        VisualElement Note(string text) { var n = L(text, "muted"); n.style.width = Length.Percent(100); n.style.whiteSpace = WhiteSpace.Normal; return n; }   // full width, wrapped: a long line must not make the grid scroll sideways
        wardGrid.Add(Note("Drag a piece onto her to wear it. Drag it off her to take it off."));
        if (stored > 0) wardGrid.Add(Note(stored + (stored == 1 ? " piece is" : " pieces are") + " in storage: too dear for a " + CurJob.name.ToLowerInvariant() + ". Change jobs in Career."));
        if (locked > 0) { var more = El("item more", L("+" + locked, "h2"), L("More in the Shop")); more.RegisterCallback<ClickEvent>(_ => SetView("shop")); wardGrid.Add(more); }

        recolorBox.Clear(); string fj = core.Call("focus");
        recolorBox.style.display = fj == "" ? DisplayStyle.None : DisplayStyle.Flex;
        if (fj != "") {
            var f = JsonUtility.FromJson<DFocus>(fj);
            recolorBox.Add(L("Recolour · <b>" + f.name + "</b>", "muted")); var row = El("row"); row.style.marginTop = 6;
            foreach (var c in data.pal.cloth) row.Add(Swatch(c, c == f.color, () => core.Call("recolor", c)));
            row.Add(B("Original", "chip", () => core.Call("recolor", "reset"))); recolorBox.Add(row);
        }
    }
    VisualElement Swatch(string color, bool on, Action click) {
        var sw = El("sw"); sw.style.backgroundColor = C(color);
        var ring = El("sw-ring" + (on ? " on" : ""), sw); ring.RegisterCallback<ClickEvent>(_ => click()); return ring;
    }

    void RenderDecor() {
        decorBody.Clear();
        decorBody.Add(L("Wall colour", "h3")); var walls = El("row");
        foreach (var w in data.walls) { string c = w; walls.Add(Swatch(c, state.room.wall == c, () => core.Call("set", "room.wall", c))); }
        decorBody.Add(walls);
        decorBody.Add(L("Floor", "h3")); var floors = El("row");
        foreach (var f in data.floors) { string v = f.v; floors.Add(B(f.l, "chip" + (state.room.floor == v ? " on" : ""), () => core.Call("set", "room.floor", v))); }
        decorBody.Add(floors);
        decorBody.Add(L("Add furniture", "h3")); var grid = El("grid");
        foreach (var f in data.furn) {
            string k = f.k;
            var th = new SvgImage().Set("furnthumb:" + k, () => core.Call("furnThumb", k)); th.style.height = 70;
            var card = El("item", th, L(f.name)); card.RegisterCallback<ClickEvent>(_ => core.Call("addFurn", k)); grid.Add(card);
        }
        decorBody.Add(grid);
        decorBody.Add(L("In the room", "h3")); var placed = El("row");
        foreach (var it in state.room.items) { string id = it.id; if (furnByK.TryGetValue(it.k, out var f)) placed.Add(El("placed", L(f.name), B("×", null, () => core.Call("unplace", id)))); }
        if (state.room.items.Length == 0) placed.Add(L("Empty room", "muted"));
        decorBody.Add(placed);
    }

    void RenderLayers() {
        layersBody.Clear();
        var ls = core.Get<DLayers>("layers").list;
        for (int i = 0; i < ls.Length; i++) {
            string slot = ls[i].slot; var dot = El("dot"); dot.style.backgroundColor = C(ls[i].color);
            layersBody.Add(El("placed", L((i + 1) + "  ", "muted"), dot, L(ls[i].name), B("×", null, () => core.Call("remove", slot))));
        }
        if (ls.Length == 0) layersBody.Add(L("Just her underwear. Pick something from the wardrobe.", "muted"));
        if (Worn("top") != "" && Worn("bottom") != "") layersBody.Add(B(state.tucked ? "Top tucked in" : "Top worn out", "chip" + (state.tucked ? " on" : ""), () => core.Call("set", "tucked", state.tucked ? "false" : "true")));
    }

    void OpenGuide(int step) { guideStep = step; OpenDrawer(null); guide.style.display = DisplayStyle.Flex; RenderGuide(); }
    void RenderGuide() {
        guideText.text = core.Call("guideText", guideStep); guideCount.text = (guideStep + 1) + "/" + data.guide;
        guideBack.SetEnabled(guideStep > 0); guideNext.text = guideStep == data.guide - 1 ? "Got it" : "Next ›";
    }
    void CloseGuide() { guide.style.display = DisplayStyle.None; core.Call("guideDone"); }

    // ================================================================ beauty parlor
    VisualElement parlorBody, mirrorFrame, parlorSeg, zoomTabs; SvgImage mirrorDoll; string parlorTab = "avatar", zoom = "face", hairSig = "";
    readonly List<(VisualElement el, string path, string val)> binds = new List<(VisualElement, string, string)>();
    readonly List<(Slider s, Label o, string path)> sliders = new List<(Slider, Label, string)>();

    void BuildParlor(VisualElement pane) {
        mirrorDoll = new SvgImage(); mirrorFrame = El("mirror-frame", mirrorDoll);
        zoomTabs = El("tabs"); zoomTabs.style.marginTop = 10;
        foreach (var (k, label) in new[] { ("face", "Face"), ("full", "Full length") }) { string z = k; var b = B(label, null, () => { zoom = z; RefreshParlor(); }); b.name = "zoom-" + k; zoomTabs.Add(b); }
        parlorSeg = El("seg");
        foreach (var (k, label) in new[] { ("avatar", "Avatar"), ("hair", "Hair"), ("makeup", "Makeup") }) { string t = k; var b = B(label, null, () => { parlorTab = t; RefreshParlorPanel(); }); b.name = "seg-" + k; parlorSeg.Add(b); }
        parlorBody = Scroll();
        pane.Add(El("parlor", El("mirror", mirrorFrame, zoomTabs), El("panel", parlorSeg, parlorBody)));
    }

    VisualElement Field(string label, VisualElement content) => El("field", L(label, "k"), content);
    VisualElement Colours(string path, IEnumerable<string> colors, bool none = false, bool match = false) {
        var row = El("row");
        if (none) row.Add(Chip(path, "none", "None"));
        if (match) row.Add(Chip(path, "match", "Match hair"));
        foreach (var c in colors) { string v = c; var sw = Swatch(v, false, () => core.Call("set", path, v)); binds.Add((sw, path, v)); row.Add(sw); }
        return row;
    }
    Button Chip(string path, string val, string label) { var b = B(label, "chip", () => core.Call("set", path, val)); binds.Add((b, path, val)); return b; }
    VisualElement Chips(string path, params string[] valLabel) { var row = El("row"); for (int i = 0; i < valLabel.Length; i += 2) row.Add(Chip(path, valLabel[i], valLabel[i + 1])); return row; }
    VisualElement Chips(string path, DOpt[] opts) { var row = El("row"); foreach (var o in opts) row.Add(Chip(path, o.v, o.l)); return row; }
    VisualElement Strength(string path, float max = 1) {
        var s = new Slider(0, max) { value = 0 }; s.AddToClassList("slider"); var o = L("", "muted"); o.style.width = 40;
        s.RegisterValueChangedCallback(e => core.Call("set", path, e.newValue.ToString("0.###", CultureInfo.InvariantCulture)));
        sliders.Add((s, o, path)); var row = El("row", s, o); row.style.flexWrap = Wrap.NoWrap; return row;
    }

    void RefreshParlorPanel() {
        binds.Clear(); sliders.Clear(); parlorBody.Clear(); var p = data.pal; var b = parlorBody;
        foreach (var c in parlorSeg.Children()) c.EnableInClassList("on", c.name == "seg-" + parlorTab);
        if (parlorTab == "avatar") {
            if (On3) AvatarStudio(b);
            b.Add(L("Skin", "h3")); b.Add(Colours("skin", p.skin));
            b.Add(L("Eyes", "h3")); b.Add(Field("Shape", Chips("eyeShape", data.eyeShapes))); b.Add(Field("Colour", Colours("eyeColor", p.eyes)));
            b.Add(L("Brows", "h3")); b.Add(Field("Shape", Chips("brow", data.brows))); b.Add(Field("Colour", Colours("browColor", new[] { "#1c1412", "#3b2416", "#6b3a1f", "#a77b4f", "#d8c3a0" }, false, true)));
            b.Add(L("Underwear", "h3")); b.Add(Colours("underwear", new[] { "#f4c7d3", "#ffffff", "#1f1b22", "#e9c46a", "#c8b6e2", "#a9c6ef", "#e8c3a0", "#d9475e" }));
        } else if (parlorTab == "hair") {
            b.Add(L("Colour", "h3")); b.Add(Colours("hairColor", p.hair));
            b.Add(L("Style", "h3")); var grid = El("grid"); string skin = core.Call("get", "skin"), hc = core.Call("get", "hairColor");
            if (On3) {   // in 3D the styles are real hair pieces, chosen in the Hair Salon
                grid.Add(El("col", L("Hairstyles are done in the Hair Salon: braids, locs, puffs and more.", "muted"), B("Go to the Hair Salon", "btn primary", () => SetView("salon"))));
            } else foreach (var h in data.hair) {
                string k = h.v;
                var card = El("hair-card", new SvgImage().Set("hair:" + k + skin + hc, () => core.Call("hairCard", k)), L(h.l, "muted"));
                card.RegisterCallback<ClickEvent>(_ => core.Call("set", "hair", k)); binds.Add((card, "hair", k)); grid.Add(card);
            }
            b.Add(grid); hairSig = skin + hc;
        } else {
            if (On3) { MakeupStudio(b); RefreshBinds(); return; }      // in 3D makeup is painted on by hand
            b.Add(L("Looks", "h3")); var pr = El("row");
            foreach (var x in data.presets) {
                string name = x.name; var dots = El("row"); dots.style.marginTop = 5;
                foreach (var d in x.dots) { var dot = El("dot"); dot.style.backgroundColor = C(d); dots.Add(dot); }
                var card = El("preset", L(name), dots); card.RegisterCallback<ClickEvent>(_ => core.Call("preset", name)); pr.Add(card);
            }
            b.Add(pr);
            b.Add(L("Eyeshadow", "h3")); b.Add(Field("Style", Chips("makeup.shadow.style", "none", "None", "soft", "Soft Wash", "smoky", "Smoky", "winged", "Winged", "halo", "Halo")));
            b.Add(Field("Colour", Colours("makeup.shadow.color", p.shadow))); b.Add(Field("Strength", Strength("makeup.shadow.opacity")));
            b.Add(L("Eyeliner", "h3")); b.Add(Field("Style", Chips("makeup.liner.style", "none", "None", "tight", "Tightline", "classic", "Classic", "winged", "Winged", "cat", "Cat-eye", "smudged", "Smudged")));
            b.Add(Field("Colour", Colours("makeup.liner.color", p.liner)));
            b.Add(L("Lashes and lenses", "h3")); b.Add(Field("Mascara", Chips("makeup.mascara", "none", "None", "natural", "Natural", "volume", "Volume", "dramatic", "Dramatic"))); b.Add(Field("Contacts", Chips("makeup.contacts", data.contacts)));
            b.Add(L("Lips", "h3")); b.Add(Field("Colour", Colours("makeup.lips.color", p.lips, true))); b.Add(Field("Finish", Chips("makeup.lips.finish", "matte", "Matte", "gloss", "Gloss", "metallic", "Metallic")));
            b.Add(Field("Strength", Strength("makeup.lips.opacity"))); b.Add(Field("Liner", Colours("makeup.lips.liner", p.lipLiner, true)));
            b.Add(L("Cheeks", "h3")); b.Add(Field("Blush", Colours("makeup.blush.color", p.blush, true))); b.Add(Field("Placement", Chips("makeup.blush.place", "apple", "Apples", "lifted", "Lifted", "draped", "Draped")));
            b.Add(Field("Strength", Strength("makeup.blush.opacity", 0.8f))); b.Add(Field("Highlighter", Chips("makeup.highlight.color", p.hl))); b.Add(Field("Glow", Strength("makeup.highlight.opacity")));
            b.Add(Field("Contour", Strength("makeup.contour.opacity", 0.7f)));
        }
        RefreshBinds();
    }
    void RefreshBinds() {
        var cur = new Dictionary<string, string>();
        foreach (var (el, path, val) in binds) { if (!cur.TryGetValue(path, out var v)) cur[path] = v = core.Call("get", path); el.EnableInClassList("on", v == val); }
        foreach (var (s, o, path) in sliders) { float v = float.Parse(core.Call("get", path), CultureInfo.InvariantCulture); s.SetValueWithoutNotify(v); o.text = Mathf.RoundToInt(v * 100) + "%"; }
    }
    void RefreshParlor() {
        mirrorFrame.style.backgroundColor = C(state.room.wall);
        string z = zoom;
        if (On3) Doll3D(mirrorDoll, "mirror", z == "face" ? "face" : "full");
        else mirrorDoll.Set("doll:" + dollKey + ":" + z, () => core.Call("doll", z == "face" ? "140 84 120 160" : "0 70 400 750"));
        foreach (var c in zoomTabs.Children()) c.EnableInClassList("on", c.name == "zoom-" + zoom);
        if (parlorBody.childCount == 0 || (parlorTab == "hair" && hairSig != core.Call("get", "skin") + core.Call("get", "hairColor"))) RefreshParlorPanel(); else RefreshBinds();
    }

    // ================================================================ hair salon (3D)
    Image salonImg; VisualElement salonBody; float salonYaw, salonFrom; int salonW, salonH; bool salonTurn; string hairColor0 = "";
    static readonly HashSet<string> Made = new HashSet<string> { "groomafro", "blowout", "washngo", "silkpress", "boxbraids", "twists", "longlocs", "puffs", "highpuff", "twa", "frohawk", "fulani", "braidbun" };
    void BuildSalon(VisualElement pane) {
        salonImg = new Image { scaleMode = ScaleMode.StretchToFill }; salonImg.style.flexGrow = 1; salonImg.pickingMode = PickingMode.Ignore;
        var view = El(null, salonImg); view.style.flexGrow = 1; view.style.flexBasis = 0; view.style.marginRight = 20; view.style.overflow = Overflow.Hidden; view.style.backgroundColor = C("#ead9f7"); salonView = view;
        view.style.borderTopLeftRadius = view.style.borderTopRightRadius = view.style.borderBottomLeftRadius = view.style.borderBottomRightRadius = 20;
        view.RegisterCallback<GeometryChangedEvent>(e => { float k = view.scaledPixelsPerPoint; salonW = Mathf.RoundToInt(e.newRect.width * k); salonH = Mathf.RoundToInt(e.newRect.height * k); });
        view.RegisterCallback<PointerDownEvent>(e => { salonTurn = true; salonFrom = e.position.x; view.CapturePointer(e.pointerId); });   // drag to turn her and see the back
        view.RegisterCallback<PointerMoveEvent>(e => { if (!salonTurn || !view.HasPointerCapture(e.pointerId)) return; if (simStyle != null) SimStroke(e.position); else { salonYaw -= (e.position.x - salonFrom) * 0.7f; salonFrom = e.position.x; } });
        view.RegisterCallback<PointerUpEvent>(e => { if (view.HasPointerCapture(e.pointerId)) view.ReleasePointer(e.pointerId); salonTurn = false; });
        var turn = El("row", B("‹ Turn", "btn small", () => salonYaw -= 45), B("Front", "btn small", () => salonYaw = 0), B("Back", "btn small", () => salonYaw = 180), B("Turn ›", "btn small", () => salonYaw += 45));
        turn.style.position = Position.Absolute; turn.style.left = 14; turn.style.bottom = 14; view.Add(turn);
        salonBody = Scroll();
        pane.Add(El("parlor", view, El("panel", L("Hair Salon", "h2"), L("Pick a style and a colour. Drag her to turn her round.", "muted"), salonBody)));
    }
    void RefreshSalon() {
        var b = salonBody; b.Clear();
        if (!On3) { b.Add(L("The salon works in 3D.", "h3")); b.Add(B("Switch to 3D", "btn primary", () => Set3D(true, true))); salonImg.image = null; return; }
        if (simStyle != null) { SimPanel(b); return; }             // a session is under way: the steps, not the menu
        string me = Av, cur = Hair3D(me); var mine = look3 ?? MyLook(null);
        void Section(string title, List<(string val, string label)> opts) {
            b.Add(L(title, "h3")); var grid = El("grid");
            foreach (var (val, label) in opts) {
                string v = val; var look = MyLook(null); look.hair = v == "" ? null : v;
                var card = El("hair-card" + (cur == v ? " on" : ""), new SvgImage().Set3D("3d:hair:" + look.Key, (w, h) => stage3d.Shot(look, "portrait", w, h)), L(label, "muted"));
                string lab = label; card.RegisterCallback<ClickEvent>(_ => { if (v == cur) return; StartSim(v, lab); }); grid.Add(card);
            }
            b.Add(grid);
        }
        var african = new List<(string, string)>(); var more = new List<(string, string)> { ("", "Her own") };
        foreach (var e in stage3d.Hair) if (Made.Contains(e.id)) african.Add(("h:" + e.id, e.label));
        foreach (var e in stage3d.Hair) if (!Made.Contains(e.id)) african.Add(("h:" + e.id, e.label));
        foreach (var e in stage3d.Starlets) if (e.id != me && !SLStage3D.IsHelper(e.id) && Array.Exists(e.parts, q => q.mesh == "hair")) more.Add(("s:" + e.id, char.ToUpperInvariant(e.id[0]) + e.id.Substring(1) + "'s"));
        Section("African styles", african);
        b.Add(L("Colour", "h3")); var row = El("row"); string hc = core.Call("get", "hairColor");
        row.Add(B("Natural", "chip" + (hc == hairColor0 ? " on" : ""), () => core.Call("set", "hairColor", hairColor0)));
        foreach (var c in data.pal.hair) { string v = c; row.Add(Swatch(v, hc == v, () => core.Call("set", "hairColor", v))); }
        b.Add(row);
        Section("More styles", more);
    }
    void TickSalon() {
        if (!On3 || view != "salon" || salonW < 16 || picker.ClassListContains("on")) return;
        float sway = shotsDir == null && !salonTurn ? Mathf.Sin(Time.time * 0.6f) * 6f : 0;
        salonImg.image = stage3d.Salon(salonW, salonH, look3 ?? MyLook(null), salonYaw + sway);
    }

    // ================================================================ shop
    VisualElement shopTabs, shopGrid; SvgImage fitDoll; Label fitNote; string shopCat = "all"; readonly List<string> tryOn = new List<string>();
    void BuildShop(VisualElement pane) {
        fitDoll = new SvgImage(); fitNote = L("", "muted"); fitNote.style.marginTop = 8;
        var fitting = El("fitting", L("Fitting room", "h2"), El("fit-doll", fitDoll), fitNote, B("Clear", "btn small", () => { tryOn.Clear(); RefreshShop(); }));
        shopTabs = El("tabs"); shopGrid = Scroll("grid");
        pane.Add(El("shop", fitting, El("col grow", El("row", L("Boutique", "h2"), L("   Try pieces on before you buy. Everything you buy goes to your wardrobe.", "muted")), shopTabs, shopGrid)));
    }
    void RefreshShop() {
        shopTabs.Clear();
        foreach (var (k, label) in new[] { ("all", "All"), ("top", "Tops"), ("bottom", "Bottoms"), ("dress", "Dresses"), ("outer", "Jackets"), ("shoes", "Shoes"), ("acc", "Accessories"), ("sport", "Sports"), ("work", "Workwear"), ("goth", "Goth"), ("men", "Menswear") }) {
            string cat = k; shopTabs.Add(B(label, k == shopCat ? "on" : null, () => { shopCat = cat; RefreshShop(); }));
        }
        var list = new List<DItem>(Array.FindAll(data.items, i => i.price > 0 && Allowed(i) && (shopCat == "all" || i.cat == shopCat)));
        list.Sort((a, b) => Owns(a.id) != Owns(b.id) ? Owns(a.id).CompareTo(Owns(b.id)) : a.price.CompareTo(b.price));
        shopGrid.Clear();
        foreach (var it in list) {
            string id = it.id; bool own = Owns(id), trying = tryOn.Contains(id);
            var tags = El("row"); foreach (var t in it.tags) tags.Add(L(t, "tag"));
            var buy = own ? B("Wear", "btn small primary", () => { core.Call("wearNow", id); SetView("suite"); }) : B("Buy", "btn small primary", () => { core.Call("buy", id); tryOn.Remove(id); });
            if (!own) buy.SetEnabled(profile.coins >= it.price);
            VisualElement price = L("In your wardrobe", "owned-tag");
            if (!own) { var coin = new SvgImage("coin").Set("icon:coin", () => core.Call("icon", "coin")); price = El("row", coin, L(it.price + " coins", "price")); }
            Action toggle = () => {
                if (tryOn.Contains(id)) tryOn.Remove(id); else { tryOn.RemoveAll(o => itemById[o].slot == it.slot); tryOn.Add(id); }
                RefreshShop();
            };
            var rowB = El("row", B(trying ? "Take off" : "Try on", "btn small", toggle), buy); rowB.style.marginTop = 6;
            var thumb = El("shop-thumb", Thumb(it, false)); DragToWear(thumb, it, pos => fitDoll.parent.worldBound.Contains(pos), toggle);   // drag a piece onto her in the fitting room
            shopGrid.Add(El("shop-card" + (trying ? " trying" : ""), thumb, L(it.name, "look-name"), tags, price, rowB));
        }
        string ids = "[" + string.Join(",", tryOn.ConvertAll(s => "\"" + s + "\"")) + "]";
        if (On3) Doll3D(fitDoll, "fit", "full", MyLook(ids)); else fitDoll.Set("fit:" + dollKey + ids, () => core.Call("tryOnDoll", ids));
        fitNote.text = tryOn.Count > 0 ? "Trying on: " + string.Join(", ", tryOn.ConvertAll(s => itemById[s].name)) : "Press Try on to see a piece on her before you buy.";
    }

    // ================================================================ contests
    VisualElement themeTabs, contestBanner, contestDollBox, contestSide; SvgImage contestDoll; Label contestTitle, contestDesc; string themeSel; DResult lastResult; string lastResultTheme;
    void BuildContests(VisualElement pane) {
        themeTabs = El("tabs");
        contestBanner = El("contest-banner", L("STYLE CONTEST", "tile-h"), contestTitle = L("", "contest-title"), contestDesc = L(""));
        contestDoll = new SvgImage(); contestDollBox = El("contest-doll", contestDoll);
        contestSide = Scroll();
        pane.Add(themeTabs); pane.Add(contestBanner); pane.Add(El("contest-body", contestDollBox, contestSide));
    }
    void RefreshContests() {
        if (themeSel == null) themeSel = profile.featured;
        var th = Array.Find(data.themes, t => t.id == themeSel);
        themeTabs.Clear();
        foreach (var t in data.themes) { string id = t.id; themeTabs.Add(B(t.name + (t.id == profile.featured ? "  · Today" : ""), t.id == themeSel ? "on" : null, () => { themeSel = id; RefreshContests(); })); }
        contestBanner.style.backgroundColor = C(th.bg[0]); contestTitle.text = th.name; contestDesc.text = th.desc + "   Theme: " + string.Join(", ", th.tags) + ". Makeup: " + th.tip.ToLowerInvariant() + ".";
        contestDollBox.style.backgroundColor = C(th.bg[1]);
        if (On3) Doll3D(contestDoll, "contest", "full"); else contestDoll.Set("doll:" + dollKey + ":contest", () => core.Call("doll"));
        var s = contestSide; s.Clear();
        var brief = core.Get<DBrief>("brief", th.id);
        s.Add(L("Judging checklist", "h3"));
        foreach (var l in brief.lines) { var tick = Icon(l.ok ? "check" : "ring", l.ok ? "#1d9a63" : "#c9bfd6", 18); tick.style.marginRight = 8; var row = El("row", tick, L(l.text, "check" + (l.ok ? " ok" : ""))); row.style.flexWrap = Wrap.NoWrap; s.Add(row); }
        s.Add(L(brief.best >= 0 ? "Today's best: " + brief.best + " points" : "Prize: up to 50 coins and 25 fame", "muted"));
        var acts = El("row", B("Enter the contest", "btn primary big", EnterContest), B("Change her outfit", "btn", () => { SetView("suite"); OpenDrawer("wardrobe"); })); acts.style.marginTop = 12; s.Add(acts);
        if (lastResult != null && lastResultTheme == th.id) {
            var r = lastResult;
            var ring = El("score-ring", L(r.score.ToString(), "score-num"), L("points", "muted")); ring.style.borderTopColor = ring.style.borderBottomColor = ring.style.borderLeftColor = ring.style.borderRightColor = C(r.medalColor);
            string won = r.repeat ? "You've already won today's prize for this contest. Beat your best score to earn more." : r.coins > 0 || r.fame > 0 ? "You won " + r.coins + " coins and " + r.fame + " fame" + (r.beat ? " for beating your best score." : ".") : "No prize this time. Follow the checklist and try again.";
            s.Add(El("result", ring, El("col grow", L(r.medal, "h2"), L(won))));
            for (int i = 0; i < data.judges.Length; i++) {
                var j = data.judges[i]; var say = r.judges[i];
                var face = new SvgImage().Set("judge:" + i, () => core.Call("portrait", j.face, j.lips));
                s.Add(El("judge", El("judge-face", face), El("col grow", El("row", L(j.name, "look-name gap"), L(j.role, "muted gap"), Stars(say.stars)), L("“" + say.say + "”"))));
            }
        }
    }
    void EnterContest() { lastResult = core.Get<DResult>("enter", themeSel); lastResultTheme = themeSel; dirty = true; }

    // ================================================================ album and the save dialog
    VisualElement albumBody, saveOverlay; SvgImage savePreview; TextField lookName; readonly Dictionary<string, Texture2D> photos = new Dictionary<string, Texture2D>();
    void BuildAlbum(VisualElement pane) { pane.Add(El("row", L("My Album", "h2"))); pane.Add(albumBody = Scroll("grid")); }
    void RefreshAlbum() {
        albumBody.Clear();
        var looks = core.Get<DLooks>("album").list;
        if (looks.Length == 0) { var e = L("<b>Your album is empty</b>\nDress your starlet, then press Camera or Save photo in My Suite.", "empty"); e.style.flexGrow = 1; albumBody.Add(e); return; }
        foreach (var l in looks) {
            string id = l.id; var img = El("look-img");
            if (!string.IsNullOrEmpty(l.thumb) && File.Exists(l.thumb)) {
                if (!photos.TryGetValue(l.thumb, out var t)) { t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(l.thumb)); photos[l.thumb] = t; }
                img.style.backgroundImage = t;
            }
            albumBody.Add(El("look", img, El("look-meta", L(l.name, "look-name"), L(l.date, "muted"),
                El("row", B("Wear", "btn small", () => { core.Call("albumWear", id); SetView("suite"); }), B("Delete", "btn small", () => { core.Call("albumDel", id); RefreshAlbum(); })))));
        }
    }
    void BuildSaveModal() {
        savePreview = new SvgImage(); lookName = new TextField(); lookName.AddToClassList("input"); lookName.textEdition.placeholder = "Name this look";
        saveOverlay = El("overlay", El("modal", L("Save to My Album", "h2"), L("A photo of your starlet in her suite. You earn 5 coins and 10 fame.", "muted"), El("modal-prev", savePreview), lookName,
            El("row", El("grow"), B("Cancel", "btn", () => saveOverlay.RemoveFromClassList("on")), B("Save photo", "btn primary", () => ConfirmSave(lookName.value)))));
        saveOverlay.Q(className: "modal-prev").style.marginTop = 10;
        overlayHost.Add(saveOverlay);
    }
    void OpenSave() {
        lookName.value = ""; saveOverlay.AddToClassList("on");
        string sig = Sig(core.Call("state")); var room = state.room; string id = Av;
        savePreview.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        if (On3) savePreview.Set3D("3d:photo:" + sig, (w, h) => stage3d.RoomShot("photo" + sig, w, Mathf.RoundToInt(w * RoomH / RoomW), room, furnByK, FurnSvg, look3 ?? MyLook(null)));
        else savePreview.Set("photo:" + sig, () => core.Call("roomFull"));
    }
    void ConfirmSave(string name) {
        const int w = 750, h = 465;
        Texture2D tex;
        if (On3) tex = SLStage3D.Read(stage3d.RoomShot("save" + Sig(core.Call("state")), w, h, state.room, furnByK, FurnSvg, look3 ?? MyLook(null)));
        else { tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.LoadRawTextureData(SLArt.RenderNow(core.Call("roomFull"), w, h)); tex.Apply(); }
        string dir = shotsDir ?? Path.Combine(Application.persistentDataPath, "album"); Directory.CreateDirectory(dir); // the screenshot tour keeps its photo out of the real album
        string file = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
        File.WriteAllBytes(file, tex.EncodeToPNG()); photos[file] = tex;
        core.Call("albumAdd", name ?? "", file);
        saveOverlay.RemoveFromClassList("on"); core.Flush();
    }
}
