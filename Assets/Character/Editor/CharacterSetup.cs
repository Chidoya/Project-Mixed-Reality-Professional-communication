using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ProCommVR.Hospital.Face;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Zet de low-poly personages uit Blender klaar (Assets/Character/&lt;Naam&gt;/&lt;Naam&gt;.fbx):
    /// Humanoid rig, URP Lit materialen, Animator Controller en prefab per personage.
    /// Maakt ook een lege standaard scene Assets/Scenes/liam_test_scene.unity als die nog niet bestaat.
    /// Mag opnieuw gedraaid worden, bv. nadat er animaties in Assets/Character/Animations bijkomen.
    /// </summary>
    public static class CharacterSetup
    {
        private const string Root = "Assets/Character";
        private const string AnimationsFolder = Root + "/Animations";
        private const string TestScenePath = "Assets/Scenes/liam_test_scene.unity";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string LogPrefix = "[CharacterSetup] ";

        // Gezicht (blender/scripts/07_gezicht.py): één gedeeld materiaal met een paletbeeld
        private const string FaceFolder = Root + "/Face";
        private const string FacePalettePath = FaceFolder + "/FacePalette.png";
        private const string FaceMaterialPath = FaceFolder + "/Face.mat";
        private const string TearDropMaterialPath = FaceFolder + "/TearDrop.mat";
        private const string FacePresetsPath = "Assets/Hospital/Data/FacePresets.asset";

        // State in de Animator -> mogelijke bestandsnamen van Mixamo-clips (zonder spaties, kleine letters)
        private static readonly (string State, string[] Names)[] States =
        {
            ("Idle", new[] { "idle" }),
            ("Talking", new[] { "talking" }),
            ("Sitting", new[] { "sittingidle", "sitting" }),
            ("Laying", new[] { "layingidle", "lyingidle", "laying", "lying" }),
            ("Sad", new[] { "sadidle", "sad" }),
            ("Angry", new[] { "angry" }),
            ("Crying", new[] { "crying" }),
        };

        [MenuItem("ProCommVR/Characters/Setup")]
        public static void Setup() => Setup(null);

        /// <summary>Enkel de personages in `only` (null = allemaal); de andere blijven onaangeroerd.</summary>
        public static void Setup(ICollection<string> only)
        {
            var clips = ImportAnimationClips();
            PrepareFaceAssets();
            var prefabs = new List<GameObject>();

            foreach (var fbxPath in FindCharacterModels().Where(p => only == null || only.Contains(Path.GetFileNameWithoutExtension(p))))
            {
                var name = Path.GetFileNameWithoutExtension(fbxPath);
                ConfigureModelImporter(fbxPath);
                ConvertMaterials(fbxPath, name);
                ValidateAvatar(fbxPath, name);
                var controller = BuildController(name, clips);
                prefabs.Add(BuildPrefab(fbxPath, name, controller));
            }

            CreateTestSceneIfMissing();
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix}Klaar: {prefabs.Count} personages, {clips.Count} animatieclips " +
                      $"({string.Join(", ", clips.Keys)}).");
        }

        /// <summary>
        /// Voor de command line: Unity -batchmode -executeMethod ProCommVR.Editor.CharacterSetup.RunBatch
        /// [-characters Naam1,Naam2] om enkel die personages klaar te zetten.
        /// </summary>
        public static void RunBatch()
        {
            try
            {
                var args = System.Environment.GetCommandLineArgs();
                var index = System.Array.IndexOf(args, "-characters");
                Setup(index >= 0 && index + 1 < args.Length ? args[index + 1].Split(',') : null);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError(LogPrefix + e);
                EditorApplication.Exit(1);
            }
        }

        private static IEnumerable<string> FindCharacterModels()
        {
            // Enkel Assets/Character/<Naam>/<Naam>.fbx, niet de animatieclips
            return AssetDatabase.FindAssets("t:Model", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith(AnimationsFolder))
                .Where(p => Path.GetFileName(Path.GetDirectoryName(p)) == Path.GetFileNameWithoutExtension(p))
                .OrderBy(p => p);
        }

        private static string PrefabPath(string name) => $"{Root}/{name}/{name}.prefab";

        // --- Model en avatar -----------------------------------------------------

        private static void ConfigureModelImporter(string fbxPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importNormals = ModelImporterNormals.Import; // flat shading uit Blender behouden
            importer.importBlendShapes = true;                     // gezicht (07_gezicht.py)
            importer.importBlendShapeNormals = ModelImporterNormals.None; // anders worden de vlakjes zacht
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();
        }

        private static void ValidateAvatar(string fbxPath, string name)
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                Debug.LogError($"{LogPrefix}{name}: geen geldige Humanoid avatar.");
            else
                Debug.Log($"{LogPrefix}{name}: Humanoid avatar OK ({avatar.humanDescription.human.Length} botten gemapt).");
        }

        // --- Materialen ----------------------------------------------------------

        /// <summary>
        /// Maakt voor elk ingebed FBX-materiaal een bewerkbaar URP Lit materiaal met dezelfde effen kleur
        /// en koppelt het via een remap. Bij opnieuw draaien blijven bestaande remaps staan.
        /// </summary>
        private static void ConvertMaterials(string fbxPath, string name)
        {
            var embedded = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().ToList();
            if (embedded.Count == 0)
                return;

            var folder = $"{Root}/{name}/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder($"{Root}/{name}", "Materials");

            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            var shader = Shader.Find(LitShader);
            var faceMaterial = AssetDatabase.LoadAssetAtPath<Material>(FaceMaterialPath);
            foreach (var source in embedded)
            {
                if (source.name.EndsWith("_Face") && faceMaterial != null)
                {
                    // Alle gezichten delen één materiaal met het paletbeeld
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), faceMaterial);
                    continue;
                }
                var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                var path = $"{folder}/{source.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", 0.2f);
                EditorUtility.SetDirty(mat);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), mat);
            }
            importer.SaveAndReimport();
        }

        // --- Animaties -----------------------------------------------------------

        /// <summary>Zet Mixamo-clips op Humanoid + loop en geeft ze terug per state-naam.</summary>
        private static Dictionary<string, AnimationClip> ImportAnimationClips()
        {
            var result = new Dictionary<string, AnimationClip>();
            if (!AssetDatabase.IsValidFolder(AnimationsFolder))
                return result;

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationsFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var key = Path.GetFileNameWithoutExtension(path).Replace(" ", "").Replace("_", "").ToLowerInvariant();
                var state = States.FirstOrDefault(s => s.Names.Contains(key)).State;
                if (state == null)
                {
                    Debug.LogWarning($"{LogPrefix}Animatie '{path}' herken ik niet, overgeslagen.");
                    continue;
                }

                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                var clipSettings = importer.defaultClipAnimations;
                foreach (var c in clipSettings)
                {
                    c.name = state;
                    c.loopTime = true;
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true;
                }
                importer.clipAnimations = clipSettings;
                importer.SaveAndReimport();

                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip != null)
                    result[state] = clip;
            }
            return result;
        }

        private static AnimatorController BuildController(string name, Dictionary<string, AnimationClip> clips)
        {
            var path = $"{Root}/{name}/{name}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(path);

            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states.ToArray())
                machine.RemoveState(child.state);

            foreach (var (state, _) in States)
            {
                if (!clips.TryGetValue(state, out var clip))
                    continue;
                var s = machine.AddState(state);
                s.motion = clip;
                if (state == "Idle")
                    machine.defaultState = s;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // --- Prefab en scene ------------------------------------------------------

        private static GameObject BuildPrefab(string fbxPath, string name, AnimatorController controller)
        {
            // Bestaande prefab openen en bijwerken, zodat toegevoegde componenten (en verwijzingen
            // ernaar in scenes) bij opnieuw draaien blijven bestaan.
            var path = PrefabPath(name);
            var existing = File.Exists(path);
            GameObject root;
            if (existing)
                root = PrefabUtility.LoadPrefabContents(path);
            else
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                root = (GameObject)PrefabUtility.InstantiatePrefab(model);
                root.name = name;
            }

            var animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            AddFaceComponents(root, name);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (existing)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);
            return prefab;
        }

        // --- Gezicht ---------------------------------------------------------------

        /// <summary>Palet (point filter), gedeeld Face-materiaal, traandruppel-materiaal en FacePresets-asset.</summary>
        private static void PrepareFaceAssets()
        {
            if (!File.Exists(FacePalettePath))
            {
                Debug.LogWarning($"{LogPrefix}{FacePalettePath} ontbreekt: kopieer blender/export/unity/_Face/FacePalette.png.");
                return;
            }

            var texImporter = (TextureImporter)AssetImporter.GetAtPath(FacePalettePath);
            texImporter.textureType = TextureImporterType.Default;
            texImporter.sRGBTexture = true;
            texImporter.mipmapEnabled = false;
            texImporter.filterMode = FilterMode.Point;
            texImporter.wrapMode = TextureWrapMode.Clamp;
            texImporter.textureCompression = TextureImporterCompression.Uncompressed;
            texImporter.npotScale = TextureImporterNPOTScale.None;
            texImporter.SaveAndReimport();
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(FacePalettePath);

            var shader = Shader.Find(LitShader);
            var face = LoadOrCreateMaterial(FaceMaterialPath, shader);
            face.SetTexture("_BaseMap", palette);
            face.SetColor("_BaseColor", Color.white);
            face.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(face);

            var tear = LoadOrCreateMaterial(TearDropMaterialPath, shader);
            tear.SetColor("_BaseColor", new Color(0.78f, 0.94f, 1f));
            tear.SetFloat("_Smoothness", 0.92f);
            tear.EnableKeyword("_EMISSION");
            tear.SetColor("_EmissionColor", new Color(0.22f, 0.30f, 0.36f));
            tear.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(tear);

            if (AssetDatabase.LoadAssetAtPath<FacePresets>(FacePresetsPath) == null)
            {
                var presets = ScriptableObject.CreateInstance<FacePresets>();
                presets.ResetToDefaults();
                AssetDatabase.CreateAsset(presets, FacePresetsPath);
            }
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            return mat;
        }

        /// <summary>FaceController, FaceFromAnimator, HeadLookAt en TearDrops (enkel als het model een gezicht heeft).</summary>
        private static void AddFaceComponents(GameObject root, string name)
        {
            var hasFace = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Any(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("Blink") >= 0);
            if (!hasFace)
            {
                Debug.LogWarning($"{LogPrefix}{name}: geen gezicht-blendshapes (run blender/scripts/07_gezicht.py).");
                return;
            }

            var face = root.GetComponent<FaceController>() ?? root.AddComponent<FaceController>();
            var so = new SerializedObject(face);
            so.FindProperty("_presets").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FacePresets>(FacePresetsPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<FaceFromAnimator>() == null)
                root.AddComponent<FaceFromAnimator>();
            var look = root.GetComponent<HeadLookAt>() ?? root.AddComponent<HeadLookAt>();
            if (!look.Bake())
                Debug.LogWarning($"{LogPrefix}{name}: kijkrichting van het hoofd niet gevonden.");

            var tears = root.GetComponent<TearDrops>() ?? root.AddComponent<TearDrops>();
            var faceRenderer = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .First(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("Blink") >= 0);
            if (!tears.Bake(faceRenderer))
                Debug.LogWarning($"{LogPrefix}{name}: traanpad niet gevonden (blendshape Tears).");
            var tso = new SerializedObject(tears);
            tso.FindProperty("_size").floatValue = 0.011f;
            tso.FindProperty("_material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(TearDropMaterialPath);
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Lege standaard scene (camera + licht). Een bestaande scene wordt nooit aangeraakt.</summary>
        private static void CreateTestSceneIfMissing()
        {
            if (File.Exists(TestScenePath))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"{LogPrefix}Lege scene aangemaakt: {TestScenePath}");
        }
    }
}
