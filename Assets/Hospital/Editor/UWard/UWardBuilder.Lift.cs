using System.Linq;
using ProCommVR.Hospital;
using UnityEngine;
using static ProCommVR.Editor.UWardLayout;

namespace ProCommVR.Editor
{
    /// <summary>
    /// Fase 4: twee liften in de hal en het trappenhuis, zichtbaar maar niet toegankelijk. De liftdeuren en de deur van het
    /// trappenhuis blijven dicht, hebben een collider en een LockedDoor met de reden. Achter de liftdeuren zit een donkere schacht.
    /// Lobby/Lift heeft een oproeppaneel met LiftButton_Up en LiftButton_Down (de U-afdeling is één verdieping).
    /// </summary>
    public static partial class UWardBuilder
    {
        private const float LiftFrameWidth = 0.1f;
        private const float LiftFrameDepth = 0.08f;
        private const float LiftDoorThickness = 0.05f;
        private const float LiftPanelHeight = 1.2f;
        private const float LiftButtonSize = 0.08f;
        private const string LiftClosedReason = "The lifts are not part of this training.";
        private const string StairsClosedReason = "The stairs are closed during this training.";

        // Trap: twee steile vluchten met een bordes, tegen de noordmuur van het trappenhuis
        private const float StepRise = 0.17f;
        private const float StepRun = 0.28f;
        private const float FlightWidth = 1.3f;
        private const float FlightGap = 0.1f;

        private static void BuildLifts(Transform lobby)
        {
            var lift = Child(lobby, "Lift");
            var frame = Mat("LiftFrame", new Color(0.35f, 0.36f, 0.38f), 0.6f, 0.5f);
            var door = Mat("LiftDoor", new Color(0.78f, 0.80f, 0.82f), 0.8f, 0.6f);
            var shaft = Mat("LiftShaft", new Color(0.22f, 0.23f, 0.25f));
            var hallFace = LiftWallY + LiftWallThickness / 2f;

            for (var i = 0; i < Lifts.Length; i++)
            {
                var car = Child(lift, $"Lift_{i + 1}");
                var r = Lifts[i];
                var half = LiftOpeningWidth / 2f;
                Box("Shaft", car, ToUnity(r.CenterX, (r.Y0 + r.Y1) / 2f - 0.005f, CeilingHeight / 2f),
                    new Vector3(r.X1 - r.X0, CeilingHeight, r.Y1 - r.Y0 - 0.01f), shaft);

                var y = hallFace + LiftFrameDepth / 2f;
                Box("Frame_Left", car, ToUnity(r.CenterX - half - LiftFrameWidth / 2f, y, LiftOpeningHeight / 2f),
                    new Vector3(LiftFrameWidth, LiftOpeningHeight, LiftFrameDepth), frame);
                Box("Frame_Right", car, ToUnity(r.CenterX + half + LiftFrameWidth / 2f, y, LiftOpeningHeight / 2f),
                    new Vector3(LiftFrameWidth, LiftOpeningHeight, LiftFrameDepth), frame);
                Box("Frame_Top", car, ToUnity(r.CenterX, y, LiftOpeningHeight + LiftFrameWidth / 2f),
                    new Vector3(LiftOpeningWidth + 2f * LiftFrameWidth, LiftFrameWidth, LiftFrameDepth), frame);

                Box("LiftDoor_Left", car, ToUnity(r.CenterX - half / 2f, LiftWallY, LiftOpeningHeight / 2f),
                    new Vector3(half, LiftOpeningHeight, LiftDoorThickness), door);
                Box("LiftDoor_Right", car, ToUnity(r.CenterX + half / 2f, LiftWallY, LiftOpeningHeight / 2f),
                    new Vector3(half, LiftOpeningHeight, LiftDoorThickness), door);
                car.gameObject.AddComponent<LockedDoor>().SetReason(LiftClosedReason);
            }

            // Oproeppaneel tussen de twee liften
            var panelX = (Lifts[0].X1 + Lifts[1].X0) / 2f;
            var panelY = hallFace + 0.02f;
            Box("Panel", lift, ToUnity(panelX, panelY, LiftPanelHeight), new Vector3(0.14f, 0.3f, 0.04f), frame);
            var buttonMat = Mat("LiftButton", new Color(0.9f, 0.9f, 0.88f));
            foreach (var (label, dy) in new[] { ("Up", 0.06f), ("Down", -0.06f) })
            {
                var button = Box($"LiftButton_{label}", lift, ToUnity(panelX, panelY + 0.025f, LiftPanelHeight + dy),
                    new Vector3(LiftButtonSize, LiftButtonSize, 0.015f), buttonMat);
                Label($"Text_{label}", lift, label, button.transform.localPosition + Vector3.back * 0.009f, Vector3.back,
                    LiftButtonSize, LiftButtonSize * 0.5f, new Color(0.15f, 0.15f, 0.15f));
            }
        }

        /// <summary>Dichte deur: een collider over de opening en een LockedDoor met de reden voor de speler.</summary>
        private static void Lock(Transform door, float width, string reason)
        {
            var blocker = door.gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, DoorHeight / 2f, 0f);
            blocker.size = new Vector3(width, DoorHeight, 0.2f);
            door.gameObject.AddComponent<LockedDoor>().SetReason(reason);
        }

        /// <summary>
        /// Trappenhuis (de deur, op slot, staat in BuildCore): binnen een trap (vlucht naar het westen, bordes, vlucht terug
        /// naar het oosten) met een gedimde lamp, zodat het door het raampje van de deur zichtbaar is.
        /// </summary>
        private static void BuildStairwell(Transform core)
        {
            var room = Child(core, "Stairwell");
            var stairs = Child(room, "Stairs");
            var r = Inner(CoreRoom("Stairwell"));
            var stair = Mat("Stair", new Color(0.62f, 0.64f, 0.66f));
            const int steps1 = 9, steps2 = 8;
            var landingX = r.X0 + 1f;
            var landingHeight = steps1 * StepRise;
            var lane1 = (r.Y0 + r.Y0 + FlightWidth) / 2f;
            var lane2 = r.Y0 + FlightWidth + FlightGap + FlightWidth / 2f;

            // Vlucht 1: van oost naar west omhoog tot het bordes
            for (var i = 0; i < steps1; i++)
            {
                var h = (i + 1) * StepRise;
                var x = landingX + (steps1 - i - 0.5f) * StepRun;
                Box($"Step1_{i + 1}", stairs, ToUnity(x, lane1, h / 2f), new Vector3(StepRun, h, FlightWidth), stair);
            }
            var landingDepth = 2f * FlightWidth + FlightGap;
            Box("Landing", stairs, ToUnity((r.X0 + landingX) / 2f, r.Y0 + landingDepth / 2f, landingHeight / 2f),
                new Vector3(landingX - r.X0, landingHeight, landingDepth), stair);
            // Vlucht 2: van het bordes terug naar het oosten, tot vlak onder het plafond
            for (var i = 0; i < steps2; i++)
            {
                var h = landingHeight + (i + 1) * StepRise;
                var x = landingX + (i + 0.5f) * StepRun;
                Box($"Step2_{i + 1}", stairs, ToUnity(x, lane2, h / 2f), new Vector3(StepRun, h, FlightWidth), stair);
            }
            // Leuning op de middenwand tussen de vluchten
            var railLength = steps1 * StepRun;
            Box("Wall_Middle", stairs, ToUnity(landingX + railLength / 2f, r.Y0 + FlightWidth + FlightGap / 2f, (landingHeight + 0.9f) / 2f),
                new Vector3(railLength, landingHeight + 0.9f, FlightGap), stair);

            var center = PlanPoint(new Vector2(r.CenterX, r.CenterY), CeilingHeight);
            AddCeilingLamp(room, "Lamp", center);
            AddLight("Light", room, center + Vector3.down * 0.4f, CoreLightRange, 0.8f);
        }

        private static void TakeLiftScreenshots()
        {
            Shot("u_afdeling_lifts.png", PlanPoint(new Vector2(21f, 6.8f), EyeHeight), PlanPoint(new Vector2(21f, 3f), 1.2f));
            Shot("u_afdeling_stairwell.png", PlanPoint(new Vector2(16.4f, 6.5f), EyeHeight), PlanPoint(new Vector2(10.5f, 1.5f), 1.0f));
        }
    }
}
