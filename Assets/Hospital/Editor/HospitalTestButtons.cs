using System;
using ProCommVR.Hospital;
using UnityEditor;
using UnityEngine;

namespace ProCommVR.Editor
{
    /// <summary>Testknoppen in de Inspector voor de losse stukken in Assets/Hospital/Scripts.</summary>
    internal static class TestButtons
    {
        /// <summary>Tekent de gewone Inspector en daaronder knoppen. Enkel actief in Play mode, tenzij inEditMode.</summary>
        public static void Draw(UnityEditor.Editor editor, bool inEditMode, params (string Label, Action Action)[] buttons)
        {
            editor.DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Test", EditorStyles.boldLabel);
            if (!inEditMode && !Application.isPlaying)
                EditorGUILayout.HelpBox("Start Play mode om te testen.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!inEditMode && !Application.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var (label, action) in buttons)
                    if (GUILayout.Button(label))
                        action();
            }
        }
    }

    [CustomEditor(typeof(ItemSpots))]
    internal class ItemSpotsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var spots = (ItemSpots)target;
            var buttons = new (string, Action)[spots.Count + 1];
            buttons[0] = ("Next", () => Move(spots, spots.Next));
            for (var i = 0; i < spots.Count; i++)
            {
                var index = i;
                buttons[i + 1] = (spots.SpotName(i).Replace("Spot_", ""), () => Move(spots, () => spots.MoveTo(index)));
            }
            TestButtons.Draw(this, true, buttons);
        }

        private static void Move(ItemSpots spots, Action move)
        {
            if (spots.Item != null)
                Undo.RecordObjects(new UnityEngine.Object[] { spots, spots.Item }, "Move item");
            move();
        }
    }

    [CustomEditor(typeof(RoomTint))]
    internal class RoomTintEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var tint = (RoomTint)target;
            TestButtons.Draw(this, false, ("Dim", tint.Dim), ("Warm", tint.Warm), ("Restore", tint.Restore));
        }
    }

    [CustomEditor(typeof(EarGrow))]
    internal class EarGrowEditor : UnityEditor.Editor
    {
        private float _seconds = 4f;

        public override void OnInspectorGUI()
        {
            var ear = (EarGrow)target;
            TestButtons.Draw(this, false, ($"Grow ({_seconds:0} s)", () => ear.Grow(_seconds)), ("Reset", ear.ResetEar));
            _seconds = EditorGUILayout.Slider("Test: seconden", _seconds, 0.5f, 20f);
        }
    }

    [CustomEditor(typeof(MonitorScreen))]
    internal class MonitorScreenEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var monitor = (MonitorScreen)target;
            TestButtons.Draw(this, false, ("Alarm on", () => monitor.SetAlarm(true)), ("Alarm off", () => monitor.SetAlarm(false)));
        }
    }

    [CustomEditor(typeof(PassingCart))]
    internal class PassingCartEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var cart = (PassingCart)target;
            TestButtons.Draw(this, false, ("Play", cart.Play), ("Stop", cart.Stop), ("Reset", cart.ResetToStart));
        }
    }
}
