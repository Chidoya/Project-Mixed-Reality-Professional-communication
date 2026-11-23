using System;
using System.Collections.Generic;
using System.Linq;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>How the conversation went, for the closing card (phase 6). Only from existing results.</summary>
    public enum DebriefOutcome { Good, Mixed, Critical }

    /// <summary>One rubric item as the doctor talks about it.</summary>
    [Serializable]
    public class DebriefItem
    {
        public int Id;
        public string Criterion;
        public int Score;
        public int Max = 2;
        [Tooltip("English feedback for this score (feedback_en); the Dutch text only when the server has no English yet.")]
        public string Feedback;
        public bool FeedbackIsEnglish;
        public string BetterPhrase;
        public string EvidenceQuote;
        public int EvidenceTurn;
        public bool Uncertain;
    }

    /// <summary>The critical error that ended the conversation (only when there was one).</summary>
    [Serializable]
    public class DebriefCritical
    {
        public string Label;
        public int Turn;
        public string Quote;
        public string Title;
        public string Feedback;
    }

    /// <summary>A moment to talk about and to replay: a key moment of the server, the critical one first.</summary>
    [Serializable]
    public class DebriefMoment
    {
        public int TurnIndex;
        public bool IsCritical;
        public string WhatHappened;
        public string Effect;
        public int AngerAfter;
        [NonSerialized] public RecordedTurn Turn;
        public bool HasTurn => Turn != null;
    }

    /// <summary>One object of the emotion timeline on the table.</summary>
    [Serializable]
    public class TimelinePoint
    {
        public int TurnIndex;
        public bool HasEmotion;
        public Emotion Emotion;
        public float Trust;
        public bool IsKey;
        public bool IsCritical;
        public string StudentText;
        [NonSerialized] public RecordedTurn Turn;

        public Emotion? Dominant => HasEmotion ? Emotion : null;
    }

    /// <summary>
    /// Everything the doctor-led debrief shows, built from the existing results (<see cref="AiDebrief"/>) and the turns the
    /// <see cref="ConversationRecorder"/> kept. Changes no score: it only reads them.
    /// </summary>
    [Serializable]
    public class DebriefData
    {
        public string ScenarioId = "";
        public string EndReason = "";
        public List<DebriefItem> Items = new();
        public List<DebriefMoment> Moments = new();
        public List<TimelinePoint> Timeline = new();
        public bool HasFinalEmotion;
        public Emotion FinalEmotion;
        public int TotalScore;
        public int MaxScore;
        public DebriefCritical Critical;

        public bool HasCritical => Critical != null && !string.IsNullOrEmpty(Critical.Label);
        public Emotion? Final => HasFinalEmotion ? FinalEmotion : null;

        /// <summary>Good (no critical error, at least <paramref name="goodFraction"/> of the max), Critical, or Mixed.</summary>
        public DebriefOutcome Outcome(float goodFraction)
        {
            if (HasCritical)
                return DebriefOutcome.Critical;
            return MaxScore > 0 && TotalScore >= goodFraction * MaxScore ? DebriefOutcome.Good : DebriefOutcome.Mixed;
        }

        /// <summary>
        /// The item with the lowest score that is not the critical error itself (that one is talked about first anyway);
        /// null when every item has the max score.
        /// </summary>
        public DebriefItem Lowest()
        {
            DebriefItem lowest = null;
            foreach (var item in Items)
            {
                if (item.Score >= item.Max)
                    continue;
                if (HasCritical && item.EvidenceTurn == Critical.Turn && item.Score == 0)
                    continue;
                if (lowest == null || item.Score < lowest.Score)
                    lowest = item;
            }
            return lowest;
        }

        /// <summary>
        /// The item to praise: the highest score, preferably one that was really done (with a quote), not only "never did
        /// the wrong thing". Null when nothing scored.
        /// </summary>
        public DebriefItem Highest(DebriefItem except = null)
        {
            DebriefItem best = null;
            foreach (var item in Items)
            {
                if (item == except || item.Score <= 0)
                    continue;
                if (best == null || Rank(item) > Rank(best))
                    best = item;
            }
            return best;

            static int Rank(DebriefItem i) => i.Score * 2 + (string.IsNullOrEmpty(i.EvidenceQuote) ? 0 : 1);
        }

        /// <summary>The moment to replay automatically: the critical one, otherwise the key moment with the lowest trust.</summary>
        public DebriefMoment MostImportant()
        {
            var critical = Moments.FirstOrDefault(m => m.IsCritical);
            if (critical != null)
                return critical;
            DebriefMoment lowest = null;
            foreach (var moment in Moments)
            {
                var trust = TrustAt(moment.TurnIndex);
                if (lowest == null || trust < TrustAt(lowest.TurnIndex))
                    lowest = moment;
            }
            return lowest;
        }

        public float TrustAt(int turnIndex)
        {
            var point = Timeline.FirstOrDefault(p => p.TurnIndex == turnIndex);
            return point != null ? point.Trust : 100f;
        }

        public TimelinePoint PointAt(int turnIndex) => Timeline.FirstOrDefault(p => p.TurnIndex == turnIndex);

        /// <summary>
        /// Builds the debrief data. Key moments are mapped to the recorded turns by turn_index; a critical error is the
        /// first moment (also when it is not one of the key moments); a moment without a recorded turn keeps its text but
        /// has no audio (a warning is logged).
        /// </summary>
        public static DebriefData Build(AiDebrief debrief, IReadOnlyList<RecordedTurn> turns, Emotion? finalEmotion,
            string scenarioId = "")
        {
            turns ??= Array.Empty<RecordedTurn>();
            var data = new DebriefData
            {
                ScenarioId = scenarioId ?? "",
                EndReason = debrief?.end_reason ?? "",
                TotalScore = debrief?.total_score ?? 0,
                MaxScore = debrief?.max_score ?? 0,
                HasFinalEmotion = finalEmotion.HasValue,
                FinalEmotion = finalEmotion ?? default,
            };
            if (debrief == null)
                return data;

            foreach (var item in debrief.items ?? Array.Empty<AiDebriefItem>())
            {
                var english = !string.IsNullOrEmpty(item.feedback_en);
                data.Items.Add(new DebriefItem
                {
                    Id = item.id,
                    Criterion = item.criterion,
                    Score = item.score,
                    Feedback = english ? item.feedback_en : item.feedback,
                    FeedbackIsEnglish = english,
                    BetterPhrase = item.better_phrase,
                    EvidenceQuote = item.evidence_quote,
                    EvidenceTurn = item.evidence_turn,
                    Uncertain = item.uncertain,
                });
            }

            if (debrief.critical != null && debrief.critical.Present)
            {
                var c = debrief.critical;
                data.Critical = new DebriefCritical
                {
                    Label = c.label,
                    Turn = c.turn,
                    Quote = c.quote,
                    Title = string.IsNullOrEmpty(c.title_en) ? c.title : c.title_en,
                    Feedback = string.IsNullOrEmpty(c.feedback_en) ? c.feedback : c.feedback_en,
                };
                data.Moments.Add(new DebriefMoment
                {
                    TurnIndex = c.turn,
                    IsCritical = true,
                    WhatHappened = data.Critical.Title,
                    Turn = Find(turns, c.turn, true),
                });
            }

            foreach (var key in debrief.key_moments ?? Array.Empty<AiKeyMoment>())
            {
                if (data.HasCritical && key.turn_index == data.Critical.Turn)
                {
                    var critical = data.Moments[0];
                    critical.Effect = key.effect;
                    critical.AngerAfter = key.anger_after;
                    continue;
                }
                data.Moments.Add(new DebriefMoment
                {
                    TurnIndex = key.turn_index,
                    WhatHappened = key.what_happened,
                    Effect = key.effect,
                    AngerAfter = key.anger_after,
                    Turn = Find(turns, key.turn_index, true),
                });
            }

            // The timeline: one point per recorded turn (the recorder keeps at most 8); without recordings the trust curve.
            var keyTurns = new HashSet<int>(data.Moments.Select(m => m.TurnIndex));
            if (turns.Count > 0)
            {
                foreach (var turn in turns.OrderBy(t => t.Index))
                {
                    var dominant = turn.Dominant;
                    data.Timeline.Add(new TimelinePoint
                    {
                        TurnIndex = turn.Index,
                        HasEmotion = dominant.HasValue,
                        Emotion = dominant ?? default,
                        Trust = turn.Trust,
                        IsKey = keyTurns.Contains(turn.Index),
                        IsCritical = data.HasCritical && data.Critical.Turn == turn.Index,
                        StudentText = turn.StudentText,
                        Turn = turn,
                    });
                }
            }
            else
            {
                var curve = debrief.trust_curve ?? Array.Empty<int>();
                for (var i = Mathf.Max(1, curve.Length - 8); i < curve.Length; i++)
                    data.Timeline.Add(new TimelinePoint
                    {
                        TurnIndex = i,
                        Trust = curve[i],
                        IsKey = keyTurns.Contains(i),
                        IsCritical = data.HasCritical && data.Critical.Turn == i,
                        StudentText = "",
                    });
            }
            return data;
        }

        private static RecordedTurn Find(IReadOnlyList<RecordedTurn> turns, int index, bool warn)
        {
            foreach (var turn in turns)
                if (turn.Index == index)
                    return turn;
            if (warn && turns.Count > 0)
                Debug.LogWarning($"[Debrief] No recorded turn {index} (of {turns.Count}): this moment has no audio.");
            return null;
        }
    }

    /// <summary>Where the doctor-led debrief gets its data from (the AI conversation now, the button version later).</summary>
    public interface IDebriefSource
    {
        DebriefData Build();
    }

    /// <summary>The debrief of the AI conversation: the server's AiDebrief plus the recorded turns.</summary>
    public class AiDebriefSource : IDebriefSource
    {
        private readonly AiDebrief _debrief;
        private readonly ConversationRecorder _recorder;
        private readonly EmotionHolder _holder;

        public AiDebriefSource(AiDebrief debrief, ConversationRecorder recorder, EmotionHolder holder)
        {
            _debrief = debrief;
            _recorder = recorder;
            _holder = holder;
        }

        public DebriefData Build()
        {
            var turns = _recorder != null ? _recorder.Turns : null;
            var final = FeltEmotion.FeltMost(_debrief, turns, _holder);
            var scenario = _recorder != null && _recorder.Npc != null ? _recorder.Npc.ScenarioId : "";
            return DebriefData.Build(_debrief, turns, final, scenario);
        }
    }

    public static class FeltEmotion
    {
        /// <summary>
        /// v2: what the patient felt MOST over the whole conversation, for the question "what did he feel?". The AI
        /// server counts every reply (felt_emotion); without it the recorded turns are counted (a tie: the later one);
        /// only without both the patient's emotion at the end. Before, only the end counted: one worried or surprised
        /// last line made a man who was sad the whole time "afraid".
        /// </summary>
        public static Emotion? FeltMost(AiDebrief debrief, IReadOnlyList<RecordedTurn> turns, EmotionHolder holder)
        {
            var fromServer = debrief?.felt_emotion?.emotion;
            if (!string.IsNullOrEmpty(fromServer) && Enum.TryParse<Emotion>(fromServer, out var parsed))
                return parsed;
            if (turns != null && turns.Count > 0)
            {
                var counts = new Dictionary<Emotion, int>();
                var last = new Dictionary<Emotion, int>();
                for (var i = 0; i < turns.Count; i++)
                {
                    var dominant = turns[i].Dominant;
                    if (!dominant.HasValue)
                        continue;
                    counts.TryGetValue(dominant.Value, out var n);
                    counts[dominant.Value] = n + 1;
                    last[dominant.Value] = i;
                }
                if (counts.Count > 0)
                    return counts.Keys.OrderByDescending(e => counts[e]).ThenByDescending(e => last[e]).First();
            }
            return holder != null ? holder.Target.Dominant : null;
        }
    }

    /// <summary>A fixed DebriefData, for tests and test scenes (no AI server needed).</summary>
    public class FixedDebriefSource : IDebriefSource
    {
        private readonly DebriefData _data;
        public FixedDebriefSource(DebriefData data) => _data = data;
        public DebriefData Build() => _data;
    }
}
