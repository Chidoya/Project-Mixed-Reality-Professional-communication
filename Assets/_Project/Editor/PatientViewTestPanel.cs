using ProCommVR.Emotions;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
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
    /// Builds the test panel of the PatientViewTest scene: a world-space canvas (1 unit = 1 mm) with the patient's
    /// status, a button per emotion, a restart button and the controls, wired to <see cref="PatientViewTestControls"/>.
    /// </summary>
    internal static class PatientViewTestPanel
    {
        private static readonly Vector2 Size = new Vector2(720f, 560f);
        private const float PixelSize = 0.001f;
        private static readonly Color Background = new Color(0.12f, 0.14f, 0.18f, 0.92f);
        private static readonly Color TextColor = new Color(0.95f, 0.96f, 0.97f);

        internal static PatientViewTestControls AddToScene(EmotionHolder holder, ScenarioSequence sequence, PovController pov,
            Camera eventCamera, Vector3 position, Quaternion rotation)
        {
            if (holder == null || sequence == null || pov == null)
                throw new System.InvalidOperationException("The test panel needs an EmotionHolder, a ScenarioSequence and a PovController.");

            var root = new GameObject("Patient View Test Panel", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = Size;
            rect.localScale = Vector3.one * PixelSize;
            rect.SetPositionAndRotation(position, rotation);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eventCamera;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            var background = root.AddComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            background.color = Background;

            Label(rect, "Title", "Patient view test", 40f, new Vector2(0f, 230f), new Vector2(660f, 60f), FontStyles.Bold);
            var status = Label(rect, "Status", "Status", 26f, new Vector2(0f, 135f), new Vector2(660f, 120f), FontStyles.Normal);

            var controls = root.AddComponent<PatientViewTestControls>();
            var serialized = new SerializedObject(controls);
            serialized.FindProperty("_holder").objectReferenceValue = holder;
            serialized.FindProperty("_sequence").objectReferenceValue = sequence;
            serialized.FindProperty("_pov").objectReferenceValue = pov;
            serialized.FindProperty("_status").objectReferenceValue = status;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Button(rect, "Joy", new Vector2(-235f, 25f), new Color(1f, 0.85f, 0.3f), controls.ShowJoy);
            Button(rect, "Anger", new Vector2(0f, 25f), new Color(0.95f, 0.4f, 0.35f), controls.ShowAnger);
            Button(rect, "Fear", new Vector2(235f, 25f), new Color(0.65f, 0.5f, 0.9f), controls.ShowFear);
            Button(rect, "Sadness", new Vector2(-235f, -75f), new Color(0.45f, 0.65f, 0.95f), controls.ShowSadness);
            Button(rect, "Neutral", new Vector2(0f, -75f), new Color(0.8f, 0.82f, 0.85f), controls.ShowNeutral);
            Button(rect, "Restart", new Vector2(235f, -75f), Color.white, controls.RestartConversation);

            Label(rect, "Help",
                "Y: switch view (during the conversation, near the patient)\n" +
                "A: next emotion   B: restart conversation\n" +
                "Keyboard: 1 Joy, 2 Anger, 3 Fear, 4 Sadness, 0 Neutral, R restart",
                22f, new Vector2(0f, -195f), new Vector2(660f, 120f), FontStyles.Normal);
            return controls;
        }

        private static void Button(RectTransform parent, string text, Vector2 position, Color color, UnityAction onClick)
        {
            var buttonObject = new GameObject($"Button {text}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(215f, 80f);
            var image = buttonObject.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            UnityEventTools.AddPersistentListener(button.onClick, onClick);

            var label = Label(rect, "Label", text, 32f, Vector2.zero, rect.sizeDelta, FontStyles.Bold);
            label.color = new Color(0.1f, 0.1f, 0.12f);
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, string text, float fontSize, Vector2 position,
            Vector2 size, FontStyles style)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = TextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }
    }
}
