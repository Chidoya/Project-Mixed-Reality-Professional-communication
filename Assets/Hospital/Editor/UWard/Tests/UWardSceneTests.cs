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

        // --- Scenario 2: de dochter, de panelen en de route -------------------------------------------

        private const string Scenario2 = "Scenarios/Scenario2_AngryDaughter";
        private const float PlayerRadius = 0.2f;
        private const float PanelHalfWidth = 0.4f;   // ongeveer de helft van Jalils paneel met 4 antwoorden
        private const float FaceHeight = 1.6f;

        private static Transform S2(string path) => Root.Find($"{Scenario2}/{path}");

        private static bool IsActor(Collider c) =>
            c.transform.IsChildOf(S2("Actors/Daughter")) || c.transform == S2("Actors/Blocker_Corridor");

        /// <summary>
        /// Vanaf Anchor_Player_Corridor kan de speler (capsule r 0,2) op geen enkele baan over de breedte van de gang naar het
        /// zuiden zonder eerst de dochter of Blocker_Corridor te raken. De collider van de dochter blijft minstens 0,3 m van de muur.
        /// </summary>
        [Test]
        public void DaughterBlocksTheCorridor()
        {
            Physics.SyncTransforms();
            var failures = new List<string>();
            var player = S2("Anchors/Anchor_Player_Corridor");
            var daughter = S2("Actors/Daughter");
            var x0 = CorridorRight.X0 + InnerWall / 2f + PlayerRadius + 0.01f;
            var x1 = CorridorRight.X1 - InnerWall / 2f - PlayerRadius - 0.01f;
            var startY = PlanOf(player.position).y;
            var distance = PlanOf(daughter.position).y - startY + 1.5f;
            for (var x = x0; x <= x1 + 0.001f; x += 0.1f)
            {
                var foot = ToUnity(x, startY, PlayerRadius + 0.1f);
                var head = foot + Vector3.up * 1.2f;
                // Kaarten en bordjes op de gangmuur (hoogstens 6 cm diep) tellen niet: daar kan de speler toch niet langs
                var hits = Physics.CapsuleCastAll(foot, head, PlayerRadius, Vector3.back, distance)
                    .Where(h => !(h.collider.bounds.size.x <= 0.06f && h.collider.bounds.size.z > h.collider.bounds.size.x))
                    .OrderBy(h => h.distance).ToList();
                var hit = hits.Count > 0;
                var info = hit ? hits[0] : default;
                var ok = hit && IsActor(info.collider);
                Row("Dochter verspert de gang", $"baan x {x:0.00}", hit ? $"raakt {info.collider.name} op y {PlanOf(info.point).y:0.00}" : "raakt niets", ok);
                if (!ok)
                    failures.Add($"x {x:0.00}");
            }

            var capsule = daughter.GetComponent<CapsuleCollider>();
            var bottom = daughter.TransformPoint(capsule.center + Vector3.down * (capsule.height / 2f - capsule.radius));
            var top = daughter.TransformPoint(capsule.center + Vector3.up * (capsule.height / 2f - capsule.radius));
            var walls = Physics.OverlapCapsule(bottom, top, capsule.radius + 0.3f).Where(c => c.name.StartsWith("Wall_")).Select(c => c.name).ToList();
            Row("Dochter >= 0,3 m van de muur", "Actors/Daughter", walls.Count == 0 ? "vrij" : string.Join(", ", walls), walls.Count == 0);
            if (walls.Count > 0)
                failures.Add("dochter te dicht bij de muur");
            Assert.IsEmpty(failures, "Gang niet versperd: " + string.Join(", ", failures));
        }

        /// <summary>
        /// Anchor_Panel_Corridor en Anchor_Panel_Room: het paneel (0,8 x 0,5 m) zit niet in een muur, staat voor de speler en
        /// hangt minstens 0,4 m naast de kijklijn van de speler naar het gezicht van de dochter (het bedekt haar hoofd niet).
        /// </summary>
        [Test]
        public void PanelsKeepTheDaughterVisible()
        {
            Physics.SyncTransforms();
            var failures = new List<string>();
            foreach (var (panelName, playerName, daughterName) in new[]
                     {
                         ("Anchor_Panel_Corridor", "Anchor_Player_Corridor", "Anchor_Daughter_Corridor"),
                         ("Anchor_Panel_Room", "Anchor_Player_Room", "Anchor_Daughter_Room"),
                     })
            {
                var panel = S2($"Anchors/{panelName}");
                var eye = S2($"Anchors/{playerName}").position + Vector3.up * 1.65f;
                var face = S2($"Anchors/{daughterName}").position + Vector3.up * FaceHeight;
                var walls = Physics.OverlapBox(panel.position, new Vector3(PanelHalfWidth, 0.25f, 0.01f), panel.rotation)
                    .Where(c => c.name.StartsWith("Wall_")).Select(c => c.name).ToList();
                var line = (face - eye).normalized;
                var toPanel = panel.position - eye;
                var inFront = Vector3.Dot(toPanel, line) > 0.3f;
                var offLine = Vector3.Cross(line, toPanel).magnitude;
                var ok = walls.Count == 0 && inFront && offLine >= PanelHalfWidth;
                Row("Paneel vrij", panelName, $"{(walls.Count == 0 ? "niet in een muur" : "in " + string.Join(", ", walls))}, " +
                                              $"{offLine:0.00} m naast de kijklijn naar haar gezicht, voor de speler {(inFront ? "ja" : "nee")}", ok);
                if (!ok)
                    failures.Add(panelName);
            }
            Assert.IsEmpty(failures, "Paneel in de weg: " + string.Join(", ", failures));
        }

        /// <summary>Path_ToFamilyRoom (dochter, WP_1..3, Anchor_Daughter_Room) gaat op knie-, heup- en hoofdhoogte door geen muur.</summary>
        [Test]
        public void PathToFamilyRoomGoesThroughTheDoor()
        {
            Physics.SyncTransforms();
            var failures = new List<string>();
            var points = new[] { "Anchors/Anchor_Daughter_Corridor", "Anchors/Path_ToFamilyRoom/WP_1", "Anchors/Path_ToFamilyRoom/WP_2",
                "Anchors/Path_ToFamilyRoom/WP_3", "Anchors/Anchor_Daughter_Room" }.Select(S2).ToList();
            for (var i = 1; i < points.Count; i++)
                foreach (var height in new[] { 0.3f, 1.0f, 1.6f })
                {
                    var a = points[i - 1].position + Vector3.up * height;
                    var b = points[i].position + Vector3.up * height;
                    var hits = Physics.RaycastAll(a, (b - a).normalized, (b - a).magnitude)
                        .Where(h => !IsActor(h.collider)).Select(h => h.collider.name).Distinct().ToList();
                    var walls = hits.Where(n => n.StartsWith("Wall_")).ToList();
                    var ok = walls.Count == 0;
                    Row("Route naar de gesprekskamer", $"{points[i - 1].name} -> {points[i].name}, {height:0.0} m",
                        hits.Count == 0 ? "vrij" : "raakt " + string.Join(", ", hits), ok);
                    if (!ok)
                        failures.Add($"{points[i - 1].name} -> {points[i].name}");
                }
            Assert.IsEmpty(failures, "Route door een muur: " + string.Join(", ", failures.Distinct()));
        }

        // --- Scenario 2: past de brancard? --------------------------------------------------------------

        private const float SweepStep = 0.1f;
        private const float SweepTurnStep = 5f;
        private const float SweepFloorGap = 0.03f;   // boven de vloer en de teleportzones

        /// <summary>
        /// Omhullende doos van de brancard met de portier, in de ruimte van Gurney. De brancard via zijn mesh renderers (even
        /// actief gezet), de portier via zijn CapsuleCollider: de bounds van een skinned mesh zijn in edit mode die van de T-pose.
        /// </summary>
        private static Bounds GurneyLocalBox(Transform gurney)
        {
            var wasActive = gurney.gameObject.activeSelf;
            gurney.gameObject.SetActive(true);
            var box = new Bounds();
            var first = true;
            void Add(Vector3 world)
            {
                var local = gurney.InverseTransformPoint(world);
                if (first)
                    box = new Bounds(local, Vector3.zero);
                else
                    box.Encapsulate(local);
                first = false;
            }
            foreach (var r in gurney.GetComponentsInChildren<MeshRenderer>())
            {
                var b = r.bounds;
                for (var i = 0; i < 8; i++)
                    Add(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
            }
            foreach (var capsule in gurney.GetComponentsInChildren<CapsuleCollider>())
            {
                var t = capsule.transform;
                var c = capsule.center;
                foreach (var dx in new[] { -capsule.radius, capsule.radius })
                    foreach (var dz in new[] { -capsule.radius, capsule.radius })
                        foreach (var dy in new[] { -capsule.height / 2f, capsule.height / 2f })
                            Add(t.TransformPoint(c + new Vector3(dx, dy, dz)));
            }
            gurney.gameObject.SetActive(wasActive);
            return box;
        }

        /// <summary>Colliders die de doos raakt op deze plek (zonder de brancard zelf), met een extra marge rondom.</summary>
        private static List<Collider> SweepHits(Transform gurney, Bounds box, Vector3 position, Quaternion rotation, float margin)
        {
            var bottom = Mathf.Max(box.min.y, SweepFloorGap);
            var center = new Vector3(box.center.x, (bottom + box.max.y) / 2f, box.center.z);
            var half = new Vector3(box.extents.x + margin, (box.max.y - bottom) / 2f, box.extents.z + margin);
            return Physics.OverlapBox(position + rotation * center, half, rotation)
                .Where(c => !c.transform.IsChildOf(gurney) && !c.isTrigger).ToList();
        }

        /// <summary>Kleinste afstand tot een muur (0 tot 0,5 m, op 1 cm), of -1 als hij al iets raakt.</summary>
        private static float WallClearance(Transform gurney, Bounds box, Vector3 position, Quaternion rotation)
        {
            if (SweepHits(gurney, box, position, rotation, 0f).Count > 0)
                return -1f;
            for (var m = 0.01f; m <= 0.5f; m += 0.01f)
                if (SweepHits(gurney, box, position, rotation, m).Any(c => c.name.StartsWith("Wall_")))
                    return m - 0.01f;
            return 0.5f;
        }

        /// <summary>
        /// Schuift de doos van brancard en portier in stappen van 10 cm langs Path_Gurney, gedraaid in de rijrichting zoals
        /// Jalils PathMover, en draait bij elk waypoint ter plaatse in stappen van 5 graden. Meldt wat hij raakt (muur, deur,
        /// personage, prop) en per stuk de kleinste afstand tot een muur. Hij mag de dochter en Blocker_Corridor niet raken.
        /// Het klokpaneel hangt hoger dan de brancard + 0,5 m.
        /// </summary>
        [Test]
        public void GurneyFitsAlongItsRoute()
        {
            Physics.SyncTransforms();
            var gurney = S2("Actors/Gurney");
            Assert.IsNotNull(gurney, "Actors/Gurney ontbreekt");
            Assert.IsFalse(gurney.gameObject.activeSelf, "Actors/Gurney moet inactief zijn (Jalils PathMover zet hem aan)");
            var box = GurneyLocalBox(gurney);
            Row("Brancard", "doos met portier", $"lengte {box.size.z:0.00} m, breedte {box.size.x:0.00} m, hoogte {box.max.y:0.00} m", true);

            var route = GurneyRoute.Select(n => S2($"Anchors/{n}")).ToList();
            var failures = new List<string>();
            var heading = route[0].rotation;
            for (var i = 1; i < route.Count; i++)
            {
                var a = route[i - 1].position;
                var b = route[i].position;
                var direction = Quaternion.LookRotation(Vector3.ProjectOnPlane(b - a, Vector3.up));
                var minimum = float.MaxValue;
                var hits = new HashSet<string>();
                void Check(Vector3 p, Quaternion r)
                {
                    foreach (var c in SweepHits(gurney, box, p, r, 0f))
                        hits.Add(c.name);
                    minimum = Mathf.Min(minimum, WallClearance(gurney, box, p, r));
                }

                // Ter plaatse draaien naar het nieuwe stuk
                var turn = Quaternion.Angle(heading, direction);
                for (var t = 0f; t < turn; t += SweepTurnStep)
                    Check(a, Quaternion.RotateTowards(heading, direction, t));
                var length = (b - a).magnitude;
                for (var d = 0f; d <= length + 0.001f; d += SweepStep)
                    Check(Vector3.MoveTowards(a, b, d), direction);
                heading = direction;

                var ok = hits.Count == 0;
                var stretch = $"{route[i - 1].name} -> {route[i].name}";
                Row("Brancard past", stretch, $"draai {turn:0} graden, kleinste afstand tot een muur {minimum:0.00} m" +
                                             (ok ? "" : ", raakt " + string.Join(", ", hits)), ok);
                if (!ok)
                    failures.Add(stretch);
            }

            // Einde: draait naar de rotatie van Gurney_End en staat achter de dochter
            var end = route[route.Count - 1];
            var atEnd = SweepHits(gurney, box, end.position, end.rotation, 0.1f).Select(c => c.name).ToList();
            var daughter = S2("Actors/Daughter");
            var gap = PlanOf(end.position + end.rotation * new Vector3(0f, 0f, box.max.z)).y - PlanOf(daughter.position).y;
            var endOk = atEnd.Count == 0;
            Row("Brancard stopt achter de dochter", "Gurney_End", $"voorkant {gap - DaughterColliderRadius:0.00} m achter haar collider, " +
                                                               (endOk ? "raakt niets (10 cm marge)" : "raakt " + string.Join(", ", atEnd)), endOk);
            if (!endOk)
                failures.Add("Gurney_End");

            var panel = S2("Actors/Countdown_Mount/Panel").GetComponent<Renderer>().bounds;
            var high = panel.min.y > box.max.y + 0.5f;
            Row("Klokpaneel hoog genoeg", "Countdown_Mount", $"onderkant {panel.min.y:0.00} m, brancard met portier {box.max.y:0.00} m + 0,5 m", high);
            if (!high)
                failures.Add("Countdown_Mount");
            Assert.IsEmpty(failures, "Brancard past niet: " + string.Join(", ", failures));
        }

        /// <summary>
        /// Deur van 3C (1,3 m): de brancard past recht door de opening (breedte met 5 cm marge aan elke kant). Ter info ook een
        /// bocht vanuit de gang de kamer in (boog, zoals in de hoek): niet nodig voor scenario 2, wel voor als Jalil de vader laat ophalen.
        /// </summary>
        [Test]
        public void GurneyFitsThroughDoor3C()
        {
            Physics.SyncTransforms();
            var gurney = S2("Actors/Gurney");
            var box = GurneyLocalBox(gurney);
            var spec = PatientRooms.First(r => r.Id == "3C");
            var width = spec.DoorTo - spec.DoorFrom;
            var gurneyOnly = box.size.x;
            var ok = gurneyOnly + 0.1f <= width;
            Row("Brancard door de deur van 3C", "breedte", $"opening {width:0.00} m, brancard met portier {gurneyOnly:0.00} m breed", ok);

            // Ter info: in de rechtergang naar het noorden (x 34,6), een boog van 1 m naar het oosten, door het midden van de deur
            // de kamer in. Zonder de dochter en Blocker_Corridor (die staan er dan niet meer).
            var door = spec.DoorCenter;
            const float radius = 1.0f, laneX = 34.6f;
            var center = new Vector2(laneX + radius, door.y + radius);
            var points = new List<Vector2> { new Vector2(laneX, door.y + radius + 1.5f) };
            for (var phi = 0f; phi <= 90.1f; phi += 30f)
            {
                var r = phi * Mathf.Deg2Rad;
                points.Add(center + radius * new Vector2(-Mathf.Cos(r), -Mathf.Sin(r)));
            }
            points.Add(new Vector2(door.x + 2.2f, door.y));
            var hits = new HashSet<string>();
            var heading = Quaternion.LookRotation(PlanDir(points[1] - points[0]));
            void Hit(Vector3 p, Quaternion q)
            {
                foreach (var c in SweepHits(gurney, box, p, q, 0f).Where(c => !IsActor(c)))
                    hits.Add(c.name);
            }
            for (var i = 1; i < points.Count; i++)
            {
                var a = ToUnity(points[i - 1].x, points[i - 1].y);
                var b = ToUnity(points[i].x, points[i].y);
                var direction = Quaternion.LookRotation(Vector3.ProjectOnPlane(b - a, Vector3.up));
                for (var t = 0f; t < Quaternion.Angle(heading, direction); t += SweepTurnStep)
                    Hit(a, Quaternion.RotateTowards(heading, direction, t));
                for (var d = 0f; d <= (b - a).magnitude + 0.001f; d += SweepStep)
                    Hit(Vector3.MoveTowards(a, b, d), direction);
                heading = direction;
            }
            Row("Ter info: bocht de kamer 3C in", "boog straal 1 m", hits.Count == 0 ? "past" : "raakt " + string.Join(", ", hits), true);
            Assert.IsTrue(ok, "De brancard past niet door de deur van 3C");
        }

        private static Vector3 PlanDir(Vector2 d) => new Vector3(d.x, 0f, -d.y);

        // --- Scenario 2: alarmlampen (Play mode) ------------------------------------------------------

        private const int MaxLightsPerObject = 4;   // Mobile_Renderer (Forward): extra lichten per object

        /// <summary>
        /// In Play mode: 4 alarmlampen, in rust uit (lens Off, licht disabled). Aan met SetOn(true): lens On en licht enabled.
        /// Screenshots u_afdeling_alarm_off.png en _on.png. Telt ook per renderer hoeveel puntlichten hem raken (met en zonder
        /// alarm), want de Quest (Forward) toont er maar 4 per object.
        /// </summary>
        [UnityTest]
        public IEnumerator AlarmLampsInPlayMode()
        {
            yield return new EnterPlayMode();
            yield return new WaitForSeconds(0.5f);
            var failures = new List<string>();
            var lamps = Object.FindObjectsByType<ProCommVR.Hospital.AlarmLamp>(FindObjectsSortMode.None).OrderBy(l => l.name).ToList();
            var countOk = lamps.Count > 0 && lamps.Count <= MaxLightsPerObject;
            Row("Alarmlampen", "aantal", $"{lamps.Count} (max {MaxLightsPerObject})", countOk);
            if (!countOk)
                failures.Add("aantal");

            bool State(bool on)
            {
                var ok = true;
                foreach (var lamp in lamps)
                {
                    var light = lamp.GetComponentInChildren<Light>(true);
                    var lens = lamp.transform.Find("Lens").GetComponent<Renderer>().sharedMaterial;
                    var good = lamp.IsOn == on && light.enabled == on && lens.name == (on ? "AlarmLens_On" : "AlarmLens_Off") &&
                               light.shadows == LightShadows.None && Mathf.Approximately(light.range, 4f);
                    Row($"Alarmlamp {(on ? "aan" : "uit")}", lamp.name, $"licht {(light.enabled ? "aan" : "uit")}, lens {lens.name}", good);
                    ok &= good;
                }
                return ok;
            }

            if (!State(false))
                failures.Add("in rust niet uit");
            var countsOff = LightsPerRenderer();
            yield return AlarmShot("off");

            lamps.ForEach(l => l.SetOn(true));
            yield return null;
            if (!State(true))
                failures.Add("SetOn(true) werkt niet");
            var countsOn = LightsPerRenderer();
            yield return AlarmShot("on");
            foreach (var (label, counts) in new[] { ("alarm uit", countsOff), ("alarm aan", countsOn) })
            {
                var over = counts.Where(c => c.Value > MaxLightsPerObject).OrderByDescending(c => c.Value).ToList();
                Row("Lichten per object", label, $"max {counts.Values.DefaultIfEmpty(0).Max()} puntlichten op één renderer, " +
                    $"{over.Count} renderers met meer dan {MaxLightsPerObject}" +
                    (over.Count > 0 ? " (bv. " + string.Join(", ", over.Take(4).Select(o => $"{o.Key} {o.Value}")) + ")" : ""), true);
            }
            lamps.ForEach(l => l.SetOn(false));

            yield return new ExitPlayMode();
            Assert.IsEmpty(failures, "Alarmlampen: " + string.Join(", ", failures));
        }

        /// <summary>Per zichtbare renderer: aantal actieve puntlichten waarvan het bereik zijn bounds raakt.</summary>
        private static Dictionary<string, int> LightsPerRenderer()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.enabled && l.type == LightType.Point).ToList();
            var result = new Dictionary<string, int>();
            foreach (var r in Root.GetComponentsInChildren<MeshRenderer>())
            {
                var n = lights.Count(l => r.bounds.SqrDistance(l.transform.position) <= l.range * l.range);
                var key = r.transform.parent != null ? $"{r.transform.parent.name}/{r.name}" : r.name;
                result[key] = Mathf.Max(n, result.TryGetValue(key, out var old) ? old : 0);
            }
            return result;
        }

        /// <summary>De rechtergang vanuit het zuiden: gangdeur van de gesprekskamer, de dochter en de deur van 3C.</summary>
        private static IEnumerator AlarmShot(string state)
        {
            var go = new GameObject("TestCamera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            var eye = ToUnity(35.3f, 12.6f, 1.65f);
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(ToUnity(34.6f, 3.0f, 1.6f) - eye));
            HospitalAssetPrep.Capture(cam, $"docs/Screenshots/u_afdeling_alarm_{state}.png");
            Object.Destroy(go);
            yield return null;
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
            if (!CheckBedEdge(root, "Body_Pyjama"))
                failures.Add("3C pyjama");
            yield return PreOpScreenshots(root, "pyjama");

            // Scenario 2: het schort aan, zoals Jalil het later omwisselt
            var patient3C = root.Find("Characters/Patient_3C");
            patient3C.Find("Body_Pyjama").gameObject.SetActive(false);
            patient3C.Find("Body_Gown").gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            if (!CheckBedEdge(root, "Body_Gown"))
                failures.Add("3C gown");
            yield return PreOpScreenshots(root, "gown");
            patient3C.Find("Body_Gown").gameObject.SetActive(false);
            patient3C.Find("Body_Pyjama").gameObject.SetActive(true);

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

        private static IEnumerator PreOpScreenshots(Transform root, string outfit)
        {
            var shots = UWardBuilder.PreOpShots(root);
            for (var i = 0; i < shots.Length; i++)
            {
                var go = new GameObject("TestCamera");
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 75f;
                cam.nearClipPlane = 0.05f;
                go.transform.SetPositionAndRotation(shots[i].Eye, Quaternion.LookRotation(shots[i].Target - shots[i].Eye));
                HospitalAssetPrep.Capture(cam, $"docs/Screenshots/u_afdeling_3C_play_{outfit}_{(i == 0 ? "door" : "close")}.png");
                Object.Destroy(go);
                yield return null;
            }
        }

        /// <summary>
        /// Scenario 2, kamer 3C: de vader zit op de rand van bed 1. Heupen op het matras (hoogstens 5 cm erin, hoogstens 20 cm
        /// erboven) en boven het bed, voeten op de vloer en buiten het bed, gezicht weg van het bed de kamer in.
        /// </summary>
        private static bool CheckBedEdge(Transform root, string body)
        {
            var room = root.Find("Floor3/Room_3C");
            var anchor = room.Find("Furniture/Bed/Anchor_BedEdge");
            var animator = root.Find($"Characters/Patient_3C/{body}").GetComponent<Animator>();
            var bed = RendererBounds(room.Find("Furniture/Bed"));
            Vector3 Bone(HumanBodyBones b) => animator.GetBoneTransform(b).position;
            var hips = Bone(HumanBodyBones.Hips);
            var leftFoot = Bone(HumanBodyBones.LeftFoot);
            var rightFoot = Bone(HumanBodyBones.RightFoot);
            var feetY = Mathf.Min(leftFoot.y, rightFoot.y);
            bool InBed(Vector3 p) => p.x > bed.min.x && p.x < bed.max.x && p.z > bed.min.z && p.z < bed.max.z;
            var facing = Vector3.Dot(Vector3.ProjectOnPlane(animator.transform.forward, Vector3.up).normalized, anchor.forward);
            var aboveMattress = hips.y - anchor.position.y;
            var ok = aboveMattress > -0.05f && aboveMattress < 0.2f && InBed(hips) && feetY < 0.15f &&
                     !InBed((leftFoot + rightFoot) / 2f) && facing > 0.95f;
            Row("Play mode: zit op de bedrand", $"Patient_3C/{body}", $"heupen {aboveMattress:+0.00;-0.00} m t.o.v. matras, " +
                $"voeten op {feetY:0.00} m, heupen boven het bed {(InBed(hips) ? "ja" : "nee")}, voeten naast het bed " +
                $"{(!InBed((leftFoot + rightFoot) / 2f) ? "ja" : "nee")}, kijkrichting cos {facing:0.00}", ok);
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
