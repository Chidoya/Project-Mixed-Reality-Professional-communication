using System.Collections;
using System.Linq;
using ProCommVR.AiTalk;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Player;
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
            var openingClip = npc.GetComponentsInChildren<AudioSource>().FirstOrDefault(a => a.name == "Voice")?.clip;
            var opening = new float[openingClip != null ? openingClip.samples * openingClip.channels : 0];
            var openingRate = openingClip != null ? openingClip.frequency : 16000;
            if (openingClip != null)
                openingClip.GetData(opening, 0);
            Check(holder.Dominant == Emotion.Sadness, $"his opening turn makes him sad ({holder.Current}, AI: {npc.LastTurn?.emotion}, tension {npc.Anger})");
            Screenshot("prototype1-patient");

            // Hands-free talking: no button. A spoken sentence between silences is sent to the AI by itself.
            var voice = Object.FindAnyObjectByType<VoiceActivityTalk>();
            var ptt = Object.FindAnyObjectByType<ProCommVR.Hospital.AiTalk.PushToTalk>();
            Check(voice != null && voice.Listening && (ptt == null || !ptt.enabled), "Prototype1 talks hands-free (push-to-talk is off)");
            if (voice != null && opening.Length > 0)
            {
                voice.StopMicrophone();
                yield return 1f;
                var sent = 0f;
                void OnSent(byte[] wav, float seconds) => sent = seconds;
                voice.SpeechSent += OnSent;
                var turns = 0;
                void OnTurn(ProCommVR.Hospital.AiTalk.AiTurn t) => turns++;
                npc.TurnApplied += OnTurn;
                var noise = new float[openingRate];
                for (var i = 0; i < noise.Length; i++)
                    noise[i] = (Random.value - 0.5f) * 0.004f;
                voice.Feed(noise, openingRate);
                Check(sent == 0f, "silence is not sent");
                // The patient's own (recorded) sentence as if the student said it, then silence.
                voice.Feed(opening, openingRate);
                voice.Feed(noise, openingRate);
                voice.Feed(noise, openingRate);
                Check(sent > 0.5f && Mathf.Abs(sent - opening.Length / (float)openingRate) < 1.2f,
                    $"after the silence the sentence is sent by itself ({sent:0.0} s of {opening.Length / (float)openingRate:0.0} s)");
                var replyWait = EditorApplication.timeSinceStartup + 120f;
                while (turns == 0 && EditorApplication.timeSinceStartup < replyWait)
                    yield return null;
                Check(turns > 0, $"the AI hears the hands-free sentence (heard: \"{npc.LastTurn?.transcript}\", reply: \"{npc.LastTurn?.reply}\")");
                while (npc.IsSpeaking || npc.Busy)
                    yield return null;
                voice.SpeechSent -= OnSent;
                npc.TurnApplied -= OnTurn;
            }

            // The nurse view shows nothing about how the conversation goes: no bar, no HUD, no trust or emotion on the
            // clipboard. Only the patient view does.
            var progressBar = Object.FindAnyObjectByType<ConversationProgressBar>();
            Check(progressBar != null && !progressBar.IsVisible, "the nurse view shows no conversation progress bar");
            Check(!hud.IsShown && !hud.IsCanvasOn, "the nurse view shows no trust and no emotion (the HUD is only in the patient view)");
            var nurseClipboard = Object.FindAnyObjectByType<ProCommVR.Testing.DevClipboard>(FindObjectsInactive.Include);
            Check(nurseClipboard != null && !nurseClipboard.DevMode && nurseClipboard.PageCount == 3
                  && Enumerable.Range(0, nurseClipboard.PageCount).All(i => nurseClipboard.PageName(i) != "Dev"),
                "the clipboard has no Dev page (dev flag off): Case, Guide, Tasks");
            Check(progressBar != null && Mathf.Approximately(progressBar.Targets.x, 55f) && Mathf.Approximately(progressBar.Targets.y, 46f),
                $"the bar reads its limits from the scenario (trust {progressBar?.Targets.x}, tension {progressBar?.Targets.y})");
            Check(progressBar != null && progressBar.Progress >= 0f && progressBar.Progress < 0.95f,
                $"the bar shows how far the conversation is ({progressBar?.Progress:0.00}, trust {npc.Trust}, tension {npc.Anger})");

            // The ward map at the lobby entrance and the murmur of people.
            var map = GameObject.Find("Ward Map (lobby entrance)");
            Check(map != null && map.GetComponentsInChildren<SpriteRenderer>().Length >= 4
                  && map.GetComponentsInChildren<SpriteRenderer>().All(r => r.color.r > 0.9f && r.color.b < 0.2f),
                "the ward map hangs at the lobby entrance with orange points of interest");
            if (map != null)
                ObjectShot(map.transform.position + Vector3.back * 1.7f + Vector3.down * 0.1f, map.transform.position, "prototype1-map");
            var murmurs = Object.FindObjectsByType<ProCommVR.Atmosphere.AmbienceLoop>(FindObjectsSortMode.None)
                .Select(a => a.GetComponent<AudioSource>()).ToList();
            Check(murmurs.Count >= 4 && murmurs.All(a => a.isPlaying && a.clip != null && a.loop && a.volume <= 0.25f)
                  && murmurs.Select(a => a.clip).Distinct().Count() >= 3,
                $"the ambience of people talking plays, quiet and varied ({murmurs.Count} sources, {murmurs.Select(a => a.clip).Distinct().Count()} clips)");

            // The dev clipboard starts in the left hand.
            var clipboardAtStart = Object.FindAnyObjectByType<ProCommVR.Testing.DevClipboard>(FindObjectsInactive.Include);
            Check(clipboardAtStart != null && clipboardAtStart.IsCarried,
                $"the dev clipboard is in the left hand from the start (parent {clipboardAtStart?.transform.parent?.name}, hand {(clipboardAtStart != null ? clipboardAtStart.Hand : null)?.name})");

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
                // What the last sentence did: an arrow next to Trust (and the dominant emotion).
                var trustNow = reputation.Value;
                bridge.HoldTrust = true;
                reputation.Set(trustNow + 6f);
                yield return 0.3f;
                Check(hud.TrustTrend == 1 && hud.TrustLabelText.Contains("+6"), $"a rise of the trust shows an up arrow ({hud.TrustLabelText})");
                reputation.Set(trustNow - 4f);
                yield return 0.3f;
                Check(hud.TrustTrend == -1 && hud.TrustLabelText.Contains("-10"), $"a drop shows a down arrow ({hud.TrustLabelText})");
                holder.SetTarget(new EmotionState(0.05f, 0.05f, 0.05f, 0.9f));
                yield return 0.3f;
                Check(hud.EmotionTrendFor == Emotion.Sadness && hud.EmotionTrend == 1, $"a stronger emotion shows an up arrow next to it ({hud.EmotionTrendFor} {hud.EmotionTrend})");
                Screenshot("prototype1-hud-arrows");
                reputation.Set(trustNow);
                bridge.HoldTrust = false;
                var rain = Object.FindObjectsByType<EmotionEffectAnchor>(FindObjectsSortMode.None).FirstOrDefault(a => a.name.StartsWith("Rain Inside"));
                Check(rain != null && rain.IsOn && rain.Particles.Length > 0 && rain.Particles[0].isPlaying, "Sadness: it rains inside the room");
                var puddles = Object.FindObjectsByType<EmotionEffectAnchor>(FindObjectsSortMode.None).Where(a => a.name.StartsWith("Puddle")).ToList();
                Check(puddles.Count >= 2 && puddles.All(p => p.IsOn), $"Sadness: puddles grow on the floor ({puddles.Count})");
                Check(puddles.All(p => Mathf.Abs(p.transform.position.y - patientSpot.position.y) < 0.05f), "the puddles lie on the floor");
                Screenshot("prototype1-sadness");

                // The player as the patient sees him: a white figure with an aura from the trust.
                var trustBefore2 = reputation.Value;
                var aura = Object.FindAnyObjectByType<ReputationAura>();
                Check(aura != null && aura.Avatar != null && aura.Avatar.IsVisible, "the patient sees the player's figure, with the trust aura");
                if (aura != null && aura.Avatar != null)
                {
                    var renderers = aura.Avatar.GetComponentsInChildren<Renderer>();
                    Check(renderers.Length > 0 && renderers.All(r => r.sharedMaterials.All(m => m == aura.WhiteMaterial)),
                        "the player's figure is blank white");
                    var trustBefore = reputation.Value;
                    trustBefore2 = trustBefore;
                    foreach (var (trust, tier) in new[] { (10f, ReputationTier.Abyss), (30f, ReputationTier.Liquid), (50f, ReputationTier.Neutral),
                                 (70f, ReputationTier.Golden), (90f, ReputationTier.Radiant) })
                    {
                        reputation.Set(trust);
                        yield return 2f;
                        var look = aura.Look(tier);
                        var others = new[] { ReputationTier.Abyss, ReputationTier.Liquid, ReputationTier.Golden, ReputationTier.Radiant }
                            .Where(t => t != tier).Select(aura.Look);
                        Check(aura.Current == tier && others.All(o => !o.root.activeSelf)
                              && (tier == ReputationTier.Neutral || (look.root.activeSelf && Mathf.Approximately(aura.Weight(tier), 1f)
                                                                     && look.particles.All(p => p.particleCount > 0))),
                            $"trust {trust}: {tier} ({aura.Current}, weight {aura.Weight(tier):0.00})");
                        Screenshot($"prototype1-aura-{tier.ToString().ToLowerInvariant()}");
                    }
                    var wings = aura.Look(ReputationTier.Radiant).wings;
                    Check(wings.Length == 2 && wings.All(w => w.GetComponent<MeshFilter>().sharedMesh != null && w.localScale.x > 0.9f),
                        "Radiant: the wings are unfolded");
                    // Inside a tier every change shows: the aura gets stronger the deeper the trust is in it.
                    reputation.Set(38f);
                    yield return 0.2f;
                    var shallow = aura.Depth;
                    reputation.Set(22f);
                    yield return 0.2f;
                    Check(aura.Current == ReputationTier.Liquid && aura.Depth > shallow + 0.4f,
                        $"the black liquid gets stronger as the trust drops within its tier ({shallow:0.00} at 38, {aura.Depth:0.00} at 22)");
                    reputation.Set(trustBefore);
                }

                // The dev clipboard: emotion and trust set by hand stay until "AI drives".
                var clipboard = Object.FindAnyObjectByType<ProCommVR.Testing.DevClipboard>(FindObjectsInactive.Include);
                Check(clipboard != null, "Prototype1 has the dev clipboard");
                if (clipboard != null)
                {
                    // On the desk for the pictures (the left controller is not tracked in batchmode, so the hand is off).
                    clipboard.PutDown();
                    clipboard.ShowFear();
                    clipboard.TrustSlider.value = 85f;
                    yield return holder.BlendSeconds + 0.3f;
                    Check(holder.Dominant == Emotion.Fear && Mathf.Approximately(reputation.Value, 85f) && bridge.HoldEmotions && bridge.HoldTrust,
                        $"the dev clipboard sets Fear and trust 85 ({holder.Current}, {reputation.Value})");
                    Check(clipboard.Page == 0 && clipboard.PageName(0) == "Case", $"the clipboard starts on the case page ({clipboard.Page})");
                    // The case and the guide come from the scenario file (via the AI server), not from Unity.
                    Check(clipboard.CaseText.Contains("GOAL") && clipboard.CaseText.Contains("Be careful with promises")
                          && !clipboard.CaseText.Contains("Do not:"),
                        $"the case page is a briefing with a goal list and a subtle warning, without a list of what is not allowed ({clipboard.CaseText})");
                    Check(!System.Text.RegularExpressions.Regex.IsMatch(clipboard.CaseText + clipboard.GuideText, "walk|stroll|corridor",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase), "the case and the guide never mention walking");
                    ClipboardShot(clipboard, "prototype1-clipboard-case");
                    clipboard.NextPage();
                    yield return null;
                    Check(clipboard.Page == 1 && clipboard.PageName(1) == "Guide", $"> shows the guide page ({clipboard.Page})");
                    Check(clipboard.GuideText.Contains("Rain") && clipboard.GuideText.Contains("Fire") && clipboard.GuideText.Contains("Plants"),
                        $"the guide gives hints per patient state, with what to look for in the patient view ({clipboard.GuideText})");
                    ClipboardShot(clipboard, "prototype1-clipboard-guide");
                    clipboard.NextPage();
                    yield return null;
                    Check(clipboard.Page == 2 && clipboard.PageName(2) == "Tasks", $"> shows the tasks page ({clipboard.Page})");
                    ClipboardShot(clipboard, "prototype1-clipboard-tasks");
                    clipboard.NextPage();
                    Check(clipboard.Page == 0 && clipboard.PageCount == 3, $"the clipboard has 3 pages without dev mode ({clipboard.PageCount})");
                    clipboard.LetAiDrive();
                    Check(!bridge.HoldEmotions && !bridge.HoldTrust, "AI drives again");
                    clipboard.Take();
                    Check(clipboard.IsCarried, "Take puts the clipboard in the left hand");
                    clipboard.PutDown();
                    Check(!clipboard.IsCarried, "Put down puts it back on the desk");
                    clipboard.Take();
                    reputation.Set(trustBefore2);
                    holder.SetImmediate(new EmotionState(0.05f, 0.05f, 0.05f, 0.76f));
                    yield return 1f;
                }

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

                // No limit in Prototype1: Y keeps working after 3 switches.
                Check(pov.Settings.Unlimited, "Prototype1 has no limit on the switches per conversation");
                for (var i = 0; i < 3; i++)
                {
                    foreach (var step in WaitUntilAllowed(pov))
                        yield return step;
                    foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                        yield return step;
                    foreach (var step in WaitUntilAllowed(pov))
                        yield return step;
                    foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                        yield return step;
                }
                Check(pov.SwitchesThisConversation >= 4 && pov.WhyNot() != "no switches left in this conversation",
                    $"Y still works after {pov.SwitchesThisConversation} switches ({pov.WhyNot() ?? "allowed"})");
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
            Check(progressBar == null || Mathf.Approximately(progressBar.Progress, 1f), "the conversation bar is full at the end");
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

            // A good conversation, typed (same lines as Tools/AiServer/test_conversation_length.py): it lasts at least
            // 10 turns before the patient agrees, then the player goes to the doctor.
            foreach (var step in GoodTextConversation(npc))
                yield return step;
            doctorWait = EditorApplication.timeSinceStartup + 60f;
            while (flow.Current != PrototypeFlow.Stage.Debrief && EditorApplication.timeSinceStartup < doctorWait)
                yield return null;
            Check(flow.Current == PrototypeFlow.Stage.Debrief, $"after the resolved conversation the player is at the doctor ({flow.Current})");
            debriefWait = EditorApplication.timeSinceStartup + 90f;
            while (debrief.State == AiDebriefPanel.PanelState.Waiting && EditorApplication.timeSinceStartup < debriefWait)
                yield return null;
            debrief.Retry();
            retryWait = EditorApplication.timeSinceStartup + 60f;
            while ((flow.Current != PrototypeFlow.Stage.Talking || !bridge.IsPlaying) && EditorApplication.timeSinceStartup < retryWait)
                yield return null;
            while (npc.IsSpeaking || npc.Busy)
                yield return null;

            // A critical error: a black screen with "KRITIEKE FOUT", then the doctor (and Done ends the prototype).
            npc.EndNow("critical_error");
            var cardSeen = false;
            doctorWait = EditorApplication.timeSinceStartup + 60f;
            while (flow.Current != PrototypeFlow.Stage.Debrief && EditorApplication.timeSinceStartup < doctorWait)
            {
                if (flow.IsEndingCardShown && !cardSeen)
                {
                    cardSeen = true;
                    var card = Object.FindAnyObjectByType<EndingCard>();
                    Check(card != null && card.Title == "KRITIEKE FOUT", $"a critical error shows KRITIEKE FOUT on a black screen ({card?.Title})");
                }
                yield return null;
            }
            Check(cardSeen, "the critical error ends on the black screen before the doctor");
            Check(!flow.IsEndingCardShown && Vector3.Distance(Flat(origin.Camera.transform.position), Flat(doctorSpot.position)) < 0.3f,
                "after the black screen the player stands at the doctor");
            debriefWait = EditorApplication.timeSinceStartup + 90f;
            while (debrief.State != AiDebriefPanel.PanelState.Ready && debrief.State != AiDebriefPanel.PanelState.Failed
                   && EditorApplication.timeSinceStartup < debriefWait)
                yield return null;
            debrief.Done();
            yield return 0.5f;
            Check(flow.Current == PrototypeFlow.Stage.Finished, $"Done ends the prototype ({flow.Current})");
        }

        internal static readonly string[] GoodLines =
        {
            "Hello, I'm Jalil, one of the nurses on this ward. How are you feeling today?",
            "I can see you're really sad and disappointed. That must be hard.",
            "It sounds like today hurts a lot. What is on your mind?",
            "What is it about the birthday that matters most to you?",
            "So missing her party feels like letting her down. Did I get that right?",
            "You have to stay because the infection in your leg still needs the antibiotics through the drip. Only the doctor decides about going home.",
            "The doctor comes by this evening, you can ask her about it then.",
            "I'm here for you. We'll find something that helps you today.",
            "What if we set up a video call with your granddaughter during the party?",
            "And shall I help you sit up a little, so you're ready for the call?",
            "We could also get you something to drink first. How does that sound?",
            "Then let's do it: I help you sit up and we call her this afternoon.",
            "Great. I'll bring the tablet and we'll call her together.",
            "I'm glad you want to try. Shall we sit you up now?",
            "Okay, I'll help you sit up and then we call her.",
            "Here we go, slowly. Tell me if it hurts.",
        };

        /// <summary>
        /// Types the good lines one by one (AiNpc.SendStudentText) until the AI server ends the conversation. Checks that
        /// it does not end before turn 10 and that it ends resolved.
        /// </summary>
        private static IEnumerable GoodTextConversation(AiNpc npc)
        {
            var turns = 0;
            foreach (var line in GoodLines)
            {
                if (npc.HasEnded)
                    break;
                var waitIdle = EditorApplication.timeSinceStartup + 120f;
                while ((npc.Busy || npc.IsSpeaking) && EditorApplication.timeSinceStartup < waitIdle)
                    yield return null;
                var task = npc.SendStudentText(line);
                var replyWait = EditorApplication.timeSinceStartup + 120f;
                while (!task.GetAwaiter().IsCompleted && EditorApplication.timeSinceStartup < replyWait)
                    yield return null;
                turns++;
                var turn = npc.LastTurn;
                Log($"text turn {turns}: \"{line}\" -> labels [{string.Join(", ", turn?.labels ?? new string[0])}], " +
                    $"trust {turn?.trust}, tension {turn?.anger}, phase {turn?.phase}, ended {turn?.ended}: \"{turn?.reply}\"");
                if (turn != null && turn.HasEnded)
                    break;
            }
            Check(npc.EndReason == "resolved" || npc.LastTurn?.ended == "resolved",
                $"the typed good conversation ends resolved ({npc.LastTurn?.ended ?? "not ended"} after {turns} turns)");
            Check(turns >= 10, $"a good conversation takes at least 10 turns before END ({turns})");
        }

        /// <summary>A picture from a temporary camera at a point, looking at a target.</summary>
        private static void ObjectShot(Vector3 from, Vector3 target, string name)
        {
            if (string.IsNullOrEmpty(_screenshotFolder))
                return;
            var cameraObject = new GameObject("Object Shot Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.transform.position = from;
            camera.transform.LookAt(target);
            var texture = new RenderTexture(1280, 960, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            System.IO.Directory.CreateDirectory(_screenshotFolder);
            var path = System.IO.Path.Combine(_screenshotFolder, $"{++_screenshotIndex:00}-{name}.png");
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            camera.targetTexture = null;
            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
            Log($"screenshot {path}");
        }

        /// <summary>A picture of the clipboard from above (a temporary camera; it is hidden in the patient view).</summary>
        private static void ClipboardShot(ProCommVR.Testing.DevClipboard clipboard, string name)
        {
            if (string.IsNullOrEmpty(_screenshotFolder))
                return;
            var visual = clipboard.transform.Find("Visual").gameObject;
            var wasActive = visual.activeSelf;
            visual.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var cameraObject = new GameObject("Clipboard Shot Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.02f;
            // From the visitor side, above the desk, looking down at the board.
            var board = clipboard.transform;
            camera.transform.position = board.position + board.forward * 0.25f + Vector3.up * 0.42f;
            camera.transform.LookAt(board.position);
            var texture = new RenderTexture(1280, 960, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            System.IO.Directory.CreateDirectory(_screenshotFolder);
            var path = System.IO.Path.Combine(_screenshotFolder, $"{++_screenshotIndex:00}-{name}.png");
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            camera.targetTexture = null;
            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
            visual.SetActive(wasActive);
            Log($"screenshot {path}");
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
