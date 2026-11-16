using System.Collections;
using System.Linq;
using ProCommVR.Player;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// The nurse avatar of the player in Scenario01_PatientRoom (it comes with the XR rig variant): exactly one, detached
    /// from the rig, hidden by default, cheap; once visible it follows the simulated head (position under the head,
    /// yaw only, upright, feet on the floor) with its own head folded away; with Follow Head off it stays where it is
    /// and shows its head when the camera is far away. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunPlayerAvatar
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        [MenuItem("ProCommVR/Tests/Player Avatar (Scenario 01)")]
        public static void RunPlayerAvatar()
        {
            Launch("avatar", "Assets/_Project/Scenes/Scenario01_PatientRoom.unity");
        }

        private static IEnumerator PlayerAvatarTest()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var avatars = Object.FindObjectsByType<PlayerAvatar>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Check(avatars.Length == 1 && origin != null, $"exactly one player avatar ({avatars.Length}) and an XR Origin");
            if (Errors.Count > 0)
                yield break;
            var avatar = avatars[0];
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            Check(avatar.transform.parent == null && avatar.Origin == origin, "the avatar left the rig and follows its XR Origin");
            Check(!avatar.IsVisible && renderers.Length > 0 && renderers.All(r => !r.enabled), "hidden by default (nurse view)");
            Check(avatar.Head != null, "the avatar has a Head bone");

            var triangles = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(r => r.sharedMesh.triangles.Length / 3) +
                            avatar.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
            Check(triangles <= 3000, $"at most 3k triangles ({triangles})");
            Check(avatar.GetComponentsInChildren<Light>(true).Length == 0, "no lights on the avatar");
            Check(renderers.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off), "no shadows");

            var events = 0;
            var lastEvent = false;
            void OnVisibility(bool visible)
            {
                events++;
                lastEvent = visible;
            }

            avatar.VisibilityChanged += OnVisibility;
            AddSimulatedDevices();
            EditorApplication.update += UpdateDevices;
            try
            {
                var floor = origin.transform.position.y;
                _headWorld = new Vector3(origin.transform.position.x, floor + HeadHeight, origin.transform.position.z);
                _headRotation = Quaternion.Euler(20f, 30f, 0f);
                yield return 0.5f;

                avatar.SetVisible(true);
                Check(avatar.IsVisible && events == 1 && lastEvent && renderers.All(r => r.enabled), "SetVisible(true) shows it and raises the event");
                yield return 0.3f;
                CheckFollows(avatar, origin, "standing still");
                Check(avatar.IsHeadHidden && avatar.Head.localScale.x < 0.01f,
                    $"own head folded away ({Vector3.Distance(origin.Camera.transform.position, avatar.Head.position):F2} m from the camera)");

                // Walk and turn.
                _headWorld += new Vector3(1f, 0f, 0.5f);
                _headRotation = Quaternion.Euler(-10f, 120f, 0f);
                yield return 0.3f;
                CheckFollows(avatar, origin, "after walking 1.1 m and turning");
                Check(avatar.IsHeadHidden, "own head still folded away");

                // Follow Head off (as in the patient view): the avatar stays, the far camera sees its head.
                avatar.FollowHead = false;
                var stayed = avatar.transform.position;
                _headWorld += new Vector3(-2f, 0f, 0f);
                _headRotation = Quaternion.LookRotation(avatar.transform.position + Vector3.up * 1.0f - _headWorld);
                yield return 0.3f;
                Screenshot("avatar-from-outside");
                Check(Vector3.Distance(avatar.transform.position, stayed) < 0.001f, "with Follow Head off the avatar stays where it was");
                Check(!avatar.IsHeadHidden && avatar.Head.localScale.x > 0.5f,
                    $"its head is visible from {Vector3.Distance(origin.Camera.transform.position, avatar.Head.position):F2} m");
                var headHeight = avatar.Head.position.y - floor;
                Check(headHeight > 1.2f && headHeight < 1.9f, $"the head is at a standing height ({headHeight:F2} m)");

                avatar.SetVisible(false);
                Check(!avatar.IsVisible && events == 2 && !lastEvent && renderers.All(r => !r.enabled), "SetVisible(false) hides it again");
            }
            finally
            {
                avatar.VisibilityChanged -= OnVisibility;
                EditorApplication.update -= UpdateDevices;
                RemoveSimulatedDevices();
            }
        }

        private static void CheckFollows(PlayerAvatar avatar, XROrigin origin, string when)
        {
            var camera = origin.Camera.transform;
            var flat = Flat(camera.forward).normalized;
            var expected = camera.position - flat * 0.1f;
            var position = avatar.transform.position;
            var offset = Flat(position - expected).magnitude;
            Check(offset < 0.02f, $"{when}: the body stands under the head ({offset:F3} m off)");
            Check(Mathf.Abs(position.y - origin.transform.position.y) < 0.001f, $"{when}: feet on the floor (y {position.y:F2})");
            Check(Vector3.Angle(avatar.transform.up, Vector3.up) < 0.1f, $"{when}: upright");
            Check(Vector3.Angle(Flat(avatar.transform.forward), flat) < 1f, $"{when}: turned with the head's yaw");
        }
    }
}
