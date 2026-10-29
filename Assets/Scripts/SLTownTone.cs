using UnityEngine;
/// <summary>On the town camera: tone maps each frame (Resources/SLTownTone.shader). Exposure is set by the hour.</summary>
public sealed class SLTownTone : MonoBehaviour {
    public float exposure = 1; Material mat;
    void OnRenderImage(RenderTexture src, RenderTexture dst) {
        if (mat == null) { var sh = Resources.Load<Shader>("SLTownTone"); if (sh == null) { Graphics.Blit(src, dst); return; } mat = new Material(sh); }
        mat.SetFloat("_Exposure", exposure); Graphics.Blit(src, dst, mat);
    }
}
