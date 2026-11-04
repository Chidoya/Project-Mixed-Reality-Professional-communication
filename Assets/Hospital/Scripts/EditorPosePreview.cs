using System.Linq;
using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Toont in edit mode de echte houding van een personage in plaats van de T-pose: zet de clip van de state uit
    /// CharacterPose (Laying, Sitting, Idle, ...) op het startmoment. De root blijft waar hij staat. In Play mode doet dit
    /// niets: dan speelt de Animator via CharacterPose. De prefab en de controller in Assets/Character veranderen niet.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Animator), typeof(CharacterPose))]
    public class EditorPosePreview : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnEnable() => Sample();

        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                    Sample();
            };
        }

        /// <summary>Zet het personage in de pose van zijn state (enkel in edit mode).</summary>
        public void Sample()
        {
            if (Application.isPlaying)
                return;
            var pose = GetComponent<CharacterPose>();
            var controller = GetComponent<Animator>().runtimeAnimatorController;
            var clip = controller == null || pose == null
                ? null
                : controller.animationClips.FirstOrDefault(c => c.name == pose.State);
            if (clip == null)
                return;
            var position = transform.position;
            var rotation = transform.rotation;
            clip.SampleAnimation(gameObject, clip.length * pose.StartOffset);
            transform.SetPositionAndRotation(position, rotation);
        }
#endif
    }
}
