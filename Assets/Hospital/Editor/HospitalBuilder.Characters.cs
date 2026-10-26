using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProCommVR.Hospital;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Blok "PlaceCharacters": patiënten in hun kamer (volgens de PatientCase) en zorgverleners als decor.
    /// Gebruikt de bestaande prefabs en controllers uit Assets/Character zonder ze aan te passen;
    /// de pose wordt gestart door CharacterPose. Posities worden berekend op de echte pose van de animatie.
    /// </summary>
    public static partial class HospitalBuilder
    {
        private const string CharacterFolder = "Assets/Character";
        private const string CharactersRootName = "Characters";

        // Patiënt in bed of op de stoel
        private const float LayingClearance = 0.01f;     // laagste punt van het lichaam boven het matras
        private const float HipsAboveSeat = 0.12f;       // heupgewricht boven het zitvlak (heupen op ongeveer 0,57 m)
        private const float SeatBackOffset = 0.05f;      // heupen iets naar achter op de zitting
        private const float FloorTolerance = 0.03f;      // voeten mogen zoveel door de vloer voor er gecorrigeerd wordt

        // Zorgverleners
        private const float StaffColliderRadius = 0.25f;
        private const float StaffColliderHeight = 1.7f;

        private struct StaffSpot
        {
            public string Prefab, State;
            public int Level;
            public Vector3 Position, LookAt;
            public StaffSpot(string prefab, string state, int level, Vector3 position, Vector3 lookAt)
            {
                Prefab = prefab; State = state; Level = level; Position = position; LookAt = lookAt;
            }
        }

        // Per vloer 2 tot 3, paren tegenover elkaar aan de zuidkant van de gang (de deuren blijven vrij). Chirurgen bij de lift.
        private static readonly StaffSpot[] Staff =
        {
            new StaffSpot("Surgeon_Male", "Talking", 0, new Vector3(1.7f, 0f, -0.7f), new Vector3(2.6f, 0f, -0.3f)),
            new StaffSpot("Surgeon_Female", "Idle", 0, new Vector3(2.6f, 0f, -0.3f), new Vector3(1.7f, 0f, -0.7f)),
            new StaffSpot("Nurse_Female_White", "Idle", 0, new Vector3(5.6f, 0f, 5.2f), new Vector3(5.6f, 0f, 0f)),

            new StaffSpot("Doctor_Male", "Talking", 1, new Vector3(10.8f, 0f, 0.8f), new Vector3(11.7f, 0f, 0.9f)),
            new StaffSpot("Nurse_Female_Scrubs", "Idle", 1, new Vector3(11.7f, 0f, 0.9f), new Vector3(10.8f, 0f, 0.8f)),
            new StaffSpot("Nurse_Male_White", "Idle", 1, new Vector3(16.6f, 0f, 0.7f), new Vector3(0f, 0f, 1.5f)),

            new StaffSpot("Nurse_Female_White", "Talking", 2, new Vector3(6.6f, 0f, 0.8f), new Vector3(7.5f, 0f, 0.8f)),
            new StaffSpot("Doctor_Female", "Idle", 2, new Vector3(7.5f, 0f, 0.8f), new Vector3(6.6f, 0f, 0.8f)),
            new StaffSpot("Nurse_Male_Scrubs", "Idle", 2, new Vector3(16.4f, 0f, 2.2f), new Vector3(0f, 0f, 1.5f)),

            new StaffSpot("Doctor_Female", "Talking", 3, new Vector3(12.0f, 0f, 0.8f), new Vector3(12.9f, 0f, 0.9f)),
            new StaffSpot("Nurse_Male_White", "Idle", 3, new Vector3(12.9f, 0f, 0.9f), new Vector3(12.0f, 0f, 0.8f)),
            new StaffSpot("Nurse_Female_Scrubs", "Idle", 3, new Vector3(6.5f, 0f, 0.6f), new Vector3(6.5f, 0f, 3f)),
        };

        [MenuItem("ProCommVR/Hospital/Place Characters")]
        public static void PlaceCharactersMenu()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (root == null)
            {
                Debug.LogError($"{LogPrefix}Geen '{RootName}' in de scene. Draai eerst ProCommVR > Hospital > Build.");
                return;
            }
            _cases = LoadCases();
            PlaceCharacters(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            TakeScreenshots();
        }

        private static void PlaceCharacters(Transform root)
        {
            var old = root.Find(CharactersRootName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var characters = new GameObject(CharactersRootName).transform;
            characters.SetParent(root, false);

            try
            {
                foreach (var patientCase in _cases.Values.OrderBy(c => c.DoorLabel))
                    PlacePatient(root, characters, patientCase);
                for (var i = 0; i < Staff.Length; i++)
                    PlaceStaff(characters, Staff[i], i);
            }
            finally
            {
                StopSamplingKeepRoots();
                foreach (var probe in _probes)
                    Object.DestroyImmediate(probe);
                _probes.Clear();
            }
        }

        // --- Patiënten ------------------------------------------------------------

        private static void PlacePatient(Transform root, Transform characters, PatientCase patientCase)
        {
            var label = patientCase.DoorLabel;
            var room = root.Find($"Floor{label.Substring(0, 1)}/Room_{label}");
            if (room == null || patientCase.PatientPrefab == null)
            {
                Debug.LogWarning($"{LogPrefix}Case {label}: kamer of personage ontbreekt, overgeslagen.");
                return;
            }

            var patient = (GameObject)PrefabUtility.InstantiatePrefab(patientCase.PatientPrefab, characters);
            patient.name = $"Patient_{label}";
            var state = patientCase.StartPose == StartPose.Laying ? "Laying" : "Sitting";
            patient.AddComponent<CharacterPose>().Configure(state, 0f);

            // Meten op een kopie: AnimationMode zet een gesampled object bij het stoppen terug naar zijn beginstand
            var probe = Probe(patientCase.PatientPrefab, patient.name);
            if (!SamplePose(probe, state, 0f))
                return;
            if (patientCase.StartPose == StartPose.Laying)
                LayOnBed(probe, room, label);
            else
                SitOnChair(probe, room, label);
            patient.transform.SetPositionAndRotation(probe.transform.position, probe.transform.rotation);
        }

        /// <summary>
        /// Lichaam in de lengte van het bed (hoofd naar het hoofdeinde), midden op het matras,
        /// en met het laagste punt van het lichaam net op het matras.
        /// </summary>
        private static void LayOnBed(GameObject patient, Transform room, string label)
        {
            var anchor = room.Find("Anchor_Laying");
            var animator = patient.GetComponent<Animator>();
            patient.transform.SetPositionAndRotation(new Vector3(anchor.position.x, room.position.y, anchor.position.z), room.rotation);

            var head = animator.GetBoneTransform(HumanBodyBones.Head).position;
            var feet = (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position + animator.GetBoneTransform(HumanBodyBones.RightFoot).position) / 2f;
            var bodyAxis = Vector3.ProjectOnPlane(head - feet, Vector3.up);
            patient.transform.Rotate(0f, Vector3.SignedAngle(bodyAxis, room.forward, Vector3.up), 0f, Space.World);

            head = animator.GetBoneTransform(HumanBodyBones.Head).position;
            feet = (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position + animator.GetBoneTransform(HumanBodyBones.RightFoot).position) / 2f;
            var middle = (head + feet) / 2f;
            patient.transform.position += new Vector3(anchor.position.x - middle.x, 0f, anchor.position.z - middle.z);

            var lowest = MinVertexY(patient);
            patient.transform.position += Vector3.up * (anchor.position.y + LayingClearance - lowest);
            Debug.Log($"{LogPrefix}Patient_{label} ligt op het bed: laagste punt {MinVertexY(patient) - room.position.y:0.000} m, " +
                      $"matras {anchor.position.y - room.position.y:0.000} m, root {patient.transform.position.y - room.position.y:0.000} m boven de vloer.");
        }

        /// <summary>
        /// De stoel draait naar de deur (de speler komt daar binnen), de patiënt zit erop met de heupen op HipsAboveSeat
        /// boven het zitvlak. Zakken de voeten door de vloer, dan gaat het personage omhoog (positie, niet de animatie).
        /// </summary>
        private static void SitOnChair(GameObject patient, Transform room, string label)
        {
            var anchor = room.Find("Anchor_Sitting");
            var chair = room.Find("Furniture/Chair");
            // Eerst terug naar de stand uit de prefab, zodat opnieuw draaien niet blijft optellen
            var source = PrefabUtility.GetCorrespondingObjectFromSource(chair);
            if (source != null)
                chair.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            Physics.SyncTransforms();

            var seatCenter = HospitalAssetPrep.WorldBounds(chair.gameObject).center;
            var towardDoor = Vector3.ProjectOnPlane(room.position - seatCenter, Vector3.up).normalized;
            chair.RotateAround(seatCenter, Vector3.up, Vector3.SignedAngle(anchor.forward, towardDoor, Vector3.up));

            var animator = patient.GetComponent<Animator>();
            patient.transform.SetPositionAndRotation(new Vector3(seatCenter.x, room.position.y, seatCenter.z), Quaternion.LookRotation(towardDoor));
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            var target = new Vector3(seatCenter.x, anchor.position.y + HipsAboveSeat, seatCenter.z) - towardDoor * SeatBackOffset;
            patient.transform.position += target - hips;

            var feetBelow = room.position.y - MinVertexY(patient);
            if (feetBelow > FloorTolerance)
                patient.transform.position += Vector3.up * feetBelow;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            Debug.Log($"{LogPrefix}Patient_{label} zit op de stoel: heupen op {hips.y - room.position.y:0.000} m, " +
                      $"zitvlak {anchor.position.y - room.position.y:0.000} m, laagste punt {MinVertexY(patient) - room.position.y:0.000} m" +
                      (feetBelow > FloorTolerance ? $" (opgetild met {feetBelow:0.000} m)." : "."));
        }

        // --- Zorgverleners ------------------------------------------------------------

        private static void PlaceStaff(Transform characters, StaffSpot spot, int index)
        {
            var path = $"{CharacterFolder}/{spot.Prefab}/{spot.Prefab}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"{LogPrefix}Personage niet gevonden: {path}");
                return;
            }

            var levelY = spot.Level * FloorHeight;
            var position = spot.Position + Vector3.up * levelY;
            var look = Vector3.ProjectOnPlane(spot.LookAt - spot.Position, Vector3.up);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, characters);
            go.name = $"{(spot.Level == 0 ? "Lobby" : $"Floor{spot.Level}")}_{spot.Prefab}";
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look));

            // Deterministische spreiding zodat dezelfde animaties niet synchroon lopen
            var offset = (index * 0.37f) % 1f;
            go.AddComponent<CharacterPose>().Configure(spot.State, offset);
            var probe = Probe(prefab, go.name);
            probe.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
            if (SamplePose(probe, spot.State, offset))
                go.transform.position += Vector3.up * (levelY - MinVertexY(probe));

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = StaffColliderRadius;
            capsule.height = StaffColliderHeight;
            capsule.center = Vector3.up * (StaffColliderHeight / 2f);
        }

        private static readonly List<GameObject> _probes = new List<GameObject>();

        /// <summary>Tijdelijke kopie om de pose op te meten. Wordt opgeruimd na het stoppen van AnimationMode.</summary>
        private static GameObject Probe(GameObject prefab, string name)
        {
            var probe = Object.Instantiate(prefab);
            probe.name = $"Probe_{name}";
            probe.hideFlags = HideFlags.DontSave;
            _probes.Add(probe);
            return probe;
        }

        // --- Pose tonen in de editor --------------------------------------------------------
        //
        // De pose komt uit AnimationMode (zoals het Animation-venster): die zet bij het stoppen alles terug, ook de root.
        // Daarom worden de roots na het stoppen opnieuw gezet. Voor screenshots worden de geposeerde lichamen
        // tijdelijk als gewone meshes gebakken, zodat de camera ze in pose rendert. De scene zelf blijft in T-pose;
        // in Play mode neemt de Animator (via CharacterPose) het over.

        private static readonly List<GameObject> _poseProxies = new List<GameObject>();
        private static readonly List<SkinnedMeshRenderer> _hiddenSkins = new List<SkinnedMeshRenderer>();

        /// <summary>Voor screenshots: alle personages met een CharacterPose in hun pose tonen.</summary>
        private static void BeginPosePreview()
        {
            foreach (var pose in Object.FindObjectsByType<CharacterPose>(FindObjectsSortMode.None))
            {
                if (!SamplePose(pose.gameObject, pose.State, pose.StartOffset))
                    continue;
                foreach (var skin in pose.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh, true);
                    var proxy = new GameObject($"PoseProxy_{pose.name}");
                    proxy.hideFlags = HideFlags.DontSave;
                    proxy.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                    proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
                    proxy.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    _poseProxies.Add(proxy);
                    skin.enabled = false;
                    _hiddenSkins.Add(skin);
                }
            }
            StopSamplingKeepRoots();
        }

        private static void EndPosePreview()
        {
            foreach (var proxy in _poseProxies)
            {
                Object.DestroyImmediate(proxy.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(proxy);
            }
            _poseProxies.Clear();
            foreach (var skin in _hiddenSkins)
                if (skin != null)
                    skin.enabled = true;
            _hiddenSkins.Clear();
            StopSamplingKeepRoots();
        }

        /// <summary>
        /// Stopt AnimationMode, maar houdt de positie en rotatie van elk personage zoals ze nu zijn.
        /// De roots worden daarna expliciet als override van hun prefab-instantie vastgelegd: een wijziging tijdens
        /// AnimationMode telt als preview, en zonder deze stap kan de opgeslagen scene de personages op (0, 0, 0) zetten.
        /// </summary>
        private static void StopSamplingKeepRoots()
        {
            if (!AnimationMode.InAnimationMode())
                return;
            var roots = Object.FindObjectsByType<CharacterPose>(FindObjectsSortMode.None)
                .Select(p => (p.transform, p.transform.position, p.transform.rotation)).ToList();
            AnimationMode.StopAnimationMode();
            foreach (var (t, position, rotation) in roots)
            {
                t.SetPositionAndRotation(position, rotation);
                if (PrefabUtility.IsPartOfPrefabInstance(t))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                EditorUtility.SetDirty(t);
            }
        }

        /// <summary>
        /// Zet het personage in de pose van een state op het gegeven moment (0-1). De root blijft waar hij stond.
        /// Geeft false als de state geen clip heeft.
        /// </summary>
        private static bool SamplePose(GameObject character, string state, float normalizedTime)
        {
            var animator = character.GetComponent<Animator>();
            var clip = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == state)
                : null;
            if (clip == null)
            {
                Debug.LogWarning($"{LogPrefix}{character.name}: geen animatie '{state}' in de controller.");
                return false;
            }
            var position = character.transform.position;
            var rotation = character.transform.rotation;
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(character, clip, clip.length * normalizedTime);
            AnimationMode.EndSampling();
            character.transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        /// <summary>Laagste punt van het geposeerde lichaam in wereldcoördinaten.</summary>
        private static float MinVertexY(GameObject character)
        {
            var min = float.MaxValue;
            var mesh = new Mesh();
            foreach (var skin in character.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(mesh, true);
                var matrix = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices)
                    min = Mathf.Min(min, matrix.MultiplyPoint3x4(v).y);
            }
            Object.DestroyImmediate(mesh);
            return min;
        }
    }
}
