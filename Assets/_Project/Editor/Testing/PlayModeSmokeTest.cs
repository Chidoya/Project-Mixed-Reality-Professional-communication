using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// Runs a scripted check in Play mode and exits the editor with 0 (passed) or 1 (failed).
    /// Every error or exception logged while playing fails the run. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario01Mood
    /// (the shift tests are in PlayModeSmokeTest.Shift.cs, the Scenario 2 tests in PlayModeSmokeTest.Scenario02.cs).
    /// The test survives the domain reload of entering Play mode through SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static partial class PlayModeSmokeTest
    {
        private const string TestKey = "ProCommVR.SmokeTest";
        private const float TimeoutSeconds = 240f;

        private static readonly List<string> Errors = new List<string>();
        private static IEnumerator _routine;
        private static double _waitUntil;
        private static double _deadline;

        static PlayModeSmokeTest()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(TestKey, "")))
                return;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (EditorApplication.isPlaying)
                Begin();
        }

        [MenuItem("ProCommVR/Tests/Scenario 01 Mood")]
        public static void RunScenario01Mood()
        {
            Launch("mood", "Assets/_Project/Scenes/Scenario01_PatientRoom.unity");
        }

        private static void Launch(string test, string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);
            SessionState.SetString(TestKey, test);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && _routine == null)
                Begin();
        }

        private static void Begin()
        {
            var test = SessionState.GetString(TestKey, "");
            Log($"Running '{test}' in Play mode.");
            Application.logMessageReceived += OnLog;
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            _routine = CreateRoutine(test);
            EditorApplication.update += Tick;
        }

        private static IEnumerator CreateRoutine(string test)
        {
            switch (test)
            {
                case "mood": return Scenario01Mood();
                case "shift": return ShiftLoop(false);
                case "shift-timeout": return ShiftLoop(true);
                case "s2-good": return Scenario02("consultation-room", Answer.Best);
                case "s2-poor": return Scenario02("corridor", Answer.Poorest);
                case "s2-timeup": return Scenario02("corridor", Answer.None);
                default: return Fail($"Unknown test '{test}'.");
            }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                Finish(false, $"Timed out after {TimeoutSeconds} s.");
                return;
            }
            if (EditorApplication.timeSinceStartup < _waitUntil)
                return;

            try
            {
                if (!_routine.MoveNext())
                {
                    Finish(Errors.Count == 0, Errors.Count == 0 ? "All checks passed." : $"{Errors.Count} error(s) logged.");
                    return;
                }
                if (_routine.Current is float seconds)
                    _waitUntil = EditorApplication.timeSinceStartup + seconds;
            }
            catch (Exception exception)
            {
                Errors.Add(exception.ToString());
                Finish(false, "The test itself threw an exception.");
            }
        }

        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.EraseString(TestKey);
            foreach (var error in Errors)
                Debug.Log($"[SmokeTest] ERROR: {error}");
            Log($"{(passed ? "PASSED" : "FAILED")}: {message}");
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                Errors.Add($"{type}: {condition}\n{stackTrace}");
        }

        private static IEnumerator Fail(string message)
        {
            Errors.Add(message);
            yield break;
        }

        /// <summary>
        /// Records a failed check without stopping the test.
        /// </summary>
        private static void Check(bool condition, string what)
        {
            if (condition)
                Log($"ok: {what}");
            else
                Errors.Add($"Check failed: {what}");
        }

        private static void Log(string message)
        {
            Debug.Log($"[SmokeTest] {message}");
        }

        private static string Describe(Light light)
        {
            return light == null ? "no light" : $"color {light.color}, intensity {light.intensity:F2}";
        }

        private static string DescribeAmbient()
        {
            var probe = RenderSettings.ambientProbe;
            return $"mode {RenderSettings.ambientMode}, ambientLight {RenderSettings.ambientLight}, " +
                   $"probe L0 ({probe[0, 0]:F3}, {probe[1, 0]:F3}, {probe[2, 0]:F3})";
        }

        /// <summary>
        /// Scenario 1 is Somber: after the fade, the main light and ambient light match the Somber profile.
        /// Logs the values right after the start too: the fade happens once, in the first 1.5 s of the scene.
        /// </summary>
        private static IEnumerator Scenario01Mood()
        {
            var light = UnityEngine.Object.FindAnyObjectByType<Light>();
            var mood = UnityEngine.Object.FindAnyObjectByType<Atmosphere.MoodController>();
            Log($"frame 1: light {Describe(light)}; ambient {DescribeAmbient()}");
            yield return 2.5f;
            Log($"t=2.5s: light {Describe(light)}; ambient {DescribeAmbient()}");

            var profile = mood.CurrentProfile;
            Check(mood.CurrentMood == Scenarios.Mood.Somber, $"Scenario 1 sets the mood to Somber (got {mood.CurrentMood})");
            Check(profile != null && light.color == profile.LightColor && Mathf.Approximately(light.intensity, profile.LightIntensity),
                "main light has the Somber color and intensity");
            Check(profile != null && RenderSettings.ambientLight == profile.AmbientColor, "ambient light has the Somber color");
            var audio = UnityEngine.Object.FindAnyObjectByType<Atmosphere.MoodAudio>();
            Check(audio != null && profile != null && audio.CurrentSource.isPlaying && audio.CurrentSource.clip == profile.Sound,
                $"the Somber sound plays ({(audio != null && audio.CurrentSource.clip != null ? audio.CurrentSource.clip.name : "none")})");
        }
    }
}
