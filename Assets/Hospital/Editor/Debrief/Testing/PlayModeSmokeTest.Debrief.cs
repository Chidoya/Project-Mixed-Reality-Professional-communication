using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProCommVR.AiTalk;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Hospital.Debrief;
using ProCommVR.Hospital.Debrief.Editor;
using ProCommVR.Hospital.Debrief.Testing;
using ProCommVR.Pov;
using Unity.Profiling;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// The doctor-led debrief (Liam), on top of Jalil's smoke test machinery (same partial class, own launcher, so
    /// Jalil's files stay unchanged). Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunDebriefDoctor      (doctor test scene)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunDebriefReflection  (Prototype1, no AI server needed)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunDebriefFeedback    (Prototype1, no AI server needed)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunMemoryReplay       (Prototype1, no AI server needed)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunDebriefLive        (Prototype1, needs the AI server)
    /// The Prototype1 tests without the server inject a finished conversation (recorded turns with 1 s test tones) and a
    /// debrief through a test source.
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private const string DebriefKey = "ProCommVR.SmokeTest.Debrief";
        private const float DebriefTimeout = 600f;

        [MenuItem("ProCommVR/Tests/Debrief: doctor (test scene)")]
        public static void RunDebriefDoctor() => LaunchDebrief("doctor", DebriefDoctorTestScene.ScenePath);

        [MenuItem("ProCommVR/Tests/Debrief: reflection question (Prototype1)")]
        public static void RunDebriefReflection() => LaunchDebrief("reflection", Prototype1SceneBuilder.ScenePath);

        [MenuItem("ProCommVR/Tests/Debrief: timeline and feedback (Prototype1)")]
        public static void RunDebriefFeedback() => LaunchDebrief("feedback", Prototype1SceneBuilder.ScenePath);

        [MenuItem("ProCommVR/Tests/Debrief: memory replay (Prototype1)")]
        public static void RunMemoryReplay() => LaunchDebrief("memory", Prototype1SceneBuilder.ScenePath);

        [MenuItem("ProCommVR/Tests/Scenario 1: the patient in Prototype1 (face, story, bed)")]
        public static void RunScenario1Patient() => LaunchDebrief("patient", Prototype1SceneBuilder.ScenePath);

        [MenuItem("ProCommVR/Tests/Debrief: live with the AI server (Prototype1)")]
        public static void RunDebriefLive() => LaunchDebrief("live", Prototype1SceneBuilder.ScenePath);

        private static void LaunchDebrief(string test, string scene)
        {
            EditorSceneManager.OpenScene(scene);
            SessionState.SetString(DebriefKey, test);
            EditorApplication.playModeStateChanged += OnDebriefPlayMode;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void DebriefAfterReload()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(DebriefKey, "")))
                return;
            EditorApplication.playModeStateChanged += OnDebriefPlayMode;
            if (EditorApplication.isPlaying)
                EditorApplication.delayCall += BeginDebrief;
        }

        private static void OnDebriefPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                BeginDebrief();
        }

        private static void BeginDebrief()
        {
            var test = SessionState.GetString(DebriefKey, "");
            if (string.IsNullOrEmpty(test) || _routine != null)
                return;
            SessionState.EraseString(DebriefKey);
            EditorApplication.playModeStateChanged -= OnDebriefPlayMode;
            Log($"Running debrief test '{test}' in Play mode.");
            Application.logMessageReceived += OnLog;
            _deadline = EditorApplication.timeSinceStartup + DebriefTimeout;
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            _routine = test switch
            {
                "doctor" => DebriefDoctorTest(),
                "reflection" => DebriefFlowTest(DebriefPart.Reflection),
                "feedback" => DebriefFlowTest(DebriefPart.Feedback),
                "memory" => DebriefFlowTest(DebriefPart.Memory),
                "live" => DebriefLiveTest(),
                "patient" => Scenario1PatientTest(),
                _ => Fail($"Unknown debrief test '{test}'."),
            };
            EditorApplication.update += Tick;
        }

        private enum DebriefPart { Reflection, Feedback, Memory }

        private static bool _subtitleChecked;

        // ---- The doctor in her test scene ------------------------------------------------------------------------------

        private static IEnumerator DebriefDoctorTest()
        {
            var panel = Object.FindAnyObjectByType<DebriefDoctorTestPanel>();
            var speaker = Object.FindAnyObjectByType<DoctorSpeaker>();
            var pointer = Object.FindAnyObjectByType<DoctorPointer>();
            Check(panel != null && speaker != null && pointer != null, "the test scene has the head doctor, her pointer and the test panel");
            if (panel == null || speaker == null)
                yield break;
            yield return 1f;
            var missing = panel.Lines.Where(l => l.Clip == null).Select(l => l.Key).ToList();
            Check(panel.Lines.Count >= 60 && missing.Count == 0, $"every line has a voice clip ({panel.Lines.Count} lines, missing: {string.Join(", ", missing)})");
            var face = speaker.Face;
            Check(face != null && speaker.Look != null, "the head doctor has a face and a head that looks");

            // Every line plays: a few in full (mouth, subtitle), all the others started and skipped with A.
            var played = 0;
            for (var i = 0; i < panel.Lines.Count; i++)
            {
                var line = panel.Lines[i];
                speaker.StartCoroutine(speaker.Say(line));
                var started = false;
                var maxMouth = 0f;
                var frames = 0;
                var full = i < 3;
                var startFrame = Time.frameCount;
                var startTime = Time.time;
                do
                {
                    started |= speaker.Voice.isPlaying && speaker.Voice.clip == line.Clip;
                    if (face != null)
                        maxMouth = Mathf.Max(maxMouth, face.TalkLevel);
                    frames = Time.frameCount - startFrame;
                    if (frames >= 4 && i == 0 && !_subtitleChecked)
                    {
                        _subtitleChecked = true;
                        var subtitle = speaker.Subtitle;
                        var label = subtitle != null ? subtitle.GetComponentInChildren<TMPro.TMP_Text>() : null;
                        Check(subtitle != null && subtitle.gameObject.activeSelf && label != null && label.text == line.Text,
                            $"the subtitle shows the line ({label?.text})");
                        var distance = subtitle != null ? Vector3.Distance(subtitle.transform.position, Camera.main.transform.position) : 0f;
                        Check(distance > 0.8f && distance < 3f, $"the subtitle hangs at reading distance ({distance:0.00} m)");
                        Screenshot("debrief-doctor-talks");
                    }
                    if (!full && frames >= 6 && Time.time - startTime > 0.3f && speaker.IsSpeaking)
                        DebriefInput.Press(true); // A skips the line
                    yield return null;
                } while (speaker.IsSpeaking || frames < 2);
                if (started)
                    played++;
                if (full)
                    Check(maxMouth > 0.05f, $"her mouth moves with the voice ({line.Key}: {maxMouth:0.00})");
                else if (i == 3)
                    Check(Time.time - startTime < line.Clip.length - 0.3f, $"A skips a line ({line.Key} ended after {Time.time - startTime:0.0} s, clip {line.Clip.length:0.0} s)");
                yield return 0.4f; // the pause after the line
            }
            Check(played == panel.Lines.Count, $"every clip played ({played} / {panel.Lines.Count})");
            Check(!speaker.IsSpeaking, "after the line she is quiet");

            // Pointing at the whiteboard: her arm turns to it.
            var board = GameObject.Find("Whiteboard Front").transform;
            pointer.PointAt(board);
            yield return 1f;
            var animator = speaker.GetComponentInChildren<Animator>();
            var right = Vector3.Dot(board.position - speaker.transform.position, speaker.transform.right) >= 0f;
            var upper = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            var hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var angle = Vector3.Angle(hand.position - upper.position, board.position - upper.position);
            Check(pointer.Weight > 0.99f && angle < 25f, $"she points at the whiteboard (arm {angle:0} degrees from it)");
            Screenshot("debrief-doctor-points");
            pointer.Stop();
            yield return 0.6f;
            Check(pointer.Weight <= 0f, "she lowers her arm again");
        }

        // ---- Prototype1 with a fake finished conversation ---------------------------------------------------------------

        private static IEnumerator DebriefFlowTest(DebriefPart part)
        {
            DoctorSpeaker.TestLineSeconds = 0.35f;
            var flow = Object.FindAnyObjectByType<PrototypeFlow>();
            var panel = Object.FindAnyObjectByType<AiDebriefPanel>();
            var director = Object.FindAnyObjectByType<DebriefDirector>();
            var recorder = Object.FindAnyObjectByType<ConversationRecorder>();
            var npc = Object.FindAnyObjectByType<AiNpc>();
            var holder = npc != null ? npc.GetComponent<EmotionHolder>() : null;
            var origin = Object.FindAnyObjectByType<XROrigin>();
            var pov = Object.FindAnyObjectByType<PovController>();
            Check(flow != null && panel != null && director != null && recorder != null && npc != null && holder != null && origin != null,
                "Prototype1 has the flow, the panel, the debrief director, the recorder and the patient");
            if (Errors.Count > 0)
                yield break;
            Check(panel.DoctorLed && panel.Director == director, "Liam's debrief panel is in Doctor Led mode in Prototype1");
            var serializedFlow = new SerializedObject(flow);
            var doctorSpot = (Transform)serializedFlow.FindProperty("_doctorSpot").objectReferenceValue;
            var patientSpot = (Transform)serializedFlow.FindProperty("_patientSpot").objectReferenceValue;
            yield return 1.5f;

            // A finished conversation: 5 recorded turns (1 s test tones), the critical error in turn 3.
            var critical = part != DebriefPart.Reflection;
            var turns = FakeTurns(npc.transform, patientSpot);
            foreach (var turn in turns)
                recorder.AddForTest(turn);
            Check(recorder.Turns.Count == 5, $"the recorder keeps the 5 turns ({recorder.Turns.Count})");
            var data = DebriefData.Build(FakeDebrief(critical), recorder.Turns, Emotion.Sadness, "disappointed_patient");
            var retried = 0;
            var closed = 0;
            panel.Retried += () => retried++;
            panel.Closed += () => closed++;

            // At the doctor (as PrototypeFlow brings the player there).
            flow.Teleport(doctorSpot);
            yield return 0.3f;
            var doctorHome = director.Doctor.transform.position;
            AddSimulatedDevices();
            _headWorld = origin.Camera.transform.position;
            _headRotation = Quaternion.LookRotation(Flat(origin.Camera.transform.forward));
            _lastOriginPosition = origin.transform.position;
            EditorApplication.update += UpdateDevices;
            try
            {
                panel.BeginDoctorLed(new FixedDebriefSource(data));
                Check(panel.State == AiDebriefPanel.PanelState.Ready && panel.DirectorActive, "the panel hands the debrief to the doctor");
                foreach (var step in WaitFor(() => director.CurrentStep == DebriefStepKind.ReflectionQuestion && director.ReflectionProps.Count == 4
                                                   && !director.Doctor.IsSpeaking && director.Doctor.LinesSaid >= 2, 30f))
                    yield return step;
                Check(director.Greeted, "the head doctor greeted the player with her voice");
                var canvas = panel.GetComponentInChildren<Canvas>(true);
                Check(canvas == null || !canvas.gameObject.activeSelf, "no report panel is shown");
                var camera = origin.Camera.transform;
                Check(Vector3.Distance(Flat(camera.position), Flat(director.PlayerTableSpot.position)) < 0.35f,
                    $"the player stands at the table ({camera.position} vs {director.PlayerTableSpot.position})");
                var toDoctor = Flat(director.Doctor.transform.position - camera.position);
                Check(toDoctor.magnitude < 4.2f && Vector3.Angle(Flat(camera.forward), toDoctor) < 50f,
                    $"the doctor stands across the table, in view ({toDoctor.magnitude:0.0} m, {Vector3.Angle(Flat(camera.forward), toDoctor):0} degrees)");
                Check(GameObject.Find("Doctor Bubble") == null, "the flow's doctor bubble is hidden: she speaks herself");
                foreach (var prop in director.ReflectionProps)
                {
                    var interactable = prop.GetComponent<XRSimpleInteractable>();
                    var onTable = prop.transform.position.y - director.PlayerTableSpot.position.y;
                    Check(interactable != null && interactable.isActiveAndEnabled && prop.GetComponent<Collider>() != null && onTable > 0.6f && onTable < 0.9f,
                        $"the {prop.Emotion} object is on the table and selectable ({onTable:0.00} m)");
                }
                Screenshot("debrief-reflection-question");

                // Laser + trigger on the flame (Anger).
                var flame = director.ReflectionProps.First(p => p.Emotion == Emotion.Anger);
                _headRotation = Quaternion.LookRotation(flame.transform.position - _headWorld);
                var flameCenter = flame.GetComponent<Collider>().bounds.center;
                foreach (var step in Click(flameCenter, () => director.HasPicked))
                    yield return step;
                Check(director.HasPicked && director.PickedEmotion == Emotion.Anger,
                    $"laser + trigger picks the flame ({(director.HasPicked ? director.PickedEmotion.ToString() : "nothing")})");
                if (!director.HasPicked)
                    director.PickReflection(Emotion.Anger);
                foreach (var step in WaitFor(() => director.RevealedProp != null, 10f))
                    yield return step;
                Check(director.RevealedProp != null && director.RevealedProp.Emotion == Emotion.Sadness && director.RevealedProp.IsGlowing,
                    "the reveal: the rain cloud (his real emotion, Sadness) glows");
                foreach (var step in WaitFor(() => director.Doctor.CurrentKey.StartsWith("reveal_"), 5f))
                    yield return step;
                Check(director.Doctor.CurrentKey == "reveal_Anger_Sadness", $"she reacts to the pick ({director.Doctor.CurrentKey}: {director.Doctor.CurrentText})");
                Screenshot("debrief-reveal");
                _headRotation = Quaternion.LookRotation(Flat(camera.forward));

                if (part == DebriefPart.Reflection)
                {
                    panel.Done();
                    yield return 0.5f;
                    Check(closed == 1 && !director.Running && director.ReflectionProps.Count == 0, "Done from outside stops the doctor and cleans up");
                    yield break;
                }

                // ---- The timeline on the table ----
                foreach (var step in WaitFor(() => director.CurrentStep == DebriefStepKind.Feedback, 20f))
                    yield return step;
                var timeline = director.Timeline;
                Check(timeline.Props.Count == 5, $"5 objects on the table, one per turn ({timeline.Props.Count})");
                for (var i = 0; i < timeline.Props.Count && i < turns.Count; i++)
                {
                    var expected = turns[i].Dominant;
                    var height = timeline.Posts[i].localScale.y * 2f;
                    var wanted = EmotionTimeline.PostHeightFor(turns[i].Trust, timeline.PostHeightRange);
                    Check(timeline.Props[i].Emotion == expected && Mathf.Abs(height - wanted) < 0.002f,
                        $"turn {turns[i].Index}: {EmotionProp.NameOf(expected)} on a post of {height * 100f:0} cm (trust {turns[i].Trust})");
                }
                var leftToRight = Vector3.Dot(timeline.Props[4].transform.position - timeline.Props[0].transform.position, camera.right) > 0f;
                Check(leftToRight, "the conversation reads left to right for the player");
                var criticalProp = timeline.PropFor(3);
                Check(criticalProp != null && criticalProp.transform.Find("Ring").gameObject.activeSelf, "the critical turn has a (red) ring");
                Screenshot("debrief-timeline");
                // pointing at an object shows what the student said then
                var hovered = timeline.Props[1];
                _headRotation = Quaternion.LookRotation(hovered.transform.position - _headWorld);
                _aimTarget = hovered.GetComponent<Collider>().bounds.center;
                foreach (var step in WaitFor(() => hovered.IsHovered, 3f))
                    yield return step;
                Check(hovered.IsHovered && hovered.LabelVisible && hovered.LabelText.Contains("sentence two"),
                    $"pointing at a turn shows what you said ({hovered.LabelText})");
                Screenshot("debrief-timeline-hover");
                _aimTarget = Vector3.zero;
                _headRotation = Quaternion.LookRotation(Flat(camera.forward));

                // ---- Feedback: 3 points with sticky notes ----
                foreach (var step in WaitFor(() => director.CurrentStep == DebriefStepKind.Memory, 40f))
                    yield return step;
                var notes = director.BoardVisuals.NoteTexts;
                Check(director.FeedbackPoints == 3 && notes.Count == 3, $"the doctor goes through 3 points with a note each ({notes.Count})");
                Check(notes.Count == 3 && notes[0].text.Contains("Never promise") && notes[0].text.Contains("\""),
                    $"note 1: the critical error with a better phrase in quotes ({(notes.Count > 0 ? notes[0].text : "")})");
                Check(notes.Count == 3 && notes[1].text.Contains("small step") && notes[2].text.Contains("named his sadness"),
                    $"note 2 the lowest item, note 3 the praise ({(notes.Count > 2 ? notes[1].text + " | " + notes[2].text : "")})");
                var boardFront = director.BoardVisuals.Notes.Count > 0 ? director.BoardVisuals.Notes[0].transform.position : Vector3.zero;
                var boardDistance = Vector3.Distance(camera.position, boardFront);
                Check(boardDistance < 6f, $"the notes are on the whiteboard in front of the player ({boardDistance:0.0} m)");
                Screenshot("debrief-notes");

                if (part == DebriefPart.Memory)
                {
                    foreach (var step in MemoryChecks(director, holder, origin, pov, npc, turns))
                        yield return step;
                }
                else
                {
                    foreach (var step in WaitFor(() => director.CurrentStep != DebriefStepKind.Memory, 60f))
                        yield return step;
                }

                // ---- Consequence (critical), board, choice ----
                foreach (var step in WaitFor(() => director.Consequence.IsShown, 10f))
                    yield return step;
                Check(director.Consequence.IsShown && director.Consequence.ShownOutcome == DebriefOutcome.Critical,
                    "the closing card shows the critical outcome (an empty chair)");
                Screenshot("debrief-consequence");
                DebriefInput.Press(false); // B skips it
                foreach (var step in WaitFor(() => director.ChoiceOpen, 30f))
                    yield return step;
                Check(director.BoardVisuals.StampText == $"{data.TotalScore} / {data.MaxScore}" && data.TotalScore == 6 && data.MaxScore == 14,
                    $"the stamp shows the same score as the debrief ({director.BoardVisuals.StampText})");
                Check(director.ChoiceBubbles.Count == 2, "two choices: try again and finish");
                Screenshot("debrief-board-choice");

                // On demand: pick a moment on the table, its memory plays, then the choice again.
                if (part == DebriefPart.Feedback)
                {
                    var played = director.Memory.Played;
                    director.RequestMemory(2);
                    foreach (var step in WaitFor(() => director.Memory.IsPlaying, 10f))
                        yield return step;
                    Check(director.Memory.IsPlaying && director.Memory.CurrentTurn?.Index == 2, "picking turn 2 on the table plays its memory");
                    yield return 0.5f;
                    DebriefInput.Press(false); // B skips the memory
                    foreach (var step in WaitFor(() => !director.Memory.IsPlaying && director.ChoiceOpen, 20f))
                        yield return step;
                    Check(director.Memory.Played == played + 1 && director.ChoiceOpen, "B skips the memory, back to the choice");
                }

                // Try again (A): the panel's Retried, a new conversation clears the recorder.
                DebriefInput.Press(true);
                foreach (var step in WaitFor(() => retried > 0, 10f))
                    yield return step;
                Check(retried == 1 && closed == 0 && !director.Running && !panel.DirectorActive, "A: try again ends the debrief with Retried");
                Check(director.BoardVisuals.Notes.Count == 0 && director.Timeline.Props.Count == 0 && director.ReflectionProps.Count == 0,
                    "the table and the board are cleared");
                Check(Vector3.Distance(director.Doctor.transform.position, doctorHome) < 0.01f, "the doctor is back at her place by the whiteboard");
                foreach (var step in WaitFor(() => recorder.Turns.Count == 0, 10f))
                    yield return step;
                Check(recorder.Turns.Count == 0, "Try again starts a new conversation: the recorder is empty");

                // A second debrief, a good one, ends with Done (B).
                foreach (var turn in FakeTurns(npc.transform, patientSpot))
                    recorder.AddForTest(turn);
                var good = FakeDebrief(false);
                good.total_score = 12;
                var goodData = DebriefData.Build(good, recorder.Turns, Emotion.Joy, "disappointed_patient");
                flow.Teleport(doctorSpot);
                panel.BeginDoctorLed(new FixedDebriefSource(goodData));
                foreach (var step in WaitFor(() => director.CurrentStep == DebriefStepKind.ReflectionQuestion && director.ReflectionProps.Count == 4, 20f))
                    yield return step;
                director.PickReflection(Emotion.Joy);
                foreach (var step in WaitFor(() => director.Consequence.IsShown || director.ChoiceOpen, 120f))
                    yield return step;
                Check(director.Consequence.ShownOutcome == DebriefOutcome.Good, $"a good talk shows the video call ({director.Consequence.ShownOutcome})");
                foreach (var step in WaitFor(() => director.ChoiceOpen, 60f))
                    yield return step;
                Check(director.Data.Outcome(director.Script.GoodFraction) == DebriefOutcome.Good && director.Doctor.LinesSaid > 10, "the good debrief reaches the choice");
                DebriefInput.Press(false);
                foreach (var step in WaitFor(() => closed > 0, 10f))
                    yield return step;
                Check(closed == 1 && flow.Current == PrototypeFlow.Stage.Finished, $"B: finish ends the debrief with Closed ({flow.Current})");
            }
            finally
            {
                EditorApplication.update -= UpdateDevices;
                RemoveSimulatedDevices();
                DoctorSpeaker.TestLineSeconds = 0f;
            }
        }

        private static IEnumerable MemoryChecks(DebriefDirector director, EmotionHolder holder, XROrigin origin, PovController pov,
            AiNpc npc, List<RecordedTurn> turns)
        {
            var memory = director.Memory;
            var before = holder.Target;
            var tableSpot = director.PlayerTableSpot.position;
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            // idle at the table: the allocations of the editor and the test itself, as a baseline
            var idle = new List<long>();
            while (!memory.IsPlaying && idle.Count < 400)
            {
                if (gc.Valid && gc.LastValue >= 0)
                    idle.Add(gc.LastValue);
                yield return null;
            }
            foreach (var step in WaitFor(() => memory.IsPlaying && memory.VolumeWeight > 0.95f, 15f))
                yield return step;
            // the simulated head looks where a player would: between the white figure and the patient
            var lookAt = memory.Ghost != null ? (memory.Ghost.transform.position + npc.transform.position) / 2f : npc.transform.position;
            _headRotation = Quaternion.LookRotation(Flat(lookAt - origin.Camera.transform.position));
            yield return 0.3f;
            Check(memory.IsPlaying && memory.CurrentTurn != null && memory.CurrentTurn.Index == 3,
                $"the memory of the critical moment plays (turn {memory.CurrentTurn?.Index})");
            var camera = origin.Camera.transform;
            var toPatient = Flat(npc.transform.position - camera.position).magnitude;
            Check(toPatient < 4.5f, $"the player is in room 1A, next to the patient ({toPatient:0.0} m)");
            Check(memory.Volume != null && memory.Volume.enabled && memory.VolumeWeight > 0.95f, "the memory look is on (Volume weight 1)");
            var ghost = memory.Ghost;
            var ghostRenderers = ghost != null ? ghost.GetComponentsInChildren<Renderer>() : Array.Empty<Renderer>();
            var white = AssetDatabase.LoadAssetAtPath<Material>(DebriefAssets.GhostMaterialPath);
            Check(ghost != null && ghostRenderers.Length > 0 && ghostRenderers.All(r => r.enabled && r.sharedMaterials.All(m => m == white)),
                $"a white figure of the student stands where the student stood ({ghostRenderers.Length} renderers)");
            var ghostToRecorded = ghost != null ? Flat(ghost.transform.position - npc.transform.TransformPoint(turns[2].HeadLocalPosition)).magnitude : 99f;
            Check(ghostToRecorded < 0.05f, $"the figure stands on the recorded spot ({ghostToRecorded:0.00} m)");
            var toGhost = ghost != null ? Flat(ghost.transform.position - camera.position).magnitude : 0f;
            Check(toGhost > 0.6f && toGhost < 1.6f, $"the player stands next to the figure ({toGhost:0.00} m)");
            var toDoctor = Flat(director.Doctor.transform.position - camera.position).magnitude;
            Check(toDoctor < 1.6f, $"the doctor stands next to the player ({toDoctor:0.00} m)");
            Check(holder.Target.Approximately(turns[2].Emotion, 0.01f), $"the patient's emotion is the one of then ({holder.Target})");
            Check(pov == null || pov.WhyNot() != "", $"Y does nothing in the memory ({pov?.WhyNot()})");
            Check(!npc.Busy, "the patient does not start a new turn");
            Screenshot("debrief-memory");
            var during = new List<long>();
            foreach (var step in WaitFor(() =>
                     {
                         if (gc.Valid && gc.LastValue >= 0)
                             during.Add(gc.LastValue);
                         return memory.StudentClipPlayed && memory.PatientClipPlayed;
                     }, 20f))
                yield return step;
            Check(memory.StudentClipPlayed, "the student's own recorded voice plays from the figure");
            Check(memory.PatientClipPlayed, "then the patient answers with his real reply");
            Screenshot("debrief-memory-patient");
            foreach (var step in WaitFor(() => !memory.IsPlaying, 30f))
                yield return step;
            yield return 0.8f;
            Check(!memory.IsPlaying && memory.VolumeWeight == 0f && !memory.Volume.enabled, "back: the memory look is off (Volume weight 0)");
            Check(memory.Ghost == null && GameObject.Find("Memory Ghost (you)") == null, "the figure is gone");
            Check(holder.Target.Approximately(before, 0.01f), $"the patient's emotion is restored ({holder.Target} = {before})");
            Check(Vector3.Distance(Flat(camera.position), Flat(tableSpot)) < 0.35f, "the player is back at the table");
            _headRotation = Quaternion.LookRotation(Flat(director.PlayerTableSpot.forward));
            long Median(List<long> list) => list.Count == 0 ? -1 : list.OrderBy(v => v).ElementAt(list.Count / 2);
            Log($"GC allocated per frame: idle at the table median {Median(idle)} B ({idle.Count} frames), during the memory median {Median(during)} B ({during.Count} frames).");
            Check(Median(during) <= Math.Max(0, Median(idle)) + 512, "the memory adds no garbage per frame (median within 512 B of idle, editor and test included)");
        }

        // ---- The man with the infected leg in Prototype1 (no AI server needed) ---------------------------------------

        private static IEnumerator Scenario1PatientTest()
        {
            var npc = Object.FindAnyObjectByType<AiNpc>();
            var flow = Object.FindAnyObjectByType<PrototypeFlow>();
            Check(npc != null && flow != null && Object.FindObjectsByType<AiNpc>(FindObjectsSortMode.None).Length == 1, "one AI patient in Prototype1");
            if (npc == null)
                yield break;
            var patient = npc.gameObject;
            var driver = patient.GetComponentInChildren<ProCommVR.Hospital.PatientPov.EmotionFaceDriver>();
            var holder = patient.GetComponent<EmotionHolder>();
            var animator = patient.GetComponent<Animator>();
            Check(patient.name == "Patient_1A" && patient.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Bandage")),
                "Patient_1A is the man with the bandaged (infected) leg");
            Check(driver != null && npc.FaceFollowsEmotions && driver.Holder == holder, "his face follows the same EmotionHolder as the room");
            Check(patient.GetComponent<AiPatientBridge>() != null && patient.GetComponent<PatientReputation>() != null
                  && patient.GetComponent<ProCommVR.Pov.PovAnchor>() != null && patient.GetComponent<ConversationRecorder>() != null,
                "the AI bridge, the trust, the patient view anchor and the recorder moved to him");
            var bridge = patient.GetComponent<AiPatientBridge>();
            var bridgeNpc = new SerializedObject(bridge).FindProperty("_npc").objectReferenceValue;
            var pov = Object.FindAnyObjectByType<PovController>();
            var povPatient = new SerializedObject(pov).FindProperty("_conversationSource").objectReferenceValue;
            Check(bridgeNpc == npc && povPatient == bridge, "the references point to the new patient (bridge, POV)");
            yield return 2.5f;
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            var head = animator.GetBoneTransform(HumanBodyBones.Head).position;
            var bed = GameObject.Find("Room_1A").transform.Find("Furniture/Bed");
            var bedBounds = bed.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            Check(hips.y > bedBounds.max.y - 0.35f && hips.y < bedBounds.max.y + 0.35f && Mathf.Abs(head.y - hips.y) < 0.5f
                  && bedBounds.Contains(new Vector3(hips.x, bedBounds.center.y, hips.z)),
                $"he lies in the bed (hips {hips.y:0.00} m, head {head.y:0.00} m, bed top {bedBounds.max.y:0.00} m)");
            var story = Object.FindAnyObjectByType<ProCommVR.Hospital.PatientPov.StoryProps>();
            Check(story != null && GameObject.Find("Birthday Card") != null && GameObject.Find("Photo Frame") != null && GameObject.Find("Balloon") != null,
                "the granddaughter's card, photo and balloon are in his room");
            holder.SetImmediate(EmotionState.Zero.With(Emotion.Sadness, 0.7f));
            yield return 1f;
            Check(driver.GetMix(ProCommVR.Hospital.Face.FaceExpression.Sad) + driver.GetMix(ProCommVR.Hospital.Face.FaceExpression.Crying) > 0.3f,
                $"sad in the room is sad on his face (Sad {driver.GetMix(ProCommVR.Hospital.Face.FaceExpression.Sad):0.00}, Crying {driver.GetMix(ProCommVR.Hospital.Face.FaceExpression.Crying):0.00})");
            holder.SetImmediate(EmotionState.Zero.With(Emotion.Anger, 0.7f));
            yield return 1f;
            Check(driver.GetMix(ProCommVR.Hospital.Face.FaceExpression.Angry) > 0.3f, "angry in the room is angry on his face");
            var spot = (Transform)new SerializedObject(flow).FindProperty("_patientSpot").objectReferenceValue;
            flow.Teleport(spot);
            yield return 0.5f;
            Screenshot("scenario1-patient-angry");
            holder.SetImmediate(EmotionState.Zero.With(Emotion.Sadness, 0.7f));
            yield return 1f;
            Screenshot("scenario1-patient-sad");
        }

        // ---- Live, with the AI server -----------------------------------------------------------------------------------

        private static IEnumerator DebriefLiveTest()
        {
            var flow = Object.FindAnyObjectByType<PrototypeFlow>();
            var panel = Object.FindAnyObjectByType<AiDebriefPanel>();
            var director = Object.FindAnyObjectByType<DebriefDirector>();
            var recorder = Object.FindAnyObjectByType<ConversationRecorder>();
            var npc = Object.FindAnyObjectByType<AiNpc>();
            var shift = Object.FindAnyObjectByType<AiShift>();
            var start = Object.FindAnyObjectByType<AiShiftStartButton>();
            var pager = Object.FindAnyObjectByType<PagerCue>();
            Check(flow && panel && director && recorder && npc && shift && start && pager, "Prototype1 has the flow, the debrief, the recorder and the pager");
            if (Errors.Count > 0)
                yield break;
            var patientSpot = (Transform)new SerializedObject(flow).FindProperty("_patientSpot").objectReferenceValue;
            DoctorSpeaker.TestLineSeconds = 0.35f;
            try
            {
                start.Press();
                flow.Teleport(patientSpot);
                foreach (var step in WaitFor(() => flow.Current == PrototypeFlow.Stage.Talking && npc.ConversationSeconds > 0f && !npc.Busy, 180f))
                    yield return step;
                Check(npc.ConversationSeconds > 0f, "the AI conversation started (is the AI server running?)");
                if (npc.ConversationSeconds <= 0f)
                    yield break;
                while (npc.IsSpeaking)
                    yield return null;
                // Three spoken turns (Piper test sentences in Tools/AiServer/tests/audio, made by tools/gen_test_sentences.py).
                var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Tools", "AiServer", "tests", "audio"));
                var files = System.IO.Directory.Exists(folder) ? System.IO.Directory.GetFiles(folder, "debrief_*.wav").OrderBy(f => f).ToArray() : Array.Empty<string>();
                Check(files.Length >= 3, $"three recorded test sentences ({files.Length} in {folder})");
                foreach (var file in files.Take(3))
                {
                    _ = npc.SendStudentAudio(System.IO.File.ReadAllBytes(file));
                    foreach (var step in WaitFor(() => !npc.Busy, 90f))
                        yield return step;
                    while (npc.IsSpeaking)
                        yield return null;
                    yield return 0.3f;
                }
                Check(recorder.Turns.Count == 3 && recorder.Turns.All(t => t.StudentClip != null && t.PatientClip != null && !string.IsNullOrEmpty(t.StudentText)),
                    $"one recorded turn per spoken sentence, with both clips ({recorder.Turns.Count}: " +
                    string.Join(" | ", recorder.Turns.Select(t => $"{t.Index} '{t.StudentText}' {t.StudentClip != null}/{t.PatientClip != null} {t.Dominant}")) + ")");

                // Finish: the pager, then the doctor.
                flow.FinishConversation();
                foreach (var step in WaitFor(() => pager.IsShown, 5f))
                    yield return step;
                Check(pager.IsShown && pager.Text.Contains("Briefing room") && pager.BeepPlayed, $"the pager beeps and shows '{pager.Text}'");
                foreach (var step in WaitFor(() => pager.Pulses >= 3, 3f))
                    yield return step;
                Check(pager.Pulses == 3, "the pager vibrates 3 times");
                foreach (var step in WaitFor(() => pager.FootstepsPlayed, 5f))
                    yield return step;
                Check(pager.FootstepsPlayed, "footsteps during the fade");
                foreach (var step in WaitFor(() => director.Running && director.CurrentStep == DebriefStepKind.ReflectionQuestion && director.ReflectionProps.Count == 4, 120f))
                    yield return step;
                Check(director.Running && panel.Result != null && !panel.Result.Failed, $"the real debrief goes to the doctor ({panel.Result?.error})");
                Check(director.Greeted, "the doctor greeted on arrival");
                Check(panel.Result != null && panel.Result.items.All(i => !string.IsNullOrEmpty(i.feedback_en)), "the server sends English feedback for every item");
                Check(director.Data.Timeline.Count == 3 && director.Data.Items.All(i => i.FeedbackIsEnglish), "the doctor uses the 3 recorded turns and the English feedback");
                Check(director.Data.TotalScore == panel.Result.total_score && director.Data.MaxScore == panel.Result.max_score, "the score is the server's score");
                director.PickReflection(Emotion.Sadness);
                foreach (var step in WaitFor(() => director.ChoiceOpen, 240f))
                    yield return step;
                Check(director.ChoiceOpen && director.BoardVisuals.StampText == $"{panel.Result.total_score} / {panel.Result.max_score}",
                    $"the stamp shows the server's score ({director.BoardVisuals.StampText})");
                Check(shift.Stage == AiShift.ShiftStage.Report, $"the shift recorded the task ({shift.Stage})");
                Check(GameObject.Find("Shift Report") != null, "the shift report is shown at the doctor (as before)");
                Screenshot("debrief-live-choice");
                DebriefInput.Press(false);
                foreach (var step in WaitFor(() => flow.Current == PrototypeFlow.Stage.Finished, 20f))
                    yield return step;
                Check(flow.Current == PrototypeFlow.Stage.Finished && shift.Stage == AiShift.ShiftStage.Finished, "Finish shift ends the prototype as before");
            }
            finally
            {
                DoctorSpeaker.TestLineSeconds = 0f;
            }
        }

        // ---- Helpers ----------------------------------------------------------------------------------------------------

        private static IEnumerable WaitFor(Func<bool> condition, float seconds)
        {
            var until = EditorApplication.timeSinceStartup + seconds;
            while (!condition() && EditorApplication.timeSinceStartup < until)
                yield return null;
        }

        private static readonly string[] Words = { "one", "two", "three", "four", "five" };

        /// <summary>5 turns: sad, sad, angry (the critical error), calm, sad; trust 25, 30, 18, 40, 45; test tones as voices.</summary>
        private static List<RecordedTurn> FakeTurns(Transform patient, Transform spot)
        {
            var emotions = new Emotion?[] { Emotion.Sadness, Emotion.Sadness, Emotion.Anger, null, Emotion.Sadness };
            var faces = new[] { "Sad", "Sad", "Angry", "Neutral", "Sad" };
            var trust = new[] { 25f, 30f, 18f, 40f, 45f };
            var list = new List<RecordedTurn>();
            for (var i = 0; i < 5; i++)
            {
                var head = spot.position + Vector3.up * 1.6f + spot.right * (0.1f * i - 0.2f);
                var forward = patient.InverseTransformDirection(spot.forward);
                list.Add(new RecordedTurn
                {
                    Index = i + 1,
                    StudentText = $"Test sentence {Words[i]} of the student.",
                    PatientText = $"Reply {Words[i]} of the patient.",
                    StudentClip = Tone($"student_{i + 1}", 1f, 330f),
                    PatientClip = Tone($"patient_{i + 1}", 1f, 180f),
                    AiEmotion = faces[i],
                    Emotion = emotions[i].HasValue ? EmotionState.Zero.With(emotions[i].Value, 0.6f) : EmotionState.Zero,
                    Trust = trust[i],
                    Anger = 50f,
                    AngerChange = i == 2 ? 12f : -3f,
                    HeadLocalPosition = patient.InverseTransformPoint(head),
                    HeadLocalYaw = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.z)).eulerAngles.y,
                    HasPose = true,
                    Time = 10f * i,
                });
            }
            return list;
        }

        private static AudioClip Tone(string name, float seconds, float frequency)
        {
            const int rate = 16000;
            var data = new float[(int)(rate * seconds)];
            for (var i = 0; i < data.Length; i++)
                data[i] = 0.2f * Mathf.Sin(2f * Mathf.PI * frequency * i / rate) * Mathf.Min(1f, Mathf.Min(i, data.Length - i) / 800f);
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>The debrief of scenario 1 with the real texts: 6 / 14, critical in turn 3 (or none).</summary>
        private static AiDebrief FakeDebrief(bool critical)
        {
            var scenario = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", "Tools", "AiServer", "scenarios", "disappointed_patient.json"));
            var feedback = new Dictionary<int, string[]>();
            foreach (var id in new[] { 1, 2, 3, 4, 5, 6, 7 })
                feedback[id] = new[] { "0", "1", "2" }.Select(s => FeedbackEn(scenario, id, s)).ToArray();
            string Better(int id) => AssetDatabase.LoadAssetAtPath<DebriefScript>(DebriefAssets.ScriptPath).Voices.Find($"s1_item{id}_better")?.Text ?? "";
            var scores = critical ? new[] { 2, 0, 1, 0, 1, 1, 1 } : new[] { 2, 2, 1, 0, 1, 0, 0 };
            var items = new AiDebriefItem[7];
            for (var i = 0; i < 7; i++)
                items[i] = new AiDebriefItem
                {
                    id = i + 1, criterion = $"criterion {i + 1}", score = scores[i],
                    evidence_quote = i == 0 ? "I can see you're sad" : "", evidence_turn = i == 0 ? 2 : i == 1 && critical ? 3 : 0,
                    feedback = "nl", feedback_en = feedback[i + 1][scores[i]], better_phrase = Better(i + 1),
                };
            var d = new AiDebrief
            {
                end_reason = critical ? "critical_error" : "resolved",
                total_score = 6, max_score = 14, items = items,
                trust_curve = new[] { 20, 25, 30, 18, 40, 45 }, anger_curve = new[] { 60, 57, 54, 66, 63, 60 },
                key_moments = new[]
                {
                    new AiKeyMoment { turn_index = 1, what_happened = "a", effect = "anger -3" },
                    new AiKeyMoment { turn_index = 3, what_happened = "b", effect = "anger +12" },
                    new AiKeyMoment { turn_index = 5, what_happened = "c", effect = "anger -3" },
                },
            };
            if (critical)
            {
                var library = AssetDatabase.LoadAssetAtPath<DebriefScript>(DebriefAssets.ScriptPath).Voices;
                d.critical = new AiCritical
                {
                    label = "false_discharge_promise", turn = 3, quote = "you can go home for the party", title = "Vals ontslag beloofd",
                    title_en = "False promise of discharge", feedback = "nl",
                    feedback_en = library.Find("s1_critical_false_discharge_promise")?.Text ?? "",
                };
            }
            return d;
        }

        private static string FeedbackEn(string scenario, int id, string score)
        {
            // The scenario file is the source of the English feedback (as the server sends it).
            var itemStart = scenario.IndexOf($"\"id\": {id},", StringComparison.Ordinal);
            var en = scenario.IndexOf("\"feedback_en\"", itemStart, StringComparison.Ordinal);
            var key = scenario.IndexOf($"\"{score}\":", en, StringComparison.Ordinal);
            var open = scenario.IndexOf('"', key + score.Length + 3);
            var close = scenario.IndexOf('"', open + 1);
            return scenario.Substring(open + 1, close - open - 1);
        }
    }
}
