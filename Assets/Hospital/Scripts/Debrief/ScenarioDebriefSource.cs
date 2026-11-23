using System.Linq;
using ProCommVR.Emotions;
using ProCommVR.Scenarios;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// Phase 7 (optional, not wired into a scene yet): the doctor-led debrief for Jalil's button version
    /// (Scenario01_PatientRoom, the shift). Built from the existing results only (<see cref="SessionScore"/>): per answered
    /// scenario one moment, the student's words = the text of the chosen option (no recording: the memory shows it as
    /// text from the white figure), the patient's words = its NpcReaction (with NpcVoice if set), his emotion = Initial
    /// Emotion + the option's EmotionDelta. One item per scenario with the option's Feedback; the better phrase is the text
    /// of the option with the highest score. Changes no score.
    /// </summary>
    public class ScenarioDebriefSource : IDebriefSource
    {
        private readonly SessionScore _score;

        public ScenarioDebriefSource(SessionScore score) => _score = score;

        public DebriefData Build()
        {
            var data = new DebriefData();
            if (_score == null)
                return data;
            data.TotalScore = _score.Total;
            data.MaxScore = _score.BestTotal;
            var index = 0;
            EmotionState last = default;
            foreach (var result in _score.Results)
            {
                index++;
                var scenario = result.Scenario;
                var chosen = result.Chosen;
                var after = scenario.InitialEmotion.Add(chosen.EmotionDelta).Clamped();
                last = after;
                var best = scenario.Options.OrderByDescending(o => o.Score).FirstOrDefault();
                var turn = new RecordedTurn
                {
                    Index = index,
                    StudentText = chosen.Text,
                    PatientText = chosen.NpcReaction,
                    PatientClip = chosen.NpcVoice, // an asset: never released by the debrief
                    Emotion = after,
                    Trust = scenario.BestScore > 0 ? UnityEngine.Mathf.Clamp01((chosen.Score + 2f) / (scenario.BestScore + 2f)) * 100f : 50f,
                };
                data.Items.Add(new DebriefItem
                {
                    Id = index,
                    Criterion = scenario.Title,
                    Score = chosen.Score,
                    Max = scenario.BestScore,
                    Feedback = chosen.Feedback,
                    FeedbackIsEnglish = true,
                    BetterPhrase = best != null && best != chosen ? best.Text : "",
                    EvidenceQuote = chosen.Text,
                    EvidenceTurn = index,
                });
                data.Moments.Add(new DebriefMoment { TurnIndex = index, WhatHappened = scenario.Title, Turn = turn });
                data.Timeline.Add(new TimelinePoint
                {
                    TurnIndex = index,
                    HasEmotion = after.Dominant.HasValue,
                    Emotion = after.Dominant ?? default,
                    Trust = turn.Trust,
                    IsKey = true,
                    StudentText = chosen.Text,
                    Turn = turn,
                });
            }
            data.HasFinalEmotion = last.Dominant.HasValue;
            data.FinalEmotion = last.Dominant ?? default;
            return data;
        }
    }
}
