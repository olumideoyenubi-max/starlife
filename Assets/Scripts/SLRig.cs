// Makes a 3D starlet able to move: the pieces SLStage3D puts together are plain meshes baked in her pose, so this gives
// her a skeleton (Resources/Rigs/<avatar>.bytes, written by Tools/blender/rig_export.py) and turns every piece into a
// skinned mesh whose vertices follow the joints of the avatar the piece was cut from (nearest point of that avatar's own
// meshes, with its weights). Hairstyles made for the salon follow the head. SLRigMotion then poses the joints each frame:
// walking, running, sitting and waving, worked out from speed the way Starlife Town on the web does it.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class SLRig {
    public static readonly string[] Joints = { "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
        "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase" };
    public const int Hips = 0, Spine = 1, Spine1 = 2, Spine2 = 3, Neck = 4, Head = 5, LShoulder = 6, RShoulder = 10, LArm = 7, LFore = 8, LHand = 9, RArm = 11, RFore = 12, RHand = 13,
        LUp = 14, LLeg = 15, LFoot = 16, RUp = 18, RLeg = 19, RFoot = 20;
    const string Base = "olivia";

    public readonly string id;
    public readonly int[] parent = new int[Joints.Length];
    public readonly Vector3[] rest = new Vector3[Joints.Length];
    Vector3[] pts; BoneWeight[] wts;
    readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
    const float Cell = 0.03f;

    static readonly Dictionary<string, SLRig> cache = new Dictionary<string, SLRig>();
    public static SLRig Get(string id) {
        if (id == null) return null;
        if (cache.TryGetValue(id, out var r)) return r;
        var ta = Resources.Load<TextAsset>("Rigs/" + id);
        cache[id] = r = ta != null ? new SLRig(id, ta.bytes) : null;
        return r;
    }
    SLRig(string id, byte[] b) {
        this.id = id;
        using (var br = new BinaryReader(new MemoryStream(b))) {
            int nj = br.ReadInt32();
            for (int i = 0; i < nj; i++) {
                int len = br.ReadInt32(); string name = System.Text.Encoding.UTF8.GetString(br.ReadBytes(len));
                int j = Array.IndexOf(Joints, name); int p = br.ReadInt32(); var v = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                if (j >= 0) { parent[j] = p; rest[j] = v; }
            }
            int n = br.ReadInt32(); pts = new Vector3[n]; wts = new BoneWeight[n];
            for (int i = 0; i < n; i++) {
                pts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                var ji = br.ReadBytes(4); var wi = br.ReadBytes(4);
                wts[i] = new BoneWeight { boneIndex0 = ji[0], boneIndex1 = ji[1], boneIndex2 = ji[2], boneIndex3 = ji[3], weight0 = wi[0] / 255f, weight1 = wi[1] / 255f, weight2 = wi[2] / 255f, weight3 = wi[3] / 255f };
                if (!grid.TryGetValue(Key(pts[i]), out var l)) grid[Key(pts[i])] = l = new List<int>(); l.Add(i);
            }
        }
    }
    // ---- accessories ride one part of her: on the head (hats, glasses, earrings), rigid in her hand (a tote, a clutch), or on
    // her body but never her arms or legs (necklaces, ties, a bag across her), so nothing swings with a hand going past it
    static bool OnHead(string part) => part.StartsWith("hat_") || part.StartsWith("glasses_") || part.StartsWith("ear_") || part == "horns" || part == "catears" || part == "earrings_m";
    static bool InHand(string part) => part.StartsWith("hand_");
    static void Pin(BoneWeight[] bw, Vector3[] v, Matrix4x4 toFig, SLRig src, string part) {
        if (OnHead(part)) { for (int i = 0; i < bw.Length; i++) bw[i] = new BoneWeight { boneIndex0 = Head, weight0 = 1 }; return; }
        if (InHand(part)) {      // the bone at the top of the piece (where she holds the handle), for all of it
            int top = 0; for (int i = 1; i < v.Length; i++) if (toFig.MultiplyPoint3x4(v[i]).y > toFig.MultiplyPoint3x4(v[top]).y) top = i;
            var w = src.Nearest(toFig.MultiplyPoint3x4(v[top])); int b = w.boneIndex0 >= LShoulder && w.boneIndex0 <= RHand ? (w.boneIndex0 <= LHand ? LHand : RHand) : w.boneIndex0;
            for (int i = 0; i < bw.Length; i++) bw[i] = new BoneWeight { boneIndex0 = b, weight0 = 1 }; return;
        }
        for (int i = 0; i < bw.Length; i++) {      // the torso: an arm's or a leg's weight goes to the chest or the hips
            var w = bw[i]; float y = toFig.MultiplyPoint3x4(v[i]).y;
            int Fix(int b) => b >= LShoulder && b <= RHand ? Spine2 : b >= LUp ? (y > 0.95f ? Spine : Hips) : b;
            bw[i] = new BoneWeight { boneIndex0 = Fix(w.boneIndex0), weight0 = w.weight0, boneIndex1 = Fix(w.boneIndex1), weight1 = w.weight1, boneIndex2 = Fix(w.boneIndex2), weight2 = w.weight2, boneIndex3 = Fix(w.boneIndex3), weight3 = w.weight3 };
        }
    }
    static long Key(Vector3 p) => Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));
    static long Key(int x, int y, int z) => ((long)(x + 20000) * 40000 + (y + 20000)) * 40000 + (z + 20000);
    /// <summary>The weights of the point of this avatar nearest to p (her own space, metres).</summary>
    BoneWeight Nearest(Vector3 p) {
        int cx = Mathf.FloorToInt(p.x / Cell), cy = Mathf.FloorToInt(p.y / Cell), cz = Mathf.FloorToInt(p.z / Cell);
        int best = -1; float bd = float.MaxValue;
        for (int r = 0; r <= 6; r++) {
            for (int x = -r; x <= r; x++) for (int y = -r; y <= r; y++) for (int z = -r; z <= r; z++) {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z))) != r) continue;
                if (!grid.TryGetValue(Key(cx + x, cy + y, cz + z), out var l)) continue;
                foreach (int i in l) { float d = (pts[i] - p).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            }
            if (best >= 0 && Mathf.Sqrt(bd) < r * Cell) break;      // nothing in a further ring can be closer
        }
        return best >= 0 ? wts[best] : new BoneWeight { boneIndex0 = Spine2, weight0 = 1 };
    }

    // ---------------------------------------------------------------- putting a figure on a skeleton
    static readonly Dictionary<(Mesh, string, Matrix4x4), Mesh> skinned = new Dictionary<(Mesh, string, Matrix4x4), Mesh>();
    /// <summary>Gives a figure built by SLStage3D (a transform holding its pieces) a skeleton and skins every piece to it.
    /// Returns the joints, in the order of Joints.</summary>
    public static Transform[] Attach(Transform figure) {
        // the skeleton is the one of the body she stands in: the shared body, or the avatar whose body or suit she wears
        string body = null;
        foreach (Transform c in figure) { var parts = c.name.Split('/'); if (parts.Length == 3 && parts[0] == "Starlets" && (parts[2] == "body" || parts[2] == "suit")) { body = parts[1]; if (parts[2] == "body") break; } }
        var rig = Get(body) ?? Get(Base);
        var bones = new Transform[Joints.Length];
        var keepPos = figure.localPosition; var keepRot = figure.localRotation; var keepScale = figure.localScale; var keepParent = figure.parent;
        figure.SetParent(null, false); figure.localPosition = Vector3.zero; figure.localRotation = Quaternion.identity; figure.localScale = Vector3.one;
        for (int j = 0; j < Joints.Length; j++) {
            var t = new GameObject(Joints[j]).transform; bones[j] = t;
            t.SetParent(rig.parent[j] >= 0 && rig.parent[j] < j ? bones[rig.parent[j]] : figure, false);
            t.position = figure.TransformPoint(rig.rest[j]);
        }
        var pieces = new List<Transform>(); foreach (Transform c in figure) if (c.GetComponent<MeshFilter>() != null) pieces.Add(c);
        foreach (var c in pieces) {
            var mf = c.GetComponent<MeshFilter>(); var mr = c.GetComponent<MeshRenderer>(); if (mf == null || mr == null || mf.sharedMesh == null) continue;
            var parts = c.name.Split('/');
            SLRig src = parts[0] == "Starlets" ? (Get(parts[1]) ?? Get(Base)) : null;     // salon hairstyles have no rig of their own: they follow the head
            var toFig = figure.worldToLocalMatrix * c.localToWorldMatrix;
            var key = (mf.sharedMesh, (src != null ? src.id : "head") + "|" + rig.id, toFig);
            if (!skinned.TryGetValue(key, out var mesh)) {
                mesh = UnityEngine.Object.Instantiate(mf.sharedMesh); mesh.name = mf.sharedMesh.name + " (skinned)";
                var v = mf.sharedMesh.vertices; var bw = new BoneWeight[v.Length];
                for (int i = 0; i < v.Length; i++) bw[i] = src != null ? src.Nearest(toFig.MultiplyPoint3x4(v[i])) : new BoneWeight { boneIndex0 = Head, weight0 = 1 };
                if (src != null && (parts[1] == "acc" || parts[1] == "goth")) Pin(bw, v, toFig, src, parts[2]);
                mesh.boneWeights = bw;
                var bp = new Matrix4x4[bones.Length]; for (int j = 0; j < bones.Length; j++) bp[j] = bones[j].worldToLocalMatrix * c.localToWorldMatrix;
                mesh.bindposes = bp;
                skinned[key] = mesh;
            }
            var mats = mr.sharedMaterials; var shadow = mr.shadowCastingMode; var block = new MaterialPropertyBlock(); mr.GetPropertyBlock(block);
            UnityEngine.Object.DestroyImmediate(mr); UnityEngine.Object.DestroyImmediate(mf);
            var sk = c.gameObject.AddComponent<SkinnedMeshRenderer>();
            sk.sharedMesh = mesh; sk.bones = bones; sk.rootBone = c; sk.sharedMaterials = mats; sk.shadowCastingMode = shadow;
            sk.localBounds = new Bounds(c.InverseTransformPoint(figure.TransformPoint(new Vector3(0, 0.9f, 0))), new Vector3(2.4f, 2.4f, 2.4f));
            sk.SetPropertyBlock(block);
        }
        figure.SetParent(keepParent, false); figure.localPosition = keepPos; figure.localRotation = keepRot; figure.localScale = keepScale;
        return bones;
    }
}

// Motion-capture clips (Resources/Motion, from Lagos Run): for every sample, each bone's rotation away from a standing
// reference pose in the character's own space, the arms as directions per segment, and the hips' offset. They play on
// any skeleton whose bones stand like the reference, which the starlets' do (the arms are aimed, not rotated).
[Serializable] public class SLClip { public string name; public int samples; public bool loop, standRef; public float seconds; public float[] q, pelvis, arms; }
[Serializable] public class SLClipFile { public string[] bones; public SLClip[] clips; }
public static class SLMoves {
    static readonly Dictionary<string, (SLClip c, int[] slot)> clips = new Dictionary<string, (SLClip, int[])>();
    // the clips' (game-engine) bone for each of SLRig.Joints, or null: clavicles and arms are handled by the arm directions
    static readonly string[] Map = { "pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head", null, null, null, null, null, null, null, null,
        "thigh_l", "calf_l", "foot_l", "ball_l", "thigh_r", "calf_r", "foot_r", "ball_r" };
    public static (SLClip c, int[] slot) Get(string name) {
        if (clips.Count == 0)
            foreach (var path in new[] { "Motion/m2m_base", "Motion/pack" }) {     // later files win
                var ta = Resources.Load<TextAsset>(path); if (ta == null) continue;
                var f = JsonUtility.FromJson<SLClipFile>(ta.text);
                foreach (var c in f.clips) { var slot = new int[Map.Length]; for (int j = 0; j < Map.Length; j++) slot[j] = Map[j] == null ? -1 : Array.IndexOf(f.bones, Map[j]); clips[c.name] = (c, slot); }
            }
        return clips.TryGetValue(name, out var r) ? r : (null, null);
    }
}

/// <summary>Poses a rigged starlet every frame: motion-capture walking, running and standing (SLMoves), cross-faded by
/// speed with the cycle moved on by the distance covered so the feet don't slide; a talking idle in conversation; and
/// on top, by hand, sitting, waving and holding a take-away cup.</summary>
public sealed class SLRigMotion {
    public readonly Transform[] bones; readonly Transform root; readonly Quaternion[] rest, bindRoot; readonly Vector3 hipsRest; readonly float hipsHeight;
    public string pose = "stand"; public float seatDrop = 0.47f, lookX, lookY; public bool cup, talking;
    /// <summary>An emote while standing (dance, phone, arms, nod, crouch: motion clips; clap, cheer, laugh, think, pose: by hand), or
    /// what she does in a seat (relax, chat, phone, eat, lean); null for none. Walking away ends a standing emote.</summary>
    public string emote, sitStyle;
    float emoteW, emoteT;
    static readonly Dictionary<string, string> EmoteClips = new Dictionary<string, string> {
        { "dance", "Dance_Simple" }, { "phone", "Idle_TalkingPhone" }, { "arms", "Idle_FoldArms" }, { "nod", "Yes" }, { "crouch", "Crouch_Idle" }, { "eat", "Consume" },
        { "box", "Punch_Cross" } };
    /// <summary>Workouts: running on a treadmill (the run cycle on the spot), boxing (a clip), and by hand: dumbbell curls,
    /// squats, jumping jacks, a yoga flow and stretches.</summary>
    public static readonly string[] Workouts = { "treadmill", "box", "curl", "squat", "jacks", "yoga", "stretch" };
    float t, cycle, idleT, runW, moveW, sitW;
    readonly Vector3[] cur; readonly Quaternion[] a = new Quaternion[22], b = new Quaternion[22], c3 = new Quaternion[22];
    public Transform CupHolder { get; private set; }
    public SLRigMotion(Transform[] bones) {
        this.bones = bones; root = bones[0].parent; rest = new Quaternion[bones.Length]; bindRoot = new Quaternion[bones.Length]; cur = new Vector3[bones.Length];
        var inv = Quaternion.Inverse(root.rotation);
        for (int i = 0; i < bones.Length; i++) { rest[i] = bones[i].localRotation; bindRoot[i] = inv * bones[i].rotation; }
        hipsRest = bones[SLRig.Hips].localPosition; hipsHeight = Mathf.Max(0.5f, bones[SLRig.Hips].position.y - root.position.y);
        t = UnityEngine.Random.value * 10; idleT = UnityEngine.Random.value;
    }
    // ---- clips
    Vector3 Sample(string name, float ct, Quaternion rr, Quaternion[] outRot, out bool ok) {
        var (c, slot) = SLMoves.Get(name); ok = c != null; if (!ok) return Vector3.zero;
        float x = Mathf.Repeat(ct, 1) * c.samples; int i0 = Mathf.Min((int)x, c.samples - 1), i1 = (i0 + 1) % c.samples; float f = x - i0;
        int stride = c.q.Length / c.samples;
        for (int j = 0; j < slot.Length; j++) {
            int ci = slot[j]; if (ci < 0) { outRot[j] = Quaternion.identity; continue; }
            int p = i0 * stride + ci * 4, q = i1 * stride + ci * 4;
            outRot[j] = Quaternion.Slerp(new Quaternion(c.q[p], c.q[p + 1], c.q[p + 2], c.q[p + 3]), new Quaternion(c.q[q], c.q[q + 1], c.q[q + 2], c.q[q + 3]), f);
        }
        // arm directions: [side][upper, fore] in the character's space, kept in the spare slots of outRot as look rotations
        for (int side = 0; side < 2; side++) for (int seg = 0; seg < 2; seg++) {
            int p = i0 * 18 + side * 9 + seg * 3, q = i1 * 18 + side * 9 + seg * 3;
            var d = Vector3.Slerp(new Vector3(c.arms[p], c.arms[p + 1], c.arms[p + 2]), new Vector3(c.arms[q], c.arms[q + 1], c.arms[q + 2]), f);
            outRot[(side == 0 ? SLRig.LArm : SLRig.RArm) + seg] = Quaternion.LookRotation(d);
        }
        int pa = i0 * 3, pb = i1 * 3;
        var lift = Vector3.Lerp(new Vector3(c.pelvis[pa], c.pelvis[pa + 1], c.pelvis[pa + 2]), new Vector3(c.pelvis[pb], c.pelvis[pb + 1], c.pelvis[pb + 2]), f);
        lift.z = 0; return lift;
    }
    static readonly int[] ArmSegs = { SLRig.LArm, SLRig.LFore, SLRig.RArm, SLRig.RFore };
    void Blend(Quaternion[] into, Quaternion[] from, float w) { if (w <= 0) return; for (int j = 0; j < into.Length; j++) into[j] = Quaternion.Slerp(into[j], from[j], w); }
    void Put(Quaternion[] r, Quaternion rr, Vector3 lift) {
        for (int j = 0; j < bones.Length; j++) {
            if (j == SLRig.LShoulder || j == SLRig.RShoulder || j == SLRig.LHand || j == SLRig.RHand || Array.IndexOf(ArmSegs, j) >= 0) { bones[j].localRotation = rest[j]; continue; }
            bones[j].rotation = rr * r[j] * bindRoot[j];
        }
        // aim each arm segment along the clip's direction (the starlets stand with their arms down, unlike the reference)
        foreach (int j in ArmSegs) {
            var tip = bones[j + 1]; var want = rr * (r[j] * Vector3.forward);
            bones[j].rotation = Quaternion.FromToRotation(tip.position - bones[j].position, want) * bones[j].rotation;
        }
        bones[SLRig.Hips].localPosition = hipsRest; bones[SLRig.Hips].position += rr * lift * hipsHeight;
    }
    // ---- hand-made poses on top
    void Rot(int j, float x, float y, float z, float k) {
        cur[j] = Vector3.Lerp(cur[j], new Vector3(x, y, z), k); var e = cur[j] * Mathf.Rad2Deg;
        bones[j].localRotation = rest[j] * Quaternion.AngleAxis(e.x, Vector3.right) * Quaternion.AngleAxis(-e.y, Vector3.up) * Quaternion.AngleAxis(-e.z, Vector3.forward);
    }
    readonly Quaternion[] keep = new Quaternion[22]; Vector3 keepHips;
    public void Animate(float dt, float speed) {
        t += dt; float k = 1 - Mathf.Exp(-dt * 12);
        if (emote == "treadmill") speed = 3.1f;      // running, on the spot
        Dumbbells(emote == "curl");
        var rr = root.rotation;
        sitW = Mathf.MoveTowards(sitW, pose == "sit" ? 1 : 0, dt * 3.5f);
        if (sitW >= 1) { Sit(dt); return; }
        // walk and run: one cycle moved on by distance, so both clips keep their feet together while they blend
        runW = Mathf.Lerp(runW, Mathf.Clamp01((speed - 1.9f) / 1.6f), 1 - Mathf.Exp(-dt * 6));
        moveW = Mathf.Lerp(moveW, Mathf.Clamp01(speed / 0.6f), 1 - Mathf.Exp(-dt * 8));
        float stride = Mathf.Lerp(1.38f, 2.5f, runW) * (hipsHeight / 0.9f);
        cycle += speed * dt / stride; idleT += dt / 6.5f;
        bool okA, okB;
        var lift = Sample(talking ? "Idle_Talking" : "Idle", talking ? idleT * 6.5f / 3.67f : idleT * 6.5f / 1.6f, rr, a, out okA);
        if (moveW > 0.01f) {
            var l2 = Sample("Walk", cycle, rr, b, out okB);
            if (runW > 0.01f) { var l3 = Sample("Run", cycle, rr, c3, out _); Blend(b, c3, runW); l2 = Vector3.Lerp(l2, l3, runW); }
            if (okB) { Blend(a, b, moveW); lift = Vector3.Lerp(lift, l2, moveW); }
        }
        if (!okA) { for (int j = 0; j < a.Length; j++) a[j] = Quaternion.identity; }
        // an emote, eased in while she stands still
        emoteW = Mathf.MoveTowards(emoteW, emote != null && moveW < 0.25f ? 1 : 0, dt * 3);
        if (emote == null && emoteW <= 0) emoteT = 0; else emoteT += dt;
        if (emoteW > 0 && emote != null && EmoteClips.TryGetValue(emote, out var clip)) {
            var (ec, _) = SLMoves.Get(clip);
            if (ec != null) { var le = Sample(clip, emoteT / ec.seconds, rr, c3, out bool okE); if (okE) { Blend(a, c3, emoteW); lift = Vector3.Lerp(lift, le, emoteW); } }
        }
        Put(a, rr, lift);
        if (emoteW > 0 && emote != null && !EmoteClips.ContainsKey(emote)) Gesture(emote, emoteW, k);
        // waving, or the cup in the right hand, on top
        bool gesture = emoteW > 0.01f && emote != null && !EmoteClips.ContainsKey(emote);     // a hand-made emote has the arms
        if (!gesture && pose == "wave") { Rot(SLRig.RArm, -0.2f, 0, -2.5f, k); Rot(SLRig.RFore, 0, Mathf.Sin(t * 9) * 0.5f, -0.5f, k); }
        else if (!gesture && cup) { Rot(SLRig.RArm, -0.15f, 0, -0.08f, k); Rot(SLRig.RFore, -1.35f, 0, 0, k); }
        else if (!gesture) { cur[SLRig.RArm] = cur[SLRig.RFore] = Vector3.zero; }
        if (lookX != 0 || lookY != 0) bones[SLRig.Head].localRotation *= Quaternion.Euler(lookX * Mathf.Rad2Deg, -lookY * Mathf.Rad2Deg, 0);
        // easing into a seat: blend from the standing pose toward sitting
        if (sitW > 0) { for (int j = 0; j < bones.Length; j++) keep[j] = bones[j].localRotation; keepHips = bones[SLRig.Hips].localPosition; Sit(dt); 
            for (int j = 0; j < bones.Length; j++) bones[j].localRotation = Quaternion.Slerp(keep[j], bones[j].localRotation, sitW);
            bones[SLRig.Hips].localPosition = Vector3.Lerp(keepHips, bones[SLRig.Hips].localPosition, sitW); }
    }
    void Sit(float dt) {
        float k = 1, breathe = Mathf.Sin(t * 1.7f) * 0.012f;
        for (int j = 0; j < bones.Length; j++) { bones[j].localRotation = rest[j]; cur[j] = Vector3.zero; }
        bones[SLRig.Hips].localPosition = hipsRest + new Vector3(0, seatDrop - hipsRest.y + 0.04f, -0.06f);
        Rot(SLRig.LUp, -1.5f, 0, 0.04f, k); Rot(SLRig.RUp, -1.5f, 0, -0.04f, k);
        Rot(SLRig.LLeg, 1.5f, 0, 0, k); Rot(SLRig.RLeg, 1.5f, 0, 0, k);
        Rot(SLRig.LArm, -0.45f, 0, 0.1f, k); Rot(SLRig.RArm, cup ? -0.3f : -0.45f, 0, -0.1f, k);
        Rot(SLRig.LFore, -0.9f, 0, 0, k); Rot(SLRig.RFore, cup ? -1.35f : -0.9f, 0, 0, k);
        Rot(SLRig.Spine, -0.05f + breathe, 0, 0, k); Rot(SLRig.Head, Mathf.Sin(t * 0.4f) * 0.08f, Mathf.Sin(t * 0.27f) * 0.25f, 0, k);
        // what she does in her seat
        switch (sitStyle) {
            case "chat":       // talking with her hands, turning to the people beside her
                Rot(SLRig.RFore, -1.1f - Mathf.Sin(t * 2.3f) * 0.35f, Mathf.Sin(t * 1.7f) * 0.3f, 0, k); Rot(SLRig.LFore, -1.0f - Mathf.Sin(t * 1.9f + 1) * 0.25f, 0, 0, k);
                Rot(SLRig.Head, Mathf.Sin(t * 1.3f) * 0.08f, Mathf.Sin(t * 0.5f) * 0.45f, 0, k); break;
            case "phone":      // scrolling: both hands up in front, head down
                Rot(SLRig.LArm, -0.55f, 0, 0.35f, k); Rot(SLRig.RArm, -0.55f, 0, -0.35f, k); Rot(SLRig.LFore, -1.75f, 0, 0, k); Rot(SLRig.RFore, -1.75f, Mathf.Sin(t * 3) * 0.05f, 0, k);
                Rot(SLRig.Head, 0.42f, 0, 0, k); Rot(SLRig.Neck, 0.15f, 0, 0, k); break;
            case "eat":        // a bite or a sip, now and then
                float b = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 1.6f)), 3);
                Rot(SLRig.RArm, -0.45f - b * 0.35f, 0, -0.15f, k); Rot(SLRig.RFore, -0.9f - b * 1.25f, 0, 0, k); Rot(SLRig.Head, 0.1f - b * 0.1f, 0, 0, k); break;
            case "lean":       // leaning back, at ease
                Rot(SLRig.Spine, -0.22f + breathe, 0, 0, k); Rot(SLRig.Spine1, -0.1f, 0, 0, k); Rot(SLRig.LArm, 0.1f, 0, 0.25f, k); Rot(SLRig.RArm, 0.1f, 0, -0.25f, k);
                Rot(SLRig.LFore, -0.3f, 0, 0, k); Rot(SLRig.RFore, -0.3f, 0, 0, k); Rot(SLRig.Head, -0.15f, Mathf.Sin(t * 0.3f) * 0.3f, 0, k); break;
        }
    }
    /// <summary>Both legs along these directions (the left's; the right's mirrored), the hips lowered so the feet stay on the floor, plus a hop.</summary>
    void Legs(Vector3 thigh, Vector3 shin, float hop) {
        thigh.Normalize(); shin.Normalize();
        float drop = hipsHeight * (1 - (Mathf.Abs(thigh.y) + Mathf.Abs(shin.y)) / 2);
        bones[SLRig.Hips].position += root.up * (hop * hipsHeight - drop);
        Aim(SLRig.LUp, thigh); Aim(SLRig.LLeg, shin); Aim(SLRig.RUp, new Vector3(-thigh.x, thigh.y, thigh.z)); Aim(SLRig.RLeg, new Vector3(-shin.x, shin.y, shin.z));
    }
    readonly List<GameObject> bells = new List<GameObject>();
    GameObject phone;
    /// <summary>Her phone, the Starphone Duo, in her right hand: a slim slab with a lit screen and two camera lenses on the back.</summary>
    public void Phone(bool on) {
        if (!on) { if (phone != null) UnityEngine.Object.Destroy(phone); phone = null; return; }
        if (phone != null) return;
        var sh = Resources.Load<Shader>("SLTown"); int layer = bones[SLRig.RHand].gameObject.layer;
        Material M(Color c, float g, float m, Color? glow = null) { var x = new Material(sh); x.SetColor("_Color", c); x.SetFloat("_Glossiness", g); x.SetFloat("_Metallic", m); if (glow.HasValue) { x.SetColor("_EmissionColor", glow.Value); x.SetFloat("_EmA", 1); } return x; }
        var h = new GameObject("Starphone Duo").transform; h.SetParent(bones[SLRig.RHand], false); h.localPosition = new Vector3(0.0f, -0.085f, 0.03f);
        h.rotation = Quaternion.LookRotation(-root.forward, root.up) * Quaternion.Euler(-35, 0, 0);       // screen up toward her face
        void P(PrimitiveType ty, Vector3 at, Vector3 size, Material m) { var g = GameObject.CreatePrimitive(ty); UnityEngine.Object.Destroy(g.GetComponent<Collider>()); g.transform.SetParent(h, false); g.transform.localPosition = at; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = m; g.layer = layer; }
        P(PrimitiveType.Cube, Vector3.zero, new Vector3(0.072f, 0.148f, 0.008f), M(new Color(0.85f, 0.78f, 0.72f), 0.8f, 0.9f));          // titanium, rose
        P(PrimitiveType.Cube, new Vector3(0, 0, -0.0042f), new Vector3(0.066f, 0.142f, 0.001f), M(Color.black, 0.95f, 0, new Color(0.45f, 0.35f, 0.75f) * 1.4f));   // the screen, lit
        P(PrimitiveType.Cube, new Vector3(0.016f, 0.05f, 0.0048f), new Vector3(0.03f, 0.03f, 0.002f), M(new Color(0.75f, 0.68f, 0.62f), 0.9f, 0.9f));         // the camera bump
        foreach (float y in new[] { 0.042f, 0.058f }) P(PrimitiveType.Cylinder, new Vector3(0.016f, y, 0.0062f), new Vector3(0.012f, 0.001f, 0.012f), M(new Color(0.05f, 0.05f, 0.08f), 1, 0.3f));
        foreach (Transform c in h) c.localRotation = c.GetComponent<MeshFilter>().sharedMesh.name.Contains("Cylinder") ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
        h.gameObject.layer = layer; phone = h.gameObject;
    }
    /// <summary>A dumbbell in each hand while she curls.</summary>
    void Dumbbells(bool on) {
        if (!on) { foreach (var b in bells) UnityEngine.Object.Destroy(b); bells.Clear(); return; }
        if (bells.Count > 0) return;
        var sh = Resources.Load<Shader>("SLTown");
        var iron = new Material(sh); iron.SetColor("_Color", new Color(0.12f, 0.12f, 0.14f)); iron.SetFloat("_Glossiness", 0.55f); iron.SetFloat("_Metallic", 0.6f);
        var grip = new Material(sh); grip.SetColor("_Color", new Color(0.75f, 0.76f, 0.78f)); grip.SetFloat("_Glossiness", 0.8f); grip.SetFloat("_Metallic", 1);
        foreach (int hand in new[] { SLRig.LHand, SLRig.RHand }) {
            var h = new GameObject("dumbbell").transform; h.SetParent(bones[hand], false); h.localPosition = new Vector3(0, -0.08f, 0.02f);
            h.rotation = Quaternion.LookRotation(root.forward, root.up) * Quaternion.Euler(0, 0, 90); bells.Add(h.gameObject);
            void P(PrimitiveType ty, Vector3 at, Vector3 size, Material m) { var g = GameObject.CreatePrimitive(ty); UnityEngine.Object.Destroy(g.GetComponent<Collider>()); g.transform.SetParent(h, false); g.transform.localPosition = at; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = m; g.layer = bones[hand].gameObject.layer; }
            P(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.028f, 0.09f, 0.028f), grip);
            foreach (float y in new[] { -0.09f, 0.09f }) P(PrimitiveType.Cylinder, new Vector3(0, y, 0), new Vector3(0.085f, 0.03f, 0.085f), iron);
            h.gameObject.layer = bones[hand].gameObject.layer;
        }
    }
    /// <summary>Points an arm segment along a direction in her own space (x toward her left side, y up, z forward), the way the
    /// clips' arms are put on, so a gesture comes out the same on every figure whatever its bones' axes.</summary>
    void Aim(int j, Vector3 dir) {
        var left = root.InverseTransformPoint(bones[SLRig.LArm].position).x >= 0 ? 1f : -1f;      // which way her left is in this rig
        var d = root.TransformDirection(new Vector3(dir.x * left, dir.y, dir.z).normalized);
        var tip = bones[j + 1]; bones[j].rotation = Quaternion.FromToRotation(tip.position - bones[j].position, d) * bones[j].rotation;
    }
    /// <summary>The hand-made emotes, over the standing pose (w eases them in and out).</summary>
    void Gesture(string g, float w, float k) {
        float s = Mathf.Sin(t * 13); var keepR = new Quaternion[bones.Length]; for (int j = 0; j < bones.Length; j++) keepR[j] = bones[j].localRotation;
        switch (g) {
            case "clap":       // elbows in, hands meeting in front of her chest
                float o = 0.22f + 0.2f * Mathf.Max(0, s);
                Aim(SLRig.LArm, new Vector3(0.18f, -0.75f, 0.45f)); Aim(SLRig.RArm, new Vector3(-0.18f, -0.75f, 0.45f));
                Aim(SLRig.LFore, new Vector3(-o, 0.25f, 0.9f)); Aim(SLRig.RFore, new Vector3(o, 0.25f, 0.9f)); break;
            case "cheer":
                float j2 = Mathf.Abs(Mathf.Sin(t * 5));
                Rot(SLRig.LArm, -0.25f, 0, 2.5f + j2 * 0.2f, k); Rot(SLRig.RArm, -0.25f, 0, -2.5f - j2 * 0.2f, k); Rot(SLRig.LFore, 0, 0, 0.3f, k); Rot(SLRig.RFore, 0, 0, -0.3f, k);
                Rot(SLRig.Head, -0.2f, 0, 0, k); break;
            case "laugh":
                float h = Mathf.Abs(Mathf.Sin(t * 9));
                Rot(SLRig.Spine1, 0.12f + h * 0.08f, 0, 0, k); Rot(SLRig.Head, -0.25f + h * 0.12f, 0, 0, k);
                Rot(SLRig.LArm, -0.35f, 0, 0.35f, k); Rot(SLRig.RArm, -0.35f, 0, -0.35f, k); Rot(SLRig.LFore, -1.5f, 0, 0, k); Rot(SLRig.RFore, -1.5f, 0, 0, k); break;
            case "think":
                Rot(SLRig.RArm, -0.75f, 0, -0.25f, k); Rot(SLRig.RFore, -2.3f, 0.2f, 0, k); Rot(SLRig.LArm, -0.5f, 0, 0.5f, k); Rot(SLRig.LFore, -1.4f, 0, 0, k);
                Rot(SLRig.Head, 0.12f, 0.15f, 0.12f, k); break;
            case "pose":       // a hand on her hip, the other behind her head, weight to one side
                Aim(SLRig.LArm, new Vector3(0.75f, -0.62f, -0.15f)); Aim(SLRig.LFore, new Vector3(-0.85f, -0.45f, 0.2f));
                Aim(SLRig.RArm, new Vector3(-0.7f, 0.68f, 0.05f)); Aim(SLRig.RFore, new Vector3(0.75f, 0.35f, -0.35f));
                Rot(SLRig.Spine, 0, 0, 0.08f, k); Rot(SLRig.Head, 0, -0.2f, -0.12f, k); break;
            case "wave":
                Rot(SLRig.RArm, -0.2f, 0, -2.5f, k); Rot(SLRig.RFore, 0, Mathf.Sin(t * 9) * 0.5f, -0.5f, k); break;
            case "text": {     // looking at her phone, held in both hands in front of her
                Aim(SLRig.LArm, new Vector3(0.14f, -0.88f, 0.42f)); Aim(SLRig.RArm, new Vector3(-0.14f, -0.88f, 0.42f));
                Aim(SLRig.LFore, new Vector3(-0.4f, 0.42f, 0.82f)); Aim(SLRig.RFore, new Vector3(0.4f, 0.42f, 0.82f));
                Rot(SLRig.Head, 0.38f, 0, 0, k); Rot(SLRig.Neck, 0.12f, 0, 0, k); break; }
            case "curl": {     // dumbbell curls, one arm and then the other
                float a = Mathf.SmoothStep(0, 1, Mathf.PingPong(t / 0.9f, 1)), b2 = Mathf.SmoothStep(0, 1, Mathf.PingPong(t / 0.9f + 1, 1));
                Aim(SLRig.LArm, new Vector3(0.14f, -0.97f, 0.12f)); Aim(SLRig.RArm, new Vector3(-0.14f, -0.97f, 0.12f));
                Aim(SLRig.LFore, Vector3.Lerp(new Vector3(0.1f, -0.97f, 0.2f), new Vector3(0.05f, 0.62f, 0.78f), a)); Aim(SLRig.RFore, Vector3.Lerp(new Vector3(-0.1f, -0.97f, 0.2f), new Vector3(-0.05f, 0.62f, 0.78f), b2));
                break; }
            case "squat": {    // down with the arms out in front, and up again
                float p = Mathf.SmoothStep(0, 1, Mathf.PingPong(t / 1.1f, 1));
                Legs(Vector3.Lerp(new Vector3(0.08f, -1, 0), new Vector3(0.22f, -0.42f, 0.88f), p), Vector3.Lerp(new Vector3(0.05f, -1, 0), new Vector3(0.05f, -0.95f, -0.28f), p), 0);
                Rot(SLRig.Spine, 0.35f * p, 0, 0, k);
                Aim(SLRig.LArm, Vector3.Lerp(new Vector3(0.15f, -0.95f, 0.1f), new Vector3(0.12f, 0.05f, 1), p)); Aim(SLRig.RArm, Vector3.Lerp(new Vector3(-0.15f, -0.95f, 0.1f), new Vector3(-0.12f, 0.05f, 1), p));
                Aim(SLRig.LFore, Vector3.Lerp(new Vector3(0.15f, -0.95f, 0.15f), new Vector3(0.1f, 0.05f, 1), p)); Aim(SLRig.RFore, Vector3.Lerp(new Vector3(-0.15f, -0.95f, 0.15f), new Vector3(-0.1f, 0.05f, 1), p));
                break; }
            case "jacks": {    // jumping jacks: arms over the head and the feet apart, then together
                float p = Mathf.SmoothStep(0, 1, Mathf.PingPong(t / 0.38f, 1));
                Legs(Vector3.Lerp(new Vector3(0.04f, -1, 0), new Vector3(0.32f, -0.95f, 0), p), Vector3.Lerp(new Vector3(0.04f, -1, 0), new Vector3(0.3f, -0.95f, 0), p), Mathf.Sin(p * Mathf.PI) * 0.06f);
                var up = Vector3.Lerp(new Vector3(0.3f, -0.95f, 0.05f), new Vector3(0.45f, 0.89f, 0.05f), p);
                Aim(SLRig.LArm, up); Aim(SLRig.LFore, up); Aim(SLRig.RArm, new Vector3(-up.x, up.y, up.z)); Aim(SLRig.RFore, new Vector3(-up.x, up.y, up.z));
                break; }
            case "yoga": {     // a slow flow: arms raised, warrior two, tree pose
                float c = Mathf.Repeat(t / 4.5f, 3); int i0 = (int)c; float f = Mathf.SmoothStep(0, 1, Mathf.Clamp01((c - i0 - 0.7f) / 0.3f));
                var P = new[] {
                    (lu: new Vector3(0.12f, 0.99f, 0.05f), lf: new Vector3(0.05f, 1, 0.05f), th: new Vector3(0.06f, -1, 0), sh: new Vector3(0.05f, -1, 0), rth: new Vector3(-0.06f, -1, 0), rsh: new Vector3(-0.05f, -1, 0)),
                    (lu: new Vector3(1, 0.02f, 0), lf: new Vector3(1, 0.02f, 0), th: new Vector3(0.45f, -0.7f, 0.25f), sh: new Vector3(0.2f, -1, -0.1f), rth: new Vector3(-0.45f, -0.9f, 0), rsh: new Vector3(-0.4f, -0.92f, 0)),
                    (lu: new Vector3(0.45f, -0.55f, 0.6f), lf: new Vector3(-0.7f, 0.45f, 0.55f), th: new Vector3(0.04f, -1, 0), sh: new Vector3(0.03f, -1, 0), rth: new Vector3(-0.85f, -0.35f, 0.3f), rsh: new Vector3(0.75f, -0.65f, 0.05f)) };
                var A = P[i0]; var B = P[(i0 + 1) % 3];
                Vector3 L(Vector3 x, Vector3 y) => Vector3.Slerp(x.normalized, y.normalized, f);
                Aim(SLRig.LArm, L(A.lu, B.lu)); Aim(SLRig.LFore, L(A.lf, B.lf));
                var ru = L(A.lu, B.lu); var rf = L(A.lf, B.lf); Aim(SLRig.RArm, new Vector3(-ru.x, ru.y, ru.z)); Aim(SLRig.RFore, new Vector3(-rf.x, rf.y, rf.z));
                Aim(SLRig.LUp, L(A.th, B.th)); Aim(SLRig.LLeg, L(A.sh, B.sh)); Aim(SLRig.RUp, L(A.rth, B.rth)); Aim(SLRig.RLeg, L(A.rsh, B.rsh));
                break; }
            case "stretch": {  // side bends, one arm over the head
                float sb = Mathf.Sin(t * 1.2f);
                Rot(SLRig.Spine, 0, 0, 0.22f * sb, k); Rot(SLRig.Spine1, 0, 0, 0.15f * sb, k);
                if (sb > 0) { Aim(SLRig.LArm, new Vector3(0.2f, 0.98f, 0)); Aim(SLRig.LFore, new Vector3(-0.45f, 0.89f, 0)); Aim(SLRig.RArm, new Vector3(-0.3f, -0.95f, 0.1f)); }
                else { Aim(SLRig.RArm, new Vector3(-0.2f, 0.98f, 0)); Aim(SLRig.RFore, new Vector3(0.45f, 0.89f, 0)); Aim(SLRig.LArm, new Vector3(0.3f, -0.95f, 0.1f)); }
                break; }
        }
        if (w < 1) for (int j = 0; j < bones.Length; j++) bones[j].localRotation = Quaternion.Slerp(keepR[j], bones[j].localRotation, w);
    }
    /// <summary>A take-away cup in her right hand.</summary>
    public void Cup(bool on, Material cupM, Material sleeveM, Material lidM) {
        cup = on;
        if (!on) { if (CupHolder != null) UnityEngine.Object.Destroy(CupHolder.gameObject); CupHolder = null; return; }
        if (CupHolder != null) return;
        var h = new GameObject("Cup").transform; h.SetParent(bones[SLRig.RHand], false);
        h.localPosition = new Vector3(0.0f, -0.07f, 0.04f);
        void Part(Material m, float r, float len, float y) {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); UnityEngine.Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(h, false); g.transform.localScale = new Vector3(r * 2, len / 2, r * 2); g.transform.localPosition = new Vector3(0, y, 0);
            g.GetComponent<Renderer>().sharedMaterial = m;
        }
        Part(cupM, 0.033f, 0.12f, 0); Part(sleeveM, 0.036f, 0.05f, 0); Part(lidM, 0.038f, 0.014f, 0.066f);
        CupHolder = h;
    }
}
