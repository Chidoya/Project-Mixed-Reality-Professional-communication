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
