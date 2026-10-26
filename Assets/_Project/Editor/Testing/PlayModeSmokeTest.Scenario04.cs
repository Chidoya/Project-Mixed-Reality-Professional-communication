using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProCommVR.Scenarios;
using ProCommVR.Shift;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// Scenario 4 in ShiftHospital: the wife of a patient with a poor prognosis in room 1B. Each test starts the shift in
    /// the lobby, rides the lift to floor 1, looks into the dim purple room from the door (the patient asleep, the wife on
    /// the chair, the monitor), steps up to the wife (she stands up), answers and clicks "Finish". Then the shift timer
    /// is set to run out so the report can be checked. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario04Good  (best answer: she sits down, the room turns blue)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario04Poor  (false hope: she stays standing, the room stays purple)
    /// RunShiftLoop also plays Scenario 4 (best answer) as part of the whole shift.
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private const string Scenario04Id = "poor-prognosis";

        [MenuItem("ProCommVR/Tests/Scenario 04: Good Answer")]
        public static void RunScenario04Good()
        {
            Launch("s4-good", ShiftScenePath);
        }

        [MenuItem("ProCommVR/Tests/Scenario 04: Poor Answer")]
        public static void RunScenario04Poor()
        {
            Launch("s4-poor", ShiftScenePath);
        }

        /// <summary>
        /// One scenario of the shift on its own: finds its task, starts the shift at the computer in the lobby, plays the
        /// task (care), ends the shift with its timer and logs the report for the check (checkReport).
        /// </summary>
        private static IEnumerator ScenarioInShift(string scenarioId, Func<ShiftManager, XROrigin, PatientRoom, IEnumerable> care,
            Action<string, PatientRoom> checkReport)
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var manager = Object.FindAnyObjectByType<ShiftManager>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            var startButton = Object.FindAnyObjectByType<ShiftStartButton>();
            var results = Object.FindAnyObjectByType<ResultsScreen>(FindObjectsInactive.Include);
            Check(manager != null && origin != null && startButton != null && results != null, "scene has a ShiftManager, XR Origin, start computer and results screen");
            if (Errors.Count > 0)
                yield break;

            AddSimulatedDevices();
            EditorApplication.update += UpdateDevices;
            try
            {
                _headWorld = Camera.main.transform.position;
                _headWorld.y = origin.transform.position.y + HeadHeight;
                _headRotation = Quaternion.LookRotation(Flat(origin.transform.forward));
                yield return 1f;

                var room = manager.AssignedRooms.FirstOrDefault(r => r.Task.Scenario.Id == scenarioId);
                Check(room != null, $"'{scenarioId}' is a task of the shift");
                if (room == null)
                    yield break;
                CheckMood(room, room.Task.PatientMood);
                Check(room.Task.Scenario.Options.Count == 4, $"{room.RoomName}: the scenario has 4 options");

                foreach (var step in Click(startButton.GetComponent<Collider>().bounds.center, () => manager.State == ShiftState.Running))
                    yield return step;
                Check(manager.State == ShiftState.Running, "laser + trigger on the computer starts the shift");
                if (manager.State != ShiftState.Running)
                    yield break;

                foreach (var step in care(manager, origin, room))
                    yield return step;

                SetPrivateProperty(manager, nameof(ShiftManager.TimeRemaining), 0.5f);
                var waitUntil = EditorApplication.timeSinceStartup + 3f;
                while (manager.State == ShiftState.Running && EditorApplication.timeSinceStartup < waitUntil)
                    yield return null;
                Check(manager.State == ShiftState.Ended, "the shift ends when its timer runs out");
                yield return 0.5f;
                var body = string.Join(" | ", results.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text));
                Log($"results: {body.Replace("\n", " / ")}");
                checkReport(body, room);
                _headRotation = Quaternion.LookRotation(Flat(results.transform.position - _headWorld));
                yield return 0.3f;
                Screenshot($"{scenarioId}-results");
            }
            finally
            {
                EditorApplication.update -= UpdateDevices;
                RemoveSimulatedDevices();
            }
        }

        private static IEnumerator Scenario04(Answer answer)
        {
            return ScenarioInShift(Scenario04Id, (manager, origin, room) => CareForBedside(manager, origin, room, answer), (body, room) =>
            {
                var scenario = room.Task.Scenario;
                var score = answer == Answer.Best ? scenario.BestScore : scenario.Options.Min(o => o.Score);
                Check(body.Contains($"{room.RoomName} - {room.Task.PatientName}") && body.Contains($"{score} / {scenario.BestScore}"),
                    $"report: {room.RoomName} scored {score} / {scenario.BestScore}");
                Check(room.ChosenOption != null && body.Contains(room.ChosenOption.Feedback), "report: the feedback on the choice");
            });
        }

        /// <summary>
        /// Scenario 4 the way a player plays it: from the door of room 1B the dim purple room with the sleeping patient,
        /// his wife on the chair and the monitor; within reach of the wife she stands up and asks, the player answers
        /// (best or poorest) and clicks "Finish".
        /// </summary>
        private static IEnumerable CareForBedside(ShiftManager manager, XROrigin origin, PatientRoom room, Answer answer)
        {
            var scenario = room.Task.Scenario;
            var attention = room.GetComponentInChildren<NpcAttention>();
            var wife = attention.transform;
            var seat = wife.parent;
            var wifeAnimator = wife.GetComponentInChildren<Animator>();
            var patientAnimator = room.transform.Find("Patient (asleep)").GetComponentInChildren<Animator>();
            var monitor = room.GetComponentInChildren<VitalSignsMonitor>();
            var light = RoomLights(room)[0];
            wifeAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            patientAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (var step in RideLiftTo(origin, room.transform.position.y))
                yield return step;

            // From the doorway: a quiet, dim, purple room; she keeps watch, he sleeps, the vital signs fluctuate.
            var door = room.transform.position + room.transform.forward * 0.3f;
            Teleport(origin, door, wife.position);
            var readings = new HashSet<int>();
            for (var t = 0f; t < 4f; t += 0.5f)
            {
                readings.Add(monitor.HeartRate);
                yield return 0.5f;
            }
            Check(ClickableBubbles(room).Length == 0 && !attention.IsAttending, $"{room.RoomName}: no options and the wife stays seated while the player is at the door");
            Check(wifeAnimator.GetCurrentAnimatorStateInfo(0).IsName("Sitting"), $"{room.RoomName}: the wife sits by the bed ({StateName(wifeAnimator)})");
            Check(patientAnimator.GetCurrentAnimatorStateInfo(0).IsName("Laying"), $"{room.RoomName}: the patient lies in bed ({StateName(patientAnimator)})");
            var bed = RendererBounds(room.transform.Find("Furniture/Bed").gameObject);
            var hips = patientAnimator.GetBoneTransform(HumanBodyBones.Hips).position;
            Check(bed.Contains(new Vector3(hips.x, bed.center.y, hips.z)) && hips.y > bed.min.y + 0.3f && hips.y < bed.max.y + 0.4f,
                $"{room.RoomName}: the patient is on the bed (hips {hips - room.transform.position})");
            Check(readings.Count(r => r > 0) >= 2, $"{room.RoomName}: the vital signs fluctuate ({string.Join(", ", readings)})");
            var color = light.color;
            Check(room.MoodController.CurrentMood == Mood.Distressed && color.b > color.g && color.r > color.g, $"{room.RoomName}: soft purple light ({color})");
            var baseIntensity = light.intensity / room.MoodController.CurrentProfile.RoomLightIntensity;
            Check(light.intensity < baseIntensity * 0.9f, $"{room.RoomName}: the room light is dimmed ({light.intensity:F2} of {baseIntensity:F2})");
            Screenshot("s4-doorway");

            // Within reach: she stands up, reaches toward the nurse and asks.
            var towardDoor = Flat(room.transform.position - seat.position).normalized;
            Teleport(origin, seat.position + towardDoor * 1.6f, seat.position);
            var waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (ClickableBubbles(room).Length < 4 && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            yield return 1f;
            var options = ClickableBubbles(room);
            Check(attention.IsAttending && Flat(wife.position - seat.position).magnitude > 0.5f, $"{room.RoomName}: the wife stands up ({Flat(wife.position - seat.position).magnitude:F2} m from the chair)");
            Check(wifeAnimator.GetCurrentAnimatorStateInfo(0).IsName("Talking"), $"{room.RoomName}: she reaches toward the nurse ({StateName(wifeAnimator)})");
            Check(Vector3.Angle(Flat(wife.forward), Flat(_headWorld - wife.position)) < 25f, $"{room.RoomName}: she faces the nurse");
            Check(NpcText(room).Contains(scenario.NpcLine), $"{room.RoomName}: she asks \"{scenario.NpcLine}\"");
            Check(options.Length == 4, $"{room.RoomName}: 4 options (got {options.Length})");
            _headRotation = Quaternion.LookRotation(wife.position + Vector3.up * 1.5f - _headWorld);
            yield return 0.3f;
            Screenshot("s4-wife-asks");
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
            Check(NpcText(room).Contains(chosen.NpcReaction), $"{room.RoomName}: her reaction line");
            if (answer == Answer.Best)
            {
                Check(Flat(wife.position - seat.position).magnitude < 0.05f && wifeAnimator.GetCurrentAnimatorStateInfo(0).IsName("Sitting"),
                    $"{room.RoomName}: she sits down again ({StateName(wifeAnimator)})");
                CheckMood(room, Mood.Somber);
                color = light.color;
                Check(color.b > color.r && color.b > color.g, $"{room.RoomName}: the room turns blue ({color})");
                Check(Mathf.Approximately(light.intensity, baseIntensity), $"{room.RoomName}: the light is no longer dimmed ({light.intensity:F2})");
            }
            else
            {
                Check(Flat(wife.position - seat.position).magnitude > 0.5f && wifeAnimator.GetCurrentAnimatorStateInfo(0).IsName("Talking"),
                    $"{room.RoomName}: she stays standing, hopeful ({StateName(wifeAnimator)})");
                CheckMood(room, Mood.Distressed);
            }
            _headRotation = Quaternion.LookRotation(seat.position + Vector3.up * 1.0f - _headWorld);
            yield return 0.3f;
            Screenshot($"s4-reaction-{answer.ToString().ToLowerInvariant()}");

            var finish = ClickableBubbles(room);
            Check(finish.Length == 1, $"{room.RoomName}: one Finish bubble after the choice");
            if (finish.Length == 0)
                yield break;
            foreach (var step in Click(Center(finish[0]), () => manager.IsCompleted(room)))
                yield return step;
            Check(room.Outcome == TaskOutcome.Completed && manager.IsCompleted(room.PatientId), $"{room.RoomName}: completed (outcome {room.Outcome})");
            Check(room.Score == chosen.Score, $"{room.RoomName}: score {room.Score}");
            Check(room.GetComponentInChildren<RoomSign>().GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Cared for"), $"{room.RoomName}: the sign says Cared for");
        }
    }
}
