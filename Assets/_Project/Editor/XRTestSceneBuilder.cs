using System.IO;
using System.Linq;
using ProCommVR.XRTest;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Builds the technical baseline for VR testing: imports the XRI Starter Assets sample, creates a
    /// trigger-select copy of its input actions, a project rig variant without gaze interaction,
    /// and the XRTest scene (floor, light, rig, test targets).
    /// Run in two steps, because the sample import needs a recompile in between:
    ///   1. -executeMethod ProCommVR.Editor.XRTestSceneBuilder.ImportStarterAssets
    ///   2. -executeMethod ProCommVR.Editor.XRTestSceneBuilder.BuildTestScene
    /// </summary>
    public static class XRTestSceneBuilder
    {
        private const string XRIPackage = "com.unity.xr.interaction.toolkit";
        private const string StarterAssetsSample = "Starter Assets";
        private const string SampleRigName = "XR Origin (XR Rig)";
        private const string SampleActionsName = "XRI Default Input Actions";

        private const string InputActionsPath = "Assets/_Project/Settings/Input/ProCommVR XRI Input Actions.inputactions";
        private const string RigVariantPath = "Assets/_Project/Prefabs/XR Origin (ProCommVR).prefab";
        private const string FloorMaterialPath = "Assets/_Project/Materials/Floor.mat";
        private const string TargetMaterialPath = "Assets/_Project/Materials/TestTarget.mat";
        private const string ScenePath = "Assets/_Project/Scenes/XRTest.unity";

        [MenuItem("ProCommVR/Setup/1. Import XRI Starter Assets")]
        public static void ImportStarterAssets()
        {
            var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .First(p => p.name == XRIPackage);
            var sample = Sample.FindByPackage(XRIPackage, package.version)
                .FirstOrDefault(s => s.displayName == StarterAssetsSample);
            if (sample.displayName == null)
                throw new System.InvalidOperationException($"[ProCommVR] Sample '{StarterAssetsSample}' not found in {XRIPackage}.");

            if (!sample.isImported)
                sample.Import(Sample.ImportOptions.HideImportWindow);
            Debug.Log($"[ProCommVR] XRI Starter Assets available at {sample.importPath}.");
        }

        [MenuItem("ProCommVR/Setup/2. Build XR Test Scene")]
        public static void BuildTestScene()
        {
            var sampleRigPath = FindSampleAsset($"\"{SampleRigName}\" t:Prefab");
            var sampleActionsPath = FindSampleAsset($"\"{SampleActionsName}\" t:InputActionAsset");

            CreateTriggerSelectInputActions(sampleActionsPath);
            var rigVariant = CreateRigVariant(sampleRigPath, sampleActionsPath);
            CreateScene(rigVariant);

            AssetDatabase.SaveAssets();
            Debug.Log($"[ProCommVR] XR test scene created at {ScenePath}.");
        }

        private static string FindSampleAsset(string filter)
        {
            var path = AssetDatabase.FindAssets(filter, new[] { "Assets/Samples" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.Contains(StarterAssetsSample));
            if (path == null)
                throw new System.InvalidOperationException($"[ProCommVR] '{filter}' not found. Run step 1 (import Starter Assets) first.");
            return path;
        }

        /// <summary>
        /// Copies the sample input actions and swaps the controller bindings so that Select is the trigger
        /// and Activate is the grip (the XRI default is the other way around).
        /// </summary>
        private static void CreateTriggerSelectInputActions(string sampleActionsPath)
        {
            EnsureFolder(Path.GetDirectoryName(InputActionsPath));
            if (!File.Exists(InputActionsPath))
                AssetDatabase.CopyAsset(sampleActionsPath, InputActionsPath);

            var actions = InputActionAsset.FromJson(File.ReadAllText(InputActionsPath));
            foreach (var hand in new[] { "Left", "Right" })
            {
                var map = actions.FindActionMap($"XRI {hand} Interaction", true);
                SwapControllerBinding(map, "Select", "{GripButton}", "{TriggerButton}");
                SwapControllerBinding(map, "Select Value", "{Grip}", "{Trigger}");
                SwapControllerBinding(map, "Activate", "{TriggerButton}", "{GripButton}");
                SwapControllerBinding(map, "Activate Value", "{Trigger}", "{Grip}");
            }

            File.WriteAllText(InputActionsPath, actions.ToJson());
            Object.DestroyImmediate(actions);
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate);
        }

        private static void SwapControllerBinding(InputActionMap map, string actionName, string from, string to)
        {
            var action = map.FindAction(actionName, true);
            for (var i = 0; i < action.bindings.Count; i++)
            {
                var path = action.bindings[i].path;
                if (path.StartsWith("<XRController>") && path.EndsWith("/" + from))
                    action.ChangeBinding(i).WithPath(path.Substring(0, path.Length - from.Length) + to);
            }
        }

        private static GameObject CreateRigVariant(string sampleRigPath, string sampleActionsPath)
        {
            EnsureFolder(Path.GetDirectoryName(RigVariantPath));
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sampleRigPath));
            try
            {
                var gaze = rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Gaze Interactor");
                if (gaze == null)
                    throw new System.InvalidOperationException("[ProCommVR] 'Gaze Interactor' not found in the sample rig.");
                gaze.gameObject.SetActive(false);

                RetargetInputActions(rig, sampleActionsPath);
                return PrefabUtility.SaveAsPrefabAsset(rig, RigVariantPath);
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        /// <summary>
        /// Points every input action reference in the rig from the sample asset to the project copy.
        /// </summary>
        private static void RetargetInputActions(GameObject rig, string sampleActionsPath)
        {
            var projectActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var projectReferences = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .GroupBy(ReferenceKey)
                .ToDictionary(g => g.Key, g => g.First());

            var retargeted = 0;
            foreach (var component in rig.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;

                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    var value = property.objectReferenceValue;
                    if (value == null || AssetDatabase.GetAssetPath(value) != sampleActionsPath)
                        continue;

                    if (value is InputActionAsset)
                        property.objectReferenceValue = projectActions;
                    else if (value is InputActionReference reference && projectReferences.TryGetValue(ReferenceKey(reference), out var replacement))
                        property.objectReferenceValue = replacement;
                    else
                        continue;
                    retargeted++;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Debug.Log($"[ProCommVR] Retargeted {retargeted} input action references to {InputActionsPath}.");
        }

        // The copy keeps the action IDs of the sample, so the ID matches references across both assets.
        private static System.Guid ReferenceKey(InputActionReference reference)
        {
            return reference.action.id;
        }

        private static void CreateScene(GameObject rigVariant)
        {
            EnsureFolder(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.None;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(FloorMaterialPath, new Color(0.55f, 0.55f, 0.55f));

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigVariant, scene);
            rig.transform.position = Vector3.zero;

            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));

            var target = CreateTestTarget();
            CreateTestCanvas(rig.GetComponentInChildren<Camera>(), target);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var otherScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(otherScenes).ToArray();
        }

        private static RayTestTarget CreateTestTarget()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Ray Test Target";
            cube.transform.position = new Vector3(0.5f, 1.2f, 2f);
            cube.transform.localScale = Vector3.one * 0.3f;
            cube.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(TargetMaterialPath, Color.white);
            cube.AddComponent<XRSimpleInteractable>();
            return cube.AddComponent<RayTestTarget>();
        }

        private static void CreateTestCanvas(Camera eventCamera, RayTestTarget target)
        {
            var canvasObject = new GameObject("Test Canvas", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eventCamera;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(400f, 200f);
            canvasRect.position = new Vector3(-0.5f, 1.3f, 2f);
            canvasRect.localScale = Vector3.one * 0.002f;

            var buttonObject = new GameObject("Test Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasRect, false);
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.sizeDelta = new Vector2(360f, 120f);
            UnityEventTools.AddPersistentListener(buttonObject.GetComponent<Button>().onClick, target.ToggleColor);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonRect, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            var label = labelObject.GetComponent<Text>();
            label.text = "Trigger test";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 40;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.black;
        }

        private static Material GetOrCreateMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            EnsureFolder(Path.GetDirectoryName(path));
            material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
