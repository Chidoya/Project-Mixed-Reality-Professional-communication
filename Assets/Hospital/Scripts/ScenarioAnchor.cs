using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Markeert een leeg anker van een scenario (plek en richting voor een personage, de speler, een paneel, ...). Doet niets in
    /// het spel; in de Scene-view tekent het een bolletje en een pijl in de richting van forward, met de naam erbij. Het tekenen
    /// zit volledig in UNITY_EDITOR, dus in een build is dit een lege component. Jalil kan de ankers ook op deze component zoeken.
    /// </summary>
    public class ScenarioAnchor : MonoBehaviour
    {
        [SerializeField] private Color _color = new Color(1f, 0.55f, 0.1f);
        [SerializeField, Min(0.01f)] private float _size = 0.12f;

        public void Configure(Color color, float size)
        {
            _color = color;
            _size = size;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _color;
            var p = transform.position;
            Gizmos.DrawSphere(p, _size);
            var tip = p + transform.forward * (_size * 5f);
            Gizmos.DrawLine(p, tip);
            var side = transform.right * (_size * 1.2f);
            var back = -transform.forward * (_size * 1.5f);
            Gizmos.DrawLine(tip, tip + back + side);
            Gizmos.DrawLine(tip, tip + back - side);
            UnityEditor.Handles.color = _color;
            UnityEditor.Handles.Label(p + Vector3.up * (_size * 2f), name);
        }
#endif
    }
}
