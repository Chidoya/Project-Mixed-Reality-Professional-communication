using System.IO;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Builds the emotion HUD prefab (Assets/_Project/Prefabs/UI/EmotionHud.prefab) in code, in the style of the
    /// dialogue bubbles (world-space canvas, 1 unit = 1 mm, TextMeshPro, the rounded built-in UISprite), and adds it to a
    /// scene. The HUD is drawn on top of the room (ZTest Always: the shader ProCommVR/UI Overlay for the panel and the bars,
    /// TextMeshPro's Mobile Distance Field Overlay for the text), so a wall between the eyes and the panel never cuts it off. Safe to run again. Batchmode:
    ///   -executeMethod ProCommVR.Editor.EmotionHudSetup.BuildPrefab
    /// </summary>
    public static class EmotionHudSetup
    {
        internal const string PrefabPath = "Assets/_Project/Prefabs/UI/EmotionHud.prefab";
        internal const string Header = "PATIENT VIEW";
        internal const string ImageMaterialPath = "Assets/_Project/Materials/UI/HudOverlay.mat";
        internal const string TextMaterialPath = "Assets/_Project/Materials/UI/HudOverlay_Text.mat";
        internal const string ImageShader = "ProCommVR/UI Overlay";
        internal const string TextShader = "TextMeshPro/Mobile/Distance Field Overlay";

        private const float PixelSize = 0.001f; // 1 unit = 1 mm
        private const float Width = 500f;
        private const float Padding = 26f;
        private const float HeaderHeight = 40f;
        private const float RowHeight = 58f;
        private const float HintHeight = 40f;
        private const float LabelWidth = 190f;
        private const float BarWidth = 180f;
        private const float BarHeight = 18f;
        private const float RowFontSize = 40f; // capitals about 28 mm: about 1 degree at 1.6 m
        private const float SmallFontSize = 28f;
        private const float TrustHeight = 96f;
        internal const string TrustLabel = "TRUST IN YOU";

        private static readonly Color PanelColor = new Color(9f / 255f, 18f / 255f, 26f / 255f, 0.74f);
        private static readonly Color TextColor = new Color(0.95f, 0.96f, 0.97f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.12f);

        private static readonly (Emotion Emotion, string Hex)[] Rows =
        {
            (Emotion.Joy, "#5FD0A4"),
            (Emotion.Anger, "#F2784B"),
            (Emotion.Fear, "#B79CF2"),
            (Emotion.Sadness, "#6FA8E8"),
        };

        [MenuItem("ProCommVR/Setup/10. Build Emotion HUD Prefab")]
        public static GameObject BuildPrefab()
        {
            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(PrefabPath));
            _imageMaterial = OverlayMaterial(ImageMaterialPath, new Material(Shader.Find(ImageShader)));
            _textMaterial = OverlayMaterial(TextMaterialPath, new Material(TMP_Settings.defaultFontAsset.material) { shader = Shader.Find(TextShader) });
            var height = Padding * 2f + HeaderHeight + RowHeight * Rows.Length + TrustHeight + HintHeight;
            var root = new GameObject("EmotionHud", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(Width, height);
                rect.localScale = Vector3.one * PixelSize;

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 10;
                root.AddComponent<CanvasScaler>();
                root.AddComponent<CanvasGroup>();

                var background = root.AddComponent<Image>();
                background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = 0.35f; // rounder corners
                background.color = PanelColor;
                background.material = _imageMaterial;
                background.raycastTarget = false;

                var y = -Padding;
                var header = Text(rect, "Header", Header, SmallFontSize, new Vector2(Padding, y), new Vector2(Width - Padding * 2f, HeaderHeight),
                    TextAlignmentOptions.Left, FontStyles.Bold);
                header.characterSpacing = 6f;
                header.alpha = 0.75f;
                y -= HeaderHeight;

                var rows = new EmotionHud.Row[Rows.Length];
                for (var i = 0; i < Rows.Length; i++)
                {
                    ColorUtility.TryParseHtmlString(Rows[i].Hex, out var color);
                    var row = new EmotionHud.Row { Emotion = Rows[i].Emotion };
                    row.Label = Text(rect, $"{Rows[i].Emotion} Label", Rows[i].Emotion.ToString(), RowFontSize,
                        new Vector2(Padding, y), new Vector2(LabelWidth, RowHeight), TextAlignmentOptions.Left, FontStyles.Normal);

                    var barX = Padding + LabelWidth;
                    var barY = y - (RowHeight - BarHeight) * 0.5f;
                    Box(rect, $"{Rows[i].Emotion} Track", new Vector2(barX, barY), new Vector2(BarWidth, BarHeight), TrackColor);
                    var fill = Box(rect, $"{Rows[i].Emotion} Fill", new Vector2(barX, barY), new Vector2(0f, BarHeight), color);
                    row.Fill = (RectTransform)fill.transform;
                    row.FillImage = fill;

                    var numberX = barX + BarWidth;
                    row.Number = Text(rect, $"{Rows[i].Emotion} Number", "0", RowFontSize, new Vector2(numberX, y),
                        new Vector2(Width - Padding - numberX, RowHeight), TextAlignmentOptions.Right, FontStyles.Normal);
                    rows[i] = row;
                    y -= RowHeight;
                }

                // Trust meter: how the patient thinks about the nurse (PatientReputation), a full-width bar under the emotions.
                var trust = new GameObject("Trust", typeof(RectTransform));
                var trustRect = (RectTransform)trust.transform;
                Place(rect, trustRect, new Vector2(0f, y), new Vector2(Width, TrustHeight));
                var trustLabel = Text(trustRect, "Trust Label", TrustLabel, SmallFontSize, new Vector2(Padding, -10f),
                    new Vector2(Width * 0.6f, 36f), TextAlignmentOptions.Left, FontStyles.Bold);
                trustLabel.characterSpacing = 6f;
                trustLabel.alpha = 0.75f;
                var trustNumber = Text(trustRect, "Trust Number", "50", RowFontSize, new Vector2(Width * 0.6f, -6f),
                    new Vector2(Width * 0.4f - Padding, 44f), TextAlignmentOptions.Right, FontStyles.Bold);
                var trustBarWidth = Width - Padding * 2f;
                Box(trustRect, "Trust Track", new Vector2(Padding, -54f), new Vector2(trustBarWidth, BarHeight + 6f), TrackColor);
                var trustFill = Box(trustRect, "Trust Fill", new Vector2(Padding, -54f), new Vector2(trustBarWidth * 0.5f, BarHeight + 6f),
                    new Color(0.98f, 0.76f, 0.3f));
                y -= TrustHeight;

                var hint = Text(rect, "Hint", PovSetup.HintText, SmallFontSize, new Vector2(Padding, y),
                    new Vector2(Width - Padding * 2f, HintHeight), TextAlignmentOptions.Left, FontStyles.Italic);
                hint.alpha = 0.8f;

                var hud = root.AddComponent<EmotionHud>();
                var serialized = new SerializedObject(hud);
                var rowList = serialized.FindProperty("_rows");
                rowList.arraySize = rows.Length;
                for (var i = 0; i < rows.Length; i++)
                {
                    var element = rowList.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("Emotion").enumValueIndex = (int)rows[i].Emotion;
                    element.FindPropertyRelative("Label").objectReferenceValue = rows[i].Label;
                    element.FindPropertyRelative("Fill").objectReferenceValue = rows[i].Fill;
                    element.FindPropertyRelative("FillImage").objectReferenceValue = rows[i].FillImage;
                    element.FindPropertyRelative("Number").objectReferenceValue = rows[i].Number;
                }
                serialized.FindProperty("_header").objectReferenceValue = header;
                serialized.FindProperty("_hint").objectReferenceValue = hint;
                serialized.FindProperty("_barWidth").floatValue = BarWidth;
                serialized.FindProperty("_trustGroup").objectReferenceValue = trust;
                serialized.FindProperty("_trustFill").objectReferenceValue = trustFill.rectTransform;
                serialized.FindProperty("_trustFillImage").objectReferenceValue = trustFill;
                serialized.FindProperty("_trustNumber").objectReferenceValue = trustNumber;
                serialized.FindProperty("_trustLabel").objectReferenceValue = trustLabel;
                serialized.FindProperty("_trustBarWidth").floatValue = trustBarWidth;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Adds the HUD to the scene, connected to the patient's emotions and the POV switch.
        /// </summary>
        internal static EmotionHud AddToScene(EmotionHolder holder, PovController pov, PatientReputation reputation = null)
        {
            var prefab = BuildPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Emotion HUD (patient view)";
            var hud = instance.GetComponent<EmotionHud>();
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("_holder").objectReferenceValue = holder;
            serialized.FindProperty("_pov").objectReferenceValue = pov;
            serialized.FindProperty("_reputation").objectReferenceValue = reputation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        private static Material _imageMaterial;
        private static Material _textMaterial;

        /// <summary>
        /// Saves the material as an asset, or updates the existing asset to it (keeps its GUID).
        /// </summary>
        private static Material OverlayMaterial(string path, Material template)
        {
            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(path));
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = template;
                material.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = template.shader;
                material.CopyPropertiesFromMaterial(template);
                Object.DestroyImmediate(template);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static TextMeshProUGUI Text(RectTransform parent, string name, string text, float size, Vector2 topLeft, Vector2 extent,
            TextAlignmentOptions alignment, FontStyles style)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            Place(parent, label.rectTransform, topLeft, extent);
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = TextColor;
            label.alignment = alignment == TextAlignmentOptions.Right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSharedMaterial = _textMaterial;
            label.raycastTarget = false;
            return label;
        }

        private static Image Box(RectTransform parent, string name, Vector2 topLeft, Vector2 extent, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            Place(parent, image.rectTransform, topLeft, extent);
            image.color = color;
            image.material = _imageMaterial;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Anchors a child at the top-left of the panel: topLeft in units from the panel's top-left (y negative down).
        /// </summary>
        private static void Place(RectTransform parent, RectTransform child, Vector2 topLeft, Vector2 extent)
        {
            child.SetParent(parent, false);
            child.anchorMin = child.anchorMax = new Vector2(0f, 1f);
            child.pivot = new Vector2(0f, 1f);
            child.anchoredPosition = topLeft;
            child.sizeDelta = extent;
        }
    }
}
