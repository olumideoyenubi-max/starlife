// Starlife Town's shops, cafés, bars and restaurants that you walk into. Going through a door builds that place's room
// out at SLTownWorld.RoomX (far beyond the town, so only the room is in view) with its fittings, its customers and the
// people who work there: talk to a cashier, barista, host or sales assistant to order, browse or buy; the cooks, waiters
// and stock staff go about their work. Restaurant Row (the block between the theatre and the Grand Hall) is new here:
// eight eateries with their own storefronts, from coffee and burgers to a tasting menu. Positions are in the town's
// coordinates (x, z, ry), like the rest of SLTown; each room is laid out in its own frame (x across, z from the door
// in, y up) and turned into town coordinates through its root. Part of SLApp.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static SLJson;

public partial class SLApp {
    // ================================================================ Restaurant Row
    sealed class Eatery {
        public string id, name, sub, kind, blurb; public Color main, accent; public int[] open; public float x, z, ry; public bool toGo;
        public (string name, int cost, string fx, int min)[] menu;
    }
    // block (1,-1): its west front faces the theatre, its north front Étoile Motors' street, east and south the quieter roads
    static readonly Eatery[] Eateries = {
        new Eatery { id = "greenwave", name = "Greenwave Coffee", sub = "Coffee house · lattes, frappés, pastries", kind = "coffee", open = new[] { 6, 22 }, x = 31.6f, z = -55, ry = -Mathf.PI / 2, toGo = true,
            main = C("#1e6b4a"), accent = C("#f4efe6"), blurb = "Green aprons, a hiss of steamed milk and a queue that moves fast.",
            menu = new[] { ("Caramel Cloud Latte", 6, "energy:25,fun:8", 20), ("Iced Matcha", 6, "energy:20,fun:6", 20), ("Vanilla Bean Frappé", 7, "fun:16,energy:12", 20), ("Butter Croissant", 4, "hunger:18", 15), ("Egg & Cheese Wrap", 7, "hunger:32", 20) } },
        new Eatery { id = "bigbun", name = "Big Bun Burgers", sub = "Burgers, fries and shakes · open late", kind = "burger", open = new[] { 7, 2 }, x = 31.6f, z = -41, ry = -Mathf.PI / 2, toGo = true,
            main = C("#c8102e"), accent = C("#ffc72c"), blurb = "Bright menus, the beep of the fryers and fries that are always hot.",
            menu = new[] { ("Big Bun Double", 9, "hunger:55,fun:8", 25), ("Crispy Chicken Burger", 8, "hunger:48", 25), ("Golden Fries", 4, "hunger:20,fun:6", 15), ("Nuggets ×9", 7, "hunger:35,fun:5", 20), ("Strawberry Shake", 5, "fun:14,hunger:10", 15) } },
        new Eatery { id = "sunnyside", name = "Sunny Side", sub = "Brunch · pancakes, eggs, fresh juice", kind = "diner", open = new[] { 7, 16 }, x = 37, z = -31.6f, ry = 0,
            main = C("#f2b134"), accent = C("#3a2a1a"), blurb = "Stacks of pancakes, bottomless coffee and sunshine through the big windows.",
            menu = new[] { ("Buttermilk Pancakes", 11, "hunger:45,fun:10", 40), ("Avocado Toast & Eggs", 12, "hunger:42", 40), ("Full Brunch Plate", 15, "hunger:60,energy:10", 50), ("Fresh Orange Juice", 4, "energy:12", 10) } },
        new Eatery { id = "jollof", name = "Jollof & Co.", sub = "West African kitchen · jollof, suya, plantain", kind = "african", open = new[] { 11, 23 }, x = 59, z = -31.6f, ry = 0,
            main = C("#d9541e"), accent = C("#ffe9a8"), blurb = "Smoky party jollof, suya off the grill and Afrobeats on the speakers.",
            menu = new[] { ("Party Jollof & Chicken", 14, "hunger:55,fun:10", 45), ("Beef Suya Platter", 13, "hunger:45,fun:12", 40), ("Egusi & Pounded Yam", 15, "hunger:60", 50), ("Dodo & Puff-Puff", 7, "hunger:25,fun:10", 25), ("Chapman", 5, "fun:12,energy:6", 15) } },
        new Eatery { id = "bella", name = "Trattoria Bella", sub = "Italian · handmade pasta, wood-fired pizza", kind = "italian", open = new[] { 12, 23 }, x = 64.4f, z = -41, ry = Mathf.PI / 2,
            main = C("#2e6b3a"), accent = C("#fbf3e4"), blurb = "Checked tablecloths, a glowing pizza oven and a nonna-approved carbonara.",
            menu = new[] { ("Margherita from the Oven", 14, "hunger:50,fun:10", 45), ("Truffle Tagliatelle", 22, "hunger:50,fun:15", 50), ("Lasagne della Casa", 18, "hunger:58", 50), ("Tiramisu", 8, "fun:15,hunger:12", 20) } },
        new Eatery { id = "sakura", name = "Sakura Omakase", sub = "Sushi counter · the chef’s tasting", kind = "sushi", open = new[] { 17, 24 }, x = 64.4f, z = -55, ry = Mathf.PI / 2,
            main = C("#1b1b1f"), accent = C("#f2c6cf"), blurb = "Ten seats at a pale wood counter, and a chef who decides what you eat.",
            menu = new[] { ("Omakase · 12 pieces", 85, "hunger:55,fun:30,social:10", 90), ("Salmon & Tuna Nigiri", 28, "hunger:35,fun:12", 40), ("Matcha & Mochi", 9, "fun:12,energy:6", 20) } },
        new Eatery { id = "gilded", name = "The Gilded Steakhouse", sub = "Steaks, seafood and a piano bar", kind = "steak", open = new[] { 17, 24 }, x = 40, z = -64.4f, ry = Mathf.PI,
            main = C("#3b1f14"), accent = C("#e2c47a"), blurb = "Leather booths, brass lamps and a pianist playing standards.",
            menu = new[] { ("Ribeye & Truffle Fries", 48, "hunger:65,fun:15", 75), ("Lobster Thermidor", 62, "hunger:55,fun:25", 80), ("Chocolate Fondant", 12, "fun:18,hunger:10", 20) } },
        new Eatery { id = "lumiere", name = "Maison Lumière", sub = "Fine French dining · tasting menu", kind = "fine", open = new[] { 18, 24 }, x = 56, z = -64.4f, ry = Mathf.PI,
            main = C("#f4f0e8"), accent = C("#8a6d3b"), blurb = "Crystal chandeliers, white linen and seven courses, each a little work of art.",
            menu = new[] { ("Seven-Course Tasting Menu", 120, "hunger:70,fun:35,social:15", 150), ("Sparkling Rosé Mocktail & Canapés", 30, "fun:20,social:10,hunger:15", 40), ("Soufflé au Chocolat", 16, "fun:18,hunger:10", 25) } },
    };
    Eatery EateryBy(string id) => Eateries.FirstOrDefault(e => e.id == id);
    Transform rowRoot;

    void SetUpEateries() {
        foreach (var e in Eateries) {
            if (PlaceBy(e.id) == null) town.places.Add(new SLTownWorld.Place { id = e.id, name = e.name, sub = e.sub, color = "#" + ColorUtility.ToHtmlStringRGB(e.main), x = e.x, z = e.z, ry = e.ry, open = e.open, node = town.NearestNode(e.x, e.z), hangout = true });
        }
        if (rowRoot != null) Destroy(rowRoot.gameObject);
        rowRoot = new GameObject("Restaurant Row").transform; rowRoot.SetParent(town.root, false);
        foreach (var e in Eateries) StoreFront(e);
        SLTownWorld.SetLayer(rowRoot);
    }

    // ---------------------------------------------------------------- materials and simple shapes
    readonly Dictionary<string, Material> roomMats = new Dictionary<string, Material>();
    Material Mat(Color c, float gloss = 0.3f, float metal = 0, Color? glow = null, Texture2D tex = null, Vector2? tile = null, Texture2D nrm = null, float nightGlow = 0) {
        string key = ColorUtility.ToHtmlStringRGBA(c) + gloss + metal + glow + (tex != null ? tex.name : "") + tile + nightGlow;
        if (roomMats.TryGetValue(key, out var m)) return m;
        m = new Material(Resources.Load<Shader>("SLTown")); m.SetColor("_Color", c); m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", metal);
        if (glow.HasValue) { m.SetColor("_EmissionColor", glow.Value); m.SetFloat("_EmA", 1); m.SetFloat("_EmB", nightGlow); }
        if (tex != null) { m.SetTexture("_MainTex", tex); m.mainTextureScale = tile ?? Vector2.one; }
        if (nrm != null) { m.EnableKeyword("_NORMALMAP"); m.SetTexture("_BumpMap", nrm); m.SetTextureScale("_BumpMap", tile ?? Vector2.one); }
        roomMats[key] = m; return m;
    }
    Material glassM;
    Material Glass() { if (glassM == null) { glassM = new Material(Resources.Load<Shader>("SLTownClear")); glassM.SetColor("_Color", new Color(0.75f, 0.85f, 0.9f, 0.22f)); glassM.SetFloat("_Glossiness", 0.97f); } return glassM; }
    static Transform Part(Transform parent, PrimitiveType t, Vector3 at, Vector3 size, Material m, float yaw = 0, float pitch = 0) {
        var g = GameObject.CreatePrimitive(t); UnityEngine.Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = at; g.transform.localScale = size; g.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
        g.GetComponent<Renderer>().sharedMaterial = m; return g.transform;
    }
    static Transform Box(Transform p, Vector3 at, Vector3 size, Material m, float yaw = 0) => Part(p, PrimitiveType.Cube, at, size, m, yaw);
    static Transform Cyl(Transform p, Vector3 at, float r, float h, Material m) => Part(p, PrimitiveType.Cylinder, at, new Vector3(r * 2, h / 2, r * 2), m);
    Font signFont, textFont;
    /// <summary>Text standing in the parent's frame, readable from its -z side (flip: from its +z side).</summary>
    TextMesh Words(Transform p, string text, Vector3 at, float size, Color c, bool flip = false, bool bold = true, TextAnchor anchor = TextAnchor.MiddleCenter) {
        if (signFont == null) { signFont = Resources.Load<Font>("Fonts/DMSans-Bold"); textFont = Resources.Load<Font>("Fonts/DMSans-Regular"); }
        var t = new GameObject("text").AddComponent<TextMesh>(); t.transform.SetParent(p, false); t.transform.localPosition = at; t.transform.localRotation = Quaternion.Euler(0, flip ? 180 : 0, 0);
        t.font = bold ? signFont : textFont; t.text = text; t.fontSize = 96; t.characterSize = size; t.anchor = anchor; t.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center; t.color = c;
        t.GetComponent<MeshRenderer>().sharedMaterial = t.font.material; return t;
    }
    // procedural textures: tiles, checks, stripes and book spines
    readonly Dictionary<string, Texture2D> roomTex = new Dictionary<string, Texture2D>();
    Texture2D Pattern(string name, int size, Func<int, int, Color> f) {
        if (roomTex.TryGetValue(name, out var t)) return t;
        t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        var px = new Color[size * size]; for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = f(x, y);
        t.SetPixels(px); t.Apply(true); roomTex[name] = t; return t;
    }
    Texture2D Checker(string name, Color a, Color b, int cells = 8, float grout = 0) => Pattern(name, 256, (x, y) => {
        int c = 256 / cells; if (grout > 0 && (x % c < grout || y % c < grout)) return Color.Lerp(a, b, 0.5f) * 0.8f;
        return ((x / c + y / c) % 2 == 0 ? a : b) * (0.96f + 0.04f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f)); });
    Texture2D Spines() => Pattern("spines", 256, (x, y) => {
        int shelf = y / 64, yy = y % 64; if (yy < 6) return new Color(0.32f, 0.2f, 0.12f);      // the shelf board
        var r = new System.Random(shelf * 977 + x / 9); float h = 0.62f + (float)r.NextDouble() * 0.36f; if (yy - 6 > h * 58) return new Color(0.12f, 0.08f, 0.06f);
        float hue = (float)r.NextDouble(); var c = Color.HSVToRGB(hue, 0.35f + (float)r.NextDouble() * 0.4f, 0.35f + (float)r.NextDouble() * 0.45f);
        if (x % 9 == 0) c *= 0.6f; if (Mathf.Abs(yy - 6 - h * 40) < 2) c = Color.Lerp(c, new Color(0.9f, 0.8f, 0.5f), 0.6f); return c; });
    Texture2D Kente() => Pattern("kente", 256, (x, y) => {
        Color[] band = { C("#e8a317"), C("#1f7a3a"), C("#b8261e"), C("#111111"), C("#e8a317"), C("#2c4f9e") };
        int bx = x / 32, by = y / 32; bool weft = ((x / 4) + (y / 4)) % 2 == 0; var c = band[(weft ? bx : by + 2) % band.Length];
        if ((x % 32 < 3) || (y % 32 < 3)) c = C("#111111"); return c * (0.9f + 0.1f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f)); });
    Texture2D RoomTex(string name) => Resources.Load<Texture2D>("Room/" + name);

    // ---------------------------------------------------------------- storefronts on Restaurant Row
    void StoreFront(Eatery e) {
        var t = new GameObject(e.name).transform; t.SetParent(rowRoot, false);
        float fx = e.x - Mathf.Sin(e.ry) * 1.4f, fz = e.z - Mathf.Cos(e.ry) * 1.4f;       // the building's face, behind the door point
        t.position = SLTownWorld.W(fx, town.HeightAt(e.x, e.z), fz); t.rotation = SLTownWorld.Yaw(e.ry);     // local +z: out to the street
        var dark = Mat(Color.Lerp(e.main, Color.black, 0.55f), 0.5f); var frame = Mat(new Color(0.08f, 0.08f, 0.09f), 0.6f, 0.4f);
        var glow = Mat(new Color(0.12f, 0.1f, 0.08f), 0.9f, 0, new Color(1f, 0.78f, 0.52f) * 0.55f, nightGlow: 0.9f);
        Box(t, new Vector3(0, 2.2f, 0.05f), new Vector3(8.4f, 4.4f, 0.14f), dark);
        foreach (float sx in new[] { -2.55f, 2.55f }) {                               // shop windows, lit from inside
            Box(t, new Vector3(sx, 1.75f, 0.13f), new Vector3(3.1f, 2.5f, 0.04f), glow);
            Box(t, new Vector3(sx, 1.75f, 0.16f), new Vector3(3.3f, 0.08f, 0.06f), frame); Box(t, new Vector3(sx, 0.48f, 0.18f), new Vector3(3.3f, 0.12f, 0.1f), frame);
            Box(t, new Vector3(sx, 3.02f, 0.18f), new Vector3(3.3f, 0.1f, 0.1f), frame);
            Words(t, e.kind == "burger" ? "OPEN LATE" : e.kind == "coffee" ? "COFFEE · PASTRIES" : HoursTxt(e.open[0], e.open[1]), new Vector3(sx, 2.6f, 0.17f), 0.022f, e.accent, true);
        }
        Box(t, new Vector3(0, 1.32f, 0.13f), new Vector3(1.5f, 2.64f, 0.04f), Mat(new Color(0.15f, 0.12f, 0.09f), 0.9f, 0, new Color(1f, 0.85f, 0.6f) * 0.8f, nightGlow: 1));     // the door
        Box(t, new Vector3(0, 2.68f, 0.17f), new Vector3(1.7f, 0.1f, 0.1f), frame); foreach (float sx in new[] { -0.8f, 0.8f }) Box(t, new Vector3(sx, 1.34f, 0.17f), new Vector3(0.1f, 2.7f, 0.1f), frame);
        Cyl(t, new Vector3(0.5f, 1.15f, 0.22f), 0.025f, 0.6f, Mat(new Color(0.8f, 0.65f, 0.35f), 0.85f, 1));       // the handle
        // the sign, and an awning in the house colours
        Box(t, new Vector3(0, 3.62f, 0.25f), new Vector3(7.8f, 0.95f, 0.22f), Mat(e.main, 0.45f, 0, e.main * 0.25f, nightGlow: 0.8f));
        Words(t, e.name.ToUpperInvariant(), new Vector3(0, 3.62f, 0.37f), e.name.Length > 16 ? 0.05f : 0.062f, e.accent, true);
        var aw = Part(t, PrimitiveType.Cube, new Vector3(0, 3.0f, 0.95f), new Vector3(7.6f, 0.05f, 1.5f), Mat(e.main, 0.25f), 0, 14);
        for (int i = 0; i < 7; i += 2) Part(t, PrimitiveType.Cube, new Vector3(-3.25f + i * 1.085f, 3.0f, 0.95f), new Vector3(1.085f, 0.055f, 1.5f), Mat(e.accent, 0.25f), 0, 14);
        Box(t, new Vector3(0, 2.8f, 1.68f), new Vector3(7.6f, 0.25f, 0.03f), Mat(e.main, 0.25f));
        if (e.kind == "coffee" || e.kind == "burger") {                               // a round logo by the door
            var disc = Part(t, PrimitiveType.Cylinder, new Vector3(-3.6f, 3.62f, 0.4f), new Vector3(0.82f, 0.03f, 0.82f), Mat(e.accent, 0.5f, 0, e.accent * 0.2f, nightGlow: 0.6f), 0, 90);
            Words(t, e.kind == "coffee" ? "≈" : "B", new Vector3(-3.6f, 3.62f, 0.44f), 0.09f, e.main, true);
        }
        var plant = Prefab("Furniture/plant");
        foreach (float sx in new[] { -3.75f, 3.75f }) {
            if (plant != null) { var pl = Instantiate(plant, t, false).transform; pl.localPosition = new Vector3(sx, 0, 0.55f); FitHeight(pl, 1.3f); }
            else Cyl(t, new Vector3(sx, 0.35f, 0.55f), 0.3f, 0.7f, Mat(new Color(0.25f, 0.25f, 0.27f)));
        }
    }
    GameObject Prefab(string path) => Resources.Load<GameObject>(path);
    /// <summary>A Poly Haven piece (Assets/Furniture, from fetch_furniture.py) in the room, its front to local +z; null if it is not in this build.</summary>
    Transform Model(string kind, Vector3 at, float yawDeg = 0, float scale = 1) {
        var pf = Prefab("Furniture/" + kind); if (pf == null) return null;
        var t = Instantiate(pf, room.root, false).transform; t.localPosition = at; t.localRotation = Quaternion.Euler(0, yawDeg, 0); t.localScale *= scale; return t;
    }
    /// <summary>The next of the town's people to work or eat here: each room shuffles them its own way, and nobody is in it twice.</summary>
    string NextBody() {
        if (room.crowd == null) {
            var r = new System.Random(room.p.id.GetHashCode()); room.crowd = stage3d.Starlets.Select(s => s.id).Where(id => id.StartsWith("p_")).OrderBy(id => r.Next()).ToList();
        }
        if (room.crowd.Count == 0) return null; string id = room.crowd[0]; room.crowd.RemoveAt(0); return id;
    }
    static void FitHeight(Transform t, float h) {
        var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return; var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.y > 0.01f) t.localScale *= h / b.size.y;
    }

    // ================================================================ inside
    sealed class Worker { public TP pp; public string role, kind; public Vector2 post; public float postRy, wait, talkT; public List<Vector2> beat; public int leg; public bool sells; }
    sealed class Room {
        public SLTownWorld.Place p; public string kind; public Transform root; public float w, d, h = 4.2f;
        public readonly List<float[]> cols = new List<float[]>(); public readonly List<SLTownWorld.Seat> seats = new List<SLTownWorld.Seat>();
        public readonly List<Worker> workers = new List<Worker>(); public readonly List<TP> guests = new List<TP>(); public Color amb = new Color(0.55f, 0.5f, 0.45f);
        public float camDist, camPitch; public int pixelLights;
        public string chair, table; public List<string> crowd;     // the furniture models it uses (null: built here), and who works and eats there
    }
    Room room;
    static readonly Dictionary<string, string> RoomKinds = new Dictionary<string, string> {
        { "boutique", "fashion" }, { "velours", "fashion" }, { "noir", "fashion" }, { "hartwell", "fashion" }, { "sneakers", "shoes" },
        { "glimmer", "jewel" }, { "aurelle", "jewel" }, { "celestine", "jewel" }, { "books", "books" },
        { "dore", "coffee" }, { "cafe", "coffee" }, { "patisserie", "coffee" }, { "gelato", "coffee" }, { "candy", "coffee" },
        { "salon", "salon" }, { "spa", "spa" }, { "bar", "bar" }, { "lounge", "bar" } };
    string RoomKind(SLTownWorld.Place p) { var e = EateryBy(p.id); if (e != null) return e.kind; return RoomKinds.TryGetValue(p.id, out var k) ? k : null; }
    bool HasRoom(SLTownWorld.Place p) => p != null && RoomKind(p) != null;
    /// <summary>Where you are on the town's map: inside a room, at its door.</summary>
    Vector2 MapPos() => room != null ? new Vector2(room.p.x, room.p.z) : new Vector2(player.pos.x, player.pos.z);
    // the room's own frame (x across, z in from the door) to the town's coordinates
    Vector2 RoomPt(float x, float z) { var w = room.root.TransformPoint(new Vector3(x, 0, z)); return new Vector2(w.x, -w.z); }
    float RoomRy(float a) { var d = room.root.TransformDirection(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a))); return Mathf.Atan2(d.x, -d.z); }
    /// <summary>Something solid in the room: a box people walk around (and, if tall, the camera stays in front of).</summary>
    Transform Solid(Vector3 at, Vector3 size, Material m, float yaw = 0, float colH = 1) {
        var t = Box(room.root, at, size, m, yaw); var b = t.GetComponent<Renderer>().bounds;
        room.cols.Add(new[] { b.min.x, -b.max.z, b.max.x, -b.min.z, colH }); return t;
    }
    void NoWalk(float x0, float z0, float x1, float z1, float colH = 1) {
        var a = RoomPt(x0, z0); var b = RoomPt(x1, z1); room.cols.Add(new[] { Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), colH });
    }

    void EnterRoom(SLTownWorld.Place p) {
        if (busy != null || modal != null) return;
        if (room != null) ExitRoom(true);
        string kind = RoomKind(p); if (kind == null) return;
        room = new Room { p = p, kind = kind, camDist = cam.dist, camPitch = cam.pitch, pixelLights = QualitySettings.pixelLightCount };
        bool big = kind == "burger" || kind == "italian" || kind == "steak" || kind == "fine" || kind == "african" || kind == "diner";
        room.w = big ? 16 : 14; room.d = big ? 20 : 17;
        room.root = new GameObject("Room " + p.id).transform; room.root.SetParent(town.root, false);
        room.root.position = SLTownWorld.W(SLTownWorld.RoomX, 0, 0); room.root.rotation = SLTownWorld.Yaw(0);
        RoomShell(); RoomFittings();
        foreach (var c in room.cols) town.colliders.Add(c);
        foreach (var s in room.seats) town.seats.Add(s);
        SLTownWorld.SetLayer(room.root);
        foreach (var r in room.root.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
        QualitySettings.pixelLightCount = 8;
        // in through the door, facing into the room; a friend who is with you follows you in
        StandUp(); stepT = 0; groundSit = false; player.mo.emote = null;
        var at = RoomPt(0, 3.6f); player.pos = new Vector3(at.x, 0, at.y); player.ry = RoomRy(0); player.speed = 0;
        cam.yaw = player.ry + Mathf.PI; cam.pitch = 0.2f; cam.dist = 3.3f; cam.cur = 0; cam.lastDrag = Time.time;
        foreach (var f in people.Where(x => x.state == "follow")) { var fp = RoomPt(-1.2f, 2.6f); f.pos = new Vector3(fp.x, 0, fp.y); f.ry = player.ry; Place(f); }
        Place(player);
        var lead = room.workers.FirstOrDefault(w => w.sells);
        ShowToast("Welcome to " + p.name + "!" + (lead != null ? " Talk to " + lead.pp.First + " to " + (kind == "fashion" || kind == "shoes" || kind == "jewel" ? "see the collection." : kind == "books" ? "find a book." : kind == "salon" || kind == "spa" ? "book in." : "order.") : ""));
    }
    void ExitRoom(bool silent = false) {
        if (room == null) return;
        if (shop != null) CloseBoutique(); CloseCard();
        if (player.seat != null) StandUp();
        stepT = 0;
        foreach (var c in room.cols) town.colliders.Remove(c);
        foreach (var s in room.seats) town.seats.Remove(s);
        foreach (var w in room.workers) Destroy(w.pp.go.gameObject);
        foreach (var g in room.guests) Destroy(g.go.gameObject);
        Destroy(room.root.gameObject);
        QualitySettings.pixelLightCount = room.pixelLights; cam.dist = room.camDist; cam.pitch = room.camPitch;
        var p = room.p; room = null;
        if (player != null && !silent) {
            var o = town.Collide(new Vector2(p.x + Mathf.Sin(p.ry) * 0.5f, p.z + Mathf.Cos(p.ry) * 0.5f));
            player.pos = new Vector3(o.x, town.HeightAt(o.x, o.y), o.y); player.ry = p.ry; cam.yaw = p.ry + Mathf.PI; cam.cur = 0; Place(player);
            foreach (var f in people.Where(x => x.state == "follow")) { f.pos = player.pos + new Vector3(Mathf.Cos(p.ry) * 1.2f, 0, -Mathf.Sin(p.ry) * 1.2f); Place(f); }
        }
    }
    /// <summary>Indoors: the room's own warm light instead of the sky's, no sun, no haze.</summary>
    void RoomLight() {
        town.sun.intensity = 0; RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = room.amb * 1.1f; RenderSettings.ambientEquatorColor = room.amb * 0.85f; RenderSettings.ambientGroundColor = room.amb * 0.55f;
        if (tone != null) tone.exposure = 1.05f;
    }
    Near RoomNear() {
        Near best = null; float bd = 2.7f;
        foreach (var w in room.workers) { float d = D(w.pp, player); if (d < bd) { bd = d; best = new Near { kind = "worker", w = w, label = "Talk to " + w.pp.First, sub = w.role }; } }
        if (best != null) return best;
        var door = RoomPt(0, 0.6f); if (new Vector2(door.x - player.pos.x, door.y - player.pos.z).magnitude < 1.9f) return new Near { kind = "exit", label = "Leave " + room.p.name, sub = "back to the street" };
        return null;
    }
    void TickRoom(float dt) {
        foreach (var w in room.workers) {
            var pp = w.pp; var mo = pp.mo;
            if (w.talkT > 0) { w.talkT -= dt; pp.speed = Mathf.Lerp(pp.speed, 0, dt * 8); pp.ry = Turn(pp.ry, Mathf.Atan2(player.pos.x - pp.pos.x, player.pos.z - pp.pos.z), dt * 6); }
            else if (w.beat != null) {
                if (w.wait > 0) { w.wait -= dt; pp.speed = Mathf.Lerp(pp.speed, 0, dt * 8); }
                else {
                    var tgt = w.beat[w.leg]; MoveToward(pp, tgt.x, tgt.y, 1.15f, dt);
                    var c = town.Collide(new Vector2(pp.pos.x, pp.pos.z), 0.25f); pp.pos.x = c.x; pp.pos.z = c.y;
                    if (new Vector2(tgt.x - pp.pos.x, tgt.y - pp.pos.z).magnitude < 0.25f) { w.leg = (w.leg + 1) % w.beat.Count; w.wait = 2 + R() * 4; }
                }
            } else {
                var tgt = w.post; if (new Vector2(tgt.x - pp.pos.x, tgt.y - pp.pos.z).magnitude > 0.1f) MoveToward(pp, tgt.x, tgt.y, 1.1f, dt);
                else { pp.speed = Mathf.Lerp(pp.speed, 0, dt * 8); pp.ry = Turn(pp.ry, w.postRy, dt * 4); }
            }
            // cooks and baristas keep their hands busy; everyone chats while you talk to them
            mo.talking = w.talkT > 0 || (w.kind == "cook" && pp.speed < 0.1f);
            mo.Animate(dt, pp.speed); Place(pp);
        }
        foreach (var g in room.guests) { g.mo.talking = true; g.mo.Animate(dt, 0); }
        // the door out stays in reach even if you wander behind the counter
        if (Mathf.Abs(player.pos.x - SLTownWorld.RoomX) > room.w) ExitRoom();
    }
    void TalkToWorker(Worker w) {
        w.talkT = 8; w.pp.waveT = 1.2f;
        string line = Pick(WorkerLines(w));
        if (w.sells) {
            if (O(data_town("BRANDS")).ContainsKey(room.p.id)) { ShowToast(w.pp.First + ": “" + line + "”"); OpenPlace(room.p); return; }
            OpenPlace(room.p, w.pp.First + ": “" + line + "”"); return;
        }
        OpenCard(w.pp.name, w.role + " · " + room.p.name, line, new List<Opt> {
            new Opt { label = "Chat for a minute", note = "+social", run = () => { Bump("social", 4); lifeMin += 5; CloseCard(); ShowToast(w.pp.First + " laughs. “Come back soon!”"); } },
            new Opt { label = "Let " + (w.pp.male ? "him" : "her") + " get on with it", run = CloseCard } });
    }
    string[] WorkerLines(Worker w) {
        switch (w.role) {
            case "Barista": case "Cashier": return new[] { "Hi! What can I get started for you?", "Welcome in! Hot or iced today?", "Hey, love the outfit! What are you having?" };
            case "Crew": return new[] { "Welcome to " + room.p.name + "! Can I take your order?", "Hi there! Meal or just a snack?", "Fries are fresh out of the fryer!" };
            case "Host": case "Maître d’": return new[] { "Good evening, welcome. A table for you?", "Welcome! Let me show you to your table.", "Lovely to have you with us tonight." };
            case "Waiter": case "Server": return new[] { "Can I bring you anything else?", "The chef’s special is wonderful tonight.", "Enjoy your meal!" };
            case "Chef": case "Cook": case "Sushi chef": return new[] { "Behind you, hot plate!", "Order up!", "Everything’s made fresh, right here." };
            case "Sales assistant": case "Stylist": return new[] { "Hi! The new season just came in, take a look.", "That colour would be gorgeous on you.", "Let me know if you want to try anything on." };
            case "Jeweller": return new[] { "Welcome. May I show you something special?", "Each piece is set by hand.", "That one catches the light beautifully." };
            case "Security": return new[] { "Good day, welcome in.", "Enjoy your visit.", "Just keeping an eye on the sparkle." };
            case "Bookseller": return new[] { "Looking for anything in particular?", "Our book club meets on Thursdays!", "This one kept me up all night." };
            case "Bartender": return new[] { "What are we mixing tonight?", "Try the house mocktail, it’s a hit.", "Coming right up!" };
            case "Hair stylist": case "Therapist": return new[] { "Welcome! Ready to be pampered?", "We have a chair free right now.", "Sit back and relax." };
            default: return new[] { "Hi! Welcome in.", "Busy day today!", "Let me know if you need anything." };
        }
    }
    static readonly string[] HerNames = { "Ama", "Chloe", "Ifeoma", "Sofia", "Mei", "Priya", "Lucia", "Zara", "Nia", "Hana", "Grace", "Yemi", "Inès", "Aiko" };
    static readonly string[] HisNames = { "Tunde", "Marco", "Kenji", "Sam", "Femi", "Luca", "Omar", "Kwame", "Jonah", "Diego", "Hugo", "Ravi" };
    int hire;
    Worker Hire(string role, string kind, float x, float z, float face, Color top, Color? bottom = null, bool sells = false, string hat = null, bool apron = false, float[] beat = null) {
        string id = NextBody(); if (id == null) return null; bool male = SLStage3D.IsMale(id);
        var names = male ? HisNames : HerNames; string name = names[(hire * 7 + room.p.id.GetHashCode() & 0x7fff) % names.Length]; hire++;
        var look = new Look3 { id = id, height = male ? 1.06f : 1f, uniform = top, uniformBottom = bottom ?? new Color(0.12f, 0.12f, 0.14f) };
        var pp = Spawn(id, null, look); pp.name = name; pp.role = role; pp.walkSpeed = 1.1f;
        var at = RoomPt(x, z); pp.pos = new Vector3(at.x, 0, at.y); pp.ry = RoomRy(face); Place(pp); pp.mo.Animate(0.016f, 0);
        var w = new Worker { pp = pp, role = role, kind = kind, post = at, postRy = pp.ry, sells = sells };
        if (beat != null) { w.beat = new List<Vector2>(); for (int i = 0; i + 1 < beat.Length; i += 2) w.beat.Add(RoomPt(beat[i], beat[i + 1])); w.wait = R() * 3; }
        Dress3(pp, hat, apron, top);
        room.workers.Add(w); return w;
    }
    /// <summary>A work hat and apron, hung on the figure's bones so they move with it.</summary>
    void Dress3(TP pp, string hat, bool apron, Color top) {
        var rs = pp.fig.GetComponentsInChildren<Renderer>(); var head = rs.FirstOrDefault(r => r.name.EndsWith("/head")); var bones = pp.mo.bones;
        var fwd = SLTownWorld.W(Mathf.Sin(pp.ry), 0, Mathf.Cos(pp.ry)); var rot = pp.go.rotation;
        if (hat != null && head != null) {
            var hb = head.bounds; var top0 = new Vector3(hb.center.x, hb.max.y, hb.center.z) + fwd * 0.01f;
            var h = new GameObject("hat").transform; h.position = top0; h.rotation = rot;
            if (hat == "chef") { Part(h, PrimitiveType.Cylinder, new Vector3(0, 0.06f, 0), new Vector3(0.21f, 0.09f, 0.21f), Mat(new Color(0.97f, 0.97f, 0.96f), 0.2f)); Part(h, PrimitiveType.Sphere, new Vector3(0, 0.17f, 0), new Vector3(0.26f, 0.12f, 0.26f), Mat(new Color(0.97f, 0.97f, 0.96f), 0.2f)); }
            else { Part(h, PrimitiveType.Sphere, new Vector3(0, -0.02f, -0.01f), new Vector3(0.21f, 0.12f, 0.23f), Mat(top, 0.25f)); Part(h, PrimitiveType.Cube, new Vector3(0, -0.055f, 0.11f), new Vector3(0.17f, 0.012f, 0.1f), Mat(top * 0.8f, 0.25f)); }
            h.SetParent(bones[SLRig.Head], true);
        }
        if (apron) {
            var hips = bones[SLRig.Hips].position; float s = pp.look.height;
            var a = new GameObject("apron").transform; a.position = hips + fwd * 0.15f * s + Vector3.down * 0.1f * s; a.rotation = rot;
            Part(a, PrimitiveType.Cube, Vector3.zero, new Vector3(0.36f, 0.5f, 0.012f) * s, Mat(new Color(0.12f, 0.11f, 0.1f), 0.2f));
            Part(a, PrimitiveType.Cube, new Vector3(0, 0.25f * s, -0.07f), new Vector3(0.4f, 0.03f, 0.16f) * s, Mat(new Color(0.12f, 0.11f, 0.1f), 0.2f));
            a.SetParent(bones[SLRig.Hips], true);
        }
        SLTownWorld.SetLayer(pp.go);
    }
    /// <summary>A customer at a table, sitting and chatting.</summary>
    void Guest(float x, float z, float face, float seatY = 0.63f) {
        string id = NextBody(); if (id == null) return;
        var pp = Spawn(id, null, new Look3 { id = id, height = SLStage3D.IsMale(id) ? 1.06f : 1f }); var at = RoomPt(x, z);
        pp.pos = new Vector3(at.x, 0, at.y); pp.ry = RoomRy(face); pp.mo.pose = "sit"; pp.mo.seatDrop = seatY + 0.06f; Place(pp);
        room.seats.RemoveAll(s => new Vector2(s.x - at.x, s.z - at.y).magnitude < 0.45f);     // the chair is hers
        room.guests.Add(pp);
    }
    void Seat(float x, float z, float face, float y = 0.63f) { var at = RoomPt(x, z); room.seats.Add(new SLTownWorld.Seat { x = at.x, z = at.y, ry = RoomRy(face), y = y, place = room.p.id }); }

    // ---------------------------------------------------------------- the room's shell: floor, walls, ceiling, lights, the door
    void RoomShell() {
        string k = room.kind; float W = room.w, D = room.d, H = room.h; var e = EateryBy(room.p.id);
        Color wallC = k == "burger" ? C("#f4efe6") : k == "coffee" ? C("#e9dfd2") : k == "diner" ? C("#fff3d6") : k == "african" ? C("#f2d9b8") : k == "italian" ? C("#efe2c9")
            : k == "sushi" ? C("#d9c8a8") : k == "steak" ? C("#4a2c1e") : k == "fine" ? C("#f3eee6") : k == "jewel" ? C("#1d1a22") : k == "bar" ? C("#2a1f33")
            : k == "books" ? C("#e7dcc7") : k == "spa" ? C("#dfe8df") : k == "salon" ? C("#f6e3ea") : k == "shoes" ? C("#e8e8ea") : C("#f1ebe4");
        Texture2D floorT = k == "burger" ? Checker("tiles_rw", C("#f2f0ec"), C("#c8102e"), 8, 3) : k == "diner" ? Checker("tiles_bw", C("#f5f2ea"), C("#2b2b2e"), 8, 2)
            : k == "fine" || k == "jewel" || k == "spa" ? RoomTex("floor_white") : k == "steak" || k == "bar" || k == "books" ? RoomTex("floor_walnut") : k == "salon" ? RoomTex("floor_rose") : RoomTex("floor_oak");
        Texture2D floorN = floorT != null && floorT.name.StartsWith("floor") ? RoomTex(floorT.name + "_n") : null;
        var floor = Mat(Color.white, k == "fine" || k == "jewel" ? 0.75f : 0.45f, 0, null, floorT, new Vector2(W / 2.5f, D / 2.5f), floorN);
        Box(room.root, new Vector3(0, -0.05f, D / 2), new Vector3(W, 0.1f, D), floor);
        var wall = Mat(wallC * (k == "steak" || k == "jewel" || k == "bar" ? 1.1f : 1.3f), 0.2f, 0, null, RoomTex("wall"), new Vector2(W / 3, H / 3), RoomTex("wall_n"));     // the plaster texture darkens the paint
        Solid(new Vector3(0, H / 2, D + 0.15f), new Vector3(W + 0.6f, H, 0.3f), wall, 0, 6);
        Solid(new Vector3(-W / 2 - 0.15f, H / 2, D / 2), new Vector3(0.3f, H, D), wall, 0, 6); Solid(new Vector3(W / 2 + 0.15f, H / 2, D / 2), new Vector3(0.3f, H, D), wall, 0, 6);
        Solid(new Vector3(-W / 4 - 0.6f, H / 2, -0.15f), new Vector3(W / 2 - 1.2f, H, 0.3f), wall, 0, 6); Solid(new Vector3(W / 4 + 0.6f, H / 2, -0.15f), new Vector3(W / 2 - 1.2f, H, 0.3f), wall, 0, 6);
        Box(room.root, new Vector3(0, H - 0.6f, -0.15f), new Vector3(2.4f, 1.2f, 0.3f), wall);
        // the way out: glass doors with the street's daylight behind them, and a lit EXIT sign
        Box(room.root, new Vector3(0, 1.5f, -0.1f), new Vector3(2.4f, 3.0f, 0.06f), Mat(new Color(0.6f, 0.7f, 0.75f), 0.95f, 0, new Color(0.75f, 0.85f, 0.95f) * 0.9f));
        Box(room.root, new Vector3(0, 1.5f, -0.06f), new Vector3(0.06f, 3.0f, 0.08f), Mat(new Color(0.1f, 0.1f, 0.1f), 0.6f, 0.6f));
        var ex = Box(room.root, new Vector3(0, 3.25f, 0.02f), new Vector3(0.7f, 0.24f, 0.05f), Mat(new Color(0.1f, 0.5f, 0.2f), 0.4f, 0, new Color(0.2f, 1f, 0.4f) * 1.4f));
        Words(room.root, "EXIT", new Vector3(0, 3.25f, 0.05f), 0.03f, Color.white, true);
        // baseboards, a ceiling, and the lights in it
        var trim = Mat(Color.Lerp(wallC, Color.black, 0.35f), 0.4f);    // baseboards and mouldings
        Box(room.root, new Vector3(0, 0.06f, D - 0.02f), new Vector3(W, 0.12f, 0.04f), trim); Box(room.root, new Vector3(-W / 2 + 0.02f, 0.06f, D / 2), new Vector3(0.04f, 0.12f, D), trim); Box(room.root, new Vector3(W / 2 - 0.02f, 0.06f, D / 2), new Vector3(0.04f, 0.12f, D), trim);
        Box(room.root, new Vector3(0, H + 0.05f, D / 2), new Vector3(W + 0.6f, 0.1f, D + 0.6f), Mat(k == "steak" || k == "bar" || k == "jewel" ? C("#1a1412") : C("#f4f1ec"), 0.1f));
        Color lc = k == "bar" ? new Color(0.85f, 0.55f, 1f) : k == "burger" || k == "shoes" || k == "spa" ? new Color(1f, 0.97f, 0.92f) : new Color(1f, 0.82f, 0.6f);
        room.amb = k == "bar" || k == "jewel" || k == "steak" ? new Color(0.32f, 0.27f, 0.3f) : k == "fine" || k == "sushi" ? new Color(0.48f, 0.43f, 0.38f) : new Color(0.62f, 0.58f, 0.53f);
        float li = k == "bar" || k == "steak" || k == "jewel" ? 1.3f : 1.7f;
        for (int ix = 0; ix < 2; ix++) for (int iz = 0; iz < 3; iz++) {
            float x = (ix - 0.5f) * W * 0.5f, z = D * (0.22f + iz * 0.29f);
            var l = new GameObject("light").AddComponent<Light>(); l.type = LightType.Point; l.color = lc; l.intensity = li; l.range = 9.5f; l.shadows = LightShadows.None;
            l.transform.SetParent(room.root, false); l.transform.localPosition = new Vector3(x, H - 0.7f, z);
            // a real fitting where this build has one: chandeliers for fine dining and jewels, pendants over tables, a modern lamp in the shops
            string lk = k == "fine" ? "chandelier" : k == "jewel" ? "crystal" : k == "bar" ? null : new[] { "steak", "italian", "african", "coffee", "diner" }.Contains(k) ? "pendant" : "ceiling_lamp";
            float lh = lk == "chandelier" ? 0.85f : lk == "crystal" || lk == "pendant" ? 0.9f : 0.8f;
            if (lk != null && Model(lk, new Vector3(x, H - lh, z)) != null) {
                if (lk == "pendant" || lk == "ceiling_lamp") Part(room.root, PrimitiveType.Sphere, new Vector3(x, H - lh + 0.06f, z), new Vector3(0.16f, 0.05f, 0.16f), Mat(Color.white, 0.5f, 0, lc * 2.5f));
                else Part(room.root, PrimitiveType.Sphere, new Vector3(x, H - lh * 0.55f, z), Vector3.one * 0.14f, Mat(Color.white, 0.5f, 0, lc * 1.6f));
            }
            else if (k == "fine") { Cyl(room.root, new Vector3(x, H - 0.3f, z), 0.02f, 0.6f, Mat(new Color(0.8f, 0.68f, 0.4f), 0.9f, 1)); for (int c = 0; c < 6; c++) { float a = c * Mathf.PI / 3; Part(room.root, PrimitiveType.Sphere, new Vector3(x + Mathf.Sin(a) * 0.35f, H - 0.75f, z + Mathf.Cos(a) * 0.35f), Vector3.one * 0.11f, Mat(Color.white, 0.9f, 0, new Color(1f, 0.85f, 0.6f) * 2.2f)); } Part(room.root, PrimitiveType.Sphere, new Vector3(x, H - 0.85f, z), new Vector3(0.5f, 0.35f, 0.5f), Glass()); }
            else if (k == "bar" || k == "steak" || k == "italian" || k == "african" || k == "coffee" || k == "diner") { Cyl(room.root, new Vector3(x, H - 0.5f, z), 0.008f, 1.0f, Mat(Color.black)); Part(room.root, PrimitiveType.Sphere, new Vector3(x, H - 1.05f, z), new Vector3(0.38f, 0.26f, 0.38f), Mat(e != null ? e.main : wallC * 0.6f, 0.6f, 0.3f, lc * 0.6f)); Part(room.root, PrimitiveType.Sphere, new Vector3(x, H - 1.13f, z), Vector3.one * 0.13f, Mat(Color.white, 0.5f, 0, lc * 2.5f)); }
            else Box(room.root, new Vector3(x, H - 0.03f, z), new Vector3(1.6f, 0.04f, 0.6f), Mat(Color.white, 0.2f, 0, lc * 1.6f));
        }
        WallDressing(wallC, trim);
        var plant = Prefab("Furniture/plant");
        if (plant != null) foreach (float sx in new[] { -1, 1 }) { var pl = Instantiate(plant, room.root, false).transform; pl.localPosition = new Vector3(sx * (W / 2 - 0.7f), 0, 0.8f); FitHeight(pl, 1.5f); NoWalk(sx * (W / 2 - 1.1f), 0.4f, sx * (W / 2 - 0.3f), 1.2f); }
    }

    /// <summary>Panelling below the dado rail, tall windows onto the street (daylight or night, as when you came in) and framed art.</summary>
    void WallDressing(Color wallC, Material trim) {
        string k = room.kind; float W = room.w, D = room.d;
        bool windows = new[] { "coffee", "diner", "burger", "italian", "sushi", "salon", "spa" }.Contains(k);      // on the left wall
        bool panels = k != "burger" && k != "shoes" && k != "books";
        var panel = Mat(Color.Lerp(wallC, k == "fine" || k == "spa" || k == "salon" ? Color.white : C("#3a2618"), 0.45f), 0.45f);
        if (panels) foreach (float sx in new[] { -1f, 1f }) {
            Box(room.root, new Vector3(sx * (W / 2 - 0.03f), 0.55f, D / 2), new Vector3(0.05f, 1.1f, D - 0.2f), panel);
            Box(room.root, new Vector3(sx * (W / 2 - 0.06f), 1.12f, D / 2), new Vector3(0.06f, 0.05f, D - 0.2f), trim);
            for (float z = 1.2f; z < D - 0.6f; z += 1.4f) Box(room.root, new Vector3(sx * (W / 2 - 0.06f), 0.55f, z), new Vector3(0.02f, 0.75f, 1.1f), Mat(Color.Lerp(wallC, Color.black, 0.2f) * 1.1f, 0.5f));
        }
        float h = Hour; bool day = h >= 7 && h < 18.5f; float dusk = h >= 17 && h < 19.5f ? 1 : 0;
        Color sky = day ? Color.Lerp(new Color(0.75f, 0.88f, 1f), new Color(1f, 0.75f, 0.5f), dusk) : new Color(0.08f, 0.1f, 0.2f);
        var pane = Mat(sky, 0.95f, 0, sky * (day ? 1.15f : 0.35f)); var mull = Mat(Color.Lerp(wallC, Color.white, 0.6f), 0.4f);
        float wx = -(W / 2 - 0.04f);
        if (windows) for (int i = 0; i < 3; i++) {
            float z = D * (0.3f + i * 0.2f), y = 2.0f;
            Box(room.root, new Vector3(wx, y, z), new Vector3(0.04f, 2.0f, 1.5f), pane);
            Box(room.root, new Vector3(wx + 0.03f, y, z), new Vector3(0.05f, 2.1f, 0.07f), mull); Box(room.root, new Vector3(wx + 0.03f, y + 0.25f, z), new Vector3(0.05f, 0.06f, 1.5f), mull);
            foreach (float d in new[] { -0.78f, 0.78f }) Box(room.root, new Vector3(wx + 0.05f, y, z + d), new Vector3(0.1f, 2.15f, 0.08f), trim);
            Box(room.root, new Vector3(wx + 0.06f, y - 1.07f, z), new Vector3(0.18f, 0.06f, 1.65f), trim); Box(room.root, new Vector3(wx + 0.05f, y + 1.06f, z), new Vector3(0.1f, 0.08f, 1.65f), trim);
            if (day) { var l = new GameObject("daylight").AddComponent<Light>(); l.type = LightType.Point; l.color = sky; l.intensity = 0.9f; l.range = 5; l.shadows = LightShadows.None; l.transform.SetParent(room.root, false); l.transform.localPosition = new Vector3(wx + 1.2f, 2.0f, z); }
        }
        // framed pictures: soft abstract colour fields, a different set for each place
        // (none where shelves, the bar's bottles or the kente wall already fill the walls)
        if (new[] { "bar", "shoes", "books", "african", "italian" }.Contains(k)) return;
        float ax = windows ? W / 2 - 0.05f : -(W / 2 - 0.05f);
        var frame = Mat(k == "fine" || k == "steak" || k == "jewel" ? new Color(0.8f, 0.65f, 0.35f) : new Color(0.12f, 0.1f, 0.09f), 0.7f, k == "fine" || k == "steak" || k == "jewel" ? 1 : 0);
        int seed = room.p.id.GetHashCode() & 0xffff;
        for (int i = 0; i < 3; i++) {
            float z = D * (0.3f + i * 0.2f), sgn = Mathf.Sign(ax);
            var art = Pattern("art" + seed + i, 128, (x, y) => {
                float n = Mathf.PerlinNoise(x * 0.025f + seed + i * 3.1f, y * 0.025f + i), m = Mathf.PerlinNoise(x * 0.06f + 40 + i, y * 0.06f + seed * 0.01f);
                return Color.HSVToRGB(((seed % 100) / 100f + i * 0.21f + n * 0.25f) % 1, 0.35f + m * 0.35f, 0.55f + n * 0.4f); });
            Box(room.root, new Vector3(ax - sgn * 0.01f, 2.3f, z), new Vector3(0.04f, 1.05f, 0.85f), frame);
            Box(room.root, new Vector3(ax - sgn * 0.035f, 2.3f, z), new Vector3(0.02f, 0.92f, 0.72f), Mat(Color.white, 0.2f, 0, null, art));
        }
    }

    // ---------------------------------------------------------------- what is in each kind of room, and who works there
    void RoomFittings() {
        hire = 0; var e = EateryBy(room.p.id); float W = room.w, D = room.d;
        Color main = e != null ? e.main : C(room.p.color), acc = e != null ? e.accent : Color.white;
        string k = room.kind;
        room.chair = k == "coffee" || k == "diner" ? "chair_cafe" : new[] { "italian", "african", "steak", "fine", "sushi", "jewel" }.Contains(k) ? "chair_dining" : null;
        room.table = k == "coffee" ? "table_cafe" : k == "diner" || k == "steak" ? "table_dining" : null;
        switch (room.kind) {
            case "coffee": Coffee(main, acc, e); break;
            case "burger": Burger(main, acc, e); break;
            case "diner": case "italian": case "african": case "steak": case "fine": case "sushi": Dining(main, acc, e); break;
            case "fashion": case "shoes": Fashion(main); break;
            case "jewel": Jewels(main); break;
            case "books": Books(); break;
            case "bar": Bar(main); break;
            case "salon": case "spa": Pamper(main); break;
        }
    }
    // a counter across the room at depth z, from x0 to x1, its top in the house colour
    void Counter(float x0, float x1, float z, Color front, Color top, float hgt = 1.05f, float depth = 0.75f) {
        Solid(new Vector3((x0 + x1) / 2, hgt / 2, z), new Vector3(x1 - x0, hgt, depth), Mat(front, 0.35f));
        Box(room.root, new Vector3((x0 + x1) / 2, hgt + 0.03f, z), new Vector3(x1 - x0 + 0.1f, 0.06f, depth + 0.1f), Mat(top, 0.7f, 0.1f));
        Box(room.root, new Vector3((x0 + x1) / 2, 0.06f, z - depth / 2 - 0.01f), new Vector3(x1 - x0, 0.12f, 0.02f), Mat(front * 0.5f, 0.3f));
    }
    /// <summary>A table and its chairs; returns the chairs (x, z, facing) so a guest can take one.</summary>
    List<Vector3> Table(float x, float z, bool round, Color top, float cloth = 0, int chairs = 2, Color? chairC = null, float size = 0.8f) {
        var seats = new List<Vector3>();
        var tm = Mat(top, cloth > 0 ? 0.15f : 0.6f); float depth = size;
        if (cloth == 0 && room.table != null && Model(room.table, new Vector3(x, 0, z)) != null) {      // a real table: the café's round one is 1 m across, the dining one 1.08 by 0.67
            if (room.table == "table_cafe") size = depth = 1.0f; else { size = 1.08f; depth = 0.67f; }
        } else {
        if (round) Cyl(room.root, new Vector3(x, 0.74f, z), size / 2, 0.04f, tm); else Box(room.root, new Vector3(x, 0.74f, z), new Vector3(size, 0.04f, size), tm);
        if (cloth > 0) { if (round) Cyl(room.root, new Vector3(x, 0.55f, z), size / 2 + 0.02f, 0.38f, tm); else Box(room.root, new Vector3(x, 0.55f, z), new Vector3(size + 0.04f, 0.38f, size + 0.04f), tm); }
        Cyl(room.root, new Vector3(x, 0.37f, z), 0.04f, 0.74f, Mat(new Color(0.1f, 0.1f, 0.1f), 0.6f, 0.7f));
        }
        NoWalk(x - size / 2, z - depth / 2, x + size / 2, z + depth / 2);
        var cc = chairC ?? new Color(0.25f, 0.17f, 0.11f);
        for (int i = 0; i < chairs; i++) {
            float a = chairs == 2 ? (i == 0 ? Mathf.PI : 0) : i * Mathf.PI * 2 / chairs, r = (chairs == 2 ? depth : size) / 2 + 0.35f;
            float cx = x + Mathf.Sin(a) * r, cz = z + Mathf.Cos(a) * r; Chair(cx, cz, a + Mathf.PI, cc); seats.Add(new Vector3(cx, cz, a + Mathf.PI));
        }
        return seats;
    }
    void Chair(float x, float z, float face, Color c) {
        if (room.chair != null && Model(room.chair, new Vector3(x, 0, z), face * Mathf.Rad2Deg) != null) { Seat(x, z, face); return; }
        var t = new GameObject("chair").transform; t.SetParent(room.root, false); t.localPosition = new Vector3(x, 0, z); t.localRotation = Quaternion.Euler(0, face * Mathf.Rad2Deg, 0);
        var m = Mat(c, 0.35f); var leg = Mat(new Color(0.08f, 0.08f, 0.08f), 0.5f, 0.6f);
        Box(t, new Vector3(0, 0.45f, 0), new Vector3(0.44f, 0.05f, 0.42f), m); Box(t, new Vector3(0, 0.72f, -0.2f), new Vector3(0.44f, 0.5f, 0.04f), m);
        foreach (var (lx, lz) in new[] { (-0.19f, -0.18f), (0.19f, -0.18f), (-0.19f, 0.18f), (0.19f, 0.18f) }) Box(t, new Vector3(lx, 0.22f, lz), new Vector3(0.03f, 0.44f, 0.03f), leg);
        Seat(x, z, face);
    }
    void Stool(float x, float z, float face, Color c) {
        Cyl(room.root, new Vector3(x, 0.4f, z), 0.025f, 0.8f, Mat(new Color(0.1f, 0.1f, 0.1f), 0.6f, 0.8f)); Cyl(room.root, new Vector3(x, 0.8f, z), 0.2f, 0.06f, Mat(c, 0.4f));
        Cyl(room.root, new Vector3(x, 0.02f, z), 0.18f, 0.03f, Mat(new Color(0.1f, 0.1f, 0.1f), 0.6f, 0.8f)); Seat(x, z, face, 0.95f);
    }
    void Board(float x, float z, float w, float h, string title, IEnumerable<string> lines, Color bg, Color fg, bool lit = true) {
        Box(room.root, new Vector3(x, 3.0f, z), new Vector3(w, h, 0.06f), Mat(bg, 0.3f, 0, lit ? bg * 0.4f + Color.white * 0.05f : (Color?)null));
        Words(room.root, title, new Vector3(x, 3.0f + h / 2 - 0.22f, z - 0.04f), 0.028f, fg);
        int i = 0; foreach (var l in lines) { Words(room.root, l, new Vector3(x, 3.0f + h / 2 - 0.5f - i * 0.24f, z - 0.04f), 0.017f, fg, false, false); i++; }
    }
    IEnumerable<string> MenuLines(Eatery e) => e != null ? e.menu.Select(m => m.name + "  ·  " + m.cost) : A(data_town("COFFEE")).Select(A).Take(6).Select(c => (string)c[0] + "  ·  " + F(c[1]));

    void Coffee(Color main, Color acc, Eatery e) {
        float W = room.w, D = room.d; var wood = C("#6b4a2f");
        Counter(-W / 2 + 1, 2.5f, D - 3.2f, room.p.id == "candy" ? C("#ffb3c7") : room.p.id == "gelato" ? C("#f7d7e0") : wood, room.p.id == "greenwave" ? C("#2b2b2b") : C("#e9e4dc"));
        NoWalk(-W / 2, D - 3.6f, -W / 2 + 1, D);
        // the pastry case, the espresso machines and grinders, cups along the back wall
        Box(room.root, new Vector3(1.3f, 1.3f, D - 3.2f), new Vector3(2.0f, 0.45f, 0.6f), Glass());
        if (Model("cake", new Vector3(0.75f, 1.09f, D - 3.2f)) != null) { Model("cake", new Vector3(1.35f, 1.09f, D - 3.2f), 40); Model("cake", new Vector3(1.95f, 1.09f, D - 3.2f), 80, 0.8f); }
        else for (int i = 0; i < 8; i++) Part(room.root, PrimitiveType.Sphere, new Vector3(0.5f + i * 0.22f, 1.17f, D - 3.2f + (i % 2) * 0.15f), new Vector3(0.16f, 0.07f, 0.12f), Mat(Color.Lerp(C("#d9a35f"), C("#6b3d1f"), (i % 3) * 0.4f), 0.5f));
        Model("register", new Vector3(-1.5f, 1.08f, D - 3.25f), 180);
        if (Model("coffeecart", new Vector3(W / 2 - 1.8f, 0, D - 0.6f), 180) != null) NoWalk(W / 2 - 2.3f, D - 1, W / 2 - 1.3f, D);
        if (room.p.id == "dore" || room.p.id == "patisserie") Model("teaset", new Vector3(-3.6f, 1.08f, D - 3.2f), 180);
        Model("vase", new Vector3(W / 2 - 4.0f, 0.98f, D - 0.45f), 0, 0.8f);
        Counter(-W / 2 + 0.5f, W / 2 - 3.5f, D - 0.45f, wood * 0.8f, C("#cfc8bd"), 0.95f, 0.6f);
        for (int i = 0; i < 2; i++) {
            float x = -3.8f + i * 2.4f; var steel = Mat(new Color(0.75f, 0.76f, 0.78f), 0.85f, 1);
            Box(room.root, new Vector3(x, 1.25f, D - 0.5f), new Vector3(0.9f, 0.55f, 0.5f), steel); Box(room.root, new Vector3(x, 1.56f, D - 0.5f), new Vector3(0.95f, 0.06f, 0.52f), Mat(main, 0.6f));
            Cyl(room.root, new Vector3(x - 0.2f, 1.02f, D - 0.75f), 0.035f, 0.12f, Mat(Color.black, 0.5f)); Cyl(room.root, new Vector3(x + 0.2f, 1.02f, D - 0.75f), 0.035f, 0.12f, Mat(Color.black, 0.5f));
            Cyl(room.root, new Vector3(x + 0.75f, 1.3f, D - 0.5f), 0.09f, 0.4f, Mat(new Color(0.1f, 0.1f, 0.1f), 0.5f)); Part(room.root, PrimitiveType.Sphere, new Vector3(x + 0.75f, 1.6f, D - 0.5f), new Vector3(0.2f, 0.25f, 0.2f), Glass());
        }
        for (int i = 0; i < 12; i++) Cyl(room.root, new Vector3(-W / 2 + 1 + i * 0.32f, 2.05f, D - 0.2f), 0.05f, 0.11f, Mat(i % 3 == 0 ? main : Color.white, 0.6f));
        Box(room.root, new Vector3(-W / 2 + 2.9f, 1.98f, D - 0.2f), new Vector3(4.2f, 0.04f, 0.3f), Mat(wood, 0.4f));
        Board(-1.5f, D - 0.05f, 5.2f, 1.5f, room.p.name.ToUpperInvariant(), MenuLines(e), room.p.id == "greenwave" ? C("#1d2b24") : C("#2a2420"), C("#f4efe6"));
        // tables, a long window bench and a sofa corner
        Color tc = C("#e9e4dc"); var t1 = Table(-4, 6, true, tc); var t2 = Table(-1, 7.8f, true, tc); var t3 = Table(2.5f, 6, true, tc); Table(-4, 10, true, tc, 0, 2);
        var sofa = Prefab("Furniture/sofa");
        if (sofa != null) { var s = Instantiate(sofa, room.root, false).transform; s.localPosition = new Vector3(W / 2 - 1.0f, 0, 9.5f); s.localRotation = Quaternion.Euler(0, -90, 0); FitHeight(s, 0.85f); NoWalk(W / 2 - 1.6f, 8, W / 2, 11); Seat(W / 2 - 1.0f, 9.0f, -Mathf.PI / 2, 0.68f); Seat(W / 2 - 1.0f, 10.0f, -Mathf.PI / 2, 0.68f); }
        Hire("Cashier", "front", -1.5f, D - 2.4f, Mathf.PI, main, null, true, null, true);
        Hire("Barista", "cook", -3.8f, D - 1.2f, 0, main, null, false, null, true);
        Hire("Barista", "floor", 1, 9, 0, main, null, false, null, true, new[] { -4f, 5f, 2.5f, 5f, -1f, 8.6f, 3f, 10.5f });
        Guest(t1[0].x, t1[0].y, t1[0].z); Guest(t3[1].x, t3[1].y, t3[1].z); Guest(t2[0].x, t2[0].y, t2[0].z);
    }

    void Burger(Color main, Color acc, Eatery e) {
        float W = room.w, D = room.d; var steel = Mat(new Color(0.72f, 0.74f, 0.76f), 0.8f, 1);
        Counter(-W / 2 + 1.5f, W / 2 - 1.5f, D - 5, main, C("#e8e6e2"));
        NoWalk(-W / 2, D - 5.4f, -W / 2 + 1.5f, D); NoWalk(W / 2 - 1.5f, D - 5.4f, W / 2, D);
        for (int i = 0; i < 3; i++) { float x = -3 + i * 3; Box(room.root, new Vector3(x, 1.3f, D - 5.1f), new Vector3(0.45f, 0.35f, 0.3f), Mat(new Color(0.1f, 0.1f, 0.12f), 0.6f)); Box(room.root, new Vector3(x, 1.3f, D - 5.27f), new Vector3(0.4f, 0.28f, 0.01f), Mat(Color.black, 0.9f, 0, new Color(0.2f, 0.5f, 0.9f) * 0.8f)); }
        // big lit menu boards
        var list = e.menu.ToList();
        for (int b = 0; b < 3; b++) Board(-4.5f + b * 4.5f, D - 4.7f, 4.0f, 1.4f, b == 0 ? "BURGERS" : b == 1 ? "MEALS" : "SIDES & SHAKES", list.Skip(b * 2).Take(2).Select(m => m.name + "  ·  " + m.cost), C("#1b1b1b"), acc);
        foreach (int b in new[] { 0, 1, 2 }) Box(room.root, new Vector3(-4.5f + b * 4.5f, 3.85f, D - 4.7f), new Vector3(4.0f, 0.08f, 0.08f), Mat(acc, 0.4f, 0, acc * 0.7f));
        Box(room.root, new Vector3(0, 3.4f, D - 4.75f), new Vector3(W - 3, 1.8f, 0.04f), Mat(new Color(0.08f, 0.08f, 0.08f), 0.3f));
        // the kitchen: fryers, the grill, a warming pass, steel everywhere
        Counter(-W / 2 + 0.5f, W / 2 - 0.5f, D - 0.5f, new Color(0.7f, 0.72f, 0.74f), new Color(0.82f, 0.83f, 0.85f), 0.95f, 0.8f);
        for (int i = 0; i < 3; i++) { Box(room.root, new Vector3(-5 + i * 0.9f, 1.05f, D - 0.5f), new Vector3(0.7f, 0.2f, 0.6f), steel); Box(room.root, new Vector3(-5 + i * 0.9f, 1.12f, D - 0.5f), new Vector3(0.6f, 0.04f, 0.5f), Mat(C("#c9a13b"), 0.9f, 0, C("#c9a13b") * 0.3f)); }
        Box(room.root, new Vector3(0.5f, 1.0f, D - 0.5f), new Vector3(2.4f, 0.06f, 0.7f), Mat(new Color(0.12f, 0.12f, 0.12f), 0.7f, 0.6f));
        for (int i = 0; i < 6; i++) Cyl(room.root, new Vector3(-0.3f + (i % 3) * 0.55f, 1.05f, D - 0.7f + (i / 3) * 0.35f), 0.07f, 0.03f, Mat(C("#5a3218"), 0.4f));
        Box(room.root, new Vector3(4.5f, 1.3f, D - 2.6f), new Vector3(3, 0.5f, 0.5f), steel); Box(room.root, new Vector3(4.5f, 1.6f, D - 2.6f), new Vector3(3, 0.04f, 0.5f), Mat(Color.white, 0.4f, 0, new Color(1, 0.6f, 0.3f) * 0.8f));
        NoWalk(3, D - 2.9f, 6, D - 2.3f);
        Box(room.root, new Vector3(0, 2.6f, D - 0.4f), new Vector3(W - 1, 0.6f, 0.9f), steel);       // the extractor hood
        // booths along the side wall, tables in the middle
        var red = Mat(main, 0.5f); var tableM = Mat(C("#e8e6e2"), 0.6f);
        for (int i = 0; i < 4; i++) {
            float z = 2.5f + i * 2.4f, x = -W / 2 + 0.9f;
            Solid(new Vector3(x, 0.74f, z), new Vector3(1.2f, 0.05f, 0.75f), tableM); Cyl(room.root, new Vector3(x, 0.37f, z), 0.05f, 0.74f, steel);
            foreach (float dz in new[] { -0.75f, 0.75f }) { Box(room.root, new Vector3(x, 0.24f, z + dz), new Vector3(1.6f, 0.48f, 0.5f), red); Box(room.root, new Vector3(x, 0.8f, z + dz * 1.32f), new Vector3(1.6f, 0.75f, 0.14f), red); NoWalk(x - 0.8f, z + dz - 0.25f, x + 0.8f, z + dz + 0.25f); Seat(x + 0.2f, z + dz, dz < 0 ? 0 : Mathf.PI, 0.66f); }
        }
        Table(1.5f, 4, false, C("#e8e6e2"), 0, 2, main); Table(4.5f, 4, false, C("#e8e6e2"), 0, 2, main); Table(1.5f, 7.5f, false, C("#e8e6e2"), 0, 2, main); Table(4.5f, 7.5f, false, C("#e8e6e2"), 0, 2, main);
        Hire("Crew", "front", -3, D - 4.2f, Mathf.PI, main, null, true, "cap", false); Hire("Crew", "front", 3, D - 4.2f, Mathf.PI, main, null, true, "cap", false);
        Hire("Cook", "cook", -4.1f, D - 1.4f, 0, C("#2b2b2b"), null, false, "cap", true); Hire("Cook", "cook", 0.5f, D - 1.4f, 0, C("#2b2b2b"), null, false, "cap", true);
        Hire("Crew", "floor", 3, 6, 0, main, null, false, "cap", false, new[] { 3f, 2.3f, 3f, 9f, -3f, 9f, -3f, 5f });
        Guest(-W / 2 + 1.1f, 2.5f - 0.75f, 0); Guest(-W / 2 + 1.1f, 4.9f + 0.75f, Mathf.PI); Guest(1.5f, 4 - 0.75f, 0); Guest(4.5f, 7.5f + 0.75f, Mathf.PI);
    }

    void Dining(Color main, Color acc, Eatery e) {
        float W = room.w, D = room.d; string k = room.kind;
        Color cloth = k == "italian" ? Color.white : k == "african" ? C("#f6e7c8") : k == "diner" ? C("#ffffff") : k == "sushi" ? C("#d8bf94") : Color.white;
        Texture2D clothT = k == "italian" ? Checker("gingham", C("#c8312a"), C("#f6f1e8"), 16) : null;
        Color chair = k == "steak" ? C("#5a2a1a") : k == "fine" ? C("#e9dfcf") : k == "diner" ? C("#f2b134") : k == "african" ? C("#8a4b22") : C("#4a3322");
        // the open kitchen at the back, behind a pass with heat lamps
        Counter(-W / 2 + 1, W / 2 - 1, D - 2.6f, k == "fine" ? C("#efe8dc") : C("#2b2522"), k == "sushi" ? C("#e7d3ae") : C("#d9d4cc"), 1.1f, 0.6f);
        NoWalk(-W / 2, D - 2.9f, -W / 2 + 1, D); NoWalk(W / 2 - 1, D - 2.9f, W / 2, D);
        for (int i = 0; i < 4; i++) { float x = -4.5f + i * 3; Cyl(room.root, new Vector3(x, 2.3f, D - 2.6f), 0.01f, 1.0f, Mat(Color.black)); Part(room.root, PrimitiveType.Sphere, new Vector3(x, 1.8f, D - 2.6f), new Vector3(0.3f, 0.18f, 0.3f), Mat(C("#b33a1f"), 0.5f, 0.3f, new Color(1, 0.4f, 0.15f) * 1.2f)); }
        Counter(-W / 2 + 0.5f, W / 2 - 0.5f, D - 0.45f, new Color(0.7f, 0.72f, 0.74f), new Color(0.85f, 0.86f, 0.88f), 0.95f, 0.7f);
        if (k == "italian") {      // the pizza oven
            var brick = Mat(C("#9a4a2c"), 0.2f); Part(room.root, PrimitiveType.Sphere, new Vector3(W / 2 - 2.2f, 1.4f, D - 0.9f), new Vector3(2.2f, 1.8f, 1.6f), brick);
            Part(room.root, PrimitiveType.Sphere, new Vector3(W / 2 - 2.2f, 1.2f, D - 1.6f), new Vector3(0.8f, 0.6f, 0.2f), Mat(Color.black, 0.2f, 0, new Color(1, 0.45f, 0.1f) * 2.2f));
        }
        if (k == "african") { Box(room.root, new Vector3(-W / 2 + 0.02f, 2.0f, D / 2), new Vector3(0.04f, 2.2f, D - 4), Mat(Color.white, 0.2f, 0, null, Kente(), new Vector2(D / 1.2f, 2.2f / 1.2f))); }
        if (k == "sushi") {        // a long pale-wood counter with stools in front of the chefs
            Counter(-4.5f, 4.5f, D - 5.2f, C("#3a2a1e"), C("#e7d3ae"), 1.05f, 0.7f);
            for (int i = 0; i < 8; i++) Stool(-4 + i * 1.15f, D - 6.1f, 0, C("#2b2b2b"));
            for (int i = 0; i < 6; i++) Box(room.root, new Vector3(-3.5f + i * 1.4f, 1.12f, D - 5.2f), new Vector3(0.35f, 0.03f, 0.18f), Mat(C("#3b6e3a"), 0.4f));
            Box(room.root, new Vector3(0, 2.6f, D - 0.05f), new Vector3(W - 2, 1.4f, 0.04f), Mat(C("#f3ead8"), 0.2f, 0, C("#f3ead8") * 0.2f));
            for (int i = 0; i < 5; i++) Box(room.root, new Vector3(-5 + i * 2.5f, 2.6f, D - 0.07f), new Vector3(0.04f, 1.4f, 0.05f), Mat(C("#2a1d14"), 0.3f));
        }
        // a bar along one side, its shelves of bottles lit from behind
        if (k == "steak" || k == "fine" || k == "italian" || k == "african") {
            float bx = W / 2 - 1.2f; Solid(new Vector3(bx, 0.55f, D * 0.42f), new Vector3(0.7f, 1.1f, 5.5f), Mat(k == "fine" ? C("#e9dfcf") : C("#3b2418"), 0.5f));
            Box(room.root, new Vector3(bx, 1.13f, D * 0.42f), new Vector3(0.8f, 0.05f, 5.6f), Mat(k == "fine" ? C("#d6c8a8") : C("#1c1410"), 0.85f, 0.2f));
            Box(room.root, new Vector3(W / 2 - 0.15f, 2.0f, D * 0.42f), new Vector3(0.1f, 1.6f, 5.2f), Mat(acc, 0.2f, 0, acc * 0.35f));
            for (int s = 0; s < 3; s++) for (int i = 0; i < 12; i++) Cyl(room.root, new Vector3(W / 2 - 0.3f, 1.42f + s * 0.5f, D * 0.42f - 2.4f + i * 0.42f), 0.04f, 0.3f, Mat(Color.HSVToRGB((i * 0.13f + s * 0.3f) % 1, 0.5f, 0.6f), 0.95f));
            for (int i = 0; i < 4; i++) Stool(bx - 0.75f, D * 0.42f - 1.8f + i * 1.2f, Mathf.PI / 2, chair);
        }
        if (k == "steak") {        // the piano
            Solid(new Vector3(-W / 2 + 1.6f, 0.5f, D * 0.62f), new Vector3(1.5f, 1.0f, 1.9f), Mat(C("#0d0c0c"), 0.9f), 20);
            Box(room.root, new Vector3(-W / 2 + 1.6f, 1.02f, D * 0.62f + 0.75f), new Vector3(1.4f, 0.04f, 0.25f), Mat(Color.white, 0.6f));
        }
        // the host's stand by the door, and the tables
        Solid(new Vector3(-1.9f, 0.55f, 2.2f), new Vector3(0.7f, 1.1f, 0.45f), Mat(Color.Lerp(chair, Color.black, 0.3f), 0.6f));
        Cyl(room.root, new Vector3(-1.75f, 1.2f, 2.2f), 0.06f, 0.12f, Mat(Color.white, 0.4f, 0, new Color(1, 0.8f, 0.5f) * 1.5f));
        Model("vase", new Vector3(-2.05f, 1.1f, 2.2f), 0, 0.6f);
        var tableC = k == "steak" ? C("#1c1410") : cloth; float cl = k == "steak" || k == "diner" ? 0 : 1;
        var tm = clothT != null ? Mat(Color.white, 0.15f, 0, null, clothT, new Vector2(2, 2)) : null;
        int n = 0; float z0 = k == "sushi" ? 4.5f : 4.5f, zMax = k == "sushi" ? D - 8 : D - 5;
        for (float z = z0; z <= zMax; z += 2.8f) for (float x = -W / 2 + 2.2f; x <= W / 2 - 3.5f; x += 3.1f) {
            if (x > -2.6f && x < 0.6f && z < 5) continue;       // keep the way in clear
            var ch = Table(x, z, k == "fine" || k == "sushi", tableC, cl, 2, chair, k == "fine" ? 1.0f : 0.85f);
            Cyl(room.root, new Vector3(x, 0.82f, z), 0.025f, 0.1f, Mat(Color.white, 0.4f, 0, new Color(1, 0.7f, 0.35f) * 2f));    // a candle
            if (n % 3 != 1) Guest(ch[0].x, ch[0].y, ch[0].z);
            n++;
        }
        if (tm != null) foreach (var r in room.root.GetComponentsInChildren<Renderer>().Where(r => r.sharedMaterial == Mat(tableC, 0.15f))) r.sharedMaterial = tm;
        Color uni = k == "fine" || k == "steak" ? C("#151515") : k == "diner" ? main : k == "sushi" ? C("#1b1b1f") : main;
        Hire(k == "fine" ? "Maître d’" : "Host", "front", -1.9f, 2.75f, Mathf.PI, k == "diner" || k == "african" ? main : C("#151515"), null, true);
        Hire(k == "sushi" ? "Sushi chef" : "Chef", "cook", -2.5f, D - 1.3f, 0, Color.white, C("#2b2b2b"), false, k == "sushi" ? "cap" : "chef", true);
        Hire(k == "sushi" ? "Sushi chef" : "Cook", "cook", 2.5f, k == "sushi" ? D - 4.4f : D - 1.3f, k == "sushi" ? Mathf.PI : 0, Color.white, C("#2b2b2b"), false, k == "sushi" ? "cap" : "chef", true);
        Hire("Waiter", "floor", 0, 6, 0, uni, C("#121212"), false, null, k != "fine", new[] { -0.5f, 5.5f, -0.5f, D - 3.6f, 3f, D - 3.6f, 3f, 7f, -3.5f, 7f });
        Hire("Server", "floor", 2, 8, 0, uni, C("#121212"), false, null, k != "fine", new[] { 1.5f, D - 3.6f, 1.5f, 6f, -4.5f, 10f, -1.5f, D - 3.6f });
    }

    void Fashion(Color main) {
        float W = room.w, D = room.d; bool shoes = room.kind == "shoes"; var metal = Mat(new Color(0.8f, 0.68f, 0.4f), 0.85f, 1);
        // the cash desk at the back, a feature wall in the house colour, mirrors and fitting rooms
        Counter(-2.5f, 2.5f, D - 2.5f, C("#f4f1ec"), C("#1c1c1c"), 1.0f, 0.7f);
        Box(room.root, new Vector3(0, 2.4f, D - 0.05f), new Vector3(6, 2.6f, 0.06f), Mat(main, 0.3f, 0, main * 0.12f));
        Words(room.root, room.p.name.ToUpperInvariant(), new Vector3(0, 2.6f, D - 0.1f), 0.07f, Color.white);
        for (int i = 0; i < 3; i++) {
            float x = W / 2 - 1 - i * 1.4f; Box(room.root, new Vector3(x, 1.2f, D - 0.6f), new Vector3(1.2f, 2.4f, 0.04f), Mat(C("#b33a5a"), 0.2f));
            Cyl(room.root, new Vector3(x, 2.45f, D - 0.6f), 0.015f, 1.3f, metal).localRotation = Quaternion.Euler(0, 0, 90);
        }
        NoWalk(W / 2 - 4.4f, D - 1.2f, W / 2, D);
        for (int i = 0; i < 2; i++) {
            if (Model("mirror", new Vector3(-W / 2 + 0.08f, 0.55f, 4 + i * 5), 90) != null) continue;
            Box(room.root, new Vector3(-W / 2 + 0.06f, 1.3f, 4 + i * 5), new Vector3(0.03f, 2.2f, 1.0f), Mat(new Color(0.8f, 0.85f, 0.9f), 0.98f, 1));
        }
        Model("register", new Vector3(1.2f, 1.03f, D - 2.55f), 180);
        foreach (float sx in new[] { -1f, 1f }) if (Model("ottoman", new Vector3(sx * 1.6f, 0, D - 5.0f), 0) != null) { NoWalk(sx * 1.6f - 0.35f, D - 5.25f, sx * 1.6f + 0.35f, D - 4.75f); Seat(sx * 1.6f, D - 5.0f, Mathf.PI, 0.6f); }
        foreach (float sx in new[] { -1f, 1f }) if (Model("shelves", new Vector3(sx * 4.2f, 0, D - 0.3f), 90) != null) {
            NoWalk(sx * 4.2f - 0.55f, D - 0.55f, sx * 4.2f + 0.55f, D);
            for (int j = 0; j < 9; j++) Box(room.root, new Vector3(sx * 4.2f - 0.33f + (j % 3) * 0.33f, 0.22f + (j / 3) * 0.47f, D - 0.32f), new Vector3(0.26f, 0.12f, 0.22f), Mat(Color.HSVToRGB((j * 0.11f + sx * 0.3f + 1) % 1, 0.35f, 0.8f), 0.3f));
        }
        var r = new System.Random(room.p.id.GetHashCode());
        if (shoes) {      // a wall of trainers, lit shelf by shelf
            for (int s = 0; s < 5; s++) {
                foreach (float side in new[] { -1f, 1f }) {
                    Box(room.root, new Vector3(side * (W / 2 - 0.25f), 0.6f + s * 0.45f, D / 2 - 1), new Vector3(0.45f, 0.03f, D - 6), Mat(Color.white, 0.4f, 0, Color.white * 0.25f));
                    for (int i = 0; i < 16; i++) { var c = Color.HSVToRGB((float)r.NextDouble(), 0.3f + (float)r.NextDouble() * 0.6f, 0.5f + (float)r.NextDouble() * 0.5f); var sh = Part(room.root, PrimitiveType.Capsule, new Vector3(side * (W / 2 - 0.25f), 0.68f + s * 0.45f, 3 + i * 0.62f), new Vector3(0.11f, 0.15f, 0.11f), Mat(c, 0.5f), 0, 90); }
                }
            }
            NoWalk(-W / 2, 2, -W / 2 + 0.5f, D - 3); NoWalk(W / 2 - 0.5f, 2, W / 2, D - 3);
            for (int i = 0; i < 2; i++) { Solid(new Vector3(-1.8f + i * 3.6f, 0.4f, 7.5f), new Vector3(1.6f, 0.8f, 1.6f), Mat(C("#202020"), 0.6f)); for (int j = 0; j < 4; j++) Part(room.root, PrimitiveType.Capsule, new Vector3(-2.2f + i * 3.6f + (j % 2) * 0.7f, 0.86f, 7.2f + (j / 2) * 0.6f), new Vector3(0.12f, 0.16f, 0.12f), Mat(Color.HSVToRGB((float)r.NextDouble(), 0.7f, 0.9f), 0.5f), 0, 90); }
            foreach (float sx in new[] { -1.8f, 1.8f }) { if (Model("ottoman", new Vector3(sx, 0, 10.5f), 0) != null) { NoWalk(sx - 0.35f, 10.25f, sx + 0.35f, 10.75f); Seat(sx, 10.5f, 0, 0.6f); } else Chair(sx, 10.5f, 0, C("#202020")); }
        } else {
            // clothes rails down both walls, garments in the season's colours
            foreach (float side in new[] { -1f, 1f }) {
                float x = side * (W / 2 - 0.7f); Cyl(room.root, new Vector3(x, 1.75f, D / 2), 0.02f, D - 7, metal).localRotation = Quaternion.Euler(90, 0, 0);
                foreach (float z in new[] { 3.6f, D - 3.6f }) Cyl(room.root, new Vector3(x, 0.88f, z), 0.02f, 1.76f, metal);
                for (float z = 4; z < D - 4; z += 0.32f) { var c = Color.HSVToRGB((float)r.NextDouble(), 0.25f + (float)r.NextDouble() * 0.5f, 0.35f + (float)r.NextDouble() * 0.6f); Box(room.root, new Vector3(x, 1.2f - (float)r.NextDouble() * 0.25f, z), new Vector3(0.5f, 1.0f + (float)r.NextDouble() * 0.4f, 0.06f), Mat(c, 0.25f)); }
                NoWalk(x - 0.35f, 3.4f, x + 0.35f, D - 3.4f);
            }
            // a display table of folded pieces
            Solid(new Vector3(0, 0.42f, 7.5f), new Vector3(2.4f, 0.84f, 1.2f), Mat(C("#e9e2d6"), 0.5f));
            for (int i = 0; i < 6; i++) for (int j = 0; j < 3; j++) Box(room.root, new Vector3(-0.9f + i * 0.36f, 0.88f + j * 0.05f, 7.5f + ((i + j) % 2) * 0.2f - 0.1f), new Vector3(0.3f, 0.05f, 0.35f), Mat(Color.HSVToRGB(((i * 0.17f) + j * 0.05f) % 1, 0.4f, 0.8f), 0.3f));
            // dress forms in this season's looks
            var looks = A(data_town("mannequinLooks")); var brand = O(Get(data_town("BRANDS"), room.p.id));
            var bi = brand != null ? A(Get(brand, "items")).Select(O).ToList() : null;
            for (int m = 0; m < 4; m++) {
                var L = new Look3 { id = "olivia", skinTint = new Color(1.25f, 1.32f, 1.38f), signature = false, underwear = new Color(0.94f, 0.92f, 0.9f) };
                if (bi != null && bi.Count > 0) { var it = bi[m % bi.Count]; string id = Swap(S(Get(it, "item"))); if (id != null && itemById.ContainsKey(id)) L.worn.Add((itemById[id].slot, id, C(S(Get(it, "color"))))); }
                if (L.worn.Count == 0 && looks.Count > 0) { var lk = A(looks[(m + room.p.id.Length) % looks.Count]); var outfit = O(lk[0]); var colors = O(lk[1]); foreach (var kv in outfit) { string item = Swap(kv.Value as string); if (item == null || !itemById.ContainsKey(item)) continue; var col = colors != null && colors.TryGetValue(kv.Value as string, out var cv) ? (string)cv : itemById[item].color; L.worn.Add((itemById[item].slot, item, C(col))); } }
                float mx = m < 2 ? -2.6f + m * 5.2f : -2.6f + (m - 2) * 5.2f, mz = m < 2 ? 4.2f : 10.5f;
                Cyl(room.root, new Vector3(mx, 0.08f, mz), 0.45f, 0.16f, Mat(C("#f4f1ec"), 0.6f)); NoWalk(mx - 0.45f, mz - 0.45f, mx + 0.45f, mz + 0.45f);
                var go = new GameObject("Mannequin").transform; go.SetParent(room.root, false); var fig = stage3d.Figure(L, go, "Figure");
                foreach (Transform c in fig) { var part = c.name.Split('/').Last(); if (part == "head" || part == "hair" || part == "eyes" || part == "teeth" || part == "extra" || part == "brows" || part == "lashes") c.gameObject.SetActive(false); }
                go.localPosition = new Vector3(mx, 0.16f, mz); go.localRotation = Quaternion.Euler(0, 180, 0); go.localScale = Vector3.one * 0.98f;
            }
        }
        var arm = Prefab("Furniture/armchair");
        if (arm != null) { var a = Instantiate(arm, room.root, false).transform; a.localPosition = new Vector3(-W / 2 + 1.3f, 0, D - 2.2f); a.localRotation = Quaternion.Euler(0, 120, 0); FitHeight(a, 0.95f); NoWalk(-W / 2 + 0.6f, D - 2.9f, -W / 2 + 2, D - 1.5f); Seat(-W / 2 + 1.3f, D - 2.2f, 120 * Mathf.Deg2Rad, 0.62f); }
        Hire("Sales assistant", "front", 0, D - 1.7f, Mathf.PI, C("#151515"), null, true);
        Hire("Stylist", "floor", 1.5f, 6, 0, C("#151515"), null, true, null, false, new[] { -2.2f, 6.2f, 2.2f, 6.2f, 2.2f, 9.5f, -2.2f, 9.5f });
        Hire("Stock assistant", "floor", -3, 9, 0, main * 0.7f, null, false, null, false, new[] { -W / 2 + 1.6f, 5, -W / 2 + 1.6f, D - 4.5f, W / 2 - 1.6f, D - 4.5f, W / 2 - 1.6f, 5 });
    }

    void Jewels(Color main) {
        float W = room.w, D = room.d; var gold = Mat(new Color(0.85f, 0.7f, 0.4f), 0.9f, 1); var velvet = Mat(C("#2a1b3a"), 0.15f);
        // lit glass cases in a horseshoe, rings and necklaces sparkling inside
        var cases = new (float x, float z, float yaw)[] { (-4, 6, 90), (-4, 10, 90), (4, 6, 90), (4, 10, 90), (-1.6f, D - 3.5f, 0), (1.6f, D - 3.5f, 0) };
        var r = new System.Random(5);
        foreach (var (x, z, yaw) in cases) {
            Solid(new Vector3(x, 0.45f, z), yaw == 90 ? new Vector3(0.8f, 0.9f, 2.6f) : new Vector3(2.6f, 0.9f, 0.8f), velvet);
            Box(room.root, new Vector3(x, 0.92f, z), yaw == 90 ? new Vector3(0.82f, 0.04f, 2.62f) : new Vector3(2.62f, 0.04f, 0.82f), Mat(C("#efe9f5"), 0.4f, 0, C("#efe9f5") * 0.5f));
            Box(room.root, new Vector3(x, 1.12f, z), yaw == 90 ? new Vector3(0.8f, 0.36f, 2.6f) : new Vector3(2.6f, 0.36f, 0.8f), Glass());
            for (int i = 0; i < 7; i++) {
                float t = -1.05f + i * 0.35f; var at = yaw == 90 ? new Vector3(x, 0.98f, z + t) : new Vector3(x + t, 0.98f, z);
                if (i % 2 == 0) Part(room.root, PrimitiveType.Cylinder, at, new Vector3(0.09f, 0.008f, 0.09f), gold, 0, 90);
                Part(room.root, PrimitiveType.Sphere, at + Vector3.up * 0.03f, Vector3.one * 0.035f, Mat(Color.white, 1, 0, Color.HSVToRGB((float)r.NextDouble(), 0.3f, 1) * 2.5f));
            }
        }
        Box(room.root, new Vector3(0, 2.4f, D - 0.05f), new Vector3(5, 2.2f, 0.06f), Mat(main * 0.5f, 0.5f, 0.3f, main * 0.15f));
        Words(room.root, room.p.name.ToUpperInvariant(), new Vector3(0, 2.4f, D - 0.1f), 0.07f, new Color(0.95f, 0.85f, 0.6f));
        foreach (float sx in new[] { -1f, 1f }) Chair(sx * 1.6f, D - 5.2f, Mathf.PI, C("#2a1b3a"));
        Hire("Jeweller", "front", -1.6f, D - 2.7f, Mathf.PI, C("#151515"), null, true); Hire("Jeweller", "front", 1.6f, D - 2.7f, Mathf.PI, C("#151515"), null, true);
        Hire("Security", "front", 2.0f, 1.6f, -Mathf.PI / 2 - 0.3f, C("#1c1c22"), C("#1c1c22"));
    }

    void Books() {
        float W = room.w, D = room.d; var shelfM = Mat(Color.white, 0.25f, 0, null, Spines(), new Vector2(3, 2)); var wood = Mat(C("#5a3a24"), 0.35f);
        foreach (float side in new[] { -1f, 1f }) { var s = Solid(new Vector3(side * (W / 2 - 0.25f), 1.4f, D / 2 + 0.5f), new Vector3(0.5f, 2.8f, D - 4), shelfM, 0, 3); }
        Box(room.root, new Vector3(0, 1.4f, D - 0.25f), new Vector3(W - 1.2f, 2.8f, 0.5f), shelfM);
        for (int i = 0; i < 2; i++) { var s = Solid(new Vector3(-2.2f + i * 4.4f, 0.9f, 8.5f), new Vector3(0.6f, 1.8f, 4.5f), shelfM); }
        Counter(-W / 2 + 1, 0.5f, 3.2f, C("#5a3a24"), C("#2a1d14"), 1.0f, 0.7f);
        Solid(new Vector3(0, 0.4f, 13.5f), new Vector3(2.4f, 0.8f, 1.2f), wood);
        for (int i = 0; i < 8; i++) Box(room.root, new Vector3(-0.9f + i * 0.26f, 0.84f, 13.5f), new Vector3(0.18f, 0.06f, 0.26f), Mat(Color.HSVToRGB(i * 0.12f % 1, 0.5f, 0.75f), 0.3f));
        Model("register", new Vector3(-3.5f, 1.03f, 3.2f), 180);
        var arm = Prefab("Furniture/armchair2") ?? Prefab("Furniture/armchair");
        if (arm != null) foreach (var (x, z, yaw) in new[] { (-4.2f, 13.5f, 70f), (4.2f, 13.5f, -70f) }) { var a = Instantiate(arm, room.root, false).transform; a.localPosition = new Vector3(x, 0, z); a.localRotation = Quaternion.Euler(0, yaw, 0); FitHeight(a, 0.95f); NoWalk(x - 0.6f, z - 0.6f, x + 0.6f, z + 0.6f); Seat(x, z, yaw * Mathf.Deg2Rad, 0.62f); }
        Hire("Bookseller", "front", -3.5f, 3.9f, Mathf.PI, C("#3b5b3a"), null, true, null, false);
        Hire("Bookseller", "floor", 0, 6, 0, C("#3b5b3a"), null, false, null, false, new[] { 0f, 6f, 0f, 11f, -4.3f, 11f, -4.3f, 6.5f, 4.3f, 6.5f });
        Guest(-4.2f, 13.5f, 70 * Mathf.Deg2Rad, 0.55f);
    }

    void Bar(Color main) {
        float W = room.w, D = room.d; var neon = main;
        Counter(-4, 4, D - 3, C("#1a1420"), C("#c9a86a"), 1.1f, 0.7f);
        Box(room.root, new Vector3(0, 0.15f, D - 3.4f), new Vector3(8, 0.05f, 0.04f), Mat(neon, 0.5f, 0, neon * 3));
        for (int s = 0; s < 3; s++) { Box(room.root, new Vector3(0, 1.4f + s * 0.55f, D - 0.25f), new Vector3(8, 0.03f, 0.35f), Mat(Color.white, 0.6f, 0, neon * 0.6f)); for (int i = 0; i < 18; i++) Cyl(room.root, new Vector3(-3.8f + i * 0.45f, 1.58f + s * 0.55f, D - 0.25f), 0.045f, 0.32f, Mat(Color.HSVToRGB((i * 0.11f + s * 0.27f) % 1, 0.6f, 0.75f), 0.95f)); }
        for (int i = 0; i < 6; i++) Stool(-3.4f + i * 1.35f, D - 3.9f, 0, main);
        var sofa = Prefab("Furniture/sofa");
        foreach (var (x, z, yaw) in new[] { (-W / 2 + 1.0f, 6f, 90f), (-W / 2 + 1.0f, 10f, 90f), (W / 2 - 1.0f, 8f, -90f) }) {
            if (sofa != null) { var s = Instantiate(sofa, room.root, false).transform; s.localPosition = new Vector3(x, 0, z); s.localRotation = Quaternion.Euler(0, yaw, 0); FitHeight(s, 0.85f); }
            NoWalk(x - 0.6f, z - 1.2f, x + 0.6f, z + 1.2f); Seat(x, z - 0.5f, yaw * Mathf.Deg2Rad, 0.68f); Seat(x, z + 0.5f, yaw * Mathf.Deg2Rad, 0.68f);
            float tx = x + (yaw > 0 ? 1.3f : -1.3f); Cyl(room.root, new Vector3(tx, 0.22f, z), 0.4f, 0.44f, Mat(C("#1c1c1c"), 0.8f)); NoWalk(tx - 0.4f, z - 0.4f, tx + 0.4f, z + 0.4f);
            Cyl(room.root, new Vector3(tx, 0.5f, z), 0.04f, 0.12f, Mat(Color.white, 0.4f, 0, neon * 2));
        }
        foreach (float dz in new[] { -0.75f, 0.75f }) if (Model("lounge", new Vector3(2.6f, 0, 12 + dz), dz < 0 ? 0 : 180) != null) { NoWalk(2.2f, 12 + dz - 0.45f, 3.0f, 12 + dz + 0.45f); Seat(2.6f, 12 + dz, dz < 0 ? 0 : Mathf.PI, 0.6f); }
        Box(room.root, new Vector3(-W / 2 + 0.05f, 3.3f, D / 2), new Vector3(0.04f, 0.05f, D - 2), Mat(neon, 0.5f, 0, neon * 3)); Box(room.root, new Vector3(W / 2 - 0.05f, 3.3f, D / 2), new Vector3(0.04f, 0.05f, D - 2), Mat(neon, 0.5f, 0, neon * 3));
        Hire("Bartender", "front", -1.5f, D - 2.1f, Mathf.PI, C("#151515"), null, true); Hire("Bartender", "cook", 1.8f, D - 1.0f, 0, C("#151515"));
        Hire("Server", "floor", 0, 7, 0, C("#151515"), null, false, null, false, new[] { -3.5f, 6f, -3.5f, 10f, 3.5f, 8f, 0f, D - 4.6f });
        Guest(-W / 2 + 1.0f, 5.5f, Mathf.PI / 2, 0.62f); Guest(W / 2 - 1.0f, 8.5f, -Mathf.PI / 2, 0.62f); Guest(-1.4f, D - 3.9f, 0, 0.9f);
    }

    void Pamper(Color main) {
        float W = room.w, D = room.d; bool salon = room.kind == "salon";
        Counter(-W / 2 + 1, -1, 3.2f, C("#f4f1ec"), main, 1.0f, 0.7f);
        var arm = Prefab("Furniture/armchair");
        for (int i = 0; i < 4; i++) {
            float z = 6 + i * 2.6f, x = W / 2 - 1.2f;
            if (!salon || Model("mirror", new Vector3(W / 2 - 0.08f, 0.75f, z), -90) == null) {
                Box(room.root, new Vector3(W / 2 - 0.06f, 1.5f, z), new Vector3(0.03f, 1.4f, 1.0f), Mat(new Color(0.85f, 0.88f, 0.9f), 0.98f, 1));
                Box(room.root, new Vector3(W / 2 - 0.08f, 1.5f, z), new Vector3(0.04f, 1.5f, 1.1f), Mat(main, 0.4f));
            }
            if (salon) { if (Model("barber", new Vector3(x, 0, z), 90) == null) { Cyl(room.root, new Vector3(x, 0.25f, z), 0.25f, 0.5f, Mat(C("#1a1a1a"), 0.6f, 0.6f)); Box(room.root, new Vector3(x, 0.55f, z), new Vector3(0.6f, 0.12f, 0.6f), Mat(C("#2b2b2b"), 0.5f)); Box(room.root, new Vector3(x + 0.27f, 0.95f, z), new Vector3(0.08f, 0.8f, 0.6f), Mat(C("#2b2b2b"), 0.5f)); } }
            else { Solid(new Vector3(x - 0.6f, 0.38f, z), new Vector3(2.0f, 0.75f, 0.8f), Mat(Color.white, 0.3f)); Box(room.root, new Vector3(x - 0.6f, 0.78f, z), new Vector3(2.0f, 0.06f, 0.8f), Mat(C("#e8efe6"), 0.2f)); }
            NoWalk(x - 0.4f, z - 0.4f, x + 0.4f, z + 0.4f);
            if (i % 2 == 0) Guest(x, z, salon ? Mathf.PI / 2 : -Mathf.PI / 2, salon ? 0.62f : 0.55f);     // in the salon, facing the mirror
        }
        if (arm != null) foreach (float z in new[] { 6f, 8.5f }) { var a = Instantiate(arm, room.root, false).transform; a.localPosition = new Vector3(-W / 2 + 1.0f, 0, z); a.localRotation = Quaternion.Euler(0, 90, 0); FitHeight(a, 0.95f); NoWalk(-W / 2 + 0.4f, z - 0.6f, -W / 2 + 1.6f, z + 0.6f); Seat(-W / 2 + 1.0f, z, Mathf.PI / 2, 0.62f); }
        var plant = Prefab("Furniture/plant"); if (plant != null) for (int i = 0; i < 3; i++) { var p = Instantiate(plant, room.root, false).transform; p.localPosition = new Vector3(-W / 2 + 0.8f, 0, 11 + i * 1.6f); FitHeight(p, 1.6f); }
        Hire("Receptionist", "front", -3.5f, 3.9f, Mathf.PI, main, null, true);
        Hire(salon ? "Hair stylist" : "Therapist", "cook", W / 2 - 2.0f, 6, Mathf.PI / 2, salon ? C("#151515") : Color.white, null, true);
        Hire(salon ? "Hair stylist" : "Therapist", "cook", W / 2 - 2.0f, 11.2f, Mathf.PI / 2, salon ? C("#151515") : Color.white, null, true);
    }

    // ---------------------------------------------------------------- ordering at Restaurant Row
    void OpenEatery(SLTownWorld.Place p, string greet) {
        var e = EateryBy(p.id); float h = Hour; bool open = IsOpen(p, h); var friend = people.FirstOrDefault(x => x.state == "follow");
        var opts = new List<Opt>();
        foreach (var m in e.menu) {
            var fx = new Dictionary<string, object>(); foreach (var kv in m.fx.Split(',')) { var q = kv.Split(':'); fx[q[0]] = (double)float.Parse(q[1], CultureInfo.InvariantCulture); }
            var mm = m; opts.Add(new Opt { label = m.name, note = m.cost + " coins · " + (e.toGo ? "eat in" : "at your table") + " · " + m.min + " min", disabled = !open || profile.coins < m.cost,
                run = () => DoActivity(p, Act(mm.name, mm.min, fx, new Dictionary<string, object> { { "cost", (double)mm.cost }, { "busy", (e.kind == "coffee" ? "Sipping a " : "Enjoying the ") + mm.name } })) });
        }
        if (e.toGo && open) opts.Add(new Opt { label = "Order to go", note = "take it with you", run = () => OpenCard(e.name, "To go", "What can we get you?", e.menu.Select(m => new Opt { label = m.name, note = m.cost + " coins", disabled = profile.coins < m.cost, run = () => EateryToGo(e, m.name, m.cost, m.fx) }).ToList()) });
        if (room != null && room.p == p) opts.Add(new Opt { label = "Take a seat", run = () => SitAt(p.id) });
        var pals = Pals(p);
        if (open && pals.Count > 0) opts.Insert(0, new Opt { label = (e.toGo ? "Grab a bite with " : "Dinner with ") + Names(pals), note = "+social +hunger", accent = true, run = () => DoActivity(p, Act("Eating together", 60, Fx(("social", 35), ("hunger", 40), ("fun", 15)), new Dictionary<string, object> { { "cost", (double)(e.menu[0].cost * (pals.Count + 1)) }, { "busy", "Eating at " + e.name } }), pals) });
        if (open) opts.Add(new Opt { label = "Call your friends to meet here", note = "they’ll walk over", run = () => CallFriends(p) });
        bool dressy = e.kind == "fine" || e.kind == "steak" || e.kind == "sushi";
        string say = !open ? "Closed right now. Opens at " + HoursTxt(e.open[0], e.open[1]).Split('–')[0] + "." : e.blurb + (dressy ? " Dress code: elegant. Your look scores " + OutfitScore() + "/100." : "") + (friend != null ? " " + friend.First + " is with you." : "");
        OpenCard(e.name, e.sub + " · " + HoursTxt(e.open[0], e.open[1]), (greet != null ? greet + " " : "") + say, opts);
    }
    void EateryToGo(Eatery e, string name, int cost, string fx) {
        if (profile.coins < cost) { ShowToast("Not enough coins."); return; }
        core.Call("earn", (-cost).ToString(CultureInfo.InvariantCulture), "0");
        float food = 0; foreach (var kv in fx.Split(',')) { var q = kv.Split(':'); float v = float.Parse(q[1], CultureInfo.InvariantCulture); Bump(q[0], v); if (q[0] == "hunger") food = v; }
        if (food > 0) BodyChange(Me, food * 0.0012f, 0); if (bodyDirty) { bodyDirty = false; player.look = MyLook(null); Dress(player); }
        if (e.kind == "coffee") { CupOn(player, true); coffeeT = 90; }
        CloseCard(); UpdateHud(true); ShowToast(name + " to go. Enjoy!");
    }
}
