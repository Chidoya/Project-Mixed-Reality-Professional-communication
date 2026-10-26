using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProCommVR.Hospital;
using UnityEditor;
using UnityEngine;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Zet de low-poly props uit Blender klaar (Assets/Hospital/Art/Props/&lt;Naam&gt;.fbx, gemaakt door blender/scripts/06_props.py):
    /// schaal 1, flat shading uit Blender, gedeelde URP Lit materialen en een prefab per prop.
    /// Zelfde aanpak als CharacterSetup. Mag opnieuw gedraaid worden.
    /// </summary>
    public static class PropSetup
    {
        private const string Root = "Assets/Hospital/Art/Props";
        private const string MaterialFolder = Root + "/Materials";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string LogPrefix = "[PropSetup] ";

        // Afwerking per materiaal uit 06_props.py (metallic, smoothness); de rest is mat (0, 0.2) zoals de personages
        private static readonly Dictionary<string, (float Metallic, float Smoothness)> Finish = new Dictionary<string, (float, float)>
        {
            { "Prop_Metal", (0.8f, 0.65f) },
            { "Prop_Silver", (1f, 0.8f) },
            { "Prop_PillWhite", (0f, 0.9f) },
            { "Prop_Glass", (0f, 0.9f) },
            { "Prop_Water", (0f, 0.95f) },
            { "Prop_Screen", (0f, 0.8f) },
            { "Prop_Lamp", (0f, 0.7f) },
        };

        // Stand van de wijzers in de FBX (06_props.py zet de klok op 10:10:30), om de draaias te meten
        private const float ExportedMinuteAngle = 60f;
        private const float ExportedHourAngle = 305f;

        [MenuItem("ProCommVR/Hospital/Setup Props")]
        public static void Setup()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            var count = 0;
            foreach (var fbxPath in FindModels())
            {
                var name = Path.GetFileNameWithoutExtension(fbxPath);
                ConfigureModelImporter(fbxPath);
                ConvertMaterials(fbxPath);
                BuildPrefab(fbxPath, name);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix}Klaar: {count} props in {PrefabFolder}.");
        }

        /// <summary>Draait Setup enkel als er een prefab ontbreekt (bv. vanuit HospitalBuilder.Build).</summary>
        public static void EnsureSetup()
        {
            if (FindModels().Any(p => LoadPrefab(Path.GetFileNameWithoutExtension(p)) == null))
                Setup();
        }

        public static GameObject LoadPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");

        /// <summary>Voor de command line: Unity -batchmode -executeMethod ProCommVR.Editor.PropSetup.RunBatch</summary>
        public static void RunBatch()
        {
            try
            {
                Setup();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError(LogPrefix + e);
                EditorApplication.Exit(1);
            }
        }

        private static IEnumerable<string> FindModels()
        {
            return AssetDatabase.FindAssets("t:Model", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == Root)
                .OrderBy(p => p);
        }

        private static void ConfigureModelImporter(string fbxPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importNormals = ModelImporterNormals.Import; // flat shading uit Blender behouden
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Eén URP Lit materiaal per naam (Prop_White, Prop_Metal, ...), gedeeld door alle props, met de effen kleur
        /// uit de FBX. Gekoppeld via een remap; bestaande materialen worden bijgewerkt.
        /// </summary>
        private static void ConvertMaterials(string fbxPath)
        {
            var embedded = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().ToList();
            if (embedded.Count == 0)
                return;

            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            var shader = Shader.Find(LitShader);
            foreach (var source in embedded)
            {
                var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                var path = $"{MaterialFolder}/{source.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                var (metallic, smoothness) = Finish.TryGetValue(source.name, out var f) ? f : (0f, 0.2f);
                mat.shader = shader;
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
                if (source.name == "Prop_Lamp")
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(mat);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), mat);
            }
            importer.SaveAndReimport();
        }

        private static void BuildPrefab(string fbxPath, string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name;
            try
            {
                if (name == "Wall_Clock")
                    SetupClock(instance);
                if (name == "Supply_Cart" || name == "Gurney")
                    AddBoxCollider(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabFolder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Meet rond welke as de wijzers draaien na de FBX-conversie en zet ze op 12 uur, met een WallClock die ze bestuurt.
        /// In Blender draaien ze rond hun lokale Y-as; welke Unity-as dat wordt, hangt af van de conversie.
        /// </summary>
        private static void SetupClock(GameObject clock)
        {
            var hour = clock.transform.Find("Hand_Hour");
            var minute = clock.transform.Find("Hand_Minute");
            var second = clock.transform.Find("Hand_Second");
            if (hour == null || minute == null || second == null)
            {
                Debug.LogError($"{LogPrefix}Wall_Clock: wijzers Hand_Hour, Hand_Minute en Hand_Second niet gevonden.");
                return;
            }

            minute.localRotation.ToAngleAxis(out var angle, out var axis);
            if (angle > 180f)
            {
                angle = 360f - angle;
                axis = -axis;
            }
            // Minutenwijzer staat op 60 graden met de klok mee: dan is dit de as "met de klok mee"
            var clockwise = Mathf.Abs(angle - ExportedMinuteAngle) < 1f ? axis : -axis;
            var rest = Quaternion.AngleAxis(-ExportedMinuteAngle, clockwise) * minute.localRotation;
            var hourRest = Quaternion.AngleAxis(-ExportedHourAngle, clockwise) * hour.localRotation;
            var front = Vector3.Scale(minute.position - clock.transform.position, new Vector3(1f, 0f, 1f)).normalized;
            Debug.Log($"{LogPrefix}Wall_Clock: wijzers draaien met de klok mee rond {Round(clockwise)} in de ruimte van de klok " +
                      $"(wereld {Round(clock.transform.rotation * clockwise)}), ruststand {Round(rest.eulerAngles)}, " +
                      $"uurwijzer wijkt {Quaternion.Angle(rest, hourRest):0.0} graden af. Root-rotatie van de prefab " +
                      $"{Round(clock.transform.rotation.eulerAngles)}, voorkant van de props naar {Round(front)}.");

            var wallClock = clock.AddComponent<WallClock>();
            wallClock.Configure(hour, minute, second, clockwise, rest);
            foreach (var hand in new[] { hour, minute, second })
                hand.localRotation = rest;
        }

        private static void AddBoxCollider(GameObject go)
        {
            var bounds = HospitalAssetPrep.WorldBounds(go);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size;
        }

        private static string Round(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";

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
