using ProCommVR.Hospital;
using UnityEditor;
using UnityEngine;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Tijdelijke testknoppen voor de alarmlampen (scenario 2): ProCommVR > Hospital > Test Alarm > Aan, Uit of Knipperen.
    /// Werkt op alle AlarmLamp-componenten in de open scene. Bedoeld voor Play mode; in edit mode verandert het de scene
    /// (zet het alarm dan weer uit voor je opslaat). Mag weg zodra Jalils mood de lampen aanstuurt.
    /// </summary>
    public static class AlarmTestMenu
    {
        private const string Menu = "ProCommVR/Hospital/Test Alarm/";

        [MenuItem(Menu + "Aan")]
        private static void On() => ForAll(l => l.SetOn(true), "aan");

        [MenuItem(Menu + "Uit")]
        private static void Off() => ForAll(l => l.SetOn(false), "uit");

        [MenuItem(Menu + "Knipperen")]
        private static void Blink() => ForAll(l => l.Blink(0.5f), "knipperen");

        private static void ForAll(System.Action<AlarmLamp> action, string what)
        {
            var lamps = Object.FindObjectsByType<AlarmLamp>(FindObjectsSortMode.None);
            foreach (var lamp in lamps)
                action(lamp);
            if (!Application.isPlaying)
                Debug.LogWarning("[AlarmTestMenu] Edit mode: de scene is gewijzigd. Zet het alarm weer uit voor je opslaat.");
            Debug.Log($"[AlarmTestMenu] {lamps.Length} alarmlamp(en) {what}.");
        }
    }
}
