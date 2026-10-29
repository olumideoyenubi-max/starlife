// Starlife in Unity: the shell (top bar, main menu, toast, starlet picker, home) and shared UI helpers.
// The views themselves are in SLViews.cs; drawing and rules come from the core (SLCore).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable] public class DItem { public string id, name, slot, cat, group, color; public int price; public string[] tags; }
[Serializable] public class DStarlet { public string id, name, role, face, lips; public int fame; }
[Serializable] public class DOpt { public string v, l, dot; }
[Serializable] public class DPal { public string[] skin, eyes, hair, shadow, liner, lips, lipLiner, blush, cloth; public DOpt[] hl; }
[Serializable] public class DPreset { public string name; public string[] dots; }
[Serializable] public class DFurn { public string k, name, layer; public float[] box; }
[Serializable] public class DTheme { public string id, name, desc, tip; public string[] bg, tags; }
[Serializable] public class DJudge { public string name, role, face, lips; }
[Serializable] public class DData {
    public DItem[] items; public DStarlet[] starlets; public DPal pal; public DOpt[] contacts; public DPreset[] presets;
    public DOpt[] eyeShapes, brows, hair, floors; public string[] walls; public DFurn[] furn; public DTheme[] themes; public DJudge[] judges; public int guide, famePerLevel;
}
[Serializable] public class DProfile { public int coins, fame, level, xp; public string[] owned; public bool bonus, canUndo, hasSave, guideSeen; public string name, featured; }
[Serializable] public class DVec { public float x, y; }
[Serializable] public class DRoomItem { public string id, k; public float x, y; }
[Serializable] public class DRoom { public string wall, floor; public DVec doll; public DRoomItem[] items; }
[Serializable] public class DState { public string starlet; public DRoom room; public bool tucked; }
[Serializable] public class DLayer { public string slot, name, color; }
[Serializable] public class DLayers { public DLayer[] list; }
[Serializable] public class DFocus { public string id, name, color; }
[Serializable] public class DLine { public bool ok; public string text; }
[Serializable] public class DBrief { public DLine[] lines; public int best; }
[Serializable] public class DJudgeSay { public int stars; public string say; }
[Serializable] public class DResult { public int score, coins, fame; public string medal, medalColor; public bool beat, repeat; public DJudgeSay[] judges; }
[Serializable] public class DLook { public string id, name, date, thumb; }
[Serializable] public class DLooks { public DLook[] list; }

[RequireComponent(typeof(UIDocument))]
public partial class SLApp : MonoBehaviour {
    SLCore core; DData data; DProfile profile; DState state;
    VisualElement root, overlayHost; string view = "home", dollKey = "";
    readonly Dictionary<string, VisualElement> views = new Dictionary<string, VisualElement>();
    readonly Dictionary<string, Button> navBtns = new Dictionary<string, Button>();
    readonly Dictionary<string, DItem> itemById = new Dictionary<string, DItem>();
    readonly Dictionary<string, DFurn> furnByK = new Dictionary<string, DFurn>();
    bool dirty = true; string shotsDir;
    Label meName, meLevel, xpText, coinVal, fameVal, toast; VisualElement xpFill; SvgImage meFace; IVisualElementScheduledItem toastTimer;

    // ---------------------------------------------------------------- helpers
    static VisualElement El(string cls, params VisualElement[] kids) {
        var e = new VisualElement(); if (!string.IsNullOrEmpty(cls)) foreach (var c in cls.Split(' ')) e.AddToClassList(c);
        foreach (var k in kids) if (k != null) e.Add(k); return e;
    }
    static Label L(string text, string cls = null) { var l = new Label(text); if (cls != null) foreach (var c in cls.Split(' ')) l.AddToClassList(c); return l; }
    static Button B(string text, string cls, Action click) {
        var b = new Button(click) { text = text }; b.ClearClassList(); b.AddToClassList("unity-text-element");
        if (cls != null) foreach (var c in cls.Split(' ')) {
            b.AddToClassList(c);
            if (c == "primary" || c == "save-btn") b.style.backgroundImage = Grad("#a87bf7", "#7c4ee0");
            else if (c == "continue") b.style.backgroundImage = Grad("#77ecbc", "#3fcf8e");
        }
        return b;
    }
    /// <summary>A button with a drawn icon in front of its label.</summary>
    static Button IB(string icon, string color, string text, string cls, Action click) {
        var b = B("", cls, click); b.style.flexDirection = FlexDirection.Row; b.style.alignItems = Align.Center;
        var ic = Icon(icon, color, 20); ic.style.marginRight = 7; ic.pickingMode = PickingMode.Ignore;
        var l = L(text); l.pickingMode = PickingMode.Ignore; b.Add(ic); b.Add(l); return b;
    }
    // line icons on a 24 x 24 grid, drawn by the same renderer as the dolls
    static readonly Dictionary<string, string> Icons = new Dictionary<string, string> {
        { "hanger", "<path d='M12 7.5a2 2 0 1 0-2-2'/><path d='M12 7.5v2l8.5 6a1.2 1.2 0 0 1-.7 2.2H4.2a1.2 1.2 0 0 1-.7-2.2l8.5-6'/>" },
        { "mirror", "<ellipse cx='12' cy='9.5' rx='5.5' ry='7'/><path d='M12 16.5v5M8 21.5h8'/>" },
        { "sofa", "<path d='M5 11V8a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v3'/><path d='M3 12.5a2 2 0 0 1 4 0V15h10v-2.5a2 2 0 0 1 4 0V18H3z'/><path d='M6 18v2M18 18v2'/>" },
        { "camera", "<path d='M4 8h3l1.5-2.5h7L17 8h3a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1z'/><circle cx='12' cy='13.5' r='3.5'/>" },
        { "shirt", "<path d='M8 4l-5 3 2 4 2-1v10h10V10l2 1 2-4-5-3a4 4 0 0 1-8 0z'/>" },
        { "shuffle", "<path d='M3 7h3c5 0 5 10 10 10h5M3 17h3c1.6 0 2.7-1 3.6-2.4M21 7h-5c-1.6 0-2.7 1-3.6 2.4'/><path d='M18 4l3 3-3 3M18 14l3 3-3 3'/>" },
        { "layers", "<path d='M12 3l9 5-9 5-9-5z'/><path d='M3 12.5l9 5 9-5M3 16.5l9 5 9-5'/>" },
        { "undo", "<path d='M8 5L4 9l4 4'/><path d='M4 9h10a5.5 5.5 0 0 1 0 11H9'/>" },
        { "check", "<circle cx='12' cy='12' r='10' fill='COLOR' stroke='none'/><path d='M7 12.5l3.4 3.4L17 9' stroke='#ffffff' stroke-width='2.4'/>" },
        { "cube", "<path d='M12 3l8 4.5v9L12 21l-8-4.5v-9z'/><path d='M4 7.5l8 4.5 8-4.5M12 12v9'/>" },
        { "ring", "<circle cx='12' cy='12' r='9'/>" },
        { "star", "<path d='M12 2.5l2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4l-5.9 3.1 1.2-6.5L2.5 9.4l6.6-.9z' fill='COLOR' stroke-width='1.2' stroke='#b98300'/>" },
    };
    const string StarPath = "M12 2.5l2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4l-5.9 3.1 1.2-6.5L2.5 9.4l6.6-.9z";
    static SvgImage Icon(string name, string color, int size) {
        var i = new SvgImage().Set("icon:" + name + color, () => "<svg viewBox='0 0 24 24'><g fill='none' stroke='" + color + "' stroke-width='1.7' stroke-linecap='round' stroke-linejoin='round'>" + Icons[name].Replace("COLOR", color) + "</g></svg>");
        i.style.width = i.style.height = size; return i;
    }
    static SvgImage Stars(int n) {
        var sb = new System.Text.StringBuilder("<svg viewBox='0 0 120 24'>");
        for (int k = 0; k < 5; k++) sb.Append("<path transform='translate(" + (k * 24) + ",0)' d='" + StarPath + "' fill='" + (k < n ? "#f5b82e" : "#e6e0ee") + "'/>");
        var i = new SvgImage().Set("stars:" + n, () => sb + "</svg>"); i.style.width = 80; i.style.height = 16; return i;
    }
    static readonly Dictionary<string, Texture2D> grads = new Dictionary<string, Texture2D>();
    /// <summary>A vertical two-colour gradient to use as a background image.</summary>
    static Texture2D Grad(string top, string bottom) {
        if (grads.TryGetValue(top + bottom, out var t) && t != null) return t;
        t = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) t.SetPixel(0, y, Color.Lerp(C(bottom), C(top), y / 63f));
        t.Apply(); grads[top + bottom] = t; return t;
    }
    static Color C(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    static string N(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    string Sig(string s) => Hash128.Compute(s).ToString();
    VisualElement Scroll(string cls = null) { var s = new ScrollView(ScrollViewMode.Vertical); s.style.flexGrow = 1; if (cls != null) s.contentContainer.AddToClassList(cls); return s; }

    // ---------------------------------------------------------------- start-up
    void Start() {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-slshots") shotsDir = args[i + 1]; else if (args[i] == "-sltown") { shotsDir = args[i + 1]; townTour = true; }
        string save = townTour ? "starlife-town-shots.json" : shotsDir != null ? "starlife-shots.json" : "starlife-save.json";
        if (shotsDir != null) { Directory.CreateDirectory(shotsDir); File.Delete(Path.Combine(Application.persistentDataPath, save)); }
        Application.targetFrameRate = 60;

        core = new SLCore(save);
        core.Changed = _ => dirty = true; core.ProfileChanged = () => dirty = true; core.Toast = ShowToast;
        data = core.Get<DData>("data");
        foreach (var it in data.items) itemById[it.id] = it;
        foreach (var f in data.furn) furnByK[f.k] = f;
        string myName = core.Pref("name.custom"); if (myName != "") { core.Call("rename", myName); foreach (var x in data.starlets) if (x.id == "custom") x.name = myName; }

        var doc = GetComponent<UIDocument>();
        panel = Instantiate(doc.panelSettings); panel.scaleMode = PanelScaleMode.ConstantPixelSize; doc.panelSettings = panel;
        root = doc.rootVisualElement;
        root.styleSheets.Add(Resources.Load<StyleSheet>("starlife"));
        root.AddToClassList("root"); root.style.flexGrow = 1;
        BuildShell();
        Fit();
        Reload();
        SetView("home");
        if (shotsDir != null || PlayerPrefs.GetInt("sl3d", 1) == 1) Set3D(true, false);
        if (!profile.hasSave || !profile.guideSeen) OpenPicker();
        if (shotsDir != null) StartCoroutine(townTour ? TownShots() : Shots());
    }

    // One layout for wide screens (desktop, web, landscape tablets) and a narrow one for upright phones.
    // The panel is scaled so the wide layout is at least 1280 x 900 points and the narrow one 430 points across.
    PanelSettings panel; int fitW, fitH; Rect fitSafe; bool narrowMode;
    void Fit() {
        int w = Screen.width, h = Screen.height; var sa = Screen.safeArea;
        if (w == fitW && h == fitH && sa == fitSafe) return;
        fitW = w; fitH = h; fitSafe = sa;
        bool narrow = w < h;
        float scale = narrow ? w / 430f : Application.isMobilePlatform ? Mathf.Min(h / 640f, w / 1180f) : Mathf.Min(h / 900f, w / 1280f);
        panel.scale = Mathf.Max(scale, 0.1f);
        root.EnableInClassList("narrow", narrow); narrowMode = narrow;
        root.style.paddingTop = (h - sa.yMax) / scale; root.style.paddingBottom = sa.yMin / scale;
        root.style.paddingLeft = sa.xMin / scale; root.style.paddingRight = (w - sa.xMax) / scale;
        dirty = true;
    }

    void Update() {
        Fit();
        SLArt.Pump();
        if (dirty) { dirty = false; Reload(); if (!inTown) RefreshView(); }
        if (inTown) { TickTown(); return; }
        Tick3D(); TickSalon(); TickGym(); TickMakeup();
    }
    void OnApplicationPause(bool p) { if (p) { if (inTown) SaveLife(); core?.Flush(); } }
    void OnApplicationQuit() { if (inTown) SaveLife(); core?.Flush(); }
    void LateUpdate() { if (Time.frameCount % 120 == 0) core?.Flush(); }

    void Reload() {
        profile = core.Get<DProfile>("profile"); state = core.Get<DState>("state");
        dollKey = Sig(core.Call("dollSig"));
        look3 = On3 ? MyLook(null) : null;
        if (On3) { var mine = look3; meFace.Set3D("3d:me:" + mine.Key, (w, h) => stage3d.Shot(mine, "portrait", w, h)); }
        else meFace.Set("me:" + dollKey + state.starlet, () => core.Call("mePortrait"));
        meName.text = profile.name; meLevel.text = profile.level.ToString();
        xpFill.style.width = Length.Percent(100f * profile.xp / data.famePerLevel);
        xpText.text = profile.xp + " / " + data.famePerLevel + " fame to level " + (profile.level + 1);
        coinVal.text = profile.coins.ToString(); fameVal.text = profile.fame.ToString();
    }

    void BuildShell() {
        VisualElement Pill(string icon, out Label val) {
            var dot = new SvgImage().Set("icon:" + icon, () => core.Call("icon", icon)); dot.style.width = dot.style.height = 30; dot.style.marginRight = 8;
            val = L("0"); return El("pill", dot, val);
        }
        root.style.backgroundImage = Grad("#a9cdf2", "#e6f2fc");
        meFace = new SvgImage();
        var me = El("me", El("me-face", meFace), El("col", meName = L("", "me-name"), El("lvl", meLevel = L("1", "lvl-badge"), El("xp", xpFill = El("xp-fill")), xpText = L("", "xp-text"))));
        me.RegisterCallback<ClickEvent>(_ => OpenPicker());
        var star = Icon("star", "#ffd84a", 26); star.style.marginRight = 8;
        root.Add(El("topbar", star, L("Starlife", "wordmark"), me, El("grow"), Pill("coin", out coinVal), Pill("fame", out fameVal),
            B("?", "help", () => { SetView("suite"); OpenGuide(0); })));

        var nav = El("mainnav"); var page = El("page", nav);
        foreach (var (id, label) in new[] { ("home", "HOME"), ("suite", "MY SUITE"), ("parlor", "BEAUTY PARLOR"), ("salon", "HAIR SALON"), ("gym", "GYM"), ("career", "CAREER"), ("events", "EVENTS"), ("shop", "SHOP"), ("contests", "CONTESTS"), ("album", "MY ALBUM") }) {
            string v = id; navBtns[id] = B(label, null, () => SetView(v)); nav.Add(navBtns[id]);
            var pane = El("view"); views[id] = pane; page.Add(pane);
        }
        var goOut = B("GO OUT ✦", "goout", GoOut); nav.Add(goOut);      // Starlife Town (SLTown.cs)
        root.Add(page);
        BuildHome(views["home"]); BuildSuite(views["suite"]); BuildParlor(views["parlor"]); BuildSalon(views["salon"]); BuildGym(views["gym"]); BuildCareer(views["career"]); BuildEvents(views["events"]); BuildShop(views["shop"]); BuildContests(views["contests"]); BuildAlbum(views["album"]);
        overlayHost = El(null); overlayHost.style.position = Position.Absolute; overlayHost.style.left = overlayHost.style.top = overlayHost.style.right = overlayHost.style.bottom = 0;
        overlayHost.pickingMode = PickingMode.Ignore; root.Add(overlayHost);
        BuildSaveModal(); BuildPicker();
        toast = L("", "toast"); toast.pickingMode = PickingMode.Ignore; overlayHost.Add(toast);
    }

    public void SetView(string v) {
        view = v;
        foreach (var kv in views) kv.Value.EnableInClassList("on", kv.Key == v);
        foreach (var kv in navBtns) kv.Value.EnableInClassList("on", kv.Key == v);
        RefreshView();
    }
    void RefreshView() {
        switch (view) {
            case "home": RefreshHome(); break;
            case "suite": RefreshSuite(); break;
            case "parlor": RefreshParlor(); break;
            case "salon": RefreshSalon(); break;
            case "gym": RefreshGym(); break;
            case "career": RefreshCareer(); break;
            case "events": RefreshEvents(); break;
            case "shop": RefreshShop(); break;
            case "contests": RefreshContests(); break;
            case "album": RefreshAlbum(); break;
        }
    }

    void ShowToast(string msg) {
        if (toast == null) return;
        toast.text = msg.Replace("✦", "").Trim(); toast.AddToClassList("on");
        toastTimer?.Pause(); toastTimer = toast.schedule.Execute(() => toast.RemoveFromClassList("on")).StartingIn(1900);
    }

    // ---------------------------------------------------------------- home
    VisualElement homeBody;
    void BuildHome(VisualElement pane) { pane.Add(homeBody = Scroll()); }
    void RefreshHome() {
        homeBody.Clear();
        var hero = new SvgImage();
        if (On3) { var six = Array.ConvertAll(Array.FindAll(SLStage3D.Lineup6, stage3d.Has), SigLook); hero.Set3D("3d:hero:" + string.Join(",", Array.ConvertAll(six, l => l.Key)), (w, h) => stage3d.Lineup(six, w, h)); } else hero.Set("hero", () => core.Call("hero"));
        homeBody.Add(El("hero", hero));
        var head = El("row", El("col grow", L("Welcome back, " + profile.name.Split(' ')[0] + "!", "h2"), L("Dress up, decorate your suite and win contests to earn coins and fame.", "muted")),
            B("Go to my suite", "btn primary big", () => SetView("suite")));
        head.style.marginTop = 14; homeBody.Add(head);

        var th = Array.Find(data.themes, t => t.id == profile.featured);
        var themeBox = L(th.name, "tile-theme"); themeBox.style.backgroundColor = C(th.bg[0]);
        var bonusBtn = B(profile.bonus ? "Claim 50 coins" : "Claimed", "btn primary small", () => core.Call("bonus")); bonusBtn.SetEnabled(profile.bonus);
        var cheapest = Array.FindAll(data.items, i => i.price > 0 && Array.IndexOf(profile.owned, i.id) < 0);
        Array.Sort(cheapest, (a, b) => a.price.CompareTo(b.price));
        var tiles = El("tiles",
            El("tile", L("DAILY BONUS", "tile-h"), L(profile.coins + " coins", "tile-big"), L("Come back every day for 50 coins and 5 fame.", "muted"), bonusBtn),
            El("tile", L("TODAY'S CONTEST", "tile-h"), themeBox, L(th.desc, "muted"), B("Enter the contest", "btn small", () => { themeSel = th.id; SetView("contests"); })),
            El("tile", L("NEW IN THE SHOP", "tile-h"), L(cheapest.Length + " pieces to collect", "tile-big"), L(cheapest.Length > 0 ? "From " + cheapest[0].price + " coins: " + cheapest[0].name + "." : "You own the whole collection!", "muted"), B("Visit the shop", "btn small", () => SetView("shop"))));
        homeBody.Add(tiles);

        homeBody.Add(L("THE STARLETS", "sec"));
        var people = El("people");
        foreach (var x in data.starlets) {
            string id = x.id;
            var p = El("person" + (state.starlet == x.id ? " cur" : ""), El("person-face", Face(x)), L(x.name, "person-name"), L(x.role, "muted"), L(x.fame + " fame", "muted"));
            p.RegisterCallback<ClickEvent>(_ => OpenPicker(id)); people.Add(p);
        }
        homeBody.Add(people);
    }

    // ---------------------------------------------------------------- starlet picker
    VisualElement picker, carTrack; SvgImage pickerRoom; Label pickerNote; string pickSel = "aria";
    void BuildPicker() {
        pickerRoom = new SvgImage();
        var roomBox = El("picker-room", pickerRoom);
        roomBox.RegisterCallback<GeometryChangedEvent>(e => { float k = Mathf.Min(e.newRect.width / 1000f, e.newRect.height / 620f); pickerRoom.style.width = 1000 * k; pickerRoom.style.height = 620 * k; });
        picker = El("picker", L("Starlife", "picker-logo"), roomBox, L("CHOOSE YOUR STARLET", "picker-title"), carTrack = El("car"), pickerNote = L("", "picker-note"),
            B("CONTINUE", "continue", () => ClosePicker(true)));
        overlayHost.Add(picker);
    }
    /// <summary>A starlet's round picture: her 3D avatar's face, or the drawn portrait.</summary>
    SvgImage Face(DStarlet x) {
        var i = new SvgImage(); string id = x.id;
        if (On3) { var look = SigLook(id); i.Set3D("3d:face:" + id + look.Key, (w, h) => stage3d.Shot(look, "portrait", w, h)); } else i.Set("face:" + id, () => core.Call("portrait", x.face, x.lips));
        return i;
    }
    void OpenPicker() => OpenPicker(string.IsNullOrEmpty(state.starlet) ? "aria" : state.starlet);
    void OpenPicker(string sel) {
        pickSel = sel; carTrack.Clear();
        foreach (var x in data.starlets) {
            string id = x.id;
            var it = El("car-item", El("car-face", Face(x)), L(x.name.Split(' ')[0]));
            it.name = "pick-" + id; it.RegisterCallback<ClickEvent>(_ => { pickSel = id; RenderPicker(); }); carTrack.Add(it);
        }
        picker.AddToClassList("on"); RenderPicker();
    }
    void RenderPicker() {
        string id = pickSel;
        if (On3) { var st = JsonUtility.FromJson<DState>(core.Call("starletJson", id)); var look = SigLook(id); pickerRoom.Set3D("3d:pick:" + id + look.Key, (w, h) => stage3d.RoomShot("pick" + id, w, h, st.room, furnByK, FurnSvg, look)); }
        else pickerRoom.Set("pick:" + id, () => core.Call("roomFull", core.Call("starletJson", id)));
        foreach (var c in carTrack.Children()) c.EnableInClassList("on", c.name == "pick-" + id);
        var x = Array.Find(data.starlets, s => s.id == id);
        pickerNote.text = "<b>" + x.name + "</b>, " + x.role.ToLowerInvariant() + ". You can restyle her any time, and there are <b>millions</b> of ways to dress up!";
    }
    void ClosePicker(bool go) {
        if (go) core.Call("pickStarlet", pickSel);
        picker.RemoveFromClassList("on");
        Reload(); SetView("suite");
        if (go && !profile.guideSeen) OpenGuide(0);
    }

    // ---------------------------------------------------------------- screenshot tour (-slshots <dir>), on its own save file
    IEnumerator Shots() {
        int n = 0;
        IEnumerator Snap(string name) {
            for (int i = 0; i < 6; i++) yield return null;
            float t = Time.realtimeSinceStartup;
            while ((SLArt.Busy > 0 || dirty) && Time.realtimeSinceStartup - t < 30) yield return null;
            for (int i = 0; i < 4; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, (++n).ToString("00") + "-" + name + ".png"));
            yield return null; yield return null;
        }
        yield return Snap("picker");
        pickSel = "zuri"; RenderPicker(); yield return Snap("picker-zuri");
        pickSel = "aria"; ClosePicker(true); yield return Snap("suite-guide");
        CloseGuide(); OpenDrawer("wardrobe"); yield return Snap("wardrobe");
        foreach (var id in new[] { "blouse", "jeans", "blazer", "sneakers", "tote", "hoops" }) core.Call("equip", id);
        yield return Snap("dressed");
        Set3D(false, false); yield return Snap("dressed-drawn"); Set3D(true, false);
        void Dump(Look3 look, string shot, int w, int h, string file, float yaw = 0) { var t2 = SLStage3D.Read(stage3d.Shot(look, shot, w, h, yaw)); File.WriteAllBytes(Path.Combine(shotsDir, file + ".png"), t2.EncodeToPNG()); Destroy(t2); }
        foreach (var e in stage3d.Starlets) if (e.id != "basics") Dump(new Look3 { id = e.id }, "full", 400, 820, "avatar-" + e.id);      // every avatar as she came
        Dump(new Look3 { id = "zuri", signature = false, underwear = C("#f4c7d3") }, "full", 400, 820, "life-0-underwear-halter-briefs");
        Dump(new Look3 { id = "lucy", signature = false, underwear = C("#1f1b22"), bra = "bandeau", pants = "shorts" }, "full", 400, 820, "life-1-underwear-bandeau-shorts");
        Dump(new Look3 { id = "charlotte", signature = false, underwear = C("#a9c6ef"), bra = "tank" }, "full", 400, 820, "life-2-underwear-tank");
        Dump(new Look3 { id = "lina", body = "jenny", height = 1.06f, hair = "h:boxbraids" }, "full", 400, 820, "life-3-custom");
        float[][] builds = { new float[] { 0, 0, 0, 0, 0, 0, 0 }, new float[] { -0.7f, 0, -0.2f, -0.4f, -0.3f, 0.2f, 0 }, new float[] { 1, 0, 0.3f, 0.8f, 0.6f, -0.1f, 0 }, new float[] { 0.2f, 1, 1, 0.2f, 0.2f, 0, -0.2f },
            new float[] { 0.35f, 0, 0, 0.9f, 0.8f, 0, 0 }, new float[] { -0.5f, 0.1f, 0.1f, -0.2f, -0.1f, 0.8f, -0.3f }, new float[] { -0.15f, 0.1f, 0.1f, 1.4f, 1.4f, 0.1f, 0, 1.4f } };
        for (int k = 0; k < builds.Length; k++) foreach (var (tag, wear) in new[] { ("under", ""), ("dressed", "tee,jeans,sneakers"), ("dress", "lbd,pumps") }) foreach (int yaw in new[] { 0, 90 }) {
            var look = new Look3 { id = "lucy", signature = false, underwear = C("#f4c7d3") }; Array.Copy(builds[k], look.shape, builds[k].Length);
            if (wear != "") foreach (var id in wear.Split(',')) look.worn.Add((itemById[id].slot, id, C(itemById[id].color)));
            Dump(look, "full", 400, 820, "build-" + k + "-" + tag + "-" + yaw.ToString("00"), yaw);
        }
        foreach (var e in stage3d.Hair) Dump(new Look3 { id = "zuri", hair = "h:" + e.id }, "face", 360, 460, "hair-" + e.id);
        foreach (var e in stage3d.Hair) Dump(new Look3 { id = "lucy", hair = "h:" + e.id }, "full", 400, 820, "hairfull-" + e.id, 150);   // every added hairstyle
        string[] outfits = { "lucy:sportsbra,leggings,trainers", "mary:hoodie,joggers,hightops", "ivy:gymtank,gymshorts,runners", "jenny:tracktop,tennisskirt,trainers", "noor:scrubtop,scrubpants,workboots", "mina:chefcoat,workpants,workboots", "zuri:polo,workpants,sneakers",
            "zuri:tee,jeans,sneakers", "lucy:blouse,pleated,pumps", "mary:turtle,wide,ankleboots", "charlotte:cami,shorts,sandals", "noor:gown", "ivy:slip,sandals",
            "aria:blazer,pencil,kneeboots", "mina:puffer,mini,flats", "luz:wrap,pumps,sunnies", "olivia:corset,jeans,flats", "songbird:breton,shorts,sneakers", "jenny:trench,wide,pumps",
            "lina:sundress,sneakers", "rosie:lbd,pumps", "cute:denimjk,mini,ankleboots", "pigtail:cardigan,pencil,flats", "eve:biker,jeans,kneeboots", "zuri:sequin,sandals,roundspecs" };
        string[] spin = { "mary:gothgown,platforms,spikecollar,horns", "cute:harness,kilt,creepers,choker,catears,coffinbag", "lina:meshtop,jeans,gothcoat,crosschain,crossear", "rosie:cami,mini", "zuri:tee,jeans,sneakers", "lucy:blouse,pleated,pumps", "charlotte:cami,shorts,sandals", "ivy:slip,sandals", "lucy:sportsbra,leggings,trainers", "aria:blazer,pencil,kneeboots", "lina:sundress,sneakers", "mary:turtle,wide,ankleboots", "zuri:" };
        for (int k = 0; k < spin.Length; k++) {         // the same looks from the side and the back: clothes must hold up all the way round
            var bits = spin[k].Split(':'); var look = new Look3 { id = bits[0], underwear = C("#f4c7d3"), signature = false };
            if (bits[1] != "") foreach (var id in bits[1].Split(',')) look.worn.Add((itemById[id].slot, id, C(itemById[id].color)));
            foreach (int yaw in new[] { 0, 70, 180, 250 }) Dump(look, "full", 400, 820, "spin-" + k.ToString("00") + "-" + yaw.ToString("000"), yaw);
        }
        for (int k = 0; k < outfits.Length; k++) {      // wardrobe pieces on different starlets
            var bits = outfits[k].Split(':'); var look = new Look3 { id = bits[0], underwear = C("#f4c7d3") };
            foreach (var id in bits[1].Split(',')) look.worn.Add((itemById[id].slot, id, C(itemById[id].color)));
            Dump(look, "full", 400, 820, "worn-" + k.ToString("00") + "-" + bits[1].Replace(',', '+'));
        }
        wardrobeCat = "acc"; RefreshSuite(); yield return Snap("wardrobe-acc");
        OpenDrawer("decor"); foreach (var k in new[] { "sofa", "armchair", "coffeetable", "boxes", "plant", "floorlamp" }) core.Call("addFurn", k); yield return Snap("decor");
        OpenDrawer(null); ToggleLayers(); yield return Snap("layers"); ToggleLayers();
        SetView("parlor"); yield return Snap("parlor-avatar");
        parlorTab = "hair"; RefreshParlorPanel(); yield return Snap("parlor-hair");
        core.Call("set", "hair", "waves"); core.Call("set", "hairColor", "#8f3b1b");
        parlorTab = "makeup"; RefreshParlorPanel(); core.Call("preset", "Night Out"); yield return Snap("parlor-makeup");
        for (int i = 0; i < 4; i++) yield return null;
        MkDemo(); yield return Snap("makeup-painted");
        zoom = "full"; RefreshParlor(); yield return Snap("parlor-full");
        SetView("salon"); yield return Snap("salon");
        SetHair3D(Av, "h:boxbraids"); yield return Snap("salon-boxbraids");
        salonYaw = 180; SetHair3D(Av, "h:fulani"); yield return Snap("salon-fulani-back"); salonYaw = 0;
        StartSim("h:twists", "Twists"); yield return null; yield return null;
        for (int i = 0; i < 110; i++) SimTouch(new Vector2(170 + (i * 53) % 230, 110 + (i * 37) % 150));
        yield return Snap("salon-sim-shampoo");
        for (int i = 0; i < 4000 && simStyle != null; i++) SimTouch(new Vector2(170 + (i * 53) % 230, 110 + (i * 37) % 150));
        yield return Snap("salon-sim-done");
        SetHair3D(Av, "h:highpuff"); SetView("suite"); OpenDrawer(null); yield return Snap("suite-highpuff"); SetHair3D(Av, "");
        // events: an ordinary day, then Halloween (a holiday, open to all), with one invitation accepted
        SetView("events"); yield return Snap("events");
        evDate = new DateTime(2026, 10, 29); { var inv = Invitations(EvToday).Find(x => x.day >= EvToday); if (inv != null) core.SetPref("inv." + inv.Key, "accepted"); }
        dirty = true; yield return Snap("events-halloween");
        { var h = Holidays(2026).Find(x => x.id == "halloween"); Attend(h, "evdone." + h.Key + "." + EvToday.ToString("yyyyMMdd")); } yield return Snap("events-attended");
        evDate = null;
        // life: underwear, the gym, a career
        SetView("suite"); OpenDrawer("wardrobe"); wardrobeCat = "basics"; core.Call("undress"); SetP("sig", "0"); yield return Snap("underwear");
        SetP("bra", "bandeau"); SetP("pants", "shorts"); yield return Snap("underwear-bandeau");
        core.Call("grant", Json(new[] { "sportsbra", "leggings", "trainers" })); foreach (var id in new[] { "sportsbra", "leggings", "trainers" }) core.Call("equip", id);
        OpenDrawer(null); SetView("gym"); yield return Snap("gym");
        Workout("Weights", 5); yield return Snap("gym-workout");
        SetView("career"); yield return Snap("career");
        TakeJob(Array.Find(Jobs, j => j.id == "nurse")); foreach (var id in new[] { "scrubtop", "scrubpants" }) core.Call("equip", id); yield return Snap("career-nurse");
        Work(); yield return Snap("career-worked");
        SetView("suite"); OpenDrawer("wardrobe"); wardrobeCat = "outer"; RefreshSuite(); yield return Snap("wardrobe-on-a-nurse-wage");
        TakeJob(Array.Find(Jobs, j => j.id == "popstar")); OpenDrawer(null); core.Call("undress"); SetP("sig", "1");
        foreach (var id in new[] { "blouse", "jeans", "blazer", "sneakers", "tote", "hoops" }) core.Call("equip", id);
        SetView("shop"); yield return Snap("shop");
        tryOn.Add("gown"); tryOn.Add("tiara"); RefreshShop(); yield return Snap("shop-tryon");
        core.Call("bonus"); core.Call("buy", "lbd"); tryOn.Clear(); yield return Snap("shop-bought");
        SetView("contests"); yield return Snap("contest");
        EnterContest(); yield return Snap("contest-result");
        SetView("home"); yield return Snap("home");
        SetView("suite"); OpenSave(); yield return Snap("save");
        ConfirmSave("Tour look"); SetView("album"); yield return Snap("album");
        core.Flush();
        Application.Quit();
    }
}
