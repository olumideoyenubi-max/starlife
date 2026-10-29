// Starlife's social calendar: holidays and weekly get-togethers anyone can walk into, and invitations that want an
// answer first. Part of SLApp. An event pays by how well her outfit fits its dress code (DressCheck in SLLife.cs).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public partial class SLApp {
    sealed class Ev {
        public string id, name, blurb, bg; public string[] tags; public int coins, fame; public DateTime day;
        public int before, after;      // open events run from this many days before their day to this many after
        public bool rsvp;              // true: an invitation she must accept beforehand, and attend on the day itself
        public string Key => id + "." + day.ToString("yyyyMMdd");
    }

    DateTime? evDate;                  // set by the screenshot tour to visit a holiday
    DateTime EvToday => (evDate ?? DateTime.Now).Date;

    static DateTime Easter(int y) {    // Easter Sunday (Gregorian)
        int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3, h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7, m = (a + 11 * h + 22 * l) / 451, month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(y, month, day);
    }
    /// <summary>The year's holidays. Nobody needs an invitation: each is open for a few days around its date.</summary>
    static List<Ev> Holidays(int y) {
        Ev H(string id, string name, DateTime day, string bg, string blurb, int before, int after, params string[] tags) =>
            new Ev { id = id, name = name, day = day, bg = bg, blurb = blurb, before = before, after = after, tags = tags, coins = 60, fame = 10 };
        return new List<Ev> {
            H("newyear", "New Year's Day", new DateTime(y, 1, 1), "#2e2a4f", "Fireworks are over; the brunches go on for days.", 0, 3, "Glam", "Chic"),
            H("valentine", "Valentine's Day", new DateTime(y, 2, 14), "#d9475e", "Roses, candlelight and a table for two.", 2, 1, "Glam", "Night out"),
            H("womensday", "Women's Day Gala", new DateTime(y, 3, 8), "#7c4ee0", "A room full of women who run things.", 1, 1, "Chic", "Office"),
            H("easter", "Easter Weekend", Easter(y), "#8fcf9a", "Garden parties, pastels and a long weekend.", 2, 1, "Summer", "Casual"),
            H("summerfest", "Summer Carnival", new DateTime(y, 8, 1), "#f5b82e", "Music in the streets from noon till late.", 1, 3, "Summer", "Casual"),
            H("independence", "Independence Day", new DateTime(y, 10, 1), "#1d9a63", "Green and white, parades and a proud party.", 1, 1, "Chic", "Glam"),
            H("halloween", "Halloween", new DateTime(y, 10, 31), "#1f1b22", "The one night a year when darker is better.", 3, 1, "Edgy", "Night out"),
            H("christmas", "Christmas", new DateTime(y, 12, 25), "#b3263a", "Lights, carols and everyone home.", 3, 2, "Winter", "Glam"),
            H("nye", "New Year's Eve", new DateTime(y, 12, 31), "#d6b25e", "The biggest party of the year. Dress for midnight.", 1, 0, "Glam", "Red carpet"),
        };
    }
    /// <summary>Something is on every day of the week, no invitation needed.</summary>
    static Ev Weekly(DateTime day) {
        (string id, string name, string bg, string blurb, string[] tags)[] w = {
            ("brunch", "Sunday Brunch", "#f6c9a8", "Pancakes and gossip, no rush.", new[] { "Casual", "Chic" }),
            ("coffee", "Coffee Morning", "#c8a979", "A catch-up before the week gets going.", new[] { "Casual", "Office" }),
            ("openmic", "Open Mic Night", "#3f3a5f", "A small stage and a brave crowd.", new[] { "Edgy", "Night out" }),
            ("gallery", "Gallery Opening", "#cbbcef", "White walls, small canapes, big opinions.", new[] { "Chic", "Office" }),
            ("funrun", "Charity Fun Run", "#3fcf8e", "Five kilometres for a good cause.", new[] { "Sporty" }),
            ("nightout", "Friday Night Out", "#e8478c", "The week is over. The dance floor is not.", new[] { "Night out", "Glam" }),
            ("rooftop", "Rooftop Party", "#4fb6e8", "Sunset, a DJ and the whole skyline.", new[] { "Summer", "Glam" }) };
        var e = w[(int)day.DayOfWeek];
        return new Ev { id = e.id, name = e.name, day = day, bg = e.bg, blurb = e.blurb, tags = e.tags, coins = 18, fame = 3 };
    }
    /// <summary>This week's and next week's invitations: three a week, the same ones every time the week is looked at.</summary>
    static List<Ev> Invitations(DateTime today) {
        (string id, string name, string bg, string blurb, string[] tags)[] pool = {
            ("premiere", "Film Premiere", "#8f1d2c", "A red carpet, a wall of cameras, and her name on the list.", new[] { "Red carpet", "Glam" }),
            ("gala", "Charity Gala", "#2e2a4f", "Black tie, a silent auction and a seat at the top table.", new[] { "Glam", "Chic" }),
            ("runway", "Fashion Week Show", "#3a3f4a", "Front row. Everyone will look at what she wears.", new[] { "Chic", "Edgy" }),
            ("launch", "Album Launch Party", "#7c4ee0", "A warehouse, a guest list and a very loud speaker stack.", new[] { "Night out", "Edgy" }),
            ("garden", "Garden Party", "#86aba1", "Lawn games, a marquee and a string quartet.", new[] { "Summer", "Casual" }),
            ("awards", "Awards Night", "#d6b25e", "She is nominated. The speech is optional, the outfit is not.", new[] { "Red carpet", "Glam" }),
            ("birthday", "A Friend's Birthday", "#f4b8c8", "Cake, karaoke and people who knew her before the fame.", new[] { "Casual", "Night out" }),
            ("boardroom", "Investors' Dinner", "#3b4a5f", "Quiet money, a private room and a firm handshake.", new[] { "Office", "Chic" }),
            ("skilodge", "Lodge Weekend", "#a9c6ef", "An open fire and snow up to the windows.", new[] { "Winter", "Casual" }) };
        var res = new List<Ev>(); DateTime monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        for (int w = 0; w < 2; w++) {
            DateTime start = monday.AddDays(7 * w); var rnd = new System.Random(start.Year * 1000 + start.DayOfYear); var used = new HashSet<int>();
            while (used.Count < 3) {
                int i = rnd.Next(pool.Length); if (!used.Add(i)) continue; var p = pool[i];
                res.Add(new Ev { id = p.id, name = p.name, bg = p.bg, blurb = p.blurb, tags = p.tags, day = start.AddDays(rnd.Next(7)), coins = 45, fame = 8, rsvp = true });
            }
        }
        res.Sort((a, b) => a.day.CompareTo(b.day)); return res;
    }
    static string When(DateTime day, DateTime today) {
        int d = (day - today).Days;
        return d == 0 ? "Today" : d == 1 ? "Tomorrow" : d == -1 ? "Yesterday" : d > 1 && d < 7 ? day.ToString("dddd") : day.ToString("d MMMM");
    }

    VisualElement eventsBody, eventsBox; SvgImage eventsDoll; string eventResult = "";
    void BuildEvents(VisualElement pane) {
        eventsDoll = new SvgImage(); eventsBox = El("contest-doll", eventsDoll); eventsBox.style.backgroundColor = C("#f3e6f5"); eventsBody = Scroll();
        pane.Add(El("contest-body", eventsBox, eventsBody));
    }
    VisualElement EventCard(Ev e, string when, params VisualElement[] extra) {
        var head = L(e.name, "contest-title"); head.style.fontSize = 20; head.style.whiteSpace = WhiteSpace.Normal;
        var top = El(null, L(when.ToUpperInvariant() + (e.rsvp ? " · INVITATION" : " · OPEN TO ALL"), "tile-h"), head); top.style.backgroundColor = C(e.bg);
        top.style.paddingLeft = top.style.paddingRight = 14; top.style.paddingTop = 10; top.style.paddingBottom = 10; top.style.borderTopLeftRadius = top.style.borderTopRightRadius = 13;
        foreach (var k in top.Children()) if (k is Label lab) lab.style.color = Color.white;
        var blurb = L(e.blurb, "muted"); blurb.style.whiteSpace = WhiteSpace.Normal;
        var dress = L("Dress code: " + string.Join(" or ", e.tags).ToLowerInvariant() + ". Up to " + e.coins + " coins and " + e.fame + " fame.", "muted"); dress.style.whiteSpace = WhiteSpace.Normal;
        var body = El(null, blurb, dress); body.style.paddingLeft = body.style.paddingRight = 14; body.style.paddingTop = 10; body.style.paddingBottom = 12;
        foreach (var x in extra) if (x != null) body.Add(x);
        var card = El(null, top, body); card.style.width = 330; card.style.marginRight = 14; card.style.marginBottom = 14; card.style.backgroundColor = C("#fbf9fe");
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 14;
        card.style.borderLeftWidth = card.style.borderRightWidth = card.style.borderTopWidth = card.style.borderBottomWidth = 1;
        card.style.borderLeftColor = card.style.borderRightColor = card.style.borderTopColor = card.style.borderBottomColor = C("#ece6f7");
        return card;
    }

    void RefreshEvents() {
        if (On3) Doll3D(eventsDoll, "events", "full"); else eventsDoll.Set("doll:" + dollKey + ":events", () => core.Call("doll"));
        var s = eventsBody; s.Clear(); DateTime today = EvToday; string stamp = today.ToString("yyyyMMdd");
        s.Add(L("Events", "h2"));
        var intro = L("Holidays and the week's get-togethers are open to everyone: just turn up. Invitations want an answer first. Either way, dress for it.", "muted"); intro.style.whiteSpace = WhiteSpace.Normal; s.Add(intro);
        if (eventResult != "") { var r = L(eventResult, "h3"); r.style.whiteSpace = WhiteSpace.Normal; s.Add(r); }

        // invitations she accepted and then missed cost a little fame, once
        var invites = Invitations(today);
        foreach (var e in invites) if (e.day < today && core.Pref("inv." + e.Key) == "accepted") {
            core.SetPref("inv." + e.Key, "missed"); core.Call("earn", 0d, -2d); ShowToast("She missed the " + e.name + " she said yes to: -2 fame");
        }

        // ---- on today: holidays in their window, the weekly event, and accepted invitations for today
        var now = new List<Ev>(); var soon = new List<Ev>();
        foreach (int y in new[] { today.Year - 1, today.Year, today.Year + 1 })
            foreach (var e in Holidays(y)) { if (today >= e.day.AddDays(-e.before) && today <= e.day.AddDays(e.after)) now.Add(e); else if (e.day > today && (e.day - today).Days <= 120) soon.Add(e); }
        now.Add(Weekly(today));
        foreach (var e in invites) if (e.day == today && core.Pref("inv." + e.Key) == "accepted") now.Add(e);
        s.Add(L("ON TODAY", "sec")); var grid = El("grid");
        foreach (var e in now) {
            var ev = e; string done = "evdone." + e.Key + "." + stamp; bool been = core.Pref(done) == "1";
            var (lines, _, _) = DressCheck(e.tags, 2); var check = El(null); check.style.marginTop = 8; Checklist(check, lines);
            var go = B(been ? "She has been" : "Go to the event", "btn small" + (been ? "" : " primary"), () => Attend(ev, done)); go.SetEnabled(!been); go.style.marginTop = 8; go.style.alignSelf = Align.FlexStart;
            grid.Add(EventCard(e, e.rsvp ? "Today" : e.day == today ? "Today" : "On now, " + e.day.ToString("d MMMM"), check, go));
        }
        s.Add(grid);
        var change = B("Change her outfit", "btn", () => { SetView("suite"); OpenDrawer("wardrobe"); }); change.style.alignSelf = Align.FlexStart; s.Add(change);

        // ---- invitations: accept or decline ahead of the day
        s.Add(L("INVITATIONS", "sec")); grid = El("grid"); int shown = 0;
        foreach (var e in invites) {
            var ev = e; string key = "inv." + e.Key, st = core.Pref(key);
            if (e.day < today || st == "declined" || st == "attended" || st == "missed" || (e.day == today && st == "accepted")) continue;
            VisualElement act;
            if (st == "accepted") {
                var yes = L("She is going. Come back " + When(e.day, today).ToLowerInvariant() + " to attend.", "check ok"); yes.style.whiteSpace = WhiteSpace.Normal;
                var cancel = B("Send apologies", "btn small", () => { core.SetPref(key, "declined"); dirty = true; }); cancel.style.alignSelf = Align.FlexStart; cancel.style.marginTop = 6;
                act = El(null, yes, cancel);
            } else {
                act = El("row", B("Accept", "btn small primary", () => { core.SetPref(key, "accepted"); ShowToast("RSVP sent: she will be at the " + ev.name); dirty = true; }),
                                B("Decline", "btn small", () => { core.SetPref(key, "declined"); dirty = true; }));
            }
            act.style.marginTop = 8; grid.Add(EventCard(e, When(e.day, today), act)); shown++;
        }
        if (shown == 0) { var none = L("No invitations waiting. New ones arrive every Monday.", "muted"); grid.Add(none); }
        s.Add(grid);

        // ---- holidays coming up
        soon.Sort((a, b) => a.day.CompareTo(b.day));
        s.Add(L("HOLIDAYS COMING UP", "sec")); grid = El("grid");
        foreach (var e in soon) { int d = (e.day - today).Days; var left = L("In " + d + (d == 1 ? " day" : " days") + ". No invitation needed.", "check"); left.style.marginTop = 6; grid.Add(EventCard(e, e.day.ToString("d MMMM"), left)); }
        s.Add(grid);
    }

    void Attend(Ev e, string doneKey) {
        var (_, score, _) = DressCheck(e.tags, 2);
        int coins = Mathf.RoundToInt(e.coins * (0.25f + 0.75f * score / 100f)), fame = Mathf.RoundToInt(e.fame * score / 100f);
        core.SetPref(doneKey, "1"); if (e.rsvp) core.SetPref("inv." + e.Key, "attended");
        core.Call("earn", (double)coins, (double)fame);
        eventResult = e.name + ": " + (score >= 85 ? "she was the best-dressed there. " : score >= 55 ? "she looked the part. " : "she turned up, but not dressed for it. ") + "+" + coins + " coins" + (fame > 0 ? ", +" + fame + " fame." : ".");
        dirty = true;
    }
}
