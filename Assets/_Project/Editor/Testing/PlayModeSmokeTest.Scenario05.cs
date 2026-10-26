using System.Collections;
using System.Linq;
using ProCommVR.Scenarios;
using ProCommVR.Shift;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// Scenario 5 in ShiftHospital: the verbally aggressive patient in the shared room 1C, with the patient of the other
    /// bed listening. Each test starts the shift in the lobby, rides the lift to floor 1, walks in (the patient comes at
    /// the nurse shouting, the other patient lowers his newspaper), chooses whether to draw the privacy curtain, answers
    /// and clicks "Finish". Then the shift timer is set to run out so the report can be checked. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario05Good  (curtain drawn, best answer: he agrees to the test)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario05Fail  (no curtain, diagnosis said out loud: the task fails, alarm)
    /// RunShiftLoop also plays Scenario 5 (curtain, best answer) as part of the whole shift.
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private const string Scenario05Id = "aggressive-patient-privacy";

        [MenuItem("ProCommVR/Tests/Scenario 05: Curtain, Good Answer")]
        public static void RunScenario05Good()
        {
            Launch("s5-good", ShiftScenePath);
        }

        [MenuItem("ProCommVR/Tests/Scenario 05: No Curtain, Privacy Breach")]
        public static void RunScenario05Fail()
        {
            Launch("s5-fail", ShiftScenePath);
        }

        private static IEnumerator Scenario05(Answer answer)
        {
            var curtain = answer == Answer.Best;
            return ScenarioInShift(Scenario05Id, (manager, origin, room) => CareForSharedRoom(manager, origin, room, curtain, answer), (body, room) =>
            {
                var scenario = room.Task.Scenario;
                if (answer == Answer.Best)
                {
                    Check(body.Contains($"{room.RoomName} - {room.Task.PatientName}") && body.Contains($"{scenario.BestScore} / {scenario.BestScore}"),
                        $"report: {room.RoomName} scored {scenario.BestScore} / {scenario.BestScore} (answer and curtain)");
                    Check(body.Contains("Talked in: Behind the privacy curtain"), "report: the curtain was drawn");
                }
                else
                {
                    Check(body.Contains("failed: not resolved") && room.ChosenOption != null && body.Contains(room.ChosenOption.Feedback),
                        "report: the task failed, with the feedback on the answer");
                    Check(body.Contains("Talked in: In the open room"), "report: the curtain was not drawn");
                    Check(body.Contains("Failed: 1"), "report subtitle counts 1 failed task");
                }
            });
        }

        /// <summary>
        /// Scenario 5 the way a player plays it: from the door of room 1C the patient comes at the nurse shouting and the
        /// patient of the other bed lowers his newspaper; the player draws the curtain (or not), answers (best, or the
        /// privacy breach) and clicks "Finish".
        /// </summary>
        private static IEnumerable CareForSharedRoom(ShiftManager manager, XROrigin origin, PatientRoom room, bool drawCurtain, Answer answer)
        {
            var scenario = room.Task.Scenario;
            var presenter = room.GetComponentInChildren<ScenarioPresenter>();
            var curtain = room.GetComponentInChildren<PrivacyCurtain>();
            var speech = room.GetComponentInChildren<BystanderSpeech>();
            var neighbour = speech.transform;
            var neighbourAttention = neighbour.GetComponentInChildren<NpcAttention>();
            var neighbourAnimator = neighbour.GetComponentInChildren<Animator>();
            var patientAttention = room.GetComponentsInChildren<NpcAttention>().First(a => a != neighbourAttention);
            var patient = patientAttention.transform;
            var patientAnimator = patient.GetComponentInChildren<Animator>();
            var reading = neighbour.Find("Newspaper (reading)").gameObject;
            var lowered = neighbour.Find("Newspaper (lowered)").gameObject;
            patientAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            neighbourAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var patientStart = patient.position;
            var curtainLength = curtain.transform.localScale.z;

            foreach (var step in RideLiftTo(origin, room.transform.position.y))
                yield return step;

            // In the corridor, before the door: nothing happens yet.
            Teleport(origin, room.transform.position - room.transform.forward * 1.2f, patient.position);
            yield return 0.6f;
            Check(!patientAttention.IsAttending && reading.activeSelf && !lowered.activeSelf, $"{room.RoomName}: before the player comes in the patient stands by his bed and the other patient reads");
            Check(!curtain.IsDrawn && curtainLength < 0.3f, $"{room.RoomName}: the privacy curtain is open ({curtainLength:F2})");
            Check(neighbourAnimator.GetCurrentAnimatorStateInfo(0).IsName("Laying"), $"{room.RoomName}: the other patient lies in his bed ({StateName(neighbourAnimator)})");
            var otherBed = RendererBounds(room.transform.Find("Furniture/Bed (next patient)").gameObject);
            var hips = neighbourAnimator.GetBoneTransform(HumanBodyBones.Hips).position;
            Check(otherBed.Contains(new Vector3(hips.x, otherBed.center.y, hips.z)) && hips.y > otherBed.min.y + 0.3f, $"{room.RoomName}: he is on the other bed (hips {hips - room.transform.position})");

            // In the doorway: he comes at the nurse shouting; the other patient lowers his newspaper and listens.
            Teleport(origin, room.transform.position + room.transform.forward * 0.3f, patient.position);
            var waitUntil = EditorApplication.timeSinceStartup + 3f;
            while (ClickableBubbles(room).Length < scenario.Locations.Count && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            yield return 1.2f;
            Check(patientAttention.IsAttending && Flat(patient.position - patientStart).magnitude > 0.3f, $"{room.RoomName}: the patient comes at the nurse ({Flat(patient.position - patientStart).magnitude:F2} m)");
            Check(patientAnimator.GetCurrentAnimatorStateInfo(0).IsName("Angry"), $"{room.RoomName}: he shouts ({StateName(patientAnimator)})");
            Check(NpcText(room).Contains(scenario.LocationLine), $"{room.RoomName}: \"{scenario.LocationLine}\"");
            Check(neighbourAttention.IsAttending && !reading.activeSelf && lowered.activeSelf, $"{room.RoomName}: the other patient lowers his newspaper and listens");
            var places = ClickableBubbles(room);
            Check(places.Length == scenario.Locations.Count, $"{room.RoomName}: {scenario.Locations.Count} choices: draw the curtain or not (got {places.Length})");
            _headRotation = Quaternion.LookRotation(patient.position + Vector3.up * 1.4f - _headWorld);
            yield return 0.3f;
            Screenshot("s5-shouting");
            if (places.Length == 0)
                yield break;

            var location = scenario.Locations.First(l => l.Id == (drawCurtain ? "curtain" : "open-room"));
            var place = places.FirstOrDefault(b => BubbleText(b) == location.ChoiceText);
            Check(place != null, $"{room.RoomName}: a bubble for '{location.Name}'");
            if (place == null)
                yield break;
            foreach (var step in Click(Center(place), () => room.ChosenLocation != null))
                yield return step;
            Check(room.ChosenLocation == location, $"{room.RoomName}: laser + trigger chooses '{location.Name}'");
            if (drawCurtain)
            {
                yield return 0.3f;
                Check(ClickableBubbles(room).Length == 0, $"{room.RoomName}: no options while the curtain is drawn");
                waitUntil = EditorApplication.timeSinceStartup + 4f;
                while (!curtain.IsDrawn && EditorApplication.timeSinceStartup < waitUntil)
                    yield return null;
                Check(curtain.IsDrawn && Mathf.Approximately(curtain.transform.localScale.z, 1f), $"{room.RoomName}: the curtain is drawn between the beds");
            }
            else
            {
                Check(!curtain.IsDrawn, $"{room.RoomName}: the curtain stays open");
            }

            waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (ClickableBubbles(room).Length < 4 && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            var options = ClickableBubbles(room);
            Check(options.Length == 4, $"{room.RoomName}: 4 options (got {options.Length})");
            Check(NpcText(room).Contains(scenario.NpcLine), $"{room.RoomName}: \"{scenario.NpcLine}\"");
            _headRotation = Quaternion.LookRotation(patient.position + Vector3.up * 1.2f - _headWorld);
            yield return 0.3f;
            Screenshot($"s5-options-{location.Id}");
            if (options.Length == 0)
                yield break;

            var chosen = answer == Answer.Best
                ? scenario.Options.OrderByDescending(o => o.Score).First()
                : scenario.Options.OrderBy(o => o.Score).First();
            var bubble = options.FirstOrDefault(b => BubbleText(b) == chosen.Text);
            Check(bubble != null, $"{room.RoomName}: a bubble for the {answer} option");
            if (bubble == null)
                yield break;
            foreach (var step in Click(Center(bubble), () => room.ChosenOption != null))
                yield return step;
            Check(room.ChosenOption == chosen, $"{room.RoomName}: laser + trigger picks the {answer} option (score {chosen.Score})");

            yield return 2.5f;
            Check(NpcText(room).Contains(chosen.NpcReaction), $"{room.RoomName}: the patient's reaction line");
            if (answer == Answer.Best)
            {
                Check(patientAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), $"{room.RoomName}: the patient calms down ({StateName(patientAnimator)})");
                Check(neighbourAnimator.GetCurrentAnimatorStateInfo(0).IsName("Laying") && speech.CurrentLine == "", $"{room.RoomName}: the other patient stays in bed and silent");
                CheckMood(room, Mood.Neutral);
            }
            else
            {
                Check(Flat(patient.position - room.transform.position).magnitude < 1.5f && patientAnimator.GetCurrentAnimatorStateInfo(0).IsName("Angry"),
                    $"{room.RoomName}: the patient heads for the door ({Flat(patient.position - room.transform.position).magnitude:F2} m from it)");
                Check(speech.HasReacted && speech.CurrentLine.Contains(chosen.BystanderReaction), $"{room.RoomName}: the other patient overheard it: \"{speech.CurrentLine}\"");
                Check(neighbourAnimator.GetCurrentAnimatorStateInfo(0).IsName("Sad") && neighbourAnimator.transform.position.y - room.transform.position.y < 0.05f,
                    $"{room.RoomName}: the other patient gets out of bed, upset ({StateName(neighbourAnimator)})");
                CheckMood(room, Mood.Alarm);
                CheckMoodSound(room);
                var light = RoomLights(room)[0];
                float low = float.MaxValue, high = 0f;
                for (var t = 0f; t < 1.5f; t += 0.05f)
                {
                    low = Mathf.Min(low, light.intensity);
                    high = Mathf.Max(high, light.intensity);
                    yield return 0.05f;
                }
                Check(high > 0f && low < high * 0.5f, $"{room.RoomName}: the red light flashes (intensity {low:F2} to {high:F2})");
            }
            _headRotation = Quaternion.LookRotation(Vector3.Lerp(patient.position, neighbour.position, 0.5f) + Vector3.up * 1.0f - _headWorld);
            yield return 0.3f;
            Screenshot($"s5-reaction-{answer.ToString().ToLowerInvariant()}");

            var finish = ClickableBubbles(room);
            Check(finish.Length == 1, $"{room.RoomName}: one Finish bubble after the choice");
            if (finish.Length == 0)
                yield break;
            var expected = answer == Answer.Best ? TaskOutcome.Completed : TaskOutcome.FailedAnswer;
            foreach (var step in Click(Center(finish[0]), () => room.Outcome != TaskOutcome.None))
                yield return step;
            Check(room.Outcome == expected && manager.GetOutcome(room.PatientId) == expected, $"{room.RoomName}: outcome {room.Outcome} (expected {expected})");
            // The room from the door, high up: both beds, the curtain between them and the other patient.
            _headWorld = room.transform.TransformPoint(0.35f, 2.0f, 0.2f);
            _headRotation = Quaternion.LookRotation(room.transform.TransformPoint(-0.3f, 0.6f, 2.3f) - _headWorld);
            yield return 0.3f;
            Screenshot($"s5-room-{(drawCurtain ? "curtain" : "open")}");
            var sign = room.GetComponentInChildren<RoomSign>().GetComponentInChildren<TMPro.TMP_Text>().text;
            if (answer == Answer.Best)
            {
                Check(room.Score == chosen.Score + location.Score, $"{room.RoomName}: score {room.Score} (answer {chosen.Score} + curtain {location.Score})");
                Check(sign.Contains("Cared for"), $"{room.RoomName}: the sign says Cared for");
            }
            else
            {
                Check(manager.IsFailed(room.PatientId) && !manager.IsCompleted(room.PatientId) && manager.FailedCount == 1, $"{room.RoomName}: the shift counts the task as failed");
                Check(room.Score == 0, $"{room.RoomName}: a failed task has no score");
                Check(sign.Contains("Not resolved"), $"{room.RoomName}: the sign says Not resolved");
                Check(Board().Contains("Failed: not resolved") && Board().Contains("Failed: 1"), $"{room.RoomName}: the shift board shows the failed task");
                Check(manager.State == ShiftState.Running, "the shift goes on after a failed task");
            }
        }
    }
}
