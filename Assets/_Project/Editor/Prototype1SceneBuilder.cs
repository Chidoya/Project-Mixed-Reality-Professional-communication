using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProCommVR.AiTalk;
using ProCommVR.Emotions;
using ProCommVR.Hospital;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Builds Prototype1 (Assets/_Project/Scenes/Prototype1.unity): one patient, free speech through Liam's AI server,
    /// in Liam's U-ward (Assets/Scenes/liam_u_afdeling.unity, built by his UWardBuilder; his scene on disk stays as it is).
    /// <list type="bullet">
    /// <item>The player starts in the lobby at the reception desk and starts the shift at its computer (AiShift, one task).</item>
    /// <item>Room 1A: Liam's patient (Patient_1A, lying in bed) gets Liam's AiNpc (scenario disappointed_patient), our
    /// emotions, the trust (reputation) and the AI bridge; Y switches to his eyes (patient view, emotion HUD with the
    /// trust meter, the room reacts to his emotion: plants, sun, fire, darkness, rain inside and puddles).</item>
    /// <item>After the conversation: to the doctor in the briefing room, who opens Liam's debrief panel (PrototypeFlow).</item>
    /// </list>
    /// Only works in the Editor or over Quest Link: the AI server runs on the laptop (Tools/AiServer). Batchmode:
    ///   -executeMethod ProCommVR.Editor.Prototype1SceneBuilder.BuildScene
    /// </summary>
    public static class Prototype1SceneBuilder
    {
        internal const string ScenePath = "Assets/_Project/Scenes/Prototype1.unity";
        internal const string WardScenePath = "Assets/Scenes/liam_u_afdeling.unity";
        internal const string ScenarioId = "disappointed_patient";
        private const string HospitalRootName = "Hospital";
        private const string RoomLabel = "1A";
        private const string DoctorModel = "Doctor_Female";
        private const string LogPrefix = "[ProCommVR] Prototype1: ";

        // Plan points (UWardLayout: x from left to right, y from top to bottom, in meters).
        // Room 1A: x 0..6, y 7.20..12.13, door in the corridor wall x = 6 between y 10.58 and 11.88.
        private static readonly Vector2 DoorPlan = new Vector2(6.0f, 11.23f);
        // Briefing room: inner x 25.1..33.5, y 12.7..19.5; table 4.6 x 1.8 m in the middle, whiteboard on the north wall.
        // The player stands between the whiteboard and the north row of chairs, looking at the whiteboard; the doctor stands
        // in front of the whiteboard, front left of the player (about 1.5 m, 45 degrees), so the debrief panel straight
        // ahead never goes through the doctor.
        private static readonly Vector2 DoctorPlan = new Vector2(28.2f, 13.4f);
        private static readonly Vector2 DoctorSpotPlan = new Vector2(29.3f, 14.45f);
        private static readonly Vector2 WhiteboardPlan = new Vector2(29.3f, 12.7f);

        private static readonly string[] TeleportAreas = { "Hall", "CorridorLeft", "CorridorBottom", "CorridorRight", "Room_1A", "BriefingRoom" };
        private static readonly Color SignTextColor = new Color(0.12f, 0.12f, 0.14f);

        [MenuItem("ProCommVR/Setup/12. Build Prototype 1 (AI patient, U-ward)")]
        public static void BuildScene()
        {
            if (!File.Exists(WardScenePath))
                throw new FileNotFoundException("[ProCommVR] Liam's U-ward scene is missing (origin/Liam, liam_u_afdeling).", WardScenePath);

            // Open the scene first: opening a scene unloads assets that were loaded before it.
            var scene = EditorSceneManager.OpenScene(WardScenePath, OpenSceneMode.Single);
            var hospital = scene.GetRootGameObjects().FirstOrDefault(g => g.name == HospitalRootName)?.transform;
            if (hospital == null)
                throw new System.InvalidOperationException($"[ProCommVR] No '{HospitalRootName}' in {WardScenePath}.");

            var rigVariant = ScenarioTestSceneBuilder.LoadRequired<GameObject>(ScenarioTestSceneBuilder.RigVariantPath, "Run 'Build XR Test Scene' first.");
            var bubblePrefab = ScenarioTestSceneBuilder.LoadRequired<GameObject>(ScenarioTestSceneBuilder.BubblePrefabPath, "Run 'Build Dialogue Bubble Prefab' first.");
            var settings = AiTalkSceneBuilder.EnsureSettings();
            var talkAction = AiTalkSceneBuilder.EnsureInputActions();

            // The XR rig brings its own camera.
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(camera.gameObject);
            AddTeleportAreas(hospital);
            OpenDoor(RequireChild(hospital, $"Floor1/Door_{RoomLabel}"), RequireChild(hospital, $"Floor1/Room_{RoomLabel}"));

            var room = RequireChild(hospital, $"Floor1/Room_{RoomLabel}");
            var patient = RequireChild(hospital, $"Characters/Patient_{RoomLabel}");
            LogRoom(room);
            Physics.SyncTransforms();

            var prototype = new GameObject("Prototype1").transform;
            var places = new GameObject("Places").transform;
            places.SetParent(prototype, false);

            // ---- Player: starts in the lobby, in front of the reception desk (the shift starts at its computer) ----
            var station = hospital.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "StaffStation");
            var deskAnchor = station != null ? station.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Anchor_Clipboard") : null;
            if (deskAnchor == null)
                throw new System.InvalidOperationException("[ProCommVR] No Lobby/StaffStation/Anchor_Clipboard: the shift needs the reception desk.");
            var visitorSide = Flat(deskAnchor.forward).normalized;
            var start = Spot(places, "Start (lobby, reception desk)", OnFloor(deskAnchor.position + visitorSide * 0.95f), -visitorSide);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigVariant, scene);
            rig.transform.SetPositionAndRotation(start.position, start.rotation);
            var origin = rig.GetComponent<XROrigin>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule)).transform.SetParent(prototype, false);

            // ---- The patient: Liam's AI conversation, our emotions and trust ----
            // Liam's push-to-talk stays in the scene but off: the player talks hands-free (VoiceActivityTalk below).
            var ptt = new GameObject("PushToTalk (off, hands-free talk)").AddComponent<PushToTalk>();
            ptt.enabled = false;
            ptt.transform.SetParent(prototype, false);
            Set(ptt, "_settings", settings);
            Set(ptt, "_talkAction", talkAction);

            var npc = patient.gameObject.AddComponent<AiNpc>();
            var serializedNpc = new SerializedObject(npc);
            serializedNpc.FindProperty("_settings").objectReferenceValue = settings;
            serializedNpc.FindProperty("_pushToTalk").objectReferenceValue = ptt;
            serializedNpc.FindProperty("_scenarioId").stringValue = ScenarioId;
            serializedNpc.FindProperty("_autoStart").boolValue = false; // PrototypeFlow starts it when the player is near
            serializedNpc.ApplyModifiedPropertiesWithoutUndo();
            // Always listening: what the player says is sent after a short silence (no button, no lost first words).
            var voice = new GameObject("Voice Activity Talk (always on)").AddComponent<VoiceActivityTalk>();
            voice.transform.SetParent(prototype, false);
            Set(voice, "_npc", npc);
            // The orange bar at the bottom of the view: how far the conversation is to its end (calm or escalated).
            var progress = new GameObject("Conversation Progress Bar").AddComponent<ConversationProgressBar>();
            progress.transform.SetParent(prototype, false);
            Set(progress, "_npc", npc);
            Set(progress, "_camera", origin.Camera);
            Set(progress, "_imageMaterial", AssetDatabase.LoadAssetAtPath<Material>(EmotionHudSetup.ImageMaterialPath));
            Set(progress, "_textMaterial", AssetDatabase.LoadAssetAtPath<Material>(EmotionHudSetup.TextMaterialPath));
            // The ward map at the lobby entrance and the murmur of people in the lobby and the corridors.
            var map = WardMapAndAmbienceSetup.AddMap(prototype, new Vector2(15.0f, 20.3f));
            var mapTask = WardMapAndAmbienceSetup.AddMapNote(map, new Vector2(3.0f, 9.7f), "TASK 1");
            WardMapAndAmbienceSetup.AddAmbience(prototype);

            var emotions = patient.gameObject.AddComponent<EmotionHolder>();
            var reputation = patient.gameObject.AddComponent<PatientReputation>();
            var bridge = patient.gameObject.AddComponent<AiPatientBridge>();
            var serializedBridge = new SerializedObject(bridge);
            serializedBridge.FindProperty("_npc").objectReferenceValue = npc;
            serializedBridge.FindProperty("_holder").objectReferenceValue = emotions;
            serializedBridge.FindProperty("_reputation").objectReferenceValue = reputation;
            var head = patient.GetComponentInChildren<Animator>()?.GetBoneTransform(HumanBodyBones.Head);
            serializedBridge.FindProperty("_head").objectReferenceValue = head;
            serializedBridge.ApplyModifiedPropertiesWithoutUndo();

            // ---- Patient view: Y, the HUD with the trust meter, the room's reaction ----
            var bed = TightBoundsOf(room, "Furniture/Bed");
            var bedLength = Mathf.Abs(Vector3.Dot(bed.size, room.forward));
            // The player stands past the foot of the bed, looking at the patient (the bed's head is against the outer wall).
            var patientSpotPoint = OnFloor(new Vector3(bed.center.x, room.position.y, bed.center.z) - room.forward * (bedLength / 2f + 0.75f));
            var patientSpot = Spot(places, "Conversation (foot of the bed)", patientSpotPoint, room.forward);
            // Lying in bed, the patient looks to the foot of his bed and the door.
            var lookAt = new GameObject("Patient Looks At (foot of the bed)").transform;
            lookAt.SetParent(places, false);
            lookAt.position = patientSpotPoint + Vector3.up * 1.2f;

            var pov = PovSetup.AddToScene(null, patient, origin);
            pov.name = $"POV Controller (Room {RoomLabel})";
            pov.transform.SetParent(prototype, false);
            var serializedPov = new SerializedObject(pov);
            serializedPov.FindProperty("_conversationSource").objectReferenceValue = bridge;
            serializedPov.ApplyModifiedPropertiesWithoutUndo();
            var anchor = patient.GetComponent<PovAnchor>();
            var serializedAnchor = new SerializedObject(anchor);
            serializedAnchor.FindProperty("_lookAt").objectReferenceValue = lookAt;
            serializedAnchor.ApplyModifiedPropertiesWithoutUndo();

            var hud = EmotionHudSetup.AddToScene(emotions, pov, reputation);
            hud.transform.SetParent(prototype, true);
            // What the last sentence did: an arrow next to Trust and the dominant emotion (only in the patient view).
            SetBool(hud, "_showLastChange", true);
            // The player as the patient sees him: a white figure with an aura from the trust (black waves to wings).
            var aura = ReputationAuraSetup.AddToScene(reputation, rig.GetComponentInChildren<ProCommVR.Player.PlayerAvatar>(true));
            aura.transform.SetParent(prototype, true);
            // Every change of the trust shows in the aura, not only a change of tier.
            SetFloat(aura, "_edgeStrength", 0.35f);
            // A wider range of strength, so the room follows the tension (the rain gets heavier or lighter).
            SetFloat(bridge, "_minStrength", 0.2f);
            SetFloat(bridge, "_maxStrength", 1f);
            // The clipboard on the lobby desk: a task template and the dev controls (emotion, trust slider).
            // The case and the guide of the scenario, from the AI server (scenario file), for the clipboard.
            var briefingData = new GameObject("Scenario Briefing (case, guide)").AddComponent<ScenarioBriefing>();
            briefingData.transform.SetParent(prototype, false);
            Set(briefingData, "_npc", npc);
            var devClipboard = DevClipboardSetup.AddToScene(hospital, emotions, reputation, bridge, pov, rig, origin.Camera, voice, briefingData);
            devClipboard.transform.SetParent(prototype, true);
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            var world = EmotionWorldSetup.AddToScene(emotions, pov, LightsReaching(room, sun, bed));
            world.name = $"Patient World (Room {RoomLabel})";
            world.transform.SetParent(prototype, true);
            BuildEffects(room, bed, patientSpotPoint, origin.Camera.transform);

            // ---- Debrief: Liam's panel, opened by the flow at the doctor ----
            var debrief = new GameObject("AiDebriefPanel").AddComponent<AiDebriefPanel>();
            debrief.transform.SetParent(prototype, false);
            var serializedDebrief = new SerializedObject(debrief);
            serializedDebrief.FindProperty("_npc").objectReferenceValue = npc;
            serializedDebrief.FindProperty("_openOnConversationEnd").boolValue = false;
            serializedDebrief.ApplyModifiedPropertiesWithoutUndo();

            // ---- The doctor in the briefing room ----
            var doctorSpot = Spot(places, "Doctor (briefing room)", OnFloor(Plan(DoctorSpotPlan)), Plan(WhiteboardPlan) - Plan(DoctorSpotPlan));
            var doctorBubble = BuildDoctor(prototype, bubblePrefab, doctorSpot);

            // ---- Signs ----
            var signs = new GameObject("Signs").transform;
            signs.SetParent(prototype, false);
            // Above the open door of 1A: Liam's patient card next to the door already tells who the patient is.
            var briefing = CreateSign(signs, "Briefing Sign",
                "<b>Room 1A: Mr. X, 51</b>\n" +
                "Leg infection, antibiotics through a drip for a few more days. Only the doctor decides about discharge.\n" +
                "Today he misses his granddaughter's birthday.\n" +
                "Read <b>Case</b> and <b>Guide</b> on your clipboard. Talk in English.  <b>Y</b>: see through his eyes.",
                DoorTop(room) + Vector3.up * 0.35f - room.forward * 0.12f, room.forward, 2.2f);
            briefing.fontSize = 0.42f;
            var talkHint = CreateSign(signs, "Talk Hint", "",
                patientSpotPoint + room.forward * 0.9f + room.right * -0.9f + Vector3.up * 1.95f, room.forward, 1.6f);
            talkHint.fontSize = 0.55f;

            // ---- The loop ----
            var flow = new GameObject("Prototype Flow").AddComponent<PrototypeFlow>();
            flow.transform.SetParent(prototype, false);
            var serializedFlow = new SerializedObject(flow);
            serializedFlow.FindProperty("_patient").objectReferenceValue = npc;
            serializedFlow.FindProperty("_bridge").objectReferenceValue = bridge;
            serializedFlow.FindProperty("_debrief").objectReferenceValue = debrief;
            serializedFlow.FindProperty("_pov").objectReferenceValue = pov;
            serializedFlow.FindProperty("_origin").objectReferenceValue = origin;
            serializedFlow.FindProperty("_fade").objectReferenceValue = origin.Camera.GetComponentInChildren<ScreenFade>(true);
            serializedFlow.FindProperty("_patientSpot").objectReferenceValue = patientSpot;
            serializedFlow.FindProperty("_doctorSpot").objectReferenceValue = doctorSpot;
            serializedFlow.FindProperty("_doctorBubble").objectReferenceValue = doctorBubble;
            serializedFlow.FindProperty("_briefingSign").objectReferenceValue = briefing;
            serializedFlow.FindProperty("_talkHint").objectReferenceValue = talkHint;
            serializedFlow.FindProperty("_voice").objectReferenceValue = voice;
            // Critical error or escalated: a black screen with what happened, then the doctor.
            var endingCard = new GameObject("Ending Card").AddComponent<EndingCard>();
            endingCard.transform.SetParent(prototype, false);
            Set(endingCard, "_camera", origin.Camera);
            Set(endingCard, "_textMaterial", AssetDatabase.LoadAssetAtPath<Material>(EmotionHudSetup.TextMaterialPath));
            serializedFlow.FindProperty("_endingCard").objectReferenceValue = endingCard;
            serializedFlow.FindProperty("_waitForShiftStart").boolValue = true; // nothing runs before the shift starts
            serializedFlow.ApplyModifiedPropertiesWithoutUndo();

            // ---- The shift: one task (data in the inspector of AiShift), started at the reception desk ----
            var doorTask = CreateSign(signs, "Door Task Sign",
                "<b>TASK 1: Mr. X</b>\n<size=70%>your patient is in here</size>",
                DoorTop(room) - Vector3.up * 0.55f - room.forward * 0.12f + room.right * 0.95f, room.forward, 1.2f);
            doorTask.fontSize = 0.6f;
            doorTask.color = WardMapAndAmbienceSetup.Orange;
            // Above Liam's debrief panel (it fills the view straight ahead), in front of the whiteboard.
            var report = CreateSign(signs, "Shift Report",
                "", doctorSpot.position + Flat(doctorSpot.forward).normalized * 1.6f + Vector3.up * 2.3f, doctorSpot.forward, 1.6f);
            report.fontSize = 0.32f;
            report.alignment = TextAlignmentOptions.Center;
            report.rectTransform.sizeDelta = new Vector2(1.6f, 0.42f);
            SetLabels(debrief, "Try again (A / R)", "Finish shift (B / Enter)");

            var shift = new GameObject("AI Shift (one task)").AddComponent<AiShift>();
            shift.transform.SetParent(prototype, false);
            var serializedShift = new SerializedObject(shift);
            serializedShift.FindProperty("_flow").objectReferenceValue = flow;
            serializedShift.FindProperty("_debrief").objectReferenceValue = debrief;
            serializedShift.FindProperty("_clipboard").objectReferenceValue = devClipboard;
            serializedShift.FindProperty("_endingCard").objectReferenceValue = endingCard;
            serializedShift.FindProperty("_fade").objectReferenceValue = origin.Camera.GetComponentInChildren<ScreenFade>(true);
            serializedShift.FindProperty("_report").objectReferenceValue = report;
            var running = serializedShift.FindProperty("_showWhenRunning");
            running.arraySize = 2;
            running.GetArrayElementAtIndex(0).objectReferenceValue = doorTask.gameObject;
            running.GetArrayElementAtIndex(1).objectReferenceValue = mapTask;
            serializedShift.ApplyModifiedPropertiesWithoutUndo();

            // The computer on the reception desk: point and pull the trigger to start the shift.
            var computer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            computer.name = "Shift Computer (start shift)";
            computer.transform.SetParent(prototype, false);
            computer.transform.SetPositionAndRotation(deskAnchor.position + Vector3.up * 0.22f - visitorSide * 0.15f,
                Quaternion.LookRotation(-visitorSide));
            computer.transform.localScale = new Vector3(0.42f, 0.28f, 0.03f);
            computer.GetComponent<MeshRenderer>().sharedMaterial = SignMaterial();
            computer.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var button = computer.AddComponent<AiShiftStartButton>();
            var buttonLabel = CreateSign(signs, "Shift Computer Label", "",
                deskAnchor.position + Vector3.up * 0.62f - visitorSide * 0.15f, -visitorSide, 0.9f);
            buttonLabel.fontSize = 0.55f;
            var serializedButton = new SerializedObject(button);
            serializedButton.FindProperty("_shift").objectReferenceValue = shift;
            serializedButton.FindProperty("_label").objectReferenceValue = buttonLabel;
            serializedButton.ApplyModifiedPropertiesWithoutUndo();

            // Saved under a new path, last in the Build Settings: a headset build still starts with the shift.
            ScenarioTestSceneBuilder.SaveSceneInBuild(scene, ScenePath);
            Debug.Log($"{LogPrefix}built. Start {start.position}, conversation spot {patientSpotPoint}, doctor spot {doctorSpot.position}.");
        }

        // ---- Effects of the patient view in room 1A ----------------------------------------------------------------

        /// <summary>
        /// The effect anchors of room 1A in Liam's U-ward room (origin in the middle of the corridor wall, forward into
        /// the room, the bed's head against the outer wall and the window in it). Every point on the floor is checked:
        /// it goes onto the floor (raycast) and only where there is no furniture or wall, so nothing floats or sticks out
        /// of a wall (the feedback of the playtest of 2026-11-22).
        /// </summary>
        private static void BuildEffects(Transform room, Bounds bed, Vector3 playerSpot, Transform camera)
        {
            var effects = new GameObject($"Emotion Effects (Room {RoomLabel})").transform;
            effects.SetParent(room, true);
            var f = Flat(room.forward).normalized;
            var r = Vector3.Cross(Vector3.up, f);
            var floor = OnFloor(room.position + f * 2.8f).y;
            var ceiling = CeilingAbove(room.position + f * 2.8f, floor);
            Vector3 Room(float u, float v) => new Vector3(room.position.x, floor, room.position.z) + r * u + f * v;
            var obstacles = Obstacles(room);
            bool Free(Vector3 point, float radius, float height) => IsFree(obstacles, point, radius, height, playerSpot);

            // Joy: three plants on the floor, in the free corners and along the walls; sunlight through the window.
            var plantModels = new[] { ("pottedPlant", 1.0f), ("plantSmall3", 0.6f), ("plantSmall2", 0.5f) };
            var plantCandidates = new List<Vector3>();
            foreach (var v in new[] { 5.2f, 4.4f, 3.4f, 2.4f, 1.4f, 0.6f })
                foreach (var u in new[] { 2.1f, -2.1f, 1.5f, -1.5f })
                    plantCandidates.Add(Room(u, v));
            var plants = PickFree(plantCandidates, 3, p => Free(p, 0.3f, 1.0f), 1.2f);
            for (var i = 0; i < plants.Count; i++)
                EmotionWorldSetup.AddPlant(effects, $"Plant {i + 1}", OnFloor(plants[i]), plantModels[i].Item1, plantModels[i].Item2, 30f + 75f * i);
            var window = FindWindow(room);
            if (window.HasValue)
            {
                var w = window.Value;
                EmotionWorldSetup.AddSunBeam(effects, w.center + f * 0.8f + Vector3.up * 0.4f, Room(0f, 2.6f),
                    w.center - f * 0.9f, new Vector3(1.2f, 1.6f, 1.4f));
            }
            else
                Debug.LogWarning($"{LogPrefix}no window found in room {RoomLabel}: no sunbeam.");

            // Anger: fires on the nightstand, at the foot of the bed and on the floor; one flickering light.
            var nightstand = FindFurniture(room, "nightstand", "cabinetbed", "cabinet");
            if (nightstand.HasValue)
                EmotionWorldSetup.AddFire(effects, "Fire Nightstand", new Vector3(nightstand.Value.center.x, nightstand.Value.max.y, nightstand.Value.center.z), 0.25f);
            var bedLength = Mathf.Abs(Vector3.Dot(bed.size, f));
            EmotionWorldSetup.AddFire(effects, "Fire Bed", new Vector3(bed.center.x, bed.max.y - 0.05f, bed.center.z) - f * (bedLength / 2f - 0.25f), 0.3f);
            var fireFloor = PickFree(plantCandidates.Where(p => !plants.Contains(p)).ToList(), 1, p => Free(p, 0.3f, 0.6f), 0f);
            if (fireFloor.Count > 0)
                EmotionWorldSetup.AddFire(effects, "Fire Floor", OnFloor(fireFloor[0]), 0.35f);
            EmotionWorldSetup.AddFireLight(effects, Room(0f, 3.0f) + Vector3.up * 1.4f);

            // Fear: the room goes (very) dark, only the flashlight on the player's head lights it.
            EmotionWorldSetup.AddFlashlight(effects, camera);

            // Sadness: it rains inside the room (from just under the ceiling to the floor), puddles grow on the floor and
            // a few drops fall from the ceiling.
            var height = ceiling - floor - 0.08f;
            EmotionWorldSetup.AddIndoorRain(effects, "Rain Inside", Room(0f, 2.9f), new Vector2(4.4f, 5.0f), height, room.eulerAngles.y);
            var puddleCandidates = new List<Vector3>();
            foreach (var v in new[] { 1.6f, 2.4f, 3.2f, 0.9f })
                foreach (var u in new[] { -1.2f, 1.2f, 0f, -1.9f, 1.9f })
                    puddleCandidates.Add(Room(u, v));
            var puddles = PickFree(puddleCandidates, 3, p => Free(p, 0.35f, 0.05f), 1.0f);
            for (var i = 0; i < puddles.Count; i++)
                EmotionWorldSetup.AddPuddle(effects, $"Puddle {i + 1}", OnFloor(puddles[i]), 0.35f + 0.1f * i, 1.3f, 25f + 60f * i);
            EmotionWorldSetup.AddDrips(effects, "Drips Bed", new Vector3(bed.center.x, ceiling - 0.02f, bed.center.z) - f * (bedLength / 2f + 0.3f), height);
            EmotionWorldSetup.AddDrips(effects, "Drips Middle", Room(0.8f, 2.0f) + Vector3.up * (height + 0.06f), height);

            Debug.Log($"{LogPrefix}effects in room {RoomLabel}: floor {floor:0.00} m, ceiling {ceiling:0.00} m, {plants.Count} plants, " +
                      $"{puddles.Count} puddles, nightstand {(nightstand.HasValue ? "found" : "not found")}.");
        }

        /// <summary>
        /// The first `count` points that are free, at least `spacing` meters apart.
        /// </summary>
        private static List<Vector3> PickFree(List<Vector3> candidates, int count, System.Func<Vector3, bool> free, float spacing)
        {
            var picked = new List<Vector3>();
            foreach (var point in candidates)
            {
                if (picked.Count >= count)
                    break;
                if (picked.Any(p => Vector3.Distance(p, point) < spacing) || !free(point))
                    continue;
                picked.Add(point);
            }
            if (picked.Count < count)
                Debug.LogWarning($"{LogPrefix}only {picked.Count} of {count} free floor points found.");
            return picked;
        }

        /// <summary>
        /// Everything that stands on the floor near the room: furniture, walls, the bathroom, characters (bounds of their
        /// renderers). Floors, ceilings and very thin things are left out.
        /// </summary>
        private static List<Bounds> Obstacles(Transform room)
        {
            var area = new Bounds(room.position + room.forward * 3f + Vector3.up * 1.5f, new Vector3(9f, 4f, 9f));
            var list = new List<Bounds>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer.GetComponentInParent<EmotionEffectAnchor>(true) != null)
                    continue;
                var bounds = renderer.bounds;
                if (!bounds.Intersects(area) || bounds.size.y < 0.08f)
                    continue;
                // A floor or ceiling plate: wide and thin.
                if (bounds.size.y < 0.3f && bounds.size.x > 2f && bounds.size.z > 2f)
                    continue;
                list.Add(bounds);
            }
            return list;
        }

        private static bool IsFree(List<Bounds> obstacles, Vector3 point, float radius, float height, Vector3 playerSpot)
        {
            // Keep the place where the player stands (and the way from the door to it) free.
            if (Vector3.Distance(Flat(point), Flat(playerSpot)) < 0.8f)
                return false;
            var box = new Bounds(point + Vector3.up * (0.03f + height / 2f), new Vector3(radius * 2f, height, radius * 2f));
            return !obstacles.Any(b => b.Intersects(box));
        }

        private static Bounds? FindWindow(Transform room)
        {
            // Liam's windows are in the outer wall of the room, named Window_1A_1 (anywhere under the hospital).
            var hospital = room.root;
            var window = hospital.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == $"Window_{RoomLabel}_1");
            if (window == null)
                return null;
            var renderers = window.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return null;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Bounds? FindFurniture(Transform room, params string[] names)
        {
            foreach (var name in names)
            {
                var item = room.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name.ToLowerInvariant().Contains(name) && t.GetComponentInChildren<Renderer>() != null);
                if (item != null)
                    return Scenario01SceneBuilder.TightBounds(item.gameObject);
            }
            return null;
        }

        // ---- The doctor ----------------------------------------------------------------------------------------------

        private static DialogueBubble BuildDoctor(Transform parent, GameObject bubblePrefab, Transform playerSpot)
        {
            var root = new GameObject("Doctor (debrief)").transform;
            root.SetParent(parent, false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Character/{DoctorModel}/{DoctorModel}.prefab");
            var position = OnFloor(Plan(DoctorPlan));
            var towardPlayer = Flat(playerSpot.position - position).normalized;
            if (prefab != null)
            {
                var doctor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                doctor.name = DoctorModel;
                doctor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(towardPlayer));
                doctor.AddComponent<CharacterPose>().Configure("Talking", 0f);
            }
            else
                Debug.LogWarning($"{LogPrefix}no {DoctorModel} prefab: the doctor is missing (the debrief still works).");

            var bubble = (GameObject)PrefabUtility.InstantiatePrefab(bubblePrefab, root);
            bubble.name = "Doctor Bubble";
            var bubblePosition = position + Vector3.up * 2.05f;
            bubble.transform.SetPositionAndRotation(bubblePosition,
                Quaternion.LookRotation(Flat(bubblePosition - (playerSpot.position + Vector3.up * 1.6f)).normalized));
            bubble.SetActive(false);
            return bubble.GetComponent<DialogueBubble>();
        }

        // ---- Teleport, lights, helpers ---------------------------------------------------------------------------------

        /// <summary>
        /// Liam left empty markers for the XR setup (Hospital/Teleport/TeleportArea_*, a thin collider at floor level):
        /// the ones Prototype1 needs become teleport areas of the XR Interaction Toolkit.
        /// </summary>
        private static void AddTeleportAreas(Transform hospital)
        {
            foreach (var name in TeleportAreas)
            {
                var area = hospital.Find($"Teleport/TeleportArea_{name}");
                if (area == null)
                {
                    Debug.LogWarning($"{LogPrefix}no teleport marker {name}.");
                    continue;
                }
                if (area.GetComponent<TeleportationArea>() == null)
                    area.gameObject.AddComponent<TeleportationArea>();
            }
        }

        /// <summary>
        /// Liam's door is one mesh (frame and leaf, no collider): it swings 90 degrees into the room around its hinge, as in
        /// the shift, so the room is open.
        /// </summary>
        private static void OpenDoor(Transform door, Transform room)
        {
            var bounds = Scenario01SceneBuilder.TightBounds(door.gameObject);
            var along = Flat(room.right).normalized;
            var halfWidth = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(along.x), 0f, Mathf.Abs(along.z))));
            var hinge = bounds.center - along * halfWidth;
            door.RotateAround(hinge, Vector3.up, Vector3.SignedAngle(along, room.forward, Vector3.up));
        }

        /// <summary>The middle of the doorway of 1A (plan x 6, y 11.23), at the top of the door.</summary>
        private static Vector3 DoorTop(Transform room) => Plan(DoorPlan) + Vector3.up * UWardLayout.DoorHeight;

        /// <summary>The sun and the point and spot lights whose range reaches the room.</summary>
        private static Light[] LightsReaching(Transform room, Light sun, Bounds bed)
        {
            var bounds = new Bounds(room.position + room.forward * 2.8f + Vector3.up * 1.5f, new Vector3(5.5f, 3.2f, 5.5f));
            var lights = new List<Light>();
            if (sun != null)
                lights.Add(sun);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                if (light == sun || light.type == LightType.Directional || light.GetComponentInParent<EmotionEffectAnchor>(true) != null)
                    continue;
                var position = light.transform.position;
                if (Vector3.Distance(bounds.ClosestPoint(position), position) <= light.range)
                    lights.Add(light);
            }
            return lights.ToArray();
        }

        private static void LogRoom(Transform room)
        {
            var names = room.GetComponentsInChildren<Transform>(true).Where(t => t.parent == room || t.parent?.parent == room)
                .Select(t => t.parent == room ? t.name : $"{t.parent.name}/{t.name}");
            Debug.Log($"{LogPrefix}room {room.name} at {room.position}, forward {room.forward}: {string.Join(", ", names)}");
            var door = room.parent.Find($"Door_{RoomLabel}");
            if (door != null)
                foreach (var part in door.GetComponentsInChildren<Component>(true).Where(c => c is Renderer || c is Collider))
                    Debug.Log($"{LogPrefix}door part {part.name} ({part.GetType().Name}) " +
                              (part is Renderer rend ? $"bounds {rend.bounds.center} size {rend.bounds.size}" : ""));
        }

        private static Bounds TightBoundsOf(Transform room, string path)
        {
            var item = room.Find(path);
            if (item == null)
                throw new System.InvalidOperationException($"[ProCommVR] No '{path}' in {room.name}.");
            return Scenario01SceneBuilder.TightBounds(item.gameObject);
        }

        private static Transform RequireChild(Transform parent, string path)
        {
            var child = parent.Find(path);
            if (child == null)
                throw new System.InvalidOperationException($"[ProCommVR] No '{path}' under {parent.name} in {WardScenePath}.");
            return child;
        }

        private static Transform Spot(Transform parent, string name, Vector3 position, Vector3 forward)
        {
            var spot = new GameObject(name).transform;
            spot.SetParent(parent, false);
            var flat = Flat(forward);
            spot.SetPositionAndRotation(position, Quaternion.LookRotation(flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward));
            return spot;
        }

        private static TextMeshPro CreateSign(Transform parent, string name, string text, Vector3 position, Vector3 viewerForward, float width)
        {
            var label = new GameObject(name).AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            // TextMeshPro is read from its back side: forward points away from the reader.
            label.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Flat(viewerForward).normalized));
            label.rectTransform.sizeDelta = new Vector2(width, 0.6f);
            label.text = text;
            label.fontSize = 0.9f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = SignTextColor;
            return label;
        }

        private static Vector3 Plan(Vector2 point) => UWardLayout.ToUnity(point.x, point.y);

        private static Vector3 OnFloor(Vector3 point) => EmotionWorldSetup.OnFloor(point + Vector3.up * 0.6f, 0.4f, 2f);

        private static float CeilingAbove(Vector3 point, float floor)
        {
            Physics.SyncTransforms();
            var origin = new Vector3(point.x, floor + 0.5f, point.z);
            if (Physics.Raycast(origin, Vector3.up, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return floor + UWardLayout.CeilingHeight;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static void SetLabels(AiDebriefPanel debrief, string retry, string done)
        {
            var serialized = new SerializedObject(debrief);
            serialized.FindProperty("_retryLabel").stringValue = retry;
            serialized.FindProperty("_doneLabel").stringValue = done;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A light grey material for the computer screen (URP Lit, created once).</summary>
        private static Material SignMaterial()
        {
            const string path = "Assets/_Project/Materials/ShiftComputer.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.82f, 0.88f, 0.95f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
