using System.Collections.Generic;
using System.Linq;
using ProCommVR.Hospital;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Hospital.Face;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor
{
    /// <summary>
    /// De vier andere scenario's in de testscene, elk op een eigen plek naast de gang van scenario 2:
    /// 1 Patient_Male in bed (eenpersoonskamer), 3 Doctor_Male in een gang (de zoon wacht verderop),
    /// 4 Patient_Female_Old op de stoel naast haar man (Patient_Male_Old slaapt in bed),
    /// 5 Patient_Male op een stoel in een gedeelde kamer (Patient_Male_Old in het tweede bed).
    /// Zelfde personages als in Jalils ShiftHospitalSceneBuilder. Kamers = Liams U-afdelingprefabs.
    /// </summary>
    public static partial class AiTalkSceneBuilder
    {
        private const string RoomPrefabFolder = "Assets/Hospital/Prefabs/UWard";
        private const float HipsAboveSeat = 0.12f;      // zoals UWardBuilder.Characters
        private const float SeatBackOffset = 0.05f;
        private const float TalkDistance = 1.15f;
        private const float BedsideDistance = 1.45f;   // van het hoofd tot net voorbij het voeteneinde van het bed

        /// <summary>De stoel staat rechts van het bed (+x); van daar schuin naar het voeteneinde (-z) en het bed (-x).</summary>
        private static Vector3 TowardBedFoot(Transform room) => (-room.right * 0.5f - room.forward).normalized;

        private class Area
        {
            public int Number;
            public string Title;
            public AiNpc Npc;
            public Transform Viewpoint;
        }

        private static void BuildScenarioAreas(AiTalkSettings settings, PushToTalk ptt, AiNpc daughter, Transform player)
        {
            var areas = new List<Area>();
            var viewpoints = new GameObject("Viewpoints").transform;

            // 2: de bestaande gang met de dochter (camera staat er al)
            ConfigureNpc(daughter, settings, ptt, "angry_daughter");
            areas.Add(new Area { Number = 2, Title = "Angry daughter", Npc = daughter,
                Viewpoint = Viewpoint(viewpoints, "Viewpoint_2", player.position, player.rotation) });

            areas.Add(BuildDisappointedPatient(settings, ptt, viewpoints, new Vector3(-14f, 0f, 0f)));
            areas.Add(BuildDoctor(settings, ptt, viewpoints, new Vector3(14f, 0f, 0f)));
            areas.Add(BuildPoorPrognosis(settings, ptt, viewpoints, new Vector3(-28f, 0f, 0f)));
            areas.Add(BuildAggressivePatient(settings, ptt, viewpoints, new Vector3(-42f, 0f, 0f)));

            var selector = new GameObject("ScenarioSelector").AddComponent<AiTalkScenarioSelector>();
            var so = new SerializedObject(selector);
            var list = so.FindProperty("_entries");
            var sorted = areas.OrderBy(a => a.Number).ToList();
            list.arraySize = sorted.Count;
            for (var i = 0; i < sorted.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Number").intValue = sorted[i].Number;
                e.FindPropertyRelative("Title").stringValue = sorted[i].Title;
                e.FindPropertyRelative("Npc").objectReferenceValue = sorted[i].Npc;
                e.FindPropertyRelative("Viewpoint").objectReferenceValue = sorted[i].Viewpoint;
            }
            so.FindProperty("_startNumber").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"{LogPrefix}Scenariokiezer: {string.Join(", ", sorted.Select(a => $"{a.Number} {a.Title}"))}");
        }

        // --- 1: teleurgestelde patiënt in bed ---------------------------------------------------

        private static Area BuildDisappointedPatient(AiTalkSettings settings, PushToTalk ptt, Transform viewpoints, Vector3 origin)
        {
            var room = BuildRoom("Area_1_DisappointedPatient", "PatientRoom_Single", origin);
            var anchor = room.Find("Anchor_Laying");
            var patient = Character("Patient_Male", room, "Patient_X");
            var head = LayOn(patient, anchor.position, room.forward);
            var npc = patient.AddComponent<AiNpc>();
            ConfigureNpc(npc, settings, ptt, "disappointed_patient");
            // Naast het bed, aan de kant van het nachtkastje maar verder naar het voeteneinde
            var eye = new Vector3(anchor.position.x - 0.95f, EyeHeight, head.z - 0.75f);
            return new Area { Number = 1, Title = "Disappointed patient", Npc = npc, Viewpoint = LookAt(viewpoints, "Viewpoint_1", eye, head) };
        }

        // --- 3: gehaaste arts in de gang ----------------------------------------------------------

        private static Area BuildDoctor(AiTalkSettings settings, PushToTalk ptt, Transform viewpoints, Vector3 origin)
        {
            var area = new GameObject("Area_3_DoctorAndFamily").transform;
            area.position = origin;
            BuildCorridorAt(area);
            var doctor = Character("Doctor_Male", area, "Dr_Maes");
            doctor.transform.SetPositionAndRotation(origin + new Vector3(0f, 0f, DaughterDistance), Quaternion.Euler(0f, 180f, 0f));
            var son = Character("Patient_Male", area, "Son_Of_Patient_X");
            son.transform.SetPositionAndRotation(origin + new Vector3(0.9f, 0f, 4.2f), Quaternion.Euler(0f, 200f, 0f));
            son.AddComponent<CharacterPose>().Configure("Idle", 0.3f);
            var npc = doctor.AddComponent<AiNpc>();
            ConfigureNpc(npc, settings, ptt, "doctor_and_family");
            return new Area { Number = 3, Title = "Doctor and family", Npc = npc,
                Viewpoint = Viewpoint(viewpoints, "Viewpoint_3", origin + new Vector3(0f, EyeHeight, 0f), Quaternion.Euler(6f, 0f, 0f)) };
        }

        // --- 4: vrouw naast het bed van haar man ----------------------------------------------------

        private static Area BuildPoorPrognosis(AiTalkSettings settings, PushToTalk ptt, Transform viewpoints, Vector3 origin)
        {
            var room = BuildRoom("Area_4_PoorPrognosis", "PatientRoom_Single", origin);
            var husband = Character("Patient_Male_Old", room, "Patient_X_Sleeping");
            LayOn(husband, room.Find("Anchor_Laying").position, room.forward);
            Quiet(husband);
            var wife = Character("Patient_Female_Old", room, "Wife_Of_Patient_X");
            // Schuin naar het voeteneinde van het bed, waar de verpleegkundige staat: bed en verpleegkundige in beeld
            var face = TowardBedFoot(room);
            var head = SitOn(wife, room.Find("Anchor_Sitting"), room.Find("Furniture/Chair"), face);
            var npc = wife.AddComponent<AiNpc>();
            ConfigureNpc(npc, settings, ptt, "poor_prognosis");
            var eye = new Vector3(head.x, EyeHeight, head.z) + face * BedsideDistance;
            return new Area { Number = 4, Title = "Poor prognosis", Npc = npc, Viewpoint = LookAt(viewpoints, "Viewpoint_4", eye, head) };
        }

        // --- 5: agressieve patiënt in een gedeelde kamer ---------------------------------------------

        private static Area BuildAggressivePatient(AiTalkSettings settings, PushToTalk ptt, Transform viewpoints, Vector3 origin)
        {
            var room = BuildRoom("Area_5_AggressivePatient", "PatientRoom_Shared", origin);
            var neighbour = Character("Patient_Male_Old", room, "Patient_In_Next_Bed");
            LayOn(neighbour, room.Find("Anchor_Laying_2").position, room.forward);
            Quiet(neighbour);
            var patient = Character("Patient_Male", room, "Patient_X");
            var face = TowardBedFoot(room);
            var head = SitOn(patient, room.Find("Anchor_Sitting"), room.Find("Furniture/Chair"), face);
            var npc = patient.AddComponent<AiNpc>();
            ConfigureNpc(npc, settings, ptt, "aggressive_patient");
            var eye = new Vector3(head.x, EyeHeight, head.z) + face * BedsideDistance;
            return new Area { Number = 5, Title = "Aggressive patient", Npc = npc, Viewpoint = LookAt(viewpoints, "Viewpoint_5", eye, head) };
        }

        // --- Hulp -------------------------------------------------------------------------------------

        private static void ConfigureNpc(AiNpc npc, AiTalkSettings settings, PushToTalk ptt, string scenarioId)
        {
            var so = new SerializedObject(npc);
            so.FindProperty("_settings").objectReferenceValue = settings;
            so.FindProperty("_pushToTalk").objectReferenceValue = ptt;
            so.FindProperty("_scenarioId").stringValue = scenarioId;
            so.FindProperty("_autoStart").boolValue = false; // de kiezer start het gesprek
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Character(string model, Transform parent, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(LoadCharacter(model), parent);
            go.name = name;
            return go;
        }

        /// <summary>Een slapende of zwijgende figurant: kijkt de speler niet aan.</summary>
        private static void Quiet(GameObject character)
        {
            var look = character.GetComponentInChildren<HeadLookAt>();
            if (look != null)
                look.enabled = false;
        }

        /// <summary>Kamerprefab van de U-afdeling (bed, stoel, ankers, licht) met eigen vloer, muren en plafond.</summary>
        private static Transform BuildRoom(string name, string prefabName, Vector3 origin)
        {
            var area = new GameObject(name).transform;
            area.position = origin;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomPrefabFolder}/{prefabName}.prefab");
            if (prefab == null)
                throw new System.InvalidOperationException($"Kamerprefab ontbreekt: {prefabName} (draai de U-afdeling-builder)");
            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, area);
            room.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var bounds = new Bounds(origin, Vector3.zero);
            foreach (var r in room.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(r.bounds);
            var halfWidth = Mathf.Max(2f, bounds.extents.x + 0.6f);
            var depth = Mathf.Max(5f, bounds.max.z - origin.z + 0.05f);
            var height = CorridorHeight;
            var shell = new GameObject("Shell").transform;
            shell.SetParent(area, false);
            var cx = bounds.center.x - origin.x;
            Box("Floor", shell, new Vector3(cx, -0.05f, depth / 2f - 0.5f), new Vector3(halfWidth * 2f, 0.1f, depth + 1f), Mat("Floor"));
            Box("Ceiling", shell, new Vector3(cx, height + 0.05f, depth / 2f - 0.5f), new Vector3(halfWidth * 2f, 0.1f, depth + 1f), Mat("Ceiling"));
            Box("Wall_Back", shell, new Vector3(cx, height / 2f, depth + 0.05f), new Vector3(halfWidth * 2f, height, 0.1f), Mat("RoomWall") ?? Mat("Wall"));
            Box("Wall_Left", shell, new Vector3(cx - halfWidth, height / 2f, depth / 2f - 0.5f), new Vector3(0.1f, height, depth + 1f), Mat("RoomWall") ?? Mat("Wall"));
            Box("Wall_Right", shell, new Vector3(cx + halfWidth, height / 2f, depth / 2f - 0.5f), new Vector3(0.1f, height, depth + 1f), Mat("RoomWall") ?? Mat("Wall"));
            Box("Wall_Front", shell, new Vector3(cx, height / 2f, -1.05f), new Vector3(halfWidth * 2f, height, 0.1f), Mat("Wall"));
            var fill = new GameObject("Room_Fill_Light").AddComponent<Light>();
            fill.transform.SetParent(area, false);
            fill.transform.localPosition = new Vector3(cx, height - 0.2f, depth * 0.35f);
            fill.type = LightType.Point;
            fill.range = 6f;
            fill.intensity = 1.2f;
            return room.transform;
        }

        /// <summary>Gang zoals die van scenario 2, maar op de plek van `area`.</summary>
        private static void BuildCorridorAt(Transform area)
        {
            var length = CorridorEnd - CorridorStart;
            var midZ = (CorridorStart + CorridorEnd) / 2f;
            var half = CorridorWidth / 2f;
            Box("Floor", area, new Vector3(0f, -0.05f, midZ), new Vector3(CorridorWidth + 0.2f, 0.1f, length), Mat("Floor"));
            Box("Ceiling", area, new Vector3(0f, CorridorHeight + 0.05f, midZ), new Vector3(CorridorWidth + 0.2f, 0.1f, length), Mat("Ceiling"));
            Box("Wall_Left", area, new Vector3(-half - 0.05f, CorridorHeight / 2f, midZ), new Vector3(0.1f, CorridorHeight, length), Mat("Wall"));
            Box("Wall_Right", area, new Vector3(half + 0.05f, CorridorHeight / 2f, midZ), new Vector3(0.1f, CorridorHeight, length), Mat("Wall"));
            Box("Wall_End", area, new Vector3(0f, CorridorHeight / 2f, CorridorEnd + 0.05f), new Vector3(CorridorWidth, CorridorHeight, 0.1f), Mat("Wall"));
            Box("Wall_Back", area, new Vector3(0f, CorridorHeight / 2f, CorridorStart - 0.05f), new Vector3(CorridorWidth, CorridorHeight, 0.1f), Mat("Wall"));
            foreach (var z in new[] { 0.6f, 3.2f, 5.8f })
            {
                var lamp = new GameObject($"Ceiling_Light_{z:0.0}").AddComponent<Light>();
                lamp.transform.SetParent(area, false);
                lamp.type = LightType.Point;
                lamp.range = 5f;
                lamp.intensity = 1.6f;
                lamp.transform.localPosition = new Vector3(0f, CorridorHeight - 0.15f, z);
            }
        }

        /// <summary>
        /// Legt een personage in de Laying-pose op een matras: lichaam langs `headDirection` (hoofd die kant op),
        /// midden op het punt, net boven de matras. Geeft de positie van het hoofd terug.
        /// </summary>
        private static Vector3 LayOn(GameObject character, Vector3 mattress, Vector3 headDirection)
        {
            character.AddComponent<CharacterPose>().Configure("Laying", 0f);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(character);
            var probe = Object.Instantiate(source);
            probe.hideFlags = HideFlags.DontSave;
            try
            {
                if (!SamplePose(probe, "Laying"))
                    return mattress;
                var animator = probe.GetComponent<Animator>();
                probe.transform.SetPositionAndRotation(mattress, Quaternion.identity);
                Vector3 Head() => animator.GetBoneTransform(HumanBodyBones.Head).position;
                Vector3 Feet() => (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position +
                                   animator.GetBoneTransform(HumanBodyBones.RightFoot).position) / 2f;
                var axis = Vector3.ProjectOnPlane(Head() - Feet(), Vector3.up);
                probe.transform.Rotate(0f, Vector3.SignedAngle(axis, headDirection, Vector3.up), 0f, Space.World);
                var middle = (Head() + Feet()) / 2f;
                probe.transform.position += new Vector3(mattress.x - middle.x, 0f, mattress.z - middle.z);
                probe.transform.position += Vector3.up * (mattress.y + LayingClearance - MinVertexY(probe));
                character.transform.SetPositionAndRotation(probe.transform.position, probe.transform.rotation);
                return Head();
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Zet een personage in de Sitting-pose op de stoel, met de stoel en het gezicht naar `face`.
        /// Geeft de positie van het hoofd terug.
        /// </summary>
        private static Vector3 SitOn(GameObject character, Transform anchor, Transform chair, Vector3 face)
        {
            face = Vector3.ProjectOnPlane(face, Vector3.up).normalized;
            var seat = anchor.position;
            if (chair != null)
                chair.RotateAround(new Vector3(seat.x, 0f, seat.z), Vector3.up, Vector3.SignedAngle(anchor.forward, face, Vector3.up));
            character.AddComponent<CharacterPose>().Configure("Sitting", 0f);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(character);
            var probe = Object.Instantiate(source);
            probe.hideFlags = HideFlags.DontSave;
            try
            {
                probe.transform.SetPositionAndRotation(new Vector3(seat.x, 0f, seat.z), Quaternion.LookRotation(face));
                if (!SamplePose(probe, "Sitting"))
                    return seat + Vector3.up * 0.7f;
                var animator = probe.GetComponent<Animator>();
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                var target = new Vector3(seat.x, seat.y + HipsAboveSeat, seat.z) - face * SeatBackOffset;
                probe.transform.position += target - hips;
                var below = -MinVertexY(probe);
                if (below > 0.01f)
                    probe.transform.position += Vector3.up * below;
                character.transform.SetPositionAndRotation(probe.transform.position, probe.transform.rotation);
                return animator.GetBoneTransform(HumanBodyBones.Head).position;
            }
            finally
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(probe);
            }
        }

        private static Transform Viewpoint(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);
            return t;
        }

        private static Transform LookAt(Transform parent, string name, Vector3 eye, Vector3 target) =>
            Viewpoint(parent, name, eye, Quaternion.LookRotation(target - eye, Vector3.up));
    }
}
