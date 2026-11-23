using System.Collections.Generic;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// The doctor's voice for the feedback that comes from the AI server: per rubric item and score
    /// (<c>s1_item2_score0</c>), the better phrase per item (<c>s1_item2_better</c>) and the critical error
    /// (<c>s1_critical_false_discharge_promise</c>). Rendered in advance with Piper (Tools/AiServer/tools/gen_doctor_lines.py),
    /// so nothing is generated live during the debrief. The text is kept too: when the server's text differs from the
    /// recorded one (the scenario was edited and the clips were not rendered again), the doctor shows the server's text
    /// without the old clip.
    /// </summary>
    [CreateAssetMenu(menuName = "ProCommVR/Debrief Voice Library", fileName = "DebriefVoiceLibrary")]
    public class DebriefVoiceLibrary : ScriptableObject
    {
        public List<DebriefLine> Entries = new();

        private Dictionary<string, DebriefLine> _byKey;

        public DebriefLine Find(string key)
        {
            if (_byKey == null || _byKey.Count != Entries.Count)
            {
                _byKey = new Dictionary<string, DebriefLine>();
                foreach (var entry in Entries)
                    if (entry != null && !string.IsNullOrEmpty(entry.Key))
                        _byKey[entry.Key] = entry;
            }
            return key != null && _byKey.TryGetValue(key, out var found) ? found : null;
        }

        /// <summary>
        /// The line for this key with the text the server sent: the clip only when its recorded text is the same
        /// (ignoring case and spaces), otherwise text only (and a warning).
        /// </summary>
        public DebriefLine For(string key, string text)
        {
            var entry = Find(key);
            if (entry == null)
                return new DebriefLine(key, text);
            if (string.IsNullOrEmpty(text) || Same(entry.Text, text))
                return entry;
            Debug.LogWarning($"[Debrief] Voice clip '{key}' was recorded for another text; showing the new text without voice. " +
                             "Run Tools/AiServer/tools/gen_doctor_lines.py again.");
            return new DebriefLine(key, text);
        }

        private static bool Same(string a, string b) =>
            string.Equals(Normalize(a), Normalize(b), System.StringComparison.OrdinalIgnoreCase);

        private static string Normalize(string s) => string.Join(" ", (s ?? "").Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));

        private void OnValidate() => _byKey = null;
    }
}
