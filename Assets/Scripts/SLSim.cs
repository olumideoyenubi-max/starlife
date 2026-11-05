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
    // it stays on her in every room, from every side. Each product also paints its finish into a second layer (red:
    // gloss, green: metallic, blue: glitter), which the skin shader turns into wet shine, foil and twinkling sparks.
    sealed class MkTool { public string id, label, finish = "matte"; public float flow, max, size; public bool hard, erase, scatter; public string[] colors; }
    static readonly (string id, string label, float gloss, float metal, float glitter)[] MkFinishes = {
        ("matte", "Matte", 0, 0, 0), ("satin", "Satin", 0.45f, 0.05f, 0), ("gloss", "Gloss", 1, 0, 0), ("metallic", "Metallic", 0.75f, 0.9f, 0), ("glitter", "Glitter", 0.6f, 0.35f, 1) };
    MkTool[] mkTools; MkTool mkTool; readonly Dictionary<string, string> mkColor = new Dictionary<string, string>(); readonly Dictionary<string, int> mkVer = new Dictionary<string, int>();
    Image makeupImg; Color[] mkUV; int mkW, mkH; string mkFor = ""; Texture2D mkTex, mkFxTex; Color32[] mkPx, mkFxPx, mkUndo, mkFxUndo; float mkSize = 14; bool mkDown; Vector2 mkLast;
    // whose face is in the mirror: hers, or a makeover client's (SLMakeover.cs)
    Look3 mkSubject; string mkSubjectKey; Action mkPending; Vector4 mkMarks = new Vector4(0.5f, 0.585f, 0.2f, 0.4f);
    Vector3 mkLearnt = new Vector3(0.52f, 0.3f, 0.33f);     // eyes' height, pupil distance and mouth's height as shares of the head's outline (updated as faces are found)
    readonly System.Random mkRnd = new System.Random(7);

    string MkKey(string sid, string face) => (shotsDir != null ? "shots-" : "") + sid + "-" + face;
    /// <summary>Called for every look: the makeup she has on, if any has been painted.</summary>
    void WearMakeup(Look3 L, string sid, bool create = false) {
        if (stage3d == null) return; string key = MkKey(sid, L.id);
        L.makeup = stage3d.Makeup(key, create); L.makeupFx = stage3d.Makeup(key + "-fx", create); mkVer.TryGetValue(key, out L.makeupVer);
    }
    Look3 MkLook() { if (mkSubject != null) return mkSubject; var L = MyLook(null); Array.Clear(L.shape, 0, L.shape.Length); L.height = 1; WearMakeup(L, Av, true); return L; }   // painted on her face at its natural size
    string MkSaveKey() => mkSubject != null ? mkSubjectKey : MkKey(Av, MkLook().id);

    void MkInitTools() {
        if (mkTools != null) return;
        var p = data.pal;
        string[] skin = { "#f6d7c3", "#e9b996", "#d39a72", "#b57a52", "#8d5a3b", "#5e3a25" };
        mkTools = new[] {
            new MkTool { id = "base", label = "Foundation", flow = 0.05f, max = 0.38f, size = 40, colors = skin, finish = "satin" },
            new MkTool { id = "conceal", label = "Concealer", flow = 0.08f, max = 0.45f, size = 12, colors = new[] { "#fbe3d0", "#f2c9a8", "#dcaa82", "#bb8a62", "#90603f" }, finish = "matte" },
            new MkTool { id = "contour", label = "Contour", flow = 0.05f, max = 0.4f, size = 20, colors = new[] { "#b98a6a", "#9b6b4c", "#7a4e35", "#5c3826" }, finish = "matte" },
            new MkTool { id = "blush", label = "Blush", flow = 0.06f, max = 0.42f, size = 30, colors = p.blush, finish = "satin" },
            new MkTool { id = "glow", label = "Highlighter", flow = 0.05f, max = 0.4f, size = 22, colors = new[] { "#fff4dc", "#ffe9b8", "#f6d7c3", "#ffffff", "#f7c6d9", "#e9c46a" }, finish = "metallic" },
            new MkTool { id = "brows", label = "Brow pencil", flow = 0.35f, max = 0.8f, size = 4, hard = true, colors = new[] { "#2a1a12", "#4a3020", "#6b4a33", "#141010", "#9a7354" } },
            new MkTool { id = "shadow", label = "Eyeshadow", flow = 0.12f, max = 0.7f, size = 12, colors = p.shadow, finish = "satin" },
            new MkTool { id = "liner", label = "Eyeliner", flow = 0.7f, max = 0.95f, size = 3.5f, hard = true, colors = p.liner, finish = "gloss" },
            new MkTool { id = "lips", label = "Lipstick", flow = 0.55f, max = 0.9f, size = 7, hard = true, colors = p.lips, finish = "satin" },
            new MkTool { id = "gloss", label = "Lip gloss", flow = 0.3f, max = 0.35f, size = 7, colors = new[] { "#ffffff", "#ffb3c7", "#ff6f91", "#e8a07a", "#c94f7c" }, finish = "gloss" },
            new MkTool { id = "glitter", label = "Glitter", flow = 0.2f, max = 0.5f, size = 14, colors = new[] { "#f5d76e", "#e8e8f0", "#ff9ed2", "#8fd3ff", "#b78cff", "#3fe0c5" }, finish = "glitter" },
            new MkTool { id = "freckles", label = "Freckles", flow = 0.55f, max = 0.6f, size = 26, hard = true, scatter = true, colors = new[] { "#8a5a3c", "#6b4430", "#a9714d" } },
            new MkTool { id = "erase", label = "Remover", flow = 0.5f, max = 1, size = 18, erase = true, colors = new string[0] } };
        mkTool = mkTools[Array.FindIndex(mkTools, t => t.id == "lips")]; mkSize = mkTool.size;
        foreach (var t in mkTools) mkColor[t.id] = t.colors.Length > 0 ? t.colors[Mathf.Min(2, t.colors.Length - 1)] : "#000000";
    }
    void MkInitMirror() {
        if (makeupImg != null) return;
        makeupImg = new Image { scaleMode = ScaleMode.StretchToFill }; makeupImg.style.position = Position.Absolute;
        makeupImg.style.left = makeupImg.style.top = makeupImg.style.right = makeupImg.style.bottom = 0; mirrorFrame.Add(makeupImg);
        makeupImg.RegisterCallback<PointerDownEvent>(e => { if (mkUV == null || e.button != 0) return; mkDown = true; MkKeepUndo(); makeupImg.CapturePointer(e.pointerId); mkLast = MkPos(e.localPosition); MkStamp(mkLast); MkShow(); });
        makeupImg.RegisterCallback<PointerMoveEvent>(e => {
            if (!mkDown || !makeupImg.HasPointerCapture(e.pointerId)) return;
            Vector2 to = MkPos(e.localPosition); float step = Mathf.Max(1f / mkW, mkSize / 520f / 3f); int n = Mathf.Max(1, Mathf.CeilToInt((to - mkLast).magnitude / step));
            for (int i = 1; i <= n; i++) MkStamp(Vector2.Lerp(mkLast, to, i / (float)n));
            mkLast = to; MkShow();
        });
        makeupImg.RegisterCallback<PointerUpEvent>(e => { if (makeupImg.HasPointerCapture(e.pointerId)) makeupImg.ReleasePointer(e.pointerId); if (mkDown) { mkDown = false; MkSave(); } });
    }
    void MkKeepUndo() { if (mkPx != null) mkUndo = (Color32[])mkPx.Clone(); if (mkFxPx != null) mkFxUndo = (Color32[])mkFxPx.Clone(); }

    void MakeupStudio(VisualElement b) {
        MkInitTools(); MkInitMirror(); mkSubject = null;
        b.Add(L("Makeup studio", "h3")); b.Add(L("Pick a product, a shade and a finish, then paint straight onto " + her + " face in the mirror. It stays on " + him + " everywhere.", "muted"));
        var tools = El("row");
        foreach (var t in mkTools) { var tt = t; tools.Add(B(t.label, "chip" + (t == mkTool ? " on" : ""), () => { mkTool = tt; mkSize = tt.size; RefreshParlorPanel(); })); }
        b.Add(Field("Product", tools));
        if (mkTool.colors.Length > 0) {
            var cols = El("row"); string cur = mkColor[mkTool.id];
            foreach (var c in mkTool.colors) { string v = c; if (v == "none") continue; cols.Add(Swatch(v, cur == v, () => { mkColor[mkTool.id] = v; RefreshParlorPanel(); })); }
            b.Add(Field("Shade", cols));
            var fin = El("row");
            foreach (var f in MkFinishes) { string id = f.id; fin.Add(B(f.label, "chip" + (mkTool.finish == id ? " on" : ""), () => { mkTool.finish = id; RefreshParlorPanel(); })); }
            b.Add(Field("Finish", fin));
        }
        var sl = new Slider(2, 44) { value = mkSize }; sl.AddToClassList("slider"); sl.RegisterValueChangedCallback(e => mkSize = e.newValue);
        var srow = El("row", sl); srow.style.flexWrap = Wrap.NoWrap; b.Add(Field("Brush size", srow));
        var acts = El("row", B("Undo last stroke", "btn small", () => { if (mkUndo == null || mkPx == null) return; mkPx = (Color32[])mkUndo.Clone(); if (mkFxUndo != null) mkFxPx = (Color32[])mkFxUndo.Clone(); MkShow(); MkSave(); }),
            B("Wipe it all off", "btn small", MkWipe));
        acts.style.marginTop = 12; b.Add(acts);
        b.Add(L("Full looks", "h3")); b.Add(L("One tap paints a whole face, made with the same brushes. Paint over it to make it yours.", "muted"));
        var looks = El("row");
        foreach (var lk in MkLooks.Keys) { string n = lk; looks.Add(B(n, "chip", () => { MkWipe(); MkPaintLook(n); })); }
        b.Add(looks);
        b.Add(L("Tips", "h3")); b.Add(L("Build foundation, contour and blush up softly: go over the same spot for more. Lipstick, liner and brows draw solid lines with a small brush. Gloss adds wet shine over a colour; Glitter sparkles as " + she + " moves. The remover takes makeup off where you rub.", "muted"));
    }
    void MkWipe() { if (mkPx == null) return; MkKeepUndo(); for (int i = 0; i < mkPx.Length; i++) mkPx[i] = new Color32(0, 0, 0, 0); if (mkFxPx != null) for (int i = 0; i < mkFxPx.Length; i++) mkFxPx[i] = new Color32(0, 0, 0, 0); MkShow(); MkSave(); }
    Vector2 MkPos(Vector3 local) => new Vector2(local.x / Mathf.Max(1, makeupImg.resolvedStyle.width), 1 - local.y / Mathf.Max(1, makeupImg.resolvedStyle.height));
    void MkShow() { if (mkTex != null) { mkTex.SetPixels32(mkPx); mkTex.Apply(false); } if (mkFxTex != null && mkFxPx != null) { mkFxTex.SetPixels32(mkFxPx); mkFxTex.Apply(false); } }
    void MkSave() {
        if (mkTex == null) return; string key = MkSaveKey();
        File.WriteAllBytes(SLStage3D.MakeupFile(key), mkTex.EncodeToPNG()); if (mkFxTex != null) File.WriteAllBytes(SLStage3D.MakeupFile(key + "-fx"), mkFxTex.EncodeToPNG());
        mkVer.TryGetValue(key, out int v); mkVer[key] = v + 1; dirty = true;
    }
    /// <summary>One dab of the brush at a point of the mirror (0..1 across, 0..1 up).</summary>
    void MkStamp(Vector2 n) {
        if (mkUV == null || mkPx == null) return;
        float r = Mathf.Max(1.2f, mkSize * mkW / 520f); int cx = (int)(n.x * mkW), cy = (int)(n.y * mkH), ri = Mathf.CeilToInt(r), T = mkTex.width;
        Color32 col = C(mkColor[mkTool.id]); var fin = Array.Find(MkFinishes, f => f.id == mkTool.finish);
        var fxCol = new Color32((byte)(fin.gloss * 255), (byte)(fin.metal * 255), (byte)(fin.glitter * 255), 255);
        for (int dy = -ri; dy <= ri; dy++) for (int dx = -ri; dx <= ri; dx++) {
            float d = Mathf.Sqrt(dx * dx + dy * dy); if (d > r) continue; int x = cx + dx, y = cy + dy; if (x < 0 || y < 0 || x >= mkW || y >= mkH) continue;
            if (mkTool.scatter && mkRnd.NextDouble() > 0.018) continue;               // freckles: a sprinkle of dots, not a wash
            var uv = mkUV[y * mkW + x]; if (uv.b < 0.5f) continue;                       // not her skin: hair, eyes, the wall behind
            float f = mkTool.hard ? (d < r * 0.75f ? 1 : 0.45f) : (1 - d / r) * (1 - d / r), a = mkTool.flow * f;
            if (mkTool.scatter) a = mkTool.flow * (0.5f + (float)mkRnd.NextDouble() * 0.5f);
            int tx = (int)(uv.r * T), ty = (int)(uv.g * T), spread = mkTool.scatter ? 1 : 1;
            for (int oy = -spread; oy <= spread; oy++) for (int ox = -spread; ox <= spread; ox++) {
                int px = tx + ox, py = ty + oy; if (px < 0 || py < 0 || px >= T || py >= T) continue; int i = py * T + px; var dst = mkPx[i]; float da = dst.a / 255f;
                if (mkTool.erase) { dst.a = (byte)(da * (1 - a) * 255); mkPx[i] = dst; if (mkFxPx != null) { var fd = mkFxPx[i]; mkFxPx[i] = new Color32((byte)(fd.r * (1 - a)), (byte)(fd.g * (1 - a)), (byte)(fd.b * (1 - a)), fd.a); } continue; }
                float oa = Mathf.Max(da, Mathf.Min(mkTool.max, a + da * (1 - a))), t = Mathf.Clamp01(a / Mathf.Max(oa, 0.001f));
                // gloss is a clear coat: it barely tints, but its shine goes on strongly
                float tc = mkTool.id == "gloss" ? t * 0.35f : t;
                mkPx[i] = new Color32((byte)Mathf.Lerp(dst.r, col.r, tc), (byte)Mathf.Lerp(dst.g, col.g, tc), (byte)Mathf.Lerp(dst.b, col.b, tc), (byte)(mkTool.id == "gloss" ? Mathf.Max(dst.a, oa * 255 * 0.6f) : oa * 255));
                if (mkFxPx != null) { var fd = mkFxPx[i]; float k = Mathf.Clamp01(a * (mkTool.id == "gloss" ? 3 : 1.6f)); mkFxPx[i] = new Color32((byte)Mathf.Lerp(fd.r, fxCol.r, k), (byte)Mathf.Lerp(fd.g, fxCol.g, k), (byte)Mathf.Lerp(fd.b, fxCol.b, k), 255); }
            }
        }
    }
    void TickMakeup() {
        bool on = On3 && view == "parlor" && (parlorTab == "makeup" || parlorTab == "clients") && makeupImg != null && !picker.ClassListContains("on");
        if (makeupImg != null) makeupImg.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        if (!on) return;
        float k = makeupImg.scaledPixelsPerPoint; int w = Mathf.RoundToInt(makeupImg.resolvedStyle.width * k), h = Mathf.RoundToInt(makeupImg.resolvedStyle.height * k); if (w < 16 || h < 16) return;
        var look = MkLook(); string sig = look.id + "|" + look.hair + "|" + look.skinTint + "|" + (mkSubjectKey ?? "") + "|" + w + "x" + h;
        if (sig != mkFor || mkUV == null) {       // a new face, hairstyle or mirror size: map the mirror to her skin again
            mkFor = sig; mkW = w; mkH = h; mkUV = stage3d.FaceUV(w, h, look); mkMarks = MkFindFace(stage3d.FaceLive(w, h, look), w, h); mkTex = look.makeup; mkPx = mkTex.GetPixels32(); mkFxTex = look.makeupFx; mkFxPx = mkFxTex != null ? mkFxTex.GetPixels32() : null; mkUndo = mkFxUndo = null;
        }
        if (mkPending != null) { var a = mkPending; mkPending = null; a(); }
        makeupImg.image = stage3d.FaceLive(w, h, look);
    }

    // ---- full looks, painted with the same brushes: (product, shade, finish, brush size, points across 0..1 and up 0..1 of the mirror)
    void MkStroke(string tool, string color, string finish, float size, params float[] xy) {
        MkInitTools(); var keepT = mkTool; var keepS = mkSize;
        mkTool = Array.Find(mkTools, t => t.id == tool); string keepC = mkColor[tool], keepF = mkTool.finish; mkColor[tool] = color; if (finish != null) mkTool.finish = finish; mkSize = size;
        for (int i = 0; i + 3 < xy.Length; i += 2) for (int s = 0; s <= 24; s++) MkStamp(Vector2.Lerp(new Vector2(xy[i], xy[i + 1]), new Vector2(xy[i + 2], xy[i + 3]), s / 24f));
        mkColor[tool] = keepC; mkTool.finish = keepF; mkTool = keepT; mkSize = keepS;
    }
    /// <summary>Finds her features in the mirror itself, so looks land on any face whatever its texture layout: the whites of her eyes
    /// (bright, colourless pixels in the upper face) give the eyes and the distance between them, and the reddest band low in
    /// the middle of the face gives the mouth. Returns (eye midpoint x, y, pupil distance as a share of the width, mouth y).</summary>
    Vector4 MkFindFace(RenderTexture rt, int w, int h) {
        var fall = new Vector4(0.5f, 0.585f, 0.2f, 0.4f); if (mkUV == null) return fall;     // (no face at all: the old fixed layout)
        var keep = RenderTexture.active; RenderTexture.active = rt; var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply(false);
        RenderTexture.active = keep; var px = t.GetPixels32(); int tw = t.width, th = t.height; Destroy(t);
        Color32 P(int x, int y) => px[Mathf.Clamp(y * th / h, 0, th - 1) * tw + Mathf.Clamp(x * tw / w, 0, tw - 1)];
        int x0 = w, x1 = 0, y0 = h, y1 = 0;
        for (int y = 0; y < h; y += 2) for (int x = 0; x < w; x += 2) if (mkUV[y * w + x].b > 0.5f) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
        if (x1 - x0 < 20 || y1 - y0 < 20) return fall;
        float fw = x1 - x0, fh = y1 - y0, cx = (x0 + x1) / 2f;
        // the eyes: the mask marks her skin, and her eyes are not skin, so they show as two holes in it either side of the
        // nose (hair over the forehead is a hole too, but it runs across the middle, where the nose is skin)
        bool Skin(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && mkUV[y * w + x].b > 0.5f;
        int bestRow = -1, bestN = 0; float sl = 0, nl = 0, sr = 0, nr = 0, sy = 0;
        for (int y = (int)(y0 + fh * 0.3f); y < y0 + fh * 0.85f; y++) {
            if (!Skin((int)cx, y)) continue;
            int rl = (int)cx, rr = (int)cx; while (rl > x0 && Skin(rl - 1, y)) rl--; while (rr < x1 && Skin(rr + 1, y)) rr++;      // the skin run through the nose
            int l = 0, r = 0;
            for (int x = rl - 1; x > cx - fw * 0.42f; x--) { if (Skin(x, y)) { if (l > 0) break; } else l++; }
            for (int x = rr + 1; x < cx + fw * 0.42f; x++) { if (Skin(x, y)) { if (r > 0) break; } else r++; }
            // skin on the far side too (a hole, not the edge of the face), and above and below it (an eye, not a fringe hanging over the brow)
            bool Walled(int x) { int k = (int)(fh * 0.12f); bool up = false, dn = false; for (int d = 1; d <= k; d++) { if (Skin(x, y + d)) up = true; if (Skin(x, y - d)) dn = true; } return up && dn; }
            bool closed = Skin(rl - l - 2, y) && Skin(rr + r + 2, y) && l > 0 && r > 0 && Walled(rl - l / 2 - 1) && Walled(rr + r / 2 + 1);
            int n = closed ? Mathf.Min(l, r) : 0;
            if (n > bestN) { bestN = n; bestRow = y; }
        }
        // without a clear pair of eyes: where they and the mouth sat, within the head's outline, on the faces found so far
        var byShape = new Vector4(cx / w, (y0 + fh * mkLearnt.x) / h, fw * mkLearnt.y / w, (y0 + fh * mkLearnt.z) / h);
        // eyes painted on the head's own skin (the one-piece avatars) leave no hole: then the whites of the eyes are looked for
        if (bestRow < 0 || bestN < fw * 0.05f) return MkWhites(P, Skin, x0, y0, fw, fh, cx, w, h, byShape);
        for (int y = bestRow - (int)(fh * 0.04f); y <= bestRow + (int)(fh * 0.04f); y++) for (int x = (int)(cx - fw * 0.42f); x < cx + fw * 0.42f; x++) {
            if (y < 0 || y >= h || Skin(x, y) || Mathf.Abs(x - cx) < fw * 0.05f) continue;
            // inside the face: skin both further out and further in on this row
            bool inside = false; for (int k = 2; k < fw * 0.2f; k++) if (Skin(x + (x < cx ? -k : k), y)) { inside = true; break; }
            if (!inside) continue;
            if (x < cx) { sl += x; nl++; } else { sr += x; nr++; } sy += y;
        }
        if (nl < 2 || nr < 2) return MkWhites(P, Skin, x0, y0, fw, fh, cx, w, h, byShape);
        float el = sl / nl, er = sr / nr, ey = sy / (nl + nr);
        if (er - el < fw * 0.2f || er - el > fw * 0.7f) return MkWhites(P, Skin, x0, y0, fw, fh, cx, w, h, byShape);
        return MkMouth(P, Skin, el, er, ey, x0, y0, fw, fh, cx, w, h);
    }
    /// <summary>The eyes by their whites: bright pixels with little colour in the upper face, either side of the nose.</summary>
    Vector4 MkWhites(Func<int, int, Color32> P, Func<int, int, bool> Skin, int x0, int y0, float fw, float fh, float cx, int w, int h, Vector4 byShape) {
        var lums = new List<int>(); for (int y = (int)(y0 + fh * 0.35f); y < y0 + fh * 0.5f; y += 3) for (int x = (int)(cx - fw * 0.3f); x < cx + fw * 0.3f; x += 3) { var c = P(x, y); lums.Add(Mathf.Max(c.r, Mathf.Max(c.g, c.b))); }
        lums.Sort(); int skinL = lums.Count > 0 ? lums[lums.Count / 2] : 160, white = Mathf.Clamp(skinL + 12, 110, 235);
        bool IsWhite(Color32 c) { int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b)); return mx > white && mx - mn < mx * 0.22f; }
        int bestRow = -1, bestN = 0;
        for (int y = (int)(y0 + fh * 0.45f); y < y0 + fh * 0.85f; y++) {
            int n = 0; for (int x = (int)(x0 + fw * 0.1f); x < x0 + fw * 0.9f; x++) if (Skin(x, y) && Mathf.Abs(x - cx) > fw * 0.07f && IsWhite(P(x, y))) n++;
            if (n > bestN) { bestN = n; bestRow = y; }
        }
        if (bestRow < 0 || bestN < 4) return byShape;
        float sl = 0, nl = 0, sr = 0, nr = 0, sy = 0;
        for (int y = bestRow - (int)(fh * 0.03f); y <= bestRow + (int)(fh * 0.03f); y++) for (int x = (int)(x0 + fw * 0.1f); x < x0 + fw * 0.9f; x++)
            if (Skin(x, y) && Mathf.Abs(x - cx) > fw * 0.07f && IsWhite(P(x, y))) { if (x < cx) { sl += x; nl++; } else { sr += x; nr++; } sy += y; }
        if (nl < 2 || nr < 2) return byShape;
        float el = sl / nl, er = sr / nr, ey = sy / (nl + nr);
        if (er - el < fw * 0.2f || er - el > fw * 0.7f) return byShape;
        return MkMouth(P, Skin, el, er, ey + (er - el) * 0.14f, x0, y0, fw, fh, cx, w, h);      // (the whites sit low in the eye)
    }
    /// <summary>With the eyes known: the mouth (the reddest row below them), and the result; what was found is remembered for faces that hide theirs.</summary>
    Vector4 MkMouth(Func<int, int, Color32> P, Func<int, int, bool> Skin, float el, float er, float ey, int x0, int y0, float fw, float fh, float cx, int w, int h) {
        // the mouth: the reddest row (red over green, against the skin's own) low in the middle of the face
        float skinR = 0; int sn = 0; for (int y = (int)(y0 + fh * 0.3f); y < y0 + fh * 0.45f; y += 2) for (int x = (int)(cx - fw * 0.25f); x < cx + fw * 0.25f; x += 2) { var c = P(x, y); skinR += (c.r + 1f) / (c.g + 1f); sn++; }
        skinR /= Mathf.Max(1, sn); float ipd = er - el; int mouthRow = -1; float best = 0;
        for (int y = (int)(ey - ipd * 1.6f); y < ey - ipd * 0.7f; y++) {
            if (y < 0) continue; float sum = 0; int n = 0;
            for (int x = (int)(cx - ipd * 0.3f); x < cx + ipd * 0.3f; x++) { if (!Skin(x, y)) continue; var c = P(x, y); sum += (c.r + 1f) / (c.g + 1f) - skinR; n++; }
            if (n > 0 && sum / n > best) { best = sum / n; mouthRow = y; }
        }
        float my = mouthRow >= 0 ? mouthRow : ey - ipd * 1.15f;
        mkLearnt = Vector3.Lerp(mkLearnt, new Vector3((ey - y0) / fh, ipd / fw, (my - y0) / fh), 0.5f);
        return new Vector4((el + er) / 2f / w, ey / h, ipd / w, my / h);
    }
    // ---- the face, measured from her eyes: x across and y up, in eye-to-eye distances (IPD) from the point between her pupils.
    // (Eyes at x ±0.5; brows 0.4 above; cheek apples 0.6 out and 0.55 down; the mouth at -1.15.) Below the eyes, distances are
    // stretched or squeezed so -1.15 lands on her actual mouth: faces differ most there.
    Vector2 MkF(float x, float y) {
        float up = mkMarks.z * mkW / Mathf.Max(1f, mkH), down = mkMarks.y > mkMarks.w ? (mkMarks.y - mkMarks.w) / 1.15f : up;
        return new Vector2(mkMarks.x + x * mkMarks.z, mkMarks.y + y * (y >= 0 ? up : down));
    }
    void MkFace(string tool, string color, string finish, float size, params float[] xy) {
        var m = new float[xy.Length]; for (int i = 0; i + 1 < xy.Length; i += 2) { var q = MkF(xy[i], xy[i + 1]); m[i] = q.x; m[i + 1] = q.y; }
        MkStroke(tool, color, finish, size * mkMarks.z / 0.2f, m);      // brush sizes were set for a face 0.2 of the mirror wide between the pupils
    }
    void MkMirror(string tool, string color, string finish, float size, params float[] xy) {      // the same stroke on both sides of the face
        MkFace(tool, color, finish, size, xy); var m = (float[])xy.Clone(); for (int i = 0; i < m.Length; i += 2) m[i] = -m[i]; MkFace(tool, color, finish, size, m);
    }
    // lids (lift above the lash line, in IPD) and an optional flick of a wing out past the outer corner
    void MkBothEyes(string tool, string color, string finish, float size, float lift, float wing = 0) {
        MkMirror(tool, color, finish, size, 0.27f, lift, 0.5f, lift + 0.03f, 0.74f, lift);
        if (wing > 0) MkMirror(tool, color, finish, size * 0.8f, 0.72f, lift, 0.74f + wing * 0.8f, lift + wing);
    }
    void MkCheeks(string tool, string color, string finish, float size, float y = -0.55f) => MkMirror(tool, color, finish, size, 0.52f, y + 0.04f, 0.72f, y - 0.02f);
    void MkLips(string tool, string color, string finish, float size = 6) {
        MkFace(tool, color, finish, size, -0.34f, -1.15f, 0f, -1.1f, 0.34f, -1.15f);            // the upper lip
        MkFace(tool, color, finish, size, -0.3f, -1.18f, 0f, -1.24f, 0.3f, -1.18f);             // the lower lip
    }
    void MkBase(string shade, string finish) { for (float y = -1.45f; y <= 1.0f; y += 0.3f) MkFace("base", shade, finish, 44, -1.0f, y, 1.0f, y); }
    void MkBrows(string color) => MkMirror("brows", color, "matte", 4, 0.22f, 0.36f, 0.52f, 0.45f, 0.8f, 0.4f);
    void MkContour() { MkMirror("contour", "#9b6b4c", "matte", 18, 1.0f, -0.45f, 0.62f, -0.82f); MkMirror("contour", "#9b6b4c", "matte", 6, 0.14f, -0.1f, 0.12f, -0.62f); }
    void MkHighlight(string color = "#fff4dc", string finish = "metallic") { MkMirror("glow", color, finish, 16, 0.6f, -0.28f, 0.85f, -0.22f); MkFace("glow", color, finish, 7, 0f, -0.1f, 0f, -0.6f); MkFace("glow", color, finish, 5, -0.05f, -0.98f, 0.05f, -0.98f); }
    Dictionary<string, Action> mkLooks;
    Dictionary<string, Action> MkLooks => mkLooks ?? (mkLooks = new Dictionary<string, Action> {
        { "Natural", () => { MkBase("#e9b996", "satin"); MkBrows("#4a3020"); MkCheeks("blush", "#f19a9a", "satin", 26); MkLips("lips", "#c97a7a", "satin"); MkLips("gloss", "#ffffff", "gloss"); } },
        { "Soft glam", () => { MkBase("#e9b996", "satin"); MkContour(); MkHighlight(); MkBrows("#4a3020"); MkBothEyes("shadow", "#b07a6a", "satin", 14, 0.24f); MkBothEyes("liner", "#2a1a12", "gloss", 3, 0.14f, 0.11f); MkCheeks("blush", "#e8848a", "satin", 26); MkLips("lips", "#c46a72", "satin"); } },
        { "Smoky eye", () => { MkBase("#e9b996", "matte"); MkBrows("#2a1a12"); MkBothEyes("shadow", "#3a3340", "metallic", 16, 0.24f); MkBothEyes("shadow", "#1b1820", "matte", 10, 0.24f); MkBothEyes("liner", "#0e0c10", "gloss", 3.5f, 0.14f, 0.09f); MkLips("lips", "#b98a7a", "matte"); } },
        { "Red carpet", () => { MkBase("#e9b996", "satin"); MkContour(); MkHighlight("#ffe9b8"); MkBrows("#2a1a12"); MkBothEyes("shadow", "#c9a46a", "metallic", 14, 0.24f); MkBothEyes("liner", "#0e0c10", "gloss", 3.5f, 0.14f, 0.18f); MkLips("lips", "#a3122e", "satin"); MkCheeks("blush", "#d8747a", "satin", 22); } },
        { "Neon", () => { MkBase("#e9b996", "matte"); MkBothEyes("liner", "#2ef2ff", "metallic", 4, 0.14f, 0.27f); MkBothEyes("shadow", "#ff2ea6", "metallic", 12, 0.24f); MkCheeks("glitter", "#8fd3ff", "glitter", 10, -0.35f); MkLips("lips", "#7a1aff", "metallic"); } },
        { "Regency", () => { MkBase("#f6d7c3", "matte"); MkBrows("#6b4a33"); MkCheeks("blush", "#f2a7b0", "satin", 30, -0.5f); MkLips("lips", "#d98a96", "satin", 5); MkFace("freckles", "#8a5a3c", "matte", 18, -0.75f, -0.45f, 0.75f, -0.45f); } },
        { "Old Hollywood", () => { MkBase("#f2c9a8", "matte"); MkBrows("#2a1a12"); MkBothEyes("shadow", "#e6d2c0", "satin", 14, 0.24f); MkBothEyes("liner", "#0e0c10", "gloss", 4, 0.14f, 0.23f); MkLips("lips", "#b3263a", "matte"); MkHighlight("#ffffff", "satin"); } },
        { "Glitter", () => { MkBase("#e9b996", "satin"); MkBothEyes("glitter", "#f5d76e", "glitter", 14, 0.24f); MkBothEyes("liner", "#2a1a12", "gloss", 3, 0.14f, 0.11f); MkHighlight("#f5d76e", "glitter"); MkLips("lips", "#e05a8a", "gloss"); MkLips("gloss", "#ffb3c7", "gloss"); } } });
    void MkPaintLook(string name) { if (MkLooks.TryGetValue(name, out var a)) { if (mkUV == null) { mkPending = () => { a(); MkShow(); MkSave(); }; return; } a(); MkShow(); MkSave(); } }
    /// <summary>For the screenshot tour: a full face painted by code, with the same brush as the pointer uses.</summary>
    void MkDemo() {
        if (mkUV == null) return; MkKeepUndo(); MkPaintLook("Soft glam");
    }

    // ================================================================ salon session (Hair Salon)
    // Picking a style starts a session: shampoo, rinse, blow-dry, comb out, then style, each done by dragging the
    // tool over her hair until its bar fills. The new style goes on when the last step is done.
    static readonly (string name, string hint, string fx)[] SimSteps = {
        ("Shampoo", "Rub the shampoo all over {her} hair.", "bubble"), ("Rinse", "Rinse the lather out with the shower head.", "water"),
        ("Blow-dry", "Dry {her} hair with the dryer.", "air"), ("Comb out", "Comb through from the roots to the tips.", "comb"), ("Style", "Work the style in, section by section.", "spark") };
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
        var hint = L(Pn(SimSteps[simStep].hint) + " Drag over " + him + " in the picture.", "muted"); hint.style.marginTop = 10; hint.style.whiteSpace = WhiteSpace.Normal; b.Add(hint);
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
