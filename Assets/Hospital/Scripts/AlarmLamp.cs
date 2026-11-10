using System.Collections;
using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Rode alarmlamp (scenario 2, beeld 7): een kapje met een lens en een rood puntlicht. In rust uit (lens AlarmLens_Off,
    /// licht disabled); SetOn(true) zet de lens op AlarmLens_On en het licht aan, Blink laat hem knipperen.
    /// Geen koppeling met andere scripts: Jalil mag SetOn en Blink aanroepen, bv. vanuit zijn MoodController bij Mood.Alarm.
    /// Quest 3: het licht staat alleen aan tijdens het alarm, er zijn hoogstens 4 alarmlampen in de scene.
    /// </summary>
    public class AlarmLamp : MonoBehaviour
    {
        [SerializeField] private Renderer _lens;
        [SerializeField] private Light _light;
        [SerializeField] private Material _offMaterial;
        [SerializeField] private Material _onMaterial;

        private Coroutine _blink;

        public bool IsOn { get; private set; }

        /// <summary>Koppelt lens, licht en materialen (gebruikt door de builder).</summary>
        public void Configure(Renderer lens, Light pointLight, Material off, Material on)
        {
            _lens = lens;
            _light = pointLight;
            _offMaterial = off;
            _onMaterial = on;
            Apply(false);
        }

        /// <summary>Aan of uit (stopt knipperen).</summary>
        public void SetOn(bool on)
        {
            StopBlink();
            Apply(on);
        }

        /// <summary>Knippert met deze tussentijd in seconden (aan, uit, aan, ...). 0 of minder = gewoon aan. Enkel in Play mode.</summary>
        public void Blink(float interval)
        {
            StopBlink();
            if (interval <= 0f || !Application.isPlaying || !isActiveAndEnabled)
            {
                Apply(true);
                return;
            }
            _blink = StartCoroutine(BlinkRoutine(interval));
        }

        private IEnumerator BlinkRoutine(float interval)
        {
            var wait = new WaitForSeconds(interval);
            var on = true;
            while (true)
            {
                Apply(on);
                on = !on;
                yield return wait;
            }
        }

        private void StopBlink()
        {
            if (_blink != null)
                StopCoroutine(_blink);
            _blink = null;
        }

        private void OnDisable() => StopBlink();

        private void Apply(bool on)
        {
            IsOn = on;
            if (_lens != null)
                _lens.sharedMaterial = on ? _onMaterial : _offMaterial;
            if (_light != null)
                _light.enabled = on;
        }
    }
}
