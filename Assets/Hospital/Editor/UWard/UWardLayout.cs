using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Alle maten van de U-afdeling (docs/Plattegrond/maten.md, versie 8) in plancoördinaten:
    /// x van links naar rechts (0..42), y van boven naar onder (0..28), in meter.
    /// Omrekenen naar Unity: X = x - 21, Z = 14 - y (de bovenkant van het plan is +Z).
    /// </summary>
    public static class UWardLayout
    {
        // --- Gebouw -----------------------------------------------------------------
        public const float Width = 42f;
        public const float Depth = 28f;
        public const float CeilingHeight = 3.0f;
        public const float OuterWall = 0.25f;
        public const float InnerWall = 0.16f;
        public const float SlabThickness = 0.2f;
        public const float FloorFinishThickness = 0.01f;

        // Openingen
        public const float PatientDoorWidth = 1.3f;
        public const float DoorHeight = 2.1f;
        public const float WindowWidth = 1.4f;
        public const float WindowSill = 0.9f;
        public const float WindowTop = 2.2f;
        public const float PassageTop = 2.7f;          // de 6 m brede doorgang tussen de hal en de gang onder

        // Accentstreep op de gangkant van de kamermuren
        public const float StripeBottom = 0.95f;
        public const float StripeHeight = 0.15f;
        public const float StripeDepth = 0.02f;

        public static Vector3 ToUnity(float x, float y, float height = 0f) => new Vector3(x - Width / 2f, height, Depth / 2f - y);

        // --- Voorkant van de Kenney-modellen -------------------------------------------------
        // Nagekeken met het model alleen in een lege scene, rotatie (0, 0, 0), bekeken vanaf +Z, -Z, +X en -X:
        // cabinetBed (deurtje), bedSingle (voeteneinde), chair (zitkant), bathroomSink, kitchenCabinet: lokaal +Z.
        // Uitzonderingen: het reservoir van het toilet en de wand van de douche zitten aan +X, dus hun voorkant is -X.
        // Waarde: hoek rond Y van +Z naar de voorkant.
        private static readonly Dictionary<string, float> ModelFrontYaw = new Dictionary<string, float>
        {
            { "toilet", -90f },
            { "shower", -90f },
        };

        public static float FrontYaw(string model) => model != null && ModelFrontYaw.TryGetValue(model, out var yaw) ? yaw : 0f;

        // --- Typen ------------------------------------------------------------------

        /// <summary>Rechthoek in het plan.</summary>
        public readonly struct PlanRect
        {
            public readonly float X0, Y0, X1, Y1;
            public PlanRect(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
            public float CenterX => (X0 + X1) / 2f;
            public float CenterY => (Y0 + Y1) / 2f;
        }

        /// <summary>Lijnstuk in het plan: verticaal (x = At, y van From tot To) of horizontaal (y = At, x van From tot To).</summary>
        public readonly struct Segment
        {
            public readonly bool Vertical;
            public readonly float At, From, To;
            public Segment(bool vertical, float at, float from, float to) { Vertical = vertical; At = at; From = from; To = to; }
            public static Segment V(float x, float y0, float y1) => new Segment(true, x, y0, y1);
            public static Segment H(float y, float x0, float x1) => new Segment(false, y, x0, x1);
            public float Length => To - From;
            public float Center => (From + To) / 2f;
        }

        public enum OpeningKind { Door, Window, Passage }

        /// <summary>
        /// Opening in een muur. Voor een deur is dit de enige bron: de muur wordt rond Segment gebouwd en de deur komt in het
        /// midden ervan, en draait open naar Into (plan). Locked: dicht met een collider en een LockedDoor.
        /// </summary>
        public readonly struct OpeningSpec
        {
            public readonly string Name;
            public readonly OpeningKind Kind;
            public readonly Segment Segment;
            public readonly float Bottom, Top;
            public readonly Vector2 Into;
            public readonly bool Locked;
            public OpeningSpec(string name, OpeningKind kind, Segment segment, float bottom, float top,
                Vector2 into = default, bool locked = false)
            {
                Name = name; Kind = kind; Segment = segment; Bottom = bottom; Top = top; Into = into; Locked = locked;
            }

            /// <summary>Midden van de opening op de middellijn van de muur (plan).</summary>
            public Vector2 Center => Segment.Vertical ? new Vector2(Segment.At, Segment.Center) : new Vector2(Segment.Center, Segment.At);
        }

        public readonly struct WallSpec
        {
            public readonly string Name, Parent;
            public readonly Segment Segment;
            public readonly float Thickness, Bottom, Top;
            public readonly string Material;
            public WallSpec(string name, string parent, Segment segment, float thickness, string material = "Wall",
                float bottom = 0f, float top = CeilingHeight)
            {
                Name = name; Parent = parent; Segment = segment; Thickness = thickness; Material = material; Bottom = bottom; Top = top;
            }
        }

        /// <summary>Kant van de kamer waar de gang ligt (en dus de deur).</summary>
        public enum CorridorSide { East, West, North }

        public sealed class PatientRoomSpec
        {
            public readonly string Id;         // bv. "1A"
            public readonly int Zone;          // 1, 2 of 3 (Hospital/FloorN)
            public readonly PlanRect Rect;
            public readonly float DoorFrom, DoorTo;   // langs de gangmuur
            public readonly int Beds;
            public readonly CorridorSide Side;
            public readonly bool Mirrored;     // badkamer aan de kant van de grootste x (2F)
            public PatientRoomSpec(string id, int zone, PlanRect rect, float doorFrom, float doorTo, int beds, CorridorSide side,
                bool mirrored = false)
            {
                Id = id; Zone = zone; Rect = rect; DoorFrom = doorFrom; DoorTo = doorTo; Beds = beds; Side = side; Mirrored = mirrored;
            }

            /// <summary>Richting van de gang de kamer in (plan).</summary>
            public Vector2 Into => Side switch
            {
                CorridorSide.East => Vector2.left,
                CorridorSide.West => Vector2.right,
                _ => Vector2.up,
            };

            /// <summary>Richting langs de gangmuur, weg van de hoek met de badkamer (plan).</summary>
            public Vector2 Along => Side == CorridorSide.North ? (Mirrored ? Vector2.left : Vector2.right) : Vector2.up;

            /// <summary>Binnenkant van de gangmuur, aan de kamerkant.</summary>
            private float CorridorFace => Side switch
            {
                CorridorSide.East => Rect.X1 - InnerWall / 2f,
                CorridorSide.West => Rect.X0 + InnerWall / 2f,
                _ => Rect.Y0 + InnerWall / 2f,
            };

            /// <summary>Binnenkant van de muur aan het begin van de kamer (waar de badkamer staat).</summary>
            private float StartFace
            {
                get
                {
                    float Face(float line, float outer, float sign) =>
                        line + sign * (Mathf.Approximately(line, outer) ? UWardLayout.OuterWall : InnerWall / 2f);
                    if (Side != CorridorSide.North)
                        return Face(Rect.Y0, 0f, 1f);
                    return Mirrored ? Face(Rect.X1, Width, -1f) : Face(Rect.X0, 0f, 1f);
                }
            }

            /// <summary>
            /// Plancoördinaat van een punt in de kamer: u langs de gangmuur vanaf het begin van de kamer, v vanaf de gangmuur de kamer in.
            /// </summary>
            public Vector2 At(float u, float v)
            {
                var corner = Side == CorridorSide.North ? new Vector2(StartFace, CorridorFace) : new Vector2(CorridorFace, StartFace);
                return corner + u * Along + v * Into;
            }

            /// <summary>Oorsprong van Room_*: midden van de kamer op de binnenkant van de gangmuur.</summary>
            public Vector2 Origin => Side == CorridorSide.North
                ? new Vector2(Rect.CenterX, CorridorFace)
                : new Vector2(CorridorFace, Rect.CenterY);

            /// <summary>Midden van de deur op de middellijn van de gangmuur.</summary>
            public Vector2 DoorCenter => Side switch
            {
                CorridorSide.East => new Vector2(Rect.X1, (DoorFrom + DoorTo) / 2f),
                CorridorSide.West => new Vector2(Rect.X0, (DoorFrom + DoorTo) / 2f),
                _ => new Vector2((DoorFrom + DoorTo) / 2f, Rect.Y0),
            };

            /// <summary>Lijnstuk van de deur in de gangmuur.</summary>
            public Segment Door => Side switch
            {
                CorridorSide.East => Segment.V(Rect.X1, DoorFrom, DoorTo),
                CorridorSide.West => Segment.V(Rect.X0, DoorFrom, DoorTo),
                _ => Segment.H(Rect.Y0, DoorFrom, DoorTo),
            };

            /// <summary>Lijnstuk van de buitenmuur tegenover de deur (daar komen de ramen en de hoofdeinden).</summary>
            public Segment OuterWall => Side switch
            {
                CorridorSide.East => Segment.V(Rect.X0, Rect.Y0, Rect.Y1),
                CorridorSide.West => Segment.V(Rect.X1, Rect.Y0, Rect.Y1),
                _ => Segment.H(Rect.Y1, Rect.X0, Rect.X1),
            };
        }

        public readonly struct CoreRoomSpec
        {
            public readonly string Name, Parent;
            public readonly PlanRect Rect;
            public CoreRoomSpec(string name, string parent, PlanRect rect) { Name = name; Parent = parent; Rect = rect; }
        }

        // --- Gangen -------------------------------------------------------------------
        public const float CorridorWidth = 2.4f;
        public static readonly PlanRect CorridorLeft = new PlanRect(6.0f, 0f, 8.4f, 22.0f);
        public static readonly PlanRect CorridorRight = new PlanRect(33.6f, 0f, 36.0f, 22.0f);
        public static readonly PlanRect CorridorBottom = new PlanRect(6.0f, 19.6f, 36.0f, 22.0f);

        // --- Patiëntkamers ------------------------------------------------------------
        public static readonly PatientRoomSpec[] PatientRooms =
        {
            // Floor1, linkerarm, deur in de muur x = 6
            new PatientRoomSpec("1C", 1, new PlanRect(0f, 0.00f, 6f, 7.20f), 2.60f, 3.90f, 2, CorridorSide.East),
            new PatientRoomSpec("1A", 1, new PlanRect(0f, 7.20f, 6f, 12.13f), 10.58f, 11.88f, 1, CorridorSide.East),
            new PatientRoomSpec("1B", 1, new PlanRect(0f, 12.13f, 6f, 17.07f), 15.52f, 16.82f, 1, CorridorSide.East),
            new PatientRoomSpec("1D", 1, new PlanRect(0f, 17.07f, 6f, 22.00f), 20.45f, 21.75f, 1, CorridorSide.East),
            // Floor3, rechterarm, deur in de muur x = 36
            new PatientRoomSpec("3C", 3, new PlanRect(36f, 0.00f, 42f, 7.20f), 2.60f, 3.90f, 2, CorridorSide.West),
            new PatientRoomSpec("3A", 3, new PlanRect(36f, 7.20f, 42f, 12.13f), 10.58f, 11.88f, 1, CorridorSide.West),
            new PatientRoomSpec("3B", 3, new PlanRect(36f, 12.13f, 42f, 17.07f), 15.52f, 16.82f, 1, CorridorSide.West),
            new PatientRoomSpec("3D", 3, new PlanRect(36f, 17.07f, 42f, 22.00f), 20.45f, 21.75f, 1, CorridorSide.West),
            // Floor2, onderste rij, deur in de muur y = 22
            new PatientRoomSpec("2E", 2, new PlanRect(0.0f, 22f, 8.4f, 28f), 6.55f, 7.85f, 3, CorridorSide.North),
            new PatientRoomSpec("2A", 2, new PlanRect(8.4f, 22f, 12.9f, 28f), 11.35f, 12.65f, 1, CorridorSide.North),
            new PatientRoomSpec("2B", 2, new PlanRect(12.9f, 22f, 17.4f, 28f), 15.85f, 17.15f, 1, CorridorSide.North),
            new PatientRoomSpec("2G", 2, new PlanRect(17.4f, 22f, 24.6f, 28f), 20.00f, 21.30f, 2, CorridorSide.North),
            new PatientRoomSpec("2C", 2, new PlanRect(24.6f, 22f, 29.1f, 28f), 27.55f, 28.85f, 1, CorridorSide.North),
            new PatientRoomSpec("2D", 2, new PlanRect(29.1f, 22f, 33.6f, 28f), 32.05f, 33.35f, 1, CorridorSide.North),
            new PatientRoomSpec("2F", 2, new PlanRect(33.6f, 22f, 42.0f, 28f), 34.15f, 35.45f, 3, CorridorSide.North, mirrored: true),
        };

        /// <summary>Van de binnenkant van de gangmuur tot de binnenkant van de buitenmuur (alle kamers zijn 6 m diep).</summary>
        public const float RoomInteriorDepth = 6f - InnerWall / 2f - OuterWall;

        // --- Badkamer (in de hoek aan het begin van de kamer, tegen de gangmuur) --------
        public const float BathroomLength = 1.95f;    // binnenmaat langs de gangmuur
        public const float BathroomDepth = 2.2f;      // binnenmaat de kamer in
        public const float BathroomWall = 0.1f;
        public const float BathroomDoorWidth = 0.9f;
        public const float BathroomDoorInset = 0.1f;  // van de zijmuur tot de deur

        /// <summary>Plan-lijnstuk tussen twee punten op dezelfde x of dezelfde y.</summary>
        public static Segment Between(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) < 0.001f
            ? Segment.V(a.x, Mathf.Min(a.y, b.y), Mathf.Max(a.y, b.y))
            : Segment.H(a.y, Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x));

        /// <summary>Voormuur (evenwijdig aan de gang, met de deur) en zijmuur van de badkamer.</summary>
        public static IEnumerable<WallSpec> BathroomWalls(PatientRoomSpec room)
        {
            const float t = BathroomWall;
            yield return new WallSpec("Wall_Bath_Front", null,
                Between(room.At(0f, BathroomDepth + t / 2f), room.At(BathroomLength + t, BathroomDepth + t / 2f)), t, "RoomWall");
            yield return new WallSpec("Wall_Bath_Side", null,
                Between(room.At(BathroomLength + t / 2f, 0f), room.At(BathroomLength + t / 2f, BathroomDepth)), t, "RoomWall");
        }

        public static OpeningSpec BathroomDoor(PatientRoomSpec room)
        {
            var v = BathroomDepth + BathroomWall / 2f;
            var to = BathroomLength - BathroomDoorInset;
            return new OpeningSpec($"BathDoor_{room.Id}", OpeningKind.Door,
                Between(room.At(to - BathroomDoorWidth, v), room.At(to, v)), 0f, DoorHeight);
        }

        // --- Kern ---------------------------------------------------------------------
        public static readonly PlanRect Core = new PlanRect(8.4f, 0f, 33.6f, 19.6f);
        public const float ToiletSplitY = 4.6f;
        // Versie 8: de oude briefingruimte (y 8,0..19,6) is gesplitst in de gesprekskamer (boven) en de briefingruimte (onder)
        public const float FamilyRoomSplitY = 12.6f;
        public static readonly CoreRoomSpec[] CoreRooms =
        {
            new CoreRoomSpec("Stairwell", "Core", new PlanRect(8.4f, 0f, 17.0f, 7.0f)),
            new CoreRoomSpec("RestRoom", "Core", new PlanRect(8.4f, 7.0f, 17.0f, 13.3f)),
            new CoreRoomSpec("MedicationRoom", "Core", new PlanRect(8.4f, 13.3f, 17.0f, 19.6f)),
            new CoreRoomSpec("Hall", "Lobby", new PlanRect(17.0f, 0f, 25.0f, 19.6f)),
            // Afwijking van maten.md (scheiding op y 4,0): op y 4,6, zodat de herendeur vrij van de voormuur van de liften ligt
            new CoreRoomSpec("Toilet_Men", "Core", new PlanRect(25.0f, 0f, 33.6f, ToiletSplitY)),
            new CoreRoomSpec("Toilet_Women", "Core", new PlanRect(25.0f, ToiletSplitY, 33.6f, 8.0f)),
            new CoreRoomSpec("FamilyRoom", "Core", new PlanRect(25.0f, 8.0f, 33.6f, FamilyRoomSplitY)),
            new CoreRoomSpec("BriefingRoom", "Core", new PlanRect(25.0f, FamilyRoomSplitY, 33.6f, 19.6f)),
        };

        // Hal: liften (fase 4), balie (fase 3)
        public static readonly PlanRect[] Lifts = { new PlanRect(17.6f, 0.25f, 20.8f, 2.85f), new PlanRect(21.1f, 0.25f, 24.3f, 2.85f) };
        public const float LiftWallThickness = 0.2f;
        public const float LiftWallY = 2.85f + LiftWallThickness / 2f;   // voormuur van de liften, op de rand van de schachten
        public const float LiftOpeningWidth = 1.1f;
        public const float LiftOpeningHeight = 2.2f;
        public static readonly PlanRect ReceptionDesk = new PlanRect(18.5f, 15.6f, 23.5f, 18.0f);

        // --- Muren --------------------------------------------------------------------

        /// <summary>Alle muren. Buitenmuren liggen binnen de omtrek; binnenmuren liggen op hun lijn uit maten.md.</summary>
        public static IEnumerable<WallSpec> Walls()
        {
            const float o = OuterWall / 2f;
            yield return new WallSpec("Wall_Outer_North", "Structure", Segment.H(o, 0f, Width), OuterWall);
            yield return new WallSpec("Wall_Outer_South", "Structure", Segment.H(Depth - o, 0f, Width), OuterWall);
            yield return new WallSpec("Wall_Outer_West", "Structure", Segment.V(o, 0f, Depth), OuterWall);
            yield return new WallSpec("Wall_Outer_East", "Structure", Segment.V(Width - o, 0f, Depth), OuterWall);

            // Gangmuren van de kamers (met de kamerdeuren) en de scheidingsmuren tussen de kamers
            yield return new WallSpec("Wall_Corridor", "Floor1", Segment.V(6f, 0f, 22f), InnerWall, "RoomWall");
            yield return new WallSpec("Wall_Corridor", "Floor3", Segment.V(36f, 0f, 22f), InnerWall, "RoomWall");
            yield return new WallSpec("Wall_Corridor", "Floor2", Segment.H(22f, 0f, Width), InnerWall, "RoomWall");
            foreach (var room in PatientRooms)
            {
                var parent = $"Floor{room.Zone}";
                if (room.Side == CorridorSide.North)
                {
                    if (room.Rect.X1 < Width - 0.01f)
                        yield return new WallSpec($"Wall_{room.Id}_East", parent, Segment.V(room.Rect.X1, 22f, Depth), InnerWall, "RoomWall");
                }
                else if (room.Rect.Y1 < 22f - 0.01f)
                    yield return new WallSpec($"Wall_{room.Id}_South", parent,
                        Segment.H(room.Rect.Y1, room.Rect.X0, room.Rect.X1), InnerWall, "RoomWall");
            }

            // Kern
            yield return new WallSpec("Wall_West", "Core", Segment.V(8.4f, 0f, 19.6f), InnerWall);
            yield return new WallSpec("Wall_East", "Core", Segment.V(33.6f, 0f, 19.6f), InnerWall);
            // De zuidmuur van de kern in drie stukken: het stuk van de hal (met de doorgang) staat achter de balie en heet
            // Lobby/Wall_North, zoals de muur achter de balie in liam_test_scene (Jalil zoekt hem op).
            yield return new WallSpec("Wall_South_West", "Core", Segment.H(19.6f, 8.4f, 17.0f), InnerWall);
            yield return new WallSpec("Wall_North", "Lobby", Segment.H(19.6f, 17.0f, 25.0f), InnerWall);
            yield return new WallSpec("Wall_South_East", "Core", Segment.H(19.6f, 25.0f, 33.6f), InnerWall);
            yield return new WallSpec("Wall_Hall_West", "Core", Segment.V(17.0f, 0f, 19.6f), InnerWall);
            yield return new WallSpec("Wall_Hall_East", "Core", Segment.V(25.0f, 0f, 19.6f), InnerWall);
            yield return new WallSpec("Wall_Stairwell_South", "Core", Segment.H(7.0f, 8.4f, 17.0f), InnerWall);

            // Voormuur van de liften over de hele breedte van de hal, en een scheidingsmuur tussen de twee schachten
            yield return new WallSpec("Wall_Lifts", "Lobby",
                Segment.H(LiftWallY, 17.0f + InnerWall / 2f, 25.0f - InnerWall / 2f), LiftWallThickness);
            yield return new WallSpec("Wall_LiftDivider", "Lobby",
                Segment.V((Lifts[0].X1 + Lifts[1].X0) / 2f, OuterWall, Lifts[0].Y1), Lifts[1].X0 - Lifts[0].X1);
            yield return new WallSpec("Wall_RestRoom_South", "Core", Segment.H(13.3f, 8.4f, 17.0f), InnerWall);
            yield return new WallSpec("Wall_ToiletMen_South", "Core", Segment.H(ToiletSplitY, 25.0f, 33.6f), InnerWall);
            yield return new WallSpec("Wall_ToiletWomen_South", "Core", Segment.H(8.0f, 25.0f, 33.6f), InnerWall);
            yield return new WallSpec("Wall_FamilyRoom_South", "Core", Segment.H(FamilyRoomSplitY, 25.0f, 33.6f), InnerWall);
        }

        /// <summary>Accentstreep in de kleur van de zone, aan de gangkant van de kamermuren (onderbroken door de deuren).</summary>
        public static IEnumerable<(WallSpec Wall, int Zone)> Stripes()
        {
            const float inset = InnerWall / 2f + StripeDepth / 2f;
            const float bottom = StripeBottom, top = StripeBottom + StripeHeight;
            yield return (new WallSpec("Accent_Corridor", "Floor1", Segment.V(6f + inset, 0.25f, 22f - inset), StripeDepth, null, bottom, top), 1);
            yield return (new WallSpec("Accent_Corridor", "Floor3", Segment.V(36f - inset, 0.25f, 22f - inset), StripeDepth, null, bottom, top), 3);
            yield return (new WallSpec("Accent_Corridor", "Floor2", Segment.H(22f - inset, 6f, 36f), StripeDepth, null, bottom, top), 2);
        }

        // --- Openingen ----------------------------------------------------------------

        private static readonly Vector2 IntoWest = new Vector2(-1f, 0f);
        private static readonly Vector2 IntoEast = new Vector2(1f, 0f);
        private static readonly Vector2 IntoNorth = new Vector2(0f, -1f);

        /// <summary>
        /// Deuren van de kern (1,0 m, de trap 1,2 m, de toiletten 0,8 m), vrij gekozen waar maten.md enkel "deur naar de hal"
        /// zegt. Alle deuren draaien de ruimte in. De toiletdeuren zitten in een vlakke muur: de herendeur 45 cm onder de
        /// voormuur van de liften (die loopt tot y 3,05), de damesdeur op het midden uit Liams plan (y 7,05).
        /// Versie 8: de gesprekskamer krijgt de oude halldeur van de briefingruimte (y 9..10) en een deur naar de gang rechts;
        /// de halldeur van de briefingruimte schuift naar y 14..15.
        /// </summary>
        public static readonly OpeningSpec[] CoreDoors =
        {
            new OpeningSpec("Door_Stairwell", OpeningKind.Door, Segment.V(17.0f, 4.2f, 5.4f), 0f, DoorHeight, IntoWest, locked: true),
            new OpeningSpec("Door_RestRoom", OpeningKind.Door, Segment.V(17.0f, 9.6f, 10.6f), 0f, DoorHeight, IntoWest),
            new OpeningSpec("Door_MedicationRoom", OpeningKind.Door, Segment.V(17.0f, 14.0f, 15.0f), 0f, DoorHeight, IntoWest),
            new OpeningSpec("Door_Toilet_Men", OpeningKind.Door, Segment.V(25.0f, 3.5f, 4.3f), 0f, DoorHeight, IntoEast),
            new OpeningSpec("Door_Toilet_Women", OpeningKind.Door, Segment.V(25.0f, 6.65f, 7.45f), 0f, DoorHeight, IntoEast),
            new OpeningSpec("Door_FamilyRoom_Hall", OpeningKind.Door, Segment.V(25.0f, 9.0f, 10.0f), 0f, DoorHeight, IntoEast),
            new OpeningSpec("Door_FamilyRoom_Corridor", OpeningKind.Door, Segment.V(33.6f, 9.5f, 10.5f), 0f, DoorHeight, IntoWest),
            new OpeningSpec("Door_Briefing_Hall", OpeningKind.Door, Segment.V(25.0f, 14.0f, 15.0f), 0f, DoorHeight, IntoEast),
            new OpeningSpec("Door_Briefing_Corridor", OpeningKind.Door, Segment.H(19.6f, 30.5f, 31.5f), 0f, DoorHeight, IntoNorth),
        };

        /// <summary>Alle deuren (patiëntkamers en kern), met hun opening en draairichting.</summary>
        public static IEnumerable<OpeningSpec> Doors() =>
            PatientRooms.Select(r => new OpeningSpec($"Door_{r.Id}", OpeningKind.Door, r.Door, 0f, DoorHeight, r.Into)).Concat(CoreDoors);

        /// <summary>De 6 m brede doorgang tussen de hal en de gang onder, gecentreerd onder de hal.</summary>
        public static readonly OpeningSpec HallPassage =
            new OpeningSpec("Passage_Hall", OpeningKind.Passage, Segment.H(19.6f, 18.0f, 24.0f), 0f, PassageTop);

        public static IEnumerable<OpeningSpec> Openings()
        {
            foreach (var room in PatientRooms)
            {
                yield return BathroomDoor(room);
                // Eén raam per bed, gelijk verdeeld over de buitenmuur tegenover de deur
                var outer = room.OuterWall;
                for (var i = 0; i < room.Beds; i++)
                {
                    var c = outer.From + (i + 0.5f) * outer.Length / room.Beds;
                    yield return new OpeningSpec($"Window_{room.Id}_{i + 1}", OpeningKind.Window,
                        new Segment(outer.Vertical, outer.At, c - WindowWidth / 2f, c + WindowWidth / 2f), WindowSill, WindowTop);
                }
            }
            foreach (var door in Doors())
                yield return door;
            for (var i = 0; i < Lifts.Length; i++)
                yield return new OpeningSpec($"LiftOpening_{i + 1}", OpeningKind.Door,
                    Segment.H(LiftWallY, Lifts[i].CenterX - LiftOpeningWidth / 2f, Lifts[i].CenterX + LiftOpeningWidth / 2f), 0f, LiftOpeningHeight);
            yield return HallPassage;

            // Ramen aan de doodlopende gangeinden (bovenkant)
            yield return new OpeningSpec("Window_CorridorLeft", OpeningKind.Window,
                Segment.H(0f, CorridorLeft.CenterX - WindowWidth / 2f, CorridorLeft.CenterX + WindowWidth / 2f), WindowSill, WindowTop);
            yield return new OpeningSpec("Window_CorridorRight", OpeningKind.Window,
                Segment.H(0f, CorridorRight.CenterX - WindowWidth / 2f, CorridorRight.CenterX + WindowWidth / 2f), WindowSill, WindowTop);
        }

        /// <summary>Openingen die op deze muur liggen: zelfde richting, lijn binnen de muurdikte (min. 15 cm), binnen de lengte.</summary>
        public static IEnumerable<OpeningSpec> OpeningsOn(WallSpec wall)
        {
            var tolerance = Mathf.Max(wall.Thickness / 2f, 0.15f) + 0.01f;
            return Openings().Where(o => o.Segment.Vertical == wall.Segment.Vertical &&
                                         Mathf.Abs(o.Segment.At - wall.Segment.At) <= tolerance &&
                                         o.Segment.From >= wall.Segment.From - 0.01f && o.Segment.To <= wall.Segment.To + 0.01f);
        }

        // --- Licht --------------------------------------------------------------------
        public const float CorridorLightSpacing = 6f;

        /// <summary>
        /// Ganglichten per zone, om de 6 m vanaf 3 m van het begin van de gang. Ze heten Light_3, Light_9, ... zoals in
        /// liam_test_scene (afstand langs de gang), zodat Floor{n}/Light_3 blijft bestaan.
        /// </summary>
        public static IEnumerable<(int Zone, string Name, float X, float Y)> CorridorLights()
        {
            for (var d = CorridorLightSpacing / 2f; d < CorridorLeft.Y1 - CorridorWidth; d += CorridorLightSpacing)
            {
                yield return (1, $"{d:0}", CorridorLeft.CenterX, d);
                yield return (3, $"{d:0}", CorridorRight.CenterX, d);
            }
            for (var d = CorridorLightSpacing / 2f; d < CorridorBottom.X1 - CorridorBottom.X0; d += CorridorLightSpacing)
                yield return (2, $"{d:0}", CorridorBottom.X0 + d, CorridorBottom.CenterY);
        }

        public static readonly Vector2[] HallLights = { new Vector2(21f, 6.5f), new Vector2(21f, 13f) };
    }
}
