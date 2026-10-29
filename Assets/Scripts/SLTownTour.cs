// The screenshot tour of Starlife Town, for checking it without anyone at the screen:
//   Starlife.app/Contents/MacOS/Starlife -sltown <dir> -screen-width 1440 -screen-height 900 -screen-fullscreen 0
// It plays on its own save (starlife-town-shots.json), visits the town's places by day and by night, opens a boutique,
// the café, a conversation and the phone, watches her walk, run and sit up close, and quits. Part of SLApp.
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using static SLJson;

public partial class SLApp {
    bool townTour;
    IEnumerator TownShots() {
        int n = 0;
        IEnumerator Snap(string name, int settle = 20) {
            for (int i = 0; i < settle; i++) yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "town-" + (++n).ToString("00") + "-" + name + ".png"));
            yield return null; yield return null;
        }
        void Go(string id, float back = 3f, float hour = 11, float yawOff = 0) {
            var p = PlaceBy(id); if (p == null) return;
            CloseCard(); if (shop != null) CloseBoutique(); StandUp();
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
            autoWalk = new Vector2(0.8f, 0.7f); for (float w0 = Time.time; Time.time - w0 < 1.2f;) yield return null; yield return Snap("driving-turn", 2);
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
        File.WriteAllText(Path.Combine(shotsDir, "town-log.txt"), "people " + people.Count + "\nplaces " + town.places.Count + "\ncoins " + profile.coins + "\nneeds " + Nums(needs) + "\nlux " + core.Pref("town.lux") + "\n");
        LeaveTown("suite"); yield return null; yield return null;
        ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, "town-99-back-in-suite.png")); yield return null; yield return null;
        Application.Quit();
    }
}
