using System.Collections.Generic;
using System.Linq;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Hospital.Debrief.Editor;
using ProCommVR.Hospital.PatientPov;
using ProCommVR.Pov;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Hospital.Prototype1.Editor
{
    /// <summary>
    /// Liam's part of Prototype1, called with ONE line at the end of Jalil's Prototype1SceneBuilder. Scenario 1 in one place:
    /// <list type="number">
    /// <item>Patient X is the old man with the bandaged (infected) leg (PatientX_Scenario1, a prefab variant of Liam's
    /// Patient_Male_Old) instead of Patient_Male: same place and pose, every component and every reference in the scene
    /// goes over to him (the AI conversation, our emotions, the trust, the patient view).</item>
    /// <item>Patient POV plus in room 1A: his face and gaze follow the same EmotionHolder as the room, the granddaughter's
    /// card, photo, balloon and party clock, heartbeat, muffled sound and inner voice (PatientPovSetup.AddToWardRoom).</item>
    /// <item>The doctor-led debrief (DebriefSetup).</item>
    /// </list>
    /// Liam's U-ward scene on disk stays as it is: everything happens in the Prototype1 scene being built.
    /// </summary>
    public static class LiamPrototype1Setup
    {
        private const string LogPrefix = "[Prototype1/Liam] ";

        public static void AddToScene(Transform prototype)
        {
            var npc = Object.FindAnyObjectByType<AiNpc>(FindObjectsInactive.Include);
            if (npc == null)
                throw new System.InvalidOperationException(LogPrefix + "no AI patient in the scene.");

            // 1. The man with the infected leg.
            var patient = SwapCharacter(npc.transform, PatientPovSetup.EnsurePatientPrefab());
            npc = patient.GetComponent<AiNpc>();
            var serializedNpc = new SerializedObject(npc);
            serializedNpc.FindProperty("_faceFollowsEmotions").boolValue = true;
            serializedNpc.ApplyModifiedPropertiesWithoutUndo();

            // 2. Patient POV plus in his room.
            var holder = patient.GetComponent<EmotionHolder>();
            var pov = Object.FindAnyObjectByType<PovController>(FindObjectsInactive.Include);
            var room = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Room_1A" && t.Find("Furniture/Nightstand") != null);
            if (room == null)
                throw new System.InvalidOperationException(LogPrefix + "no Room_1A with Furniture/Nightstand.");
            var povPlus = PatientPovSetup.AddToWardRoom(holder, pov, patient, room);

            // 3. The debrief at the head doctor.
            DebriefSetup.AddToScene(prototype);
            Debug.Log($"{LogPrefix}patient {patient.name} is PatientX_Scenario1, patient POV plus in {room.name} ({povPlus.name}), debrief added.");
        }

        /// <summary>
        /// Replaces a character instance by another character prefab at the same place and pose. Components on the old
        /// root that the new one lacks are copied (with their values), objects the builders added under the old character
        /// move along (bones matched by humanoid bone or name), and every reference in the scene to the old character
        /// (its components, GameObjects, bones) points to the new one. Returns the new root, with the old name.
        /// </summary>
        public static Transform SwapCharacter(Transform old, GameObject prefab)
        {
            var name = old.name;
            var created = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.parent);
            var fresh = created.transform;
            fresh.SetPositionAndRotation(old.position, old.rotation);
            fresh.localScale = old.localScale;
            fresh.SetSiblingIndex(old.GetSiblingIndex());

            var oldAnimator = old.GetComponent<Animator>();
            var newAnimator = created.GetComponent<Animator>();
            var map = new Dictionary<Transform, Transform> { [old] = fresh };
            if (oldAnimator != null && newAnimator != null && oldAnimator.isHuman && newAnimator.isHuman)
                for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
                {
                    var a = oldAnimator.GetBoneTransform(bone);
                    var b = newAnimator.GetBoneTransform(bone);
                    if (a != null && b != null)
                        map[a] = b;
                }

            Transform Map(Transform t)
            {
                if (t == null || !t.IsChildOf(old))
                    return null;
                if (map.TryGetValue(t, out var mapped))
                    return mapped;
                var path = AnimationUtility.CalculateTransformPath(t, old);
                mapped = fresh.Find(path) ?? fresh.GetComponentsInChildren<Transform>(true).FirstOrDefault(c => c.name == t.name);
                if (mapped == null)
                    mapped = Map(t.parent); // an added object: it moves along, its parent decides
                map[t] = mapped;
                return mapped;
            }

            // Components the old root had and the new one lacks (AI, emotions, trust, patient view, pose).
            var copied = new List<string>();
            foreach (var component in old.GetComponents<Component>())
            {
                if (component == null || component is Transform)
                    continue;
                if (created.GetComponent(component.GetType()) != null)
                    continue;
                var copy = created.AddComponent(component.GetType());
                EditorUtility.CopySerialized(component, copy);
                copied.Add(component.GetType().Name);
            }

            // Objects the builders added under the old character (not part of its prefab) move to the new one.
            var moved = new List<string>();
            foreach (var t in old.GetComponentsInChildren<Transform>(true).ToArray())
            {
                // (a child of an object that already moved is no longer under the old character: it stays with its parent)
                if (t == old || t.parent == null || !t.IsChildOf(old) || !PrefabUtility.IsAddedGameObjectOverride(t.gameObject))
                    continue;
                var parent = Map(t.parent);
                var local = (t.localPosition, t.localRotation, t.localScale);
                t.SetParent(parent, false);
                (t.localPosition, t.localRotation, t.localScale) = local;
                map[t] = t;
                moved.Add(t.name);
            }

            // Every reference in the scene to the old character now points to the new one.
            var remapped = 0;
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.transform.IsChildOf(old))
                    continue;
                var serialized = new SerializedObject(behaviour);
                var property = serialized.GetIterator();
                var changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                        continue;
                    var replacement = property.objectReferenceValue switch
                    {
                        GameObject go when go.transform.IsChildOf(old) => Map(go.transform)?.gameObject,
                        Transform tr when tr.IsChildOf(old) => Map(tr),
                        Component c when c.transform.IsChildOf(old) => SameComponent(c, Map(c.transform)),
                        _ => null,
                    };
                    if (replacement == null)
                        continue;
                    property.objectReferenceValue = replacement;
                    changed = true;
                    remapped++;
                }
                if (changed)
                    serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Object.DestroyImmediate(old.gameObject);
            created.name = name;
            Debug.Log($"{LogPrefix}{name} is now {prefab.name}: copied {string.Join(", ", copied)}; moved {string.Join(", ", moved)}; " +
                      $"{remapped} references moved to the new character.");
            return fresh;
        }

        private static Object SameComponent(Component original, Transform target)
        {
            if (target == null)
                return null;
            var all = original.GetComponents(original.GetType());
            var index = System.Array.IndexOf(all, original);
            var candidates = target.GetComponents(original.GetType());
            return candidates.Length == 0 ? null : candidates[Mathf.Clamp(index, 0, candidates.Length - 1)];
        }
    }
}
