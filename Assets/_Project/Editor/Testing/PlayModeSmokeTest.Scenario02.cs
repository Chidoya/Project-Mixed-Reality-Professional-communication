using System.Collections;
using System.Linq;
using ProCommVR.Navigation;
using ProCommVR.Scenarios;
using ProCommVR.Shift;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// Scenario 2 in ShiftHospital: the angry daughter in the corridor of floor 3, with the choice of where to talk
    /// and the countdown of the surgical gurney. Each test starts the shift in the lobby, rides the lift to floor 3
    /// and plays one path; then the shift timer is set to run out so the report can be checked. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario02Good    (consultation room, best answer in time)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario02Poor    (corridor, poorest answer in time)
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunScenario02TimeUp  (corridor, no answer: the task fails)
    /// RunShiftLoop also plays Scenario 2 (consultation room, best answer) as part of the whole shift.
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        private enum Answer
        {
            Best,
            Poorest,
            None
        }

        private const string Scenario02Id = "angry-family-member";

        [MenuItem("ProCommVR/Tests/Scenario 02: In Time, Good Answer")]
        public static void RunScenario02Good()
        {
            Launch("s2-good", ShiftScenePath);
        }

        [MenuItem("ProCommVR/Tests/Scenario 02: In Time, Poor Answer")]
        public static void RunScenario02Poor()
        {
            Launch("s2-poor", ShiftScenePath);
        }

        [MenuItem("ProCommVR/Tests/Scenario 02: Time Runs Out")]
        public static void RunScenario02TimeUp()
        {
            Launch("s2-timeup", ShiftScenePath);
        }

        private static IEnumerator Scenario02(string locationId, Answer answer)
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

                var room = manager.AssignedRooms.FirstOrDefault(r => r.Task.Scenario.Id == Scenario02Id);
                Check(room != null, "Scenario 2 is a task of the shift");
                if (room == null)
                    yield break;
                var scenario = room.Task.Scenario;
                var countdown = room.Countdown;
                var gurney = Gurney(room);
                Check(countdown != null && gurney != null, $"{room.RoomName} has a task countdown and a gurney");
                Check(scenario.HasTimeLimit && scenario.TimeLimit > 0f, $"Scenario 2 has a time limit ({scenario.TimeLimit} s)");
                Check(scenario.Locations.Select(l => l.Id).SequenceEqual(new[] { "corridor", "consultation-room" }), "Scenario 2 offers the corridor and the consultation room");
                Check(scenario.Locations.All(l => l.Score == 0), "the place to talk does not count in the score (yet)");
                Check(scenario.Options.Count == 4, "Scenario 2 has 4 options");
                CheckMood(room, Mood.Tense);
                Check(!gurney.gameObject.activeSelf, "the gurney is not in the corridor yet");
                Check(countdown.State == CountdownState.Idle, $"countdown idle in the briefing (got {countdown.State})");

                foreach (var step in Click(startButton.GetComponent<Collider>().bounds.center, () => manager.State == ShiftState.Running))
                    yield return step;
                Check(manager.State == ShiftState.Running, "laser + trigger on the computer starts the shift");
                if (manager.State != ShiftState.Running)
                    yield break;
                yield return 1f;
                Check(countdown.State == CountdownState.Armed && Mathf.Approximately(countdown.TimeRemaining, scenario.TimeLimit),
                    $"the countdown waits until the player meets the daughter (state {countdown.State}, {countdown.TimeRemaining:F1} s left)");

                foreach (var step in CareForCorridor(manager, origin, room, locationId, answer))
                    yield return step;

                if (answer != Answer.None)
                {
                    // In time: the countdown stays stopped, the gurney never comes and the mood stays.
                    yield return 2f;
                    Check(countdown.State == CountdownState.Stopped && !gurney.gameObject.activeSelf, "after answering in time the countdown stays stopped and no gurney comes");
                    CheckMood(room, Mood.Tense);
                }

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
                switch (answer)
                {
                    case Answer.Best:
                        Check(body.Contains($"{room.RoomName} - ") && body.Contains($"{best} / {best}"), $"report: {room.RoomName} scored {best} / {best}");
                        Check(body.Contains("Talked in: Consultation room"), "report: talked in the consultation room");
                        break;
                    case Answer.Poorest:
                        var poorest = scenario.Options.Min(o => o.Score);
                        Check(body.Contains($"{poorest} / {best}"), $"report: {room.RoomName} scored {poorest} / {best}");
                        Check(body.Contains("Talked in: Corridor"), "report: talked in the corridor");
                        break;
                    default:
                        Check(body.Contains("failed: time ran out") && body.Contains(scenario.TimeUpFeedback), "report: the task failed because the time ran out, with the feedback");
                        Check(body.Contains("Failed: 1"), "report subtitle counts 1 failed task");
                        break;
                }
                _headRotation = Quaternion.LookRotation(Flat(results.transform.position - _headWorld));
                yield return 0.3f;
                Screenshot($"s2-results-{answer.ToString().ToLowerInvariant()}");
            }
            finally
            {
                EditorApplication.update -= UpdateDevices;
                RemoveSimulatedDevices();
            }
        }

        /// <summary>
        /// Scenario 2 the way a player plays it: rides the lift to the corridor, meets the daughter (the countdown
        /// starts), chooses where to talk, walks with her into the consultation room if chosen, and then answers
        /// (best or poorest option) and clicks "Finish", or lets the time run out (Answer.None).
        /// </summary>
        private static IEnumerable CareForCorridor(ShiftManager manager, XROrigin origin, PatientRoom room, string locationId, Answer answer)
        {
            var scenario = room.Task.Scenario;
            var countdown = room.Countdown;
            var presenter = room.GetComponentInChildren<ScenarioPresenter>();
            var daughter = room.GetComponentInChildren<NpcReactor>().transform;
            var ticking = countdown.GetComponentInChildren<AudioSource>(true);
            var gurney = Gurney(room);

            foreach (var step in RideLiftTo(origin, room.transform.position.y))
                yield return step;

            // Out of the lift: the daughter blocks the corridor, the countdown starts.
            var waitUntil = EditorApplication.timeSinceStartup + 1.5f;
            while (countdown.State != CountdownState.Running && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            var distance = Flat(daughter.position - _headWorld).magnitude;
            Check(countdown.State == CountdownState.Running, $"{room.RoomName}: the countdown starts when the player meets the daughter ({distance:F1} m)");
            Check(ticking != null && ticking.isPlaying && ticking.gameObject.activeInHierarchy, $"{room.RoomName}: the countdown is shown and ticks");
            Check(ClickableBubbles(room).Length == 0, $"{room.RoomName}: no choice yet from the lift ({distance:F1} m)");
            // Look up a little: the countdown hangs from the ceiling between the lift and the daughter.
            Teleport(origin, _headWorld, daughter.position);
            _headRotation = Quaternion.LookRotation((ticking.transform.position + daughter.position + Vector3.up * 1.6f) * 0.5f - _headWorld);
            yield return 1f;
            Check(countdown.TimeRemaining < countdown.Duration, $"{room.RoomName}: the countdown runs ({countdown.TimeRemaining:F1} of {countdown.Duration:F0} s)");
            Screenshot("s2-corridor");

            // Step up to her: the choice of where to talk.
            Teleport(origin, daughter.position + daughter.forward * 1.0f, daughter.position);
            waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (ClickableBubbles(room).Length < 2 && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            var places = ClickableBubbles(room);
            Check(places.Length == scenario.Locations.Count, $"{room.RoomName}: {scenario.Locations.Count} places to choose from within 1.5 m (got {places.Length})");
            Check(NpcText(room).Contains(scenario.LocationLine), $"{room.RoomName}: the daughter complains first");
            Screenshot("s2-location-choice");
            var location = scenario.Locations.First(l => l.Id == locationId);
            var place = places.FirstOrDefault(b => BubbleText(b) == location.ChoiceText);
            Check(place != null, $"{room.RoomName}: a bubble for '{location.Name}'");
            if (place == null)
                yield break;
            foreach (var step in Click(Center(place), () => room.ChosenLocation != null))
                yield return step;
            Check(room.ChosenLocation == location && presenter.ChosenLocation == location, $"{room.RoomName}: laser + trigger chooses '{location.Name}'");

            if (locationId == "consultation-room")
            {
                // She walks ahead into the consultation room; the player follows.
                var path = daughter.parent.GetComponent<PathMover>();
                Check(path != null && path.IsMoving, $"{room.RoomName}: the daughter walks to the consultation room");
                yield return 0.5f;
                Check(ClickableBubbles(room).Length == 0, $"{room.RoomName}: no options while she walks away");
                waitUntil = EditorApplication.timeSinceStartup + 12f;
                while (path != null && !path.HasArrived && EditorApplication.timeSinceStartup < waitUntil)
                    yield return null;
                var consultation = GameObject.Find("Hospital/Floor3/Room_3B");
                var inside = consultation != null && RendererBounds(consultation.transform.Find("Wall_Back").gameObject).min.z > daughter.position.z &&
                             daughter.position.z > consultation.transform.position.z + 0.5f;
                Check(path != null && path.HasArrived && inside, $"{room.RoomName}: the daughter is in the consultation room ({daughter.position})");
                Teleport(origin, daughter.position + daughter.forward * 1.0f, daughter.position);
            }

            waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (ClickableBubbles(room).Length < 4 && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            var options = ClickableBubbles(room);
            Check(options.Length == 4, $"{room.RoomName}: 4 options at the chosen place (got {options.Length})");
            Check(NpcText(room).Contains(scenario.NpcLine), $"{room.RoomName}: the daughter says \"{scenario.NpcLine}\"");
            Check(countdown.State == CountdownState.Running && ticking.isPlaying, $"{room.RoomName}: the countdown keeps ticking during the options ({countdown.TimeRemaining:F1} s left)");
            Screenshot($"s2-options-{locationId}");
            if (options.Length == 0)
                yield break;

            if (answer == Answer.None)
            {
                foreach (var step in LetTimeRunOut(manager, room, gurney, ticking))
                    yield return step;
                yield break;
            }

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
            Check(countdown.State == CountdownState.Stopped && !ticking.isPlaying && !ticking.gameObject.activeInHierarchy,
                $"{room.RoomName}: answering in time stops the countdown ({countdown.TimeRemaining:F1} s left), the ticking and the display");

            waitUntil = EditorApplication.timeSinceStartup + 2f;
            while (ClickableBubbles(room).Length != 1 && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            yield return 0.6f;
            var animator = daughter.GetComponentInChildren<Animator>();
            var expectedState = answer == Answer.Best ? "Talking" : "Angry";
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName(expectedState), $"{room.RoomName}: the daughter reacts ({expectedState})");
            Check(NpcText(room).Contains(chosen.NpcReaction), $"{room.RoomName}: the daughter's reaction line");
            Screenshot($"s2-reaction-{answer.ToString().ToLowerInvariant()}");
            var finish = ClickableBubbles(room);
            Check(finish.Length == 1, $"{room.RoomName}: one Finish bubble after the choice");
            if (finish.Length == 0)
                yield break;
            foreach (var step in Click(Center(finish[0]), () => manager.IsCompleted(room)))
                yield return step;
            Check(room.Outcome == TaskOutcome.Completed && manager.IsCompleted(room.PatientId) && !manager.IsFailed(room.PatientId),
                $"{room.RoomName}: completed in time (outcome {room.Outcome})");
            yield return 0.2f;
            Check(room.Score == chosen.Score + location.Score, $"{room.RoomName}: score {room.Score} (option {chosen.Score} + place {location.Score})");
            Check(room.GetComponentInChildren<RoomSign>().GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Cared for"), $"{room.RoomName}: the sign says Cared for");
            Check(Board().Contains($"{room.RoomName}: {room.Task.PatientName}   <color=#1E8C3A><b>Cared for"), $"{room.RoomName}: the shift board says Cared for");
        }

        /// <summary>
        /// The player does not answer: the countdown (shortened) runs out, the task fails and the alarm goes off.
        /// </summary>
        private static IEnumerable LetTimeRunOut(ShiftManager manager, PatientRoom room, PathMover gurney, AudioSource ticking)
        {
            var countdown = room.Countdown;
            var scenario = room.Task.Scenario;
            SetPrivateProperty(countdown, nameof(TaskCountdown.TimeRemaining), 2f);
            var waitUntil = EditorApplication.timeSinceStartup + 5f;
            while (room.Outcome == TaskOutcome.None && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            yield return 0.2f;

            Check(countdown.State == CountdownState.Expired && !ticking.isPlaying, $"{room.RoomName}: the countdown runs out and stops ticking");
            Check(room.Outcome == TaskOutcome.FailedTimeUp && manager.IsFailed(room.PatientId) && !manager.IsCompleted(room.PatientId),
                $"{room.RoomName}: the task failed because the time ran out (outcome {room.Outcome})");
            Check(manager.FailedCount == 1 && manager.CompletedCount == 0 && manager.FinishedCount == 1, "the shift counts 1 failed task, 0 cared for");
            Check(manager.State == ShiftState.Running, "the shift goes on after a failed task");
            Check(room.ChosenOption == null && room.Score == 0, $"{room.RoomName}: no answer, no score");
            Check(ClickableBubbles(room).Length == 0, $"{room.RoomName}: the options are gone");
            Check(NpcText(room).Contains(scenario.TimeUpLine), $"{room.RoomName}: the daughter keeps shouting");
            yield return 0.3f;
            Check(gurney.gameObject.activeSelf && gurney.IsMoving, $"{room.RoomName}: the surgical gurney comes down the corridor");

            // Alarm: red light (flashing), red sign, warning sound.
            yield return 2.5f;
            CheckMood(room, Mood.Alarm);
            CheckMoodSound(room);
            Check(room.MoodController.CurrentProfile.Sound.name.Contains("Alarm"), $"{room.RoomName}: the warning sound plays ({room.MoodController.CurrentProfile.Sound.name})");
            var light = RoomLights(room)[0];
            float low = float.MaxValue, high = 0f;
            for (var t = 0f; t < 1.5f; t += 0.05f)
            {
                low = Mathf.Min(low, light.intensity);
                high = Mathf.Max(high, light.intensity);
                yield return 0.05f;
            }
            Check(high > 0f && low < high * 0.5f, $"{room.RoomName}: the red light flashes (intensity {low:F2} to {high:F2})");
            Check(room.GetComponentInChildren<RoomSign>().GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Time ran out"), $"{room.RoomName}: the sign says Time ran out");
            Check(Board().Contains("Failed: time ran out") && Board().Contains("Failed: 1"), $"{room.RoomName}: the shift board shows the failed task apart from Cared for");

            // Look at the daughter and the corridor behind her, where the gurney comes.
            var daughter = room.GetComponentInChildren<NpcReactor>().transform;
            _headWorld = new Vector3(daughter.position.x - 2.2f, _headWorld.y, 1.0f);
            _headRotation = Quaternion.LookRotation(new Vector3(daughter.position.x + 4f, _headWorld.y - 0.3f, 2f) - _headWorld);
            yield return 0.2f;
            Screenshot("s2-time-up-alarm");
            waitUntil = EditorApplication.timeSinceStartup + 12f;
            while (!gurney.HasArrived && EditorApplication.timeSinceStartup < waitUntil)
                yield return null;
            Check(gurney.HasArrived, $"{room.RoomName}: the gurney arrives in the corridor ({gurney.transform.position})");
            Screenshot("s2-time-up-gurney");
        }

        private static PathMover Gurney(PatientRoom room)
        {
            return room.GetComponentsInChildren<PathMover>(true).FirstOrDefault(p => p.name.Contains("Gurney"));
        }

        private static string NpcText(PatientRoom room)
        {
            return string.Join(" ", room.GetComponentsInChildren<DialogueBubble>().Where(b => !b.GetComponent<UnityEngine.UI.Button>().enabled).Select(BubbleText));
        }

        private static string BubbleText(DialogueBubble bubble)
        {
            return bubble.GetComponentInChildren<TMPro.TMP_Text>().text;
        }

        private static string Board()
        {
            return Object.FindObjectsByType<ShiftBoard>(FindObjectsSortMode.None).First().GetComponentInChildren<TMPro.TMP_Text>().text;
        }
    }
}
