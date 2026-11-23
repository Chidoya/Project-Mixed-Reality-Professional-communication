using ProCommVR.Atmosphere;
using ProCommVR.Hospital;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Two things for Liam's U-ward in Prototype1:
    /// <list type="bullet">
    /// <item>The map: the floor plan (Art/Map/UWardFloorPlan.png, the same plan as UWardLayout: 42 x 28 m, about 50 pixels
    /// per meter) on the corridor wall next to the entrance of the lobby, with the points of interest in orange.</item>
    /// <item>The ambience: the murmur of people talking (Audio/Ambience/HospitalBabble_A/B/C.wav, made by
    /// Tools/Ambience/make_babble.py: other people per clip) from a few places in the lobby and the corridors, quiet,
    /// each place with another clip.</item>
    /// </list>
    /// </summary>
    internal static class WardMapAndAmbienceSetup
    {
        private const string MapTexturePath = "Assets/_Project/Art/Map/UWardFloorPlan.png";
        private const string MapMaterialPath = "Assets/_Project/Materials/Map/UWardMap.mat";
        private static readonly string[] BabblePaths =
        {
            "Assets/_Project/Audio/Ambience/HospitalBabble_A.wav",
            "Assets/_Project/Audio/Ambience/HospitalBabble_B.wav",
            "Assets/_Project/Audio/Ambience/HospitalBabble_C.wav",
        };

        // The building outline in the image (pixels, measured): plan x 0..42 and y 0..28.
        private static readonly Vector2 ImageSize = new Vector2(2296f, 1598f);
        private static readonly Vector2 PlanOrigin = new Vector2(97.5f, 97.5f);
        private static readonly Vector2 PixelsPerMeter = new Vector2(2101f / UWardLayout.Width, 1403f / UWardLayout.Depth);

        internal static readonly Color Orange = new Color(1f, 0.5f, 0.05f);

        /// <summary>A point on the map: plan position, label, and where the label goes (right or left of the dot).</summary>
        private readonly struct Point
        {
            public readonly Vector2 Plan;
            public readonly string Label;
            public readonly bool LabelLeft;
            public Point(float x, float y, string label, bool labelLeft = false) { Plan = new Vector2(x, y); Label = label; LabelLeft = labelLeft; }
        }

        /// <summary>The map on the wall of the medication room, on the corridor side, just left of the lobby entrance.</summary>
        internal static Transform AddMap(Transform parent, Vector2 youAreHere, params (Vector2 plan, string label, bool left)[] extra)
        {
            const float width = 1.5f;
            var height = width * ImageSize.y / ImageSize.x;
            // The wall y = 19.6 (x 8.4..17); its corridor face is at y + half the wall.
            var wallFace = 19.6f + UWardLayout.InnerWall / 2f + 0.012f;
            var center = UWardLayout.ToUnity(15.0f, wallFace, 1.68f);

            var map = new GameObject("Ward Map (lobby entrance)").transform;
            map.SetParent(parent, false);
            map.position = center;
            map.rotation = Quaternion.identity; // the quad faces -Z, into the corridor (plan +y)

            var frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frame.name = "Frame";
            Object.DestroyImmediate(frame.GetComponent<Collider>());
            frame.transform.SetParent(map, false);
            frame.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            frame.transform.localScale = new Vector3(width + 0.06f, height + 0.06f, 1f);
            frame.GetComponent<MeshRenderer>().sharedMaterial = FrameMaterial();
            Quiet(frame.GetComponent<MeshRenderer>());

            var plan = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plan.name = "Floor Plan";
            Object.DestroyImmediate(plan.GetComponent<Collider>());
            plan.transform.SetParent(map, false);
            plan.transform.localScale = new Vector3(width, height, 1f);
            plan.GetComponent<MeshRenderer>().sharedMaterial = MapMaterial();
            Quiet(plan.GetComponent<MeshRenderer>());

            var points = new System.Collections.Generic.List<Point>
            {
                new Point(youAreHere.x, youAreHere.y, "You are here"),
                new Point(3.0f, 9.7f, "Room 1A: patient"),
                new Point(21.0f, 16.8f, "Reception", true),
                new Point(29.3f, 16.2f, "Doctor (briefing)"),
            };
            foreach (var e in extra)
                points.Add(new Point(e.plan.x, e.plan.y, e.label, e.left));
            var dot = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            foreach (var point in points)
            {
                var local = MapLocal(point.Plan, width, height);
                var marker = new GameObject($"Point {point.Label}").transform;
                marker.SetParent(map, false);
                marker.localPosition = new Vector3(local.x, local.y, -0.004f);
                var sprite = marker.gameObject.AddComponent<SpriteRenderer>();
                sprite.sprite = dot;
                sprite.color = Orange;
                var size = point.Label == "You are here" ? 0.075f : 0.055f;
                marker.localScale = Vector3.one * (size / dot.bounds.size.x);

                var text = new GameObject("Label").AddComponent<TextMeshPro>();
                text.transform.SetParent(map, false);
                text.rectTransform.sizeDelta = new Vector2(0.5f, 0.08f);
                text.rectTransform.pivot = new Vector2(point.LabelLeft ? 1f : 0f, 0.5f);
                text.transform.localPosition = new Vector3(local.x + (point.LabelLeft ? -0.045f : 0.045f), local.y, -0.005f);
                text.text = $"<b>{point.Label}</b>";
                text.fontSize = 0.32f;
                text.color = new Color(0.85f, 0.38f, 0.0f);
                text.alignment = point.LabelLeft ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }

            var title = new GameObject("Title").AddComponent<TextMeshPro>();
            title.transform.SetParent(map, false);
            title.rectTransform.sizeDelta = new Vector2(width, 0.1f);
            title.transform.localPosition = new Vector3(0f, height / 2f + 0.09f, -0.003f);
            title.text = "<b>WARD MAP</b>   <color=#ff800d>●</color> points of interest";
            title.fontSize = 0.55f;
            title.color = new Color(0.15f, 0.15f, 0.18f);
            title.alignment = TextAlignmentOptions.Center;
            return map;
        }

        /// <summary>Plan point (meters) to a point on the map quad (local, meters from its center).</summary>
        private static Vector2 MapLocal(Vector2 plan, float width, float height)
        {
            var pixel = PlanOrigin + Vector2.Scale(plan, PixelsPerMeter);
            return new Vector2((pixel.x / ImageSize.x - 0.5f) * width, (0.5f - pixel.y / ImageSize.y) * height);
        }

        /// <summary>The murmur of people: a few looping 3D sources in the lobby and the corridors.</summary>
        internal static void AddAmbience(Transform parent)
        {
            var clips = new System.Collections.Generic.List<AudioClip>();
            foreach (var path in BabblePaths)
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[ProCommVR] Ambience missing: {path} (run Tools/Ambience/make_babble.py).");
                    continue;
                }
                var settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.CompressedInMemory || settings.compressionFormat != AudioCompressionFormat.Vorbis)
                {
                    settings.loadType = AudioClipLoadType.CompressedInMemory;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = 0.5f;
                    importer.defaultSampleSettings = settings;
                    importer.forceToMono = true;
                    importer.SaveAndReimport();
                }
                clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            if (clips.Count == 0)
                return;

            var root = new GameObject("Ambience (people talking)").transform;
            root.SetParent(parent, false);
            (Vector2 plan, float volume, string name)[] places =
            {
                (new Vector2(21.0f, 8.0f), 0.2f, "Lobby (lifts)"),
                (new Vector2(21.0f, 14.0f), 0.16f, "Lobby (reception)"),
                (new Vector2(12.0f, 20.8f), 0.1f, "Corridor bottom (left)"),
                (new Vector2(30.0f, 20.8f), 0.1f, "Corridor bottom (right)"),
                (new Vector2(34.8f, 10.0f), 0.08f, "Corridor right"),
            };
            var index = 0;
            foreach (var (plan, volume, name) in places)
            {
                var clip = clips[index++ % clips.Count];
                var go = new GameObject($"Murmur {name}");
                go.transform.SetParent(root, false);
                go.transform.position = UWardLayout.ToUnity(plan.x, plan.y, 1.5f);
                var source = go.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false; // AmbienceLoop starts it at a random point
                source.volume = volume;
                source.spatialBlend = 0.85f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2f;
                source.maxDistance = 16f;
                source.dopplerLevel = 0f;
                go.AddComponent<AmbienceLoop>();
            }
        }

        private static Material MapMaterial()
        {
            var texturePath = MapTexturePath;
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            if (importer != null && (importer.maxTextureSize != 2048 || !importer.mipmapEnabled))
            {
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MapMaterialPath);
            if (material == null)
            {
                XRTestSceneBuilder.EnsureFolder("Assets/_Project/Materials/Map");
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "UWardMap" };
                AssetDatabase.CreateAsset(material, MapMaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material FrameMaterial()
        {
            const string path = "Assets/_Project/Materials/Map/MapFrame.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                XRTestSceneBuilder.EnsureFolder("Assets/_Project/Materials/Map");
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MapFrame" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.2f, 0.22f, 0.26f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Quiet(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
