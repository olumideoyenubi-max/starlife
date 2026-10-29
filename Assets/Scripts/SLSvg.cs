// Software renderer for the SVG that the Starlife core draws: paths, strokes, dashes, gradients,
// patterns, clip paths, group opacity and the filter primitives the artwork uses (blur, lighting,
// turbulence, colour matrix, composite, merge, blend). Pure C#, no UnityEngine, so it can run on
// a worker thread and be tested outside the editor.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public struct SLMat {
    public float a, b, c, d, e, f; // x' = a x + c y + e ; y' = b x + d y + f
    public static readonly SLMat I = new SLMat { a = 1, d = 1 };
    public static SLMat operator *(SLMat p, SLMat q) => new SLMat {
        a = p.a * q.a + p.c * q.b, b = p.b * q.a + p.d * q.b, c = p.a * q.c + p.c * q.d, d = p.b * q.c + p.d * q.d,
        e = p.a * q.e + p.c * q.f + p.e, f = p.b * q.e + p.d * q.f + p.f };
    public void Map(float x, float y, out float ox, out float oy) { ox = a * x + c * y + e; oy = b * x + d * y + f; }
    public float Scale => (float)Math.Sqrt(Math.Abs(a * d - b * c));
    public SLMat Inv() { float k = 1f / (a * d - b * c); return new SLMat { a = d * k, b = -b * k, c = -c * k, d = a * k, e = (c * f - d * e) * k, f = (b * e - a * f) * k }; }
    public static SLMat T(float x, float y) => new SLMat { a = 1, d = 1, e = x, f = y };
    public static SLMat S(float x, float y) => new SLMat { a = x, d = y };
}

public sealed class SLSurface {
    public int X, Y, W, H; public float[] P; // premultiplied RGBA
    public SLSurface(int x, int y, int w, int h) { X = x; Y = y; W = Math.Max(w, 0); H = Math.Max(h, 0); P = new float[W * H * 4]; }
}

public static class SLSvg {
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    /// <summary>Glyph bitmaps for &lt;text&gt;, supplied by the host: key is the text, value is alpha w,h,data (row 0 at the top).</summary>
    public sealed class Glyphs { public int W, H; public float[] A; public float Em; }

    /// <summary>Renders to straight-alpha RGBA32. flip = true puts the bottom row first (Unity texture order).</summary>
    public static byte[] Render(string svg, int w, int h, bool flip = false, Dictionary<string, Glyphs> text = null) {
        var s = RenderSurface(svg, w, h, text);
        var o = new byte[w * h * 4];
        for (int y = 0; y < h; y++) {
            int si = y * w * 4, di = (flip ? h - 1 - y : y) * w * 4;
            for (int x = 0; x < w; x++, si += 4, di += 4) {
                float a = s.P[si + 3];
                if (a <= 0.0005f) continue;
                float k = 255f / a;
                o[di] = B(s.P[si] * k); o[di + 1] = B(s.P[si + 1] * k); o[di + 2] = B(s.P[si + 2] * k); o[di + 3] = B(a * 255f);
            }
        }
        return o;
    }
    static byte B(float v) => (byte)(v <= 0 ? 0 : v >= 255 ? 255 : v + 0.5f);

    public static SLSurface RenderSurface(string svg, int w, int h, Dictionary<string, Glyphs> text = null) {
        var doc = new XmlDocument(); doc.LoadXml(svg);
        var r = new R { text = text };
        r.Index(doc.DocumentElement);
        var dst = new SLSurface(0, 0, w, h);
        var root = doc.DocumentElement;
        var m = Viewport(root, 0, 0, w, h, "xMidYMid meet"); r.vp = m.Scale;
        var st = new Style();
        foreach (XmlNode n in root.ChildNodes) if (n is XmlElement e) r.Draw(e, m, st, dst, null);
        return dst;
    }

    static SLMat Viewport(XmlElement e, float x, float y, float w, float h, string defAspect) {
        var vb = Nums(e.GetAttribute("viewBox"));
        if (vb.Count < 4) return SLMat.T(x, y);
        string par = e.HasAttribute("preserveAspectRatio") ? e.GetAttribute("preserveAspectRatio") : defAspect;
        float sx = w / vb[2], sy = h / vb[3];
        if (!par.StartsWith("none")) { float k = par.Contains("slice") ? Math.Max(sx, sy) : Math.Min(sx, sy); sx = sy = k; }
        float tx = x - vb[0] * sx, ty = y - vb[1] * sy, ex = w - vb[2] * sx, ey = h - vb[3] * sy;
        if (par.Contains("xMid")) tx += ex / 2; else if (par.Contains("xMax")) tx += ex;
        if (par.Contains("YMid")) ty += ey / 2; else if (par.Contains("YMax")) ty += ey;
        return new SLMat { a = sx, d = sy, e = tx, f = ty };
    }

    // ------------------------------------------------------------------ parsing helpers
    static float F(string s, float d = 0) {
        if (string.IsNullOrEmpty(s)) return d;
        s = s.Trim(); bool pct = s.EndsWith("%"); if (pct) s = s.Substring(0, s.Length - 1);
        return float.TryParse(s, NumberStyles.Float, Inv, out var v) ? (pct ? v / 100f : v) : d;
    }
    static List<float> Nums(string s) {
        var l = new List<float>(); if (s == null) return l;
        int i = 0, n = s.Length;
        while (i < n) {
            char ch = s[i];
            if (ch == '-' || ch == '+' || ch == '.' || (ch >= '0' && ch <= '9')) {
                int j = i + 1; bool dot = ch == '.', exp = false;
                while (j < n) {
                    char c2 = s[j];
                    if (c2 >= '0' && c2 <= '9') j++;
                    else if (c2 == '.' && !dot && !exp) { dot = true; j++; }
                    else if ((c2 == 'e' || c2 == 'E') && !exp && j + 1 < n && (char.IsDigit(s[j + 1]) || s[j + 1] == '-')) { exp = true; j += 2; }
                    else break;
                }
                l.Add(float.Parse(s.Substring(i, j - i), NumberStyles.Float, Inv)); i = j;
            } else i++;
        }
        return l;
    }
    static bool Color(string s, out float r, out float g, out float b) {
        r = g = b = 0;
        if (string.IsNullOrEmpty(s) || s[0] != '#') return s == "black" || s == "#000";
        s = s.Trim();
        if (s.Length == 4) { r = Hx(s[1]) * 17; g = Hx(s[2]) * 17; b = Hx(s[3]) * 17; }
        else if (s.Length >= 7) { r = Hx(s[1]) * 16 + Hx(s[2]); g = Hx(s[3]) * 16 + Hx(s[4]); b = Hx(s[5]) * 16 + Hx(s[6]); }
        r /= 255f; g /= 255f; b /= 255f; return true;
    }
    static int Hx(char c) => c <= '9' ? c - '0' : (c | 32) - 'a' + 10;
    static string Url(string s) { if (string.IsNullOrEmpty(s)) return null; int i = s.IndexOf("url(#"); if (i < 0) return null; int j = s.IndexOf(')', i); return s.Substring(i + 5, j - i - 5); }
    static SLMat Transform(string s) {
        var m = SLMat.I; int i = 0;
        while (i < s.Length) {
            int p = s.IndexOf('(', i); if (p < 0) break; int q = s.IndexOf(')', p);
            string fn = s.Substring(i, p - i).Trim(' ', ','); var v = Nums(s.Substring(p + 1, q - p - 1)); i = q + 1;
            SLMat t = SLMat.I;
            if (fn == "translate") t = SLMat.T(v[0], v.Count > 1 ? v[1] : 0);
            else if (fn == "scale") t = SLMat.S(v[0], v.Count > 1 ? v[1] : v[0]);
            else if (fn == "rotate") {
                double a = v[0] * Math.PI / 180; float cs = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                t = new SLMat { a = cs, b = sn, c = -sn, d = cs };
                if (v.Count > 2) t = SLMat.T(v[1], v[2]) * t * SLMat.T(-v[1], -v[2]);
            } else if (fn == "matrix") t = new SLMat { a = v[0], b = v[1], c = v[2], d = v[3], e = v[4], f = v[5] };
            m = m * t;
        }
        return m;
    }

    sealed class Style {
        public string fill = "#000", stroke = "none", cap = "butt", join = "miter", dash;
        public float sw = 1, fo = 1, so = 1, fs = 16; public string anchor = "start";
        public Style With(XmlElement e) {
            if (!e.HasAttributes) return this;
            var s = (Style)MemberwiseClone();
            foreach (XmlAttribute a in e.Attributes) {
                switch (a.Name) {
                    case "fill": s.fill = a.Value; break;
                    case "stroke": s.stroke = a.Value; break;
                    case "stroke-width": s.sw = F(a.Value, 1); break;
                    case "stroke-linecap": s.cap = a.Value; break;
                    case "stroke-linejoin": s.join = a.Value; break;
                    case "stroke-dasharray": s.dash = a.Value; break;
                    case "fill-opacity": s.fo = F(a.Value, 1); break;
                    case "stroke-opacity": s.so = F(a.Value, 1); break;
                    case "font-size": s.fs = F(a.Value, 16); break;
                    case "text-anchor": s.anchor = a.Value; break;
                }
            }
            return s;
        }
    }
    sealed class Mask { public int X, Y, W, H; public float[] A; }
    sealed class Sub { public List<float> p = new List<float>(64); public bool closed; }

    abstract class Paint { public abstract void At(float x, float y, out float r, out float g, out float b, out float a); }
    sealed class Solid : Paint {
        public float R, G, Bl, A;
        public override void At(float x, float y, out float r, out float g, out float b, out float a) { r = R; g = G; b = Bl; a = A; }
    }
    sealed class Grad : Paint {
        public SLMat inv; public float bx, by, bw, bh, x1, y1, x2 = 1, y2, cx = .5f, cy = .5f, rad = .5f, op = 1; public bool radial; public float[] lut = new float[1024];
        public override void At(float x, float y, out float r, out float g, out float b, out float a) {
            inv.Map(x, y, out var ux, out var uy);
            float nx = (ux - bx) / bw, ny = (uy - by) / bh, t;
            if (radial) { float dx = nx - cx, dy = ny - cy; t = (float)Math.Sqrt(dx * dx + dy * dy) / rad; }
            else { float dx = x2 - x1, dy = y2 - y1; t = ((nx - x1) * dx + (ny - y1) * dy) / (dx * dx + dy * dy); }
            int i = t <= 0 ? 0 : t >= 1 ? 255 : (int)(t * 255f + .5f); i *= 4;
            r = lut[i] * op; g = lut[i + 1] * op; b = lut[i + 2] * op; a = lut[i + 3] * op;
        }
    }
    sealed class Pat : Paint {
        public SLMat inv; public float pw, ph, op = 1; public SLSurface tile;
        public override void At(float x, float y, out float r, out float g, out float b, out float a) {
            inv.Map(x, y, out var ux, out var uy);
            float fx = ux / pw, fy = uy / ph; fx -= (float)Math.Floor(fx); fy -= (float)Math.Floor(fy);
            int tx = Math.Min(tile.W - 1, (int)(fx * tile.W)), ty = Math.Min(tile.H - 1, (int)(fy * tile.H)), i = (ty * tile.W + tx) * 4;
            r = tile.P[i] * op; g = tile.P[i + 1] * op; b = tile.P[i + 2] * op; a = tile.P[i + 3] * op;
        }
    }

    // ------------------------------------------------------------------ renderer
    sealed class R {
        public Dictionary<string, XmlElement> ids = new Dictionary<string, XmlElement>();
        public Dictionary<string, Glyphs> text;
        float[] acc = new float[0];

        public void Index(XmlElement e) {
            if (e.HasAttribute("id")) ids[e.GetAttribute("id")] = e;
            foreach (XmlNode n in e.ChildNodes) if (n is XmlElement c) Index(c);
        }
        static bool IsShape(string n) => n == "path" || n == "rect" || n == "circle" || n == "ellipse" || n == "polyline" || n == "polygon" || n == "line";
        static bool Skip(string n) => n == "defs" || n == "clipPath" || n == "linearGradient" || n == "radialGradient" || n == "filter" || n == "pattern" || n == "title" || n == "mask";

        SLMat Local(XmlElement e, SLMat m) {
            if (e.HasAttribute("transform")) m = m * Transform(e.GetAttribute("transform"));
            if (e.LocalName == "svg" && e.HasAttribute("viewBox") && e.HasAttribute("width"))
                m = m * Viewport(e, F(e.GetAttribute("x")), F(e.GetAttribute("y")), F(e.GetAttribute("width")), F(e.GetAttribute("height")), "xMidYMid meet");
            return m;
        }

        public float vp = 1; // device pixels per user unit of the nearest <svg> viewport (lighting filters are resolution dependent)
        public void Draw(XmlElement e, SLMat pm, Style pst, SLSurface dst, Mask clip) {
            if (e.LocalName != "svg") { DrawEl(e, pm, pst, dst, clip); return; }
            float old = vp; vp = Local(e, pm).Scale;
            try { DrawEl(e, pm, pst, dst, clip); } finally { vp = old; }
        }
        void DrawEl(XmlElement e, SLMat pm, Style pst, SLSurface dst, Mask clip) {
            string n = e.LocalName;
            if (Skip(n)) return;
            var st = pst.With(e); var m = Local(e, pm);
            float op = e.HasAttribute("opacity") ? F(e.GetAttribute("opacity"), 1) : 1;
            if (op <= 0) return;
            string filt = Url(e.GetAttribute("filter")), cp = Url(e.GetAttribute("clip-path"));
            bool shape = IsShape(n), text = n == "text", cont = n == "g" || n == "svg" || n == "a";
            if (!shape && !cont && !text) return;
            XmlElement fe = null; if (filt != null) ids.TryGetValue(filt, out fe);
            bool layer = fe != null || (op < 1 && (cont || (st.fill != "none" && st.stroke != "none")));
            Mask cm = clip;
            if (cp != null && ids.TryGetValue(cp, out var ce)) cm = BuildClip(ce, m, dst, clip);
            if (!layer) {
                if (cont) { foreach (XmlNode c in e.ChildNodes) if (c is XmlElement ch) Draw(ch, m, st, dst, cm); }
                else if (text) DrawText(e, m, st, dst, cm, op);
                else DrawShape(e, m, st, dst, cm, op);
                return;
            }
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            Extent(e, pm, pst, ref x0, ref y0, ref x1, ref y1);
            if (x1 < x0) return;
            float pad = fe != null ? FilterPad(fe, m.Scale) : 1;
            int lx0 = (int)Math.Floor(Math.Max(x0, dst.X - pad)) - 1, ly0 = (int)Math.Floor(Math.Max(y0, dst.Y - pad)) - 1;
            int lx1 = (int)Math.Ceiling(Math.Min(x1, dst.X + dst.W + pad)) + 1, ly1 = (int)Math.Ceiling(Math.Min(y1, dst.Y + dst.H + pad)) + 1;
            if (lx1 <= lx0 || ly1 <= ly0) return;
            var L = new SLSurface(lx0, ly0, lx1 - lx0, ly1 - ly0);
            if (cont) { foreach (XmlNode c in e.ChildNodes) if (c is XmlElement ch) Draw(ch, m, st, L, null); }
            else if (text) DrawText(e, m, st, L, null, 1);
            else DrawShape(e, m, st, L, null, 1);
            if (fe != null) SLFilter.Apply(fe, L, m, vp);
            Composite(dst, L, op, cm);
        }

        static void Composite(SLSurface d, SLSurface s, float op, Mask mk) {
            int x0 = Math.Max(d.X, s.X), y0 = Math.Max(d.Y, s.Y), x1 = Math.Min(d.X + d.W, s.X + s.W), y1 = Math.Min(d.Y + d.H, s.Y + s.H);
            for (int y = y0; y < y1; y++) {
                int si = ((y - s.Y) * s.W + x0 - s.X) * 4, di = ((y - d.Y) * d.W + x0 - d.X) * 4;
                int mi = mk != null ? (y - mk.Y) * mk.W + x0 - mk.X : 0; bool mrow = mk != null && y >= mk.Y && y < mk.Y + mk.H;
                for (int x = x0; x < x1; x++, si += 4, di += 4, mi++) {
                    float sa = s.P[si + 3]; if (sa <= 0) continue;
                    float k = op;
                    if (mk != null) { if (!mrow || x < mk.X || x >= mk.X + mk.W) continue; k *= mk.A[mi]; if (k <= 0) continue; }
                    float ia = 1 - sa * k;
                    d.P[di] = s.P[si] * k + d.P[di] * ia; d.P[di + 1] = s.P[si + 1] * k + d.P[di + 1] * ia;
                    d.P[di + 2] = s.P[si + 2] * k + d.P[di + 2] * ia; d.P[di + 3] = sa * k + d.P[di + 3] * ia;
                }
            }
        }

        // device-space extent of an element and its children, including stroke width and filter regions
        void Extent(XmlElement e, SLMat pm, Style pst, ref float x0, ref float y0, ref float x1, ref float y1) {
            string n = e.LocalName; if (Skip(n)) return;
            var st = pst.With(e); var m = Local(e, pm);
            float a0 = 1e9f, b0 = 1e9f, a1 = -1e9f, b1 = -1e9f;
            if (IsShape(n)) {
                var subs = Geometry(e, m);
                float g = st.stroke != "none" ? st.sw * m.Scale * (st.join == "miter" ? 2f : .5f) + 1 : 0;
                foreach (var s in subs) for (int i = 0; i < s.p.Count; i += 2) { a0 = Math.Min(a0, s.p[i] - g); a1 = Math.Max(a1, s.p[i] + g); b0 = Math.Min(b0, s.p[i + 1] - g); b1 = Math.Max(b1, s.p[i + 1] + g); }
            } else if (n == "text") {
                m.Map(F(e.GetAttribute("x")), F(e.GetAttribute("y")), out var tx, out var ty); float g = st.fs * m.Scale * 6;
                a0 = tx - g; a1 = tx + g; b0 = ty - g; b1 = ty + g;
            } else foreach (XmlNode c in e.ChildNodes) if (c is XmlElement ch) Extent(ch, m, st, ref a0, ref b0, ref a1, ref b1);
            if (a1 < a0) return;
            string filt = Url(e.GetAttribute("filter"));
            if (filt != null && ids.TryGetValue(filt, out var fe)) {
                float fx = fe.HasAttribute("x") ? F(fe.GetAttribute("x")) : -.1f, fy = fe.HasAttribute("y") ? F(fe.GetAttribute("y")) : -.1f;
                float fw = fe.HasAttribute("width") ? F(fe.GetAttribute("width")) : 1.2f, fh = fe.HasAttribute("height") ? F(fe.GetAttribute("height")) : 1.2f;
                float w = a1 - a0, h = b1 - b0; a0 += fx * w; b0 += fy * h; a1 = a0 + fw * w; b1 = b0 + fh * h;
            }
            x0 = Math.Min(x0, a0); y0 = Math.Min(y0, b0); x1 = Math.Max(x1, a1); y1 = Math.Max(y1, b1);
        }
        static float FilterPad(XmlElement fe, float scale) {
            float s = 0;
            foreach (XmlNode c in fe.ChildNodes) if (c is XmlElement p && p.LocalName == "feGaussianBlur") s = Math.Max(s, F(p.GetAttribute("stdDeviation")));
            return s * scale * 3 + 2;
        }

        // ---------------------------------------------------------------- geometry
        List<Sub> Geometry(XmlElement e, SLMat m) {
            var o = new List<Sub>(); float sc = m.Scale;
            switch (e.LocalName) {
                case "path": PathData(e.GetAttribute("d"), m, o); break;
                case "rect": {
                    float x = F(e.GetAttribute("x")), y = F(e.GetAttribute("y")), w = F(e.GetAttribute("width")), h = F(e.GetAttribute("height"));
                    float rx = e.HasAttribute("rx") ? F(e.GetAttribute("rx")) : -1, ry = e.HasAttribute("ry") ? F(e.GetAttribute("ry")) : -1;
                    if (rx < 0) rx = ry; if (ry < 0) ry = rx; if (rx < 0) rx = ry = 0;
                    rx = Math.Min(rx, w / 2); ry = Math.Min(ry, h / 2);
                    if (w <= 0 || h <= 0) break;
                    var s = new Sub { closed = true }; o.Add(s);
                    if (rx <= 0) { Pt(s, m, x, y); Pt(s, m, x + w, y); Pt(s, m, x + w, y + h); Pt(s, m, x, y + h); }
                    else {
                        int k = Math.Max(3, (int)(Math.Max(rx, ry) * sc * .7f) + 2);
                        Arc(s, m, x + w - rx, y + ry, rx, ry, -90, 0, k); Arc(s, m, x + w - rx, y + h - ry, rx, ry, 0, 90, k);
                        Arc(s, m, x + rx, y + h - ry, rx, ry, 90, 180, k); Arc(s, m, x + rx, y + ry, rx, ry, 180, 270, k);
                    }
                    break;
                }
                case "circle": case "ellipse": {
                    float cx = F(e.GetAttribute("cx")), cy = F(e.GetAttribute("cy")), rx, ry;
                    if (e.LocalName == "circle") rx = ry = F(e.GetAttribute("r")); else { rx = F(e.GetAttribute("rx")); ry = F(e.GetAttribute("ry")); }
                    if (rx <= 0 || ry <= 0) break;
                    var s = new Sub { closed = true }; o.Add(s);
                    Arc(s, m, cx, cy, rx, ry, 0, 360, Math.Max(10, Math.Min(120, (int)(Math.Max(rx, ry) * sc * 1.6f) + 8)), false);
                    break;
                }
                case "polyline": case "polygon": {
                    var v = Nums(e.GetAttribute("points")); var s = new Sub { closed = e.LocalName == "polygon" }; o.Add(s);
                    for (int i = 0; i + 1 < v.Count; i += 2) Pt(s, m, v[i], v[i + 1]);
                    break;
                }
                case "line": { var s = new Sub(); o.Add(s); Pt(s, m, F(e.GetAttribute("x1")), F(e.GetAttribute("y1"))); Pt(s, m, F(e.GetAttribute("x2")), F(e.GetAttribute("y2"))); break; }
            }
            return o;
        }
        static void Pt(Sub s, SLMat m, float x, float y) { m.Map(x, y, out var ox, out var oy); s.p.Add(ox); s.p.Add(oy); }
        static void Arc(Sub s, SLMat m, float cx, float cy, float rx, float ry, float a0, float a1, int k, bool last = true) {
            int n = last ? k : k - 1;
            for (int i = 0; i <= n; i++) { double a = (a0 + (a1 - a0) * i / k) * Math.PI / 180; Pt(s, m, cx + rx * (float)Math.Cos(a), cy + ry * (float)Math.Sin(a)); }
        }
        static void Cubic(Sub s, SLMat m, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3) {
            float len = (Math.Abs(x1 - x0) + Math.Abs(y1 - y0) + Math.Abs(x2 - x1) + Math.Abs(y2 - y1) + Math.Abs(x3 - x2) + Math.Abs(y3 - y2)) * m.Scale;
            int n = Math.Max(2, Math.Min(80, (int)(len / 3.5f) + 2));
            for (int i = 1; i <= n; i++) {
                float t = (float)i / n, u = 1 - t, k0 = u * u * u, k1 = 3 * u * u * t, k2 = 3 * u * t * t, k3 = t * t * t;
                Pt(s, m, k0 * x0 + k1 * x1 + k2 * x2 + k3 * x3, k0 * y0 + k1 * y1 + k2 * y2 + k3 * y3);
            }
        }
        static void PathData(string d, SLMat m, List<Sub> o) {
            int i = 0, n = d.Length; char cmd = ' '; Sub s = null;
            float cx = 0, cy = 0, sx = 0, sy = 0, px = 0, py = 0; char prev = ' ';
            float Num() {
                while (i < n && (d[i] == ' ' || d[i] == ',' || d[i] == '\n' || d[i] == '\t' || d[i] == '\r')) i++;
                int j = i; if (j < n && (d[j] == '-' || d[j] == '+')) j++;
                bool dot = false;
                while (j < n) { char c = d[j]; if (c >= '0' && c <= '9') j++; else if (c == '.' && !dot) { dot = true; j++; } else if ((c == 'e' || c == 'E') && j + 1 < n && (d[j + 1] == '-' || char.IsDigit(d[j + 1]))) { j += 2; } else break; }
                float v = j > i ? float.Parse(d.Substring(i, j - i), NumberStyles.Float, Inv) : 0; i = Math.Max(j, i + (j > i ? 0 : 1)); return v;
            }
            bool More() { while (i < n && (d[i] == ' ' || d[i] == ',' || d[i] == '\n' || d[i] == '\t' || d[i] == '\r')) i++; return i < n && (d[i] == '-' || d[i] == '+' || d[i] == '.' || (d[i] >= '0' && d[i] <= '9')); }
            while (i < n) {
                char c = d[i];
                if (char.IsLetter(c)) { cmd = c; i++; if (cmd == 'Z' || cmd == 'z') { if (s != null) s.closed = true; cx = sx; cy = sy; s = null; prev = 'z'; continue; } }
                else if (c == ' ' || c == ',' || c == '\n' || c == '\t' || c == '\r') { i++; continue; }
                if (!More()) { if (i < n && !char.IsLetter(d[i])) i++; continue; }
                bool rel = char.IsLower(cmd); char u = char.ToUpperInvariant(cmd);
                float ox = rel ? cx : 0, oy = rel ? cy : 0;
                if (s == null && u != 'M') { s = new Sub(); o.Add(s); Pt(s, m, cx, cy); }
                switch (u) {
                    case 'M': cx = ox + Num(); cy = oy + Num(); sx = cx; sy = cy; s = new Sub(); o.Add(s); Pt(s, m, cx, cy); cmd = rel ? 'l' : 'L'; break;
                    case 'L': cx = ox + Num(); cy = oy + Num(); Pt(s, m, cx, cy); break;
                    case 'H': cx = ox + Num(); Pt(s, m, cx, cy); break;
                    case 'V': cy = oy + Num(); Pt(s, m, cx, cy); break;
                    case 'C': { float x1 = ox + Num(), y1 = oy + Num(), x2 = ox + Num(), y2 = oy + Num(), x = ox + Num(), y = oy + Num(); Cubic(s, m, cx, cy, x1, y1, x2, y2, x, y); px = x2; py = y2; cx = x; cy = y; break; }
                    case 'S': { float x1 = prev == 'C' ? 2 * cx - px : cx, y1 = prev == 'C' ? 2 * cy - py : cy, x2 = ox + Num(), y2 = oy + Num(), x = ox + Num(), y = oy + Num(); Cubic(s, m, cx, cy, x1, y1, x2, y2, x, y); px = x2; py = y2; cx = x; cy = y; u = 'C'; break; }
                    case 'Q': { float x1 = ox + Num(), y1 = oy + Num(), x = ox + Num(), y = oy + Num(); Cubic(s, m, cx, cy, cx + (x1 - cx) * 2 / 3, cy + (y1 - cy) * 2 / 3, x + (x1 - x) * 2 / 3, y + (y1 - y) * 2 / 3, x, y); px = x1; py = y1; cx = x; cy = y; break; }
                    case 'T': { float x1 = prev == 'Q' ? 2 * cx - px : cx, y1 = prev == 'Q' ? 2 * cy - py : cy, x = ox + Num(), y = oy + Num(); Cubic(s, m, cx, cy, cx + (x1 - cx) * 2 / 3, cy + (y1 - cy) * 2 / 3, x + (x1 - x) * 2 / 3, y + (y1 - y) * 2 / 3, x, y); px = x1; py = y1; cx = x; cy = y; u = 'Q'; break; }
                    case 'A': {
                        float rx = Num(), ry = Num(), rot = Num(), large = Num(), sweep = Num(), x = ox + Num(), y = oy + Num();
                        SvgArc(s, m, cx, cy, rx, ry, rot, large != 0, sweep != 0, x, y); cx = x; cy = y; break;
                    }
                    default: Num(); break;
                }
                prev = u;
            }
        }
        static void SvgArc(Sub s, SLMat m, float x0, float y0, float rx, float ry, float rot, bool large, bool sweep, float x, float y) {
            if (rx == 0 || ry == 0) { Pt(s, m, x, y); return; }
            rx = Math.Abs(rx); ry = Math.Abs(ry);
            double ph = rot * Math.PI / 180, cs = Math.Cos(ph), sn = Math.Sin(ph);
            double dx = (x0 - x) / 2, dy = (y0 - y) / 2, x1 = cs * dx + sn * dy, y1 = -sn * dx + cs * dy;
            double l = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry); if (l > 1) { double q = Math.Sqrt(l); rx *= (float)q; ry *= (float)q; }
            double num = (double)rx * rx * ry * ry - (double)rx * rx * y1 * y1 - (double)ry * ry * x1 * x1, den = (double)rx * rx * y1 * y1 + (double)ry * ry * x1 * x1;
            double co = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, num / den));
            double cxp = co * rx * y1 / ry, cyp = -co * ry * x1 / rx;
            double cx = cs * cxp - sn * cyp + (x0 + x) / 2, cy = sn * cxp + cs * cyp + (y0 + y) / 2;
            double a0 = Math.Atan2((y1 - cyp) / ry, (x1 - cxp) / rx), a1 = Math.Atan2((-y1 - cyp) / ry, (-x1 - cxp) / rx), da = a1 - a0;
            if (sweep && da < 0) da += 2 * Math.PI; else if (!sweep && da > 0) da -= 2 * Math.PI;
            int n = Math.Max(4, Math.Min(90, (int)(Math.Abs(da) * Math.Max(rx, ry) * m.Scale / 3) + 3));
            for (int i = 1; i <= n; i++) {
                double a = a0 + da * i / n, ex = rx * Math.Cos(a), ey = ry * Math.Sin(a);
                Pt(s, m, (float)(cs * ex - sn * ey + cx), (float)(sn * ex + cs * ey + cy));
            }
        }

        // ---------------------------------------------------------------- paint
        Paint MakePaint(string spec, float opacity, SLMat m, List<Sub> subs) {
            if (spec == null || spec == "none" || opacity <= 0) return null;
            string id = Url(spec);
            if (id == null) { Color(spec, out var r, out var g, out var b); return new Solid { R = r * opacity, G = g * opacity, Bl = b * opacity, A = opacity }; }
            if (!ids.TryGetValue(id, out var e)) return null;
            var inv = m.Inv();
            if (e.LocalName == "pattern") {
                float pw = F(e.GetAttribute("width")), ph = F(e.GetAttribute("height")), sc = m.Scale * 2;
                int tw = Math.Max(2, (int)Math.Ceiling(pw * sc)), th = Math.Max(2, (int)Math.Ceiling(ph * sc));
                var tile = new SLSurface(0, 0, tw, th); var tm = SLMat.S(tw / pw, th / ph); var st = new Style();
                foreach (XmlNode c in e.ChildNodes) if (c is XmlElement ch) Draw(ch, tm, st, tile, null);
                return new Pat { inv = inv, pw = pw, ph = ph, tile = tile, op = opacity };
            }
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            foreach (var s in subs) for (int i = 0; i < s.p.Count; i += 2) { inv.Map(s.p[i], s.p[i + 1], out var ux, out var uy); x0 = Math.Min(x0, ux); x1 = Math.Max(x1, ux); y0 = Math.Min(y0, uy); y1 = Math.Max(y1, uy); }
            var gp = new Grad { inv = inv, bx = x0, by = y0, bw = Math.Max(x1 - x0, 1e-4f), bh = Math.Max(y1 - y0, 1e-4f), op = opacity, radial = e.LocalName == "radialGradient" };
            if (gp.radial) { gp.cx = F(e.GetAttribute("cx"), .5f); gp.cy = F(e.GetAttribute("cy"), .5f); gp.rad = F(e.GetAttribute("r"), .5f); }
            else { gp.x1 = F(e.GetAttribute("x1"), 0); gp.y1 = F(e.GetAttribute("y1"), 0); gp.x2 = F(e.GetAttribute("x2"), 1); gp.y2 = F(e.GetAttribute("y2"), 0); }
            var stops = new List<float[]>();
            foreach (XmlNode c in e.ChildNodes) if (c is XmlElement se && se.LocalName == "stop") {
                Color(se.GetAttribute("stop-color"), out var r, out var g, out var b);
                stops.Add(new[] { F(se.GetAttribute("offset")), r, g, b, se.HasAttribute("stop-opacity") ? F(se.GetAttribute("stop-opacity"), 1) : 1 });
            }
            if (stops.Count == 0) return null;
            for (int i = 0; i < 256; i++) {
                float t = i / 255f; int k = 0; while (k < stops.Count - 1 && stops[k + 1][0] <= t) k++;
                float[] A = stops[k], Bs = stops[Math.Min(k + 1, stops.Count - 1)];
                float u = Bs[0] > A[0] ? Math.Min(1, Math.Max(0, (t - A[0]) / (Bs[0] - A[0]))) : 0; if (t < stops[0][0]) u = 0;
                float al = A[4] + (Bs[4] - A[4]) * u;
                gp.lut[i * 4] = (A[1] + (Bs[1] - A[1]) * u) * al; gp.lut[i * 4 + 1] = (A[2] + (Bs[2] - A[2]) * u) * al; gp.lut[i * 4 + 2] = (A[3] + (Bs[3] - A[3]) * u) * al; gp.lut[i * 4 + 3] = al;
            }
            return gp;
        }

        void DrawShape(XmlElement e, SLMat m, Style st, SLSurface dst, Mask clip, float op) {
            var subs = Geometry(e, m); if (subs.Count == 0) return;
            if (st.fill != "none") { var p = MakePaint(st.fill, st.fo * op, m, subs); if (p != null) Fill(subs, p, dst, clip, true); }
            if (st.stroke != "none" && st.sw > 0) {
                var p = MakePaint(st.stroke, st.so * op, m, subs); if (p == null) return;
                float hw = Math.Max(st.sw * m.Scale / 2, 0.01f);
                float[] dash = null;
                if (!string.IsNullOrEmpty(st.dash) && st.dash != "none") { var dv = Nums(st.dash); if (dv.Count > 0) { if (dv.Count % 2 == 1) dv.AddRange(dv.ToArray()); dash = dv.ToArray(); } }
                float pl = e.HasAttribute("pathLength") ? F(e.GetAttribute("pathLength")) : 0;
                var polys = new List<Sub>();
                foreach (var s in subs) {
                    if (dash == null) Stroke(s.p, s.closed, hw, st, polys);
                    else foreach (var seg in Dash(s, dash, pl, m.Scale)) Stroke(seg, false, hw, st, polys);
                }
                Fill(polys, p, dst, clip, false);
            }
        }

        static List<List<float>> Dash(Sub s, float[] dash, float pathLength, float scale) {
            var o = new List<List<float>>(); var p = s.p; int n = p.Count / 2; if (n < 2) return o;
            float total = 0; int cnt = s.closed ? n : n - 1;
            for (int i = 0; i < cnt; i++) { int j = (i + 1) % n; total += (float)Math.Sqrt((p[j * 2] - p[i * 2]) * (p[j * 2] - p[i * 2]) + (p[j * 2 + 1] - p[i * 2 + 1]) * (p[j * 2 + 1] - p[i * 2 + 1])); }
            float k = pathLength > 0 ? total / pathLength : scale; float sum = 0; foreach (var v in dash) sum += v; if (sum <= 0) return o;
            int di = 0; float left = dash[0] * k; bool on = true; List<float> cur = new List<float> { p[0], p[1] };
            for (int i = 0; i < cnt; i++) {
                int j = (i + 1) % n; float ax = p[i * 2], ay = p[i * 2 + 1], bx = p[j * 2], by = p[j * 2 + 1];
                float len = (float)Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)), pos = 0;
                while (len - pos > left) {
                    pos += left; float t = len > 0 ? pos / len : 0, x = ax + (bx - ax) * t, y = ay + (by - ay) * t;
                    if (on) { cur.Add(x); cur.Add(y); if (cur.Count >= 4 && dash[di] > 0) o.Add(cur); cur = null; } else cur = new List<float> { x, y };
                    on = !on; di = (di + 1) % dash.Length; left = dash[di] * k; if (left <= 0 && dash[di] <= 0) left = 0;
                    if (o.Count > 4000) return o;
                }
                left -= len - pos; if (on && cur != null) { cur.Add(bx); cur.Add(by); }
            }
            if (on && cur != null && cur.Count >= 4) o.Add(cur);
            return o;
        }

        static void Poly(List<Sub> o, params float[] v) {
            float ar = 0; int n = v.Length / 2;
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; ar += v[i * 2] * v[j * 2 + 1] - v[j * 2] * v[i * 2 + 1]; }
            var s = new Sub { closed = true };
            if (ar >= 0) s.p.AddRange(v); else for (int i = n - 1; i >= 0; i--) { s.p.Add(v[i * 2]); s.p.Add(v[i * 2 + 1]); }
            o.Add(s);
        }
        static void Disc(List<Sub> o, float x, float y, float r) {
            int n = Math.Max(8, Math.Min(48, (int)(r * 2.2f) + 6)); var v = new float[n * 2];
            for (int i = 0; i < n; i++) { double a = i * 2 * Math.PI / n; v[i * 2] = x + r * (float)Math.Cos(a); v[i * 2 + 1] = y + r * (float)Math.Sin(a); }
            Poly(o, v);
        }
        static void Stroke(List<float> p, bool closed, float hw, Style st, List<Sub> o) {
            // drop repeated points
            var q = new List<float>(p.Count);
            for (int i = 0; i < p.Count; i += 2) { if (q.Count >= 2 && Math.Abs(q[q.Count - 2] - p[i]) < 1e-4f && Math.Abs(q[q.Count - 1] - p[i + 1]) < 1e-4f) continue; q.Add(p[i]); q.Add(p[i + 1]); }
            int n = q.Count / 2;
            if (closed && n > 2 && Math.Abs(q[0] - q[n * 2 - 2]) < 1e-4f && Math.Abs(q[1] - q[n * 2 - 1]) < 1e-4f) { q.RemoveRange(n * 2 - 2, 2); n--; }
            if (n == 0) return;
            bool round = st.join == "round", rcap = st.cap == "round", sq = st.cap == "square";
            if (n == 1) { if (rcap) Disc(o, q[0], q[1], hw); else if (sq) Poly(o, q[0] - hw, q[1] - hw, q[0] + hw, q[1] - hw, q[0] + hw, q[1] + hw, q[0] - hw, q[1] + hw); return; }
            int segs = closed ? n : n - 1; float pnx = 0, pny = 0; bool have = false; float fnx = 0, fny = 0;
            for (int i = 0; i < segs; i++) {
                int j = (i + 1) % n; float ax = q[i * 2], ay = q[i * 2 + 1], bx = q[j * 2], by = q[j * 2 + 1];
                float dx = bx - ax, dy = by - ay, len = (float)Math.Sqrt(dx * dx + dy * dy); if (len < 1e-6f) continue;
                float ux = dx / len, uy = dy / len, nx = -uy * hw, ny = ux * hw;
                float sax = ax, say = ay, sbx = bx, sby = by;
                if (!closed && sq) { if (i == 0) { sax -= ux * hw; say -= uy * hw; } if (i == segs - 1) { sbx += ux * hw; sby += uy * hw; } }
                Poly(o, sax + nx, say + ny, sbx + nx, sby + ny, sbx - nx, sby - ny, sax - nx, say - ny);
                if (have) Join(o, ax, ay, pnx, pny, nx, ny, hw, round, st.join == "bevel");
                else { fnx = nx; fny = ny; }
                pnx = nx; pny = ny; have = true;
            }
            if (closed && have) Join(o, q[0], q[1], pnx, pny, fnx, fny, hw, round, st.join == "bevel");
            if (!closed && rcap) { Disc(o, q[0], q[1], hw); Disc(o, q[n * 2 - 2], q[n * 2 - 1], hw); }
        }
        static void Join(List<Sub> o, float x, float y, float n1x, float n1y, float n2x, float n2y, float hw, bool round, bool bevel) {
            float cr = n1x * n2y - n1y * n2x, dt = n1x * n2x + n1y * n2y;
            if (dt > 0 && Math.Abs(cr) < 0.2f * hw) return; // gap narrower than a fifth of a pixel
            if (round) { Disc(o, x, y, hw); return; }
            float s = cr > 0 ? -1 : 1; // outer side
            float ax = x + n1x * s, ay = y + n1y * s, bx = x + n2x * s, by = y + n2y * s;
            float mx = n1x + n2x, my = n1y + n2y, ml = mx * mx + my * my;
            if (!bevel && ml > 1e-6f) {
                float k = 2 * hw * hw / ml; // miter length factor along (n1+n2)
                if (Math.Sqrt(k * k * ml) / hw <= 4f) { Poly(o, x, y, ax, ay, x + mx * k * s, y + my * k * s, bx, by); return; }
            }
            Poly(o, x, y, ax, ay, bx, by);
        }

        // ---------------------------------------------------------------- rasteriser (signed-area accumulation)
        int aw, ah; float aox, aoy;
        void AccLine(float x0, float y0, float x1, float y1) {
            if (y0 == y1) return;
            // clip in x by splitting at the buffer edges; outside parts become vertical edges on the boundary
            if (x0 < 0 || x1 < 0) {
                if (x0 <= 0 && x1 <= 0) { x0 = x1 = 0; }
                else { float t = (0 - x0) / (x1 - x0), ym = y0 + (y1 - y0) * t; if (x0 < 0) { AccLine(0, y0, 0, ym); AccLine(0, ym, x1, y1); } else { AccLine(x0, y0, 0, ym); AccLine(0, ym, 0, y1); } return; }
            }
            if (x0 > aw || x1 > aw) {
                if (x0 >= aw && x1 >= aw) { x0 = x1 = aw; }
                else { float t = (aw - x0) / (x1 - x0), ym = y0 + (y1 - y0) * t; if (x0 > aw) { AccLine(aw, y0, aw, ym); AccLine(aw, ym, x1, y1); } else { AccLine(x0, y0, aw, ym); AccLine(aw, ym, aw, y1); } return; }
            }
            float dir = 1; if (y0 > y1) { dir = -1; float t = x0; x0 = x1; x1 = t; t = y0; y0 = y1; y1 = t; }
            if (y1 <= 0 || y0 >= ah) return;
            float dxdy = (x1 - x0) / (y1 - y0), x = x0;
            if (y0 < 0) { x -= y0 * dxdy; y0 = 0; }
            int yEnd = Math.Min(ah, (int)Math.Ceiling(y1)), stride = aw + 2;
            for (int y = (int)y0; y < yEnd; y++) {
                int lin = y * stride;
                float dy = Math.Min(y + 1, y1) - Math.Max(y, y0), xn = x + dxdy * dy, d = dy * dir;
                if (xn < 0) xn = 0; else if (xn > aw) xn = aw; // rounding must not walk off the buffer
                float xa = x < xn ? x : xn, xb = x < xn ? xn : x;
                float xaf = (float)Math.Floor(xa); int xi = (int)xaf; float xbc = (float)Math.Ceiling(xb); int xj = (int)xbc;
                if (xj <= xi + 1) { float xmf = .5f * (x + xn) - xaf; acc[lin + xi] += d - d * xmf; acc[lin + xi + 1] += d * xmf; }
                else {
                    float s = 1f / (xb - xa), x0f = xa - xaf, a0 = .5f * s * (1 - x0f) * (1 - x0f), x1f = xb - xbc + 1, am = .5f * s * x1f * x1f;
                    acc[lin + xi] += d * a0;
                    if (xj == xi + 2) acc[lin + xi + 1] += d * (1 - a0 - am);
                    else {
                        float a1 = s * (1.5f - x0f); acc[lin + xi + 1] += d * (a1 - a0);
                        for (int k = xi + 2; k < xj - 1; k++) acc[lin + k] += d * s;
                        float a2 = a1 + (xj - xi - 3) * s; acc[lin + xj - 1] += d * (1 - a2 - am);
                    }
                    acc[lin + xj] += d * am;
                }
                x = xn;
            }
        }
        // accumulate subpaths over the rectangle (rx,ry,rw,rh); returns false when nothing overlaps
        bool Accumulate(List<Sub> subs, int cx0, int cy0, int cx1, int cy1, out int rx, out int ry) {
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            foreach (var s in subs) for (int i = 0; i < s.p.Count; i += 2) { float px = s.p[i], py = s.p[i + 1]; if (px < x0) x0 = px; if (px > x1) x1 = px; if (py < y0) y0 = py; if (py > y1) y1 = py; }
            rx = Math.Max(cx0, (int)Math.Floor(x0)); ry = Math.Max(cy0, (int)Math.Floor(y0));
            int ex = Math.Min(cx1, (int)Math.Ceiling(x1) + 1), ey = Math.Min(cy1, (int)Math.Ceiling(y1) + 1);
            aw = ex - rx; ah = ey - ry; if (aw <= 0 || ah <= 0) return false;
            int need = (aw + 2) * ah + 4; if (acc.Length < need) acc = new float[need * 2]; else Array.Clear(acc, 0, need);
            aox = rx; aoy = ry;
            foreach (var s in subs) {
                int n = s.p.Count / 2; if (n < 2) continue;
                for (int i = 0; i < n; i++) { int j = (i + 1) % n; AccLine(s.p[i * 2] - aox, s.p[i * 2 + 1] - aoy, s.p[j * 2] - aox, s.p[j * 2 + 1] - aoy); }
            }
            return true;
        }
        void Fill(List<Sub> subs, Paint paint, SLSurface dst, Mask clip, bool evenOddSafe) {
            if (!Accumulate(subs, dst.X, dst.Y, dst.X + dst.W, dst.Y + dst.H, out int rx, out int ry)) return;
            var solid = paint as Solid; int stride = aw + 2; var P = dst.P;
            for (int y = 0; y < ah; y++) {
                float sum = 0; int lin = y * stride, gy = ry + y, di = ((gy - dst.Y) * dst.W + rx - dst.X) * 4;
                bool mrow = clip == null || (gy >= clip.Y && gy < clip.Y + clip.H); int mi = clip != null ? (gy - clip.Y) * clip.W + rx - clip.X : 0;
                for (int x = 0; x < aw; x++, di += 4, mi++) {
                    sum += acc[lin + x];
                    float cov = sum < 0 ? -sum : sum; if (cov < 0.004f) continue; if (cov > 1) cov = 1;
                    if (clip != null) { int gx = rx + x; if (!mrow || gx < clip.X || gx >= clip.X + clip.W) continue; cov *= clip.A[mi]; if (cov < 0.004f) continue; }
                    float r, g, b, a;
                    if (solid != null) { r = solid.R; g = solid.G; b = solid.Bl; a = solid.A; } else paint.At(rx + x + .5f, gy + .5f, out r, out g, out b, out a);
                    float ia = 1 - a * cov;
                    P[di] = r * cov + P[di] * ia; P[di + 1] = g * cov + P[di + 1] * ia; P[di + 2] = b * cov + P[di + 2] * ia; P[di + 3] = a * cov + P[di + 3] * ia;
                }
            }
        }

        Mask BuildClip(XmlElement ce, SLMat m, SLSurface dst, Mask parent) {
            var mk = new Mask { X = dst.X, Y = dst.Y, W = dst.W, H = dst.H, A = new float[dst.W * dst.H] };
            foreach (XmlNode c in ce.ChildNodes) {
                if (!(c is XmlElement ch) || !IsShape(ch.LocalName)) continue;
                var subs = Geometry(ch, Local(ch, m));
                if (!Accumulate(subs, dst.X, dst.Y, dst.X + dst.W, dst.Y + dst.H, out int rx, out int ry)) continue;
                int stride = aw + 2;
                for (int y = 0; y < ah; y++) {
                    float sum = 0; int lin = y * stride, mi = (ry + y - mk.Y) * mk.W + rx - mk.X;
                    for (int x = 0; x < aw; x++, mi++) { sum += acc[lin + x]; float cov = sum < 0 ? -sum : sum; if (cov > 1) cov = 1; if (cov > mk.A[mi]) mk.A[mi] = cov; }
                }
            }
            if (parent != null) for (int y = 0; y < mk.H; y++) for (int x = 0; x < mk.W; x++) {
                int gx = mk.X + x, gy = mk.Y + y; float pa = gx >= parent.X && gx < parent.X + parent.W && gy >= parent.Y && gy < parent.Y + parent.H ? parent.A[(gy - parent.Y) * parent.W + gx - parent.X] : 0;
                mk.A[y * mk.W + x] *= pa;
            }
            return mk;
        }

        void DrawText(XmlElement e, SLMat m, Style st, SLSurface dst, Mask clip, float op) {
            if (text == null || !text.TryGetValue(e.InnerText, out var g) || st.fill == "none") return;
            Color(st.fill, out var cr, out var cg, out var cb); float al = st.fo * op;
            // the glyph bitmap is g.Em pixels per em; place it at (x, y) baseline with the anchor
            float k = st.fs / g.Em, w = g.W * k, h = g.H * k, x = F(e.GetAttribute("x")), y = F(e.GetAttribute("y"));
            float ox = st.anchor == "middle" ? x - w / 2 : st.anchor == "end" ? x - w : x, oy = y - h * 0.78f;
            var full = m * new SLMat { a = k, d = k, e = ox, f = oy }; var inv = full.Inv();
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            foreach (var c in new[] { new[] { 0f, 0f }, new[] { g.W, 0f }, new[] { g.W, (float)g.H }, new[] { 0f, (float)g.H } }) { full.Map(c[0], c[1], out var px, out var py); x0 = Math.Min(x0, px); x1 = Math.Max(x1, px); y0 = Math.Min(y0, py); y1 = Math.Max(y1, py); }
            int ix0 = Math.Max(dst.X, (int)x0), iy0 = Math.Max(dst.Y, (int)y0), ix1 = Math.Min(dst.X + dst.W, (int)x1 + 1), iy1 = Math.Min(dst.Y + dst.H, (int)y1 + 1);
            for (int py = iy0; py < iy1; py++) for (int px = ix0; px < ix1; px++) {
                inv.Map(px + .5f, py + .5f, out var gx, out var gy); gx -= .5f; gy -= .5f;
                int gx0 = (int)Math.Floor(gx), gy0 = (int)Math.Floor(gy); float fx = gx - gx0, fy = gy - gy0;
                float S(int sx, int sy) => sx < 0 || sy < 0 || sx >= g.W || sy >= g.H ? 0 : g.A[sy * g.W + sx];
                float cov = (S(gx0, gy0) * (1 - fx) + S(gx0 + 1, gy0) * fx) * (1 - fy) + (S(gx0, gy0 + 1) * (1 - fx) + S(gx0 + 1, gy0 + 1) * fx) * fy;
                cov *= al; if (cov < 0.004f) continue;
                if (clip != null) { if (px < clip.X || py < clip.Y || px >= clip.X + clip.W || py >= clip.Y + clip.H) continue; cov *= clip.A[(py - clip.Y) * clip.W + px - clip.X]; }
                int di = ((py - dst.Y) * dst.W + px - dst.X) * 4; float ia = 1 - cov;
                dst.P[di] = cr * cov + dst.P[di] * ia; dst.P[di + 1] = cg * cov + dst.P[di + 1] * ia; dst.P[di + 2] = cb * cov + dst.P[di + 2] * ia; dst.P[di + 3] = cov + dst.P[di + 3] * ia;
            }
        }
    }

    internal static float Num(string s, float d = 0) => F(s, d);
    internal static List<float> NumList(string s) => Nums(s);
    internal static bool Col(string s, out float r, out float g, out float b) => Color(s, out r, out g, out b);
}
