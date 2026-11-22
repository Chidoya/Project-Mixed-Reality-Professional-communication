using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Controle-screenshots: deuren en sanitair, een kamer per arm, een badkamer, en de personages. De camera's van de
    /// personages staan in CharacterShots, zodat de Play mode-test (Tests/UWardSceneTests) dezelfde beelden maakt.
    /// </summary>
    public static partial class UWardBuilder
    {
        private static void TakeFixScreenshots(Transform root)
        {
            void PlanShot(string file, Vector2 eye, Vector2 target, float targetHeight = 1.1f) =>
                Shot($"u_afdeling_{file}.png", PlanPoint(eye, EyeHeight), PlanPoint(target, targetHeight));
            // De twee toiletdeuren recht van voren (midden tussen beide deuren), en de herendeur even open zoals Jalils OpenDoor
            var men = CoreDoors.First(d => d.Name == "Door_Toilet_Men");
            var women = CoreDoors.First(d => d.Name == "Door_Toilet_Women");
            var between = (men.Center + women.Center) / 2f;
            PlanShot("toiletdeuren", between + new Vector2(-3.2f, 0f), between, 1.2f);
            var menDoor = root.Find($"Core/{men.Name}");
            var (position, rotation) = (menDoor.position, menDoor.rotation);
            menDoor.RotateAround(menDoor.position - menDoor.right * (men.Segment.Length / 2f), Vector3.up,
                Vector3.SignedAngle(menDoor.right, menDoor.forward, Vector3.up));
            PlanShot("fix_toilet_door_open", men.Center + new Vector2(-2.6f, 1.4f), men.Center + new Vector2(1.2f, -0.6f), 1.0f);
            menDoor.SetPositionAndRotation(position, rotation);
            PlanShot("fix_briefing_door_hall", new Vector2(23.9f, 12.4f), new Vector2(25f, 14.5f));
            PlanShot("fix_briefing_door_corridor", new Vector2(29.6f, 21.4f), new Vector2(31f, 19.6f));

            foreach (var id in new[] { "1B", "2B", "3B" })
            {
                var spec = PatientRooms.First(r => r.Id == id);
                var room = root.Find($"Floor{spec.Zone}/Room_{id}");
                var door = room.InverseTransformPoint(PlanPoint(spec.DoorCenter));
                Shot($"u_afdeling_fix_room_{id}.png", room.TransformPoint(new Vector3(door.x, EyeHeight, 0.4f)),
                    room.TransformPoint(new Vector3(0f, 0.6f, RoomInteriorDepth - 1.0f)));
            }

            var bath = PatientRooms.First(r => r.Id == "1B");
            Shot("u_afdeling_fix_bathroom_1B.png", PlanPoint(bath.At(1.4f, BathroomDepth + 1.1f), EyeHeight),
                PlanPoint(bath.At(0.8f, 0.4f), 0.5f));

            foreach (var (name, eye, target) in CharacterShots(root))
                Shot($"u_afdeling_chars_edit_{name}.png", eye, target);
        }

        /// <summary>Camera's voor de personages: liggend (1A), zittend (3A), gedeelde kamer (1C) en het personeel in de hal.</summary>
        internal static IEnumerable<(string Name, Vector3 Eye, Vector3 Target)> CharacterShots(Transform root)
        {
            var a1 = root.Find("Floor1/Room_1A");
            yield return ("1A", a1.TransformPoint(new Vector3(-1.6f, 1.8f, 2.2f)), a1.TransformPoint(new Vector3(0f, 0.6f, RoomInteriorDepth - 1.1f)));
            var spec3 = PatientRooms.First(r => r.Id == "3A");
            var a3 = root.Find("Floor3/Room_3A");
            var door3 = a3.InverseTransformPoint(PlanPoint(spec3.DoorCenter));
            yield return ("3A", a3.TransformPoint(new Vector3(door3.x, EyeHeight, 0.4f)), a3.TransformPoint(new Vector3(0.6f, 0.7f, RoomInteriorDepth - 1.4f)));
            var c1 = root.Find("Floor1/Room_1C");
            yield return ("shared_1C", c1.TransformPoint(new Vector3(0f, 2.2f, 0.4f)), c1.TransformPoint(new Vector3(0f, 0.5f, RoomInteriorDepth - 1.2f)));
            yield return ("staff", PlanPoint(new Vector2(21f, 14.3f), EyeHeight), PlanPoint(new Vector2(21f, 4f), 1.0f));
        }
    }
}
