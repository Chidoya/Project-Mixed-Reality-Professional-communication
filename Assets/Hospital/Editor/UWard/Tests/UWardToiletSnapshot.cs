using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor.Tests
{
    /// <summary>
    /// Momentopname van alles rond de toiletten in de OPGESLAGEN liam_u_afdeling.unity: de twee toiletdeuren en hun bordjes,
    /// de toiletruimtes met hun inrichting, de muren van de toiletten, de muurstukken van de hal en de kern naast de
    /// toiletten (y 0..8) en de fauteuils tussen de toiletdeuren. Per object positie, rotatie en bounds, gesorteerd, zodat
    /// twee momentopnames (voor en na een build) regel per regel te vergelijken zijn.
    /// Command line: Unity -batchmode -executeMethod ProCommVR.Editor.Tests.UWardToiletSnapshot.RunBatch -snapshotOut pad.txt
    /// </summary>
    public static class UWardToiletSnapshot
    {
        private const string ScenePath = "Assets/Scenes/liam_u_afdeling.unity";
        private const float ToiletZoneY = 8.0f;   // onderkant van het vrouwentoilet in het plan

        public static void RunBatch()
        {
            var args = System.Environment.GetCommandLineArgs();
            var index = System.Array.IndexOf(args, "-snapshotOut");
            var path = index >= 0 && index + 1 < args.Length ? args[index + 1] : "Logs/u_ward_toilets.txt";
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                File.WriteAllLines(path, Lines());
                Debug.Log($"[UWardToiletSnapshot] {path}");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[UWardToiletSnapshot] " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Eén regel per object: pad | positie | rotatie | bounds-midden | bounds-maat (mm nauwkeurig).</summary>
        public static List<string> Lines()
        {
            var root = GameObject.Find("Hospital").transform;
            var items = new List<Transform>();
            void Add(string path)
            {
                var t = root.Find(path);
                if (t != null)
                    items.Add(t);
            }

            foreach (var label in new[] { "Men", "Women" })
            {
                Add($"Core/Door_Toilet_{label}");
                Add($"Core/Door_Toilet_{label}/Sign_Toilet_{label}");
                Add($"Core/Toilet_{label}");
                var room = root.Find($"Core/Toilet_{label}");
                if (room != null)
                    items.AddRange(room.GetComponentsInChildren<Transform>().Where(t => t != room && t.parent.name == "Furniture" || t.parent == room));
            }
            Add("Core/Wall_ToiletMen_South");
            Add("Core/Wall_ToiletWomen_South");

            // De muren van de hal (x 25) en de kern (x 33,6) naast de toiletten worden in stukken van max. 6 m gebouwd; een
            // nieuwe deur verderop verandert die opdeling, niet de muur. Daarom het muurvlak: per hoogte de dichte stukken (plan-y).
            var pieces = new List<string>();
            foreach (var (wall, x) in new[] { ("Core/Wall_Hall_East", 25.0f), ("Core/Wall_East", 33.6f) })
            {
                var w = root.Find(wall);
                if (w == null)
                    continue;
                var boxes = w.GetComponentsInChildren<Renderer>().Select(r => r.bounds).ToList();
                foreach (var height in new[] { 0.5f, 1.5f, 2.5f })
                {
                    var runs = new List<string>();
                    float? start = null;
                    const float step = 0.01f;
                    for (var y = 0f; y <= ToiletZoneY + step / 2f; y += step)
                    {
                        var p = ToUnity(x, y, height);
                        var closed = boxes.Any(b => b.Contains(p));
                        if (closed && start == null)
                            start = y;
                        if ((!closed || y + step > ToiletZoneY + step / 2f) && start != null)
                        {
                            runs.Add($"{start:0.00}..{(closed ? y : y - step):0.00}");
                            start = null;
                        }
                    }
                    pieces.Add($"{wall} (muurvlak) | hoogte {height:0.0} m | dicht op y {string.Join(", ", runs)}");
                }
            }

            // De fauteuils tegen de oostmuur van de hal, tussen en naast de toiletdeuren
            var furniture = root.Find("Lobby/Furniture");
            if (furniture != null)
                items.AddRange(furniture.Cast<Transform>().Where(t => t.name.StartsWith("WaitingChair") && t.position.x > 2f));

            return items.Distinct().Select(Line).Concat(pieces).OrderBy(l => l).ToList();
        }

        private static string Line(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length == 0 ? new Bounds(t.position, Vector3.zero) : renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            return $"{Path(t)} | pos {V(t.position)} | rot {V(t.rotation.eulerAngles)} | bounds {V(bounds.center)} | maat {V(bounds.size)}";
        }

        private static string Path(Transform t) => t.parent == null || t.name == "Hospital" ? t.name : $"{Path(t.parent)}/{t.name}";

        private static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
    }
}
