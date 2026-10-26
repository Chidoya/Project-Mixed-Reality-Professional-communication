using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Maakt de interieur-assets in Assets/ThirdParty klaar voor de Quest 3:
    /// importinstellingen (schaal, as-conversie), URP Lit materialen, texture-limieten met Android-compressie,
    /// een eigen materiaal voor de Hospital Door, een testscene met screenshots en een rapport per asset.
    /// Mag opnieuw gedraaid worden. Bestaande materialen worden hergebruikt (behalve die van de deur).
    /// </summary>
    public static class HospitalAssetPrep
    {
        private const string ThirdPartyRoot = "Assets/ThirdParty";
        private const string CheckScenePath = "Assets/Scenes/liam_asset_check.unity";
        private const string ScreenshotFolder = "docs/Screenshots";
        private const string ReportPath = "docs/asset_check_report.md";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string LogPrefix = "[HospitalAssetPrep] ";

        // Texturen
        private const int LargeTextureSize = 1024;  // grote vlakken (muren, vloer) en het patiëntenbed
        private const int SmallTextureSize = 512;   // kleine props
        private const int UnusedTextureSize = 256;  // AO/metallic/roughness/normal: niet gebruikt in de materialen
        private const TextureImporterFormat AndroidFormat = TextureImporterFormat.ASTC_6x6;
        private const float AstcBitsPerPixel = 128f / 36f;

        // Schaal: wijkt een model meer dan dit af van de verwachte maat, dan wordt het geschaald
        private const float ScaleTolerance = 0.2f;

        // Testscene
        private const float LayoutGap = 1f;
        private const int ScreenshotWidth = 1920;
        private const int ScreenshotHeight = 1080;
        private const float CameraFov = 40f;

        private static readonly Color DoorColor = new Color(0.82f, 0.83f, 0.85f);
        private static readonly Color DoorHandleColor = new Color(0.25f, 0.25f, 0.27f);

        private static readonly string[] UnusedTextureSuffixes =
            { "_ao", "_metallic", "_roughness", "_normal", "_opacity", "_emissive", "_r", "_n" };
        private static readonly string[] AlbedoSuffixes = { "", "_albedo", "_t", "_basecolor", "_diffuse", "_color" };
        private static readonly string[] HandleNames = { "handle", "knob", "klink", "metal", "chrome" };
        private static readonly string[] TransparentNames = { "glass", "transparent" };

        private static readonly string[] KenneyShowcase =
            { "bedSingle", "cabinetBed", "chair", "televisionModern", "wallWindow", "wallDoorway" };
        private static readonly string[] ReferenceCharacters =
            { "Assets/Character/Patient_Male/Patient_Male.prefab", "Assets/Character/Nurse_Female_Scrubs/Nurse_Female_Scrubs.prefab" };

        private class Pack
        {
            public string Name;
            public string Folder;               // relatief t.o.v. ThirdPartyRoot
            public float FixedScale;            // > 0: vaste schaal, anders meten
            public string ReferenceModel;       // model waarop gemeten wordt (null = eerste model)
            public float ExpectedLongest;       // verwachte langste zijde in meter
            public string[] LargeTexturePrefixes; // null = alle gebruikte texturen groot
            public bool AllLarge;
        }

        private static readonly Pack[] Packs =
        {
            new Pack { Name = "Hospital Room (UgurYksk)", Folder = "Sketchfab/HospitalRoom_UgurYksk", FixedScale = 1f,
                       LargeTexturePrefixes = new[] { "wall", "ground", "doorwall", "windowwall", "bed" } },
            new Pack { Name = "GRADD Hospital Room", Folder = "GRADD/HospitalRoom", FixedScale = 0.0254f }, // SketchUp-export in inches
            new Pack { Name = "Hospital Bed (ansh_artis)", Folder = "Sketchfab/HospitalBed_anshartis", ExpectedLongest = 2.1f, AllLarge = true },
            new Pack { Name = "Hospital Door (anasrar)", Folder = "Sketchfab/HospitalDoor_anasrar", ExpectedLongest = 2.1f },
            new Pack { Name = "Kenney Furniture Kit", Folder = "Kenney/FurnitureKit", ReferenceModel = "bedSingle", ExpectedLongest = 2f },
        };

        private class PackResult
        {
            public Pack Pack;
            public float Scale;
            public string ScaleNote;
            public List<string> Models = new List<string>();
            public HashSet<Texture2D> UsedTextures = new HashSet<Texture2D>();
        }

        [MenuItem("ProCommVR/Hospital/Prepare Assets")]
        public static void Prepare()
        {
            var results = new List<PackResult>();
            foreach (var pack in Packs)
            {
                var folder = $"{ThirdPartyRoot}/{pack.Folder}";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"{LogPrefix}Map ontbreekt, overgeslagen: {folder}");
                    continue;
                }
                results.Add(PreparePack(pack, folder));
            }

            AssetDatabase.SaveAssets();
            var scene = BuildCheckScene(results);
            WriteReport(results, scene);
            AssetDatabase.Refresh();
            Debug.Log($"{LogPrefix}Klaar. Rapport: {ReportPath}, screenshots in {ScreenshotFolder}/.");
        }

        /// <summary>Voor de command line: Unity -batchmode -executeMethod ProCommVR.Editor.HospitalAssetPrep.RunBatch</summary>
        public static void RunBatch()
        {
            try
            {
                Prepare();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError(LogPrefix + e);
                EditorApplication.Exit(1);
            }
        }

        // --- Per pack ------------------------------------------------------------

        private static PackResult PreparePack(Pack pack, string folder)
        {
            var result = new PackResult { Pack = pack };
            result.Models = AssetDatabase.FindAssets("t:Model", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            if (result.Models.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix}{pack.Name}: geen modellen gevonden.");
                return result;
            }

            result.Scale = DetermineScale(pack, result.Models, out result.ScaleNote);
            foreach (var model in result.Models)
                ConfigureModelImporter(model, result.Scale);

            var textures = FindTextures(folder);
            foreach (var texture in textures.Values)
                ConfigureTexture(texture, pack);

            var materialFolder = EnsureFolder(folder, "Materials");
            var converted = new Dictionary<string, Material>();
            foreach (var model in result.Models)
                ConvertMaterials(model, materialFolder, textures, converted);

            if (pack.Folder.Contains("HospitalDoor"))
                ApplyDoorMaterials(result.Models);

            foreach (var model in result.Models)
                foreach (var mat in ModelMaterials(model))
                    if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") is Texture2D t)
                        result.UsedTextures.Add(t);

            Debug.Log($"{LogPrefix}{pack.Name}: {result.Models.Count} model(len), schaal {result.Scale:0.####} ({result.ScaleNote}).");
            return result;
        }

        private static float DetermineScale(Pack pack, List<string> models, out string note)
        {
            if (pack.FixedScale > 0f)
            {
                note = "vaste schaal";
                return pack.FixedScale;
            }

            var reference = pack.ReferenceModel == null
                ? models[0]
                : models.FirstOrDefault(m => Path.GetFileNameWithoutExtension(m) == pack.ReferenceModel) ?? models[0];
            ConfigureModelImporter(reference, 1f);
            var size = MeasureModel(AssetDatabase.LoadAssetAtPath<GameObject>(reference)).size;
            var longest = Mathf.Max(size.x, size.y, size.z);
            if (longest <= 0f)
            {
                note = "niet meetbaar";
                return 1f;
            }

            var factor = pack.ExpectedLongest / longest;
            if (Mathf.Abs(factor - 1f) <= ScaleTolerance)
            {
                note = $"{Path.GetFileNameWithoutExtension(reference)} is al {longest:0.00} m";
                return 1f;
            }
            note = $"{Path.GetFileNameWithoutExtension(reference)} was {longest:0.###} eenheden, geschaald naar {pack.ExpectedLongest} m";
            return factor;
        }

        private static void ConfigureModelImporter(string path, float scale)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = scale;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = false;
            importer.generateSecondaryUV = true; // voor baked lighting later
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();
        }

        // --- Texturen ------------------------------------------------------------

        private static Dictionary<string, string> FindTextures(string folder)
        {
            var result = new Dictionary<string, string>();
            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath))
                result[Path.GetFileNameWithoutExtension(path).ToLowerInvariant()] = path;
            return result;
        }

        private static bool IsUnusedMap(string name) => UnusedTextureSuffixes.Any(s => name.EndsWith(s));

        private static void ConfigureTexture(string path, Pack pack)
        {
            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            int size;
            if (IsUnusedMap(name))
                size = UnusedTextureSize;
            else if (pack.AllLarge || (pack.LargeTexturePrefixes != null && pack.LargeTexturePrefixes.Any(name.StartsWith)))
                size = LargeTextureSize;
            else
                size = SmallTextureSize;

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = size;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = size;
            android.format = AndroidFormat;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        // --- Materialen ----------------------------------------------------------

        /// <summary>
        /// Maakt voor elk ingebed materiaal een URP Lit materiaal (gedeeld per pack, op naam) en koppelt het via een remap.
        /// Ontbreekt de texture in het ingebedde materiaal, dan wordt ze op naam gezocht (bv. Wall1 -> Wall1_albedo.jpg).
        /// </summary>
        private static void ConvertMaterials(string modelPath, string materialFolder,
            Dictionary<string, string> textures, Dictionary<string, Material> converted)
        {
            var embedded = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToList();
            if (embedded.Count == 0)
                return;

            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            var shader = Shader.Find(LitShader);
            foreach (var source in embedded)
            {
                var key = CleanName(source.name);
                if (!converted.TryGetValue(key, out var mat))
                {
                    var path = $"{materialFolder}/{key}.mat";
                    mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null)
                    {
                        mat = new Material(shader);
                        FillFromSource(mat, source, key, textures);
                        AssetDatabase.CreateAsset(mat, path);
                    }
                    converted[key] = mat;
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), mat);
            }
            importer.SaveAndReimport();
        }

        private static void FillFromSource(Material mat, Material source, string key, Dictionary<string, string> textures)
        {
            var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap")
                : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            if (texture == null)
                texture = FindAlbedo(key, textures);

            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", texture != null ? new Color(1f, 1f, 1f, color.a) : color);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetFloat("_Metallic", 0f);

            var lower = key.ToLowerInvariant();
            if (color.a < 0.99f || TransparentNames.Any(lower.Contains))
                MakeTransparent(mat, color.a < 0.99f ? color.a : 0.35f);
            EditorUtility.SetDirty(mat);
        }

        private static Texture2D FindAlbedo(string key, Dictionary<string, string> textures)
        {
            var name = key.ToLowerInvariant();
            foreach (var suffix in AlbedoSuffixes)
                if (textures.TryGetValue(name + suffix, out var path))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return null;
        }

        private static void MakeTransparent(Material mat, float alpha)
        {
            var c = mat.GetColor("_BaseColor");
            mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, alpha));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>De deur heeft geen texturen: lichtgrijs, en donkergrijs voor alles wat op een klink lijkt.</summary>
        private static void ApplyDoorMaterials(List<string> models)
        {
            foreach (var model in models)
                foreach (var mat in ModelMaterials(model))
                {
                    var handle = HandleNames.Any(mat.name.ToLowerInvariant().Contains);
                    mat.SetTexture("_BaseMap", null);
                    mat.SetColor("_BaseColor", handle ? DoorHandleColor : DoorColor);
                    mat.SetFloat("_Metallic", handle ? 0.6f : 0f);
                    mat.SetFloat("_Smoothness", handle ? 0.5f : 0.2f);
                    EditorUtility.SetDirty(mat);
                }
        }

        private static IEnumerable<Material> ModelMaterials(string modelPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            return prefab == null
                ? Enumerable.Empty<Material>()
                : prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct();
        }

        private static string CleanName(string name)
        {
            if (name.EndsWith("-material"))
                name = name.Substring(0, name.Length - "-material".Length);
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        // --- Metingen ------------------------------------------------------------

        private static Bounds MeasureModel(GameObject prefab)
        {
            var instance = Object.Instantiate(prefab);
            try
            {
                return WorldBounds(instance);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        internal static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static long CountTriangles(GameObject prefab)
        {
            long total = 0;
            var meshes = prefab.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => s.sharedMesh))
                .Where(m => m != null);
            foreach (var mesh in meshes)
                for (var i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles)
                        total += mesh.GetIndexCount(i) / 3;
            return total;
        }

        private static float EstimateAndroidMegabytes(IEnumerable<Texture2D> textures) =>
            textures.Sum(t => t.width * t.height * AstcBitsPerPixel / 8f * 4f / 3f) / (1024f * 1024f);

        // --- Testscene en screenshots ------------------------------------------------

        private class SceneItem
        {
            public string Label;
            public GameObject Instance;
        }

        private static List<SceneItem> BuildCheckScene(List<PackResult> results)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var items = new List<SceneItem>();
            var root = new GameObject("AssetCheck").transform;

            foreach (var result in results)
            {
                var models = result.Pack.Folder.StartsWith("Kenney")
                    ? KenneyShowcase.Select(n => result.Models.FirstOrDefault(m => Path.GetFileNameWithoutExtension(m) == n)).Where(m => m != null)
                    : result.Models;
                foreach (var model in models)
                    items.Add(new SceneItem
                    {
                        Label = $"{result.Pack.Name}: {Path.GetFileNameWithoutExtension(model)}",
                        Instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model), root),
                    });
            }
            foreach (var path in ReferenceCharacters)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"{LogPrefix}Personage niet gevonden: {path}");
                    continue;
                }
                items.Add(new SceneItem { Label = $"Referentie: {prefab.name}", Instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root) });
            }

            // Naast elkaar op de grond, voorkant op z = 0
            var cursor = 0f;
            foreach (var item in items)
            {
                var b = WorldBounds(item.Instance);
                item.Instance.transform.position += new Vector3(cursor - b.min.x, -b.min.y, -b.min.z);
                cursor += b.size.x + LayoutGap;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(cursor / 2f, -0.01f, 5f);
            ground.transform.localScale = new Vector3(cursor / 10f + 2f, 1f, 3f);

            Directory.CreateDirectory(ScreenshotFolder);
            var all = items.Select(i => WorldBounds(i.Instance)).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var overview = CreateCamera("Overview Camera", all, 30f);
            Capture(overview, $"{ScreenshotFolder}/asset_check.png");

            // Close-up: alles behalve de twee volledige kamers, voor de hoogtevergelijking met de personages
            var small = items.Where(i => !i.Label.StartsWith("Hospital Room") && !i.Label.StartsWith("GRADD")).ToList();
            if (small.Count > 0)
            {
                var closeBounds = small.Select(i => WorldBounds(i.Instance)).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var close = CreateCamera("Closeup Camera", closeBounds, 12f);
                Capture(close, $"{ScreenshotFolder}/asset_check_closeup.png");
                close.gameObject.SetActive(false);
            }

            EditorSceneManager.SaveScene(scene, CheckScenePath);
            return items;
        }

        private static Camera CreateCamera(string name, Bounds target, float pitch)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = CameraFov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.89f, 0.93f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;

            var aspect = (float)ScreenshotWidth / ScreenshotHeight;
            var halfV = CameraFov * 0.5f * Mathf.Deg2Rad;
            var halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
            var distance = Mathf.Max(target.extents.x / Mathf.Tan(halfH), target.extents.y / Mathf.Tan(halfV)) * 1.1f + target.extents.z;

            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            go.transform.SetPositionAndRotation(target.center - rotation * Vector3.forward * distance, rotation);
            return cam;
        }

        internal static void Capture(Camera cam, string path)
        {
            var rt = new RenderTexture(ScreenshotWidth, ScreenshotHeight, 24);
            var previous = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(ScreenshotWidth, ScreenshotHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, ScreenshotWidth, ScreenshotHeight), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        // --- Rapport -------------------------------------------------------------

        private static void WriteReport(List<PackResult> results, List<SceneItem> sceneItems)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("# Asset check (gegenereerd)");
            sb.AppendLine();
            sb.AppendLine("Gegenereerd door `ProCommVR > Hospital > Prepare Assets` (`Assets/Hospital/Editor/HospitalAssetPrep.cs`).");
            sb.AppendLine($"Texture-limieten: {LargeTextureSize} (grote vlakken, bed), {SmallTextureSize} (props), {UnusedTextureSize} (niet gebruikte maps). Android: {AndroidFormat}.");
            sb.AppendLine("Geheugen = schatting voor Android (ASTC 6x6 met mipmaps), enkel texturen die in een materiaal zitten.");
            sb.AppendLine();
            sb.AppendLine("## Per asset");
            sb.AppendLine();
            sb.AppendLine("| Asset | Modellen | Driehoeken | Materialen | Texturen in gebruik | Grootte na import | Geheugen (Android) | Afmetingen (m) | Schaal |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");

            foreach (var r in results)
            {
                var prefabs = r.Models.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToList();
                var tris = prefabs.Sum(CountTriangles);
                var materials = r.Models.SelectMany(ModelMaterials).Distinct().Count();
                var sizes = r.UsedTextures.GroupBy(t => $"{t.width}x{t.height}").OrderByDescending(g => g.Key)
                    .Select(g => $"{g.Count()}x {g.Key}");
                var dims = prefabs.Count == 1
                    ? FormatSize(MeasureModel(prefabs[0]).size, inv)
                    : $"{prefabs.Count} modellen";
                var trisText = prefabs.Count > 1
                    ? $"{tris:N0} totaal, gem. {tris / prefabs.Count:N0}/model"
                    : $"{tris:N0}";
                sb.AppendLine($"| {r.Pack.Name} | {prefabs.Count} | {trisText} | {materials} | {r.UsedTextures.Count} | " +
                              $"{(r.UsedTextures.Count == 0 ? "geen" : string.Join(", ", sizes))} | " +
                              $"{EstimateAndroidMegabytes(r.UsedTextures).ToString("0.0", inv)} MB | {dims} | " +
                              $"{r.Scale.ToString("0.####", inv)} ({r.ScaleNote}) |");
            }

            sb.AppendLine();
            sb.AppendLine("## In de testscene");
            sb.AppendLine();
            sb.AppendLine($"Scene: `{CheckScenePath}`. Screenshots: `{ScreenshotFolder}/asset_check.png` en `{ScreenshotFolder}/asset_check_closeup.png`.");
            sb.AppendLine();
            sb.AppendLine("| Object | Driehoeken | Materialen | Afmetingen (m, b x h x d) |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var item in sceneItems)
            {
                var mats = item.Instance.GetComponentsInChildren<Renderer>(true).SelectMany(x => x.sharedMaterials).Where(m => m != null).Distinct().Count();
                sb.AppendLine($"| {item.Label} | {CountTriangles(item.Instance):N0} | {mats} | {FormatSize(WorldBounds(item.Instance).size, inv)} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Materialen van de deur");
            sb.AppendLine();
            var door = results.FirstOrDefault(r => r.Pack.Folder.Contains("HospitalDoor"));
            if (door != null)
                foreach (var mat in door.Models.SelectMany(ModelMaterials).Distinct())
                    sb.AppendLine($"- `{mat.name}`: {(HandleNames.Any(mat.name.ToLowerInvariant().Contains) ? "klink (donkergrijs)" : "deur (lichtgrijs)")}");

            File.WriteAllText(ReportPath, sb.ToString());
        }

        private static string FormatSize(Vector3 s, CultureInfo inv) =>
            $"{s.x.ToString("0.00", inv)} x {s.y.ToString("0.00", inv)} x {s.z.ToString("0.00", inv)}";

        private static string EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
            return path;
        }
    }
}
