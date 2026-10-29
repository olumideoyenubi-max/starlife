// The SVG filter primitives used by the Starlife artwork, applied in place to a layer.
using System;
using System.Collections.Generic;
using System.Xml;

public static class SLFilter {
    public static void Apply(XmlElement fe, SLSurface L, SLMat m, float vpScale) {
        int w = L.W, h = L.H; float sc = m.Scale;
        var res = new Dictionary<string, float[]>(); float[] src = L.P, srcA = null, last = src;
        float[] Get(string name) {
            if (string.IsNullOrEmpty(name)) return last;
            if (name == "SourceGraphic") return src;
            if (name == "SourceAlpha") { if (srcA == null) { srcA = new float[src.Length]; for (int i = 3; i < src.Length; i += 4) srcA[i] = src[i]; } return srcA; }
            return res.TryGetValue(name, out var v) ? v : last;
        }
        foreach (XmlNode node in fe.ChildNodes) {
            if (!(node is XmlElement p)) continue;
            string a = p.GetAttribute("in"), b = p.GetAttribute("in2"); float[] o;
            switch (p.LocalName) {
                case "feGaussianBlur": { var sd = SLSvg.NumList(p.GetAttribute("stdDeviation")); o = Blur(Get(a), w, h, (sd.Count > 0 ? sd[0] : 0) * sc, a == "SourceAlpha"); break; }
                case "feColorMatrix": o = ColorMatrix(Get(a), SLSvg.NumList(p.GetAttribute("values"))); break;
                case "feComposite": o = Comp(Get(a), Get(b), p); break;
                case "feMerge": {
                    o = new float[src.Length];
                    foreach (XmlNode mn in p.ChildNodes) if (mn is XmlElement me) Over(o, Get(me.GetAttribute("in")));
                    break;
                }
                case "feDiffuseLighting": o = Light(Get(a), w, h, p, sc, false); break;
                case "feSpecularLighting": o = Light(Get(a), w, h, p, sc, true); break;
                case "feComponentTransfer": o = Transfer(Get(a), p); break;
                case "feBlend": {
                    float[] top = Get(a), bot = Get(b); o = new float[src.Length]; bool mul = p.GetAttribute("mode") == "multiply";
                    for (int i = 0; i < o.Length; i += 4) {
                        float qa = top[i + 3], qb = bot[i + 3];
                        for (int c = 0; c < 3; c++) { float ca = top[i + c], cb = bot[i + c]; o[i + c] = mul ? (1 - qb) * ca + (1 - qa) * cb + ca * cb : ca + cb * (1 - qa); }
                        o[i + 3] = qa + qb - qa * qb;
                    }
                    break;
                }
                case "feTurbulence": o = Turbulence(L, m, p); break;
                default: continue;
            }
            last = o; string rn = p.GetAttribute("result"); if (!string.IsNullOrEmpty(rn)) res[rn] = o;
        }
        if (!ReferenceEquals(last, L.P)) Array.Copy(last, L.P, L.P.Length);
    }

    static void Over(float[] d, float[] s) {
        for (int i = 0; i < d.Length; i += 4) { float ia = 1 - s[i + 3]; d[i] = s[i] + d[i] * ia; d[i + 1] = s[i + 1] + d[i + 1] * ia; d[i + 2] = s[i + 2] + d[i + 2] * ia; d[i + 3] = s[i + 3] + d[i + 3] * ia; }
    }
    static float[] Comp(float[] a, float[] b, XmlElement p) {
        var o = new float[a.Length]; string op = p.GetAttribute("operator");
        if (op == "in") { for (int i = 0; i < o.Length; i += 4) { float k = b[i + 3]; o[i] = a[i] * k; o[i + 1] = a[i + 1] * k; o[i + 2] = a[i + 2] * k; o[i + 3] = a[i + 3] * k; } }
        else if (op == "arithmetic") {
            float k1 = SLSvg.Num(p.GetAttribute("k1")), k2 = SLSvg.Num(p.GetAttribute("k2")), k3 = SLSvg.Num(p.GetAttribute("k3")), k4 = SLSvg.Num(p.GetAttribute("k4"));
            for (int i = 0; i < o.Length; i += 4) {
                float al = Clamp01(k1 * a[i + 3] * b[i + 3] + k2 * a[i + 3] + k3 * b[i + 3] + k4); o[i + 3] = al;
                for (int c = 0; c < 3; c++) { float v = k1 * a[i + c] * b[i + c] + k2 * a[i + c] + k3 * b[i + c] + k4; o[i + c] = v < 0 ? 0 : v > al ? al : v; }
            }
        } else { Array.Copy(b, o, o.Length); Over(o, a); }
        return o;
    }
    static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    static float[] ColorMatrix(float[] s, List<float> v) {
        var o = new float[s.Length]; if (v.Count < 20) { Array.Copy(s, o, o.Length); return o; }
        for (int i = 0; i < o.Length; i += 4) {
            float a = s[i + 3], k = a > 1e-5f ? 1 / a : 0, r = s[i] * k, g = s[i + 1] * k, b = s[i + 2] * k;
            float na = Clamp01(v[15] * r + v[16] * g + v[17] * b + v[18] * a + v[19]);
            o[i] = Clamp01(v[0] * r + v[1] * g + v[2] * b + v[3] * a + v[4]) * na; o[i + 1] = Clamp01(v[5] * r + v[6] * g + v[7] * b + v[8] * a + v[9]) * na;
            o[i + 2] = Clamp01(v[10] * r + v[11] * g + v[12] * b + v[13] * a + v[14]) * na; o[i + 3] = na;
        }
        return o;
    }
    static float[] Transfer(float[] s, XmlElement p) {
        float[] sl = { 1, 1, 1 }, ic = { 0, 0, 0 };
        foreach (XmlNode n in p.ChildNodes) if (n is XmlElement f && f.GetAttribute("type") == "linear") {
            int c = f.LocalName == "feFuncR" ? 0 : f.LocalName == "feFuncG" ? 1 : f.LocalName == "feFuncB" ? 2 : -1; if (c < 0) continue;
            sl[c] = SLSvg.Num(f.GetAttribute("slope"), 1); ic[c] = SLSvg.Num(f.GetAttribute("intercept"));
        }
        var o = new float[s.Length];
        for (int i = 0; i < o.Length; i += 4) {
            float a = s[i + 3], k = a > 1e-5f ? 1 / a : 0; o[i + 3] = a;
            for (int c = 0; c < 3; c++) o[i + c] = Clamp01(s[i + c] * k * sl[c] + ic[c]) * a;
        }
        return o;
    }

    // ---- blur: exact kernel for small radii, three box passes otherwise (as browsers do)
    static float[] Blur(float[] s, int w, int h, float sd, bool alphaOnly) {
        var o = (float[])s.Clone(); if (sd < 0.05f || w == 0 || h == 0) return o;
        var t = new float[o.Length]; int c0 = alphaOnly ? 3 : 0;
        if (sd < 2f) {
            int r = (int)Math.Ceiling(sd * 3); var k = new float[r * 2 + 1]; float sum = 0;
            for (int i = -r; i <= r; i++) { k[i + r] = (float)Math.Exp(-i * i / (2.0 * sd * sd)); sum += k[i + r]; }
            for (int i = 0; i < k.Length; i++) k[i] /= sum;
            Conv(o, t, w, h, 4, w * 4, k, r, c0); Conv(t, o, h, w, w * 4, 4, k, r, c0);
            return o;
        }
        int d = (int)Math.Floor(sd * 3 * Math.Sqrt(2 * Math.PI) / 4 + 0.5);
        int[][] pass = d % 2 == 1 ? new[] { new[] { d / 2, d / 2 }, new[] { d / 2, d / 2 }, new[] { d / 2, d / 2 } } : new[] { new[] { d / 2, d / 2 - 1 }, new[] { d / 2 - 1, d / 2 }, new[] { d / 2, d / 2 } };
        Box(o, t, w, h, 4, w * 4, pass[0], c0); Box(t, o, w, h, 4, w * 4, pass[1], c0); Box(o, t, w, h, 4, w * 4, pass[2], c0);
        Box(t, o, h, w, w * 4, 4, pass[0], c0); Box(o, t, h, w, w * 4, 4, pass[1], c0); Box(t, o, h, w, w * 4, 4, pass[2], c0);
        return o;
    }
    static void Conv(float[] s, float[] d, int len, int lines, int step, int lineStep, float[] k, int r, int c0) {
        for (int l = 0; l < lines; l++) for (int c = c0; c < 4; c++) {
            int b = l * lineStep + c;
            for (int i = 0; i < len; i++) {
                float v = 0; int j0 = Math.Max(0, i - r), j1 = Math.Min(len - 1, i + r);
                for (int j = j0; j <= j1; j++) v += s[b + j * step] * k[j - i + r];
                d[b + i * step] = v;
            }
        }
    }
    static void Box(float[] s, float[] d, int len, int lines, int step, int lineStep, int[] lr, int c0) {
        int lft = lr[0], rgt = lr[1]; float inv = 1f / (lft + rgt + 1);
        for (int l = 0; l < lines; l++) for (int c = c0; c < 4; c++) {
            int b = l * lineStep + c; float sum = 0;
            for (int j = 0; j <= Math.Min(rgt, len - 1); j++) sum += s[b + j * step];
            for (int i = 0; i < len; i++) {
                d[b + i * step] = sum * inv;
                int add = i + rgt + 1, rem = i - lft;
                if (add < len) sum += s[b + add * step];
                if (rem >= 0) sum -= s[b + rem * step];
            }
        }
    }

    // ---- lighting from the alpha channel as a height map, distant light only
    static float[] Light(float[] s, int w, int h, XmlElement p, float res, bool spec) {
        float ss = SLSvg.Num(p.GetAttribute("surfaceScale"), 1) * res, kc = SLSvg.Num(p.GetAttribute(spec ? "specularConstant" : "diffuseConstant"), 1), ex = SLSvg.Num(p.GetAttribute("specularExponent"), 1);
        float cr = 1, cg = 1, cb = 1; if (p.HasAttribute("lighting-color")) SLSvg.Col(p.GetAttribute("lighting-color"), out cr, out cg, out cb);
        double az = 0, el = 0;
        foreach (XmlNode n in p.ChildNodes) if (n is XmlElement le && le.LocalName == "feDistantLight") { az = SLSvg.Num(le.GetAttribute("azimuth")) * Math.PI / 180; el = SLSvg.Num(le.GetAttribute("elevation")) * Math.PI / 180; }
        float lx = (float)(Math.Cos(az) * Math.Cos(el)), ly = (float)(Math.Sin(az) * Math.Cos(el)), lz = (float)Math.Sin(el);
        float hx = lx, hy = ly, hz = lz + 1, hl = (float)Math.Sqrt(hx * hx + hy * hy + hz * hz); hx /= hl; hy /= hl; hz /= hl;
        var o = new float[s.Length];
        for (int y = 0; y < h; y++) {
            int ym = Math.Max(0, y - 1), yp = Math.Min(h - 1, y + 1);
            for (int x = 0; x < w; x++) {
                int xm = Math.Max(0, x - 1), xp = Math.Min(w - 1, x + 1);
                float a00 = s[(ym * w + xm) * 4 + 3], a10 = s[(ym * w + x) * 4 + 3], a20 = s[(ym * w + xp) * 4 + 3];
                float a01 = s[(y * w + xm) * 4 + 3], a21 = s[(y * w + xp) * 4 + 3];
                float a02 = s[(yp * w + xm) * 4 + 3], a12 = s[(yp * w + x) * 4 + 3], a22 = s[(yp * w + xp) * 4 + 3];
                float nx = -ss * ((a20 + 2 * a21 + a22) - (a00 + 2 * a01 + a02)) / 4, ny = -ss * ((a02 + 2 * a12 + a22) - (a00 + 2 * a10 + a20)) / 4;
                float nl = 1f / (float)Math.Sqrt(nx * nx + ny * ny + 1); int i = (y * w + x) * 4;
                if (!spec) {
                    float v = kc * Math.Max(0, (nx * lx + ny * ly + lz) * nl);
                    o[i] = Clamp01(v * cr); o[i + 1] = Clamp01(v * cg); o[i + 2] = Clamp01(v * cb); o[i + 3] = 1;
                } else {
                    float nh = Math.Max(0, (nx * hx + ny * hy + hz) * nl), v = kc * (float)Math.Pow(nh, ex);
                    float r = Clamp01(v * cr), g = Clamp01(v * cg), b = Clamp01(v * cb);
                    o[i] = r; o[i + 1] = g; o[i + 2] = b; o[i + 3] = Math.Max(r, Math.Max(g, b));
                }
            }
        }
        return o;
    }

    // ---- feTurbulence, following the reference implementation in the SVG specification
    const int BSize = 0x100, BM = 0xff, PerlinN = 0x1000;
    static float[] Turbulence(SLSurface L, SLMat m, XmlElement p) {
        var bf = SLSvg.NumList(p.GetAttribute("baseFrequency")); float fx = bf.Count > 0 ? bf[0] : 0, fy = bf.Count > 1 ? bf[1] : fx;
        int oct = (int)SLSvg.Num(p.GetAttribute("numOctaves"), 1); bool fractal = p.GetAttribute("type") == "fractalNoise";
        long seed = (long)Math.Round(SLSvg.Num(p.GetAttribute("seed")));
        var lat = new int[BSize + BSize + 2]; var gr = new double[4, BSize + BSize + 2, 2];
        if (seed <= 0) seed = -(seed % 2147483646) + 1; if (seed > 2147483646) seed = 2147483646;
        long Rnd(long sd) { long r = 16807 * (sd % 127773) - 2836 * (sd / 127773); if (r <= 0) r += 2147483647; return r; }
        int i, j, k;
        for (k = 0; k < 4; k++) for (i = 0; i < BSize; i++) {
            lat[i] = i;
            for (j = 0; j < 2; j++) gr[k, i, j] = (double)(((seed = Rnd(seed)) % (BSize + BSize)) - BSize) / BSize;
            double s = Math.Sqrt(gr[k, i, 0] * gr[k, i, 0] + gr[k, i, 1] * gr[k, i, 1]); gr[k, i, 0] /= s; gr[k, i, 1] /= s;
        }
        i = BSize;
        while (--i > 0) { k = lat[i]; lat[i] = lat[j = (int)((seed = Rnd(seed)) % BSize)]; lat[j] = k; }
        for (i = 0; i < BSize + 2; i++) { lat[BSize + i] = lat[i]; for (k = 0; k < 4; k++) for (j = 0; j < 2; j++) gr[k, BSize + i, j] = gr[k, i, j]; }
        double Noise(int ch, double vx, double vy) {
            double t = vx + PerlinN; int bx0 = ((int)t) & BM, bx1 = (bx0 + 1) & BM; double rx0 = t - (int)t, rx1 = rx0 - 1;
            t = vy + PerlinN; int by0 = ((int)t) & BM, by1 = (by0 + 1) & BM; double ry0 = t - (int)t, ry1 = ry0 - 1;
            int a = lat[bx0], b = lat[bx1], b00 = lat[a + by0], b10 = lat[b + by0], b01 = lat[a + by1], b11 = lat[b + by1];
            double sx = rx0 * rx0 * (3 - 2 * rx0), sy = ry0 * ry0 * (3 - 2 * ry0);
            double u = rx0 * gr[ch, b00, 0] + ry0 * gr[ch, b00, 1], v = rx1 * gr[ch, b10, 0] + ry0 * gr[ch, b10, 1], p0 = u + sx * (v - u);
            u = rx0 * gr[ch, b01, 0] + ry1 * gr[ch, b01, 1]; v = rx1 * gr[ch, b11, 0] + ry1 * gr[ch, b11, 1]; double p1 = u + sx * (v - u);
            return p0 + sy * (p1 - p0);
        }
        var inv = m.Inv(); var o = new float[L.P.Length]; var c4 = new float[4];
        for (int y = 0; y < L.H; y++) for (int x = 0; x < L.W; x++) {
            inv.Map(L.X + x + .5f, L.Y + y + .5f, out var ux, out var uy);
            for (int ch = 0; ch < 4; ch++) {
                double sum = 0, vx = ux * fx, vy = uy * fy, ratio = 1;
                for (int n = 0; n < oct; n++) { double nv = Noise(ch, vx, vy); sum += (fractal ? nv : Math.Abs(nv)) / ratio; vx *= 2; vy *= 2; ratio *= 2; }
                c4[ch] = Clamp01((float)(fractal ? (sum + 1) / 2 : sum));
            }
            int idx = (y * L.W + x) * 4; o[idx] = c4[0] * c4[3]; o[idx + 1] = c4[1] * c4[3]; o[idx + 2] = c4[2] * c4[3]; o[idx + 3] = c4[3];
        }
        return o;
    }
}
