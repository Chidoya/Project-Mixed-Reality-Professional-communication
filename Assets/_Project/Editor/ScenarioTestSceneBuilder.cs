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
    /// Builds the scenario system test setup: TMP essentials, the dialogue bubble and results screen prefabs,
    /// two placeholder scenarios and the ScenarioTest scene. Run each step in its own batchmode run:
    ///   1. -executeMethod ProCommVR.Editor.ScenarioTestSceneBuilder.ImportTmpEssentialsAndExit (without -quit)
    ///   2. -executeMethod ProCommVR.Editor.ScenarioTestSceneBuilder.BuildBubblePrefab
    ///   3. -executeMethod ProCommVR.Editor.ScenarioTestSceneBuilder.BuildResultsScreenPrefab
    ///   4. -executeMethod ProCommVR.Editor.ScenarioTestSceneBuilder.BuildScenarioTestScene
    /// Requires the XR rig variant from XRTestSceneBuilder.
    /// </summary>
    public static class ScenarioTestSceneBuilder
    {
        private const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private const string BubblePrefabPath = "Assets/_Project/Prefabs/UI/DialogueBubble.prefab";
        private const string ResultsPrefabPath = "Assets/_Project/Prefabs/UI/ResultsScreen.prefab";
        private const string PlaceholderScenarioPath = "Assets/_Project/Dialogue/Scenarios/Placeholder_Scenario.asset";
        private const string PlaceholderScenario2Path = "Assets/_Project/Dialogue/Scenarios/Placeholder_Scenario2.asset";
        private const string RigVariantPath = "Assets/_Project/Prefabs/XR Origin (ProCommVR).prefab";
        private const string FloorMaterialPath = "Assets/_Project/Materials/Floor.mat";
        private const string NpcMaterialPath = "Assets/_Project/Materials/PlaceholderNpc.mat";
        private const string ScenePath = "Assets/_Project/Scenes/ScenarioTest.unity";

        private static readonly Vector2 BubbleSize = new Vector2(600f, 220f);
        private const float BubblePixelSize = 0.001f; // 1 px = 1 mm, so a bubble is 60 x 22 cm
        private static readonly Vector2 ResultsPanelSize = new Vector2(900f, 900f);
        private static readonly Color TextColor = new Color(0.12f, 0.12f, 0.14f);

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
                label.color = TextColor;
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

        [MenuItem("ProCommVR/Setup/5. Build Results Screen Prefab")]
        public static void BuildResultsScreenPrefab()
        {
            var bubblePrefab = LoadRequired<GameObject>(BubblePrefabPath, "Run 'Build Dialogue Bubble Prefab' first.");
            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(ResultsPrefabPath));

            // Plain root with the panel (its own canvas) and the restart bubble side by side, so the
            // bubble canvas is not nested in the panel canvas.
            var root = new GameObject("ResultsScreen");
            try
            {
                var panel = new GameObject("Panel", typeof(RectTransform));
                panel.transform.SetParent(root.transform, false);
                var panelRect = (RectTransform)panel.transform;
                panelRect.sizeDelta = ResultsPanelSize;
                panelRect.localScale = Vector3.one * BubblePixelSize;

                var canvas = panel.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                panel.AddComponent<CanvasScaler>();

                var background = panel.AddComponent<Image>();
                background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                background.type = Image.Type.Sliced;
                background.color = new Color(1f, 1f, 1f, 0.95f);
                background.raycastTarget = false;

                var title = CreateLabel(panelRect, "Title", 40f, 52f, TextAlignmentOptions.Center);
                var titleRect = title.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = Vector2.one;
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.offsetMin = new Vector2(32f, -140f);
                titleRect.offsetMax = new Vector2(-32f, -24f);
                title.fontStyle = FontStyles.Bold;

                var body = CreateLabel(panelRect, "Body", 16f, 28f, TextAlignmentOptions.TopLeft);
                var bodyRect = body.rectTransform;
                bodyRect.anchorMin = Vector2.zero;
                bodyRect.anchorMax = Vector2.one;
                bodyRect.offsetMin = new Vector2(40f, 32f);
                bodyRect.offsetMax = new Vector2(-40f, -160f);

                var restart = (GameObject)PrefabUtility.InstantiatePrefab(bubblePrefab);
                restart.name = "Restart Bubble";
                restart.transform.SetParent(root.transform, false);
                restart.transform.localPosition = new Vector3(0f, -(ResultsPanelSize.y * BubblePixelSize * 0.5f + 0.15f), 0f);

                var screen = root.AddComponent<ResultsScreen>();
                var serialized = new SerializedObject(screen);
                serialized.FindProperty("_titleLabel").objectReferenceValue = title;
                serialized.FindProperty("_bodyLabel").objectReferenceValue = body;
                serialized.FindProperty("_restartBubble").objectReferenceValue = restart.GetComponent<DialogueBubble>();
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ResultsPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            Debug.Log($"[ProCommVR] Results screen prefab created at {ResultsPrefabPath}.");
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, float minSize, float maxSize, TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = name;
            label.color = TextColor;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = minSize;
            label.fontSizeMax = maxSize;
            label.raycastTarget = false;
            return label;
        }

        [MenuItem("ProCommVR/Setup/6. Build Scenario Test Scene")]
        public static void BuildScenarioTestScene()
        {
            // Create the scene first: NewScene unloads assets that were loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rigVariant = LoadRequired<GameObject>(RigVariantPath, "Run 'Build XR Test Scene' first.");
            var bubblePrefab = LoadRequired<GameObject>(BubblePrefabPath, "Run 'Build Dialogue Bubble Prefab' first.")
                .GetComponent<DialogueBubble>();
            var resultsPrefab = LoadRequired<GameObject>(ResultsPrefabPath, "Run 'Build Results Screen Prefab' first.");
            var scenarios = new[] { GetOrCreatePlaceholderScenario(), GetOrCreatePlaceholderScenario2() };

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

            // Placeholder NPC: a capsule 2.5 m in front of the player start (GAME_BRIEF.md). The options
            // appear once the player walks within 1.5 m.
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "Placeholder NPC";
            npc.transform.position = new Vector3(0f, 0.9f, 2.5f);
            npc.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            npc.GetComponent<Renderer>().sharedMaterial = XRTestSceneBuilder.GetOrCreateMaterial(NpcMaterialPath, new Color(0.45f, 0.6f, 0.8f));

            var presenterObject = new GameObject("Scenario Presenter");
            var npcAnchor = new GameObject("NPC Bubble Anchor").transform;
            npcAnchor.SetParent(presenterObject.transform, false);
            npcAnchor.position = new Vector3(0f, 2.15f, 2.5f);
            npcAnchor.localScale = Vector3.one * 1.3f;
            var optionsAnchor = new GameObject("Options Anchor").transform;
            optionsAnchor.SetParent(presenterObject.transform, false);
            optionsAnchor.position = new Vector3(0f, 1.15f, 1.6f);

            var presenter = presenterObject.AddComponent<ScenarioPresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("_playOnStart").boolValue = false; // the sequence starts it
            serialized.FindProperty("_bubblePrefab").objectReferenceValue = bubblePrefab;
            serialized.FindProperty("_npcBubbleAnchor").objectReferenceValue = npcAnchor;
            serialized.FindProperty("_optionsAnchor").objectReferenceValue = optionsAnchor;
            serialized.FindProperty("_npc").objectReferenceValue = npc.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var results = (GameObject)PrefabUtility.InstantiatePrefab(resultsPrefab, scene);
            results.SetActive(false);

            var sequence = new GameObject("Scenario Sequence").AddComponent<ScenarioSequence>();
            serialized = new SerializedObject(sequence);
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_resultsScreen").objectReferenceValue = results.GetComponent<ResultsScreen>();
            var list = serialized.FindProperty("_scenarios");
            list.arraySize = scenarios.Length;
            for (var i = 0; i < scenarios.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = scenarios[i];
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
            return GetOrCreateScenario(PlaceholderScenarioPath, serialized =>
            {
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
            });
        }

        /// <summary>
        /// Second invented scenario, so the sequence and the results screen can be tested.
        /// </summary>
        private static Scenario GetOrCreatePlaceholderScenario2()
        {
            return GetOrCreateScenario(PlaceholderScenario2Path, serialized =>
            {
                serialized.FindProperty("_id").stringValue = "placeholder-upset-relative";
                serialized.FindProperty("_title").stringValue = "[PLACEHOLDER] The upset relative";
                serialized.FindProperty("_mood").enumValueIndex = (int)Mood.Tense;
                serialized.FindProperty("_npcName").stringValue = "Relative (placeholder)";
                serialized.FindProperty("_npcLine").stringValue =
                    "My father rang the bell twenty minutes ago and nobody came. Is this how you treat people here?";

                var options = serialized.FindProperty("_options");
                options.arraySize = Scenario.OptionCount;
                SetOption(options, 0, 3,
                    "You're right to be upset, twenty minutes is too long. I'll go to your father now, and afterwards we can talk about what happened.",
                    "Good choice: you acknowledge the anger, act on the urgent need first and offer a follow-up.");
                SetOption(options, 1, 1,
                    "We have a lot of patients today, we're doing our best.",
                    "Partly right: it explains the situation, but it sounds defensive and does not help the father.");
                SetOption(options, 2, 0,
                    "I'll tell my colleague who is responsible for his room.",
                    "Low: you pass the problem on, so the relative feels nobody takes ownership.");
                SetOption(options, 3, -2,
                    "Please lower your voice, or I'll have to ask you to leave.",
                    "Critical error: you escalate before listening. A boundary can be needed, but not as the first reaction.");
            });
        }

        private static Scenario GetOrCreateScenario(string path, System.Action<SerializedObject> fill)
        {
            var scenario = AssetDatabase.LoadAssetAtPath<Scenario>(path);
            if (scenario != null)
                return scenario;

            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(path));
            scenario = ScriptableObject.CreateInstance<Scenario>();
            AssetDatabase.CreateAsset(scenario, path);

            var serialized = new SerializedObject(scenario);
            fill(serialized);
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
