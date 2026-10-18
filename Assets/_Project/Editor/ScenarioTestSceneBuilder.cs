using System.IO;
using System.Linq;
using ProCommVR.Scenarios;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Builds the scenario system test setup: TMP essentials, the dialogue bubble prefab, a placeholder
    /// scenario and the ScenarioTest scene. Run each step in its own batchmode run:
    ///   1. -executeMethod ProCommVR.Editor.ScenarioTestSceneBuilder.ImportTmpEssentialsAndExit (without -quit)
    ///   2. -executeMethod ProCommVR.Editor.ScenarioTestSceneBuilder.BuildBubblePrefab
    ///   3. -executeMethod ProCommVR.Editor.ScenarioTestSceneBuilder.BuildScenarioTestScene
    /// Requires the XR rig variant from XRTestSceneBuilder.
    /// </summary>
    public static class ScenarioTestSceneBuilder
    {
        private const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private const string BubblePrefabPath = "Assets/_Project/Prefabs/UI/DialogueBubble.prefab";
        private const string PlaceholderScenarioPath = "Assets/_Project/Dialogue/Scenarios/Placeholder_Scenario.asset";
        private const string RigVariantPath = "Assets/_Project/Prefabs/XR Origin (ProCommVR).prefab";
        private const string FloorMaterialPath = "Assets/_Project/Materials/Floor.mat";
        private const string NpcMaterialPath = "Assets/_Project/Materials/PlaceholderNpc.mat";
        private const string ScenePath = "Assets/_Project/Scenes/ScenarioTest.unity";

        private static readonly Vector2 BubbleSize = new Vector2(600f, 220f);
        private const float BubblePixelSize = 0.001f; // 1 px = 1 mm, so a bubble is 60 x 22 cm

        [MenuItem("ProCommVR/Setup/3. Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath))
            {
                Debug.Log("[ProCommVR] TMP Essential Resources already imported.");
                return;
            }
            AssetDatabase.ImportPackage(TmpEssentialsPackage, false);
            Debug.Log("[ProCommVR] TMP Essential Resources import started.");
        }

        /// <summary>
        /// Batchmode variant: package import is asynchronous, so run this without -quit;
        /// the editor exits itself once the import has finished.
        /// </summary>
        public static void ImportTmpEssentialsAndExit()
        {
            if (File.Exists(TmpSettingsPath))
            {
                Debug.Log("[ProCommVR] TMP Essential Resources already imported.");
                EditorApplication.Exit(0);
                return;
            }
            AssetDatabase.importPackageCompleted += _ =>
            {
                Debug.Log("[ProCommVR] TMP Essential Resources imported.");
                EditorApplication.Exit(0);
            };
            AssetDatabase.importPackageFailed += (_, error) =>
            {
                Debug.LogError($"[ProCommVR] TMP Essential Resources import failed: {error}");
                EditorApplication.Exit(1);
            };
            ImportTmpEssentials();
        }

        [MenuItem("ProCommVR/Setup/4. Build Dialogue Bubble Prefab")]
        public static void BuildBubblePrefab()
        {
            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(BubblePrefabPath));

            var root = new GameObject("DialogueBubble", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = BubbleSize;
                rect.localScale = Vector3.one * BubblePixelSize;

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                root.AddComponent<CanvasScaler>();
                root.AddComponent<TrackedDeviceGraphicRaycaster>();

                var background = root.AddComponent<Image>();
                background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                background.type = Image.Type.Sliced;
                background.color = Color.white;

                var button = root.AddComponent<Button>();
                button.targetGraphic = background;
                var colors = button.colors;
                colors.highlightedColor = new Color(0.8f, 0.9f, 1f);
                colors.pressedColor = new Color(0.6f, 0.78f, 1f);
                colors.selectedColor = Color.white;
                button.colors = colors;

                var labelObject = new GameObject("Label", typeof(RectTransform));
                labelObject.transform.SetParent(rect, false);
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(24f, 16f);
                labelRect.offsetMax = new Vector2(-24f, -16f);

                var label = labelObject.AddComponent<TextMeshProUGUI>();
                label.text = "Bubble text";
                label.color = new Color(0.12f, 0.12f, 0.14f);
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 36f;
                label.raycastTarget = false;

                var bubble = root.AddComponent<DialogueBubble>();
                var serialized = new SerializedObject(bubble);
                serialized.FindProperty("_label").objectReferenceValue = label;
                serialized.FindProperty("_button").objectReferenceValue = button;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BubblePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            Debug.Log($"[ProCommVR] Dialogue bubble prefab created at {BubblePrefabPath}.");
        }

        [MenuItem("ProCommVR/Setup/5. Build Scenario Test Scene")]
        public static void BuildScenarioTestScene()
        {
            var rigVariant = LoadRequired<GameObject>(RigVariantPath, "Run 'Build XR Test Scene' first.");
            var bubblePrefab = LoadRequired<DialogueBubble>(BubblePrefabPath, "Run 'Build Dialogue Bubble Prefab' first.");
            var scenario = GetOrCreatePlaceholderScenario();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);

            PrefabUtility.InstantiatePrefab(rigVariant, scene);
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));

            // Placeholder NPC: a capsule about 2.2 m in front of the player start.
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "Placeholder NPC";
            npc.transform.position = new Vector3(0f, 0.9f, 2.2f);
            npc.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            npc.GetComponent<Renderer>().sharedMaterial = XRTestSceneBuilder.GetOrCreateMaterial(NpcMaterialPath, new Color(0.45f, 0.6f, 0.8f));

            var presenterObject = new GameObject("Scenario Presenter");
            var npcAnchor = new GameObject("NPC Bubble Anchor").transform;
            npcAnchor.SetParent(presenterObject.transform, false);
            npcAnchor.position = new Vector3(0f, 2.15f, 2.2f);
            npcAnchor.localScale = Vector3.one * 1.3f;
            var optionsAnchor = new GameObject("Options Anchor").transform;
            optionsAnchor.SetParent(presenterObject.transform, false);
            optionsAnchor.position = new Vector3(0f, 1.15f, 1.1f);

            var presenter = presenterObject.AddComponent<ScenarioPresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("_scenario").objectReferenceValue = scenario;
            serialized.FindProperty("_bubblePrefab").objectReferenceValue = bubblePrefab;
            serialized.FindProperty("_npcBubbleAnchor").objectReferenceValue = npcAnchor;
            serialized.FindProperty("_optionsAnchor").objectReferenceValue = optionsAnchor;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            var otherScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(otherScenes).ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log($"[ProCommVR] Scenario test scene created at {ScenePath}.");
        }

        /// <summary>
        /// Invented placeholder content to test the system. Real scenarios come from the content team.
        /// </summary>
        private static Scenario GetOrCreatePlaceholderScenario()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<Scenario>(PlaceholderScenarioPath);
            if (scenario != null)
                return scenario;

            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(PlaceholderScenarioPath));
            scenario = ScriptableObject.CreateInstance<Scenario>();
            AssetDatabase.CreateAsset(scenario, PlaceholderScenarioPath);

            var serialized = new SerializedObject(scenario);
            serialized.FindProperty("_id").stringValue = "placeholder-waiting-patient";
            serialized.FindProperty("_title").stringValue = "[PLACEHOLDER] The waiting patient";
            serialized.FindProperty("_mood").enumValueIndex = (int)Mood.Neutral;
            serialized.FindProperty("_npcName").stringValue = "Patient (placeholder)";
            serialized.FindProperty("_npcLine").stringValue =
                "I've been waiting all morning and nobody tells me anything. When can I finally go home?";

            var options = serialized.FindProperty("_options");
            options.arraySize = Scenario.OptionCount;
            SetOption(options, 0, 3,
                "I understand the waiting is frustrating. I'll check with the doctor and come back to you within fifteen minutes.",
                "Good choice: you acknowledge the feeling and make a concrete commitment you can keep.");
            SetOption(options, 1, 1,
                "The doctor is very busy today, so you'll have to be patient.",
                "Partly right: it is honest, but it ignores how the patient feels and offers no next step.");
            SetOption(options, 2, 1,
                "I'm not sure. I'll ask someone later.",
                "Partly right: you don't promise anything false, but 'later' is vague and leaves the patient uncertain.");
            SetOption(options, 3, -2,
                "Don't worry, you'll definitely go home today.",
                "Critical error: you give a certainty you cannot guarantee. If it turns out differently, trust is damaged.");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(scenario);
            return scenario;
        }

        private static void SetOption(SerializedProperty options, int index, int score, string text, string feedback)
        {
            var option = options.GetArrayElementAtIndex(index);
            option.FindPropertyRelative("_text").stringValue = text;
            option.FindPropertyRelative("_score").intValue = score;
            option.FindPropertyRelative("_feedback").stringValue = feedback;
        }

        private static T LoadRequired<T>(string path, string hint) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException($"[ProCommVR] {path} not found. {hint}");
            return asset;
        }
    }
}
