using System.IO;
using System.Linq;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Editor setup of the POV switch (patient view), used by the scene builders:
    /// - the "SwitchPov" action (Y on the left controller) in the project's copy of the XRI input actions,
    /// - the settings asset, the fade material,
    /// - per scene: a <see cref="PovAnchor"/> on the NPC, a fade quad on the camera, the "Press Y to return" hint and a
    ///   <see cref="PovController"/>.
    /// </summary>
    public static class PovSetup
    {
        internal const string ActionMapName = "XRI Left Interaction";
        internal const string ActionName = "SwitchPov";
        private const string Binding = "<XRController>{LeftHand}/{SecondaryButton}";
        private const string SettingsPath = "Assets/_Project/Settings/Pov/PovSwitchSettings.asset";
        private const string FadeMaterialPath = "Assets/_Project/Materials/Pov/ScreenFade.mat";
        internal const string HintText = "Press Y to return";

        /// <summary>
        /// Adds the SwitchPov action to the input actions (once). Safe to run again. Also called when the input
        /// actions are recreated (XRTestSceneBuilder).
        /// </summary>
        [MenuItem("ProCommVR/Setup/9. Add Switch POV Input Action")]
        public static void EnsureSwitchAction()
        {
            var path = XRTestSceneBuilder.InputActionsPath;
            var actions = InputActionAsset.FromJson(File.ReadAllText(path));
            try
            {
                var map = actions.FindActionMap(ActionMapName, true);
                if (map.FindAction(ActionName) != null)
                    return;
                map.AddAction(ActionName, InputActionType.Button, Binding);
                File.WriteAllText(path, actions.ToJson());
            }
            finally
            {
                Object.DestroyImmediate(actions);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[ProCommVR] Added '{ActionMapName}/{ActionName}' ({Binding}) to {path}.");
        }

        internal static InputActionReference SwitchActionReference()
        {
            EnsureSwitchAction();
            var reference = AssetDatabase.LoadAllAssetsAtPath(XRTestSceneBuilder.InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.actionMap.name == ActionMapName && r.action.name == ActionName);
            if (reference == null)
                throw new System.InvalidOperationException($"[ProCommVR] No InputActionReference for {ActionMapName}/{ActionName}.");
            return reference;
        }

        internal static PovSwitchSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PovSwitchSettings>(SettingsPath);
            if (settings != null)
                return settings;
            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<PovSwitchSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static Material GetOrCreateFadeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FadeMaterialPath);
            if (material != null)
                return material;
            XRTestSceneBuilder.EnsureFolder(Path.GetDirectoryName(FadeMaterialPath));
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "ScreenFade" };
            // Transparent, alpha blended, no depth write, drawn after everything else.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_ZTest", (float)CompareFunction.Always);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            material.renderQueue = (int)RenderQueue.Overlay;
            AssetDatabase.CreateAsset(material, FadeMaterialPath);
            return material;
        }

        /// <summary>
        /// Adds the patient view to a scene: the anchor on the NPC's body, the fade on the rig's camera, the hint and
        /// the controller. Eyes: only for an NPC without a Humanoid rig (a child of its body). Returns the controller.
        /// </summary>
        internal static PovController AddToScene(ScenarioPresenter presenter, Transform npcBody, XROrigin origin, Transform eyes = null)
        {
            var anchor = npcBody.gameObject.AddComponent<PovAnchor>();
            if (eyes != null)
            {
                var serializedAnchor = new SerializedObject(anchor);
                serializedAnchor.FindProperty("_eyes").objectReferenceValue = eyes;
                serializedAnchor.ApplyModifiedPropertiesWithoutUndo();
            }

            // Fade: a quad just in front of the camera, without a collider (it must not block the lasers).
            var fade = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fade.name = "POV Screen Fade";
            Object.DestroyImmediate(fade.GetComponent<Collider>());
            fade.transform.SetParent(origin.Camera.transform, false);
            fade.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            fade.transform.localRotation = Quaternion.identity;
            fade.transform.localScale = new Vector3(1f, 1f, 1f);
            var fadeRenderer = fade.GetComponent<MeshRenderer>();
            fadeRenderer.sharedMaterial = GetOrCreateFadeMaterial();
            fadeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            fadeRenderer.receiveShadows = false;
            fadeRenderer.enabled = false;
            var screenFade = fade.AddComponent<ScreenFade>();

            // Hint: a non-clickable dialogue bubble, so it looks like the rest of the UI.
            var bubblePrefab = AssetDatabase.LoadAssetAtPath<DialogueBubble>(ScenarioTestSceneBuilder.BubblePrefabPath);
            var hint = (DialogueBubble)PrefabUtility.InstantiatePrefab(bubblePrefab);
            hint.name = "POV Hint (Press Y to return)";
            hint.SetText(HintText);
            hint.SetClickable(false);
            hint.transform.localScale *= 0.8f;
            hint.gameObject.SetActive(false);

            var controllerObject = new GameObject("POV Controller");
            var controller = controllerObject.AddComponent<PovController>();
            hint.transform.SetParent(controllerObject.transform, true);
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_settings").objectReferenceValue = GetOrCreateSettings();
            serialized.FindProperty("_switchAction").objectReferenceValue = SwitchActionReference();
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_anchor").objectReferenceValue = anchor;
            serialized.FindProperty("_origin").objectReferenceValue = origin;
            serialized.FindProperty("_fade").objectReferenceValue = screenFade;
            serialized.FindProperty("_hint").objectReferenceValue = hint.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }
    }
}
