// Turns the core's SVG into textures off the main thread, with a cache, and a VisualElement that shows one.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public static class SLArt {
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    static readonly Queue<string> order = new Queue<string>();
    static readonly Dictionary<string, List<Action<Texture2D>>> pending = new Dictionary<string, List<Action<Texture2D>>>();
    static readonly ConcurrentQueue<Action> main = new ConcurrentQueue<Action>();
    public static int Busy => pending.Count;

    // the web player has no threads: there, one drawing is done per frame on the main thread
    static readonly bool Threaded = Application.platform != RuntimePlatform.WebGLPlayer;
    static readonly Queue<Action> jobs = new Queue<Action>();
    public static void Pump() {
        if (jobs.Count > 0) jobs.Dequeue()();
        while (main.TryDequeue(out var a)) a();
    }

    public static void Get(string key, Func<string> svg, int w, int h, Action<Texture2D> done) {
        if (cache.TryGetValue(key, out var hit) && hit != null) { done(hit); return; }
        if (pending.TryGetValue(key, out var list)) { list.Add(done); return; }
        pending[key] = new List<Action<Texture2D>> { done };
        string src = svg();
        Action job = () => {
            byte[] px = null; string err = null;
            try { px = SLSvg.Render(src, w, h, true); } catch (Exception e) { err = e.ToString(); }
            main.Enqueue(() => {
                var cbs = pending[key]; pending.Remove(key);
                if (px == null) { Debug.LogError("Starlife: could not draw " + key + "\n" + err); return; }
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = key };
                t.LoadRawTextureData(px); t.Apply(false, true);
                cache[key] = t; order.Enqueue(key);
                while (order.Count > 500) { string old = order.Dequeue(); if (cache.TryGetValue(old, out var ot)) { cache.Remove(old); UnityEngine.Object.Destroy(ot); } }
                foreach (var cb in cbs) cb(t);
            });
        };
        if (Threaded) Task.Run(job); else jobs.Enqueue(job);
    }
    public static byte[] RenderNow(string svg, int w, int h) => SLSvg.Render(svg, w, h, true);
}

/// <summary>Shows SVG from the core, drawn at the element's own pixel size. The key identifies the drawing for the cache.</summary>
public class SvgImage : VisualElement {
    Func<string> svg; Func<int, int, Texture> tex3d; string key; int ver; int lw, lh, fixW, fixH;
    public SvgImage(string cls = null) {
        if (cls != null) AddToClassList(cls);
        AddToClassList("svg");
        RegisterCallback<GeometryChangedEvent>(_ => Refresh());
        RegisterCallback<AttachToPanelEvent>(_ => schedule.Execute(Refresh));
    }
    public SvgImage Set(string key, Func<string> svg) {
        if (this.key == key) return this;
        this.key = key; this.svg = svg; tex3d = null; lw = lh = 0; Refresh(); return this;
    }
    /// <summary>Shows a picture made for the element's pixel size instead of a drawing (the 3D stage's renders).</summary>
    public SvgImage Set3D(string key, Func<int, int, Texture> render) {
        if (this.key == key) return this;
        this.key = key; svg = null; tex3d = render; lw = lh = 0; Refresh(); return this;
    }
    /// <summary>Draw at this pixel size whatever the layout size, so moving or scaling the element never redraws it.</summary>
    public SvgImage Fixed(int w, int h) { if (w != fixW || h != fixH) { fixW = w; fixH = h; lw = lh = 0; if (key != null) Refresh(); } return this; }
    void Refresh() {
        if ((svg == null && tex3d == null) || panel == null) return;
        var r = contentRect; float s = scaledPixelsPerPoint;
        int w = Mathf.Min(2048, fixW > 0 ? fixW : Mathf.RoundToInt(r.width * s)), h = Mathf.Min(2048, fixH > 0 ? fixH : Mathf.RoundToInt(r.height * s));
        if (w < 4 || h < 4 || (w == lw && h == lh)) return;
        lw = w; lh = h; int v = ++ver;
        if (tex3d != null) { var t3 = tex3d(w, h); style.backgroundImage = t3 is RenderTexture rt ? Background.FromRenderTexture(rt) : Background.FromTexture2D(t3 as Texture2D); return; }
        SLArt.Get(key + "@" + w + "x" + h, svg, w, h, t => { if (v == ver) style.backgroundImage = t; });
    }
}
