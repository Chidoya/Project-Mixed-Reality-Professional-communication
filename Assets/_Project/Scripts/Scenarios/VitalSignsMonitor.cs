using TMPro;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// A patient monitor in the world whose vital signs fluctuate (heart rate, oxygen saturation, blood pressure),
    /// e.g. next to the bed of a patient whose condition is deteriorating (Scenario 4). Only a display: the values
    /// are random around the baselines and mean nothing medically.
    /// </summary>
    public class VitalSignsMonitor : MonoBehaviour
    {
        [SerializeField] private TMP_Text _heartRate;
        [SerializeField] private TMP_Text _saturation;
        [SerializeField] private TMP_Text _bloodPressure;

        [Tooltip("Heart rate (beats per minute) around which the value fluctuates.")]
        [SerializeField] private float _heartRateBase = 112f;
        [SerializeField, Min(0f)] private float _heartRateSpread = 9f;

        [Tooltip("Oxygen saturation (%) around which the value fluctuates.")]
        [SerializeField] private float _saturationBase = 90f;
        [SerializeField, Min(0f)] private float _saturationSpread = 2f;

        [SerializeField] private Vector2 _bloodPressureBase = new Vector2(96f, 58f);

        [Tooltip("Seconds between two readings.")]
        [SerializeField, Min(0.1f)] private float _interval = 1.2f;

        private float _next;

        /// <summary>
        /// The heart rate shown now.
        /// </summary>
        public int HeartRate { get; private set; }

        private void Update()
        {
            if (Time.time < _next)
                return;
            _next = Time.time + _interval;
            HeartRate = Mathf.RoundToInt(_heartRateBase + Random.Range(-_heartRateSpread, _heartRateSpread));
            var saturation = Mathf.RoundToInt(_saturationBase + Random.Range(-_saturationSpread, _saturationSpread));
            var systolic = Mathf.RoundToInt(_bloodPressureBase.x + Random.Range(-6f, 6f));
            var diastolic = Mathf.RoundToInt(_bloodPressureBase.y + Random.Range(-4f, 4f));
            if (_heartRate != null)
                _heartRate.text = $"<size=45%>HR</size> {HeartRate}";
            if (_saturation != null)
                _saturation.text = $"<size=45%>SpO2</size> {saturation}%";
            if (_bloodPressure != null)
                _bloodPressure.text = $"<size=45%>BP</size> {systolic}/{diastolic}";
        }
    }
}
