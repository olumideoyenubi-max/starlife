// The makeover studio (Beauty Parlor > Makeover): clients come in one after another, each with a story, a face, a
// skin undertone, a hair type and a brief ("Cyberpunk neon runway", "Regency drama", ...). You style four things:
// the hair (a real hairstyle and colour), the face prep and base, the eyes, and the lips and cheeks; each choice is
// painted onto the client's face in the mirror with the makeup studio's own brushes and finishes (SLSim.cs), and
// a status panel shows the look's settings as the engine has them. Reveal the look and the client says how happy
// she is; a good result earns coins and fame. Part of SLApp (3D only).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public partial class SLApp {
    sealed class MoClient { public string name, story, face, undertone, hairType, brief; public string[] wants; public Color tint; public int n; }
    sealed class MoChoice { public string label, tech; public string[] tags; public Action paint; public string hair, hairHex; public float sheen, weight; }
    static readonly (string brief, string[] wants)[] MoBriefs = {
        ("Cyberpunk neon runway", new[] { "neon", "metallic", "bold", "sleek" }), ("Regency era historical drama", new[] { "soft", "natural", "updo", "romantic" }),
        ("Soft-glam red carpet", new[] { "glam", "glow", "satin", "volume" }), ("Afrobeats music video", new[] { "bold", "glitter", "braids", "warm" }),
        ("Bridal glow", new[] { "soft", "glow", "updo", "romantic" }), ("Gothic editorial", new[] { "dark", "matte", "bold", "sleek" }),
        ("Y2K pop star", new[] { "glitter", "gloss", "playful", "volume" }), ("Old Hollywood premiere", new[] { "glam", "red", "satin", "sleek" }) };
    static readonly string[] MoNames = { "Adaeze", "Camille", "Ines", "Yaa", "Mira", "Sade", "Freya", "Keiko", "Zainab", "Lola", "Nneka", "Elodie", "Amara", "Rosa", "Imani", "Tiwa" };
    static readonly string[] MoJobs = { "a session singer", "a law student", "a TikTok baker", "an architect", "a tennis pro", "a fashion buyer", "a news anchor", "a nurse", "a poet", "a DJ", "a ballet dancer", "a startup founder" };
    static readonly (string name, Color tint)[] MoUndertones = { ("Warm golden", new Color(1.04f, 0.98f, 0.9f)), ("Cool rosy", new Color(0.98f, 0.97f, 1.04f)), ("Neutral", Color.white), ("Olive", new Color(0.98f, 1.01f, 0.92f)), ("Deep warm", new Color(0.86f, 0.8f, 0.74f)) };
    static readonly string[] MoHairTypes = { "4C coils", "4A curls", "3B ringlets", "2A waves", "1B straight", "relaxed and silk-pressed" };
    MoClient moClient; readonly Dictionary<string, int> moPick = new Dictionary<string, int>(); string moVerdict; int moServed;
    Dictionary<string, MoChoice[]> moChoices;

    MoClient NewClient() {
        var r = new System.Random(Environment.TickCount + moServed * 977);
        var faces = stage3d.Starlets.Where(e => Array.Exists(e.parts, q => q.mesh == "head") && !SLStage3D.IsHelper(e.id) && !SLStage3D.IsMale(e.id) && e.id != Av && !e.id.StartsWith("p_") && !e.id.StartsWith("f_")).Select(e => e.id).ToList();
        var b = MoBriefs[r.Next(MoBriefs.Length)]; var u = MoUndertones[r.Next(MoUndertones.Length)];
        var c = new MoClient { name = MoNames[r.Next(MoNames.Length)], face = faces.Count > 0 ? faces[r.Next(faces.Count)] : "aria", undertone = u.name, tint = u.tint, hairType = MoHairTypes[r.Next(MoHairTypes.Length)], brief = b.brief, wants = b.wants, n = ++moServed };
        c.story = c.name + " is " + MoJobs[r.Next(MoJobs.Length)] + " with " + c.hairType + " hair and a " + u.name.ToLowerInvariant() + " undertone. She has a " + b.brief.ToLowerInvariant() + " tonight and wants to look the part.";
        return c;
    }
    Look3 MoLook() {
        var c = moClient; var L = new Look3 { id = c.face, skinTint = c.tint };
        if (moPick.TryGetValue("hair", out int h)) { var ch = MoChoicesFor()["hair"][h]; L.hair = ch.hair; L.hairColor = C(ch.hairHex); }
        if (stage3d != null) { string key = "client-" + c.n; L.makeup = stage3d.Makeup(MkKey(key, c.face), true); L.makeupFx = stage3d.Makeup(MkKey(key, c.face) + "-fx", true); }
        return L;
    }
    Dictionary<string, MoChoice[]> MoChoicesFor() {
        if (moChoices != null) return moChoices;
        MoChoice Hair(string label, string hair, string hex, float sheen, float weight, params string[] tags) => new MoChoice { label = label, hair = hair, hairHex = hex, sheen = sheen, weight = weight, tags = tags,
            tech = "groom " + hair.Substring(2) + " · colour " + hex + " · sheen " + sheen.ToString("0.00") + " · physics weight " + weight.ToString("0.0") };
        moChoices = new Dictionary<string, MoChoice[]> {
            { "hair", new[] {
                Hair("Sleek silk press, glass shine", "h:silkpress", "#1a1210", 0.92f, 0.4f, "sleek", "glam", "dark"),
                Hair("Knotless box braids, gold cuffs", "h:boxbraids", "#2a1810", 0.55f, 1.6f, "braids", "bold", "warm"),
                Hair("Braided crown bun", "h:braidbun", "#3b2416", 0.6f, 0.9f, "updo", "romantic", "braids"),
                Hair("Blown-out volume", "h:blowout", "#3b2416", 0.45f, 0.8f, "volume", "glam", "playful"),
                Hair("Fulani braids with beads", "h:fulani", "#1c1412", 0.5f, 1.2f, "braids", "warm", "bold"),
                Hair("Neon-tinted high puff", "h:highpuff", "#e040fb", 0.6f, 0.7f, "neon", "playful", "bold") } },
            { "base", new[] {
                new MoChoice { label = "Glass skin", tech = "coverage 35% · specular roughness 18% · gloss 0.85 · highlight metallic 0.30", tags = new[] { "glow", "soft", "natural" }, paint = () => { MkBase(MoShade(), "satin"); MkHighlight("#fff4dc", "gloss"); } },
                new MoChoice { label = "Soft-matte full coverage", tech = "coverage 90% · specular roughness 72% · metallic 0.00 · pore blur on", tags = new[] { "matte", "glam", "dark" }, paint = () => { MkBase(MoShade(), "matte"); MkBase(MoShade(), "matte"); MkContour(); } },
                new MoChoice { label = "Bronzed satin sculpt", tech = "coverage 60% · specular roughness 45% · contour depth 0.4 · bronze metallic 0.45", tags = new[] { "warm", "glam", "bold" }, paint = () => { MkBase(MoShade(), "satin"); MkContour(); MkHighlight("#e9c46a", "metallic"); } } } },
            { "eyes", new[] {
                new MoChoice { label = "Neon graphic liner", tech = "liner opacity 100% · emissive cyan · wing 3 mm · lid shimmer metallic 0.9", tags = new[] { "neon", "bold", "playful" }, paint = () => { MkBothEyes("shadow", "#ff2ea6", "metallic", 12, 0.24f); MkBothEyes("liner", "#2ef2ff", "metallic", 4, 0.14f, 0.27f); } },
                new MoChoice { label = "Smoky metallic lid", tech = "lid colour #3a3340 · metallic 0.80 · roughness 25% · liner opacity 90%", tags = new[] { "dark", "metallic", "glam" }, paint = () => { MkBothEyes("shadow", "#3a3340", "metallic", 16, 0.24f); MkBothEyes("liner", "#0e0c10", "gloss", 3.5f, 0.14f, 0.09f); } },
                new MoChoice { label = "Soft champagne wash", tech = "lid colour #e6d2c0 · satin · lash density 1.2x · liner opacity 40%", tags = new[] { "soft", "natural", "romantic" }, paint = () => { MkBothEyes("shadow", "#e6d2c0", "satin", 14, 0.24f); MkBothEyes("liner", "#4a3020", "matte", 2.5f, 0.14f, 0.05f); } },
                new MoChoice { label = "Glitter halo eye", tech = "glitter particles 0.9 · lid colour #f5d76e · liner opacity 70%", tags = new[] { "glitter", "playful", "bold" }, paint = () => { MkBothEyes("glitter", "#f5d76e", "glitter", 14, 0.24f); MkBothEyes("liner", "#2a1a12", "gloss", 3, 0.14f, 0.11f); } },
                new MoChoice { label = "Classic winged liner", tech = "liner opacity 100% · wing 5 mm · lid #e8d6c8 satin", tags = new[] { "glam", "sleek", "red" }, paint = () => { MkBothEyes("shadow", "#e8d6c8", "satin", 14, 0.24f); MkBothEyes("liner", "#0e0c10", "gloss", 4, 0.14f, 0.23f); } } } },
            { "lips", new[] {
                new MoChoice { label = "Matte red lip, soft flush", tech = "lip #a3122e matte · roughness 85% · blush radius 22 px", tags = new[] { "red", "glam", "bold" }, paint = () => { MkLips("lips", "#a3122e", "matte"); MkCheeks("blush", "#d8747a", "satin", 22); } },
                new MoChoice { label = "Glossy pink, draped blush", tech = "lip #e05a8a · gloss coat 1.0 · blush radius 30 px, satin", tags = new[] { "gloss", "playful", "romantic" }, paint = () => { MkLips("lips", "#e05a8a", "satin"); MkLips("gloss", "#ffb3c7", "gloss"); MkCheeks("blush", "#f2a7b0", "satin", 30); } },
                new MoChoice { label = "Nude satin, bronze cheek", tech = "lip #c4907a satin · cheek bronze metallic 0.4 · blend radius 26 px", tags = new[] { "natural", "warm", "soft" }, paint = () => { MkLips("lips", "#c4907a", "satin"); MkCheeks("contour", "#b98a6a", "metallic", 24); } },
                new MoChoice { label = "Black cherry matte", tech = "lip #4a0f1e matte · roughness 90% · cheek sculpt only", tags = new[] { "dark", "matte", "bold" }, paint = () => { MkLips("lips", "#4a0f1e", "matte"); } },
                new MoChoice { label = "Holographic violet", tech = "lip #7a1aff metallic 0.9 · glitter particles 0.5 on the cheekbones", tags = new[] { "neon", "metallic", "glitter" }, paint = () => { MkLips("lips", "#7a1aff", "metallic"); MkCheeks("glitter", "#8fd3ff", "glitter", 12, -0.35f); } } } } };
        return moChoices;
    }
    string MoShade() => moClient.undertone.StartsWith("Deep") ? "#8d5a3b" : moClient.undertone.StartsWith("Olive") ? "#d39a72" : moClient.undertone.StartsWith("Cool") ? "#f2c9b8" : "#e9b996";
    // each client is offered three of each: one that suits her brief, and two others
    readonly Dictionary<string, int[]> moOffer = new Dictionary<string, int[]>();
    int[] MoOffer(string cat) {
        if (moOffer.TryGetValue(cat, out var o)) return o;
        var all = MoChoicesFor()[cat]; var r = new System.Random(moClient.n * 131 + cat.Length);
        var fit = Enumerable.Range(0, all.Length).OrderByDescending(i => all[i].tags.Count(t => moClient.wants.Contains(t)) * 10 + r.Next(9)).First();
        var rest = Enumerable.Range(0, all.Length).Where(i => i != fit).OrderBy(_ => r.Next()).Take(2);
        return moOffer[cat] = new[] { fit }.Concat(rest).OrderBy(_ => r.Next()).ToArray();
    }
    void MoRepaint() {
        mkPending = () => { MkWipe(); foreach (var cat in new[] { "base", "eyes", "lips" }) if (moPick.TryGetValue(cat, out int i)) MoChoicesFor()[cat][i].paint(); MkShow(); MkSave(); };
    }

    void Makeover(VisualElement b) {
        MkInitTools(); MkInitMirror();
        if (moClient == null) { moClient = NewClient(); moPick.Clear(); moOffer.Clear(); moVerdict = null; }
        mkSubject = MoLook(); mkSubjectKey = MkKey("client-" + moClient.n, moClient.face);
        var c = moClient;
        b.Add(L("Client " + c.n + ": " + c.name, "h3")); b.Add(L(c.story, "muted"));
        b.Add(El("mo-brief", L("THE BRIEF", "mo-lbl"), L(c.brief, "mo-briefT")));
        // the engine's status of the look, as it stands
        string Pick(string cat, Func<MoChoice, string> f, string none) => moPick.TryGetValue(cat, out int i) ? f(MoChoicesFor()[cat][i]) : none;
        string hairLine = moPick.TryGetValue("hair", out int hi) ? MoChoicesFor()["hair"][hi].label + ", " + MoChoicesFor()["hair"][hi].hairHex + ", physics weight " + MoChoicesFor()["hair"][hi].weight.ToString("0.0") + ", sheen " + MoChoicesFor()["hair"][hi].sheen.ToString("0.00") : "natural " + c.hairType + ", untouched";
        var matrix = "[3D MESH STATUS: " + c.name.ToUpperInvariant() + "]\n" +
            "- Hair Groom: " + hairLine + "\n" +
            "- Skin Texture: " + c.undertone + " undertone, " + Pick("base", x => x.tech, "bare skin, roughness 55%, metallic 0.00") + "\n" +
            "- Eye Layers: " + Pick("eyes", x => x.tech, "bare lids, natural lashes, no liner") + "\n" +
            "- Lip/Cheek Nodes: " + Pick("lips", x => x.tech, "bare lips, no blush");
        b.Add(El("mo-matrix", L(matrix, "mo-mono")));
        foreach (var (cat, title) in new[] { ("hair", "Hair mesh and texture"), ("base", "Face prep and base"), ("eyes", "Eye cosmetics"), ("lips", "Lip and cheek mapping") }) {
            b.Add(L(title, "h3")); var col = El("mo-col");
            foreach (int i in MoOffer(cat)) {
                var ch = MoChoicesFor()[cat][i]; string cc = cat; int ii = i; bool on = moPick.TryGetValue(cat, out int cur) && cur == i;
                var btn = B("", "mo-choice" + (on ? " on" : ""), () => { moPick[cc] = ii; moVerdict = null; if (cc != "hair") MoRepaint(); RefreshParlorPanel(); });
                btn.Add(L(ch.label, "mo-cl")); btn.Add(L(ch.tech, "mo-ct")); col.Add(btn);
            }
            b.Add(col);
        }
        if (moVerdict != null) { b.Add(El("mo-verdict", L(moVerdict, "mo-vt"))); b.Add(B("Next client", "btn primary", () => { moClient = null; mkSubject = null; RefreshParlorPanel(); })); return; }
        var reveal = B("Reveal the look", "btn primary", MoReveal); reveal.SetEnabled(moPick.Count == 4); reveal.style.marginTop = 12; b.Add(reveal);
        if (moPick.Count < 4) b.Add(L("Choose one in each of the four sections to reveal the look.", "muted"));
    }
    void MoReveal() {
        var c = moClient; int score = 40; var liked = new List<string>(); var missed = new List<string>();
        foreach (var (cat, word) in new[] { ("hair", "hair"), ("base", "skin"), ("eyes", "eyes"), ("lips", "lips") }) {
            var ch = MoChoicesFor()[cat][moPick[cat]]; int hits = ch.tags.Count(t => c.wants.Contains(t));
            score += hits >= 2 ? 15 : hits == 1 ? 10 : 2; (hits > 0 ? liked : missed).Add(word);
        }
        score = Mathf.Clamp(score + UnityEngine.Random.Range(-3, 4), 0, 100);
        int coins = score / 3, fame = score / 10; core.Call("earn", (double)coins, (double)fame); profile = core.Get<DProfile>("profile");
        string mood = score >= 90 ? "She gasps at the mirror. \"This is exactly the " + c.brief.ToLowerInvariant() + " look I dreamed of!\"" : score >= 75 ? "She turns her head side to side, grinning. \"I love it.\"" : score >= 55 ? "\"It's nice... not quite what I pictured for a " + c.brief.ToLowerInvariant() + ", but nice.\"" : "\"Hmm. This isn't really the brief, is it?\"";
        moVerdict = "Client satisfaction: " + score + "%\n" + mood + (liked.Count > 0 ? "\nShe loved the " + string.Join(", ", liked) + "." : "") + (missed.Count > 0 ? " The " + string.Join(", ", missed) + " missed the brief." : "") + "\n+" + coins + " coins, +" + fame + " fame.";
        RefreshParlorPanel();
    }
}
