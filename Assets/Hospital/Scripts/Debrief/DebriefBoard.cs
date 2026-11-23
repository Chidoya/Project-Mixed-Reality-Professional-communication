using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// What ends up on the whiteboard and the table in the debrief: up to three sticky notes (yellow = to improve, green =
    /// well done, red = the critical error; short text, a better phrase in quotes) and the score as a small stamp on a
    /// patient file. Large enough to read from the other side of the table (about 5 m from the board).
    /// </summary>
    public class DebriefBoard : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Vector2 _noteSize = new(0.7f, 0.56f);
        [SerializeField, Min(0f)] private float _noteGap = 0.06f;
        [SerializeField] private Color _textColor = new(0.1f, 0.1f, 0.14f);

        private readonly List<GameObject> _notes = new();
        private readonly List<TextMeshPro> _noteTexts = new();
        private GameObject _stamp;
        private TextMeshPro _stampText;
        private MaterialPropertyBlock _block;

        public IReadOnlyList<GameObject> Notes => _notes;
        public IReadOnlyList<TextMeshPro> NoteTexts => _noteTexts;
        public GameObject Stamp => _stamp;
        public string StampText => _stampText != null ? _stampText.text : "";

        /// <summary>
        /// Puts note number <paramref name="slot"/> of <paramref name="count"/> on the board. <paramref name="board"/>:
        /// position = the middle of the board's front, forward = out of the board into the room.
        /// Returns the note (the doctor points at it).
        /// </summary>
        public Transform AddNote(Transform board, int slot, int count, string text, string betterPhrase, Color color,
            Material material, TMP_FontAsset font)
        {
            var note = new GameObject($"Sticky Note {slot + 1}");
            note.transform.SetParent(transform, false);
            var right = Vector3.Cross(Vector3.up, -board.forward); // left to right for someone facing the board
            var width = count * _noteSize.x + (count - 1) * _noteGap;
            var x = -width / 2f + _noteSize.x / 2f + slot * (_noteSize.x + _noteGap);
            var tilt = (slot % 2 == 0 ? -1.5f : 2f);
            note.transform.SetPositionAndRotation(board.position + right * x + board.forward * (0.006f + 0.002f * slot),
                Quaternion.LookRotation(-board.forward, Vector3.up) * Quaternion.Euler(0f, 0f, tilt));

            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(paper.GetComponent<Collider>());
            paper.name = "Paper";
            paper.transform.SetParent(note.transform, false);
            paper.transform.localScale = new Vector3(_noteSize.x, _noteSize.y, 1f);
            var renderer = paper.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Tint(renderer, color);

            var label = new GameObject("Text").AddComponent<TextMeshPro>();
            label.transform.SetParent(note.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.003f);
            if (font != null)
                label.font = font;
            label.rectTransform.sizeDelta = new Vector2(_noteSize.x * 0.88f, _noteSize.y * 0.86f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.3f;
            label.fontSizeMax = 0.95f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = _textColor;
            label.fontStyle = FontStyles.Bold;
            label.text = string.IsNullOrEmpty(betterPhrase) ? text : $"{text}\n<size=78%><i>\"{betterPhrase}\"</i></size>";
            _notes.Add(note);
            _noteTexts.Add(label);
            StartCoroutine(Pop(note.transform));
            return note.transform;
        }

        /// <summary>The score as a red stamp on a patient file lying on the table (<paramref name="spot"/>: on the table top).</summary>
        public void ShowStamp(Transform spot, int total, int max, Material material, TMP_FontAsset font)
        {
            HideStamp();
            _stamp = new GameObject("Patient File (score)");
            _stamp.transform.SetParent(transform, false);
            _stamp.transform.SetPositionAndRotation(spot.position, spot.rotation);
            var folder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(folder.GetComponent<Collider>());
            folder.transform.SetParent(_stamp.transform, false);
            folder.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            folder.transform.localScale = new Vector3(0.24f, 0.012f, 0.32f);
            folder.GetComponent<Renderer>().sharedMaterial = material;
            Tint(folder.GetComponent<Renderer>(), new Color(0.55f, 0.72f, 0.9f));
            var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(paper.GetComponent<Collider>());
            paper.transform.SetParent(_stamp.transform, false);
            paper.transform.localPosition = new Vector3(0f, 0.0135f, 0f);
            paper.transform.localScale = new Vector3(0.21f, 0.002f, 0.29f);
            paper.GetComponent<Renderer>().sharedMaterial = material;
            Tint(paper.GetComponent<Renderer>(), new Color(0.97f, 0.97f, 0.95f));

            var title = Text(_stamp.transform, "Mr. X  |  Room 1A\n<size=70%>Conversation</size>", 0.16f, _textColor, font);
            title.transform.localPosition = new Vector3(0f, 0.0152f, 0.08f);
            title.rectTransform.sizeDelta = new Vector2(0.19f, 0.07f);
            _stampText = Text(_stamp.transform, $"{total} / {max}", 0.42f, new Color(0.8f, 0.12f, 0.1f), font);
            _stampText.transform.localPosition = new Vector3(0f, 0.0152f, -0.04f);
            _stampText.transform.localRotation = Quaternion.Euler(90f, 0f, -12f);
            _stampText.fontStyle = FontStyles.Bold;
            StartCoroutine(Pop(_stamp.transform));
        }

        public void HideStamp()
        {
            if (_stamp != null)
                Destroy(_stamp);
            _stamp = null;
            _stampText = null;
        }

        public void Clear()
        {
            foreach (var note in _notes)
                if (note != null)
                    Destroy(note);
            _notes.Clear();
            _noteTexts.Clear();
            HideStamp();
        }

        private static TextMeshPro Text(Transform parent, string text, float size, Color color, TMP_FontAsset font)
        {
            var label = new GameObject("Text").AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lying on the paper, readable from the player's side
            if (font != null)
                label.font = font;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(0.2f, 0.1f);
            label.color = color;
            label.text = text;
            return label;
        }

        private void Tint(Renderer renderer, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, color);
            _block.SetColor(EmissionColor, color * 0.15f);
            renderer.SetPropertyBlock(_block);
        }

        private static System.Collections.IEnumerator Pop(Transform target)
        {
            var t = 0f;
            while (t < 1f && target != null)
            {
                t += Time.deltaTime / 0.25f;
                var s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                target.localScale = Vector3.one * (s < 1f ? s * 1.05f : 1f);
                yield return null;
            }
        }
    }
}
