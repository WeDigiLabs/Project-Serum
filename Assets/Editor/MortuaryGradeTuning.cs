using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Local, explicit requests only. No scene changes happen on project load.
[InitializeOnLoad]
public static class MortuaryGradeTuning
{
    const string Request = "Library/MortuaryTune.request";
    static double nextCheck;
    static MortuaryGradeTuning() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != "Assets/Scene/Level_02/Level 2.unity") return;
        var command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "inspect") Inspect("Library/MortuaryTuneBefore.txt");
            if (command == "apply") Apply();
        }
        catch (System.Exception e) { File.WriteAllText("Library/MortuaryTuneError.txt", e.ToString()); Debug.LogException(e); }
    }
    static T Get<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (p.TryGet<T>(out var c)) { c.active = true; return c; }
        c = p.Add<T>(false);
        AssetDatabase.AddObjectToAsset(c, p);
        return c;
    }
    static void Inspect(string path)
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var sb = new StringBuilder();
        foreach (var v in roots.SelectMany(r => r.GetComponentsInChildren<Volume>(true)))
        {
            sb.AppendLine($"VOLUME {v.name} priority={v.priority} enabled={v.enabled} weight={v.weight}");
            var p = v.HasInstantiatedProfile() ? v.profile : v.sharedProfile;
            if (p != null) foreach (var c in p.components) sb.AppendLine(EditorJsonUtility.ToJson(c));
        }
        foreach (var l in roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)))
            sb.AppendLine($"LIGHT {l.name} active={l.gameObject.activeInHierarchy} enabled={l.enabled} type={l.type} color={l.color} intensity={l.intensity} range={l.range}");
        foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<Component>(true)))
            if (c != null && (c.GetType().Name.Contains("Fog") || c.GetType().Name.Contains("LensFlare")))
                sb.AppendLine(c.GetType().Name + " " + EditorJsonUtility.ToJson(c));
        sb.AppendLine($"Ambient {RenderSettings.ambientMode} {RenderSettings.ambientSkyColor} intensity={RenderSettings.ambientIntensity}");
        File.WriteAllText(path, sb.ToString());
    }
    static void Apply()
    {
        var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/Mortuary Blue Cinematic.asset");
        if (p == null) return;
        Inspect("Library/MortuaryTuneBefore.txt");
        Directory.CreateDirectory("Library/MortuaryGradeBackup");
        File.WriteAllText("Library/MortuaryGradeBackup/GradeBeforeRetune.json", string.Join("\n", p.components.Select(c => EditorJsonUtility.ToJson(c))));
        Undo.RecordObjects(p.components.Cast<Object>().ToArray(), "Refine mortuary grade");
        Get<Tonemapping>(p).mode.Override(TonemappingMode.ACES);
        var c = Get<ColorAdjustments>(p);
        c.postExposure.Override(-0.25f); c.contrast.Override(22f); c.saturation.Override(-42f);
        c.colorFilter.Override(Color.white); c.hueShift.Override(0f);
        var w = Get<WhiteBalance>(p); w.temperature.Override(2f); w.tint.Override(-4f);
        var s = Get<SplitToning>(p);
        s.shadows.Override(new Color(0.48f, 0.505f, 0.515f));
        s.highlights.Override(new Color(0.515f, 0.505f, 0.49f)); s.balance.Override(-10f);
        var h = Get<ShadowsMidtonesHighlights>(p);
        h.shadows.Override(new Vector4(0.99f, 1.005f, 1.015f, -0.01f));
        h.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
        h.highlights.Override(new Vector4(1.015f, 1.005f, 0.99f, 0f));
        var mix = Get<ChannelMixer>(p);
        mix.redOutRedIn.Override(85f); mix.redOutGreenIn.Override(5f); mix.redOutBlueIn.Override(10f);
        mix.greenOutRedIn.Override(0f); mix.greenOutGreenIn.Override(85f); mix.greenOutBlueIn.Override(15f);
        mix.blueOutRedIn.Override(0f); mix.blueOutGreenIn.Override(25f); mix.blueOutBlueIn.Override(75f);
        var b = Get<Bloom>(p);
        b.threshold.Override(1.3f); b.intensity.Override(0.06f); b.scatter.Override(0.15f);
        b.clamp.Override(4f); b.tint.Override(Color.white); b.dirtIntensity.Override(0f);
        Get<ScreenSpaceLensFlare>(p).intensity.Override(0f);
        Get<ChromaticAberration>(p).intensity.Override(0f);
        Get<Vignette>(p).intensity.Override(0.18f);
        foreach (var component in p.components) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(p); AssetDatabase.SaveAssetIfDirty(p);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Materials/Danger Light.mat");
        if (material != null && material.HasProperty("_EmissionColor"))
        {
            File.WriteAllText("Library/MortuaryGradeBackup/EmissionBeforeRetune.json", EditorJsonUtility.ToJson(material));
            Undo.RecordObject(material, "Control warning light emission");
            material.SetColor("_EmissionColor", new Color(3f, 0.045f, 0.012f, 1f));
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }
        var scene = SceneManager.GetActiveScene();
        var volume = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Volume>(true))
            .First(v => v.sharedProfile == p);
        if (volume.HasInstantiatedProfile()) volume.profile = null;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = volume.gameObject;
        SceneView.RepaintAll();
        Inspect("Library/MortuaryTuneAfter.txt");
        File.WriteAllText("Library/MortuaryTuneDone.txt", "Grade and emission refined and saved.");
    }
}
