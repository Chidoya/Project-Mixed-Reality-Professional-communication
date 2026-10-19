using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Zet de low-poly personages uit Blender klaar (Assets/Character/&lt;Naam&gt;/&lt;Naam&gt;.fbx):
    /// Humanoid rig, URP Lit materialen, Animator Controller en prefab per personage,
    /// plus de testscene Assets/Scenes/liam_test_scene.unity.
    /// Mag opnieuw gedraaid worden, bv. nadat er animaties in Assets/Character/Animations bijkomen.
    /// </summary>
    public static class CharacterSetup
    {
        private const string Root = "Assets/Character";
        private const string AnimationsFolder = Root + "/Animations";
        private const string TestScenePath = "Assets/Scenes/liam_test_scene.unity";
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string LogPrefix = "[CharacterSetup] ";

        private const float Spacing = 1.5f;
        private const float PatientRowZ = 3f;

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
        public static void Setup()
        {
            var clips = ImportAnimationClips();
            var prefabs = new List<GameObject>();

            foreach (var fbxPath in FindCharacterModels())
            {
                var name = Path.GetFileNameWithoutExtension(fbxPath);
                ConfigureModelImporter(fbxPath);
                ConvertMaterials(fbxPath, name);
                ValidateAvatar(fbxPath, name);
                var controller = BuildController(name, clips);
                prefabs.Add(BuildPrefab(fbxPath, name, controller));
            }

            BuildTestScene(prefabs, overwrite: false);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix}Klaar: {prefabs.Count} personages, {clips.Count} animatieclips " +
                      $"({string.Join(", ", clips.Keys)}).");
        }

        [MenuItem("ProCommVR/Characters/Rebuild Test Scene")]
        public static void RebuildTestScene()
        {
            var prefabs = FindCharacterModels()
                .Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(Path.GetFileNameWithoutExtension(p))))
                .Where(p => p != null)
                .ToList();
            BuildTestScene(prefabs, overwrite: true);
        }

        /// <summary>Voor de command line: Unity -batchmode -executeMethod ProCommVR.Editor.CharacterSetup.RunBatch</summary>
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
            foreach (var source in embedded)
            {
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

        // --- Prefab en testscene -------------------------------------------------

        private static GameObject BuildPrefab(string fbxPath, string name, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name;

            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath(name));
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void BuildTestScene(List<GameObject> prefabs, bool overwrite)
        {
            if (!overwrite && File.Exists(TestScenePath))
            {
                Debug.Log($"{LogPrefix}{TestScenePath} bestaat al, niet overschreven (gebruik Rebuild Test Scene).");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 1f);
            floor.transform.position = new Vector3(0f, 0f, 1.5f);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 2.6f, -8f);
                camera.transform.LookAt(new Vector3(0f, 1f, 1.5f));
            }

            var patients = new GameObject("Patients").transform;
            var staff = new GameObject("Staff").transform;
            var patientPrefabs = prefabs.Where(p => p.name.StartsWith("Patient")).ToList();
            var staffPrefabs = prefabs.Except(patientPrefabs).ToList();
            PlaceRow(patientPrefabs, patients, PatientRowZ);
            PlaceRow(staffPrefabs, staff, 0f);

            EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log($"{LogPrefix}Testscene opgeslagen: {TestScenePath}");
        }

        private static void PlaceRow(List<GameObject> prefabs, Transform parent, float z)
        {
            for (var i = 0; i < prefabs.Count; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], parent);
                var x = (i - (prefabs.Count - 1) / 2f) * Spacing;
                go.transform.position = new Vector3(x, 0f, z);
                // Personages kijken naar de camera (-Z)
                go.transform.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Inverse(FacingOffset(go));
            }
        }

        /// <summary>Rotatie van de kijkrichting van het model t.o.v. +Z, bepaald via de voeten.</summary>
        private static Quaternion FacingOffset(GameObject go)
        {
            var animator = go.GetComponent<Animator>();
            var foot = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            var toes = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftToes) : null;
            if (foot == null || toes == null)
                return Quaternion.identity;
            var forward = Vector3.ProjectOnPlane(toes.position - foot.position, Vector3.up);
            var localForward = Quaternion.Inverse(go.transform.rotation) * forward;
            return localForward.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(localForward.normalized);
        }
    }
}
