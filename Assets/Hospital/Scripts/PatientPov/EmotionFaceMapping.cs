using ProCommVR.Hospital.Face;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// How the patient's emotions (EmotionHolder) show on his face and in his gaze. Tune it in
    /// Assets/Hospital/Data/PatientPov/EmotionFaceMapping.asset, no code needed.
    /// </summary>
    [CreateAssetMenu(menuName = "ProCommVR/Patient POV/Emotion Face Mapping", fileName = "EmotionFaceMapping")]
    public class EmotionFaceMapping : ScriptableObject
    {
        [Header("Expressions")]
        public FaceExpression Joy = FaceExpression.Happy;
        public FaceExpression Anger = FaceExpression.Angry;
        public FaceExpression Fear = FaceExpression.Worried;
        public FaceExpression Sadness = FaceExpression.Sad;

        [Tooltip("Above this Sadness value, Sad blends into Crying (tears).")]
        [Range(0f, 1f)] public float CryingFrom = 0.75f;

        [Tooltip("Sadness value at which the face is fully Crying.")]
        [Range(0f, 1f)] public float CryingFull = 0.85f;

        [Tooltip("Overall strength of the expressions (1 = the presets as they are).")]
        [Range(0f, 1.5f)] public float Intensity = 1f;

        [Tooltip("Seconds for the face to follow a change (the holder already blends; this only smooths).")]
        [Min(0.05f)] public float BlendTime = 0.5f;

        [Header("Gaze")]
        [Tooltip("Below this strength the patient simply looks at the nurse (as without emotion).")]
        [Range(0f, 1f)] public float GazeFrom = 0.25f;

        [Tooltip("Sadness: how far below the window's middle he looks (degrees down).")]
        [Range(0f, 45f)] public float SadLookDownDegrees = 20f;

        [Tooltip("Fear: seconds between two glances at the door (random in this range).")]
        public Vector2 FearGlanceInterval = new Vector2(1.2f, 2.6f);

        [Tooltip("Fear: how long one glance at the door lasts.")]
        [Min(0.1f)] public float FearGlanceSeconds = 0.6f;

        [Tooltip("Distance of the gaze point from the head (HeadLookAt only looks at targets within its range of 3 m).")]
        [Min(0.3f)] public float GazePointDistance = 1.2f;

        public FaceExpression ExpressionFor(ProCommVR.Emotions.Emotion emotion)
        {
            switch (emotion)
            {
                case ProCommVR.Emotions.Emotion.Joy: return Joy;
                case ProCommVR.Emotions.Emotion.Anger: return Anger;
                case ProCommVR.Emotions.Emotion.Fear: return Fear;
                default: return Sadness;
            }
        }
    }
}
