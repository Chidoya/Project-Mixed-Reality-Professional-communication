using System.IO;
using System.Linq;
using ProCommVR.Hospital;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Bouwstenen voor de U-afdeling. Kopieën van de private hulpfuncties van HospitalBuilder (die blijft ongewijzigd),
    /// met muren en ramen in plancoördinaten. Materialen worden enkel geladen: de bestaande assets van liam_test_scene
    /// worden nooit overschreven.
    /// </summary>
    public static partial class UWardBuilder
    {
        private const string MaterialFolder = "Assets/Hospital/Materials";
        private const string KenneyFolder = "Assets/ThirdParty/Kenney/FurnitureKit";
        private const string LitShader = "Universal Render Pipeline/Lit";

        // Licht zoals in liam_test_scene (Quest 3: geen schaduwen, weinig lichten)
        private const float CorridorLightRange = 7f;
        private const float CorridorLightIntensity = 1.6f;
        private const float LobbyLightRange = 8f;
        private const float LobbyLightIntensity = 1.6f;
        private const float LightBelowCeiling = 0.3f;
        private static readonly Color LightColor = new Color(1f, 0.96f, 0.9f);
        private static readonly Color AmbientColor = new Color(0.42f, 0.43f, 0.46f);

        // Kleuren van liam_test_scene, enkel gebruikt als een materiaal ontbreekt
        private static readonly Color[] ZoneColors =
        {
            new Color(0.30f, 0.69f, 0.31f),
            new Color(0.96f, 0.60f, 0.10f),
            new Color(0.86f, 0.20f, 0.18f),
        };

        private static Material WallMat => Mat("Wall", new Color(0.95f, 0.94f, 0.91f));
        private static Material RoomWallMat => Mat("RoomWall", new Color(0.92f, 0.93f, 0.90f));
        private static Material FloorMat => Mat("Floor", new Color(0.72f, 0.75f, 0.78f));
        private static Material CeilingMat => Mat("Ceiling", new Color(0.93f, 0.93f, 0.92f));
        private static Material AccentMat(int zone) => Mat($"Accent_Floor{zone}", ZoneColors[zone - 1]);

        // --- Muren en ramen -------------------------------------------------------------

        /// <summary>
        /// Muur volgens een WallSpec, met een stuk onder (borstwering) en boven (latei) elke opening.
        /// Zelfde opbouw als HospitalBuilder.Wall, maar de lengte en openingen komen uit het plan.
        /// </summary>
        private static void Wall(Transform parent, WallSpec spec, Material mat)
        {
            var root = new GameObject(spec.Name).transform;
            root.SetParent(parent, false);
            var s = spec.Segment;
            // Langs het plan-x loopt de muur naar +X in Unity; langs het plan-y naar -Z, dus bouwen we vanaf To naar +Z.
            var start = s.Vertical ? ToUnity(s.At, s.To) : ToUnity(s.From, s.At);
            var axis = s.Vertical ? Vector3.forward : Vector3.right;
            float Along(float planCoord) => s.Vertical ? s.To - planCoord : planCoord - s.From;
            var index = 0;

            // Lange stukken worden opgedeeld (max. LightTileSize), zodat elk stuk de lampen in zijn buurt krijgt (zie BuildShell)
            void Piece(float a, float b, float y0, float y1)
            {
                if (b - a < 0.001f || y1 - y0 < 0.001f)
                    return;
                var parts = Mathf.CeilToInt((b - a) / LightTileSize);
                var step = (b - a) / parts;
                for (var p = 0; p < parts; p++)
                {
                    var (pa, pb) = (a + p * step, a + (p + 1) * step);
                    var size = s.Vertical ? new Vector3(spec.Thickness, y1 - y0, pb - pa) : new Vector3(pb - pa, y1 - y0, spec.Thickness);
                    Box($"{spec.Name}_{index++}", root, start + axis * ((pa + pb) / 2f) + Vector3.up * ((y0 + y1) / 2f), size, mat);
                }
            }

            var openings = OpeningsOn(spec)
                .Where(o => o.Bottom < spec.Top && o.Top > spec.Bottom)
                .Select(o => (A: Mathf.Min(Along(o.Segment.From), Along(o.Segment.To)), B: Mathf.Max(Along(o.Segment.From), Along(o.Segment.To)), o.Bottom, o.Top))
                .OrderBy(o => o.A);
            var cursor = 0f;
            foreach (var o in openings)
            {
                Piece(cursor, o.A, spec.Bottom, spec.Top);
                Piece(o.A, o.B, spec.Bottom, Mathf.Max(spec.Bottom, o.Bottom));
                Piece(o.A, o.B, Mathf.Min(spec.Top, o.Top), spec.Top);
                cursor = o.B;
            }
            Piece(cursor, s.Length, spec.Bottom, spec.Top);
        }

        /// <summary>Glas en kader in een raamopening, op de middellijn van de muur (zoals HospitalBuilder.BuildWindow).</summary>
        private static void BuildWindow(Transform parent, OpeningSpec opening, float wallAt, float wallThickness)
        {
            var window = new GameObject(opening.Name).transform;
            window.SetParent(parent, false);
            var frame = Mat("WindowFrame", new Color(0.97f, 0.97f, 0.97f));
            var glass = Mat("WindowGlass", new Color(0.62f, 0.80f, 0.93f), 0f, 0.9f, new Color(0.25f, 0.33f, 0.40f));
            var s = opening.Segment;
            var width = s.Length;
            var height = opening.Top - opening.Bottom;
            var centerY = opening.Bottom + height / 2f;
            var center = s.Vertical ? ToUnity(wallAt, s.Center) : ToUnity(s.Center, wallAt);
            const float f = 0.06f;

            // Maten in (dikte, hoogte, breedte); voor een muur langs het plan-x worden dikte en breedte omgewisseld.
            Vector3 Size(float depth, float h, float w) => s.Vertical ? new Vector3(depth, h, w) : new Vector3(w, h, depth);
            Vector3 Along(float offset) => (s.Vertical ? Vector3.forward : Vector3.right) * offset;

            Box("Glass", window, center + Vector3.up * centerY, Size(0.03f, height, width), glass);
            Box("Frame_Bottom", window, center + Vector3.up * (opening.Bottom + f / 2f), Size(wallThickness + 0.06f, f, width), frame);
            Box("Frame_Top", window, center + Vector3.up * (opening.Top - f / 2f), Size(wallThickness + 0.02f, f, width), frame);
            Box("Frame_Left", window, center + Vector3.up * centerY + Along(-width / 2f + f / 2f), Size(wallThickness + 0.02f, height, f), frame);
            Box("Frame_Right", window, center + Vector3.up * centerY + Along(width / 2f - f / 2f), Size(wallThickness + 0.02f, height, f), frame);
            Box("Frame_Middle", window, center + Vector3.up * centerY, Size(0.05f, height, f * 0.6f), frame);
        }

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

        // --- Kenney-meubels ---------------------------------------------------------------

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

        // --- Licht ------------------------------------------------------------------------

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

        /// <summary>Lamp aan het plafond met een puntlicht 30 cm eronder, zoals in de gangen van liam_test_scene.</summary>
        private static void CeilingLight(Transform parent, string suffix, float x, float y, float range, float intensity)
        {
            var ceiling = ToUnity(x, y, CeilingHeight);
            AddCeilingLamp(parent, $"Lamp_{suffix}", ceiling);
            AddLight($"Light_{suffix}", parent, ceiling + Vector3.down * LightBelowCeiling, range, intensity);
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

        /// <summary>
        /// Statisch voor batching en later baked lighting, behalve deuren, liftdeuren, knoppen en de klok (die bewegen of reageren).
        /// </summary>
        private static void MarkStatic(Transform t)
        {
            if (t.name.StartsWith("Door_") || t.name.StartsWith("LiftDoor") || t.name.StartsWith("LiftButton") ||
                t.GetComponent<WallClock>() != null)
                return;
            if (t.GetComponent<Light>() == null)
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            foreach (Transform child in t)
                MarkStatic(child);
        }

        // --- Materialen -------------------------------------------------------------------

        /// <summary>
        /// Laadt een materiaal uit Assets/Hospital/Materials. Bestaat het niet, dan wordt het aangemaakt met deze waarden;
        /// een bestaand materiaal wordt nooit aangepast (het hoort ook bij liam_test_scene).
        /// </summary>
        private static Material Mat(string name, Color color, float metallic = 0f, float smoothness = 0.2f, Color? emission = null)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;

            EnsureFolder(MaterialFolder);
            mat = new Material(Shader.Find(LitShader));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
