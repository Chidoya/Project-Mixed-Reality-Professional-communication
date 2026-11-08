using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor.Tests
{
    /// <summary>
    /// Controles op de OPGESLAGEN scene liam_u_afdeling.unity (eerst bouwen met Build U-Afdeling): deuren in hun opening en
    /// naar binnen draaiend, sanitair en nachtkastjes met de voorkant naar de ruimte, personeel weg van de muren, en in
    /// Play mode de houding van de patiënten (met screenshots u_afdeling_chars_play_*.png). Elke controle schrijft een rij
    /// "[UWardCheck] | ... |" in de log, zodat de resultaten als tabel te lezen zijn.
    /// </summary>
    public class UWardSceneTests
    {
        private const string ScenePath = "Assets/Scenes/liam_u_afdeling.unity";
        private const float Tolerance = 0.02f;
        private const float FrameOverhang = 0.04f;

        private static Transform Root => GameObject.Find("Hospital").transform;

        [SetUp]
        public void OpenSavedScene()
        {
            if (!Application.isPlaying)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // --- Deuren -------------------------------------------------------------------------------

        /// <summary>Alle deuren uit UWardLayout.Doors(), met de ruimte waar ze in opengaan (de ruimte net achter de opening).</summary>
        private static IEnumerable<(OpeningSpec Spec, PlanRect Room)> DoorsWithRoom()
        {
            var rooms = PatientRooms.Select(r => r.Rect).Concat(CoreRooms.Select(r => r.Rect)).ToList();
            foreach (var spec in Doors())
            {
                var behind = spec.Center + spec.Into * 0.5f;
                yield return (spec, rooms.First(r => behind.x > r.X0 && behind.x < r.X1 && behind.y > r.Y0 && behind.y < r.Y1));
            }
        }

        /// <summary>Kozijn binnen de muuropening, en het midden van de deur tot op 2 cm gelijk aan de data.</summary>
        [Test]
        public void DoorsSitInTheirOpening()
        {
            var failures = new List<string>();
            foreach (var spec in Doors())
            {
                var opening = spec.Segment;
                var b = RendererBounds(Find(spec.Name));
                var (min, max) = (PlanOf(b.min), PlanOf(b.max));
                var along = opening.Vertical ? (Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y)) : (min.x, max.x);
                var across = opening.Vertical ? (min.x, max.x) : (Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y));
                var center = PlanOf(b.center);
                var offset = (center - spec.Center).magnitude;
                var halfDepth = InnerWall / 2f + FrameOverhang / 2f + Tolerance;
                var ok = along.Item1 >= opening.From - Tolerance && along.Item2 <= opening.To + Tolerance &&
                         across.Item1 >= opening.At - halfDepth && across.Item2 <= opening.At + halfDepth &&
                         b.min.y > -Tolerance && b.max.y < DoorHeight + Tolerance && offset <= Tolerance;
                Row("Deur in opening", spec.Name, $"midden data ({spec.Center.x:0.00}, {spec.Center.y:0.00}) | " +
                                                  $"midden scene ({center.x:0.00}, {center.y:0.00}) | afwijking {offset * 100f:0.0} cm", ok);
                if (!ok)
                    failures.Add(spec.Name);
            }
            Assert.IsEmpty(failures, "Deuren buiten hun opening: " + string.Join(", ", failures));
        }

        /// <summary>Dezelfde draai als Jalils OpenDoor: scharnier aan -x van de houder, deurblad draait naar z (de ruimte in).</summary>
        [Test]
        public void DoorsSwingIntoTheirRoom()
        {
            var failures = new List<string>();
            foreach (var (spec, room) in DoorsWithRoom())
            {
                var door = Find(spec.Name);
                var width = spec.Segment.Length;
                var hinge = door.position - door.right * (width / 2f);
                var openLeaf = PlanOf(hinge + door.forward * (width * 0.6f));
                var inside = openLeaf.x > room.X0 && openLeaf.x < room.X1 && openLeaf.y > room.Y0 && openLeaf.y < room.Y1;
                var forward = new Vector2(door.forward.x, -door.forward.z);
                var ok = inside && Vector2.Dot(forward, spec.Into) > 0.99f;
                Row("Deur draait naar binnen", spec.Name, $"open deurblad op ({openLeaf.x:0.00}, {openLeaf.y:0.00})", ok);
                if (!ok)
                    failures.Add(spec.Name);
            }
            Assert.IsEmpty(failures, "Deuren die niet de ruimte in draaien: " + string.Join(", ", failures));
        }

        /// <summary>Het vlak dat het deurblad bij het openen bestrijkt (breedte x breedte, de ruimte in) raakt niets.</summary>
        [Test]
        public void DoorSwingIsClear()
        {
            Physics.SyncTransforms();
            var failures = new List<string>();
            foreach (var spec in Doors())
            {
                var door = Find(spec.Name);
                var width = spec.Segment.Length;
                const float fromWall = 0.12f;
                var center = door.position + door.forward * (fromWall + (width - fromWall) / 2f) + Vector3.up * 1.05f;
                var half = new Vector3(width / 2f - 0.05f, 0.95f, (width - fromWall) / 2f);
                var hits = Physics.OverlapBox(center, half, door.rotation)
                    .Where(c => !c.transform.IsChildOf(door)).Select(c => c.name).Distinct().ToList();
                var ok = hits.Count == 0;
                Row("Deur draait vrij open", spec.Name, ok ? "vrij" : "raakt " + string.Join(", ", hits), ok);
                if (!ok)
                    failures.Add(spec.Name);
            }
            Assert.IsEmpty(failures, "Deuren die bij het openen iets raken: " + string.Join(", ", failures));
        }

        /// <summary>Elke wachtstoel in de hal staat minstens 0,5 m naast elke deur in zijn muur.</summary>
        [Test]
        public void WaitingChairsKeepDoorsFree()
        {
            const float clearance = 0.5f;
            var failures = new List<string>();
            foreach (Transform chair in Root.Find("Lobby/Furniture"))
            {
                if (!chair.name.StartsWith("WaitingChair"))
                    continue;
                var b = RendererBounds(chair);
                var (min, max) = (PlanOf(b.min), PlanOf(b.max));
                var (y0, y1) = (Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y));
                var x = (min.x + max.x) / 2f;
                var gaps = CoreDoors.Where(d => d.Segment.Vertical && Mathf.Abs(d.Segment.At - x) < 1.2f)
                    .Select(d => (d.Name, Gap: Mathf.Max(d.Segment.From - y1, y0 - d.Segment.To))).ToList();
                var nearest = gaps.OrderBy(g => g.Gap).FirstOrDefault();
                var ok = gaps.All(g => g.Gap >= clearance - Tolerance);
                Row("Wachtstoel vrij van deuren", chair.name, nearest.Name == null ? "geen deur in deze muur" : $"{nearest.Gap:0.00} m tot {nearest.Name}", ok);
                if (!ok)
                    failures.Add(chair.name);
            }
            Assert.IsEmpty(failures, "Wachtstoelen te dicht bij een deur: " + string.Join(", ", failures));
        }

        // --- Sanitair en nachtkastjes ------------------------------------------------------------------

        [Test]
        public void FixturesFaceTheRoom()
        {
            var failures = new List<string>();
            // Sanitair: de rug raakt binnen 0,7 m een muur en de voorkant kijkt de ruimte in. Nachtkastje: lade zoals het voeteneinde.
            void Check(Transform item, Vector3 target, string label, bool againstWall)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
                var front = Vector3.ProjectOnPlane(item.rotation * Quaternion.Euler(0f, FrontYaw(source != null ? source.name : null), 0f) * Vector3.forward, Vector3.up).normalized;
                var center = RendererBounds(item).center;
                var dot = Vector3.Dot(front, Vector3.ProjectOnPlane(target - center, Vector3.up).normalized);
                var wall = againstWall
                    ? Physics.RaycastAll(center, -front, 0.7f).Where(h => !h.collider.transform.IsChildOf(item))
                        .Select(h => h.collider.name).FirstOrDefault(n => n.StartsWith("Wall_"))
                    : null;
                var ok = againstWall ? wall != null && dot > 0f : dot > 0.99f;
                Row("Voorkant naar de ruimte", label, againstWall ? $"rug tegen {wall ?? "geen muur"}, cos {dot:0.00}" : $"zelfde kant als het voeteneinde, cos {dot:0.00}", ok);
                if (!ok)
                    failures.Add(label);
            }

            Physics.SyncTransforms();
            foreach (var spec in PatientRooms)
            {
                var room = Root.Find($"Floor{spec.Zone}/Room_{spec.Id}");
                var bathCenter = ToUnity(spec.At(BathroomLength / 2f, BathroomDepth / 2f).x, spec.At(BathroomLength / 2f, BathroomDepth / 2f).y);
                foreach (var fixture in new[] { "Toilet", "Sink", "Shower" })
                    Check(room.Find($"Bathroom/{fixture}"), bathCenter, $"{spec.Id} {fixture}", true);
                foreach (Transform item in room.Find("Furniture"))
                    if (item.name.StartsWith("Nightstand"))
                    {
                        // De lade wijst dezelfde kant op als het voeteneinde van het bed (de kamer in)
                        var bed = room.Find("Furniture/" + item.name.Replace("Nightstand", "Bed"));
                        var foot = Vector3.ProjectOnPlane(bed.rotation * Quaternion.Euler(0f, FrontYaw("bedSingle"), 0f) * Vector3.forward, Vector3.up).normalized;
                        Check(item, RendererBounds(item).center + foot, $"{spec.Id} {item.name}", false);
                    }
            }
            foreach (var name in new[] { "Toilet_Men", "Toilet_Women" })
            {
                var rect = CoreRooms.First(r => r.Name == name).Rect;
                var center = ToUnity(rect.CenterX, rect.CenterY);
                foreach (Transform item in Root.Find($"Core/{name}/Furniture"))
                    if (item.name.StartsWith("Toilet") || item.name.StartsWith("Sink"))
                        Check(item, center, $"{name} {item.name}", true);
            }
            Assert.IsEmpty(failures, "Verkeerd gedraaid: " + string.Join(", ", failures));
        }

        // --- Versie 8: gesprekskamer en toiletten ------------------------------------------------------

        private const string ToiletReferencePath = "Assets/Hospital/Editor/UWard/Tests/UWardToiletReference.txt";

        /// <summary>
        /// De toiletten (deuren, bordjes, inrichting, muren en de fauteuils ertussen) staan exact zoals op origin/Liam vóór
        /// versie 8: zelfde regels als UWardToiletReference.txt (momentopname van UWardToiletSnapshot, mm nauwkeurig).
        /// </summary>
        [Test]
        public void ToiletsUnchanged()
        {
            var reference = System.IO.File.ReadAllLines(ToiletReferencePath).Where(l => l.Length > 0).ToList();
            var now = UWardToiletSnapshot.Lines();
            var missing = reference.Except(now).ToList();
            var extra = now.Except(reference).ToList();
            foreach (var line in missing)
                Row("Toiletten ongewijzigd", "verdwenen of verschoven", line, false);
            foreach (var line in extra)
                Row("Toiletten ongewijzigd", "nieuw of verschoven", line, false);
            Row("Toiletten ongewijzigd", "alles", $"{reference.Count} regels vergeleken", missing.Count == 0 && extra.Count == 0);
            Assert.IsTrue(missing.Count == 0 && extra.Count == 0,
                $"Toiletten gewijzigd: {missing.Count} regels verdwenen, {extra.Count} nieuw (zie [UWardCheck] in de log).");
        }

        /// <summary>
        /// Gesprekskamer volgens versie 8: de objecten die Jalil opzoekt (Light, deuren, teleportzone), warm licht, en een vrije
        /// doorgang van de gangdeur de kamer in (plan y 10, van x 33,6 tot 31,2) op knie- en borsthoogte.
        /// </summary>
        [Test]
        public void FamilyRoomAsInPlanV8()
        {
            var failures = new List<string>();
            void Check(string item, bool ok, string detail)
            {
                Row("Gesprekskamer v8", item, detail, ok);
                if (!ok)
                    failures.Add(item);
            }

            var room = Root.Find("Core/FamilyRoom");
            Check("Core/FamilyRoom", room != null, room != null ? "gevonden" : "ontbreekt");
            if (room == null)
            {
                Assert.Fail("Core/FamilyRoom ontbreekt");
                return;
            }
            var light = room.Find("Light")?.GetComponent<Light>();
            Check("Core/FamilyRoom/Light", light != null && light.shadows == LightShadows.None && light.color.r > light.color.b,
                light == null ? "ontbreekt" : $"kleur {light.color}, schaduw {light.shadows}");
            foreach (var path in new[] { "Core/Door_FamilyRoom_Hall", "Core/Door_FamilyRoom_Corridor", "Core/Wall_FamilyRoom_South",
                         "Teleport/TeleportArea_FamilyRoom", "Core/FamilyRoom/Sign_FamilyRoom", "Core/FamilyRoom/Painting" })
                Check(path, Root.Find(path) != null, Root.Find(path) != null ? "gevonden" : "ontbreekt");
            foreach (var item in new[] { "Sofa", "Armchair_1", "Armchair_2", "CoffeeTable", "Tissue_Box", "Dresser", "Plant" })
                Check($"Furniture/{item}", room.Find($"Furniture/{item}") != null, room.Find($"Furniture/{item}") != null ? "gevonden" : "ontbreekt");

            Physics.SyncTransforms();
            foreach (var height in new[] { 0.5f, 1.2f })
            {
                var from = ToUnity(33.6f - InnerWall, 10.0f, height);
                var to = ToUnity(31.2f, 10.2f, height);
                var hit = Physics.Linecast(from, to, out var info);
                Check($"doorgang gangdeur, {height:0.0} m", !hit, hit ? $"geraakt: {info.collider.name}" : "vrij tot (31,2 / 10,2)");
            }
            Assert.IsEmpty(failures, "Gesprekskamer: " + string.Join(", ", failures));
        }

        // --- Personeel ------------------------------------------------------------------------------

        [Test]
        public void StaffKeepClearOfWalls()
        {
            const float clearance = 0.8f;
            Physics.SyncTransforms();
            var failures = new List<string>();
            foreach (Transform character in Root.Find("Characters"))
            {
                if (character.name.StartsWith("Patient_"))
                    continue;
                var bottom = character.position + Vector3.up * 0.3f;
                var walls = Physics.OverlapCapsule(bottom, bottom + Vector3.up * 1.4f, clearance)
                    .Where(c => c.name.StartsWith("Wall_")).Select(c => c.name).ToList();
                var ok = walls.Count == 0;
                Row("Personeel >= 0,8 m van de muur", character.name, ok ? "vrij" : string.Join(", ", walls), ok);
                if (!ok)
                    failures.Add(character.name);
            }
            Assert.IsEmpty(failures, "Te dicht bij een muur: " + string.Join(", ", failures));
        }

        // --- Play mode: houding van de patiënten ---------------------------------------------------------

        [UnityTest]
        public IEnumerator PatientsInPlayMode()
        {
            yield return new EnterPlayMode();
            yield return new WaitForSeconds(1.5f);

            var failures = new List<string>();
            var root = Root;
            var laying = CheckLaying(root, "1A");
            if (!laying)
                failures.Add("1A");
            foreach (var id in new[] { "2A", "3A" })
                if (!CheckSitting(root, id))
                    failures.Add(id);
            foreach (var (name, eye, target) in UWardBuilder.CharacterShots(root))
            {
                var go = new GameObject("TestCamera");
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 75f;
                cam.nearClipPlane = 0.05f;
                go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                HospitalAssetPrep.Capture(cam, $"docs/Screenshots/u_afdeling_chars_play_{name}.png");
                Object.Destroy(go);
            }

            yield return new ExitPlayMode();
            Assert.IsEmpty(failures, "Houding klopt niet in Play mode: " + string.Join(", ", failures));
        }

        /// <summary>Hoofd naar het hoofdeinde, heupen op het matras, niets door het bed, binnen de breedte van het bed.</summary>
        private static bool CheckLaying(Transform root, string id)
        {
            var spec = PatientRooms.First(r => r.Id == id);
            var room = root.Find($"Floor{spec.Zone}/Room_{id}");
            var animator = root.Find($"Characters/Patient_{id}").GetComponent<Animator>();
            var mattress = room.Find("Anchor_Laying").position.y;
            var bed = RendererBounds(room.Find("Furniture/Bed"));
            bed.Expand(new Vector3(0.3f, 0f, 0.3f));
            Vector3 Bone(HumanBodyBones b) => animator.GetBoneTransform(b).position;
            var head = Bone(HumanBodyBones.Head);
            var hips = Bone(HumanBodyBones.Hips);
            var feet = (Bone(HumanBodyBones.LeftFoot) + Bone(HumanBodyBones.RightFoot)) / 2f;
            var lowest = new[] { head, hips, Bone(HumanBodyBones.LeftFoot), Bone(HumanBodyBones.RightFoot) }.Min(p => p.y);
            var headToHeadboard = Vector3.Dot(head - feet, room.forward) > 0.5f;
            var onMattress = hips.y - mattress > -0.05f && hips.y - mattress < 0.35f && lowest > mattress - 0.1f;
            bool InBed(Vector3 p) => p.x > bed.min.x && p.x < bed.max.x && p.z > bed.min.z && p.z < bed.max.z;
            var ok = headToHeadboard && onMattress && InBed(head) && InBed(hips);
            Row("Play mode: ligt", $"Patient_{id}", $"heupen {hips.y - mattress:+0.00;-0.00} m t.o.v. matras, laagste bot {lowest:0.00} m, " +
                                                     $"hoofd naar hoofdeinde {(headToHeadboard ? "ja" : "nee")}", ok);
            return ok;
        }

        /// <summary>Heupen op de zitting (boven de stoel), voeten op de vloer.</summary>
        private static bool CheckSitting(Transform root, string id)
        {
            var spec = PatientRooms.First(r => r.Id == id);
            var room = root.Find($"Floor{spec.Zone}/Room_{id}");
            var animator = root.Find($"Characters/Patient_{id}").GetComponent<Animator>();
            var seat = room.Find("Anchor_Sitting").position.y;
            var chair = RendererBounds(room.Find("Furniture/Chair")).center;
            Vector3 Bone(HumanBodyBones b) => animator.GetBoneTransform(b).position;
            var hips = Bone(HumanBodyBones.Hips);
            var feet = Mathf.Min(Bone(HumanBodyBones.LeftFoot).y, Bone(HumanBodyBones.RightFoot).y);
            var overSeat = new Vector2(hips.x - chair.x, hips.z - chair.z).magnitude;
            var ok = hips.y - seat > 0f && hips.y - seat < 0.3f && overSeat < 0.3f && feet < 0.2f;
            Row("Play mode: zit", $"Patient_{id}", $"heupen {hips.y - seat:+0.00;-0.00} m boven zitting, {overSeat:0.00} m naast het midden, " +
                                                    $"voeten op {feet:0.00} m", ok);
            return ok;
        }

        // --- Hulp -------------------------------------------------------------------------------------

        private static Transform Find(string name) =>
            Root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        private static Bounds RendererBounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            var b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        private static Vector2 PlanOf(Vector3 p) => new Vector2(p.x + Width / 2f, Depth / 2f - p.z);

        private static void Row(string check, string item, string detail, bool ok) =>
            Debug.Log($"[UWardCheck] | {check} | {item} | {detail} | {(ok ? "ok" : "FOUT")} |");
    }
}
