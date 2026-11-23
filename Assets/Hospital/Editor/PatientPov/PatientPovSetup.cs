using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProCommVR.Emotions;
using ProCommVR.Hospital.Face;
using ProCommVR.Player;
using ProCommVR.Pov;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// Patient POV plus (Liam): adds to a scene with Jalil's patient view (EmotionHolder, PovController,
    /// PatientWorldController) everything that makes the patient's emotion felt beyond color: his face and gaze
    /// (EmotionFaceDriver), the nurse as he perceives her (PerceivedNurse), the granddaughter's card, photo, balloon and
    /// the party clock (StoryProps, StoryClock), a heartbeat, muffled sound and an inner voice (HeartbeatFeedback,
    /// MuffledWorld, InnerVoice), and in the test scene a read-only face/world label.
    /// Called with one line from Scenario01SceneBuilder.Build and ShiftHospitalSceneBuilder.BuildPatientView.
    /// The data assets are created once (tuned values are kept): Assets/Hospital/Data/PatientPov/.
    /// </summary>
    public static class PatientPovSetup
    {
        public const string PatientPrefabPath = "Assets/Hospital/Prefabs/PatientPov/PatientX_Scenario1.prefab";
        private const string PatientSourcePath = "Assets/Character/Patient_Male_Old/Patient_Male_Old.prefab";
        private const string DataFolder = "Assets/Hospital/Data/PatientPov";
        private const string ArtFolder = "Assets/Hospital/Art/PatientPov";
        private const string AudioFolder = "Assets/Hospital/Audio/PatientPov";
        private const string ClockPrefabPath = "Assets/Hospital/Art/Props/Prefabs/Wall_Clock.prefab";
        private const string HudTextMaterialPath = "Assets/_Project/Materials/UI/HudOverlay_Text.mat";
        private const string HudImageMaterialPath = "Assets/_Project/Materials/UI/HudOverlay.mat";
        private const string SimpleLit = "Universal Render Pipeline/Simple Lit";

        public static string FaceMappingPath => $"{DataFolder}/EmotionFaceMapping.asset";
        public static string NurseProfilePath => $"{DataFolder}/PerceivedNurseProfile.asset";
        public static string StoryPropsPath => $"{DataFolder}/ScenarioOneStoryProps.asset";
        public static string InnerVoicePath => $"{DataFolder}/InnerVoiceLines.asset";

        /// <summary>
        /// Adds Patient POV plus to the scene. room: the shift's patient room (Room_1A), or null for the standalone
        /// Scenario 1 room (Scenario01_PatientRoom and PatientViewTest). Returns the root object.
        /// </summary>
        public static GameObject AddToScene(EmotionHolder holder, PovController pov, Transform patient, Transform room, Transform camera)
        {
            return Add(holder, pov, patient, room, room == null ? Layout.Standalone() : Layout.Of(room), true, true);
        }

        /// <summary>
        /// The same for a room of Liam's U-ward (Prototype1): the window is found by name under the hospital
        /// (Window_1A_1), the clock goes on a side wall (raycast). Without the perceived nurse: in Prototype1 the player
        /// in the patient view is Jalil's white figure with the trust aura, which already shows how the patient sees him.
        /// </summary>
        public static GameObject AddToWardRoom(EmotionHolder holder, PovController pov, Transform patient, Transform room)
        {
            return Add(holder, pov, patient, room, Layout.OfWard(room), false, false);
        }

        private static GameObject Add(EmotionHolder holder, PovController pov, Transform patient, Transform room, Layout layout,
            bool perceivedNurse, bool debugLabel)
        {
            if (holder == null || pov == null || patient == null)
                throw new System.InvalidOperationException("[PatientPov] AddToScene needs the EmotionHolder, the PovController and the patient.");
            EnsureAssets();
            var world = Object.FindObjectsByType<PatientWorldController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(w => new SerializedObject(w).FindProperty("_holder").objectReferenceValue == holder);

            var root = new GameObject(room == null ? "Patient POV Plus" : $"Patient POV Plus ({room.name})");
            if (room != null)
                root.transform.SetParent(room, true);

            var driver = AddFace(holder, pov, world, patient, layout, root.transform);
            if (debugLabel)
                AddDebugLabel(root, driver, world, pov);
            if (perceivedNurse)
                AddPerceivedNurse(holder, pov, world, patient);
            AddStoryProps(holder, pov, world, layout, root.transform);
            var clockSource = AddClock(holder, pov, world, layout, root.transform);
            AddSound(holder, pov, world, patient, root.transform, clockSource);
            return root;
        }

        // ---- Layout ---------------------------------------------------------------------------------------

        /// <summary>Where things go, measured from the room's own furniture (no fixed coordinates in the shift).</summary>
        private struct Layout
        {
            public Bounds Nightstand;
            public Vector3 WindowPoint;     // middle of the window
            public Vector3 Outward;         // from the room out through the window (flat)
            public Vector3 DoorPoint;       // middle of the doorway, at eye height
            public bool BalloonOutside;     // clear glass: the balloon floats outside; closed blind: inside, tied to the chair
            public Vector3 BalloonAnchor;   // where the string is tied
            public Vector3 ClockPosition;   // on the wall
            public Vector3 ClockFront;      // the clock face looks this way
            public float Floor;

            public static Layout Standalone()
            {
                // Scenario01SceneBuilder: room x -2..2, z 0..4, window in the east wall (x = 2, z 2..4, closed blind),
                // door in the south wall at x -1, visitor chair by the window, bedside cabinet at the head of the bed.
                var cabinet = GameObject.Find("Props/Bedside Cabinet");
                var chair = GameObject.Find("Props/Visitor Chair");
                var chairBounds = chair != null ? Measure(chair) : new Bounds(new Vector3(1.55f, 0.45f, 2.3f), new Vector3(0.5f, 0.9f, 0.5f));
                return new Layout
                {
                    Nightstand = cabinet != null ? Measure(cabinet) : new Bounds(new Vector3(0.45f, 0.35f, 3.75f), new Vector3(0.45f, 0.7f, 0.4f)),
                    WindowPoint = new Vector3(2f, 1.75f, 3f),
                    Outward = Vector3.right,
                    DoorPoint = new Vector3(-1f, 1.2f, 0.1f),
                    BalloonOutside = false,
                    BalloonAnchor = new Vector3(chairBounds.center.x, chairBounds.max.y, chairBounds.center.z),
                    ClockPosition = new Vector3(1.97f, 2.05f, 1.25f),
                    ClockFront = Vector3.left,
                    Floor = 0f,
                };
            }

            public static Layout Of(Transform room)
            {
                var glass = Measure(Require(room, "Window/Glass"));
                var sill = Measure(Require(room, "Window/Frame_Bottom"));
                var nightstand = Measure(Require(room, "Furniture/Nightstand"));
                var insideX = sill.min.x + 0.03f;
                var floor = room.position.y;
                return new Layout
                {
                    Nightstand = nightstand,
                    WindowPoint = glass.center,
                    Outward = Vector3.right, // Liam's rooms: the window is in the +x wall (see BuildPatientViewEffects)
                    DoorPoint = room.position + Vector3.up * 1.2f,
                    BalloonOutside = true,
                    BalloonAnchor = new Vector3(glass.max.x + 0.7f, glass.min.y - 0.3f, glass.center.z + 0.35f),
                    ClockPosition = new Vector3(insideX - 0.02f, floor + 2.05f, glass.min.z - 0.75f),
                    ClockFront = Vector3.left,
                    Floor = floor,
                };
            }

            /// <summary>
            /// Liam's U-ward room (Room_1A): origin in the middle of the corridor wall, forward into the room, the bed's
            /// head against the outer wall with the window (Window_1A_1, under the hospital) in it.
            /// </summary>
            public static Layout OfWard(Transform room)
            {
                var id = room.name.Replace("Room_", "");
                var nightstand = Measure(Require(room, "Furniture/Nightstand"));
                var window = room.root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == $"Window_{id}_1");
                var forward = new Vector3(room.forward.x, 0f, room.forward.z).normalized;
                var right = Vector3.Cross(Vector3.up, forward);
                var floor = room.position.y;
                var glass = window != null ? Measure(window.gameObject) : new Bounds(room.position + forward * 5.8f + Vector3.up * 1.5f, Vector3.one);
                // The clock on the side wall that is free (raycast sideways from the middle of the room).
                Physics.SyncTransforms();
                var middle = room.position + forward * 3.2f + Vector3.up * 2.05f;
                var clock = middle + right * 2f;
                var front = -right;
                foreach (var side in new[] { right, -right })
                    if (Physics.Raycast(middle, side, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        clock = hit.point - side * 0.02f;
                        front = -side;
                        break;
                    }
                return new Layout
                {
                    Nightstand = nightstand,
                    WindowPoint = glass.center,
                    Outward = forward,
                    DoorPoint = room.position + Vector3.up * 1.2f,
                    BalloonOutside = true,
                    BalloonAnchor = glass.center + forward * 0.7f - Vector3.up * 0.3f + right * 0.35f,
                    ClockPosition = clock,
                    ClockFront = front,
                    Floor = floor,
                };
            }

            private static GameObject Require(Transform room, string path)
            {
                var child = room.Find(path);
                if (child == null)
                    throw new System.InvalidOperationException($"[PatientPov] {room.name} has no '{path}'.");
                return child.gameObject;
            }
        }

        private static Bounds Measure(GameObject go)
        {
            return ProCommVR.Editor.Scenario01SceneBuilder.TightBounds(go);
        }

        // ---- Phase 2: face and gaze -----------------------------------------------------------------------

        private static EmotionFaceDriver AddFace(EmotionHolder holder, PovController pov, PatientWorldController world, Transform patient,
            Layout layout, Transform root)
        {
            var face = patient.GetComponentInChildren<FaceController>(true);
            if (face == null)
            {
                Debug.LogWarning($"[PatientPov] {patient.name} has no FaceController (no face to match): skipped the face.");
                return null;
            }
            var gaze = new GameObject("Gaze Targets").transform;
            gaze.SetParent(root, false);
            var window = Point(gaze, "Window", layout.WindowPoint);
            var door = Point(gaze, "Door", layout.DoorPoint);
            var gazePoint = Point(gaze, "Gaze Point", layout.WindowPoint);

            var driver = face.gameObject.GetComponent<EmotionFaceDriver>() ?? face.gameObject.AddComponent<EmotionFaceDriver>();
            Set(driver, "_holder", holder);
            Set(driver, "_face", face);
            Set(driver, "_mapping", AssetDatabase.LoadAssetAtPath<EmotionFaceMapping>(FaceMappingPath));
            Set(driver, "_world", world);
            Set(driver, "_pov", pov);
            Set(driver, "_look", face.GetComponent<HeadLookAt>());
            Set(driver, "_window", window);
            Set(driver, "_door", door);
            Set(driver, "_gazePoint", gazePoint);
            return driver;
        }

        private static void AddDebugLabel(GameObject root, EmotionFaceDriver driver, PatientWorldController world, PovController pov)
        {
            if (driver == null)
                return;
            var label = root.AddComponent<PatientPovDebugLabel>();
            Set(label, "_face", driver);
            Set(label, "_world", world);
            Set(label, "_pov", pov);
        }

        // ---- Phase 3: the nurse as the patient sees her ----------------------------------------------------

        private static void AddPerceivedNurse(EmotionHolder holder, PovController pov, PatientWorldController world, Transform patient)
        {
            var avatar = Object.FindAnyObjectByType<PlayerAvatar>(FindObjectsInactive.Include);
            if (avatar == null)
            {
                Debug.LogWarning("[PatientPov] No PlayerAvatar in the scene: skipped the perceived nurse.");
                return;
            }
            // Scene side only: an added component on the avatar instance, Jalil's prefab stays as it is.
            var nurse = avatar.GetComponent<PerceivedNurse>() ?? avatar.gameObject.AddComponent<PerceivedNurse>();
            Set(nurse, "_holder", holder);
            Set(nurse, "_pov", pov);
            Set(nurse, "_profile", AssetDatabase.LoadAssetAtPath<PerceivedNurseProfile>(NurseProfilePath));
            Set(nurse, "_world", world);
            Set(nurse, "_patient", patient);
            Set(nurse, "_face", avatar.GetComponentInChildren<FaceController>(true));
        }

        // ---- Phase 4: the granddaughter's things -------------------------------------------------------------

        private static void AddStoryProps(EmotionHolder holder, PovController pov, PatientWorldController world, Layout layout, Transform root)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ScenarioOneStoryProps>(StoryPropsPath);
            var props = new GameObject("Story Props").transform;
            props.SetParent(root, false);

            // On the nightstand, facing the bed (the patient's head is at the bed side of the nightstand).
            var top = new Vector3(layout.Nightstand.center.x, layout.Nightstand.max.y, layout.Nightstand.center.z);
            var toBed = FlatToward(top, PatientHeadGuess(layout));
            var side = Vector3.Cross(Vector3.up, toBed);
            var cardPosition = top + side * settings.CardOffset.x + toBed * settings.CardOffset.y;
            var photoPosition = top + side * settings.PhotoOffset.x + toBed * settings.PhotoOffset.y;

            var card = BuildCard(props, settings, cardPosition, Quaternion.LookRotation(-toBed));
            // v2: the card starts closed and opens when the student gives him a reason (BirthdayCard)
            var opener = card.gameObject.AddComponent<BirthdayCard>();
            Set(opener, "_npc", holder != null ? holder.GetComponent<ProCommVR.Hospital.AiTalk.AiNpc>() : null);
            Set(opener, "_front", card.Find("Panel Left"));
            Set(opener, "_inside", card.Find("Panel Right"));
            var frame = BuildPhoto(props, photoPosition, Quaternion.LookRotation(toBed), out var photoColor);
            var balloon = BuildBalloon(props, layout);

            var story = props.gameObject.AddComponent<StoryProps>();
            Set(story, "_holder", holder);
            Set(story, "_pov", pov);
            Set(story, "_world", world);
            Set(story, "_settings", settings);
            Set(story, "_card", card);
            Set(story, "_photoFrame", frame);
            Set(story, "_photoColor", photoColor);
            Set(story, "_balloon", balloon);
            var so = new SerializedObject(story);
            so.FindProperty("_driftDirection").vector3Value = layout.BalloonOutside ? (layout.Outward + Vector3.forward * 0.5f).normalized : Vector3.back;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 PatientHeadGuess(Layout layout)
        {
            // The bed side of the nightstand: toward the window for the standalone room is wrong, so use the bed's
            // side: in both rooms the patient sits next to the nightstand, on the side away from the wall.
            var holder = Object.FindAnyObjectByType<PovAnchor>();
            return holder != null ? holder.transform.position + Vector3.up * 1.2f : layout.Nightstand.center + Vector3.left;
        }

        /// <summary>
        /// A birthday card standing on the nightstand: "HAPPY BIRTHDAY" on the front, Emma's invitation inside. v2: built
        /// closed (the inside panel folded behind the front, a narrow tent); <see cref="BirthdayCard"/> opens it.
        /// </summary>
        private static Transform BuildCard(Transform parent, ScenarioOneStoryProps settings, Vector3 position, Quaternion facing)
        {
            var card = new GameObject("Birthday Card").transform;
            card.SetParent(parent, false);
            card.SetPositionAndRotation(position, facing);
            var paper = MakeMaterial("Card_Paper", new Color(0.98f, 0.95f, 0.86f));
            const float width = 0.15f, height = 0.2f, front = -8f, inside = 188f;
            // The panels' visible sides face the patient: the card's -z (facing is away from the bed).
            Panel(card, "Left", paper, new Vector3(-width * 0.5f, height * 0.5f, 0f), Quaternion.Euler(0f, front, 0f), width, height,
                settings.CardFront, new Color(0.9f, 0.25f, 0.45f), true);
            Panel(card, "Right", paper, new Vector3(width * 0.5f, height * 0.5f, 0f), Quaternion.Euler(0f, inside, 0f), width, height,
                settings.CardInside, new Color(0.2f, 0.35f, 0.75f), false);
            return card;
        }

        private static void Panel(Transform card, string name, Material paper, Vector3 offset, Quaternion turn, float width, float height,
            string text, Color color, bool bold)
        {
            var hinge = new GameObject($"Panel {name}").transform;
            hinge.SetParent(card, false);
            hinge.localRotation = turn;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Paper";
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(hinge, false);
            cube.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            cube.transform.localScale = new Vector3(width, height, 0.002f);
            NoShadows(cube.GetComponent<Renderer>(), paper);

            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshPro>();
            label.transform.SetParent(hinge, false);
            label.rectTransform.sizeDelta = new Vector2(width * 0.86f, height * 0.86f);
            label.transform.localPosition = new Vector3(offset.x, offset.y, -0.0015f);
            label.text = text;
            label.color = color;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Italic;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.05f;
            label.fontSizeMax = bold ? 1.2f : 0.6f;
            label.textWrappingMode = TextWrappingModes.Normal;
            NoShadows(label.GetComponent<Renderer>(), null);
        }

        /// <summary>A photo frame (pivot at its bottom front edge) with a grey photo and the color photo on top.</summary>
        private static Transform BuildPhoto(Transform parent, Vector3 position, Quaternion facing, out Renderer colorPhoto)
        {
            var pivot = new GameObject("Photo Frame").transform;
            pivot.SetParent(parent, false);
            pivot.SetPositionAndRotation(position, facing * Quaternion.Euler(-12f, 0f, 0f)); // leans back on its stand
            const float width = 0.13f, height = 0.17f, depth = 0.015f;
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            Object.DestroyImmediate(frame.GetComponent<Collider>());
            frame.transform.SetParent(pivot, false);
            frame.transform.localPosition = new Vector3(0f, height * 0.5f, -depth * 0.5f);
            frame.transform.localScale = new Vector3(width, height, depth);
            NoShadows(frame.GetComponent<Renderer>(), MakeMaterial("Photo_Frame", new Color(0.35f, 0.22f, 0.14f)));

            var grey = Quad(pivot, "Photo (grey)", new Vector3(0f, height * 0.5f, 0.0008f), width * 0.82f, height * 0.82f);
            NoShadows(grey, PhotoMaterial("Photo_Grey", "Photo_Grey.png", false));
            var color = Quad(pivot, "Photo (color)", new Vector3(0f, height * 0.5f, 0.0016f), width * 0.82f, height * 0.82f);
            NoShadows(color, PhotoMaterial("Photo_Color", "Photo_Color.png", true));
            colorPhoto = color;
            return pivot;
        }

        private static Renderer Quad(Transform parent, string name, Vector3 position, float width, float height)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = position;
            // A Unity quad faces -z: turn it so the picture faces the frame's +z (the patient).
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localScale = new Vector3(width, height, 1f);
            return quad.GetComponent<Renderer>();
        }

        /// <summary>A balloon on a string: outside the window (clear glass) or tied to the visitor chair (closed blind).</summary>
        private static Transform BuildBalloon(Transform parent, Layout layout)
        {
            const float stringLength = 0.75f;
            var balloon = new GameObject("Balloon").transform;
            balloon.SetParent(parent, false);
            balloon.position = layout.BalloonAnchor + Vector3.up * (stringLength + 0.16f);

            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(balloon, false);
            body.transform.localScale = new Vector3(0.28f, 0.33f, 0.28f);
            body.GetComponent<MeshFilter>().sharedMesh = BalloonMesh();
            NoShadows(body.GetComponent<Renderer>(), MakeMaterial("Balloon", new Color(0.92f, 0.16f, 0.22f), 0.75f));

            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "String";
            Object.DestroyImmediate(line.GetComponent<Collider>());
            line.transform.SetParent(balloon, false);
            line.transform.localPosition = new Vector3(0f, -0.16f - stringLength * 0.5f, 0f);
            line.transform.localScale = new Vector3(0.004f, stringLength, 0.004f);
            NoShadows(line.GetComponent<Renderer>(), MakeMaterial("Balloon_String", new Color(0.95f, 0.95f, 0.95f)));
            return balloon;
        }

        // ---- Phase 4: the clock -----------------------------------------------------------------------------

        private static AudioSource AddClock(EmotionHolder holder, PovController pov, PatientWorldController world, Layout layout, Transform root)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ClockPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[PatientPov] {ClockPrefabPath} missing: no story clock.");
                return null;
            }
            var clock = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            clock.name = "Party Clock";
            clock.transform.SetParent(root, false);
            // The prop's front is -z (PropSetup): turn it to face the room.
            var yaw = Vector3.SignedAngle(Vector3.back, layout.ClockFront, Vector3.up);
            clock.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation;
            clock.transform.position = layout.ClockPosition;
            foreach (var r in clock.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;

            var source = clock.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.6f;
            source.minDistance = 1f;
            source.maxDistance = 6f;
            var story = clock.AddComponent<StoryClock>();
            Set(story, "_clock", clock.GetComponent<WallClock>());
            Set(story, "_holder", holder);
            Set(story, "_pov", pov);
            Set(story, "_world", world);
            Set(story, "_settings", AssetDatabase.LoadAssetAtPath<ScenarioOneStoryProps>(StoryPropsPath));
            Set(story, "_tickSource", source);
            Set(story, "_tick", AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/clock_tick.wav"));
            return source;
        }

        // ---- Phase 5: heartbeat, muffled world, inner voice -------------------------------------------------

        private static void AddSound(EmotionHolder holder, PovController pov, PatientWorldController world, Transform patient, Transform root,
            AudioSource clockSource)
        {
            var sound = new GameObject("Patient POV Sound");
            sound.transform.SetParent(root, false);

            var heartSource = TwoD(sound, "Heartbeat");
            var heart = sound.AddComponent<HeartbeatFeedback>();
            Set(heart, "_holder", holder);
            Set(heart, "_pov", pov);
            Set(heart, "_world", world);
            Set(heart, "_source", heartSource);
            Set(heart, "_beat", AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/heartbeat.wav"));

            var voiceSource = TwoD(sound, "Inner Voice");
            var voice = sound.AddComponent<InnerVoice>();
            Set(voice, "_holder", holder);
            Set(voice, "_pov", pov);
            Set(voice, "_lines", AssetDatabase.LoadAssetAtPath<InnerVoiceLines>(InnerVoicePath));
            Set(voice, "_source", voiceSource);
            var hud = Object.FindObjectsByType<EmotionHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            var subtitle = BuildSubtitle(root, hud, out var text);
            Set(voice, "_subtitle", subtitle);
            Set(voice, "_subtitleText", text);
            Set(voice, "_hud", hud != null ? hud.transform : null);

            var muffle = sound.AddComponent<MuffledWorld>();
            Set(muffle, "_holder", holder);
            Set(muffle, "_pov", pov);
            Set(muffle, "_world", world);
            Set(muffle, "_patient", patient);
            var so = new SerializedObject(muffle);
            var exclude = so.FindProperty("_exclude");
            var list = new List<AudioSource> { heartSource, voiceSource };
            if (clockSource != null)
                list.Add(clockSource);
            exclude.arraySize = list.Count;
            for (var i = 0; i < list.Count; i++)
                exclude.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioSource TwoD(GameObject owner, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(owner.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;
            return source;
        }

        /// <summary>The italic subtitle of the inner voice: a small world-space panel that follows the HUD, drawn on top.</summary>
        private static CanvasGroup BuildSubtitle(Transform root, EmotionHud hud, out TMP_Text text)
        {
            var go = new GameObject("Inner Voice Subtitle", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(700f, 90f);
            rect.localScale = Vector3.one * 0.001f;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 11;
            go.AddComponent<CanvasScaler>();
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var background = go.AddComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            background.color = new Color(9f / 255f, 18f / 255f, 26f / 255f, 0.6f);
            background.material = AssetDatabase.LoadAssetAtPath<Material>(HudImageMaterialPath);
            background.raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var labelRect = label.rectTransform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(20f, 6f);
            labelRect.offsetMax = new Vector2(-20f, -6f);
            label.text = "";
            label.fontSize = 30f;
            label.fontStyle = FontStyles.Italic;
            label.color = new Color(0.95f, 0.96f, 0.97f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            var overlay = AssetDatabase.LoadAssetAtPath<Material>(HudTextMaterialPath);
            if (overlay != null)
                label.fontSharedMaterial = overlay;
            label.raycastTarget = false;
            text = label;
            go.SetActive(false);
            return group;
        }

        // ---- Phase 1: the patient --------------------------------------------------------------------------

        /// <summary>
        /// Patient X of Scenario 1: a prefab variant of Liam's Patient_Male_Old (old man in a hospital gown, face with
        /// FaceController, HeadLookAt, TearDrops) with a bandage around the lower right leg (the infected leg).
        /// Created once; delete it to rebuild.
        /// </summary>
        public static GameObject EnsurePatientPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PatientPrefabPath);
            if (existing != null)
                return existing;
            EnsureFolder(ArtFolder);
            EnsureFolder(Path.GetDirectoryName(PatientPrefabPath).Replace('\\', '/'));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PatientSourcePath);
            if (source == null)
                throw new System.InvalidOperationException($"[PatientPov] {PatientSourcePath} missing.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = "PatientX_Scenario1";
                var animator = instance.GetComponentInChildren<Animator>();
                var shin = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightLowerLeg) : null;
                var foot = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
                if (shin != null && foot != null)
                {
                    var bandage = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    bandage.name = "Bandage (infected leg)";
                    Object.DestroyImmediate(bandage.GetComponent<Collider>());
                    var from = shin.position;
                    var to = foot.position;
                    var length = Vector3.Distance(from, to);
                    bandage.transform.position = Vector3.Lerp(from, to, 0.55f);
                    bandage.transform.rotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
                    // Unity's cylinder is 2 m tall and 1 m wide.
                    bandage.transform.localScale = new Vector3(0.115f, length * 0.2f, 0.115f);
                    bandage.transform.SetParent(shin, true);
                    NoShadows(bandage.GetComponent<Renderer>(), MakeMaterial("Bandage", new Color(0.95f, 0.93f, 0.88f)));
                }
                else
                    Debug.LogWarning("[PatientPov] Patient_Male_Old has no Humanoid leg bones: no bandage.");
                return PrefabUtility.SaveAsPrefabAsset(instance, PatientPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// How high above its root a character sits in an Animator state (e.g. "Sitting"): the hips of the state's first
        /// frame minus the thickness of the thighs. Measured on a temporary copy, so the prefab and the scene are untouched.
        /// Returns the fallback when the state or the clip is not found.
        /// </summary>
        public static float SeatHeight(GameObject prefab, string state, float fallback)
        {
            var copy = (GameObject)Object.Instantiate(prefab);
            try
            {
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = copy.GetComponentInChildren<Animator>();
                var controller = animator != null ? animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null;
                var clip = controller != null
                    ? controller.layers[0].stateMachine.states.Where(s => s.state.name == state).Select(s => s.state.motion as AnimationClip).FirstOrDefault()
                    : null;
                if (clip == null || !animator.isHuman)
                {
                    Debug.LogWarning($"[PatientPov] No '{state}' clip on {prefab.name}: seat height {fallback} m assumed.");
                    return fallback;
                }
                clip.SampleAnimation(copy, 0f);
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                // The seat is under the thighs: a little below the hips (about the thigh's thickness).
                var seat = Mathf.Min(hips.position.y, knee != null ? knee.position.y : hips.position.y) - 0.07f;
                Debug.Log($"[PatientPov] {prefab.name} in '{clip.name}': hips at {hips.position.y:F2} m, seat at {seat:F2} m above the root.");
                return seat;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        // ---- Assets -----------------------------------------------------------------------------------------

        [MenuItem("ProCommVR/Patient POV/Create Data Assets")]
        public static void EnsureAssets()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(ArtFolder);
            EnsureFolder(AudioFolder);
            CreateIfMissing<EmotionFaceMapping>(FaceMappingPath);
            CreateIfMissing<PerceivedNurseProfile>(NurseProfilePath);
            CreateIfMissing<ScenarioOneStoryProps>(StoryPropsPath);
            var lines = CreateIfMissing<InnerVoiceLines>(InnerVoicePath);
            var changed = false;
            foreach (var line in lines.Lines)
            {
                if (line.Clip != null)
                    continue;
                line.Clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/thought_{line.Emotion.ToString().ToLowerInvariant()}.wav");
                changed |= line.Clip != null;
            }
            if (changed)
            {
                EditorUtility.SetDirty(lines);
                AssetDatabase.SaveAssets();
            }
            ConfigureAudio();
            EnsurePhotoTextures();
        }

        private static T CreateIfMissing<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Short mono clips for the Quest: decompressed on load (tiny), Vorbis in the build.</summary>
        private static void ConfigureAudio()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer) || importer.forceToMono)
                    continue;
                importer.forceToMono = true;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// The placeholder photo (no real photo): a simple drawn child with a party hat, in color and in grey.
        /// </summary>
        private static void EnsurePhotoTextures()
        {
            var colorPath = $"{ArtFolder}/Photo_Color.png";
            var greyPath = $"{ArtFolder}/Photo_Grey.png";
            if (File.Exists(colorPath) && File.Exists(greyPath))
                return;
            const int size = 128;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = PhotoPixel(x / (float)size, y / (float)size);
            WritePng(colorPath, pixels, size);
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                var g = (byte)Mathf.Clamp(p.r * 0.299f + p.g * 0.587f + p.b * 0.114f, 0f, 255f);
                pixels[i] = new Color32(g, g, g, 255);
            }
            WritePng(greyPath, pixels, size);
        }

        private static Color32 PhotoPixel(float u, float v)
        {
            Color c = new Color(0.55f, 0.8f, 0.97f); // sky
            if (v < 0.22f) c = new Color(0.45f, 0.75f, 0.35f); // grass
            // Confetti.
            var cell = Mathf.FloorToInt(u * 9f) * 31 + Mathf.FloorToInt(v * 9f) * 17;
            if (v > 0.6f && (cell % 7 == 0) && Frac(u * 9f) > 0.4f && Frac(u * 9f) < 0.6f && Frac(v * 9f) > 0.4f && Frac(v * 9f) < 0.6f)
                c = cell % 2 == 0 ? new Color(1f, 0.8f, 0.2f) : new Color(0.9f, 0.3f, 0.6f);
            // Body (a party dress).
            if (Mathf.Abs(u - 0.5f) < 0.17f - (v - 0.1f) * 0.15f && v > 0.08f && v < 0.42f) c = new Color(0.95f, 0.45f, 0.6f);
            // Face.
            var dx = u - 0.5f;
            var dy = v - 0.55f;
            if (dx * dx + dy * dy < 0.15f * 0.15f) c = new Color(1f, 0.85f, 0.72f);
            // Hair on the sides.
            if (dx * dx + dy * dy < 0.17f * 0.17f && dx * dx + dy * dy >= 0.15f * 0.15f && dy > -0.05f) c = new Color(0.55f, 0.33f, 0.15f);
            // Eyes and smile.
            if (Sq(u - 0.45f) + Sq(v - 0.58f) < 0.018f * 0.018f || Sq(u - 0.55f) + Sq(v - 0.58f) < 0.018f * 0.018f) c = new Color(0.15f, 0.12f, 0.1f);
            var r = Mathf.Sqrt(Sq(dx) + Sq(v - 0.55f));
            if (v < 0.52f && Mathf.Abs(r - 0.07f) < 0.01f && Mathf.Abs(dx) < 0.06f) c = new Color(0.75f, 0.2f, 0.25f);
            // Party hat (a striped triangle).
            var hatBase = 0.69f;
            if (v > hatBase && v < 0.9f && Mathf.Abs(dx) < (0.9f - v) * 0.5f)
                c = Mathf.FloorToInt((v - hatBase) * 40f) % 2 == 0 ? new Color(0.3f, 0.45f, 0.95f) : new Color(1f, 0.85f, 0.25f);
            if (Sq(dx) + Sq(v - 0.91f) < 0.02f * 0.02f) c = new Color(1f, 0.3f, 0.3f);
            return c;
        }

        private static float Sq(float x) => x * x;
        private static float Frac(float x) => x - Mathf.Floor(x);

        private static void WritePng(string path, Color32[] pixels, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static Material MakeMaterial(string name, Color color, float smoothness = 0.2f)
        {
            var path = $"{ArtFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find(SimpleLit)) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Simple Lit with the photo texture (lit, so in the dark Fear room it only shows in the flashlight).</summary>
        private static Material PhotoMaterial(string name, string texture, bool transparent)
        {
            var path = $"{ArtFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find(SimpleLit)) { name = name };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtFolder}/{texture}"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.3f);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A low-poly balloon (10 x 7 segments, 120 triangles, flat shaded like Liam's characters).</summary>
        private static Mesh BalloonMesh()
        {
            var path = $"{ArtFolder}/Balloon.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            const int around = 10, rings = 7;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 Vertex(int ring, int seg)
            {
                var v = ring / (float)rings;
                var angle = v * Mathf.PI;
                var radius = Mathf.Sin(angle) * (1f - 0.25f * v); // narrower toward the knot
                var a = seg * Mathf.PI * 2f / around;
                return new Vector3(Mathf.Cos(a) * radius * 0.5f, Mathf.Cos(angle) * 0.5f, Mathf.Sin(a) * radius * 0.5f);
            }
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                triangles.Add(vertices.Count); vertices.Add(a);
                triangles.Add(vertices.Count); vertices.Add(b);
                triangles.Add(vertices.Count); vertices.Add(c);
            }
            for (var r = 0; r < rings; r++)
            for (var s = 0; s < around; s++)
            {
                var a = Vertex(r, s);
                var b = Vertex(r, s + 1);
                var c = Vertex(r + 1, s);
                var d = Vertex(r + 1, s + 1);
                if (r > 0) Tri(a, b, c);
                if (r < rings - 1) Tri(b, d, c);
            }
            mesh = new Mesh { name = "Balloon" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ---- Helpers ----------------------------------------------------------------------------------------

        private static Transform Point(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = position;
            return point;
        }

        private static Vector3 FlatToward(Vector3 from, Vector3 to)
        {
            var d = to - from;
            d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? Vector3.forward : d.normalized;
        }

        private static void NoShadows(Renderer renderer, Material material)
        {
            if (renderer == null)
                return;
            if (material != null)
                renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null)
                throw new System.InvalidOperationException($"[PatientPov] {target.GetType().Name} has no field '{property}'.");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
