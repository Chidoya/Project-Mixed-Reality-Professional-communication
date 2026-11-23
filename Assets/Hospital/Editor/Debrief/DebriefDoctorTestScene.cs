using ProCommVR.Hospital.Debrief.Testing;
using ProCommVR.Scenarios;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProCommVR.Hospital.Debrief.Editor
{
    /// <summary>
    /// A small test scene for the head doctor: the doctor in front of a whiteboard, buttons to play each line
    /// (Assets/Scenes/liam_debrief_doctor_test.unity, not in the Build Settings).
    /// </summary>
    public static class DebriefDoctorTestScene
    {
        public const string ScenePath = "Assets/Scenes/liam_debrief_doctor_test.unity";
        private const string RigPath = "Assets/_Project/Prefabs/XR Origin (ProCommVR).prefab";
        private const string BubblePath = "Assets/_Project/Prefabs/UI/DialogueBubble.prefab";

        [MenuItem("ProCommVR/Liam/Debrief/Build Doctor Test Scene")]
        public static void Build()
        {
            DebriefAssets.EnsureAll();
            // A new scene unloads the assets loaded before it: load them after.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var script = AssetDatabase.LoadAssetAtPath<DebriefScript>(DebriefAssets.ScriptPath);
            var material = DebriefAssets.EnsurePropMaterial();

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(1f, 1f, 1f);
            floor.GetComponent<Renderer>().sharedMaterial = material;

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Whiteboard";
            board.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, 3.2f), Quaternion.identity);
            board.transform.localScale = new Vector3(2.4f, 1.2f, 0.03f);
            board.GetComponent<Renderer>().sharedMaterial = material;
            var boardFront = new GameObject("Whiteboard Front").transform;
            boardFront.SetPositionAndRotation(new Vector3(0f, 1.5f, 3.18f), Quaternion.LookRotation(Vector3.back));

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));

            var doctor = (GameObject)PrefabUtility.InstantiatePrefab(DebriefAssets.EnsureHeadDoctor());
            doctor.transform.SetPositionAndRotation(new Vector3(-0.8f, 0f, 1.9f), Quaternion.LookRotation(new Vector3(0.8f, 0f, -1.9f)));
            var bubblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePath);
            var subtitle = (GameObject)PrefabUtility.InstantiatePrefab(bubblePrefab);
            subtitle.name = "Doctor Subtitle Bubble";
            subtitle.SetActive(false);
            var speaker = doctor.GetComponent<DoctorSpeaker>();
            Set(speaker, "_subtitle", subtitle.GetComponent<DialogueBubble>());

            var panel = new GameObject("Doctor Test Panel").AddComponent<DebriefDoctorTestPanel>();
            var status = new GameObject("Status").AddComponent<TextMeshPro>();
            status.transform.SetPositionAndRotation(new Vector3(0.55f, 2.35f, 3.1f), Quaternion.LookRotation(new Vector3(0.18f, 0f, 1f)));
            status.rectTransform.sizeDelta = new Vector2(1.6f, 0.5f);
            status.alignment = TextAlignmentOptions.Center;
            status.fontSize = 0.45f;
            status.color = new Color(0.1f, 0.1f, 0.14f);
            status.textWrappingMode = TextWrappingModes.Normal;
            DialogueBubble Button(string label, float x)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(bubblePrefab);
                go.name = $"Button {label}";
                go.transform.SetPositionAndRotation(new Vector3(x, 1.15f, 1.3f), Quaternion.LookRotation(new Vector3(x * 0.3f, -0.2f, 1f)));
                go.transform.localScale *= 0.45f;
                var bubble = go.GetComponent<DialogueBubble>();
                bubble.GetComponentInChildren<TMP_Text>().text = label;
                return bubble;
            }
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("_script").objectReferenceValue = script;
            serialized.FindProperty("_doctor").objectReferenceValue = speaker;
            serialized.FindProperty("_pointer").objectReferenceValue = doctor.GetComponent<DoctorPointer>();
            serialized.FindProperty("_board").objectReferenceValue = boardFront;
            serialized.FindProperty("_status").objectReferenceValue = status;
            serialized.FindProperty("_previous").objectReferenceValue = Button("Previous (P)", 0.05f);
            serialized.FindProperty("_play").objectReferenceValue = Button("Play (Enter)", 0.35f);
            serialized.FindProperty("_next").objectReferenceValue = Button("Next (N)", 0.65f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DebriefAssets.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Debrief] Doctor test scene saved: {ScenePath}");
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
