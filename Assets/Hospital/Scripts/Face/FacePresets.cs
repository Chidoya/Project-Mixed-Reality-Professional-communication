using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProCommVR.Hospital.Face
{
    /// <summary>
    /// Per uitdrukking een lijst blendshape-gewichten (0..100). De waarden komen uit
    /// blender/scripts/07_gezicht.py (EXPRESSIONS), zodat Blender-preview en Unity hetzelfde tonen.
    /// </summary>
    [CreateAssetMenu(menuName = "ProCommVR/Face Presets", fileName = "FacePresets")]
    public class FacePresets : ScriptableObject
    {
        [Serializable]
        public struct ShapeWeight
        {
            public string Shape;
            [Range(0f, 100f)] public float Weight;

            public ShapeWeight(string shape, float weight)
            {
                Shape = shape;
                Weight = weight;
            }
        }

        [Serializable]
        public class Preset
        {
            public FaceExpression Expression;
            public List<ShapeWeight> Weights = new();
        }

        [SerializeField] private List<Preset> _presets = new();

        public IReadOnlyList<Preset> Presets => _presets;

        public IReadOnlyList<ShapeWeight> Get(FaceExpression expression)
        {
            foreach (var p in _presets)
                if (p.Expression == expression)
                    return p.Weights;
            return Array.Empty<ShapeWeight>();
        }

        /// <summary>De standaardtabel (ook gebruikt als er geen asset gekoppeld is).</summary>
        public static List<Preset> DefaultPresets()
        {
            Preset P(FaceExpression e, params (string, float)[] w)
            {
                var p = new Preset { Expression = e };
                foreach (var (s, v) in w)
                    p.Weights.Add(new ShapeWeight(s, v));
                return p;
            }

            return new List<Preset>
            {
                P(FaceExpression.Neutral),
                P(FaceExpression.Happy, ("Smile", 100), ("BrowsRaise", 25), ("EyesNarrow", 15)),
                P(FaceExpression.Sad, ("BrowsSad", 100), ("Frown", 60), ("EyesNarrow", 25), ("LookDown", 80)),
                P(FaceExpression.Angry, ("BrowsAngry", 100), ("EyesNarrow", 60), ("MouthTight", 60), ("Frown", 45)),
                P(FaceExpression.Worried, ("BrowsSad", 70), ("BrowsRaise", 50), ("EyesWide", 40), ("MouthTight", 30)),
                P(FaceExpression.Surprised, ("BrowsRaise", 100), ("EyesWide", 80), ("MouthOpen", 55)),
                P(FaceExpression.Pain, ("BrowsSad", 100), ("BrowsAngry", 20), ("EyesNarrow", 90), ("Frown", 45), ("MouthOpen", 30)),
                P(FaceExpression.Crying, ("BrowsSad", 100), ("Blink", 60), ("Frown", 80), ("MouthOpen", 30), ("Tears", 100)),
            };
        }

        public void ResetToDefaults() => _presets = DefaultPresets();

        private void Reset() => ResetToDefaults();
    }
}
