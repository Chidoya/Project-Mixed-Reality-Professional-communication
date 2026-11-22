using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// The granddaughter's things in Scenario 1 (a birthday card, a photo, a balloon, the clock) and how they react in the
    /// patient view. Tune it in Assets/Hospital/Data/PatientPov/ScenarioOneStoryProps.asset. Positions are offsets from
    /// the furniture the builder finds (nightstand, window), so the same values work in both rooms.
    /// </summary>
    [CreateAssetMenu(menuName = "ProCommVR/Patient POV/Scenario 1 Story Props", fileName = "ScenarioOneStoryProps")]
    public class ScenarioOneStoryProps : ScriptableObject
    {
        [Header("Card (on the nightstand)")]
        public string CardFront = "HAPPY\nBIRTHDAY";
        public string CardInside = "Grandpa,\ncome to my party!\nSaturday 3 pm\nLove, Emma";
        [Tooltip("Card position on the nightstand top, toward the bed (meters, in the nightstand's frame: x along the bed side, z forward).")]
        public Vector2 CardOffset = new Vector2(-0.1f, 0f);
        [Tooltip("Anger: at full strength the card is crumpled to this scale.")]
        public Vector3 CrumpledScale = new Vector3(0.7f, 0.45f, 0.85f);
        [Range(0f, 60f)] public float CrumpleTiltDegrees = 25f;

        [Header("Photo (next to the card)")]
        public Vector2 PhotoOffset = new Vector2(0.15f, -0.03f);
        [Tooltip("Joy: the photo gets brighter by this factor.")]
        [Range(1f, 1.6f)] public float JoyBrightness = 1.25f;
        [Tooltip("Sadness value (not strength) above which the photo tips over, face down.")]
        [Range(0f, 1f)] public float TipOverAt = 0.8f;
        [Min(0.05f)] public float TipSeconds = 0.8f;

        [Header("Balloon")]
        [Tooltip("Joy: how far the balloon rises at full strength (meters).")]
        public float RiseHeight = 0.5f;
        [Tooltip("Joy: sway (meters, at 0.5 Hz).")]
        public float Sway = 0.06f;
        [Tooltip("Sadness: the balloon deflates to this scale and drifts away.")]
        [Range(0.1f, 1f)] public float DeflatedScale = 0.35f;
        [Tooltip("Sadness: how far it drifts (meters) and sinks.")]
        public float DriftDistance = 1.6f;
        public float SinkDistance = 0.6f;

        [Header("Clock (patient view only)")]
        [Tooltip("The time the clock shows when the player enters the patient view (the party is at 3 pm).")]
        [Range(0, 23)] public int StartHour = 14;
        [Range(0, 59)] public int StartMinute = 40;
        [Tooltip("Sadness: how fast time goes (the second hand crawls).")]
        [Range(0.05f, 1f)] public float SadTimeScale = 0.2f;
        [Tooltip("Fear and Anger: how fast time goes (the clock races).")]
        [Range(1f, 5f)] public float FastTimeScale = 2.5f;
        [Range(0f, 1f)] public float TickVolume = 0.12f;
        [Range(0f, 1f)] public float LoudTickVolume = 0.55f;

        [Header("Blend")]
        [Min(0.05f)] public float BlendSeconds = 2f;
    }
}
