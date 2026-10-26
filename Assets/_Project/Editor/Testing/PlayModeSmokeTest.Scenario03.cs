using System.Collections;
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
    /// Scenario 3 in ShiftHospital: the son at the nurse's station of floor 2 and the busy doctor. Each test starts the
    /// shift in the lobby, rides the lift to floor 2, meets the son, tries the doctor while he rushes between the rooms
    /// (no options), catches him at his desk, answers and tells the son. Then the shift timer is set to run out so the
    /// report can be checked. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario03Good  (best answer: a time is agreed, mood Happy)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario03Poor  (poorest answer: the doctor is annoyed, mood Tense)
    /// RunShiftLoop also plays Scenario 3 (best answer) as part of the whole shift.
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private const string Scenario03Id = "doctor-and-family";

        [MenuItem("ProCommVR/Tests/Scenario 03: Good Answer")]
        public static void RunScenario03Good()
        {
            Launch("s3-good", ShiftScenePath);
        }

        [MenuItem("ProCommVR/Tests/Scenario 03: Poor Answer")]
        public static void RunScenario03Poor()
        {
            Launch("s3-poor", ShiftScenePath);
        }

        private static IEnumerator Scenario03(Answer answer)
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

                var room = manager.AssignedRooms.FirstOrDefault(r => r.Task.Scenario.Id == Scenario03Id);
                Check(room != null, "Scenario 3 is a task of the shift");
                if (room == null)
                    yield break;
                var scenario = room.Task.Scenario;
                Check(scenario.Options.Count == 4 && scenario.HasBystander && !string.IsNullOrWhiteSpace(scenario.BusyLine),
                    "Scenario 3 has 4 options, a bystander (the son) and a busy line for the doctor");
                Check(room.GetComponentInChildren<BusyNpc>() != null && room.GetComponentInChildren<BystanderSpeech>() != null,
                    $"{room.RoomName} has a busy doctor and the son's speech bubble");
                CheckMood(room, Mood.Neutral);
                Check(room.GetComponentInChildren<BusyNpc>().State == BusyState.Idle, "the doctor waits for the shift to start");

                foreach (var step in Click(startButton.GetComponent<Collider>().bounds.center, () => manager.State == ShiftState.Running))
                    yield return step;
                Check(manager.State == ShiftState.Running, "laser + trigger on the computer starts the shift");
                if (manager.State != ShiftState.Running)
                    yield break;

                foreach (var step in CareForStation(manager, origin, room, answer))
                    yield return step;

                // End the shift (its own timer) to see the report.
                SetPrivateProperty(manager, nameof(ShiftManager.TimeRemaining), 0.5f);
                var waitUntil = EditorApplication.timeSinceStartup + 3f;
                while (manager.State == ShiftState.Running && EditorApplication.timeSinceStartup < waitUntil)
                    yield return null;
                Check(manager.State == ShiftState.Ended, "the shift ends when its timer runs out");
                yield return 0.5f;
                var body = string.Join(" | ", results.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text));
                Log($"results: {body.Replace("\n", " / ")}");
                var best = scenario.BestScore;
                var score = answer == Answer.Best ? best : scenario.Options.Min(o => o.Score);
                Check(body.Contains($"{room.RoomName} - {room.Task.PatientName}") && body.Contains($"{score} / {best}"),
                    $"report: {room.RoomName} scored {score} / {best}");
                Check(body.Contains(room.ChosenOption != null ? room.ChosenOption.Feedback : "?"), "report: the feedback on the choice");
                _headRotation = Quaternion.LookRotation(Flat(results.transform.position - _headWorld));
                yield return 0.3f;
                Screenshot($"s3-results-{answer.ToString().ToLowerInvariant()}");
            }
            finally
            {
                EditorApplication.update -= UpdateDevices;
                RemoveSimulatedDevices();
            }
        }

        /// <summary>
        /// Scenario 3 the way a player plays it: out of the lift the son at the nurse's station complains, the doctor
        /// rushes by and has no time, then (after his round, shortened for the test) he reviews paperwork at his desk in
        /// room 2C: the player steps up to him, gets the 4 options, answers (best or poorest), clicks "Finish" and walks
        /// back to the station to tell the son, who reacts.
        /// </summary>
        /// <summary>
        /// The current state of a character's Animator (Liam's states), for check messages.
        /// </summary>
        private static string StateName(Animator animator)
        {
            var info = animator.GetCurrentAnimatorStateInfo(0);
            var name = new[] { "Idle", "Talking", "Sitting", "Laying", "Sad", "Angry", "Crying" }.FirstOrDefault(info.IsName) ?? $"hash {info.shortNameHash}";
            return $"{name}{(animator.IsInTransition(0) ? ", in transition" : "")}, culling {animator.cullingMode}, enabled {animator.isActiveAndEnabled}, " +
                   $"forward {animator.transform.forward}, root forward {animator.transform.root.forward}";
        }

        private static IEnumerable CareForStation(ShiftManager manager, XROrigin origin, PatientRoom room, Answer answer)
        {
            var scenario = room.Task.Scenario;
            var presenter = room.GetComponentInChildren<ScenarioPresenter>();
            var busy = room.GetComponentInChildren<BusyNpc>();
            var doctor = busy.transform;
            var doctorAnimator = doctor.GetComponentInChildren<Animator>();
            var speech = room.GetComponentInChildren<BystanderSpeech>();
            var son = speech.transform;
            var sonAttention = son.GetComponentInChildren<NpcAttention>();
            var sonAnimator = son.GetComponentInChildren<Animator>();

            foreach (var step in RideLiftTo(origin, room.transform.position.y))
                yield return step;

            // Out of the lift: the son at the nurse's station turns to the player and complains.
            var waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (!sonAttention.IsAttending && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            yield return 1f;
            Check(sonAttention.IsAttending, $"{room.RoomName}: the son notices the player ({Flat(son.position - _headWorld).magnitude:F1} m)");
            Check(speech.CurrentLine == scenario.BystanderLine, $"{room.RoomName}: the son says \"{scenario.BystanderLine}\"");
            Check(sonAnimator.GetCurrentAnimatorStateInfo(0).IsName("Angry"), $"{room.RoomName}: the son is angry ({StateName(sonAnimator)})");
            Check(Vector3.Angle(Flat(sonAnimator.transform.forward), Flat(_headWorld - sonAnimator.transform.position)) < 30f, $"{room.RoomName}: the son turns to the player");
            // Later in a shift the doctor may be at his desk: wait for his next round.
            waitUntil = EditorApplication.timeSinceStartup + 40f;
            while (busy.State != BusyState.Rushing && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            yield return 1f;
            Check(busy.State == BusyState.Rushing && presenter.HoldChoices, $"{room.RoomName}: the doctor rushes between the rooms (state {busy.State})");
            _headRotation = Quaternion.LookRotation(son.position + Vector3.up * 1.4f - _headWorld);
            yield return 0.3f;
            Screenshot("s3-station");

            // Step into the doctor's way: he stops, turns to the player and says he has no time. No options.
            var ahead = doctor.position + doctor.forward * 1.3f;
            Teleport(origin, ahead, doctor.position);
            waitUntil = EditorApplication.timeSinceStartup + 3f;
            while (!busy.IsPausedForPlayer && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            Check(busy.IsPausedForPlayer, $"{room.RoomName}: the doctor stops when the player steps into his way");
            yield return 0.8f;
            _headRotation = Quaternion.LookRotation(doctor.position + Vector3.up * 1.5f - _headWorld);
            Check(NpcText(room).Contains(scenario.BusyLine), $"{room.RoomName}: the doctor says \"{scenario.BusyLine}\"");
            Check(ClickableBubbles(room).Length == 0, $"{room.RoomName}: no options while the doctor is busy, also within 1.5 m ({Flat(doctor.position - _headWorld).magnitude:F1} m)");
            Check(doctorAnimator.GetCurrentAnimatorStateInfo(0).IsName("Talking") || doctorAnimator.IsInTransition(0), $"{room.RoomName}: the doctor gestures");
            Screenshot("s3-doctor-busy");

            // After his round he goes to his desk (the round is shortened for the test). The player steps aside.
            SetPrivateField(busy, "_busyTime", 0f);
            Teleport(origin, new Vector3(son.position.x - 1.0f, 0f, 1.6f), doctor.position);
            waitUntil = EditorApplication.timeSinceStartup + 30f;
            while (busy.State != BusyState.AtDesk && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            Check(busy.State == BusyState.AtDesk, $"{room.RoomName}: the doctor goes to his desk to review paperwork (state {busy.State})");
            var office = GameObject.Find("Hospital/Floor2/Room_2C").transform;
            var local = office.InverseTransformPoint(doctor.position);
            Check(local.z > 1.5f && local.z < 3.8f && Mathf.Abs(local.x) < 1.9f, $"{room.RoomName}: the doctor stands in his office ({local})");
            Check(!presenter.HoldChoices && NpcText(room).Contains(scenario.NpcLine), $"{room.RoomName}: at his desk he is reachable: \"{scenario.NpcLine}\"");

            // From the doorway: still no options. Behind him, within 1.5 m: the options, and he turns to the player.
            Teleport(origin, office.position + office.forward * 0.4f, doctor.position);
            yield return 0.5f;
            Check(ClickableBubbles(room).Length == 0, $"{room.RoomName}: no options from the office door");
            Screenshot("s3-office-door");
            Teleport(origin, doctor.position - doctor.forward * 1.0f, doctor.position);
            waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (ClickableBubbles(room).Length < 4 && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            var options = ClickableBubbles(room);
            Check(options.Length == 4, $"{room.RoomName}: 4 options within 1.5 m of the doctor at his desk (got {options.Length})");
            yield return 1f;
            Check(busy.State == BusyState.Engaged, $"{room.RoomName}: the doctor stays to talk (state {busy.State})");
            Check(Vector3.Angle(Flat(doctor.forward), Flat(_headWorld - doctor.position)) < 20f, $"{room.RoomName}: the doctor turns to the player");
            options = ClickableBubbles(room);
            Screenshot("s3-doctor-options");
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

            waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (ClickableBubbles(room).Length != 1 && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            yield return 0.6f;
            var expectedState = answer == Answer.Best ? "Talking" : "Idle";
            Check(doctorAnimator.GetCurrentAnimatorStateInfo(0).IsName(expectedState), $"{room.RoomName}: the doctor reacts ({expectedState}, got {StateName(doctorAnimator)})");
            Check(NpcText(room).Contains(chosen.NpcReaction), $"{room.RoomName}: the doctor's reaction line");
            Check(!speech.HasReacted && speech.CurrentLine == scenario.BystanderLine, $"{room.RoomName}: the son does not know the outcome yet");
            yield return 1.6f;
            CheckMood(room, answer == Answer.Best ? Mood.Happy : Mood.Tense);
            Screenshot($"s3-doctor-reaction-{answer.ToString().ToLowerInvariant()}");

            var finish = ClickableBubbles(room);
            Check(finish.Length == 1, $"{room.RoomName}: one Finish bubble after the choice");
            if (finish.Length == 0)
                yield break;
            foreach (var step in Click(Center(finish[0]), () => manager.IsCompleted(room)))
                yield return step;
            Check(room.Outcome == TaskOutcome.Completed && manager.IsCompleted(room.PatientId), $"{room.RoomName}: completed (outcome {room.Outcome})");
            Check(room.Score == chosen.Score, $"{room.RoomName}: score {room.Score}");

            // Back to the nurse's station to tell the son.
            Teleport(origin, son.position + Vector3.left * 1.9f, son.position);
            waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (!speech.HasReacted && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            yield return 0.6f;
            Check(speech.HasReacted && speech.CurrentLine.Contains(chosen.BystanderReaction), $"{room.RoomName}: the son reacts when the player tells him: \"{speech.CurrentLine}\"");
            var sonState = answer == Answer.Best ? "Talking" : "Angry";
            Check(sonAnimator.GetCurrentAnimatorStateInfo(0).IsName(sonState), $"{room.RoomName}: the son's body language ({sonState})");
            Check(room.GetComponentInChildren<RoomSign>().GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Cared for"), $"{room.RoomName}: the sign says Cared for");
            Check(Board().Contains($"{room.RoomName}: {room.Task.PatientName}   <color=#1E8C3A><b>Cared for"), $"{room.RoomName}: the shift board says Cared for");
            _headRotation = Quaternion.LookRotation(son.position + Vector3.up * 1.6f - _headWorld);
            yield return 0.3f;
            Screenshot($"s3-son-reaction-{answer.ToString().ToLowerInvariant()}");
        }
    }
}
