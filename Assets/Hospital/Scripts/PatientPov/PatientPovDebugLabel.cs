using System.Text;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using ProCommVR.Testing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// Test scene only: a read-only line under the patient view test panel, "Face: Sad 72% | World: Sadness 72%", so
    /// you can check that the face and the room show the same emotion. Does nothing when the scene has no test panel.
    /// </summary>
    public class PatientPovDebugLabel : MonoBehaviour
    {
        [SerializeField] private EmotionFaceDriver _face;
        [SerializeField] private PatientWorldController _world;
        [SerializeField] private PovController _pov;
        [SerializeField, Min(0.05f)] private float _interval = 0.2f;

        private readonly StringBuilder _text = new StringBuilder(64);
        private TMP_Text _label;
        private float _next;

        public TMP_Text Label => _label;

        private void Start()
        {
            var panel = FindAnyObjectByType<PatientViewTestControls>();
            if (panel == null || _face == null)
            {
                enabled = false;
                return;
            }
            var parent = (RectTransform)panel.transform;
            var box = new GameObject("Face And World", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)box.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f);
            rect.sizeDelta = new Vector2(720f, 64f);
            var image = box.GetComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);
            image.raycastTarget = false;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = 28f;
            label.color = new Color(0.95f, 0.96f, 0.97f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            _label = label;
            UpdateText();
        }

        private void Update()
        {
            if (_label == null || Time.unscaledTime < _next)
                return;
            _next = Time.unscaledTime + _interval;
            UpdateText();
        }

        private void UpdateText()
        {
            _text.Clear();
            var face = _face.DominantFace;
            _text.Append("Face: ").Append(FaceName(face)).Append(' ').Append(Percent(_face.GetMix(face))).Append("% | World: ");
            if (_world != null && _pov != null && _pov.Mode == PovMode.Patient)
            {
                var shown = _world.ShownEmotion;
                _text.Append(EmotionName(shown)).Append(' ').Append(Percent(shown.HasValue ? _world.Weight : 0f)).Append('%');
            }
            else
            {
                // The nurse view does not show the room's reaction: show what the patient view would show.
                var strength = PatientPovMath.DominantStrength(_face.Holder, _face.FullStrengthAt, out var dominant);
                _text.Append(EmotionName(dominant)).Append(' ').Append(Percent(strength)).Append("% (in patient view)");
            }
            _label.SetText(_text);
        }

        private static int Percent(float value) => Mathf.RoundToInt(value * 100f);

        private static string FaceName(Face.FaceExpression expression)
        {
            switch (expression)
            {
                case Face.FaceExpression.Happy: return "Happy";
                case Face.FaceExpression.Sad: return "Sad";
                case Face.FaceExpression.Angry: return "Angry";
                case Face.FaceExpression.Worried: return "Worried";
                case Face.FaceExpression.Surprised: return "Surprised";
                case Face.FaceExpression.Pain: return "Pain";
                case Face.FaceExpression.Crying: return "Crying";
                default: return "Neutral";
            }
        }

        private static string EmotionName(Emotion? emotion)
        {
            if (!emotion.HasValue)
                return "Neutral";
            switch (emotion.Value)
            {
                case Emotion.Joy: return "Joy";
                case Emotion.Anger: return "Anger";
                case Emotion.Fear: return "Fear";
                default: return "Sadness";
            }
        }
    }
}
