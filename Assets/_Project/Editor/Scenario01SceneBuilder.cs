using System.Collections.Generic;
using ProCommVR.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Builds Scenario01_PatientRoom: a 4 x 4 m patient room (GAME_BRIEF.md, Scenario 1) assembled from the
    /// third-party assets, the placeholder patient on the bed and the scenario system.
    /// Batchmode: -executeMethod ProCommVR.Editor.Scenario01SceneBuilder.BuildScene
    /// Requires the prefabs from ScenarioTestSceneBuilder (bubble, results screen) and the XR rig variant.
    ///
    /// Layout (meters, y up): room interior x -2..2, z 0..4. Door in the south wall (z = 0), bed with its head
    /// against the north wall, window in the east wall. The player starts inside the door, about 2.5 m from the patient.
    /// </summary>
    public static class Scenario01SceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Scenario01_PatientRoom.unity";
        private const string ScenarioPath = "Assets/_Project/Dialogue/Scenarios/Scenario01_DisappointedPatient.asset";
        private const string MaterialFolder = "Assets/_Project/Materials/PatientRoom";

        // Room structure (walls, floor, window, ceiling lights) and hospital props: modular kit by UgurYksk.
        private const string RoomKitPath = "Assets/ThirdParty/Sketchfab/HospitalRoom_UgurYksk/HospitalScene.dae";
        private const string RoomKitTextures = "Assets/ThirdParty/Sketchfab/HospitalRoom_UgurYksk/textures";
        private const string DoorPath = "Assets/ThirdParty/Sketchfab/HospitalDoor_anasrar/source/untitled.fbx";
        private const string BedPath = "Assets/ThirdParty/Sketchfab/HospitalBed_anshartis/source/BED ASSEST_2.fbx";
        private const string GraddPath = "Assets/ThirdParty/GRADD/HospitalRoom/Medical-Hospital-Equipment-001.obj";
        private const string KenneyFolder = "Assets/ThirdParty/Kenney/FurnitureKit";

        private const float RoomSize = 4f;
        private const float TileSize = 2f;        // one kit floor tile / wall segment
        private const float InchToMeter = 0.0254f; // the GRADD OBJ is modelled in inches
        private const float BedLength = 2.1f;
        private const float MattressHeight = 0.62f;

        private static readonly Vector3 PlayerStart = new Vector3(-1f, 0f, 0.5f);
        private static readonly Vector3 PatientSeat = new Vector3(-0.15f, MattressHeight, 3f);
        private const float BedCenterX = -0.6f;

        private static readonly Dictionary<string, Material> KitMaterials = new Dictionary<string, Material>();

        [MenuItem("ProCommVR/Setup/7. Build Scenario 01 Patient Room")]
        public static void BuildScene()
        {
            // Create the scene first: NewScene unloads assets that were loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            KitMaterials.Clear();

            var rigVariant = ScenarioTestSceneBuilder.LoadRequired<GameObject>(ScenarioTestSceneBuilder.RigVariantPath, "Run 'Build XR Test Scene' first.");
            var scenario = ScenarioTestSceneBuilder.LoadRequired<Scenario>(ScenarioPath, "The Scenario 1 asset is missing.");

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;

            var wallHeight = BuildRoom();
            BuildProps();

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigVariant, scene);
            var toPatient = PatientSeat - PlayerStart;
            toPatient.y = 0f;
            rig.transform.SetPositionAndRotation(PlayerStart, Quaternion.LookRotation(toPatient));
            new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));

            var patient = BuildPatient(out var poses);

            var presenter = ScenarioTestSceneBuilder.AddScenarioSystem(scene, patient,
                new Vector3(PatientSeat.x, 2.05f, PatientSeat.z),
                new Vector3(BedCenterX, 1.15f, 1.9f),
                new[] { scenario }, light);

            // The NPC line faces the door, where the player starts.
            var npcAnchor = presenter.transform.Find("NPC Bubble Anchor");
            npcAnchor.rotation = Quaternion.LookRotation(toPatient);

            var reactor = patient.parent.gameObject.AddComponent<NpcReactor>();
            var voice = patient.parent.gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            var serialized = new SerializedObject(reactor);
            serialized.FindProperty("_presenter").objectReferenceValue = presenter;
            serialized.FindProperty("_body").objectReferenceValue = patient;
            serialized.FindProperty("_voice").objectReferenceValue = voice;
            var poseList = serialized.FindProperty("_poses");
            poseList.arraySize = poses.Count;
            for (var i = 0; i < poses.Count; i++)
            {
                var element = poseList.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Pose").enumValueIndex = (int)poses[i].Key;
                element.FindPropertyRelative("Target").objectReferenceValue = poses[i].Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[ProCommVR] Patient room built, wall height {wallHeight:F2} m.");
            ScenarioTestSceneBuilder.SaveSceneFirstInBuild(scene, ScenePath);
        }

        /// <summary>
        /// Floor, walls (door wall south, window wall east), ceiling, ceiling lights and invisible colliders.
        /// Returns the wall height.
        /// </summary>
        private static float BuildRoom()
        {
            var room = new GameObject("Room").transform;
            var kit = InstantiateTemplate(RoomKitPath);
            try
            {
                // Scale the kit so one floor tile is exactly TileSize wide.
                var kitScale = TileSize / TightBounds(kit.transform.Find("Ground.001").gameObject).size.x;
                var half = RoomSize * 0.5f;

                for (var x = 0; x < 2; x++)
                for (var z = 0; z < 2; z++)
                {
                    var tile = PlaceKitPieces(kit, room, "Floor Tile", kitScale, 0f, "Ground.001");
                    Align(tile, new Vector3(0f, 1f, 0f), new Vector3(-half + x * TileSize, 0f, z * TileSize));
                }

                // Wall segments are modelled along z. Pivot: which point of the segment's bounds lands on the target.
                // South wall (z = 0): the segment at x -2..0 holds the door; we swap the kit door for the anasrar door.
                var doorWall = PlaceKitPieces(kit, room, "Wall South (door)", kitScale, 90f, "DoorWall.001", "Door.001");
                Align(doorWall, new Vector3(0f, 0f, 1f), new Vector3(-half, 0f, 0f));
                var kitDoor = doorWall.transform.Find("Door.001");
                PlaceDoor(room, TightBounds(kitDoor.gameObject));
                Object.DestroyImmediate(kitDoor.gameObject);

                Align(PlaceKitPieces(kit, room, "Wall South", kitScale, 90f, "Wall3.001"), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 0f));
                Align(PlaceKitPieces(kit, room, "Wall North", kitScale, 90f, "Wall3.001"), new Vector3(0f, 0f, 0f), new Vector3(-half, 0f, RoomSize));
                Align(PlaceKitPieces(kit, room, "Wall North", kitScale, 90f, "Wall3.001"), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, RoomSize));
                Align(PlaceKitPieces(kit, room, "Wall West", kitScale, 0f, "Wall3.001"), new Vector3(1f, 0f, 0f), new Vector3(-half, 0f, 0f));
                Align(PlaceKitPieces(kit, room, "Wall West", kitScale, 0f, "Wall3.001"), new Vector3(1f, 0f, 0f), new Vector3(-half, 0f, TileSize));
                Align(PlaceKitPieces(kit, room, "Wall East", kitScale, 0f, "Wall3.001"), new Vector3(0f, 0f, 0f), new Vector3(half, 0f, 0f));
                // East wall at the head end of the bed: the window the patient looks out of.
                Align(PlaceKitPieces(kit, room, "Wall East (window)", kitScale, 180f, "WindowWall.001"), new Vector3(0f, 0f, 0f), new Vector3(half, 0f, TileSize));

                var wallHeight = TightBounds(doorWall).size.y;

                var lights = PlaceKitPieces(kit, room, "Ceiling Light", kitScale, 90f, "LightCube.001", "LightCube.002");
                Align(lights, new Vector3(0.5f, 1f, 0.5f), new Vector3(0f, wallHeight, RoomSize * 0.5f));

                var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ceiling.name = "Ceiling";
                ceiling.transform.SetParent(room, false);
                ceiling.transform.SetPositionAndRotation(new Vector3(0f, wallHeight, RoomSize * 0.5f), Quaternion.Euler(180f, 0f, 0f));
                ceiling.transform.localScale = new Vector3(RoomSize / 10f, 1f, RoomSize / 10f);
                Object.DestroyImmediate(ceiling.GetComponent<Collider>());
                ceiling.GetComponent<Renderer>().sharedMaterial = XRTestSceneBuilder.GetOrCreateMaterial($"{MaterialFolder}/Ceiling.mat", new Color(0.92f, 0.92f, 0.9f));

                AddColliders(room, wallHeight);
                return wallHeight;
            }
            finally
            {
                Object.DestroyImmediate(kit);
            }
        }

        /// <summary>
        /// The kit meshes have no colliders: an invisible floor so the rig does not fall, and walls so
        /// thumbstick movement stays inside the room.
        /// </summary>
        private static void AddColliders(Transform room, float wallHeight)
        {
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(room, false);
            const float thickness = 0.2f;
            var half = RoomSize * 0.5f;
            AddBox(colliders, "Floor", new Vector3(0f, -thickness * 0.5f, half), new Vector3(RoomSize, thickness, RoomSize));
            AddBox(colliders, "South", new Vector3(0f, wallHeight * 0.5f, -thickness * 0.5f), new Vector3(RoomSize, wallHeight, thickness));
            AddBox(colliders, "North", new Vector3(0f, wallHeight * 0.5f, RoomSize + thickness * 0.5f), new Vector3(RoomSize, wallHeight, thickness));
            AddBox(colliders, "West", new Vector3(-half - thickness * 0.5f, wallHeight * 0.5f, half), new Vector3(thickness, wallHeight, RoomSize));
            AddBox(colliders, "East", new Vector3(half + thickness * 0.5f, wallHeight * 0.5f, half), new Vector3(thickness, wallHeight, RoomSize));
        }

        private static void AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var box = new GameObject(name).AddComponent<BoxCollider>();
            box.transform.SetParent(parent, false);
            box.transform.position = center;
            box.size = size;
        }

        /// <summary>
        /// Fits the anasrar door into the opening of the kit's door wall (the box of the kit door it replaces).
        /// </summary>
        private static void PlaceDoor(Transform room, Bounds opening)
        {
            var door = new GameObject("Door").transform;
            door.SetParent(room, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DoorPath));
            model.transform.SetParent(door, false);

            var size = TightBounds(door.gameObject).size;
            door.localScale = new Vector3(opening.size.x / size.x, opening.size.y / size.y, Mathf.Max(opening.size.z, 0.05f) / size.z);
            Align(door.gameObject, new Vector3(0.5f, 0f, 0.5f), new Vector3(opening.center.x, opening.min.y, opening.center.z));
        }

        private static void BuildProps()
        {
            var props = new GameObject("Props").transform;
            var half = RoomSize * 0.5f;

            // Bed (anshartis): head end against the north wall. The model's head end points to -x, so turn it 90 degrees.
            var bed = PlaceModel(BedPath, props, "Hospital Bed", 90f);
            var bedSize = TightBounds(bed).size;
            bed.transform.localScale *= BedLength / bedSize.z;
            Align(bed, new Vector3(0.5f, 0f, 1f), new Vector3(BedCenterX, 0f, RoomSize - 0.05f));

            // Hospital props from the same kit as the room, so the style matches.
            var kit = InstantiateTemplate(RoomKitPath);
            try
            {
                var kitScale = TileSize / TightBounds(kit.transform.Find("Ground.001").gameObject).size.x;
                Align(PlaceKitPieces(kit, props, "IV Pole", kitScale, 0f, "Circle.002", "Circle.003"),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(BedCenterX - 0.75f, 0f, 3.4f));
                Align(PlaceKitPieces(kit, props, "Patient Monitor", kitScale, 180f, "Cube.003", "Cube.007"),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(-half + 0.35f, 0f, 2.5f));
                Align(PlaceKitPieces(kit, props, "Bedside Cabinet", kitScale, 180f, "Cube.001"),
                    new Vector3(0.5f, 0f, 1f), new Vector3(0.45f, 0f, RoomSize - 0.05f));
                Align(PlaceKitPieces(kit, props, "Overbed Table", kitScale, 180f, "Cube.002"),
                    new Vector3(0f, 0f, 0.5f), new Vector3(-half + 0.05f, 0f, 1.5f));
            }
            finally
            {
                Object.DestroyImmediate(kit);
            }

            // Hospital props from the GRADD set.
            var drawers = PlaceGradd(props, "Drawers On Wheels", "Drawers_on_Wheels_001", 0f);
            Align(drawers, new Vector3(1f, 0f, 0.5f), new Vector3(half - 0.05f, 0f, 0.7f));
            var bin = PlaceGradd(props, "Biohazard Bin", "Biohazard_Bin_001", 0f);
            Align(bin, new Vector3(0.5f, 0f, 0f), new Vector3(0.35f, 0f, 0.1f));

            // Generic furniture (Kenney): a visitor chair by the window and a plant in the corner.
            var chair = PlaceModel($"{KenneyFolder}/chair.fbx", props, "Visitor Chair", -90f);
            ScaleToHeight(chair, 0.9f);
            Align(chair, new Vector3(0.5f, 0f, 0.5f), new Vector3(half - 0.45f, 0f, 2.3f));
            var plant = PlaceModel($"{KenneyFolder}/pottedPlant.fbx", props, "Plant", 0f);
            ScaleToHeight(plant, 0.7f);
            Align(plant, new Vector3(1f, 0f, 1f), new Vector3(half - 0.1f, 0f, RoomSize - 0.1f));
        }

        /// <summary>
        /// Placeholder patient (capsule, no character model yet) sitting slumped on the edge of the bed,
        /// looking out of the window. A small dark visor shows which way the patient faces.
        /// Also creates one target per NpcPose for the NpcReactor.
        /// </summary>
        private static Transform BuildPatient(out List<KeyValuePair<NpcPose, Transform>> poses)
        {
            var root = new GameObject("Patient X (placeholder)").transform;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root, false);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = ScenarioTestSceneBuilder.GetOrCreatePlaceholderNpcMaterial();

            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Face Direction";
            visor.transform.SetParent(body.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.6f, 0.42f);
            visor.transform.localScale = new Vector3(0.6f, 0.12f, 0.2f);
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.GetComponent<Renderer>().sharedMaterial = XRTestSceneBuilder.GetOrCreateMaterial($"{MaterialFolder}/PlaceholderNpcFace.mat", new Color(0.15f, 0.18f, 0.25f));

            // The capsule is 2 m tall at scale 1: 0.45 makes it 0.9 m, the upper body of a seated person.
            body.transform.localScale = new Vector3(0.4f, 0.45f, 0.4f);
            var seatCenter = PatientSeat + Vector3.up * 0.42f;
            var toWindow = Vector3.right;
            var toPlayer = PlayerStart - PatientSeat;
            toPlayer.y = 0f;

            // Start pose: slumped forward, looking out of the window (Scenario 1 source document).
            body.transform.SetPositionAndRotation(seatCenter, Lean(toWindow, 15f));

            var poseRoot = new GameObject("Poses").transform;
            poseRoot.SetParent(root, false);
            poses = new List<KeyValuePair<NpcPose, Transform>>
            {
                // Sits up and turns to the nurse.
                new KeyValuePair<NpcPose, Transform>(NpcPose.Engaged, CreatePose(poseRoot, "Engaged", seatCenter + Vector3.up * 0.04f, Lean(toPlayer, 0f))),
                // Turns his back to the nurse.
                new KeyValuePair<NpcPose, Transform>(NpcPose.TurnsAway, CreatePose(poseRoot, "Turns Away", seatCenter, Lean(-toPlayer, 10f))),
                // Sinks further down, back to the window.
                new KeyValuePair<NpcPose, Transform>(NpcPose.Withdrawn, CreatePose(poseRoot, "Withdrawn", seatCenter - Vector3.up * 0.04f, Lean(toWindow, 30f))),
            };
            return body.transform;
        }

        private static Quaternion Lean(Vector3 facing, float forwardTilt)
        {
            return Quaternion.LookRotation(facing.normalized) * Quaternion.Euler(forwardTilt, 0f, 0f);
        }

        private static Transform CreatePose(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var pose = new GameObject(name).transform;
            pose.SetParent(parent, false);
            pose.SetPositionAndRotation(position, rotation);
            return pose;
        }

        // ---- Asset placement helpers -------------------------------------------------------------------

        private static GameObject InstantiateTemplate(string path)
        {
            var asset = ScenarioTestSceneBuilder.LoadRequired<GameObject>(path, "Third-party asset missing, see CREDITS.md.");
            var instance = (GameObject)Object.Instantiate(asset);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return instance;
        }

        /// <summary>
        /// Copies the named pieces of the room kit into a new group (keeping their relative layout),
        /// scales and turns the group, and gives the pieces URP materials. Position it afterwards with Align.
        /// </summary>
        private static GameObject PlaceKitPieces(GameObject kit, Transform parent, string name, float scale, float yaw, params string[] pieces)
        {
            var group = new GameObject(name);
            foreach (var pieceName in pieces)
            {
                var source = kit.transform.Find(pieceName);
                var piece = new GameObject(pieceName);
                piece.transform.SetPositionAndRotation(source.position, source.rotation);
                piece.transform.localScale = source.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var materials = source.GetComponent<MeshRenderer>().sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = GetOrCreateKitMaterial(materials[i].name);
                piece.AddComponent<MeshRenderer>().sharedMaterials = materials;
                piece.transform.SetParent(group.transform, true);
            }
            group.transform.localScale = Vector3.one * scale;
            group.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            group.transform.SetParent(parent, true);
            return group;
        }

        /// <summary>
        /// The kit's own materials use the built-in Standard shader (pink in URP). Recreate them as URP Simple Lit
        /// with the kit's albedo (and emission) textures. Normal maps are left out for performance on the Quest.
        /// </summary>
        private static Material GetOrCreateKitMaterial(string name)
        {
            if (KitMaterials.TryGetValue(name, out var cached))
                return cached;

            var path = $"{MaterialFolder}/Kit_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                XRTestSceneBuilder.EnsureFolder(MaterialFolder);
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{RoomKitTextures}/{name}_albedo.jpg"));
                material.SetFloat("_Smoothness", 0.2f);
                var emission = AssetDatabase.LoadAssetAtPath<Texture2D>($"{RoomKitTextures}/{name}_emissive.jpg");
                if (emission != null)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetTexture("_EmissionMap", emission);
                    material.SetColor("_EmissionColor", Color.white);
                }
                AssetDatabase.CreateAsset(material, path);
            }
            KitMaterials[name] = material;
            return material;
        }

        internal static GameObject PlaceModel(string path, Transform parent, string name, float yaw)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(ScenarioTestSceneBuilder.LoadRequired<GameObject>(path, "Third-party asset missing, see CREDITS.md."));
            model.name = name;
            model.transform.SetParent(parent, false);
            model.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.rotation;
            return model;
        }

        /// <summary>
        /// The GRADD OBJ is one big file with all props side by side: keep only the meshes of one prop.
        /// </summary>
        internal static GameObject PlaceGradd(Transform parent, string name, string prop, float yaw)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(ScenarioTestSceneBuilder.LoadRequired<GameObject>(GraddPath, "Third-party asset missing, see CREDITS.md."));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = name;
            for (var i = model.transform.childCount - 1; i >= 0; i--)
            {
                var child = model.transform.GetChild(i);
                if (!child.name.Contains(prop))
                    Object.DestroyImmediate(child.gameObject);
            }
            model.transform.SetParent(parent, false);
            model.transform.localScale = Vector3.one * InchToMeter;
            model.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return model;
        }

        internal static void ScaleToHeight(GameObject go, float height)
        {
            go.transform.localScale *= height / TightBounds(go).size.y;
        }

        /// <summary>
        /// Moves the object so that a point of its bounds lands on the target. The pivot is relative to the bounds:
        /// (0, 0, 0) is the minimum corner, (1, 1, 1) the maximum, (0.5, 0, 0.5) the bottom center.
        /// </summary>
        internal static void Align(GameObject go, Vector3 pivot, Vector3 target)
        {
            var bounds = TightBounds(go);
            var point = bounds.min + Vector3.Scale(bounds.size, pivot);
            go.transform.position += target - point;
        }

        /// <summary>
        /// World bounds from the actual vertices. Renderer.bounds is too loose for rotated meshes.
        /// </summary>
        internal static Bounds TightBounds(GameObject go)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;
                var matrix = filter.transform.localToWorldMatrix;
                foreach (var vertex in mesh.vertices)
                {
                    var point = matrix.MultiplyPoint3x4(vertex);
                    if (first)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
            return bounds;
        }
    }
}
