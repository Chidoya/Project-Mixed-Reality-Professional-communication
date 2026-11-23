using System;
using ProCommVR.Emotions;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// One turn of the conversation as the debrief needs it: what the student said (text and own voice), what the
    /// patient answered (text and voice), his emotion and trust after the turn, and where the player stood.
    /// Kept in memory only, for this session (see <see cref="ConversationRecorder"/>).
    /// </summary>
    [Serializable]
    public class RecordedTurn
    {
        [Tooltip("Turn number of the AI server (1, 2, ...), the same as AiDebrief.key_moments[].turn_index.")]
        public int Index;
        [TextArea] public string StudentText;
        [NonSerialized] public AudioClip StudentClip;
        [TextArea] public string PatientText;
        [NonSerialized] public AudioClip PatientClip;
        [Tooltip("The emotion label of the AI server (Sad, Angry, Worried, Happy...), for the face.")]
        public string AiEmotion;
        [Tooltip("Our emotion after the turn (the EmotionHolder's target).")]
        public EmotionState Emotion;
        public float Trust;
        public float Anger;
        [Tooltip("Change of the anger (tension) in this turn, to keep the most important turns when there are too many.")]
        public float AngerChange;
        [Tooltip("Head position relative to the patient (his local space) when the student spoke.")]
        public Vector3 HeadLocalPosition;
        [Tooltip("Yaw of the head relative to the patient (degrees).")]
        public float HeadLocalYaw;
        public bool HasPose;
        [Tooltip("Seconds since the start of the conversation.")]
        public float Time;

        /// <summary>The dominant one of our four emotions after the turn, or null (neutral).</summary>
        public Emotion? Dominant => Emotion.Dominant;

        public bool HasStudentClip => StudentClip != null;
        public bool HasPatientClip => PatientClip != null;

        /// <summary>Destroys the clips (they were made for this session only).</summary>
        public void Release()
        {
            if (StudentClip != null)
                UnityEngine.Object.Destroy(StudentClip);
            if (PatientClip != null)
                UnityEngine.Object.Destroy(PatientClip);
            StudentClip = PatientClip = null;
        }
    }
}
