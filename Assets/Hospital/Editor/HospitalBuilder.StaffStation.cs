using System;
using System.Linq;
using ProCommVR.Hospital;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Blokken "StaffStation" (verpleegpost in de lobby, voor begin en einde van de shift) en "GuideLights"
    /// (lichtstroken in de gangvloer). Enkel omgeving en vaste plekken (ankers); de spellogica is van Jalil.
    /// Bestaande objecten (ReceptionDesk, Lift, Floor*, Room_*, Card_*) worden niet aangeraakt. Zie Assets/Hospital/README.md.
    /// </summary>
    public static partial class HospitalBuilder
    {
        private const string StaffStationName = "StaffStation";

        // Voorkant van de props (Blender -Y) wijst in Unity naar -Z; gemeten door PropSetup op de Wall_Clock
        private static readonly Vector3 PropFront = Vector3.back;

        // Jalils ShiftBoard hangt op de noordmuur achter de balie: midden x = 7.3, 0.9 m x 1.4 breed
        private const float ShiftBoardMinX = 6.67f;
        private const float ShiftBoardMaxX = 7.93f;

        // Wanditems op de noordmuur, rechts van het ShiftBoard (onderkant van elk item)
        private const float BadgeReaderX = 8.3f;
        private const float BadgeReaderY = 1.1f;
        private const float TabletDockX = 8.8f;
        private const float TabletDockY = 1.0f;
        private const float WallClockX = 8.55f;
        private const float WallClockY = 2.05f;
        private const float DeskItemInset = 0.3f;            // clipboard en tablet: zo ver van de uiteinden van de balie
        private const float DeskItemEdgeGap = 0.15f;         // en zo ver van de rand aan de personeelskant
        private const float CartGapFromDesk = 0.45f;          // van de oostkant van de balie tot het midden van de kar

        // Plek voor een scherm aan het einde van de shift: oostmuur, tussen de wegwijzers en de plant
        private const float EndScreenZ = 4.0f;
        private const float EndScreenY = 1.7f;

        // Lichtstroken: één baan per deur, de baan naar A het dichtst bij de deuren, zodat ze elkaar niet kruisen
        private const float GuideWidth = 0.06f;
        private const float GuideThickness = 0.002f;
        private const float GuideLaneNorthZ = 2.2f;
        private const float GuideLaneSpacing = 0.15f;
        private const float GuideStartX = WallThickness / 2f + 0.05f;

        // --- Verpleegpost ------------------------------------------------------------

        private static void BuildStaffStation(Transform lobby)
        {
            var desk = lobby.Find("Furniture/ReceptionDesk");
            if (desk == null)
            {
                Debug.LogError($"{LogPrefix}Lobby/Furniture/ReceptionDesk niet gevonden, geen StaffStation.");
                return;
            }
            var station = new GameObject(StaffStationName).transform;
            station.SetParent(lobby, false);

            var d = HospitalAssetPrep.WorldBounds(desk.gameObject);
            var wallZ = LobbyMaxZ - WallThickness / 2f;

            // Aan de muur, naar de lobby gericht
            PlaceProp("Badge_Reader", station, new Vector3(BadgeReaderX, BadgeReaderY, wallZ), Vector3.back);
            PlaceProp("Tablet_Dock", station, new Vector3(TabletDockX, TabletDockY, wallZ), Vector3.back);
            var clock = PlaceProp("Wall_Clock", station, new Vector3(WallClockX, WallClockY, wallZ), Vector3.back);
            if (clock != null && clock.TryGetComponent<WallClock>(out var wallClock))
                wallClock.SetTime(DateTime.Now);

            // Op de balie, aan de personeelskant (+z); de bezoekerskant blijft vrij voor Jalils startcomputer.
            // De balie is ondiep: de clipboard ligt dwars (lange kant langs x).
            var staffZ = d.max.z - DeskItemEdgeGap;
            var clipboardPos = DeskTop(desk.gameObject, d, d.max.x - DeskItemInset, staffZ);
            var tabletPos = DeskTop(desk.gameObject, d, d.min.x + DeskItemInset, staffZ);
            var towardStaff = Quaternion.Euler(0f, 78f, 0f) * Vector3.forward;
            var clipboard = PlaceProp("Clipboard", station, lobby.InverseTransformPoint(clipboardPos), towardStaff);
            PlaceProp("Care_Tablet", station, lobby.InverseTransformPoint(tabletPos), Quaternion.Euler(0f, 10f, 0f) * Vector3.forward);

            // Kar naast de balie, laden naar de lobby
            PlaceProp("Supply_Cart", station, lobby.InverseTransformPoint(new Vector3(d.max.x + CartGapFromDesk, 0f, d.center.z + 0.1f)),
                Vector3.back);

            // Ankers: vooruit (blauwe as) = de richting waarin de speler kijkt of reikt
            var face = Quaternion.LookRotation(Vector3.forward);
            Anchor("Anchor_BadgeScan", station, new Vector3(BadgeReaderX, BadgeReaderY + 0.06f, wallZ - 0.1f), face);
            Anchor("Anchor_TabletDock", station, new Vector3(TabletDockX, TabletDockY + 0.02f, wallZ - 0.03f), face);
            if (clipboard != null)
                Anchor("Anchor_Clipboard", station, lobby.InverseTransformPoint(clipboardPos) + Vector3.up * 0.01f,
                    Quaternion.LookRotation(-towardStaff));
            Anchor("Anchor_EndScreen", station, new Vector3(LobbyLength - WallThickness / 2f - 0.02f, EndScreenY, EndScreenZ),
                Quaternion.LookRotation(Vector3.right));

            CheckStationClearance(lobby, station, desk);
        }

        /// <summary>
        /// Plaatst een prop-prefab met zijn oorsprong (onderaan, bij wandobjecten tegen de muur) op localPosition en draait
        /// hem enkel rond y tot zijn voorkant naar front wijst. De root van de prefab houdt zijn rotatie uit de FBX-conversie.
        /// </summary>
        private static GameObject PlaceProp(string prop, Transform parent, Vector3 localPosition, Vector3 front)
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
            go.transform.localPosition = localPosition;
            return go;
        }

        /// <summary>Bovenvlak van de balie in (x, z), gemeten met een raycast; anders de bovenkant van de bounds.</summary>
        private static Vector3 DeskTop(GameObject desk, Bounds bounds, float x, float z)
        {
            var top = TopAt(desk, x, z);
            return new Vector3(x, float.IsNaN(top) ? bounds.max.y : top, z);
        }

        /// <summary>Meldt (zonder iets te verplaatsen) als de post Jalils bord, de bezoekerskant of de bestaande meubels raakt.</summary>
        private static void CheckStationClearance(Transform lobby, Transform station, Transform desk)
        {
            var deskBounds = HospitalAssetPrep.WorldBounds(desk.gameObject);
            foreach (Transform item in station)
            {
                if (item.GetComponentInChildren<Renderer>() == null)
                    continue;
                var b = HospitalAssetPrep.WorldBounds(item.gameObject);
                if (b.max.z > LobbyMaxZ - WallThickness - 0.2f && b.max.x > ShiftBoardMinX && b.min.x < ShiftBoardMaxX)
                    Debug.LogWarning($"{LogPrefix}{item.name} hangt op de plek van Jalils ShiftBoard.");
                if (b.min.z < deskBounds.center.z && b.max.x > deskBounds.min.x && b.min.x < deskBounds.max.x)
                    Debug.LogWarning($"{LogPrefix}{item.name} staat aan de bezoekerskant van de balie.");
                foreach (Transform furniture in lobby.Find("Furniture"))
                {
                    if (furniture == desk && b.min.y >= deskBounds.max.y - 0.05f)
                        continue;
                    var f = HospitalAssetPrep.WorldBounds(furniture.gameObject);
                    f.Expand(-0.02f);
                    if (f.Intersects(b))
                        Debug.LogWarning($"{LogPrefix}{item.name} raakt {furniture.name}.");
                }
            }
        }

        // --- Lichtstroken in de gang -------------------------------------------------------

        /// <summary>
        /// Per deur een smalle strook in de vloer: langs de gang vanaf de lift, dan haaks naar de deur.
        /// Emissive materiaal, geen lichtbron en geen collider. FloorGuideLight op Guide_&lt;deur&gt; kleurt hem.
        /// </summary>
        private static void BuildGuideLights(Transform level, int floor)
        {
            var root = new GameObject("GuideLights").transform;
            root.SetParent(level, false);
            var mat = Mat("FloorGuide", FloorGuideLight.Neutral, 0f, 0.5f, FloorGuideLight.Neutral * 0.5f);
            var doorWallZ = CorridorWidth - WallThickness / 2f;
            var y = FloorFinishThickness + GuideThickness / 2f;

            for (var i = 0; i < DoorCentersX.Length; i++)
            {
                var guide = new GameObject($"Guide_{floor}{DoorLetters[i]}").transform;
                guide.SetParent(root, false);
                var laneZ = GuideLaneNorthZ - i * GuideLaneSpacing;
                var doorX = DoorCentersX[i];
                var laneEnd = doorX - GuideWidth / 2f;
                Strip("Lane", guide, new Vector3((GuideStartX + laneEnd) / 2f, y, laneZ), new Vector3(laneEnd - GuideStartX, GuideThickness, GuideWidth), mat);
                var turnStart = laneZ - GuideWidth / 2f;
                Strip("ToDoor", guide, new Vector3(doorX, y, (turnStart + doorWallZ) / 2f), new Vector3(GuideWidth, GuideThickness, doorWallZ - turnStart), mat);
                guide.gameObject.AddComponent<FloorGuideLight>();
            }
        }

        private static void Strip(string name, Transform parent, Vector3 localCenter, Vector3 size, Material mat)
        {
            var go = Box(name, parent, localCenter, size, mat);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // --- Screenshots -------------------------------------------------------------

        private static void TakeStationScreenshots()
        {
            Shot("hospital_lobby_staff_station.png", new Vector3(5.6f, EyeHeight, 2.4f), new Vector3(8.3f, 1.3f, LobbyMaxZ));

            var guides = Object.FindObjectsByType<FloorGuideLight>(FindObjectsSortMode.None);
            var calm = guides.FirstOrDefault(g => g.name == "Guide_1A");
            var difficult = guides.FirstOrDefault(g => g.name == "Guide_1C");
            if (calm == null || difficult == null)
                return;
            calm.SetColor(FloorGuideLight.Calm);
            difficult.SetColor(FloorGuideLight.Difficult);
            var floor1 = FloorHeight;
            Shot("hospital_guide_lights_floor1.png", new Vector3(0.6f, floor1 + EyeHeight, 0.5f), new Vector3(9f, floor1, 2.4f));
            calm.SetNeutral();
            difficult.SetNeutral();
        }
    }
}
