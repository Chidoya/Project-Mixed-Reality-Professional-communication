using System;
using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Zet de wijzers van de Wall_Clock op de echte tijd van het toestel.
    /// De wijzers draaien rond hun eigen oorsprong (het midden van de klok). As en ruststand (12 uur) worden
    /// door ProCommVR > Hospital > Setup Props ingevuld, gemeten op het geïmporteerde model.
    /// </summary>
    public class WallClock : MonoBehaviour
    {
        [SerializeField] private Transform _hourHand;
        [SerializeField] private Transform _minuteHand;
        [SerializeField] private Transform _secondHand;
        [Tooltip("As (in de ruimte van de klok) waarrond een positieve hoek met de klok mee draait, gezien van voren.")]
        [SerializeField] private Vector3 _clockwiseAxis = Vector3.forward;
        [Tooltip("Lokale rotatie van een wijzer die naar 12 uur wijst.")]
        [SerializeField] private Quaternion _restRotation = Quaternion.identity;
        [SerializeField] private bool _smoothSeconds;

        private void Update()
        {
            SetTime(DateTime.Now);
        }

        public void SetTime(DateTime time)
        {
            var seconds = time.Second + (_smoothSeconds ? time.Millisecond / 1000f : 0f);
            var minutes = time.Minute + seconds / 60f;
            var hours = time.Hour % 12 + minutes / 60f;
            Rotate(_hourHand, hours * 30f);
            Rotate(_minuteHand, minutes * 6f);
            Rotate(_secondHand, seconds * 6f);
        }

        public void Configure(Transform hour, Transform minute, Transform second, Vector3 clockwiseAxis, Quaternion restRotation)
        {
            _hourHand = hour;
            _minuteHand = minute;
            _secondHand = second;
            _clockwiseAxis = clockwiseAxis;
            _restRotation = restRotation;
        }

        private void Rotate(Transform hand, float degrees)
        {
            if (hand != null)
                hand.localRotation = Quaternion.AngleAxis(degrees, _clockwiseAxis) * _restRotation;
        }
    }
}
