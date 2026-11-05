// Starlife's settings: music and sound volume, graphics quality, full screen and quit. Opened from the top bar, with
// Esc, or from the town phone's clock. In town the game waits while it is open. Part of SLApp.
using UnityEngine;
using UnityEngine.UIElements;

public partial class SLApp {
    VisualElement settings; int baseLights = -1;
    bool SettingsOpen => settings != null && settings.ClassListContains("on");
    static bool OnDesktop => !Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer;

    void BuildSettings() {
        settings = El("overlay"); overlayHost.Add(settings);
    }
    void OpenSettings() { RenderSettingsPanel(); settings.AddToClassList("on"); }
    void CloseSettings() { settings.RemoveFromClassList("on"); PlayerPrefs.Save(); }

    void RenderSettingsPanel() {
        settings.Clear(); var a = SLAudio.I; var m = El("modal"); m.Add(L("Settings", "h2"));
        VisualElement Bar(string label, float value, System.Action<float> set, System.Action after = null) {
            var sl = new Slider(0, 1) { value = value }; sl.AddToClassList("slider"); sl.style.flexGrow = 1;
            sl.RegisterValueChangedCallback(e => set(e.newValue));
            if (after != null) sl.RegisterCallback<PointerUpEvent>(_ => after(), TrickleDown.TrickleDown);
            var row = El("row", sl); row.style.flexWrap = Wrap.NoWrap; return Field(label, row);
        }
        m.Add(Bar("Music", a.MusicVolume, v => a.MusicVolume = v));
        m.Add(Bar("Sounds", a.SoundVolume, v => a.SoundVolume = v, () => a.Play("coin")));
        var q = El("row"); int cur = Quality;
        foreach (var (v, label) in new[] { (2, "High"), (1, "Medium"), (0, "Low") }) { int vv = v; q.Add(B(label, "chip" + (cur == v ? " on" : ""), () => { Quality = vv; RenderSettingsPanel(); })); }
        m.Add(Field("Graphics", q)); var note = L("Lower it if the town runs slowly: fewer lights and softer or no shadows.", "muted"); note.style.whiteSpace = WhiteSpace.Normal; m.Add(note);
        var acts = El("row"); acts.style.marginTop = 14;
        if (OnDesktop) {
            acts.Add(B(Screen.fullScreen ? "Use a window" : "Full screen", "btn", () => { Screen.fullScreen = !Screen.fullScreen; settings.schedule.Execute(RenderSettingsPanel).StartingIn(100); }));
            var quit = B("Quit the game", "btn", () => { if (inTown) SaveLife(); core.Flush(); PlayerPrefs.Save(); Application.Quit(); }); quit.style.marginLeft = 8; acts.Add(quit);
        }
        acts.Add(El("grow")); acts.Add(B("Done", "btn primary", CloseSettings)); m.Add(acts);
        settings.Add(m);
    }

    /// <summary>0 low, 1 medium, 2 high, remembered between runs.</summary>
    int Quality { get => PlayerPrefs.GetInt("sl.quality", 2); set { PlayerPrefs.SetInt("sl.quality", value); ApplyQuality(); } }
    int RoomLights => Quality == 0 ? 2 : Quality == 1 ? 4 : 8;
    void ApplyQuality() {
        if (baseLights < 0) baseLights = QualitySettings.pixelLightCount;
        int q = Quality; float dist = inTown || townBusyLoading ? 80 : 24;
        QualitySettings.shadows = q == 0 ? ShadowQuality.Disable : ShadowQuality.All;
        QualitySettings.shadowResolution = q == 2 ? ShadowResolution.VeryHigh : ShadowResolution.Medium;
        QualitySettings.shadowDistance = q == 2 ? dist : dist * 0.6f;
        QualitySettings.pixelLightCount = room != null ? RoomLights : q == 2 ? baseLights : Mathf.Min(baseLights, q == 1 ? 2 : 1);
    }

    /// <summary>Called each frame: Esc opens and closes settings. True while the game should wait.</summary>
    bool TickSettings() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            if (SettingsOpen) { CloseSettings(); return true; }
            bool free = inTown ? shop == null && modal == null && !PhoneOpen : !picker.ClassListContains("on") && !saveOverlay.ClassListContains("on");
            if (free) { OpenSettings(); return true; }
        }
        return SettingsOpen;
    }

    // ================================================================ sound that follows what happens
    int heardCoins = -1, heardLevel; string heardDoll; float stepClock;
    /// <summary>Reload calls this: coins going up or down and a new level are heard wherever they came from.</summary>
    void HearProfile() {
        var a = SLAudio.I; if (a == null) return;
        if (heardCoins >= 0) {
            if (profile.level > heardLevel) a.Play("level"); else if (profile.coins > heardCoins) a.Play("coin"); else if (profile.coins < heardCoins) a.Play("buy");
            if (!inTown && heardDoll != null && heardDoll != dollKey) a.Play("swish", 0.8f);
        }
        heardCoins = profile.coins; heardLevel = profile.level; heardDoll = dollKey;
    }
    /// <summary>Footsteps while she walks, faster when she runs, and the car's engine while driving.</summary>
    void TickTownSound() {
        var a = SLAudio.I; if (a == null) return;
        a.Scene(room != null ? "room" : "town");
        if (driving != null) { a.Engine(Mathf.Clamp01(Mathf.Abs(driving.v) / 22f)); return; }
        a.Engine(-1);
        if (player == null || busy != null || player.seat != null || player.speed < 0.3f) { stepClock = 0.1f; return; }
        stepClock -= Time.deltaTime;
        if (stepClock <= 0) { stepClock = Mathf.Clamp(0.95f / Mathf.Max(player.speed, 0.5f), 0.22f, 0.6f); a.Play("step", 0.55f, Random.Range(0.88f, 1.12f)); }
    }
}
