using System.Linq;
using ProCommVR.AiTalk;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using ProCommVR.Testing;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Builds the <see cref="DevClipboard"/> on the desk of the lobby (Liam's staff station): his clipboard model with a
    /// world-space canvas on it (pages Tasks and Dev). Liam's own clipboard in the scene is switched off in this scene
    /// only; his prefab is not changed. The canvas is laid out on a design size and scaled to the clipboard.
    /// </summary>
    internal static class DevClipboardSetup
    {
        private const string ClipboardPrefabPath = "Assets/Hospital/Art/Props/Prefabs/Clipboard.prefab";
        private static readonly Vector2 Design = new Vector2(220f, 300f);
        private static readonly Color Paper = new Color(0.96f, 0.95f, 0.9f, 1f);
        private static readonly Color Ink = new Color(0.12f, 0.12f, 0.15f);
        private static readonly Color ButtonColor = new Color(0.86f, 0.87f, 0.9f);

        internal static DevClipboard AddToScene(Transform hospital, EmotionHolder holder, PatientReputation reputation,
            AiPatientBridge bridge, PovController pov, GameObject rig, Camera eventCamera, VoiceActivityTalk voice = null,
            ScenarioBriefing briefing = null)
        {
            var station = hospital.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "StaffStation");
            var liamClipboard = station != null ? station.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Clipboard") : null;
            var anchor = station != null ? station.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Anchor_Clipboard") : null;
            if (liamClipboard == null || anchor == null)
                throw new System.InvalidOperationException("[ProCommVR] The clipboard of the staff station (Lobby/StaffStation) is missing.");

            // Built at the origin (to measure it), then moved onto the desk. Forward points to the visitor side.
            var root = new GameObject("Dev Clipboard (Tasks, Dev)").transform;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ClipboardPrefabPath), visual);
            // The clip at the far end (the top of the page), as the player reads it.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(anchor.rotation) * liamClipboard.rotation;
            model.transform.localScale = liamClipboard.lossyScale;
            var bounds = Scenario01SceneBuilder.TightBounds(model);

            // The paper: the clipboard's top, a little smaller than the board (the clip is at the far end).
            var paper = new Vector2(bounds.size.x * 0.9f, bounds.size.z * 0.84f);
            var scale = Mathf.Min(paper.x / Design.x, paper.y / Design.y);
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            var rect = (RectTransform)canvasObject.transform;
            rect.SetParent(visual, false);
            rect.sizeDelta = Design;
            rect.localScale = Vector3.one * scale;
            rect.localPosition = new Vector3(bounds.center.x, bounds.max.y + 0.002f, bounds.center.z + bounds.size.z * 0.05f); // away from the clip
            Debug.Log($"[ProCommVR] Dev clipboard: board {bounds.size.x:0.000} x {bounds.size.z:0.000} m, canvas scale {scale:0.00000}.");
            // Face up, the text's up away from the visitor (root -Z).
            rect.localRotation = Quaternion.Euler(90f, 180f, 0f);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eventCamera;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            var background = canvasObject.AddComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            background.color = Paper;

            var clipboard = root.gameObject.AddComponent<DevClipboard>();

            // ---- Page 1: Case (the situation of the patient) ----
            var casePage = Page(rect, "Page Case");
            var caseTitle = Label(casePage, "Title", "<b>CASE</b>", 16f, new Vector2(0f, 128f), new Vector2(204f, 24f), TextAlignmentOptions.Center);
            var caseBody = Label(casePage, "Body", "", 8.4f, new Vector2(0f, 2f), new Vector2(206f, 226f), TextAlignmentOptions.TopLeft);
            AutoSize(caseBody, 5.5f);

            // ---- Page: Guide (hints per patient state, the nurse looks at the patient to know which one applies) ----
            var guidePage = Page(rect, "Page Guide");
            Label(guidePage, "Title", "<b>GUIDE</b>", 16f, new Vector2(0f, 128f), new Vector2(204f, 24f), TextAlignmentOptions.Center);
            var guideBody = Label(guidePage, "Body", "", 8.4f, new Vector2(0f, 2f), new Vector2(206f, 226f), TextAlignmentOptions.TopLeft);
            AutoSize(guideBody, 5.5f);

            // ---- Page 2: Tasks ----
            var tasks = Page(rect, "Page Tasks");
            Label(tasks, "Title", "<b>TODAY'S TASKS</b>", 16f, new Vector2(0f, 128f), new Vector2(204f, 24f), TextAlignmentOptions.Center);
            var tasksBody = Label(tasks, "Body", "", 9.5f, new Vector2(0f, 4f), new Vector2(204f, 214f), TextAlignmentOptions.TopLeft);
            AutoSize(tasksBody, 6f);

            // ---- Page 3: Dev ----
            var dev = Page(rect, "Page Dev");
            Label(dev, "Title", "<b>DEV</b>", 16f, new Vector2(0f, 128f), new Vector2(204f, 24f), TextAlignmentOptions.Center);
            var status = Label(dev, "Status", "", 8f, new Vector2(0f, 88f), new Vector2(204f, 56f), TextAlignmentOptions.TopLeft);
            var emotionSize = new Vector2(64f, 26f);
            Button(dev, "Joy", new Vector2(-70f, 42f), emotionSize, new Color(1f, 0.85f, 0.3f), clipboard.ShowJoy);
            Button(dev, "Anger", new Vector2(0f, 42f), emotionSize, new Color(0.95f, 0.45f, 0.4f), clipboard.ShowAnger);
            Button(dev, "Fear", new Vector2(70f, 42f), emotionSize, new Color(0.7f, 0.58f, 0.92f), clipboard.ShowFear);
            Button(dev, "Sadness", new Vector2(-35f, 10f), emotionSize, new Color(0.5f, 0.68f, 0.95f), clipboard.ShowSadness);
            Button(dev, "Neutral", new Vector2(35f, 10f), emotionSize, ButtonColor, clipboard.ShowNeutral);
            Label(dev, "Trust Label", "<b>Trust</b>", 10f, new Vector2(-84f, -26f), new Vector2(40f, 18f), TextAlignmentOptions.Left);
            var slider = Slider(dev, new Vector2(4f, -26f), new Vector2(128f, 18f));
            var trustValue = Label(dev, "Trust Value", "50", 11f, new Vector2(90f, -26f), new Vector2(28f, 18f), TextAlignmentOptions.Right);
            Button(dev, "AI drives", new Vector2(-52f, -60f), new Vector2(96f, 26f), ButtonColor, clipboard.LetAiDrive);
            Button(dev, "Restart talk", new Vector2(52f, -60f), new Vector2(96f, 26f), ButtonColor, clipboard.RestartConversation);
            // The microphone: which one, how loud, and whether that counts as speech. Next mic tries the next one.
            var micLabel = Label(dev, "Mic", "", 6.5f, new Vector2(-28f, -90f), new Vector2(152f, 28f), TextAlignmentOptions.Left);
            Button(dev, "Next mic", new Vector2(80f, -90f), new Vector2(50f, 24f), ButtonColor, clipboard.NextMicrophone);

            // ---- Footer: pages and carry ----
            Button(rect, "<", new Vector2(-88f, -128f), new Vector2(30f, 24f), ButtonColor, clipboard.PreviousPage);
            var pageLabel = Label(rect, "Page Label", "", 9f, new Vector2(-40f, -128f), new Vector2(64f, 24f), TextAlignmentOptions.Center);
            Button(rect, ">", new Vector2(8f, -128f), new Vector2(30f, 24f), ButtonColor, clipboard.NextPage);
            var carry = Button(rect, "Take (X)", new Vector2(70f, -128f), new Vector2(72f, 24f), new Color(0.75f, 0.88f, 0.78f), clipboard.ToggleCarry);

            var leftHand = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Left Controller");
            var serialized = new SerializedObject(clipboard);
            serialized.FindProperty("_holder").objectReferenceValue = holder;
            serialized.FindProperty("_reputation").objectReferenceValue = reputation;
            serialized.FindProperty("_bridge").objectReferenceValue = bridge;
            serialized.FindProperty("_pov").objectReferenceValue = pov;
            var pages = serialized.FindProperty("_pages");
            // The shift's tasks first, then the briefing; Dev only with dev mode (DevClipboard._devMode).
            pages.arraySize = 4;
            pages.GetArrayElementAtIndex(0).objectReferenceValue = tasks.gameObject;
            pages.GetArrayElementAtIndex(1).objectReferenceValue = casePage.gameObject;
            pages.GetArrayElementAtIndex(2).objectReferenceValue = guidePage.gameObject;
            pages.GetArrayElementAtIndex(3).objectReferenceValue = dev.gameObject;
            var pageNames = serialized.FindProperty("_pageNames");
            var names = new[] { "Tasks", "Case", "Guide", "Dev" };
            pageNames.arraySize = names.Length;
            for (var i = 0; i < names.Length; i++)
                pageNames.GetArrayElementAtIndex(i).stringValue = names[i];
            serialized.FindProperty("_briefing").objectReferenceValue = briefing;
            serialized.FindProperty("_caseTitle").objectReferenceValue = caseTitle;
            serialized.FindProperty("_caseBody").objectReferenceValue = caseBody;
            serialized.FindProperty("_guideBody").objectReferenceValue = guideBody;
            serialized.FindProperty("_pageLabel").objectReferenceValue = pageLabel;
            serialized.FindProperty("_tasksBody").objectReferenceValue = tasksBody;
            serialized.FindProperty("_status").objectReferenceValue = status;
            serialized.FindProperty("_trustSlider").objectReferenceValue = slider;
            serialized.FindProperty("_trustValue").objectReferenceValue = trustValue;
            serialized.FindProperty("_visual").objectReferenceValue = visual.gameObject;
            serialized.FindProperty("_hand").objectReferenceValue = leftHand;
            serialized.FindProperty("_carryLabel").objectReferenceValue = carry;
            serialized.FindProperty("_voice").objectReferenceValue = voice;
            serialized.FindProperty("_micLabel").objectReferenceValue = micLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            dev.gameObject.SetActive(false);
            casePage.gameObject.SetActive(false);
            guidePage.gameObject.SetActive(false);

            // On the desk, where Liam's clipboard lies; his own is switched off in this scene.
            root.SetPositionAndRotation(liamClipboard.position, anchor.rotation);
            liamClipboard.gameObject.SetActive(false);
            if (leftHand == null)
                Debug.LogWarning("[ProCommVR] Dev clipboard: no 'Left Controller' in the rig, Take does nothing.");
            return clipboard;
        }

        private static void AutoSize(TextMeshProUGUI label, float min)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = label.fontSize;
        }

        private static RectTransform Page(RectTransform parent, string name)
        {
            var page = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)page.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = Design;
            return rect;
        }

        private static TextMeshProUGUI Button(RectTransform parent, string text, Vector2 position, Vector2 size, Color color, UnityAction onClick)
        {
            var buttonObject = new GameObject($"Button {text}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = buttonObject.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 6f;
            image.color = color;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            UnityEventTools.AddPersistentListener(button.onClick, onClick);
            return Label(rect, "Label", $"<b>{text}</b>", 9f, Vector2.zero, size, TextAlignmentOptions.Center);
        }

        private static Slider Slider(RectTransform parent, Vector2 position, Vector2 size)
        {
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var sliderObject = new GameObject("Trust Slider", typeof(RectTransform));
            var rect = (RectTransform)sliderObject.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var background = Child(rect, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 6f;
            background.color = new Color(0.78f, 0.78f, 0.8f);
            var fillArea = Child(rect, "Fill Area", Vector2.zero, Vector2.one, new Vector2(4f, 3f), new Vector2(-4f, -3f));
            var fill = Child(fillArea, "Fill", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = sprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = 6f;
            fillImage.color = new Color(0.35f, 0.65f, 0.45f);
            var handleArea = Child(rect, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            var handle = Child(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f), new Vector2(-8f, -4f), new Vector2(8f, 4f));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = knob;
            handleImage.color = new Color(0.2f, 0.22f, 0.26f);

            var slider = sliderObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.value = 50f;
            return slider;
        }

        private static RectTransform Child(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, string text, float fontSize, Vector2 position, Vector2 size,
            TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Ink;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }
    }
}
