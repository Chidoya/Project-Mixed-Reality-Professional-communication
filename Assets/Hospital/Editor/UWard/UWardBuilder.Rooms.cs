using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProCommVR.Hospital;
using TMPro;
using UnityEditor;
using UnityEngine;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Fase 2: de 15 patiëntkamers. De inrichting (bedden, nachtkastjes, stoelen, gordijnen, lamp, ankers) zit in drie prefabs in
    /// Assets/Hospital/Prefabs/UWard (PatientRoom.prefab van liam_test_scene blijft ongewijzigd). Badkamer, deur en kaart
    /// verschillen per kamer en worden in de scene gebouwd. Namen zoals in liam_test_scene: FloorN/Room_1A met Anchor_Laying,
    /// Anchor_Sitting en Furniture/Chair, FloorN/Door_1A en FloorN/Card_1A.
    /// </summary>
    public static partial class UWardBuilder
    {
        private const string RoomPrefabFolder = "Assets/Hospital/Prefabs/UWard";
        private const string CaseFolder = "Assets/Hospital/Data";
        private const string DoorModelPath = "Assets/ThirdParty/Sketchfab/HospitalDoor_anasrar/source/untitled.fbx";
        private const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // Inrichting, zoals de kamer in liam_test_scene
        private const float BedWallGap = 0.05f;
        private const float ChairSideOffset = 0.95f;
        private const float ChairFromBackWall = 1.1f;
        private const float MattressHeight = 0.5f;
        private const float ChairSeatHeight = 0.45f;
        private const float HeightAdjustMin = 0.7f;
        private const float HeightAdjustMax = 1.4f;
        private const float RoomLightIntensity = 1.4f;

        // Privacygordijn tussen de bedden, aan een rail onder het plafond
        private const float CurtainLength = 2.3f;
        private const float CurtainBottom = 0.35f;
        private const float CurtainTop = 2.45f;
        private const float CurtainRailHeight = 2.5f;

        // Patiëntkaart naast de deur, zoals in liam_test_scene
        private const float CardWidth = 0.6f;
        private const float CardHeight = 0.8f;
        private const float CardHeaderHeight = 0.14f;
        private const float CardCenterY = 1.5f;
        private const float CardGapFromDoor = 0.12f;
        private const float CardDepth = 0.02f;
        private const float CardPadding = 0.04f;
        private static readonly Color CardTextColor = new Color(0.15f, 0.16f, 0.18f);
        private static readonly Color CardComingSoonColor = new Color(0.55f, 0.56f, 0.58f);

        // Badkamer: vrije maat voor douche, toilet en lavabo
        private const float ShowerSize = 0.9f;

        // Het kozijn steekt aan elke kant 2 cm uit de muur
        private const float DoorFrameOverhang = 0.04f;

        private const float ScreenshotFov = 75f;
        private const float EyeHeight = 1.65f;

        /// <summary>Soort kamer: prefab, x van de bedden (gecentreerd op de ramen) en bereik van het licht.</summary>
        private sealed class RoomKind
        {
            public string Prefab;
            public float[] Bays;
            public float LightRange;
        }

        private static readonly RoomKind SingleRoom = new RoomKind { Prefab = "PatientRoom_Single", Bays = new[] { 0f }, LightRange = 5.5f };
        private static readonly RoomKind SharedRoom = new RoomKind { Prefab = "PatientRoom_Shared", Bays = new[] { -1.8f, 1.8f }, LightRange = 7f };
        private static readonly RoomKind CornerRoom = new RoomKind { Prefab = "PatientRoom_Shared3", Bays = new[] { -2.8f, 0f, 2.8f }, LightRange = 7.5f };

        private static TMP_FontAsset _font;
        private static readonly HashSet<string> Logged = new HashSet<string>();

        private static void BuildRooms(Dictionary<string, Transform> groups)
        {
            Logged.Clear();
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (_font == null)
                Debug.LogWarning($"{LogPrefix}Geen TextMeshPro-lettertype op {TmpFontPath}: kaarten zonder tekst.");
            var cases = LoadCases();
            var prefabs = new[] { SingleRoom, SharedRoom, CornerRoom }.ToDictionary(k => k, BuildRoomPrefab);

            foreach (var spec in PatientRooms)
            {
                var floor = groups[$"Floor{spec.Zone}"];
                var kind = spec.Beds switch { 1 => SingleRoom, 2 => SharedRoom, _ => CornerRoom };
                var rotation = Quaternion.LookRotation(PlanDirection(spec.Into));

                var room = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind], floor);
                room.name = $"Room_{spec.Id}";
                room.transform.SetPositionAndRotation(ToUnity(spec.Origin.x, spec.Origin.y), rotation);
                BuildBathroom(spec, room.transform);

                PlaceDoor(floor, $"Door_{spec.Id}", spec.DoorCenter, spec.Into, PatientDoorWidth);
                cases.TryGetValue(spec.Id, out var patientCase);
                PlaceCard(floor, spec, rotation, patientCase);
            }
            Debug.Log($"{LogPrefix}{PatientRooms.Length} kamers.");
        }

        /// <summary>Richting in het plan naar Unity (plan-y loopt naar -Z).</summary>
        private static Vector3 PlanDirection(Vector2 d) => new Vector3(d.x, 0f, -d.y);

        private static Vector3 PlanPoint(Vector2 p, float height = 0f) => ToUnity(p.x, p.y, height);

        // --- Prefabs --------------------------------------------------------------------

        /// <summary>
        /// Inrichting rond de oorsprong: binnenkant van de gangmuur op z = 0, kamer naar +z, buitenmuur op z = RoomInteriorDepth.
        /// Per bed: hoofdeinde tegen de buitenmuur, nachtkastje links, stoel rechts met de rug naar +x. Het eerste bed heet
        /// Bed / Chair / Anchor_Laying / Anchor_Sitting zoals in liam_test_scene, de volgende krijgen _2, _3.
        /// </summary>
        private static GameObject BuildRoomPrefab(RoomKind kind)
        {
            EnsureFolder(RoomPrefabFolder);
            var room = new GameObject(kind.Prefab);
            var furniture = new GameObject("Furniture").transform;
            furniture.SetParent(room.transform, false);
            const float back = RoomInteriorDepth;

            for (var i = 0; i < kind.Bays.Length; i++)
            {
                var x = kind.Bays[i];
                var suffix = i == 0 ? "" : $"_{i + 1}";

                var bed = PlaceKenney("bedSingle", furniture, $"Bed{suffix}", Vector3.zero, 0f);
                var bedLength = HospitalAssetPrep.WorldBounds(bed).size.z;
                var bedCenter = new Vector3(x, 0f, back - BedWallGap - bedLength / 2f);
                AlignToFloor(bed, bedCenter);
                OrientHeadboard(bed, bedCenter);
                var mattress = AdjustHeight(bed, bedCenter, MattressHeight, "matras");

                var bedHalfWidth = HospitalAssetPrep.WorldBounds(bed).extents.x;
                // Nachtkastje links van het hoofdeinde, met de lade dezelfde kant op als het voeteneinde van het bed (de kamer in)
                var stand = PlaceKenney("cabinetBed", furniture, $"Nightstand{suffix}", Vector3.zero, 0f);
                FaceWorld(stand, FrontOf(bed));
                var standSize = HospitalAssetPrep.WorldBounds(stand).size;
                AlignToFloor(stand, new Vector3(x - bedHalfWidth - 0.1f - standSize.x / 2f, 0f, back - BedWallGap - standSize.z / 2f));

                var chairPos = new Vector3(x + ChairSideOffset, 0f, back - ChairFromBackWall);
                var chair = PlaceKenney("chair", furniture, $"Chair{suffix}", chairPos, 0f);
                OrientBack(chair, Vector3.right, chairPos);
                var seat = AdjustHeight(chair, chairPos, ChairSeatHeight, "zitvlak");

                Anchor($"Anchor_Laying{suffix}", room.transform, new Vector3(bedCenter.x, mattress, bedCenter.z), Quaternion.identity);
                Anchor($"Anchor_Sitting{suffix}", room.transform, new Vector3(chairPos.x, seat, chairPos.z), Quaternion.LookRotation(Vector3.left));
            }

            // Gordijn halverwege tussen twee bedden
            var curtain = Mat("Curtain", new Color(0.62f, 0.78f, 0.80f));
            var rail = Mat("WindowFrame", new Color(0.97f, 0.97f, 0.97f));
            for (var i = 1; i < kind.Bays.Length; i++)
            {
                var x = (kind.Bays[i - 1] + kind.Bays[i]) / 2f;
                var z = back - CurtainLength / 2f;
                var suffix = i == 1 ? "" : $"_{i}";
                Box($"Curtain{suffix}", furniture, new Vector3(x, (CurtainBottom + CurtainTop) / 2f, z),
                    new Vector3(0.02f, CurtainTop - CurtainBottom, CurtainLength), curtain);
                Box($"CurtainRail{suffix}", furniture, new Vector3(x, CurtainRailHeight, z), new Vector3(0.03f, 0.03f, CurtainLength + 0.1f), rail);
            }

            var center = new Vector3(0f, CeilingHeight, back / 2f);
            AddCeilingLamp(room.transform, "Lamp", center);
            AddLight("Light", room.transform, center + Vector3.down * 0.4f, kind.LightRange, RoomLightIntensity);

            MarkStatic(room.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(room, $"{RoomPrefabFolder}/{kind.Prefab}.prefab");
            Object.DestroyImmediate(room);
            return prefab;
        }

        // --- Badkamer ---------------------------------------------------------------------

        /// <summary>
        /// Twee muren met een deuropening (geen deurblad), douche in de hoek, toilet tegen de gangmuur, lavabo tegen de muur aan
        /// het begin van de kamer. Gebouwd in wereldcoördinaten en daarna onder Room_* gehangen.
        /// </summary>
        private static void BuildBathroom(PatientRoomSpec spec, Transform room)
        {
            var bath = new GameObject("Bathroom").transform;
            foreach (var wall in BathroomWalls(spec))
                Wall(bath, wall, RoomWallMat);

            var toCorridor = PlanDirection(-spec.Into);
            var toStart = PlanDirection(-spec.Along);
            Fixture("shower", bath, "Shower", PlanPoint(spec.At(ShowerSize / 2f, ShowerSize / 2f)), toCorridor, ShowerSize);
            Fixture("toilet", bath, "Toilet", PlanPoint(spec.At(BathroomLength - 0.45f, 0.35f)), toCorridor, 0.7f);
            Fixture("bathroomSink", bath, "Sink", PlanPoint(spec.At(0.3f, BathroomDepth - 0.6f)), toStart, 0.6f);

            bath.SetParent(room, true);
        }

        /// <summary>Wereldrichting van de voorkant van een Kenney-model (volgens UWardLayout.FrontYaw); bij het bed het voeteneinde.</summary>
        private static Vector3 FrontOf(GameObject go)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            return Vector3.ProjectOnPlane(go.transform.rotation * Quaternion.Euler(0f, FrontYaw(source != null ? source.name : null), 0f) * Vector3.forward,
                Vector3.up).normalized;
        }

        /// <summary>Kenney-model met de rug tegen de muur (voorkant volgens FrontYaw), verkleind als het groter is dan maxFootprint.</summary>
        private static void Fixture(string model, Transform parent, string name, Vector3 center, Vector3 back, float maxFootprint)
        {
            var go = PlaceKenney(model, parent, name, parent.InverseTransformPoint(center), 0f);
            FaceWorld(go, -back);
            AlignToFloor(go, center);
            var size = HospitalAssetPrep.WorldBounds(go).size;
            var factor = maxFootprint / Mathf.Max(size.x, size.z);
            if (factor < 1f)
            {
                go.transform.localScale *= factor;
                AlignToFloor(go, center);
            }
            // Tegen de muur schuiven
            var b = HospitalAssetPrep.WorldBounds(go);
            var reach = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(back.x), 0f, Mathf.Abs(back.z))));
            var target = center + back * (maxFootprint / 2f - reach);
            AlignToFloor(go, target);
            if (Logged.Add(name))
                Debug.Log($"{LogPrefix}{name}: {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} m{(factor < 1f ? $" (schaal {factor:0.00})" : "")}.");
        }

        // --- Deur en kaart ------------------------------------------------------------------

        /// <summary>
        /// Hospital Door in een deuropening, dicht. Het model (één mesh met kozijn en deurblad, scharnier aan zijn oorsprong) wordt
        /// geschaald naar de opening (breedte x 2,1 m) en in de diepte naar de muur, zodat het kozijn gelijk ligt met de muur.
        /// De houder draait met z de ruimte in, zodat Jalils OpenDoor hem naar binnen opendraait (scharnier aan de kant van -x).
        /// Niet static.
        /// </summary>
        private static Transform PlaceDoor(Transform parent, string name, Vector2 center, Vector2 into, float width, float wallThickness = InnerWall)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(DoorModelPath);
            if (asset == null)
                throw new FileNotFoundException("Hospital Door niet gevonden", DoorModelPath);
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(PlanPoint(center), Quaternion.LookRotation(PlanDirection(into)));

            // De root van het model houdt zijn rotatie uit de FBX-conversie; enkel de houder en "Fit" draaien of schalen
            var fit = new GameObject("Fit").transform;
            fit.SetParent(holder, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, fit);
            model.name = "Model";
            var size = holder.InverseTransformVector(HospitalAssetPrep.WorldBounds(model).size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            if (size.x < size.z)
                Debug.LogWarning($"{LogPrefix}{name}: deurmodel ligt dwars in de opening ({size.x:0.00} x {size.z:0.00} m).");
            fit.localScale = new Vector3(width / size.x, DoorHeight / size.y, (wallThickness + DoorFrameOverhang) / size.z);
            AlignToFloor(model, holder.position);
            if (Logged.Add("Door"))
                Debug.Log($"{LogPrefix}Deurmodel {size.x:0.00} x {size.y:0.00} x {size.z:0.00} m, geschaald naar de opening en de muurdikte.");
            return holder;
        }

        /// <summary>
        /// Kaart op de gangkant van de muur naast de deur, aan de kant van het midden van de kamer, tenzij hij daar buiten de gang
        /// zou vallen (hoekkamers 2E en 2F). Inhoud zoals in liam_test_scene: kop in de zonekleur, naam, leeftijd, situatie, "Watch out".
        /// </summary>
        private static void PlaceCard(Transform floor, PatientRoomSpec spec, Quaternion rotation, PatientCase patientCase)
        {
            var north = spec.Side == CorridorSide.North;
            var door = (spec.DoorFrom + spec.DoorTo) / 2f;
            var roomCenter = north ? spec.Rect.CenterX : spec.Rect.CenterY;
            var corridor = north ? CorridorBottom : spec.Side == CorridorSide.East ? CorridorLeft : CorridorRight;
            var min = north ? corridor.X0 + InnerWall / 2f : corridor.Y0;
            var max = north ? corridor.X1 - InnerWall / 2f : corridor.Y1;
            var offset = PatientDoorWidth / 2f + CardGapFromDoor + CardWidth / 2f;
            var along = door + Mathf.Sign(roomCenter - door) * offset;
            if (along - CardWidth / 2f < min || along + CardWidth / 2f > max)
                along = door - Mathf.Sign(roomCenter - door) * offset;

            var wall = north ? spec.Rect.Y0 : spec.Side == CorridorSide.East ? spec.Rect.X1 : spec.Rect.X0;
            var plan = north ? new Vector2(along, wall) : new Vector2(wall, along);
            var card = new GameObject($"Card_{spec.Id}").transform;
            card.SetParent(floor, false);
            card.SetPositionAndRotation(PlanPoint(plan, CardCenterY) - PlanDirection(spec.Into) * (InnerWall / 2f + StripeDepth), rotation);

            var available = patientCase != null;
            var headerColor = available ? ZoneColors[spec.Zone - 1] : CardComingSoonColor;
            var headerMat = available ? AccentMat(spec.Zone) : Mat("CardComingSoon", CardComingSoonColor);
            var bodyHeight = CardHeight - CardHeaderHeight;

            Box("Header", card, new Vector3(0f, CardHeight / 2f - CardHeaderHeight / 2f, -CardDepth / 2f),
                new Vector3(CardWidth, CardHeaderHeight, CardDepth), headerMat);
            Box("Body", card, new Vector3(0f, -CardHeaderHeight / 2f, -CardDepth / 2f),
                new Vector3(CardWidth, bodyHeight, CardDepth), Mat("CardBody", new Color(0.98f, 0.98f, 0.97f)));

            var front = -CardDepth - 0.003f;
            var header = available ? $"Room {patientCase.Room}  ·  {patientCase.Difficulty}" : $"Room {spec.Id}";
            Label("HeaderText", card, header, new Vector3(0f, CardHeight / 2f - CardHeaderHeight / 2f, front), Vector3.back,
                CardWidth - 2f * CardPadding, CardHeaderHeight * 0.7f, Color.white);

            var body = available
                ? $"<b>{patientCase.PatientName}</b>, {patientCase.Age}\n\n{patientCase.Situation}\n\n" +
                  $"<b><color=#{ColorUtility.ToHtmlStringRGB(headerColor * 0.8f)}>Watch out:</color></b> {patientCase.WatchOut}"
                : "Coming soon";
            Label("BodyText", card, body, new Vector3(0f, -CardHeaderHeight / 2f, front), Vector3.back,
                CardWidth - 2f * CardPadding, bodyHeight - 2f * CardPadding, available ? CardTextColor : CardComingSoonColor,
                wrap: available, alignment: available ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center);
        }

        /// <summary>Alle PatientCase-assets in Assets/Hospital/Data, per deur (bv. "1A").</summary>
        private static Dictionary<string, PatientCase> LoadCases()
        {
            var result = new Dictionary<string, PatientCase>();
            if (!AssetDatabase.IsValidFolder(CaseFolder))
                return result;
            foreach (var path in AssetDatabase.FindAssets("t:PatientCase", new[] { CaseFolder }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var c = AssetDatabase.LoadAssetAtPath<PatientCase>(path);
                if (c == null || string.IsNullOrWhiteSpace(c.DoorLabel))
                    continue;
                if (!result.TryAdd(c.DoorLabel, c))
                    Debug.LogWarning($"{LogPrefix}Deur {c.DoorLabel} heeft meer dan één case, {path} wordt genegeerd.");
            }
            Debug.Log($"{LogPrefix}{result.Count} case(s): {string.Join(", ", result.Keys.OrderBy(k => k))}.");
            return result;
        }

        private static void Label(string name, Transform parent, string text, Vector3 localPosition, Vector3 normal,
            float width, float height, Color color, bool wrap = false, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            if (_font == null)
                return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.LookRotation(-normal);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = _font;
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = 10f;
            tmp.rectTransform.sizeDelta = new Vector2(width, height);
            tmp.ForceMeshUpdate();
        }

        private static void Anchor(string name, Transform parent, Vector3 localPosition, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, rotation);
        }

        // --- Meubels richten en op hoogte brengen (kopieën uit HospitalBuilder) ---------------

        /// <summary>Hoogste punt van het object recht boven (x, z), via een raycast op de mesh colliders. NaN als er niets is.</summary>
        private static float TopAt(GameObject go, float x, float z)
        {
            Physics.SyncTransforms();
            var heights = Physics.RaycastAll(new Vector3(x, 100f, z), Vector3.down, 200f)
                .Where(h => h.collider.transform.IsChildOf(go.transform))
                .Select(h => h.point.y).ToList();
            return heights.Count == 0 ? float.NaN : heights.Max();
        }

        /// <summary>Draait het bed zodat het hoogste uiteinde (het hoofdeinde) naar +z wijst, tegen de buitenmuur.</summary>
        private static void OrientHeadboard(GameObject bed, Vector3 center)
        {
            var b = HospitalAssetPrep.WorldBounds(bed);
            var front = TopAt(bed, b.center.x, b.max.z - 0.08f);
            var back = TopAt(bed, b.center.x, b.min.z + 0.08f);
            if (back > front)
            {
                bed.transform.Rotate(0f, 180f, 0f, Space.World);
                AlignToFloor(bed, center);
            }
        }

        /// <summary>Draait een object zodat de hoogste kant (rugleuning, spoelbak, ...) naar backDirection wijst.</summary>
        private static void OrientBack(GameObject go, Vector3 backDirection, Vector3 worldCenter)
        {
            var b = HospitalAssetPrep.WorldBounds(go);
            var sample = Mathf.Min(b.extents.x, b.extents.z) * 0.75f;
            var directions = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            var highest = directions
                .Select(d => (Dir: d, Top: TopAt(go, b.center.x + d.x * sample, b.center.z + d.z * sample)))
                .Where(s => !float.IsNaN(s.Top))
                .OrderByDescending(s => s.Top)
                .FirstOrDefault();
            if (highest.Dir == Vector3.zero)
                return;
            go.transform.Rotate(0f, Vector3.SignedAngle(highest.Dir, backDirection, Vector3.up), 0f, Space.World);
            AlignToFloor(go, worldCenter);
        }

        /// <summary>Schaalt het object in de hoogte zodat het bovenvlak in het midden op target komt. Geeft de hoogte terug.</summary>
        private static float AdjustHeight(GameObject go, Vector3 center, float target, string what)
        {
            var b = HospitalAssetPrep.WorldBounds(go);
            var measured = TopAt(go, b.center.x, b.center.z);
            if (float.IsNaN(measured) || measured <= 0.01f)
            {
                Debug.LogWarning($"{LogPrefix}{go.name}: hoogte van het {what} niet meetbaar, verwacht {target} m.");
                return target;
            }
            var factor = target / measured;
            if (factor < HeightAdjustMin || factor > HeightAdjustMax)
            {
                Debug.LogWarning($"{LogPrefix}{go.name}: {what} op {measured:0.00} m, te ver van {target} m om te schalen.");
                return measured;
            }
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x, s.y * factor, s.z);
            AlignToFloor(go, center);
            return TopAt(go, b.center.x, b.center.z);
        }

        // --- Screenshots --------------------------------------------------------------------

        /// <summary>Vanuit de deuropening de kamer in: een eenpersoonskamer, een gedeelde kamer en een hoekkamer.</summary>
        private static void TakeRoomScreenshots(Transform root)
        {
            foreach (var (id, file) in new[] { ("1A", "single"), ("1C", "shared"), ("2E", "corner") })
            {
                var spec = PatientRooms.First(r => r.Id == id);
                var room = root.Find($"Floor{spec.Zone}/Room_{id}");
                var door = room.InverseTransformPoint(PlanPoint(spec.DoorCenter));
                var eye = room.TransformPoint(new Vector3(door.x, EyeHeight, 0.3f));
                var target = room.TransformPoint(new Vector3(0f, 0.8f, RoomInteriorDepth - 1.2f));
                Shot($"u_afdeling_rooms_{file}.png", eye, target);
            }

            // Vanuit de gang: deur en kaart van 1A
            var first = PatientRooms.First(r => r.Id == "1A");
            var doorPoint = PlanPoint(first.DoorCenter);
            var toCorridor = PlanDirection(-first.Into);
            var toCard = PlanDirection(Vector2.down);   // de kaart van 1A hangt aan de kant van de kleinere y
            Shot("u_afdeling_rooms_door.png", doorPoint + toCorridor * 1.9f + toCard * 1.6f + Vector3.up * EyeHeight,
                doorPoint + toCard * 0.4f + Vector3.up * 1.3f);
        }

        private static void Shot(string file, Vector3 position, Vector3 lookAt)
        {
            var go = new GameObject("ScreenshotCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = ScreenshotFov;
                cam.nearClipPlane = 0.05f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                HospitalAssetPrep.Capture(cam, $"{ScreenshotFolder}/{file}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
