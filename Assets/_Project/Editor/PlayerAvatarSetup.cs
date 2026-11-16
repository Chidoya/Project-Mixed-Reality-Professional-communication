using ProCommVR.Player;
using UnityEditor;
using UnityEngine;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Puts the nurse avatar in the XR rig variant, so every scene that uses the rig has it (once).
    /// 1. Creates (or updates) the prefab variant Assets/_Project/Prefabs/Player/PlayerAvatar_Nurse.prefab of Liam's
    ///    Nurse_Female_Scrubs, with a <see cref="PlayerAvatar"/> on it. Liam's prefab is not changed.
    /// 2. Replaces the avatar in "XR Origin (ProCommVR).prefab" by a fresh instance of that variant.
    /// Safe to run again. Also called when the rig variant is rebuilt (XRTestSceneBuilder). Batchmode:
    ///   -executeMethod ProCommVR.Editor.PlayerAvatarSetup.Run
    /// </summary>
    public static class PlayerAvatarSetup
    {
        /// <summary>
        /// The character used as the player's body. Nurse_Male_Scrubs works the same way.
        /// </summary>
        private const string SourceCharacterPath = "Assets/Character/Nurse_Female_Scrubs/Nurse_Female_Scrubs.prefab";

        internal const string AvatarPrefabPath = "Assets/_Project/Prefabs/Player/PlayerAvatar_Nurse.prefab";
        private const string AvatarName = "Player Avatar (Nurse)";

        [MenuItem("ProCommVR/Setup/8. Add Player Avatar to Rig")]
        public static void Run()
        {
            var avatar = CreateAvatarPrefab();
            AddToRig(ScenarioTestSceneBuilder.RigVariantPath, avatar);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProCommVR] Player avatar '{AvatarPrefabPath}' added to '{ScenarioTestSceneBuilder.RigVariantPath}'.");
        }

        /// <summary>
        /// The prefab variant of the nurse with a PlayerAvatar (hidden, following the head).
        /// </summary>
        internal static GameObject CreateAvatarPrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceCharacterPath);
            if (source == null)
                throw new System.InvalidOperationException($"[ProCommVR] Character not found: {SourceCharacterPath}");

            XRTestSceneBuilder.EnsureFolder("Assets/_Project/Prefabs/Player");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = "PlayerAvatar_Nurse";
                if (instance.GetComponent<PlayerAvatar>() == null)
                    instance.AddComponent<PlayerAvatar>();

                // Cheap: no shadows, no light probes beyond the default, the animator only runs while visible.
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var animator = instance.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                }
                return PrefabUtility.SaveAsPrefabAsset(instance, AvatarPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// Replaces any avatar in the rig prefab by one fresh instance of the avatar prefab, at the rig's origin.
        /// </summary>
        internal static void AddToRig(string rigPath, GameObject avatarPrefab)
        {
            var rig = PrefabUtility.LoadPrefabContents(rigPath);
            try
            {
                foreach (var old in rig.GetComponentsInChildren<PlayerAvatar>(true))
                    Object.DestroyImmediate(old.gameObject);

                var avatar = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab, rig.transform);
                avatar.name = AvatarName;
                avatar.transform.localPosition = Vector3.zero;
                avatar.transform.localRotation = Quaternion.identity;
                PrefabUtility.SaveAsPrefabAsset(rig, rigPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rig);
            }
        }
    }
}
