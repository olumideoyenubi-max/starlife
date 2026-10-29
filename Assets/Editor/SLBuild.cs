using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

// One-off project setup and command-line builds:
//   Unity -batchmode -quit -projectPath <path> -executeMethod SLBuild.Setup
//   Unity -batchmode -quit -projectPath <path> -executeMethod SLBuild.BuildMac
public static class SLBuild {
    const string ScenePath = "Assets/Scenes/Main.unity", PanelPath = "Assets/Resources/StarlifePanel.asset", ThemePath = "Assets/Resources/StarlifeTheme.tss";

    [MenuItem("Starlife/Set Up Project")]
    public static void Setup() {
        if (!File.Exists(ThemePath)) { File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n"); AssetDatabase.ImportAsset(ThemePath); }
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
        if (panel == null) { panel = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(panel, PanelPath); }
        panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1440, 900);
        panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panel.match = 1f;
        panel.clearColor = false;
        EditorUtility.SetDirty(panel);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0xcf, 0xe6, 0xf7, 255); cam.orthographic = true; cam.tag = "MainCamera";
        var go = new GameObject("Starlife");
        var doc = go.AddComponent<UIDocument>(); doc.panelSettings = panel;
        go.AddComponent<SLApp>();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.companyName = "Olumide Oyenubi";
        PlayerSettings.productName = "Starlife";
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.olumideoyenubi.starlife");
        Mobile();
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
        Debug.Log("SLBuild.Setup done");
    }

    // Turns the avatars in Assets/Starlets/<id> (pieces written by Tools/blender/wardrobe_export.py) and the hairstyles in
    // Assets/Hair/<id> (Tools/blender/hair_fit.py) into prefabs under Resources, one child per piece, with the game's own
    // materials, and writes Resources/starlife3d.json so the game knows what there is.
    [System.Serializable] class Part { public string mesh, material, tex, normal; public bool alpha; public float[] color, tone; public float rough, metal, lum; }
    [System.Serializable] class Manifest { public string id, label; public Part[] parts; }
    [System.Serializable] class P3 { public string mesh, material; public float[] tone; public float lum; }
    [System.Serializable] class E3 { public string id, label; public P3[] parts; }
    [System.Serializable] class Index3 { public System.Collections.Generic.List<E3> starlets = new System.Collections.Generic.List<E3>(), hair = new System.Collections.Generic.List<E3>(), furniture = new System.Collections.Generic.List<E3>(); }
    [MenuItem("Starlife/Import 3D Starlets")]
    public static void Import3D() {
        AssetDatabase.Refresh();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/StarletTint.shader"); var index = new Index3();
        ImportSet("Assets/Starlets", "starlet.fbx", "Assets/Resources/Starlets", shader, index.starlets);
        ImportSet("Assets/Hair", "hair.fbx", "Assets/Resources/Hair", shader, index.hair);
        if (Directory.Exists("Assets/Resources/Room")) foreach (var f in Directory.GetFiles("Assets/Resources/Room")) {      // room materials: normal maps as normal maps, all sharp at a slant
            var ti = AssetImporter.GetAtPath(f.Replace('\\', '/')) as TextureImporter; if (ti == null) continue;
            var want = f.EndsWith("_n.jpg") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (ti.textureType != want || ti.anisoLevel != 8) { ti.textureType = want; ti.anisoLevel = 8; ti.wrapMode = TextureWrapMode.Repeat; ti.SaveAndReimport(); }
        }
        ImportSet("Assets/Furniture", "prop.fbx", "Assets/Resources/Furniture", shader, index.furniture);   // from Tools/blender/prop_export.py
        File.WriteAllText("Assets/Resources/starlife3d.json", JsonUtility.ToJson(index)); AssetDatabase.ImportAsset("Assets/Resources/starlife3d.json");
        AssetDatabase.SaveAssets();
        Debug.Log("SLBuild.Import3D done: " + index.starlets.Count + " avatars, " + index.hair.Count + " hairstyles, " + index.furniture.Count + " furniture models");
    }
    static void ImportSet(string src, string fbx, string outDir, Shader shader, System.Collections.Generic.List<E3> index) {
        if (!Directory.Exists(src)) return;
        Directory.CreateDirectory(outDir);
        var dirs = Directory.GetDirectories(src); System.Array.Sort(dirs);
        foreach (var dir in dirs) {
            string d = dir.Replace('\\', '/'), mf = d + "/manifest.json"; if (!File.Exists(mf) || !File.Exists(d + "/" + fbx)) continue;
            var man = JsonUtility.FromJson<Manifest>(File.ReadAllText(mf));
            var mats = new System.Collections.Generic.Dictionary<string, Material>(); var e = new E3 { id = man.id, label = man.label ?? man.id, parts = new P3[man.parts.Length] };
            for (int i = 0; i < man.parts.Length; i++) {
                var p = man.parts[i]; e.parts[i] = new P3 { mesh = p.mesh, material = p.material, tone = p.tone, lum = p.lum };
                if (!string.IsNullOrEmpty(p.normal)) {
                    var ti = AssetImporter.GetAtPath(d + "/" + p.normal) as TextureImporter;
                    if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                }
                string path = d + "/" + p.mesh + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
                bool hasTex = !string.IsNullOrEmpty(p.tex), isHair = fbx == "hair.fbx" || p.mesh == "hair", isSkin = p.mesh == "body" || p.mesh == "head" || p.mesh == "feet";
                if (hasTex) {      // full-quality textures: no blocky compression, sharp at a slant
                    var tx = AssetImporter.GetAtPath(d + "/" + p.tex) as TextureImporter;
                    if (tx != null && (tx.textureCompression != TextureImporterCompression.CompressedHQ || tx.anisoLevel != 8)) { tx.textureCompression = TextureImporterCompression.CompressedHQ; tx.anisoLevel = 8; tx.mipmapEnabled = true; tx.SaveAndReimport(); }
                }
                m.shader = shader;
                m.SetFloat("_Sheen", isHair ? 0.12f : isSkin ? 0.22f : fbx == "prop.fbx" ? 0 : 0.4f);
                m.SetFloat("_SSS", isSkin ? 0.32f : 0); m.SetFloat("_Hair", isHair && !p.material.StartsWith("Bead") ? 1 : 0);
                m.SetFloat("_Detail", isHair ? (hasTex ? 0.35f : 0.9f) : isSkin ? 0.1f : 0); m.SetFloat("_DetailScale", isHair ? 520 : isSkin ? 2600 : 400);   // skin: a faint pore-sized grain, so it is not plastic-smooth m.SetFloat("_Keep", 0);
                m.SetTexture("_MainTex", hasTex ? AssetDatabase.LoadAssetAtPath<Texture2D>(d + "/" + p.tex) : null);
                m.SetTexture("_BumpMap", string.IsNullOrEmpty(p.normal) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(d + "/" + p.normal));
                m.SetColor("_Tint", hasTex || p.color == null || p.color.Length < 3 ? Color.white : new Color(p.color[0], p.color[1], p.color[2]).gamma);
                m.SetFloat("_Recolor", 0); m.SetFloat("_Inset", 0); m.SetFloat("_Cutoff", p.alpha ? 0.35f : 0); m.SetFloat("_Cull", 0); m.SetFloat("_Glossiness", isHair ? 0.18f : Mathf.Clamp01(1 - p.rough) * 0.6f);
                EditorUtility.SetDirty(m); mats[p.mesh] = m;
            }
            // every piece becomes a direct child of the prefab, named after its mesh, so the game can pick pieces out by name
            var root = new GameObject(man.id); var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(d + "/" + fbx)); int done = 0;
            if (model.GetComponent<Renderer>() != null) { model.name = man.parts[0].mesh; model.transform.SetParent(root.transform, true); }
            else { while (model.transform.childCount > 0) model.transform.GetChild(0).SetParent(root.transform, true); Object.DestroyImmediate(model); }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) {
                if (!mats.TryGetValue(r.name, out var m)) { Debug.LogWarning("SLBuild.Import3D: " + man.id + " has no material for " + r.name); continue; }
                var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = m; r.sharedMaterials = ms; done++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, outDir + "/" + man.id + ".prefab"); Object.DestroyImmediate(root);
            index.Add(e);
            if (done != man.parts.Length) Debug.LogWarning("SLBuild.Import3D: " + man.id + ", only " + done + " of " + man.parts.Length + " pieces");
        }
    }

    [MenuItem("Starlife/Build Mac App")]
    public static void BuildMac() {
        Import3D();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/Mac/Starlife.app", target = BuildTarget.StandaloneOSX });
        Debug.Log("SLBuild Mac: " + report.summary.result + ", " + report.summary.totalErrors + " errors, " + (report.summary.totalSize / 1048576) + " MB");
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // phones: IL2CPP for 64-bit ARM, free rotation (the layout switches between wide and narrow by itself)
    static void Mobile() {
        var android = UnityEditor.Build.NamedBuildTarget.Android; var ios = UnityEditor.Build.NamedBuildTarget.iOS;
        PlayerSettings.SetApplicationIdentifier(android, "com.olumideoyenubi.starlife");
        PlayerSettings.SetApplicationIdentifier(ios, "com.olumideoyenubi.starlife");
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
    }
    static void Build(BuildTarget target, string path) {
        Import3D();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = path, target = target });
        Debug.Log("SLBuild " + target + ": " + report.summary.result + ", " + report.summary.totalErrors + " errors, " + (report.summary.totalSize / 1048576) + " MB -> " + path);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
    // Windows 64-bit, built from the Mac (Mono scripting: Unity cannot cross-compile IL2CPP for Windows on a Mac)
    [MenuItem("Starlife/Build Windows")]
    public static void BuildWindows() {
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Starlife.exe");
    }
    [MenuItem("Starlife/Build Web")]
    public static void BuildWeb() {
        PlayerSettings.WebGL.template = "PROJECT:Starlife";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip; PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace; // the script engine and the renderer rely on caught exceptions
        Build(BuildTarget.WebGL, "Builds/Web");
    }
    [MenuItem("Starlife/Build Android APK")]
    public static void BuildAndroid() { Mobile(); EditorUserBuildSettings.buildAppBundle = false; Build(BuildTarget.Android, "Builds/Android/Starlife.apk"); }
    [MenuItem("Starlife/Build iOS Xcode Project")]
    public static void BuildIOS() { Mobile(); Build(BuildTarget.iOS, "Builds/iOS"); }
}
