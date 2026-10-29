// Hands-on beauty: a makeup studio where you paint on the starlet's face in the mirror, and a salon session where
// you wash, dry and style her hair yourself. Part of SLApp (3D only).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

public partial class SLApp {
    // ================================================================ makeup studio (Beauty Parlor > Makeup)
    // The mirror shows her face live. A second, hidden picture of the same view records which point of her skin
    // texture is under every pixel, so a brush stroke on the mirror is painted into a makeup layer on her skin itself:
    // it stays on her in every room, from every side.
    sealed class MkTool { public string id, label; public float flow, max, size; public bool hard, erase; public string[] colors; }
    MkTool[] mkTools; MkTool mkTool; readonly Dictionary<string, string> mkColor = new Dictionary<string, string>(); readonly Dictionary<string, int> mkVer = new Dictionary<string, int>();
    Image makeupImg; Color[] mkUV; int mkW, mkH; string mkFor = ""; Texture2D mkTex; Color32[] mkPx, mkUndo; float mkSize = 14; bool mkDown; Vector2 mkLast;

    string MkKey(string sid, string face) => (shotsDir != null ? "shots-" : "") + sid + "-" + face;
    /// <summary>Called for every look: the makeup she has on, if any has been painted.</summary>
    void WearMakeup(Look3 L, string sid, bool create = false) {
        if (stage3d == null) return; string key = MkKey(sid, L.id);
        L.makeup = stage3d.Makeup(key, create); mkVer.TryGetValue(key, out L.makeupVer);
    }
    Look3 MkLook() { var L = MyLook(null); Array.Clear(L.shape, 0, L.shape.Length); L.height = 1; WearMakeup(L, Av, true); return L; }   // painted on her face at its natural size

    void MakeupStudio(VisualElement b) {
        if (mkTools == null) {
            var p = data.pal;
            mkTools = new[] {
                new MkTool { id = "lips", label = "Lipstick", flow = 0.55f, max = 0.9f, size = 7, hard = true, colors = p.lips },
                new MkTool { id = "blush", label = "Blush", flow = 0.06f, max = 0.42f, size = 30, colors = p.blush },
                new MkTool { id = "shadow", label = "Eyeshadow", flow = 0.12f, max = 0.7f, size = 12, colors = p.shadow },
                new MkTool { id = "liner", label = "Eyeliner", flow = 0.7f, max = 0.95f, size = 3.5f, hard = true, colors = p.liner },
                new MkTool { id = "glow", label = "Highlighter", flow = 0.05f, max = 0.4f, size = 22, colors = new[] { "#fff4dc", "#ffe9b8", "#f6d7c3", "#ffffff", "#f7c6d9" } },
                new MkTool { id = "erase", label = "Remover", flow = 0.5f, max = 1, size = 18, erase = true, colors = new string[0] } };
            mkTool = mkTools[0]; mkSize = mkTool.size;
            foreach (var t in mkTools) mkColor[t.id] = t.colors.Length > 0 ? t.colors[Mathf.Min(2, t.colors.Length - 1)] : "#000000";
        }
        if (makeupImg == null) {
            makeupImg = new Image { scaleMode = ScaleMode.StretchToFill }; makeupImg.style.position = Position.Absolute;
            makeupImg.style.left = makeupImg.style.top = makeupImg.style.right = makeupImg.style.bottom = 0; mirrorFrame.Add(makeupImg);
            makeupImg.RegisterCallback<PointerDownEvent>(e => { if (mkUV == null || e.button != 0) return; mkDown = true; mkUndo = (Color32[])mkPx.Clone(); makeupImg.CapturePointer(e.pointerId); mkLast = MkPos(e.localPosition); MkStamp(mkLast); MkShow(); });
            makeupImg.RegisterCallback<PointerMoveEvent>(e => {
                if (!mkDown || !makeupImg.HasPointerCapture(e.pointerId)) return;
                Vector2 to = MkPos(e.localPosition); float step = Mathf.Max(1f / mkW, mkSize / 520f / 3f); int n = Mathf.Max(1, Mathf.CeilToInt((to - mkLast).magnitude / step));
                for (int i = 1; i <= n; i++) MkStamp(Vector2.Lerp(mkLast, to, i / (float)n));
                mkLast = to; MkShow();
            });
            makeupImg.RegisterCallback<PointerUpEvent>(e => { if (makeupImg.HasPointerCapture(e.pointerId)) makeupImg.ReleasePointer(e.pointerId); if (mkDown) { mkDown = false; MkSave(); } });
        }
        b.Add(L("Makeup studio", "h3")); b.Add(L("Pick a tool and a colour, then paint straight onto her face in the mirror. It stays on her everywhere.", "muted"));
        var tools = El("row");
        foreach (var t in mkTools) { var tt = t; tools.Add(B(t.label, "chip" + (t == mkTool ? " on" : ""), () => { mkTool = tt; mkSize = tt.size; RefreshParlorPanel(); })); }
        b.Add(Field("Tool", tools));
        if (mkTool.colors.Length > 0) {
            var cols = El("row"); string cur = mkColor[mkTool.id];
            foreach (var c in mkTool.colors) { string v = c; if (v == "none") continue; cols.Add(Swatch(v, cur == v, () => { mkColor[mkTool.id] = v; RefreshParlorPanel(); })); }
            b.Add(Field("Colour", cols));
        }
        var sl = new Slider(2, 44) { value = mkSize }; sl.AddToClassList("slider"); sl.RegisterValueChangedCallback(e => mkSize = e.newValue);
        var srow = El("row", sl); srow.style.flexWrap = Wrap.NoWrap; b.Add(Field("Brush size", srow));
        var acts = El("row", B("Undo last stroke", "btn small", () => { if (mkUndo == null || mkPx == null) return; mkPx = (Color32[])mkUndo.Clone(); MkShow(); MkSave(); }),
            B("Wipe it all off", "btn small", () => { if (mkPx == null) return; mkUndo = (Color32[])mkPx.Clone(); for (int i = 0; i < mkPx.Length; i++) mkPx[i] = new Color32(0, 0, 0, 0); MkShow(); MkSave(); }));
        acts.style.marginTop = 12; b.Add(acts);
        b.Add(L("Tips", "h3")); b.Add(L("Lipstick and eyeliner draw solid lines: use a small brush. Blush, eyeshadow and highlighter build up softly: go over the same spot for more. The remover takes makeup off where you rub.", "muted"));
    }
    Vector2 MkPos(Vector3 local) => new Vector2(local.x / Mathf.Max(1, makeupImg.resolvedStyle.width), 1 - local.y / Mathf.Max(1, makeupImg.resolvedStyle.height));
    void MkShow() { if (mkTex != null) { mkTex.SetPixels32(mkPx); mkTex.Apply(false); } }
    void MkSave() {
        if (mkTex == null) return; string key = MkKey(Av, MkLook().id);
        File.WriteAllBytes(SLStage3D.MakeupFile(key), mkTex.EncodeToPNG()); mkVer.TryGetValue(key, out int v); mkVer[key] = v + 1; dirty = true;
    }
    /// <summary>One dab of the brush at a point of the mirror (0..1 across, 0..1 up).</summary>
    void MkStamp(Vector2 n) {
        if (mkUV == null || mkPx == null) return;
        float r = Mathf.Max(1.2f, mkSize * mkW / 520f); int cx = (int)(n.x * mkW), cy = (int)(n.y * mkH), ri = Mathf.CeilToInt(r), T = mkTex.width;
        Color32 col = C(mkColor[mkTool.id]);
        for (int dy = -ri; dy <= ri; dy++) for (int dx = -ri; dx <= ri; dx++) {
            float d = Mathf.Sqrt(dx * dx + dy * dy); if (d > r) continue; int x = cx + dx, y = cy + dy; if (x < 0 || y < 0 || x >= mkW || y >= mkH) continue;
            var uv = mkUV[y * mkW + x]; if (uv.b < 0.5f) continue;                       // not her skin: hair, eyes, the wall behind
            float f = mkTool.hard ? (d < r * 0.75f ? 1 : 0.45f) : (1 - d / r) * (1 - d / r), a = mkTool.flow * f;
            int tx = (int)(uv.r * T), ty = (int)(uv.g * T);
            for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++) {
                int px = tx + ox, py = ty + oy; if (px < 0 || py < 0 || px >= T || py >= T) continue; int i = py * T + px; var dst = mkPx[i]; float da = dst.a / 255f;
                if (mkTool.erase) { dst.a = (byte)(da * (1 - a) * 255); mkPx[i] = dst; continue; }
                float oa = Mathf.Max(da, Mathf.Min(mkTool.max, a + da * (1 - a))), t = Mathf.Clamp01(a / Mathf.Max(oa, 0.001f));
                mkPx[i] = new Color32((byte)Mathf.Lerp(dst.r, col.r, t), (byte)Mathf.Lerp(dst.g, col.g, t), (byte)Mathf.Lerp(dst.b, col.b, t), (byte)(oa * 255));
            }
        }
    }
    void TickMakeup() {
        bool on = On3 && view == "parlor" && parlorTab == "makeup" && makeupImg != null && !picker.ClassListContains("on");
        if (makeupImg != null) makeupImg.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        if (!on) return;
        float k = makeupImg.scaledPixelsPerPoint; int w = Mathf.RoundToInt(makeupImg.resolvedStyle.width * k), h = Mathf.RoundToInt(makeupImg.resolvedStyle.height * k); if (w < 16 || h < 16) return;
        var look = MkLook(); string sig = look.id + "|" + look.hair + "|" + w + "x" + h;
        if (sig != mkFor || mkUV == null) {       // a new face, hairstyle or mirror size: map the mirror to her skin again
            mkFor = sig; mkW = w; mkH = h; mkUV = stage3d.FaceUV(w, h, look); mkTex = look.makeup; mkPx = mkTex.GetPixels32(); mkUndo = null;
        }
        makeupImg.image = stage3d.FaceLive(w, h, look);
    }
    /// <summary>For the screenshot tour: a full face painted by code, with the same brush as the pointer uses.</summary>
    void MkDemo() {
        if (mkUV == null) return; mkUndo = (Color32[])mkPx.Clone();
        void Stroke(string tool, string color, float size, params float[] xy) {
            mkTool = Array.Find(mkTools, t => t.id == tool); mkColor[tool] = color; mkSize = size;
            for (int i = 0; i + 3 < xy.Length; i += 2) for (int s = 0; s <= 24; s++) MkStamp(Vector2.Lerp(new Vector2(xy[i], xy[i + 1]), new Vector2(xy[i + 2], xy[i + 3]), s / 24f));
        }
        Stroke("blush", "#f19a9a", 34, 0.33f, 0.47f, 0.36f, 0.48f); Stroke("blush", "#f19a9a", 34, 0.67f, 0.47f, 0.64f, 0.48f);
        Stroke("shadow", "#7a4fa3", 12, 0.36f, 0.585f, 0.44f, 0.585f); Stroke("shadow", "#7a4fa3", 12, 0.56f, 0.585f, 0.64f, 0.585f);
        Stroke("lips", "#b3263a", 6, 0.45f, 0.395f, 0.55f, 0.395f, 0.5f, 0.385f, 0.45f, 0.395f);
        MkShow(); MkSave(); mkTool = mkTools[0]; mkSize = mkTool.size;
    }

    // ================================================================ salon session (Hair Salon)
    // Picking a style starts a session: shampoo, rinse, blow-dry, comb out, then style, each done by dragging the
    // tool over her hair until its bar fills. The new style goes on when the last step is done.
    static readonly (string name, string hint, string fx)[] SimSteps = {
        ("Shampoo", "Rub the shampoo all over her hair.", "bubble"), ("Rinse", "Rinse the lather out with the shower head.", "water"),
        ("Blow-dry", "Dry her hair with the dryer.", "air"), ("Comb out", "Comb through from the roots to the tips.", "comb"), ("Style", "Work the style in, section by section.", "spark") };
    string simStyle, simLabel; int simStep; float simProg; Vector2 simLast; bool simHas; float simFx; VisualElement salonView, simLayer, simBar; readonly List<VisualElement> simBubbles = new List<VisualElement>();

    void StartSim(string style, string label) {
        if (!On3) { SetHair3D(Av, style); return; }
        simStyle = style; simLabel = label; simStep = 0; simProg = 0; simHas = false; ClearSimFx(); dirty = true;
        ShowToast("Let's do it: start with the shampoo");
    }
    void ClearSimFx() { if (simLayer != null) simLayer.Clear(); simBubbles.Clear(); }
    void EndSim(bool apply) {
        string style = simStyle; simStyle = null; ClearSimFx();
        if (apply) {
            SetHair3D(Av, style);
            string key = "salon." + Today; bool first = core.Pref(key, "") == "";
            if (first) { core.SetPref(key, "1"); core.Call("earn", 5d, 3d); ShowToast("Beautiful work! " + simLabel + " done: +5 coins, +3 fame"); } else ShowToast("Beautiful work! " + simLabel + " is done");
        }
        dirty = true;
    }
    void SimPanel(VisualElement b) {
        b.Add(L("Doing: " + simLabel, "h3"));
        string verb = simLabel.Contains("braid") || simLabel.Contains("Braid") || simLabel.Contains("Cornrow") ? "Braid" : simLabel.Contains("wist") ? "Twist" : simLabel.Contains("loc") || simLabel.Contains("Loc") ? "Retwist" : "Style";
        for (int i = 0; i < SimSteps.Length; i++) {
            string name = i == SimSteps.Length - 1 ? verb : SimSteps[i].name; bool done = i < simStep, now = i == simStep;
            var tick = Icon(done ? "check" : "ring", done ? "#1d9a63" : now ? "#9b6bf2" : "#c9bfd6", 18); tick.style.marginRight = 8;
            var row = El("row", tick, L(name, now ? "look-name" : "check" + (done ? " ok" : ""))); row.style.flexWrap = Wrap.NoWrap; b.Add(row);
        }
        var hint = L(SimSteps[simStep].hint + " Drag over her in the picture.", "muted"); hint.style.marginTop = 10; hint.style.whiteSpace = WhiteSpace.Normal; b.Add(hint);
        simBar = El(null); simBar.style.height = 12; simBar.style.backgroundColor = C("#9b6bf2"); simBar.style.width = Length.Percent(simProg * 100);
        simBar.style.borderTopLeftRadius = simBar.style.borderTopRightRadius = simBar.style.borderBottomLeftRadius = simBar.style.borderBottomRightRadius = 6;
        var track = El(null, simBar); track.style.height = 12; track.style.backgroundColor = C("#ece6f7"); track.style.marginTop = 8; track.style.marginBottom = 12;
        track.style.borderTopLeftRadius = track.style.borderTopRightRadius = track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 6; b.Add(track);
        b.Add(El("row", B("Skip: just put the style on", "btn small", () => EndSim(true)), B("Cancel", "btn small", () => EndSim(false))));
    }
    /// <summary>The pointer dragged over the salon picture during a session.</summary>
    void SimStroke(Vector2 world) => SimTouch(salonView.WorldToLocal(world));
    void SimTouch(Vector2 p) {
        if (simStyle == null || salonView == null) return;
        if (simLayer == null) { simLayer = El(null); simLayer.pickingMode = PickingMode.Ignore; simLayer.style.position = Position.Absolute; simLayer.style.left = simLayer.style.top = simLayer.style.right = simLayer.style.bottom = 0; salonView.Add(simLayer); }
        if (!simHas) { simHas = true; simLast = p; return; }
        float d = (p - simLast).magnitude; simLast = p; if (d > 220) return;        // a jump, not a stroke
        simProg += d / 2400f; simFx += d;
        while (simFx > 16) { simFx -= 16; SimFx(p + new Vector2(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(-18f, 18f))); }
        if (simBar != null) simBar.style.width = Length.Percent(Mathf.Clamp01(simProg) * 100);
        if (simProg < 1) return;
        simProg = 0; simStep++; simHas = false;
        if (simStep == 2) ClearSimFx();
        if (simStep >= SimSteps.Length) EndSim(true); else { ShowToast(SimSteps[simStep - 1].name + " done. Next: " + SimSteps[simStep].name.ToLowerInvariant()); dirty = true; }
    }
    void SimFx(Vector2 p) {
        string kind = SimSteps[simStep].fx; var e = El(null); e.pickingMode = PickingMode.Ignore; e.style.position = Position.Absolute;
        void Round(float size, string color, float opacity) {
            e.style.width = e.style.height = size; e.style.left = p.x - size / 2; e.style.top = p.y - size / 2; e.style.backgroundColor = C(color); e.style.opacity = opacity;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = size / 2;
        }
        if (kind == "bubble") {
            Round(UnityEngine.Random.Range(12f, 28f), "#ffffff", 0.88f); e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = 1;
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = C("#d9c7ee"); simLayer.Add(e); simBubbles.Add(e);
            if (simBubbles.Count > 170) { simBubbles[0].RemoveFromHierarchy(); simBubbles.RemoveAt(0); }
            return;
        }
        if (kind == "water") {       // water takes the lather with it
            for (int i = simBubbles.Count - 1; i >= 0; i--) { var bb = simBubbles[i]; var c = new Vector2(bb.resolvedStyle.left + bb.resolvedStyle.width / 2, bb.resolvedStyle.top + bb.resolvedStyle.height / 2); if ((c - p).magnitude < 46) { bb.RemoveFromHierarchy(); simBubbles.RemoveAt(i); } }
            Round(UnityEngine.Random.Range(6f, 11f), "#7fc4f2", 0.8f); e.style.height = e.resolvedStyle.width * 1.8f;
        } else if (kind == "air") { Round(8, "#ffffff", 0.5f); e.style.width = UnityEngine.Random.Range(26f, 46f); e.style.height = 3; }
        else if (kind == "comb") { Round(6, "#c9a45c", 0.75f); e.style.width = 2; e.style.height = UnityEngine.Random.Range(22f, 38f); }
        else Round(UnityEngine.Random.Range(6f, 12f), "#ffd84a", 0.95f);
        simLayer.Add(e); e.schedule.Execute(() => e.RemoveFromHierarchy()).StartingIn(kind == "water" ? 520 : 380);
    }
}
