using System.Collections;
using TMPro;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// A short closing card on the whiteboard: "Saturday, 3 pm", with a picture of what happened later, chosen from the
    /// existing results only (<see cref="DebriefData.Outcome"/>): Good (a video call from the party, the granddaughter's
    /// voice), Mixed (the birthday card still closed, no voice), Critical (an empty chair at the party, the doctor says one
    /// calm line). At most <see cref="DebriefScript.ConsequenceSeconds"/>, B skips. Texts and pictures are data.
    /// </summary>
    public class ConsequenceCard : MonoBehaviour
    {
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Material _pictureMaterial;
        [SerializeField] private Vector2 _size = new(1.1f, 0.95f);

        private GameObject _card;
        private TextMeshPro _caption;
        private AudioSource _voice;

        public bool IsShown => _card != null;
        public DebriefOutcome ShownOutcome { get; private set; }
        public int Shown { get; private set; }
        public string Caption => _caption != null ? _caption.text : "";

        /// <summary>Shows the card for the outcome on the board (position = middle of the board front, forward = out).</summary>
        public IEnumerator Show(DebriefOutcome outcome, DebriefScript script, Transform board, DoctorSpeaker doctor,
            TMP_FontAsset font)
        {
            Hide();
            ShownOutcome = outcome;
            Shown++;
            _card = new GameObject($"Consequence Card ({outcome})");
            _card.transform.SetParent(transform, false);
            _card.transform.SetPositionAndRotation(board.position + board.forward * 0.03f,
                Quaternion.LookRotation(-board.forward, Vector3.up));

            var frame = Quad(_card.transform, "Frame", new Vector3(0f, 0f, 0.002f), _size + new Vector2(0.06f, 0.2f), null,
                new Color(0.96f, 0.95f, 0.92f));
            frame.name = "Frame";
            var texture = outcome switch
            {
                DebriefOutcome.Good => script.GoodImage,
                DebriefOutcome.Critical => script.CriticalImage,
                _ => script.MixedImage,
            };
            Quad(_card.transform, "Picture", new Vector3(0f, -0.04f, 0f), new Vector2(_size.x, _size.y - 0.1f), texture, Color.white);

            var title = Label(_card.transform, script.ConsequenceTitle, 0.75f, new Vector3(0f, _size.y / 2f + 0.02f, -0.004f), font);
            title.fontStyle = FontStyles.Bold;
            _caption = Label(_card.transform, outcome switch
            {
                DebriefOutcome.Good => script.GoodCaption,
                DebriefOutcome.Critical => script.CriticalCaption,
                _ => script.MixedCaption,
            }, 0.45f, new Vector3(0f, -_size.y / 2f - 0.02f, -0.004f), font);

            var elapsed = 0f;
            var max = script.ConsequenceSeconds;
            if (outcome == DebriefOutcome.Good && script.GranddaughterClip != null)
            {
                _voice = _card.AddComponent<AudioSource>();
                _voice.spatialBlend = 0.5f;
                _voice.minDistance = 4f;
                _voice.clip = script.GranddaughterClip;
                _voice.Play();
            }
            if (outcome == DebriefOutcome.Critical && doctor != null)
            {
                var routine = doctor.Say(script.Line("consequence_critical"));
                while (routine.MoveNext() && elapsed < max)
                {
                    if (DebriefInput.B)
                        break;
                    elapsed += Time.deltaTime;
                    yield return routine.Current;
                }
                doctor.Stop();
            }
            var minimum = outcome == DebriefOutcome.Mixed ? 4f : 5f;
            while (elapsed < Mathf.Min(max, Mathf.Max(minimum, _voice != null ? _voice.clip.length + 1.5f : 0f)))
            {
                if (DebriefInput.B)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            Hide();
        }

        public void Hide()
        {
            if (_card != null)
                Destroy(_card);
            _card = null;
            _caption = null;
            _voice = null;
        }

        private Transform Quad(Transform parent, string name, Vector3 local, Vector2 size, Texture texture, Color color)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = local;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (_pictureMaterial != null)
                renderer.sharedMaterial = _pictureMaterial;
            var block = new MaterialPropertyBlock();
            if (texture != null)
                block.SetTexture(BaseMap, texture);
            block.SetColor(BaseColor, color);
            renderer.SetPropertyBlock(block);
            return quad.transform;
        }

        private static TextMeshPro Label(Transform parent, string text, float size, Vector3 local, TMP_FontAsset font)
        {
            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = local;
            if (font != null)
                label.font = font;
            label.fontSize = size;
            label.rectTransform.sizeDelta = new Vector2(1.2f, 0.12f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = new Color(0.1f, 0.1f, 0.14f);
            label.text = text;
            return label;
        }
    }
}
