using TMPro;
using UnityEngine;

namespace ProCommVR.AiTalk
{
    /// <summary>
    /// Text on the black screen between the conversation and the doctor, e.g. "CRITICAL ERROR" with what went wrong. A
    /// small world-space canvas in front of the camera, drawn after the black fade quad (a copy of the overlay text
    /// material with a later render queue), so it is readable while the view is black. Made at runtime.
    /// </summary>
    public class EndingCard : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [Tooltip("TextMeshPro overlay material (ZTest always); a copy is drawn after the fade.")]
        [SerializeField] private Material _textMaterial;
        [SerializeField] private float _distance = 1.2f;

        private GameObject _root;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;
        private TextMeshProUGUI _body;

        public bool IsShown => _root != null && _root.activeSelf;
        public string Title => _title != null ? _title.text : "";
        public string Subtitle => _subtitle != null ? _subtitle.text : "";
        public string Body => _body != null ? _body.text : "";

        private void Awake()
        {
            if (_camera == null)
                _camera = Camera.main;
            Build();
            _root.SetActive(false);
        }

        public void Show(string title, Color titleColor, string subtitle, string body)
        {
            _title.text = title;
            _title.color = titleColor;
            _subtitle.text = subtitle;
            _body.text = body;
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        private void Build()
        {
            const float pixels = 1000f;
            _root = new GameObject("Ending Card (black screen)", typeof(RectTransform));
            var rect = (RectTransform)_root.transform;
            if (_camera != null)
                rect.SetParent(_camera.transform, false);
            rect.localPosition = new Vector3(0f, 0f, _distance);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one / pixels;
            rect.sizeDelta = new Vector2(900f, 500f);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            Material material = null;
            if (_textMaterial != null)
            {
                material = new Material(_textMaterial) { name = "Ending Card Text" };
                material.renderQueue = 4010; // after the fade quad (Overlay, 4000)
            }
            _title = Label(rect, "Title", 80f, new Vector2(0f, 150f), new Vector2(900f, 110f), FontStyles.Bold, material);
            _subtitle = Label(rect, "Subtitle", 44f, new Vector2(0f, 60f), new Vector2(900f, 70f), FontStyles.Bold, material);
            _body = Label(rect, "Body", 32f, new Vector2(0f, -90f), new Vector2(860f, 220f), FontStyles.Normal, material);
            _subtitle.color = Color.white;
            _body.color = new Color(0.85f, 0.85f, 0.88f);
        }

        private static TextMeshProUGUI Label(RectTransform parent, string name, float size, Vector2 position, Vector2 box,
            FontStyles style, Material material)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            if (material != null)
                text.fontSharedMaterial = material;
            return text;
        }
    }
}
