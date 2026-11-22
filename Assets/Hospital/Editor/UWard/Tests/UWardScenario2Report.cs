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
    /// Overdracht van scenario 2: opent de OPGESLAGEN liam_u_afdeling.unity en schrijft een markdown-tabel met per pad of het
    /// bestaat, actief is, en de positie in plan- en Unity-coördinaten. Daarna de namen uit namen.md die Jalil al opzocht.
    /// Command line: Unity -batchmode -executeMethod ProCommVR.Editor.Tests.UWardScenario2Report.RunBatch -reportOut pad.md
    /// </summary>
    public static class UWardScenario2Report
    {
        private const string ScenePath = "Assets/Scenes/liam_u_afdeling.unity";

        private static readonly string[] Scenario2Paths =
        {
            "Core/FamilyRoom", "Core/FamilyRoom/Light", "Core/Door_FamilyRoom_Hall", "Core/Door_FamilyRoom_Corridor",
            "Floor3/Room_3C/Props/Gown", "Floor3/Room_3C/Furniture/Bed/Anchor_BedEdge",
            "Characters/Patient_3C/Body_Pyjama", "Characters/Patient_3C/Body_Gown",
            "Scenarios/Scenario2_AngryDaughter/Actors/Daughter", "Scenarios/Scenario2_AngryDaughter/Actors/Blocker_Corridor",
            "Scenarios/Scenario2_AngryDaughter/Actors/Countdown_Mount", "Scenarios/Scenario2_AngryDaughter/Actors/Gurney",
            "Scenarios/Scenario2_AngryDaughter/Actors/Gurney/Porter",
            "Floor3/AlarmLights/AlarmLight_3C", "Floor3/AlarmLights/AlarmLight_Corridor", "Floor3/AlarmLights/AlarmLight_FamilyRoom",
            "Core/FamilyRoom/AlarmLight",
        };

        // Namen die Jalils ShiftHospitalSceneBuilder al opzoekt (namen.md, "Bestaat met dezelfde naam" en "Nieuw")
        private static IEnumerable<string> OldNames()
        {
            foreach (var room in PatientRooms)
            {
                var floor = $"Floor{room.Zone}";
                yield return $"{floor}/Room_{room.Id}";
                yield return $"{floor}/Room_{room.Id}/Anchor_Laying";
                yield return $"{floor}/Room_{room.Id}/Anchor_Sitting";
                yield return $"{floor}/Room_{room.Id}/Furniture/Bed";
                yield return $"{floor}/Room_{room.Id}/Furniture/Chair";
                yield return $"{floor}/Room_{room.Id}/Furniture/Nightstand";
                yield return $"{floor}/Room_{room.Id}/Light";
                yield return $"{floor}/Door_{room.Id}";
                yield return $"{floor}/Card_{room.Id}";
                yield return $"{floor}/GuideLights/Guide_{room.Id}";
            }
            foreach (var floor in new[] { "Floor1", "Floor3" })
                foreach (var light in new[] { "Light_3", "Light_9", "Light_15" })
                    yield return $"{floor}/{light}";
            yield return "Floor2/Light_3";
            yield return "Floor2/NurseStation";
            foreach (var path in new[]
                     {
                         "Lobby/Furniture/ReceptionDesk", "Lobby/StaffStation", "Lobby/StaffStation/Anchor_BadgeScan",
                         "Lobby/StaffStation/Anchor_TabletDock", "Lobby/StaffStation/Anchor_Clipboard", "Lobby/StaffStation/Anchor_EndScreen",
                         "Lobby/Wall_North", "Lobby/Light_1", "Lobby/Light_2", "Lobby/XROriginStart", "Lobby/Lift/Lift_1", "Lobby/Lift/Lift_2",
                         "Lobby/Lift/LiftButton_Up", "Lobby/Lift/LiftButton_Down", "Characters/Patient_1A", "Characters/Patient_2A",
                         "Characters/Patient_3A", "Core/BriefingRoom/Screen", "Core/BriefingRoom/Whiteboard", "Core/Door_Stairwell",
                         "Core/Door_Briefing_Hall", "Core/Door_Briefing_Corridor", "Core/Door_Toilet_Men", "Core/Door_Toilet_Women",
                     })
                yield return path;
        }

        public static void RunBatch()
        {
            var args = System.Environment.GetCommandLineArgs();
            var index = System.Array.IndexOf(args, "-reportOut");
            var path = index >= 0 && index + 1 < args.Length ? args[index + 1] : "Logs/u_ward_scenario2_report.md";
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                File.WriteAllLines(path, Lines());
                Debug.Log($"[UWardScenario2Report] {path}");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[UWardScenario2Report] " + e);
                EditorApplication.Exit(1);
            }
        }

        public static List<string> Lines()
        {
            var root = GameObject.Find("Hospital").transform;
            var lines = new List<string>
            {
                "| Pad (onder Hospital/) | Gevonden | Actief | Plan (x, y, hoogte) | Unity (X, Y, Z) |",
                "| --- | --- | --- | --- | --- |",
            };
            var anchors = root.Find("Scenarios/Scenario2_AngryDaughter/Anchors");
            var paths = Scenario2Paths.ToList();
            if (anchors != null)
                paths.InsertRange(8, anchors.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != anchors && t.childCount == 0)
                    .Select(t => "Scenarios/Scenario2_AngryDaughter/Anchors/" + Relative(anchors, t)));
            foreach (var p in paths)
                lines.Add(Row(root, p));

            var missing = OldNames().Where(n => root.Find(n) == null).ToList();
            lines.Add("");
            lines.Add($"Oude namen uit namen.md: {OldNames().Count()} gecontroleerd, {missing.Count} ontbreken" +
                      (missing.Count > 0 ? ": " + string.Join(", ", missing) : "."));
            return lines;
        }

        private static string Row(Transform root, string path)
        {
            var t = root.Find(path);
            if (t == null)
                return $"| `{path}` | NEE | | | |";
            var p = t.position;
            return $"| `{path}` | ja | {(t.gameObject.activeInHierarchy ? "ja" : "nee")} | " +
                   $"{p.x + Width / 2f:0.00} / {Depth / 2f - p.z:0.00} / {p.y:0.00} | {p.x:0.00} / {p.y:0.00} / {p.z:0.00} |";
        }

        private static string Relative(Transform parent, Transform t) => t.parent == parent ? t.name : $"{Relative(parent, t.parent)}/{t.name}";
    }
}
