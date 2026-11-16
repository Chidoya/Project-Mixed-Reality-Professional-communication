using ProCommVR.Emotions;
using UnityEditor;
using UnityEngine;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Shows an <see cref="EmotionState"/> field with an <see cref="EmotionRangeAttribute"/> as a label and four sliders
    /// (Joy, Anger, Fear, Sadness) in the attribute's range.
    /// </summary>
    [CustomPropertyDrawer(typeof(EmotionRangeAttribute))]
    public class EmotionStateDrawer : PropertyDrawer
    {
        private static readonly string[] Fields = { "_joy", "_anger", "_fear", "_sadness" };
        private static readonly string[] Labels = { "Joy", "Anger", "Fear", "Sadness" };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return property.type == nameof(EmotionState) ? line * (Fields.Length + 1) : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.type != nameof(EmotionState))
            {
                EditorGUI.LabelField(position, label.text, "Use EmotionRange on an EmotionState field.");
                return;
            }

            var range = (EmotionRangeAttribute)attribute;
            var line = EditorGUIUtility.singleLineHeight;
            var step = line + EditorGUIUtility.standardVerticalSpacing;
            var row = new Rect(position.x, position.y, position.width, line);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(row, label);
            using (new EditorGUI.DisabledScope(range.ReadOnly))
            using (new EditorGUI.IndentLevelScope())
            {
                for (var i = 0; i < Fields.Length; i++)
                {
                    row.y += step;
                    EditorGUI.Slider(row, property.FindPropertyRelative(Fields[i]), range.Min, range.Max, Labels[i]);
                }
            }
            EditorGUI.EndProperty();
        }
    }
}
