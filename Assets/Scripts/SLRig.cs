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
    public const int Hips = 0, Spine = 1, Spine1 = 2, Spine2 = 3, Neck = 4, Head = 5, LArm = 7, LFore = 8, LHand = 9, RArm = 11, RFore = 12, RHand = 13,
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

/// <summary>Poses a rigged starlet every frame. Angles follow Starlife Town on the web (its bones are in a right-handed
/// space with x to her left), so y and z turns change sign here. The walk phase moves with distance, so feet don't slide.</summary>
public sealed class SLRigMotion {
    public readonly Transform[] bones; readonly Quaternion[] rest; readonly Vector3 hipsRest;
    public string pose = "stand"; public float seatDrop = 0.47f, lookX, lookY; public bool cup;
    float t, amp, phase; readonly Vector3[] cur;
    public Transform CupHolder { get; private set; }
    public SLRigMotion(Transform[] bones) {
        this.bones = bones; rest = new Quaternion[bones.Length]; cur = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++) rest[i] = bones[i].localRotation;
        hipsRest = bones[SLRig.Hips].localPosition;
        t = UnityEngine.Random.value * 10;
    }
    void Rot(int j, float x, float y, float z, float k) {
        cur[j] = Vector3.Lerp(cur[j], new Vector3(x, y, z), k); var e = cur[j] * Mathf.Rad2Deg;
        bones[j].localRotation = rest[j] * Quaternion.AngleAxis(e.x, Vector3.right) * Quaternion.AngleAxis(-e.y, Vector3.up) * Quaternion.AngleAxis(-e.z, Vector3.forward);
    }
    float hipsY;
    public void Animate(float dt, float speed) {
        float k = 1 - Mathf.Exp(-dt * 14); t += dt;
        float running = Mathf.Clamp01((speed - 1.6f) / 2.4f);
        float targetAmp = speed > 0.05f ? Mathf.Lerp(0.48f, 0.78f, running) : 0;
        amp = Mathf.Lerp(amp, targetAmp, 1 - Mathf.Exp(-dt * 8));
        float cycle = Mathf.Lerp(1.7f, 2.5f, running);
        phase += speed * dt / cycle * Mathf.PI * 2;
        float p = phase, A = amp, breathe = Mathf.Sin(t * 1.7f) * 0.012f;
        if (pose == "sit") {
            hipsY = Mathf.Lerp(hipsY, seatDrop - hipsRest.y + 0.04f, k);
            bones[SLRig.Hips].localPosition = hipsRest + new Vector3(0, hipsY, -0.06f);
            Rot(SLRig.LUp, -1.5f, 0, 0.04f, k); Rot(SLRig.RUp, -1.5f, 0, -0.04f, k);
            Rot(SLRig.LLeg, 1.5f, 0, 0, k); Rot(SLRig.RLeg, 1.5f, 0, 0, k);
            Rot(SLRig.LFoot, 0, 0, 0, k); Rot(SLRig.RFoot, 0, 0, 0, k);
            Rot(SLRig.LArm, -0.45f, 0, 0.1f, k); Rot(SLRig.RArm, cup ? -0.3f : -0.45f, 0, -0.1f, k);
            Rot(SLRig.LFore, -0.9f, 0, 0, k); Rot(SLRig.RFore, cup ? -1.35f : -0.9f, 0, 0, k);
            Rot(SLRig.Spine, -0.05f + breathe, 0, 0, k); Rot(SLRig.Head, Mathf.Sin(t * 0.4f) * 0.08f, Mathf.Sin(t * 0.27f) * 0.25f, 0, k * 0.5f);
            Rot(SLRig.Hips, 0, 0, 0, k);
            return;
        }
        float bob = Mathf.Cos(2 * p) * 0.016f * A * 2;
        hipsY = Mathf.Lerp(hipsY, bob - A * 0.03f * running, k);
        bones[SLRig.Hips].localPosition = hipsRest + new Vector3(0, hipsY, 0);
        float hipL = -Mathf.Sin(p) * A, hipR = Mathf.Sin(p) * A;
        float kneeK = Mathf.Lerp(0.95f, 1.5f, running);
        float knL = 0.05f + kneeK * Mathf.Pow(Mathf.Max(0, Mathf.Cos(p)), 1.6f) * (A / 0.6f) + 0.15f * running;
        float knR = 0.05f + kneeK * Mathf.Pow(Mathf.Max(0, Mathf.Cos(p + Mathf.PI)), 1.6f) * (A / 0.6f) + 0.15f * running;
        Rot(SLRig.LUp, hipL, 0, 0.02f, k); Rot(SLRig.RUp, hipR, 0, -0.02f, k);
        Rot(SLRig.LLeg, knL, 0, 0, k); Rot(SLRig.RLeg, knR, 0, 0, k);
        Rot(SLRig.LFoot, -(hipL + knL) * 0.55f + (A > 0.1f ? Mathf.Max(0, -Mathf.Sin(p)) * 0.25f : 0), 0, 0, k);
        Rot(SLRig.RFoot, -(hipR + knR) * 0.55f + (A > 0.1f ? Mathf.Max(0, Mathf.Sin(p)) * 0.25f : 0), 0, 0, k);
        Rot(SLRig.LArm, Mathf.Sin(p) * A * 0.75f + (A < 0.05f ? Mathf.Sin(t * 1.1f) * 0.02f : 0), 0, 0.05f, k);
        Rot(SLRig.LFore, -0.12f - A * 0.45f - running * 0.9f, 0, 0, k);
        if (pose == "wave") { Rot(SLRig.RArm, -0.2f, 0, -2.5f, k); Rot(SLRig.RFore, 0, Mathf.Sin(t * 9) * 0.5f, -0.5f, k); }
        else if (cup) { Rot(SLRig.RArm, -0.15f - Mathf.Sin(p) * A * 0.2f, 0, -0.08f, k); Rot(SLRig.RFore, -1.35f, 0, 0, k); }
        else { Rot(SLRig.RArm, -Mathf.Sin(p) * A * 0.75f, 0, -0.05f, k); Rot(SLRig.RFore, -0.12f - A * 0.45f - running * 0.9f, 0, 0, k); }
        Rot(SLRig.Hips, 0, Mathf.Sin(p) * 0.09f * A, 0, k);
        Rot(SLRig.Spine, 0.04f * running + breathe, -Mathf.Sin(p) * 0.13f * A, 0, k);
        Rot(SLRig.Head, lookX, lookY, 0, k * 0.4f);
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
