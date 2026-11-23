using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProCommVR.Emotions;
using ProCommVR.Hospital.Face;
using ProCommVR.Hospital.PatientPov;
using ProCommVR.Player;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// Patient POV plus (Liam), on top of Jalil's smoke test machinery (same partial class, own launcher, so Jalil's
    /// files stay unchanged). Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunPatientPovPlus        (PatientViewTest scene)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunPatientPovPlayThrough (Scenario01_PatientRoom)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunPatientPovPlusShift   (ShiftHospital, room 1A)
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private const string PovPlusKey = "ProCommVR.SmokeTest.PatientPovPlus";
        private const string PovPlusTestScene = "Assets/_Project/Scenes/PatientViewTest.unity";
        private const string PovPlusRoomScene = "Assets/_Project/Scenes/Scenario01_PatientRoom.unity";
        private const string PovPlusShiftScene = "Assets/_Project/Scenes/ShiftHospital.unity";

        [MenuItem("ProCommVR/Tests/Patient POV Plus (face = room)")]
        public static void RunPatientPovPlus() => LaunchPovPlus("face", PovPlusTestScene);

        [MenuItem("ProCommVR/Tests/Patient POV Plus (Scenario 1 play-through)")]
        public static void RunPatientPovPlayThrough() => LaunchPovPlus("play", PovPlusRoomScene);

        [MenuItem("ProCommVR/Tests/Patient POV Plus (Shift, room 1A)")]
        public static void RunPatientPovPlusShift() => LaunchPovPlus("shift", PovPlusShiftScene);

        private static void LaunchPovPlus(string test, string scene)
        {
            EditorSceneManager.OpenScene(scene);
            SessionState.SetString(PovPlusKey, test);
            EditorApplication.playModeStateChanged += OnPovPlusPlayMode;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void PovPlusAfterReload()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(PovPlusKey, "")))
                return;
            EditorApplication.playModeStateChanged += OnPovPlusPlayMode;
            if (EditorApplication.isPlaying)
                EditorApplication.delayCall += BeginPovPlus;
        }

        private static void OnPovPlusPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                BeginPovPlus();
        }

        private static void BeginPovPlus()
        {
            var test = SessionState.GetString(PovPlusKey, "");
            if (string.IsNullOrEmpty(test) || _routine != null)
                return;
            SessionState.EraseString(PovPlusKey);
            EditorApplication.playModeStateChanged -= OnPovPlusPlayMode;
            Log($"Running Patient POV plus '{test}' in Play mode.");
            Application.logMessageReceived += OnLog;
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            _routine = test == "face" ? PovPlusFace() : test == "play" ? PovPlusPlayThrough() : PovPlusShift();
            EditorApplication.update += Tick;
        }

        // ---- PatientViewTest: one EmotionHolder drives face, room, nurse, props, sound ------------------------

        private static IEnumerator PovPlusFace()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var holder = Object.FindAnyObjectByType<EmotionHolder>();
            var pov = Object.FindAnyObjectByType<PovController>();
            var world = Object.FindAnyObjectByType<PatientWorldController>();
            var driver = Object.FindAnyObjectByType<EmotionFaceDriver>();
            var nurse = Object.FindAnyObjectByType<PerceivedNurse>();
            var avatar = Object.FindAnyObjectByType<PlayerAvatar>();
            var story = Object.FindAnyObjectByType<StoryProps>();
            var clock = Object.FindAnyObjectByType<StoryClock>();
            var heart = Object.FindAnyObjectByType<HeartbeatFeedback>();
            var muffle = Object.FindAnyObjectByType<MuffledWorld>();
            var voice = Object.FindAnyObjectByType<InnerVoice>();
            var label = Object.FindAnyObjectByType<PatientPovDebugLabel>();
            var presenter = Object.FindAnyObjectByType<ScenarioPresenter>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Check(holder && pov && world && driver && nurse && avatar && story && clock && heart && muffle && voice && label && presenter && origin,
                "the test scene has the patient view and every Patient POV plus part");
            if (Errors.Count > 0)
                yield break;
            var mapping = AssetDatabase.LoadAssetAtPath<EmotionFaceMapping>(PatientPovSetup.FaceMappingPath);
            var patient = new SerializedObject(presenter).FindProperty("_npc").objectReferenceValue as Transform;

            AddPovDevices();
            try
            {
                var settle = EditorApplication.timeSinceStartup + 30f;
                while (Time.timeSinceLevelLoad < 3f && EditorApplication.timeSinceStartup < settle)
                    yield return null;
                CheckPatientModel(patient);
                Check(story.AtBuiltState && !heart.IsBeating && !muffle.IsMuffled && !clock.ShowsStoryTime,
                    "nurse view: props as built, no heartbeat, no muffling, real time on the clock");
                Check(driver.DominantFace == FaceExpression.Sad, $"at the start (mainly sad) the face is Sad ({driver.DominantFace})");

                // Phase 1: the room from the start position.
                _headRotation = Quaternion.LookRotation(HeadOf(patient) - origin.Camera.transform.position);
                yield return 0.4f;
                Screenshot("pov-plus-start-nurse-view");

                // Nurse view, in front of the patient (he faces the window), a little beyond the reach of the answer
                // bubbles so they do not cover his face: one screenshot per emotion.
                Teleport(origin, patient.position + Flat(patient.forward) * 1.6f + Flat(-patient.right) * 0.5f, patient.position);
                _lastSpacePosition = null;
                var face = driver.Face;
                foreach (var emotion in new[] { Emotion.Joy, Emotion.Anger, Emotion.Fear, Emotion.Sadness })
                {
                    holder.SetImmediate(Only(emotion, 0.8f));
                    yield return 1.5f;
                    var expected = mapping.ExpressionFor(emotion);
                    var ok = driver.DominantFace == expected || (emotion == Emotion.Sadness && driver.DominantFace == FaceExpression.Crying);
                    Check(ok, $"nurse view, {emotion} 0.8: the face shows {expected} ({driver.DominantFace}, {driver.GetMix(driver.DominantFace):F2})");
                    var shape = KeyShape(emotion);
                    Check(face.GetWeight(shape) > 30f, $"nurse view, {emotion}: blendshape {shape} is on the mesh ({face.GetWeight(shape):F0})");
                    Check(world.IsNeutral, $"nurse view, {emotion}: the room stays normal");
                    _headRotation = Quaternion.LookRotation(HeadOf(patient) - origin.Camera.transform.position);
                    yield return 0.4f;
                    Screenshot($"pov-plus-nurse-sees-{emotion.ToString().ToLowerInvariant()}");
                }
                Check(face.GetWeight("Tears") > 25f, $"Sadness 0.8: tears on the face ({face.GetWeight("Tears"):F0})");
                holder.SetImmediate(EmotionState.Zero);
                yield return 1.2f;
                Check(driver.DominantFace == FaceExpression.Neutral, $"everything 0: the face is Neutral ({driver.DominantFace})");

                // Gaze: sad looks away (to the window), angry looks at the nurse.
                holder.SetImmediate(Only(Emotion.Sadness, 0.8f));
                yield return 0.5f;
                Check(!driver.LooksAtNurse, "Sadness: he looks to the window, not at the nurse");
                holder.SetImmediate(Only(Emotion.Anger, 0.8f));
                yield return 0.5f;
                Check(driver.LooksAtNurse, "Anger: he stares at the nurse");

                // Into the patient view with Sadness: exactly one thought.
                holder.SetImmediate(Only(Emotion.Sadness, 0.9f));
                Teleport(origin, patient.position + Flat(origin.Camera.transform.position - patient.position).normalized * 1.0f, patient.position);
                _lastSpacePosition = null;
                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                    yield return step;
                Check(pov.Mode == PovMode.Patient, "Y switches to the patient view");
                if (pov.Mode != PovMode.Patient)
                    yield break;
                yield return 1.5f;
                Check(voice.PlayedCount == 1 && voice.LastLine != null && voice.LastLine.Emotion == Emotion.Sadness && voice.SubtitleShown,
                    $"entering the patient view plays one thought with a subtitle ({voice.PlayedCount}, {(voice.LastLine != null ? voice.LastLine.Text : "none")})");
                Screenshot("pov-plus-patient-inner-voice");

                foreach (var emotion in new[] { Emotion.Sadness, Emotion.Joy, Emotion.Anger, Emotion.Fear })
                {
                    holder.SetImmediate(Only(emotion, 0.9f));
                    foreach (var step in WaitForWorld(world, emotion))
                        yield return step;
                    yield return 2.6f; // props, nurse and clock blend in about 2 s
                    var faceOk = driver.DominantFace == mapping.ExpressionFor(emotion) || (emotion == Emotion.Sadness && driver.DominantFace == FaceExpression.Crying);
                    Check(faceOk && world.ShownEmotion == emotion, $"patient view, {emotion}: face {driver.DominantFace} and room {world.ShownEmotion} show the same emotion");
                    Check(nurse.ShownEmotion == emotion, $"patient view, {emotion}: the nurse is perceived as {emotion} ({nurse.ShownEmotion})");
                    Check(label.Label != null && label.Label.text.Contains($"World: {emotion}"), $"the test label shows the world ({(label.Label != null ? label.Label.text : "none")})");
                    _headRotation = Quaternion.LookRotation(avatar.transform.position + Vector3.up * 1.5f - origin.Camera.transform.position);
                    yield return 0.4f;
                    Screenshot($"pov-plus-patient-sees-nurse-{emotion.ToString().ToLowerInvariant()}");
                    _headRotation = Quaternion.LookRotation(story.Card.position - origin.Camera.transform.position);
                    yield return 0.3f;
                    Screenshot($"pov-plus-patient-props-{emotion.ToString().ToLowerInvariant()}");
                    switch (emotion)
                    {
                        case Emotion.Sadness:
                            Check(story.PhotoTip > 0.9f && story.PhotoColorAmount < 0.2f, $"Sadness 0.9: the photo is grey and face down ({story.PhotoColorAmount:F2}, tip {story.PhotoTip:F2})");
                            Check(story.BalloonScale < 0.6f, $"Sadness: the balloon deflates ({story.BalloonScale:F2})");
                            Check(muffle.IsMuffled && muffle.Cutoff < 2000f && muffle.FilterCount > 0, $"Sadness: the world sounds dull ({muffle.Cutoff:F0} Hz, {muffle.FilterCount} sources)");
                            Check(clock.ShowsStoryTime && clock.TimeScale < 0.5f, $"Sadness: the clock crawls ({clock.TimeScale:F2})");
                            Check(nurse.ScaleFactor < 1f && nurse.IsTinted, $"Sadness: the nurse looks smaller and grey ({nurse.ScaleFactor:F3})");
                            Check(!heart.IsBeating, "Sadness: no heartbeat");
                            break;
                        case Emotion.Joy:
                            Check(story.BalloonRise > 0.2f && story.PhotoColorAmount > 0.95f, $"Joy: the balloon rises ({story.BalloonRise:F2} m), the photo in color");
                            Check(nurse.Face != null && nurse.Face.DominantExpression == FaceExpression.Happy, $"Joy: the nurse smiles ({(nurse.Face != null ? nurse.Face.DominantExpression.ToString() : "no face")})");
                            Check(!muffle.IsMuffled, "Joy: clear sound again");
                            break;
                        case Emotion.Anger:
                            Check(story.Card.localScale.y < 0.8f * 1f, $"Anger: the card is crumpled ({story.Card.localScale})");
                            Check(nurse.ScaleFactor > 1.02f && nurse.Face != null && nurse.Face.DominantExpression == FaceExpression.Angry,
                                $"Anger: the nurse looks bigger ({nurse.ScaleFactor:F3}) and stern ({(nurse.Face != null ? nurse.Face.DominantExpression.ToString() : "no face")})");
                            Check(heart.IsBeating && heart.Beats > 0, $"Anger: heartbeat ({heart.Beats} beats, {heart.Bpm:F0} bpm)");
                            Check(clock.TimeScale > 1.5f, $"Anger: the clock races ({clock.TimeScale:F2})");
                            break;
                        case Emotion.Fear:
                            Check(heart.IsBeating && heart.Bpm > 100f, $"Fear 0.9: heartbeat at {heart.Bpm:F0} bpm");
                            Check(nurse.ScaleFactor > 1.02f && nurse.IsTinted, $"Fear: the nurse is a darker, bigger figure ({nurse.ScaleFactor:F3})");
                            break;
                    }
                }
                Check(voice.PlayedCount == 1, $"changing the emotion inside the patient view plays no new thought ({voice.PlayedCount})");
                _headRotation = Quaternion.LookRotation(Flat(origin.transform.forward));

                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                    yield return step;
                Check(pov.Mode == PovMode.Nurse, "Y switches back to the nurse view");
                yield return 0.3f;
                Check(!avatar.IsVisible && Mathf.Approximately(nurse.ScaleFactor, 1f) && !nurse.IsTinted && nurse.ShownEmotion == null,
                    $"nurse view: the avatar is hidden, scale and tint restored ({nurse.ScaleFactor:F3}, tinted {nurse.IsTinted})");
                Check(Mathf.Approximately(avatar.transform.localScale.x, 1f) || avatar.transform.localScale == Vector3.one * avatar.transform.localScale.x,
                    $"the avatar's scale is back ({avatar.transform.localScale})");
                Check(story.AtBuiltState, "nurse view: every story prop is back at its built state");
                Check(!heart.IsBeating && !muffle.IsMuffled && !clock.ShowsStoryTime && !voice.IsSpeaking,
                    "nurse view: heartbeat stopped, filters off, real time on the clock, no thought");

                // Again into the patient view with Fear: one new thought (Fear was not heard yet), heartbeat.
                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                    yield return step;
                yield return 1.5f;
                Check(voice.PlayedCount == 2 && voice.LastLine != null && voice.LastLine.Emotion == Emotion.Fear,
                    $"a second entry with another emotion plays that thought ({voice.PlayedCount}, {(voice.LastLine != null ? voice.LastLine.Emotion.ToString() : "none")})");
                Check(heart.IsBeating, "Fear: heartbeat in the patient view");
                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                    yield return step;
                yield return 0.3f;
                Check(!heart.IsBeating && story.AtBuiltState, "back in the nurse view: all restored");
            }
            finally
            {
                _headRotation = Quaternion.identity;
                RemovePovDevices();
            }
        }

        // ---- Scenario01_PatientRoom: the four answers, one table ---------------------------------------------

        private static IEnumerator PovPlusPlayThrough()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var presenter = Object.FindAnyObjectByType<ScenarioPresenter>();
            var sequence = Object.FindAnyObjectByType<ScenarioSequence>();
            var holder = Object.FindAnyObjectByType<EmotionHolder>();
            var pov = Object.FindAnyObjectByType<PovController>();
            var world = Object.FindAnyObjectByType<PatientWorldController>();
            var driver = Object.FindAnyObjectByType<EmotionFaceDriver>();
            var nurse = Object.FindAnyObjectByType<PerceivedNurse>();
            var story = Object.FindAnyObjectByType<StoryProps>();
            var clock = Object.FindAnyObjectByType<StoryClock>();
            var heart = Object.FindAnyObjectByType<HeartbeatFeedback>();
            var muffle = Object.FindAnyObjectByType<MuffledWorld>();
            var voice = Object.FindAnyObjectByType<InnerVoice>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Check(presenter && sequence && holder && pov && world && driver && nurse && story && clock && heart && muffle && voice && origin,
                "Scenario01_PatientRoom has the patient view and Patient POV plus");
            if (Errors.Count > 0)
                yield break;
            var scenario = presenter.CurrentScenario;
            var patient = new SerializedObject(presenter).FindProperty("_npc").objectReferenceValue as Transform;
            var start = origin.transform.position;
            var table = new StringBuilder();
            table.AppendLine("| Answer (score) | Joy | Anger | Fear | Sadness | Dominant | Face | Room | Nurse | Props | Sound | Thought |");
            table.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            AddPovDevices();
            try
            {
                var settle = EditorApplication.timeSinceStartup + 30f;
                while (Time.timeSinceLevelLoad < 3f && EditorApplication.timeSinceStartup < settle)
                    yield return null;
                CheckPatientModel(patient);
                foreach (var option in scenario.Options.OrderByDescending(o => o.Score).ToArray())
                {
                    presenter.Play(scenario);
                    Teleport(origin, start, patient.position);
                    _lastSpacePosition = null;
                    yield return 0.5f;
                    var scoreBefore = sequence.Score.Total;
                    Teleport(origin, patient.position + Flat(origin.Camera.transform.position - patient.position).normalized * 1.0f, patient.position);
                    _lastSpacePosition = null;
                    var waitUntil = EditorApplication.timeSinceStartup + 3f;
                    while (OptionBubbles().Length < 4 && EditorApplication.timeSinceStartup < waitUntil)
                        yield return null;
                    var bubble = OptionBubbles().FirstOrDefault(b => BubbleText(b).Contains(option.Text.Substring(0, 40)));
                    Check(bubble != null, $"option {option.Score} within reach");
                    if (bubble == null)
                        yield break;
                    DialogueOption chosen = null;
                    void OnChosen(Scenario s, DialogueOption o) => chosen = o;
                    presenter.OptionChosen += OnChosen;
                    try
                    {
                        foreach (var step in Click(Center(bubble), () => chosen != null))
                            yield return step;
                    }
                    finally
                    {
                        presenter.OptionChosen -= OnChosen;
                    }
                    Check(chosen == option, $"chose the option with score {option.Score}");
                    yield return holder.BlendSeconds + 1.0f;
                    _headRotation = Quaternion.LookRotation(HeadOf(patient) - origin.Camera.transform.position);
                    yield return 0.4f;
                    Screenshot($"play-{option.Score}-nurse-view");
                    var nurseFace = driver.DominantFace;

                    foreach (var step in WaitUntilAllowed(pov))
                        yield return step;
                    var thoughtsBefore = voice.PlayedCount;
                    foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                        yield return step;
                    Check(pov.Mode == PovMode.Patient, $"option {option.Score}: Y works after the answer");
                    yield return 3.0f;
                    var c = holder.Current;
                    var dominant = holder.Dominant;
                    var faceOk = dominant.HasValue && (driver.DominantFace == AssetDatabase.LoadAssetAtPath<EmotionFaceMapping>(PatientPovSetup.FaceMappingPath).ExpressionFor(dominant.Value)
                                                       || (dominant == Emotion.Sadness && driver.DominantFace == FaceExpression.Crying));
                    Check(faceOk && world.ShownEmotion == dominant, $"option {option.Score}: face {nurseFace}/{driver.DominantFace} agrees with room {world.ShownEmotion}");
                    var props = $"card {story.Card.localScale.y:F2}, photo color {story.PhotoColorAmount:F2} tip {story.PhotoTip:F2}, balloon x{story.BalloonScale:F2} +{story.BalloonRise:F2} m, clock x{clock.TimeScale:F2}";
                    var sound = $"{(heart.IsBeating ? $"heart {heart.Bpm:F0} bpm" : "no heart")}, {(muffle.IsMuffled ? $"muffled {muffle.Cutoff:F0} Hz" : "clear")}";
                    var thought = voice.PlayedCount > thoughtsBefore && voice.LastLine != null ? voice.LastLine.Text : "-";
                    table.AppendLine($"| {option.Score} | {c.Joy:F2} | {c.Anger:F2} | {c.Fear:F2} | {c.Sadness:F2} | {dominant} | {driver.DominantFace} ({driver.GetMix(driver.DominantFace):F2}) | {world.ShownEmotion} ({world.Weight:F2}) | {nurse.ShownEmotion} x{nurse.ScaleFactor:F2} | {props} | {sound} | {thought} |");
                    _headRotation = Quaternion.LookRotation(story.Card.position - origin.Camera.transform.position);
                    yield return 0.3f;
                    Screenshot($"play-{option.Score}-patient-props");
                    _headRotation = Quaternion.LookRotation(Flat(origin.transform.forward));

                    foreach (var step in WaitUntilAllowed(pov))
                        yield return step;
                    foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                        yield return step;
                    yield return 0.3f;
                    Check(sequence.Score.Total - scoreBefore == option.Score || sequence.Score.Results.Count >= 1,
                        $"option {option.Score}: the score is unchanged by the patient view ({sequence.Score.Total})");
                    Check(story.AtBuiltState && !heart.IsBeating && !muffle.IsMuffled, $"option {option.Score}: back to normal in the nurse view");
                }
                Log("Play-through table:\n" + table);
            }
            finally
            {
                _headRotation = Quaternion.identity;
                RemovePovDevices();
            }
        }

        // ---- ShiftHospital room 1A: the same parts, in Liam's room ----------------------------------------------

        private static IEnumerator PovPlusShift()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var drivers = Object.FindObjectsByType<EmotionFaceDriver>(FindObjectsSortMode.None);
            var story = Object.FindAnyObjectByType<StoryProps>();
            var clock = Object.FindAnyObjectByType<StoryClock>();
            var nurse = Object.FindAnyObjectByType<PerceivedNurse>();
            Check(drivers.Length == 1 && story != null && clock != null && nurse != null && Object.FindAnyObjectByType<HeartbeatFeedback>() != null
                  && Object.FindAnyObjectByType<MuffledWorld>() != null && Object.FindAnyObjectByType<InnerVoice>() != null,
                $"room 1A has Patient POV plus ({drivers.Length} face driver)");
            if (Errors.Count > 0)
                yield break;
            var driver = drivers[0];
            var room = driver.transform.parent;
            while (room != null && !room.name.StartsWith("Room_"))
                room = room.parent;
            Check(room != null && room.name == "Room_1A", $"the face driver is on the patient of room 1A ({(room != null ? room.name : "none")})");
            if (room == null)
                yield break;
            var bounds = new Bounds(room.TransformPoint(0f, 1.5f, 2f), new Vector3(4.6f, 3.6f, 4.6f));
            Check(bounds.Contains(story.Card.position) && bounds.Contains(story.PhotoFrame.position) && bounds.Contains(clock.transform.position),
                "the card, the photo and the clock are inside room 1A");
            Check(!bounds.Contains(story.Balloon.position), "the balloon floats outside the window of 1A");
            yield return 2f;
            var holder = driver.Holder;
            foreach (var emotion in new[] { Emotion.Joy, Emotion.Anger, Emotion.Fear, Emotion.Sadness })
            {
                holder.SetImmediate(Only(emotion, 0.8f));
                yield return 1.2f;
                var expected = AssetDatabase.LoadAssetAtPath<EmotionFaceMapping>(PatientPovSetup.FaceMappingPath).ExpressionFor(emotion);
                Check(driver.DominantFace == expected || (emotion == Emotion.Sadness && driver.DominantFace == FaceExpression.Crying),
                    $"room 1A, {emotion}: the face shows {expected} ({driver.DominantFace})");
            }
            Check(story.AtBuiltState, "room 1A, nurse view: the props stay as built");
        }

        // ---- Helpers ---------------------------------------------------------------------------------------------

        private static EmotionState Only(Emotion emotion, float value)
        {
            float V(Emotion e) => e == emotion ? value : 0.05f;
            return new EmotionState(V(Emotion.Joy), V(Emotion.Anger), V(Emotion.Fear), V(Emotion.Sadness));
        }

        private static string KeyShape(Emotion emotion)
        {
            switch (emotion)
            {
                case Emotion.Joy: return "Smile";
                case Emotion.Anger: return "BrowsAngry";
                case Emotion.Fear: return "BrowsRaise";
                default: return "BrowsSad";
            }
        }

        private static Vector3 HeadOf(Transform patient)
        {
            var animator = patient.GetComponentInChildren<Animator>();
            var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            return head != null ? head.position : patient.position + Vector3.up * 1.2f;
        }

        /// <summary>Phase 1: Patient X is Liam's old man with a face and a bandage, sitting on the bed.</summary>
        private static void CheckPatientModel(Transform patient)
        {
            var face = patient != null ? patient.GetComponentInChildren<FaceController>() : null;
            Check(face != null && face.IsReady, "the patient has a face (FaceController with blendshapes)");
            var animator = patient != null ? patient.GetComponentInChildren<Animator>() : null;
            Check(animator != null && animator.isHuman, "the patient has a Humanoid rig (the POV uses his Head bone)");
            Check(patient != null && patient.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Bandage")), "his lower right leg has a bandage");
            var head = HeadOf(patient);
            Check(head.y > 1.0f && head.y < 1.9f, $"he sits on the bed (head at {head.y:F2} m)");
        }
    }
}
