// Runs the Starlife core (the artifact's own drawing code and game rules, Resources/starlife_core.txt)
// inside Jint. The core keeps the look and the profile; this class stores them and relays its callbacks.
using System;
using System.Collections.Generic;
using System.IO;
using Jint;
using UnityEngine;

public sealed class SLCore {
    [Serializable] class Save { public List<string> keys = new List<string>(); public List<string> values = new List<string>(); }

    readonly Engine engine;
    readonly Dictionary<string, string> store = new Dictionary<string, string>();
    readonly string path;
    bool dirty;
    public Action<bool> Changed = _ => { };
    public Action<string> Toast = _ => { };
    public Action ProfileChanged = () => { };

    public sealed class Host {
        readonly SLCore c; public Host(SLCore c) { this.c = c; }
        public string get(string k) => c.store.TryGetValue(k, out var v) ? v : null;
        public void set(string k, string v) { c.store[k] = v; c.dirty = true; }
        public void changed(double pop) => c.Changed(pop != 0);
        public void toast(string m) => c.Toast(m);
        public void profile() => c.ProfileChanged();
    }

    public SLCore(string saveName = "starlife-save.json") {
        path = Path.Combine(Application.persistentDataPath, saveName);
        if (File.Exists(path)) {
            try { var s = JsonUtility.FromJson<Save>(File.ReadAllText(path)); for (int i = 0; i < s.keys.Count; i++) store[s.keys[i]] = s.values[i]; }
            catch (Exception e) { Debug.LogWarning("Starlife: could not read the save file, starting fresh. " + e.Message); }
        }
        engine = new Engine();
        engine.SetValue("host", new Host(this));
        engine.Execute(Resources.Load<TextAsset>("starlife_core").text);
    }

    public string Call(string fn, params object[] args) {
        var a = new object[args.Length + 1]; a[0] = fn; Array.Copy(args, 0, a, 1, args.Length);
        return engine.Invoke("slCall", a).ToString();
    }
    public T Get<T>(string fn, params object[] args) => JsonUtility.FromJson<T>(Call(fn, args));
    /// <summary>The host's own settings (job, underwear, avatar choices), kept in the same save file as the game's.</summary>
    public string Pref(string key, string def = "") => store.TryGetValue("host." + key, out var v) && v != null ? v : def;
    public void SetPref(string key, string value) { store["host." + key] = value; dirty = true; }

    public void Flush() {
        if (!dirty) return; dirty = false;
        var s = new Save(); foreach (var kv in store) { s.keys.Add(kv.Key); s.values.Add(kv.Value); }
        File.WriteAllText(path, JsonUtility.ToJson(s));
    }
}
