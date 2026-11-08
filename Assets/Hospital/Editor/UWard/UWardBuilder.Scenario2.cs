using System.IO;
using System.Linq;
using ProCommVR.Hospital;
using UnityEditor;
using UnityEngine;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Scenario 2 (de boze dochter), beeld 1: kamer 3C waar de vader klaargemaakt wordt voor een operatie. De vader
    /// (Patient_Male_Old) zit op de rand van bed 1 met zijn gezicht de kamer in, het operatieschort ligt gevouwen op het
    /// voeteneinde, een klembord op het nachtkastje, bed 2 is leeg en het privacygordijn is half dicht. De stoel van bed 1
    /// staat 40 cm dichter bij het raam dan in de andere kamers, zodat de voeten van de vader vrij staan.
    /// Hospital/Characters/Patient_3C heeft twee lichamen op dezelfde plek: Body_Pyjama (actief) en Body_Gown (inactief, met het
    /// bovenstuk in Patient_Gown_Top.mat). Jalil (of een latere trigger) wisselt ze om. Geen spellogica en geen XR-componenten.
    /// Assets/Character blijft ongewijzigd: het schortmateriaal is een nieuw materiaal, enkel op de instantie gezet.
    /// </summary>
    public static partial class UWardBuilder
    {
        private const string PreOpRoomId = "3C";
        private const string PreOpPatientModel = "Patient_Male_Old";
        private const string GownTopMaterialPath = MaterialFolder + "/Patient_Gown_Top.mat";
        private const string GownPatternPath = MaterialFolder + "/Textures/Patient_Gown_Pattern.png";
        private static readonly Color GownColor = new Color(0.62f, 0.80f, 0.90f);
        private static readonly Color GownDotColor = new Color(0.40f, 0.60f, 0.76f);

        // Op de rand van het bed: de heupen 18 cm van de rand het matras op, iets voorbij het midden naar het voeteneinde
        // (zo blijft de oorsprong van Patient_3C binnen 4,2 m van de gangmuur, waar Jalils HideMisplacedPatients hem laat staan)
        private const float BedEdgeInset = 0.18f;
        private const float BedEdgeFromFoot = 0.55f;
        private const float BedEdgeHipsAboveMattress = 0.08f;   // het matras geeft een beetje mee
        private const float CurtainOpenFraction = 0.5f;
        private const float PreOpChairShift = 0.4f;   // de stoel van bed 1 schuift naar het raam, weg van de voeten van de vader

        // --- Kamer 3C (voor MarkStatic) -------------------------------------------------------

        /// <summary>Anchor_BedEdge onder Bed, het schort, het klembord en het half dichte gordijn in Room_3C.</summary>
        private static void BuildPreOpRoom(Transform root)
        {
            var spec = PatientRooms.First(r => r.Id == PreOpRoomId);
            var room = root.Find($"Floor{spec.Zone}/Room_{PreOpRoomId}");
            var bed = room.Find("Furniture/Bed");
            var bedBounds = HospitalAssetPrep.WorldBounds(bed.gameObject);
            var bedLocal = room.InverseTransformPoint(bedBounds.center);
            var halfWidth = Mathf.Abs(room.InverseTransformVector(bedBounds.extents).x);
            var length = Mathf.Abs(room.InverseTransformVector(bedBounds.size).z);
            var foot = bedLocal.z - length / 2f;
            var mattress = room.Find("Anchor_Laying").localPosition.y;

            // De rand aan de kant van het midden van de kamer (weg van de buitenmuur aan de zijkant)
            var toCenter = bedLocal.x < 0f ? 1f : -1f;
            var edge = room.TransformPoint(new Vector3(bedLocal.x + toCenter * (halfWidth - BedEdgeInset), mattress, foot + BedEdgeFromFoot));
            var anchor = new GameObject("Anchor_BedEdge").transform;
            anchor.SetParent(bed, false);
            anchor.SetPositionAndRotation(edge, Quaternion.LookRotation(room.right * toCenter));

            // De stoel naast bed 1 (en zijn anker) schuift naar de buitenmuur, zodat de voeten van de vader vrij staan
            foreach (var item in new[] { room.Find("Furniture/Chair"), room.Find("Anchor_Sitting") })
                if (item != null)
                    item.position += room.forward * PreOpChairShift;
            Physics.SyncTransforms();

            var props = Child(room, "Props");
            // Schort gevouwen op het voeteneinde, aan de kant weg van de vader, in de lengte van het bed
            var gownSpot = room.TransformPoint(new Vector3(bedLocal.x - toCenter * 0.15f, mattress, foot + 0.22f));
            var gown = PlaceProp("Surgical_Gown", props, gownSpot, room.forward);
            if (gown != null)
            {
                gown.name = "Gown";
                SitOnSurface(gown, bed.gameObject);
                AddBoundsCollider(gown);
            }

            // Klembord op het nachtkastje, leesbaar vanaf de kamer
            var stand = room.Find("Furniture/Nightstand");
            var standBounds = HospitalAssetPrep.WorldBounds(stand.gameObject);
            var top = TopAt(stand.gameObject, standBounds.center.x, standBounds.center.z);
            var clipboard = PlaceProp("Clipboard", props,
                new Vector3(standBounds.center.x, float.IsNaN(top) ? standBounds.max.y : top, standBounds.center.z), -room.forward);
            if (clipboard != null)
                clipboard.name = "Clipboard";

            // Privacygordijn half dicht: de helft tegen de buitenmuur blijft, de rest is opzij geschoven (de rail blijft heel)
            var curtain = room.Find("Furniture/Curtain");
            if (curtain != null)
            {
                var s = curtain.localScale;
                var back = curtain.localPosition.z + s.z / 2f;
                curtain.localScale = new Vector3(s.x, s.y, s.z * CurtainOpenFraction);
                curtain.localPosition = new Vector3(curtain.localPosition.x, curtain.localPosition.y, back - s.z * CurtainOpenFraction / 2f);
            }
        }

        /// <summary>Legt een prop met zijn onderkant op het hoogste punt van surface eronder (raycast op de mesh colliders).</summary>
        private static void SitOnSurface(GameObject prop, GameObject surface)
        {
            var b = HospitalAssetPrep.WorldBounds(prop);
            var top = TopAt(surface, b.center.x, b.center.z);
            if (!float.IsNaN(top))
                prop.transform.position += Vector3.up * (top - b.min.y);
        }

        private static void AddBoundsCollider(GameObject go)
        {
            var b = HospitalAssetPrep.WorldBounds(go);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            var size = go.transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }

        // --- De vader (in PlaceCharacters, tijdens AnimationMode) -----------------------------------

        /// <summary>
        /// Hospital/Characters/Patient_3C met Body_Pyjama en Body_Gown, allebei zittend op Anchor_BedEdge (pose Sitting). Gemeten
        /// op een kopie zoals de andere patiënten: heupen BedEdgeHipsAboveMattress boven het matras, daarna zo laag dat de voeten
        /// op de vloer staan zonder dat de heupen meer dan 5 cm in het matras zakken.
        /// </summary>
        private static void PlacePreOpPatient(Transform root, Transform characters)
        {
            var spec = PatientRooms.First(r => r.Id == PreOpRoomId);
            var room = root.Find($"Floor{spec.Zone}/Room_{PreOpRoomId}");
            var anchor = room.Find("Furniture/Bed/Anchor_BedEdge");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterFolder}/{PreOpPatientModel}/{PreOpPatientModel}.prefab");
            if (anchor == null || prefab == null)
            {
                Debug.LogWarning($"{LogPrefix}Patient_{PreOpRoomId}: anker of {PreOpPatientModel} ontbreekt, overgeslagen.");
                return;
            }

            const string state = "Sitting";
            var probe = Probe(prefab, $"Patient_{PreOpRoomId}");
            if (!SamplePose(probe, state, 0f))
                return;
            var animator = probe.GetComponent<Animator>();
            probe.transform.SetPositionAndRotation(new Vector3(anchor.position.x, room.position.y, anchor.position.z), anchor.rotation);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            probe.transform.position += new Vector3(anchor.position.x - hips.x, anchor.position.y + BedEdgeHipsAboveMattress - hips.y,
                anchor.position.z - hips.z);
            var feetAbove = MinVertexY(probe) - room.position.y;
            var sink = Mathf.Clamp(feetAbove, -0.05f, BedEdgeHipsAboveMattress + 0.05f);
            probe.transform.position -= Vector3.up * sink;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            Debug.Log($"{LogPrefix}Patient_{PreOpRoomId} zit op de bedrand: heupen {hips.y - anchor.position.y:+0.000;-0.000} m t.o.v. het matras, " +
                      $"laagste punt {MinVertexY(probe) - room.position.y:0.000} m boven de vloer.");

            var patient = new GameObject($"Patient_{PreOpRoomId}").transform;
            patient.SetParent(characters, false);
            patient.SetPositionAndRotation(probe.transform.position, probe.transform.rotation);
            var gownTop = GownTopMaterial(prefab);
            foreach (var body in new[] { "Body_Pyjama", "Body_Gown" })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, patient);
                go.name = body;
                go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.AddComponent<CharacterPose>().Configure(state, 0f);
                if (body == "Body_Gown" && gownTop != null)
                    foreach (var r in go.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.sharedMaterials;
                        for (var i = 0; i < mats.Length; i++)
                            if (mats[i] != null && mats[i].name == $"{PreOpPatientModel}_Top")
                                mats[i] = gownTop;
                        r.sharedMaterials = mats;
                    }
            }
        }

        /// <summary>
        /// Patient_Gown_Top.mat: een kopie van het bovenstuk van het personage, lichtblauw met kleine stipjes (zoals een
        /// ziekenhuisschort). Het originele materiaal in Assets/Character blijft ongewijzigd. Bestaat het al, dan wordt het enkel geladen.
        /// </summary>
        private static Material GownTopMaterial(GameObject prefab)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GownTopMaterialPath);
            if (existing != null)
                return existing;
            var original = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .FirstOrDefault(m => m != null && m.name == $"{PreOpPatientModel}_Top");
            if (original == null)
            {
                Debug.LogWarning($"{LogPrefix}{PreOpPatientModel}_Top niet gevonden: Body_Gown krijgt geen schort.");
                return null;
            }
            var mat = new Material(original) { name = "Patient_Gown_Top" };
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", GownPattern());
            mat.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
            EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(mat, GownTopMaterialPath);
            return mat;
        }

        /// <summary>Kleine herhaalbare textuur: lichtblauw met een verspringend stippenpatroon (32 x 32 px).</summary>
        private static Texture2D GownPattern()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GownPatternPath);
            if (existing != null)
                return existing;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    bool Dot(int cx, int cy) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= 4;
                    tex.SetPixel(x, y, Dot(8, 8) || Dot(24, 24) ? GownDotColor : GownColor);
                }
            tex.Apply();
            EnsureFolder(Path.GetDirectoryName(GownPatternPath).Replace('\\', '/'));
            File.WriteAllBytes(GownPatternPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(GownPatternPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GownPatternPath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GownPatternPath);
        }

        // --- Plekken in de gang en de gesprekskamer (beelden 2 tot 5) ------------------------------

        private const string Scenario2Path = "Scenarios/Scenario2_AngryDaughter";
        private const string DaughterModel = "Visitor_Daughter";
        private const string DaughterState = "Angry";
        private const float CountdownRodThickness = 0.015f;
        private static readonly Color AnchorColor = new Color(1f, 0.55f, 0.1f);
        private static readonly Color WaypointColor = new Color(0.2f, 0.75f, 1f);

        /// <summary>
        /// Hospital/Scenarios/Scenario2_AngryDaughter met Anchors (lege objecten met ScenarioAnchor, waarden uit
        /// UWardLayout.Scenario2Anchors) en Actors: Countdown_Mount (klokpaneel zonder script) en Blocker_Corridor (onzichtbare
        /// BoxCollider over de breedte van de gang op de plek van de dochter; Jalil zet hem uit als ze wegloopt). De dochter zelf
        /// komt in PlaceDaughter, tijdens het plaatsen van de personages.
        /// </summary>
        private static void BuildScenario2(Transform root)
        {
            var scenario = Child(Child(root, "Scenarios"), "Scenario2_AngryDaughter");
            var anchors = Child(scenario, "Anchors");
            var actors = Child(scenario, "Actors");

            foreach (var spec in Scenario2Anchors)
            {
                var parent = anchors;
                var name = spec.Name;
                var slash = name.LastIndexOf('/');
                if (slash >= 0)
                {
                    parent = Child(anchors, name.Substring(0, slash));
                    name = name.Substring(slash + 1);
                }
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(PlanPoint(spec.Plan, spec.Height),
                    Quaternion.LookRotation(PlanDirection(Scenario2Facing(spec))));
                go.AddComponent<ScenarioAnchor>().Configure(name.StartsWith("WP_") ? WaypointColor : AnchorColor, 0.1f);
            }

            // Klokpaneel: donker en leeg, met de voorkant naar de forward van Anchor_Countdown, aan twee staafjes tot het plafond
            var countdown = Scenario2Anchor("Anchor_Countdown");
            var mount = Child(actors, "Countdown_Mount");
            mount.SetPositionAndRotation(PlanPoint(countdown.Plan, countdown.Height),
                Quaternion.LookRotation(PlanDirection(Scenario2Facing(countdown))));
            var size = CountdownPanelSize;
            Box("Panel", mount, Vector3.zero, new Vector3(size.x, size.y, CountdownPanelDepth),
                Mat("ScreenBlack", new Color(0.08f, 0.09f, 0.1f), 0f, 0.8f));
            var rodBottom = size.y / 2f;
            var rodLength = CeilingHeight - countdown.Height - rodBottom;
            foreach (var (side, x) in new[] { ("L", -size.x * 0.35f), ("R", size.x * 0.35f) })
                Box($"Rod_{side}", mount, new Vector3(x, rodBottom + rodLength / 2f, 0f),
                    new Vector3(CountdownRodThickness, rodLength, CountdownRodThickness), MetalMat);
            foreach (var c in mount.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            // Onzichtbare versperring over de hele breedte van de rechtergang, op de lijn van de dochter
            var daughter = Scenario2Anchor("Anchor_Daughter_Corridor");
            var x0 = CorridorRight.X0 + InnerWall / 2f;
            var x1 = CorridorRight.X1 - InnerWall / 2f;
            var blocker = new GameObject("Blocker_Corridor");
            blocker.transform.SetParent(actors, false);
            blocker.transform.position = PlanPoint(new Vector2((x0 + x1) / 2f, daughter.Y), BlockerHeight / 2f);
            blocker.AddComponent<BoxCollider>().size = new Vector3(x1 - x0, BlockerHeight, BlockerDepth);
        }

        /// <summary>
        /// Actors/Daughter: Visitor_Daughter op Anchor_Daughter_Corridor, pose Angry, met de voeten op de vloer (gemeten op een
        /// kopie), een CapsuleCollider en EditorPosePreview. Niet static.
        /// </summary>
        private static void PlaceDaughter(Transform root)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterFolder}/{DaughterModel}/{DaughterModel}.prefab");
            var actors = root.Find($"{Scenario2Path}/Actors");
            var anchor = root.Find($"{Scenario2Path}/Anchors/Anchor_Daughter_Corridor");
            if (prefab == null || actors == null || anchor == null)
            {
                Debug.LogWarning($"{LogPrefix}Dochter: {DaughterModel}, Actors of Anchor_Daughter_Corridor ontbreekt, overgeslagen.");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, actors);
            go.name = "Daughter";
            go.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            go.AddComponent<CharacterPose>().Configure(DaughterState, 0f);
            var probe = Probe(prefab, go.name);
            probe.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
            if (SamplePose(probe, DaughterState, 0f))
                go.transform.position += Vector3.up * (anchor.position.y - MinVertexY(probe));

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = DaughterColliderRadius;
            capsule.height = DaughterColliderHeight;
            capsule.center = Vector3.up * (DaughterColliderHeight / 2f);
        }

        /// <summary>De gang vanuit Anchor_Player_Corridor, de gang vanuit het zuiden, en de gesprekskamer vanuit Anchor_Player_Room.</summary>
        private static void TakeScenario2Screenshots(Transform root)
        {
            var anchors = root.Find($"{Scenario2Path}/Anchors");
            if (anchors == null)
                return;
            Vector3 Eye(string name) => anchors.Find(name).position + Vector3.up * EyeHeight;
            var player = anchors.Find("Anchor_Player_Corridor");
            Shot("u_afdeling_s2_corridor_player.png", Eye("Anchor_Player_Corridor"),
                Eye("Anchor_Player_Corridor") + player.forward * 4f + Vector3.down * 0.35f);
            Shot("u_afdeling_s2_corridor_from_south.png", PlanPoint(new Vector2(34.8f, 12.5f), EyeHeight),
                PlanPoint(Scenario2Anchor("Anchor_Daughter_Corridor").Plan, 1.2f));
            var room = anchors.Find("Anchor_Player_Room");
            Shot("u_afdeling_s2_familyroom_player.png", Eye("Anchor_Player_Room"),
                Eye("Anchor_Player_Room") + room.forward * 3f + Vector3.down * 0.4f);
        }

        // --- De brancard en de portier (beeld 6) -------------------------------------------------------

        private const string PorterModel = "Porter";
        private const string PorterState = "Idle";
        // Maten uit blender/scripts/06_props.py (build_gurney): matras tot 0,83 m, duwbeugels op 0,99 m van het midden en 0,90 m hoog
        private const float GurneyMattressTop = 0.83f;
        private const float GurneyPushBarOffset = 0.99f;
        private const float GurneyPushBarHeight = 0.90f;
        private const float PorterHandsBehindBar = 0.04f;
        private const float PorterChestClearance = 0.1f;   // borst minstens zo ver achter de duwbeugel

        /// <summary>
        /// Actors/Gurney: root in het midden van de brancard op de vloer, forward = rijrichting (het hoofdeinde met het kussen
        /// voorop, zoals PassingCart), op Gurney_Start. Model = Gurney.prefab met de lange kant langs forward, een laken over het
        /// matras. De portier (Porter, pose Idle) staat achter de brancard met zijn handen bij de duwbeugel, als kind van Gurney.
        /// Niets is static; Gurney staat INACTIEF (Jalils PathMover zet hem aan en laat hem rijden). Draait na MarkStatic.
        /// </summary>
        private static void PlaceGurney(Transform root)
        {
            var actors = root.Find($"{Scenario2Path}/Actors");
            var start = root.Find($"{Scenario2Path}/Anchors/Path_Gurney/Gurney_Start");
            var gurneyPrefab = PropSetup.LoadPrefab("Gurney");
            var porterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterFolder}/{PorterModel}/{PorterModel}.prefab");
            if (actors == null || start == null || gurneyPrefab == null || porterPrefab == null)
            {
                Debug.LogWarning($"{LogPrefix}Brancard: Actors, Gurney_Start, Gurney.prefab of {PorterModel} ontbreekt, overgeslagen.");
                return;
            }

            var gurney = new GameObject("Gurney").transform;
            gurney.SetParent(actors, false);
            gurney.SetPositionAndRotation(start.position, start.rotation);

            // Blender +X (hoofdeinde) is lokaal (1, 0, 0) van de prop-root: een kwartslag om y legt die as op de forward van Gurney
            var model = (GameObject)PrefabUtility.InstantiatePrefab(gurneyPrefab, gurney);
            model.name = "Model";
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * gurneyPrefab.transform.localRotation;
            AlignToFloor(model, gurney.position);
            var size = gurney.InverseTransformVector(HospitalAssetPrep.WorldBounds(model).size);
            if (Mathf.Abs(size.z) < Mathf.Abs(size.x))
                Debug.LogWarning($"{LogPrefix}Brancard ligt dwars: {size}.");

            // Laken over het matras, van het voeteneinde tot net voor het kussen
            var sheet = Box("Sheet", gurney, new Vector3(0f, GurneyMattressTop + 0.006f, -0.2f), new Vector3(0.62f, 0.012f, 1.5f),
                Mat("Sheet", new Color(0.96f, 0.97f, 0.98f)));
            Object.DestroyImmediate(sheet.GetComponent<Collider>());

            // Portier: handen bij de duwbeugel aan het voeteneinde, gemeten op een kopie in de Idle-pose
            var porter = (GameObject)PrefabUtility.InstantiatePrefab(porterPrefab, gurney);
            porter.name = PorterModel;
            porter.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            porter.AddComponent<CharacterPose>().Configure(PorterState, 0f);
            var probe = Probe(porterPrefab, porter.name);
            probe.transform.SetPositionAndRotation(gurney.position, gurney.rotation);
            if (SamplePose(probe, PorterState, 0f))
            {
                var animator = probe.GetComponent<Animator>();
                Vector3 Local(HumanBodyBones b) => gurney.InverseTransformPoint(animator.GetBoneTransform(b).position);
                var hands = (Local(HumanBodyBones.LeftHand) + Local(HumanBodyBones.RightHand)) / 2f;
                var chest = Local(HumanBodyBones.Chest);
                var barZ = -GurneyPushBarOffset;
                var z = barZ - PorterHandsBehindBar - hands.z;
                z = Mathf.Min(z, barZ - PorterChestClearance - chest.z);
                porter.transform.localPosition = new Vector3(0f, -MinVertexY(probe) + gurney.position.y, z);
                Debug.Log($"{LogPrefix}Portier achter de brancard: handen {barZ - (hands.z + z):0.00} m achter de duwbeugel, " +
                          $"op {hands.y - MinVertexY(probe) + gurney.position.y:0.00} m (beugel {GurneyPushBarHeight:0.00} m).");
            }
            var capsule = porter.AddComponent<CapsuleCollider>();
            capsule.radius = StaffColliderRadius;
            capsule.height = StaffColliderHeight;
            capsule.center = Vector3.up * (StaffColliderHeight / 2f);
        }

        /// <summary>De brancard (even actief) op Gurney_Start, in de bocht en op Gurney_End; daarna terug op de start, inactief.</summary>
        private static void TakeGurneyScreenshots(Transform root)
        {
            var gurney = root.Find($"{Scenario2Path}/Actors/Gurney");
            var anchors = root.Find($"{Scenario2Path}/Anchors");
            if (gurney == null || anchors == null)
                return;
            var (position, rotation) = (gurney.position, gurney.rotation);
            gurney.gameObject.SetActive(true);
            foreach (var pose in gurney.GetComponentsInChildren<EditorPosePreview>())
                pose.Sample();
            void At(string waypoint, string file, Vector2 eye, float lookHeight = 0.9f)
            {
                var w = anchors.Find(waypoint);
                gurney.SetPositionAndRotation(w.position, w.rotation);
                Shot(file, PlanPoint(eye, EyeHeight), w.position + Vector3.up * lookHeight);
            }
            At("Path_Gurney/Gurney_Start", "u_afdeling_s2_gurney_start.png", new Vector2(26.2f, 21.6f));
            At("Path_Gurney/Gurney_Corner_2", "u_afdeling_s2_gurney_corner.png", new Vector2(31.0f, 21.6f));
            At("Path_Gurney/Gurney_End", "u_afdeling_s2_gurney_end.png", new Vector2(34.8f, 2.4f));
            gurney.SetPositionAndRotation(position, rotation);
            gurney.gameObject.SetActive(false);
        }

        // --- Screenshots -------------------------------------------------------------------------

        /// <summary>Camera's in kamer 3C: vanuit de deur naar bed 1, en dichtbij de vader. Ook gebruikt door de Play mode-test.</summary>
        internal static (Vector3 Eye, Vector3 Target)[] PreOpShots(Transform root)
        {
            var spec = PatientRooms.First(r => r.Id == PreOpRoomId);
            var room = root.Find($"Floor{spec.Zone}/Room_{PreOpRoomId}");
            var door = room.InverseTransformPoint(PlanPoint(spec.DoorCenter));
            var bed = room.InverseTransformPoint(HospitalAssetPrep.WorldBounds(room.Find("Furniture/Bed").gameObject).center);
            return new[]
            {
                (room.TransformPoint(new Vector3(door.x, EyeHeight, 0.4f)), room.TransformPoint(new Vector3(bed.x, 0.7f, bed.z))),
                (room.TransformPoint(new Vector3(bed.x + (bed.x < 0f ? 1.9f : -1.9f), 1.5f, bed.z - 1.6f)),
                    room.TransformPoint(new Vector3(bed.x, 0.75f, bed.z - 0.4f))),
            };
        }

        /// <summary>u_afdeling_3C_edit_{pyjama,gown}_{door,close}.png: de vader in pyjama en daarna in schort.</summary>
        private static void TakePreOpScreenshots(Transform root)
        {
            var patient = root.Find($"Characters/Patient_{PreOpRoomId}");
            if (patient == null)
                return;
            var pyjama = patient.Find("Body_Pyjama").gameObject;
            var gown = patient.Find("Body_Gown").gameObject;
            var shots = PreOpShots(root);
            foreach (var (outfit, gownOn) in new[] { ("pyjama", false), ("gown", true) })
            {
                pyjama.SetActive(!gownOn);
                gown.SetActive(gownOn);
                foreach (var pose in patient.GetComponentsInChildren<EditorPosePreview>())
                    pose.Sample();
                Shot($"u_afdeling_3C_edit_{outfit}_door.png", shots[0].Eye, shots[0].Target);
                Shot($"u_afdeling_3C_edit_{outfit}_close.png", shots[1].Eye, shots[1].Target);
            }
            pyjama.SetActive(true);
            gown.SetActive(false);
        }
    }
}
