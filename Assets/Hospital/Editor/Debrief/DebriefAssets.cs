using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProCommVR.Emotions;
using ProCommVR.Hospital.Face;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProCommVR.Hospital.Debrief.Editor
{
    /// <summary>
    /// Creates and updates the assets of the doctor-led debrief from data:
    /// <list type="bullet">
    /// <item>DebriefScript_Scenario1 and DebriefVoiceLibrary_Scenario1 (Assets/Hospital/Data/Debrief) from
    /// DebriefLines_Scenario1.json and the voice manifest DoctorVoice_s1.json, with the clips of
    /// Assets/Hospital/Audio/Debrief/Doctor (Tools/AiServer/tools/gen_doctor_lines.py makes them);</item>
    /// <item>the materials (one shared material for the props and notes, one unlit for pictures), the memory Volume profile;</item>
    /// <item>the head doctor: a prefab variant of Liam's Doctor_Female with DoctorSpeaker and DoctorPointer (the original
    /// character files stay as they are).</item>
    /// </list>
    /// Menu: ProCommVR > Liam > Debrief > Import Doctor Lines.
    /// </summary>
    public static class DebriefAssets
    {
        public const string DataFolder = "Assets/Hospital/Data/Debrief";
        public const string LinesJson = DataFolder + "/DebriefLines_Scenario1.json";
        public const string ScriptPath = DataFolder + "/DebriefScript_Scenario1.asset";
        public const string LibraryPath = DataFolder + "/DebriefVoiceLibrary_Scenario1.asset";
        public const string VolumePath = DataFolder + "/MemoryVolume.asset";
        public const string AudioFolder = "Assets/Hospital/Audio/Debrief";
        public const string DoctorAudioFolder = AudioFolder + "/Doctor";
        public const string ArtFolder = "Assets/Hospital/Art/Debrief";
        public const string MaterialFolder = "Assets/Hospital/Materials/Debrief";
        public const string PropMaterialPath = MaterialFolder + "/DebriefProp.mat";
        public const string PictureMaterialPath = MaterialFolder + "/DebriefPicture.mat";
        public const string PrefabFolder = "Assets/Hospital/Prefabs/Debrief";
        public const string HeadDoctorPath = PrefabFolder + "/HeadDoctor.prefab";
        public const string DoctorModelPath = "Assets/Character/Doctor_Female/Doctor_Female.prefab";
        public const string GhostMaterialPath = "Assets/_Project/Materials/Player/AvatarWhite.mat";

        [Serializable]
        private class LinesFile
        {
            public string scenario_id;
            public string scenario_prefix;
            public string doctor_name;
            public string pager_text;
            public string[] steps;
            public int max_points = 3;
            public float good_fraction = 0.7f;
            public NoteJson[] notes;
            public string critical_note;
            public AimJson better_aim;
            public ConsequenceJson consequence;
        }

        [Serializable] private class NoteJson { public int item; public string improve; public string well_done; }
        [Serializable] private class AimJson { public string expression; public float intensity; public float joy, anger, fear, sadness; }
        [Serializable] private class ConsequenceJson { public string title, good_caption, mixed_caption, critical_caption; }

        [MenuItem("ProCommVR/Liam/Debrief/Import Doctor Lines")]
        public static void ImportMenu()
        {
            var script = EnsureAll();
            Selection.activeObject = script;
            Debug.Log($"[Debrief] Imported {script.Lines.Count} doctor lines and {script.Voices.Entries.Count} feedback lines.");
        }

        /// <summary>Makes sure every asset exists and is up to date. Returns the script.</summary>
        public static DebriefScript EnsureAll()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            ImportAudioSettings();
            ImportImageSettings();
            EnsurePropMaterial();
            EnsurePictureMaterial();
            EnsureVolumeProfile();
            EnsureHeadDoctor();
            var script = ImportLines();
            AssetDatabase.SaveAssets();
            return script;
        }

        // ---- Lines ------------------------------------------------------------------------------------------------------

        public static DebriefScript ImportLines()
        {
            var json = File.ReadAllText(LinesJson);
            var file = JsonUtility.FromJson<LinesFile>(json);
            var lines = ReadStringMap(json, "lines");
            var manifestPath = $"{DataFolder}/DoctorVoice_{file.scenario_prefix}.json";
            var spoken = File.Exists(manifestPath) ? ReadStringMap(File.ReadAllText(manifestPath), "lines") : new Dictionary<string, string>();

            var script = LoadOrCreate<DebriefScript>(ScriptPath);
            var library = LoadOrCreate<DebriefVoiceLibrary>(LibraryPath);
            script.ScenarioId = file.scenario_id;
            script.ScenarioPrefix = file.scenario_prefix;
            script.DoctorName = file.doctor_name;
            script.PagerText = file.pager_text;
            if (file.steps != null && file.steps.Length > 0)
                script.Steps = file.steps.Select(s => (DebriefStepKind)Enum.Parse(typeof(DebriefStepKind), s)).ToList();
            script.MaxPoints = Mathf.Clamp(file.max_points, 1, 3);
            script.GoodFraction = file.good_fraction;
            script.CriticalNote = file.critical_note;
            script.Notes = (file.notes ?? Array.Empty<NoteJson>())
                .Select(n => new DebriefNote { ItemId = n.item, Improve = n.improve, WellDone = n.well_done }).ToList();
            if (file.better_aim != null)
            {
                if (Enum.TryParse(file.better_aim.expression, out FaceExpression expression))
                    script.BetterAimExpression = expression;
                script.BetterAimIntensity = file.better_aim.intensity;
                script.BetterAim = new EmotionState(file.better_aim.joy, file.better_aim.anger, file.better_aim.fear, file.better_aim.sadness);
            }
            if (file.consequence != null)
            {
                script.ConsequenceTitle = file.consequence.title;
                script.GoodCaption = file.consequence.good_caption;
                script.MixedCaption = file.consequence.mixed_caption;
                script.CriticalCaption = file.consequence.critical_caption;
            }
            if (lines.TryGetValue("consequence_good_granddaughter", out var girl))
                script.GranddaughterText = girl;

            // Fixed lines (and the reveal lines) of the script; feedback lines of the scenario in the library.
            var prefix = file.scenario_prefix + "_";
            script.Lines = new List<DebriefLine>();
            library.Entries = new List<DebriefLine>();
            foreach (var pair in spoken)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DoctorAudioFolder}/{pair.Key}.wav");
                if (clip == null)
                    Debug.LogWarning($"[Debrief] No clip for '{pair.Key}' (run Tools/AiServer/tools/gen_doctor_lines.py).");
                var line = new DebriefLine(pair.Key, pair.Value, clip);
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    library.Entries.Add(line);
                else
                    script.Lines.Add(line);
            }
            // Lines in the JSON that were not rendered yet: text only (the doctor shows the subtitle for its reading time).
            foreach (var pair in lines)
                if (pair.Key != "pager" && pair.Key != "consequence_good_granddaughter" && script.Lines.All(l => l.Key != pair.Key))
                    script.Lines.Add(new DebriefLine(pair.Key, pair.Value));
            script.Voices = library;
            script.PagerClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/Pager.wav");
            script.FootstepsClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/Footsteps.wav");
            script.GranddaughterClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/Granddaughter.wav");
            script.GoodImage = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/Consequence_Good.png");
            script.MixedImage = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/Consequence_Mixed.png");
            script.CriticalImage = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/Consequence_Critical.png");
            EditorUtility.SetDirty(script);
            EditorUtility.SetDirty(library);
            return script;
        }

        /// <summary>The string values of one flat JSON object ("key": "text") inside the file (JsonUtility has no maps).</summary>
        internal static Dictionary<string, string> ReadStringMap(string json, string field)
        {
            var result = new Dictionary<string, string>();
            var start = json.IndexOf($"\"{field}\"", StringComparison.Ordinal);
            if (start < 0)
                return result;
            var open = json.IndexOf('{', start);
            var i = open + 1;
            while (i < json.Length)
            {
                SkipSpace(json, ref i);
                if (json[i] == '}')
                    break;
                if (json[i] == ',')
                {
                    i++;
                    continue;
                }
                var key = ReadString(json, ref i);
                SkipSpace(json, ref i);
                i++; // ':'
                SkipSpace(json, ref i);
                var value = ReadString(json, ref i);
                result[key] = value;
            }
            return result;
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
                i++;
        }

        private static string ReadString(string s, ref int i)
        {
            var sb = new System.Text.StringBuilder();
            i++; // opening quote
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    sb.Append(s[i] switch { 'n' => '\n', 't' => '\t', 'u' => (char)Convert.ToInt32(s.Substring(i + 1, 4), 16), _ => s[i] });
                    if (s[i] == 'u')
                        i += 4;
                }
                else
                    sb.Append(s[i]);
                i++;
            }
            i++; // closing quote
            return sb.ToString();
        }

        // ---- Audio and images -------------------------------------------------------------------------------------------

        private static void ImportAudioSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                    continue;
                var settings = importer.defaultSampleSettings;
                var changed = !importer.forceToMono || settings.loadType != AudioClipLoadType.CompressedInMemory
                              || settings.compressionFormat != AudioCompressionFormat.Vorbis;
                if (!changed)
                    continue;
                importer.forceToMono = true;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static void ImportImageSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer || importer.maxTextureSize == 512)
                    continue;
                importer.maxTextureSize = 512;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Compressed;
                var android = importer.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = 512;
                android.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
        }

        // ---- Materials, volume ------------------------------------------------------------------------------------------

        public static Material EnsurePropMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PropMaterialPath);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "DebriefProp" };
            material.SetColor("_BaseColor", Color.white);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.enableInstancing = false;
            AssetDatabase.CreateAsset(material, PropMaterialPath);
            return material;
        }

        public static Material EnsurePictureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PictureMaterialPath);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "DebriefPicture" };
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, PictureMaterialPath);
            return material;
        }

        /// <summary>The look of a memory: grey (saturation -80), a slightly warm tint, a soft vignette. No Bloom, no DoF.</summary>
        public static VolumeProfile EnsureVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile != null)
                return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumePath);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-80f);
            color.colorFilter.Override(new Color(1f, 0.95f, 0.86f));
            color.postExposure.Override(-0.15f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.35f);
            vignette.smoothness.Override(0.5f);
            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ---- The head doctor --------------------------------------------------------------------------------------------

        /// <summary>A prefab variant of Doctor_Female with DoctorSpeaker and DoctorPointer; her head looks up to 5 m.</summary>
        public static GameObject EnsureHeadDoctor()
        {
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(HeadDoctorPath);
            if (variant != null && variant.GetComponent<DoctorSpeaker>() != null && variant.GetComponent<DoctorPointer>() != null)
                return variant;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DoctorModelPath);
            if (model == null)
                throw new FileNotFoundException("[Debrief] Doctor_Female prefab is missing.", DoctorModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "HeadDoctor";
            if (instance.GetComponent<DoctorSpeaker>() == null)
                instance.AddComponent<DoctorSpeaker>();
            if (instance.GetComponent<DoctorPointer>() == null)
                instance.AddComponent<DoctorPointer>();
            var look = instance.GetComponentInChildren<HeadLookAt>();
            if (look != null)
            {
                var serialized = new SerializedObject(look);
                serialized.FindProperty("_range").floatValue = 5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            variant = PrefabUtility.SaveAsPrefabAsset(instance, HeadDoctorPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return variant;
        }

        // ---- Helpers ----------------------------------------------------------------------------------------------------

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
