using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Bouwt de U-afdeling (één verdieping, 42 x 28 m, docs/Plattegrond/maten.md) in Assets/Scenes/liam_u_afdeling.unity,
    /// met dezelfde assets en materialen als liam_test_scene. HospitalBuilder en liam_test_scene blijven ongewijzigd.
    /// Floor1, Floor2 en Floor3 zijn zones op dezelfde vloer (linkerarm, onderste rij, rechterarm), zodat de namen
    /// die Jalils shift opzoekt blijven bestaan. Mag opnieuw gedraaid worden: de root "Hospital" wordt telkens opnieuw opgebouwd.
    /// </summary>
    public static partial class UWardBuilder
    {
        private const string ScenePath = "Assets/Scenes/liam_u_afdeling.unity";
        private const string ScreenshotFolder = "docs/Screenshots";
        private const string RootName = "Hospital";
        private const string LogPrefix = "[UWardBuilder] ";
        private const float LightTileSize = 6f;

        private static readonly string[] Groups = { "Structure", "Floor1", "Floor2", "Floor3", "Corridors", "Core", "Lobby" };

        [MenuItem("ProCommVR/Hospital/Build U-Afdeling")]
        public static void Build()
        {
            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (old != null)
                Object.DestroyImmediate(old);
            // Opruimen: een eerdere versie hing de alarmlamp van de gesprekskamer los bovenaan de scene
            foreach (var stray in scene.GetRootGameObjects().Where(g => g.GetComponent<ProCommVR.Hospital.AlarmLamp>() != null))
                Object.DestroyImmediate(stray);

            var root = new GameObject(RootName).transform;
            var groups = Groups.ToDictionary(g => g, g =>
            {
                var t = new GameObject(g).transform;
                t.SetParent(root, false);
                return t;
            });

            BuildShell(groups);
            BuildCorridorLights(groups);
            BuildRooms(groups);
            BuildCore(groups);
            BuildLifts(groups["Lobby"]);
            BuildStairwell(groups["Core"]);
            // Scenario 2 na de kamers en de kern (de gesprekskamer moet bestaan)
            BuildPreOpRoom(root);
            BuildScenario2(root);
            BuildAlarmLights(root);
            BuildGuideLights(groups);
            BuildTeleportAreas(root, groups["Lobby"]);

            SetupLighting(scene, root);
            MarkStatic(root);
            // Na MarkStatic: de personages bewegen (Animator) en mogen niet static zijn
            PlaceCharacters(root);
            LogPerformance(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            // De personages tonen hun houding via EditorPosePreview (geen T-pose in de screenshots)
            TakeTopScreenshot(root);
            TakeRoomScreenshots(root);
            TakeCoreScreenshots();
            TakeFamilyRoomScreenshots();
            TakeLiftScreenshots();
            TakeCharacterScreenshots(root);
            TakeFixScreenshots(root);
            TakePreOpScreenshots(root);
            TakeScenario2Screenshots(root);
            TakeGurneyScreenshots(root);
            Debug.Log($"{LogPrefix}Klaar: {ScenePath}, screenshot in {ScreenshotFolder}/.");
        }

        /// <summary>Voor de command line: Unity -batchmode -executeMethod ProCommVR.Editor.UWardBuilder.RunBatch</summary>
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

        // --- Casco --------------------------------------------------------------------

        /// <summary>Vloer, plafond, alle muren met hun openingen, ramen en de accentstrepen in de gangen.</summary>
        private static void BuildShell(Dictionary<string, Transform> groups)
        {
            var structure = groups["Structure"];
            var center = ToUnity(Width / 2f, Depth / 2f);
            // Zelfde opbouw als liam_test_scene: een plaat met daarop een vloerafwerking van 1 cm, bovenkant op Y = 0.
            Box("Slab", structure, center + Vector3.down * (FloorFinishThickness + SlabThickness / 2f),
                new Vector3(Width, SlabThickness, Depth), CeilingMat);
            // Vloerafwerking en plafond in tegels: op de Quest (Mobile_Renderer, Forward) krijgt elk object maar 4 extra lichten,
            // dus één vloer van 42 x 28 m zou door slechts 4 van alle lampen belicht worden.
            var floor = new GameObject("FloorFinish").transform;
            floor.SetParent(structure, false);
            var ceiling = new GameObject("Ceiling").transform;
            ceiling.SetParent(structure, false);
            var columns = Mathf.CeilToInt(Width / LightTileSize);
            var rows = Mathf.CeilToInt(Depth / LightTileSize);
            var tile = new Vector2(Width / columns, Depth / rows);
            for (var i = 0; i < columns; i++)
                for (var j = 0; j < rows; j++)
                {
                    var c = ToUnity((i + 0.5f) * tile.x, (j + 0.5f) * tile.y);
                    Box($"FloorFinish_{i}_{j}", floor, c + Vector3.down * (FloorFinishThickness / 2f),
                        new Vector3(tile.x, FloorFinishThickness, tile.y), FloorMat);
                    Box($"Ceiling_{i}_{j}", ceiling, c + Vector3.up * (CeilingHeight + SlabThickness / 2f),
                        new Vector3(tile.x, SlabThickness, tile.y), CeilingMat);
                }

            var windows = new GameObject("Windows").transform;
            windows.SetParent(structure, false);
            var walls = Walls().ToList();
            foreach (var wall in walls)
            {
                Wall(groups[wall.Parent], wall, wall.Material == "RoomWall" ? RoomWallMat : WallMat);
                foreach (var window in OpeningsOn(wall).Where(o => o.Kind == OpeningKind.Window))
                    BuildWindow(windows, window, wall.Segment.At, wall.Thickness);
            }
            foreach (var (stripe, zone) in Stripes())
                Wall(groups[stripe.Parent], stripe, AccentMat(zone));

            CheckOpenings(walls.Concat(PatientRooms.SelectMany(BathroomWalls)).ToList());
        }

        /// <summary>Meldt openingen die op geen enkele muur vallen (een tikfout in UWardLayout).</summary>
        private static void CheckOpenings(List<WallSpec> walls)
        {
            var placed = walls.SelectMany(OpeningsOn).Select(o => o.Name).ToHashSet();
            foreach (var missing in Openings().Where(o => !placed.Contains(o.Name)))
                Debug.LogWarning($"{LogPrefix}{missing.Name} ligt op geen enkele muur.");
            Debug.Log($"{LogPrefix}{walls.Count} muren, {placed.Count} openingen.");
        }

        /// <summary>Ganglichten onder hun zone (Floor{n}/Light_3, ...) en lichten in de hal, zoals in liam_test_scene.</summary>
        private static void BuildCorridorLights(Dictionary<string, Transform> groups)
        {
            foreach (var (zone, suffix, x, y) in CorridorLights())
                CeilingLight(groups[$"Floor{zone}"], suffix, x, y, CorridorLightRange, CorridorLightIntensity);
            for (var i = 0; i < HallLights.Length; i++)
                CeilingLight(groups["Lobby"], $"{i + 1}", HallLights[i].x, HallLights[i].y, LobbyLightRange, LobbyLightIntensity);
        }

        // --- Screenshots --------------------------------------------------------------

        /// <summary>
        /// Bovenaanzicht van de hele vloer met een orthografische camera, als een plattegrond: alles wat boven
        /// SectionHeight begint (plafond, lampen, lateien boven deuren en ramen) is even verborgen, zodat de openingen zichtbaar zijn.
        /// </summary>
        private static void TakeTopScreenshot(Transform root)
        {
            const float sectionHeight = 1.5f;
            Directory.CreateDirectory(ScreenshotFolder);
            var hidden = root.GetComponentsInChildren<Renderer>()
                .Where(r => r.bounds.min.y > sectionHeight)
                .Select(r => r.gameObject).ToList();
            hidden.ForEach(g => g.SetActive(false));

            var go = new GameObject("ScreenshotCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Depth / 2f + 1f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                go.transform.SetPositionAndRotation(new Vector3(0f, 50f, 0f), Quaternion.Euler(90f, 0f, 0f));
                HospitalAssetPrep.Capture(cam, $"{ScreenshotFolder}/u_afdeling_top.png");
            }
            finally
            {
                Object.DestroyImmediate(go);
                hidden.ForEach(g => g.SetActive(true));
            }
        }
    }
}
