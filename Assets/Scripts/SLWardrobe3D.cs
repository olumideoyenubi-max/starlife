// Which 3D piece stands in for each wardrobe item. The pieces are the outfits the avatars came with, cut apart
// (top, bottom, shoes), so an item names the avatar it borrows from; the game's item colour is dyed onto it.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class SLWardrobe3D {
    public struct Piece { public string donor, part, slot; public Color color; public bool keep; }

    // item id -> "donor" (same part as the slot), or "donor:part+part" for dresses and the like
    static readonly Dictionary<string, string> Map = new Dictionary<string, string> {
        // tops
        { "tee", "ivy" }, { "cami", "olivia" }, { "blouse", "songbird" }, { "turtle", "mary" }, { "corset", "cute" }, { "breton", "lucy" },
        // bottoms
        { "jeans", "ivy" }, { "shorts", "rosie" }, { "mini", "lina" }, { "pleated", "charlotte" }, { "wide", "cute" }, { "pencil", "olivia" },
        // dresses: a slip is one piece, the fuller ones are a bodice and a skirt
        { "slip", "jenny:top" }, { "lbd", "jenny:top" }, { "sequin", "jenny:top" }, { "gown", "eve:top+bottom" }, { "wrap", "charlotte:top+bottom" }, { "sundress", "charlotte:top+bottom" },
        // jackets (they come with what is worn under them, so they replace the top)
        { "blazer", "pigtail:top" }, { "trench", "pigtail:top" }, { "cardigan", "charlotte:top" }, { "biker", "lucy:top" }, { "denimjk", "lucy:top" }, { "puffer", "rosie:top" },
        // shoes
        { "sneakers", "ivy" }, { "pumps", "pigtail" }, { "ankleboots", "mary" }, { "kneeboots", "mary" }, { "flats", "cute" }, { "sandals", "charlotte" },
        // sports gear and workwear
        { "sportsbra", "basics:tank" }, { "gymtank", "ivy" }, { "leggings", "ivy" }, { "gymshorts", "rosie" }, { "tennisskirt", "lina" }, { "tracktop", "lucy:top" },
        { "trainers", "jenny" }, { "runners", "lina" }, { "staffpolo", "ivy" }, { "polo", "ivy" }, { "scrubtop", "ivy" }, { "scrubpants", "pigtail" }, { "workpants", "pigtail" },
        { "chefcoat", "pigtail:top" }, { "workboots", "mary" }, { "rainbowhoodie", "alice:top!" }, { "hoodie", "alice:top" }, { "joggers", "alice" }, { "hightops", "alice" },
        // the goth collection; its accessories (goth:...) are made pieces, and metal parts (..._m) keep their silver
        { "gothgown", "eve:top+bottom" }, { "gothcoat", "pigtail:top" }, { "harness", "cute:top" }, { "meshtop", "goth:meshtop" },
        { "kilt", "goth:fishnets!+lina:bottom+goth:belt!+belt_m" }, { "platforms", "kelly:shoes" }, { "creepers", "mary:shoes" },
        { "spikecollar", "goth:collar+collar_m+cuffs+cuffs_m" }, { "crosschain", "goth:chain_m" }, { "choker", "goth:choker+choker_m" },
        { "horns", "goth:horns" }, { "catears", "goth:catears" }, { "crossear", "goth:earrings_m" }, { "coffinbag", "goth:bag+bag_m" },
        // menswear on a woman's figure: the nearest women's pieces (the men wear their own, see MenMap)
        { "shirt", "songbird" }, { "chinos", "cute" }, { "suitpants", "cute" }, { "suit", "pigtail:top" }, { "tux", "pigtail:top" }, { "oxfords", "cute" }, { "loafers", "cute" },
        // eyewear: one pair of frames, dyed
        { "roundspecs", "cute:extra" }, { "cateye", "cute:extra" }, { "sunnies", "cute:extra" }, { "heartsunnies", "cute:extra" },
    };
    // The men wear menswear made on their shared body (Tools/blender/make_men.py): every item, women's included, maps to
    // one of those sets. Dresses become a suit; accessories have no 3D piece on a man yet.
    static readonly Dictionary<string, string> MenMap = new Dictionary<string, string> {
        { "tee", "m_tee" }, { "gymtank", "m_casual" }, { "cami", "m_casual" }, { "corset", "m_casual" }, { "sportsbra", "m_casual" }, { "harness", "m_casual" }, { "meshtop", "m_casual" }, { "scrubtop", "m_tee" },
        { "blouse", "m_shirt" }, { "shirt", "m_shirt" }, { "breton", "m_shirt" }, { "turtle", "m_polo" }, { "polo", "m_polo" }, { "staffpolo", "m_polo" },
        { "blazer", "m_suit:top" }, { "trench", "m_tweed:top" }, { "suit", "m_suit:top" }, { "tux", "m_suit:top" }, { "gothcoat", "m_suit:top" }, { "chefcoat", "m_shirt:top" },
        { "denimjk", "m_hoodie:top" }, { "biker", "m_hoodie:top" }, { "cardigan", "m_hoodie:top" }, { "puffer", "m_hoodie:top" }, { "tracktop", "m_hoodie:top" }, { "hoodie", "m_hoodie:top" }, { "rainbowhoodie", "m_hoodie:top" },
        { "jeans", "m_hoodie" }, { "leggings", "m_hoodie" }, { "shorts", "m_tee" }, { "gymshorts", "m_tee" }, { "mini", "m_tee" }, { "tennisskirt", "m_tee" },
        { "pleated", "m_polo" }, { "wide", "m_polo" }, { "chinos", "m_polo" }, { "joggers", "m_casual" }, { "scrubpants", "m_polo" },
        { "pencil", "m_shirt" }, { "suitpants", "m_suit" }, { "workpants", "m_shirt" }, { "kilt", "m_casual" },
        { "slip", "m_tweed:top+bottom" }, { "lbd", "m_suit:top+bottom" }, { "sequin", "m_tweed:top+bottom" }, { "gown", "m_suit:top+bottom" }, { "wrap", "m_tweed:top+bottom" }, { "sundress", "m_tweed:top+bottom" }, { "gothgown", "m_suit:top+bottom" },
        { "sneakers", "m_hoodie" }, { "trainers", "m_hoodie" }, { "runners", "m_polo" }, { "hightops", "m_hoodie" }, { "pumps", "m_suit" }, { "oxfords", "m_suit" }, { "platforms", "m_shirt" },
        { "flats", "m_casual" }, { "loafers", "m_casual" }, { "sandals", "m_tee" }, { "ankleboots", "m_shirt" }, { "kneeboots", "m_shirt" }, { "workboots", "m_shirt" }, { "creepers", "m_shirt" },
    };
    static readonly Dictionary<string, string> MenBySlot = new Dictionary<string, string> { { "top", "m_tee" }, { "bottom", "m_hoodie" }, { "dress", "m_suit:top+bottom" }, { "outer", "m_suit:top" }, { "shoes", "m_hoodie" } };
    // used when an item has no entry yet
    static readonly Dictionary<string, string> BySlot = new Dictionary<string, string> {
        { "top", "ivy" }, { "bottom", "ivy" }, { "dress", "charlotte:top+bottom" }, { "outer", "pigtail:top" }, { "shoes", "ivy" } };

    public static bool IsClothing(string slot) => slot == "top" || slot == "bottom" || slot == "dress" || slot == "outer" || slot == "shoes";

    /// <summary>The pieces to draw for what she wears, bottom layer first, accessories last.</summary>
    public static List<Piece> Plan(List<(string slot, string item, Color color)> worn, Func<string, string, bool> has, bool male = false) {
        var map = male ? MenMap : Map; var bySlot = male ? MenBySlot : BySlot;
        var plan = new List<Piece>(); bool dress = worn.Exists(w => w.slot == "dress"), outer = worn.Exists(w => w.slot == "outer");
        foreach (string slot in new[] { "shoes", "bottom", "top", "dress", "outer", "glasses", "necklace", "earrings", "hat", "bag" }) {
            int i = worn.FindIndex(w => w.slot == slot); if (i < 0) continue;
            if (dress && (slot == "top" || slot == "bottom")) continue;       // a dress covers both
            if (outer && slot == "top") continue;                             // the jackets come with their own top
            if (!map.TryGetValue(worn[i].item, out var spec)) { if (male && !IsClothing(slot) || !bySlot.TryGetValue(slot, out spec)) continue; }
            bool keep = spec.EndsWith("!"); if (keep) spec = spec.Substring(0, spec.Length - 1);     // "!": keep the piece's own print, do not dye it
            if (spec.IndexOf(':') < 0) spec += ":" + (slot == "dress" ? "top+bottom" : slot == "outer" ? "top" : slot);   // just an avatar: the part that matches the slot
            string donor = null;
            foreach (var tok in spec.Split('+')) {                            // "avatar:part+part+other:part": a part without an avatar comes from the one before
                string part = tok; bool own = part.EndsWith("!"); if (own) part = part.Substring(0, part.Length - 1);     // a part marked "!" keeps its own colour
                int c = part.IndexOf(':'); if (c >= 0) { donor = part.Substring(0, c); part = part.Substring(c + 1); }
                if (donor != null && has(donor, part)) plan.Add(new Piece { donor = donor, part = part, slot = slot, color = worn[i].color, keep = keep || own });
            }
        }
        return plan;
    }
}
