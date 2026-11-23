using System;
using UnityEngine;

namespace ProCommVR.Hospital.Face
{
    /// <summary>
    /// Stuurt het gezicht van een personage: mengt vloeiend naar een uitdrukking (blendshapes uit
    /// blender/scripts/07_gezicht.py), knippert vanzelf, laat de wenkbrauwen licht bewegen en
    /// combineert praten (TalkLevel) met de emotie.
    /// </summary>
    [DisallowMultipleComponent]
    public class FaceController : MonoBehaviour
    {
        public static readonly string[] ShapeNames =
        {
            "Blink", "EyesWide", "EyesNarrow", "LookDown", "BrowsAngry", "BrowsSad", "BrowsRaise",
            "Smile", "Frown", "MouthOpen", "MouthTight", "Tears", "EyesSqueeze", "Grimace",
        };

        private const int IBlink = 0, IEyesWide = 1, IEyesNarrow = 2, ILookDown = 3, IBrowsSad = 5, IBrowsRaise = 6,
            IMouthOpen = 9, ITears = 11, IEyesSqueeze = 12;

        [SerializeField] private FacePresets _presets;
        [SerializeField] private FaceExpression _startExpression = FaceExpression.Neutral;
        [SerializeField, Range(0f, 1f)] private float _intensity = 1f;
        [Tooltip("Knippert vanzelf (elke 2,5 tot 5,5 s).")]
        [SerializeField] private bool _autoBlink = true;
        [Tooltip("Wenkbrauwen bewegen licht, zodat het gezicht nooit volledig stilstaat.")]
        [SerializeField] private bool _microMotion = true;
        [Tooltip("Leeg = zoekt zelf de SkinnedMeshRenderer met de blendshape Blink.")]
        [SerializeField] private SkinnedMeshRenderer _renderer;

        private int[] _index;
        private readonly float[] _current = new float[ShapeNames.Length];
        private readonly float[] _target = new float[ShapeNames.Length];
        private readonly float[] _applied = new float[ShapeNames.Length];
        private float _blendTime = 0.3f;
        private float _talkLevel;
        private float _lastWrittenMouth = -1f;

        // Knipperen
        private float _nextBlink;
        private float _blinkTime = -1f;   // tijd sinds de start van de huidige knipper, <0 = niet bezig
        private bool _doubleQueued;
        private const float CloseTime = 0.07f, ClosedHold = 0.03f, OpenTime = 0.12f;

        private float _seed;
        private float _clock;
        private bool _initialized;

        // Mix van uitdrukkingen (SetMix): gewicht 0..1 per FaceExpression, plus per uitdrukking de shape-indexen en
        // gewichten uit de presets (eenmalig opgezocht, zodat SetMix elke frame mag zonder te zoeken of te alloceren).
        private static readonly int ExpressionCount = Enum.GetValues(typeof(FaceExpression)).Length;
        private readonly float[] _mix = new float[ExpressionCount];
        private bool _mixActive;
        private int[][] _presetShape;
        private float[][] _presetWeight;

        public FaceExpression Expression { get; private set; }

        /// <summary>True zolang een mix (SetMix) het gezicht bepaalt.</summary>
        public bool HasMix => _mixActive;

        /// <summary>De uitdrukking met het grootste gewicht: bij een mix de sterkste (Neutral onder 0,1), anders Expression.</summary>
        public FaceExpression DominantExpression => Expression;

        /// <summary>Gewicht (0..1) van een uitdrukking in de mix; zonder mix 1 voor Expression.</summary>
        public float GetMixWeight(FaceExpression expression) =>
            _mixActive ? _mix[(int)expression] : (expression == Expression ? _intensity : 0f);

        /// <summary>Tijdstip (Time.time) van de laatste SetExpression van buitenaf, voor FaceFromAnimator.</summary>
        public float LastExternalSetTime { get; private set; } = float.NegativeInfinity;

        public SkinnedMeshRenderer Renderer => _renderer;
        public bool IsReady => _renderer != null;

        public event Action<FaceExpression> ExpressionChanged;

        /// <summary>Sterkte van de uitdrukking (0..1).</summary>
        public float Intensity
        {
            get => _intensity;
            set
            {
                _intensity = Mathf.Clamp01(value);
                RebuildTargets();
            }
        }

        /// <summary>Hoe ver de mond open staat door praten (0..1). MouthOpen = max(emotie, praten).</summary>
        public float TalkLevel
        {
            get => _talkLevel;
            set => _talkLevel = Mathf.Clamp01(value);
        }

        public bool AutoBlink
        {
            get => _autoBlink;
            set => _autoBlink = value;
        }

        public bool MicroMotion
        {
            get => _microMotion;
            set => _microMotion = value;
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;
            _seed = UnityEngine.Random.Range(0f, 100f);
            if (_renderer == null)
            {
                foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.sharedMesh != null && smr.sharedMesh.GetBlendShapeIndex("Blink") >= 0)
                    {
                        _renderer = smr;
                        break;
                    }
                }
            }
            if (_renderer == null)
            {
                Debug.LogWarning($"[FaceController] {name}: geen SkinnedMeshRenderer met blendshape 'Blink' gevonden.", this);
                enabled = false;
                return;
            }

            var mesh = _renderer.sharedMesh;
            _index = new int[ShapeNames.Length];
            string missing = null;
            for (var i = 0; i < ShapeNames.Length; i++)
            {
                _index[i] = mesh.GetBlendShapeIndex(ShapeNames[i]);
                if (_index[i] < 0)
                    missing = missing == null ? ShapeNames[i] : missing + ", " + ShapeNames[i];
            }
            if (missing != null)
                Debug.LogWarning($"[FaceController] {name}: blendshapes ontbreken ({missing}), die worden overgeslagen.", this);

            Apply(_startExpression, _intensity, 0f);
            Array.Copy(_target, _current, _current.Length);
            ScheduleBlink();
        }

        // --- Publieke API ---------------------------------------------------------

        /// <summary>Zet een uitdrukking. Wint 5 s lang van FaceFromAnimator (of tot de Animator-state wijzigt).</summary>
        public void SetExpression(FaceExpression expression, float intensity = 1f, float blendTime = 0.3f)
        {
            Initialize();
            LastExternalSetTime = Time.time;
            _mixActive = false;
            Apply(expression, intensity, blendTime);
        }

        /// <summary>
        /// Mengt meerdere uitdrukkingen: weights heeft één gewicht (0..1) per FaceExpression (index = (int)expression).
        /// Doelgewicht per blendshape = som over de uitdrukkingen van presetgewicht maal mixgewicht, geclampt op 0..100.
        /// Mag elke frame (geen allocaties); knipperen, micro-beweging en TalkLevel blijven werken.
        /// Expression wordt de sterkste uitdrukking van de mix (Neutral als alles onder 0,1 ligt).
        /// </summary>
        public void SetMix(float[] weights, float blendTime = 0.3f)
        {
            Initialize();
            if (weights == null)
                return;
            _mixActive = true;
            var best = FaceExpression.Neutral;
            var bestWeight = 0.1f;
            for (var i = 0; i < _mix.Length; i++)
            {
                var w = i < weights.Length ? Mathf.Clamp01(weights[i]) : 0f;
                _mix[i] = w;
                if (w > bestWeight)
                {
                    bestWeight = w;
                    best = (FaceExpression)i;
                }
            }
            _blendTime = Mathf.Max(0.01f, blendTime);
            var changed = best != Expression;
            Expression = best;
            RebuildTargets();
            if (blendTime <= 0f)
                Array.Copy(_target, _current, _current.Length);
            if (changed)
                ExpressionChanged?.Invoke(best);
        }

        /// <summary>Stopt de mix en vloeit terug naar Neutral (of laat FaceFromAnimator weer toe).</summary>
        public void ClearMix(float blendTime = 0.3f)
        {
            if (!_mixActive)
                return;
            _mixActive = false;
            Array.Clear(_mix, 0, _mix.Length);
            Apply(FaceExpression.Neutral, _intensity, blendTime);
        }

        /// <summary>Voor UnityEvents: de naam van een FaceExpression, bv. "Angry".</summary>
        public void SetExpression(string expressionName)
        {
            if (Enum.TryParse(expressionName, true, out FaceExpression e))
                SetExpression(e);
            else
                Debug.LogWarning($"[FaceController] Onbekende uitdrukking '{expressionName}'.", this);
        }

        /// <summary>Zet de uitdrukking zonder te tellen als 'van buitenaf' (gebruikt door FaceFromAnimator).</summary>
        public void ApplyAutomatic(FaceExpression expression, float blendTime = 0.4f)
        {
            Initialize();
            if (_mixActive)
                return; // een mix (bv. EmotionFaceDriver) wint van de Animator-state
            Apply(expression, _intensity, blendTime);
        }

        /// <summary>Laat het personage nu knipperen.</summary>
        public void Blink() => _blinkTime = 0f;

        /// <summary>Huidig gewicht (0..100) zoals het op de mesh staat.</summary>
        public float GetWeight(string shape)
        {
            var i = Array.IndexOf(ShapeNames, shape);
            return i < 0 ? 0f : _applied[i];
        }

        /// <summary>Zet alle gewichten meteen (zonder mengen), bv. voor screenshots.</summary>
        public void Snap()
        {
            Array.Copy(_target, _current, _current.Length);
            Step(0f);
        }

        // --- Intern ---------------------------------------------------------------

        private void Apply(FaceExpression expression, float intensity, float blendTime)
        {
            var changed = expression != Expression;
            Expression = expression;
            _intensity = Mathf.Clamp01(intensity);
            _blendTime = Mathf.Max(0.01f, blendTime);
            RebuildTargets();
            if (blendTime <= 0f)
                Array.Copy(_target, _current, _current.Length);
            if (changed)
                ExpressionChanged?.Invoke(expression);
        }

        private void RebuildTargets()
        {
            Array.Clear(_target, 0, _target.Length);
            if (_mixActive)
            {
                BuildPresetTable();
                for (var e = 0; e < _mix.Length; e++)
                {
                    var m = _mix[e];
                    if (m <= 0f)
                        continue;
                    var shapes = _presetShape[e];
                    var values = _presetWeight[e];
                    for (var s = 0; s < shapes.Length; s++)
                        _target[shapes[s]] += values[s] * m;
                }
                for (var i = 0; i < _target.Length; i++)
                    _target[i] = Mathf.Clamp(_target[i], 0f, 100f);
                return;
            }
            var weights = _presets != null
                ? _presets.Get(Expression)
                : DefaultTable[(int)Expression];
            foreach (var w in weights)
            {
                var i = Array.IndexOf(ShapeNames, w.Shape);
                if (i >= 0)
                    _target[i] = Mathf.Clamp(w.Weight, 0f, 100f) * _intensity;
            }
        }

        /// <summary>Eenmalig: per uitdrukking de shape-indexen en gewichten (0..100) uit de presets.</summary>
        private void BuildPresetTable()
        {
            if (_presetShape != null)
                return;
            _presetShape = new int[ExpressionCount][];
            _presetWeight = new float[ExpressionCount][];
            var shapes = new System.Collections.Generic.List<int>();
            var values = new System.Collections.Generic.List<float>();
            for (var e = 0; e < ExpressionCount; e++)
            {
                shapes.Clear();
                values.Clear();
                var weights = _presets != null ? _presets.Get((FaceExpression)e) : DefaultTable[e];
                foreach (var w in weights)
                {
                    var i = Array.IndexOf(ShapeNames, w.Shape);
                    if (i < 0)
                        continue;
                    shapes.Add(i);
                    values.Add(Mathf.Clamp(w.Weight, 0f, 100f));
                }
                _presetShape[e] = shapes.ToArray();
                _presetWeight[e] = values.ToArray();
            }
        }

        private static readonly FacePresets.ShapeWeight[][] DefaultTable = BuildDefaultTable();

        private static FacePresets.ShapeWeight[][] BuildDefaultTable()
        {
            var presets = FacePresets.DefaultPresets();
            var table = new FacePresets.ShapeWeight[Enum.GetValues(typeof(FaceExpression)).Length][];
            for (var i = 0; i < table.Length; i++)
                table[i] = Array.Empty<FacePresets.ShapeWeight>();
            foreach (var p in presets)
                table[(int)p.Expression] = p.Weights.ToArray();
            return table;
        }

        private void ScheduleBlink()
        {
            var interval = UnityEngine.Random.Range(2.5f, 5.5f);
            if (Expression == FaceExpression.Angry)
                interval *= 1.8f; // staren
            _nextBlink = _clock + interval;
        }

        private float BlinkAmount(float dt)
        {
            if (_autoBlink && Expression != FaceExpression.Crying && Expression != FaceExpression.Pain && _blinkTime < 0f && _clock >= _nextBlink)
                _blinkTime = 0f;
            if (_blinkTime < 0f)
                return 0f;

            _blinkTime += dt;
            var t = _blinkTime;
            float amount;
            if (t < CloseTime)
                amount = t / CloseTime;
            else if (t < CloseTime + ClosedHold)
                amount = 1f;
            else if (t < CloseTime + ClosedHold + OpenTime)
                amount = 1f - (t - CloseTime - ClosedHold) / OpenTime;
            else
            {
                amount = 0f;
                _blinkTime = -1f;
                if (!_doubleQueued && UnityEngine.Random.value < 0.15f)
                {
                    _doubleQueued = true;
                    _nextBlink = _clock + 0.1f;
                }
                else
                {
                    _doubleQueued = false;
                    ScheduleBlink();
                }
            }
            return Mathf.SmoothStep(0f, 1f, amount);
        }

        private void LateUpdate() => Step(Time.deltaTime);

        /// <summary>Eén stap mengen en de gewichten op de mesh zetten.</summary>
        public void Step(float dt)
        {
            if (!_initialized)
                Initialize();
            if (_renderer == null)
                return;
            _clock += dt;

            // Iets anders (bv. MouthMover) schreef MouthOpen sinds de vorige frame: behandel dat als praten
            var talk = _talkLevel;
            if (_index[IMouthOpen] >= 0 && _lastWrittenMouth >= 0f)
            {
                var external = _renderer.GetBlendShapeWeight(_index[IMouthOpen]);
                if (Mathf.Abs(external - _lastWrittenMouth) > 0.5f)
                    talk = Mathf.Max(talk, external / 100f);
            }

            var k = dt <= 0f ? 1f : 1f - Mathf.Exp(-dt * 4.6f / _blendTime);
            for (var i = 0; i < _current.Length; i++)
            {
                var ki = k;
                if (i == ITears && dt > 0f) // tranen komen trager op en drogen trager op
                    ki = 1f - Mathf.Exp(-dt * 4.6f / (_blendTime * (_target[i] > _current[i] ? 5f : 3f)));
                _current[i] = Mathf.Lerp(_current[i], _target[i], ki);
            }
            Array.Copy(_current, _applied, _applied.Length);

            if (_microMotion)
            {
                var t = _clock * 0.35f;
                _applied[IBrowsRaise] += Mathf.PerlinNoise(t, _seed) * 6f;
                _applied[IBrowsSad] += Mathf.PerlinNoise(_seed, t * 0.8f) * 3f;
            }

            var blink = BlinkAmount(dt);
            if (blink > 0f)
            {
                _applied[IBlink] = Mathf.Max(_applied[IBlink], blink * 100f);
                var open = 1f - blink; // bij dicht oog geen extra oogvorm erbovenop
                _applied[IEyesWide] *= open;
                _applied[IEyesNarrow] *= open;
                _applied[ILookDown] *= open;
                _applied[IEyesSqueeze] *= open;
            }

            _applied[IMouthOpen] = Mathf.Max(_applied[IMouthOpen], talk * 100f);

            for (var i = 0; i < _applied.Length; i++)
            {
                _applied[i] = Mathf.Clamp(_applied[i], 0f, 100f);
                if (_index[i] >= 0)
                    _renderer.SetBlendShapeWeight(_index[i], _applied[i]);
            }
            _lastWrittenMouth = _applied[IMouthOpen];
        }
    }
}
