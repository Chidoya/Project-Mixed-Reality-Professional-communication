using System.Collections.Generic;
using System.Linq;
using ProCommVR.Hospital;
using UnityEditor;
using UnityEngine;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Fase 5: personages, lichtstroken, teleportzones en de startplek van de speler.
    /// Patiënten zoals in liam_test_scene (welk personage en welke pose staat in de PatientCase: 1A ligt, 2A en 3A zitten op de
    /// stoel, gedraaid naar de deur), zorgverleners als decor (één achter de balie), per kamer een lichtstrook vanuit de hal,
    /// en lege markers voor Jalils XR-opzet (op Liam zit geen XR Interaction Toolkit): TeleportArea_* met enkel een collider
    /// en Lobby/XROriginStart. De pose-hulpfuncties zijn kopieën uit HospitalBuilder.Characters (die blijft ongewijzigd).
    /// </summary>
    public static partial class UWardBuilder
    {
        private const string CharacterFolder = "Assets/Character";
        private const string CharactersRootName = "Characters";

        private const float LayingClearance = 0.01f;
        private const float HipsAboveSeat = 0.12f;
        private const float SeatBackOffset = 0.05f;
        private const float FloorTolerance = 0.03f;
        private const float StaffColliderRadius = 0.25f;
        private const float StaffColliderHeight = 1.7f;

        // Lichtstroken: één baan per kamer vanuit de doorgang van de hal (x = 21). Banen die verder gaan liggen noordelijker en
        // draaien eerder af, zodat ze elkaar nooit kruisen (zoals de banen in liam_test_scene).
        private const float GuideWidth = 0.06f;
        private const float GuideThickness = 0.002f;
        private const float GuideStartX = 21f;
        private const float GuideLaneSouthY = 21.85f;
        private const float GuideLaneSpacing = 0.15f;
        private static readonly string[] GuidesWest = { "2G", "2B", "2A", "2E", "1D", "1B", "1A", "1C" };
        private static readonly string[] GuidesEast = { "2C", "2D", "2F", "3D", "3B", "3A", "3C" };

        private struct StaffSpot
        {
            public string Prefab, State, Group;
            public Vector2 Position, LookAt;
            public StaffSpot(string prefab, string state, string group, Vector2 position, Vector2 lookAt)
            {
                Prefab = prefab; State = state; Group = group; Position = position; LookAt = lookAt;
            }
        }

        // Plancoördinaten, allemaal in de hal: minstens 0,8 m van elke muur, niet voor een deur en niet in de looplijn van de
        // hal naar de doorgang. Paren kijken elkaar aan. (De gangen zijn 2,4 m breed: daar kan niemand 0,8 m van beide muren
        // staan zonder in de looplijn naar de deuren te staan.)
        private const float StaffWallClearance = 0.8f;
        private static readonly StaffSpot[] Staff =
        {
            new StaffSpot("Nurse_Female_White", "Idle", "Lobby", new Vector2(21.0f, 17.7f), new Vector2(21.0f, 12f)),
            new StaffSpot("Surgeon_Male", "Talking", "Lobby", new Vector2(18.3f, 4.3f), new Vector2(19.2f, 4.7f)),
            new StaffSpot("Surgeon_Female", "Idle", "Lobby", new Vector2(19.2f, 4.7f), new Vector2(18.3f, 4.3f)),
            new StaffSpot("Doctor_Male", "Talking", "Lobby", new Vector2(23.6f, 12.0f), new Vector2(23.6f, 12.9f)),
            new StaffSpot("Nurse_Female_Scrubs", "Idle", "Lobby", new Vector2(23.6f, 12.9f), new Vector2(23.6f, 12.0f)),
            new StaffSpot("Doctor_Female", "Talking", "Lobby", new Vector2(20.6f, 7.6f), new Vector2(21.5f, 7.6f)),
            new StaffSpot("Nurse_Male_White", "Idle", "Lobby", new Vector2(21.5f, 7.6f), new Vector2(20.6f, 7.6f)),
        };

        // --- Personages ---------------------------------------------------------------------

        private static void PlaceCharacters(Transform root)
        {
            var characters = new GameObject(CharactersRootName).transform;
            characters.SetParent(root, false);
            try
            {
                foreach (var patientCase in LoadCases().Values.OrderBy(c => c.DoorLabel))
                    PlacePatient(root, characters, patientCase);
                for (var i = 0; i < Staff.Length; i++)
                    PlaceStaff(characters, Staff[i], i);
            }
            finally
            {
                StopSamplingKeepRoots();
                foreach (var probe in Probes)
                    Object.DestroyImmediate(probe);
                Probes.Clear();
            }
            // Pas na AnimationMode: toont de houding in de Scene-view (en in de screenshots) in plaats van de T-pose
            foreach (var pose in characters.GetComponentsInChildren<CharacterPose>())
                pose.gameObject.AddComponent<EditorPosePreview>();
        }

        private static void PlacePatient(Transform root, Transform characters, PatientCase patientCase)
        {
            var label = patientCase.DoorLabel;
            var spec = PatientRooms.FirstOrDefault(r => r.Id == label);
            var room = spec == null ? null : root.Find($"Floor{spec.Zone}/Room_{label}");
            if (room == null || patientCase.PatientPrefab == null)
            {
                Debug.LogWarning($"{LogPrefix}Case {label}: kamer of personage ontbreekt, overgeslagen.");
                return;
            }

            var patient = (GameObject)PrefabUtility.InstantiatePrefab(patientCase.PatientPrefab, characters);
            patient.name = $"Patient_{label}";
            var state = patientCase.StartPose == StartPose.Laying ? "Laying" : "Sitting";
            patient.AddComponent<CharacterPose>().Configure(state, 0f);

            // Meten op een kopie: AnimationMode zet een gesampled object bij het stoppen terug naar zijn beginstand
            var probe = Probe(patientCase.PatientPrefab, patient.name);
            if (!SamplePose(probe, state, 0f))
                return;
            if (patientCase.StartPose == StartPose.Laying)
                LayOnBed(probe, room, label);
            else
                SitOnChair(probe, room, PlanPoint(spec.DoorCenter), label);
            patient.transform.SetPositionAndRotation(probe.transform.position, probe.transform.rotation);
        }

        /// <summary>Lichaam in de lengte van het bed (hoofd naar het hoofdeinde), midden op het matras, net erboven.</summary>
        private static void LayOnBed(GameObject patient, Transform room, string label)
        {
            var anchor = room.Find("Anchor_Laying");
            var animator = patient.GetComponent<Animator>();
            patient.transform.SetPositionAndRotation(new Vector3(anchor.position.x, room.position.y, anchor.position.z), room.rotation);

            Vector3 Head() => animator.GetBoneTransform(HumanBodyBones.Head).position;
            Vector3 Feet() => (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position + animator.GetBoneTransform(HumanBodyBones.RightFoot).position) / 2f;
            var bodyAxis = Vector3.ProjectOnPlane(Head() - Feet(), Vector3.up);
            patient.transform.Rotate(0f, Vector3.SignedAngle(bodyAxis, room.forward, Vector3.up), 0f, Space.World);

            var middle = (Head() + Feet()) / 2f;
            patient.transform.position += new Vector3(anchor.position.x - middle.x, 0f, anchor.position.z - middle.z);
            patient.transform.position += Vector3.up * (anchor.position.y + LayingClearance - MinVertexY(patient));
            Debug.Log($"{LogPrefix}Patient_{label} ligt op het bed: laagste punt {MinVertexY(patient):0.000} m, matras {anchor.position.y:0.000} m.");
        }

        /// <summary>De stoel draait naar de deur, de patiënt zit erop met de heupen HipsAboveSeat boven het zitvlak.</summary>
        private static void SitOnChair(GameObject patient, Transform room, Vector3 door, string label)
        {
            var anchor = room.Find("Anchor_Sitting");
            var chair = room.Find("Furniture/Chair");
            var source = PrefabUtility.GetCorrespondingObjectFromSource(chair);
            if (source != null)
                chair.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            Physics.SyncTransforms();

            var seatCenter = HospitalAssetPrep.WorldBounds(chair.gameObject).center;
            var towardDoor = Vector3.ProjectOnPlane(door - seatCenter, Vector3.up).normalized;
            chair.RotateAround(seatCenter, Vector3.up, Vector3.SignedAngle(anchor.forward, towardDoor, Vector3.up));

            var animator = patient.GetComponent<Animator>();
            patient.transform.SetPositionAndRotation(new Vector3(seatCenter.x, room.position.y, seatCenter.z), Quaternion.LookRotation(towardDoor));
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            var target = new Vector3(seatCenter.x, anchor.position.y + HipsAboveSeat, seatCenter.z) - towardDoor * SeatBackOffset;
            patient.transform.position += target - hips;

            var feetBelow = room.position.y - MinVertexY(patient);
            if (feetBelow > FloorTolerance)
                patient.transform.position += Vector3.up * feetBelow;
            Debug.Log($"{LogPrefix}Patient_{label} zit op de stoel: heupen op {animator.GetBoneTransform(HumanBodyBones.Hips).position.y:0.000} m, " +
                      $"zitvlak {anchor.position.y:0.000} m.");
        }

        private static void PlaceStaff(Transform characters, StaffSpot spot, int index)
        {
            var path = $"{CharacterFolder}/{spot.Prefab}/{spot.Prefab}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"{LogPrefix}Personage niet gevonden: {path}");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, characters);
            go.name = $"{spot.Group}_{spot.Prefab}";
            go.transform.SetPositionAndRotation(PlanPoint(spot.Position), Quaternion.LookRotation(PlanDirection(spot.LookAt - spot.Position)));

            // Deterministische spreiding zodat dezelfde animaties niet synchroon lopen
            var offset = (index * 0.37f) % 1f;
            go.AddComponent<CharacterPose>().Configure(spot.State, offset);
            var probe = Probe(prefab, go.name);
            probe.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
            if (SamplePose(probe, spot.State, offset))
                go.transform.position += Vector3.up * -MinVertexY(probe);

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = StaffColliderRadius;
            capsule.height = StaffColliderHeight;
            capsule.center = Vector3.up * (StaffColliderHeight / 2f);

            Physics.SyncTransforms();
            var bottom = go.transform.position + Vector3.up * 0.3f;
            var wall = Physics.OverlapCapsule(bottom, bottom + Vector3.up * 1.4f, StaffWallClearance)
                .FirstOrDefault(c => c.name.StartsWith("Wall_"));
            if (wall != null)
                Debug.LogWarning($"{LogPrefix}{go.name} staat minder dan {StaffWallClearance} m van {wall.name}.");
        }

        private static readonly List<GameObject> Probes = new List<GameObject>();

        private static GameObject Probe(GameObject prefab, string name)
        {
            var probe = Object.Instantiate(prefab);
            probe.name = $"Probe_{name}";
            probe.hideFlags = HideFlags.DontSave;
            Probes.Add(probe);
            return probe;
        }

        // --- Pose meten (kopieën uit HospitalBuilder.Characters) ----------------------------------

        /// <summary>Stopt AnimationMode, maar houdt de positie en rotatie van elk personage (als prefab-override).</summary>
        private static void StopSamplingKeepRoots()
        {
            if (!AnimationMode.InAnimationMode())
                return;
            var roots = Object.FindObjectsByType<CharacterPose>(FindObjectsSortMode.None)
                .Select(p => (p.transform, p.transform.position, p.transform.rotation)).ToList();
            AnimationMode.StopAnimationMode();
            foreach (var (t, position, rotation) in roots)
            {
                t.SetPositionAndRotation(position, rotation);
                if (PrefabUtility.IsPartOfPrefabInstance(t))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                EditorUtility.SetDirty(t);
            }
        }

        private static bool SamplePose(GameObject character, string state, float normalizedTime)
        {
            var animator = character.GetComponent<Animator>();
            var clip = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == state)
                : null;
            if (clip == null)
            {
                Debug.LogWarning($"{LogPrefix}{character.name}: geen animatie '{state}' in de controller.");
                return false;
            }
            var position = character.transform.position;
            var rotation = character.transform.rotation;
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(character, clip, clip.length * normalizedTime);
            AnimationMode.EndSampling();
            character.transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        private static float MinVertexY(GameObject character)
        {
            var min = float.MaxValue;
            var mesh = new Mesh();
            foreach (var skin in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(mesh, true);
                var matrix = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices)
                    min = Mathf.Min(min, matrix.MultiplyPoint3x4(v).y);
            }
            Object.DestroyImmediate(mesh);
            return min;
        }

        // --- Lichtstroken -----------------------------------------------------------------------

        /// <summary>
        /// FloorN/GuideLights/Guide_&lt;kamer&gt;: smalle strook in de vloer van de doorgang van de hal naar de deur, emissive,
        /// zonder collider. FloorGuideLight kleurt hem (Jalil kiest welke).
        /// </summary>
        private static void BuildGuideLights(Dictionary<string, Transform> groups)
        {
            var mat = Mat("FloorGuide", FloorGuideLight.Neutral, 0f, 0.5f, FloorGuideLight.Neutral * 0.5f);
            foreach (var (ids, sign) in new[] { (GuidesWest, -1f), (GuidesEast, 1f) })
            {
                var armIndex = 0;
                for (var i = 0; i < ids.Length; i++)
                {
                    var spec = PatientRooms.First(r => r.Id == ids[i]);
                    var laneY = GuideLaneSouthY - i * GuideLaneSpacing;
                    var door = spec.DoorCenter;
                    var points = new List<Vector2> { new Vector2(GuideStartX, laneY) };
                    if (spec.Side == CorridorSide.North)
                    {
                        points.Add(new Vector2(door.x, laneY));
                        points.Add(new Vector2(door.x, door.y - InnerWall / 2f));
                    }
                    else
                    {
                        // Armkamers: de zuidelijkste baan draait het laatst af, het dichtst bij de deuren
                        var wallFace = door.x - sign * InnerWall / 2f;
                        var turnX = wallFace - sign * (0.3f + armIndex++ * GuideLaneSpacing);
                        points.Add(new Vector2(turnX, laneY));
                        points.Add(new Vector2(turnX, door.y));
                        points.Add(new Vector2(wallFace, door.y));
                    }

                    var guides = Child(groups[$"Floor{spec.Zone}"], "GuideLights");
                    var guide = Child(guides, $"Guide_{spec.Id}");
                    for (var p = 1; p < points.Count; p++)
                        GuideStrip($"Part_{p}", guide, points[p - 1], points[p], mat);
                    guide.gameObject.AddComponent<FloorGuideLight>();
                }
            }
        }

        /// <summary>Strook tussen twee punten op dezelfde x of y, aan beide uiteinden een halve breedte langer (nette hoeken).</summary>
        private static void GuideStrip(string name, Transform parent, Vector2 a, Vector2 b, Material mat)
        {
            if ((a - b).sqrMagnitude < 0.0001f)
                return;
            var length = (b - a).magnitude + GuideWidth;
            var alongX = Mathf.Abs(a.y - b.y) < 0.001f;
            var size = alongX ? new Vector3(length, GuideThickness, GuideWidth) : new Vector3(GuideWidth, GuideThickness, length);
            var go = Box(name, parent, PlanPoint((a + b) / 2f, GuideThickness / 2f), size, mat);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // --- Teleportzones en startplek -----------------------------------------------------------

        /// <summary>
        /// Hospital/Teleport/TeleportArea_*: een dunne BoxCollider (zonder renderer) waarvan de bovenkant gelijk ligt met de
        /// vloer, per plek waar de speler mag komen. Jalil zet er later een TeleportationArea op. Lobby/XROriginStart: voor de
        /// balie aan de bezoekerskant, kijkend naar de balie.
        /// </summary>
        private static void BuildTeleportAreas(Transform root, Transform lobby)
        {
            var teleport = Child(root, "Teleport");
            void Area(string name, PlanRect r)
            {
                var go = new GameObject($"TeleportArea_{name}");
                go.transform.SetParent(teleport, false);
                go.transform.position = ToUnity(r.CenterX, r.CenterY, -0.005f);
                go.AddComponent<BoxCollider>().size = new Vector3(r.X1 - r.X0, 0.01f, r.Y1 - r.Y0);
            }

            Area("Hall", Inner(CoreRoom("Hall")));
            Area("CorridorLeft", Inner(new PlanRect(CorridorLeft.X0, CorridorLeft.Y0, CorridorLeft.X1, CorridorBottom.Y0)));
            Area("CorridorRight", Inner(new PlanRect(CorridorRight.X0, CorridorRight.Y0, CorridorRight.X1, CorridorBottom.Y0)));
            Area("CorridorBottom", Inner(CorridorBottom));
            Area("BriefingRoom", Inner(CoreRoom("BriefingRoom")));
            Area("FamilyRoom", Inner(CoreRoom("FamilyRoom")));
            Area("RestRoom", Inner(CoreRoom("RestRoom")));
            foreach (var room in PatientRooms)
                Area($"Room_{room.Id}", Inner(room.Rect));

            var start = Child(lobby, "XROriginStart");
            start.SetPositionAndRotation(PlanPoint(new Vector2(ReceptionDesk.CenterX, ReceptionDesk.Y0 - 1.3f)),
                Quaternion.LookRotation(PlanDirection(PlanSouth)));
        }

        // --- Prestaties ----------------------------------------------------------------------------

        /// <summary>
        /// Telt wat de Quest 3 moet tekenen: renderers, materialen, driehoeken en realtime lichten. Een schatting, geen meting:
        /// het echte aantal draw calls hangt af van static batching in de build en van het aantal lichten per object.
        /// </summary>
        private static void LogPerformance(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToList();
            var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count();
            var triangles = 0L;
            foreach (var r in renderers)
            {
                var mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null)
                    for (var s = 0; s < mesh.subMeshCount; s++)
                        triangles += mesh.GetIndexCount(s) / 3;
            }
            var lights = root.GetComponentsInChildren<Light>(true).Count(l => l.type == LightType.Point);
            var staticCount = renderers.Count(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.BatchingStatic));
            Debug.Log($"{LogPrefix}Prestaties: {renderers.Count} renderers ({staticCount} static), {materials} materialen, " +
                      $"{triangles:N0} driehoeken, {lights} puntlichten.");
        }

        // --- Screenshots -----------------------------------------------------------------------------

        private static void TakeCharacterScreenshots(Transform root)
        {
            foreach (var id in new[] { "2A", "3A" })
            {
                var spec = PatientRooms.First(r => r.Id == id);
                var room = root.Find($"Floor{spec.Zone}/Room_{id}");
                var door = room.InverseTransformPoint(PlanPoint(spec.DoorCenter));
                Shot($"u_afdeling_patient_{id}.png", room.TransformPoint(new Vector3(door.x, EyeHeight, 0.4f)),
                    room.TransformPoint(new Vector3(0.6f, 0.7f, RoomInteriorDepth - 1.4f)));
            }

            var calm = root.Find("Floor1/GuideLights/Guide_1A")?.GetComponent<FloorGuideLight>();
            var difficult = root.Find("Floor3/GuideLights/Guide_3A")?.GetComponent<FloorGuideLight>();
            if (calm == null || difficult == null)
                return;
            calm.SetColor(FloorGuideLight.Calm);
            difficult.SetColor(FloorGuideLight.Difficult);
            Shot("u_afdeling_guide_lights.png", PlanPoint(new Vector2(21f, 18.6f), 2.4f), PlanPoint(new Vector2(21f, 21.6f), 0f));
            calm.SetNeutral();
            difficult.SetNeutral();
        }
    }
}
