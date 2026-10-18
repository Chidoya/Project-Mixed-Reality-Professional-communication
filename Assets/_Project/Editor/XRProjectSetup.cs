using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Configures the project for Meta Quest 3: Android player settings, OpenXR as the XR loader,
    /// and the Meta Quest feature with the Quest controller interaction profiles.
    /// Safe to run more than once. Batchmode: -executeMethod ProCommVR.Editor.XRProjectSetup.ConfigureForQuest
    /// </summary>
    public static class XRProjectSetup
    {
        private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string XRSettingsAssetPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";

        [MenuItem("ProCommVR/Setup/Configure Project for Quest 3")]
        public static void ConfigureForQuest()
        {
            ConfigurePlayerSettings();
            ConfigureXRLoader();
            ConfigureOpenXRFeatures();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProCommVR] Project configured for Meta Quest 3 (Android, OpenXR).");
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        }

        private static void ConfigureXRLoader()
        {
            var perTarget = GetOrCreateXRSettings();
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            var generalSettings = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            generalSettings.InitManagerOnStart = true;

            var manager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!manager.activeLoaders.Any(l => l != null && l.GetType().FullName == OpenXRLoaderType)
                && !XRPackageMetadataStore.AssignLoader(manager, OpenXRLoaderType, BuildTargetGroup.Android))
            {
                throw new System.InvalidOperationException("[ProCommVR] Could not assign the OpenXR loader for Android.");
            }

            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(perTarget);
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreateXRSettings()
        {
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget settings)
                && settings != null)
            {
                return settings;
            }

            var guid = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget").FirstOrDefault();
            if (guid != null)
            {
                settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guid));
            }
            else
            {
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, XRSettingsAssetPath);
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, settings, true);
            return settings;
        }

        private static void ConfigureOpenXRFeatures()
        {
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXR == null)
                throw new System.InvalidOperationException("[ProCommVR] No OpenXR settings found for Android.");

            EnableFeature<MetaQuestFeature>(openXR);
            EnableFeature<OculusTouchControllerProfile>(openXR);
            EnableFeature<MetaQuestTouchPlusControllerProfile>(openXR);
            EditorUtility.SetDirty(openXR);
        }

        private static void EnableFeature<T>(OpenXRSettings settings) where T : OpenXRFeature
        {
            var feature = settings.GetFeature<T>();
            if (feature == null)
                throw new System.InvalidOperationException($"[ProCommVR] OpenXR feature {typeof(T).Name} not found.");

            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }
    }
}
