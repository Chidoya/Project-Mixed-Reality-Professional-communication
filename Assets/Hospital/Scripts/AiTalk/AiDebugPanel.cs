using System.Text;
using TMPro;
using UnityEngine;

namespace ProCommVR.Hospital.AiTalk
{
    /// <summary>
    /// Klein world-space paneel naast het personage (alleen voor testen, als settings.showDebugPanel aan staat):
    /// status, transcriptie, labels, boosheid, vertrouwen, band, fase en de wachttijden per stap.
    /// Bouwt zichzelf op bij de start en draait mee naar de camera.
    /// </summary>
    public class AiDebugPanel : MonoBehaviour
    {
        [SerializeField] private AiNpc _npc;
        [Tooltip("Positie t.o.v. het personage; negatieve x = links van het personage, gezien vanaf de speler.")]
        [SerializeField] private Vector3 _offset = new(-0.72f, 1.42f, 0f);
        [SerializeField] private Vector2 _size = new(0.56f, 0.5f);

        private Transform _panel;
        private TextMeshPro _text;
        private string _status = "";
        private AiTurn _turn;

        private void Start()
        {
            if (_npc == null)
                _npc = GetComponentInParent<AiNpc>() ?? FindAnyObjectByType<AiNpc>();
            if (_npc == null || _npc.Settings == null || !_npc.Settings.showDebugPanel)
            {
                enabled = false;
                return;
            }
            Build();
            _npc.TurnApplied += OnTurn;
            _npc.StatusChanged += OnStatus;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_npc == null)
                return;
            _npc.TurnApplied -= OnTurn;
            _npc.StatusChanged -= OnStatus;
        }

        private void OnTurn(AiTurn turn)
        {
            _turn = turn;
            Refresh();
        }

        private void OnStatus(string status)
        {
            _status = status;
            Refresh();
        }

        private void LateUpdate()
        {
            if (_panel == null)
                return;
            var cam = Camera.main;
            var npc = _npc.transform;
            var side = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : npc.right;
            _panel.position = npc.position + side * _offset.x + Vector3.up * _offset.y;
            if (cam != null)
                _panel.rotation = Quaternion.LookRotation(_panel.position - cam.transform.position, Vector3.up);
        }

        private void Build()
        {
            _panel = new GameObject("AiDebugPanel_View").transform;
            _panel.SetParent(transform, false);

            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Background";
            Destroy(background.GetComponent<Collider>());
            background.transform.SetParent(_panel, false);
            background.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            background.transform.localScale = new Vector3(_size.x, _size.y, 1f);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { color = new Color(0.08f, 0.1f, 0.13f, 1f) };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.08f, 0.1f, 0.13f, 1f));
            background.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(_panel, false);
            _text = textGo.AddComponent<TextMeshPro>();
            _text.rectTransform.sizeDelta = _size - new Vector2(0.04f, 0.04f);
            _text.fontSize = 0.22f;
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.color = new Color(0.92f, 0.94f, 0.96f);
            _text.richText = true;
        }

        private void Refresh()
        {
            if (_text == null)
                return;
            var sb = new StringBuilder();
            sb.Append("<b>AI TALK</b>  <color=#8fd3ff>").Append(_status).AppendLine("</color>");
            var t = _turn;
            if (t == null)
            {
                sb.AppendLine("Hold SPACE (Quest: right trigger) and talk.");
                _text.text = sb.ToString();
                return;
            }
            if (!string.IsNullOrEmpty(t.transcript))
                sb.Append("<color=#c8c8c8>You:</color> ").AppendLine(t.transcript);
            sb.Append("<color=#c8c8c8>Sarah:</color> ").AppendLine(t.reply);
            if (t.localFallback)
            {
                sb.Append("<color=#ffb347>").Append(t.retry ? "not understood (retry)" : t.error).AppendLine("</color>");
            }
            else
            {
                var labels = t.labels != null && t.labels.Length > 0 ? string.Join(", ", t.labels) : "-";
                sb.Append("<color=#c8c8c8>Labels:</color> ").AppendLine(labels);
                if (!string.IsNullOrEmpty(t.evidence_quote))
                    sb.Append("<color=#c8c8c8>Quote:</color> \"").Append(t.evidence_quote).AppendLine("\"");
                var before = t.before;
                sb.Append("Anger ").Append(before != null ? before.anger + " > " : "").Append(t.anger)
                  .Append("   Trust ").Append(before != null ? before.trust + " > " : "").Append(t.trust).AppendLine();
                sb.Append("Band ").Append(t.band).Append("   Phase ").Append(t.phase);
                if (t.HasEnded)
                    sb.Append("   <color=#7dff9a>END: ").Append(t.ended).Append("</color>");
                sb.AppendLine();
                sb.Append(t.emotion).Append(" / ").Append(t.animation).Append(" / ").Append(t.pose).AppendLine();
            }
            var ms = t.timings_ms;
            if (ms != null)
                sb.Append("<size=80%>STT ").Append(ms.stt_ms).Append("  cls ").Append(ms.classify_ms)
                  .Append("  actor ").Append(ms.actor_ms).Append("  TTS ").Append(ms.tts_ms)
                  .Append("  server ").Append(ms.request_total_ms).Append("  total ").Append(t.roundTripMs).Append(" ms</size>");
            _text.text = sb.ToString();
        }
    }
}
