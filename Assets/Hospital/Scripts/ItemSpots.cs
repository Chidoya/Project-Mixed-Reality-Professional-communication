using System;
using UnityEngine;

namespace ProCommVR.Hospital
{
    /// <summary>
    /// Laat een object (bv. de Pill_Blister) verspringen tussen benoemde plekken (lege objecten, bv. Spot_Tray,
    /// Spot_Nightstand, Spot_Windowsill). Het object neemt de positie van de plek over en houdt zijn rotatie
    /// ten opzichte van de plek, zoals bij de eerste plek.
    /// </summary>
    public class ItemSpots : MonoBehaviour
    {
        [SerializeField] private Transform _item;
        [SerializeField] private Transform[] _spots = Array.Empty<Transform>();
        [SerializeField] private int _current;
        [Tooltip("Rotatie van het object ten opzichte van zijn plek (ingevuld bij het bouwen).")]
        [SerializeField] private Quaternion _offset = Quaternion.identity;

        public event Action<int> Moved;

        public int Current => _current;
        public int Count => _spots.Length;
        public Transform Item => _item;
        public string SpotName(int index) => _spots[index] != null ? _spots[index].name : "";

        public void Configure(Transform item, Transform[] spots, int start)
        {
            _item = item;
            _spots = spots;
            _offset = Quaternion.Inverse(spots[start].rotation) * item.rotation;
            MoveTo(start);
        }

        public void MoveTo(int index)
        {
            if (_item == null || index < 0 || index >= _spots.Length || _spots[index] == null)
            {
                Debug.LogWarning($"[ItemSpots] {name}: plek {index} bestaat niet.", this);
                return;
            }
            _current = index;
            _item.SetPositionAndRotation(_spots[index].position, _spots[index].rotation * _offset);
            Moved?.Invoke(index);
        }

        /// <summary>Verspringt naar de plek met deze naam (bv. "Spot_Windowsill").</summary>
        public void MoveTo(string spotName)
        {
            var index = Array.FindIndex(_spots, s => s != null && s.name == spotName);
            if (index < 0)
                Debug.LogWarning($"[ItemSpots] {name}: geen plek '{spotName}'.", this);
            else
                MoveTo(index);
        }

        public void Next() => MoveTo((_current + 1) % Mathf.Max(1, _spots.Length));
    }
}
