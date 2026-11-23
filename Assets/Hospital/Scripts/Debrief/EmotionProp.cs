using System;
using System.Collections.Generic;
using ProCommVR.Emotions;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// A small object on the table that stands for an emotion of the patient view: a rain cloud (Sadness), a small flame
    /// (Anger), a flashlight (Fear), a flower (Joy), or a grey sphere (no strong feeling). Simple shapes in one shared
    /// material with per-renderer colors (MaterialPropertyBlock), no particles, no lights. Laser + trigger picks it
    /// (XRSimpleInteractable); hovering shows its label. Glow = emission plus a soft pulse of the scale.
    /// </summary>
    public class EmotionProp : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private bool _hasEmotion;
        [SerializeField] private Emotion _emotion;
        [SerializeField] private TextMeshPro _label;
        [SerializeField] private Renderer _ring;

        private readonly List<Renderer> _renderers = new();
        private readonly List<Color> _colors = new();
        private MaterialPropertyBlock _block;
        private XRSimpleInteractable _interactable;
        private Vector3 _baseScale = Vector3.one;
        private float _glow;
        private float _glowTarget;
        private bool _labelAlways;

        public Emotion? Emotion => _hasEmotion ? _emotion : null;
        public bool IsGlowing => _glowTarget > 0f;
        public bool IsHovered { get; private set; }
        public XRSimpleInteractable Interactable => _interactable;
        public TextMeshPro Label => _label;
        public string LabelText => _label != null ? _label.text : "";
        public bool LabelVisible => _label != null && _label.gameObject.activeSelf;

        /// <summary>Picked with laser + trigger (or <see cref="Pick"/>).</summary>
        public event Action<EmotionProp> Picked;

        /// <summary>Picks the prop as laser + trigger does (also for tests).</summary>
        public void Pick() => Picked?.Invoke(this);

        public void SetGlow(bool on, Color color)
        {
            _glowTarget = on ? 1f : 0f;
            _glowColor = color;
            if (on)
            {
                _ringBeforeGlow = _ring != null && _ring.gameObject.activeSelf;
                SetRing(color, true);
            }
            else if (_ring != null && !_ringBeforeGlow)
                _ring.gameObject.SetActive(false);
        }

        private bool _ringBeforeGlow;

        private Color _glowColor = Color.white;

        /// <summary>Shows a ring under the prop (key moment, critical moment).</summary>
        public void SetRing(Color color, bool on)
        {
            if (_ring == null)
                return;
            _ring.gameObject.SetActive(on);
            Tint(_ring, color, color * 0.8f);
        }

        public void SetLabel(string text, bool always)
        {
            if (_label == null)
                return;
            _label.text = text;
            _labelAlways = always;
            _label.gameObject.SetActive(always);
        }

        public void SetInteractable(bool on)
        {
            if (_interactable != null)
                _interactable.enabled = on;
        }

        private void Update()
        {
            if (Mathf.Approximately(_glow, _glowTarget) && _glow <= 0f)
                return;
            _glow = Mathf.MoveTowards(_glow, _glowTarget, Time.deltaTime * 2.5f);
            var pulse = _glow * (0.6f + 0.4f * Mathf.Sin(Time.time * 4f));
            for (var i = 0; i < _renderers.Count; i++)
                Tint(_renderers[i], _colors[i], _glowColor * (pulse * 2.4f));
            transform.localScale = _baseScale * (1f + 0.12f * _glow);
        }

        private void LateUpdate()
        {
            if (_label == null || !_label.gameObject.activeSelf)
                return;
            var camera = Camera.main;
            if (camera == null)
                return;
            var away = _label.transform.position - camera.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f)
                _label.transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        private void Tint(Renderer renderer, Color color, Color emission)
        {
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, color);
            _block.SetColor(EmissionColor, emission);
            renderer.SetPropertyBlock(_block);
        }

        private void OnHoverEntered(HoverEnterEventArgs _)
        {
            IsHovered = true;
            if (_label != null)
                _label.gameObject.SetActive(true);
            _baseScale *= 1.1f;
            transform.localScale = _baseScale * (1f + 0.12f * _glow);
        }

        private void OnHoverExited(HoverExitEventArgs _)
        {
            IsHovered = false;
            if (_label != null && !_labelAlways)
                _label.gameObject.SetActive(false);
            _baseScale /= 1.1f;
            transform.localScale = _baseScale * (1f + 0.12f * _glow);
        }

        private void OnSelectEntered(SelectEnterEventArgs _) => Pick();

        private void OnDestroy()
        {
            if (_interactable == null)
                return;
            _interactable.hoverEntered.RemoveListener(OnHoverEntered);
            _interactable.hoverExited.RemoveListener(OnHoverExited);
            _interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        // ---- Building -------------------------------------------------------------------------------------------------

        /// <summary>The color of an emotion (as in the patient view: gold Joy, red-orange Anger, violet Fear, blue Sadness).</summary>
        public static Color ColorOf(Emotion? emotion) => emotion switch
        {
            Emotions.Emotion.Joy => new Color(1f, 0.82f, 0.25f),
            Emotions.Emotion.Anger => new Color(1f, 0.38f, 0.12f),
            Emotions.Emotion.Fear => new Color(0.55f, 0.45f, 0.85f),
            Emotions.Emotion.Sadness => new Color(0.45f, 0.6f, 0.85f),
            _ => new Color(0.6f, 0.62f, 0.65f),
        };

        public static string NameOf(Emotion? emotion) => emotion switch
        {
            Emotions.Emotion.Joy => "Joy",
            Emotions.Emotion.Anger => "Anger",
            Emotions.Emotion.Fear => "Fear",
            Emotions.Emotion.Sadness => "Sadness",
            _ => "Calm",
        };

        /// <summary>
        /// Builds a prop of about <paramref name="size"/> meters high, standing on its origin. With
        /// <paramref name="interactable"/> it gets one box collider and an XRSimpleInteractable.
        /// </summary>
        public static EmotionProp Create(Emotion? emotion, float size, Material material, Transform parent, bool interactable,
            TMP_FontAsset font = null)
        {
            var root = new GameObject($"Prop {NameOf(emotion)}");
            root.transform.SetParent(parent, false);
            var prop = root.AddComponent<EmotionProp>();
            prop._hasEmotion = emotion.HasValue;
            prop._emotion = emotion ?? default;
            var shape = new GameObject("Shape").transform;
            shape.SetParent(root.transform, false);
            shape.localScale = Vector3.one * size;

            switch (emotion)
            {
                case Emotions.Emotion.Sadness: // a rain cloud with three drops
                    var cloud = new Color(0.55f, 0.62f, 0.75f);
                    prop.Part(PrimitiveType.Sphere, shape, new Vector3(0f, 0.72f, 0f), new Vector3(0.55f, 0.4f, 0.4f), cloud, material);
                    prop.Part(PrimitiveType.Sphere, shape, new Vector3(-0.25f, 0.66f, 0f), new Vector3(0.38f, 0.3f, 0.32f), cloud, material);
                    prop.Part(PrimitiveType.Sphere, shape, new Vector3(0.26f, 0.67f, 0f), new Vector3(0.4f, 0.32f, 0.34f), cloud, material);
                    var drop = new Color(0.4f, 0.6f, 1f);
                    for (var i = 0; i < 3; i++)
                        prop.Part(PrimitiveType.Capsule, shape, new Vector3(-0.2f + 0.2f * i, 0.32f - 0.07f * (i % 2), 0f),
                            new Vector3(0.05f, 0.08f, 0.05f), drop, material);
                    break;
                case Emotions.Emotion.Anger: // a small flame: an orange outer and a yellow inner drop shape
                    prop.Part(PrimitiveType.Sphere, shape, new Vector3(0f, 0.3f, 0f), new Vector3(0.44f, 0.5f, 0.44f), new Color(1f, 0.35f, 0.08f), material);
                    prop.Part(PrimitiveType.Sphere, shape, new Vector3(0f, 0.55f, 0f), new Vector3(0.28f, 0.5f, 0.28f), new Color(1f, 0.35f, 0.08f), material);
                    prop.Part(PrimitiveType.Sphere, shape, new Vector3(0.02f, 0.82f, 0f), new Vector3(0.12f, 0.3f, 0.12f), new Color(1f, 0.45f, 0.1f), material);
                    prop.Part(PrimitiveType.Sphere, shape, new Vector3(0f, 0.32f, 0.12f), new Vector3(0.24f, 0.36f, 0.24f), new Color(1f, 0.85f, 0.25f), material);
                    prop.Part(PrimitiveType.Cylinder, shape, new Vector3(0f, 0.03f, 0f), new Vector3(0.36f, 0.03f, 0.36f), new Color(0.25f, 0.2f, 0.18f), material);
                    break;
                case Emotions.Emotion.Fear: // a flashlight lying on its side, the lens toward the viewer
                    var body = prop.Part(PrimitiveType.Cylinder, shape, new Vector3(0f, 0.16f, 0f), new Vector3(0.22f, 0.36f, 0.22f), new Color(0.15f, 0.16f, 0.2f), material);
                    body.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    var head = prop.Part(PrimitiveType.Cylinder, shape, new Vector3(0f, 0.18f, 0.38f), new Vector3(0.32f, 0.07f, 0.32f), new Color(0.3f, 0.3f, 0.36f), material);
                    head.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    var lens = prop.Part(PrimitiveType.Cylinder, shape, new Vector3(0f, 0.18f, 0.455f), new Vector3(0.26f, 0.01f, 0.26f), new Color(1f, 0.95f, 0.7f), material);
                    lens.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case Emotions.Emotion.Joy: // a flower: stem, five petals, a heart
                    prop.Part(PrimitiveType.Cylinder, shape, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.3f, 0.05f), new Color(0.3f, 0.65f, 0.3f), material);
                    var leaf = prop.Part(PrimitiveType.Sphere, shape, new Vector3(0.08f, 0.28f, 0f), new Vector3(0.16f, 0.05f, 0.08f), new Color(0.35f, 0.7f, 0.3f), material);
                    leaf.localRotation = Quaternion.Euler(0f, 0f, 30f);
                    for (var i = 0; i < 5; i++)
                    {
                        var a = i * Mathf.PI * 2f / 5f;
                        prop.Part(PrimitiveType.Sphere, shape, new Vector3(Mathf.Cos(a) * 0.16f, 0.7f + Mathf.Sin(a) * 0.16f, 0f),
                            new Vector3(0.2f, 0.2f, 0.08f), new Color(1f, 0.55f, 0.7f), material);
                    }
                    prop.Part(PrimitiveType.Sphere, shape, new Vector3(0f, 0.7f, -0.02f), new Vector3(0.16f, 0.16f, 0.1f), new Color(1f, 0.85f, 0.25f), material);
                    break;
                default: // no strong feeling: a small grey sphere
                    prop.Part(PrimitiveType.Sphere, shape, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.5f, new Color(0.6f, 0.62f, 0.65f), material);
                    break;
            }

            // The ring under it (key moments), hidden by default.
            var ring = prop.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.002f, 0f),
                new Vector3(size * 1.1f, 0.002f, size * 1.1f), Color.white, material);
            ring.name = "Ring";
            prop._ring = ring.GetComponent<Renderer>();
            prop._renderers.Remove(prop._ring);
            prop._colors.RemoveAt(prop._colors.Count - 1);
            ring.gameObject.SetActive(false);

            // The label (shown on hover, or always).
            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, size * 1.05f + 0.03f, 0f);
            if (font != null)
                label.font = font;
            label.fontSize = 0.22f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.rectTransform.sizeDelta = new Vector2(0.45f, 0.12f);
            label.color = new Color(0.12f, 0.12f, 0.14f);
            label.outlineWidth = 0.18f;
            label.outlineColor = new Color32(255, 255, 255, 200);
            label.text = NameOf(emotion);
            label.gameObject.SetActive(false);
            prop._label = label;

            if (interactable)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, size * 0.5f, 0f);
                box.size = new Vector3(size * 1.1f, size * 1.1f, size * 1.1f);
                var simple = root.AddComponent<XRSimpleInteractable>();
                simple.hoverEntered.AddListener(prop.OnHoverEntered);
                simple.hoverExited.AddListener(prop.OnHoverExited);
                simple.selectEntered.AddListener(prop.OnSelectEntered);
                prop._interactable = simple;
            }
            prop._baseScale = root.transform.localScale;
            return prop;
        }

        /// <summary>Sets the base scale after the prop was scaled from outside (e.g. growing in).</summary>
        public void SetBaseScale(Vector3 scale)
        {
            _baseScale = scale;
            transform.localScale = scale * (1f + 0.12f * _glow);
        }

        private Transform Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            DestroyImmediate(go.GetComponent<Collider>()); // before the interactable collects its colliders
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
                renderer.sharedMaterial = material;
            _renderers.Add(renderer);
            _colors.Add(color);
            Tint(renderer, color, Color.black);
            return go.transform;
        }
    }
}
