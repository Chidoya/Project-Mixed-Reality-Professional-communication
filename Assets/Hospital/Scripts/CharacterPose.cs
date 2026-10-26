using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Speelt bij de start een state uit de Animator Controller van het personage (Idle, Talking, Sitting, Laying, ...).
    /// De controllers zelf (Assets/Character) blijven ongewijzigd; hun standaard-state is Idle.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterPose : MonoBehaviour
    {
        [SerializeField] private string _state = "Idle";
        [Tooltip("Startpunt in de animatie (0-1), zodat personages met dezelfde animatie niet synchroon bewegen.")]
        [SerializeField, Range(0f, 1f)] private float _startOffset;

        public string State => _state;
        public float StartOffset => _startOffset;

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            Play(_state, _startOffset);
        }

        /// <summary>Wisselt naar een andere state, bv. Sad na een slecht antwoord.</summary>
        public void Play(string state, float normalizedTime = 0f)
        {
            _state = state;
            _animator.Play(state, 0, normalizedTime);
        }

        public void Configure(string state, float startOffset)
        {
            _state = state;
            _startOffset = startOffset;
        }
    }
}
