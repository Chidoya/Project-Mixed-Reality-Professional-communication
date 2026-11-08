using System.Linq;
using UnityEngine;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Plattegrond versie 8: de gesprekskamer (Core/FamilyRoom, x 25,0..33,6, y 8,0..12,6) voor scenario 2. Een rustige,
    /// warme familiekamer: zetel tegen de halmuur, twee fauteuils tegenover de zetel, een lage ronde tafel met tissues, een
    /// dressoir met een schilderij erboven, een plant en warm licht. De lijn van de gangdeur de kamer in (y 10) blijft vrij.
    /// Aan de gangkant naast de gangdeur hangt een bordje met een pictogram (twee pratende hoofden, geen tekst).
    /// </summary>
    public static partial class UWardBuilder
    {
        private const float FamilyTableHeight = 0.45f;
        private const float FamilyTableDiameter = 0.8f;
        private const float FamilySeatGap = 0.45f;   // vrije ruimte tussen de zitplaatsen en de tafel
        private const float FamilyRoomLightIntensity = 1.2f;

        private static void BuildFamilyRoom(Transform room)
        {
            var r = Inner(CoreRoom("FamilyRoom"));
            var furniture = Child(room, "Furniture");
            var hallDoor = CoreDoors.First(d => d.Name == "Door_FamilyRoom_Hall").Segment;

            // Zetel tegen de halmuur, ten zuiden van de halldeur (die draait tot y 10 de kamer in)
            var sofa = AgainstWall("loungeSofa", furniture, "Sofa", new Vector2(r.X0, (hallDoor.To + r.Y1) / 2f), PlanWest);
            var sofaWidth = Width1(sofa, PlanSouth);
            var sofaY = Mathf.Clamp((hallDoor.To + r.Y1) / 2f, hallDoor.To + 0.1f + sofaWidth / 2f, r.Y1 - 0.05f - sofaWidth / 2f);
            Object.DestroyImmediate(sofa);
            sofa = AgainstWall("loungeSofa", furniture, "Sofa", new Vector2(r.X0, sofaY), PlanWest);
            var sofaFront = PlanOfWorld(HospitalAssetPrep.WorldBounds(sofa).max).x;

            // Lage ronde tafel voor de zetel, met een doos tissues
            var table = PlaceKenney("tableRound", furniture, "CoffeeTable", PlanPoint(new Vector2(sofaFront + 1f, sofaY)), 0f);
            var tableSize = HospitalAssetPrep.WorldBounds(table).size;
            var s = table.transform.localScale;
            var across = FamilyTableDiameter / Mathf.Max(tableSize.x, tableSize.z);
            table.transform.localScale = new Vector3(s.x * across, s.y * FamilyTableHeight / tableSize.y, s.z * across);
            tableSize = HospitalAssetPrep.WorldBounds(table).size;
            var tableX = sofaFront + FamilySeatGap + tableSize.x / 2f;
            AlignToFloor(table, PlanPoint(new Vector2(tableX, sofaY)));
            var tableTop = TopAt(table, PlanPoint(new Vector2(tableX, sofaY)).x, PlanPoint(new Vector2(tableX, sofaY)).z);
            PlaceProp("Tissue_Box", furniture, PlanPoint(new Vector2(tableX, sofaY), float.IsNaN(tableTop) ? FamilyTableHeight : tableTop),
                PlanDirection(PlanWest));

            // Twee fauteuils tegenover de zetel, met de rug naar het oosten
            var probe = PlaceKenney("loungeChair", furniture, "Probe", Vector3.zero, 0f);
            var chairSize = HospitalAssetPrep.WorldBounds(probe).size;
            Object.DestroyImmediate(probe);
            var chairDepth = Mathf.Min(chairSize.x, chairSize.z);
            var chairWidth = Mathf.Max(chairSize.x, chairSize.z);
            var chairX = tableX + tableSize.x / 2f + FamilySeatGap + chairDepth / 2f;
            for (var i = 0; i < 2; i++)
            {
                var pos = PlanPoint(new Vector2(chairX, sofaY + (i == 0 ? -1f : 1f) * (chairWidth / 2f + 0.05f)));
                OrientBack(PlaceKenney("loungeChair", furniture, $"Armchair_{i + 1}", pos, 0f), PlanDirection(PlanEast), pos);
            }

            // Dressoir tegen de noordmuur met een schilderij erboven, plant in de hoek bij de briefingmuur
            var dresserX = (r.X0 + r.X1) / 2f + 0.4f;
            var dresser = AgainstWall("cabinetTelevisionDoors", furniture, "Dresser", new Vector2(dresserX, r.Y0), PlanNorth);
            BuildPainting(room, new Vector2(dresserX, r.Y0), HospitalAssetPrep.WorldBounds(dresser).max.y + 0.45f);
            AgainstWall("pottedPlant", furniture, "Plant", new Vector2(r.X1 - 0.35f, r.Y1), PlanSouth);

            var center = PlanPoint(new Vector2(r.CenterX, r.CenterY), CeilingHeight);
            AddCeilingLamp(room, "Lamp", center);
            AddLight("Light", room, center + Vector3.down * 0.4f, CoreLightRange, FamilyRoomLightIntensity);
            room.Find("Light").GetComponent<Light>().color = WarmLightColor;

            FamilyRoomSign(room);
        }

        /// <summary>Schilderij (landschap) van 1,0 x 0,7 m op de muur: lijst, lucht, veld en zon. Midden op height.</summary>
        private static void BuildPainting(Transform room, Vector2 wallPoint, float bottom)
        {
            const float w = 1.0f, h = 0.7f, frame = 0.05f, depth = 0.03f;
            var painting = Child(room, "Painting");
            var into = PlanDirection(PlanSouth);   // van de noordmuur de kamer in
            var basePoint = PlanPoint(wallPoint, bottom + h / 2f);
            painting.SetPositionAndRotation(basePoint, Quaternion.LookRotation(-into));
            // Lokale -z wijst de kamer in
            Box("Frame", painting, new Vector3(0f, 0f, -depth / 2f), new Vector3(w, h, depth), Mat("PaintingFrame", new Color(0.36f, 0.24f, 0.16f)));
            var iw = w - 2f * frame;
            var ih = h - 2f * frame;
            var z = -depth - 0.002f;
            Box("Sky", painting, new Vector3(0f, ih / 4f, z), new Vector3(iw, ih / 2f, 0.004f), Mat("PaintingSky", new Color(0.62f, 0.78f, 0.90f)));
            Box("Field", painting, new Vector3(0f, -ih / 4f, z), new Vector3(iw, ih / 2f, 0.004f), Mat("PaintingField", new Color(0.55f, 0.68f, 0.38f)));
            Box("Sun", painting, new Vector3(iw * 0.25f, ih * 0.3f, z - 0.003f), new Vector3(0.09f, 0.09f, 0.004f), Mat("PaintingSun", new Color(0.98f, 0.85f, 0.45f)));
            foreach (var c in painting.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
        }

        /// <summary>
        /// Bord aan de gangkant van Core/Wall_East, ten zuiden van Door_FamilyRoom_Corridor (de kant waar de speler vandaan komt):
        /// blauw zoals de toiletborden, met een wit pictogram van twee pratende hoofden en geen tekst.
        /// </summary>
        private static void FamilyRoomSign(Transform room)
        {
            const float w = 0.3f, h = 0.3f, depth = 0.02f, height = 1.6f, gap = 0.45f;
            var door = CoreDoors.First(d => d.Name == "Door_FamilyRoom_Corridor").Segment;
            var sign = Child(room, "Sign_FamilyRoom");
            var face = PlanPoint(new Vector2(door.At + InnerWall / 2f, door.To + gap + w / 2f), height);
            sign.SetPositionAndRotation(face + Vector3.right * 0.002f, Quaternion.LookRotation(Vector3.left));   // lokale -z = de gang in
            Box("Panel", sign, new Vector3(0f, 0f, -depth / 2f), new Vector3(w, h, depth), Mat("SignToilet", new Color(0.20f, 0.40f, 0.65f)));

            var icon = Mat("SignIcon", Color.white);
            var z = -depth - 0.004f;
            void Part(string part, float x, float y, float pw, float ph) => Box(part, sign, new Vector3(x, y, z), new Vector3(pw, ph, 0.006f), icon);
            // Twee bustes in profiel, naar elkaar toe: hoofd met neus, hals, brede schouders en een smallere romp
            foreach (var (side, x, toward) in new[] { ("L", -0.075f, 1f), ("R", 0.075f, -1f) })
            {
                Part($"Head_{side}", x, 0.0f, 0.05f, 0.055f);
                Part($"Nose_{side}", x + toward * 0.03f, 0.0f, 0.012f, 0.012f);
                Part($"Neck_{side}", x, -0.035f, 0.02f, 0.015f);
                Part($"Shoulders_{side}", x, -0.06f, 0.1f, 0.035f);
                Part($"Torso_{side}", x, -0.1f, 0.075f, 0.05f);
            }
            // Twee spraakwolkjes boven de hoofden, met een staartje naar de mond
            Part("Bubble_L", -0.04f, 0.075f, 0.07f, 0.035f);
            Part("Bubble_L_Tail", -0.055f, 0.051f, 0.012f, 0.014f);
            Part("Bubble_R", 0.04f, 0.115f, 0.07f, 0.035f);
            Part("Bubble_R_Tail", 0.055f, 0.091f, 0.012f, 0.014f);
            foreach (var c in sign.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
        }

        private static Vector2 PlanOfWorld(Vector3 p) => new Vector2(p.x + Width / 2f, Depth / 2f - p.z);

        private static void TakeFamilyRoomScreenshots()
        {
            var door = CoreDoors.First(d => d.Name == "Door_FamilyRoom_Corridor");
            // Vanuit de gangdeur de kamer in, en vanuit de gang de deur met het bord
            Shot("u_afdeling_familyroom_from_corridor_door.png", PlanPoint(door.Center + new Vector2(-0.3f, 0f), EyeHeight),
                PlanPoint(new Vector2(26.5f, 11.0f), 0.8f));
            Shot("u_afdeling_familyroom_door_corridor.png", PlanPoint(new Vector2(35.5f, 13.6f), EyeHeight),
                PlanPoint(door.Center + new Vector2(0f, 0.6f), 1.3f));
            var sign = new Vector2(door.Segment.At + InnerWall / 2f, door.Segment.To + 0.6f);
            Shot("u_afdeling_familyroom_sign.png", PlanPoint(sign + new Vector2(0.9f, 0.3f), 1.6f), PlanPoint(sign, 1.6f));
            var hall = CoreDoors.First(d => d.Name == "Door_FamilyRoom_Hall");
            Shot("u_afdeling_familyroom_from_hall_door.png", PlanPoint(hall.Center + new Vector2(0.3f, 0f), EyeHeight),
                PlanPoint(new Vector2(32.5f, 10.5f), 0.8f));
        }
    }
}
