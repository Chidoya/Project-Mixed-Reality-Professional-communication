using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProCommVR.Hospital;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Hospital.AiTalk.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Testscene voor het vrije gesprek met de AI-server: Assets/Scenes/liam_ai_gesprek_test.unity.
    /// Een stuk gang, Visitor_Daughter op 1,2 m van de camera (AiNpc), een brancard met Patient_Male_Old en een klok.
    /// Maakt ook AiTalkSettings.asset en AiTalkInput.inputactions (spatie + rechtertrigger) als ze ontbreken.
    /// Menu: ProCommVR > AI Talk > Build Test Scene.
    /// </summary>
    public static partial class AiTalkSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/liam_ai_gesprek_test.unity";
        public const string SettingsPath = "Assets/Hospital/Data/AiTalkSettings.asset";
        public const string InputPath = "Assets/Hospital/Data/AiTalkInput.inputactions";
        private const string AudioFolder = "Assets/Hospital/Audio/AiTalk";
        private const string MaterialFolder = "Assets/Hospital/Materials";
        private const string LogPrefix = "[AiTalkBuilder] ";

        // Maten
        private const float CorridorWidth = 3f;
        private const float CorridorHeight = 2.8f;
        private const float CorridorStart = -2f;
        private const float CorridorEnd = 7f;
        private const float EyeHeight = 1.62f;
        private const float DaughterDistance = 1.2f;
        private const float GurneyMattressTop = 0.83f;   // blender/scripts/06_props.py (build_gurney)
        private const float LayingClearance = 0.01f;
        private static readonly Vector3 PropFront = Vector3.back; // voorkant van de props (PropSetup)

        [MenuItem("ProCommVR/AI Talk/Build Test Scene")]
        public static void Build()
        {
            // Eerst de scene: NewScene laadt ongebruikte assets uit, daarna pas de assets laden
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var settings = EnsureSettings();
            var talkAction = EnsureInputActions();
            BuildCorridor();
            BuildLights();

            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var camera = cam.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            cam.AddComponent<AudioListener>();
            cam.transform.SetPositionAndRotation(new Vector3(0f, EyeHeight, 0f), Quaternion.Euler(6f, 0f, 0f));

            var ptt = new GameObject("PushToTalk").AddComponent<PushToTalk>();
            Set(ptt, "_settings", settings);
            Set(ptt, "_talkAction", talkAction);

            var daughter = PlaceDaughter(settings, ptt);
            PlaceGurneyWithPatient();
            PlaceClock();

            var panel = new GameObject("AiDebugPanel").AddComponent<AiDebugPanel>();
            Set(panel, "_npc", daughter);
            var debrief = new GameObject("AiDebriefPanel").AddComponent<AiDebriefPanel>();
            Set(debrief, "_npc", daughter);

            // De andere scenario's (1, 3, 4, 5) en de kiezer (toetsen 1-5, linker joystick)
            BuildScenarioAreas(settings, ptt, daughter, cam.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"{LogPrefix}Scene gebouwd: {ScenePath}");
        }

        public static void RunBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + e);
                EditorApplication.Exit(1);
            }
        }

        // --- Assets ---------------------------------------------------------------------------

        public static AiTalkSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AiTalkSettings>(SettingsPath);
            if (settings != null)
                return settings;
            settings = ScriptableObject.CreateInstance<AiTalkSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix}Gemaakt: {SettingsPath}");
            return settings;
        }

        /// <summary>Input actions met één actie PushToTalk: spatie (Editor) en de trigger van de rechtercontroller (Quest).</summary>
        public static InputActionReference EnsureInputActions()
        {
            if (!File.Exists(InputPath))
            {
                var asset = ScriptableObject.CreateInstance<InputActionAsset>();
                asset.name = "AiTalkInput";
                var map = asset.AddActionMap("AiTalk");
                var action = map.AddAction("PushToTalk", InputActionType.Button);
                action.AddBinding(PushToTalk.KeyboardBinding);
                action.AddBinding(PushToTalk.RightTriggerBinding);
                File.WriteAllText(InputPath, asset.ToJson());
                Object.DestroyImmediate(asset);
                AssetDatabase.ImportAsset(InputPath);
                Debug.Log($"{LogPrefix}Gemaakt: {InputPath}");
            }
            var reference = AssetDatabase.LoadAllAssetsAtPath(InputPath).OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.name == "PushToTalk");
            if (reference == null)
                Debug.LogWarning($"{LogPrefix}Geen InputActionReference voor PushToTalk in {InputPath}; PushToTalk maakt dan zelf de actie.");
            return reference;
        }

        // --- Gang -----------------------------------------------------------------------------

        private static void BuildCorridor()
        {
            var root = new GameObject("Corridor").transform;
            var length = CorridorEnd - CorridorStart;
            var midZ = (CorridorStart + CorridorEnd) / 2f;
            var half = CorridorWidth / 2f;
            Box("Floor", root, new Vector3(0f, -0.05f, midZ), new Vector3(CorridorWidth + 0.2f, 0.1f, length), Mat("Floor"));
            Box("Ceiling", root, new Vector3(0f, CorridorHeight + 0.05f, midZ), new Vector3(CorridorWidth + 0.2f, 0.1f, length), Mat("Ceiling"));
            Box("Wall_Left", root, new Vector3(-half - 0.05f, CorridorHeight / 2f, midZ), new Vector3(0.1f, CorridorHeight, length), Mat("Wall"));
            Box("Wall_Right", root, new Vector3(half + 0.05f, CorridorHeight / 2f, midZ), new Vector3(0.1f, CorridorHeight, length), Mat("Wall"));
            Box("Wall_End", root, new Vector3(0f, CorridorHeight / 2f, CorridorEnd + 0.05f), new Vector3(CorridorWidth, CorridorHeight, 0.1f), Mat("Wall"));
            Box("Wall_Back", root, new Vector3(0f, CorridorHeight / 2f, CorridorStart - 0.05f), new Vector3(CorridorWidth, CorridorHeight, 0.1f), Mat("Wall"));
            // Plint in een accentkleur, zodat de gang niet volledig wit is
            var accent = Mat("Accent_Floor3");
            Box("Skirting_Left", root, new Vector3(-half + 0.01f, 0.06f, midZ), new Vector3(0.02f, 0.12f, length), accent);
            Box("Skirting_Right", root, new Vector3(half - 0.01f, 0.06f, midZ), new Vector3(0.02f, 0.12f, length), accent);
        }

        private static void BuildLights()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.56f);
            var sun = new GameObject("Fill Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.6f;
            sun.color = new Color(1f, 0.97f, 0.93f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, 200f, 0f);
            foreach (var z in new[] { 0.6f, 3.2f, 5.8f })
            {
                var lamp = new GameObject($"Ceiling_Light_{z:0.0}").AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 5f;
                lamp.intensity = 1.6f;
                lamp.color = new Color(1f, 0.98f, 0.94f);
                lamp.shadows = LightShadows.None;
                lamp.transform.position = new Vector3(0f, CorridorHeight - 0.15f, z);
            }
        }

        // --- Personages en props ---------------------------------------------------------------

        private static AiNpc PlaceDaughter(AiTalkSettings settings, PushToTalk ptt)
        {
            var prefab = LoadCharacter("Visitor_Daughter");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "Visitor_Daughter";
            go.transform.position = new Vector3(0f, 0f, DaughterDistance);
            // Gezicht naar de camera (-Z). De personages kijken naar +Z (zoals in liam_face_test); FaceDirection
            // geeft enkel de juiste kant (voor/achter), niet de exacte hoek.
            if (Vector3.Dot(FaceTestBuilder.FaceDirection(go), Vector3.back) < 0f)
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var npc = go.AddComponent<AiNpc>();
            Set(npc, "_settings", settings);
            Set(npc, "_pushToTalk", ptt);
            var clips = new[] { "Sarah_Hmm", "Sarah_Mm" }
                .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{n}.wav"))
                .Where(c => c != null).ToArray();
            var so = new SerializedObject(npc);
            var list = so.FindProperty("_listeningClips");
            list.arraySize = clips.Length;
            for (var i = 0; i < clips.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.FindProperty("_fallbackClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/Sarah_Sorry_What.wav");
            so.FindProperty("_timeUpClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/Sarah_TimeUp.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            return npc;
        }

        /// <summary>Brancard langs de rechtermuur (hoofdeinde van de camera weg) met Patient_Male_Old in de Laying-pose.</summary>
        private static void PlaceGurneyWithPatient()
        {
            var gurneyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hospital/Art/Props/Prefabs/Gurney.prefab");
            if (gurneyPrefab == null)
            {
                Debug.LogWarning($"{LogPrefix}Gurney.prefab ontbreekt, overgeslagen.");
                return;
            }
            var gurney = new GameObject("Gurney").transform;
            gurney.SetPositionAndRotation(new Vector3(0.95f, 0f, 3.1f), Quaternion.identity);
            // Blender +X (hoofdeinde) is lokaal (1, 0, 0) van de prop-root: een kwartslag om y legt die as op forward
            var model = (GameObject)PrefabUtility.InstantiatePrefab(gurneyPrefab, gurney);
            model.name = "Model";
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * gurneyPrefab.transform.localRotation;
            AlignToFloor(model, gurney.position);
            var sheet = Box("Sheet", gurney, new Vector3(0f, GurneyMattressTop + 0.006f, -0.2f), new Vector3(0.62f, 0.012f, 1.5f), Mat("Sheet"));
            Object.DestroyImmediate(sheet.GetComponent<Collider>());

            var patientPrefab = LoadCharacter("Patient_Male_Old");
            var patient = (GameObject)PrefabUtility.InstantiatePrefab(patientPrefab);
            patient.name = "Patient_Male_Old";
            patient.AddComponent<CharacterPose>().Configure("Laying", 0f);

            // Meten op een kopie in de Laying-pose (AnimationMode zet een gesampled object terug bij het stoppen)
            var probe = Object.Instantiate(patientPrefab);
            probe.hideFlags = HideFlags.DontSave;
            try
            {
                if (!SamplePose(probe, "Laying"))
                    return;
                var animator = probe.GetComponent<Animator>();
                probe.transform.SetPositionAndRotation(gurney.position, gurney.rotation);
                Vector3 Head() => animator.GetBoneTransform(HumanBodyBones.Head).position;
                Vector3 Feet() => (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position +
                                   animator.GetBoneTransform(HumanBodyBones.RightFoot).position) / 2f;
                var axis = Vector3.ProjectOnPlane(Head() - Feet(), Vector3.up);
                probe.transform.Rotate(0f, Vector3.SignedAngle(axis, gurney.forward, Vector3.up), 0f, Space.World);
                var middle = (Head() + Feet()) / 2f;
                probe.transform.position += new Vector3(gurney.position.x - middle.x, 0f, gurney.position.z - middle.z);
                probe.transform.position += Vector3.up * (GurneyMattressTop + LayingClearance - MinVertexY(probe));
                patient.transform.SetPositionAndRotation(probe.transform.position, probe.transform.rotation);
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(probe);
            }
        }

        private static void PlaceClock()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hospital/Art/Props/Prefabs/Wall_Clock.prefab");
            if (prefab == null)
                return;
            var clock = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            clock.name = "Wall_Clock";
            var front = Vector3.right; // hangt aan de linkermuur, kijkt de gang in
            var yaw = Vector3.SignedAngle(PropFront, front, Vector3.up);
            clock.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * clock.transform.rotation;
            clock.transform.position = new Vector3(-CorridorWidth / 2f, 2.1f, 2.6f);
            if (clock.TryGetComponent<WallClock>(out var wallClock))
                wallClock.SetTime(DateTime.Now);
        }

        // --- Hulp -----------------------------------------------------------------------------

        private static GameObject LoadCharacter(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Character/{name}/{name}.prefab");
            if (prefab == null)
                throw new InvalidOperationException($"Personage ontbreekt: {name}");
            return prefab;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
                throw new InvalidOperationException($"{target.GetType().Name}.{field} bestaat niet");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Mat(string name)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
            if (mat == null)
                Debug.LogWarning($"{LogPrefix}Materiaal ontbreekt: {name}");
            return mat;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            if (mat != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static void AlignToFloor(GameObject go, Vector3 target)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                b.Encapsulate(r.bounds);
            go.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
        }

        private static bool SamplePose(GameObject character, string state)
        {
            var animator = character.GetComponent<Animator>();
            var clip = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == state)
                : null;
            if (clip == null)
            {
                Debug.LogWarning($"{LogPrefix}{character.name}: geen animatie '{state}'.");
                return false;
            }
            var position = character.transform.position;
            var rotation = character.transform.rotation;
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(character, clip, 0f);
            AnimationMode.EndSampling();
            character.transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        private static float MinVertexY(GameObject character)
        {
            var min = float.MaxValue;
            var mesh = new Mesh();
            foreach (var skin in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(mesh, true);
                var matrix = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices)
                    min = Mathf.Min(min, matrix.MultiplyPoint3x4(v).y);
            }
            Object.DestroyImmediate(mesh);
            return min;
        }
    }

    /// <summary>
    /// Automatische test van de AI-gesprekscene, in de Editor (met venster, zodat er geluid is):
    /// Unity -projectPath . -executeMethod ProCommVR.Editor.AiTalkCapture.Run
    ///   -aiWavs "a.wav;b.wav" -aiShot docs/Screenshots/x.png [-aiShotTurn 2] -aiLog Temp/ai.log
    ///   [-aiDebriefShot docs/Screenshots/y.png] (gesprek beëindigen en de nabespreking vastleggen)
    ///   [-aiScenario 1..5] (eerst dat scenario kiezen met de scenariokiezer)
    /// De server (Tools/AiServer) moet draaien.
    /// </summary>
    [InitializeOnLoad]
    public static class AiTalkCapture
    {
        private const string Key = "AiTalkCapture.";

        static AiTalkCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs().ToList();
            string Arg(string name)
            {
                var i = args.IndexOf(name);
                return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
            }
            AiTalkSceneBuilder.Build();
            EditorSceneManager.OpenScene(AiTalkSceneBuilder.ScenePath);
            SessionState.SetBool(Key + "Active", true);
            SessionState.SetString(Key + "Wavs", Arg("-aiWavs") ?? "");
            SessionState.SetString(Key + "Shot", Arg("-aiShot") ?? "");
            SessionState.SetString(Key + "Log", Arg("-aiLog") ?? Path.GetFullPath("Temp/AiTalkTest.log"));
            SessionState.SetInt(Key + "ShotTurn", int.TryParse(Arg("-aiShotTurn"), out var n) ? n : 0);
            SessionState.SetString(Key + "DebriefShot", Arg("-aiDebriefShot") ?? "");
            SessionState.SetInt(Key + "Scenario", int.TryParse(Arg("-aiScenario"), out var sc) ? sc : 0);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key + "Active", false))
                return;
            SessionState.SetBool(Key + "Active", false);
            var runner = new GameObject("AiTalkTestRunner").AddComponent<AiTalkTestRunner>();
            runner.WavFiles = new List<string>(SessionState.GetString(Key + "Wavs", "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries));
            runner.ScreenshotPath = SessionState.GetString(Key + "Shot", "");
            runner.ScreenshotTurn = SessionState.GetInt(Key + "ShotTurn", 0);
            runner.LogPath = SessionState.GetString(Key + "Log", "");
            runner.DebriefShotPath = SessionState.GetString(Key + "DebriefShot", "");
            runner.ScenarioNumber = SessionState.GetInt(Key + "Scenario", 0);
            runner.Finished = ok =>
            {
                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            };
        }
    }
}
