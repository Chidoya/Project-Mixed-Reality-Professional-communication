using System.Linq;
using ProCommVR.AiTalk;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Player;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Hospital.Debrief.Editor
{
    /// <summary>
    /// Adds the doctor-led debrief to the open Prototype1 scene (one call from Jalil's Prototype1SceneBuilder, after his
    /// flow, debrief panel and shift exist):
    /// <list type="bullet">
    /// <item>the head doctor (prefab variant of Doctor_Female) replaces the visible Doctor_Female at the same place;</item>
    /// <item>a ConversationRecorder on the patient, a PagerCue on the player's left hand;</item>
    /// <item>the DebriefDirector with its places, all found by name and bounds (the briefing room's table and whiteboard,
    /// the conversation spot), not by fixed coordinates;</item>
    /// <item>Liam's AiDebriefPanel in Doctor Led mode (the report panel stays as a fallback when the request fails).</item>
    /// </list>
    /// Everything else in the scene stays as Jalil's builder made it.
    /// </summary>
    public static class DebriefSetup
    {
        private const string LogPrefix = "[Debrief] ";
        private const string BubblePrefabPath = "Assets/_Project/Prefabs/UI/DialogueBubble.prefab";

        /// <summary>Adds the debrief under <paramref name="prototype"/> (the scene root "Prototype1"). Returns the director.</summary>
        public static DebriefDirector AddToScene(Transform prototype)
        {
            var script = DebriefAssets.EnsureAll();
            var propMaterial = DebriefAssets.EnsurePropMaterial();
            var pictureMaterial = DebriefAssets.EnsurePictureMaterial();
            var volume = DebriefAssets.EnsureVolumeProfile();
            var headDoctorPrefab = DebriefAssets.EnsureHeadDoctor();
            var bubblePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePrefabPath);
            var ghostMaterial = AssetDatabase.LoadAssetAtPath<Material>(DebriefAssets.GhostMaterialPath);

            var flow = Object.FindAnyObjectByType<PrototypeFlow>(FindObjectsInactive.Include);
            var panel = Object.FindAnyObjectByType<AiDebriefPanel>(FindObjectsInactive.Include);
            var npc = Object.FindAnyObjectByType<AiNpc>(FindObjectsInactive.Include);
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (flow == null || panel == null || npc == null || origin == null)
                throw new System.InvalidOperationException(LogPrefix + "Prototype1 needs its flow, debrief panel, AI patient and XR rig first.");
            var holder = npc.GetComponent<EmotionHolder>();
            var avatar = origin.GetComponentInChildren<PlayerAvatar>(true);
            var fade = origin.Camera.GetComponentInChildren<ScreenFade>(true);
            var serializedFlow = new SerializedObject(flow);
            var doctorSpot = (Transform)serializedFlow.FindProperty("_doctorSpot").objectReferenceValue;
            var patientSpot = (Transform)serializedFlow.FindProperty("_patientSpot").objectReferenceValue;
            var flowBubble = ((Component)serializedFlow.FindProperty("_doctorBubble").objectReferenceValue)?.gameObject;

            // ---- The room: table and whiteboard of the briefing room, by name and bounds ----
            var room = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "BriefingRoom" && t.Find("Furniture/Table") != null);
            if (room == null)
                throw new System.InvalidOperationException(LogPrefix + "No BriefingRoom with Furniture/Table in the ward.");
            var table = Bounds(room.Find("Furniture/Table"));
            var board = Bounds(room.Find("Whiteboard/Board") ?? room.Find("Whiteboard"));
            var toPlayer = Flat(table.center - board.center).normalized;          // from the board over the table
            var along = Vector3.Cross(Vector3.up, -toPlayer).normalized;            // the player's right (facing the board)
            var depth = Mathf.Abs(Vector3.Dot(table.size, new Vector3(Mathf.Abs(toPlayer.x), 0f, Mathf.Abs(toPlayer.z))));
            var length = Mathf.Abs(Vector3.Dot(table.size, new Vector3(Mathf.Abs(along.x), 0f, Mathf.Abs(along.z))));
            var top = table.max.y;
            var center = new Vector3(table.center.x, top, table.center.z);
            var floor = doctorSpot != null ? doctorSpot.position.y : table.min.y;
            var boardDepth = Mathf.Abs(Vector3.Dot(board.size, new Vector3(Mathf.Abs(toPlayer.x), 0f, Mathf.Abs(toPlayer.z))));

            var root = new GameObject("Debrief (head doctor)").transform;
            root.SetParent(prototype, false);
            var places = new GameObject("Places").transform;
            places.SetParent(root, false);
            var playerSpot = Spot(places, "Player (table, facing the board)", Ground(center + toPlayer * (depth / 2f + 0.75f), floor), -toPlayer);
            var doctorTable = Spot(places, "Doctor (table, board side)",
                Ground(center - toPlayer * (depth / 2f + 0.55f) - along * Mathf.Min(1.1f, length * 0.24f), floor), toPlayer);
            var reflectionRow = Spot(places, "Reflection Row", center + toPlayer * (depth / 2f - 0.28f), -toPlayer);
            var timelineRow = Spot(places, "Timeline Row", center + toPlayer * (depth / 2f - 0.62f), -toPlayer);
            var fileSpot = Spot(places, "Patient File", center + toPlayer * (depth / 2f - 0.25f) + along * 0.85f, -toPlayer);
            var boardFrame = Spot(places, "Whiteboard Front", new Vector3(board.center.x, board.center.y, board.center.z) + toPlayer * (boardDepth / 2f), toPlayer);
            var eye = playerSpot.position + Vector3.up * 1.6f;
            var towardDoctor = Flat(doctorTable.position - playerSpot.position).normalized;
            var subtitle = Spot(places, "Doctor Subtitle", new Vector3(eye.x, floor + 1.32f, eye.z) + towardDoctor * 1.5f, towardDoctor);
            var choice = Spot(places, "Choice Bubbles", playerSpot.position - toPlayer * 1.05f + Vector3.up * 1.25f, -toPlayer);

            // ---- The head doctor replaces Doctor_Female at the same place ----
            var doctorRoot = prototype.Find("Doctor (debrief)");
            var oldDoctor = doctorRoot != null ? doctorRoot.GetComponentsInChildren<Animator>(true).Select(a => a.transform).FirstOrDefault() : null;
            var doctorGo = (GameObject)PrefabUtility.InstantiatePrefab(headDoctorPrefab, doctorRoot != null ? doctorRoot : root);
            doctorGo.name = "HeadDoctor";
            if (oldDoctor != null)
            {
                var top0 = oldDoctor;
                while (top0.parent != null && top0.parent != doctorRoot)
                    top0 = top0.parent;
                doctorGo.transform.SetPositionAndRotation(top0.position, top0.rotation);
                Object.DestroyImmediate(top0.gameObject);
            }
            else if (doctorSpot != null)
                doctorGo.transform.SetPositionAndRotation(doctorSpot.position + doctorSpot.forward * 1.5f, Quaternion.LookRotation(-doctorSpot.forward));
            var speaker = doctorGo.GetComponent<DoctorSpeaker>();
            var pointer = doctorGo.GetComponent<DoctorPointer>();
            var subtitleBubble = (GameObject)PrefabUtility.InstantiatePrefab(bubblePrefab, root);
            subtitleBubble.name = "Doctor Subtitle Bubble";
            subtitleBubble.SetActive(false);
            Set(speaker, "_subtitle", subtitleBubble.GetComponent<DialogueBubble>());

            // ---- The recorder on the patient ----
            var recorder = npc.GetComponent<ConversationRecorder>();
            if (recorder == null)
                recorder = npc.gameObject.AddComponent<ConversationRecorder>();
            Set(recorder, "_npc", npc);
            Set(recorder, "_holder", holder);
            Set(recorder, "_head", origin.Camera.transform);

            // ---- The pager on the left hand ----
            var leftHand = origin.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Left Controller");
            var pager = new GameObject("Pager Cue").AddComponent<PagerCue>();
            pager.transform.SetParent(root, false);
            Set(pager, "_npc", npc);
            Set(pager, "_leftHand", leftHand);
            Set(pager, "_pagerClip", script.PagerClip);
            Set(pager, "_footstepsClip", script.FootstepsClip);
            Set(pager, "_material", propMaterial);
            SetString(pager, "_text", script.PagerText);

            // ---- The parts of the director ----
            var timeline = new GameObject("Emotion Timeline").AddComponent<EmotionTimeline>();
            timeline.transform.SetParent(root, false);
            var boardVisuals = new GameObject("Board (notes, stamp)").AddComponent<DebriefBoard>();
            boardVisuals.transform.SetParent(root, false);
            var memory = new GameObject("Memory Replay").AddComponent<MemoryReplay>();
            memory.transform.SetParent(root, false);
            Set(memory, "_patient", npc);
            Set(memory, "_holder", holder);
            Set(memory, "_avatar", avatar);
            Set(memory, "_ghostMaterial", ghostMaterial);
            Set(memory, "_memoryProfile", volume);
            Set(memory, "_bubblePrefab", bubblePrefab);
            Set(memory, "_fallbackSpot", patientSpot);
            var consequence = new GameObject("Consequence Card").AddComponent<ConsequenceCard>();
            consequence.transform.SetParent(root, false);
            Set(consequence, "_pictureMaterial", pictureMaterial);

            var director = new GameObject("Debrief Director").AddComponent<DebriefDirector>();
            director.transform.SetParent(root, false);
            var d = new SerializedObject(director);
            d.FindProperty("_script").objectReferenceValue = script;
            d.FindProperty("_doctor").objectReferenceValue = speaker;
            d.FindProperty("_pointer").objectReferenceValue = pointer;
            d.FindProperty("_patient").objectReferenceValue = npc;
            d.FindProperty("_recorder").objectReferenceValue = recorder;
            d.FindProperty("_holder").objectReferenceValue = holder;
            d.FindProperty("_flow").objectReferenceValue = flow;
            d.FindProperty("_origin").objectReferenceValue = origin;
            d.FindProperty("_fade").objectReferenceValue = fade;
            d.FindProperty("_playerTableSpot").objectReferenceValue = playerSpot;
            d.FindProperty("_doctorTableSpot").objectReferenceValue = doctorTable;
            d.FindProperty("_reflectionRow").objectReferenceValue = reflectionRow;
            d.FindProperty("_timelineRow").objectReferenceValue = timelineRow;
            d.FindProperty("_fileSpot").objectReferenceValue = fileSpot;
            d.FindProperty("_board").objectReferenceValue = boardFrame;
            d.FindProperty("_subtitleSpot").objectReferenceValue = subtitle;
            d.FindProperty("_choiceSpot").objectReferenceValue = choice;
            d.FindProperty("_flowBubble").objectReferenceValue = flowBubble;
            // The shift report (with the score) stays as AiShift shows it, at once: Jalil's RunPrototype1 expects it there.
            // To show the score only at the end, drag "Shift Report" into Hide Until Board (open question for Jalil).
            d.FindProperty("_hideUntilBoard").arraySize = 0;
            d.FindProperty("_propMaterial").objectReferenceValue = propMaterial;
            d.FindProperty("_noteMaterial").objectReferenceValue = propMaterial;
            d.FindProperty("_bubblePrefab").objectReferenceValue = bubblePrefab;
            d.FindProperty("_timeline").objectReferenceValue = timeline;
            d.FindProperty("_boardVisuals").objectReferenceValue = boardVisuals;
            d.FindProperty("_memory").objectReferenceValue = memory;
            d.FindProperty("_consequence").objectReferenceValue = consequence;
            var serializedPanel = new SerializedObject(panel);
            var retry = serializedPanel.FindProperty("_retryLabel").stringValue;
            var done = serializedPanel.FindProperty("_doneLabel").stringValue;
            d.FindProperty("_retryLabel").stringValue = string.IsNullOrEmpty(retry) ? "Try again (A)" : retry.Replace(" / R", "");
            d.FindProperty("_doneLabel").stringValue = string.IsNullOrEmpty(done) ? "Finish (B)" : done.Replace(" / Enter", "");
            d.ApplyModifiedPropertiesWithoutUndo();

            // ---- Liam's panel: doctor led ----
            serializedPanel.FindProperty("_doctorLed").boolValue = true;
            serializedPanel.FindProperty("_director").objectReferenceValue = director;
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"{LogPrefix}added to {prototype.name}: table {center} (length {length:0.00} m, depth {depth:0.00} m), " +
                      $"board {board.center}, player at the table {playerSpot.position}, doctor at the table {doctorTable.position}.");
            return director;
        }

        private static Bounds Bounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(t.position, Vector3.one * 0.1f);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static Vector3 Ground(Vector3 point, float fallback)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(new Vector3(point.x, fallback + 1.2f, point.z), Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore)
                && hit.point.y < fallback + 0.3f)
                return hit.point;
            return new Vector3(point.x, fallback, point.z);
        }

        private static Transform Spot(Transform parent, string name, Vector3 position, Vector3 forward)
        {
            var spot = new GameObject(name).transform;
            spot.SetParent(parent, false);
            var flat = Flat(forward);
            spot.SetPositionAndRotation(position, Quaternion.LookRotation(flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward));
            return spot;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
