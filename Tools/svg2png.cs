// Command-line harness: renders SVG files with SLSvg so the output can be compared with Chrome.
using System; using System.IO; using System.IO.Compression; using System.Diagnostics;
static class P {
    static void Main(string[] a) {
        float scale = float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture);
        for (int i = 1; i < a.Length; i++) {
            string svg = File.ReadAllText(a[i]);
            var m = System.Text.RegularExpressions.Regex.Match(svg, "viewBox=\"([^\"]+)\""); var v = m.Groups[1].Value.Split(' ');
            int w = (int)(float.Parse(v[2]) * scale), h = (int)(float.Parse(v[3]) * scale);
            var sw = Stopwatch.StartNew(); var px = SLSvg.Render(svg, w, h); sw.Stop();
            Png(Path.ChangeExtension(a[i], ".cs.png"), px, w, h);
            Console.WriteLine(Path.GetFileName(a[i]) + " " + w + "x" + h + " " + sw.ElapsedMilliseconds + " ms");
        }
    }
    static void Png(string path, byte[] rgba, int w, int h) {
        using (var fs = File.Create(path)) {
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var hd = new byte[13]; BE(hd, 0, w); BE(hd, 4, h); hd[8] = 8; hd[9] = 6; Chunk(fs, "IHDR", hd);
            var raw = new MemoryStream();
            for (int y = 0; y < h; y++) { raw.WriteByte(0); raw.Write(rgba, y * w * 4, w * 4); }
            var z = new MemoryStream(); z.WriteByte(0x78); z.WriteByte(0x9c);
            using (var d = new DeflateStream(z, CompressionMode.Compress, true)) { var b = raw.ToArray(); d.Write(b, 0, b.Length); }
            uint s1 = 1, s2 = 0; foreach (var b in raw.ToArray()) { s1 = (s1 + b) % 65521; s2 = (s2 + s1) % 65521; }
            var ad = new byte[4]; BE(ad, 0, (int)((s2 << 16) | s1)); z.Write(ad, 0, 4);
            Chunk(fs, "IDAT", z.ToArray()); Chunk(fs, "IEND", new byte[0]);
        }
    }
    static void BE(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
    static void Chunk(Stream s, string t, byte[] d) {
        var len = new byte[4]; BE(len, 0, d.Length); s.Write(len, 0, 4);
        var td = new byte[4 + d.Length]; for (int i = 0; i < 4; i++) td[i] = (byte)t[i]; Array.Copy(d, 0, td, 4, d.Length); s.Write(td, 0, td.Length);
        uint c = 0xffffffff; foreach (var b in td) { c ^= b; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1; }
        var cb = new byte[4]; BE(cb, 0, (int)(c ^ 0xffffffff)); s.Write(cb, 0, 4);
    }
}
