using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Bouwt het ziekenhuis in Assets/Scenes/liam_test_scene.unity onder één root "Hospital":
    /// een lobby met een zelfgebouwde lift, 3 vloeren met elk een gang en 3 patiëntenkamers (één prefab, 9 keer gebruikt),
    /// accentkleur en bord per vloer, en weinig lichten. Gebruikt Kenney-meubels en de Hospital Door van anasrar.
    /// Mag opnieuw gedraaid worden: de root "Hospital" en de prefab PatientRoom worden telkens opnieuw opgebouwd.
    /// </summary>
    public static class HospitalBuilder
    {
        // --- Paden ---------------------------------------------------------------
        private const string ScenePath = "Assets/Scenes/liam_test_scene.unity";
        private const string PrefabFolder = "Assets/Hospital/Prefabs";
        private const string RoomPrefabPath = PrefabFolder + "/PatientRoom.prefab";
        private const string MaterialFolder = "Assets/Hospital/Materials";
        private const string KenneyFolder = "Assets/ThirdParty/Kenney/FurnitureKit";
        private const string DoorModelPath = "Assets/ThirdParty/Sketchfab/HospitalDoor_anasrar/source/untitled.fbx";
        private const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string ScreenshotFolder = "docs/Screenshots";
        private const string RootName = "Hospital";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string LogPrefix = "[HospitalBuilder] ";

        // --- Gebouw (alle maten in meter) ------------------------------------------
        private const int FloorCount = 3;
        private const float FloorHeight = 3.5f;
        private const float SlabThickness = 0.2f;
        private const float CeilingHeight = FloorHeight - SlabThickness;
        private const float WallThickness = 0.2f;
        private const float FloorFinishThickness = 0.01f;

        // Gang (x = lengte, z = breedte; de lift zit aan x = 0, de kamers aan de kant z = CorridorWidth)
        private const float CorridorLength = 18f;
        private const float CorridorWidth = 3f;
        private static readonly float[] DoorCentersX = { 4f, 9f, 14f };
        private static readonly string[] DoorLetters = { "A", "B", "C" };

        // Accentstreep op de gangmuren
        private const float StripeBottom = 0.95f;
        private const float StripeHeight = 0.15f;
        private const float StripeDepth = 0.02f;

        // Kamer (binnenmaten; de deurmuur is de gangmuur)
        private const float RoomWidth = 4f;
        private const float RoomDepth = 4f;
        private const float WindowWidth = 1.4f;
        private const float WindowSill = 0.9f;
        private const float WindowTop = 2.2f;
        private const float WindowCenterFromDoor = 2.6f;   // afstand van de deurmuur tot het midden van het raam
        private const float BedWallGap = 0.05f;
        private const float ChairSideOffset = 0.95f;       // van het midden van het bed tot het midden van de stoel
        private const float ChairFromBackWall = 1.1f;

        // Animaties Laying Idle en Sitting Idle zijn gemaakt voor deze hoogtes
        private const float MattressHeight = 0.5f;
        private const float ChairSeatHeight = 0.45f;
        private const float HeightAdjustMin = 0.7f;        // buiten dit bereik wordt niet geschaald, enkel gemeld
        private const float HeightAdjustMax = 1.4f;

        // Lobby
        private const float LobbyLength = 10f;
        private const float LobbyMinZ = -3f;
        private const float LobbyMaxZ = 6f;

        // Lift (aan de muur x = 0, schacht aan de kant x < 0)
        private const float LiftCenterZ = 1.5f;
        private const float LiftOpeningWidth = 1.4f;
        private const float LiftOpeningHeight = 2.2f;
        private const float LiftShaftDepth = 2.2f;
        private const float LiftShaftWidth = 2.4f;
        private const float LiftFrameWidth = 0.1f;
        private const float LiftFrameDepth = 0.08f;
        private const float LiftDoorThickness = 0.05f;
        private const float LiftPanelOffsetZ = 1.05f;      // van het midden van de liftdeur tot het paneel
        private const float LiftPanelHeight = 1.2f;
        private const float LiftButtonSize = 0.08f;
        private const float LiftButtonSpacing = 0.12f;

        // Borden
        private const float SignWidth = 2.2f;
        private const float SignHeight = 0.5f;
        private const float SignCenterY = 2.4f;
        private const float SignDepth = 0.04f;

        // Licht (Quest 3: geen schaduwen, weinig lichten)
        private const float CorridorLightSpacing = 6f;
        private const float CorridorLightRange = 7f;
        private const float CorridorLightIntensity = 1.6f;
        private const float RoomLightRange = 5.5f;
        private const float RoomLightIntensity = 1.4f;
        private const float LobbyLightRange = 8f;
        private const float LobbyLightIntensity = 1.6f;
        private static readonly Color LightColor = new Color(1f, 0.96f, 0.9f);
        private static readonly Color AmbientColor = new Color(0.42f, 0.43f, 0.46f);

        // Screenshots
        private const float ScreenshotFov = 70f;
        private const float EyeHeight = 1.65f;

        private static readonly (string Label, Color Color)[] Floors =
        {
            ("Floor 1 - Easy", new Color(0.30f, 0.69f, 0.31f)),
            ("Floor 2 - Medium", new Color(0.96f, 0.60f, 0.10f)),
            ("Floor 3 - Hard", new Color(0.86f, 0.20f, 0.18f)),
        };

        private struct Opening
        {
            public float Center, Width, Bottom, Top;
            public Opening(float center, float width, float bottom, float top)
            {
                Center = center; Width = width; Bottom = bottom; Top = top;
            }
        }

        private static TMP_FontAsset _font;
        private static Vector2 _doorSize;

        [MenuItem("ProCommVR/Hospital/Build")]
        public static void Build()
        {
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (old != null)
                Object.DestroyImmediate(old);

            _font = EnsureTmpFont();
            _doorSize = MeasureDoor();

            var roomPrefab = BuildRoomPrefab();
            var root = new GameObject(RootName).transform;

            BuildSlabs(root);
            BuildLiftShaft(root);
            BuildLobby(Level(root, "Lobby", 0));
            for (var f = 1; f <= FloorCount; f++)
                BuildFloor(Level(root, $"Floor{f}", f), f, roomPrefab);

            SetupLighting(scene, root);
            MarkStatic(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            TakeScreenshots();
            Debug.Log($"{LogPrefix}Klaar: {ScenePath}, prefab {RoomPrefabPath}, screenshots in {ScreenshotFolder}/.");
        }

        /// <summary>Voor de command line: Unity -batchmode -executeMethod ProCommVR.Editor.HospitalBuilder.RunBatch</summary>
        public static void RunBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError(LogPrefix + e);
                EditorApplication.Exit(1);
            }
        }

        private static Transform Level(Transform root, string name, int level)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(0f, level * FloorHeight, 0f);
            return t;
        }

        // --- Structuur ------------------------------------------------------------

        /// <summary>Vloerplaten voor lobby, 3 vloeren en dak. De onderkant van elke plaat is het plafond van de laag eronder.</summary>
        private static void BuildSlabs(Transform root)
        {
            var parent = new GameObject("Slabs").transform;
            parent.SetParent(root, false);

            var minX = -(LiftShaftDepth + WallThickness);
            var maxX = CorridorLength + WallThickness / 2f;
            var minZ = LobbyMinZ - WallThickness / 2f;
            var maxZ = CorridorWidth + RoomDepth + WallThickness * 1.5f;
            var size = new Vector3(maxX - minX, SlabThickness, maxZ - minZ);
            var center = new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f);

            for (var level = 0; level <= FloorCount + 1; level++)
            {
                var top = level * FloorHeight;
                Box($"Slab_{level}", parent, center + Vector3.up * (top - SlabThickness / 2f), size, Mat("Ceiling", new Color(0.93f, 0.93f, 0.92f)));
                if (level <= FloorCount)
                    Box($"FloorFinish_{level}", parent, center + Vector3.up * (top + FloorFinishThickness / 2f),
                        new Vector3(size.x, FloorFinishThickness, size.z), Mat("Floor", new Color(0.72f, 0.75f, 0.78f)));
            }
        }

        private static void BuildLiftShaft(Transform root)
        {
            var height = (FloorCount + 1) * FloorHeight;
            Box("LiftShaft", root,
                new Vector3(-WallThickness / 2f - LiftShaftDepth / 2f, height / 2f, LiftCenterZ),
                new Vector3(LiftShaftDepth, height, LiftShaftWidth), Mat("LiftShaft", new Color(0.22f, 0.23f, 0.25f)));
        }

        private static void BuildLobby(Transform level)
        {
            var wall = Mat("Wall", new Color(0.95f, 0.94f, 0.91f));
            var lobbyDepth = LobbyMaxZ - LobbyMinZ;

            Wall("Wall_West", level, new Vector3(0f, 0f, LobbyMinZ), false, lobbyDepth, 0f, CeilingHeight, WallThickness, wall,
                new Opening(LiftCenterZ - LobbyMinZ, LiftOpeningWidth, 0f, LiftOpeningHeight));
            Wall("Wall_South", level, new Vector3(0f, 0f, LobbyMinZ), true, LobbyLength, 0f, CeilingHeight, WallThickness, wall);
            Wall("Wall_North", level, new Vector3(0f, 0f, LobbyMaxZ), true, LobbyLength, 0f, CeilingHeight, WallThickness, wall);
            Wall("Wall_East", level, new Vector3(LobbyLength, 0f, LobbyMinZ), false, lobbyDepth, 0f, CeilingHeight, WallThickness, wall);

            BuildLift(level, 0, new Color(0.5f, 0.52f, 0.55f));

            // Onthaal en wachtzone
            var furniture = new GameObject("Furniture").transform;
            furniture.SetParent(level, false);
            PlaceKenney("desk", furniture, "ReceptionDesk", new Vector3(6.5f, 0f, 4.3f), 0f);
            var deskChair = PlaceKenney("chairDesk", furniture, "ReceptionChair", new Vector3(6.5f, 0f, 5.2f), 0f);
            OrientBack(deskChair, Vector3.forward, new Vector3(6.5f, 0f, 5.2f));
            for (var i = 0; i < 3; i++)
            {
                var pos = new Vector3(3f + i * 1.2f, 0f, LobbyMinZ + 0.6f);
                OrientBack(PlaceKenney("loungeChair", furniture, $"WaitingChair_{i + 1}", pos, 0f), Vector3.back, pos);
            }
            PlaceKenney("pottedPlant", furniture, "Plant_1", new Vector3(LobbyLength - 0.5f, 0f, LobbyMaxZ - 0.5f), 0f);
            PlaceKenney("pottedPlant", furniture, "Plant_2", new Vector3(LobbyLength - 0.5f, 0f, LobbyMinZ + 0.5f), 0f);

            // Borden: "Lobby" en een wegwijzer met de vloeren
            Sign("Sign_Lobby", level, "Lobby", new Vector3(5f, SignCenterY, LobbyMaxZ - WallThickness / 2f), Vector3.back,
                Mat("SignLobby", new Color(0.25f, 0.42f, 0.62f)));
            for (var f = 0; f < Floors.Length; f++)
            {
                var y = SignCenterY - 0.25f - (Floors.Length - 1 - f) * (SignHeight + 0.1f);
                Sign($"Sign_Directory_{f + 1}", level, Floors[f].Label,
                    new Vector3(LobbyLength - WallThickness / 2f, y, 1.5f), Vector3.left, AccentMat(f + 1));
            }

            AddLight("Light_1", level, new Vector3(3f, CeilingHeight - 0.3f, 1.5f), LobbyLightRange, LobbyLightIntensity);
            AddLight("Light_2", level, new Vector3(7.5f, CeilingHeight - 0.3f, 1.5f), LobbyLightRange, LobbyLightIntensity);
        }

        private static void BuildFloor(Transform level, int floor, GameObject roomPrefab)
        {
            var wall = Mat("Wall", new Color(0.95f, 0.94f, 0.91f));
            var accent = AccentMat(floor);
            var doors = DoorCentersX.Select(x => new Opening(x, _doorSize.x, 0f, _doorSize.y)).ToArray();

            Wall("Wall_South", level, Vector3.zero, true, CorridorLength, 0f, CeilingHeight, WallThickness, wall);
            Wall("Wall_North", level, new Vector3(0f, 0f, CorridorWidth), true, CorridorLength, 0f, CeilingHeight, WallThickness, wall, doors);
            Wall("Wall_East", level, new Vector3(CorridorLength, 0f, 0f), false, CorridorWidth, 0f, CeilingHeight, WallThickness, wall);
            Wall("Wall_West", level, Vector3.zero, false, CorridorWidth, 0f, CeilingHeight, WallThickness, wall,
                new Opening(LiftCenterZ, LiftOpeningWidth, 0f, LiftOpeningHeight));

            // Accentstreep aan beide kanten van de gang, onderbroken door de deuren
            var inset = WallThickness / 2f + StripeDepth / 2f;
            Wall("Accent_South", level, new Vector3(0f, 0f, inset), true, CorridorLength,
                StripeBottom, StripeBottom + StripeHeight, StripeDepth, accent);
            Wall("Accent_North", level, new Vector3(0f, 0f, CorridorWidth - inset), true, CorridorLength,
                StripeBottom, StripeBottom + StripeHeight, StripeDepth, accent, doors);

            Sign("Sign_Floor", level, Floors[floor - 1].Label, new Vector3(2.4f, SignCenterY, WallThickness / 2f), Vector3.forward, accent);
            BuildLift(level, floor, Floors[floor - 1].Color);

            for (var i = 0; i < DoorCentersX.Length; i++)
            {
                var label = $"{floor}{DoorLetters[i]}";
                PlaceDoor(level, label, new Vector3(DoorCentersX[i], 0f, CorridorWidth));
                var room = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, level);
                room.name = $"Room_{label}";
                room.transform.localPosition = new Vector3(DoorCentersX[i], 0f, CorridorWidth);
            }

            for (var x = CorridorLightSpacing / 2f; x < CorridorLength; x += CorridorLightSpacing)
            {
                var pos = new Vector3(x, CeilingHeight, CorridorWidth / 2f);
                AddCeilingLamp(level, $"Lamp_{x:0}", pos);
                AddLight($"Light_{x:0}", level, pos + Vector3.down * 0.3f, CorridorLightRange, CorridorLightIntensity);
            }
        }

        // --- Lift ----------------------------------------------------------------

        /// <summary>Kader, twee schuifdeuren (dicht) en een paneel met knoppen. Lobby: 1, 2, 3. Vloeren: L, 1, 2, 3.</summary>
        private static void BuildLift(Transform level, int floor, Color highlight)
        {
            var lift = new GameObject("Lift").transform;
            lift.SetParent(level, false);
            var frame = Mat("LiftFrame", new Color(0.35f, 0.36f, 0.38f), 0.6f, 0.5f);
            var door = Mat("LiftDoor", new Color(0.78f, 0.80f, 0.82f), 0.8f, 0.6f);

            var faceX = WallThickness / 2f + LiftFrameDepth / 2f;
            var halfOpening = LiftOpeningWidth / 2f;
            Box("Frame_Left", lift, new Vector3(faceX, LiftOpeningHeight / 2f, LiftCenterZ - halfOpening - LiftFrameWidth / 2f),
                new Vector3(LiftFrameDepth, LiftOpeningHeight, LiftFrameWidth), frame);
            Box("Frame_Right", lift, new Vector3(faceX, LiftOpeningHeight / 2f, LiftCenterZ + halfOpening + LiftFrameWidth / 2f),
                new Vector3(LiftFrameDepth, LiftOpeningHeight, LiftFrameWidth), frame);
            Box("Frame_Top", lift, new Vector3(faceX, LiftOpeningHeight + LiftFrameWidth / 2f, LiftCenterZ),
                new Vector3(LiftFrameDepth, LiftFrameWidth, LiftOpeningWidth + 2f * LiftFrameWidth), frame);

            Box("LiftDoor_Left", lift, new Vector3(0f, LiftOpeningHeight / 2f, LiftCenterZ - halfOpening / 2f),
                new Vector3(LiftDoorThickness, LiftOpeningHeight, halfOpening), door);
            Box("LiftDoor_Right", lift, new Vector3(0f, LiftOpeningHeight / 2f, LiftCenterZ + halfOpening / 2f),
                new Vector3(LiftDoorThickness, LiftOpeningHeight, halfOpening), door);

            var panelZ = LiftCenterZ + LiftPanelOffsetZ;
            var panelX = WallThickness / 2f + 0.02f;
            var labels = floor == 0 ? new[] { "1", "2", "3" } : new[] { "L", "1", "2", "3" };
            var panelHeight = labels.Length * LiftButtonSpacing + 0.1f;
            Box("Panel", lift, new Vector3(panelX, LiftPanelHeight, panelZ), new Vector3(0.04f, panelHeight, 0.2f), frame);

            var buttonMat = Mat("LiftButton", new Color(0.9f, 0.9f, 0.88f));
            for (var i = 0; i < labels.Length; i++)
            {
                var y = LiftPanelHeight + panelHeight / 2f - 0.05f - LiftButtonSpacing * (i + 0.5f);
                var current = labels[i] == floor.ToString();
                var mat = current ? Mat($"LiftButtonActive_{floor}", highlight) : buttonMat;
                var button = Box($"LiftButton_{labels[i]}", lift, new Vector3(panelX + 0.025f, y, panelZ),
                    new Vector3(0.015f, LiftButtonSize, LiftButtonSize), mat);
                Label("Text", button.transform.parent, labels[i], new Vector3(panelX + 0.034f, y, panelZ), Vector3.right,
                    LiftButtonSize, LiftButtonSize, new Color(0.15f, 0.15f, 0.15f));
            }
        }

        // --- Deuren en kamers ---------------------------------------------------------

        private static Vector2 MeasureDoor()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DoorModelPath);
            if (model == null)
                throw new FileNotFoundException("Hospital Door niet gevonden", DoorModelPath);
            var instance = Object.Instantiate(model);
            var size = HospitalAssetPrep.WorldBounds(instance).size;
            Object.DestroyImmediate(instance);
            return new Vector2(Mathf.Max(size.x, size.z), size.y);
        }

        /// <summary>De deur staat in de opening van de gangmuur. Niet static: gaat later open.</summary>
        private static void PlaceDoor(Transform level, string label, Vector3 localPosition)
        {
            var holder = new GameObject($"Door_{label}").transform;
            holder.SetParent(level, false);
            holder.localPosition = localPosition;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DoorModelPath), holder);
            model.name = "Model";
            var size = HospitalAssetPrep.WorldBounds(model).size;
            if (size.x < size.z)
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            AlignToFloor(model, holder.position);
        }

        /// <summary>
        /// Eén patiëntenkamer, gebouwd rond de oorsprong: deurmuur (= gangmuur) op z = 0, kamer naar +z.
        /// Bed met het hoofdeinde tegen de achtermuur, nachtkastje ernaast, stoel naar het bed gericht, raam in de rechtermuur.
        /// Ankers voor fase 3: Anchor_Laying (matras) en Anchor_Sitting (zitvlak).
        /// </summary>
        private static GameObject BuildRoomPrefab()
        {
            EnsureFolder(PrefabFolder);
            var room = new GameObject("PatientRoom");
            var wall = Mat("RoomWall", new Color(0.92f, 0.93f, 0.90f));
            var half = RoomWidth / 2f;
            var backZ = WallThickness / 2f + RoomDepth;          // binnenkant achtermuur
            var sideLength = backZ;

            Wall("Wall_Back", room.transform, new Vector3(-half - WallThickness, 0f, backZ + WallThickness / 2f), true,
                RoomWidth + 2f * WallThickness, 0f, CeilingHeight, WallThickness, wall);
            Wall("Wall_Left", room.transform, new Vector3(-half - WallThickness / 2f, 0f, 0f), false, sideLength, 0f, CeilingHeight, WallThickness, wall);
            Wall("Wall_Right", room.transform, new Vector3(half + WallThickness / 2f, 0f, 0f), false, sideLength, 0f, CeilingHeight, WallThickness, wall,
                new Opening(WindowCenterFromDoor, WindowWidth, WindowSill, WindowTop));
            BuildWindow(room.transform, new Vector3(half + WallThickness / 2f, 0f, WindowCenterFromDoor));

            var furniture = new GameObject("Furniture").transform;
            furniture.SetParent(room.transform, false);

            // Bed: hoofdeinde tegen de achtermuur, gecentreerd
            var bed = PlaceKenney("bedSingle", furniture, "Bed", Vector3.zero, 0f);
            var bedLength = HospitalAssetPrep.WorldBounds(bed).size.z;
            var bedCenter = new Vector3(0f, 0f, backZ - BedWallGap - bedLength / 2f);
            AlignToFloor(bed, bedCenter);
            OrientHeadboard(bed, bedCenter);
            var mattress = AdjustHeight(bed, bedCenter, MattressHeight, "matras");

            // Nachtkastje links naast het hoofdeinde
            var bedHalfWidth = HospitalAssetPrep.WorldBounds(bed).extents.x;
            var stand = PlaceKenney("cabinetBed", furniture, "Nightstand", Vector3.zero, 0f);
            var standSize = HospitalAssetPrep.WorldBounds(stand).size;
            AlignToFloor(stand, new Vector3(-bedHalfWidth - 0.1f - standSize.x / 2f, 0f, backZ - BedWallGap - standSize.z / 2f));

            // Stoel rechts van het bed, met de rugleuning naar het raam (gericht naar het bed)
            var chairPos = new Vector3(ChairSideOffset, 0f, backZ - ChairFromBackWall);
            var chair = PlaceKenney("chair", furniture, "Chair", chairPos, 0f);
            OrientBack(chair, Vector3.right, chairPos);
            var seat = AdjustHeight(chair, chairPos, ChairSeatHeight, "zitvlak");

            PlaceKenney("pottedPlant", furniture, "Plant", new Vector3(-half + 0.35f, 0f, WallThickness / 2f + 0.5f), 0f);
            AddCeilingLamp(room.transform, "Lamp", new Vector3(0f, CeilingHeight, backZ / 2f));
            AddLight("Light", room.transform, new Vector3(0f, CeilingHeight - 0.4f, backZ / 2f), RoomLightRange, RoomLightIntensity);

            Anchor("Anchor_Laying", room.transform, new Vector3(bedCenter.x, mattress, bedCenter.z), Quaternion.identity);
            Anchor("Anchor_Sitting", room.transform, new Vector3(chairPos.x, seat, chairPos.z), Quaternion.LookRotation(Vector3.left));

            MarkStatic(room.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(room, RoomPrefabPath);
            Object.DestroyImmediate(room);
            return prefab;
        }

        private static void BuildWindow(Transform parent, Vector3 wallCenterAtFloor)
        {
            var window = new GameObject("Window").transform;
            window.SetParent(parent, false);
            var frame = Mat("WindowFrame", new Color(0.97f, 0.97f, 0.97f));
            var glass = Mat("WindowGlass", new Color(0.62f, 0.80f, 0.93f), 0f, 0.9f, new Color(0.25f, 0.33f, 0.40f));
            var height = WindowTop - WindowSill;
            var centerY = WindowSill + height / 2f;
            var x = wallCenterAtFloor.x;
            var z = wallCenterAtFloor.z;
            const float f = 0.06f;

            Box("Glass", window, new Vector3(x, centerY, z), new Vector3(0.03f, height, WindowWidth), glass);
            Box("Frame_Bottom", window, new Vector3(x - 0.02f, WindowSill + f / 2f, z), new Vector3(WallThickness + 0.06f, f, WindowWidth), frame);
            Box("Frame_Top", window, new Vector3(x, WindowTop - f / 2f, z), new Vector3(WallThickness + 0.02f, f, WindowWidth), frame);
            Box("Frame_Left", window, new Vector3(x, centerY, z - WindowWidth / 2f + f / 2f), new Vector3(WallThickness + 0.02f, height, f), frame);
            Box("Frame_Right", window, new Vector3(x, centerY, z + WindowWidth / 2f - f / 2f), new Vector3(WallThickness + 0.02f, height, f), frame);
            Box("Frame_Middle", window, new Vector3(x, centerY, z), new Vector3(0.05f, height, f * 0.6f), frame);
        }

        // --- Kenney-meubels: plaatsen, richten, hoogte --------------------------------------

        private static GameObject PlaceKenney(string model, Transform parent, string name, Vector3 footprintCenter, float yaw)
        {
            var path = $"{KenneyFolder}/{model}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new FileNotFoundException("Kenney-model niet gevonden", path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            AddMeshColliders(go);
            AlignToFloor(go, parent.TransformPoint(footprintCenter));
            return go;
        }

        /// <summary>Zet het object met zijn bounds-midden (x, z) op het doel en de onderkant op de hoogte van het doel.</summary>
        private static void AlignToFloor(GameObject go, Vector3 worldTarget)
        {
            var b = HospitalAssetPrep.WorldBounds(go);
            go.transform.position += new Vector3(worldTarget.x - b.center.x, worldTarget.y - b.min.y, worldTarget.z - b.center.z);
            Physics.SyncTransforms();
        }

        private static void AddMeshColliders(GameObject go)
        {
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.GetComponent<Collider>() == null)
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            Physics.SyncTransforms();
        }

        /// <summary>Hoogste punt van het object recht boven (x, z), via een raycast op de mesh colliders. NaN als er niets is.</summary>
        private static float TopAt(GameObject go, float x, float z)
        {
            Physics.SyncTransforms();
            var heights = Physics.RaycastAll(new Vector3(x, 100f, z), Vector3.down, 200f)
                .Where(h => h.collider.transform.IsChildOf(go.transform))
                .Select(h => h.point.y).ToList();
            return heights.Count == 0 ? float.NaN : heights.Max();
        }

        /// <summary>Draait het bed zodat het hoogste uiteinde (het hoofdeinde) naar +z wijst, tegen de achtermuur.</summary>
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

        /// <summary>Draait een stoel zodat de rugleuning (de hoogste kant) naar backDirection wijst.</summary>
        private static void OrientBack(GameObject chair, Vector3 backDirection, Vector3 worldCenter)
        {
            var b = HospitalAssetPrep.WorldBounds(chair);
            var sample = Mathf.Min(b.extents.x, b.extents.z) * 0.75f;
            var directions = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            var highest = directions
                .Select(d => (Dir: d, Top: TopAt(chair, b.center.x + d.x * sample, b.center.z + d.z * sample)))
                .Where(s => !float.IsNaN(s.Top))
                .OrderByDescending(s => s.Top)
                .FirstOrDefault();
            if (highest.Dir == Vector3.zero)
                return;
            chair.transform.Rotate(0f, Vector3.SignedAngle(highest.Dir, backDirection, Vector3.up), 0f, Space.World);
            AlignToFloor(chair, worldCenter);
        }

        /// <summary>
        /// Meet de hoogte van het bovenvlak in het midden en schaalt het object in de hoogte zodat die op target uitkomt.
        /// Geeft de uiteindelijke hoogte terug.
        /// </summary>
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
            var result = TopAt(go, b.center.x, b.center.z);
            Debug.Log($"{LogPrefix}{go.name}: {what} van {measured:0.000} m naar {result:0.000} m (schaal y {factor:0.000}).");
            return result;
        }

        // --- Bouwstenen ----------------------------------------------------------

        private static GameObject Box(string name, Transform parent, Vector3 localCenter, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>
        /// Muur langs x (alongX) of langs z, vanaf start (midden van de muurdikte, op vloerhoogte), met openingen.
        /// Openingen worden opgegeven als afstand langs de muur vanaf start; er komt een stuk onder (borstwering) en boven (latei).
        /// </summary>
        private static void Wall(string name, Transform parent, Vector3 start, bool alongX, float length,
            float bottom, float top, float thickness, Material mat, params Opening[] openings)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var axis = alongX ? Vector3.right : Vector3.forward;
            var index = 0;

            void Piece(float a, float b, float y0, float y1)
            {
                if (b - a < 0.001f || y1 - y0 < 0.001f)
                    return;
                var size = alongX ? new Vector3(b - a, y1 - y0, thickness) : new Vector3(thickness, y1 - y0, b - a);
                Box($"{name}_{index++}", root, start + axis * ((a + b) / 2f) + Vector3.up * ((y0 + y1) / 2f), size, mat);
            }

            var cursor = 0f;
            foreach (var o in openings.Where(o => o.Bottom < top && o.Top > bottom).OrderBy(o => o.Center))
            {
                var a = o.Center - o.Width / 2f;
                var b = o.Center + o.Width / 2f;
                Piece(cursor, a, bottom, top);
                Piece(a, b, bottom, Mathf.Max(bottom, o.Bottom));
                Piece(a, b, Mathf.Min(top, o.Top), top);
                cursor = b;
            }
            Piece(cursor, length, bottom, top);
        }

        private static void Sign(string name, Transform parent, string text, Vector3 wallPoint, Vector3 normal, Material background)
        {
            var sign = new GameObject(name).transform;
            sign.SetParent(parent, false);
            var depthAxis = new Vector3(Mathf.Abs(normal.x), 0f, Mathf.Abs(normal.z));
            var size = Vector3.Scale(depthAxis, Vector3.one * SignDepth) +
                       Vector3.Scale(Vector3.one - depthAxis, new Vector3(SignWidth, 0f, SignWidth)) + Vector3.up * SignHeight;
            Box("Panel", sign, wallPoint + normal * (SignDepth / 2f), size, background);
            Label("Text", sign, text, wallPoint + normal * (SignDepth + 0.005f), normal, SignWidth * 0.9f, SignHeight * 0.8f, Color.white);
        }

        private static void Label(string name, Transform parent, string text, Vector3 localPosition, Vector3 normal,
            float width, float height, Color color)
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
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
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

        private static void AddCeilingLamp(Transform parent, string name, Vector3 localCeilingPoint)
        {
            var lamp = PlaceKenney("lampSquareCeiling", parent, name, localCeilingPoint, 0f);
            var b = HospitalAssetPrep.WorldBounds(lamp);
            lamp.transform.position += Vector3.up * (parent.TransformPoint(localCeilingPoint).y - b.max.y);
            foreach (var c in lamp.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
        }

        private static void AddLight(string name, Transform parent, Vector3 localPosition, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = LightColor;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Statisch voor batching en later baked lighting, behalve deuren, liftdeuren en knoppen (die bewegen of reageren).
        /// Kamers (Room_*) worden overgeslagen: die zijn al statisch in de prefab.
        /// </summary>
        private static void MarkStatic(Transform t)
        {
            if (t.name.StartsWith("Door_") || t.name.StartsWith("LiftDoor") || t.name.StartsWith("LiftButton") || t.name.StartsWith("Room_"))
                return;
            if (t.GetComponent<Light>() == null)
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            foreach (Transform child in t)
                MarkStatic(child);
        }

        // --- Materialen, licht, lettertype --------------------------------------------------

        private static Material AccentMat(int floor) => Mat($"Accent_Floor{floor}", Floors[floor - 1].Color);

        /// <summary>Maakt of werkt een URP Lit materiaal bij in Assets/Hospital/Materials.</summary>
        private static Material Mat(string name, Color color, float metallic = 0f, float smoothness = 0.2f, Color? emission = null)
        {
            EnsureFolder(MaterialFolder);
            var path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(LitShader));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetupLighting(UnityEngine.SceneManagement.Scene scene, Transform root)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor;

            var sun = scene.GetRootGameObjects().Select(g => g.GetComponent<Light>())
                .FirstOrDefault(l => l != null && l.type == LightType.Directional);
            if (sun == null)
            {
                var go = new GameObject("Directional Light");
                go.transform.SetParent(root, false);
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.None;
            sun.intensity = 0.6f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>Lettertype voor de borden. Importeert de TMP Essential Resources (meegeleverd met Unity) als die ontbreken.</summary>
        private static TMP_FontAsset EnsureTmpFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (font != null)
                return font;

            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            var package = info == null ? null : Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (package != null && File.Exists(package))
            {
                AssetDatabase.ImportPackage(package, false);
                AssetDatabase.Refresh();
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            }
            if (font == null)
                Debug.LogWarning($"{LogPrefix}Geen TextMeshPro-lettertype: borden zonder tekst. " +
                                 "Importeer Window > TextMeshPro > Import TMP Essential Resources en draai Build opnieuw.");
            return font;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // --- Screenshots -----------------------------------------------------------

        private static void TakeScreenshots()
        {
            Directory.CreateDirectory(ScreenshotFolder);
            var floor1 = FloorHeight;
            Shot("hospital_corridor_floor1.png", new Vector3(0.7f, floor1 + EyeHeight, 1.1f), new Vector3(CorridorLength, floor1 + 1.3f, 1.9f));
            Shot("hospital_room_1A.png",
                new Vector3(DoorCentersX[0] - 1.4f, floor1 + EyeHeight, CorridorWidth + 0.5f),
                new Vector3(DoorCentersX[0] + 0.3f, floor1 + 0.6f, CorridorWidth + RoomDepth - 0.8f));
            Shot("hospital_lobby.png", new Vector3(LobbyLength - 1f, EyeHeight, LobbyMaxZ - 1f), new Vector3(0f, 1.2f, LiftCenterZ));
        }

        private static void Shot(string file, Vector3 position, Vector3 lookAt)
        {
            var go = new GameObject("ScreenshotCamera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = ScreenshotFov;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            HospitalAssetPrep.Capture(cam, $"{ScreenshotFolder}/{file}");
            Object.DestroyImmediate(go);
        }
    }
}
