// Starlife Town's cars: real models (Resources/Cars, from Tools/blender/make_cars.py) parked along the kerbs, a limo
// outside the theatre, traffic that drives the roads on the right, turns at the junctions, keeps its distance and stops
// for anyone in its way, and Étoile Motors, where you test-drive, buy in the colour you like and drive your own car.
// Positions are in the town's coordinates (x, z, ry), like the rest of SLTown. Part of SLApp.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static SLJson;

public partial class SLApp {
    [Serializable] class CarSpec { public string id; public float length, width, height, wheel, wheelbase; }
    [Serializable] class CarSpecs { public CarSpec[] list; }
    sealed class Car {
        public CarSpec spec; public Transform go; public Transform[] wheels = new Transform[4]; public Quaternion[] wheelRest = new Quaternion[4]; public Material paint; public string hex;
        public float x, z, ry, v, steer, spin; public bool parked = true;
        // traffic
        public bool traffic; public int from, to; public List<Vector2> path = new List<Vector2>(); public float target = 9; public int holds = -1;
        public float Len => spec.length; public float Wid => spec.width;
    }
    static readonly (string id, string name, int price)[] Models = {
        ("compact", "Étoile Mini", 600), ("hatch", "Étoile City", 800), ("sedan", "Étoile Berline", 1000), ("wagon", "Étoile Break", 1100),
        ("van", "Étoile Family", 1300), ("coupe", "Étoile Coupé", 1400), ("offroad", "Étoile Trail", 1500), ("pickup", "Étoile Ranch", 1600),
        ("suv", "Étoile Grand", 1800), ("sport", "Étoile Comète", 3000), ("limo", "Étoile Limousine", 4500) };
    static readonly (string name, string hex)[] Paints = {
        ("Pearl white", "#f2efe8"), ("Midnight black", "#151418"), ("Ruby red", "#a3122e"), ("Rose gold", "#d9a39a"), ("Sapphire", "#24407a"),
        ("Champagne", "#d8c39a"), ("Emerald", "#1f6b4f"), ("Silver", "#b9bcc2"), ("Candy pink", "#f07aa8"), ("Lilac", "#a98ad6") };
    static string ModelName(string id) => Models.FirstOrDefault(m => m.id == id).name ?? id;

    readonly List<Car> cars = new List<Car>(); Car myCar, testCar; Light[] headlights; Label speedL;
    Dictionary<string, CarSpec> carSpecs; Material carGlass; readonly Dictionary<string, (Material lights, Material wheel, Texture2D paint, Color mean, float lum)> carMats = new Dictionary<string, (Material, Material, Texture2D, Color, float)>();
    float[] roads; int nRoads; readonly HashSet<(int, int)> noRoad = new HashSet<(int, int)>(); readonly Dictionary<int, Car> junctionHeld = new Dictionary<int, Car>();
    Vector2 dealer; float dealerRy;

    // ---------------------------------------------------------------- models
    Car MakeCar(string id, string hex, float x, float z, float ry) {
        if (carSpecs == null) {
            var ta = Resources.Load<TextAsset>("Cars/cars"); carSpecs = new Dictionary<string, CarSpec>();
            if (ta != null) foreach (var c in JsonUtility.FromJson<CarSpecs>("{\"list\":" + ta.text + "}").list) carSpecs[c.id] = c;
            carGlass = new Material(Resources.Load<Shader>("SLTownClear")); carGlass.SetColor("_Color", new Color(0.07f, 0.09f, 0.11f, 0.62f)); carGlass.SetFloat("_Glossiness", 0.96f); carGlass.SetFloat("_Metallic", 0.3f);
        }
        var pf = Resources.Load<GameObject>("Cars/" + id); if (pf == null || !carSpecs.TryGetValue(id, out var spec)) return null;
        if (!carMats.TryGetValue(id, out var cm)) {
            var lit = Resources.Load<Shader>("SLTown"); var pt = Resources.Load<Texture2D>("Cars/" + id + "_paint");
            var lm = new Material(lit); lm.SetTexture("_MainTex", Resources.Load<Texture2D>("Cars/" + id + "_lights")); lm.SetTexture("_EmissionMap", lm.GetTexture("_MainTex"));
            lm.SetColor("_EmissionColor", new Color(0.9f, 0.85f, 0.75f)); lm.SetFloat("_EmA", 0.12f); lm.SetFloat("_EmB", 1.3f); lm.SetFloat("_Glossiness", 0.8f);
            var wm = new Material(lit); wm.SetTexture("_MainTex", Resources.Load<Texture2D>("Cars/" + id + "_wheel")); wm.SetFloat("_Glossiness", 0.35f); wm.SetFloat("_Metallic", 0.3f);
            Color mean = new Color(0.5f, 0.5f, 0.5f); float lum = 0.5f;
            if (pt != null && pt.isReadable) { var px = pt.GetPixels(Mathf.Min(4, pt.mipmapCount - 1)); Color s = Color.black; foreach (var p in px) s += p; mean = s / px.Length; lum = Mathf.Max(0.04f, mean.r * 0.299f + mean.g * 0.587f + mean.b * 0.114f); }
            carMats[id] = cm = (lm, wm, pt, mean, lum);
        }
        var car = new Car { spec = spec, hex = hex, x = x, z = z, ry = ry };
        car.go = Instantiate(pf, town.root).transform; car.go.name = "Car " + id;
        car.paint = new Material(Resources.Load<Shader>("StarletTint"));
        car.paint.SetTexture("_MainTex", cm.paint); car.paint.SetFloat("_Recolor", 1); car.paint.SetFloat("_Gain", Mathf.Min(0.8f / cm.lum, 5f)); car.paint.SetFloat("_Lift", 0f);
        car.paint.SetColor("_Mean", cm.mean); car.paint.SetFloat("_Keep", 1); car.paint.SetFloat("_Glossiness", 0.62f); car.paint.SetFloat("_Sheen", 0.05f); car.paint.SetFloat("_Cull", 2);
        Paint(car, hex);
        foreach (var r in car.go.GetComponentsInChildren<Renderer>()) {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) {
                string n = ms[i] != null ? ms[i].name : "";
                ms[i] = n.StartsWith("Paint") ? car.paint : n.StartsWith("Glass") ? carGlass : n.StartsWith("Lights") ? cm.lights : cm.wheel;
            }
            r.sharedMaterials = ms; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        string[] wn = { "wheel_fl", "wheel_fr", "wheel_rl", "wheel_rr" };
        for (int i = 0; i < 4; i++) { car.wheels[i] = car.go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == wn[i]); if (car.wheels[i] != null) car.wheelRest[i] = car.wheels[i].localRotation; }
        SLTownWorld.SetLayer(car.go); PlaceCar(car); cars.Add(car);
        return car;
    }
    void Paint(Car car, string hex) { car.hex = hex; car.paint.SetColor("_Tint", C(hex)); }
    void PlaceCar(Car c) {
        c.go.position = SLTownWorld.W(c.x, town.HeightAt(c.x, c.z), c.z); c.go.rotation = SLTownWorld.Yaw(c.ry);
        for (int i = 0; i < 4; i++) {
            if (c.wheels[i] == null) continue;
            c.wheels[i].localRotation = Quaternion.Euler(0, i < 2 ? c.steer * Mathf.Rad2Deg : 0, 0) * Quaternion.Euler(c.spin * Mathf.Rad2Deg, 0, 0) * c.wheelRest[i];
        }
    }

    // ---------------------------------------------------------------- setting the streets up
    void SetUpCars() {
        foreach (var c in cars) Destroy(c.go.gameObject); cars.Clear(); myCar = testCar = null; junctionHeld.Clear();
        if (Resources.Load<TextAsset>("Cars/cars") == null) return;
        var rnd = new System.Random(77);
        string[] everyday = { "compact", "hatch", "sedan", "wagon", "van", "suv", "offroad", "pickup", "coupe" };
        // parked along the kerbs where the web town had its parked cars
        foreach (var o in A(data_town("parked"))) {
            var f = A(o); MakeCar(everyday[rnd.Next(everyday.Length)], Paints[rnd.Next(Paints.Length)].hex, F(f[0]), F(f[1]), F(f[2]) + (rnd.Next(2) == 0 ? 0 : Mathf.PI));
        }
        // a limousine waiting outside the theatre, and one outside the Grand Hall
        foreach (var (pid, hex) in new[] { ("theatre", "#151418"), ("hall", "#f2efe8") }) {
            var p = PlaceBy(pid); if (p == null) continue; var kerb = Kerb(p.x, p.z, p.ry, 4.9f);
            ClearParked(kerb, 7); MakeCar("limo", hex, kerb.x, kerb.y, p.ry + Mathf.PI / 2);
        }
        // the roads: a grid between the blocks; the Avenue is car-free
        nRoads = 2 * town.N + 2; roads = new float[nRoads]; for (int k = 0; k < nRoads; k++) roads[k] = (k - town.N - 1) * town.Cell + town.Cell / 2;
        var ave = O(data_town("AVE")); noRoad.Clear();
        for (int i = 0; i < nRoads; i++) for (int j = 0; j < nRoads; j++) {
            float x = roads[i], z = roads[j];
            if (x > F(ave["x0"]) - 1 && x < F(ave["x1"]) + 1 && z > F(ave["z0"]) - 8 && z < F(ave["z1"]) + 8) noRoad.Add((i, j));
        }
        BuildSignals();
        // Étoile Motors, in the shops north of Central Park's corner, with three cars in the window bay
        dealer = new Vector2(town.Cell, -town.Cell + (town.Half - 3) + 0.8f); dealerRy = 0;
        if (PlaceBy("motors") == null) {
            var mp = new SLTownWorld.Place { id = "motors", name = "Étoile Motors", sub = "Car showroom", color = "#e2c47a", x = dealer.x, z = dealer.y, ry = dealerRy, open = new[] { 9, 20 }, node = town.NearestNode(dealer.x, dealer.y) };
            town.places.Add(mp);
        }
        DealerSign();
        var bay = Kerb(dealer.x, dealer.y, dealerRy, 4.4f); ClearParked(bay, 14);
        string[] show = { "sport", "suv", "coupe" };
        for (int i = 0; i < 3; i++) { var c = MakeCar(show[i], Paints[i * 3 + 2].hex, bay.x - 7 + i * 7, bay.y, Mathf.PI / 2); if (c != null) c.go.name = "Showroom " + show[i]; }
        // your own car, where you left it
        var mine = core.Pref("town.car").Split(':');
        if (mine.Length == 2) {
            var at = core.Pref("town.carpos").Split(','); Vector2 home = HomeBay(); float ry = PlaceBy("home").ry + Mathf.PI / 2;
            if (at.Length == 3) { home = new Vector2(Num0(at[0], home.x), Num0(at[1], home.y)); ry = Num0(at[2], ry); }
            ClearParked(home, 6); myCar = MakeCar(mine[0], mine[1], home.x, home.y, ry);
        }
        // traffic
        int made = 0;
        for (int tries = 0; made < 14 && tries < 200; tries++) {
            int i = rnd.Next(nRoads), j = rnd.Next(nRoads); bool horiz = rnd.Next(2) == 0;
            int i2 = horiz ? i + (rnd.Next(2) == 0 ? 1 : -1) : i, j2 = horiz ? j : j + (rnd.Next(2) == 0 ? 1 : -1);
            if (!Edge(i, j, i2, j2)) continue;
            var c = MakeCar(everyday[rnd.Next(everyday.Length)], Paints[rnd.Next(Paints.Length)].hex, 0, 0, 0); if (c == null) break;
            c.traffic = true; c.parked = false; c.from = Node(i, j); c.to = Node(i2, j2); c.path = Lane(c.from, c.to);
            float s = (float)rnd.NextDouble(); var p0 = c.path[0]; var p1 = c.path[c.path.Count - 1]; var at = Vector2.Lerp(p0, p1, s);
            if (cars.Any(o => o != c && o.traffic && (new Vector2(o.x, o.z) - at).magnitude < 12)) { Destroy(c.go.gameObject); cars.Remove(c); continue; }
            c.x = at.x; c.z = at.y; c.path = new List<Vector2> { p1 }; c.ry = Mathf.Atan2(p1.x - p0.x, p1.y - p0.y); c.target = 8 + (float)rnd.NextDouble() * 3; PlaceCar(c); made++;
        }
    }
    Vector2 Kerb(float x, float z, float ry, float d) => new Vector2(x + Mathf.Sin(ry) * d, z + Mathf.Cos(ry) * d);
    Vector2 HomeBay() { var h = PlaceBy("home"); return Kerb(h.x, h.z, h.ry, 4.6f) + new Vector2(Mathf.Cos(h.ry), -Mathf.Sin(h.ry)) * 5; }
    void ClearParked(Vector2 at, float r) { foreach (var c in cars.Where(c => c.parked && !c.traffic && c != myCar && (new Vector2(c.x, c.z) - at).magnitude < r).ToList()) { Destroy(c.go.gameObject); cars.Remove(c); } }
    void DealerSign() {
        var old = town.root.Find("Etoile Motors sign"); if (old != null) Destroy(old.gameObject);
        var sign = new GameObject("Etoile Motors sign").transform; sign.SetParent(town.root, false);
        var back = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(back.GetComponent<Collider>()); back.transform.SetParent(sign, false);
        back.transform.localScale = new Vector3(9.5f, 1.3f, 0.12f); var bm = new Material(Resources.Load<Shader>("SLTown")); bm.SetColor("_Color", new Color(0.09f, 0.08f, 0.1f)); bm.SetFloat("_Glossiness", 0.6f); back.GetComponent<Renderer>().sharedMaterial = bm;
        var txt = new GameObject("text").AddComponent<TextMesh>(); txt.transform.SetParent(sign, false); txt.transform.localPosition = new Vector3(0, 0, 0.08f); txt.transform.localRotation = Quaternion.Euler(0, 180, 0);
        txt.font = Resources.Load<Font>("Fonts/DMSans-Bold"); txt.text = "ÉTOILE  MOTORS"; txt.fontSize = 120; txt.characterSize = 0.065f; txt.anchor = TextAnchor.MiddleCenter; txt.alignment = TextAlignment.Center; txt.color = new Color(0.89f, 0.77f, 0.48f);
        var tr = txt.GetComponent<MeshRenderer>(); tr.sharedMaterial = txt.font.material;
        sign.position = SLTownWorld.W(dealer.x, 5.0f, dealer.y + 0.85f); sign.rotation = SLTownWorld.Yaw(dealerRy);
        SLTownWorld.SetLayer(sign);
    }

    // ---------------------------------------------------------------- traffic lights
    // Junctions where three or four roads meet have lights: north-south and east-west take turns (11 s green, 2 s amber),
    // each junction offset from its neighbours. A pole on the near right of every approach faces the drivers.
    sealed class Signal { public int node; public Renderer[][] lamps = new Renderer[4][]; public int[] dirAxis = new int[4]; public int shown = -1; }
    readonly List<Signal> signals = new List<Signal>(); Material[] lampOn, lampOff; Transform signalRoot;
    const float Cycle = 26;
    /// <summary>0 green, 1 amber, 2 red for traffic moving along an axis (0: east-west, 1: north-south) at a junction.</summary>
    int LightFor(int node, int axis) {
        var (i, j) = IJ(node); float t = Mathf.Repeat(Time.time + (i + j) * 4.3f + i * 1.7f, Cycle);
        int ns = t < 11 ? 0 : t < 13 ? 1 : 2, ew = t < 13 ? 2 : t < 24 ? 0 : t < 26 ? 1 : 2;
        return axis == 1 ? ns : ew;
    }
    int Arms(int i, int j) { int n = 0; foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) if (Edge(i, j, i + di, j + dj)) n++; return n; }
    void BuildSignals() {
        if (signalRoot != null) Destroy(signalRoot.gameObject); signals.Clear();
        signalRoot = new GameObject("Traffic lights").transform; signalRoot.SetParent(town.root, false);
        var lit = Resources.Load<Shader>("SLTown");
        Material M(Color c, float glow) { var m = new Material(lit); m.SetColor("_Color", c); m.SetFloat("_Glossiness", 0.6f); if (glow > 0) { m.SetColor("_EmissionColor", c); m.SetFloat("_EmA", glow); m.SetFloat("_EmB", glow * 0.8f); } return m; }
        Color[] cols = { new Color(0.15f, 1f, 0.45f), new Color(1f, 0.7f, 0.1f), new Color(1f, 0.12f, 0.1f) };
        lampOn = cols.Select(c => M(c, 2.2f)).ToArray(); lampOff = cols.Select(c => M(c * 0.18f, 0)).ToArray();
        var metal = M(new Color(0.12f, 0.12f, 0.13f), 0); metal.SetFloat("_Metallic", 0.6f);
        GameObject Prim(PrimitiveType t, Transform p, Vector3 at, Vector3 size, Material m) { var g = GameObject.CreatePrimitive(t); Destroy(g.GetComponent<Collider>()); g.transform.SetParent(p, false); g.transform.localPosition = at; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = m; return g; }
        for (int i = 0; i < nRoads; i++) for (int j = 0; j < nRoads; j++) {
            if (noRoad.Contains((i, j)) || Arms(i, j) < 3) continue;
            var sg = new Signal { node = Node(i, j) }; var c = NodePos(sg.node); int k = 0;
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) {
                if (!Edge(i, j, i - di, j - dj)) { k++; continue; }           // no road arriving from that side
                var d = new Vector2(di, dj);                                   // the direction traffic arrives in
                var at = c - d * (Stop + 0.6f) + Right(d) * 6.6f;
                var pole = new GameObject("Light").transform; pole.SetParent(signalRoot, false);
                pole.position = SLTownWorld.W(at.x, 0.15f, at.y); pole.rotation = SLTownWorld.Yaw(Mathf.Atan2(-d.x, -d.y));
                Prim(PrimitiveType.Cylinder, pole, new Vector3(0, 1.6f, 0), new Vector3(0.11f, 1.6f, 0.11f), metal);
                Prim(PrimitiveType.Cube, pole, new Vector3(0, 3.55f, 0), new Vector3(0.34f, 0.95f, 0.26f), metal);
                var lamps = new Renderer[3];
                for (int l = 0; l < 3; l++) lamps[l] = Prim(PrimitiveType.Sphere, pole, new Vector3(0, 3.25f + (2 - l) * 0.3f, 0.13f), new Vector3(0.2f, 0.2f, 0.08f), lampOff[l]).GetComponent<Renderer>();
                sg.lamps[k] = lamps; sg.dirAxis[k] = di != 0 ? 0 : 1; k++;
            }
            signals.Add(sg);
        }
        SLTownWorld.SetLayer(signalRoot);
    }
    void TickSignals() {
        foreach (var sg in signals) {
            int key = LightFor(sg.node, 0) * 3 + LightFor(sg.node, 1); if (key == sg.shown) continue; sg.shown = key;
            for (int k = 0; k < 4; k++) { var lamps = sg.lamps[k]; if (lamps == null) continue; int st = LightFor(sg.node, sg.dirAxis[k]); for (int l = 0; l < 3; l++) lamps[l].sharedMaterial = l == st ? lampOn[l] : lampOff[l]; }
        }
    }
    readonly HashSet<int> warnedRed = new HashSet<int>();
    /// <summary>Driving yourself: crossing a stop line on red earns a word of warning (once per junction per pass).</summary>
    void CheckRedLight(Car c, float ox, float oz) {
        if (signals.Count == 0 || Mathf.Abs(c.v) < 3) return;
        foreach (var sg in signals) {
            var p = NodePos(sg.node); var before = new Vector2(ox, oz) - p; var after = new Vector2(c.x, c.z) - p;
            foreach (int axis in new[] { 0, 1 }) {
                float b0 = axis == 0 ? before.x : before.y, a0 = axis == 0 ? after.x : after.y, side = axis == 0 ? Mathf.Abs(after.y) : Mathf.Abs(after.x);
                if (side > 6 || Mathf.Abs(b0) < Stop - 0.5f == Mathf.Abs(a0) < Stop - 0.5f || Mathf.Abs(a0) > Stop) continue;      // not crossing this junction's stop line along this axis
                if (Mathf.Abs(a0) < Mathf.Abs(b0) && LightFor(sg.node, axis) == 2 && warnedRed.Add(sg.node)) ShowToast("You ran a red light! Careful on the roads of Starlife.");
            }
            if ((new Vector2(c.x, c.z) - p).magnitude > 20) warnedRed.Remove(sg.node);
        }
    }

    // ---------------------------------------------------------------- the road grid and its lanes
    int Node(int i, int j) => i * 100 + j;
    (int, int) IJ(int n) => (n / 100, n % 100);
    Vector2 NodePos(int n) { var (i, j) = IJ(n); return new Vector2(roads[i], roads[j]); }
    bool Edge(int i, int j, int i2, int j2) => i >= 0 && j >= 0 && i2 >= 0 && j2 >= 0 && i < nRoads && j < nRoads && i2 < nRoads && j2 < nRoads && !noRoad.Contains((i, j)) && !noRoad.Contains((i2, j2)) && (i == i2) != (j == j2) && Mathf.Abs(i - i2) + Mathf.Abs(j - j2) == 1;
    static Vector2 Right(Vector2 d) => new Vector2(-d.y, d.x);     // driving on the right: in the town's axes (x east, z south)
    const float LaneOff = 1.8f, Stop = 7.5f;
    List<Vector2> Lane(int a, int b) { Vector2 pa = NodePos(a), pb = NodePos(b), d = (pb - pa).normalized, r = Right(d) * LaneOff; return new List<Vector2> { pa + d * Stop + r, pb - d * Stop + r }; }
    List<Vector2> TurnPath(int a, int b, int c) {
        Vector2 pb = NodePos(b), d = (pb - NodePos(a)).normalized, e = (NodePos(c) - pb).normalized;
        Vector2 p0 = pb - d * Stop + Right(d) * LaneOff, p2 = pb + e * Stop + Right(e) * LaneOff, ctl = Mathf.Abs(Vector2.Dot(d, e)) > 0.9f ? (p0 + p2) / 2 : pb + Right(d) * LaneOff + Right(e) * LaneOff;
        var pts = new List<Vector2>(); for (int k = 1; k <= 6; k++) { float t = k / 6f; pts.Add((1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * ctl + t * t * p2); }
        return pts;
    }
    int NextNode(Car c) {
        var (i, j) = IJ(c.to); var opts = new List<int>();
        foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int n = Node(i + di, j + dj); if (n != c.from && Edge(i, j, i + di, j + dj)) opts.Add(n); }
        if (opts.Count == 0) return c.from;
        return opts[trng.Next(opts.Count)];
    }

    // ---------------------------------------------------------------- traffic
    bool trafficPaused, showCam;
    void TickTraffic(float dt) {
        foreach (var c in cars) {
            if (!c.traffic) continue;
            if (trafficPaused) { c.v = Mathf.MoveTowards(c.v, 0, dt * 10); MoveCar(c, dt); continue; }
            if (c.path.Count == 0) {
                if (c.holds >= 0 && junctionHeld.TryGetValue(c.holds, out var h) && h == c) junctionHeld.Remove(c.holds); c.holds = -1;
                // at the stop line: wait for the junction, then turn into the next road
                int n = NextNode(c);
                var (fi, fj) = IJ(c.from); var (ti, tj) = IJ(c.to); int axis = fi != ti ? 0 : 1;
                bool red = signals.Exists(sg => sg.node == c.to) && LightFor(c.to, axis) != 0;
                if (red || junctionHeld.TryGetValue(c.to, out var other) && other != c) { c.v = Mathf.MoveTowards(c.v, 0, dt * 12); MoveCar(c, dt); continue; }
                junctionHeld[c.to] = c; c.holds = c.to;
                var turn = TurnPath(c.from, c.to, n); var lane = Lane(c.to, n);
                c.path.AddRange(turn); c.path.Add(lane[1]); c.from = c.to; c.to = n;
            }
            var w = c.path[0]; var pos = new Vector2(c.x, c.z);
            if ((w - pos).magnitude < 1.2f) { c.path.RemoveAt(0); if (c.holds >= 0 && c.path.Count == 1 && junctionHeld.TryGetValue(c.holds, out var h2) && h2 == c) { junctionHeld.Remove(c.holds); c.holds = -1; } continue; }
            // steer toward the next point; slow for turns; keep a distance from anything ahead
            float want = Mathf.Atan2(w.x - c.x, w.y - c.z), turnBy = Mathf.Abs(Mathf.DeltaAngle(c.ry * Mathf.Rad2Deg, want * Mathf.Rad2Deg));
            c.ry = Turn(c.ry, want, dt * Mathf.Clamp(c.v / 3.5f, 0.6f, 3f));
            float target = c.target * (turnBy > 20 ? 0.5f : 1) * (c.path.Count > 1 ? 0.62f : 1);
            float gap = Ahead(c, 11f, 1.7f);
            if (gap < 11) target = Mathf.Min(target, Mathf.Max(0, (gap - 4.5f) * 1.6f));
            c.v = Mathf.MoveTowards(c.v, target, dt * (target < c.v ? 9 : 3.2f));
            MoveCar(c, dt);
        }
    }
    void MoveCar(Car c, float dt) {
        c.x += Mathf.Sin(c.ry) * c.v * dt; c.z += Mathf.Cos(c.ry) * c.v * dt;
        c.spin += c.v * dt / Mathf.Max(0.2f, c.spec.wheel); c.spin %= Mathf.PI * 2; PlaceCar(c);
    }
    /// <summary>How far ahead, along the car's heading, the nearest person or car is (or `look` if nothing is).</summary>
    float Ahead(Car c, float look, float half) {
        float best = look; var f = new Vector2(Mathf.Sin(c.ry), Mathf.Cos(c.ry)); var r = new Vector2(f.y, -f.x); var p = new Vector2(c.x, c.z);
        void Check(Vector2 q, float size) { var d = q - p; float along = Vector2.Dot(d, f), side = Mathf.Abs(Vector2.Dot(d, r)); if (along > 0 && along < best && side < half + size) best = along; }
        foreach (var o in cars) if (o != c && o.go.gameObject.activeSelf) Check(new Vector2(o.x, o.z) - f * (o.Len * 0.5f), o.Wid * 0.5f);
        if (player != null && driving == null) Check(new Vector2(player.pos.x, player.pos.z), 0.4f);
        foreach (var pp in people) if (pp.visible && pp.state != "inside") Check(new Vector2(pp.pos.x, pp.pos.z), 0.4f);
        return best - c.Len * 0.5f;
    }
    /// <summary>Walking into a car: the player (or a starlet) is kept outside its outline.</summary>
    Vector2 KeepOutOfCars(Vector2 p, float r) {
        foreach (var c in cars) {
            if (c == driving) continue;
            var f = new Vector2(Mathf.Sin(c.ry), Mathf.Cos(c.ry)); var s = new Vector2(f.y, -f.x); var d = p - new Vector2(c.x, c.z);
            float a = Vector2.Dot(d, f), b = Vector2.Dot(d, s), ha = c.Len / 2 + r, hb = c.Wid / 2 + r;
            if (Mathf.Abs(a) >= ha || Mathf.Abs(b) >= hb) continue;
            if (ha - Mathf.Abs(a) < hb - Mathf.Abs(b)) a = Mathf.Sign(a) * ha; else b = Mathf.Sign(b) * hb;
            p = new Vector2(c.x, c.z) + f * a + s * b;
        }
        return p;
    }

    // ---------------------------------------------------------------- driving
    Car driving;
    void GetIn(Car c) {
        if (c == null) return;
        CloseCard(); StandUp(); driving = c; c.parked = false; player.go.gameObject.SetActive(false);
        foreach (var x in people) if (x.state == "follow") x.state = "idle";
        cam.dist = 7.5f; cam.pitch = 0.2f; cam.yaw = c.ry + Mathf.PI; cam.cur = 0;
        if (headlights == null) {
            headlights = new Light[2];
            for (int i = 0; i < 2; i++) { var l = new GameObject("Headlight").AddComponent<Light>(); l.type = LightType.Spot; l.range = 32; l.spotAngle = 62; l.color = new Color(1f, 0.95f, 0.85f); l.shadows = LightShadows.None; l.cullingMask = 1 << SLTownWorld.Layer; headlights[i] = l; }
        }
        for (int i = 0; i < 2; i++) { var l = headlights[i]; l.transform.SetParent(c.go, false); l.transform.localPosition = new Vector3(i == 0 ? -0.6f : 0.6f, 0.7f, c.Len / 2 - 0.2f); l.transform.localRotation = Quaternion.Euler(8, 0, 0); l.gameObject.layer = SLTownWorld.Layer; }
        if (speedL == null) { speedL = L("", "town-speed glass"); townUI.Add(speedL); }
        speedL.style.display = DisplayStyle.Flex;
        ShowToast(c == testCar ? "Test drive: enjoy the " + ModelName(c.spec.id) + "! Press E to bring it back." : "Press E to get out. Arrows or WASD drive, Space brakes.");
    }
    void GetOut() {
        var c = driving; if (c == null) return; driving = null; c.v = 0; c.parked = true;
        var side = new Vector2(-Mathf.Cos(c.ry), Mathf.Sin(c.ry)) * (c.Wid / 2 + 0.7f);
        var p = town.Collide(new Vector2(c.x, c.z) + side); p = KeepOutOfCars(p, 0.3f);
        player.pos = new Vector3(p.x, town.HeightAt(p.x, p.y), p.y); player.ry = c.ry; player.go.gameObject.SetActive(true); Place(player);
        foreach (var l in headlights) l.enabled = false;
        speedL.style.display = DisplayStyle.None; cam.dist = 5.2f; cam.cur = 0;
        if (c == testCar) {   // the test-drive car goes back to the showroom
            Destroy(c.go.gameObject); cars.Remove(c); testCar = null; ShowToast("Thanks for the test drive! The car is back at Étoile Motors.");
        } else if (c == myCar) core.SetPref("town.carpos", Nums(new[] { c.x, c.z, c.ry }));
    }
    void TickDriving(float dt) {
        var c = driving; float ix = 0, iz = 0;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) iz += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) iz -= 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ix -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ix += 1;
        if (ix == 0 && iz == 0) {
            float px = Input.GetAxis("Horizontal"), pz = Input.GetAxis("Vertical"); if (new Vector2(px, pz).magnitude > 0.2f) { ix = px; iz = pz; }
            if (joy != null && joy.Value.magnitude > 0.12f) { ix = joy.Value.x; iz = joy.Value.y; }
        }
        if (autoWalk != null) { ix = autoWalk.Value.x; iz = autoWalk.Value.y; }
        bool brake = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton2);
        if (modal != null) { ix = 0; iz = 0; brake = true; }
        // a light, forgiving car: throttle, brakes, reverse, steering that tightens at low speed
        float max = c.spec.id == "sport" ? 22 : c.spec.id == "limo" || c.spec.id == "van" ? 14 : 17;
        if (brake) c.v = Mathf.MoveTowards(c.v, 0, dt * 14);
        else if (iz > 0) c.v = Mathf.MoveTowards(c.v, max * iz, dt * (c.v < 0 ? 12 : 5.5f));
        else if (iz < 0) c.v = Mathf.MoveTowards(c.v, c.v > 0.5f ? 0 : -5 * -iz, dt * (c.v > 0.5f ? 12 : 4));
        else c.v = Mathf.MoveTowards(c.v, 0, dt * 2.2f);
        c.steer = Mathf.MoveTowards(c.steer, ix * 0.55f, dt * 2.6f);
        float wb = Mathf.Max(1.8f, c.spec.wheelbase > 0 ? c.spec.wheelbase : c.Len * 0.6f);
        c.ry += c.v / wb * Mathf.Tan(c.steer) * dt;
        float ox = c.x, oz = c.z;
        c.x += Mathf.Sin(c.ry) * c.v * dt; c.z += Mathf.Cos(c.ry) * c.v * dt;
        // buildings, kerb furniture and other cars stop the car; people are never run over
        var f = new Vector2(Mathf.Sin(c.ry), Mathf.Cos(c.ry)); bool hit = false;
        foreach (float k in new[] { 0.45f, 0f, -0.45f }) {
            var q = new Vector2(c.x, c.z) + f * (c.Len * k); var q2 = town.Collide(q, c.Wid * 0.45f);
            if ((q2 - q).sqrMagnitude > 1e-6f) { hit = true; c.x += q2.x - q.x; c.z += q2.y - q.y; }
        }
        // the pavements, parks and plazas of every block (and the car-free Avenue) are off-limits to cars
        for (int pi = 0; pi < 3; pi++) {
            float kk = new[] { 0.45f, 0f, -0.45f }[pi]; var q = new Vector2(c.x, c.z) + f * (c.Len * kk); var q2 = KerbOut(q, c.Wid * 0.5f);
            if ((q2 - q).sqrMagnitude > 1e-6f) { hit = true; c.x += q2.x - q.x; c.z += q2.y - q.y; }
        }
        foreach (var o in cars) { if (o == c) continue; if ((new Vector2(o.x - c.x, o.z - c.z)).magnitude < (o.Len + c.Len) * 0.45f && Vector2.Dot(new Vector2(o.x - c.x, o.z - c.z), f * Mathf.Sign(c.v)) > 0) { hit = true; c.x = ox; c.z = oz; o.v = 0; } }
        if (Mathf.Abs(c.v) > 0.5f) {
            float gapFront = 9; var p = new Vector2(c.x, c.z); var dir = f * Mathf.Sign(c.v);
            foreach (var pp in people) if (pp.visible && pp.state != "inside") { var d = new Vector2(pp.pos.x, pp.pos.z) - p; float a = Vector2.Dot(d, dir), s = Mathf.Abs(d.x * dir.y - d.y * dir.x); if (a > 0 && s < c.Wid * 0.5f + 0.5f) gapFront = Mathf.Min(gapFront, a - c.Len * 0.5f); }
            if (gapFront < 0.8f) { c.x = ox; c.z = oz; c.v = 0; if (Time.time - lastHonk > 4) { lastHonk = Time.time; ShowToast("Careful! Someone is in front of the car."); } }
        }
        if (hit) c.v *= -0.25f;
        CheckRedLight(c, ox, oz);
        c.spin += c.v * dt / Mathf.Max(0.2f, c.spec.wheel); c.spin %= Mathf.PI * 2;
        PlaceCar(c);
        player.pos = new Vector3(c.x, town.HeightAt(c.x, c.z), c.z); player.ry = c.ry; player.speed = 0;
        foreach (var l in headlights) { l.enabled = town.NightNow > 0.15f; l.intensity = 2.6f * town.NightNow; }
        speedL.text = Mathf.RoundToInt(Mathf.Abs(c.v) * 3.6f) + " km/h";
    }
    float lastHonk;
    Vector2 KerbOut(Vector2 p, float r) {
        void Push(float x0, float z0, float x1, float z1) {
            if (p.x < x0 - r || p.x > x1 + r || p.y < z0 - r || p.y > z1 + r) return;
            float dl = p.x - (x0 - r), dr = (x1 + r) - p.x, dt2 = p.y - (z0 - r), db = (z1 + r) - p.y, m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(dt2, db));
            if (m == dl) p.x = x0 - r; else if (m == dr) p.x = x1 + r; else if (m == dt2) p.y = z0 - r; else p.y = z1 + r;
        }
        for (int i = -town.N; i <= town.N; i++) for (int j = -town.N; j <= town.N; j++) Push(i * town.Cell - town.Half, j * town.Cell - town.Half, i * town.Cell + town.Half, j * town.Cell + town.Half);
        var ave = O(data_town("AVE")); Push(F(ave["x0"]), F(ave["z0"]), F(ave["x1"]), F(ave["z1"]));
        return p;
    }
    void UpdateCarCamera(float dt) {
        var c = driving; var want = new Vector3(c.x, town.HeightAt(c.x, c.z) + 1.25f, c.z);
        cam.target = cam.cur == 0 ? want : Vector3.Lerp(cam.target, want, 1 - Mathf.Exp(-dt * 12));
        if (Time.time - cam.lastDrag > 1.2f) cam.yaw = Turn(cam.yaw, c.ry + Mathf.PI + (c.v < -0.5f ? Mathf.PI : 0), dt * 2.2f);
        var dir = new Vector3(Mathf.Sin(cam.yaw) * Mathf.Cos(cam.pitch), Mathf.Sin(cam.pitch), Mathf.Cos(cam.yaw) * Mathf.Cos(cam.pitch));
        float d = town.CamBlock(cam.target, dir, cam.dist + Mathf.Abs(c.v) * 0.08f);
        cam.cur = cam.cur == 0 ? d : Mathf.Lerp(cam.cur, d, 1 - Mathf.Exp(-dt * (d < cam.cur ? 25 : 4)));
        var p = cam.target + dir * cam.cur; p.y = Mathf.Max(p.y, town.HeightAt(p.x, p.z) + 0.4f);
        tcam.transform.position = SLTownWorld.W(p.x, p.y, p.z); tcam.transform.LookAt(SLTownWorld.W(cam.target.x, cam.target.y, cam.target.z));
        tcam.fieldOfView = Mathf.Lerp(tcam.fieldOfView, 55 + Mathf.Abs(c.v) * 0.6f, dt * 2);
    }

    // ---------------------------------------------------------------- Étoile Motors
    void OpenDealer(SLTownWorld.Place p) {
        bool open = IsOpen(p); var opts = new List<Opt>();
        if (open) {
            foreach (var m in Models) {
                var mm = m; bool own = core.Pref("town.car").StartsWith(m.id + ":");
                opts.Add(new Opt { label = m.name, note = own ? "yours" : m.price + " coins", accent = false, run = () => DealerModel(p, mm) });
            }
        }
        OpenCard(p.name, p.sub + " · " + HoursTxt(p.open[0], p.open[1]), !open ? "Closed right now. Opens at 9 AM." : myCar != null ? "Welcome back! Your " + ModelName(myCar.spec.id) + " is parked near home. Fancy an upgrade?" : "Every model, every colour. Test-drive anything for free.", opts);
    }
    void DealerModel(SLTownWorld.Place p, (string id, string name, int price) m) {
        var spec = carSpecs != null && carSpecs.TryGetValue(m.id, out var s) ? s : null;
        string say = spec == null ? "" : m.name + ": " + spec.length.ToString("0.0", CultureInfo.InvariantCulture) + " m long, " + (m.id == "sport" ? "top speed 80 km/h." : m.id == "limo" ? "room for the whole entourage." : m.id == "van" ? "seats the whole crew." : "made for town.");
        var opts = new List<Opt> {
            new Opt { label = "Test drive", note = "free · bring it back when you're done", run = () => { CloseCard(); if (testCar != null) { Destroy(testCar.go.gameObject); cars.Remove(testCar); } var at = Kerb(dealer.x, dealer.y, dealerRy, 4.4f) + new Vector2(12, 0); testCar = MakeCar(m.id, Paints[2].hex, at.x, at.y, Mathf.PI / 2); if (testCar != null) GetIn(testCar); } },
        };
        foreach (var pc in Paints) {
            var pp = pc; opts.Add(new Opt { label = "Buy in " + pc.name, note = m.price + " coins", disabled = profile.coins < m.price, run = () => BuyCar(m, pp.hex, pp.name) });
        }
        OpenCard(m.name, m.price + " coins · delivered to your door", say, opts);
    }
    void BuyCar((string id, string name, int price) m, string hex, string paintName) {
        if (profile.coins < m.price) { ShowToast("Not enough coins yet."); return; }
        core.Call("earn", (-m.price).ToString(), Mathf.RoundToInt(m.price / 40f).ToString()); profile = core.Get<DProfile>("profile");
        if (myCar != null) { Destroy(myCar.go.gameObject); cars.Remove(myCar); }
        var at = HomeBay(); ClearParked(at, 6); float ry = PlaceBy("home").ry + Mathf.PI / 2;
        myCar = MakeCar(m.id, hex, at.x, at.y, ry);
        core.SetPref("town.car", m.id + ":" + hex); core.SetPref("town.carpos", Nums(new[] { at.x, at.y, ry }));
        var garage = core.Pref("town.garage").Split(';').Where(x => x != "").ToList(); garage.Add(m.id + ":" + hex); core.SetPref("town.garage", string.Join(";", garage));
        CloseCard(); UpdateHud(true);
        SetWaypoint(new Waypoint { name = "your " + m.name, x = at.x, z = at.y });
        ShowToast("Congratulations! Your " + paintName.ToLowerInvariant() + " " + m.name + " is parked outside Starlife Suites. +" + Mathf.RoundToInt(m.price / 40f) + " fame.");
    }
    /// <summary>At home: take another car you own out of the garage.</summary>
    List<Opt> GarageOptions() {
        var list = new List<Opt>(); var garage = core.Pref("town.garage").Split(';').Where(x => x.Contains(':')).Distinct().ToList();
        foreach (var g in garage) {
            var parts = g.Split(':'); if (myCar != null && myCar.spec.id == parts[0] && myCar.hex == parts[1]) continue; string gg = g;
            list.Add(new Opt { label = "Take out the " + ModelName(parts[0]), note = Paints.FirstOrDefault(p => p.hex == parts[1]).name ?? "", run = () => {
                if (myCar != null) { Destroy(myCar.go.gameObject); cars.Remove(myCar); }
                var at = HomeBay(); ClearParked(at, 6); float ry = PlaceBy("home").ry + Mathf.PI / 2; myCar = MakeCar(parts[0], parts[1], at.x, at.y, ry);
                core.SetPref("town.car", gg); core.SetPref("town.carpos", Nums(new[] { at.x, at.y, ry })); CloseCard(); ShowToast("Your " + ModelName(parts[0]) + " is out front."); } });
        }
        return list;
    }
}
