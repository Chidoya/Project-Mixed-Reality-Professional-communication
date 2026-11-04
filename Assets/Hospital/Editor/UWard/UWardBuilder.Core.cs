using System;
using System.Collections.Generic;
using System.Linq;
using ProCommVR.Hospital;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Fase 3: de kern. Balie en StaffStation in de hal (kopie van liam_test_scene, met dezelfde namen en ankers),
    /// wachtstoelen, Floor2/NurseStation (leeg, voor Jalils scenario 3), briefingruimte, toiletten, rustruimte en
    /// medicatiekamer. Het trappenhuis en de liften volgen in fase 4.
    /// </summary>
    public static partial class UWardBuilder
    {
        // Richtingen in het plan (y loopt naar onder)
        private static readonly Vector2 PlanNorth = new Vector2(0f, -1f);
        private static readonly Vector2 PlanSouth = new Vector2(0f, 1f);
        private static readonly Vector2 PlanWest = new Vector2(-1f, 0f);
        private static readonly Vector2 PlanEast = new Vector2(1f, 0f);

        // Balie (U-vorm, open kant naar de gang): bezoekers aan de kant van de liften, personeel binnen in de U
        private const float CounterDepth = 0.6f;
        private const float CounterHeight = 1.05f;
        private const float CounterTopThickness = 0.04f;
        private const float WorkTopHeight = 0.74f;
        private const float WorkTopDepth = 0.55f;

        // StaffStation (zoals in liam_test_scene); de wanditems hangen op het oostelijke stuk van Lobby/Wall_North
        private static readonly Vector3 PropFront = Vector3.back;   // voorkant van de props, gemeten door PropSetup
        private const float DeskItemInset = 0.6f;
        private const float DeskItemEdgeGap = 0.15f;
        private const float EndScreenY = 1.7f;

        private const float DoorClearance = 0.5f;   // vrij aan elke kant van een deur (wachtstoelen)
        private const float CoreLightRange = 6f;
        private const float CoreLightIntensity = 1.4f;
        private static readonly Color WarmLightColor = new Color(1f, 0.82f, 0.6f);

        private static Material CounterMat => Mat("ReceptionCounter", new Color(0.78f, 0.62f, 0.45f));
        private static Material TopMat => Mat("ReceptionTop", new Color(0.95f, 0.95f, 0.94f));
        private static Material MetalMat => Mat("WindowFrame", new Color(0.97f, 0.97f, 0.97f));

        private static void BuildCore(Dictionary<string, Transform> groups)
        {
            var lobby = groups["Lobby"];
            var furniture = Child(lobby, "Furniture");
            BuildReceptionDesk(furniture);
            BuildStaffStation(lobby);
            BuildWaitingArea(furniture);

            var nurseStation = Child(groups["Floor2"], "NurseStation");
            nurseStation.SetPositionAndRotation(PlanPoint(new Vector2(12.7f, 20.3f)), Quaternion.LookRotation(PlanDirection(PlanNorth)));

            // Deuren van de kern eerst (de toiletten hangen hun bord aan de deur): positie, breedte en draairichting komen uit
            // UWardLayout.CoreDoors, dezelfde data waarmee de muuropeningen gebouwd worden. Allemaal dicht in rust.
            var core = groups["Core"];
            foreach (var spec in CoreDoors)
            {
                var door = PlaceDoor(core, spec.Name, spec.Center, spec.Into, spec.Segment.Length);
                if (spec.Locked)
                    Lock(door, spec.Segment.Length, StairsClosedReason);
            }

            BuildBriefingRoom(Child(core, "BriefingRoom"));
            BuildToilet(Child(core, "Toilet_Men"), CoreRoom("Toilet_Men"), core.Find("Door_Toilet_Men"), "Men", false);
            BuildToilet(Child(core, "Toilet_Women"), CoreRoom("Toilet_Women"), core.Find("Door_Toilet_Women"), "Women", true);
            BuildRestRoom(Child(core, "RestRoom"));
            BuildMedicationRoom(Child(core, "MedicationRoom"));
        }

        private static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null)
                return t;
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static PlanRect CoreRoom(string name) => CoreRooms.First(r => r.Name == name).Rect;

        /// <summary>Binnenmaat van een ruimte: tot de binnenkant van de muren (buitenmuur aan de rand van het gebouw).</summary>
        private static PlanRect Inner(PlanRect r)
        {
            float Off(float line, float edge) => Mathf.Approximately(line, edge) ? OuterWall : InnerWall / 2f;
            return new PlanRect(r.X0 + Off(r.X0, 0f), r.Y0 + Off(r.Y0, 0f), r.X1 - Off(r.X1, Width), r.Y1 - Off(r.Y1, Depth));
        }

        // --- Hal --------------------------------------------------------------------------

        /// <summary>
        /// U-vormige balie van 5 x 2,4 m (Lobby/Furniture/ReceptionDesk): toog aan de kant van de liften en twee zijarmen,
        /// met binnenin een lager werkblad, twee bureaustoelen en een computer. De bezoekerskant blijft vrij.
        /// </summary>
        private static void BuildReceptionDesk(Transform furniture)
        {
            var desk = Child(furniture, "ReceptionDesk");
            var r = ReceptionDesk;
            Counter("Counter_Front", desk, new PlanRect(r.X0, r.Y0, r.X1, r.Y0 + CounterDepth));
            Counter("Counter_West", desk, new PlanRect(r.X0, r.Y0 + CounterDepth, r.X0 + CounterDepth, r.Y1));
            Counter("Counter_East", desk, new PlanRect(r.X1 - CounterDepth, r.Y0 + CounterDepth, r.X1, r.Y1));
            var work = new PlanRect(r.X0 + CounterDepth, r.Y0 + CounterDepth, r.X1 - CounterDepth, r.Y0 + CounterDepth + WorkTopDepth);
            Box("WorkTop", desk, ToUnity(work.CenterX, work.CenterY, WorkTopHeight - CounterTopThickness / 2f),
                new Vector3(work.X1 - work.X0, CounterTopThickness, work.Y1 - work.Y0), TopMat);

            for (var i = 0; i < 2; i++)
            {
                var pos = PlanPoint(new Vector2(r.CenterX + (i == 0 ? -0.8f : 0.8f), work.Y1 + 0.55f));
                var chair = PlaceKenney("chairDesk", furniture, i == 0 ? "ReceptionChair" : "ReceptionChair_2", pos, 0f);
                OrientBack(chair, PlanDirection(PlanSouth), pos);
            }
            var screen = PlaceKenney("computerScreen", desk, "Computer", PlanPoint(new Vector2(r.CenterX, work.Y0 + 0.15f), WorkTopHeight), 0f);
            Face(screen, PlanSouth);
            var keyboard = PlaceKenney("computerKeyboard", desk, "Keyboard", PlanPoint(new Vector2(r.CenterX, work.Y0 + 0.38f), WorkTopHeight), 0f);
            Face(keyboard, PlanSouth);
        }

        /// <summary>Kast van de balie: houten romp met een wit blad dat 2 cm uitsteekt.</summary>
        private static void Counter(string name, Transform parent, PlanRect rect)
        {
            var part = Child(parent, name);
            var size = new Vector3(rect.X1 - rect.X0, CounterHeight - CounterTopThickness, rect.Y1 - rect.Y0);
            Box("Body", part, ToUnity(rect.CenterX, rect.CenterY, size.y / 2f), size, CounterMat);
            Box("Top", part, ToUnity(rect.CenterX, rect.CenterY, CounterHeight - CounterTopThickness / 2f),
                new Vector3(size.x + 0.04f, CounterTopThickness, size.z + 0.04f), TopMat);
        }

        /// <summary>
        /// Lobby/StaffStation zoals in liam_test_scene: badgelezer, tabletdock en klok aan de muur achter de balie, klembord en
        /// tablet op de balie aan de personeelskant, kar naast de balie, en dezelfde ankers. Vooruit (z) van een anker = de
        /// richting waarin de speler kijkt of reikt.
        /// </summary>
        private static void BuildStaffStation(Transform lobby)
        {
            var station = Child(lobby, "StaffStation");
            var desk = lobby.Find("Furniture/ReceptionDesk");
            var d = HospitalAssetPrep.WorldBounds(desk.gameObject);

            // Oostelijk stuk van Lobby/Wall_North (x 24,0..25,0), hal-kant; het westelijke stuk blijft vrij
            const float wallY = 19.6f - InnerWall / 2f;
            var toWall = PlanDirection(PlanSouth);
            var front = -toWall;
            Vector3 WallPoint(float x, float height) => PlanPoint(new Vector2(x, wallY), height);
            PlaceProp("Badge_Reader", station, WallPoint(24.3f, 1.1f), front);
            PlaceProp("Tablet_Dock", station, WallPoint(24.7f, 1.0f), front);
            var clock = PlaceProp("Wall_Clock", station, WallPoint(24.5f, 2.05f), front);
            if (clock != null && clock.TryGetComponent<WallClock>(out var wallClock))
                wallClock.SetTime(DateTime.Now);

            // Op de toog aan de personeelskant; de bezoekerskant (naar de liften) blijft vrij voor Jalils startcomputer
            var staffEdge = PlanPoint(new Vector2(0f, ReceptionDesk.Y0 + CounterDepth - DeskItemEdgeGap)).z;
            var clipboardPos = DeskTop(desk.gameObject, d, d.max.x - DeskItemInset, staffEdge);
            var tabletPos = DeskTop(desk.gameObject, d, d.min.x + DeskItemInset, staffEdge);
            var towardStaff = Quaternion.Euler(0f, 78f, 0f) * toWall;
            var clipboard = PlaceProp("Clipboard", station, clipboardPos, towardStaff);
            PlaceProp("Care_Tablet", station, tabletPos, Quaternion.Euler(0f, 10f, 0f) * toWall);
            PlaceProp("Supply_Cart", station, PlanPoint(new Vector2(ReceptionDesk.X1 + 0.45f, ReceptionDesk.CenterY + 0.3f)),
                PlanDirection(PlanNorth));

            var face = Quaternion.LookRotation(toWall);
            Anchor("Anchor_BadgeScan", station, WallPoint(24.3f, 1.16f) - toWall * 0.1f, face);
            Anchor("Anchor_TabletDock", station, WallPoint(24.7f, 1.02f) - toWall * 0.03f, face);
            if (clipboard != null)
                Anchor("Anchor_Clipboard", station, clipboardPos + Vector3.up * 0.01f, Quaternion.LookRotation(-towardStaff));
            // Scherm voor het einde van de shift: westmuur van de hal, tussen de deuren van de rustruimte en de medicatiekamer
            Anchor("Anchor_EndScreen", station, PlanPoint(new Vector2(17f + InnerWall / 2f + 0.02f, 12.3f), EndScreenY),
                Quaternion.LookRotation(PlanDirection(PlanWest)));
        }

        /// <summary>Prop-prefab (PropSetup) met zijn oorsprong op position, enkel rond y gedraaid tot de voorkant naar front wijst.</summary>
        private static GameObject PlaceProp(string prop, Transform parent, Vector3 position, Vector3 front)
        {
            var prefab = PropSetup.LoadPrefab(prop);
            if (prefab == null)
            {
                Debug.LogError($"{LogPrefix}Prop '{prop}' niet gevonden. Draai ProCommVR > Hospital > Setup Props.");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = prop;
            var yaw = Vector3.SignedAngle(PropFront, Vector3.ProjectOnPlane(front, Vector3.up), Vector3.up);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            go.transform.position = position;
            return go;
        }

        /// <summary>Bovenvlak van de balie in (x, z), gemeten met een raycast; anders de bovenkant van de bounds.</summary>
        private static Vector3 DeskTop(GameObject desk, Bounds bounds, float x, float z)
        {
            var top = TopAt(desk, x, z);
            return new Vector3(x, float.IsNaN(top) ? bounds.max.y : top, z);
        }

        /// <summary>
        /// Wachtstoelen tegen de zijmuren van de hal, enkel waar ze minstens DoorClearance van elke deur in die muur blijven:
        /// per vrij stuk zoveel als er passen, gecentreerd. West tot de rustruimte (het stuk erna is voor Anchor_EndScreen),
        /// oost tot de planten. En twee planten.
        /// </summary>
        private static void BuildWaitingArea(Transform furniture)
        {
            var hall = Inner(CoreRoom("Hall"));
            var lifts = LiftWallY + LiftWallThickness / 2f;
            var index = 1;
            foreach (var (x, toWall, end, max) in new[] { (hall.X0, PlanWest, 9.6f, 3), (hall.X1, PlanEast, 12.1f, 2) })
            {
                var chairs = 0;
                foreach (var (from, to) in FreeAlongWall(x, lifts, end))
                {
                    var probe = AgainstWall("loungeChair", furniture, "Probe", new Vector2(x, from), toWall);
                    var width = Width1(probe, PlanSouth) + 0.05f;
                    Object.DestroyImmediate(probe);
                    var count = Mathf.Min(Mathf.FloorToInt((to - from) / width), max - chairs);
                    for (var i = 0; i < count; i++, chairs++)
                    {
                        var y = (from + to) / 2f + (i - (count - 1) / 2f) * width;
                        AgainstWall("loungeChair", furniture, $"WaitingChair_{index++}", new Vector2(x, y), toWall);
                    }
                }
            }
            AgainstWall("pottedPlant", furniture, "Plant_1", new Vector2(hall.X1, 12.5f), PlanEast);
            AgainstWall("pottedPlant", furniture, "Plant_2", new Vector2(hall.X1, 14.6f), PlanEast);
        }

        /// <summary>Vrije stukken (plan-y) langs de muur op x tussen from en to, met DoorClearance rond elke deur in die muur.</summary>
        private static IEnumerable<(float From, float To)> FreeAlongWall(float x, float from, float to)
        {
            var doors = CoreDoors.Where(d => d.Segment.Vertical && Mathf.Abs(d.Segment.At - x) < 0.2f)
                .OrderBy(d => d.Segment.From);
            var cursor = from;
            foreach (var d in doors)
            {
                var end = Mathf.Min(d.Segment.From - DoorClearance, to);
                if (end > cursor)
                    yield return (cursor, end);
                cursor = Mathf.Max(cursor, d.Segment.To + DoorClearance);
            }
            if (to > cursor)
                yield return (cursor, to);
        }

        // --- Briefingruimte -----------------------------------------------------------------

        /// <summary>Tafel van 4,6 x 1,8 m met 8 stoelen, scherm aan de oostmuur, whiteboard aan de noordmuur, twee lampen.</summary>
        private static void BuildBriefingRoom(Transform room)
        {
            var r = Inner(CoreRoom("BriefingRoom"));
            var furniture = Child(room, "Furniture");
            var center = new Vector2(r.CenterX - 0.3f, r.CenterY);
            const float length = 4.6f, width = 1.8f, height = 0.74f, legInset = 0.15f;

            var table = Child(furniture, "Table");
            Box("Top", table, PlanPoint(center, height - 0.02f), new Vector3(length, 0.04f, width), TopMat);
            for (var i = 0; i < 4; i++)
            {
                var leg = center + new Vector2((i % 2 == 0 ? -1f : 1f) * (length / 2f - legInset), (i < 2 ? -1f : 1f) * (width / 2f - legInset));
                Box($"Leg_{i + 1}", table, PlanPoint(leg, (height - 0.04f) / 2f), new Vector3(0.05f, height - 0.04f, 0.05f), MetalMat);
            }

            var chair = 1;
            foreach (var side in new[] { PlanNorth, PlanSouth })
                for (var i = 0; i < 4; i++)
                {
                    var pos = PlanPoint(center + new Vector2((i - 1.5f) * 1.1f, 0f) + side * (width / 2f + 0.3f));
                    var go = PlaceKenney("chair", furniture, $"Chair_{chair++}", pos, 0f);
                    OrientBack(go, PlanDirection(side), pos);
                }

            var screenSize = new Vector3(0.05f, 1.25f, 2.2f);
            Box("Screen", room, PlanPoint(new Vector2(r.X1 - screenSize.x / 2f, center.y), 1.6f), screenSize,
                Mat("ScreenBlack", new Color(0.08f, 0.09f, 0.1f), 0f, 0.8f));
            var board = Child(room, "Whiteboard");
            Box("Board", board, PlanPoint(new Vector2(center.x, r.Y0 + 0.015f), 1.5f), new Vector3(2.4f, 1.2f, 0.03f),
                Mat("Whiteboard", new Color(0.98f, 0.98f, 0.98f), 0f, 0.7f));
            Box("Tray", board, PlanPoint(new Vector2(center.x, r.Y0 + 0.05f), 0.88f), new Vector3(2.4f, 0.03f, 0.1f), MetalMat);

            CeilingLight(room, "1", center.x, r.Y0 + (r.Y1 - r.Y0) * 0.25f, CoreLightRange, CoreLightIntensity);
            CeilingLight(room, "2", center.x, r.Y0 + (r.Y1 - r.Y0) * 0.75f, CoreLightRange, CoreLightIntensity);
        }

        // --- Toiletten ------------------------------------------------------------------------

        /// <summary>
        /// Twee toilethokjes aan de oostkant (wanden van 2 m hoog), twee lavabo's met spiegel aan de noordmuur, een lamp, en
        /// naast de deur in de hal een bord met een pictogram.
        /// </summary>
        private static void BuildToilet(Transform room, PlanRect rect, Transform door, string label, bool women)
        {
            var r = Inner(rect);
            var furniture = Child(room, "Furniture");
            var partition = Mat("Partition", new Color(0.80f, 0.84f, 0.88f));
            const float frontX = 31.7f, bottom = 0.15f, top = 2.1f, t = 0.04f, gap = 0.7f;
            var mid = r.CenterY;

            Box("Divider", furniture, PlanPoint(new Vector2((frontX + r.X1) / 2f, mid), (bottom + top) / 2f),
                new Vector3(r.X1 - frontX, top - bottom, t), partition);
            var cubicles = new[] { (r.Y0, mid), (mid, r.Y1) };
            for (var i = 0; i < cubicles.Length; i++)
            {
                var (a, b) = cubicles[i];
                var c = (a + b) / 2f;
                foreach (var (from, to, part) in new[] { (a, c - gap / 2f, "A"), (c + gap / 2f, b, "B") })
                    Box($"Front_{i + 1}{part}", furniture, PlanPoint(new Vector2(frontX, (from + to) / 2f), (bottom + top) / 2f),
                        new Vector3(t, top - bottom, to - from), partition);
                AgainstWall("toilet", furniture, $"Toilet_{i + 1}", new Vector2(r.X1, c), PlanEast);
            }
            for (var i = 0; i < 2; i++)
            {
                var x = r.X0 + 1.6f + i * 1.2f;
                AgainstWall("bathroomSink", furniture, $"Sink_{i + 1}", new Vector2(x, r.Y0), PlanNorth);
                AgainstWall("bathroomMirror", furniture, $"Mirror_{i + 1}", new Vector2(x, r.Y0), PlanNorth, 1.2f);
            }
            AddCeilingLamp(room, "Lamp", PlanPoint(new Vector2(r.CenterX, r.CenterY), CeilingHeight));
            AddLight("Light", room, PlanPoint(new Vector2(r.CenterX, r.CenterY), CeilingHeight - 0.4f), CoreLightRange, CoreLightIntensity);

            ToiletSign(door, $"Sign_Toilet_{label}", label, women);
        }

        /// <summary>
        /// Blauw bord met een wit pictogram (man of vrouw) en de tekst eronder, op de halkant van het deurblad (gemeten met een
        /// raycast), als kind van de deur zodat het meedraait. Lokale -z wijst van de deur weg.
        /// </summary>
        private static void ToiletSign(Transform door, string name, string text, bool women)
        {
            const float w = 0.3f, h = 0.45f, depth = 0.02f, height = 1.6f;
            var model = door.GetComponentInChildren<MeshFilter>();
            var probe = model.gameObject.AddComponent<MeshCollider>();
            probe.sharedMesh = model.sharedMesh;
            Physics.SyncTransforms();
            var from = door.TransformPoint(new Vector3(0f, height, -1f));
            var face = Physics.Raycast(from, door.forward, out var hit, 2f) && hit.collider == probe
                ? hit.point
                : door.TransformPoint(new Vector3(0f, height, -InnerWall / 2f));
            Object.DestroyImmediate(probe);

            var sign = Child(door, name);
            sign.SetPositionAndRotation(face - door.forward * 0.002f, door.rotation);
            Box("Panel", sign, new Vector3(0f, 0f, -depth / 2f), new Vector3(w, h, depth), Mat("SignToilet", new Color(0.20f, 0.40f, 0.65f)));

            var icon = Mat("SignIcon", Color.white);
            var z = -depth - 0.004f;
            void Part(string part, float x, float y, float pw, float ph) => Box(part, sign, new Vector3(x, y, z), new Vector3(pw, ph, 0.006f), icon);
            Part("Head", 0f, 0.14f, 0.06f, 0.06f);
            if (women)
            {
                Part("Torso", 0f, 0.075f, 0.06f, 0.05f);
                Part("Skirt", 0f, 0.01f, 0.12f, 0.08f);
                Part("Leg_L", -0.02f, -0.06f, 0.025f, 0.06f);
                Part("Leg_R", 0.02f, -0.06f, 0.025f, 0.06f);
            }
            else
            {
                Part("Body", 0f, 0.045f, 0.09f, 0.11f);
                Part("Leg_L", -0.025f, -0.06f, 0.035f, 0.1f);
                Part("Leg_R", 0.025f, -0.06f, 0.035f, 0.1f);
            }
            Label("Text", sign, text, new Vector3(0f, -0.16f, z), Vector3.back, w * 0.85f, 0.08f, Color.white);
        }

        // --- Rustruimte en medicatiekamer ------------------------------------------------------

        /// <summary>Zetel, fauteuil en salontafel, staande lamp, koffiehoek tegen de noordmuur, warm licht.</summary>
        private static void BuildRestRoom(Transform room)
        {
            var r = Inner(CoreRoom("RestRoom"));
            var furniture = Child(room, "Furniture");
            var y = 10.1f;
            AgainstWall("loungeSofa", furniture, "Sofa", new Vector2(r.X0, y), PlanWest);
            PlaceKenney("tableCoffee", furniture, "CoffeeTable", PlanPoint(new Vector2(r.X0 + 1.9f, y)), 90f);
            var armchairPos = PlanPoint(new Vector2(r.X0 + 3.4f, y));
            OrientBack(PlaceKenney("loungeChair", furniture, "Armchair", armchairPos, 0f), PlanDirection(PlanEast), armchairPos);
            AgainstWall("lampSquareFloor", furniture, "FloorLamp", new Vector2(r.X0, r.Y1 - 0.4f), PlanWest);
            AgainstWall("pottedPlant", furniture, "Plant", new Vector2(r.X0, r.Y0 + 0.4f), PlanWest);

            var cabinets = RowAgainstWall("kitchenCabinet", furniture, "CoffeeCabinet", new Vector2(r.X1 - 2.2f, r.Y0), PlanEast, PlanNorth, 2);
            OnTop("kitchenCoffeeMachine", furniture, "CoffeeMachine", cabinets[0], PlanNorth);
            var fridge = RowAgainstWall("kitchenFridgeSmall", furniture, "Fridge", new Vector2(r.X1 - 2.2f, r.Y0), PlanWest, PlanNorth, 1);
            fridge[0].transform.position += PlanDirection(PlanWest) * 0.02f;

            var center = PlanPoint(new Vector2(r.CenterX, r.CenterY), CeilingHeight);
            AddCeilingLamp(room, "Lamp", center);
            AddLight("Light", room, center + Vector3.down * 0.4f, CoreLightRange, 1.2f);
            room.Find("Light").GetComponent<Light>().color = WarmLightColor;
        }

        /// <summary>Kasten in L-vorm (onder- en bovenkasten tegen de westmuur, onderkasten met spoelbak tegen de zuidmuur), koelkast, werktafel.</summary>
        private static void BuildMedicationRoom(Transform room)
        {
            var r = Inner(CoreRoom("MedicationRoom"));
            var furniture = Child(room, "Furniture");
            var west = RowAgainstWall("kitchenCabinet", furniture, "Cabinet_West", new Vector2(r.X0, r.Y0 + 0.3f), PlanSouth, PlanWest, 6);
            RowAgainstWall("kitchenCabinetUpper", furniture, "CabinetUpper_West", new Vector2(r.X0, r.Y0 + 0.3f), PlanSouth, PlanWest, 6, 1.45f);
            var depth = HospitalAssetPrep.WorldBounds(west[0]).size.x;
            var southStart = new Vector2(r.X0 + depth, r.Y1);
            var south = RowAgainstWall("kitchenCabinet", furniture, "Cabinet_South", southStart, PlanEast, PlanSouth, 1);
            var sinkStart = southStart + PlanEast * Width1(south[0], PlanEast);
            var sink = RowAgainstWall("kitchenSink", furniture, "Sink", sinkStart, PlanEast, PlanSouth, 1);
            var restStart = sinkStart + PlanEast * Width1(sink[0], PlanEast);
            var rest = RowAgainstWall("kitchenCabinet", furniture, "Cabinet_South", restStart, PlanEast, PlanSouth, 2);
            rest[0].name = "Cabinet_South_2";
            rest[1].name = "Cabinet_South_3";
            RowAgainstWall("kitchenFridgeSmall", furniture, "MedicationFridge",
                restStart + PlanEast * (Width1(rest[0], PlanEast) * 2f + 0.05f), PlanEast, PlanSouth, 1);

            PlaceKenney("table", furniture, "WorkTable", PlanPoint(new Vector2(r.CenterX + 0.6f, r.CenterY - 0.3f)), 0f);

            var center = PlanPoint(new Vector2(r.CenterX, r.CenterY), CeilingHeight);
            AddCeilingLamp(room, "Lamp", center);
            AddLight("Light", room, center + Vector3.down * 0.4f, CoreLightRange, CoreLightIntensity);
        }

        // --- Meubels tegen een muur --------------------------------------------------------------

        /// <summary>Kenney-model met de rug (hoogste kant) naar de muur, tegen de muur geschoven. wallPoint ligt op de binnenkant van de muur.</summary>
        private static GameObject AgainstWall(string model, Transform parent, string name, Vector2 wallPoint, Vector2 toWall, float height = 0f)
        {
            var wall = PlanPoint(wallPoint, height);
            var go = PlaceKenney(model, parent, name, parent.InverseTransformPoint(wall), 0f);
            Face(go, -toWall);
            PushToWall(go, wall, PlanDirection(toWall));
            return go;
        }

        /// <summary>Draait een Kenney-model zodat zijn voorkant naar front (plan) wijst; zie FaceWorld.</summary>
        private static void Face(GameObject go, Vector2 front) => FaceWorld(go, PlanDirection(front));

        /// <summary>
        /// Draait een Kenney-model enkel rond de wereld-y zodat zijn voorkant naar front (wereld) wijst, met het midden op zijn plek.
        /// Welke lokale as de voorkant is, staat in UWardLayout.FrontYaw (de meeste modellen: +Z). Stoelen en zetels worden
        /// met OrientBack gericht (hun rug is duidelijk de hoogste kant).
        /// </summary>
        private static void FaceWorld(GameObject go, Vector3 front)
        {
            var b = HospitalAssetPrep.WorldBounds(go);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            go.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(front, Vector3.up)) *
                                    Quaternion.Euler(0f, -FrontYaw(source != null ? source.name : null), 0f);
            AlignToFloor(go, new Vector3(b.center.x, b.min.y, b.center.z));
        }

        private static void PushToWall(GameObject go, Vector3 wallPoint, Vector3 back)
        {
            var b = HospitalAssetPrep.WorldBounds(go);
            var reach = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(back.x), 0f, Mathf.Abs(back.z))));
            AlignToFloor(go, wallPoint - back * (reach + 0.01f));
        }

        /// <summary>Breedte van een object langs een richting in het plan.</summary>
        private static float Width1(GameObject go, Vector2 along)
        {
            var size = HospitalAssetPrep.WorldBounds(go).size;
            return Mathf.Abs(along.x) > 0.5f ? size.x : size.z;
        }

        /// <summary>Rij van count modellen tegen een muur, vanaf start langs along (start = rand van het eerste model).</summary>
        private static List<GameObject> RowAgainstWall(string model, Transform parent, string prefix, Vector2 start, Vector2 along,
            Vector2 toWall, int count, float height = 0f)
        {
            var items = new List<GameObject>();
            var offset = 0f;
            for (var i = 0; i < count; i++)
            {
                var go = AgainstWall(model, parent, count == 1 ? prefix : $"{prefix}_{i + 1}", start, toWall, height);
                var w = Width1(go, along);
                go.transform.position += PlanDirection(along) * (offset + w / 2f);
                offset += w;
                items.Add(go);
            }
            Physics.SyncTransforms();
            return items;
        }

        /// <summary>Model bovenop een ander object (bv. koffiemachine op een kast), naar front gericht en tegen dezelfde muur.</summary>
        private static void OnTop(string model, Transform parent, string name, GameObject below, Vector2 toWall)
        {
            var b = HospitalAssetPrep.WorldBounds(below);
            var go = PlaceKenney(model, parent, name, parent.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z)), 0f);
            Face(go, -toWall);
            var back = PlanDirection(toWall);
            var wall = new Vector3(b.center.x, b.max.y, b.center.z) + back * Mathf.Abs(Vector3.Dot(b.extents, back));
            PushToWall(go, wall, back);
        }

        // --- Screenshots --------------------------------------------------------------------

        private static void TakeCoreScreenshots()
        {
            void CoreShot(string file, Vector2 eye, Vector2 target, float targetHeight = 0.9f) =>
                Shot($"u_afdeling_core_{file}.png", PlanPoint(eye, EyeHeight), PlanPoint(target, targetHeight));
            CoreShot("hall", new Vector2(21f, 4.5f), new Vector2(21f, 16.5f));
            CoreShot("desk_staff", new Vector2(21f, 19.2f), new Vector2(21f, 15.0f), 1.0f);
            CoreShot("briefing", new Vector2(26.0f, 18.8f), new Vector2(31.5f, 12.5f));
            CoreShot("toilet", new Vector2(25.6f, 3.6f), new Vector2(33f, 1.6f));
            CoreShot("rest", new Vector2(16.4f, 12.8f), new Vector2(9.5f, 9f));
            CoreShot("medication", new Vector2(16.4f, 14.0f), new Vector2(9.5f, 18.5f));
        }
    }
}
