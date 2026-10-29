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
        Put(a, rr, lift);
        // waving, or the cup in the right hand, on top
        if (pose == "wave") { Rot(SLRig.RArm, -0.2f, 0, -2.5f, k); Rot(SLRig.RFore, 0, Mathf.Sin(t * 9) * 0.5f, -0.5f, k); }
        else if (cup) { Rot(SLRig.RArm, -0.15f, 0, -0.08f, k); Rot(SLRig.RFore, -1.35f, 0, 0, k); }
        else { cur[SLRig.RArm] = cur[SLRig.RFore] = Vector3.zero; }
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
