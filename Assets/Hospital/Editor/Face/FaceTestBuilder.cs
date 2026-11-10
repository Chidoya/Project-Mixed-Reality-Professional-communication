using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProCommVR.Hospital.Face;
using ProCommVR.Hospital.Face.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Testscene Assets/Scenes/liam_face_test.unity: alle 18 personages op een rij met het gezicht naar
    /// de camera (+Z), een licht en FaceTestDriver (toetsen 1-8, B, L, A, I).
    /// Plus FaceCapture: neemt in batchmode screenshots en videoframes op.
    /// </summary>
    public static class FaceTestBuilder
    {
        public const string ScenePath = "Assets/Scenes/liam_face_test.unity";
        private const string LogPrefix = "[FaceTest] ";
        private const float Spacing = 0.8f;

        private static readonly string[] Order =
        {
            "Patient_Female_Old", "Patient_Girl", "Patient_Female", "Patient_Male", "Patient_Boy", "Patient_Male_Old",
            "Surgeon_Female", "Nurse_Female_Scrubs", "Nurse_Female_White", "Doctor_Female", "Doctor_Male",
            "Nurse_Male_White", "Nurse_Male_Scrubs", "Surgeon_Male", "Porter",
            "Visitor_Daughter", "Visitor_Son", "Visitor_Wife",
        };

        [MenuItem("ProCommVR/Characters/Build Face Test Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.97f, 0.93f);
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(28f, 160f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.6f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2.2f, 1f, 1f);

            var placed = 0;
            for (var i = 0; i < Order.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Character/{Order[i]}/{Order[i]}.prefab");
                if (prefab == null)
                {
                    Debug.LogWarning($"{LogPrefix}Prefab ontbreekt: {Order[i]}");
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3((i - (Order.Length - 1) / 2f) * Spacing, 0f, 0f);
                var facing = FaceDirection(go);
                if (Vector3.Dot(facing, Vector3.forward) < 0f)
                    go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                placed++;
            }

            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.80f, 0.83f, 0.86f);
            cam.fieldOfView = 40f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, 3.2f), Quaternion.Euler(6f, 180f, 0f));

            new GameObject("FaceTestDriver").AddComponent<FaceTestDriver>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"{LogPrefix}Scene gebouwd: {ScenePath} ({placed} personages).");
        }

        /// <summary>Richting waarin het gezicht kijkt (wereld), uit de vertices van het Face-materiaal.</summary>
        public static Vector3 FaceDirection(GameObject character)
        {
            foreach (var smr in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = smr.sharedMesh;
                if (mesh == null)
                    continue;
                var mats = smr.sharedMaterials;
                for (var s = 0; s < mats.Length && s < mesh.subMeshCount; s++)
                {
                    if (mats[s] == null || mats[s].name != "Face")
                        continue;
                    var idx = mesh.GetTriangles(s);
                    var verts = mesh.vertices;
                    var avg = Vector3.zero;
                    foreach (var k in idx)
                        avg += verts[k];
                    avg /= Mathf.Max(1, idx.Length);
                    var world = smr.transform.TransformPoint(avg);
                    var axis = smr.transform.TransformPoint(new Vector3(0f, avg.y, 0f));
                    var dir = world - axis;
                    dir.y = 0f;
                    return dir.normalized;
                }
            }
            return character.transform.forward;
        }

        public static void RunBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError(LogPrefix + e);
                EditorApplication.Exit(1);
            }
        }
    }

    /// <summary>
    /// Command line: Unity -batchmode -executeMethod ProCommVR.Editor.FaceCapture.RunBatch -faceOut map
    /// [-screenshotOut docs/Screenshots]. Opent liam_face_test, gaat in Play mode en laat
    /// FaceDemoRecorder beelden en videoframes wegschrijven.
    /// </summary>
    [InitializeOnLoad]
    public static class FaceCapture
    {
        private const string ActiveKey = "FaceCapture.Active";
        private const string OutKey = "FaceCapture.Out";
        private const string ShotKey = "FaceCapture.Shots";

        static FaceCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void RunBatch()
        {
            var args = System.Environment.GetCommandLineArgs().ToList();
            string Arg(string name)
            {
                var i = args.IndexOf(name);
                return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
            }

            var output = Arg("-faceOut") ?? Path.GetFullPath("Temp/FaceCapture");
            var shots = Arg("-screenshotOut") ?? Path.GetFullPath("docs/Screenshots");
            if (!File.Exists(FaceTestBuilder.ScenePath))
                FaceTestBuilder.Build();
            EditorSceneManager.OpenScene(FaceTestBuilder.ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(OutKey, output);
            SessionState.SetString(ShotKey, shots);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false))
                return;
            SessionState.SetBool(ActiveKey, false);
            var recorder = new GameObject("FaceDemoRecorder").AddComponent<FaceDemoRecorder>();
            recorder.OutputFolder = SessionState.GetString(OutKey, "");
            recorder.ScreenshotFolder = SessionState.GetString(ShotKey, "");
            recorder.Finished = ok =>
            {
                EditorApplication.ExitPlaymode();
                EditorApplication.Exit(ok ? 0 : 1);
            };
            Debug.Log($"[FaceCapture] Opnemen naar {recorder.OutputFolder}");
        }
    }
}
