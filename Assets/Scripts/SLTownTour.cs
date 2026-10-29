// The screenshot tour of Starlife Town, for checking it without anyone at the screen:
//   Starlife.app/Contents/MacOS/Starlife -sltown <dir> -screen-width 1440 -screen-height 900 -screen-fullscreen 0
// It plays on its own save (starlife-town-shots.json), visits the town's places by day and by night, opens a boutique,
// the café, a conversation and the phone, watches her walk, run and sit up close, and quits. Part of SLApp.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static SLJson;

public partial class SLApp {
    bool townTour; string dirLog = "";
    IEnumerator TownShots() {
        int n = 0;
        IEnumerator Snap(string name, int settle = 20) {
            for (int i = 0; i < settle; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "town-" + (++n).ToString("00") + "-" + name + ".png"));
            yield return null; yield return null;
        }
        void Go(string id, float back = 3f, float hour = 11, float yawOff = 0) {
            var p = PlaceBy(id); if (p == null) return;
            CloseCard(); if (shop != null) CloseBoutique(); StandUp(); stepT = 0; player.mo.emote = null;
            player.pos = new Vector3(p.x + Mathf.Sin(p.ry) * back, 0, p.z + Mathf.Cos(p.ry) * back); player.pos.y = town.HeightAt(player.pos.x, player.pos.z);
            player.ry = p.ry + Mathf.PI; cam.yaw = p.ry + yawOff; cam.pitch = 0.2f; cam.dist = 6; cam.cur = 0; lifeMin = Mathf.Floor(lifeMin / 1440) * 1440 + hour * 60;
        }
        for (int i = 0; i < 10; i++) yield return null;
        // the men and passers-by as generated, and the men in menswear on their shared body, front and side
        if (stage3d == null) Set3D(true, false);
        void Dump(Look3 look, string file, float yaw = 0) { var t2 = SLStage3D.Read(stage3d.Shot(look, "full", 400, 820, yaw)); File.WriteAllBytes(Path.Combine(shotsDir, file + ".png"), t2.EncodeToPNG()); Destroy(t2); }
        foreach (var e in stage3d.Starlets) if (SLStage3D.IsMale(e.id) || e.id.StartsWith("p_")) Dump(new Look3 { id = e.id, height = SLStage3D.IsMale(e.id) ? 1.06f : 1 }, "man-" + e.id);
        foreach (var (tag, ids) in new[] { ("suit", "suit,shirt,suitpants,oxfords"), ("shirt", "shirt,chinos,loafers"), ("polo", "polo,chinos,sneakers"), ("hoodie", "hoodie,jeans,sneakers"), ("tee", "tee,shorts,sandals"), ("tux", "tux,suitpants,oxfords") })
            foreach (var who in new[] { "kai", "jin" }) foreach (int yaw in new[] { 0, 90 }) {
                if (!stage3d.Has(who)) continue;
                var look = new Look3 { id = who, height = 1.06f, signature = false };
                foreach (var id in ids.Split(',')) if (itemById.ContainsKey(id)) look.worn.Add((itemById[id].slot, id, C(itemById[id].color)));
                Dump(look, "menswear-" + tag + "-" + who + "-" + yaw, yaw);
            }
        // the refitted garments (make_fashion.py) on a starlet, front and side
        foreach (var (tag, ids) in new[] { ("gown", "gown,pumps"), ("haltermaxi", "haltermaxi,sandals"), ("ankara", "ankaramaxi,sandals"), ("jeans-silk", "silkcarre,jeans,flats"),
                 ("leather-leopard", "leopardcami,leather,ankleboots"), ("suit", "womensuit,pumps"), ("keyhole", "keyholedress,flats"), ("sundress", "sundress,sandals"), ("tee-shorts", "tee,shorts,sneakers"), ("quilted", "quilted,kneeboots"), ("blazer-pencil", "blazer,pencil,pumps"), ("trench-jeans", "trench,jeans,ankleboots"), ("biker-mini", "biker,mini,kneeboots"), ("cardigan-pleated", "cardigan,pleated,flats"), ("tracktop-workboots", "tracktop,jeans,workboots") })
            foreach (var who in new[] { "lucy", "zuri" }) foreach (int yaw in new[] { 0, 90 }) {
                var look = new Look3 { id = who, signature = false };
                foreach (var id in ids.Split(',')) if (itemById.ContainsKey(id)) look.worn.Add((itemById[id].slot, id, C(itemById[id].color)));
                Dump(look, "fashion-" + tag + "-" + who + "-" + yaw, yaw);
            }
        pickSel = "zuri"; ClosePicker(true); CloseGuide();
        foreach (var id in new[] { "blouse", "pleated", "blazer", "pumps", "pearls" }) { core.Call("grant", "[\"" + id + "\"]"); core.Call("equip", id); }
        core.Call("earn", "600", "0");
        yield return null;
        GoOut();
        while (!inTown) yield return null;
        tSpeed = 0;
        Go("home", 3, 10); yield return Snap("home-morning", 40);
        Go("park", 6, 13, 0.6f); yield return Snap("park-fountain");
        Go("cafe", 5, 9); yield return Snap("cafe-terrace");
        Go("velours", 4, 15, 0.5f); yield return Snap("avenue");
        OpenPlace(PlaceBy("velours")); yield return Snap("boutique");
        TryPiece(1); yield return Snap("boutique-tryon");
        BuyPiece(0); yield return Snap("boutique-bought");
        CloseBoutique();
        Go("dore", 3, 16); OpenPlace(PlaceBy("dore")); yield return Snap("cafe-dore");
        CoffeeToGo(A(data_town("COFFEE")).Select(A).First()); yield return Snap("coffee-to-go");
        Go("galleria", 6, 14); yield return Snap("galleria-entrance");
        var mall = O(data_town("MALL")); player.pos = new Vector3(F(mall["cx"]) + 4, town.HeightAt(F(mall["cx"]) + 4, 0), 0); player.ry = -Mathf.PI / 2; cam.yaw = Mathf.PI / 2; cam.cur = 0;
        yield return Snap("galleria-inside");
        // her walk and run, close up from the side and the front
        Go("theatre", 14, 12, 1.4f); cam.dist = 3.2f; cam.pitch = 0.08f;
        autoWalk = new Vector2(0, 1); autoFixed = true; for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null;
        cam.yaw = player.ry + Mathf.PI / 2; cam.lastDrag = Time.time + 60; yield return Snap("walk-side", 10);
        for (int f = 0; f < 8; f++) { for (float w0 = Time.time; Time.time - w0 < 0.13f;) yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "strip-walk-" + f + ".png")); yield return null; }
        cam.yaw = player.ry; yield return Snap("walk-front", 6);
        autoRun = true; for (float t = 0; t < 1.2f; t += Time.deltaTime) yield return null;
        cam.yaw = player.ry + Mathf.PI / 2; yield return Snap("run-side", 4);
        for (int f = 0; f < 8; f++) { for (float w0 = Time.time; Time.time - w0 < 0.08f;) yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "strip-run-" + f + ".png")); yield return null; }
        autoWalk = null; autoRun = false; autoFixed = false; cam.lastDrag = Time.time;
        // sitting on a park bench, and someone to talk to
        var seat = town.seats.FirstOrDefault(s => s.place == "park"); if (seat != null) { Go("park", 4, 12); SitOn(seat); cam.yaw = seat.ry + 0.5f; cam.dist = 3.5f; cam.cur = 0; yield return Snap("sitting", 40); StandUp(); }
        var friend = people.FirstOrDefault(x => x.state != "inside" && x.male && !x.crowd) ?? people.FirstOrDefault(x => x.state != "inside" && !x.crowd);
        if (friend != null) { var c = town.Collide(new Vector2(friend.pos.x + Mathf.Sin(friend.ry) * 1.6f, friend.pos.z + Mathf.Cos(friend.ry) * 1.6f)); player.pos = new Vector3(c.x, town.HeightAt(c.x, c.y), c.y); player.ry = friend.ry + Mathf.PI; cam.yaw = friend.ry + 1.3f; cam.dist = 4; cam.pitch = 0.12f; cam.cur = 0; yield return null; TalkTo(friend); yield return Snap("talk", 30); CloseCard(); }
        // cars: the showroom, buying one, driving it, traffic from above, a night drive
        core.Call("earn", "6000", "0"); profile = core.Get<DProfile>("profile");
        Go("motors", 6, 11, 0.6f); cam.dist = 10; cam.pitch = 0.25f; yield return Snap("dealer", 40);
        OpenPlace(PlaceBy("motors")); yield return Snap("dealer-card");
        DealerModel(PlaceBy("motors"), Models.First(m => m.id == "sport")); yield return Snap("dealer-model");
        BuyCar(Models.First(m => m.id == "sport"), "#a3122e", "Ruby red"); yield return null;
        if (myCar != null) {
            player.pos = new Vector3(myCar.x + 2, 0, myCar.z); cam.cur = 0; yield return Snap("my-car", 20);
            GetIn(myCar); autoWalk = new Vector2(0, 1);
            for (float w0 = Time.time; Time.time - w0 < 2.5f;) yield return null; yield return Snap("driving", 2);
            var c0 = new Vector3(myCar.x, 0, myCar.z); var right0 = tcam.transform.right;
            autoWalk = new Vector2(0.8f, 0.7f); for (float w0 = Time.time; Time.time - w0 < 1.2f;) yield return null; yield return Snap("driving-turn", 2);
            var cd = SLTownWorld.W(myCar.x, 0, myCar.z) - SLTownWorld.W(c0.x, 0, c0.z); dirLog += "steer right: " + (Vector3.Dot(cd, right0) > 0 ? "ok" : "WRONG") + " (" + Vector3.Dot(cd, right0).ToString("0.00") + ")\n";
            autoWalk = new Vector2(0, -1); for (float w0 = Time.time; Time.time - w0 < 2f;) yield return null; autoWalk = null; for (float w0 = Time.time; Time.time - w0 < 1f;) yield return null;
            GetOut(); yield return Snap("parked-mine", 10);
        }
        if (roads != null) { player.pos = new Vector3(roads[3] + 7.5f, 0.15f, roads[3] + 7.5f); cam.yaw = 0.8f + Mathf.PI; cam.pitch = 0.62f; cam.dist = 14; cam.cur = 0; lifeMin = Mathf.Floor(lifeMin / 1440) * 1440 + 13 * 60;
            for (float w0 = Time.time; Time.time - w0 < 3f;) yield return null; yield return Snap("traffic", 2); }
        if (myCar != null) {
            lifeMin = Mathf.Floor(lifeMin / 1440) * 1440 + 21.5f * 60; player.pos = new Vector3(myCar.x + 2, 0, myCar.z); GetIn(myCar); autoWalk = new Vector2(0, 1);
            for (float w0 = Time.time; Time.time - w0 < 2.5f;) yield return null; yield return Snap("driving-night", 2); autoWalk = null;
            for (float w0 = Time.time; Time.time - w0 < 1.5f;) yield return null; GetOut();
        }
        // Fashion Week: own the season's pieces, put on a show at the Grand Hall, read the reviews
        foreach (var id in new[] { "haltermaxi", "ankaramaxi", "womensuit", "leather", "silkcarre", "leopardcami", "jeans", "sandals", "flats", "keyholedress", "gown" }) core.Call("grant", "[\"" + id + "\"]");
        profile = core.Get<DProfile>("profile");
        // walking left and right goes left and right on screen
        foreach (var (dx, name) in new[] { (1f, "right"), (-1f, "left") }) {
            Go("park", 6, 12); cam.yaw = player.ry + Mathf.PI; cam.lastDrag = Time.time + 60; cam.cur = 0; yield return null; yield return null;
            var w0p = SLTownWorld.W(player.pos.x, 0, player.pos.z); var right = tcam.transform.right;
            autoWalk = new Vector2(dx, 0); for (float w0 = Time.time; Time.time - w0 < 1f;) yield return null; autoWalk = null;
            float along = Vector3.Dot(SLTownWorld.W(player.pos.x, 0, player.pos.z) - w0p, right) * dx;
            dirLog += "walk " + name + ": " + (along > 0 ? "ok" : "WRONG") + " (" + along.ToString("0.00") + ")\n";
        }
        cam.lastDrag = Time.time;
        // Restaurant Row, and the shops, cafés and restaurants you walk into
        Go("greenwave", 9, 13, 0.7f); cam.dist = 9; cam.pitch = 0.2f; yield return Snap("row-west", 30);
        Go("sunnyside", 9, 13, 0.5f); cam.dist = 9; yield return Snap("row-north", 30);
        Go("gilded", 9, 21, 0.5f); cam.dist = 9; yield return Snap("row-south-night", 30);
        foreach (var (id, hr) in new[] { ("greenwave", 10f), ("bigbun", 13f), ("sunnyside", 10f), ("jollof", 19f), ("bella", 19f), ("sakura", 20f), ("gilded", 20f), ("lumiere", 20.5f),
                                          ("velours", 14f), ("sneakers", 14f), ("aurelle", 14f), ("books", 14f), ("dore", 9f), ("bar", 21f), ("spa", 14f), ("salon", 14f) }) {
            var pl = PlaceBy(id); if (pl == null) { dirLog += "no place " + id + "\n"; continue; }
            Go(id, 1.2f, hr); EnterRoom(pl); if (room == null) { dirLog += "no room " + id + "\n"; continue; }
            cam.yaw = player.ry + Mathf.PI + 0.25f; cam.pitch = 0.18f; cam.cur = 0; yield return Snap("room-" + id, 30);
            var lead = room.workers.FirstOrDefault(w => w.sells);
            if (lead != null) {
                var lp = lead.pp.pos; var to = new Vector2(lp.x + Mathf.Sin(lead.pp.ry) * 1.6f, lp.z + Mathf.Cos(lead.pp.ry) * 1.6f);     // in front of them
                player.pos = new Vector3(to.x, 0, to.y); player.ry = Mathf.Atan2(lp.x - to.x, lp.z - to.y); cam.yaw = player.ry + Mathf.PI + 0.5f; cam.cur = 0; yield return null;
                var nr = FindNear(); dirLog += "room " + id + ": " + room.workers.Count + " staff, " + room.guests.Count + " guests, " + room.seats.Count + " free seats, near=" + (nr != null ? nr.kind : "none") + "\n";
                TalkToWorker(lead); yield return Snap("room-" + id + "-staff", 20); if (shop != null) CloseBoutique(); CloseCard();
            }
            ExitRoom(); yield return null;
            dirLog += "out of " + id + ": " + (room == null && new Vector2(player.pos.x - pl.x, player.pos.z - pl.z).magnitude < 2 ? "ok" : "WRONG") + "\n";
        }
        // emotes, things to do in a seat, sitting on the ground
        Go("park", 4, 12); cam.lastDrag = Time.time + 999;
        foreach (var em in new[] { "wave", "dance", "clap", "cheer", "laugh", "think", "pose", "phone", "arms", "nod", "crouch", "ground" }) {
            DoEmote(em); CloseCard(); cam.yaw = player.ry + 0.5f; cam.pitch = 0.12f; cam.dist = 3.2f; cam.cur = 0;
            for (float w0 = Time.time; Time.time - w0 < (em == "dance" ? 0.9f : 1.3f);) yield return null; yield return Snap("emote-" + em, 2);
            player.mo.emote = null; if (groundSit) { groundSit = false; player.mo.pose = "stand"; } for (float w0 = Time.time; Time.time - w0 < 0.5f;) yield return null;
        }
        var bench = town.seats.FirstOrDefault(x => x.place == "dore"); if (bench != null) {
            Go("dore", 3, 10); SitOn(bench);
            foreach (var st in new[] { "chat", "phone", "eat", "lean" }) { player.mo.sitStyle = st; cam.yaw = bench.ry + 0.7f; cam.dist = 3; cam.pitch = 0.15f; cam.cur = 0; for (float w0 = Time.time; Time.time - w0 < 1.2f;) yield return null; yield return Snap("sit-" + st, 2); }
            StandUp(); for (float w0 = Time.time; Time.time - w0 < 0.8f;) yield return null; dirLog += "stood up " + (new Vector2(player.pos.x - bench.x, player.pos.z - bench.z).magnitude > 0.4f ? "clear of the seat: ok" : "inside the seat: WRONG") + "\n";
        }
        cam.lastDrag = Time.time;
        // people walking and sitting, close up and frame by frame
        lifeMin = Mathf.Floor(lifeMin / 1440) * 1440 + 13 * 60;
        for (float w0 = Time.time; Time.time - w0 < 4f;) yield return null;
        // (the tour's clock stands still, so send someone on their way and someone else to a seat)
        var walker = people.FirstOrDefault(x => x.male && x.crowd) ?? people.First(); SendTo(walker, PlaceBy("park")); for (int i = 0; i < 40; i++) yield return null;
        var seatFree = town.seats.FirstOrDefault(x => x.place == "dore" && !SeatTaken(x)); var guest = people.FirstOrDefault(x => x != walker && x.crowd);
        if (seatFree != null && guest != null) { guest.pos = new Vector3(seatFree.x + 3, 0, seatFree.z + 2); guest.seat = seatFree; guest.path = new List<Vector2> { SeatFront(seatFree) }; guest.state = "toSeat"; }
        if (walker != null) for (int f = 0; f < 8; f++) {
            player.pos = new Vector3(walker.pos.x + Mathf.Cos(walker.ry) * 3.5f, 0, walker.pos.z - Mathf.Sin(walker.ry) * 3.5f); player.go.gameObject.SetActive(false);
            cam.yaw = walker.ry + Mathf.PI / 2; cam.dist = 0.01f; cam.pitch = 0.05f; cam.focusY = 1.0f; cam.cur = 0;
            for (float w0 = Time.time; Time.time - w0 < 0.15f;) yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "strip-npcwalk-" + walker.id + "-" + f + ".png")); yield return null;
        }
        player.go.gameObject.SetActive(true); cam.focusY = null; cam.dist = 5.2f;
        var sitter = guest;
        if (sitter != null) for (int f = 0; f < 8; f++) {
            var sp = sitter.seat != null ? new Vector2(sitter.seat.x, sitter.seat.z) : new Vector2(sitter.pos.x, sitter.pos.z);
            player.pos = new Vector3(sp.x + 3, 0, sp.y + 3); player.go.gameObject.SetActive(false); cam.yaw = Mathf.Atan2(3, 3); cam.dist = 0.01f; cam.focusY = 1.0f; cam.pitch = 0.1f; cam.cur = 0;
            for (float w0 = Time.time; Time.time - w0 < 0.45f;) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "strip-npcsit-" + f + "-" + sitter.state + ".png")); yield return null;
        }
        player.go.gameObject.SetActive(true); cam.focusY = null; cam.dist = 5.2f;
        // a chat with a passer-by
        var local = people.FirstOrDefault(x => x.visible && x.crowd && x.state != "inside");
        if (local != null) { player.pos = new Vector3(local.pos.x + 1.2f, 0, local.pos.z); TalkTo(local); yield return Snap("talk-local", 20); TalkMore(local); yield return Snap("talk-more", 10); Recommend(local); yield return Snap("talk-recommend", 10); CloseCard(); talking = null; local.state = local.prevState ?? "idle"; }
        // the car against the kerb, a lamp post and a person: it stops and never overlaps
        if (myCar != null) {
            Go("motors", 6, 11); GetIn(myCar); yield return null;
            var kerbHits = 0; autoWalk = new Vector2(0, 1);
            for (float w0 = Time.time; Time.time - w0 < 3f;) { yield return null; if (KerbRects().Concat(town.colliders).Any(r => BoxOverlap(CarBox(myCar.x, myCar.z, myCar.ry, myCar.Len / 2 - 0.05f, myCar.Wid / 2 - 0.05f), RectBox(r), out _))) kerbHits++; }
            autoWalk = new Vector2(1, 1); for (float w0 = Time.time; Time.time - w0 < 3f;) { yield return null; if (KerbRects().Concat(town.colliders).Any(r => BoxOverlap(CarBox(myCar.x, myCar.z, myCar.ry, myCar.Len / 2 - 0.05f, myCar.Wid / 2 - 0.05f), RectBox(r), out _))) kerbHits++; }
            autoWalk = null; dirLog += "car into kerbs and posts: " + (kerbHits == 0 ? "ok" : "WRONG (" + kerbHits + " frames overlapping)") + "\n";
            yield return Snap("car-kerb", 10);
            var dummy = people.FirstOrDefault(x => x.crowd); if (dummy != null) {
                var fr = new Vector2(myCar.x + Mathf.Sin(myCar.ry) * (myCar.Len / 2 + 2.5f), myCar.z + Mathf.Cos(myCar.ry) * (myCar.Len / 2 + 2.5f)); dummy.pos = new Vector3(fr.x, 0, fr.y); dummy.state = "watch"; Place(dummy);
                myCar.v = 0; autoWalk = new Vector2(0, 1); for (float w0 = Time.time; Time.time - w0 < 2.5f;) yield return null; autoWalk = null;
                dirLog += "car and a person: " + (!InBox(new Vector2(dummy.pos.x, dummy.pos.z), myCar, 0.2f) ? "stopped short: ok" : "WRONG") + "\n"; dummy.state = "idle";
            }
            GetOut();
        }
        Go("hall", 9, 19, 0.2f); OpenPlace(PlaceBy("hall")); yield return Snap("hall-card", 20);
        OpenFashionWeek(); yield return Snap("fw-theme", 10);
        show.theme = 0; for (int k = 0; k < 4; k++) { var hero = OwnedClothes().Where(i => (i.slot == "dress" || i.slot == "top") && !show.heroes.Contains(i.id)).OrderByDescending(i => ThemeFits(i, 0)).First(); show.heroes.Add(hero.id); show.looks.Add(CompleteLook(hero)); }
        CastAndGo(); yield return Snap("fw-cast", 10); CloseCard();
        var models = people.Where(p => !p.crowd && !p.male).OrderBy(p => p.id).Take(4).Select(p => p.id).ToList();
        var run = StartCoroutine(RunShow(models));
        for (float w0 = Time.time; Time.time - w0 < 7.5f;) yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "town-fw-runway1.png"));
        for (float w0 = Time.time; Time.time - w0 < 9f;) yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "town-fw-runway2.png"));
        for (float w0 = Time.time; Time.time - w0 < 9f;) yield return null; ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "town-fw-runway3.png"));
        while (showRunning) yield return null;
        yield return Snap("fw-reviews", 20); CloseCard();
        // evening and night
        Go("theatre", 9, 18.7f, 0.3f); cam.dist = 8; yield return Snap("theatre-sunset", 30);
        Go("club", 8, 22, 0.4f); cam.dist = 8; yield return Snap("club-night", 30);
        Go("velours", 6, 21.5f, 0.6f); cam.dist = 9; cam.pitch = 0.3f; yield return Snap("avenue-night", 30);
        lifeMin = Mathf.Floor(lifeMin / 1440) * 1440 + 15 * 60; cam.pitch = 0.9f; cam.dist = 14; yield return Snap("overview", 30);
        TogglePhone(true); yield return Snap("phone-map", 30);
        phoneTab = "friends"; RenderPhone(); yield return Snap("phone-friends");
        TogglePhone(false);
        // the people of the town, as they are right now
        foreach (var pp in people.Where(x => x.state != "inside" && (x.male || x.crowd)).Take(8)) {
            var c = town.Collide(new Vector2(pp.pos.x + Mathf.Sin(pp.ry) * 2.2f, pp.pos.z + Mathf.Cos(pp.ry) * 2.2f));
            player.pos = new Vector3(c.x, town.HeightAt(c.x, c.y), c.y); player.ry = pp.ry + Mathf.PI; cam.yaw = pp.ry + 1.2f; cam.dist = 3.6f; cam.pitch = 0.12f; cam.cur = 0;
            yield return Snap("starlet-" + pp.id, 12);
        }
        File.WriteAllText(Path.Combine(shotsDir, "town-log.txt"), dirLog + "people " + people.Count + "\nplaces " + town.places.Count + "\ncoins " + profile.coins + "\nneeds " + Nums(needs) + "\nlux " + core.Pref("town.lux") + "\n");
        LeaveTown("suite"); yield return null; yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "town-99-back-in-suite.png")); yield return null; yield return null;
        Application.Quit();
    }
}
