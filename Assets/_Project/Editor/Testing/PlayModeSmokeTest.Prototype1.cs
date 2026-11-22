using System.Collections;
using System.Linq;
using ProCommVR.AiTalk;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Pov;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// Prototype1 (Liam's U-ward, the AI patient in room 1A), with the real AI server (Tools/AiServer, started by
    /// Liam's AiServerLauncher on Play). Checks: the briefing (no conversation, Y refused); near the patient the
    /// conversation starts and the trust and emotions follow the opening turn of the AI server; Y opens the patient view
    /// with the trust meter in the HUD; the effects are on the floor (plants, puddles) and it rains inside; finishing the
    /// conversation brings the player to the doctor, where the debrief opens; Retry brings the player back with a new
    /// conversation; Done ends the prototype. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunPrototype1
    /// The student's speech is not tested here (Liam's AiTalkCapture sends recorded test sentences).
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        [MenuItem("ProCommVR/Tests/Prototype 1 (AI patient, needs the AI server)")]
        public static void RunPrototype1()
        {
            Launch("prototype1", Prototype1SceneBuilder.ScenePath);
        }

        private static IEnumerator Prototype1Test()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var flow = Object.FindAnyObjectByType<PrototypeFlow>();
            var npc = Object.FindAnyObjectByType<AiNpc>();
            var bridge = Object.FindAnyObjectByType<AiPatientBridge>();
            var pov = Object.FindAnyObjectByType<PovController>();
            var hud = Object.FindAnyObjectByType<EmotionHud>();
            var world = Object.FindAnyObjectByType<PatientWorldController>();
            var holder = Object.FindAnyObjectByType<EmotionHolder>();
            var reputation = Object.FindAnyObjectByType<PatientReputation>();
            var debrief = Object.FindAnyObjectByType<AiDebriefPanel>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Check(flow != null && npc != null && bridge != null && pov != null && hud != null && world != null && holder != null
                  && reputation != null && debrief != null && origin != null,
                "Prototype1 has the flow, the AI patient, the bridge, the patient view, the HUD, the trust and the debrief");
            if (Errors.Count > 0)
                yield break;
            var serializedFlow = new SerializedObject(flow);
            var patientSpot = serializedFlow.FindProperty("_patientSpot").objectReferenceValue as Transform;
            var doctorSpot = serializedFlow.FindProperty("_doctorSpot").objectReferenceValue as Transform;

            yield return 1f;
            Check(flow.Current == PrototypeFlow.Stage.Briefing, $"the prototype starts with the briefing ({flow.Current})");
            Check(!bridge.IsPlaying && pov.WhyNot() == "no conversation", $"no conversation yet, Y is refused ({pov.WhyNot()})");
            Check(hud.Distance < 1.3f, $"the HUD is closer than before ({hud.Distance} m, was 1.6 m)");
            Screenshot("prototype1-briefing");

            // Near the patient: the conversation starts (the AI server may still be warming up).
            flow.Teleport(patientSpot);
            var startWait = EditorApplication.timeSinceStartup + 180f;
            while (!bridge.IsPlaying && EditorApplication.timeSinceStartup < startWait)
                yield return null;
            Check(bridge.IsPlaying, "near the patient the AI conversation starts (is the AI server running?)");
            if (!bridge.IsPlaying)
                yield break;
            Check(flow.Current == PrototypeFlow.Stage.Talking, $"the flow is in the conversation ({flow.Current})");
            while (npc.IsSpeaking)
                yield return null;
            Check(Mathf.Approximately(reputation.Value, npc.Trust), $"the trust follows the AI server ({reputation.Value} = {npc.Trust})");
            Check(reputation.Value < reputation.LowThreshold, $"the disappointed patient starts with low trust ({reputation.Value})");
            yield return holder.BlendSeconds + 0.3f;
            Check(holder.Dominant == Emotion.Sadness, $"his opening turn makes him sad ({holder.Current}, AI: {npc.LastTurn?.emotion}, tension {npc.Anger})");
            Screenshot("prototype1-patient");

            // Y: the patient view, with the trust meter.
            AddPovDevices();
            try
            {
                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                    yield return step;
                Check(pov.Mode == PovMode.Patient, $"Y switches to the patient view ({pov.WhyNot()})");
                if (pov.Mode != PovMode.Patient)
                    yield break;
                foreach (var step in WaitForWorld(world, Emotion.Sadness))
                    yield return step;
                yield return 1.2f;
                Check(hud.IsShown && hud.Reputation == reputation, "the HUD shows, with the trust meter");
                Check(Mathf.Abs(hud.TrustShown - reputation.Value) < 1f && hud.TrustText == Mathf.RoundToInt(reputation.Value).ToString(),
                    $"the trust meter shows the trust ({hud.TrustText}, {reputation.Value})");
                var rain = Object.FindObjectsByType<EmotionEffectAnchor>(FindObjectsSortMode.None).FirstOrDefault(a => a.name.StartsWith("Rain Inside"));
                Check(rain != null && rain.IsOn && rain.Particles.Length > 0 && rain.Particles[0].isPlaying, "Sadness: it rains inside the room");
                var puddles = Object.FindObjectsByType<EmotionEffectAnchor>(FindObjectsSortMode.None).Where(a => a.name.StartsWith("Puddle")).ToList();
                Check(puddles.Count >= 2 && puddles.All(p => p.IsOn), $"Sadness: puddles grow on the floor ({puddles.Count})");
                Check(puddles.All(p => Mathf.Abs(p.transform.position.y - patientSpot.position.y) < 0.05f), "the puddles lie on the floor");
                Screenshot("prototype1-sadness");

                // Joy (set directly: the AI decides when the patient is happy): the plants grow on the floor, not in the air.
                holder.SetImmediate(new EmotionState(0.85f, 0.05f, 0.05f, 0.05f));
                foreach (var step in WaitForWorld(world, Emotion.Joy))
                    yield return step;
                yield return 1.5f;
                var plants = Object.FindObjectsByType<EmotionEffectAnchor>(FindObjectsSortMode.None).Where(a => a.Kind == EmotionEffectKind.Plant).ToList();
                Check(plants.Count >= 2 && plants.All(p => p.IsOn), $"Joy: the plants grow ({plants.Count})");
                foreach (var plant in plants)
                {
                    var bounds = Scenario01SceneBuilder.TightBounds(plant.Grow.gameObject);
                    Check(Mathf.Abs(bounds.min.y - patientSpot.position.y) < 0.06f, $"{plant.name} stands on the floor (bottom at {bounds.min.y:0.000} m)");
                }
                Screenshot("prototype1-joy");

                // Fear: darker than before the playtest (ambient at 2% of the room's own ambient light).
                var ambientBefore = AmbientOutsidePatientView(world);
                holder.SetImmediate(new EmotionState(0.05f, 0.05f, 0.85f, 0.05f));
                foreach (var step in WaitForWorld(world, Emotion.Fear))
                    yield return step;
                yield return 1.5f;
                var ambient = RenderSettings.ambientLight.maxColorComponent;
                Check(ambient < ambientBefore * 0.05f, $"Fear: the room is dark (ambient {ambient:0.000}, was {ambientBefore:0.000})");
                Screenshot("prototype1-fear");

                holder.SetTarget(new EmotionState(0.05f, 0.05f, 0.05f, 0.7f));
                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                    yield return step;
                Check(pov.Mode == PovMode.Nurse, "Y switches back to the nurse view");
            }
            finally
            {
                RemovePovDevices();
            }

            // Finish (as holding B): to the doctor, the debrief opens.
            flow.FinishConversation();
            var doctorWait = EditorApplication.timeSinceStartup + 60f;
            while (flow.Current != PrototypeFlow.Stage.Debrief && EditorApplication.timeSinceStartup < doctorWait)
                yield return null;
            Check(flow.Current == PrototypeFlow.Stage.Debrief, $"after the conversation the player goes to the doctor ({flow.Current})");
            var camera = origin.Camera.transform.position;
            Check(Vector3.Distance(Flat(camera), Flat(doctorSpot.position)) < 0.3f, $"the player stands in front of the doctor ({camera})");
            var doctor = GameObject.Find("Doctor (debrief)");
            var toDoctor = doctor != null ? Flat(doctor.GetComponentInChildren<Animator>().transform.position - camera) : Vector3.zero;
            Check(doctor != null && Vector3.Angle(Flat(origin.Camera.transform.forward), toDoctor) > 30f && toDoctor.magnitude < 2.2f,
                $"the doctor stands front left, next to the debrief panel ({toDoctor.magnitude:0.00} m)");
            var debriefWait = EditorApplication.timeSinceStartup + 90f;
            while (debrief.State == AiDebriefPanel.PanelState.Waiting && EditorApplication.timeSinceStartup < debriefWait)
                yield return null;
            Check(debrief.State == AiDebriefPanel.PanelState.Ready, $"the debrief is ready ({debrief.State}, {debrief.Result?.error})");
            yield return 0.5f;
            Screenshot("prototype1-doctor-debrief");

            // Retry: back to the patient, a new conversation with a fresh switch count.
            debrief.Retry();
            var retryWait = EditorApplication.timeSinceStartup + 60f;
            while ((flow.Current != PrototypeFlow.Stage.Talking || !bridge.IsPlaying) && EditorApplication.timeSinceStartup < retryWait)
                yield return null;
            Check(flow.Current == PrototypeFlow.Stage.Talking && bridge.IsPlaying, $"Retry starts a new conversation ({flow.Current})");
            Check(Vector3.Distance(Flat(origin.Camera.transform.position), Flat(patientSpot.position)) < 0.3f, "Retry brings the player back to the patient");
            Check(pov.SwitchesThisConversation == 0, $"a new conversation has a fresh switch count ({pov.SwitchesThisConversation})");
            while (npc.IsSpeaking || npc.Busy)
                yield return null;

            // Done: the end of the prototype.
            flow.FinishConversation();
            doctorWait = EditorApplication.timeSinceStartup + 60f;
            while (flow.Current != PrototypeFlow.Stage.Debrief && EditorApplication.timeSinceStartup < doctorWait)
                yield return null;
            debriefWait = EditorApplication.timeSinceStartup + 90f;
            while (debrief.State != AiDebriefPanel.PanelState.Ready && debrief.State != AiDebriefPanel.PanelState.Failed
                   && EditorApplication.timeSinceStartup < debriefWait)
                yield return null;
            debrief.Done();
            yield return 0.5f;
            Check(flow.Current == PrototypeFlow.Stage.Finished, $"Done ends the prototype ({flow.Current})");
        }

        /// <summary>The ambient light of the room itself, before the patient view multiplied its tint on top.</summary>
        private static float AmbientOutsidePatientView(PatientWorldController world)
        {
            var field = typeof(PatientWorldController).GetField("_ambientBase",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? ((Color)field.GetValue(world)).maxColorComponent : 1f;
        }
    }
}
