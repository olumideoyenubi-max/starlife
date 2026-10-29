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
        { "trainers", "jenny" }, { "runners", "lina" }, { "polo", "ivy" }, { "scrubtop", "ivy" }, { "scrubpants", "pigtail" }, { "workpants", "pigtail" },
        { "chefcoat", "pigtail:top" }, { "workboots", "mary" }, { "hoodie", "alice:top!" }, { "joggers", "alice" }, { "hightops", "alice" },
        // the goth collection; its accessories (goth:...) are made pieces, and metal parts (..._m) keep their silver
        { "gothgown", "eve:top+bottom" }, { "gothcoat", "pigtail:top" }, { "harness", "cute:top" }, { "meshtop", "goth:meshtop" },
        { "kilt", "goth:fishnets!+lina:bottom+goth:belt!+belt_m" }, { "platforms", "kelly:shoes" }, { "creepers", "mary:shoes" },
        { "spikecollar", "goth:collar+collar_m+cuffs+cuffs_m" }, { "crosschain", "goth:chain_m" }, { "choker", "goth:choker+choker_m" },
        { "horns", "goth:horns" }, { "catears", "goth:catears" }, { "crossear", "goth:earrings_m" }, { "coffinbag", "goth:bag+bag_m" },
        // eyewear: one pair of frames, dyed
        { "roundspecs", "cute:extra" }, { "cateye", "cute:extra" }, { "sunnies", "cute:extra" }, { "heartsunnies", "cute:extra" },
    };
    // used when an item has no entry yet
    static readonly Dictionary<string, string> BySlot = new Dictionary<string, string> {
        { "top", "ivy" }, { "bottom", "ivy" }, { "dress", "charlotte:top+bottom" }, { "outer", "pigtail:top" }, { "shoes", "ivy" } };

    public static bool IsClothing(string slot) => slot == "top" || slot == "bottom" || slot == "dress" || slot == "outer" || slot == "shoes";

    /// <summary>The pieces to draw for what she wears, bottom layer first, accessories last.</summary>
    public static List<Piece> Plan(List<(string slot, string item, Color color)> worn, Func<string, string, bool> has) {
        var plan = new List<Piece>(); bool dress = worn.Exists(w => w.slot == "dress"), outer = worn.Exists(w => w.slot == "outer");
        foreach (string slot in new[] { "shoes", "bottom", "top", "dress", "outer", "glasses", "necklace", "earrings", "hat", "bag" }) {
            int i = worn.FindIndex(w => w.slot == slot); if (i < 0) continue;
            if (dress && (slot == "top" || slot == "bottom")) continue;       // a dress covers both
            if (outer && slot == "top") continue;                             // the jackets come with their own top
            if (!Map.TryGetValue(worn[i].item, out var spec)) { if (!BySlot.TryGetValue(slot, out spec)) continue; }
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
