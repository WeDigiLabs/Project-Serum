using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// One-time setup for the requested open scene; the resulting profile is freely editable.
public static class SerumMortuaryGrade
{
    const string ProfilePath = "Assets/Settings/Mortuary Blue Cinematic.asset";
    const string Receipt = "Library/MortuaryGradeApplied.txt";
    [MenuItem("Tools/Serum/Apply Mortuary Blue Cinematic Grade")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            var tone = profile.Add<Tonemapping>(false);
            tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(false);
            color.postExposure.Override(-0.35f);
            color.contrast.Override(18f);
            color.saturation.Override(-22f);
            var balance = profile.Add<WhiteBalance>(false);
            balance.temperature.Override(-13f);
            balance.tint.Override(-3f);
            var split = profile.Add<SplitToning>(false);
            split.shadows.Override(new Color(0.43f, 0.50f, 0.54f));
            split.highlights.Override(new Color(0.52f, 0.505f, 0.48f));
            split.balance.Override(-20f);
            var ranges = profile.Add<ShadowsMidtonesHighlights>(false);
            ranges.shadows.Override(new Vector4(0.94f, 1.01f, 1.06f, -0.015f));
            ranges.midtones.Override(new Vector4(0.97f, 1.01f, 1.035f, 0f));
            ranges.highlights.Override(new Vector4(1.025f, 1.01f, 0.98f, 0f));
            ranges.shadowsStart.Override(0f);
            ranges.shadowsEnd.Override(0.32f);
            ranges.highlightsStart.Override(0.58f);
            ranges.highlightsEnd.Override(1f);
            var bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.16f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(0.88f, 0.95f, 1f));
            var vignette = profile.Add<Vignette>(false);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.65f);
            vignette.color.Override(new Color(0.008f, 0.013f, 0.018f));
            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }
        var roots = scene.GetRootGameObjects();
        var volume = roots.SelectMany(r => r.GetComponentsInChildren<Volume>(true))
            .FirstOrDefault(v => v.name == "Global Volume - Mortuary Blue Cinematic");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply Mortuary Blue Cinematic Grade");
        if (volume == null)
        {
            var go = new GameObject("Global Volume - Mortuary Blue Cinematic");
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create cinematic global volume");
            volume = Undo.AddComponent<Volume>(go);
        }
        Undo.RecordObject(volume, "Configure cinematic volume");
        volume.isGlobal = true;
        volume.weight = 1f;
        volume.priority = 100f;
        volume.sharedProfile = profile;
        int cameraCount = 0;
        foreach (var camera in roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
        {
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
            Undo.RecordObject(data, "Enable cinematic camera post processing");
            data.renderPostProcessing = true;
            data.volumeLayerMask |= 1 << volume.gameObject.layer;
            EditorUtility.SetDirty(data);
            cameraCount++;
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
        {
            Directory.CreateDirectory("Library/MortuaryGradeBackup");
            string backup = "Library/MortuaryGradeBackup/" + Path.GetFileName(scene.path);
            if (!File.Exists(backup)) File.Copy(scene.path, backup);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save graded scene");
        }
        Selection.activeGameObject = volume.gameObject;
        SceneView.RepaintAll();
        File.WriteAllText(Receipt, "Scene: " + scene.path + "\nProfile: " + ProfilePath +
            "\nOverrides: " + profile.components.Count + "\nCameras with post processing: " + cameraCount);
        Debug.Log("Mortuary Blue Cinematic grade applied to " + scene.name, volume);
    }
}
