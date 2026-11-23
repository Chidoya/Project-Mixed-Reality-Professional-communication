using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// The conversation as a row of objects on the table, one per recorded turn (max 8), left to right as the player sees
    /// it. The shape is the patient's emotion after that turn (cloud, flame, flashlight, flower; grey sphere = calm), on a
    /// thin post whose height is his trust (0..100 -> 2..25 cm), with a thin line over the tops: the trust curve. Key
    /// moments get a soft ring, the critical moment a red one. The objects grow in one by one. Pointing at one shows what
    /// the student said then; picking it asks for its memory (<see cref="PointPicked"/>).
    /// Static meshes in one shared material (colors per renderer), no lights.
    /// </summary>
    public class EmotionTimeline : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float _objectSize = 0.08f;
        [SerializeField] private Vector2 _postHeight = new(0.02f, 0.25f);
        [SerializeField, Min(0.1f)] private float _maxSpacing = 0.36f;
        [SerializeField, Min(0.3f)] private float _maxLength = 2.6f;
        [SerializeField, Min(0f)] private float _growDelay = 0.15f;
        [SerializeField] private Color _keyRing = new(1f, 0.85f, 0.4f);
        [SerializeField] private Color _criticalRing = new(1f, 0.2f, 0.15f);

        private readonly List<EmotionProp> _props = new();
        private readonly List<TimelinePoint> _points = new();
        private readonly List<Transform> _posts = new();
        private LineRenderer _line;

        public IReadOnlyList<EmotionProp> Props => _props;
        public IReadOnlyList<TimelinePoint> Points => _points;
        public IReadOnlyList<Transform> Posts => _posts;
        public Vector2 PostHeightRange => _postHeight;
        public bool IsBuilt => _props.Count > 0;

        /// <summary>The player picked a point of the timeline (laser + trigger).</summary>
        public event Action<TimelinePoint> PointPicked;

        public static float PostHeightFor(float trust, Vector2 range) => Mathf.Lerp(range.x, range.y, Mathf.Clamp01(trust / 100f));

        /// <summary>
        /// Builds the row on the table. <paramref name="table"/>: position = the middle of the row on the table top,
        /// right = left to right for the player, up = up.
        /// </summary>
        public void Build(IReadOnlyList<TimelinePoint> points, Transform table, Material material, TMP_FontAsset font)
        {
            Clear();
            if (points == null || points.Count == 0 || table == null)
                return;
            transform.SetPositionAndRotation(table.position, table.rotation);
            var spacing = Mathf.Min(_maxSpacing, _maxLength / Mathf.Max(1, points.Count - 1));
            var start = -spacing * (points.Count - 1) / 2f;
            var tops = new Vector3[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var height = PostHeightFor(point.Trust, _postHeight);
                var x = start + spacing * i;
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                DestroyImmediate(post.GetComponent<Collider>());
                post.name = $"Post {point.TurnIndex}";
                post.transform.SetParent(transform, false);
                post.transform.localPosition = new Vector3(x, height / 2f, 0f);
                post.transform.localScale = new Vector3(0.012f, height / 2f, 0.012f);
                var postRenderer = post.GetComponent<Renderer>();
                postRenderer.sharedMaterial = material;
                postRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Color(postRenderer, new Color(0.75f, 0.77f, 0.8f));
                _posts.Add(post.transform);

                var prop = EmotionProp.Create(point.Dominant, _objectSize, material, transform, true, font);
                prop.name = $"Turn {point.TurnIndex} ({EmotionProp.NameOf(point.Dominant)})";
                prop.transform.localPosition = new Vector3(x, height, 0f);
                prop.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // the row looks away from the player; the props face the player
                var said = string.IsNullOrEmpty(point.StudentText) ? "(no words)" : Shorten(point.StudentText, 60);
                prop.SetLabel($"You: '{said}'", false);
                if (prop.Label != null)
                {
                    prop.Label.fontSize = 0.14f;
                    prop.Label.rectTransform.sizeDelta = new Vector2(0.5f, 0.12f);
                }
                if (point.IsCritical)
                    prop.SetRing(_criticalRing, true);
                else if (point.IsKey)
                    prop.SetRing(_keyRing, true);
                var captured = point;
                prop.Picked += _ => PointPicked?.Invoke(captured);
                prop.SetBaseScale(Vector3.zero);
                _props.Add(prop);
                _points.Add(point);
                tops[i] = new Vector3(x, height + _objectSize * 0.5f, 0f);
            }

            var lineGo = new GameObject("Trust Line");
            lineGo.transform.SetParent(transform, false);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.positionCount = tops.Length;
            _line.SetPositions(tops);
            _line.widthMultiplier = 0.004f;
            _line.sharedMaterial = material;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.startColor = _line.endColor = new Color(0.9f, 0.92f, 0.95f);
            Color(_line, new Color(0.85f, 0.88f, 0.92f));
            _line.enabled = false;
        }

        /// <summary>Grows the objects in, one by one. Yield on it.</summary>
        public IEnumerator GrowIn()
        {
            for (var i = 0; i < _props.Count; i++)
            {
                StartCoroutine(Grow(_props[i]));
                if (_growDelay > 0f)
                    yield return new WaitForSeconds(_growDelay);
            }
            if (_line != null)
                _line.enabled = true;
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>Shows everything at once (tests, or skipping).</summary>
        public void ShowAll()
        {
            foreach (var prop in _props)
                prop.SetBaseScale(Vector3.one);
            if (_line != null)
                _line.enabled = true;
        }

        private static IEnumerator Grow(EmotionProp prop)
        {
            var t = 0f;
            while (t < 1f && prop != null)
            {
                t += Time.deltaTime / 0.3f;
                prop.SetBaseScale(Vector3.one * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
        }

        public EmotionProp PropFor(int turnIndex)
        {
            for (var i = 0; i < _points.Count; i++)
                if (_points[i].TurnIndex == turnIndex)
                    return _props[i];
            return null;
        }

        public void SetInteractable(bool on)
        {
            foreach (var prop in _props)
                prop.SetInteractable(on);
        }

        public void Clear()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
            _props.Clear();
            _points.Clear();
            _posts.Clear();
            _line = null;
        }

        private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 3).TrimEnd() + "...";

        private static MaterialPropertyBlock _block;

        private static void Color(Renderer renderer, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_EmissionColor", UnityEngine.Color.black);
            renderer.SetPropertyBlock(_block);
        }
    }
}
