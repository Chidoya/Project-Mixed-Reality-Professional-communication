using System.Collections;
using System.Linq;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProCommVR.Editor.Testing
{
    /// <summary>
    /// The emotion HUD in Scenario01_PatientRoom: off in the nurse view; after Y (simulated left controller) it fades in
    /// about 1.6 m away, 20 to 25 degrees to the left, facing the camera; its numbers and bars match the EmotionHolder
    /// and the dominant row is bold and bright; it follows changes (as with the inspector sliders); during a fast head
    /// turn it lags but never comes closer than its minimum angle to the center of the view; the text is at least
    /// about 0.8 degrees tall; after Y again it is off. Batchmode, without -quit:
    ///   -executeMethod ProCommVR.Editor.Testing.PlayModeSmokeTest.RunEmotionHud
    /// </summary>
    public static partial class PlayModeSmokeTest
    {
        [MenuItem("ProCommVR/Tests/Emotion HUD (Scenario 01)")]
        public static void RunEmotionHud()
        {
            Launch("hud", Scenario01ScenePath);
        }

        private static IEnumerator EmotionHudTest()
        {
            _screenshotFolder = ReadArgument("-smokeTestScreenshots");
            var hud = Object.FindAnyObjectByType<EmotionHud>();
            var holder = Object.FindAnyObjectByType<EmotionHolder>();
            var pov = Object.FindAnyObjectByType<PovController>();
            var presenter = Object.FindAnyObjectByType<ScenarioPresenter>();
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Check(hud != null && holder != null && pov != null && presenter != null && origin != null,
                "scene has an EmotionHud, EmotionHolder, PovController, presenter and XR Origin");
            if (Errors.Count > 0)
                yield break;
            Check(Object.FindObjectsByType<EmotionHud>(FindObjectsSortMode.None).Length == 1, "exactly one emotion HUD");
            Check(!hud.IsShown && !hud.IsCanvasOn, "the HUD is off in the nurse view");
            var graphics = hud.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            var onTop = graphics.All(g => g.materialForRendering != null &&
                                          (g.materialForRendering.shader.name == EmotionHudSetup.ImageShader ||
                                           g.materialForRendering.shader.name == EmotionHudSetup.TextShader));
            Check(onTop, $"all {graphics.Length} HUD graphics are drawn on top of the room (ZTest Always)");

            AddSimulatedDevices();
            AddLeftController();
            EditorApplication.update += KeepHeadLocal;
            EditorApplication.update += UpdateDevices;
            EditorApplication.update += UpdateLeftController;
            try
            {
                var patient = new SerializedObject(presenter).FindProperty("_npc").objectReferenceValue as Transform;
                var toPlayer = Flat(origin.transform.position - patient.position).normalized;
                Teleport(origin, patient.position + toPlayer * 1.0f, patient.position);
                _lastSpacePosition = null;
                yield return 0.5f;

                holder.SetImmediate(new EmotionState(0.1f, 0.6f, 0.2f, 0.3f));
                Check(!hud.IsCanvasOn, "changing the emotions in the nurse view does not show the HUD");

                foreach (var step in PressY(() => pov.Mode == PovMode.Patient && !pov.IsSwitching))
                    yield return step;
                Check(pov.Mode == PovMode.Patient, "Y switches to the patient view");
                yield return 0.5f;
                Check(hud.IsShown && hud.IsCanvasOn && hud.Alpha > 0.99f, $"the HUD is shown in the patient view (alpha {hud.Alpha:F2})");
                Check(hud.HintText == PovSetup.HintText, $"the HUD shows the return hint (\"{hud.HintText}\")");

                var camera = origin.Camera.transform;
                // Look straight ahead (the test aimed the head up at the patient before).
                _headRotation = Quaternion.LookRotation(Flat(camera.forward));
                yield return 1f;
                CheckHudPlace(hud, camera, "after the switch", 1.5f);
                CheckHudValues(hud, holder, Emotion.Anger, "start");
                CheckTextSize(hud, camera);
                Screenshot("hud-patient-view");
                var ahead = _headRotation;
                _headRotation = Quaternion.AngleAxis(-15f, Vector3.up) * ahead;
                yield return 1.2f;
                Screenshot("hud-closer-look");
                _headRotation = ahead;
                yield return 1.2f;

                // As with the inspector sliders: the bars follow the blend and the dominant row moves.
                holder.Apply(new EmotionState(0f, -0.5f, 0f, 0.5f));
                yield return holder.BlendSeconds * 0.4f;
                var number = hud.GetRow(Emotion.Sadness).Number.text;
                Check(number != "30" && number != "80", $"the Sadness bar moves along while blending ({number})");
                yield return holder.BlendSeconds + 0.3f;
                CheckHudValues(hud, holder, Emotion.Sadness, "after the change");

                // A fast head turn to the left: the panel lags, but never into the center of the view.
                var minAngle = float.MaxValue;
                var maxAngle = float.MinValue;
                _headRotation = Quaternion.AngleAxis(-60f, Vector3.up) * _headRotation;
                var until = EditorApplication.timeSinceStartup + 1.5f;
                while (EditorApplication.timeSinceStartup < until)
                {
                    var fromCenter = -Vector3.SignedAngle(Flat(camera.forward), Flat(hud.transform.position - camera.position), Vector3.up);
                    minAngle = Mathf.Min(minAngle, fromCenter);
                    maxAngle = Mathf.Max(maxAngle, fromCenter);
                    yield return null;
                }
                Check(minAngle >= hud.MinAngleFromCenter - 0.5f,
                    $"during a fast turn the HUD stays at least {hud.MinAngleFromCenter} deg left of the center (min {minAngle:F1}, max {maxAngle:F1})");
                yield return 1f;
                CheckHudPlace(hud, camera, "after the turn", 1.5f);

                foreach (var step in WaitUntilAllowed(pov))
                    yield return step;
                foreach (var step in PressY(() => pov.Mode == PovMode.Nurse && !pov.IsSwitching))
                    yield return step;
                yield return 0.5f;
                Check(pov.Mode == PovMode.Nurse && !hud.IsShown && !hud.IsCanvasOn, "Y back: the HUD is off in the nurse view");
            }
            finally
            {
                EditorApplication.update -= KeepHeadLocal;
                EditorApplication.update -= UpdateDevices;
                EditorApplication.update -= UpdateLeftController;
                if (_leftController != null && _leftController.added)
                    UnityEngine.InputSystem.InputSystem.RemoveDevice(_leftController);
                _leftController = null;
                _lastSpacePosition = null;
                RemoveSimulatedDevices();
            }
        }

        private static void CheckHudPlace(EmotionHud hud, Transform camera, string when, float tolerance)
        {
            var toHud = hud.transform.position - camera.position;
            var fromCenter = -Vector3.SignedAngle(Flat(camera.forward), Flat(toHud), Vector3.up);
            Check(Mathf.Abs(toHud.magnitude - hud.Distance) < 0.05f, $"{when}: the HUD is {toHud.magnitude:F2} m away");
            Check(fromCenter >= 20f - tolerance && fromCenter <= 25f + tolerance, $"{when}: {fromCenter:F1} deg to the left of the center");
            Check(Vector3.Angle(hud.transform.forward, Flat(toHud)) < 1f, $"{when}: facing the camera");
        }

        private static void CheckHudValues(EmotionHud hud, EmotionHolder holder, Emotion dominant, string when)
        {
            Check(hud.Dominant == dominant && holder.Dominant == dominant, $"{when}: dominant is {dominant} (HUD {hud.Dominant}, holder {holder.Dominant})");
            foreach (Emotion emotion in System.Enum.GetValues(typeof(Emotion)))
            {
                var row = hud.GetRow(emotion);
                var value = holder.Current.Get(emotion);
                var expected = Mathf.RoundToInt(value * 100f).ToString();
                var width = row.Fill.sizeDelta.x;
                var isDominant = emotion == dominant;
                Check(row.Number.text == expected && Mathf.Abs(width - value * hud.BarWidth) < 0.5f,
                    $"{when}: {emotion} shows {row.Number.text} (expected {expected}), bar {width:F0} of {hud.BarWidth:F0}");
                Check((row.Label.fontStyle == FontStyles.Bold) == isDominant && (row.FillImage.color.a > 0.99f) == isDominant,
                    $"{when}: {emotion} is {(isDominant ? "highlighted" : "dimmed")}");
            }
        }

        /// <summary>
        /// The capital height of the row text and of the smallest text, in mm and in degrees of view (from the font's
        /// cap line, so without line spacing).
        /// </summary>
        private static void CheckTextSize(EmotionHud hud, Transform camera)
        {
            var row = hud.GetRow(Emotion.Joy).Number;
            var smallest = hud.GetComponentsInChildren<TMP_Text>().OrderBy(t => t.fontSize).First();
            var distance = Vector3.Distance(camera.position, row.transform.position);
            float CapHeight(TMP_Text text) => text.font.faceInfo.capLine / text.font.faceInfo.pointSize * text.fontSize * text.rectTransform.lossyScale.y;
            float Degrees(float height) => 2f * Mathf.Atan(height * 0.5f / distance) * Mathf.Rad2Deg;
            var rowHeight = CapHeight(row);
            var smallHeight = CapHeight(smallest);
            Check(Degrees(rowHeight) >= 0.8f,
                $"row text: capitals {rowHeight * 1000f:F0} mm at {distance:F2} m = {Degrees(rowHeight):F2} deg");
            Check(Degrees(smallHeight) >= 0.55f,
                $"smallest text ({smallest.name}): capitals {smallHeight * 1000f:F0} mm = {Degrees(smallHeight):F2} deg");
        }
    }
}
