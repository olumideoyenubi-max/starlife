using UnityEditor;

// Starlet and hairstyle models stay Read/Write enabled: Starlife Town (SLRig) reads their vertices to skin them.
// The generated men, menswear sets and passers-by (Tools/blender/make_men.py) come with 2048 px textures: their skin
// keeps 2048 (1024 for the sets and passers-by) and everything else is brought down, so the game stays a sensible size.
public class SLModelImport : AssetPostprocessor {
    static readonly string[] Men = { "kai", "leo", "jin", "theo", "omar" };
    void OnPreprocessModel() {
        if (!assetPath.StartsWith("Assets/Starlets/") && !assetPath.StartsWith("Assets/Hair/")) return;
        ((ModelImporter)assetImporter).isReadable = true;
    }
    void OnPreprocessTexture() {
        if (!assetPath.StartsWith("Assets/Starlets/")) return;
        var parts = assetPath.Split('/'); if (parts.Length < 4) return;
        string id = parts[2], file = parts[3];
        bool helper = id == "manbase" || id.StartsWith("m_") || id.StartsWith("p_"), man = System.Array.IndexOf(Men, id) >= 0;
        if (!helper && !man) return;
        bool skin = file.StartsWith("body") || file.StartsWith("head");
        ((TextureImporter)assetImporter).maxTextureSize = man ? (skin ? 2048 : 1024) : (skin ? 1024 : 512);
    }
    public override uint GetVersion() { return 2; }
}
