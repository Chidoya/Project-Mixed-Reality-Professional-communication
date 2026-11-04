using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Markeert een deur (of lift) die in de training dicht blijft, met de reden die het spel aan de speler kan tonen.
    /// Het blokkeren zelf gebeurt met een gewone collider op hetzelfde object; dit component doet niets in Update.
    /// </summary>
    public class LockedDoor : MonoBehaviour
    {
        [SerializeField] private string _reason = "This door is closed.";

        public string Reason => _reason;

        public void SetReason(string reason) => _reason = reason;
    }
}
