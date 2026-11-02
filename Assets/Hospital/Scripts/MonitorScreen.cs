using System.Linq;
using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Hartslaglijn op het Screen-vlak van de Vitals_Monitor (LineRenderer, lopende ECG-lijn).
    /// SetAlarm(true): lijn rood, sneller ritme, scherm knippert rood en een piep bij elke knipper.
    /// Werkt op elk plat "Screen"-vlak; de assen worden uit de mesh gehaald, los van de rotatie van de prop.
    /// </summary>
    public class MonitorScreen : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer _screen;
        [Tooltip("Materiaal met vertexkleur, bv. Universal Render Pipeline/Particles/Unlit (Assets/Hospital/Materials/MonitorLine.mat).")]
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private int _points = 80;
        [SerializeField] private float _lineWidth = 0.004f;
        [SerializeField] private float _bpm = 72f;
        [SerializeField] private float _alarmBpm = 130f;
        [Tooltip("Hoeveel seconden de lijn breed is.")]
        [SerializeField] private float _sweepSeconds = 3f;
        [SerializeField] private Color _normalColor = new Color(0.2f, 1f, 0.35f);
        [SerializeField] private Color _alarmColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private Color _alarmScreenColor = new Color(0.45f, 0.02f, 0.02f);
        [SerializeField] private float _blinkInterval = 0.5f;
        [SerializeField] private float _beepFrequency = 960f;

        private LineRenderer _line;
        private Vector3 _origin;
        private Vector3 _right;
        private Vector3 _up;
        private float _width;
        private float _height;
        private float _beats;
        private bool _alarm;
        private float _blinkTime;
        private bool _blinkOn;
        private MaterialPropertyBlock _block;
        private AudioSource _audio;
        private AudioClip _beep;

        public bool Alarm => _alarm;
        public LineRenderer Line => _line;

        private void Awake()
        {
            Init();
        }

        public void SetAlarm(bool on)
        {
            Init();
            _alarm = on;
            _blinkTime = 0f;
            _blinkOn = false;
            _line.startColor = _line.endColor = on ? _alarmColor : _normalColor;
            SetScreenColor(null);
        }

        private void Update()
        {
            if (_line == null)
                return;
            _beats += Time.deltaTime * (_alarm ? _alarmBpm : _bpm) / 60f;
            DrawTrace();

            if (!_alarm)
                return;
            _blinkTime += Time.deltaTime;
            if (_blinkTime < _blinkInterval / 2f)
                return;
            _blinkTime = 0f;
            _blinkOn = !_blinkOn;
            SetScreenColor(_blinkOn ? _alarmScreenColor : (Color?)null);
            if (_blinkOn && _audio != null)
                _audio.PlayOneShot(_beep);
        }

        private void Init()
        {
            if (_line != null)
                return;
            if (_screen == null)
                _screen = GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Screen");
            var filter = _screen != null ? _screen.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogWarning($"[MonitorScreen] {name}: geen Screen-vlak gevonden.", this);
                return;
            }
            MeasureScreen(filter.sharedMesh);

            var go = new GameObject("HeartLine");
            go.transform.SetParent(_screen.transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.positionCount = _points;
            _line.widthMultiplier = _lineWidth;
            _line.sharedMaterial = _lineMaterial;
            _line.startColor = _line.endColor = _normalColor;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.numCapVertices = 0;
            DrawTrace();

            _block = new MaterialPropertyBlock();
            _audio = GetComponent<AudioSource>();
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 1f;
                _audio.maxDistance = 8f;
            }
            _beep = CreateBeep(_beepFrequency, 0.15f);
        }

        /// <summary>Vlak, normaal en afmetingen van het scherm in zijn eigen ruimte; "omhoog" is de wereld-y op het vlak.</summary>
        private void MeasureScreen(Mesh mesh)
        {
            var normal = mesh.normals.Length > 0 ? mesh.normals[0].normalized : Vector3.back;
            _up = Vector3.ProjectOnPlane(_screen.transform.InverseTransformDirection(Vector3.up), normal).normalized;
            _right = Vector3.Cross(normal, _up).normalized;
            var vertices = mesh.vertices;
            _width = vertices.Max(v => Vector3.Dot(v, _right)) - vertices.Min(v => Vector3.Dot(v, _right));
            _height = vertices.Max(v => Vector3.Dot(v, _up)) - vertices.Min(v => Vector3.Dot(v, _up));
            _origin = mesh.bounds.center + normal * 0.002f;
        }

        private void DrawTrace()
        {
            var beatsVisible = _sweepSeconds * (_alarm ? _alarmBpm : _bpm) / 60f;
            for (var i = 0; i < _points; i++)
            {
                var u = i / (float)(_points - 1);
                var phase = _beats - (1f - u) * beatsVisible;
                var y = Ecg(phase - Mathf.Floor(phase));
                _line.SetPosition(i, _origin + _right * ((u - 0.5f) * _width * 0.9f) + _up * ((y * 0.55f - 0.15f) * _height));
            }
        }

        /// <summary>Eén hartslag (0-1): P-golf, QRS-piek, T-golf. Uitslag ongeveer -0,25 tot 1.</summary>
        private static float Ecg(float p)
        {
            float Bump(float center, float width, float amplitude) => amplitude * Mathf.Exp(-Mathf.Pow((p - center) / width, 2f));
            return Bump(0.12f, 0.035f, 0.12f) + Bump(0.28f, 0.01f, -0.12f) + Bump(0.30f, 0.012f, 1f) +
                   Bump(0.325f, 0.012f, -0.25f) + Bump(0.55f, 0.06f, 0.28f);
        }

        private void SetScreenColor(Color? color)
        {
            if (_screen == null)
                return;
            if (color == null)
            {
                _screen.SetPropertyBlock(null);
                return;
            }
            _screen.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color.Value);
            _screen.SetPropertyBlock(_block);
        }

        private static AudioClip CreateBeep(float frequency, float seconds)
        {
            const int rate = 44100;
            var samples = new float[Mathf.RoundToInt(rate * seconds)];
            for (var i = 0; i < samples.Length; i++)
            {
                var fade = Mathf.Min(1f, Mathf.Min(i, samples.Length - i) / (rate * 0.01f));
                samples[i] = 0.5f * fade * Mathf.Sin(2f * Mathf.PI * frequency * i / rate);
            }
            var clip = AudioClip.Create("MonitorBeep", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
